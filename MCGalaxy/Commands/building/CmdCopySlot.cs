/*
    Copyright 2015-2024 MCGalaxy
    
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
using System;
using System.Collections.Generic;
using MCGalaxy.Drawing;

namespace MCGalaxy.Commands.Building
{
    public sealed class CmdCopySlot : Command2
    {
        public override string name { get { return "CopySlot"; } }
        public override string shortcut { get { return "cs"; } }
        public override string type { get { return CommandTypes.Building; } }
        public override LevelPermission defaultRank { get { return LevelPermission.AdvBuilder; } }
        public override bool SuperUseable { get { return false; } }

        public override void Use(Player p, string message, CommandData data) {
            if (message.Length == 0) {
                OutputCopySlots(p);
            } else if (message.CaselessEq("random")) {
                SetRandomCopySlot(p);
            } else if (message.CaselessStarts("clear")) {
                string[] words = message.SplitSpaces();
                if (words.Length < 2) {
                    p.Message(Locale.Get("copyslot.must_provide_slot", p));
                    return;
                }
                int num = 0;
                if (!CommandParser.GetInt(p, words[1], "Slot number", ref num, 1, p.group.CopySlots)) return;

                ClearCopySlot(p, num);
            } else {
                int i = 0;
                if (!CommandParser.GetInt(p, message, "Slot number", ref i, 1, p.group.CopySlots)) return;
                
                SetCopySlot(p, i);
            }
        }
        
        static void OutputCopySlots(Player p) {
            List<CopyState> copySlots = p.CopySlots;
            int used = 0;
            
            for (int i = 0; i < copySlots.Count; i++)
            {
                if (copySlots[i] == null) continue;
                p.Message(Locale.Get("cmd.copyslot.msg1", p), i + 1, copySlots[i].Summary);
                used++;
            }
            
            p.Message(Locale.Get("copyslot.using_slots", p),
                      used, p.group.CopySlots, p.CurrentCopySlot + 1);
        }
        
        static void SetRandomCopySlot(Player p) {
            List<CopyState> copySlots = p.CopySlots;
            List<int> slots = new List<int>();
            
            for (int i = 0; i < copySlots.Count; i++)
            {
                if (copySlots[i] == null) continue;
                slots.Add(i);
            }
            
            if (slots.Count == 0) {
                p.Message(Locale.Get("copyslot.all_empty", p));
                return;
            }
            
            int idx = new Random().Next(slots.Count);
            SetCopySlot(p, slots[idx] + 1);
        }
        
        static void SetCopySlot(Player p, int i) {
            p.CurrentCopySlot = i - 1;
            if (p.CurrentCopy == null) {
                p.Message(Locale.Get("copyslot.selected_unused", p), i);
            } else {
                p.Message(Locale.Get("copyslot.selected", p), i, p.CurrentCopy.Summary);
            }
        }

        static void ClearCopySlot(Player p, int num) {
            int i = num - 1; //We know that i is 0 at min bc num passed to this is 1 at min
            List<CopyState> copySlots = p.CopySlots;
            if (i >= copySlots.Count || copySlots[i] == null) {
                p.Message(Locale.Get("copyslot.already_empty", p), num);
                return;
            }
            copySlots[i] = null;
            p.Message(Locale.Get("copyslot.cleared", p), num);
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("copyslot.help1", p));
            p.Message(Locale.Get("copyslot.help2", p));
            p.Message(Locale.Get("copyslot.help3", p));
            p.Message(Locale.Get("copyslot.help4", p));
            p.Message(Locale.Get("copyslot.help5", p));
            p.Message(Locale.Get("copyslot.help6", p));
            p.Message(Locale.Get("copyslot.help7", p));
            p.Message(Locale.Get("copyslot.help8", p));
            p.Message(Locale.Get("copyslot.help9", p));
        }
    }
}
