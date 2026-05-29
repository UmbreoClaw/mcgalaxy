/*
    Copyright 2024 MCGalaxy contributors

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
using System.Collections.Generic;

namespace MCGalaxy.Commands.Misc
{
    public sealed class CmdLanguage : Command2
    {
        public override string name  { get { return "Language"; } }
        public override string shortcut { get { return "lang"; } }
        public override string type { get { return CommandTypes.Other; } }
        public override bool UseableWhenFrozen { get { return true; } }
        public override CommandAlias[] Aliases {
            get { return new[] { new CommandAlias("Locale") }; }
        }

        public override void Use(Player p, string message, CommandData data) {
            string[] args = message.SplitSpaces(2);
            string sub = args[0];

            if (sub.Length == 0) {
                ShowCurrent(p);
                return;
            }

            if (sub.CaselessEq("list")) {
                ListLocales(p);
                return;
            }

            if (sub.CaselessEq("reload")) {
                if (p.Rank < LevelPermission.Operator) { p.Message("&WYou do not have permission to reload locales."); return; }
                Locale.Reload();
                p.Message(Locale.Get("locale.reloaded", p));
                return;
            }

            if (sub.CaselessEq("server")) {
                if (args.Length < 2 || args[1].Length == 0) { Help(p); return; }
                if (p.Rank < LevelPermission.Operator) { p.Message("&WYou do not have permission to set the server language."); return; }
                SetServerLocale(p, args[1]);
                return;
            }

            // /Language [code] — set personal language
            SetPlayerLocale(p, sub);
        }

        static void ShowCurrent(Player p) {
            string server = Server.Config.Language;
            string player = string.IsNullOrEmpty(p.Language) ? server : p.Language;
            if (string.IsNullOrEmpty(p.Language)) {
                p.Message(Locale.Get("locale.current_server", p), server);
            } else {
                p.Message(Locale.Get("locale.current_player", p), player, server);
            }
        }

        static void ListLocales(Player p) {
            List<string> available = Locale.AvailableLocales();
            if (available.Count == 0) {
                p.Message(Locale.Get("locale.none_loaded", p));
            } else {
                p.Message(Locale.Get("locale.available", p), available.Join(", "));
            }
        }

        static void SetPlayerLocale(Player p, string code) {
            List<string> available = Locale.AvailableLocales();
            if (!available.CaselessContains(code)) {
                p.Message(Locale.Get("locale.unknown", p), code);
                return;
            }
            p.Language = code;
            p.Message(Locale.Get("locale.set_player", p), code);
        }

        static void SetServerLocale(Player p, string code) {
            List<string> available = Locale.AvailableLocales();
            if (!available.CaselessContains(code)) {
                p.Message(Locale.Get("locale.unknown", p), code);
                return;
            }
            Server.Config.Language = code;
            SrvProperties.Save();
            p.Message(Locale.Get("locale.set_server", p), code);
        }

        public override void Help(Player p) {
            p.Message(Locale.Get("locale.help1", p));
            p.Message(Locale.Get("locale.help2", p));
            p.Message(Locale.Get("locale.help3", p));
            p.Message(Locale.Get("locale.help4", p));
            p.Message(Locale.Get("locale.help5", p));
            p.Message(Locale.Get("locale.help6", p));
            p.Message(Locale.Get("locale.help7", p));
            p.Message(Locale.Get("locale.help8", p));
            p.Message(Locale.Get("locale.help9", p));
            p.Message(Locale.Get("locale.help10", p));
        }
    }
}
