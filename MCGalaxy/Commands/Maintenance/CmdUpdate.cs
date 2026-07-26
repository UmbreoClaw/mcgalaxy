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

namespace MCGalaxy.Commands.Maintenance 
{
    public sealed class CmdUpdate : Command2 
    {
        public override string name { get { return "Update"; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Owner; } }

        public override void Use(Player p, string message, CommandData data) {
            if (message.CaselessEq("check")) {
                p.Message("Checking for updates..");
                string latest, current;
                bool needsUpdating = Updater.NeedsUpdating(out latest, out current);
                if (current.Length == 0) current = "unstamped build";

                if (needsUpdating) {
                    p.Message("Server &cneeds updating&S: running {0}, latest is {1}", current, latest);
                } else {
                    p.Message("Server &ais up to date &S- {0}", current);
                }
            } else if (message.CaselessEq("latest")) {
                Updater.PerformUpdate(false);
            }  else if (message.Length == 0) {
                Updater.PerformUpdate(true);
            } else {
                Help(p);
            }
        }

        public override void Help(Player p) {
            p.Message("&T/Update check &H- checks whether a newer build is published");
            p.Message("&T/Update &H- updates to the newest survival-latest CI build");
        }
    }
}
