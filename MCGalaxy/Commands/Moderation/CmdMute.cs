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
using MCGalaxy.Events;

namespace MCGalaxy.Commands.Moderation 
{
    public sealed class CmdMute : Command2 
    {
        public override string name { get { return "Mute"; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }

        const string UNMUTE_FLAG = "-unmute";

        public override CommandAlias[] Aliases
        { get { return new[] { new CommandAlias("Unmute", UNMUTE_FLAG) }; } }
        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) { Help(p); return; }
            string[] args = message.SplitSpaces(3);
            string target;

            if (args[0].CaselessEq(UNMUTE_FLAG)) {
                if (args.Length == 1) { Help(p); return; }
                target = PlayerInfo.FindMatchesPreferOnline(p, args[1]);
                if (target == null) return;

                if (!Server.muted.Contains(target)) {
                    p.Message(Locale.Get("mute.not_muted", p), p.FormatNick(target));
                    return;
                }
                
                DoUnmute(p, target, args.Length > 2 ? args[2] : "");
                return;
            }

            target = PlayerInfo.FindMatchesPreferOnline(p, args[0]);
            if (target == null) return;

            if (Server.muted.Contains(target)) {
                p.Message(Locale.Get("mute.already_muted", p), p.FormatNick(target));
                p.Message(Locale.Get("mute.unmute_hint", p), target);
            } else {            
                Group group = ModActionCmd.CheckTarget(p, data, "mute", target);
                if (group == null) return;
                
                DoMute(p, target, args);
            }
        }
        
        void DoMute(Player p, string target, string[] args) {
            TimeSpan duration = Server.Config.ChatSpamMuteTime;
            if (args.Length > 1) {
                if (!CommandParser.GetTimespan(p, args[1], ref duration, "mute for", "s")) return;
            }
            
            string reason = args.Length > 2 ? args[2] : "";
            reason = ModActionCmd.ExpandReason(p, reason);
            if (reason == null) return;
            
            ModAction action = new ModAction(target, p, ModActionType.Muted, reason, duration);
            OnModActionEvent.Call(action);
        }
        
        void DoUnmute(Player p, string target, string reason) {
            reason = ModActionCmd.ExpandReason(p, reason);
            if (reason == null) return;
            if (p.name == target) { p.Message(Locale.Get("mute.cant_unmute_self", p)); return; }
            
            ModAction action = new ModAction(target, p, ModActionType.Unmuted, reason);
            OnModActionEvent.Call(action);
        }

        public override void Help(Player p) {
            p.Message(Locale.Get("mute.help1", p));
            p.Message(Locale.Get("mute.help2", p));
            p.Message(Locale.Get("mute.help3", p));
            p.Message(Locale.Get("mute.help4", p));
            p.Message(Locale.Get("mute.help5", p));
            p.Message(Locale.Get("mute.help6", p));
            p.Message(Locale.Get("mute.help7", p));
        }
    }
}
