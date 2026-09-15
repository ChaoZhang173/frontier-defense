#!/usr/bin/env python3
"""Serve the exported Unity Web demo on this Mac only."""
import http.server
from functools import partial
from pathlib import Path
root=Path(__file__).resolve().parents[1]/"Builds"/"Web"
class Handler(http.server.SimpleHTTPRequestHandler):
    extensions_map={**http.server.SimpleHTTPRequestHandler.extensions_map,".wasm":"application/wasm",".unityweb":"application/octet-stream"}
    def end_headers(self):
        self.send_header("Cache-Control","no-cache")
        super().end_headers()
if not (root/"index.html").exists():
    raise SystemExit("Web build has not been generated yet.")
with http.server.ThreadingHTTPServer(("127.0.0.1",8765),partial(Handler,directory=str(root))) as server:
    print("Frontier Defense: http://127.0.0.1:8765",flush=True)
    server.serve_forever()
