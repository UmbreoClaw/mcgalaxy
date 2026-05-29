/*
    Copyright 2010 MCLawl Team - Written by Valek (Modified for use with MCForge)
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
using System.Threading;

namespace MCGalaxy.Commands.Misc 
{
    public sealed class CmdRagequit : Command2 
    {
        public override string name { get { return "RageQuit"; } }
        public override string type { get { return CommandTypes.Other; } }
        public override bool MessageBlockRestricted { get { return true; } }
        public override bool SuperUseable { get { return false; } }
        public override bool UseableWhenFrozen { get { return true; } }
        
        public override void Use(Player p, string message, CommandData data) {
            p.Leave("RAGEQUIT!!");
        }

        public override void Help(Player p) {
            p.Message(Locale.Get("ragequit.help1", p));
            p.Message(Locale.Get("ragequit.help2", p));
        }
    }
    
    public sealed class CmdQuit : Command2 
    {
        public override string name { get { return "Quit"; } }
        public override string type { get { return CommandTypes.Other; } }
        public override bool MessageBlockRestricted { get { return true; } }
        public override bool SuperUseable { get { return false; } }
        public override bool UseableWhenFrozen { get { return true; } }
        
        public override void Use(Player p, string message, CommandData data) {
            string msg = message.Length > 0 ? Locale.Get("quit.left_reason") + ": " + message : Locale.Get("quit.left");
            if (p.muted) msg = Locale.Get("quit.left");
            p.Leave(msg);
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("quit.help1", p));
            p.Message(Locale.Get("quit.help2", p));
        }
    }
    
    public sealed class CmdCrashServer : Command2 
    {
        public override string name { get { return "CrashServer"; } }
        public override string shortcut { get { return "Crash"; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override bool MessageBlockRestricted { get { return true; } }
        public override bool SuperUseable { get { return false; } }
        public override bool UseableWhenFrozen { get { return true; } }

        public override void Use(Player p, string message, CommandData data) {
            if (message.Length > 0) { Help(p); return; }
            int code = new Random().Next(int.MinValue, int.MaxValue);

            p.Leave("Server crash! Error code 0x" + code.ToString("X8").TrimStart('0'));
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("crashserver.help1", p));
            p.Message(Locale.Get("crashserver.help2", p));
        }
    }
    
    public sealed class CmdHacks : Command2 
    {
        public override string name { get { return "Hacks"; } }
        public override string shortcut { get { return "Hax"; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override bool MessageBlockRestricted { get { return true; } }
        public override bool SuperUseable { get { return false; } }
        public override bool UseableWhenFrozen { get { return true; } }

        public override void Use(Player p, string message, CommandData data) {
            if (message.Length > 0) {
                p.Message(Locale.Get("hacks.abuse", p));
                Thread.Sleep(3000);
            }
            
            const string msg = "Your IP has been backtraced + reported to FBI Cyber Crimes Unit.";
            p.Leave("kicked (" + msg + ")", msg, false);
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("hacks.help1", p));
            p.Message(Locale.Get("hacks.help2", p));
        }
    }
}
