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
using MCGalaxy.Eco;

namespace MCGalaxy.Commands.Eco {
    public sealed class CmdEconomy : Command2 {
        public override string name { get { return "Economy"; } }
        public override string shortcut { get { return "Eco"; } }
        public override string type { get { return CommandTypes.Economy; } }
        public override CommandPerm[] ExtraPerms {
            get { return new[] { new CommandPerm(LevelPermission.Operator, "can setup the economy") }; }
        }
        
        public override void Use(Player p, string message, CommandData data) {
            string[] raw = message.SplitSpaces();
            string[] args = new string[] { "", "", "", "", "", "", "", "" };
            for (int i = 0; i < Math.Min(args.Length, raw.Length); i++)
                args[i] = raw[i];
            if (!CheckExtraPerm(p, data, 1)) return;
            
            if (args[0].CaselessEq("enable")) {
                p.Message(Locale.Get("economy.enabled", p));
                Economy.Enabled = true; Economy.Save();
            } else if (args[0].CaselessEq("disable")) {
                p.Message(Locale.Get("economy.disabled", p));
                Economy.Enabled = false; Economy.Save();
            } else {
                Item item = Economy.GetItem(args[0]);
                if (item != null) {
                    item.Setup(p, args);
                    Economy.Save();
                } else if (args[1].Length == 0) {
                    Help(p);
                } else {
                    Help(p, args[1]);
                }
            }
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("economy.help1", p));
            p.Message(Locale.Get("economy.help2", p));
            p.Message("   &HAll items: &S" + Economy.Items.Join(item => item.Name));
        }

        public override void Help(Player p, string message) {
            Item item = Economy.GetItem(message);
            if (item == null) {
                p.Message(Locale.Get("economy.no_item", p));
            } else {
                item.OnSetupHelp(p);
            }
        }
    }
}
