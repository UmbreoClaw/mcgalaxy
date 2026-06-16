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
using System.Windows.Forms;
using MCGalaxy.Commands;
using MCGalaxy.SQL;
using MCGalaxy.Gui.Popups;

namespace MCGalaxy.Gui {

    public partial class PropertyWindow : Form {
        bool warnDisabledVerification = true;
        
        // Localizes the high-visibility parts of the settings window (tab strip and
        // the Server tab). Uses the server's configured language.
        void LocalizeUI() {
            Text = Locale.Get("gui.settings_title");

            pageServer.Text   = Locale.Get("gui.tab_server");
            pageChat.Text     = Locale.Get("gui.tab_chat");
            pageRelay.Text    = Locale.Get("gui.tab_irc");
            pageEco.Text      = Locale.Get("gui.tab_eco");
            pageMisc.Text     = Locale.Get("gui.tab_misc");
            pageGames.Text    = Locale.Get("gui.tab_games");
            pageRanks.Text    = Locale.Get("gui.tab_ranks");
            pageCommands.Text = Locale.Get("gui.tab_commands");
            pageBlocks.Text   = Locale.Get("gui.tab_blocks");
            pageSecurity.Text = Locale.Get("gui.tab_security");

            srv_grp.Text         = Locale.Get("gui.srv_general");
            srv_lblName.Text     = Locale.Get("gui.srv_name");
            srv_lblMotd.Text     = Locale.Get("gui.srv_motd");
            srv_lblPort.Text     = Locale.Get("gui.srv_port");
            srv_btnPort.Text     = Locale.Get("gui.srv_port_forward");
            srv_lblOwner.Text    = Locale.Get("gui.srv_owner");
            srv_chkPublic.Text   = Locale.Get("gui.srv_public");
            srv_lblLanguage.Text = Locale.Get("gui.srv_language");

            grpPlayers.Text      = Locale.Get("gui.players_group");
            srv_lblPlayers.Text  = Locale.Get("gui.srv_max_players");
            srv_lblGuests.Text   = Locale.Get("gui.srv_max_guests");
            srv_cbMustAgree.Text = Locale.Get("gui.srv_must_agree");

            lvl_grp.Text         = Locale.Get("gui.lvl_settings");
            lvl_lblMain.Text     = Locale.Get("gui.lvl_main");
            lvl_chkAutoload.Text = Locale.Get("gui.lvl_autoload");
            lvl_chkWorld.Text    = Locale.Get("gui.lvl_world_chat");

            adv_grp.Text       = Locale.Get("gui.adv_config");
            srv_grpUpdate.Text = Locale.Get("gui.update_settings");
        }

        void LoadGeneralProps() {
            srv_txtName.Text = Server.Config.Name;
            srv_txtMOTD.Text = Server.Config.MOTD;
            srv_numPort.Value = Server.Config.Port;
            srv_txtOwner.Text = Server.Config.OwnerName;
            srv_chkPublic.Checked = Server.Config.Public;

            srv_cmbLanguage.Items.Clear();
            var locales = Locale.AvailableLocales();
            locales.Sort();
            foreach (string code in locales)
                srv_cmbLanguage.Items.Add(code);
            if (locales.Count == 0) srv_cmbLanguage.Items.Add("en");
            string current = Server.Config.Language;
            int idx = srv_cmbLanguage.Items.IndexOf(current);
            srv_cmbLanguage.SelectedIndex = idx >= 0 ? idx : 0;
            
            srv_numPlayers.Value = Server.Config.MaxPlayers;
            srv_numGuests.Value = Server.Config.MaxGuests;
            srv_numGuests.Maximum = srv_numPlayers.Value;
            srv_cbMustAgree.Checked = Server.Config.AgreeToRulesOnEntry;
            
            lvl_txtMain.Text = Server.Config.MainLevel;
            lvl_chkAutoload.Checked = Server.Config.AutoLoadMaps;
            lvl_chkWorld.Checked = Server.Config.ServerWideChat;
            
            warnDisabledVerification = false;
            adv_chkVerify.Checked    = Server.Config.VerifyNames;
            warnDisabledVerification = true;
            adv_chkCPE.Checked = Server.Config.EnableCPE;       
            chkUpdates.Checked = Server.Config.CheckForUpdates;
        }
        
        void ApplyGeneralProps() {
            Server.Config.Name = srv_txtName.Text;
            Server.Config.MOTD = srv_txtMOTD.Text;
            Server.Config.Port = (int)srv_numPort.Value;
            Server.Config.OwnerName = srv_txtOwner.Text;
            Server.Config.Public = srv_chkPublic.Checked;
            if (srv_cmbLanguage.SelectedItem != null)
                Server.Config.Language = srv_cmbLanguage.SelectedItem.ToString();
            
            Server.Config.MaxPlayers = (int)srv_numPlayers.Value;
            Server.Config.MaxGuests = (int)srv_numGuests.Value;
            Server.Config.AgreeToRulesOnEntry = srv_cbMustAgree.Checked;  
            
            Server.Config.MainLevel = lvl_txtMain.Text;
            Server.Config.AutoLoadMaps = lvl_chkAutoload.Checked;
            Server.Config.ServerWideChat = lvl_chkWorld.Checked;
            
            Server.Config.VerifyNames = adv_chkVerify.Checked;
            Server.Config.EnableCPE = adv_chkCPE.Checked;            
            Server.Config.CheckForUpdates = chkUpdates.Checked;
            //Server.Config.reportBack = ;  //No setting for this?                
        }        
        
        
        const string warnMsg = "Disabling name verification means players\ncan login as anyone, including YOU\n\n" +
            "Are you sure you want to disable name verification?";
        void chkVerify_CheckedChanged(object sender, EventArgs e) {
            if (!warnDisabledVerification || adv_chkVerify.Checked) return;            
            if (Popup.OKCancel(warnMsg, "Security warning")) return;
            adv_chkVerify.Checked = true;
        }
        
        void numPlayers_ValueChanged(object sender, EventArgs e) {
            // Ensure that number of guests is never more than number of players
            if (srv_numGuests.Value > srv_numPlayers.Value) {
                srv_numGuests.Value = srv_numPlayers.Value;
            }
            srv_numGuests.Maximum = srv_numPlayers.Value;
        }
        
        void ChkPort_Click(object sender, EventArgs e) {
            int port = (int)srv_numPort.Value;
            using (PortTools form = new PortTools(port)) {
                form.ShowDialog();
            }
        }

        void forceUpdateBtn_Click(object sender, EventArgs e) {
            srv_btnForceUpdate.Enabled = false;
            string msg = "Would you like to force update " + Server.SoftwareName + " now?";
            
            if (Popup.YesNo(msg, "Force update")) {
                SaveChanges();
                Updater.PerformUpdate();
                Dispose();
            } else {
                srv_btnForceUpdate.Enabled = true;
            }
        }
    }
}
