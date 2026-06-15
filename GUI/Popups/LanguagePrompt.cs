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
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MCGalaxy.Gui.Popups
{
    /// <summary> First-run dialog that lets the user pick the server language. </summary>
    public sealed class LanguagePrompt : Form
    {
        readonly ComboBox cmbLang;

        LanguagePrompt(List<string> locales, string current) {
            Text            = "Choose server language";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterScreen;
            MaximizeBox     = false;
            MinimizeBox     = false;
            ShowInTaskbar   = false;
            ClientSize      = new Size(320, 120);

            Label lbl = new Label();
            lbl.Text     = "Select the language for this server:";
            lbl.AutoSize = true;
            lbl.Location = new Point(12, 15);
            Controls.Add(lbl);

            cmbLang = new ComboBox();
            cmbLang.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbLang.Location      = new Point(15, 40);
            cmbLang.Size          = new Size(290, 23);
            foreach (string code in locales) cmbLang.Items.Add(code);
            int idx = cmbLang.Items.IndexOf(current);
            cmbLang.SelectedIndex = idx >= 0 ? idx : 0;
            Controls.Add(cmbLang);

            Button ok = new Button();
            ok.Text         = "OK";
            ok.DialogResult = DialogResult.OK;
            ok.Location     = new Point(225, 80);
            ok.Size         = new Size(80, 26);
            Controls.Add(ok);
            AcceptButton = ok;

            GuiUtils.SetIcon(this);
        }

        /// <summary> Shows the dialog modally and returns the chosen locale code,
        /// or null if cancelled. Must be called on the UI thread. </summary>
        public static string Show(List<string> locales) {
            if (locales == null || locales.Count == 0) return null;

            using (LanguagePrompt form = new LanguagePrompt(locales, Server.Config.Language)) {
                if (form.ShowDialog() != DialogResult.OK) return null;
                return form.cmbLang.SelectedItem as string;
            }
        }
    }
}
