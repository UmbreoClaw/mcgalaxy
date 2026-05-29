/*
    Copyright 2010 MCSharp team (Modified for use with MCZall/MCLawl/MCForge)
    
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
using MCGalaxy.Localization;
namespace MCGalaxy.Commands.World {
    public sealed class CmdUnload : Command2 {
        public override string name { get { return "Unload"; } }
        public override string type { get { return CommandTypes.World; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }

        public override void Use(Player p, string message, CommandData data) {
            if (CheckSuper(p, message, "level name")) return;
            
            if (message.Length == 0) {
                if (!p.level.Unload()) {
                    p.Message(Locale.Get("unload.cannot_unload", p));
                }
            } else if (message.CaselessEq("empty")) {
                Level[] loaded = LevelInfo.Loaded.Items;
                for (int i = 0; i < loaded.Length; i++) {
                    Level lvl = loaded[i];
                    if (lvl.HasPlayers()) continue;
                    lvl.Unload(true);
                }
            } else {
                Level level = Matcher.FindLevels(p, message);
                if (level == null) return;
                
                if (!level.Unload()) {
                    p.Message(Locale.Get("unload.cannot_unload", p));
                }
            }
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("unload.help1", p));
            p.Message(Locale.Get("unload.help2", p));
            p.Message(Locale.Get("unload.help3", p));
            p.Message(Locale.Get("unload.help4", p));
        }
    }
}
