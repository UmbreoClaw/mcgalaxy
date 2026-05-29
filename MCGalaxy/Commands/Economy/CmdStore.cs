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
using System;
using System.Collections.Generic;
using MCGalaxy.Eco;
using MCGalaxy.Localization;

namespace MCGalaxy.Commands.Eco 
{
    public sealed class CmdStore : Command2 
    {
        public override string name { get { return "Store"; } }
        public override string shortcut { get { return "Shop"; } }
        public override string type { get { return CommandTypes.Economy; } }
        public override CommandAlias[] Aliases {
            get { return new[] { new CommandAlias("Item") }; }
        }

        public override void Use(Player p, string message, CommandData data) {            
            if (!Economy.CheckIsEnabled(p, this)) return;
            
            if (message.Length == 0 || IsListModifier(message)) {
                Paginator.Output(p, Economy.GetEnabledItems(),
                                 PrintItemOverview, "Store", "enabled Items", message);
                p.Message(Locale.Get("store.more_info", p));
            } else {
                Item item = Economy.GetItem(message);
                if (item == null) { Help(p); return; }                

                if (!item.Enabled) {
                    p.Message(Locale.Get("store.not_buyable", p), item.ShopName); return;
                }
                item.OnStoreCommand(p);
            }
        }
        
        static void PrintItemOverview(Player p, Item item) {
            item.OnStoreOverview(p);
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("store.help1", p));
            p.Message(Locale.Get("store.help2", p));
            p.Message(Locale.Get("store.help3", p));
            p.Message(Locale.Get("store.help4", p));
            p.Message(Locale.Get("store.help5", p), Economy.EnabledItemNames());
        }
    }
}
