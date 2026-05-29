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
    public sealed class CmdPhysics : Command2 {
        public override string name { get { return "Physics"; } }
        public override string type { get { return CommandTypes.World; } }
        public override bool museumUsable { get { return false; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }
        public override CommandAlias[] Aliases {
            get { return new CommandAlias[] { new CommandAlias("KillPhysics", "kill") }; }
        }

        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) { ShowPhysics(p); return; }
            if (message.CaselessEq("kill")) { KillPhysics(p); return; }
            
            string[] args = message.SplitSpaces();
            Level lvl = p.IsSuper ? Server.mainLevel : p.level;
            
            int state = 0, stateI = args.Length == 1 ? 0 : 1;            
            if (!CommandParser.GetInt(p, args[stateI], "Physics state", ref state, 0, 5)) return;
            
            if (args.Length == 2) {
                lvl = Matcher.FindLevels(p, args[0]);
                if (lvl == null) return;
            }
            
            if (!LevelInfo.Check(p, data.Rank, lvl, "set physics of this level")) return;
            SetPhysics(lvl, state);
        }
        
        internal static string[] states = new string[] { "&cOFF", "&aNormal", "&aAdvanced", 
            "&aHardcore", "&aInstant", "&4Doors-only" };
        
        void ShowPhysics(Player p) {
            Level[] loaded = LevelInfo.Loaded.Items;
            foreach (Level lvl in loaded) {
                if (lvl.physics == 0) continue;
                p.Message(Locale.Get("cmd.physics.msg1", p), 
                               lvl.ColoredName, lvl.physics, lvl.lastCheck, lvl.lastUpdate);
            }
        }
        
        void KillPhysics(Player p) {
            Level[] levels = LevelInfo.Loaded.Items;
            foreach (Level lvl in levels) {
                if (lvl.physics == 0) continue;
                SetPhysics(lvl, 0);
            }
            p.Message(Locale.Get("physics.killed", p));
        }
        
        internal static void SetPhysics(Level lvl, int state) {
            lvl.SetPhysics(state);
            if (state == 0) lvl.ClearPhysics();
            string stateDesc = states[state];
            lvl.Message(Locale.Get("physics.state_now") + stateDesc + " &Son " + lvl.ColoredName);
            
            stateDesc = stateDesc.Substring( 2 );
            string logInfo = "Physics are now " + stateDesc + " on " + lvl.name;
            Logger.Log(LogType.SystemActivity, logInfo);
            lvl.SaveSettings();
        }

        public override void Help(Player p) {
            p.Message(Locale.Get("physics.help1", p));
            p.Message(Locale.Get("physics.help2", p));
            p.Message(Locale.Get("physics.help3", p));
            p.Message(Locale.Get("physics.help4", p));
            p.Message(Locale.Get("physics.help5", p));
        }
    }
}
