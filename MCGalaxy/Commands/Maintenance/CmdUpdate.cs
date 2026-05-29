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
                p.Message(Locale.Get("update.checking", p));
                bool needsUpdating = Updater.NeedsUpdating();
                p.Message(Locale.Get("update.status", p), needsUpdating ? Locale.Get("update.needs_updating", p) : Locale.Get("update.up_to_date", p));
            } else if (message.CaselessEq("latest")) {
                Updater.PerformUpdate(false);
            }  else if (message.Length == 0) {
                Updater.PerformUpdate(true);
            } else {
                Help(p);
            }
        }

        public override void Help(Player p) {
            p.Message(Locale.Get("update.help1", p));
            p.Message(Locale.Get("update.help2", p));
            p.Message(Locale.Get("update.help3", p));
            p.Message(Locale.Get("update.help4", p));
            p.Message(Locale.Get("update.help5", p));
            p.Message(Locale.Get("update.help6", p));
            p.Message(Locale.Get("update.help7", p));
        }
    }
}
