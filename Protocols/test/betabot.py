#!/usr/bin/env python3
"""Minimal Beta 1.7.3 protocol client: logs in, walks across the map, reports
every packet the server sends. Used to verify chunk streaming, world border,
inventory and stream integrity (no desyncs) of the AlphaIndev plugin."""
import socket, struct, sys, time

HOST = sys.argv[1] if len(sys.argv) > 1 else "127.0.0.1"
PORT = int(sys.argv[2]) if len(sys.argv) > 2 else 25565
NAME = sys.argv[3] if len(sys.argv) > 3 else "TestBot"

sock = socket.create_connection((HOST, PORT), timeout=15)
sock.settimeout(0.05)
buf = bytearray()

stats = {}
chunk_loads = chunk_unloads = chunk_data = 0
teleports = []          # positions from 0x0D
inventory_items = []    # from 0x68
kick_reason = None
desync = None

def s16(text):
    return struct.pack(">h", len(text)) + text.encode("utf-16-be")

def send(data):
    sock.sendall(data)

def need(n):
    """Returns True if buf has n bytes available."""
    return len(buf) >= n

def rd_s16_len(off):
    """String16 total size starting at off, or -1 if incomplete."""
    if len(buf) < off + 2: return -1
    n = struct.unpack(">h", bytes(buf[off:off+2]))[0]
    if n < 0: raise ValueError("negative string length: desync")
    if len(buf) < off + 2 + n * 2: return -1
    return 2 + n * 2

def rd_s16(off):
    n = struct.unpack(">h", bytes(buf[off:off+2]))[0]
    return bytes(buf[off+2:off+2+n*2]).decode("utf-16-be")

def slot_len(off):
    """Slot payload length at off (short id [+ byte,short]), or -1 incomplete."""
    if len(buf) < off + 2: return -1
    item = struct.unpack(">h", bytes(buf[off:off+2]))[0]
    if item == -1: return 2
    if len(buf) < off + 5: return -1
    return 5

def parse_one():
    """Parses one packet from buf. Returns bytes consumed, 0 if incomplete."""
    global chunk_loads, chunk_unloads, chunk_data, kick_reason
    if not need(1): return 0
    op = buf[0]
    stats[op] = stats.get(op, 0) + 1

    if op == 0x00: return 1
    if op == 0x01:  # login: int eid, str16, long, byte
        if not need(5): return 0
        sl = rd_s16_len(5)
        if sl < 0: return 0
        total = 5 + sl + 8 + 1
        return total if need(total) else 0
    if op in (0x02, 0x03, 0xFF):  # str16 payload
        sl = rd_s16_len(1)
        if sl < 0: return 0
        if op == 0xFF:
            kick_reason = rd_s16(1)
        return 1 + sl
    if op == 0x04: return 9   # time
    if op == 0x06: return 13  # spawn position
    if op == 0x09: return 2   # respawn
    if op == 0x0D:            # self pos+look: 4 doubles, 2 floats, bool
        if not need(42): return 0
        x, stance, y, z = struct.unpack(">dddd", bytes(buf[1:33]))
        teleports.append((x, y, z))
        return 42
    if op == 0x14:            # named entity spawn
        sl = rd_s16_len(5)
        if sl < 0: return 0
        total = 5 + sl + 12 + 2 + 2
        return total if need(total) else 0
    if op == 0x1D: return 5   # destroy entity
    if op == 0x1F: return 8   # rel move
    if op == 0x20: return 7   # entity look
    if op == 0x21: return 10  # rel move + look
    if op == 0x22: return 19  # entity teleport
    if op == 0x32:            # pre-chunk
        if not need(10): return 0
        mode = buf[9]
        if mode: chunk_loads += 1
        else: chunk_unloads += 1
        return 10
    if op == 0x33:            # map chunk
        if not need(18): return 0
        clen = struct.unpack(">i", bytes(buf[14:18]))[0]
        if clen < 0 or clen > 200000: raise ValueError("chunk len %d: desync" % clen)
        total = 18 + clen
        if not need(total): return 0
        chunk_data += 1
        return total
    if op == 0x35: return 12  # block change
    if op == 0x67:            # set slot
        sl = slot_len(4)
        if sl < 0: return 0
        return 4 + sl
    if op == 0x68:            # window items
        if not need(4): return 0
        count = struct.unpack(">h", bytes(buf[2:4]))[0]
        off = 4
        for _ in range(count):
            sl = slot_len(off)
            if sl < 0: return 0
            if sl == 5:
                inventory_items.append(struct.unpack(">h", bytes(buf[off:off+2]))[0])
            off += sl
        return off
    if op == 0x6A: return 5   # transaction
    raise ValueError("unknown opcode 0x%02X: desync (buf head: %s)"
                     % (op, bytes(buf[:24]).hex()))

def pump(duration):
    """Reads + parses server data for `duration` seconds."""
    global desync
    end = time.time() + duration
    while time.time() < end:
        try:
            data = sock.recv(65536)
            if not data:
                return False
            buf.extend(data)
        except socket.timeout:
            pass
        while True:
            try:
                n = parse_one()
            except ValueError as e:
                desync = str(e)
                return False
            if n == 0: break
            del buf[:n]
        if kick_reason is not None: return False
    return True

# ---- handshake + login ----
send(b"\x02" + s16(NAME))
pump(1.0)
send(b"\x01" + struct.pack(">i", 14) + s16(NAME) + struct.pack(">q", 0) + b"\x00")
pump(3.0)

initial_loads, initial_data = chunk_loads, chunk_data
if not teleports:
    print("FAIL: no spawn position received"); sys.exit(1)
x, y, z = teleports[0]
print("spawned at (%.1f, %.1f, %.1f); initial columns: %d loads, %d data" %
      (x, y, z, initial_loads, initial_data))
print("inventory slots filled: %d (first items: %s)" %
      (len(inventory_items), inventory_items[:12]))

# ---- walk east (+X) to the far edge ----
yaw, pitch = 0.0, 0.0
walked_to = x
for step in range(1400):
    x += 0.25
    send(b"\x0D" + struct.pack(">dddd", x, y, y + 1.62, z)
         + struct.pack(">ff", yaw, pitch) + b"\x01")
    walked_to = x
    if step % 4 == 0:
        if not pump(0.02): break
    if teleports and abs(teleports[-1][0] - x) > 2 and len(teleports) > 1:
        print("rubber-banded: client at x=%.1f, server sent back x=%.1f"
              % (x, teleports[-1][0]))
        x = teleports[-1][0]
        if x < walked_to - 1: break  # pushed back = border reached
pump(1.5)

print()
print("=== RESULT ===")
print("walked to x=%.1f" % walked_to)
print("chunk loads: %d (initial %d, streamed %d)" %
      (chunk_loads, initial_loads, chunk_loads - initial_loads))
print("chunk unloads: %d" % chunk_unloads)
print("chunk data packets: %d" % chunk_data)
print("self-teleports received: %d (last: %s)" %
      (len(teleports), tuple(round(v,1) for v in teleports[-1])))
print("packet id histogram: %s" %
      {("0x%02X" % k): v for k, v in sorted(stats.items())})
if desync:      print("DESYNC: %s" % desync)
if kick_reason: print("KICKED: %s" % kick_reason)
ok = desync is None and kick_reason is None and chunk_loads > initial_loads
print("STREAMING TEST:", "PASS" if ok else "FAIL")
