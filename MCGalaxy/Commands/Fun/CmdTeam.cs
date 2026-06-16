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
using MCGalaxy.Games;

namespace MCGalaxy.Commands.Fun {
    public sealed class CmdTeam : Command2 {        
        public override string name { get { return "Team"; } }
        public override string type { get { return CommandTypes.Games; } }
        public override bool SuperUseable { get { return false; } }
        public override CommandPerm[] ExtraPerms {
            get { return new[] { new CommandPerm(LevelPermission.AdvBuilder, "can create teams") }; }
        }
        
        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) { Help(p); return; }
            string[] args = message.SplitSpaces(2);

            switch (args[0].ToLower()) {
                case "owner": HandleOwner(p, args); return;
                case "kick": HandleKick(p, args); return;
                case "color": HandleColor(p, args); return;
                case "create": HandleCreate(p, args, data); return;
                case "join": HandleJoin(p, args); return;
                case "invite": HandleInvite(p, args); return;
                case "leave": HandleLeave(p, args); return;
                case "members": HandleMembers(p, args); return;
                case "list": HandleList(p, args); return;
            }
            
            Team team = p.Game.Team;
            if (team == null) {
                p.Message(Locale.Get("team.need_team_msg", p)); return;
            }
            team.Message(p, message);
        }

        void HandleOwner(Player p, string[] args) {
            Team team = p.Game.Team;
            if (team == null) { p.Message(Locale.Get("team.need_team", p)); return; }

            if (args.Length == 1) {
                p.Message(Locale.Get("team.current_owner", p), team.Owner); return;
            }

            Player who = PlayerInfo.FindMatches(p, args[1]);
            if (who == null) return;

            if (!p.name.CaselessEq(team.Owner)) {
                p.Message(Locale.Get("team.only_owner_can_set_owner", p)); return;
            }
            team.Owner = who.name;
            team.Action(p, "set the team owner to " + who.ColoredName);
            Team.SaveList();
        }

        void HandleKick(Player p, string[] args) {
            Team team = p.Game.Team;
            if (team == null) { p.Message(Locale.Get("team.need_team", p)); return; }
            if (args.Length == 1) {
                p.Message(Locale.Get("team.kick_specify", p)); return;
            }
            if (!p.name.CaselessEq(team.Owner)) {
                p.Message(Locale.Get("team.only_owner_can_kick", p)); return;
            }

            if (team.Remove(args[1])) {
                team.Action(p, "kicked " + args[1] + " from the team.");
                Player who = PlayerInfo.FindExact(args[1]);
                if (who != null) {
                    who.Game.Team = null;
                    who.SetPrefix();
                }

                team.DeleteIfEmpty();
                Team.SaveList();
            } else {
                p.Message(Locale.Get("team.kick_not_found", p));
            }
        }
        
        void HandleColor(Player p, string[] args) {
            Team team = p.Game.Team;
            if (team == null) { p.Message(Locale.Get("team.need_team", p)); return; }
            if (args.Length == 1) {
                p.Message(Locale.Get("team.color_specify", p)); return;
            }
            
            string color = Matcher.FindColor(p, args[1]);
            if (color == null) return;
            
            team.Color = color;
            team.Action(p, "changed the team color to: " + args[1]);
            team.UpdatePrefix();
            Team.SaveList();
        }
        
        void HandleCreate(Player p, string[] args, CommandData data) {
            if (!CheckExtraPerm(p, data, 1)) return;
            Team team = p.Game.Team;
            if (team != null) { p.Message(Locale.Get("team.leave_first", p)); return; }
            if (args.Length == 1) {
                p.Message(Locale.Get("team.create_specify", p)); return;
            }
            team = Team.Find(args[1]);
            if (team != null) { p.Message(Locale.Get("team.already_exists", p)); return; }
            if (args[1].Length > 8) {
                p.Message(Locale.Get("team.name_too_long", p)); return;
            }
            
            team = new Team(args[1], p.name);
            p.Game.Team = team;
            p.SetPrefix();
            Team.Add(team);
            Team.SaveList();
            Chat.MessageFrom(p, "λNICK &Screated the &a" + args[1] + " &Steam");
        }
        
        void HandleJoin(Player p, string[] args) {
            Team team = p.Game.Team;
            if (p.Game.TeamInvite == null) { p.Message(Locale.Get("team.no_invite", p)); return; }
            if (team != null) { p.Message(Locale.Get("team.leave_first_join", p)); return; }

            team = Team.Find(p.Game.TeamInvite);
            if (team == null) { p.Message(Locale.Get("team.invite_expired", p)); return; }
            
            p.Game.Team = team;
            p.Game.TeamInvite = null;
            p.SetPrefix();
            
            team.Members.Add(p.name);
            team.Action(p, "joined the team.");
            Team.SaveList();
        }
        
        void HandleInvite(Player p, string[] args) {
            Team team = p.Game.Team;
            if (team == null) { p.Message(Locale.Get("team.need_team_invite", p)); return; }
            if (args.Length == 1) {
                p.Message(Locale.Get("team.invite_specify", p)); return;
            }
            Player target = PlayerInfo.FindMatches(p, args[1]);
            if (target == null) return;

            DateTime cooldown = p.NextTeamInvite;
            DateTime now = DateTime.UtcNow;
            if (now < cooldown) {
                p.Message(Locale.Get("team.invite_cooldown", p),
                               (int)(cooldown - now).TotalSeconds);
                return;
            }
            p.NextTeamInvite = now.AddSeconds(5);

            p.Message(Locale.Get("team.invited", p), p.FormatNick(target));
            target.Message(Locale.Get("team.was_invited", target), p.ColoredName, team.Color + team.Name);
            target.Game.TeamInvite = team.Name;
        }
        
        void HandleLeave(Player p, string[] args) {
            Team team = p.Game.Team;
            if (team == null) { p.Message(Locale.Get("team.need_team_leave", p)); return; }
            
            // handle '/team leave me alone', for example
            if (args.Length > 1) {
                team.Message(p, args.Join(" ")); return;
            }
            
            team.Action(p, "left the team.");
            team.Remove(p.name);
            p.Game.Team = null;
            
            team.DeleteIfEmpty();
            p.SetPrefix();
            Team.SaveList();
        }
        
        void HandleMembers(Player p, string[] args) {
            Team team = p.Game.Team;
            if (args.Length == 1) {
                if (team == null) { p.Message(Locale.Get("team.not_in_team", p)); return; }
            } else {
                team = Team.Find(args[1]);
                if (team == null) { p.Message(Locale.Get("team.not_found", p), args[1]); return; }
            }
            p.Message(Locale.Get("team.owner", p), team.Owner);
            p.Message(Locale.Get("team.members", p), team.Members.Join());
        }
        
        void HandleList(Player p, string[] args) {
            string modifier = args.Length > 1 ? args[1] : "";
            Paginator.Output(p, Team.Teams, team => team.Color + team.Name,
                             "team list", "teams", modifier);
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("team.help1", p));
            p.Message(Locale.Get("team.help2", p));
            p.Message(Locale.Get("team.help3", p));
            p.Message(Locale.Get("team.help4", p));
            p.Message(Locale.Get("team.help5", p));
            p.Message(Locale.Get("team.help6", p));
            p.Message(Locale.Get("team.help7", p));
            p.Message(Locale.Get("team.help8", p));
            p.Message(Locale.Get("team.help9", p));
            p.Message(Locale.Get("team.help10", p));
        }
    }
}