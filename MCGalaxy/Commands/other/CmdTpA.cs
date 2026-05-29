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
using System.Threading;

namespace MCGalaxy.Commands.Misc {  
    public sealed class CmdTpA : Command2 {        
        public override string name { get { return "TPA"; } }
        public override string type { get { return CommandTypes.Other; } }
        public override bool SuperUseable { get { return false; } }
        public override CommandAlias[] Aliases {
            get { return new[] { new CommandAlias("TPAccept", "accept"), new CommandAlias("TPDeny", "deny") }; }
        }
        
        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) { Help(p); return; }
            
            if (message.CaselessEq("accept")) {
                DoAccept(p);
            } else if (message.CaselessEq("deny")) {
                DoDeny(p);
            } else {
                DoTpa(p, message);
            }
        }
        
        void DoTpa(Player p, string message) {
            Player target = PlayerInfo.FindMatches(p, message);
            if (target == null) return;
            if (target == p) { p.Message(Locale.Get("tpa.no_self", p)); return; }
            if (target.Ignores.Names.CaselessContains(p.name)) { ShowSentMessage(p, target); return; }
            
            if (target.name.CaselessEq(p.currentTpa)) {
                p.Message(Locale.Get("tpa.pending", p)); return;
            }
            if (p.level != target.level && target.level.IsMuseum) {
                p.Message(Locale.Get("tpa.in_museum", p), p.FormatNick(target)); return;
            }
            if (target.Loading) {
                p.Message(Locale.Get("tpa.waiting", p), p.FormatNick(target));
                target.BlockUntilLoad(10);
            }
            
            ShowSentMessage(p, target);
            ShowRequestMessage(p, target);
            target.senderName = p.name;
            target.Request = true;
            p.currentTpa = target.name;
            
            Thread.Sleep(90000);
            if (target.Request) {
                p.Message(Locale.Get("tpa.timed_out_sender", p));
                target.Message(Locale.Get("tpa.timed_out_target", target));

                target.Request = false;
                target.senderName = "";
                p.currentTpa = "";
            }
        }
        
        static void ShowSentMessage(Player p, Player target) {
            p.Message(Locale.Get("tpa.sent", p), p.FormatNick(target));
            p.Message(Locale.Get("tpa.timeout_notice", p));
        }

        static void ShowRequestMessage(Player p, Player target) {
            if (Chat.Ignoring(target, p)) return;

            target.Message(Locale.Get("tpa.request_received", target), target.FormatNick(p));
            target.Message(Locale.Get("tpa.accept_or_deny", target));
            target.Message(Locale.Get("tpa.timeout_notice", target));
        }
        
        void DoAccept(Player p) {
            if (!p.Request) { p.Message(Locale.Get("tpa.no_requests", p)); return; }

            Player sender = PlayerInfo.FindExact(p.senderName);
            p.Request = false;
            p.senderName = "";
            if (sender == null) {
                p.Message(Locale.Get("tpa.sender_offline", p)); return;
            }

            p.Message(Locale.Get("tpa.accepted", p), p.FormatNick(sender));
            sender.Message(Locale.Get("tpa.sender_accepted", sender), sender.FormatNick(p));
            sender.currentTpa = "";
            
            PlayerOperations.TeleportToEntity(sender, p);
        }
        
        void DoDeny(Player p) {
            if (!p.Request) { p.Message(Locale.Get("tpa.no_requests", p)); return; }

            Player sender = PlayerInfo.FindExact(p.senderName);
            p.Request = false;
            p.senderName = "";
            if (sender == null) {
                p.Message(Locale.Get("tpa.sender_offline", p)); return;
            }

            p.Message(Locale.Get("tpa.denied", p), p.FormatNick(sender));
            sender.Message(Locale.Get("tpa.sender_denied", sender), sender.FormatNick(p));
            sender.currentTpa = "";
        }

        public override void Help(Player p) {
            p.Message(Locale.Get("tpa.help1", p));
            p.Message(Locale.Get("tpa.help2", p));
            p.Message(Locale.Get("tpa.help3", p));
        }
    }
}
