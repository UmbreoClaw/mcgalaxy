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
using System;
using System.IO;
using System.Text;
using MCGalaxy.Commands.World;
using MCGalaxy.DB;
using MCGalaxy.Games;
using MCGalaxy.Levels.IO;
using MCGalaxy.Maths;
using MCGalaxy.Network;
using BlockID = System.UInt16;

namespace MCGalaxy.Commands.Info 
{
    public sealed class CmdMapInfo : Command2 
    {
        public override string name { get { return "MapInfo"; } }
        public override string shortcut { get { return "mi"; } }
        public override string type { get { return CommandTypes.Information; } }
        public override bool UseableWhenFrozen { get { return true; } }
        public override CommandAlias[] Aliases {
            get { return new[] { new CommandAlias("WInfo"), new CommandAlias("WorldInfo") }; }
        }
        
        public override void Use(Player p, string message, CommandData data) {
            string[] args = message.SplitSpaces();
            bool env = args[0].CaselessEq("env");
            string map = env ? (args.Length > 1 ? args[1] : "") : args[0];

            Level lvl = map.Length == 0 ? p.level : null;
            MapInfo info = new MapInfo();
            
            // User provided specific map name
            if (lvl == null) {
                map = Matcher.FindMaps(p, map);
                if (map == null) return;
                lvl = LevelInfo.FindExact(map);
            }
            
            if (lvl != null) {
                info.FromLevel(lvl);
            } else {
                info.FromMap(map);
            }

            // shouldn't be able to see env of levels can't vsit
            if (env && map.Length > 0 && !info.Visit.CheckDetailed(p, data.Rank)) {
                p.Message(Locale.Get("mapinfo.no_env_access", p)); return;
            }
            
            if (env) ShowEnv(p, info, info.Config);
            else ShowNormal(p, info, info.Config, args.Length > 1 && args[1].CaselessEq("all"));
        }
        
        void ShowNormal(Player p, MapInfo data, LevelConfig cfg, bool showAll) {
            p.Message(Locale.Get("mapinfo.about", p),
                      cfg.Color + data.Name, data.Width, data.Height, data.Length);

            string physicsState = CmdPhysics.states[cfg.Physics];
            p.Message(Locale.Get("mapinfo.physics_guns", p),
                      physicsState, cfg.Guns ? Locale.Get("mapinfo.guns_enabled", p) : Locale.Get("mapinfo.guns_disabled", p));

            DateTime createTime  = File.GetCreationTimeUtc(LevelInfo.MapPath(data.MapName));
            TimeSpan createDelta = DateTime.UtcNow - createTime;
            string backupPath    = LevelInfo.BackupBasePath(data.MapName);
            
            if (Directory.Exists(backupPath)) {
                int latest = LevelInfo.LatestBackup(data.MapName);
                DateTime backupTime = File.GetCreationTimeUtc(LevelInfo.BackupFilePath(data.MapName, latest.ToString()));
                TimeSpan backupDelta = DateTime.UtcNow - backupTime;
                p.Message(Locale.Get("mapinfo.created_backup", p),
                          latest, backupDelta.Shorten(), createDelta.Shorten());
            } else {
                p.Message(Locale.Get("mapinfo.created_no_backup", p), createDelta.Shorten());
            }
            
            string dbFormat = Locale.Get("mapinfo.blockdb_entries", p);
            if (data.BlockDBEntries == -1) dbFormat = Locale.Get("mapinfo.blockdb_no_entries", p);
            p.Message(dbFormat,
                      cfg.UseBlockDB ? Locale.Get("mapinfo.blockdb_enabled", p) : Locale.Get("mapinfo.blockdb_disabled", p), data.BlockDBEntries);
            
            ShowPermissions(p, data, cfg, showAll);
            p.Message(Locale.Get("mapinfo.see_env", p), data.MapName);
            ShowGameInfo(p, data, cfg);
        }
        
        void ShowPermissions(Player p, MapInfo data, LevelConfig cfg, bool showAll) {
            bool shortened = false;
            if (PrintRanks(p, Locale.Get("mapinfo.visitable_by", p), data.Visit, !showAll)) shortened = true;
            if (PrintRanks(p, Locale.Get("mapinfo.modifiable_by", p), data.Build, !showAll)) shortened = true;

            string realmOwner = cfg.RealmOwner;
            if (String.IsNullOrEmpty(cfg.RealmOwner)) {
                realmOwner = LevelInfo.DefaultRealmOwner(data.MapName);
            }
            if (!String.IsNullOrEmpty(realmOwner)) {
                string[] owners = realmOwner.SplitComma();
                p.Message(Locale.Get("mapinfo.personal_realm", p), owners.Join(n => p.FormatNick(n)));
            }
            if (shortened) p.Message(Locale.Get("mapinfo.see_all_perms", p), data.MapName);
        }
        
        /// <summary>
        /// Returns true if the printing got shortened
        /// </summary>
        static bool PrintRanks(Player p, string initial, AccessController access, bool shorten) {
            StringBuilder perms = new StringBuilder(initial);
            bool shortened = access.Describe(p, perms, shorten);
            p.Message(perms.ToString());
            return shortened;
        }
        
        void ShowGameInfo(Player p, MapInfo data, LevelConfig cfg) {
            IGame game = GetAssociatedGame(data.MapName);
            if (game == null) return;
            
            game.OutputMapSummary(p, data.MapName, cfg); // TODO: Always show this info?
            game.OutputMapInfo(p, data.MapName, cfg);
        }
        
        static IGame GetAssociatedGame(string map)
        {
            IGame[] games = IGame.RunningGames.Items;
            foreach (IGame game in games)
            {
                if (game.ClaimsMap(map)) return game;
            }
            return null;
        }
        
        void ShowEnv(Player p, MapInfo data, LevelConfig cfg) {
            string url = cfg.Terrain.Length > 0 ? cfg.Terrain : Server.Config.DefaultTerrain;
            if (url.Length > 0) {
                p.Message(Locale.Get("mapinfo.terrain", p), url);
            } else {
                p.Message(Locale.Get("mapinfo.no_terrain", p));
            }

            url = cfg.TexturePack.Length > 0 ? cfg.TexturePack : Server.Config.DefaultTexture;
            if (url.Length > 0) {
                p.Message(Locale.Get("mapinfo.texture_pack", p), url);
            } else {
                p.Message(Locale.Get("mapinfo.no_texture_pack", p));
            }

            p.Message(Locale.Get("mapinfo.colors", p),
                      Color(cfg.FogColor), Color(cfg.SkyColor),
                      Color(cfg.CloudColor), Color(cfg.LightColor), Color(cfg.ShadowColor));
            if (cfg.LavaLightColor != "" || cfg.LampLightColor != "") {
                p.Message(Locale.Get("mapinfo.fancy_colors", p),
                          Color(cfg.LavaLightColor), Color(cfg.LampLightColor));
            }
            if (cfg.LightingMode != Packet.LightingMode.None) {
                p.Message(Locale.Get("mapinfo.lighting_mode", p), cfg.LightingMode, cfg.LightingModeLocked ? "&c locked" : "");
            }
            p.Message(Locale.Get("mapinfo.water_bedrock_clouds_fog", p),
                      data.Get(EnvProp.EdgeLevel),   data.Get(EnvProp.SidesOffset),
                      data.Get(EnvProp.CloudsLevel), data.Get(EnvProp.MaxFog));
            p.Message(Locale.Get("mapinfo.edge_horizon", p),
                      Block.GetName(p, (BlockID)data.Get(EnvProp.SidesBlock)),
                      Block.GetName(p, (BlockID)data.Get(EnvProp.EdgeBlock)));
            p.Message(Locale.Get("mapinfo.clouds_weather_speed", p),
                      (data.Get(EnvProp.CloudsSpeed)  / 256f).ToString("F2"),
                      (data.Get(EnvProp.WeatherSpeed) / 256f).ToString("F2"));
            p.Message(Locale.Get("mapinfo.weather_fog", p),
                      (data.Get(EnvProp.WeatherFade) / 128f).ToString("F2"),
                      data.Get(EnvProp.ExpFog) > 0 ? "&aON" : "&cOFF");
            p.Message(Locale.Get("mapinfo.skybox", p),
                      data.GetSkybox(EnvProp.SkyboxHorSpeed),
                      data.GetSkybox(EnvProp.SkyboxVerSpeed));
        }
        
        class MapInfo {
            public ushort Width, Height, Length;
            public string Name, MapName;
            public long BlockDBEntries = -1;
            public AccessController Visit, Build;
            public LevelConfig Config;

            public void FromLevel(Level lvl) {
                Name = lvl.name; MapName = lvl.MapName;
                Width = lvl.Width; Height = lvl.Height; Length = lvl.Length;
                BlockDBEntries = lvl.BlockDB.TotalEntries();
                Config = lvl.Config;
                
                Visit = lvl.VisitAccess; 
                Build = lvl.BuildAccess;
            }
            
            public void FromMap(string map) {
                this.Name = map; MapName = map;
                string path  = LevelInfo.MapPath(map);
                Vec3U16 dims = IMapImporter.GetFor(path).ReadDimensions(path);
                
                Width = dims.X; Height = dims.Y; Length = dims.Z;
                BlockDBEntries = BlockDBFile.CountEntries(map);

                path = LevelInfo.PropsPath(map);
                LevelConfig cfg = new LevelConfig();
                cfg.Load(path);
                
                Config = cfg;
                Visit = new LevelAccessController(cfg, map, true);
                Build = new LevelAccessController(cfg, map, false);
            }
            
            public int Get(EnvProp i) {
                int value    = Config.GetEnvProp(i);
                bool block   = i == EnvProp.EdgeBlock || i == EnvProp.SidesBlock;
                int default_ = block ? Block.Invalid : EnvConfig.ENV_USE_DEFAULT;
                return value != default_ ? value : EnvConfig.DefaultEnvProp(i, Height);
            }
            
            public string GetSkybox(EnvProp i) {
                int angle = Get(i);
                return angle == 0 ? "none" : (angle / 1024.0).ToString("F3") + "/s";
            }
        }
        
        static string Color(string src) {
            return (src == null || src.Length == 0 || src == "-1") ? "&bnone" : "&b" + src;
        }
        
        public override void Help(Player p)  {
            p.Message(Locale.Get("mapinfo.help1", p));
            p.Message(Locale.Get("mapinfo.help2", p));
            p.Message(Locale.Get("mapinfo.help3", p));
            p.Message(Locale.Get("mapinfo.help4", p));
        }
    }
}
