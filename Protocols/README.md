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

## Large maps (fixed two crashes)

Big maps (e.g. 512x128x512) used to stutter and then crash the client. Two independent
causes, both fixed:

**1. Oversized packets killed the connection.** MCGalaxy's `TcpSocket` copies every
outgoing packet into a fixed **4096 byte** send buffer, so any single packet larger than
that throws and aborts the level send. Full 16x128x16 chunk columns of varied terrain
easily compress to more than 4096 bytes (small flat maps compressed under the limit,
which is why they seemed fine). Columns whose compressed packet exceeds the budget are
now recursively split into vertically-stacked sub-regions until every packet fits
(a 16x4x16 region fits even incompressible data, so this always terminates). The Indev
map payload (one giant packet by protocol design) is sent in slices under a session send
lock so nothing can interleave into the byte stream.

**2. Sending the whole map ran the client out of Java heap.** Old clients keep every
received chunk column in memory (~80 KB each), so a 512x512 map meant 1024 columns and
an `Out of memory!` crash. The plugin now behaves like a real Beta server:

* only columns within `VIEW_RADIUS` (default 8) chunks of the player are sent, nearest
  rings first — at most 17x17 = 289 columns (~24 MB) held client side, regardless of
  map size,
* new columns stream in as the player walks or teleports (teleports send the destination
  terrain *before* the position, so the client never falls into ungenerated world),
* columns further than `VIEW_RADIUS + 2` are unloaded from the client as it moves, so
  memory stays bounded forever.

`VIEW_RADIUS` is a const at the top of the map-sending region in `AlphaIndev.cs` — raise
it for more visible terrain (vanilla beta servers used 10), lower it if a client with a
tiny Java heap still struggles.

**3. Bogus lighting made the client queue endless corrections.** Chunks used to be sent
with sky light 15 *everywhere* — including inside solid stone and caves. The Beta client
recomputes lighting from the block data and schedules a correction for every disagreeing
cell, and each newly streamed column triggered relighting floods through the neighboring
terrain. On cave-riddled maps (e.g. classic-generated levels) the correction queue grew
with every chunk received while walking, until the client ran out of memory — this is why
walking away from spawn crashed even before reaching the map edge. Chunks are now sent
with heightmap-consistent sky light (full light above the highest light-blocking block,
darkness below — the same rule the client itself uses), so the client has ~nothing to
correct. Side effect: caves are now actually dark, like real Beta; surfaces are lit
normally.

## Console verbosity

`AlphaIndevPlugin.Verbose` (default **true**) logs diagnostic activity to the console:

* level sends and per-step chunk streaming (`+sent/-unloaded columns around chunk (x,z)`),
* chunk columns that exceed the packet budget and get split (with the byte size),
* block placements whose id mapping is not 1:1 (`placed id 35:14 -> stored as block 21`),
  including placements with no equivalent that get reverted,
* world border hits and fell-out-of-world rescues.

Set the field to `false` (or edit the source default) to silence it once things work.

**Sanity check: am I running the current build?** The plugin *always* logs
`AlphaIndev: sending level <map> to <player> (view radius 8 chunks)` the moment a Beta
client joins, and `+N/-M columns around chunk (x,z)` lines while they walk. If a Beta
client can join and play but the console never shows these lines, the server is still
running an **old build** of the plugin — replace the `.cs`, recompile, and restart the
server (a restart is the reliable way to swap the loaded protocol handler).

## Protocol test client

`test/betabot.py` is a minimal headless Beta 1.7.3 client
(`python3 betabot.py [host] [port] [name]`). It performs the full handshake + login,
parses every clientbound packet (so any stream desync is detected and reported), walks
across the map, and prints a summary: initial/streamed/unloaded chunk counts, inventory
contents, world-border rubber-banding, and a packet histogram. Useful for verifying a
server's plugin build without launching a real client. The entire feature set —
streaming, border, inventory/tools, wool colour metadata, unknown-block reverts — has
been verified end-to-end with it against a live server.

One side effect of view-radius streaming: a player located beyond the loaded radius may
occasionally be invisible until they next move (their entity was spawned into a column
the client hadn't loaded). Position updates re-place them automatically.

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

**Tools:** the hotbar includes a diamond pickaxe/shovel/axe (plus sword and shears in
the main inventory) so digging is much faster — dig speed is computed client side from
the held item, so the server needs no timing changes. Tool durability is also client
side, so the held tool is refreshed to full durability after every completed dig;
without that, tools would slowly wear out and break. Tools are sent with stack count 1
(they don't stack), and right-clicking with a tool in hand is now correctly ignored
instead of being fed into block placement as a bogus block id. Fully instant "creative"
breaking isn't possible — the break animation/timing lives in the client.

**Block id translation:** MCGalaxy stores classic block ids, where 21-36 are the 16
coloured wools; in Alpha/Beta those same ids mean lapis/sandstone/beds/rails/pistons.
A proper two-way translation layer now handles this:

* outgoing, coloured wools map to Beta's single wool block **with the right colour in
  the block metadata** (both in chunk data and block-change packets), so classic wool
  builds keep their colours on Beta clients,
* incoming, wool placements carry their colour in the item damage value and are stored
  as the matching classic coloured wool — the inventory includes red/blue/yellow wool,
* craftable Beta blocks get sensible classic equivalents (stairs -> planks,
  chest/crafting table -> crate, ladder -> rope, sandstone -> CPE sandstone, ...),
  and blocks with no reasonable equivalent are reverted client-side instead of being
  stored as garbage,
* bulk block updates (physics, mass edits) now convert ids properly for these clients
  instead of sending raw internal ids.

**World border:** MCGalaxy worlds are finite but Alpha/Beta clients assume infinite
terrain — walking past the map edge used to strand the client in nonexistent chunks
(falling/crashing). The plugin now acts as a world border sized to the level: movement
is clamped to the map bounds and the client is rubber-banded back inside, and falling
out of the bottom of the world rescues the player to the spawn point.

Alpha clients use a different pre-window inventory system (`0x05`) and are not given
items yet.

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
