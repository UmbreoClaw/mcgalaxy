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
using MCGalaxy.Bots;

namespace MCGalaxy.Commands.CPE
{
    public class CmdModel : EntityPropertyCmd
    {
        public override string name { get { return "Model"; } }
        public override string type { get { return CommandTypes.Other; } }
        public override LevelPermission defaultRank { get { return LevelPermission.AdvBuilder; } }
        public override CommandPerm[] ExtraPerms {
            get { return new[] { new CommandPerm(LevelPermission.Operator, "can change the model of others"),
                    new CommandPerm(LevelPermission.Operator, "can change the model of bots") }; }
        }
        public override CommandAlias[] Aliases {
            get { return new[] {
                new CommandAlias("XModel"),
                new CommandAlias("OModel", OTHER_FLAG)
            }; }
        }

        public override void Use(Player p, string message, CommandData data) {
            UseBotOrOnline(p, data, message, "model");
        }

        protected override void SetBotData(Player p, PlayerBot bot, string model) {
            model = PlayerOperations.ParseModel(p, bot, model);
            if (model == null) return;
            bot.UpdateModel(model);

            p.Message(Locale.Get("cmd.model.msg1", p), bot.ColoredName, model);
            BotsFile.Save(p.level);
        }

        protected override void SetOnlineData(Player p, Player who, string model) {
            PlayerOperations.SetModel(p, who, model);
        }

        public override void Help(Player p) {
            p.Message(Locale.Get("cmd.model.help1", p));
            p.Message(Locale.Get("cmd.model.help2", p));
            p.Message(Locale.Get("cmd.model.help3", p));
            p.Message(Locale.Get("cmd.model.help4", p));
            p.Message(Locale.Get("cmd.model.help5", p));
            p.Message(Locale.Get("cmd.model.help6", p));
        }

        public override void Help(Player p, string message) {
            if (message.CaselessEq("models")) {
                p.Message(Locale.Get("cmd.model.help7", p));
                p.Message(Locale.Get("cmd.model.help8", p));
                p.Message(Locale.Get("cmd.model.help9", p));
            } else if (message.CaselessEq("scale")) {
                p.Message(Locale.Get("cmd.model.help10", p));
                p.Message(Locale.Get("cmd.model.help11", p));
            } else {
                Help(p);
            }
        }
    }
}
