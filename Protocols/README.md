# AlphaIndev protocol plugin

`AlphaIndev.cs` is a standalone MCGalaxy plugin that lets **classic (pre-Netty)
Minecraft Java edition** clients connect to an MCGalaxy server:

| Client            | Protocol version | Notes |
|-------------------|:----------------:|-------|
| Beta 1.7.3        | 14               | Fully mapped and hardened (primary target) |
| Alpha (a1.1.x)    | 2                | Supported; a few Alpha-only packets are best-effort (see below) |
| Indev             | 9                | Supported; the exact wire format is not independently verified |

It works by registering a protocol handler for opcode `0x02` (the Alpha/Beta/Indev
handshake), so it lives alongside the normal Classic/ClassiCube protocol without
interfering with it. When the plugin is unloaded the original handler is restored.

## Building

The plugin references only `MCGalaxy_.dll` and `System.dll`, so it compiles like any
other MCGalaxy plugin.

* **In-game / console:** `/pcompile AlphaIndev` after placing `AlphaIndev.cs` in the
  server's `plugins/` source directory, then `/pload AlphaIndev`.
* **Manually:** compile `AlphaIndev.cs` against `MCGalaxy_.dll` (`net6.0`, unsafe blocks
  enabled) and drop the resulting `AlphaIndev.dll` into the server's `plugins/` folder.
  It is auto-loaded on startup.

## What was hardened

This is a robustness-focused rewrite of the original proof-of-concept. The goals were
"foolproof and less buggy", specifically eliminating the desyncs that showed up as random
*"unhandled opcode"* kicks and client-side crashes while mining, placing blocks, or using
the inventory. Key changes:

* **Compiles against modern MCGalaxy (1.9.5.3+).** Implements `GetPositionPacket`,
  `MaxEntityID`, `SendEntityProperty` and `SendToggleBlockList`, which replaced the older
  `UpdatePlayerPositions` API the original was written against.
* **Every serverbound packet is accounted for.** In a stream protocol, a single
  mis-sized packet desyncs the whole connection. All packets a client sends during normal
  play — movement, digging, block placement, arm swing, inventory/window clicks, sign
  edits, respawn, use-entity, entity actions, holding change, transactions, disconnect —
  are now parsed to their correct length, even the ones the server ignores.
* **Beta block placement is variable-length.** `0x0F` carries optional item data
  (`byte amount, short damage`) when a block is held. The original consumed a fixed size
  and desynced on every block place; it now reads the optional tail.
* **Alpha vs Beta is distinguished by protocol version, not string encoding.** Both use
  UCS-2 strings, so the previous "is it UTF-8?" heuristic could not reliably tell them
  apart and picked the wrong login/packet layout.
* **Correct dig status.** Block break is signalled by digging status `2` in Alpha/Beta.
* **Defensive bounds checking.** Oversized/negative string lengths, out-of-range block
  coordinates, and truncated packets are handled by cleanly closing or ignoring rather
  than throwing or corrupting server state. A capped `MaxEntityID` keeps the shared entity
  position buffer from overflowing (old-protocol teleports are larger than Classic's).

## Known limitations / things to field-test

* **Vertical position calibration** uses the same tuned offsets as the original
  (they are approximate — "kinda works" magic numbers).
* **Alpha-only packets** (`0x05` inventory; the `0x10` holding-change layout; respawn with
  no dimension byte; digging status semantics) are based on protocol history rather than a
  byte-verified capture. If an Alpha client misbehaves, capture its bytes and confirm those
  layouts.
* **Indev** connects through the Alpha-family framing at protocol 9; its packet layouts are
  inherited from the original and have not been independently verified.
* Support for newer (post-Netty, 1.7+) Minecraft versions is intentionally **not** included
  here — that protocol is a completely different design and is planned as separate work.
