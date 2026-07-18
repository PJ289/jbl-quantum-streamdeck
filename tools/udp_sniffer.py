"""Capture UDP packets to/from Quantum Engine port 20502."""
import socket
import struct
import sys
import threading
import time

HOST = "127.0.0.1"
PORT = 20502


def hexdump(data: bytes, width: int = 16) -> str:
    lines = []
    for i in range(0, len(data), width):
        chunk = data[i : i + width]
        hex_part = " ".join(f"{b:02x}" for b in chunk)
        ascii_part = "".join(chr(b) if 32 <= b < 127 else "." for b in chunk)
        lines.append(f"{i:04x}  {hex_part:<{width * 3}}  {ascii_part}")
    return "\n".join(lines)


def sniff(duration: float = 30.0) -> None:
    # Raw capture is not available without admin; mirror by binding a local client
    # and forwarding while logging everything we see on our socket.
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    sock.bind((HOST, 0))
    local = sock.getsockname()
    sock.settimeout(0.5)
    print(f"Sniffer client bound to {local[0]}:{local[1]}")
    print(f"Watching replies from {HOST}:{PORT} for {duration:.0f}s")
    print("Interact with Quantum Engine now (change volume, ANC, etc.)")
    deadline = time.time() + duration
    while time.time() < deadline:
        try:
            data, addr = sock.recvfrom(65535)
            ts = time.strftime("%H:%M:%S")
            print(f"\n[{ts}] {len(data)} bytes from {addr}")
            print(hexdump(data))
            # Try to decode UTF-16-LE strings embedded in payload
            for i in range(0, len(data) - 3, 2):
                try:
                    end = data.find(b"\x00\x00", i)
                    if end == -1 or end - i < 4:
                        continue
                    s = data[i : end + 1].decode("utf-16-le", errors="ignore").strip("\x00")
                    if len(s) >= 4 and s.isprintable():
                        print(f"  utf16@{i}: {s!r}")
                except Exception:
                    pass
        except socket.timeout:
            continue
    sock.close()


if __name__ == "__main__":
    dur = float(sys.argv[1]) if len(sys.argv) > 1 else 30.0
    sniff(dur)
