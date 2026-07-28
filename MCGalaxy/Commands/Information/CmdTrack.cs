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
using MCGalaxy.Network;

namespace MCGalaxy.Commands.Info
{
    public sealed class CmdTrack : Command2
    {
        public override string name { get { return "Track"; } }
        public override string type { get { return CommandTypes.Information; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }
        public override bool SuperUseable { get { return false; } }

        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) { SurvivalTrack.Arm(p); return; }

            string[] args = message.SplitSpaces();
            if (args.Length == 1 && args[0].CaselessEq("stop")) { SurvivalTrack.StopAll(p); return; }
            if (args.Length == 2 && args[1].CaselessEq("stop")) { SurvivalTrack.Stop(p, args[0]); return; }
            if (args.Length != 1) { Help(p); return; }

            Player target = PlayerInfo.FindMatches(p, args[0]);
            if (target == null) return;
            if (target == p) { p.Message("&SYou always know where you are."); return; }
            if (!p.CanSee(target, data.Rank)) {
                p.Message("&WCannot find an online player matching &b{0}", args[0]); return;
            }
            if (target.level != p.level) {
                p.Message("&W{0} &Wis not on your map.", target.ColoredName); return;
            }
            SurvivalTrack.Add(p, target, null, 0);
        }

        public override void Help(Player p) {
            p.Message("&T/Track <player> &H- live position, top right (max 3)");
            p.Message("&T/Track &H- then punch a mob or player to track it");
            p.Message("&T/Track <name> stop&H, &T/Track stop &H- stop one / all");
        }
    }
}
