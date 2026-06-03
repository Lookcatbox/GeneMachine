#!/usr/bin/env python3
"""Local static server + DeepSeek API proxy (avoids browser CORS)."""

import json
import os
import urllib.error
import urllib.request
from http.server import HTTPServer, SimpleHTTPRequestHandler

PORT = 8765
ROOT = os.path.dirname(os.path.abspath(__file__))


class Handler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=ROOT, **kwargs)

    def end_headers(self):
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type, X-Api-Key, X-Api-Base, X-Diagram-Mode")
        super().end_headers()

    def do_OPTIONS(self):
        self.send_response(204)
        self.end_headers()

    def do_POST(self):
        if self.path == "/api/chat":
            self.proxy_chat()
        else:
            self.send_error(404)

    def proxy_chat(self):
        length = int(self.headers.get("Content-Length", 0))
        body = self.rfile.read(length)

        api_key = self.headers.get("X-Api-Key", "")
        api_base = self.headers.get("X-Api-Base", "https://api.deepseek.com").rstrip("/")

        if not api_key:
            self.send_json(400, {"error": "Missing X-Api-Key header"})
            return

        url = f"{api_base}/v1/chat/completions"
        req = urllib.request.Request(
            url,
            data=body,
            headers={
                "Content-Type": "application/json",
                "Authorization": f"Bearer {api_key}",
            },
            method="POST",
        )

        try:
            timeout = 300 if json.loads(body).get("thinking") else 120
        except (json.JSONDecodeError, AttributeError):
            timeout = 120
        if self.headers.get("X-Diagram-Mode") == "1":
            timeout = 300

        try:
            with urllib.request.urlopen(req, timeout=timeout) as resp:
                data = resp.read()
                self.send_response(resp.status)
                self.send_header("Content-Type", "application/json")
                self.end_headers()
                self.wfile.write(data)
        except urllib.error.HTTPError as e:
            err_body = e.read().decode("utf-8", errors="replace")
            try:
                err_json = json.loads(err_body)
            except json.JSONDecodeError:
                err_json = {"error": err_body or str(e)}
            self.send_json(e.code, err_json)
        except Exception as e:
            self.send_json(502, {"error": str(e)})

    def send_json(self, code, obj):
        payload = json.dumps(obj, ensure_ascii=False).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.end_headers()
        self.wfile.write(payload)


def main():
    os.chdir(ROOT)
    server = HTTPServer(("127.0.0.1", PORT), Handler)
    print(f"Vocab Trainer running at http://127.0.0.1:{PORT}")
    print("Press Ctrl+C to stop.")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nStopped.")


if __name__ == "__main__":
    main()
