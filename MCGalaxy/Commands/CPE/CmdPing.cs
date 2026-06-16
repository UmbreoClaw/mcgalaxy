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

namespace MCGalaxy.Commands.Chatting 
{
    public sealed class CmdPing : Command2 
    {
        public override string name { get { return "Ping"; } }
        public override string type { get { return CommandTypes.Information; } }
        public override bool UseableWhenFrozen { get { return true; } }
        public override CommandPerm[] ExtraPerms {
            get { return new[] { new CommandPerm(LevelPermission.Operator, "can see ping of other players") }; }
        }

        public override void Use(Player p, string message, CommandData data) {
            if (!message.CaselessEq("all")) {
                if (message.Length == 0) message = p.name;

                Player who = PlayerInfo.FindMatches(p, message);
                if (who == null) return;

                if (p != who && !CheckExtraPerm(p, data, 1)) return;
                PingList ping = who.Session.Ping;

                if (!who.Supports(CpeExt.TwoWayPing)) {
                    p.Message(Locale.Get("cmd.ping.msg1", p), 
                              p == who ? "Your" : p.FormatNick(who) + "&S's");
                } else if (ping.Measures() == 0) {
                    p.Message(Locale.Get("cmd.ping.msg2", p));
                } else {
                    p.Message(p.FormatNick(who) + " &S- " + ping.Format());
                }
            } else {
                if (!CheckExtraPerm(p, data, 1)) return;
                Player[] players = PlayerInfo.Online.Items;
                p.Message(Locale.Get("cmd.ping.msg3", p));

                foreach (Player target in players) 
                {
                    if (!p.CanSee(target, data.Rank)) continue;
                    PingList ping = target.Session.Ping;

                    if (ping.Measures() == 0) continue;
                    p.Message(ping.FormatAll() + " &S- " + p.FormatNick(target));
                }
            }
        }

        public override void Help(Player p) {
            p.Message(Locale.Get("cmd.ping.help1", p));
            p.Message(Locale.Get("cmd.ping.help2", p));
            p.Message(Locale.Get("cmd.ping.help3", p));
            p.Message(Locale.Get("cmd.ping.msg4", p));
        }
    }
}
