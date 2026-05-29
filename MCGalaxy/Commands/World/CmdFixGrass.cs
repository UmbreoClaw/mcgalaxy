/*
    Copyright 2010 MCLawl Team - Written by Valek (Modified for use with MCForge)
 
   
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
using MCGalaxy.Drawing.Ops;
using MCGalaxy.Localization;
using MCGalaxy.Maths;
using MCGalaxy.Network;
using BlockID = System.UInt16;

namespace MCGalaxy.Commands.World {
    public sealed class CmdFixGrass : Command2 {
        public override string name { get { return "FixGrass"; } }
        public override string shortcut { get { return "fg"; } }
        public override string type { get { return CommandTypes.World; } }
        public override bool museumUsable { get { return false; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }
        public override bool SuperUseable { get { return false; } }

        public override void Use(Player p, string message, CommandData data) {
            FixGrassDrawOp op = new FixGrassDrawOp();
            
            if (message.Length == 0) {
                op.FixDirt   = true;
                op.FixGrass  = true;
            } else if (message.CaselessEq("light")) {
                op.LightMode = true;
            } else if (message.CaselessEq("grass")) {
                op.FixGrass  = true;
            } else if (message.CaselessEq("dirt")) {
                op.FixDirt   = true;
            } else {
                Help(p); return;
            }

            p.Message(Locale.Get("fixgrass.select_bounds", p));
            p.MakeSelection(2, "Selecting corners for &SFixGrass", op, DoFixGrass);
        }
        
        bool DoFixGrass(Player p, Vec3S32[] marks, object state, BlockID block) {
            FixGrassDrawOp op = (FixGrassDrawOp)state;
            op.AlwaysUsable = true;
            
            DrawOpPerformer.Do(op, null, p, marks, false);
            return false;
        }

        public override void Help(Player p) {
            p.Message(Locale.Get("fixgrass.help1", p));
            p.Message(Locale.Get("fixgrass.help2", p));
            p.Message(Locale.Get("fixgrass.help3", p));
            p.Message(Locale.Get("fixgrass.help4", p));
        }
    }
}
