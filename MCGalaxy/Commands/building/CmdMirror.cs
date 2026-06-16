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
using MCGalaxy.Drawing;

namespace MCGalaxy.Commands.Building {
    public sealed class CmdMirror : Command2 {
        public override string name { get { return "Mirror"; } }
        public override string type { get { return CommandTypes.Building; } }
        public override LevelPermission defaultRank { get { return LevelPermission.AdvBuilder; } }
        public override bool SuperUseable { get { return false; } }
        public override CommandAlias[] Aliases {
            get { return new [] { new CommandAlias("Flip") }; }
        }

        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) { Help(p); return; }
            if (p.CurrentCopy == null) {
                p.Message(Locale.Get("copy.not_copied_yet", p)); return;
            }
            
            CopyState cState = p.CurrentCopy;
            BlockDefinition[] defs = p.level.CustomBlockDefs;
            
            foreach (string arg in message.SplitSpaces())
            {
                if (arg.CaselessEq("x")) {
                    Flip.MirrorX(cState, defs);
                    p.Message(Locale.Get("mirror.flipped_x", p));
                } else if (arg.CaselessEq("y") || arg.CaselessEq("u")) {
                    Flip.MirrorY(cState, defs);
                    p.Message(Locale.Get("mirror.flipped_y", p));
                } else if (arg.CaselessEq("z")) {
                    Flip.MirrorZ(cState, defs);
                    p.Message(Locale.Get("mirror.flipped_z", p));
                }
            }
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("mirror.help1", p));
            p.Message(Locale.Get("mirror.help2", p));
            p.Message(Locale.Get("mirror.help3", p));
            p.Message(Locale.Get("mirror.help4", p));
            p.Message(Locale.Get("mirror.help5", p));
        }
    }
}
