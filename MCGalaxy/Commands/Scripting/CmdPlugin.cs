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
using MCGalaxy.Scripting;

namespace MCGalaxy.Commands.Scripting 
{
    public sealed class CmdPlugin : Command2 
    {
        public override string name { get { return "Plugin"; } }
        public override string type { get { return CommandTypes.Other; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Owner; } }
        public override CommandAlias[] Aliases {
            get { return new[] { new CommandAlias("PLoad", "load"), new CommandAlias("PUnload", "unload"),
                    new CommandAlias("Plugins", "list") }; }
        }
        public override bool MessageBlockRestricted { get { return true; } }
        
        public override void Use(Player p, string message, CommandData data) {
            string[] args = message.SplitSpaces(2);
            if (IsListAction(args[0])) {
                string modifier = args.Length > 1 ? args[1] : "";
                
                p.Message(Locale.Get("cmd.plugin.msg1", p));
                Paginator.Output(p, Plugin.custom, pl => pl.name,
                                 "Plugins", "plugins", modifier);
                return;
            }
            if (args.Length == 1) { Help(p); return; }
            
            string cmd = args[0], name = args[1];
            if (!Formatter.ValidFilename(p, name)) return;
            
            if (cmd.CaselessEq("load")) {
                string path = IScripting.PluginPath(name);
                ScriptingOperations.LoadPlugins(p, path);
            } else if (cmd.CaselessEq("unload")) {
                UnloadPlugin(p, name);
            } else if (cmd.CaselessEq("create")) {
                p.Message(Locale.Get("cmd.plugin.msg2", p));
            } else if (cmd.CaselessEq("compile")) {
                p.Message(Locale.Get("cmd.plugin.msg3", p));
            } else {
                Help(p);
            }
        }
        
        static void UnloadPlugin(Player p, string name) {
            int matches;
            Plugin plugin = Matcher.Find(p, name, out matches, Plugin.custom, 
                                         null, pln => pln.name, "plugins");
            
            if (plugin == null) return;
            ScriptingOperations.UnloadPlugin(p, plugin);
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("cmd.plugin.help1", p));
            p.Message(Locale.Get("cmd.plugin.help2", p));
            p.Message(Locale.Get("cmd.plugin.help3", p));
            p.Message(Locale.Get("cmd.plugin.help4", p));
            p.Message(Locale.Get("cmd.plugin.help5", p));
            p.Message(Locale.Get("cmd.plugin.help6", p));
        }
    }
}
