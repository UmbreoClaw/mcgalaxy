/*
    Copyright 2010 MCSharp team (Modified for use with MCZall/MCLawl/MCForge)
    
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
using System.IO;

namespace MCGalaxy.Commands.World {
    public sealed class CmdRestore : Command2 {        
        public override string name { get { return "Restore"; } }
        public override string type { get { return CommandTypes.World; } }
        public override bool museumUsable { get { return false; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }
        public override bool MessageBlockRestricted { get { return true; } }
        
        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) { LevelOperations.OutputBackups(p, p.level); return; }
            
            Level lvl;
            string[] args = message.SplitSpaces();
            if (args.Length >= 2) {
                lvl = Matcher.FindLevels(p, args[1]);
                if (lvl == null) return;
            } else {
                if (p.IsSuper) {
                    SuperRequiresArgs(p, "level name"); return;
                }
                lvl = p.level;
            }

            if (!LevelInfo.Check(p, data.Rank, lvl, "restore a backup of this level")) return;
            string path = LevelInfo.BackupFilePath(lvl.name, args[0]);
            
            if (File.Exists(path)) {
                DoRestore(lvl, args[0]);
            } else {
                p.Message(Locale.Get("restore.no_backup", p), args[0]);
                LevelOperations.OutputBackups(p, lvl);
            }
        }
        
        static void DoRestore(Level lvl, string backup) {
            lock (lvl.saveLock) {
                File.Copy(LevelInfo.BackupFilePath(lvl.name, backup), LevelInfo.MapPath(lvl.name), true);
                lvl.SaveChanges = false;
            }
            
            Level restore = Level.Load(lvl.name);
            if (restore != null) {
                LevelActions.Replace(lvl, restore);
            } else {
                Logger.Log(LogType.Warning, "Restore nulled");
                File.Copy(LevelInfo.MapPath(lvl.name) + ".backup", LevelInfo.MapPath(lvl.name), true);
            }
        }

        public override void Help(Player p) {
            p.Message(Locale.Get("restore.help1", p));
            p.Message(Locale.Get("restore.help2", p));
            p.Message(Locale.Get("restore.help3", p));
            p.Message(Locale.Get("restore.help4", p));
        }
    }
}
