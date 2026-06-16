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
using System;
using MCGalaxy.Tasks;

namespace MCGalaxy.Commands.Misc {
    public sealed class CmdTimer : Command2 {
        public override string name { get { return "Timer"; } }
        public override string type { get { return CommandTypes.Other; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }

        public override void Use(Player p, string message, CommandData data) {
            if (p.cmdTimer) { p.Message(Locale.Get("timer.one_at_a_time", p)); return; }
            if (message.Length == 0) { Help(p); return; }

            int TotalTime = 0;
            try
            {
                string[] bits = message.SplitSpaces(2);
                TotalTime = int.Parse(bits[0]);
                message   = bits[1];
            }
            catch
            {
                TotalTime = 60;
            }

            if (TotalTime > 300) { p.Message(Locale.Get("timer.too_long", p)); return; }

            TimerArgs args = new TimerArgs();
            args.Message = message;
            args.Repeats = (int)(TotalTime / 5) + 1;
            args.Player  = p;
            
            p.cmdTimer = true;
            p.level.Message(string.Format(Locale.Get("timer.started"), TotalTime));
            p.level.Message(args.Message);
            Server.MainScheduler.QueueRepeat(TimerCallback, args, TimeSpan.FromSeconds(5));
        }
        
        class TimerArgs {
            public string Message;
            public int Repeats;
            public Player Player;
        }
        
        static void TimerCallback(SchedulerTask task) {
            TimerArgs args = (TimerArgs)task.State;            
            Player p = args.Player;

            args.Repeats--;
            if (args.Repeats == 0 || !p.cmdTimer) {
                p.Message(Locale.Get("timer.ended", p));
                p.cmdTimer = false;
                task.Repeating = false;
            } else {
                p.level.Message(args.Message);
                p.level.Message(string.Format(Locale.Get("timer.remaining"), args.Repeats * 5));
            }
        }
        
        public override void Help(Player p)  {
            p.Message(Locale.Get("timer.help1", p));
            p.Message(Locale.Get("timer.help2", p));
            p.Message(Locale.Get("timer.help3", p));
        }
    }
}
