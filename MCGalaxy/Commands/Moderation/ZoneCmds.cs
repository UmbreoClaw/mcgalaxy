/*
    Copyright 2011 MCForge
        
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
using MCGalaxy.Commands.Building;
using MCGalaxy.Commands.CPE;
using MCGalaxy.Commands.World;
using MCGalaxy.Events.PlayerEvents;
using MCGalaxy.Maths;
using BlockID = System.UInt16;

namespace MCGalaxy.Commands.Moderation {
    public sealed class CmdZone : Command2 {
        public override string name { get { return "Zone"; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override bool museumUsable { get { return false; } }
        public override bool SuperUseable { get { return false; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }
        public override CommandAlias[] Aliases {
            get { return new[] { new CommandAlias("ZRemove", "del"), new CommandAlias("ZDelete", "del"),
                    new CommandAlias("ZAdd"), new CommandAlias("ZEdit", "perbuild") }; }
        }
        
        public override void Use(Player p, string message, CommandData data) {
            string[] args = message.SplitSpaces(4);
            if (message.Length == 0) { Help(p); return; }
            string opt = args[0];
            
            if (IsCreateAction(opt)) {
                if (args.Length == 1) { Help(p); return; }
                CreateZone(p, args, data, 1);
            } else if (IsDeleteAction(opt)) {
                if (args.Length == 1) { Help(p); return; }
                DeleteZone(p, args, data);
            } else if (opt.CaselessEq("perbuild") || opt.CaselessEq("set")) {
                if (args.Length <= 2) { Help(p); return; }
                Zone zone = Matcher.FindZones(p, p.level, args[1]);
                if (zone == null) return;
                
                if (!zone.Access.CheckDetailed(p, data.Rank)) {
                    p.Message(Locale.Get("cmd.zonecmds.msg1", p)); return;
                } else if (opt.CaselessEq("perbuild")) {
                    EditZone(p, args, data, zone);
                } else {
                    SetZoneProp(p, args, zone);
                }
            } else {
                CreateZone(p, args, data, 0);
            }
        }
        
        void CreateZone(Player p, string[] args, CommandData data, int offset) {
            if (p.level.FindZoneExact(args[offset]) != null) {
                p.Message(Locale.Get("cmd.zonecmds.msg2", p));
                return;
            }
            if (!LevelInfo.Check(p, data.Rank, p.level, "create zones in this level")) return;
            
            Zone z = new Zone();
            z.Access.Min = p.level.BuildAccess.Min;
            z.Access.Max = p.level.BuildAccess.Max;
            // TODO readd once performance issues with massive zone build blacklists are fixed
            //z.Access.CloneAccess(p.level.BuildAccess);
            
            z.Config.Name = args[offset];
            if (!PermissionCmd.Do(p, args, offset + 1, false, z.Access, data, p.level)) return;
            
            p.Message("Creating zone " + z.ColoredName);
            p.Message(Locale.Get("cmd.zonecmds.msg3", p));
            p.MakeSelection(2, "Selecting region for &SNew zone", z, AddZone);
        }
        
        bool AddZone(Player p, Vec3S32[] marks, object state, BlockID block) {
            Zone zone = (Zone)state;
            zone.MinX = (ushort)Math.Min(marks[0].X, marks[1].X);
            zone.MinY = (ushort)Math.Min(marks[0].Y, marks[1].Y);
            zone.MinZ = (ushort)Math.Min(marks[0].Z, marks[1].Z);
            zone.MaxX = (ushort)Math.Max(marks[0].X, marks[1].X);
            zone.MaxY = (ushort)Math.Max(marks[0].Y, marks[1].Y);
            zone.MaxZ = (ushort)Math.Max(marks[0].Z, marks[1].Z);

            zone.AddTo(p.level);
            p.level.Save(true);
            p.Message("Created zone " + zone.ColoredName);
            return false;
        }
        
        void DeleteZone(Player p, string[] args, CommandData data) {
            Level lvl = p.level;
            Zone zone = Matcher.FindZones(p, lvl, args[1]);
            if (zone == null) return;
            if (!zone.Access.CheckDetailed(p, data.Rank)) {
                p.Message(Locale.Get("cmd.zonecmds.msg4", p)); return;
            }
            
            zone.RemoveFrom(lvl);
            p.Message(Locale.Get("cmd.zonecmds.msg5", p), zone.ColoredName);
            lvl.Save(true);
        }
        
        void EditZone(Player p, string[] args, CommandData data, Zone zone) {
            PermissionCmd.Do(p, args, 2, false, zone.Access, data, p.level);
        }
        
        void SetZoneProp(Player p, string[] args, Zone zone) {
            ColorDesc desc = default(ColorDesc);
            if (args.Length < 4) { 
                p.Message(Locale.Get("cmd.zonecmds.msg6", p));
                return;
            }
            
            string opt = args[2], value = args[3];
            if (opt.CaselessEq("alpha")) {
                float alpha = 0;
                if (!CommandParser.GetReal(p, value, "Alpha", ref alpha, 0, 1)) return;
                
                zone.UnshowAll(p.level);
                zone.Config.ShowAlpha = (byte)(alpha * 255);
                zone.ShowAll(p.level);
            } else if (opt.CaselessEq("col") || opt.CaselessEq("color") || opt.CaselessEq("colour")) {
                if (!CommandParser.GetHex(p, value, ref desc)) return;
                
                zone.Config.ShowColor = value;
                zone.ShowAll(p.level);
            } else if (opt.CaselessEq("motd")) {
                zone.Config.MOTD = value;
                OnChangedZone(zone);
            } else if (CmdEnvironment.Handle(p, p.level, opt, value, zone.Config, "zone " + zone.ColoredName)) {
                OnChangedZone(zone);
            } else {
                Help(p, "properties"); return;
            }
            p.level.Save(true);
        }
        
        void OnChangedZone(Zone zone) {
            Player[] players = PlayerInfo.Online.Items;
            foreach (Player pl in players) {
                if (pl.ZoneIn == zone) OnChangedZoneEvent.Call(pl);
            }
        }
        
        public override void Help(Player p) {
            HelpName(p, name);
        }

        public override void Help(Player p, string message) {
            HelpName(p, name, message);
        }


        internal static void HelpName(Player p, string cmdName) {
            p.Message(Locale.Get("cmd.zonecmds.help1", p), cmdName);
            p.Message(Locale.Get("cmd.zonecmds.help2", p));
            p.Message(Locale.Get("cmd.zonecmds.help3", p), cmdName);
            p.Message(Locale.Get("cmd.zonecmds.help4", p));
            p.Message(Locale.Get("cmd.zonecmds.help5", p), cmdName);
            p.Message(Locale.Get("cmd.zonecmds.help6", p));
            p.Message(Locale.Get("cmd.zonecmds.help7", p));
            p.Message(Locale.Get("cmd.zonecmds.help8", p), cmdName);
            p.Message(Locale.Get("cmd.zonecmds.help9", p), cmdName);
        }
        internal static void HelpName(Player p, string cmdName, string message) {
            if (message.CaselessEq("properties")) {
                p.Message(Locale.Get("cmd.zonecmds.help10", p), cmdName);
                p.Message(Locale.Get("cmd.zonecmds.help11", p));
                p.Message(Locale.Get("cmd.zonecmds.help12", p));
                p.Message(Locale.Get("cmd.zonecmds.help13", p), cmdName);
                p.Message(Locale.Get("cmd.zonecmds.help14", p));
                p.Message(Locale.Get("cmd.zonecmds.help15", p), cmdName);
                p.Message(Locale.Get("cmd.zonecmds.help16", p));
                p.Message(Locale.Get("cmd.zonecmds.help17", p), cmdName);
                p.Message(Locale.Get("cmd.zonecmds.help18", p));
            } else {
                HelpName(p, cmdName);
            }
        }
    }
    
    public sealed class CmdZoneTest : Command2 {
        public override string name { get { return "ZoneTest"; } }
        public override string shortcut { get { return "ZTest"; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override bool SuperUseable { get { return false; } }
        
        public override void Use(Player p, string message, CommandData data) {
            p.Message(Locale.Get("cmd.zonecmds.msg7", p));
            p.MakeSelection(1, "Selecting point for &SZone check", data, TestZone);
        }
        
        bool TestZone(Player p, Vec3S32[] marks, object state, BlockID block) {
            Vec3S32 P = marks[0];
            Level lvl = p.level;
            bool found = false;
            CommandData data = (CommandData)state;

            Zone[] zones = lvl.Zones.Items;
            for (int i = 0; i < zones.Length; i++) {
                Zone z = zones[i];
                if (!z.Contains(P.X, P.Y, P.Z)) continue;
                found = true;
                
                AccessResult status = z.Access.Check(p.name, data.Rank);
                bool allowed = z.Access.CheckAllowed(p);
                p.Message(Locale.Get("cmd.zonecmds.msg8", p), z.ColoredName, allowed ? "&a" : "&c", status );
            }
            
            if (!found) { p.Message(Locale.Get("cmd.zonecmds.msg9", p)); }
            return true;
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("cmd.zonecmds.help19", p));
        }
    }
    
    public sealed class CmdZoneList : Command2 {
        public override string name { get { return "ZoneList"; } }
        public override string shortcut { get { return "Zones"; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override bool SuperUseable { get { return false; } }
        public override bool UseableWhenFrozen { get { return true; } }
        
        public override void Use(Player p, string message, CommandData data) {
            Zone[] zones = p.level.Zones.Items;
            Paginator.Output(p, zones, PrintZone, 
                             "ZoneList", "zones", message);
        }
        
        static void PrintZone(Player p, Zone zone) {
            p.Message(Locale.Get("cmd.zonecmds.msg10", p),
                      zone.ColoredName, 
                      zone.MinX, zone.MinY, zone.MinZ,
                      zone.MaxX, zone.MaxY, zone.MaxZ);
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("cmd.zonecmds.help20", p));
        }
    }
    
    public sealed class CmdZoneMark : Command2 {
        public override string name { get { return "ZoneMark"; } }
        public override string shortcut { get { return "ZMark"; } }
        public override string type { get { return CommandTypes.Building; } }
        public override bool SuperUseable { get { return false; } }
        public override CommandAlias[] Aliases {
            get { return new[] { new CommandAlias("zm") }; }
        }
        
        public override void Use(Player p, string message, CommandData data) {
            Zone z;

            if (message.Length == 0) {
                z = p.ZoneIn;
                if (z == null) { p.Message(Locale.Get("cmd.zonecmds.msg11", p)); return; }
            } else {
                z = Matcher.FindZones(p, p.level, message);
                if (z == null) return;
            }

            if (!CmdMark.DoMark(p, z.MinX, z.MinY, z.MinZ)) {
                p.Message(Locale.Get("cmd.zonecmds.msg12", p));
            } else {
                CmdMark.DoMark(p, z.MaxX, z.MaxY, z.MaxZ);
            }
        }
        
        public override void Help(Player p) {
            p.Message(Locale.Get("cmd.zonecmds.help21", p));
            p.Message(Locale.Get("cmd.zonecmds.help22", p));
            p.Message("&T/ZoneMark");
            p.Message(Locale.Get("cmd.zonecmds.help23", p));
        }
    }
}
