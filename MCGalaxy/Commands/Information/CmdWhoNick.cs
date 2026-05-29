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
using MCGalaxy.SQL;

namespace MCGalaxy.Commands.Info 
{
    public sealed class CmdWhoNick : Command2 
    {
        public override string name { get { return "WhoNick"; } }
        public override string shortcut { get { return "RealName"; } }
        public override string type { get { return CommandTypes.Information; } }
        public override bool UseableWhenFrozen { get { return true; } }
        
        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) { Help(p); return; }
            string[] args = message.SplitSpaces(2);
            
            if (args.Length > 1 && args[0].CaselessEq("bot")) {
                ForBot(p, args[1]);
                return;
            }
            ForPlayer(p, message);
        }
        
        static void ForPlayer(Player p, string nick) {
            nick = Colors.Strip(nick);
            Player[] players = PlayerInfo.Online.Items;
            int matches;
            
            Player match = Matcher.Find(p, nick, out matches, players, pl => p.CanSee(pl),
                                        pl => Colors.Strip(pl.DisplayName),
                                        pl => pl.ColoredName + " &S(" + pl.name + ")",
                                        "online player nicks");
            if (match == null) return;
            p.Message(Locale.Get("whonick.player_named", p), match.DisplayName, match.name);
        }
        
        static void ForBot(Player p, string nick) {
            nick = Colors.Strip(nick);
            PlayerBot[] bots = p.level.Bots.Items;
            int matches;
            
            PlayerBot match = Matcher.Find(p, nick, out matches, bots, bot => true,
                                           bot => Colors.Strip(bot.DisplayName), 
                                           bot => bot.ColoredName + " &S(" + bot.name + ")", 
                                           "bot nicknames");
            if (match == null) return;
            p.Message(Locale.Get("whonick.bot_named", p), match.DisplayName, match.name);
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("whonick.help1", p));
            p.Message(Locale.Get("whonick.help2", p));
            p.Message(Locale.Get("whonick.help3", p));
            p.Message(Locale.Get("whonick.help4", p));
        }
    }
}
