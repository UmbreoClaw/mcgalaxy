/*
    Copyright 2015-2024 MCGalaxy
        
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

namespace MCGalaxy.Commands.CPE 
{    
    public sealed class CmdCustomColors : Command2 
    {        
        public override string name { get { return "CustomColors"; } }
        public override string shortcut { get { return "ccols"; } }
        public override string type { get { return CommandTypes.Chat; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Admin; } }

        public override void Use(Player p, string message, CommandData data) {
            string[] args = message.SplitSpaces();
            if (message.Length == 0) { Help(p); return; }
            string cmd = args[0];
            
            if (IsCreateAction(cmd)) {
                AddHandler(p, args);
            } else if (IsDeleteAction(cmd)) {
                RemoveHandler(p, args);
            } else if (IsEditAction(cmd)) {
                EditHandler(p, args);
            } else if (IsListAction(cmd)) {
                string modifer = args.Length > 1 ? args[1] : "";
                ListHandler(p, "ccols list", modifer);
            } else {
                Help(p);
            }
        }
        
        void AddHandler(Player p, string[] args) {
            if (args.Length <= 4) { Help(p); return; }         
            char code = args[1][0];
            if (code >= 'A' && code <= 'F') code += ' ';
            
            if (code == ' ' || code == '\0' || code == '\u00a0' || code == '%' || code == '&') {
                p.Message(Locale.Get("cmd.customcolors.msg1", p));
                return;
            }            
            if (Colors.IsSystem(code)) {
                p.Message(Locale.Get("cmd.customcolors.msg2", p));
                return;
            }
            
            char fallback;
            if (!CheckName(p, args[2]) || !CheckFallback(p, args[3], code, out fallback)) return;
            ColorDesc col = default(ColorDesc);
            if (!CommandParser.GetHex(p, args[4], ref col)) return;
            
            col.Code = code; col.Fallback = fallback; col.Name = args[2];
            Colors.Update(col);
            p.Message(Locale.Get("cmd.customcolors.msg3", p), code);
        }
        
        void RemoveHandler(Player p, string[] args) {
            if (args.Length < 2) { Help(p); return; }
            
            char code = ParseColor(p, args[1]);
            if (code == '\0') return;
            
            Colors.Update(Colors.DefaultCol(code));
            p.Message(Locale.Get("cmd.customcolors.msg4", p), code);
        }
        
        static void ListHandler(Player p, string cmd, string modifier) {
            List<ColorDesc> validColors = new List<ColorDesc>(Colors.List.Length);
            foreach (ColorDesc color in Colors.List) 
            {
                if (color.IsModified()) validColors.Add(color);
            }
            
            Paginator.Output(p, validColors, PrintColor, 
                             cmd, "Colors", modifier);
        }
        
        // Not very elegant, because we don't want the % to be escaped like everywhere else
        internal static void PrintColor(Player p, ColorDesc col) {
            string format = "{0} &{1}({2})&S - %&S{1}, falls back to &{3}%&{3}{3}";
            if (col.Code == col.Fallback) format = "{0} &{1}({2})&S - %&S{1}";

            p.Message(format, col.Name, col.Code, Utils.Hex(col.R, col.G, col.B), col.Fallback);
        }
        
        void EditHandler(Player p, string[] args) {
            if (args.Length < 4) { Help(p); return; }
            
            char code = ParseColor(p, args[1]);
            if (code == '\0') return;
            ColorDesc col = Colors.Get(code);
            
            if (args[2].CaselessEq("name")) {
                if (!CheckName(p, args[3])) return;

                p.Message(Locale.Get("cmd.customcolors.msg5", p), col.Name, args[3]);
                col.Name = args[3];
            } else if (args[2].CaselessEq("fallback")) {
                char fallback;
                if (!CheckFallback(p, args[3], code, out fallback)) return;
                
                p.Message(Locale.Get("cmd.customcolors.msg6", p), col.Name, fallback);
                col.Fallback = fallback;
            } else if (args[2].CaselessEq("hex") || args[2].CaselessEq("color")) {
                ColorDesc rgb = default(ColorDesc);
                if (!CommandParser.GetHex(p, args[3], ref rgb)) return;
                
                p.Message(Locale.Get("cmd.customcolors.msg7", p), col.Name, Utils.Hex(rgb.R, rgb.G, rgb.B));
                col.R = rgb.R; col.G = rgb.G; col.B = rgb.B;
            } else {
                Help(p); return;
            }
            
            Colors.Update(col);
        }
        
        
        static bool CheckName(Player p, string arg) {
            if (Colors.Parse(arg).Length > 0) {
                p.Message(Locale.Get("cmd.customcolors.msg8", p), arg);
                return false;
            }
            return true;
        }
        
        static char ParseColor(Player p, string arg) {
            if (arg.Length != 1) {
                string colCode = Matcher.FindColor(p, arg);
                if (colCode != null) return colCode[1];
            } else {
                char code = arg[0];
                if (Colors.IsDefined(code)) return code;
                
                p.Message(Locale.Get("cmd.customcolors.msg9", p), code);
                p.Message(Locale.Get("cmd.customcolors.msg10", p));
            }
            return '\0';
        }
        
        static bool CheckFallback(Player p, string arg, char code, out char fallback) {
            fallback = arg[0];
            if (!Colors.IsStandard(fallback)) {
                p.Message(Locale.Get("cmd.customcolors.msg11", p), fallback); return false;
            }
            // Can't change fallback of standard colour code
            if (Colors.IsStandard(code)) fallback = code;
            
            if (fallback >= 'A' && fallback <= 'F') fallback += ' ';
            return true;
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("cmd.customcolors.help1", p));
            p.Message(Locale.Get("cmd.customcolors.help2", p));
            p.Message(Locale.Get("cmd.customcolors.help3", p));
            p.Message(Locale.Get("cmd.customcolors.help4", p));
            p.Message(Locale.Get("cmd.customcolors.help5", p));
            p.Message(Locale.Get("cmd.customcolors.help6", p));
        }
    }
}
