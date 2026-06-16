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
using MCGalaxy.DB;
using MCGalaxy.SQL;

namespace MCGalaxy.Commands.Chatting 
{
    public sealed class CmdSend : Command2 
    {
        public override string name { get { return "Send"; } }
        public override string type { get { return CommandTypes.Chat; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Builder; } }
        public override bool UseableWhenFrozen { get { return true; } }
        public override CommandParallelism Parallelism { get { return CommandParallelism.NoAndWarn; } }
        
        public override void Use(Player p, string message, CommandData data) {
            string[] parts = message.SplitSpaces(2);
            if (parts.Length <= 1) { Help(p); return; }
            if (!MessageCmd.CanSpeak(p, "Send")) return;

            string name = PlayerDB.MatchNames(p, parts[0]);
            if (name == null) return;
            message = parts[1];
            
            if (message.Length >= 256 && Database.Backend.EnforcesTextLength) {
                message = message.Substring(0, 255);
                p.Message(Locale.Get("send.msg_trimmed", p));
                p.Message(message);
            }
            Database.CreateTable("Inbox" + name, createInbox);
            
            int pending = Database.CountRows("Inbox" + name, "WHERE PlayerFrom=@0", p.name);
            if (pending >= 200) {
                Pronouns pronouns = Pronouns.GetFor(name)[0];
                p.Message(Locale.Get("send.inbox_full", p),
                          p.FormatNick(name), pronouns.Object, pronouns.Subject, pronouns.PresentPerfectVerb, pronouns.Object);
                return;
            }
            
            const string fmt = "INSERT INTO {table} (PlayerFrom, TimeSent, Contents)" +
                                            " VALUES (@0, @1, @2)";
            
            string sql = SqlUtils.WithTable(fmt, "Inbox" + name);
            Database.Execute(sql,
                            p.name, DateTime.Now.ToInvariantDateString(), message);
            p.CheckForMessageSpam();

            Player target = PlayerInfo.FindExact(name);
            p.Message(Locale.Get("send.sent", p), p.FormatNick(name));
            if (target == null) return;

            if (!Chat.Ignoring(target, p)) {
                target.Message(Locale.Get("send.received", target), target.FormatNick(p));
            }
        }
        
        static ColumnDesc[] createInbox = new ColumnDesc[] {
            new ColumnDesc("PlayerFrom", ColumnType.Char, 20),
            new ColumnDesc("TimeSent", ColumnType.DateTime),
            new ColumnDesc("Contents", ColumnType.VarChar, 255),
        };
        
        public override void Help(Player p) {
            p.Message(Locale.Get("send.help1", p));
            p.Message(Locale.Get("send.help2", p));
        }
    }
}
