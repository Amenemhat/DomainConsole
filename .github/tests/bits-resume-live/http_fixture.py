"""Serve two tiny, deterministic HTTP range files to the disposable BITS jobs."""
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import re
import sys
import time

DATA = b"DomainConsole BITS lab\n" * 108000


class Handler(BaseHTTPRequestHandler):
    def send_payload(self, head_only):
        if self.path not in {"/Content/Lab/chosen.cab", "/Content/Lab/peer.cab"}:
            self.send_error(404)
            return
        start, end = 0, len(DATA) - 1
        requested = self.headers.get("Range")
        if requested:
            match = re.fullmatch(r"bytes=(\d+)-(\d*)", requested)
            if not match or int(match[1]) >= len(DATA):
                self.send_error(416)
                return
            start = int(match[1])
            if match[2]:
                end = min(end, int(match[2]))
        self.send_response(206 if requested else 200)
        self.send_header("Content-Length", str(end - start + 1))
        self.send_header("Content-Type", "application/octet-stream")
        self.send_header("Accept-Ranges", "bytes")
        self.send_header("ETag", '"domainconsole-static-bits"')
        self.send_header("Last-Modified", "Tue, 01 Sep 2026 00:00:00 GMT")
        if requested:
            self.send_header("Content-Range", f"bytes {start}-{end}/{len(DATA)}")
        self.end_headers()
        if not head_only:
            try:
                for offset in range(start, end + 1, 16 * 1024):
                    self.wfile.write(DATA[offset:min(end + 1, offset + 16 * 1024)])
                    time.sleep(0.15)
            except (BrokenPipeError, ConnectionResetError):
                pass

    def do_HEAD(self):
        self.send_payload(True)

    def do_GET(self):
        self.send_payload(False)


with ThreadingHTTPServer(("127.0.0.1", 0), Handler) as server:
    Path(sys.argv[1]).write_text(str(server.server_port), encoding="ascii")
    server.serve_forever()
