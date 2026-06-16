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
using MCGalaxy.Drawing.Brushes;
using MCGalaxy.Drawing.Ops;
using BlockID = System.UInt16;

namespace MCGalaxy.Commands.Building 
{
    public class CmdReplaceBrush : DrawCmd 
    {
        public override string name { get { return "ReplaceBrush"; } }
        public override string shortcut { get { return "rb"; } }
        public override LevelPermission defaultRank { get { return LevelPermission.AdvBuilder; } }
        protected virtual bool ReplaceNot { get { return false; } }
        
        protected override DrawOp GetDrawOp(DrawArgs dArgs) {
            Player p = dArgs.Player;
            
            string replaceCmd = ReplaceNot ? "ReplaceNot" : "Replace";
            if (!p.CanUse(replaceCmd) || !p.CanUse("Brush")) {
                p.Message(Locale.Get("replacebrush.no_perm", p), replaceCmd); return null;
            }

            DrawOp op = new CuboidDrawOp();
            op.AffectedByTransform = false;
            return op;
        }
        
        protected override void GetBrush(DrawArgs dArgs) {
            dArgs.BrushName = ReplaceNot ? "ReplaceNotBrush" : "ReplaceBrush";
            dArgs.BrushArgs = dArgs.Message;
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("replacebrush.help1", p));
            p.Message(Locale.Get("replacebrush.help2", p));
            p.Message(BrushHelpLine);
        }
    }

    public class CmdReplaceNotBrush : CmdReplaceBrush
    {
        public override string name { get { return "ReplaceNotBrush"; } }
        public override string shortcut { get { return "rnb"; } }
        protected override bool ReplaceNot { get { return true; } }

        public override void Help(Player p) {
            p.Message(Locale.Get("replacenotbrush.help1", p));
            p.Message(Locale.Get("replacenotbrush.help2", p));
            p.Message(BrushHelpLine);
        }
    }
}
