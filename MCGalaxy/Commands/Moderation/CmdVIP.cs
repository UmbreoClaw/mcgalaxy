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
using System.Collections.Generic;

namespace MCGalaxy.Commands.Moderation {
    public sealed class CmdVIP : Command2 {
        public override string name { get { return "VIP"; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Admin; } }

        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) { List(p, ""); return; }
            string[] args = message.SplitSpaces();
            string cmd = args[0];
            
            if (IsCreateAction(cmd)) {
                if (args.Length < 2) { Help(p); return; }
                Add(p, args[1]);
            } else if (IsDeleteAction(cmd)) {
                if (args.Length < 2) { Help(p); return; }
                Remove(p, args[1]);
            } else if (IsListAction(cmd)) {
                string modifier = args.Length > 1 ? args[1] : "";
                List(p, modifier);
            } else if (args.Length == 1) {
                Add(p, args[0]);
            } else {
                Help(p);
            }
        }
        
        static void Add(Player p, string name) {
            name = PlayerInfo.FindMatchesPreferOnline(p, name);
            if (name == null) return;
            
            if (!Server.vip.Add(name)) {
                p.Message(Locale.Get("cmd.vip.msg1", p), p.FormatNick(name));
            } else {
                Server.vip.Save();
                p.Message(Locale.Get("cmd.vip.msg2", p), p.FormatNick(name));
                
                Player vip = PlayerInfo.FindExact(name);
                if (vip != null) vip.Message(Locale.Get("cmd.vip.msg3", p));
            }
        }
        
        static void Remove(Player p, string name) {
            name = PlayerInfo.FindMatchesPreferOnline(p, name);
            if (name == null) return;
            
            if (!Server.vip.Remove(name)) {
                p.Message(Locale.Get("cmd.vip.msg4", p), p.FormatNick(name));
            } else {
                Server.vip.Save();
                p.Message(Locale.Get("cmd.vip.msg5", p), p.FormatNick(name));
                
                Player vip = PlayerInfo.FindExact(name);
                if (vip != null) vip.Message(Locale.Get("cmd.vip.msg6", p));
            }
        }
        
        static void List(Player p, string modifier) {
            Server.vip.Output(p, "VIPs", "VIP list", modifier);
        }

        public override void Help(Player p) {
            p.Message(Locale.Get("cmd.vip.help1", p));
            p.Message(Locale.Get("cmd.vip.help2", p));
            p.Message(Locale.Get("cmd.vip.help3", p));
            p.Message(Locale.Get("cmd.vip.help4", p));
            p.Message(Locale.Get("cmd.vip.help5", p));
        }
    }
}
