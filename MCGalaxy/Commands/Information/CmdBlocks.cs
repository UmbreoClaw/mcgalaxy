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
using System.Collections.Generic;
using MCGalaxy.Blocks;
using MCGalaxy.Commands.World;
using BlockID = System.UInt16;

namespace MCGalaxy.Commands.Info 
{
    public sealed class CmdBlocks : Command2 
    {
        public override string name { get { return "Blocks"; } }
        public override string type { get { return CommandTypes.Information; } }
        public override bool UseableWhenFrozen { get { return true; } }
        public override CommandAlias[] Aliases {
            get { return new[] { new CommandAlias("Materials") }; }
        }

        public override void Use(Player p, string message, CommandData data) {
            string[] args = message.SplitSpaces();
            string modifier = args.Length > 1 ? args[1] : "";
            string type = args[0];
            BlockID block;
            
            if (type.Length == 0 || type.CaselessEq("basic")) {
                p.Message(Locale.Get("blocks.basic_header", p));
                OutputBlocks(p, "basic", modifier,
                             b => !Block.IsPhysicsType(b));
            } else if (type.CaselessEq("all") || type.CaselessEq("complex")) {
                p.Message(Locale.Get("blocks.complex_header", p));
                OutputBlocks(p, "complex", modifier,
                             b => Block.IsPhysicsType(b));
            } else if ((block = Block.Parse(p, type)) != Block.Invalid) {
                OutputBlockInfo(p, block);
            } else if (Group.Find(type) != null) {
                Group grp = Group.Find(type);
                p.Message(Locale.Get("blocks.rank_can_place", p), grp.ColoredName);
                OutputBlocks(p, type, modifier,
                             b => grp.CanPlace[b]);
            } else if (args.Length > 1) {
                Help(p);
            } else {
                p.Message(Locale.Get("blocks.not_found", p));
            }
        }
        
        static void OutputBlocks(Player p, string type, string modifier, Predicate<BlockID> selector) {
            List<BlockID> blocks = new List<BlockID>(Block.SUPPORTED_COUNT);
            for (BlockID b = 0; b < Block.SUPPORTED_COUNT; b++) 
            {
                if (Block.ExistsFor(p, b) && selector(b)) blocks.Add(b);
            }

            Paginator.Output(p, blocks, b => Block.GetColoredName(p, b),
                             "Blocks " + type, "blocks", modifier);
        }
        
        static void OutputBlockInfo(Player p, BlockID block) {
            string name = Block.GetName(p, block);
            BlockProps[] scope = p.IsSuper ? Block.Props : p.level.Props;
            CmdBlockProperties.Detail(p, scope, block);
            
            if (Block.IsPhysicsType(block)) {
                p.Message(Locale.Get("blocks.complex_info", p), name);
                OutputPhysicsInfo(p, scope, block); return;
            }

            string msg = "";
            for (BlockID b = Block.CPE_COUNT; b < Block.CORE_COUNT; b++)
            {
                if (Block.Convert(b) != block) continue;
                msg += Block.GetColoredName(p, b) + ", ";
            }

            if (msg.Length > 0) {
                p.Message(Locale.Get("blocks.look_like", p), name);
                p.Message(msg.Remove(msg.Length - 2));
            } else {
                p.Message(Locale.Get("blocks.no_complex_look_like", p), name);
            }
        }
        
        static void OutputPhysicsInfo(Player p, BlockProps[] scope, BlockID b) {
            BlockID conv = Block.Convert(b);
            p.Message(Locale.Get("blocks.appears_as", p), Block.GetName(p, conv));

            // TODO: Use scope[b] instead of hardcoded global
            if (Block.LightPass(b))   p.Message(Locale.Get("blocks.allows_light", p));
            if (Block.NeedRestart(b)) p.Message(Locale.Get("blocks.auto_start", p));

            if (Physics(scope, b)) {
                p.Message(Locale.Get("blocks.affects_physics", p));
            } else {
                p.Message(Locale.Get("blocks.no_physics", p));
            }

            if (Block.AllowBreak(b))     p.Message(Locale.Get("blocks.anyone_activate", p));
            if (Block.Walkthrough(conv)) p.Message(Locale.Get("blocks.walkthrough", p));
            if (Mover(scope, conv))      p.Message(Locale.Get("blocks.walkthrough_activate", p));
        }
        
        static bool Mover(BlockProps[] scope, BlockID conv) {
            bool nonSolid = Block.Walkthrough(conv);
            return BlockBehaviour.GetWalkthroughHandler(conv, scope, nonSolid) != null;
        }
        
        static bool Physics(BlockProps[] scope, BlockID b) {
            if (scope[b].IsMessageBlock || scope[b].IsPortal) return false;
            if (scope[b].IsDoor || scope[b].IsTDoor) return false;
            if (scope[b].OPBlock) return false;
            
            return BlockBehaviour.GetPhysicsHandler(b, Block.Props) != null;
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("blocks.help1", p));
            p.Message(Locale.Get("blocks.help2", p));
            p.Message(Locale.Get("blocks.help3", p));
            p.Message(Locale.Get("blocks.help4", p));
            p.Message(Locale.Get("blocks.help5", p));
        }
    }
}
