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
using System.Collections.Generic;
using System.IO;
using System.Threading;
using MCGalaxy.DB;
using MCGalaxy.SQL;

namespace MCGalaxy.Commands.Maintenance {
    public sealed class CmdServer : Command2 {
        public override string name { get { return "Server"; } }
        public override string shortcut { get { return "Serv"; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Admin; } }

        public override void Use(Player p, string message, CommandData data) {
            string[] args = message.SplitSpaces();
            switch (args[0].ToLower()) {
                    case "public": SetPublic(p, args); break;
                    case "private": SetPrivate(p, args); break;
                    case "reload": DoReload(p, args); break;
                    case "backup": DoBackup(p, args); break;
                    case "restore": DoRestore(p); break;
                    case "import": DoImport(p, args); break;
                    case "update" : p.Message(Locale.Get("server.use_update", p)); break;
                    case "upgradeblockdb": DoBlockDBUpgrade(p, args); break;
                    default: Help(p); break;
            }
        }
        
        void SetPublic(Player p, string[] args) {
            Server.Config.Public = true;
            p.Message(Locale.Get("server.now_public", p));
            Logger.Log(LogType.SystemActivity, "Server is now public!");
            SrvProperties.Save();
        }

        void SetPrivate(Player p, string[] args) {
            Server.Config.Public = false;
            p.Message(Locale.Get("server.now_private", p));
            Logger.Log(LogType.SystemActivity, "Server is now private!");
            SrvProperties.Save();
        }

        void DoReload(Player p, string[] args) {
            p.Message(Locale.Get("server.reloading", p));
            Server.LoadAllSettings();
            Server.LoadPlayerLists();
            p.Message(Locale.Get("server.reloaded", p));
        }
        
        void DoBackup(Player p, string[] args) {
            string type  = args.Length > 1 ? args[1] : "";
            string value = args.Length > 2 ? args[2] : "";
            
            if (type.CaselessEq("table")) {
                if (value.Length == 0) { p.Message(Locale.Get("server.table_name_required", p)); return; }
                if (!Formatter.ValidName(p, value, "table")) return;
                if (!Database.TableExists(value)) { p.Message(Locale.Get("server.table_not_exist", p), value); return; }

                p.Message(Locale.Get("server.table_backup_start", p), value);
                using (StreamWriter sql = new StreamWriter(value + ".sql")) {
                    Backup.BackupTable(value, sql);
                }
                p.Message(Locale.Get("server.table_backup_done", p), value);
                return;
            }
            
            bool compress = true;
            if (value.Length > 0 && !CommandParser.GetBool(p, value, ref compress)) return;
            
            if (type.Length == 0 || type.CaselessEq("all")) {
                p.Message(Locale.Get("server.backup_start", p));
                Backup.Perform(p, true, true, false, compress);
            } else if (type.CaselessEq("database") || type.CaselessEq("db")) {
                p.Message(Locale.Get("server.db_backup_start", p));
                Backup.Perform(p, false, true, false, compress);
            } else if (type.CaselessEq("files") || type.CaselessEq("file")) {
                p.Message(Locale.Get("server.files_backup_start", p));
                Backup.Perform(p, true, false, false, compress);
            } else if (type.CaselessEq("lite")) {
                p.Message(Locale.Get("server.lite_backup_start", p));
                Backup.Perform(p, true, true, true, compress);
            } else {
                Help(p);
            }
        }
        
        static void DoRestore(Player p) {
            if (!CheckPerms(p)) {
                p.Message(Locale.Get("server.restore_perms", p)); return;
            }
            Backup.Extract(p);
        }

        static bool CheckPerms(Player p) {
            if (p.IsConsole) return true;
            if (Server.Config.OwnerName.CaselessEq("Notch")) return false;
            return p.name.CaselessEq(Server.Config.OwnerName);
        }
        
        void DoImport(Player p, string[] args) {
            if (args.Length == 1) { p.Message(Locale.Get("server.import_table_required", p)); return; }
            if (!Formatter.ValidName(p, args[1], "table")) return;
            if (!File.Exists(args[1] + ".sql")) { p.Message(Locale.Get("server.import_file_not_exist", p), args[1]); return; }

            p.Message(Locale.Get("server.import_start", p), args[1]);
            using (Stream fs = File.OpenRead(args[1] + ".sql"))
                Backup.ImportSql(fs);
            p.Message(Locale.Get("server.import_done", p), args[1]);
        }
        
        void DoBlockDBUpgrade(Player p, string[] args) {
            if (args.Length == 1 || !args[1].CaselessEq("confirm")) {
                p.Message(Locale.Get("server.upgradeblockdb_explain", p));
                p.Message(Locale.Get("server.upgradeblockdb_note", p), Server.SoftwareName);
                p.MessageLines(DBUpgrader.CompactMessages);
                p.Message(Locale.Get("server.upgradeblockdb_confirm", p));
            } else if (DBUpgrader.Upgrading) {
                p.Message(Locale.Get("server.upgradeblockdb_inprogress", p));
            } else {
                try {
                    DBUpgrader.Lock();
                    DBUpgrader.Upgrade();
                } finally {
                    DBUpgrader.Unlock();
                }
            }
        }
        
        public override void Help(Player p, string message) {
            if (message.CaselessEq("backup")) {
                p.Message(Locale.Get("server.help_backup1", p));
                p.Message(Locale.Get("server.help_backup2", p));
                p.Message(Locale.Get("server.help_backup3", p));
                p.Message(Locale.Get("server.help_backup4", p));
                p.Message(Locale.Get("server.help_backup5", p));
                p.Message(Locale.Get("server.help_backup6", p));
                p.Message(Locale.Get("server.help_backup7", p));
            } else {
                base.Help(p, message);
            }
        }

        public override void Help(Player p) {
            p.Message(Locale.Get("server.help1", p));
            p.Message(Locale.Get("server.help2", p));
            p.Message(Locale.Get("server.help3", p));
            p.Message(Locale.Get("server.help4", p));
            p.Message(Locale.Get("server.help5", p));
            p.Message(Locale.Get("server.help6", p));
            p.Message(Locale.Get("server.help7", p));
            p.Message(Locale.Get("server.help8", p), Server.SoftwareName);
        }
    }
}
