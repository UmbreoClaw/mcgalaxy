/*
    Copyright 2010 MCSharp team (Modified for use with MCZall/MCLawl/MCForge)
    
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
using MCGalaxy.Events;

namespace MCGalaxy.Commands.Moderation {
    public sealed class CmdKick : Command2 {
        public override string name { get { return "Kick"; } }
        public override string shortcut { get { return "k"; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override LevelPermission defaultRank { get { return LevelPermission.AdvBuilder; } }
        
        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) { Help(p); return; }
            string[] args = message.SplitSpaces(2);
            
            Player who = PlayerInfo.FindMatches(p, args[0]);
            if (who == null) return;
            string kickMsg = "by " + p.truename, reason = null;
            
            if (args.Length > 1) {
                reason = ModActionCmd.ExpandReason(p, args[1]);
                if (message == null) return;
                kickMsg += "&f: " + reason; 
            }

            if (p == who) { p.Message(Locale.Get("kick.cant_kick_self", p)); return; }
            if (who.Rank >= data.Rank) {
                Chat.MessageFrom(p, string.Format(Locale.Get("kick.kick_failed"), who.ColoredName));
                return;
            }
            
            ModAction action = new ModAction(who.name, p, ModActionType.Kicked, reason);
            OnModActionEvent.Call(action);
            who.Kick(kickMsg, "Kicked " + kickMsg);
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("kick.help1", p));
            p.Message(Locale.Get("kick.help2", p));
            p.Message(Locale.Get("kick.help3", p));
        }
    }
}
