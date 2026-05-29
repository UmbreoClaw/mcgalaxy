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
using System.IO;
using System.Threading;
using MCGalaxy.Levels.IO;
using MCGalaxy.Localization;

namespace MCGalaxy.Commands.World {
    public sealed class CmdMuseum : Command2 {
        public override string name { get { return "Museum"; } }
        public override string type { get { return CommandTypes.World; } }
        public override bool SuperUseable { get { return false; } }

        const string CURRENT_FLAG = "*current";

        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) { LevelOperations.OutputBackups(p, p.level); return; }

            string[] args = message.ToLower().SplitSpaces();
            string mapArg = args.Length > 1 ? args[0] : p.level.MapName;
            string backupArg = args.Length > 1 ? args[1] : args[0];

            string path;
            if (backupArg == CURRENT_FLAG) {
                path = LevelInfo.MapPath(mapArg);
                if (!LevelInfo.MapExists(mapArg)) {
                    if (Directory.Exists(LevelInfo.BackupBasePath(mapArg))) {
                        p.Message(Locale.Get("museum.level_no_exist_backups", p), mapArg);
                        LevelOperations.OutputBackups(p, mapArg, LevelInfo.GetConfig(mapArg));
                    } else {
                        p.Message(Locale.Get("museum.level_no_exist", p), mapArg);
                    }
                    return;
                }
            } else {
                if (!LevelInfo.GetBackupPath(p, mapArg, backupArg, out path)) return;
            }

            string formattedMuseumName;
            if (backupArg == CURRENT_FLAG) {
                formattedMuseumName = "&cMuseum &S(" + mapArg + ")";
            } else {
                formattedMuseumName = "&cMuseum &S(" + mapArg + " " + backupArg + ")";
            }
            
            if (p.level.name.CaselessEq(formattedMuseumName)) {
                p.Message(Locale.Get("museum.already_in", p)); return;
            }
            if (Interlocked.CompareExchange(ref p.LoadingMuseum, 1, 0) == 1) {
                p.Message(Locale.Get("museum.already_loading", p)); return;
            }
            
            try {
                Level lvl = LevelActions.LoadMuseum(p, formattedMuseumName, mapArg, path);
                PlayerActions.ChangeMap(p, lvl);
            } finally {
                Interlocked.Exchange(ref p.LoadingMuseum, 0);
            }
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("museum.help1", p));
            p.Message(Locale.Get("museum.help2", p));
            p.Message(Locale.Get("museum.help3", p), LevelInfo.LATEST_MUSEUM_FLAG);
            p.Message(Locale.Get("museum.help4", p));
            p.Message(Locale.Get("museum.help5", p), CURRENT_FLAG);
            p.Message(Locale.Get("museum.help6", p));
            p.Message(Locale.Get("museum.help7", p));
        }
    }
}
