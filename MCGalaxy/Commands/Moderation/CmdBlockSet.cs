/*
    Copyright 2010 MCSharp team (Modified for use with MCZall/MCLawl/MCForge)
    
    Dual-licensed under the    Educational Community License, Version 2.0 and
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
using MCGalaxy.Blocks;
using BlockID = System.UInt16;

namespace MCGalaxy.Commands.Moderation 
{
    public sealed class CmdBlockSet : ItemPermsCmd 
    {
        public override string name { get { return "BlockSet"; } }
        
        public override void Use(Player p, string message, CommandData data) {
            bool canPlace  = true; const string PLACE_PREFIX  = "place ";
            bool canDelete = true; const string DELETE_PREFIX = "delete ";
            string placeMsg = null, deleteMsg = null;
            
            if (message.CaselessStarts(PLACE_PREFIX)) {
                canDelete = false;
                message   = message.Substring(PLACE_PREFIX.Length);
            } else if (message.CaselessStarts(DELETE_PREFIX)) {
                canPlace  = false;
                message   = message.Substring(DELETE_PREFIX.Length);
            }
            
            string[] args = message.SplitSpaces(2);
            if (args.Length < 2) { Help(p); return; }
            
            BlockID block;
            if (!CommandParser.GetBlockIfAllowed(p, args[0], "change permissions of", out block)) return;

            if (canPlace) {
                BlockPerms perms = BlockPerms.GetPlace(block);
                placeMsg  = SetPerms(p, args, data, perms, "block", "use", "usable");
            }
            if (canDelete) {
                BlockPerms perms = BlockPerms.GetDelete(block);
                deleteMsg = SetPerms(p, args, data, perms, "block", "delete", "deletable");                    
            }
            
            if (placeMsg == null && deleteMsg == null) return;
            UpdatePerms(block, p, placeMsg, deleteMsg);
        }
        
        void UpdatePerms(BlockID block, Player p, string placeMsg, string deleteMsg) {
            BlockPerms.Save();
            BlockPerms.ApplyChanges();
            
            if (!Block.IsPhysicsType(block)) {
                BlockPerms.ResendAllBlockPermissions();
            }            
            string name = Block.GetName(p, block);
            
            if (placeMsg != null && deleteMsg != null) {
                Announce(p, name + placeMsg.Replace("usable", "usable and deletable"));
            } else if (placeMsg != null) {
                Announce(p, name + placeMsg);
            } else {
                Announce(p, name + deleteMsg);
            }
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("cmd.blockset.help1", p));
            p.Message(Locale.Get("cmd.blockset.help2", p));
            p.Message(Locale.Get("cmd.blockset.help3", p));
            p.Message(Locale.Get("cmd.blockset.help4", p));
            p.Message(Locale.Get("cmd.blockset.help5", p));
            p.Message(Locale.Get("cmd.blockset.help6", p));
            p.Message(Locale.Get("cmd.blockset.help7", p));
            p.Message(Locale.Get("cmd.blockset.help8", p));
        }
        
        public override void Help(Player p, string message) {
            if (!message.CaselessEq("advanced")) { base.Help(p, message); return; }
            
            p.Message(Locale.Get("cmd.blockset.help9", p));
            p.Message(Locale.Get("cmd.blockset.help10", p));
            p.Message(Locale.Get("cmd.blockset.help11", p));
            p.Message(Locale.Get("cmd.blockset.help12", p));
            // TODO place and delete messages
        }
    }
}
