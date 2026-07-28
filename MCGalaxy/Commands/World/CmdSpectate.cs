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
using MCGalaxy.Events.PlayerEvents;
using MCGalaxy.Network;

namespace MCGalaxy.Commands.World
{
    /// <summary> Ride a player's camera and watch their survival inventory live,
    /// read-only. A true SPECTATOR state, not a /Follow wrapper: the spectator is
    /// hidden WITHOUT the fake disconnect message /Hide broadcasts, the target is
    /// told they are being watched (unless "silent", which /Hide-style stays a
    /// courtesy the target can't detect), and while spectating the player is
    /// invulnerable, invisible to mob aggro, cannot pick up drops, and cannot
    /// attack / use items / build (enforced in the survival systems via
    /// IsSpectating). Movement mirrors the target through the standard follow
    /// tick; the inventory half reuses the read-only /Inventory solo view.
    /// Operators may spectate on their own map, admins across maps. </summary>
    public sealed class CmdSpectate : Command2
    {
        public override string name { get { return "Spectate"; } }
        public override string shortcut { get { return "Spec"; } }
        public override string type { get { return CommandTypes.World; } }
        public override bool museumUsable { get { return false; } }
        public override bool SuperUseable { get { return false; } } // needs a player camera
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }
        public override CommandPerm[] ExtraPerms {
            get { return new[] { new CommandPerm(LevelPermission.Admin, "can spectate players on other maps") }; }
        }

        /// <summary> Extras key marking an active /Spectate session (value = the
        /// target's name). SurvivalInventory clears the session when it force-closes
        /// the mirrored view (target left/disconnected, viewer changed level). </summary>
        public const string SPEC_KEY    = "survival.spectating";
        const string SILENT_KEY = "survival.spectateSilent";
        // set only when THIS command performed the hide, so ending a spectate never
        // unhides someone who was already /Hide-hidden before they spectated
        const string HID_KEY    = "survival.spectateHid";

        /// <summary> Whether this player is currently spectating someone. The whole
        /// spectator ruleset hangs off this: no damage (SurvivalNet.DamagePlayer /
        /// OnPlayerDying), no mob aggro (SurvivalMobs targeting), no drop pickup
        /// (SurvivalDrops), no attack/use/toss/shoot intents, no block changes. </summary>
        public static bool IsSpectating(Player p) {
            object o;
            return p != null && p.Extras.TryGet(SPEC_KEY, out o);
        }

        /// <summary> Whether viewer is currently spectating THIS target. Used to
        /// re-hide the target's body from their spectator after anything respawns
        /// their entity globally (e.g. the death-revive cycle) - the camera rides
        /// inside that body, so re-adding it blocks the spectator's view. </summary>
        public static bool IsSpectatingTarget(Player viewer, Player target) {
            object o;
            if (viewer == null || target == null) return false;
            return viewer.Extras.TryGet(SPEC_KEY, out o) && ((string)o).CaselessEq(target.name);
        }

        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0 || message.CaselessEq("stop")) { Stop(p); return; }
            if (p.possessed) { p.Message("You're currently being &4possessed&S!"); return; }

            string[] args = message.SplitSpaces();
            bool silent = args.Length > 1 && args[1].CaselessEq("silent");

            Player target = PlayerInfo.FindMatches(p, args[0]);
            if (target == null) return;
            if (target == p) { p.Message("&WYou can't spectate yourself."); return; }
            if (!CheckRank(p, data, target, "spectate", false)) return;
            if (IsSpectating(target)) {
                p.Message("{0} &Wis spectating someone themselves.", target.ColoredName); return;
            }

            // A survival tool: only usable while on a survival map. Classic clients
            // are allowed too - they just ride along (there's no GUI to mirror).
            if (p.level == null || p.level.Config.SurvivalMode == SurvivalMode.Off) {
                p.Message("&WSpectate is a survival tool - use it while on a survival map."); return;
            }
            // Cross-map gate (mirrors /Inventory): operators spectate players on
            // their OWN map; spectating across maps is an admin capability (perm 1).
            if (target.level != p.level && !HasExtraPerm(p, data.Rank, 1)) {
                p.Message("&W{0} &Wis on {1}&W - operators can only spectate players on their own map.",
                          target.ColoredName, target.level == null ? "another map" : target.level.ColoredName);
                return;
            }

            // Switching targets: tear the old session down first (quietly for the
            // viewer - they asked for the new target, not a "stopped" message).
            object cur;
            bool sameTarget = p.Extras.TryGet(SPEC_KEY, out cur) && ((string)cur).CaselessEq(target.name);
            if (!sameTarget && IsSpectating(p)) EndSession(p, false);

            // Cross-map: get onto the target's map before hiding (a denied TP
            // leaves the viewer fully un-spectating rather than half-set-up).
            if (p.level != target.level) {
                Command.Find("TP").Use(p, target.name, data);
                if (p.level != target.level) {
                    p.Message("&WCould not reach {0}&W's map - spectate cancelled.", target.ColoredName);
                    return;
                }
            }

            HideSpectator(p, data);
            p.following = target.name;   // the standard follow tick mirrors movement
            Entities.Despawn(p, target); // don't render the body whose camera you ride

            // Live inventory mirror (read-only). Returns false for a non-survival
            // client - then it's follow-only.
            bool gui = SurvivalInventory.OpenPlayerInventory(p, target, false, solo: true,
                                                             crossMap: HasExtraPerm(p, data.Rank, 1));
            p.Extras[SPEC_KEY] = target.name;
            if (silent) p.Extras[SILENT_KEY] = true;
            else        p.Extras.Remove(SILENT_KEY);

            // The watched player deserves to know - unless the op asked for silent.
            if (!silent) target.Message("{0} &Sis now spectating you.", p.ColoredName);
            p.Message("Now spectating {0}&S{1}. &T/Spectate stop &Sto end.",
                      target.ColoredName, gui ? " &S(inventory mirrored)" : " &S(follow only)");
        }

        void Stop(Player p) {
            if (!IsSpectating(p)) {
                p.Message("You aren't spectating anyone. &HUse &T/Spectate [player]"); return;
            }
            EndSession(p, true);
        }

        /// <summary> Tears down a spectate session completely: unhide (only if the
        /// session did the hiding), stop following, respawn the target's body for
        /// the viewer, close the mirrored view, notify both sides as appropriate.
        /// The single exit path - /Spectate stop, target left the map, target
        /// disconnected and the viewer's own map change all funnel here, so a
        /// spectator can never be left hidden or invulnerable. No-op when the
        /// player isn't spectating. </summary>
        public static void EndSession(Player p, bool notifyViewer) {
            object o, tmp;
            if (!p.Extras.TryGet(SPEC_KEY, out o)) return;
            string tname  = (string)o;
            bool   silent = p.Extras.TryGet(SILENT_KEY, out tmp);
            p.Extras.Remove(SPEC_KEY);
            p.Extras.Remove(SILENT_KEY);

            if (p.following.CaselessEq(tname)) p.following = "";
            UnhideSpectator(p);

            Player target = PlayerInfo.FindExact(tname);
            if (target != null) {
                Entities.Spawn(p, target); // re-show the body we despawned
                if (!silent) target.Message("{0} &Sstopped spectating you.", p.ColoredName);
            }
            SurvivalInventory.ForceCloseView(p);
            if (notifyViewer) p.Message("Stopped spectating {0}.", p.FormatNick(tname));
        }

        /// <summary> Disconnect-time cleanup for a spectating VIEWER: clears the
        /// session state and the persisted hidden flag without any entity or chat
        /// side effects (the player is mid-teardown), so they don't rejoin
        /// invisible next session. </summary>
        public static void EndSessionQuiet(Player p) {
            object o, tmp;
            if (!p.Extras.TryGet(SPEC_KEY, out o)) return;
            p.Extras.Remove(SPEC_KEY);
            p.Extras.Remove(SILENT_KEY);
            if (p.Extras.TryGet(HID_KEY, out tmp)) {
                p.Extras.Remove(HID_KEY);
                p.hidden = false;
                Server.hidden.Remove(p.name);
                Server.hidden.Save(false);
            }
        }

        // The hide half of /Hide, WITHOUT the fake "- player disconnected" chat
        // broadcast and without the opchat toggle: entity despawn for those who
        // can't see hidden players, ops still informed, tab identity kept for the
        // spectator themselves. A player already /Hide-hidden keeps their own
        // hide untouched (HID_KEY marks only OUR hides for the teardown).
        static void HideSpectator(Player p, CommandData data) {
            if (p.hidden) return;
            Entities.GlobalDespawn(p, false);
            p.hidden   = true;
            p.hideRank = data.Rank;
            p.Extras[HID_KEY] = true;
            Chat.MessageFrom(ChatScope.Perms, p, "To Ops -λNICK&S- is now &fspectating &S(hidden)",
                             new ItemPerms(p.hideRank), null, true);
            Server.hidden.Add(p.name);
            OnPlayerActionEvent.Call(p, PlayerAction.Hide);
            Entities.GlobalSpawn(p, false);
            TabList.Add(p, p);
            Server.hidden.Save(false);
        }

        static void UnhideSpectator(Player p) {
            object tmp;
            if (!p.Extras.TryGet(HID_KEY, out tmp)) return; // we didn't hide them
            p.Extras.Remove(HID_KEY);
            if (!p.hidden) return;
            Chat.MessageFrom(ChatScope.Perms, p, "To Ops -λNICK&S- is &fvisible &Sagain (stopped spectating)",
                             new ItemPerms(p.hideRank), null, true);
            Entities.GlobalDespawn(p, false);
            p.hidden   = false;
            p.hideRank = LevelPermission.Banned;
            Server.hidden.Remove(p.name);
            OnPlayerActionEvent.Call(p, PlayerAction.Unhide);
            Entities.GlobalSpawn(p, false);
            TabList.Add(p, p);
            Server.hidden.Save(false);
        }

        public override void Help(Player p) {
            p.Message("&T/Spectate [player] &H- ride their camera, hidden");
            p.Message("&T/Spectate [player] silent &H- without telling them");
            p.Message("&T/Spectate stop &H- stop spectating");
            p.Message("&HYou are invulnerable and mirror their inventory.");
        }
    }
}
