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
namespace MCGalaxy.Commands.Chatting 
{
    public sealed class CmdWhisper : Command2 
    {
        public override string name { get { return "Whisper"; } }
        public override string type { get { return CommandTypes.Chat; } }
        public override bool SuperUseable { get { return false; } }
        public override bool UseableWhenFrozen { get { return true; } }
        public override bool MessageBlockRestricted { get { return true; } }
        public override bool UpdatesLastCmd { get { return false; } }

        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) {
                p.whisper = !p.whisper; p.whisperTo = "";
                if (p.whisper) p.Message(Locale.Get("whisper.enabled", p));
                else p.Message(Locale.Get("whisper.disabled", p));
            } else {
                Player target = PlayerInfo.FindMatches(p, message);
                if (target == null) { p.whisperTo = ""; p.whisper = false; return; }

                p.whisper   = true;
                p.whisperTo = target.name;
                p.Message(Locale.Get("whisper.auto_target", p), p.FormatNick(target));
            }
        }

        public override void Help(Player p) {
            p.Message(Locale.Get("whisper.help1", p));
            p.Message(Locale.Get("whisper.help2", p));
        }
    }
}
