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
using MCGalaxy.Drawing.Ops;
using MCGalaxy.Undo;
using MCGalaxy.Maths;

namespace MCGalaxy.Commands.Building {   
    public sealed class CmdRedo : Command2 {   
        public override string name { get { return "Redo"; } }
        public override string type { get { return CommandTypes.Building; } }
        public override bool SuperUseable { get { return false; } }

        public override void Use(Player p, string message, CommandData data) {
            if (message.Length > 0) { Help(p); return; }
            PerformRedo(p);
        }
        
        static void PerformRedo(Player p) {
            UndoDrawOpEntry[] entries = p.DrawOps.Items;
            if (entries.Length == 0) {
                p.Message(Locale.Get("redo.no_undo_to_redo", p)); return;
            }
            
            for (int i = entries.Length - 1; i >= 0; i--) {
                UndoDrawOpEntry entry = entries[i];
                if (entry.DrawOpName != "UndoSelf") continue;
                p.DrawOps.Remove(entry);
                
                RedoSelfDrawOp op = new RedoSelfDrawOp();
                op.Start = entry.Start; op.End = entry.End;
                DrawOpPerformer.Do(op, null, p, new Vec3S32[] { Vec3U16.MinVal, Vec3U16.MaxVal });
                p.Message(Locale.Get("redo.performed", p));
                return;
            }
            p.Message(Locale.Get("redo.no_ops_found", p));
        }

        public override void Help(Player p) {
            p.Message(Locale.Get("redo.help1", p));
            p.Message(Locale.Get("redo.help2", p));
        }
    }
}
