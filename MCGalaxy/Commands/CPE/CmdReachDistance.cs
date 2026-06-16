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

namespace MCGalaxy.Commands.CPE
{
    public sealed class CmdReachDistance : Command2
    {
        public override string name { get { return "ReachDistance"; } }
        public override string shortcut { get { return "Reach"; } }
        public override string type { get { return CommandTypes.Building; } }
        public override LevelPermission defaultRank { get { return LevelPermission.AdvBuilder; } }
        public override bool SuperUseable { get { return false; } }

        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) {
                p.Message(Locale.Get("cmd.reachdistance.msg1", p), p.ReachDistance); return;
            }
            
            float dist = 0;
            if (!CommandParser.GetReal(p, message, "Distance", ref dist, 0, 1024)) return;
            
            int packedDist = (int)(dist * 32);
            if (packedDist > short.MaxValue) {
                p.Message(Locale.Get("cmd.reachdistance.msg2", p), message); return;
            }

            if (p.Session.SendSetReach(dist)) {
                p.ReachDistance = dist;
                p.Message(Locale.Get("cmd.reachdistance.msg3", p), dist);
                Server.reach.Update(p.name, packedDist.ToString());
                Server.reach.Save();
            } else {
                p.Message(Locale.Get("cmd.reachdistance.msg4", p)); return;
            }
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("cmd.reachdistance.help1", p));
            p.Message(Locale.Get("cmd.reachdistance.help2", p));
            p.Message(Locale.Get("cmd.reachdistance.help3", p));
        }
    }
}
