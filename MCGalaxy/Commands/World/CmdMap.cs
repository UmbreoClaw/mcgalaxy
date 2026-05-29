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
using MCGalaxy.Localization;

namespace MCGalaxy.Commands.World {
    public sealed class CmdMap : Command2 {
        public override string name { get { return "Map"; } }
        public override string type { get { return CommandTypes.World; } }
        public override CommandPerm[] ExtraPerms {
            get { return new[] { new CommandPerm(LevelPermission.Operator, "can edit map options"),
                    new CommandPerm(LevelPermission.Admin, "can set realm owners") }; }
        }
        public override CommandAlias[] Aliases {
            get { return new[] { new CommandAlias("ps", LevelOptions.Speed),
                    new CommandAlias("AllowGuns", "{args} " + LevelOptions.Guns) }; }
        }

        public override void Use(Player p, string message, CommandData data) {
            if (CheckSuper(p, message, "level name")) return;

            if (message.Length == 0) {
                PrintMapInfo(p, p.level.Config); return;
            }

            string[] args = message.SplitSpaces(3);
            Level lvl = null;
            string optName = null, value = null;
            
            if (IsMapOption(args)) {
                if (p.IsSuper) { SuperRequiresArgs(p, "level name"); return; }
                lvl = p.level;
                
                optName = args[0];
                args = message.SplitSpaces(2);
                value = args.Length > 1 ? args[1] : "";
            } else if (args.Length == 1) {
                string map = Matcher.FindMaps(p, args[0]);
                if (map == null) return;
                
                PrintMapInfo(p, LevelInfo.GetConfig(map));
                return;
            } else {
                lvl = Matcher.FindLevels(p, args[0]);
                if (lvl == null) return;
                
                optName = args[1];
                value = args.Length > 2 ? args[2] : "";
            }
            
            if (!CheckExtraPerm(p, data, 1)) return;
            if (optName.CaselessEq(LevelOptions.RealmOwner) && !CheckExtraPerm(p, data, 2)) return;
            if (!LevelInfo.Check(p, data.Rank, lvl, "change map settings of this level")) return;
            
            LevelOption opt = LevelOptions.Find(optName);
            if (opt == null) {
                p.Message(Locale.Get("map.option_not_found", p));
            } else {
                opt.SetFunc(p, lvl, value);
                lvl.SaveSettings();
            }
        }
        
        static bool IsMapOption(string[] args) {
            LevelOption opt = LevelOptions.Find(args[0]);
            if (opt == null) return false;
            // In rare case someone uses /map motd motd My MOTD
            if (opt.Name == LevelOptions.MOTD && (args.Length == 1 || !args[1].CaselessStarts("motd "))) return true;
            
            int argsCount = HasArgument(opt.Name) ? 2 : 1;
            return args.Length == argsCount;
        }
        
        static bool HasArgument(string opt) {
            return
                opt == LevelOptions.Speed || opt == LevelOptions.Overload || opt == LevelOptions.TreeType ||
                opt == LevelOptions.Fall || opt == LevelOptions.Drown || opt == LevelOptions.RealmOwner || opt == LevelOptions.LoadDelay;
        }
        
        static void PrintMapInfo(Player p, LevelConfig cfg) {
            p.Message(Locale.Get("map.physics_settings", p));
            p.Message(Locale.Get("map.finite_random", p),
                           GetBool(cfg.FiniteLiquids), GetBool(cfg.RandomFlow));
            p.Message(Locale.Get("map.animal_edge", p),
                           GetBool(cfg.AnimalHuntAI), GetBool(cfg.EdgeWater));
            p.Message(Locale.Get("map.grass_tree", p),
                           GetBool(cfg.GrassGrow), cfg.TreeType.Capitalize(), GetBool(cfg.GrowTrees));
            p.Message(Locale.Get("map.leaf_overload", p),
                           GetBool(cfg.LeafDecay), cfg.PhysicsOverload);
            p.Message(Locale.Get("map.physics_speed", p),
                           cfg.PhysicsSpeed);

            p.Message(Locale.Get("map.survival_settings", p));
            p.Message(Locale.Get("map.survival_death", p),
                           GetBool(cfg.SurvivalDeath), cfg.FallHeight, cfg.DrownTime);
            p.Message(Locale.Get("map.guns_killer", p),
                           GetBool(cfg.Guns), GetBool(cfg.KillerBlocks));

            p.Message(Locale.Get("map.general_settings", p));
            p.Message("  MOTD: &b" + cfg.MOTD);
            p.Message("  Local level only chat: " + GetBool(!cfg.ServerWideChat));
            p.Message(Locale.Get("map.load_unload", p),
                           GetBool(cfg.LoadOnGoto), GetBool(cfg.AutoUnload));
            p.Message(Locale.Get("map.build_delete_draw", p),
                           GetBool(cfg.Buildable), GetBool(cfg.Deletable), GetBool(cfg.Drawing));
        }
        
        static string GetBool(bool value) { return value ? "&aON" : "&cOFF"; }

        public override void Help(Player p) {
            p.Message(Locale.Get("map.help1", p));
            p.Message(Locale.Get("map.help2", p));
            p.Message(Locale.Get("map.help3", p));
        }

        public override void Help(Player p, string message) {
            if (message.CaselessEq("options")) {
                p.Message(Locale.Get("cmd.map.help1", p), LevelOptions.Options.Join(o => o.Name));
                p.Message(Locale.Get("map.help3", p));
                return;
            }

            LevelOption opt = LevelOptions.Find(message);
            if (opt == null) {
                p.Message(Locale.Get("map.unrecognised_option", p), message); return;
            }

            bool isMotd = opt.Name == LevelOptions.MOTD;
            string suffix = isMotd ? " <value>" : (HasArgument(opt.Name) ? " [value]" : "");

            p.Message(Locale.Get("cmd.map.help2", p), opt.Name, suffix);
            p.Message("&H" + opt.Help);
            if (isMotd) ShowMotdRules(p);
        }

        static void ShowMotdRules(Player p) {
            p.Message(Locale.Get("map.motd_rules", p));
            p.Message(Locale.Get("cmd.map.help3", p));
            p.Message(Locale.Get("cmd.map.help4", p));
            p.Message(Locale.Get("cmd.map.help5", p));
            p.Message(Locale.Get("cmd.map.help6", p));
            p.Message(Locale.Get("cmd.map.help7", p));
            p.Message(Locale.Get("cmd.map.help8", p));
            p.Message(Locale.Get("cmd.map.help9", p),
                           Group.GetColoredName(LevelPermission.Operator));
            p.Message(Locale.Get("cmd.map.help10", p));
            p.Message(Locale.Get("cmd.map.help11", p));
            p.Message(Locale.Get("cmd.map.help12", p));
            p.Message(Locale.Get("cmd.map.help13", p));
        }
    }
}
