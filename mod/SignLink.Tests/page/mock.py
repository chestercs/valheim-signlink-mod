"""A stand-in for SignLink's HTTP API, to try the import page in a browser without the game."""
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse

PW = "test-password"
PAGE = open("/page/index.html", "rb").read()
IMAGE_ARGS = {"id", "width", "pitch", "place", "x", "up", "out", "tilt", "pose", "turn", "cover", "maxkb", "glow", "alpha",
              "animate", "dry", "name"}   # what POST /image reads (Api/Routes.cs): anything else is a stale page
SIGNS = [{"id": "1:93311", "dist": 3.2, "pos": [0, 0, 0], "yaw": 157.5, "len": 318146, "preview": "<line-height=.375><font=valheim"},
         {"id": "1:93367", "dist": 9.9, "pos": [0, 0, 0], "yaw": 157.5, "len": 0, "preview": ""}]


class H(BaseHTTPRequestHandler):
    def send(self, code, body, ctype="application/json; charset=utf-8"):
        data = body if isinstance(body, bytes) else json.dumps(body).encode()
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def route(self, method):
        u = urlparse(self.path)
        q = {k: v[0] for k, v in parse_qs(u.query).items()}
        path = u.path.rstrip("/")
        if method == "GET" and path in ("", "/index.html"):
            return self.send(200, PAGE, "text/html; charset=utf-8")
        authed = self.headers.get("X-SignLink-Password") == PW
        sent = self.headers.get("X-SignLink-Password")
        if method == "GET" and path == "/ping" and (authed or not sent):     # a wrong password is a 401, like the plugin
            return self.send(200, {"ok": True, "version": "0.4.0", **({"inWorld": True, "player": [1, 2, 3]} if authed else {})})
        if not authed:
            return self.send(401, {"ok": False, "error": "missing or wrong X-SignLink-Password (SignLink window or config)"})
        length = int(self.headers.get("Content-Length") or 0)
        body = self.rfile.read(length)
        if path == "/hovered":
            return self.send(200, {"ok": True, "sign": SIGNS[0]})
        if path == "/signs":
            return self.send(200, {"ok": True, "signs": SIGNS})
        if path == "/image" and method == "POST":
            print(f"image: {len(body)} bytes, {q}", flush=True)
            if not q.get("id"):
                return self.send(400, {"ok": False, "error": "missing ?id="})
            stale = sorted(set(q) - IMAGE_ARGS)
            if stale:
                return self.send(400, {"ok": False, "error": f"unknown parameters {stale}"})
            r = {"ok": True, "w": 499, "h": 395, "signs": 7, "kb": [388, 388, 388, 388, 388, 393, 267], "saveKb": 2780,
                 "encodeMs": 812.4}
            if q.get("dry") != "1":
                r["ids"] = ["1:93311"] + [f"9:{i}" for i in range(6)]
            return self.send(200, r)
        return self.send(400, {"ok": False, "error": f"unknown endpoint {method} {path}"})

    def do_GET(self):
        self.route("GET")

    def do_POST(self):
        self.route("POST")


ThreadingHTTPServer(("0.0.0.0", 8000), H).serve_forever()
