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
using System.IO;
using MCGalaxy.Levels.IO;
using MCGalaxy.Localization;
using MCGalaxy.Network;

namespace MCGalaxy.Commands.World {
    public sealed class CmdImport : Command2 {
        public override string name { get { return "Import"; } }
        public override string type { get { return CommandTypes.World; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }

        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) { Help(p); return; }
            if (!Directory.Exists(Paths.ImportsDir)) {
                Directory.CreateDirectory(Paths.ImportsDir);
            }
            
            if (message.CaselessEq("all")) {
                string[] paths = Directory.GetFiles(Paths.ImportsDir);
                ImportFiles(p, paths);
            } else if (message.IndexOf('/') >= 0) {
                ImportWeb(p, message);
            } else {
                if (!Formatter.ValidMapName(p, message)) return;
                ImportName(p, message);
            }
        }
        
        static void ImportWeb(Player p, string url) {
            HttpUtil.FilterURL(ref url);
            byte[] data = HttpUtil.DownloadData(url, p);
            if (data == null) return;
            
            // if data is not NULL, URL must be valid
            string path = new Uri(url).AbsolutePath;
            string map  = Path.GetFileNameWithoutExtension(path);
            if (!Formatter.ValidMapName(p, map)) return;
            
            using (Stream src = new MemoryStream(data))
                ImportFrom(p, src, path);
        }
        
        static void ImportFiles(Player p, string[] paths) {
            foreach (string path in paths)
            {
                using (Stream src = File.OpenRead(path))
                    ImportFrom(p, src, path);
            }
        }

        static void ImportName(Player p, string map) {
            map = Path.GetFileNameWithoutExtension(map);
            string path = Paths.ImportsDir + map;
            
            foreach (IMapImporter imp in IMapImporter.Formats)
            {
                path = Path.ChangeExtension(path, imp.Extension);
                if (!File.Exists(path)) continue;
                
                using (Stream src = File.OpenRead(path)) {
                    Import(p, imp, src, map); return;
                }
            }
            
            string formats = IMapImporter.Formats.Join(x => x.Extension);
            p.Message(Locale.Get("import.no_file", p), formats);
        }
        
        
        static void ImportFrom(Player p, Stream src, string path) {
            IMapImporter imp = IMapImporter.GetFor(path);
            if (imp == null) {
                string formats = IMapImporter.Formats.Join(x => x.Extension);
                p.Message(Locale.Get("import.unsupported_format", p), path, formats);
                return;
            }
            
            string map = Path.GetFileNameWithoutExtension(path);
            Import(p, imp, src, map);
        }
        
        static void Import(Player p, IMapImporter importer, Stream src, string map) {
            if (LevelInfo.MapExists(map)) {
                p.Message(Locale.Get("import.map_exists", p), map);
                return;
            }
            
            try {
                Level lvl = importer.Read(src, map, true);
                try {
                    lvl.Save(true);
                } finally {
                    lvl.Dispose();
                    Server.DoGC();
                }
            } catch (Exception ex) {
                Logger.LogError("Error importing map " + map, ex);
                p.Message(Locale.Get("import.failed", p), map);
                return;
            }
            p.Message(Locale.Get("import.success", p), map);
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("import.help1", p));
            p.Message(Locale.Get("import.help2", p));
            p.Message(Locale.Get("import.help3", p));
            p.Message(Locale.Get("import.help4", p));
            p.Message(Locale.Get("import.help5", p));
        }

        public override void Help(Player p, string message) {
            if (message.CaselessEq("formats")) {
                p.Message(Locale.Get("import.formats_header", p));
                foreach (IMapImporter format in IMapImporter.Formats) {
                    p.Message(Locale.Get("cmd.import.msg1", p), format.Extension, format.Description);
                }
            } else {
                base.Help(p, message);
            }
        }
    }
}
