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
            if (message.IndexOf(' ') == -1) {
                message = "-own " + message;
                message = message.TrimEnd();
            }
            UseBotOrOnline(p, data, message, "model");
        }
        
        protected override void SetBotData(Player p, PlayerBot bot, string model) {
            model = ParseModel(p, bot, model);
            if (model == null) return;
            bot.UpdateModel(model);
            
            p.Message(Locale.Get("cmd.model.msg1", p), bot.ColoredName, model);
            BotsFile.Save(p.level);
        }
        
        protected override void SetOnlineData(Player p, Player who, string model) {
            string orig = model;
            model = ParseModel(p, who, model);
            if (model == null) return;
            who.UpdateModel(model);
            
            if (p != who) {
                Chat.MessageFrom(who, "λNICK &Shad " + who.pronouns.Object + " model changed to &c" + model);
            } else {
                who.Message("Changed your own model to &c" + model);
            }
            
            if (!model.CaselessEq("humanoid")) {
                Server.models.Update(who.name, model);
            } else {
                Server.models.Remove(who.name);
            }
            Server.models.Save();
            
            // Remove model scale too when resetting model
            if (orig.Length == 0) CmdModelScale.UpdateSavedScale(who);
        }
        
        static string ParseModel(Player dst, Entity e, string model) {
            // Reset entity's model
            if (model.Length == 0) {
                e.ScaleX = 0; e.ScaleY = 0; e.ScaleZ = 0;
                return "humanoid";
            }
            
            model = model.ToLower();
            model = model.Replace(':', '|'); // since users assume : is for scale instead of |.
            
            float max = ModelInfo.MaxScale(e, model);
            // restrict player model scale, but bots can have unlimited model scale
            if (ModelInfo.GetRawScale(model) > max) {
                dst.Message(Locale.Get("cmd.model.msg2", p),
                            max, ModelInfo.GetRawModel(model));
                return null;
            }
            return model;
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
