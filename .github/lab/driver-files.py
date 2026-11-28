"""Read the signed Windows Server NetKVM folder from Fedora's official ISO by byte ranges."""
from pathlib import Path
import re
import subprocess
import sys
import tempfile

url = "https://fedorapeople.org/groups/virt/virtio-win/direct-downloads/stable-virtio/virtio-win.iso"
block = 2048
destination = Path(sys.argv[1])
destination.mkdir(parents=True, exist_ok=True)


def get(offset, length):
    if not 0 < length <= 16*1024*1024:
        raise ValueError("Driver ISO extent outside the expected size")
    end = offset + length - 1
    with tempfile.TemporaryDirectory() as td:
        target, headers = Path(td)/"bytes", Path(td)/"headers"
        subprocess.run(["curl", "--silent", "--show-error", "--fail", "--location",
            "--retry", "2", "--max-time", "80", "--range", f"{offset}-{end}",
            "--max-filesize", str(length), "--dump-header", str(headers), "--output", str(target),
            f"{url}?netkvm-range={offset}-{end}"], check=True)
        data = target.read_bytes()
        if len(data) != length or not re.search(rf"(?im)^content-range:\s*bytes {offset}-{end}/\d+\s*$", headers.read_text()):
            raise ValueError("Fedora mirror did not supply the requested ISO extent")
        return data


def u32(data, offset):
    return int.from_bytes(data[offset:offset+4], "little")


def directory(record):
    data = get(u32(record,2)*block, u32(record,10))
    position = 0
    entries = {}
    while position < len(data):
        size = data[position]
        if not size:
            position = (position // block + 1) * block
            continue
        item = data[position:position+size]
        name = item[33:33+item[32]]
        if name not in (b"\0",b"\1"):
            fullname=name.decode("utf-16-be" if joliet else "ascii").split(";")[0]
            if not joliet:
                # Rock Ridge NM fields retain names that exceed the primary ISO 8.3 spelling.
                pos=33+len(name)+(0 if len(name)%2 else 1)
                rrname=[]
                while pos+4<=len(item) and item[pos+2]>=4:
                    length=item[pos+2]
                    if item[pos:pos+2]==b"NM" and length>=5:
                        rrname.append(item[pos+5:pos+length])
                    pos+=length
                if rrname:
                    fullname=b"".join(rrname).decode("utf-8")
            entries[fullname.lower()] = item
        position += size
    return entries


descriptors = get(16*block, 8*block)
joliet = next((v for i in range(0,len(descriptors),block)
    if (v:=descriptors[i:i+block])[:6]==b"\x02CD001" and v[88:91] in (b"%/@",b"%/C",b"%/E")),None)
primary=descriptors[:block]
if primary[:6]!=b"\x01CD001":
    raise ValueError("Invalid primary ISO volume descriptor")
record=(joliet or primary)[156:190]
server=destination.parent.name.lower()
if server not in {"2k19","2k22"}:
    raise ValueError("Expected a Server 2019 or 2022 destination")
for component in ("netkvm",server,"amd64"):
    record = directory(record)[component]
    if not record[25]&2:
        raise ValueError("Driver path is not a directory")
found = set()
for name, record in directory(record).items():
    if record[25]&2:
        continue
    if not re.fullmatch(r"[a-z0-9_.-]+",name) or name.startswith("."):
        raise ValueError("Unexpected driver filename")
    if Path(name).suffix not in {".cat",".sys",".inf",".dll",".exe"}:
        continue
    data = get(u32(record,2)*block,u32(record,10))
    (destination/name).write_bytes(data)
    print(name, len(data), flush=True)
    found.add(name)
if not {"netkvm.inf","netkvm.cat","netkvm.sys"} <= found:
    raise ValueError("Incomplete signed driver package")
