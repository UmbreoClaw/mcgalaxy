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
using MCGalaxy.Blocks.Extended;
using MCGalaxy.Bots;
using System;

namespace MCGalaxy.Commands.Bots 
{
    public sealed class CmdBot : Command2 
    {
        public override string name { get { return "Bot"; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override bool museumUsable { get { return false; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Admin; } }
        public override bool SuperUseable { get { return false; } }
        public override CommandAlias[] Aliases {
            get { return new[] {
                    new CommandAlias("BotAdd", "add"),
                    new CommandAlias("BotRemove", "remove"),
                    new CommandAlias("BotInfo", "info")
                }; }
        }
        public override CommandPerm[] ExtraPerms {
            get { return new[] { new CommandPerm(LevelPermission.Operator, "can modify bots that do not belong to them") }; }
        }

        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) { Help(p); return; }
            string[] args = message.SplitSpaces(3);
            if (args[0].CaselessEq("info")) { BotInfo(p, args.Length < 2 ? "" : args[1]); return; }
            
            if (args.Length < 2) { Help(p); return; }
            
            if (!Formatter.ValidName(p, args[1], "bot")) return;
            if (!LevelInfo.Check(p, data.Rank, p.level, "modify bots in this level")) return;
            
            string bot = args[1], value = args.Length > 2 ? args[2] : null;
            if (args[0].CaselessEq("add")) {
                AddBot(p, bot);
            } else if (IsDeleteAction(args[0])) {
                RemoveBot(p, bot, value);
            } else if (args[0].CaselessEq("text")) {
                SetBotText(p, bot, value, data.Rank);
            } else if (args[0].CaselessEq("deathmsg") || args[0].CaselessEq("deathmessage")) {
                SetDeathMessage(p, bot, value);
            } else if (args[0].CaselessEq("rename")) {
                RenameBot(p, bot, value);
            } else if (args[0].CaselessEq("copy")) {
                CopyBot(p, bot, value);
            } else {
                Help(p);
            }
        }
        
        void AddBot(Player p, string botName) {
            botName = botName.Replace(' ', '_');
            PlayerBot bot = new PlayerBot(botName, p.level);
            bot.Owner = p.name;
            bot.CreationDate = DateTime.UtcNow.ToUnixTime();
            TryAddBot(p, bot);
        }
        
        void TryAddBot(Player p, PlayerBot bot) {
            if (BotExists(p.level, bot.name, null)) {
                p.Message(Locale.Get("cmd.bot.msg1", p)); return;
            }
            if (p.level.Bots.Count >= Server.Config.MaxBotsPerLevel) {
                p.Message(Locale.Get("cmd.bot.msg2", p)); return;
            }
            
            bot.SetInitialPos(p.Pos);
            bot.SetYawPitch(p.Rot.RotY, 0);
            
            p.Message("You added the bot " + bot.ColoredName);
            PlayerBot.Add(bot);
        }
        
        static bool BotExists(Level lvl, string name, PlayerBot skip) {
            PlayerBot[] bots = lvl.Bots.Items;
            foreach (PlayerBot bot in bots) {
                if (bot == skip) continue;
                if (bot.name.CaselessEq(name)) return true;
            }
            return false;
        }
        
        void RemoveBot(Player p, string botName, string extArgs) {
            if (botName.CaselessEq("all")) {
                //bot remove all[botname] griefer[extArgs]
                if (extArgs != null) {
                    string ownerName = PlayerInfo.FindMatchesPreferOnline(p, extArgs);
                    if (ownerName == null) { return; }
                    if (PlayerBot.CanEditAny(p) || ownerName.CaselessEq(p.name)) {
                        int removedCount = PlayerBot.RemoveBotsOwnedBy(p, ownerName, p.level, false);
                        if (removedCount == 0) {
                            p.Message(Locale.Get("cmd.bot.msg3", p), p.FormatNick(ownerName));
                        } else {
                            p.Message(Locale.Get("cmd.bot.msg4", p), removedCount, removedCount.Plural(), p.FormatNick(ownerName));
                            BotsFile.Save(p.level);
                        }
                    } else {
                        p.Message(Locale.Get("cmd.bot.msg5", p), p.FormatNick(ownerName));
                    }
                    return;
                }
                
                if (PlayerBot.CanEditAny(p)) {
                    int removedCount = PlayerBot.RemoveLoadedBots(p.level, false);
                    if (removedCount == 0) {
                        p.Message(Locale.Get("cmd.bot.msg6", p));
                    } else {
                        p.Message(Locale.Get("cmd.bot.msg7", p), removedCount, removedCount.Plural());
                        BotsFile.Save(p.level);
                    }
                } else {
                    p.Message(Locale.Get("cmd.bot.msg8", p));
                }

            } else {
                PlayerBot bot = Matcher.FindBots(p, botName);
                if (bot == null) return;
                if (!bot.EditableBy(p, "remove")) return;
                
                PlayerBot.Remove(bot);
                p.Message(Locale.Get("cmd.bot.msg9", p), bot.ColoredName);
            }
        }
        
        void SetBotText(Player p, string botName, string text, LevelPermission plRank) {
            PlayerBot bot = Matcher.FindBots(p, botName);
            if (bot == null) return;
            if (!bot.EditableBy(p, "set the text of")) return;
            
            if (text == null) {
                p.Message(Locale.Get("cmd.bot.msg10", p), bot.ColoredName);
                bot.ClickedOnText = null;
            } else {
                bool allCmds = HasExtraPerm(p, "MB", plRank, 1);
                if (!MessageBlock.Validate(p, text, allCmds)) return;
                
                p.Message(Locale.Get("cmd.bot.msg11", p), bot.ColoredName, text);
                bot.ClickedOnText = text;
            }
            BotsFile.Save(p.level);
        }
        
        void SetDeathMessage(Player p, string botName, string text) {
            PlayerBot bot = Matcher.FindBots(p, botName);
            if (bot == null) return;
            if (!bot.EditableBy(p, "set the death message of")) return;
            
            if (text == null) {
                p.Message(Locale.Get("cmd.bot.msg12", p), bot.ColoredName);
                bot.DeathMessage = null;
            } else {
                p.Message(Locale.Get("cmd.bot.msg13", p), bot.ColoredName, text);
                bot.DeathMessage = text;
            }
            BotsFile.Save(p.level);
        }
        
        void RenameBot(Player p, string botName, string newName) {
            if (newName == null) { p.Message(Locale.Get("cmd.bot.msg14", p)); return; }
            if (!Formatter.ValidName(p, newName, "bot")) return;
            
            PlayerBot bot = Matcher.FindBots(p, botName);
            if (bot == null) return;
            if (!bot.EditableBy(p, "rename")) { return; }
            if (BotExists(p.level, newName, bot)) {
                p.Message(Locale.Get("cmd.bot.msg15", p)); return;
            }
            
            p.Message(Locale.Get("cmd.bot.msg16", p), bot.ColoredName);
            if (bot.DisplayName == bot.name) {
                bot.DisplayName = newName;
                bot.GlobalDespawn();
                bot.GlobalSpawn();
            }
            
            bot.name = newName;
            BotsFile.Save(p.level);
        }
        
        void CopyBot(Player p, string botName, string newName) {
            if (newName == null) { p.Message(Locale.Get("cmd.bot.msg17", p)); return; }
            if (!Formatter.ValidName(p, newName, "bot")) return;
            
            PlayerBot bot = Matcher.FindBots(p, botName);
            if (bot == null) return;
            
            PlayerBot clone = new PlayerBot(newName, p.level);
            BotProperties props = new BotProperties();
            props.FromBot(bot);
            props.ApplyTo(clone);
            clone.Owner = p.name;
            clone.SetModel(clone.Model);
            clone.CreationDate = DateTime.UtcNow.ToUnixTime();
            BotsFile.LoadAi(props, clone);
            // Preserve custom name tag
            if (bot.DisplayName == bot.name) clone.DisplayName = newName;
            TryAddBot(p, clone);
        }
        
        void BotInfo(Player p, string botName) {
            if (botName.Length == 0) {
                if (!p.Supports(CpeExt.PlayerClick)) {
                    p.Message(Locale.Get("cmd.bot.msg18", p));
                    p.Message(Locale.Get("cmd.bot.msg19", p));
                    p.Message(Locale.Get("cmd.bot.msg20", p));
                    p.Message(Locale.Get("cmd.bot.help1", p));
                    return;
                }
                p.checkingBotInfo = true;
                p.Message(Locale.Get("cmd.bot.msg21", p));
                return;
            }
            PlayerBot bot = Matcher.FindBots(p, botName);
            if (bot == null) return;
            bot.DisplayInfo(p);
            if (p.checkingBotInfo) { p.checkingBotInfo = false; p.Message(Locale.Get("cmd.bot.msg22", p)); }
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("cmd.bot.help2", p));
            p.Message(Locale.Get("cmd.bot.help3", p));
            p.Message(Locale.Get("cmd.bot.help4", p));
            p.Message(Locale.Get("cmd.bot.help5", p));
            p.Message(Locale.Get("cmd.bot.help6", p));
            p.Message(Locale.Get("cmd.bot.help7", p));
            p.Message(Locale.Get("cmd.bot.help8", p));
            p.Message(Locale.Get("cmd.bot.help9", p));
            p.Message(Locale.Get("cmd.bot.help10", p));
            p.Message(Locale.Get("cmd.bot.help11", p));
            p.Message(Locale.Get("cmd.bot.help12", p));
            p.Message(Locale.Get("cmd.bot.help13", p));
            p.Message(Locale.Get("cmd.bot.help14", p));
            p.Message(Locale.Get("cmd.bot.help15", p));
            p.Message(Locale.Get("cmd.bot.help16", p));
        }
    }
}
