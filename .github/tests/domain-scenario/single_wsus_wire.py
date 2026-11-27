"""Summarize WSUS HTTP exchanges between two disposable lab guests."""
from contextlib import contextmanager
import csv
from datetime import datetime, timezone
import io
import re
import signal
import subprocess
import time
from uuid import UUID
from xml.etree import ElementTree


def summarize(output):
    requests = {}
    evidence = []
    for frame, epoch, path, headers, reply_to, status, payload in csv.reader(io.StringIO(output), delimiter="\t"):
        if path:
            action = re.search(r'SOAPAction:\s*"?[^\s"|]*/([A-Za-z][A-Za-z0-9]*)', headers, re.IGNORECASE)
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


def accepted_authorization(trace, identifier, since=None):
    identifier = str(UUID(identifier))
    return next((event for event in trace["requests"]
                 if event["action"].casefold() == "getauthorizationcookie"
                 and identifier in event.get("client_ids", []) and event.get("status") == 200
                 and (since is None or datetime.fromisoformat(event["at"]) >= since)), None)


def decode_capture(capture, port):
    fields = ("frame.number", "frame.time_epoch", "http.request.uri", "http.request.line",
              "http.request_in", "http.response.code", "http.file_data")
    command = ["tshark", "-n", "-r", str(capture), "-d", f"tcp.port=={port},http",
               "-Y", "http.request or http.response", "-T", "fields", "-E", "quote=d", "-E", "aggregator=|"]
    for field in fields:
        command.extend(("-e", field))
    decoded = subprocess.run(command, capture_output=True, text=True, timeout=20)
    if decoded.returncode and "cut short" not in decoded.stderr:
        raise RuntimeError("WSUS capture decode failed: " + decoded.stderr.strip()[-400:])
    return summarize(decoded.stdout)


@contextmanager
def capture_exchange(root, interface, client, server, port=8530):
    capture = root / "single-wsus-wire.pcapng"
    raw_log = root / "single-wsus-wire.stderr"
    network = f"host {client} and host {server} and tcp port {port}"
    try:
        with raw_log.open("w") as errors:
            recorder = subprocess.Popen(["tcpdump", "-i", interface, "-nn", "-s", "0", "-U", "-Z", "root",
                                         "-c", "1500", "-w", str(capture), network],
                                        stdout=subprocess.DEVNULL, stderr=errors)
            try:
                deadline = time.monotonic() + 8
                while "listening on" not in raw_log.read_text(errors="replace"):
                    if recorder.poll() is not None or time.monotonic() >= deadline:
                        raise RuntimeError("WSUS capture did not start: " + raw_log.read_text(errors="replace")[-400:])
                    time.sleep(0.1)
                yield lambda: decode_capture(capture, port)
            finally:
                if recorder.poll() is None:
                    recorder.send_signal(signal.SIGINT)
                    try:
                        recorder.wait(timeout=10)
                    except subprocess.TimeoutExpired:
                        recorder.kill()
                        recorder.wait()
    finally:
        capture.unlink(missing_ok=True)
        raw_log.unlink(missing_ok=True)


if __name__ == "__main__":
    # Check the actual capture and decoder on the runner before provisioning Windows.
    from http.client import HTTPConnection
    from http.server import BaseHTTPRequestHandler, HTTPServer
    from pathlib import Path
    from tempfile import TemporaryDirectory
    from threading import Thread

    class Handler(BaseHTTPRequestHandler):
        def do_POST(self):
            self.rfile.read(int(self.headers["Content-Length"]))
            self.send_response(200)
            self.send_header("Content-Length", "0")
            self.end_headers()

    fixture_id = "555a7030-8f05-4979-ae96-34573f1a0b7f"
    with TemporaryDirectory() as directory, HTTPServer(("127.0.0.1", 0), Handler) as server:
        with capture_exchange(Path(directory), "lo", "127.0.0.1", "127.0.0.1", server.server_port) as snapshot:
            handler = Thread(target=server.handle_request, daemon=True)
            handler.start()
            connection = HTTPConnection("127.0.0.1", server.server_port, timeout=5)
            try:
                connection.request("POST", "/SimpleAuthWebService/SimpleAuth.asmx",
                                   f"<Envelope><clientId>{fixture_id}</clientId></Envelope>",
                                   {"Content-Type": "text/xml",
                                    "SOAPAction": '"http://www.microsoft.com/GetAuthorizationCookie"'})
                connection.getresponse().read()
            finally:
                connection.close()
            handler.join(timeout=5)
            deadline = time.monotonic() + 5
            while not accepted_authorization(snapshot(), fixture_id):
                if time.monotonic() >= deadline:
                    raise RuntimeError("Capture self-check could not link the SOAP client ID to HTTP 200")
                time.sleep(0.5)
    print("WSUS capture self-check passed", flush=True)
