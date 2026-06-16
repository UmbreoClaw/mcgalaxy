/*
    Written by Jack1312
  
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

namespace MCGalaxy.Commands.Chatting 
{
    public sealed class CmdIgnore : Command2
    {
        public override string name { get { return "Ignore"; } }
        public override string type { get { return CommandTypes.Chat; } }
        public override bool SuperUseable { get { return false; } }
        public override bool MessageBlockRestricted { get { return true; } }
        public override CommandAlias[] Aliases {
            get { return new [] { new CommandAlias("Deafen", "all") }; }
        }

        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) { Help(p); return; }
            string[] args = message.SplitSpaces();
            string action = args[0].ToLower();
            
            if (action == "all") {
                Toggle(p, ref p.Ignores.All, Locale.Get("ignore.toggle_all", p)); return;
            } else if (action == "irc") {
                if (args.Length > 1) { IgnoreIRCNick(p, args[1]); }
                else { Toggle(p, ref p.Ignores.IRC, Locale.Get("ignore.toggle_irc", p)); }
                return;
            } else if (action == "titles") {
                Toggle(p, ref p.Ignores.Titles, Locale.Get("ignore.toggle_titles", p)); return;
            } else if (action == "nicks") {
                Toggle(p, ref p.Ignores.Nicks, Locale.Get("ignore.toggle_nicks", p));
                TabList.Update(p, true); return;
            } else if (action == "8ball") {
                Toggle(p, ref p.Ignores.EightBall, Locale.Get("ignore.toggle_8ball", p)); return;
            } else if (action == "drawoutput") {
                Toggle(p, ref p.Ignores.DrawOutput, Locale.Get("ignore.toggle_drawoutput", p)); return;
            } else if (action == "worldchanges") {
                Toggle(p, ref p.Ignores.WorldChanges, Locale.Get("ignore.toggle_worldchanges", p)); return;
            } else if (IsListAction(action)) {
                p.Ignores.Output(p); return;
            }
            
            if (p.Ignores.Names.CaselessRemove(action)) {
                p.Message(Locale.Get("ignore.no_longer", p), action);
            } else {
                int matches;
                Player target = PlayerInfo.FindMatches(p, action, out matches);
                if (target == null) {
                    if (matches == 0) p.Message(Locale.Get("ignore.full_name_required", p));
                    return;
                }

                if (p.Ignores.Names.CaselessRemove(target.name)) {
                    p.Message(Locale.Get("ignore.no_longer", p), p.FormatNick(target));
                } else {
                    p.Ignores.Names.Add(target.name);
                    p.Message(Locale.Get("ignore.now_ignoring", p), p.FormatNick(target));
                }
            }
            p.Ignores.Save(p);
        }
        
        static void Toggle(Player p, ref bool ignore, string format) {
            ignore = !ignore;
            if (format.StartsWith("{0}")) {
                p.Message(format, ignore ? "&cNow" : "&aNo longer");
            } else {
                p.Message(format, ignore ? "no longer" : "now", ignore ? "&c" : "&a");
            }
            p.Ignores.Save(p);
        }
        
        static void IgnoreIRCNick(Player p, string nick) {
            if (p.Ignores.IRCNicks.CaselessRemove(nick)) {
                p.Message(Locale.Get("ignore.no_longer_irc", p), nick);
            } else {
                p.Ignores.IRCNicks.Add(nick);
                p.Message(Locale.Get("ignore.now_irc", p), nick);
            }
            p.Ignores.Save(p);
        }

        public override void Help(Player p) {
            p.Message(Locale.Get("ignore.help1", p));
            p.Message(Locale.Get("ignore.help2", p));
            p.Message(Locale.Get("ignore.help3", p));
            p.Message(Locale.Get("ignore.help4", p));
            p.Message(Locale.Get("ignore.help5", p));
        }

        public override void Help(Player p, string message) {
            if (!message.CaselessEq("special")) { Help(p); return; }
            p.Message(Locale.Get("ignore.help_special1", p));
            p.Message(Locale.Get("ignore.help_special2", p));
            p.Message(Locale.Get("ignore.help_special3", p));
            p.Message(Locale.Get("ignore.help_special4", p));
            p.Message(Locale.Get("ignore.help_special5", p));
            p.Message(Locale.Get("ignore.help_special6", p));
            p.Message(Locale.Get("ignore.help_special7", p));
            p.Message(Locale.Get("ignore.help_special8", p));
            p.Message(Locale.Get("ignore.help_special9", p));
        }
    }
}
