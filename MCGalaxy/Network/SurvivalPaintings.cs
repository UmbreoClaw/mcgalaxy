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

namespace MCGalaxy.Network
{
    /// <summary> Server-authoritative Indev paintings (in-20100223 EntityPainting +
    /// ItemPainting), the MP counterpart of the client's local painting pool. The
    /// geometry is the client's port verbatim (in doubles): the plane hangs 1/16 in
    /// front of the clicked wall block, the 0.5 re-centring for 32px+ art, the MAX
    /// corner shrunk by 0.1/16. Placement rides the normal SURV_USE_ITEM intent
    /// with the painting item held; a hit (SURV_ATTACK targetKind 3) pops it off
    /// as a drop entity. Streamed as SURV_PAINT_SPAWN / SURV_PAINT_REMOVE and
    /// persisted in the level sidecar as "paint x y z dir art" lines.
    ///
    /// Divergence note: the client's placement validation tests exact block
    /// bounding boxes in front of the painting; the server tests at CELL
    /// granularity (any solid-collide block whose cell intersects the painting
    /// box blocks it). Identical for full blocks - partial blocks (slabs) in the
    /// front cells reject slightly more often than genuine.
    ///
    /// Genuine quirk kept: the "still on a wall?" check runs ONCE, at
    /// tickCounter == 100 - a painting whose wall is mined later stays floating
    /// (punch it to pop it). </summary>
    public static class SurvivalPaintings
    {
        public const ushort ITEM_PAINTING = 256 + 65;

        // Art sizes in px, mirroring the client's paintingArts table order exactly
        // (art ids on the wire index into both). 16px = 1 block cell.
        static readonly byte[] ArtW = { 16,16,16,16,16,16,16, 32,32,32,32, 16, 32,32,32,32,32, 64,64 };
        static readonly byte[] ArtH = { 16,16,16,16,16,16,16, 16,16,16,16, 32, 32,32,32,32,32, 32,64 };

        class Painting
        {
            public int  Id;
            public int  TileX, TileY, TileZ;
            public byte Dir, Art;      // dir 0..3 (yaw = dir * 90)
            public double X, Y, Z;     // genuine posX/Y/Z - the painting centre
            public double MinX, MinY, MinZ, MaxX, MaxY, MaxZ;
            public int  TickCounter;
        }

        class LevelPaintings
        {
            public List<Painting> Items = new List<Painting>();
            public int NextId = 1;
        }

        static readonly object regLock = new object();
        static readonly Dictionary<Level, LevelPaintings> registry = new Dictionary<Level, LevelPaintings>();

        static LevelPaintings GetLevel(Level lvl, bool create) {
            lock (regLock) {
                LevelPaintings lp;
                if (registry.TryGetValue(lvl, out lp)) return lp;
                if (!create) return null;
                lp = new LevelPaintings();
                registry[lvl] = lp;
                return lp;
            }
        }

        /// <summary> Whether a level has any live paintings (the sidecar's
        /// no-clobber guard, alongside mobs + containers). </summary>
        public static bool HasPaintings(Level lvl) {
            LevelPaintings lp = GetLevel(lvl, false);
            if (lp == null) return false;
            lock (lp.Items) return lp.Items.Count > 0;
        }

        /// <summary> Drops registries of levels that are no longer loaded (the
        /// standard Level-keyed registry prune, called from the survival tick;
        /// the sidecar was already written by the persistence save). </summary>
        public static void Prune(Level[] loaded) {
            lock (regLock) {
                List<Level> dead = null;
                foreach (KeyValuePair<Level, LevelPaintings> kvp in registry)
                {
                    if (Array.IndexOf(loaded, kvp.Key) < 0) {
                        if (dead == null) dead = new List<Level>();
                        dead.Add(kvp.Key);
                    }
                }
                if (dead != null) foreach (Level lvl in dead) registry.Remove(lvl);
            }
        }


        // ==================== geometry (the client's port, in doubles) ====================

        // EntityPainting.getArtSize: 0.5 for BOTH 32px and 64px - the genuine
        // source of the off-centre large paintings
        static double ArtSize(int px) { return px >= 32 ? 0.5 : 0.0; }

        static void SetDirection(Painting pt) {
            int aw = ArtW[pt.Art], ah = ArtH[pt.Art];
            double w2 = aw, h2 = ah, d2 = aw;
            if (pt.Dir != 0 && pt.Dir != 2) w2 = 0.5; else d2 = 0.5;
            w2 /= 32.0; h2 /= 32.0; d2 /= 32.0;

            double x = pt.TileX + 0.5, y = pt.TileY + 0.5, z = pt.TileZ + 0.5;
            if (pt.Dir == 0) z -= 9.0 / 16.0;
            if (pt.Dir == 1) x -= 9.0 / 16.0;
            if (pt.Dir == 2) z += 9.0 / 16.0;
            if (pt.Dir == 3) x += 9.0 / 16.0;

            if (pt.Dir == 0) x -= ArtSize(aw);
            if (pt.Dir == 1) z += ArtSize(aw);
            if (pt.Dir == 2) x += ArtSize(aw);
            if (pt.Dir == 3) z -= ArtSize(aw);
            y += ArtSize(ah);

            pt.X = x; pt.Y = y; pt.Z = z;
            pt.MinX = x - w2; pt.MinY = y - h2; pt.MinZ = z - d2;
            // genuine shrinks only the MAX corner by 0.1/16
            pt.MaxX = x + w2 - 0.1 / 16.0;
            pt.MaxY = y + h2 - 0.1 / 16.0;
            pt.MaxZ = z + d2 - 0.1 / 16.0;
        }

        static bool SolidAt(Level lvl, int x, int y, int z) {
            if (x < 0 || y < 0 || z < 0 || x >= lvl.Width || y >= lvl.Height || z >= lvl.Length) return false;
            return CollideType.IsSolid(lvl.CollideType(lvl.GetBlock((ushort)x, (ushort)y, (ushort)z)));
        }

        static bool Intersects(Painting a, Painting b) {
            return a.MinX < b.MaxX && a.MaxX > b.MinX &&
                   a.MinY < b.MaxY && a.MaxY > b.MinY &&
                   a.MinZ < b.MaxZ && a.MaxZ > b.MinZ;
        }

        // EntityPainting.onValidSurface (cell-granular front check - see the
        // divergence note in the class remarks). Caller holds lp.Items' lock.
        static bool ValidSurface(Level lvl, LevelPaintings lp, Painting pt) {
            // 1. no solid block cells intersecting the painting box
            for (int y = (int)pt.MinY; y <= (int)pt.MaxY; y++)
                for (int z = (int)pt.MinZ; z <= (int)pt.MaxZ; z++)
                    for (int x = (int)pt.MinX; x <= (int)pt.MaxX; x++)
                        if (SolidAt(lvl, x, y, z)) return false;

            // 2. every 16px cell must be backed by a solid wall block
            int cellsX = ArtW[pt.Art] / 16, cellsY = ArtH[pt.Art] / 16;
            int bx = pt.TileX, bz = pt.TileZ;
            if (pt.Dir == 0 || pt.Dir == 2) bx = (int)(pt.X - ArtW[pt.Art] / 32.0);
            else                            bz = (int)(pt.Z - ArtW[pt.Art] / 32.0);
            int by = (int)(pt.Y - ArtH[pt.Art] / 32.0);

            for (int i = 0; i < cellsX; i++)
                for (int j = 0; j < cellsY; j++)
                {
                    bool solid = (pt.Dir != 0 && pt.Dir != 2)
                        ? SolidAt(lvl, pt.TileX, by + j, bz + i)
                        : SolidAt(lvl, bx + i, by + j, pt.TileZ);
                    if (!solid) return false;
                }

            // 3. no other painting overlapping
            foreach (Painting o in lp.Items)
            {
                if (o != pt && Intersects(pt, o)) return false;
            }
            return true;
        }


        // ==================== placement (ItemPainting.onItemUse) ====================

        static readonly Random artRng = new Random();

        /// <summary> SURV_USE_ITEM with the painting held: side faces only, interior
        /// wall blocks only; tries every art on this spot and hangs a random one
        /// that fits (consuming the item). Returns whether the use was a painting
        /// use at all - like genuine, a side-face click is consumed even when no
        /// art fits (nothing is placed or consumed then). </summary>
        public static bool UsePainting(Player p, Level lvl, ushort heldId, int x, int y, int z, int face,
                                       out bool placed, out int consumeHeld) {
            placed = false; consumeHeld = 0;
            if (heldId != ITEM_PAINTING) return false;

            int dir;
            switch (face) {              // Constants.h FACE_*: XMIN0 XMAX1 ZMIN2 ZMAX3
                case 2:  dir = 0; break;
                case 0:  dir = 1; break;
                case 3:  dir = 2; break;
                case 1:  dir = 3; break;
                default: return false;   // genuine rejects floors/ceilings
            }
            if (!(x > 0 && y > 0 && z > 0 &&
                  x < lvl.Width - 1 && y < lvl.Height - 1 && z < lvl.Length - 1)) return false;

            LevelPaintings lp = GetLevel(lvl, true);
            Painting pt = new Painting();
            pt.Dir = (byte)dir; pt.TileX = x; pt.TileY = y; pt.TileZ = z;

            lock (lp.Items) {
                List<byte> valid = new List<byte>();
                for (byte i = 0; i < ArtW.Length; i++)
                {
                    pt.Art = i;
                    SetDirection(pt);
                    if (ValidSurface(lvl, lp, pt)) valid.Add(i);
                }
                if (valid.Count == 0) return true; // click consumed, nothing hung

                lock (artRng) pt.Art = valid[artRng.Next(valid.Count)];
                SetDirection(pt);
                pt.Id = lp.NextId++;
                pt.TickCounter = 0;
                lp.Items.Add(pt);
            }

            placed = true; consumeHeld = 1;
            BroadcastSpawn(lvl, pt);
            return true;
        }


        // ==================== hits (EntityPainting.attackEntityFrom) ====================

        /// <summary> SURV_ATTACK targetKind 3: any landed punch pops the painting
        /// off as a drop entity. Reach-validated like every other attack. </summary>
        public static void HandleAttack(Player p, Level lvl, int paintId) {
            LevelPaintings lp = GetLevel(lvl, false);
            if (lp == null) return;

            Painting pt = null;
            lock (lp.Items) {
                foreach (Painting o in lp.Items) { if (o.Id == paintId) { pt = o; break; } }
                if (pt == null) return;

                double dx = p.Pos.X / 32.0 - pt.X, dy = p.Pos.Y / 32.0 - pt.Y, dz = p.Pos.Z / 32.0 - pt.Z;
                if (dx * dx + dy * dy + dz * dz > 6 * 6) return;
                lp.Items.Remove(pt);
            }
            PopOff(lvl, pt);
        }

        static void PopOff(Level lvl, Painting pt) {
            BroadcastRemove(lvl, pt.Id);
            SurvivalDrops.SpawnScatter(lvl, pt.X, pt.Y, pt.Z, ITEM_PAINTING, 1,
                                       SurvivalDrops.MinedDelay(lvl));
        }


        // ==================== tick (the genuine once-at-100 wall check) ====================

        /// <summary> 20 TPS pass: each painting checks its wall exactly once, at
        /// tickCounter == 100, popping off if it isn't valid any more. Called from
        /// the survival mob tick on Indev levels. </summary>
        public static void Tick(Level lvl) {
            LevelPaintings lp = GetLevel(lvl, false);
            if (lp == null) return;

            List<Painting> popped = null;
            lock (lp.Items) {
                for (int i = lp.Items.Count - 1; i >= 0; i--)
                {
                    Painting pt = lp.Items[i];
                    if (pt.TickCounter++ != 100) continue;
                    if (ValidSurface(lvl, lp, pt)) continue;
                    lp.Items.RemoveAt(i);
                    if (popped == null) popped = new List<Painting>();
                    popped.Add(pt);
                }
            }
            if (popped == null) return;
            foreach (Painting pt in popped) PopOff(lvl, pt);
        }


        // ==================== streaming ====================

        static void BroadcastSpawn(Level lvl, Painting pt) {
            Player[] players = PlayerInfo.Online.Items;
            foreach (Player pl in players)
            {
                if (pl.level == lvl && SurvivalNet.Active(pl, lvl))
                    SurvivalNet.SendPaintSpawn(pl, pt.Id, pt.TileX, pt.TileY, pt.TileZ, pt.Dir, pt.Art);
            }
        }

        static void BroadcastRemove(Level lvl, int id) {
            Player[] players = PlayerInfo.Online.Items;
            foreach (Player pl in players)
            {
                if (pl.level == lvl && SurvivalNet.Active(pl, lvl))
                    SurvivalNet.SendPaintRemove(pl, id);
            }
        }

        /// <summary> Streams the level's live paintings to a joining player
        /// (called from SurvivalNet.SendHandshake). </summary>
        public static void SendLevel(Player p, Level lvl) {
            LevelPaintings lp = GetLevel(lvl, false);
            if (lp == null) return;
            lock (lp.Items) {
                foreach (Painting pt in lp.Items)
                    SurvivalNet.SendPaintSpawn(p, pt.Id, pt.TileX, pt.TileY, pt.TileZ, pt.Dir, pt.Art);
            }
        }


        // ==================== sidecar persistence ====================

        /// <summary> Writes "paint x y z dir art" lines (SurvivalPersistence.Save). </summary>
        public static void SavePaintings(Level lvl, System.IO.StreamWriter w) {
            LevelPaintings lp = GetLevel(lvl, false);
            if (lp == null) return;
            lock (lp.Items) {
                foreach (Painting pt in lp.Items)
                    w.WriteLine("paint {0} {1} {2} {3} {4}", pt.TileX, pt.TileY, pt.TileZ, pt.Dir, pt.Art);
            }
        }

        /// <summary> Restores one sidecar "paint" line (SurvivalPersistence.Load). </summary>
        public static void RestorePainting(Level lvl, string[] parts) {
            if (parts.Length < 6) return;
            int x, y, z; byte dir, art;
            if (!int.TryParse(parts[1], out x) || !int.TryParse(parts[2], out y) ||
                !int.TryParse(parts[3], out z) || !byte.TryParse(parts[4], out dir) ||
                !byte.TryParse(parts[5], out art)) return;
            if (dir > 3 || art >= ArtW.Length) return;

            LevelPaintings lp = GetLevel(lvl, true);
            Painting pt = new Painting();
            pt.Dir = dir; pt.Art = art; pt.TileX = x; pt.TileY = y; pt.TileZ = z;
            SetDirection(pt);
            // restored paintings skip the once-at-100 wall check (it already ran
            // in the life they were saved from - matching the genuine quirk)
            pt.TickCounter = 101;
            lock (lp.Items) {
                pt.Id = lp.NextId++;
                lp.Items.Add(pt);
            }
            BroadcastSpawn(lvl, pt);
        }
    }
}
