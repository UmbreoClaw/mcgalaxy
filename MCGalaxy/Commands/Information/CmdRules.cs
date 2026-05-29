/*
    Copyright 2010 MCSharp team (Modified for use with MCZall/MCLawl/MCForge)
    
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
using MCGalaxy.Util;

namespace MCGalaxy.Commands.Info 
{
    public sealed class CmdRules : Command2 
    {
        public override string name { get { return "Rules"; } }
        public override string type { get { return CommandTypes.Information; } }
        public override CommandPerm[] ExtraPerms {
            get { return new[] { new CommandPerm(LevelPermission.Builder, "can send rules to others") }; }
        }
        public override CommandAlias[] Aliases {
            get { return new[] { new CommandAlias("Agree", "agree"), new CommandAlias("Disagree", "disagree") }; }
        }
        public override bool UseableWhenFrozen { get { return true; } }
        
        public override void Use(Player p, string message, CommandData data) {
            TextFile rulesFile = TextFile.Files["Rules"];
            rulesFile.EnsureExists();
            
            if (message.CaselessEq("agree")) { Agree(p); return; }
            if (message.CaselessEq("disagree")) { Disagree(p, data); return; }
            
            Player target = p;
            if (message.Length > 0) {
                if (!CheckExtraPerm(p, data, 1)) return;
                target = PlayerInfo.FindMatches(p, message);
                if (target == null) return;
            }
            if (target != null) target.hasreadrules = true;

            string[] rules = rulesFile.GetText();
            target.Message(Locale.Get("rules.header", p));
            target.MessageLines(rules);

            if (target != null && p != target) {
                p.Message(Locale.Get("rules.sent_to", p), p.FormatNick(target));
                target.Message(Locale.Get("rules.sent_by", target), target.FormatNick(p));
            }
        }
        
        void Agree(Player p) {
            if (p.IsSuper) { p.Message(Locale.Get("rules.agree_ingame_only", p)); return; }
            if (!Server.Config.AgreeToRulesOnEntry) { p.Message(Locale.Get("rules.agree_not_enabled", p)); return; }
            if (!p.hasreadrules) { p.Message(Locale.Get("rules.must_read_first", p)); return; }

            if (!Server.agreed.Add(p.name)) {
                p.Message(Locale.Get("rules.already_agreed", p));
            } else {
                p.agreed = true;
                p.Message(Locale.Get("rules.agreed_thanks", p));
                Server.agreed.Save(false);
            }
        }

        void Disagree(Player p, CommandData data) {
            if (p.IsSuper) { p.Message(Locale.Get("rules.disagree_ingame_only", p)); return; }
            if (!Server.Config.AgreeToRulesOnEntry) { p.Message(Locale.Get("rules.agree_not_enabled", p)); return; }

            if (data.Rank > LevelPermission.Guest) {
                p.Message(Locale.Get("rules.rank_prevents_disagree", p)); return;
            }
            p.Leave(Locale.Get("rules.leave_reason"));
        }

        public override void Help(Player p) {
            if (HasExtraPerm(p, p.Rank, 1)) {
                p.Message(Locale.Get("rules.help_send", p));
            }
            p.Message(Locale.Get("rules.help1", p));
            p.Message(Locale.Get("rules.help2", p));
            p.Message(Locale.Get("rules.help3", p));
        }
    }
}
