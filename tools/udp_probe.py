"""Probe Quantum Engine UDP IPC on 127.0.0.1:20502."""
import socket
import struct
import sys
import time

HOST = "127.0.0.1"
PORT = 20502
TIMEOUT = 2.0


def hexdump(data: bytes, width: int = 16) -> str:
    lines = []
    for i in range(0, len(data), width):
        chunk = data[i : i + width]
        hex_part = " ".join(f"{b:02x}" for b in chunk)
        ascii_part = "".join(chr(b) if 32 <= b < 127 else "." for b in chunk)
        lines.append(f"{i:04x}  {hex_part:<{width * 3}}  {ascii_part}")
    return "\n".join(lines)


def recv_all(sock: socket.socket, attempts: int = 5) -> list[bytes]:
    packets: list[bytes] = []
    sock.settimeout(TIMEOUT)
    for _ in range(attempts):
        try:
            data, addr = sock.recvfrom(65535)
            packets.append(data)
            print(f"<- {len(data)} bytes from {addr}")
            print(hexdump(data))
        except socket.timeout:
            break
    return packets


def send_probe(sock: socket.socket, label: str, payload: bytes) -> list[bytes]:
    print(f"\n=== {label} ({len(payload)} bytes) ===")
    if payload:
        print(hexdump(payload))
    sock.sendto(payload, (HOST, PORT))
    return recv_all(sock)


def main() -> int:
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind(("127.0.0.1", 0))
    local = sock.getsockname()
    print(f"Bound local endpoint {local[0]}:{local[1]}")
    print(f"Target {HOST}:{PORT}")

    probes: list[tuple[str, bytes]] = [
        ("empty", b""),
        ("null_4", b"\x00\x00\x00\x00"),
        ("ascii_ping", b"PING"),
        ("utf16_overlayanc", "OverlayANCProp".encode("utf-16-le")),
        ("utf16_get", "Get".encode("utf-16-le")),
        ("msg_id_1", struct.pack("<I", 1)),
        ("msg_id_1_len", struct.pack("<II", 1, 0)),
        ("prop_len_name", struct.pack("<I", 14) + b"OverlayANCProp"),
    ]

    for label, payload in probes:
        packets = send_probe(sock, label, payload)
        if packets:
            print(f"Got {len(packets)} response packet(s)")
        time.sleep(0.2)

    sock.close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
