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
using MCGalaxy.Bots;

namespace MCGalaxy.Commands.Chatting 
{    
    public class CmdNick : EntityPropertyCmd 
    {       
        public override string name { get { return "Nick"; } }
        public override string shortcut { get { return "Nickname"; } }
        public override string type { get { return CommandTypes.Chat; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }
        public override CommandPerm[] ExtraPerms {
            get { return new[] { new CommandPerm(LevelPermission.Operator, "can change the nick of others"),
                    new CommandPerm(LevelPermission.Operator, "can change the nick of bots") }; }
        }
        public override CommandAlias[] Aliases {
            get { return new[] {
                new CommandAlias("XNick"),
                new CommandAlias("ONick", OTHER_FLAG)
            }; }
        }
        
        public override void Use(Player p, string message, CommandData data) {
            UseBotOrPlayer(p, data, message, "nick");
        }

        protected override void SetBotData(Player p, PlayerBot bot, string nick) {
            if (!MessageCmd.CanSpeak(p, name)) return;
            
            if (nick.Length == 0) {
                bot.DisplayName = bot.name;
                p.level.Message(string.Format(Locale.Get("nick.bot_reverted", p), bot.ColoredName));
            } else {
                string nameTag = nick.CaselessEq("empty") ? "" : nick;
                if (nick.Length > 62) { p.Message(Locale.Get("nick.too_long", p)); return; }

                p.Message(Locale.Get("nick.bot_changed", p), bot.ColoredName, "&c" + nameTag);
                bot.DisplayName = Colors.Escape(nick);
            }
            
            bot.GlobalDespawn();
            bot.GlobalSpawn();
            BotsFile.Save(p.level);
        }
        
        protected override void SetPlayerData(Player p, string target, string nick) {
            PlayerOperations.SetNick(p, target, nick);
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("nick.help1", p));
            p.Message(Locale.Get("nick.help2", p));
            p.Message(Locale.Get("nick.help3", p));
            p.Message(Locale.Get("nick.help4", p));
            p.Message(Locale.Get("nick.help5", p));
            p.Message(Locale.Get("nick.help6", p));
            p.Message(Locale.Get("nick.help7", p));
            p.Message(Locale.Get("nick.help8", p));
        }
    }
}

