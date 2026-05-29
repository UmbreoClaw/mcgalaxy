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
using MCGalaxy.Drawing.Brushes;
using MCGalaxy.Drawing.Ops;
using MCGalaxy.Maths;

namespace MCGalaxy.Commands.Building {
    public class CmdReplace : DrawCmd {
        public override string name { get { return "Replace"; } }
        public override string shortcut { get { return "r"; } }
        public override LevelPermission defaultRank { get { return LevelPermission.AdvBuilder; } }
        
        protected override DrawOp GetDrawOp(DrawArgs dArgs) { 
            DrawOp op = new CuboidDrawOp(); 
            op.AffectedByTransform = false;
            return op;
        }
        
        protected override void GetBrush(DrawArgs dArgs) {
            dArgs.BrushName = "Replace";
            dArgs.BrushArgs = dArgs.Message;
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("replace.help1", p));
            p.Message(Locale.Get("replace.help2", p));
            p.Message(Locale.Get("replace.help3", p));
            p.Message(Locale.Get("replace.help4", p));
        }
    }

    public sealed class CmdReplaceNot : CmdReplace {
        public override string name { get { return "ReplaceNot"; } }
        public override string shortcut { get { return "rn"; } }
        
        protected override void GetBrush(DrawArgs dArgs) {
            dArgs.BrushName = "ReplaceNot";
            dArgs.BrushArgs = dArgs.Message;
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("replacenot.help1", p));
            p.Message(Locale.Get("replacenot.help2", p));
            p.Message(Locale.Get("replacenot.help3", p));
            p.Message(Locale.Get("replacenot.help4", p));
        }
    }
}
