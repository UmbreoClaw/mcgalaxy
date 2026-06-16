/*
    Copyright 2010 MCSharp team (Modified for use with MCZall/MCLawl/MCForge)
    
    Dual-licensed under the    Educational Community License, Version 2.0 and
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
using MCGalaxy.DB;

namespace MCGalaxy.Commands.Info 
{
    public sealed class CmdBanInfo : Command2 
    {
        public override string name { get { return "BanInfo"; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override bool UseableWhenFrozen { get { return true; } }
        public override bool MessageBlockRestricted { get { return false; } }
        
        public override void Use(Player p, string message, CommandData data) {
            if (CheckSuper(p, message, "player name")) return;
            if (message.Length == 0) message = p.name;
            
            string target = PlayerInfo.FindMatchesPreferOnline(p, message);
            if (target == null) return;
            string nick = p.FormatNick(target);
            
            string tempData = Server.tempBans.Get(target);
            string tempBanner = null, tempReason = null;
            DateTime tempExpiry = DateTime.MinValue;

            if (!string.IsNullOrEmpty(tempData)) {
                Ban.UnpackTempBanData(tempData, out tempReason, out tempBanner, out tempExpiry);
            }
            
            bool permaBanned = Group.BannedRank.Players.Contains(target);
            bool isBanned = permaBanned || tempExpiry >= DateTime.UtcNow;
            string msg = nick;
            string ip  = PlayerDB.FindIP(target);
            bool ipBanned = ip != null && Server.bannedIP.Contains(ip);
            
            if (!ipBanned && isBanned) msg += Locale.Get("baninfo.is_banned", p);
            else if (!ipBanned && !isBanned) msg += Locale.Get("baninfo.not_banned", p);
            else if (ipBanned && isBanned) msg += Locale.Get("baninfo.ip_also_banned", p);
            else msg += Locale.Get("baninfo.ip_only_banned", p);
            
            string banner, reason, prevRank;
            DateTime time;
            Ban.GetBanData(target, out banner, out reason, out time, out prevRank);
            if (banner != null && permaBanned) {
                string grpName = Group.GetColoredName(prevRank);
                msg += string.Format(Locale.Get("baninfo.former_rank", p), grpName);
            }
            p.Message(msg);
            
            if (tempExpiry >= DateTime.UtcNow) {
                TimeSpan delta = tempExpiry - DateTime.UtcNow;
                p.Message(Locale.Get("baninfo.temp_banned", p),
                          delta.Shorten(), p.FormatNick(tempBanner));
                if (tempReason.Length > 0) {
                    p.Message(Locale.Get("baninfo.reason", p), tempReason);
                }
            }

            if (banner != null) {
                DisplayDetails(p, banner, reason, time, permaBanned ? Locale.Get("baninfo.banned_label", p) : Locale.Get("baninfo.last_banned_label", p));
            } else {
                p.Message(Locale.Get("baninfo.no_previous_bans", p), nick);
            }
            Ban.GetUnbanData(target, out banner, out reason, out time);
            DisplayDetails(p, banner, reason, time, permaBanned ? Locale.Get("baninfo.last_unbanned_label", p) : Locale.Get("baninfo.unbanned_label", p));
        }
        
        static void DisplayDetails(Player p, string banner, string reason, DateTime time, string type) {
            if (banner == null) return;

            TimeSpan delta = DateTime.UtcNow - time;
            p.Message(Locale.Get("baninfo.event_ago_by", p),
                          type, delta.Shorten(), p.FormatNick(banner));
            p.Message(Locale.Get("baninfo.reason", p), reason);
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("baninfo.help1", p));
            p.Message(Locale.Get("baninfo.help2", p));
        }
    }
}
