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
    public sealed class CmdSpin : Command2 {
        public override string name { get { return "Spin"; } }
        public override string type { get { return CommandTypes.Building; } }
        public override LevelPermission defaultRank { get { return LevelPermission.AdvBuilder; } }
        public override bool SuperUseable { get { return false; } }
        public override CommandAlias[] Aliases {
            get { return new [] { new CommandAlias("Rotate") }; }
        }

        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) message = "y";
            if (p.CurrentCopy == null) {
                p.Message(Locale.Get("copy.not_copied_yet", p)); return;
            }
            
            CopyState cState = p.CurrentCopy;
            string opt = message.ToLower();
            BlockDefinition[] defs = p.level.CustomBlockDefs;
            
            // /Mirror used to be part of spin
            if (opt.CaselessStarts("mirror")) {
                p.Message(Locale.Get("spin.mirror_deprecated", p), opt);
                return;
            }
            
            string[] args = opt.SplitSpaces();
            char axis = 'Y';
            int angle = 90;
            if (!Handle(ref axis, ref angle, args[0])) { Help(p); return; }
            if (args.Length > 1 && !Handle(ref axis, ref angle, args[1])) { Help(p); return; }
            
            CopyState newState = cState;
            if (angle == 0) {
            } else if (axis == 'X') {
                newState = Flip.RotateX(cState, angle, defs);
            } else if (axis == 'Y') {
                newState = Flip.RotateY(cState, angle, defs);
            } else if (axis == 'Z') {
                newState = Flip.RotateZ(cState, angle, defs);
            }

            newState.CopySource = cState.CopySource;
            newState.CopyTime   = cState.CopyTime;
            p.CurrentCopy = newState;
            p.Message(Locale.Get("spin.rotated", p), angle, axis);
        }
        
        bool Handle(ref char axis, ref int angle, string arg) {
            int value;
            if (arg == "x" || arg == "y" || arg == "z") {
                axis = char.ToUpper(arg[0]); return true;
            } else if (NumberUtils.TryParseInt32(arg, out value)) {
                // Clamp to [0, 360)
                value %= 360;
                if (value < 0) value += 360;
                angle = value;
                return angle == 0 || angle == 90 || angle == 180 || angle == 270;
            }
            return false;
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("spin.help1", p));
            p.Message(Locale.Get("spin.help2", p));
        }
    }
}
