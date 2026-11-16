#!/usr/bin/env bash
set -euo pipefail
lab="${RUNNER_TEMP:?}/dc-wsus-lab"
mkdir -p "$lab"
if [[ "${GITHUB_ACTIONS:-}" != true || "${RUNNER_OS:-}" != Linux ]]; then
  echo 'This workflow provisions only a disposable Linux Actions runner.' >&2
  exit 1
fi
sudo test -c /dev/kvm
sudo apt-get update -qq
sudo env DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends qemu-system-x86 qemu-utils seabios ovmf libguestfs-tools python3-winrm python3-pil libguestfs-perl ntfs-3g
# Reclaim space from unused SDKs on this disposable runner before writing seven overlays.
sudo rm -rf /usr/local/lib/android/sdk /usr/share/dotnet /opt/ghc
df -h "$lab"
curl --fail --location --retry 3 --max-time 1200 'https://go.microsoft.com/fwlink/p/?clcid=0x409&country=us&culture=en-us&linkid=2195166' -o "$lab/windows.vhd"
curl --fail --location --retry 3 --max-time 240 https://fedorapeople.org/groups/virt/virtio-win/direct-downloads/stable-virtio/virtio-win.iso -o "$lab/drivers.iso"
file "$lab/windows.vhd"
qemu-img info "$lab/windows.vhd"
sha256sum "$lab/windows.vhd" "$lab/drivers.iso"
sudo ip link add dclab0 type bridge
sudo ip address add 10.77.57.1/24 dev dclab0
sudo ip link set dclab0 up
# Allow guest-to-guest traffic on the private switch; exclude every external interface.
sudo iptables -I FORWARD -i dclab0 ! -o dclab0 -j DROP
sudo iptables -I FORWARD -o dclab0 ! -i dclab0 -j DROP
sudo chown root:kvm /dev/kvm
sudo chmod 660 /dev/kvm
nohup sudo -E bash -c '
  /usr/bin/python3 -u .github/lab/windows-lab.py "$1"
  echo "$?" > "$1/guest-exit"
' bash "$lab" > "$lab/guest.log" 2>&1 < /dev/null &
