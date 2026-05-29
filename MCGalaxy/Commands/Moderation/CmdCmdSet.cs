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
namespace MCGalaxy.Commands.Moderation 
{
    public sealed class CmdCmdSet : ItemPermsCmd 
    {
        public override string name { get { return "CmdSet"; } }

        public override void Use(Player p, string message, CommandData data) {
            string[] args = message.SplitSpaces(3);
            if (args.Length < 2) { Help(p); return; }
            
            string cmdName = args[0], cmdArgs = "", msg;
            Command.Search(ref cmdName, ref cmdArgs);
            Command cmd = Command.Find(cmdName);
            
            if (cmd == null) { p.Message(Locale.Get("cmd.cmdset.msg1", p)); return; }
            
            if (!p.CanUse(cmd)) {
                cmd.Permissions.MessageCannotUse(p);
                p.Message(Locale.Get("cmd.cmdset.msg2", p), cmd.name); return;
            }
            
            if (args.Length == 2) {
                msg = SetPerms(p, args, data, cmd.Permissions, "command", "use", "usable");
                if (msg != null) 
                    UpdateCommandPerms(cmd.Permissions, p, msg);
            } else {
                
                int num = 0;
                if (!CommandParser.GetInt(p, args[2], "Extra permission number", ref num)) return;
                
                CommandExtraPerms perms = CommandExtraPerms.Find(cmd.name, num);
                if (perms == null) {
                    p.Message(Locale.Get("cmd.cmdset.msg3", p)); return;
                }
                
                msg = SetPerms(p, args, data, perms, "extra permission", "use", "usable");
                if (msg != null) 
                    UpdateExtraPerms(perms, p, msg);
            }
        }
        
        void UpdateCommandPerms(ItemPerms perms, Player p, string msg) {
            CommandPerms.Save();
            CommandPerms.ApplyChanges();
            Announce(p, perms.ItemName + msg);
        }
        
        void UpdateExtraPerms(CommandExtraPerms perms, Player p, string msg) {
            CommandExtraPerms.Save();
            //Announce(p, cmd.name + "&S's extra permission " + idx + " was set to " + grp.ColoredName);
            Announce(p, perms.CmdName + " extra permission #" + perms.Num + msg);
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("cmd.cmdset.help1", p));
            p.Message(Locale.Get("cmd.cmdset.help2", p));
            p.Message(Locale.Get("cmd.cmdset.help3", p));
            p.Message(Locale.Get("cmd.cmdset.help4", p));
            p.Message(Locale.Get("cmd.cmdset.help5", p));
            p.Message(Locale.Get("cmd.cmdset.help6", p));
        }
        
        public override void Help(Player p, string message) {
            if (!message.CaselessEq("advanced")) { base.Help(p, message); return; }
            
            p.Message(Locale.Get("cmd.cmdset.help7", p));
            p.Message(Locale.Get("cmd.cmdset.help8", p));
            p.Message(Locale.Get("cmd.cmdset.help9", p));
            p.Message(Locale.Get("cmd.cmdset.help10", p));
        }
    }
}
