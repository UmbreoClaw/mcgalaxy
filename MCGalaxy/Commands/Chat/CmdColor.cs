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
using MCGalaxy.Bots;
using MCGalaxy.DB;

namespace MCGalaxy.Commands.Chatting
{    
    public class CmdColor : EntityPropertyCmd 
    {
        public override string name { get { return "Color"; } }
        public override string type { get { return CommandTypes.Chat; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }
        public override CommandPerm[] ExtraPerms {
            get { return new[] { new CommandPerm(LevelPermission.Operator, "can change the color of others"),
                    new CommandPerm(LevelPermission.Operator, "can change the color of bots") }; }
        }
        public override CommandAlias[] Aliases {
            get { return new[] {
                new CommandAlias("Colour"),
                new CommandAlias("XColor"),
                new CommandAlias("OColor", OTHER_FLAG)
            }; }
        }        
        public override void Use(Player p, string message, CommandData data) { 
            UseBotOrPlayer(p, data, message, "color"); 
        }

        protected override void SetBotData(Player p, PlayerBot bot, string colName) {
            string color = colName.Length == 0 ? "&1" : Matcher.FindColor(p, colName);
            if (color == null) return;
            
            p.Message(Locale.Get("color.bot_color_changed", p), bot.ColoredName, color + Colors.Name(color));
            bot.color = color;
            
            bot.GlobalDespawn();
            bot.GlobalSpawn();
            BotsFile.Save(p.level);
        }
        
        protected override void SetPlayerData(Player p, string target, string colName) {
            PlayerOperations.SetColor(p, target, colName);
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("color.help1", p));
            p.Message(Locale.Get("color.help2", p));
            p.Message(Locale.Get("color.help3", p));
            p.Message(Locale.Get("color.help4", p));
            p.Message(Locale.Get("color.help5", p));
            p.Message(Locale.Get("color.help6", p));
            p.Message(Locale.Get("color.help7", p));
            p.Message(Locale.Get("color.help8", p));
        }
    }
}
