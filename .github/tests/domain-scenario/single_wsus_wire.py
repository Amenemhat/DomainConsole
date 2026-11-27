"""Summarize WSUS HTTP exchanges between two disposable lab guests."""
from contextlib import contextmanager
from datetime import datetime, timezone
import re
import signal
import subprocess
from uuid import UUID
from xml.etree import ElementTree


def summarize(output):
    requests = {}
    evidence = []
    for line in output.splitlines():
        frame, epoch, path, headers, reply_to, status, payload = line.split("\t")
        if path:
            action = re.search(r'SOAPAction:\s*"?[^\s"]*/([A-Za-z][A-Za-z0-9]*)', headers, re.IGNORECASE)
            event = {"at": datetime.fromtimestamp(float(epoch), timezone.utc).isoformat(),
                     "service": path.partition("?")[0], "action": action[1] if action else ""}
            try:
                document = ElementTree.fromstring(bytes.fromhex(payload.replace(":", "")))
                for node in document.iter():
                    tag = node.tag.rsplit("}", 1)[-1].lower()
                    if tag in ("clientid", "computerid") and node.text:
                        try:
                            event.setdefault("client_ids", []).append(str(UUID(node.text.strip())))
                        except ValueError:
                            pass
            except (ValueError, ElementTree.ParseError):
                pass
            requests[frame] = event
            evidence.append(event)
        elif status and reply_to in requests:
            event = requests[reply_to]
            event["status"] = int(status)
            if int(status) >= 400:
                try:
                    document = ElementTree.fromstring(bytes.fromhex(payload.replace(":", "")))
                    for node in document.iter():
                        tag = node.tag.rsplit("}", 1)[-1].lower()
                        if tag == "errorcode" and node.text and re.fullmatch(r"\w{1,40}", node.text):
                            event["fault"] = node.text
                except (ValueError, ElementTree.ParseError):
                    pass
    return {"request_count": len(evidence), "requests": evidence[:160]}


@contextmanager
def capture_exchange(root, client, server, checks, save):
    fields = ("frame.number", "frame.time_epoch", "http.request.uri", "http.request.line",
              "http.request_in", "http.response.code", "http.file_data")
    capture = root / "single-wsus-wire.pcapng"
    raw_log = root / "single-wsus-wire.stderr"
    network = f"host {client} and host {server} and tcp port 8530"
    try:
        with raw_log.open("w") as errors:
            recorder = subprocess.Popen(["tshark", "-n", "-i", "dclab21", "-f", network,
                                         "-a", "filesize:8192", "-w", str(capture)],
                                        stdout=subprocess.DEVNULL, stderr=errors)
            try:
                yield
            finally:
                if recorder.poll() is None:
                    recorder.send_signal(signal.SIGINT)
                    try:
                        recorder.wait(timeout=10)
                    except subprocess.TimeoutExpired:
                        recorder.kill()
                        recorder.wait()
                try:
                    command = ["tshark", "-n", "-r", str(capture), "-d", "tcp.port==8530,http",
                               "-Y", "http.request or http.response", "-T", "fields"]
                    for field in fields:
                        command.extend(("-e", field))
                    decoded = subprocess.run(command, capture_output=True, text=True, timeout=30, check=True)
                    checks["wsus_wire"] = summarize(decoded.stdout)
                except (OSError, ValueError, subprocess.SubprocessError) as error:
                    checks["wsus_wire_error"] = type(error).__name__
                save()
    finally:
        capture.unlink(missing_ok=True)
        raw_log.unlink(missing_ok=True)
