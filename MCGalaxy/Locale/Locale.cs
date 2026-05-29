/*
    Copyright 2024 MCGalaxy contributors

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
using System.IO;
using System.Text;

namespace MCGalaxy
{
    /// <summary> Provides server-wide and per-player locale (translation) support. </summary>
    /// <remarks>
    /// Locale files are stored as UTF-8 key=value text files in the locale/ directory.
    /// Lines starting with # are comments. Format: key = translated string
    /// {0}, {1}, etc. are format placeholders. &amp;X codes are colour codes.
    /// </remarks>
    public static class Locale
    {
        // volatile so Reload() swap is visible to all threads without locking reads
        static volatile Dictionary<string, Dictionary<string, string>> locales =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary> Returns translated string for key using the player's preferred locale,
        /// falling back to the server default, then to English, then to the key itself. </summary>
        public static string Get(string key, Player p = null)
        {
            string locale = (p != null && !string.IsNullOrEmpty(p.Language))
                ? p.Language
                : Server.Config.Language;
            return Get(key, locale);
        }

        /// <summary> Returns translated string for key in the specified locale. </summary>
        public static string Get(string key, string locale)
        {
            var snapshot = locales;
            string value;

            Dictionary<string, string> translations;
            if (snapshot.TryGetValue(locale, out translations) &&
                translations.TryGetValue(key, out value))
                return value;

            // Fallback to English
            if (!locale.Equals("en", StringComparison.OrdinalIgnoreCase) &&
                snapshot.TryGetValue("en", out translations) &&
                translations.TryGetValue(key, out value))
                return value;

            // Last resort: return the key unchanged
            return key;
        }

        /// <summary> Returns a list of all loaded locale codes (e.g. "en", "es"). </summary>
        public static List<string> AvailableLocales()
        {
            return new List<string>(locales.Keys);
        }

        /// <summary> Loads all .lang files from the locale directory. Safe to call on startup. </summary>
        public static void Load()
        {
            string dir = Paths.LocaleDir;
            if (!Directory.Exists(dir)) {
                try { Directory.CreateDirectory(dir); } catch { }
            }

            var loaded = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            string[] files;
            try { files = Directory.GetFiles(dir, "*.lang"); }
            catch { files = new string[0]; }

            foreach (string file in files) {
                string code = Path.GetFileNameWithoutExtension(file);
                loaded[code] = ParseFile(file);
            }

            locales = loaded;

            if (loaded.Count > 0)
                Logger.Log(LogType.SystemActivity, "Loaded {0} locale(s)", loaded.Count);
        }

        /// <summary> Reloads all locale files from disk. </summary>
        public static void Reload()
        {
            locales = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            Load();
        }

        static Dictionary<string, string> ParseFile(string path)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string[] lines;
            try { lines = File.ReadAllLines(path, Encoding.UTF8); }
            catch { return dict; }

            foreach (string line in lines) {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed[0] == '#') continue;

                int eq = trimmed.IndexOf('=');
                if (eq < 0) continue;

                string key   = trimmed.Substring(0, eq).Trim();
                string value = trimmed.Substring(eq + 1).Trim();
                if (key.Length > 0) dict[key] = value;
            }

            return dict;
        }
    }
}
