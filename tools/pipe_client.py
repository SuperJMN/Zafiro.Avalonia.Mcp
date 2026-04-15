#!/usr/bin/env python3
"""Simple named pipe client for Zafiro.Avalonia.Mcp diagnostic protocol."""
import socket
import json
import sys
import os
import glob

def find_app():
    disc_dir = os.path.join("/tmp", "zafiro-avalonia-mcp")
    if not os.path.isdir(disc_dir):
        return None
    for f in glob.glob(os.path.join(disc_dir, "*.json")):
        try:
            with open(f) as fh:
                info = json.load(fh)
                pid = info.get("pid")
                if pid and os.path.exists(f"/proc/{pid}"):
                    return info
        except:
            pass
    return None

def send_request(pipe_name, method, params=None, req_id="1"):
    sock_path = f"/tmp/CoreFxPipe_{pipe_name}"
    req = {"method": method, "id": req_id}
    if params:
        req["params"] = params
    payload = json.dumps(req) + "\n"

    s = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
    s.settimeout(10)
    s.connect(sock_path)
    s.sendall(payload.encode("utf-8"))

    buf = b""
    while True:
        chunk = s.recv(4096)
        if not chunk:
            break
        buf += chunk
        if b"\n" in buf:
            break
    s.close()
    line = buf.split(b"\n")[0]
    return json.loads(line)

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: pipe_client.py <method> [params_json]")
        print("       pipe_client.py discover")
        sys.exit(1)

    if sys.argv[1] == "discover":
        info = find_app()
        if info:
            print(json.dumps(info, indent=2))
        else:
            print("No running apps found")
        sys.exit(0)

    method = sys.argv[1]
    params = json.loads(sys.argv[2]) if len(sys.argv) > 2 else None

    info = find_app()
    if not info:
        print("No running app found. Start SampleApp first.")
        sys.exit(1)

    pipe_name = info["pipeName"]
    resp = send_request(pipe_name, method, params)
    print(json.dumps(resp, indent=2, ensure_ascii=False))
