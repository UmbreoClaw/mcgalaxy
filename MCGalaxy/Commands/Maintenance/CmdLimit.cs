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

namespace MCGalaxy.Commands.Maintenance {
    public sealed class CmdLimit : Command2 {        
        public override string name { get { return "Limit"; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Admin; } }

        public override void Use(Player p, string message, CommandData data) {
            string[] args = message.SplitSpaces();
            if (message.Length == 0) { Help(p); return; }
            bool hasLimit = args.Length > 1;
            
            if (args[0].CaselessEq("rt") || args[0].CaselessEq("reloadthreshold")) {
                float threshold = 0;
                if (hasLimit && !CommandParser.GetReal(p, args[1], "Limit", ref threshold, 0, 100)) return;
                
                SetLimitPercent(p, ref Server.Config.DrawReloadThreshold, threshold, hasLimit);
                return;
            }
            
            int limit = 0;
            if (hasLimit && !CommandParser.GetInt(p, args[1], "Limit", ref limit, 0)) return;
            
            switch (args[0].ToLower()) {
                case "rp":
                case "restartphysics":
                    SetLimit(p, "Custom /rp limit", ref Server.Config.PhysicsRestartLimit, limit, hasLimit);
                    return;
                case "rpnormal":
                    SetLimit(p, "Normal /rp limit", ref Server.Config.PhysicsRestartNormLimit, limit, hasLimit);
                    return;
                case "pu":
                case "physicsundo":
                    SetLimit(p, "Physics undo max entries", ref Server.Config.PhysicsUndo, limit, hasLimit);
                    return;
            }

            if (args.Length < 2) { Help(p); return; }
            if (args.Length == 2) { p.Message(Locale.Get("limit.need_rank", p)); return; }
            Group grp = Matcher.FindRanks(p, args[2]);
            if (grp == null) return;

            switch (args[0].ToLower()) {
                case "draw":
                    Chat.MessageAll(string.Format(Locale.Get("limit.draw_set"), grp.ColoredName, limit));
                    grp.DrawLimit = limit; break;
                case "maxundo":
                    Chat.MessageAll(string.Format(Locale.Get("limit.undo_set"), grp.ColoredName, limit));
                    grp.MaxUndo = TimeSpan.FromSeconds(limit); break;
                case "gen":
                    Chat.MessageAll(string.Format(Locale.Get("limit.gen_set"), grp.ColoredName, limit));
                    grp.GenVolume = limit; break;
                case "realms":
                    Chat.MessageAll(string.Format(Locale.Get("limit.realms_set"), grp.ColoredName, limit));
                    grp.OverseerMaps = limit; break;
                default:
                    Help(p); return;
            }
            Group.SaveAll(Group.GroupList);
        }
        
        static void SetLimitPercent(Player p, ref float target, float value, bool hasValue) {
            string type = Locale.Get("limit.reload_threshold");
            if (hasValue) target = value / 100.0f;
            string percent = (target * 100).ToString("F2") + "%";

            if (!hasValue) {
                p.Message(type + ": &b" + percent);
            } else {
                Chat.MessageAll(string.Format(Locale.Get("limit.type_set_to"), type, "&b" + percent));
                SrvProperties.Save();
            }
        }

        static void SetLimit(Player p, string type, ref int target, int value, bool hasValue) {
            if (!hasValue) {
                p.Message(type + ": &b" + target);
            } else {
                target = value;
                Chat.MessageAll(string.Format(Locale.Get("limit.type_set_to"), type, "&b" + target));
                SrvProperties.Save();
            }
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("limit.help1", p));
            p.Message(Locale.Get("limit.help2", p));
            p.Message(Locale.Get("limit.help3", p));
            p.Message(Locale.Get("limit.help4", p));
            p.Message(Locale.Get("limit.help5", p));
            p.Message(Locale.Get("limit.help6", p));
        }
    }
}
