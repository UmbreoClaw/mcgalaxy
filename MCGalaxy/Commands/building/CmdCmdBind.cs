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

namespace MCGalaxy.Commands.Building 
{
    public sealed class CmdCmdBind : Command2 
    {
        public override string name { get { return "CmdBind"; } }
        public override string shortcut { get { return "cb"; } }
        public override string type { get { return CommandTypes.Building; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Builder; } }
        public override bool SuperUseable { get { return false; } }
        public override bool MessageBlockRestricted { get { return true; } }
        
        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) {
                bool anyBinds = false;
                foreach (var kvp in p.CmdBindings)
                {
                    p.Message(Locale.Get("cmd.cmdbind.help1", p), kvp.Key, kvp.Value);
                    anyBinds = true;
                }
                
                if (!anyBinds) p.Message(Locale.Get("cmdbind.no_binds", p));
                return;
            }

            string[] parts = message.SplitSpaces(2);
            string trigger = parts[0];

            if (parts.Length == 1) {
                string value;
                if (!p.CmdBindings.TryGetValue(trigger, out value)) {
                    p.Message(Locale.Get("cmdbind.no_cmd_bound", p), trigger);
                } else {
                    p.Message(Locale.Get("cmd.cmdbind.help2", p), trigger, value);
                }
            } else {
                p.CmdBindings[trigger] = parts[1];
                p.Message(Locale.Get("cmd.cmdbind.msg1", p), trigger, parts[1]);
            }
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("cmdbind.help1", p));
            p.Message(Locale.Get("cmdbind.help2", p));
            p.Message(Locale.Get("cmdbind.help3", p));
            p.Message(Locale.Get("cmdbind.help4", p));
            p.Message(Locale.Get("cmdbind.help5", p));
            p.Message(Locale.Get("cmdbind.help6", p));
            p.Message(Locale.Get("cmdbind.help7", p));
        }
    }
}
