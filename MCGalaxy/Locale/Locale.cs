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

        /// <summary> Reloads all locale files from disk without a gap where keys go missing. </summary>
        public static void Reload()
        {
            // Load() builds a fresh dictionary then atomically swaps it in via the
            // volatile field — no need to clear first, which would cause a brief window
            // where Get() returns raw key names instead of translations.
            Load();
        }

        static Dictionary<string, string> ParseFile(string path)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string[] lines;
            try { lines = File.ReadAllLines(path, Encoding.UTF8); }
            catch { return dict; }

            foreach (string line in lines) {
                // Skip blank lines and comments. Only leading whitespace is ignored
                // here - the value itself must keep any indentation it has.
                int start = 0;
                while (start < line.Length && (line[start] == ' ' || line[start] == '\t')) start++;
                if (start >= line.Length || line[start] == '#') continue;

                int eq = line.IndexOf('=');
                if (eq < 0) continue;

                string key = line.Substring(0, eq).Trim();
                if (key.Length == 0) continue;

                // The canonical separator is " = " (one space each side). Strip exactly
                // one leading separator space so intentional indentation in help text
                // (e.g. "  &HsubItem") is preserved rather than trimmed away.
                string value = line.Substring(eq + 1);
                if (value.Length > 0 && value[0] == ' ') value = value.Substring(1);

                dict[key] = value;
            }

            return dict;
        }
    }
}
