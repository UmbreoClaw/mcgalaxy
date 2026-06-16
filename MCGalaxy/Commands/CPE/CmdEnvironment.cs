/*
    Copyright 2011 MCForge
        
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

namespace MCGalaxy.Commands.CPE 
{
    public sealed class CmdEnvironment : Command2 
    {
        public override string name { get { return "Environment"; } }
        public override string shortcut { get { return "Env"; } }
        public override string type { get { return CommandTypes.World; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }

        public override void Use(Player p, string message, CommandData data) {
            if (message.CaselessEq("preset")) {
                ExplainPresets(p); return;
            }
            
            Level lvl     = null;
            EnvConfig cfg = null;
            string area   = "server";
            
            if (message.CaselessStarts("global ")) {
                message = message.Substring("global ".Length);
                cfg = Server.Config;
            } else if (message.CaselessStarts("level ")) {
                message = message.Substring("level ".Length);
            }
            
            // Work on current level by default
            if (cfg == null) {
                if (p.IsSuper) { p.Message(Locale.Get("cmd.environment.msg1", p), p.SuperName); return; }
                
                lvl = p.level; cfg = lvl.Config;
                area = lvl.ColoredName;
                if (!LevelInfo.Check(p, data.Rank, lvl, "set env settings of this level")) return;
            }
            
            string[] args = message.SplitSpaces(2);
            string opt = args[0], value = args.Length > 1 ? args[1] : "";
            if (!Handle(p, lvl, opt, value, cfg, area)) { Help(p); }
        }
        
        internal static bool Handle(Player p, Level lvl, string type, string value, EnvConfig cfg, string area) {
            if (type.CaselessEq("preset")) {
                EnvPreset preset = EnvPreset.Find(value);
                if (preset == null) { ExplainPresets(p); return false; }
                
                cfg.SkyColor    = preset.Sky;
                cfg.CloudColor  = preset.Clouds;
                cfg.FogColor    = preset.Fog;
                cfg.ShadowColor = preset.Shadow;
                cfg.LightColor  = preset.Sun;
            } else if (type.CaselessEq("normal")) {
                cfg.ResetEnv();
                p.Message(Locale.Get("cmd.environment.msg2", p), area);
            } else {
                EnvOption opt = EnvOptions.Find(type);
                if (opt == null) return false;
                opt.SetFunc(p, area, cfg, value);
            }
            
            if (lvl == null) {
                Player[] players = PlayerInfo.Online.Items;
                foreach (Player pl in players) {
                    pl.SendCurrentEnv();
                }
                SrvProperties.Save();
            } else {
                SendEnv(lvl);
                lvl.SaveSettings();
            }
            return true;
        }
        
        static void SendEnv(Level lvl) {
            Player[] players = PlayerInfo.Online.Items;
            foreach (Player pl in players) {
                if (pl.level != lvl) continue;
                pl.SendCurrentEnv();
            }
        }

        static void ExplainPresets(Player p) {
            p.Message(Locale.Get("cmd.environment.help1", p));
            EnvPreset.ListFor(p);
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("cmd.environment.help2", p));
            p.Message(Locale.Get("cmd.environment.help3", p));
            p.Message(Locale.Get("cmd.environment.help4", p));
            p.Message(Locale.Get("cmd.environment.help5", p));
            p.Message(Locale.Get("cmd.environment.help6", p));
        }
        
        public override void Help(Player p, string message) {
            if (message.CaselessEq("variable") || message.CaselessEq("variables")) {
                p.Message(Locale.Get("cmd.environment.help7", p), EnvOptions.Options.Join(o => o.Name));
                p.Message(Locale.Get("cmd.environment.help8", p));
                return;
            } else if (message.CaselessEq("presets")) {
                ExplainPresets(p); return;
            }
            
            EnvOption opt = EnvOptions.Find(message);
            if (opt != null) {
                p.Message(Locale.Get("cmd.environment.help9", p), opt.Name);
                p.Message(opt.Help);
                p.Message(Locale.Get("cmd.environment.help10", p));
            } else {
                p.Message(Locale.Get("cmd.environment.msg3", p), message);
            }
        }
    }
}
