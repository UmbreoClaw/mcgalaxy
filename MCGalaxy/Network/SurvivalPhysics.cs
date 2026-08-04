/*
    Copyright 2015-2024 MCGalaxy

    Dual-licensed under the Educational Community License, Version 2.0 and
    the GNU General Public License, Version 3 (the "Licenses"); you may
    not use this file except in compliance with the Licenses. You may
    obtain a copy of the Licenses at

    https://opensource.org/license/ecl-2-0/
    https://www.gnu.org/licenses/gpl-3.0.html

    Unless required by applicable law or agreed to in writing,
    software distributed under the Licenses are distributed on an "AS IS"
    BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
    or implied. See the Licenses for the specific language governing
    permissions and limitations under the Licenses.
 */
using System;
using System.Collections.Generic;
using MCGalaxy.Blocks;
using BlockID = System.UInt16;

namespace MCGalaxy.Network
{
    /// <summary> Server-authoritative Indev block physics: BlockFire (spread,
    /// burn-out, flint) and the genuine FINITE fluids (BlockFlowing/Stationary -
    /// spread-down-then-one-horizontal, volume-conserving donor removal, infinite
    /// springs, stagnation/petrify/evaporate, water<->lava/fire interactions).
    /// Ports of the client's IndevFire.c and IndevTest.c fluid section, driven off
    /// the 20 TPS survival tick and streamed to viewers as ordinary SetBlock.
    /// Indev maps only; every change flows through SurvivalGrowth.SetView so it is
    /// broadcast and re-announced back here via Notify (the genuine notify hook). </summary>
    /// <remarks>
    /// The scheduler is genuine World.tick: ONE shared FIFO tick list for fire and
    /// fluids, no dedup, entries recording the scheduled block id (stale entries
    /// drop at run time), at most 200 pops per tick shared between count-downs and
    /// runs. The notify hook mirrors genuine onBlockAdded/onNeighborBlockChange
    /// synchronously: fire support checks, still-fluid petrify/wake
    /// (BlockStationary), and spring floods (BlockSource) all run inline; only the
    /// fluid updateTicks themselves ride the tick list. There is no load-time
    /// scan - fire/moving fluid on a loaded map revive via the random tickOnLoad
    /// pass. TNT caught by fire is primed as a full-fuse entity
    /// (FireTryCatch -> SurvivalTnt.Ignite).
    /// </remarks>
    internal static class SurvivalPhysics
    {
        const ushort FIRE       = SurvivalBlocks.FIRE;         // 51
        const ushort WATER_SRC  = SurvivalBlocks.WATER_SOURCE; // 52
        const ushort LAVA_SRC   = SurvivalBlocks.LAVA_SOURCE;  // 53

        internal static bool IsFire(ushort v)        { return v == FIRE; }
        internal static bool IsMovingFluid(ushort v) { return v == Block.Water || v == Block.Lava; }
        internal static bool IsSource(ushort v)      { return v == WATER_SRC || v == LAVA_SRC; }

        // Genuine material table: BlockSource's ctor registers Material.water for
        // BOTH springs - the LAVA spring (53) is water-material (BlockSource.java:11,
        // a genuine quirk) - so every material-based gate (petrify, the equalize
        // path's "below is my material", sponge absorb) treats it as water.
        static bool IsWaterMat(ushort b) { return b == Block.Water || b == Block.StillWater || b == WATER_SRC || b == LAVA_SRC; }
        static bool IsLavaMat(ushort b)  { return b == Block.Lava  || b == Block.StillLava; }

        // ==================== per-level state ====================

        // NextTickListEntry: genuine World.tickList holds (coords, blockID, time)
        // for fire AND fluids in ONE list - the id recorded at schedule time makes
        // stale entries (the cell changed since) silent no-ops at run time.
        struct TickEntry { public int Index; public ushort BlockId; public int Time; }

        sealed class LevelPhys
        {
            public byte[] FireAge;                       // one nibble (0-15) per cell
            public int    FireVol;

            // genuine World.tickList: one shared FIFO, no dedup, drained <=200
            // pops per tick (decrements and runs share the same budget)
            public readonly Queue<TickEntry> TickList = new Queue<TickEntry>();

            public readonly Random Rng = new Random();
            public readonly int[] LiquidOrder = { 0, 1, 2, 3 };

            // flood-fill scratch (genuine x + (z<<10) layer packing), lazily sized
            public ushort[] Stamps; public int StampsLen; public ushort Counter;
            public int[] StackA, StackB; public int StackCap;
        }

        static readonly Dictionary<Level, LevelPhys> registry = new Dictionary<Level, LevelPhys>();
        static readonly object registryLock = new object();

        const int TICK_SCHED_MAX = 1 << 18; // genuine tickList is unbounded; this is a runaway backstop only

        static LevelPhys Get(Level lvl, bool create) {
            lock (registryLock) {
                LevelPhys lp;
                if (registry.TryGetValue(lvl, out lp)) return lp;
                if (!create) return null;
                lp = new LevelPhys();
                registry[lvl] = lp;
                return lp;
            }
        }

        public static void Prune(Level[] loaded) {
            lock (registryLock) {
                List<Level> dead = null;
                foreach (KeyValuePair<Level, LevelPhys> kvp in registry)
                {
                    if (Array.IndexOf(loaded, kvp.Key) < 0) {
                        if (dead == null) dead = new List<Level>();
                        dead.Add(kvp.Key);
                    }
                }
                if (dead != null) foreach (Level lvl in dead) registry.Remove(lvl);
            }
        }


        // ==================== block helpers ====================

        static ushort View(Level lvl, int x, int y, int z) { return SurvivalGrowth.ViewAt(lvl, x, y, z); }

        // Genuine setBlock/setBlockWithNotify refuse the OUTER SHELL (x/y/z 0 and
        // dim-1): every runtime write there silently fails, which is what makes
        // the map-edge ocean ring an INFINITE source - border cells can never be
        // emptied by donor pulls, petrified, evaporated, or spread into. Ours
        // accepted shell writes, so sponge refloods (and any drainage near the
        // edge) DRAINED the edge ring and the ocean surface never converged
        // (user-reported stepped patches). setTileNoUpdate is full-range in
        // genuine, and a fluid's still<->moving state flip is exactly that class,
        // so those keep the whole map.
        static bool Interior(Level lvl, int x, int y, int z) {
            return x > 0 && y > 0 && z > 0 &&
                   x < lvl.Width - 1 && y < lvl.Height - 1 && z < lvl.Length - 1;
        }
        static bool StateFlipWrite(ushort oldV, ushort newV) {
            return (oldV == Block.StillWater && newV == Block.Water)      ||
                   (oldV == Block.Water      && newV == Block.StillWater) ||
                   (oldV == Block.StillLava  && newV == Block.Lava)       ||
                   (oldV == Block.Lava       && newV == Block.StillLava);
        }
        static void   Set(Level lvl, int x, int y, int z, ushort v) {
            if (!Interior(lvl, x, y, z) && !StateFlipWrite(SurvivalGrowth.ViewAt(lvl, x, y, z), v)) return;
            SurvivalGrowth.SetView(lvl, x, y, z, v);
        }
        // fire's spread/burn writes carry the "(fire)" BlockDB author, so /About
        // names the culprit and /UndoPlayer (fire) can roll a blaze back
        static void   SetFire(Level lvl, int x, int y, int z, ushort v) {
            if (!Interior(lvl, x, y, z)) return;
            SurvivalGrowth.SetView(lvl, SurvivalActors.Fire, x, y, z, v);
        }
        static bool   In(Level lvl, int x, int y, int z) {
            return x >= 0 && y >= 0 && z >= 0 && x < lvl.Width && y < lvl.Height && z < lvl.Length;
        }
        static int Pack(Level lvl, int x, int y, int z) { return (y * lvl.Length + z) * lvl.Width + x; }
        static void Unpack(Level lvl, int idx, out int x, out int y, out int z) {
            x = idx % lvl.Width; z = (idx / lvl.Width) % lvl.Length; y = idx / (lvl.Width * lvl.Length);
        }

        // isBlockNormalCube == isOpaqueCube: glass, slabs and leaves are NOT
        // normal cubes, so fire cannot rest on them (the collide-solid test
        // wrongly accepted them as support).
        static bool NormalCube(Level lvl, int x, int y, int z) {
            if (!In(lvl, x, y, z)) return false;
            return SurvivalGrowth.OpaqueCube(View(lvl, x, y, z));
        }


        // ==================== the notify hook ====================

        /// <summary> Announced for every server-authored block change on the level
        /// (SurvivalGrowth.SetView). Runs the genuine onBlockAdded /
        /// onNeighborBlockChange reactions SYNCHRONOUSLY, exactly like genuine's
        /// notify recursion: fire support checks, still-fluid petrify/wake
        /// (BlockStationary), spring floods (BlockSource) and sponge handling all
        /// happen inline; only the fluid/fire updateTicks themselves are enqueued
        /// onto the shared tick list. Recursion is bounded: wake flips early-return
        /// here, petrified stone triggers no further petrify, fire chains are
        /// short. </summary>
        // Genuine setBlock-class writes notify nobody; the sponge absorb is one
        // (BlockSponge.onBlockAdded uses setBlock, not setBlockWithNotify). The
        // flag spans the absorb's writes so removing a pond's worth of water
        // does not wake the ocean around it - which redistributed the whole
        // surface through donor pulls (user-reported patchy stepped water).
        [ThreadStatic] static bool quietWrites;

        internal static void Notify(Level lvl, int x, int y, int z, ushort oldV, ushort newV) {
            Notify(lvl, x, y, z, oldV, newV, false);
        }

        // fullWrite: genuine distinguishes write CLASS, not id pairs - a PLAYER
        // replacing still water with moving water is setBlockWithNotify (its
        // onBlockAdded schedules, its neighbours are notified) even though the
        // same id pair from the wake/settle paths is setTileNoUpdate. Player
        // edits pass true; every internal Set keeps the flip semantics.
        static void Notify(Level lvl, int x, int y, int z, ushort oldV, ushort newV, bool fullWrite) {
            if (lvl.Config.SurvivalMode != SurvivalMode.Indev) return;
            if (quietWrites) return;
            LevelPhys lp = Get(lvl, true);

            // Notify is reachable from PLAYER receive threads (OnBlockChanged) as
            // well as the tick thread (Set -> SetView -> Notify); the schedules are
            // a plain Queue, so every mutation serializes on the LevelPhys monitor
            // (re-entrant, so tick-thread nesting is fine).
            // A fluid flipping between its own still and moving states is genuine
            // setTileNoUpdate: it notifies NOBODY, runs no onBlockAdded, resets no
            // metadata. BlockStationary's wake pairs the flip with its OWN explicit
            // scheduleBlockUpdate (NotifyNeighbour), and the settle flip schedules
            // nothing - so this branch is a pure no-op. Running the neighbour
            // notifies for these flips closed a feedback loop - a woken cell's
            // conversion woke its neighbours, their re-stilling woke it back, and
            // whole lakes flickered still<->moving forever (user-reported as
            // "water physics super fast" + the lighting pulsing with it).
            if (!fullWrite && StateFlipWrite(oldV, newV)) return;

            if (oldV == Block.Sapling && newV != Block.Sapling)
                SurvivalGrowth.ClearSaplingStage(lvl, x, y, z);

            lock (lp) {
                // BlockFire.onBlockAdded runs synchronously: a fire with no normal
                // cube below and no flammable neighbour is removed at once, else it
                // schedules its first update. (This used to enqueue unconditionally,
                // so an unsupported fire lingered ~1s server-side before the check.)
                if (newV == FIRE) {
                    if (!NormalCube(lvl, x, y - 1, z) && !CanNeighbourCatch(lvl, x, y, z)) {
                        SetFire(lvl, x, y, z, Block.Air);
                    } else {
                        SetAge(lp, lvl, Pack(lvl, x, y, z), 0);
                        ScheduleUpdate(lp, Pack(lvl, x, y, z), FIRE);
                    }
                }
                else if (oldV == FIRE) SetAge(lp, lvl, Pack(lvl, x, y, z), 0);

                // BlockFlowing.onBlockAdded: a placed moving fluid schedules its
                // first update at its tickRate
                if (newV == Block.Water || newV == Block.Lava) ScheduleUpdate(lp, Pack(lvl, x, y, z), newV);

                // BlockSource.onBlockAdded runs synchronously inside setBlock:
                // the 4 horizontal AIR neighbours flood with the moving fluid the
                // instant the spring is placed
                if (newV == WATER_SRC || newV == LAVA_SRC)
                    RandomTickSource(lvl, x, y, z, newV);

                // BlockSponge: onBlockAdded absorbs every water-material block in
                // the 5x5x5 cube; onBlockRemoval notifies that whole cube, whose
                // still-fluid neighbours then wake and re-flood the dry pocket.
                // (The canFlow veto alone kept water OUT but never removed what
                // was already there, and mining a sponge only woke its 6 direct
                // neighbours - all air inside the absorbed gap - so the pocket
                // stayed dry forever. The client has always absorbed; SP/MP split.)
                if (newV == Block.Sponge && oldV != Block.Sponge) SpongeAbsorb(lvl, x, y, z);
                if (oldV == Block.Sponge && newV != Block.Sponge) SpongeRemoved(lp, lvl, x, y, z);

                // neighbours react: re-check adjacent fires, wake adjacent still fluids
                NotifyNeighbour(lp, lvl, x - 1, y, z, newV);
                NotifyNeighbour(lp, lvl, x + 1, y, z, newV);
                NotifyNeighbour(lp, lvl, x, y - 1, z, newV);
                NotifyNeighbour(lp, lvl, x, y + 1, z, newV);
                NotifyNeighbour(lp, lvl, x, y, z - 1, newV);
                NotifyNeighbour(lp, lvl, x, y, z + 1, newV);
            }
        }

        // BlockSponge.onBlockAdded: isWater (Material.water: moving, still, and
        // the water spring - BlockSource registers Material.water) -> air.
        static void SpongeAbsorb(Level lvl, int x, int y, int z) {
            quietWrites = true;
            try {
                SpongeAbsorbCore(lvl, x, y, z);
            } finally { quietWrites = false; }
        }

        static void SpongeAbsorbCore(Level lvl, int x, int y, int z) {
            for (int sy = y - 2; sy <= y + 2; sy++)
                for (int sz = z - 2; sz <= z + 2; sz++)
                    for (int sx = x - 2; sx <= x + 2; sx++)
                    {
                        if (!Interior(lvl, sx, sy, sz)) continue; // genuine setBlock: shell unwritable
                        ushort b = View(lvl, sx, sy, sz);
                        // World.isWater is material-based, so the LAVA spring
                        // (Material.water, genuine quirk) is absorbed too
                        if (IsWaterMat(b))
                            SurvivalGrowth.SetView(lvl, sx, sy, sz, Block.Air);
                    }
        }

        // BlockSponge.onBlockRemoval: notifyBlocksOfNeighborChange over the whole
        // +-2 cube - i.e. the 6 NEIGHBOURS of every cube cell react, which reaches
        // the still water standing at distance 3 (just past the old canFlow veto).
        static void SpongeRemoved(LevelPhys lp, Level lvl, int x, int y, int z) {
            for (int sy = y - 2; sy <= y + 2; sy++)
                for (int sz = z - 2; sz <= z + 2; sz++)
                    for (int sx = x - 2; sx <= x + 2; sx++)
                    {
                        // genuine passes each cube cell's CURRENT id as the
                        // changed id (getBlockId in onBlockRemoval) - a lava
                        // cell in the cube petrifies adjacent still water, a
                        // flammable cell force-wakes it; only absorbed cells
                        // announce air
                        ushort cellV = In(lvl, sx, sy, sz) ? View(lvl, sx, sy, sz) : Block.Air;
                        NotifyNeighbour(lp, lvl, sx - 1, sy, sz, cellV);
                        NotifyNeighbour(lp, lvl, sx + 1, sy, sz, cellV);
                        NotifyNeighbour(lp, lvl, sx, sy - 1, sz, cellV);
                        NotifyNeighbour(lp, lvl, sx, sy + 1, sz, cellV);
                        NotifyNeighbour(lp, lvl, sx, sy, sz - 1, cellV);
                        NotifyNeighbour(lp, lvl, sx, sy, sz + 1, cellV);
                    }
        }

        /// <summary> OnBlockChangedEvent: a player edit finished. Route it to the
        /// physics notify so placed fire/fluid come alive and mining next to a
        /// fluid/fire wakes the neighbours. The event carries no old block, but
        /// only the new block + neighbour reactions matter for scheduling. </summary>
        // OnBlockChangedEvent does not carry the old block, but sponge removal
        // needs it (nothing else about the post-write world says one was there).
        // OnBlockChanging runs synchronously before the write on the same player
        // thread, so a thread-local snapshot pairs the two exactly.
        [ThreadStatic] static int preChangeCoord;
        [ThreadStatic] static ushort preChangeView;

        public static void OnBlockChanging(Player p, ushort x, ushort y, ushort z, ushort block, bool placing, ref bool cancel) {
            Level lvl = p.level;
            if (lvl == null || lvl.Config.SurvivalMode != SurvivalMode.Indev || cancel) return;
            preChangeCoord = (x | (y << 12) | (z << 24)) + 1; // +1 so 0 = "no snapshot"
            preChangeView  = SurvivalGrowth.ViewAt(lvl, x, y, z);
        }

        public static void OnBlockChanged(Player p, ushort x, ushort y, ushort z, ChangeResult result) {
            Level lvl = p.level;
            if (lvl == null || lvl.Config.SurvivalMode != SurvivalMode.Indev) return;
            if (result == ChangeResult.Unchanged) return;
            ushort now = SurvivalGrowth.ViewAt(lvl, x, y, z);
            ushort oldV = Block.Air;
            if (preChangeCoord == (x | (y << 12) | (z << 24)) + 1) oldV = preChangeView;
            preChangeCoord = 0;
            Notify(lvl, x, y, z, oldV, now, true); // player edit = setBlockWithNotify class
            // a player edit may change the light (torch placed/mined, roof opened)
            SurvivalGrowth.MarkLightDirty(lvl);
        }

        // BlockStationary.onNeighborBlockChange, synchronous and verbatim:
        // canFlow scan first (down, -x, +x, -z, +z), then petrify when the block
        // that just CHANGED is the opposite MATERIAL (early return - petrify
        // happens INSTEAD of the wake), then the flammable-changed wake (both
        // liquids, keyed on the changed id), then the wake itself: a genuine
        // setTileNoUpdate flip to the moving id plus an explicit
        // scheduleBlockUpdate at the fluid's tickRate.
        static void NotifyNeighbour(LevelPhys lp, Level lvl, int x, int y, int z, ushort changedV) {
            if (!In(lvl, x, y, z)) return;
            ushort b = View(lvl, x, y, z);
            if (b == FIRE) {
                // BlockFire.onNeighborBlockChange, synchronous and schedule-free:
                // an unsupported fire is removed immediately; a supported one just
                // keeps its own update chain. (This used to enqueue an extra 1s
                // entry instead, so a fire outlived its mined support and burned
                // through extra updateTicks genuine never runs.)
                if (!NormalCube(lvl, x, y - 1, z) && !CanNeighbourCatch(lvl, x, y, z))
                    SetFire(lvl, x, y, z, Block.Air);
                return;
            }
            if (b != Block.StillWater && b != Block.StillLava) return;

            bool water = b == Block.StillWater;
            bool wake  = CanFlowInto(lvl, water, x, y - 1, z) ||
                         CanFlowInto(lvl, water, x - 1, y, z) || CanFlowInto(lvl, water, x + 1, y, z) ||
                         CanFlowInto(lvl, water, x, y, z - 1) || CanFlowInto(lvl, water, x, y, z + 1);

            if (changedV != Block.Air &&
                (water ? IsLavaMat(changedV) : IsWaterMat(changedV))) {
                Set(lvl, x, y, z, Block.Stone);
                return;
            }

            if (FireChance(changedV) > 0) wake = true;

            if (wake) {
                ushort moving = water ? (ushort)Block.Water : (ushort)Block.Lava;
                Set(lvl, x, y, z, moving); // stateFlip class: notifies nobody
                ScheduleUpdate(lp, Pack(lvl, x, y, z), moving);
            }
        }


        // ==================== per-tick driver ====================

        // World.scheduleBlockUpdate: delay = tickRate of the SCHEDULED id -
        // fire 20, moving lava 25, moving water (and every other id) 5. The
        // entry then costs one pop per tick while counting down, so the
        // effective period is tickRate+1 (water 6, lava 26, fire 21).
        static int TickRateOf(ushort id) {
            if (id == FIRE) return 20;        // BlockFire.tickRate
            if (id == Block.Lava) return 25;  // BlockFlowing.tickRate (lava)
            return 5;                          // water + Block default
        }

        static void ScheduleUpdate(LevelPhys lp, int index, ushort blockId) {
            if (lp.TickList.Count >= TICK_SCHED_MAX) return; // runaway backstop only
            lp.TickList.Enqueue(new TickEntry { Index = index, BlockId = blockId, Time = TickRateOf(blockId) });
        }

        /// <summary> One 20 TPS physics tick: genuine World.tick's scheduled pass.
        /// Snapshot min(size, 200) BEFORE draining, pop the head that many times;
        /// a counting-down entry decrements and requeues at the tail, a due entry
        /// runs its block's updateTick only if the recorded id still matches the
        /// world (stale entries drop silently). Entries scheduled DURING the pass
        /// land beyond the snapshot and are first touched next tick - that is what
        /// makes the effective fluid period 6/26, not 5/25. There is no load-time
        /// scan: a freshly loaded map's fire and suspended moving fluid wake via
        /// the random tickOnLoad pass (SurvivalGrowth's random ticks), exactly as
        /// genuine setTickOnLoad works. </summary>
        public static void Tick(Level lvl) {
            LevelPhys lp = Get(lvl, true);
            lock (lp) { // serialize against player-thread Notify (see Notify)
                int n = Math.Min(lp.TickList.Count, 200); // genuine World.tick cap
                for (int i = 0; i < n; i++)
                {
                    TickEntry e = lp.TickList.Dequeue();
                    if (e.Time > 0) {
                        e.Time--;
                        if (lp.TickList.Count < TICK_SCHED_MAX) lp.TickList.Enqueue(e);
                        continue;
                    }
                    int x, y, z; Unpack(lvl, e.Index, out x, out y, out z);
                    if (!In(lvl, x, y, z)) continue;
                    ushort b = View(lvl, x, y, z);
                    if (b != e.BlockId) continue; // genuine stale-id skip

                    if (b == FIRE)                                FireUpdate(lp, lvl, x, y, z);
                    else if (b == Block.Water || b == Block.Lava) FluidUpdate(lp, lvl, e.Index, b);
                    else if (b == WATER_SRC || b == LAVA_SRC)     RandomTickSource(lvl, x, y, z, b);
                }
            }
        }


        // ==================== fire ====================

        static void EnsureFireAges(LevelPhys lp, Level lvl) {
            int vol = lvl.Width * lvl.Height * lvl.Length;
            if (lp.FireAge == null || lp.FireVol != vol) { lp.FireAge = new byte[vol]; lp.FireVol = vol; }
        }
        static int GetAge(LevelPhys lp, Level lvl, int index) {
            EnsureFireAges(lp, lvl);
            return (index >= 0 && index < lp.FireVol) ? lp.FireAge[index] : 0;
        }
        static void SetAge(LevelPhys lp, Level lvl, int index, int age) {
            EnsureFireAges(lp, lvl);
            if (index >= 0 && index < lp.FireVol) lp.FireAge[index] = (byte)(age & 15);
        }

        /// <summary> Sidecar: one "fire x y z age" line per mid-burn fire cell.
        /// Genuine keeps fire age in the level's data nibble array, which is part
        /// of the saved level - a reloaded fire resumes its 16-stage burn where it
        /// stopped instead of restarting the full ~16s from age 0. </summary>
        internal static void SaveFireAges(Level lvl, System.IO.TextWriter w) {
            LevelPhys lp = Get(lvl, false);
            if (lp == null) return;
            lock (lp) {
                if (lp.FireAge == null || lp.FireVol != lvl.Width * lvl.Height * lvl.Length) return;
                for (int i = 0; i < lp.FireVol; i++)
                {
                    if (lp.FireAge[i] == 0) continue;
                    int x, y, z; Unpack(lvl, i, out x, out y, out z);
                    if (View(lvl, x, y, z) != FIRE) continue; // stale age, id persists on its own
                    w.WriteLine("fire " + x + " " + y + " " + z + " " + lp.FireAge[i]);
                }
            }
        }

        internal static void RestoreFireAge(Level lvl, string[] p) {
            if (p.Length < 5) return;
            int x, y, z, age;
            if (!int.TryParse(p[1], out x) || !int.TryParse(p[2], out y) ||
                !int.TryParse(p[3], out z) || !int.TryParse(p[4], out age)) return;
            if (!In(lvl, x, y, z)) return;
            LevelPhys lp = Get(lvl, true);
            lock (lp) {
                if (View(lvl, x, y, z) != FIRE) return;
                SetAge(lp, lvl, Pack(lvl, x, y, z), age);
                // Fire is tickOnLoad in genuine: the random pass finds the cell
                // and its updateTick re-enters the scheduled chain - only the age
                // was memory-only, so only the age needed restoring.
            }
        }

        internal static void RandomTickFire(Level lvl, int x, int y, int z) {
            LevelPhys lp = Get(lvl, true);
            if (View(lvl, x, y, z) != FIRE) return;
            lock (lp) FireUpdate(lp, lvl, x, y, z); // vs player-thread Notify
        }

        static readonly byte[] fireChance  = BuildFireTable(true);
        static readonly byte[] fireAbility = BuildFireTable(false);
        static byte[] BuildFireTable(bool chance) {
            byte[] t = new byte[256];
            // BlockFire.setBurnRate(chance, ability)
            t[Block.Wood]      = chance ? (byte)5  : (byte)20;
            t[Block.Log]       = chance ? (byte)5  : (byte)5;
            t[Block.Leaves]    = chance ? (byte)30 : (byte)60;
            t[Block.Bookshelf] = chance ? (byte)30 : (byte)20;
            t[Block.TNT]       = chance ? (byte)15 : (byte)100;
            for (int i = Block.Red; i <= Block.White; i++) t[i] = chance ? (byte)30 : (byte)60; // cloth 21-36
            return t;
        }
        static int FireChance(ushort b)  { return b < 256 ? fireChance[b]  : 0; }
        static int FireAbility(ushort b) { return b < 256 ? fireAbility[b] : 0; }
        static bool CanCatch(ushort b)   { return FireChance(b) > 0; }

        static bool CanBlockCatch(Level lvl, int x, int y, int z) {
            return In(lvl, x, y, z) && CanCatch(View(lvl, x, y, z));
        }
        static bool CanNeighbourCatch(Level lvl, int x, int y, int z) {
            return CanBlockCatch(lvl, x + 1, y, z) || CanBlockCatch(lvl, x - 1, y, z)
                || CanBlockCatch(lvl, x, y - 1, z) || CanBlockCatch(lvl, x, y + 1, z)
                || CanBlockCatch(lvl, x, y, z - 1) || CanBlockCatch(lvl, x, y, z + 1);
        }
        static int EncourageChance(Level lvl, int x, int y, int z, int cur) {
            int c = In(lvl, x, y, z) ? FireChance(View(lvl, x, y, z)) : 0;
            return c > cur ? c : cur;
        }

        const float TNT_BLAST_RADIUS = 4.0f; // c0.30/Indev PrimedTnt.explode radius

        // BlockFire.tryToCatchBlockOnFire: rand(bound) < ability consumes the block
        // (half fire, half air); caught TNT is removed and primed as a full-fuse
        // entity (BlockTNT.onBlockDestroyedByPlayer) rather than blowing instantly.
        static void FireTryCatch(LevelPhys lp, Level lvl, int x, int y, int z, int bound) {
            if (!In(lvl, x, y, z)) return;
            ushort b = View(lvl, x, y, z);
            int ability = FireAbility(b);
            if (lp.Rng.Next(bound) >= ability) return;
            if (b == Block.TNT) {
                // genuine rolls the SAME 50/50 fire-or-air for a caught TNT
                // before priming it - half the time the cell keeps burning
                SetFire(lvl, x, y, z, lp.Rng.Next(2) == 0 ? FIRE : (ushort)Block.Air);
                SurvivalTnt.Ignite(lvl, x, y, z, SurvivalTnt.DefaultFuse(lvl));
                return;
            }
            SetFire(lvl, x, y, z, lp.Rng.Next(2) == 0 ? FIRE : (ushort)Block.Air);
        }

        // BlockFire.updateTick, verbatim.
        static void FireUpdate(LevelPhys lp, Level lvl, int x, int y, int z) {
            int index = Pack(lvl, x, y, z);
            int meta = GetAge(lp, lvl, index);

            if (meta < 15) { SetAge(lp, lvl, index, meta + 1); ScheduleUpdate(lp, index, FIRE); }

            if (!CanNeighbourCatch(lvl, x, y, z)) {
                if (!NormalCube(lvl, x, y - 1, z) || meta > 3) SetFire(lvl, x, y, z, Block.Air);
                return;
            }
            if (!CanBlockCatch(lvl, x, y - 1, z) && meta == 15 && lp.Rng.Next(4) == 0) {
                SetFire(lvl, x, y, z, Block.Air);
                return;
            }
            if (meta % 5 != 0 || meta <= 5) return;

            FireTryCatch(lp, lvl, x + 1, y, z, 300);
            FireTryCatch(lp, lvl, x - 1, y, z, 300);
            FireTryCatch(lp, lvl, x, y - 1, z, 100);
            FireTryCatch(lp, lvl, x, y + 1, z, 200);
            FireTryCatch(lp, lvl, x, y, z - 1, 300);
            FireTryCatch(lp, lvl, x, y, z + 1, 300);

            for (int xx = x - 1; xx <= x + 1; xx++)
                for (int zz = z - 1; zz <= z + 1; zz++)
                    for (int yy = y - 1; yy <= y + 4; yy++)
                    {
                        if (xx == x && yy == y && zz == z) continue;
                        int bound = 100;
                        if (yy > y + 1) bound += (yy - (y + 1)) * 100;
                        if (!In(lvl, xx, yy, zz) || View(lvl, xx, yy, zz) != Block.Air) continue;

                        int chance = EncourageChance(lvl, xx + 1, yy, zz, 0);
                        chance = EncourageChance(lvl, xx - 1, yy, zz, chance);
                        chance = EncourageChance(lvl, xx, yy - 1, zz, chance);
                        chance = EncourageChance(lvl, xx, yy + 1, zz, chance);
                        chance = EncourageChance(lvl, xx, yy, zz - 1, chance);
                        chance = EncourageChance(lvl, xx, yy, zz + 1, chance);
                        if (chance > 0 && lp.Rng.Next(bound) <= chance) SetFire(lvl, xx, yy, zz, FIRE);
                    }
        }

        // BlockFire.fireSpread fireCheck: existing fire counts, an air cell becomes
        // fire, anything else fails.
        static bool FireSpreadCheck(Level lvl, int x, int y, int z) {
            if (!In(lvl, x, y, z)) return false;
            ushort b = View(lvl, x, y, z);
            if (b == FIRE) return true;
            if (b != Block.Air) return false;
            SetFire(lvl, x, y, z, FIRE);
            return true;
        }

        // Lava flowing next to a flammable block lights the first free spot around
        // it (up, -x, +x, -z, +z, below) or turns the block itself to fire.
        static bool LavaFlowInto(Level lvl, int x, int y, int z) {
            if (!In(lvl, x, y, z) || !CanCatch(View(lvl, x, y, z))) return false;
            bool lit = FireSpreadCheck(lvl, x, y + 1, z)
                    || FireSpreadCheck(lvl, x - 1, y, z) || FireSpreadCheck(lvl, x + 1, y, z)
                    || FireSpreadCheck(lvl, x, y, z - 1) || FireSpreadCheck(lvl, x, y, z + 1)
                    || FireSpreadCheck(lvl, x, y - 1, z);
            if (!lit) SetFire(lvl, x, y, z, FIRE);
            return true;
        }


        // ==================== finite fluids ====================

        internal static void RandomTickFluid(Level lvl, int x, int y, int z, ushort v) {
            LevelPhys lp = Get(lvl, true);
            lock (lp) FluidUpdate(lp, lvl, Pack(lvl, x, y, z), v); // vs player-thread Notify
        }

        // BlockSource.updateTick (== the onBlockAdded fill): an infinite spring -
        // fill each of the 4 horizontal air neighbours with the flowing fluid
        // (which then flows via the fluid physics). The source itself just sits
        // and refills; genuine order -x, +x, -z, +z, air cells only.
        internal static void RandomTickSource(Level lvl, int x, int y, int z, ushort v) {
            ushort fluid = v == LAVA_SRC ? (ushort)Block.Lava : (ushort)Block.Water;
            if (In(lvl, x - 1, y, z) && View(lvl, x - 1, y, z) == Block.Air) Set(lvl, x - 1, y, z, fluid);
            if (In(lvl, x + 1, y, z) && View(lvl, x + 1, y, z) == Block.Air) Set(lvl, x + 1, y, z, fluid);
            if (In(lvl, x, y, z - 1) && View(lvl, x, y, z - 1) == Block.Air) Set(lvl, x, y, z - 1, fluid);
            if (In(lvl, x, y, z + 1) && View(lvl, x, y, z + 1) == Block.Air) Set(lvl, x, y, z + 1, fluid);
        }

        // BlockFluid.canFlow: target must be air or a non-colliding non-liquid
        // (plants/torch/fire/gears - flowing over them destroys them); water also
        // refuses within 2 blocks of a sponge.
        static bool CanFlowInto(Level lvl, bool water, int x, int y, int z) {
            if (!In(lvl, x, y, z)) return false;
            ushort b = View(lvl, x, y, z);
            if (b != Block.Air && lvl.CollideType(lvl.GetBlock((ushort)x, (ushort)y, (ushort)z)) != CollideType.WalkThrough)
                return false;
            if (water) {
                for (int sx = x - 2; sx <= x + 2; sx++)
                    for (int sy = y - 2; sy <= y + 2; sy++)
                        for (int sz = z - 2; sz <= z + 2; sz++)
                            if (In(lvl, sx, sy, sz) && View(lvl, sx, sy, sz) == Block.Sponge) return false;
            }
            return true;
        }

        // --- flood-fill scratch (genuine x + (z<<10) layer packing) ---
        static bool EnsureScratch(LevelPhys lp, Level lvl) {
            int stamps = 1024 * lvl.Length;
            int cap = lvl.Width * lvl.Length + 64;
            if (lp.Stamps != null && lp.StampsLen == stamps && lp.StackCap == cap) return true;
            lp.StampsLen = stamps; lp.StackCap = cap; lp.Counter = 0;
            lp.Stamps = new ushort[stamps];
            lp.StackA = new int[cap];
            lp.StackB = new int[cap];
            return true;
        }
        static void BumpCounter(LevelPhys lp) {
            if (++lp.Counter == 30000) { Array.Clear(lp.Stamps, 0, lp.StampsLen); lp.Counter = 1; }
        }

        static bool IsBody(ushort b, ushort moving, ushort still) { return b == moving || b == still; }

        // World.floodFill on one layer: 0 = adjacent air found (can absorb), 1 =
        // fully closed, 2 = touches the border.
        static int FloodFill(LevelPhys lp, Level lvl, int x, int y, int z, ushort moving, ushort still) {
            if (!In(lvl, x, y, z)) return 0;
            EnsureScratch(lp, lvl);
            BumpCounter(lp);
            ushort[] stamps = lp.Stamps; int[] stackA = lp.StackA;
            int W = lvl.Width, L = lvl.Length, H = lvl.Height;
            int top = 0;
            stackA[top++] = x + (z << 10);

            while (top > 0)
            {
                int p2 = stackA[--top];
                if (stamps[p2] == lp.Counter) continue;
                x = p2 & 1023; z = p2 >> 10;
                if (x == 0 || x == W - 1 || y == 0 || y == H - 1 || z == 0 || z == L - 1) return 2;

                while (x > 0 && stamps[p2 - 1] != lp.Counter && IsBody(View(lvl, x - 1, y, z), moving, still)) { x--; p2--; }
                if (x > 0 && View(lvl, x - 1, y, z) == Block.Air) return 0;

                bool spanN = false, spanS = false;
                for (; x < W && stamps[p2] != lp.Counter && IsBody(View(lvl, x, y, z), moving, still); x++, p2++)
                {
                    if (x == 0 || x == W - 1) return 2;
                    if (z > 0) {
                        ushort b = View(lvl, x, y, z - 1);
                        if (b == Block.Air) return 0;
                        bool match = stamps[p2 - 1024] != lp.Counter && IsBody(b, moving, still);
                        if (match && !spanN && top < lp.StackCap) stackA[top++] = p2 - 1024;
                        spanN = match;
                    }
                    if (z < L - 1) {
                        ushort b = View(lvl, x, y, z + 1);
                        if (b == Block.Air) return 0;
                        bool match = stamps[p2 + 1024] != lp.Counter && IsBody(b, moving, still);
                        if (match && !spanS && top < lp.StackCap) stackA[top++] = p2 + 1024;
                        spanS = match;
                    }
                    stamps[p2] = lp.Counter;
                }
                if (x < W && View(lvl, x, y, z) == Block.Air) return 0;
            }
            return 1;
        }

        // World.fluidFlowCheck: walk the body upward for a donor cell to pull
        // volume from. Returns packed ((y<<10|z)<<10)|x of the farthest cell on the
        // highest layer, -9999 when the body touches a SOURCE (infinite), or -1 OOB.
        static int FlowCheck(LevelPhys lp, Level lvl, int x, int y, int z, ushort moving, ushort still) {
            if (!In(lvl, x, y, z)) return -1;
            EnsureScratch(lp, lvl);
            int ox = x, oz = z, W = lvl.Width, L = lvl.Length, H = lvl.Height;
            ushort source = moving == Block.Water ? WATER_SRC : LAVA_SRC;
            int donor = ((y << 10 | z) << 10) | x;
            bool sourced = false;
            int[] stackA = lp.StackA, stackB = lp.StackB;
            ushort[] stamps = lp.Stamps;
            int top = 0;
            stackA[top++] = x + (z << 10);

            for (; y < H; y++)
            {
                int best = -1, upTop = 0;
                // genuine resets the source flag PER LAYER (World.fluidFlowCheck:
                // "var12 = false" tops each do-iteration and only the LAST layer's
                // value reaches the -9999 return) - a spring buried in a lower
                // layer does NOT make spreading free; only a spring adjacent to
                // the topmost walked layer does
                sourced = false;
                BumpCounter(lp);

                while (top > 0)
                {
                    int p2 = stackA[--top];
                    if (stamps[p2] == lp.Counter) continue;
                    x = p2 & 1023; z = p2 >> 10;
                    int dz2 = (z - oz) * (z - oz);

                    while (x > 0 && stamps[p2 - 1] != lp.Counter && IsBody(View(lvl, x - 1, y, z), moving, still)) { x--; p2--; }
                    if (x > 0 && View(lvl, x - 1, y, z) == source) sourced = true;

                    bool spanN = false, spanS = false, spanUp = false;
                    for (; x < W && stamps[p2] != lp.Counter && IsBody(View(lvl, x, y, z), moving, still); x++, p2++)
                    {
                        if (z > 0) {
                            ushort b = View(lvl, x, y, z - 1);
                            if (b == source) sourced = true;
                            bool match = stamps[p2 - 1024] != lp.Counter && IsBody(b, moving, still);
                            if (match && !spanN && top < lp.StackCap) stackA[top++] = p2 - 1024;
                            spanN = match;
                        }
                        if (z < L - 1) {
                            ushort b = View(lvl, x, y, z + 1);
                            if (b == source) sourced = true;
                            bool match = stamps[p2 + 1024] != lp.Counter && IsBody(b, moving, still);
                            if (match && !spanS && top < lp.StackCap) stackA[top++] = p2 + 1024;
                            spanS = match;
                        }
                        if (y < H - 1) {
                            bool match = IsBody(View(lvl, x, y + 1, z), moving, still);
                            if (match && !spanUp && upTop < lp.StackCap) stackB[upTop++] = p2;
                            spanUp = match;
                        }
                        int d = (x - ox) * (x - ox) + dz2;
                        if (d > best) { best = d; donor = ((y << 10 | z) << 10) | x; }
                        stamps[p2] = lp.Counter;
                    }
                    if (x < W && View(lvl, x, y, z) == source) sourced = true;
                }

                if (upTop == 0) break;
                int[] tmp = stackA; stackA = stackB; stackB = tmp;
                lp.StackA = stackA; lp.StackB = stackB;
                top = upTop;
            }
            return sourced ? -9999 : donor;
        }

        // BlockFlowing.liquidSpread: plain (stagnation-retry) spread. Genuine
        // pairs the setBlockWithNotify with an explicit scheduleBlockUpdate,
        // ON TOP of the onBlockAdded schedule the write itself runs - a spread
        // cell genuinely enters the tick list twice (the duplicate is cheap:
        // whichever runs second either re-runs a still-moving cell or is
        // dropped by the stale-id check once the cell settles).
        static bool FluidSpread(LevelPhys lp, Level lvl, ushort moving, bool water, int tx, int ty, int tz) {
            if (!CanFlowInto(lvl, water, tx, ty, tz)) return false;
            Set(lvl, tx, ty, tz, moving);
            ScheduleUpdate(lp, Pack(lvl, tx, ty, tz), moving);
            return true;
        }

        // BlockFlowing.liquidSpread2: spread + donor removal (volume conservation).
        static bool FluidSpread2(LevelPhys lp, Level lvl, int x, int y, int z, ushort moving, ushort still,
                                 bool water, int tx, int ty, int tz) {
            if (!CanFlowInto(lvl, water, tx, ty, tz)) return false;

            int r = FlowCheck(lp, lvl, x, y, z, moving, still);
            if (r != -9999) {
                if (r < 0) return false;
                int dx = r & 1023; r >>= 10;
                int dz = r & 1023; r >>= 10;
                int dy = r & 1023;
                if ((dy > ty || !CanFlowInto(lvl, water, tx, ty - 1, tz)) && dy <= ty &&
                    dx != 0 && dx != lvl.Width - 1 && dz != 0 && dz != lvl.Length - 1) {
                    return false;
                }
                Set(lvl, dx, dy, dz, Block.Air);
            }
            Set(lvl, tx, ty, tz, moving);
            ScheduleUpdate(lp, Pack(lvl, tx, ty, tz), moving); // genuine explicit schedule (see FluidSpread)
            return true;
        }

        // water side effects: extinguish adjacent fire, petrify adjacent lava.
        static bool WaterContact(Level lvl, int x, int y, int z) {
            if (!In(lvl, x, y, z)) return false;
            ushort b = View(lvl, x, y, z);
            if (b == FIRE) { Set(lvl, x, y, z, Block.Air); return true; }
            if (b == Block.Lava || b == Block.StillLava) { Set(lvl, x, y, z, Block.Stone); return true; }
            return false;
        }

        // BlockFlowing.update - the whole genuine driver.
        static void FluidUpdate(LevelPhys lp, Level lvl, int index, ushort block) {
            int x, y, z; Unpack(lvl, index, out x, out y, out z);
            bool water = block == Block.Water;
            ushort moving = water ? (ushort)Block.Water : (ushort)Block.Lava;
            ushort still  = water ? (ushort)Block.StillWater : (ushort)Block.StillLava;

            bool canSide = CanFlowInto(lvl, water, x - 1, y, z) || CanFlowInto(lvl, water, x + 1, y, z) ||
                           CanFlowInto(lvl, water, x, y, z - 1) || CanFlowInto(lvl, water, x, y, z + 1);

            ushort below = y > 0 ? View(lvl, x, y - 1, z) : (ushort)Block.Air;
            // getBlockMaterial(below) == this.material: for water this includes
            // BOTH springs (each is Material.water - the genuine quirk means a
            // lava spring below flowing water also selects the equalize path)
            if (canSide && y > 0 && (water ? IsWaterMat(below) : IsLavaMat(below))) {
                if (FloodFill(lp, lvl, x, y - 1, z, moving, still) == 1) {
                    int r = FlowCheck(lp, lvl, x, y, z, moving, still);
                    if (r != -9999) {
                        if (r < 0) return;
                        int dx = r & 1023; r >>= 10;
                        int dz = r & 1023; r >>= 10;
                        int dy = r & 1023;
                        Set(lvl, dx, dy, dz, Block.Air);
                    }
                    return;
                }
            }

            bool spread = FluidSpread2(lp, lvl, x, y, z, moving, still, water, x, y - 1, z);

            // one random horizontal direction per update (partial Fisher-Yates)
            for (int i = 0; i < 4; i++)
            {
                int j = lp.Rng.Next(4 - i) + i;
                int t = lp.LiquidOrder[i]; lp.LiquidOrder[i] = lp.LiquidOrder[j]; lp.LiquidOrder[j] = t;
                int dir = lp.LiquidOrder[i];
                if (!spread) {
                    if (dir == 0) spread = FluidSpread2(lp, lvl, x, y, z, moving, still, water, x - 1, y, z);
                    else if (dir == 1) spread = FluidSpread2(lp, lvl, x, y, z, moving, still, water, x + 1, y, z);
                    else if (dir == 2) spread = FluidSpread2(lp, lvl, x, y, z, moving, still, water, x, y, z - 1);
                    else if (dir == 3) spread = FluidSpread2(lp, lvl, x, y, z, moving, still, water, x, y, z + 1);
                }
            }

            if (!spread && canSide) {
                if (lp.Rng.Next(3) == 0) {
                    if (lp.Rng.Next(3) == 0) {
                        for (int i = 0; i < 4; i++)
                        {
                            int j = lp.Rng.Next(4 - i) + i;
                            int t = lp.LiquidOrder[i]; lp.LiquidOrder[i] = lp.LiquidOrder[j]; lp.LiquidOrder[j] = t;
                            int dir = lp.LiquidOrder[i];
                            if (!spread) {
                                if (dir == 0) spread = FluidSpread(lp, lvl, moving, water, x - 1, y, z);
                                else if (dir == 1) spread = FluidSpread(lp, lvl, moving, water, x + 1, y, z);
                                else if (dir == 2) spread = FluidSpread(lp, lvl, moving, water, x, y, z - 1);
                                else if (dir == 3) spread = FluidSpread(lp, lvl, moving, water, x, y, z + 1);
                            }
                        }
                    } else if (!water) {
                        Set(lvl, x, y, z, Block.Stone);
                    } else {
                        Set(lvl, x, y, z, Block.Air);
                    }
                }
                return;
            }

            if (water) {
                spread |= WaterContact(lvl, x - 1, y, z);
                spread |= WaterContact(lvl, x + 1, y, z);
                spread |= WaterContact(lvl, x, y, z - 1);
                spread |= WaterContact(lvl, x, y, z + 1);
            } else {
                spread |= LavaFlowInto(lvl, x - 1, y, z);
                spread |= LavaFlowInto(lvl, x + 1, y, z);
                spread |= LavaFlowInto(lvl, x, y, z - 1);
                spread |= LavaFlowInto(lvl, x, y, z + 1);
            }

            if (!spread) {
                Set(lvl, x, y, z, still); // settled: become still (setTileNoUpdate)
            } else {
                ScheduleUpdate(lp, index, block); // keep flowing at tickRate
            }
        }
    }
}
