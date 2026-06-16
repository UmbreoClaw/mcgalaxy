/*
    Copyright 2012 MCForge
    
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

namespace MCGalaxy.Commands.Info 
{
    public sealed class CmdLoaded : Command2 
    {
        public override string name { get { return "Loaded"; } }
        public override string type { get { return CommandTypes.Information; } }
        public override bool UseableWhenFrozen { get { return true; } }
        
        public override void Use(Player p, string message, CommandData data) {
            Level[] loaded = LevelInfo.Loaded.Items;
            p.Message(Locale.Get("loaded.header", p));
            Paginator.Output(p, loaded, (lvl) => FormatMap(p, lvl),
                             "Levels", "levels", message);
            p.Message(Locale.Get("loaded.use_levels", p));
        }
        
        static string FormatMap(Player p, Level lvl) {            
            bool canVisit = p.IsSuper || lvl.VisitAccess.CheckAllowed(p);
            string physics = " [" +  lvl.physics + "]";
            string visit = canVisit ? "" : " &c[no]";
            return lvl.ColoredName + physics + visit;
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("loaded.help1", p));
            p.Message(Locale.Get("loaded.help2", p));
        }
    }
}
