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

namespace MCGalaxy.Network
{
    /// <summary> /Track: live coordinate readouts for up to three players or
    /// mobs on the tracker's map, shown in the CPE MessageTypes top-right
    /// status lines (Status1-3). Updated from the survival tick at 4 Hz,
    /// resent only on change. Tracking auto-stops - with a chat alert and the
    /// status line cleared - when the target disconnects, dies/despawns, or
    /// ends up on a different map from the tracker. </summary>
    public static class SurvivalTrack
    {
        class Entry {
            public byte   Slot;      // 0-2 -> Status1..Status3
            public Player TargetP;   // player target, or...
            public int    TargetMob; // ...mob id when TargetP is null
            public string MobName;
            public string LastSent;
        }

        // bare /Track arms punch-to-select: the next survival ATTACK within
        // this window picks the punched mob/player instead of hitting it
        const string ARM_KEY   = "survival.trackArm";
        const int    MAX_TRACKS = 3;

        static readonly Dictionary<Player, List<Entry>> tracks = new Dictionary<Player, List<Entry>>();
        static readonly object trackLock = new object();
        static int tickCounter;

        public static void Arm(Player p) {
            p.Extras[ARM_KEY] = DateTime.UtcNow.AddSeconds(15);
            p.Message("&SPunch a mob or player within &b15s &Sto track it.");
        }

        static bool Armed(Player p) {
            object o;
            return p.Extras.TryGet(ARM_KEY, out o) && DateTime.UtcNow < (DateTime)o;
        }

        /// <summary> Consumes an armed tracker's punch: the punched target is
        /// tracked instead of hit. Called from the survival attack paths. </summary>
        public static bool TryConsumePunch(Player p, Player targetP, string mobName, int mobId) {
            if (!Armed(p)) return false;
            p.Extras.Remove(ARM_KEY);
            Add(p, targetP, mobName, mobId);
            return true;
        }

        public static void Add(Player p, Player targetP, string mobName, int mobId) {
            if (!p.Supports(CpeExt.MessageTypes)) {
                p.Message("&WYour client does not support the MessageTypes extension needed for the readout."); return;
            }
            lock (trackLock) {
                List<Entry> list;
                if (!tracks.TryGetValue(p, out list)) { list = new List<Entry>(); tracks[p] = list; }

                foreach (Entry e in list) {
                    bool same = targetP != null ? e.TargetP == targetP
                                                : e.TargetP == null && e.TargetMob == mobId;
                    if (same) { p.Message("&SAlready tracking {0}&S.", Label(e)); return; }
                }
                if (list.Count >= MAX_TRACKS) {
                    p.Message("&WAlready tracking {0} targets - &T/Track <name> stop &Wone first.", MAX_TRACKS); return;
                }

                bool[] used = new bool[MAX_TRACKS];
                foreach (Entry e in list) used[e.Slot] = true;
                byte slot = 0;
                for (byte i = 0; i < MAX_TRACKS; i++) { if (!used[i]) { slot = i; break; } }

                Entry en = new Entry();
                en.Slot = slot; en.TargetP = targetP; en.MobName = mobName; en.TargetMob = mobId;
                list.Add(en);
                p.Message("&STracking {0} &S- live position shown top right.", Label(en));
            }
        }

        static string Label(Entry e) {
            return e.TargetP != null ? e.TargetP.ColoredName : "&b" + e.MobName + " #" + e.TargetMob;
        }

        /// <summary> /Track &lt;name&gt; stop - name matches a tracked player's
        /// name or a mob label ("creeper" / "creeper #12"). </summary>
        public static void Stop(Player p, string name) {
            lock (trackLock) {
                List<Entry> list;
                if (!tracks.TryGetValue(p, out list) || list.Count == 0) {
                    p.Message("&SYou are not tracking anything."); return;
                }
                foreach (Entry e in list) {
                    bool match = e.TargetP != null
                        ? e.TargetP.name.CaselessEq(name)
                        : e.MobName.CaselessEq(name) || (e.MobName + " #" + e.TargetMob).CaselessEq(name);
                    if (!match) continue;
                    StopEntry(p, list, e, null);
                    p.Message("&SStopped tracking {0}&S.", Label(e));
                    return;
                }
                p.Message("&SNot tracking anything matching &b{0}&S.", name);
            }
        }

        public static void StopAll(Player p) {
            lock (trackLock) {
                List<Entry> list;
                if (!tracks.TryGetValue(p, out list) || list.Count == 0) {
                    p.Message("&SYou are not tracking anything."); return;
                }
                while (list.Count > 0) StopEntry(p, list, list[0], null);
                p.Message("&SStopped all tracking.");
            }
        }

        // removes the entry and clears its status line; a non-null reason is
        // the auto-stop chat alert ("disconnected", "left the map", ...)
        static void StopEntry(Player p, List<Entry> list, Entry e, string reason) {
            list.Remove(e);
            if (list.Count == 0) tracks.Remove(p);
            p.SendCpeMessage((CpeMessageType)((byte)CpeMessageType.Status1 + e.Slot), "");
            if (reason != null) p.Message("&SStopped tracking {0} &S({1}).", Label(e), reason);
        }

        /// <summary> Both sides of a disconnect: drop the leaver's own trackers,
        /// and alert anyone who was tracking the leaver. </summary>
        public static void OnPlayerDisconnect(Player p) {
            lock (trackLock) {
                tracks.Remove(p);
                var owners = new List<Player>(tracks.Keys);
                foreach (Player owner in owners) {
                    List<Entry> list = tracks[owner];
                    for (int i = list.Count - 1; i >= 0; i--) {
                        if (list[i].TargetP == p) StopEntry(owner, list, list[i], "disconnected");
                    }
                }
            }
        }

        /// <summary> Refreshes every readout. Called from the survival tick
        /// (20 Hz); updates at 4 Hz and resends a line only when it changed. </summary>
        public static void Tick() {
            tickCounter++;
            if ((tickCounter % 5) != 0) return;

            lock (trackLock) {
                if (tracks.Count == 0) return;
                var owners = new List<Player>(tracks.Keys);
                foreach (Player owner in owners) {
                    List<Entry> list = tracks[owner];
                    for (int i = list.Count - 1; i >= 0; i--) {
                        Entry e = list[i];
                        string line = null;

                        if (e.TargetP != null) {
                            Player t = e.TargetP;
                            if (t.level != owner.level) {
                                StopEntry(owner, list, e, "no longer on your map"); continue;
                            }
                            Maths.Vec3S32 c = t.Pos.FeetBlockCoords;
                            line = string.Format("{0}&f {1}, {2}, {3}", t.ColoredName, c.X, c.Y, c.Z);
                        } else {
                            int x, y, z;
                            if (!SurvivalMobs.TryGetMob(owner.level, e.TargetMob, out x, out y, out z)) {
                                StopEntry(owner, list, e, "gone"); continue;
                            }
                            line = string.Format("&b{0} #{1}&f {2}, {3}, {4}", e.MobName, e.TargetMob, x, y, z);
                        }

                        if (line == e.LastSent) continue;
                        e.LastSent = line;
                        owner.SendCpeMessage((CpeMessageType)((byte)CpeMessageType.Status1 + e.Slot), line);
                    }
                }
            }
        }
    }
}
