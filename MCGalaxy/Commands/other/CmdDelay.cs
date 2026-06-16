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
using System.Threading;

namespace MCGalaxy.Commands.Misc {
    public sealed class CmdDelay : Command2 {
        public override string name { get { return "Delay"; } }
        public override string type { get { return CommandTypes.Other; } }

        public override void Use(Player p, string message, CommandData data) {
            TimeSpan duration = TimeSpan.Zero;
            if (!CommandParser.GetTimespan(p, message, ref duration, "wait for", "ms")) return;
            
            if (duration.TotalSeconds > 60) {
                p.Message(Locale.Get("delay.too_long", p)); return;
            }

            if (data.Context != CommandContext.MessageBlock) {
                p.Message(Locale.Get("delay.mb_only", p)); return;
            }
            Thread.Sleep((int)duration.TotalMilliseconds);
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("delay.help1", p));
            p.Message(Locale.Get("delay.help2", p));
            p.Message(Locale.Get("delay.help3", p));
            p.Message(Locale.Get("delay.help4", p));
        }
    }
}
