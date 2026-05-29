/*
    Copyright 2011 MCForge

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
using MCGalaxy.Localization;

namespace MCGalaxy.Commands.Moderation {
    public sealed class CmdFollow : Command2 {
        public override string name { get { return "Follow"; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }
        public override bool SuperUseable { get { return false; } }

        public override void Use(Player p, string message, CommandData data) {
            if (p.possessed) { p.Message(Locale.Get("follow.being_possessed", p)); return; }
            string[] args = message.SplitSpaces(2);
            string name = args[0];

            bool stealth = false;
            if (message == "#") {
                if (p.following.Length > 0) { stealth = true; name = ""; }
                else { Help(p); return; }
            } else if (args.Length > 1 && args[0] == "#") {
                if (p.hidden) stealth = true;
                name = args[1];
            }

            if (name.Length == 0 && p.following.Length == 0) { Help(p); return; }
            if (name.CaselessEq(p.following) || (name.Length == 0 && p.following.Length > 0)) {
                Unfollow(p, data, stealth);
            } else {
                Follow(p, name, data, stealth);
            }
        }

        static void Unfollow(Player p, CommandData data, bool stealth) {
            p.Message(Locale.Get("follow.stopped", p), p.FormatNick(p.following));

            Player target = PlayerInfo.FindExact(p.following);
            if (target != null) Entities.Spawn(p, target);
            p.following = "";

            if (!p.hidden) return;
            if (!stealth) {
                Command.Find("Hide").Use(p, "", data);
            } else {
                p.Message(Locale.Get("follow.still_hidden", p));
            }
        }

        static void Follow(Player p, string name, CommandData data, bool stealth) {
            Player target = PlayerInfo.FindMatches(p, name);
            if (target == null) return;
            if (target == p) { p.Message(Locale.Get("follow.no_self", p)); return; }
            if (!CheckRank(p, data, target, "follow", false)) return;

            if (target.following.Length > 0) {
                p.Message(Locale.Get("follow.already_following", p),
                          p.FormatNick(target), p.FormatNick(target.following)); return;
            }

            if (!p.hidden) Command.Find("Hide").Use(p, "", data);

            if (p.level != target.level) Command.Find("TP").Use(p, target.name, data);
            if (p.following.Length > 0) {
                Player old = PlayerInfo.FindExact(p.following);
                if (old != null) Entities.Spawn(p, old);
            }

            p.following = target.name;
            p.Message(Locale.Get("follow.now_following", p), p.FormatNick(target));
            Entities.Despawn(p, target);
        }

        public override void Help(Player p) {
            p.Message(Locale.Get("follow.help1", p));
            p.Message(Locale.Get("follow.help2", p));
            p.Message(Locale.Get("follow.help3", p));
            p.Message(Locale.Get("follow.help4", p));
        }
    }
}
