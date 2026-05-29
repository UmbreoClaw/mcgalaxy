/*
    Copyright 2011 MCForge
        
    Dual-licensed under the    Educational Community License, Version 2.0 and
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

namespace MCGalaxy.Commands.Info 
{
    public sealed class CmdLastCmd : Command2 
    {
        public override string name { get { return "LastCmd"; } }
        public override string shortcut { get { return "Last"; } }
        public override string type { get { return CommandTypes.Information; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }
        public override bool UpdatesLastCmd { get { return false; } }

        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) {
                Player[] players = PlayerInfo.Online.Items;
                foreach (Player pl in players) 
                {
                    if (p.CanSee(pl, data.Rank)) ShowLastCommand(p, pl);
                }
            } else {
                Player who = PlayerInfo.FindMatches(p, message);
                if (who != null) ShowLastCommand(p, who);
            }
        }
        
        static void ShowLastCommand(Player p, Player target) {
            if (target.lastCMD.Length == 0) {
                p.Message(Locale.Get("lastcmd.no_commands_yet", p),
                          p.FormatNick(target));
            } else {
                TimeSpan delta = DateTime.UtcNow - target.lastCmdTime;
                p.Message(Locale.Get("lastcmd.last_used", p),
                          p.FormatNick(target), target.lastCMD, delta.Shorten(true));
            }
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("lastcmd.help1", p));
            p.Message(Locale.Get("lastcmd.help2", p));
            p.Message(Locale.Get("lastcmd.help3", p));
            p.Message(Locale.Get("lastcmd.help4", p));
        }
    }
}
