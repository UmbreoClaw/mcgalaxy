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
using BlockID = System.UInt16;

namespace MCGalaxy.Commands.Building 
{
    public sealed class CmdBind : Command2 
    {
        public override string name { get { return "Bind"; } }
        public override string type { get { return CommandTypes.Building; } }
        public override LevelPermission defaultRank { get { return LevelPermission.AdvBuilder; } }
        public override bool SuperUseable { get { return false; } }

        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) { Help(p); return; }
            string[] args = message.SplitSpaces();
            if (args.Length > 2) { Help(p); return; }
            
            if (args[0].CaselessEq("clear")) {
                for (int b = 0; b < p.BlockBindings.Length; b++) {
                    p.BlockBindings[b] = (BlockID)b;
                }
                p.Message(Locale.Get("bind.all_unbound", p));
                return;
            }
            
            BlockID src;
            if (!CommandParser.GetBlock(p, args[0], out src)) return;
            if (Block.IsPhysicsType(src)) {
                p.Message(Locale.Get("bind.no_physics_bind", p)); return;
            }

            if (args.Length == 2) {
                BlockID dst;
                if (!CommandParser.GetBlockIfAllowed(p, args[1], "bind a block to", out dst)) return;
                
                p.BlockBindings[src] = dst;
                p.Message(Locale.Get("bind.bound_to", p), Block.GetName(p, src), Block.GetName(p, dst));
            } else {
                if (p.BlockBindings[src] == src) {
                    p.Message(Locale.Get("bind.not_bound", p), Block.GetName(p, src)); return;
                }
                p.BlockBindings[src] = src;
                p.Message(Locale.Get("bind.unbound", p), Block.GetName(p, src));
            }
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("bind.help1", p));
            p.Message(Locale.Get("bind.help2", p));
            p.Message(Locale.Get("bind.help3", p));
            p.Message(Locale.Get("bind.help4", p));
        }
    }
}
