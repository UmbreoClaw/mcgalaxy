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
using System.Collections.Generic;
using MCGalaxy.Commands;
using MCGalaxy.Eco;
using MCGalaxy.Modules.Awards;

namespace MCGalaxy.DB 
{
    public delegate void OnlineStatPrinter(Player p, Player who);
    
    /// <summary> Prints stats for an online player in /info. </summary>
    public static class OnlineStat 
    {
        /// <summary> List of stats that can be output to /info. </summary>
        public static List<OnlineStatPrinter> Stats = new List<OnlineStatPrinter>() {
            CoreLine,
            (p, who) => MiscLine(p, who.name, who.TimesDied, who.money),
            BlocksModifiedLine,
            (p, who) => BlockStatsLine(p, who.TotalPlaced, who.TotalDeleted, who.TotalDrawn),
            TimeSpentLine,
            LoginLine,
            (p, who) => LoginsLine(p, who.TimesVisited, who.TimesBeenKicked),
            (p, who) => BanLine(p, who.name),
            (p, who) => SpecialGroupLine(p, who.name),
            (p, who) => IPLine(p, who.name, who.ip),
            IdleLine,
            EntityLine,
        };
        
        public static void CoreLine(Player p, Player who) {
            string prefix   = who.title.Length == 0 ? "" : who.MakeTitle(who.title, who.titlecolor);
            string fullName = prefix + who.ColoredName;
            CommonCoreLine(p, fullName, who.name, who.group, who.TotalMessagesSent);
        }
        
        internal static void CommonCoreLine(Player p, string fullName, string name, Group grp, int messages) {
            p.Message(Locale.Get("whois.has", p), fullName, name);
            p.Message(Locale.Get("whois.rank", p), grp.ColoredName, messages);

            List<Pronouns> pros = Pronouns.GetFor(name);
            if (pros[0] == Pronouns.Default) { return; }
            p.Message(Locale.Get("whois.pronouns", p), pros.Join((pro) => pro.Name, ", "));
        }
        
        public static void MiscLine(Player p, string name, int deaths, int money) {
            if (Economy.Enabled) {
                p.Message(Locale.Get("whois.deaths_money", p),
                               deaths, PlayerAwards.Summarise(name), money, Server.Config.Currency);
            } else {
                p.Message(Locale.Get("whois.deaths", p),
                               deaths, PlayerAwards.Summarise(name));
            }
        }
        
        public static void BlocksModifiedLine(Player p, Player who) {
            p.Message(Locale.Get("whois.modified", p), who.TotalModified, who.SessionModified);
        }
        
        public static void BlockStatsLine(Player p, long placed, long deleted, long drawn) {
            p.Message(Locale.Get("whois.block_stats", p),
                           placed, deleted, drawn);
        }
        
        public static void TimeSpentLine(Player p, Player who) {
            TimeSpan timeOnline = DateTime.UtcNow - who.SessionStartTime;
            p.Message(Locale.Get("whois.time_spent", p),
                           who.TotalTime.Shorten(), timeOnline.Shorten());
        }
        
        public static void LoginLine(Player p, Player who) {
            p.Message(Locale.Get("whois.first_login_online", p),
                           who.FirstLogin.ToString("yyyy-MM-dd"));
        }
        
        public static void LoginsLine(Player p, int logins, int kicks) {
            p.Message(Locale.Get("whois.logins", p), logins, kicks);
        }
        
        public static void BanLine(Player p, string name) {
            if (!Group.BannedRank.Players.Contains(name)) return;            
            string banner, reason, prevRank;
            DateTime time;
            Ban.GetBanData(name, out banner, out reason, out time, out prevRank);
            
            if (banner != null) {
                p.Message(Locale.Get("whois.banned_by", p), reason, p.FormatNick(banner));
            } else {
                p.Message(Locale.Get("whois.is_banned", p));
            }
        }
        
        public static void SpecialGroupLine(Player p, string name) {
            string owner;
            name  = Server.ToRawUsername(name);
            owner = Server.ToRawUsername(Server.Config.OwnerName);
            
            if (Server.Devs.CaselessContains(name))
                p.Message(Locale.Get("whois.is_dev", p), Server.SoftwareName);
            if (owner.CaselessEq(name))
                p.Message(Locale.Get("whois.is_owner", p));
        }
        
        public static void IPLine(Player p, string name, string ip) {
            ItemPerms seeIpPerms = CommandExtraPerms.Find("WhoIs", 1);
            if (!seeIpPerms.UsableBy(p)) return;
            
            string ipMsg = ip;
            if (Server.bannedIP.Contains(ip)) ipMsg = "&8" + ip + Locale.Get("whois.ip_banned_suffix", p);
            
            p.Message(Locale.Get("whois.ip", p), ipMsg);
            if (Server.Config.WhitelistedOnly && Server.whiteList.Contains(name))
                p.Message(Locale.Get("whois.whitelisted", p));
        }
                
        public static void IdleLine(Player p, Player who) {
            TimeSpan idleTime = DateTime.UtcNow - who.LastAction;
            if (who.afkMessage != null) {
                p.Message(Locale.Get("whois.idle_afk", p), idleTime.Shorten(), who.afkMessage);
            } else if (idleTime.TotalMinutes >= 1) {
                p.Message(Locale.Get("whois.idle", p), idleTime.Shorten());
            }
        }
        
        public static void EntityLine(Player p, Player who) {
            bool hasSkin  = !who.SkinName.CaselessEq(who.truename);
            // TODO remove hardcoding
            bool hasModel = !(who.Model.CaselessEq("humanoid") || who.Model.CaselessEq("human"));
            
            if (hasSkin && hasModel) {
                //We only want do display skin and model on the same line if they actually fit on one line
                string format = String.Format(Locale.Get("whois.skin_model", p), who.SkinName, who.Model);
                if (format.Length <= NetUtils.StringSize - 2) { //-2 to account for "> " in line wrap
                    p.Message(format);
                    //One line OK, exit method
                    return;
                }
                //else {
                // Not one line, fall through to split line case
                //}
            }
            if (hasSkin) {
                p.Message(Locale.Get("whois.skin", p), who.SkinName);
            }
            if (hasModel) {
                p.Message(Locale.Get("whois.model", p), who.Model);
            }
        }
    }
}