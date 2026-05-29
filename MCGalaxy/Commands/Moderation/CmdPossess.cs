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

using System;
using MCGalaxy.Localization;
namespace MCGalaxy.Commands.Moderation {
    
    public sealed class CmdPossess : Command2 {
        public override string name { get { return "Possess"; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Admin; } }
        public override bool SuperUseable { get { return false; } }

        static void Unpossess(Player target) {
            target.following = "";
            target.possessed = false;
            Entities.GlobalRespawn(target, true);
        }

        public override void Use(Player p, string message, CommandData data) {
            string[] args = message.SplitSpaces();
            if (args.Length > 2) { Help(p); return; }
            string skin = args.Length > 1 ? args[1] : "";
            string name = args[0];
            
            if (name.Length == 0) {
                if (p.possess.Length == 0) { p.Message(Locale.Get("possess.not_possessing", p)); return; }
                
                Player target = PlayerInfo.FindExact(p.possess);
                p.possess = "";
                if (target == null) { p.Message(Locale.Get("possess.disabled", p)); return;  }
                
                Unpossess(target);
                p.invincible = false;
                Command.Find("Hide").Use(p, "", data);
                p.Message(Locale.Get("possess.stopped", p), p.FormatNick(target));
            } else {
                Player target = PlayerInfo.FindMatches(p, name);
                if (target == null) return;
                if (!CheckRank(p, data, target, "teleport", false)) return;
                
                if (p == target) { p.Message(Locale.Get("possess.no_self", p)); return; }
                if (target.possess.Length > 0) {
                    p.Message(Locale.Get("possess.target_possessing", p)); return;
                }
                if (target.following.Length > 0) {
                    p.Message(Locale.Get("possess.target_following", p)); return;
                }
                if (p.possess.Length > 0) {
                    Player prev = PlayerInfo.FindExact(p.possess);
                    if (prev != null) Unpossess(prev);
                }
                
                Command.Find("TP").Use(p, target.name, data);
                if (!p.hidden) Command.Find("Hide").Use(p, "", data);
                p.possess = target.name;
                target.following = p.name;
                if (!p.invincible) p.invincible = true;
                
                bool result = (skin == "#") ? target.MarkPossessed() : target.MarkPossessed(p.name);
                if (!result) return;
                
                Entities.Despawn(p, target);
                target.possessed = true;
                p.Message(Locale.Get("possess.now_possessing", p), p.FormatNick(target));
            }
        }

        public override void Help(Player p) {
            p.Message(Locale.Get("possess.help1", p));
            p.Message(Locale.Get("possess.help2", p));
            p.Message(Locale.Get("possess.help3", p));
            p.Message(Locale.Get("possess.help4", p));
        }
    }
}
