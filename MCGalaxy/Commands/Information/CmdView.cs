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

namespace MCGalaxy.Commands.Info 
{
    public sealed class CmdView : Command2 
    {
        public override string name { get { return "View"; } }
        public override string type { get { return CommandTypes.Information; } }
        public override bool UseableWhenFrozen { get { return true; } }
        
        public override void Use(Player p, string message, CommandData data) {
            if (!Directory.Exists("extra/text/")) 
                Directory.CreateDirectory("extra/text");
            
            if (message.Length == 0) {
                string[] files = Directory.GetFiles("extra/text", "*.txt");
                string all = files.Join(f => Path.GetFileNameWithoutExtension(f));

                if (all.Length == 0) {
                    p.Message(Locale.Get("view.no_files", p));
                } else {
                    p.Message(Locale.Get("view.available_files", p));
                    p.Message(all);
                }
            } else {
                message = Path.GetFileName(message);
                
                if (File.Exists("extra/text/" + message + ".txt")) {
                    string[] lines = File.ReadAllLines("extra/text/" + message + ".txt");
                    p.MessageLines(lines);
                } else {
                    p.Message(Locale.Get("view.file_not_exist", p));
                }
            }
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("view.help1", p));
            p.Message(Locale.Get("view.help2", p));
        }
    }
}