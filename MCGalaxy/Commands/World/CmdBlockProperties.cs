/*
    Copyright 2015-2024 MCGalaxy
        
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
using MCGalaxy.Blocks;
using BlockID = System.UInt16;

namespace MCGalaxy.Commands.World {
    public sealed class CmdBlockProperties : Command2 {
        public override string name { get { return "BlockProperties"; } }
        public override string shortcut { get { return "BlockProps"; } }
        public override string type { get { return CommandTypes.World; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Admin; } }

        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) { Help(p); return; }
            string[] args = message.SplitSpaces(4);
            if (args.Length < 2) { Help(p); return; }
            
            BlockProps[] scope = GetScope(p, data, args[0]);
            if (scope == null) return;           
            if (IsListAction(args[1]) && (args.Length == 2 || IsListModifier(args[2]))) {
                ListProps(p, scope, args); return;
            }
            
            BlockID block = GetBlock(p, scope, args[1]);
            if (block == Block.Invalid) return;
            if (args.Length < 3) { Help(p); return; }            
            string opt = args[2];
            
            if (opt.CaselessEq("copy")) {
                CopyProps(p, scope, block, args);
            } else if (opt.CaselessEq("reset") || IsDeleteAction(opt)) {
                ResetProps(p, scope, block);
            } else {
                SetProps(p, scope, block, args);
            }
        }
        
        static BlockProps[] GetScope(Player p, CommandData data, string scope) {
            if (scope.CaselessEq("core") || scope.CaselessEq("global")) return Block.Props;

            if (scope.CaselessEq("level")) {
                if (p.IsSuper) { p.Message(Locale.Get("blockprops.no_level_scope", p), p.SuperName); return null; }
                if (!LevelInfo.Check(p, data.Rank, p.level, "change properties of blocks in this level")) return null;
                return p.level.Props;
            }

            p.Message(Locale.Get("blockprops.invalid_scope", p));
            return null;
        }
        
        static BlockID GetBlock(Player p, BlockProps[] scope, string str) {
            Player pScope = scope == Block.Props ? Player.Console : p;
            BlockID block = Block.Parse(pScope, str);
            
            if (block == Block.Invalid) {
                p.Message(Locale.Get("blockprops.no_block", p), str);
            }
            return block;
        }
        
        internal static void Detail(Player p, BlockProps[] scope, BlockID block) {
            BlockProps props = scope[block];
            string name = BlockProps.ScopedName(scope, p, block);
            p.Message(Locale.Get("cmd.blockproperties.help1", p), name);
            
            if (props.KillerBlock)          p.Message(Locale.Get("cmd.blockproperties.msg1", p));
            if (props.DeathMessage != null) p.Message("  Death message: &S" + props.DeathMessage);
            
            if (props.IsDoor)  p.Message(Locale.Get("cmd.blockproperties.msg2", p));
            if (props.IsTDoor) p.Message(Locale.Get("cmd.blockproperties.msg3", p));
            if (props.oDoorBlock != Block.Invalid) 
                p.Message(Locale.Get("cmd.blockproperties.msg4", p));
            
            if (props.IsPortal)       p.Message(Locale.Get("cmd.blockproperties.msg5", p));
            if (props.IsMessageBlock) p.Message(Locale.Get("cmd.blockproperties.msg6", p));
            
            if (props.WaterKills) p.Message(Locale.Get("cmd.blockproperties.msg7", p));
            if (props.LavaKills)  p.Message(Locale.Get("cmd.blockproperties.msg8", p));
            
            if (props.OPBlock) p.Message(Locale.Get("cmd.blockproperties.msg9", p));
            if (props.IsRails) p.Message(Locale.Get("cmd.blockproperties.msg10", p));
            
            if (props.AnimalAI != AnimalAI.None) {
                p.Message(Locale.Get("cmd.blockproperties.msg11", p), props.AnimalAI);
            }
            if (props.StackBlock != Block.Air) {
                p.Message(Locale.Get("cmd.blockproperties.msg12", p), 
                          BlockProps.ScopedName(scope, p, props.StackBlock));
            }
            if (props.Drownable) p.Message(Locale.Get("cmd.blockproperties.help2", p));
            
            if (props.GrassBlock != Block.Invalid) {
                p.Message(Locale.Get("cmd.blockproperties.msg13", p), 
                          BlockProps.ScopedName(scope, p, props.GrassBlock));
            }
            if (props.DirtBlock != Block.Invalid) {
                p.Message(Locale.Get("cmd.blockproperties.msg14", p), 
                          BlockProps.ScopedName(scope, p, props.DirtBlock));
            }
        }
        
        static List<BlockID> FilterProps(BlockProps[] scope) {
            int changed = BlockProps.ScopeId(scope);
            List<BlockID> filtered = new List<BlockID>();
            
            for (int b = 0; b < scope.Length; b++) {
                if ((scope[b].ChangedScope & changed) == 0) continue;                
                filtered.Add((BlockID)b);
            }
            return filtered;
        }
        
        void ListProps(Player p, BlockProps[] scope, string[] args) {
            List<BlockID> filtered = FilterProps(scope);
            string cmd      = "BlockProps " + args[0] + " list";
            string modifier = args.Length > 2 ? args[2] : "";
            
            Paginator.Output(p, filtered, b => BlockProps.ScopedName(scope, p, b),
                             cmd, "modified blocks", modifier);
        }
        
        void CopyProps(Player p, BlockProps[] scope, BlockID block, string[] args) {
            if (args.Length < 4) { Help(p); return; }
            BlockID dst = GetBlock(p, scope, args[3]);
            if (dst == Block.Invalid) return;
            
            scope[dst] = scope[block];
            scope[dst].ChangedScope |= BlockProps.ScopeId(scope);
            
            p.Message(Locale.Get("blockprops.copied", p),
                      BlockProps.ScopedName(scope, p, block),
                      BlockProps.ScopedName(scope, p, dst));
            BlockProps.ApplyChanges(scope, p.level, block, true);
        }
        
        void ResetProps(Player p, BlockProps[] scope, BlockID block) {
            scope[block] = BlockProps.MakeDefault(scope, p.level, block);
            string name  = BlockProps.ScopedName(scope, p, block);
            
            p.Message(Locale.Get("blockprops.reset", p), name);
            BlockProps.ApplyChanges(scope, p.level, block, true);
        }
        
        void SetProps(Player p, BlockProps[] scope, BlockID block, string[] args) {
            BlockOption opt = BlockOptions.Find(args[2]);
            if (opt == null) { Help(p); return; }
            string value = args.Length > 3 ? args[3] : "";
            
            opt.SetFunc(p, scope, block, value);
            scope[block].ChangedScope |= BlockProps.ScopeId(scope);
            BlockProps.ApplyChanges(scope, p.level, block, true);
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("blockprops.help1", p));
            p.Message(Locale.Get("blockprops.help2", p));
            p.Message(Locale.Get("blockprops.help3", p));
            p.Message(Locale.Get("blockprops.help4", p));
            p.Message(Locale.Get("blockprops.help5", p));
            p.Message(Locale.Get("blockprops.help6", p));
            p.Message(Locale.Get("blockprops.help7", p));
            p.Message(Locale.Get("blockprops.help8", p));
            p.Message(Locale.Get("blockprops.help9", p));
        }

        public override void Help(Player p, string message) {
            if (message.CaselessEq("props") || message.CaselessEq("properties")) {
                p.Message(Locale.Get("cmd.blockproperties.help3", p), BlockOptions.Options.Join(o => o.Name));
                p.Message(Locale.Get("blockprops.help_props_more", p));
                return;
            }

            BlockOption opt = BlockOptions.Find(message);
            if (opt != null) {
                p.Message(opt.Help);
            } else {
                p.Message(Locale.Get("blockprops.unrecognised", p), message);
            }
        }
    }
}
