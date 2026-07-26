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
using System.Net;
using MCGalaxy.Network;
using MCGalaxy.Platform;
using MCGalaxy.Tasks;

namespace MCGalaxy 
{
    /// <summary> Checks for and applies software updates. </summary>
    public static class Updater 
    {    
        // This fork updates from its OWN rolling release: every push to the
        // survival branch republishes the survival-latest assets (see
        // .github/workflows/survival-support.yml), so /Server update pulls the
        // newest CI build directly - no fresh copy, no official CDN.
        public static string SourceURL = "https://github.com/UmbreoClaw/mcgalaxy";
        public const string BaseURL    = "https://raw.githubusercontent.com/UmbreoClaw/mcgalaxy/master/";
        public const string UploadsURL = "https://github.com/UmbreoClaw/mcgalaxy/releases/tag/survival-latest";
        const string RELEASE_URL       = "https://github.com/UmbreoClaw/mcgalaxy/releases/download/survival-latest/";
        const string CurrentVersionURL = RELEASE_URL + "current_version.txt";
        const string CHANGELOG_URL     = BaseURL + "Changelog.txt";

        const string DLL_URL = RELEASE_URL + "MCGalaxy_.dll";
        const string GUI_URL = RELEASE_URL + "MCGalaxy.exe";
        const string CLI_URL = RELEASE_URL + "MCGalaxyCLI.exe";

        // A rolling release has no version number to compare - the release's
        // current_version.txt (commit sha + build date, stamped by CI) is
        // compared against the copy saved by the last successful update.
        // Missing local copy = "needs updating" (fresh install / pre-fix build).
        const string VERSION_FILE = "props/build_version.txt";

        public static event EventHandler NewerVersionDetected;
        
        public static void UpdaterTask(SchedulerTask task) {
            UpdateCheck();
            task.Delay = TimeSpan.FromHours(2);
        }

        static void UpdateCheck() {
            if (!Server.Config.CheckForUpdates) return;

            try {
                if (!NeedsUpdating()) {
                    Logger.Log(LogType.SystemActivity, "No update found!");
                } else if (NewerVersionDetected != null) {
                    NewerVersionDetected(null, EventArgs.Empty);
                }
            } catch (Exception ex) {
                Logger.LogError("Error checking for updates", ex);
            }
        }
        
        public static bool NeedsUpdating() {
            using (WebClient client = HttpUtil.CreateWebClient()) {
                string latest  = client.DownloadString(CurrentVersionURL).Trim();
                string current = File.Exists(VERSION_FILE) ? File.ReadAllText(VERSION_FILE).Trim() : "";
                return latest.Length > 0 && latest != current;
            }
        }
        

        // Backwards compatibility
        public static void PerformUpdate() { PerformUpdate(true); }
        
        public static void PerformUpdate(bool release) {
            try {
                try {
                    DeleteFiles("Changelog.txt", "MCGalaxy_.update", "MCGalaxy.update", "MCGalaxyCLI.update",
                                "prev_MCGalaxy_.dll", "prev_MCGalaxy.exe", "prev_MCGalaxyCLI.exe");
                } catch {
                }
        		
                // one rolling channel - "release" and "latest" are the same build
                Logger.Log(LogType.SystemActivity, "Downloading survival-latest update files");
                WebClient client = HttpUtil.CreateWebClient();
                string newVersion = client.DownloadString(CurrentVersionURL).Trim();

                DownloadFile(client, DLL_URL, "MCGalaxy_.update");
#if MCG_STANDALONE
                // Self contained executable, no separate CLI or GUI to download
#elif MCG_DOTNET
                DownloadFile(client, CLI_URL, "MCGalaxyCLI.update");
#else
                DownloadFile(client, GUI_URL, "MCGalaxy.update");
                DownloadFile(client, CLI_URL, "MCGalaxyCLI.update");
#endif
                DownloadFile(client, CHANGELOG_URL, "Changelog.txt");

                Server.SaveAllLevels();
                Player[] players = PlayerInfo.Online.Items;
                foreach (Player pl in players) pl.SaveStats();
                
                string serverDLL = Server.GetServerDLLPath();
                string serverGUI = "MCGalaxy.exe";
#if !MCG_DOTNET
                string serverCLI = "MCGalaxyCLI.exe";
#else
                string serverCLI = Server.GetServerExePath();
#endif
                
                // Move current files to previous files (by moving instead of copying, 
                //  can overwrite original the files without breaking the server)
                FileIO.TryMove(serverDLL, "prev_MCGalaxy_.dll");
                FileIO.TryMove(serverGUI, "prev_MCGalaxy.exe");
                FileIO.TryMove(serverCLI, "prev_MCGalaxyCLI.exe");

                // Move update files to current files
                FileIO.TryMove("MCGalaxy_.update",   serverDLL);
                FileIO.TryMove("MCGalaxy.update",    serverGUI);
                FileIO.TryMove("MCGalaxyCLI.update", serverCLI);

                // remember which build we are now on, for the next NeedsUpdating
                try { File.WriteAllText(VERSION_FILE, newVersion); } catch { }

                Server.Stop(true, "Updating server.");
            } catch (Exception ex) {
                Logger.LogError("Error performing update", ex);
            }
        }
        
        static void DownloadFile(WebClient client, string url, string dst) {
            Logger.Log(LogType.SystemActivity, "Downloading {0} to {1}", 
                       url, Path.GetFileName(dst));
            client.DownloadFile(url, dst);
        }
        
        static void DeleteFiles(params string[] paths) {
            foreach (string path in paths) { FileIO.TryDelete(path); }
        }
    }
}
