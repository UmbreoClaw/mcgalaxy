/*
 * Written By Jack1312

    Copyright 2010 MCSharp team (Modified for use with MCZall/MCLawl/MCForge)
    
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
using System.Collections.Generic;
using System.IO;
using MCGalaxy.DB;
using MCGalaxy.Events;

namespace MCGalaxy.Commands.Moderation {
    public sealed class CmdReport : Command2 {
        public override string name { get { return "Report"; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override CommandPerm[] ExtraPerms {
            get { return new[] { new CommandPerm(LevelPermission.Operator, "can manage reports") }; }
        }
        public override CommandAlias[] Aliases {
            get { return new CommandAlias[] { new CommandAlias("Reports", "list") }; }
        }

        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) { Help(p); return; }
            string[] args = message.SplitSpaces(2);
            if (!Directory.Exists("extra/reported"))
                Directory.CreateDirectory("extra/reported");

            string cmd = args[0];
            if (IsListAction(cmd)) {
                HandleList(p, args, data);
            } else if (cmd.CaselessEq("clear")) {
                HandleClear(p, args, data);
            } else if (IsDeleteAction(cmd)) {
                HandleDelete(p, args, data);
            } else if (IsInfoAction(cmd)) {
                HandleCheck(p, args, data);
            } else {
                HandleAdd(p, args);
            }
        }
        
        void HandleList(Player p, string[] args, CommandData data) {
            if (!CheckExtraPerm(p, data, 1)) return;
            string[] users = GetReportedUsers();
            
            if (users.Length > 0) {
                p.Message(Locale.Get("report.list_header", p));
                string modifier = args.Length > 1 ? args[1] : "";
                Paginator.Output(p, users, pl => p.FormatNick(pl),
                                 "Review list", "players", modifier);

                p.Message(Locale.Get("report.list_check", p));
                p.Message(Locale.Get("report.list_delete", p));
            } else {
                p.Message(Locale.Get("report.no_reports", p));
            }
        }
        
        void HandleCheck(Player p, string[] args, CommandData data) {
            if (args.Length != 2) {
                p.Message(Locale.Get("report.need_name", p)); return;
            }
            if (!CheckExtraPerm(p, data, 1)) return;
            
            string target = PlayerDB.MatchNames(p, args[1]);
            if (target == null) return;
            string nick = p.FormatNick(target);
            
            if (!HasReports(target)) {
                p.Message(Locale.Get("report.not_reported", p), nick); return;
            }

            string[] reports = File.ReadAllLines("extra/reported/" + target + ".txt");
            p.MessageLines(reports);
        }
        
        void HandleDelete(Player p, string[] args, CommandData data) {
            if (args.Length != 2) {
                p.Message(Locale.Get("report.need_name", p)); return;
            }
            if (!CheckExtraPerm(p, data, 1)) return;
            
            string target = PlayerDB.MatchNames(p, args[1]);
            if (target == null) return;
            string nick = p.FormatNick(target);
            
            if (!HasReports(target)) {
                p.Message(Locale.Get("report.not_reported", p), nick); return;
            }
            if (!Directory.Exists("extra/reportedbackups"))
                Directory.CreateDirectory("extra/reportedbackups");
            
            DeleteReport(target);
            p.Message(Locale.Get("report.deleted", p), nick);
            Chat.MessageFromOps(p, Locale.Get("report.deleted_ops") + nick);
            Logger.Log(LogType.UserActivity, "Reports on {1} were deleted by {0}", p.name, target);
        }
        
        void HandleClear(Player p, string[] args, CommandData data) {
            if (!CheckExtraPerm(p, data, 1)) return;
            if (!Directory.Exists("extra/reportedbackups"))
                Directory.CreateDirectory("extra/reportedbackups");
            
            string[] users = GetReportedUsers();
            foreach (string user in users) { DeleteReport(user); }
            
            p.Message(Locale.Get("report.cleared", p));
            Chat.MessageFromOps(p, Locale.Get("report.cleared_ops"));
            Logger.Log(LogType.UserActivity, p.name + " cleared ALL reports!");
        }
        
        void HandleAdd(Player p, string[] args) {
            if (args.Length != 2) {
                p.Message(Locale.Get("report.need_reason", p)); return;
            }
            
            string target = PlayerDB.MatchNames(p, args[0]);
            if (target == null) return;
            string nick = p.FormatNick(target);

            List<string> reports = new List<string>();
            if (HasReports(target)) {
                reports = Utils.ReadAllLinesList(ReportPath(target));
            }
            ItemPerms checkPerms = CommandExtraPerms.Find(name, 1);
            
            if (reports.Count >= 5) {
                p.Message(Locale.Get("report.too_many", p),
                          nick, CommandExtraPerms.Find(name, 1).Describe());
                return;
            }
            
            string reason = ModActionCmd.ExpandReason(p, args[1]);
            if (reason == null) return;
            
            reports.Add(reason + " - Reported by " + p.name + " at " + DateTime.Now);
            File.WriteAllLines(ReportPath(target), reports.ToArray());
            p.Message(Locale.Get("report.sent", p),
                      checkPerms.Describe());
            
            ModAction action = new ModAction(target, p, ModActionType.Reported, reason);
            OnModActionEvent.Call(action);
            if (!action.Announce) return;
            
            string opsMsg = "λNICK &Sreported " + nick + "&S. Reason: " + reason;
            Chat.MessageFrom(ChatScope.Perms, p, opsMsg, checkPerms, null, true);
            string allMsg = "Use &T/Report check " + target + " &Sto see all of " + Pronouns.GetFor(target)[0].Object + " reports";
            Chat.MessageFrom(ChatScope.Perms, p, allMsg, checkPerms, null, true);
        }
        
        
        static bool HasReports(string user) {
            return File.Exists(ReportPath(user));
        }
        static string ReportPath(string user) {
            return "extra/reported/" + user + ".txt";
        }
                
        static string[] GetReportedUsers() {
            string[] users = Directory.GetFiles("extra/reported", "*.txt");
            for (int i = 0; i < users.Length; i++) {
                users[i] = Path.GetFileNameWithoutExtension(users[i]);
            }
            return users;
        }
        
        static void DeleteReport(string user) {
            string backup = "extra/reportedbackups/" + user + ".txt";
            FileIO.TryDelete(backup);           
            File.Move(ReportPath(user), backup);
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("report.help1", p));
            p.Message(Locale.Get("report.help2", p));
            p.Message(Locale.Get("report.help3", p));
            p.Message(Locale.Get("report.help4", p));
            p.Message(Locale.Get("report.help5", p));
        }
    }
}
