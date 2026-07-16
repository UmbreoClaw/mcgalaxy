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

## MCGalaxy version toggle (read this if it won't compile)

MCGalaxy changed how it broadcasts entity positions: older releases use
`UpdatePlayerPositions()`, newer ones use `GetPositionPacket()` / `MaxEntityID`. A single
plugin can only match one, so there is a switch at the top of `AlphaIndev.cs`:

```csharp
#define LEGACY_ENTITY_API
```

* **Leave it uncommented** for older MCGalaxy (the usual downloadable build). This is the
  default, so most people don't need to touch it.
* **Comment it out** (`//#define LEGACY_ENTITY_API`) for newer MCGalaxy.

If compilation fails with *"no suitable method found to override"* mentioning
`UpdatePlayerPositions`, `GetPositionPacket` or `MaxEntityID`, you've got the wrong
setting — flip that one line and recompile. (Both settings have been compile-checked.)

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

## Large maps (fixed crash)

MCGalaxy's `TcpSocket` copies every outgoing packet into a fixed **4096 byte** send
buffer, so any single packet larger than that throws and kills the connection. Full
16x128x16 chunk columns of varied terrain easily compress to more than 4096 bytes, which
is why **small flat maps worked but big maps (e.g. 512x128x512) stuttered and crashed**
partway through loading. Fixes:

* Chunk columns whose compressed packet exceeds the budget are recursively split into
  vertically-stacked sub-regions until every packet fits (a 16x4x16 region fits even if
  its data is completely incompressible, so this always terminates).
* Columns are sent nearest-to-spawn first, so the player's surroundings render right away
  while the rest of the map streams in behind them.
* The Indev map payload (one giant logical packet by protocol design) is sent in slices
  under a session send lock so nothing can interleave into the byte stream.

Very large maps still mean a lot of data for a 2010-era client to chew through (a
512x512 map is ~84 MB of chunk arrays client side) — expect some initial loading stutter,
and gigantic maps (1024x1024+) may exhaust the old client's default 1 GB Java heap.

## Beta inventory (building support)

Beta survival clients join with an empty inventory, so previously they couldn't place a
single block. The plugin now:

* fills the hotbar and main inventory with stacks of buildable blocks at login
  (`0x68 Window Items`),
* **tops the used stack back up to 64 after every block placement** (`0x67 Set Slot`),
  and after dropping an item with Q — effectively infinite blocks, creative-style,
* acknowledges inventory window clicks (`0x6A Transaction`) like a vanilla server, so
  moving items around the inventory doesn't leave unconfirmed actions,
* resyncs the world, inventory, and position when the client respawns after dying
  (previously a respawn left the client on an endless loading screen).

Only blocks whose Beta ids match the classic ids are handed out, so no id remapping is
needed. Relatedly, classic's 16 coloured wool blocks (ids 21-36) — which mean
lapis/sandstone/beds/rails in Beta — are now all shown to Alpha/Beta clients as the
single wool block instead of unrelated garbage blocks.

Digging speed is client-side (survival timing) — the server can't make blocks break
instantly on these old clients. Alpha clients use a different pre-window inventory
system (`0x05`) and are not given items yet.

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
