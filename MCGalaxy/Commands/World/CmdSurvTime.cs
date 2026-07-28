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
using MCGalaxy.Network;

namespace MCGalaxy.Commands.World
{
    /// <summary> Shows or sets the shared survival world clock (day/night cycle,
    /// pushed to all survival players). Split out of /Survival ("/Survival time"). </summary>
    public sealed class CmdSurvTime : Command2
    {
        public override string name { get { return "SurvTime"; } }
        public override string type { get { return CommandTypes.World; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }

        public override void Use(Player p, string message, CommandData data) {
            // the clock is per-map: console (no current level) reads/sets main,
            // matching the other survival commands' console fallback
            Level lvl = p.level ?? Server.mainLevel;
            if (lvl == null) { p.Message("No level to inspect."); return; }
            if (p.level == null) p.Message("(console: using the main level {0}&S)", lvl.ColoredName);
            if (lvl.Config.SurvivalMode == SurvivalMode.Off)
                p.Message("&WNote: {0} &Wis not a survival map - its clock never advances.", lvl.ColoredName);
            if (message.Length == 0) {
                int t = SurvivalNet.WorldTimeOf(lvl);
                p.Message("World time: &b{0}&S ({1}&S), sky light &b{2}&S/15",
                          t, SurvivalMobs.DescribeTime(t), SurvivalNet.CurrentSkyLightPublic(lvl));
                p.Message("Cycle: 3600 noon, 9600 sunset, 15600 midnight, 21600 sunrise (20 min/day).");
                p.Message("Set with &T/SurvTime [day/noon/sunset/night/midnight/dawn/<ticks>]");
                return;
            }
            // Indev's celestial angle leads the clock by 0.15 of a day, so these
            // are NOT the standard Minecraft tick values - each one is the time
            // that genuinely produces that sky (see SurvivalNet.SkyLight).
            int time;
            switch (message.ToLower()) {
                case "dawn": case "sunrise": time = 21600; break; // mid dawn ramp
                case "day":  case "morning": time = 1000;  break; // full daylight
                case "noon":                 time = 3600;  break; // brightest
                case "sunset": case "dusk":  time = 9600;  break; // mid dusk ramp
                case "night":                time = 13000; break; // fully dark
                case "midnight":             time = 15600; break; // darkest
                default:
                    if (!int.TryParse(message, out time)) {
                        p.Message("&WNot a time: {0}", message); return;
                    }
                    break;
            }
            SurvivalNet.SetWorldTime(lvl, time);
            p.Message("World time set to &b{0}&S ({1}&S) - pushed to all survival players.",
                      SurvivalNet.WorldTimeOf(lvl), SurvivalMobs.DescribeTime(SurvivalNet.WorldTimeOf(lvl)));
        }

        public override void Help(Player p) {
            p.Message("&T/SurvTime &H- shows the survival world clock");
            p.Message("&T/SurvTime [day/noon/sunset/night/midnight/dawn/<ticks>]");
            p.Message("&HSets this map's own day/night clock, applied live.");
        }
    }
}
