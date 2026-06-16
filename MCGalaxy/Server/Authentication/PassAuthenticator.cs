/*
    Written by Jack1312
    Copyright 2011-2012 MCForge
        
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
using System.Security.Cryptography;
using System.Text;
using MCGalaxy.Events.PlayerEvents;

namespace MCGalaxy.Authentication
{
    /// <summary> Manages optional additional verification for certain users </summary>
    public abstract class ExtraAuthenticator
    {
        /// <summary> The currently/actively used authenticator </summary>
        public static ExtraAuthenticator Current { get; protected set; }
        
        protected abstract void Activate();
        
        protected abstract void Deactivate();
        
        public static void SetActive(ExtraAuthenticator auth) {
            if (Current != null) Current.Deactivate();
            
            Current = auth;
            auth.Activate();
        }
        
        
        /// <summary> Informs the given player that they must first
        /// verify before they can perform the given action </summary>
        public abstract void RequiresVerification(Player p, string action);
        
        /// <summary> Informs the given player that they should verify,
        /// otherwise they will be unable to perform some actions </summary>
        public abstract void NeedVerification(Player p);
        
        /// <summary> Attempts to automatically verify the player at login </summary>
        public abstract void AutoVerify(Player p, string mppass);
        
        protected void Verify(Player p) {
            p.Message(Locale.Get("pass.now_verified", p));
            p.verifiedPass = true;
            p.Unverified   = false;
        }
    }
    
    /// <summary> Performs extra authentication using a per player password </summary>
    public abstract class PassAuthenticator : ExtraAuthenticator
    {
        public override void RequiresVerification(Player p, string action) {
            p.Message(Locale.Get("pass.must_verify", p), action);
        }
        
        public override void NeedVerification(Player p) {
            if (!HasPassword(p.name)) {
                p.Message(Locale.Get("pass.set_password", p));
            } else {
                p.Message(Locale.Get("pass.complete_verification", p));
            }
        }
        
        public override void AutoVerify(Player p, string mppass) {
            if (!HasPassword(p.name)) return;
            if (!VerifyPassword(p.name, mppass)) return;
            
            Verify(p);
        }
        
        
        /// <summary> Returns whether the given player has a stored password </summary>
        public abstract bool HasPassword(string name);
        
        /// <summary> Returns whether the given pasword equals
        /// the stored password for the given player </summary>
        public abstract bool VerifyPassword(string name, string password);
        
        /// <summary> Sets the stored password for the given player </summary>
        public abstract void StorePassword(string name, string password);
        
        /// <summary> Removes the stored password for the given player </summary>
        /// <returns> Whether the given player actually had a stored password </returns>
        public abstract bool ResetPassword(string name);
        
        
        protected override void Activate() {
            OnPlayerHelpEvent.Register(OnPlayerHelp, Priority.Low);
            OnPlayerCommandEvent.Register(OnPlayerCommand, Priority.Low);
        }
        
        protected override void Deactivate() {
            OnPlayerHelpEvent.Unregister(OnPlayerHelp);
            OnPlayerCommandEvent.Unregister(OnPlayerCommand);
        }
        
        void OnPlayerHelp(Player p, string target, ref bool cancel) {
            if (!(target.CaselessEq("pass") || target.CaselessEq("password") || target.CaselessEq("setpass"))) return;
            
            PrintHelp(p);
            cancel = true;
        }
        
        void OnPlayerCommand(Player p, string cmd, string args, CommandData data) {
            if (cmd.CaselessEq("pass")) {
                ExecPassCommand(p, args, data);
                p.cancelcommand = true;
            } else if (cmd.CaselessEq("setpass")) {
                ExecPassCommand(p, "set " + args, data);
                p.cancelcommand = true;
            } else if (cmd.CaselessEq("resetpass")) {
                ExecPassCommand(p, "reset " + args, data);
                p.cancelcommand = true;
            }
        }
        

        void ExecPassCommand(Player p, string message, CommandData data) {
            if (!Server.Config.verifyadmins) {
                p.Message(Locale.Get("pass.not_enabled", p)); return;
            }
            if (data.Rank < Server.Config.VerifyAdminsRank) {
                Formatter.MessageNeedMinPerm(p, "+ require password verification",
                                             Server.Config.VerifyAdminsRank); return;
            }
            
            message = message.Trim();
            if (message.Length == 0) { PrintHelp(p); return; }
            string[] args = message.SplitSpaces(2);
            
            if (args.Length == 2 && args[0].CaselessEq("set")) {
                DoSetPassword(p, args[1]);
            } else if (args.Length == 2 && args[0].CaselessEq("reset")) {
                DoResetPassword(p, args[1], data);
            } else {
                DoVerifyPassword(p, message);
            }
        }
        
        void DoVerifyPassword(Player p, string password) {
            if (!p.Unverified) { p.Message(Locale.Get("pass.already_verified", p)); return; }
            if (p.passtries >= 3) { p.Kick(Locale.Get("pass.guess_kick", p)); return; }
            if (password.IndexOf(' ') >= 0) { p.Message(Locale.Get("pass.one_word", p)); return; }

            if (!HasPassword(p.name)) {
                p.Message(Locale.Get("pass.not_set_yet", p));
                p.Message(Locale.Get("pass.different_pass", p));
                return;
            }
            if (VerifyPassword(p.name, password)) {
                Verify(p); return;
            }
            
            p.passtries++;
            p.Message(Locale.Get("pass.wrong", p));
            p.Message(Locale.Get("pass.forgot", p), Server.Config.OwnerName);
        }
        
        void DoSetPassword(Player p, string password) {
            if (p.Unverified && HasPassword(p.name)) {
                RequiresVerification(p, "can change your verification password");
                p.Message(Locale.Get("pass.forgot", p), Server.Config.OwnerName);
                return;
            }
            if (password.IndexOf(' ') >= 0) { 
                p.Message(Locale.Get("pass.must_one_word", p)); return; 
            }
            
            StorePassword(p.name, password);
            p.Message(Locale.Get("pass.set_to", p), password);
        }
        
        void DoResetPassword(Player p, string name, CommandData data) {
            string target = PlayerInfo.FindMatchesPreferOnline(p, name);
            if (target == null) return;
            
            if (p.Unverified) {
                RequiresVerification(p, "can reset verification passwords");
                return;
            }
            if (data.Rank < Server.Config.ResetPasswordRank) {
                p.Message(Locale.Get("pass.cannot_reset", p),
                          Group.GetColoredName(Server.Config.ResetPasswordRank));
                return;
            }
            
            if (ResetPassword(target)) {
                p.Message(Locale.Get("pass.reset_for", p), p.FormatNick(target));
            } else {
                p.Message(Locale.Get("pass.no_password", p), p.FormatNick(target));
            }
        }
        
        static void PrintHelp(Player p) {
            p.Message(Locale.Get("pass.help1", p));
            p.Message(Locale.Get("pass.help2", p),
                      Group.GetColoredName(Server.Config.ResetPasswordRank));
            p.Message(Locale.Get("pass.help3", p));
            p.Message(Locale.Get("pass.help4", p));
            p.Message(Locale.Get("pass.help5", p));
            p.Message(Locale.Get("pass.help6", p),
                      Group.GetColoredName(Server.Config.VerifyAdminsRank));
            p.Message(Locale.Get("pass.help7", p));
        }
    }
    
    /// <summary> Password authenticator that loads/stores passwords in /extra/passwords folder </summary>
    public class DefaultPassAuthenticator : PassAuthenticator
    {
        const string PASS_FOLDER = "extra/passwords/";
        
        public override bool HasPassword(string name) { return GetHashPath(name) != null; }
 
        public override bool VerifyPassword(string name, string password) {
            string path = GetHashPath(name);
            if (path == null) return false;
            
            return CheckHash(path, name, password);
        }
        
        public override void StorePassword(string name, string password) {
            byte[] hash = ComputeHash(name, password);
            
            Directory.CreateDirectory(PASS_FOLDER);
            File.WriteAllBytes(HashPath(name), hash);
        }
        
        public override bool ResetPassword(string name) {
            string path = GetHashPath(name);
            if (path == null) return false;
            
            File.Delete(path);
            return true;
        }
        
        
        static string GetHashPath(string name) {
            string path = HashPath(name);
            return File.Exists(path) ? path : null;
        }

        static string HashPath(string name) {
            // unfortunately necessary for backwards compatibility
            name = Server.ToRawUsername(name);
            
            return PASS_FOLDER + name.ToLower() + ".pwd";
        }

        static bool CheckHash(string path, string name, string pass) {
            byte[] stored   = File.ReadAllBytes(path);
            byte[] computed = ComputeHash(name, pass);
            return ArraysEqual(computed, stored);
        }

        static byte[] ComputeHash(string name, string pass) {
            // The constant string added to the username salt is to mitigate
            // rainbow tables. We should really have a unique salt for each
            // user, but this is close enough.
            byte[] data = Encoding.UTF8.GetBytes("0bec662b-416f-450c-8f50-664fd4a41d49" + name.ToLower() + " " + pass);
            return SHA256.Create().ComputeHash(data);
        }
        
        static bool ArraysEqual(byte[] a, byte[] b) {
            if (a.Length != b.Length) return false;
            
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }
}