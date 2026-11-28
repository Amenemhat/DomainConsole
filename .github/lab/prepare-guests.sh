#!/usr/bin/env bash
set -euo pipefail
lab="${RUNNER_TEMP:?}/dc-wsus-lab"
mkdir -p "$lab"
if [[ "${GITHUB_ACTIONS:-}" != true || "${RUNNER_OS:-}" != Linux ]]; then
  echo 'This workflow provisions only a disposable Linux Actions runner.' >&2
  exit 1
fi
sudo test -c /dev/kvm
# Stock python3-winrm uses ntlm-auth, which needs an NT hash before it can start a session.
OPENSSL_CONF="$PWD/.github/lab/ntlm-openssl.cnf" /usr/bin/python3 -c 'import hashlib; assert hashlib.new("md4", "password".encode("utf-16-le")).hexdigest()=="8846f7eaee8fb117ad06bdd830b7586c"'

sudo apt-get update -qq
sudo env DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends qemu-system-x86 qemu-utils seabios ovmf libguestfs-tools python3-winrm python3-pil libguestfs-perl ntfs-3g genisoimage
# Reclaim space from unused SDKs on this disposable runner before writing seven overlays.
sudo rm -rf /usr/local/lib/android/sdk /usr/share/dotnet /opt/ghc
df -h "$lab"
curl --fail --location --retry 3 --max-time 1200 'https://go.microsoft.com/fwlink/p/?clcid=0x409&country=us&culture=en-us&linkid=2195166' -o "$lab/windows.vhd"
if python3 .github/lab/driver-files.py "$lab/drivers/NetKVM/2k22/amd64"; then
  genisoimage -quiet -R -J -o "$lab/drivers.iso" "$lab/drivers"
else
  curl --fail --silent --show-error --location --retry 2 --max-time 1200 https://fedorapeople.org/groups/virt/virtio-win/direct-downloads/stable-virtio/virtio-win.iso -o "$lab/drivers.iso"
fi
file "$lab/windows.vhd"
qemu-img info "$lab/windows.vhd"
sha256sum "$lab/windows.vhd" "$lab/drivers.iso"
sudo ip link add dclab0 type bridge
sudo ip address add 10.77.57.1/24 dev dclab0
sudo ip link set dclab0 up
# Allow guest-to-guest traffic on the private switch; exclude every external interface.
sudo iptables -I FORWARD -i dclab0 ! -o dclab0 -j DROP
sudo iptables -I FORWARD -o dclab0 ! -i dclab0 -j DROP
sudo iptables -I FORWARD -i dclab0 -o dclab0 -j ACCEPT
sudo iptables -I INPUT -i dclab0 -s 10.77.57.11/32 -p tcp --dport 8765 -j ACCEPT
if [[ "${DC_TEST_USER_RECOVERY:-}" == "1" ]]; then
  sudo iptables -I INPUT -i dclab0 -s 10.77.57.21/32 -p tcp --dport 8765 -j ACCEPT
fi
sudo chown root:kvm /dev/kvm
sudo chmod 660 /dev/kvm
if [[ "${DC_MEDIA_ONLY:-}" != "1" ]]; then bash .github/lab/boot-guests.sh; fi
