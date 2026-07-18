import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer


class WebAssemblyRequestHandler(SimpleHTTPRequestHandler):
    def end_headers(self):
        self.send_header("Cross-Origin-Embedder-Policy", "require-corp")
        self.send_header("Cross-Origin-Opener-Policy", "same-origin")
        self.send_header("Cache-Control", "no-store")
        super().end_headers()


parser = argparse.ArgumentParser()
parser.add_argument("--directory", required=True)
parser.add_argument("--port", type=int, default=8000)
arguments = parser.parse_args()
handler = partial(WebAssemblyRequestHandler, directory=arguments.directory)
server = ThreadingHTTPServer(("127.0.0.1", arguments.port), handler)
print(f"Serving RedBolt WebAssembly at http://127.0.0.1:{arguments.port}")
server.serve_forever()
