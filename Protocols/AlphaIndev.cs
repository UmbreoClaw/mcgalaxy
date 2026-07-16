//reference System.dll
// AlphaIndev protocol plugin for MCGalaxy
// Adds support for connecting with classic (pre-Netty) Minecraft Java editions:
//   * Indev     (protocol version 9)
//   * Alpha     (protocol version 2,  e.g. a1.1.x)
//   * Beta      (protocol version 14, e.g. b1.7.3)
//
// This is a hardened rewrite focused on robustness:
//   * Compiles against modern MCGalaxy (implements GetPositionPacket / MaxEntityID / etc.)
//   * Every serverbound packet a client can send is accounted for, so the TCP stream
//     never desyncs (desyncs previously showed up as random "unknown opcode" kicks and
//     client-side crashes while mining / placing / using the inventory).
//   * Defensive bounds checking everywhere - a malformed or oversized packet closes the
//     connection cleanly instead of throwing or corrupting server state.
//
// ============================ MCGALAXY VERSION TOGGLE ============================
// Older MCGalaxy releases broadcast entity positions via UpdatePlayerPositions(), while
// newer ones (that added GetPositionPacket / MaxEntityID) use a shared buffer instead.
// A single plugin can only match one of the two, so pick the one your server uses:
//
//   * Leave the line below UNCOMMENTED for older MCGalaxy (the common downloadable build).
//   * COMMENT it out for newer MCGalaxy (if you get errors mentioning GetPositionPacket
//     or MaxEntityID, that means your server is newer - comment the line out).
//
// Symptom of the wrong setting is a compile error about UpdatePlayerPositions() or
// GetPositionPacket() 'no suitable method found to override'. Flip this line to fix it.
#define LEGACY_ENTITY_API
// ================================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using MCGalaxy;
using MCGalaxy.Maths;
using MCGalaxy.Network;
using System.Runtime.InteropServices;
using System.Text;
using BlockID = System.UInt16;

namespace PluginAlphaIndev
{
    public sealed class AlphaIndevPlugin : Plugin
    {
        public override string name { get { return "AlphaIndev"; } }
        public override string MCGalaxy_Version { get { return "1.9.5.3"; } }
        public override string creator { get { return "MCGalaxy"; } }

        ProtocolConstructor oldCons;
        const byte OPCODE_HANDSHAKE = AlphaIndevProtocol.OPCODE_HANDSHAKE;

        public override void Load(bool startup) {
            oldCons = INetSocket.Protocols[OPCODE_HANDSHAKE];
            INetSocket.Protocols[OPCODE_HANDSHAKE] = ConstructAlphaIndev;
        }

        public override void Unload(bool shutdown) {
            // restore original protocol constructor (may be null)
            INetSocket.Protocols[OPCODE_HANDSHAKE] = oldCons;
        }

        static INetProtocol ConstructAlphaIndev(INetSocket socket) {
            return new AlphaIndevHandshake(socket);
        }
    }

    // ==================================================================================
    //  Handshake handling
    //  The very first packet(s) let us figure out whether the client is Indev, Alpha or
    //  Beta, before we hand control off to the appropriate protocol implementation.
    // ==================================================================================
    unsafe class AlphaIndevHandshake : INetProtocol
    {
        const byte OPCODE_HANDSHAKE = AlphaIndevProtocol.OPCODE_HANDSHAKE;
        const byte OPCODE_LOGIN     = AlphaIndevProtocol.OPCODE_LOGIN;

        AlphaIndevParser parser;
        INetSocket socket;
        string player;

        public AlphaIndevHandshake(INetSocket s) { socket = s; }
        public void Disconnect() { }

        public int ProcessReceived(byte[] buffer, int length) {
            try {
                return ProcessPacket(buffer, length);
            } catch (Exception ex) {
                Logger.Log(LogType.Warning, "Error handling I/A/B handshake");
                Logger.LogError(ex);
                socket.Close();
                return length;
            }
        }

        int ProcessPacket(byte[] buffer, int length) {
            if (length < 1) return 0; // need at least the opcode

            switch (buffer[0]) {
                case OPCODE_LOGIN:     return HandleLogin(buffer, length);
                case OPCODE_HANDSHAKE: return HandleHandshake(buffer, length);

                default:
                    Logger.Log(LogType.UserActivity,
                               "Disconnected I/A/B connection (unexpected opcode {0} during handshake)", buffer[0]);
                    socket.Close();
                    return length;
            }
        }

        static readonly byte[] handshake_fields = { AlphaIndevParser.FIELD_BYTE, AlphaIndevParser.FIELD_STRING };
        int HandleHandshake(byte[] buffer, int length) {
            // handshake packet: u8 opcode, u16 str_len, u8* str_contents
            // utf8 or unicode is used for strings depending on protocol
            //   e.g. utf8: 0x02 0x00 0x01 'A'
            //   e.g. uni:  0x02 0x00 0x01 0x00 'A'
            // Need opcode + at least the first byte of the string contents to detect encoding
            if (length < 4) return 0;

            parser      = new AlphaIndevParser();
            parser.utf8 = buffer[3] != 0;

            FieldValue* values = stackalloc FieldValue[AlphaIndevParser.MAX_FIELDS];
            int size = parser.ParsePacket(buffer, 0, length, handshake_fields, values);
            if (size == AlphaIndevParser.INCOMPLETE) return 0;
            if (size == AlphaIndevParser.INVALID) { socket.Close(); return length; }

            player = parser.ReadString(buffer, 1);
            if (player == null) { socket.Close(); return length; }
            Logger.Log(LogType.UserActivity, "I/A/B connecting player: " + player);

            // '-' tells the client that no name verification is required
            SendHandshake("-");
            return size;
        }

        void SendHandshake(string serverID) {
            byte[] data = new byte[1 + 2 + parser.CalcStringLength(serverID)];
            data[0] = OPCODE_HANDSHAKE;
            parser.WriteString(data, 1, serverID);
            socket.Send(data, SendFlags.None);
        }

        static readonly byte[] login_fields = { AlphaIndevParser.FIELD_BYTE, AlphaIndevParser.FIELD_INT };
        int HandleLogin(byte[] buffer, int length) {
            if (parser == null) {
                // client somehow sent login before handshake - can't know the string encoding
                Logger.Log(LogType.UserActivity, "Disconnected I/A/B connection (login before handshake)");
                socket.Close();
                return length;
            }

            FieldValue* values = stackalloc FieldValue[AlphaIndevParser.MAX_FIELDS];
            int size = parser.ParsePacket(buffer, 0, length, login_fields, values);
            if (size == AlphaIndevParser.INCOMPLETE) return 0;
            if (size == AlphaIndevParser.INVALID) { socket.Close(); return length; }

            int version = values[1].I32;
            if (!parser.utf8 && version == IndevProtocol.PROTOCOL_VERSION) {
                socket.protocol = new IndevProtocol(socket, player, parser.utf8);
            } else {
                socket.protocol = new AlphaProtocol(socket, player, parser.utf8);
            }

            // re-process the login packet with the now-selected protocol
            return socket.protocol.ProcessReceived(buffer, length);
        }
    }

    // Union used to return parsed field values without boxing/allocation
    [StructLayout(LayoutKind.Explicit)]
    struct FieldValue
    {
        [FieldOffset(0)] public byte U8;
        [FieldOffset(0)] public ushort U16;
        [FieldOffset(0)] public int I32;
        [FieldOffset(0)] public long I64;
        [FieldOffset(0)] public float F32;
        [FieldOffset(0)] public double F64;
    }

    // ==================================================================================
    //  Generic big-endian field parser/serializer shared by all three protocols.
    // ==================================================================================
    unsafe class AlphaIndevParser
    {
        public bool utf8;
        public Encoding CurEncoding { get { return utf8 ? Encoding.UTF8 : Encoding.BigEndianUnicode; } }

        // Largest number of fields any single packet definition uses (stackalloc sizing)
        public const int MAX_FIELDS = 24;
        // ParsePacket sentinel return values
        public const int INCOMPLETE = -1; // not enough bytes yet, wait for more
        public const int INVALID    = -2; // packet is malformed / impossibly large
        // Sanity limit on string lengths (in bytes). Real packets never come close.
        const int MAX_STRING_BYTES = 32767;

        // NetUtils (rather than MemUtils) so this compiles against both old and new MCGalaxy
        public static ushort ReadU16(byte[] array, int index) { return NetUtils.ReadU16(array, index); }
        public static int    ReadI32(byte[] array, int index) { return NetUtils.ReadI32(array, index); }

        public static float ReadF32(byte[] array, int offset) {
            int value = ReadI32(array, offset);
            return *(float*)&value;
        }

        public static double ReadF64(byte[] array, int offset) {
            long hi = ReadI32(array, offset + 0) & 0xFFFFFFFFL;
            long lo = ReadI32(array, offset + 4) & 0xFFFFFFFFL;

            long value = (hi << 32) | lo;
            return *(double*)&value;
        }

        // Number of raw bytes a string field occupies (excluding the 2 byte length prefix)
        public int ReadStringLength(byte[] buffer, int offset) {
            int len = ReadU16(buffer, offset);
            // Just to confuse you, 'len' isn't always a byte count
            //   utf8 = number of bytes
            //   uni  = number of characters
            return utf8 ? len : (len * 2);
        }

        // Reads a string, returning null if the declared length would read out of bounds
        public string ReadString(byte[] buffer, int offset) {
            if (offset < 0 || offset + 2 > buffer.Length) return null;
            int len = ReadStringLength(buffer, offset);
            if (len < 0 || offset + 2 + len > buffer.Length) return null;
            try {
                return CurEncoding.GetString(buffer, offset + 2, len);
            } catch {
                return null;
            }
        }

        static void WriteU16(ushort value, byte[] array, int index) { NetUtils.WriteU16(value, array, index); }

        public int CalcStringLength(string value) { return CurEncoding.GetByteCount(value); }
        public void WriteString(byte[] buffer, int offset, string value) {
            int len = CurEncoding.GetBytes(value, 0, value.Length, buffer, offset + 2);

            // Just to confuse you, 'len' isn't always a byte count
            //   utf8 = number of bytes
            //   uni  = number of characters
            if (!utf8) len >>= 1;
            WriteU16((ushort)len, buffer, offset);
        }


        public const byte FIELD_BYTE   = 0;
        public const byte FIELD_SHORT  = 1;
        public const byte FIELD_INT    = 2;
        public const byte FIELD_FLOAT  = 3;
        public const byte FIELD_DOUBLE = 4;
        public const byte FIELD_LONG   = 5;
        public const byte FIELD_STRING = 6;

        static bool CheckFieldSize(int amount, ref int offset, ref int left) {
            if (left < amount) return false;

            offset += amount;
            left   -= amount;
            return true;
        }

        // Parses the fixed set of 'fields' starting at 'offset'.
        //   returns number of bytes the packet occupies on success
        //   returns INCOMPLETE if there aren't enough bytes yet
        //   returns INVALID if the packet is malformed (e.g. an absurd string length)
        public int ParsePacket(byte[] buffer, int offset, int left,
                               byte[] fields, FieldValue* values) {
            int total = left;

            foreach (byte field in fields)
            {
                switch (field) {
                    case FIELD_BYTE:
                        if (!CheckFieldSize(1, ref offset, ref left)) return INCOMPLETE;
                        values->U8 = buffer[offset - 1];
                        values++;
                        break;

                    case FIELD_SHORT:
                        if (!CheckFieldSize(2, ref offset, ref left)) return INCOMPLETE;
                        values->U16 = ReadU16(buffer, offset - 2);
                        values++;
                        break;

                    case FIELD_INT:
                        if (!CheckFieldSize(4, ref offset, ref left)) return INCOMPLETE;
                        values->I32 = ReadI32(buffer, offset - 4);
                        values++;
                        break;

                    case FIELD_FLOAT:
                        if (!CheckFieldSize(4, ref offset, ref left)) return INCOMPLETE;
                        values->F32 = ReadF32(buffer, offset - 4);
                        values++;
                        break;

                    case FIELD_DOUBLE:
                        if (!CheckFieldSize(8, ref offset, ref left)) return INCOMPLETE;
                        values->F64 = ReadF64(buffer, offset - 8);
                        values++;
                        break;

                    case FIELD_LONG:
                        if (!CheckFieldSize(8, ref offset, ref left)) return INCOMPLETE;
                        values->I64 = ((long)ReadI32(buffer, offset - 8) << 32)
                                    | (ReadI32(buffer, offset - 4) & 0xFFFFFFFFL);
                        values++;
                        break;

                    case FIELD_STRING:
                        if (!CheckFieldSize(2, ref offset, ref left)) return INCOMPLETE;
                        int strLen = ReadStringLength(buffer, offset - 2);
                        if (strLen < 0 || strLen > MAX_STRING_BYTES) return INVALID;

                        // store the offset of the length prefix so the caller can ReadString later
                        values->I32 = offset - 2;
                        values++;
                        if (!CheckFieldSize(strLen, ref offset, ref left)) return INCOMPLETE;
                        break;

                    default:
                        return INVALID;
                }
            }
            return total - left;
        }
    }

    // ==================================================================================
    //  Shared base for all three protocol implementations.
    // ==================================================================================
    unsafe abstract class AlphaIndevProtocol : IGameSession
    {
        protected AlphaIndevParser parser;

        public AlphaIndevProtocol(INetSocket s, string name, bool utf8) {
            socket = s;
            player = new Player(s, this);
            parser = new AlphaIndevParser();
            parser.utf8 = utf8;

            // TEMP HACK - name is properly set again during login
            player.name = name; player.truename = name;
        }

        // ---- Opcodes -------------------------------------------------------------
        public const byte OPCODE_PING           = 0x00;
        public const byte OPCODE_LOGIN          = 0x01;
        public const byte OPCODE_HANDSHAKE      = 0x02;
        public const byte OPCODE_CHAT           = 0x03;
        public const byte OPCODE_SPAWN_POSITION = 0x06;
        public const byte OPCODE_USE_ENTITY     = 0x07;
        public const byte OPCODE_RESPAWN        = 0x09;
        public const byte OPCODE_SELF_STATEONLY = 0x0A;
        public const byte OPCODE_SELF_MOVE      = 0x0B;
        public const byte OPCODE_SELF_LOOK      = 0x0C;
        public const byte OPCODE_SELF_MOVE_LOOK = 0x0D;
        public const byte OPCODE_BLOCK_DIG      = 0x0E;
        public const byte OPCODE_BLOCK_PLACE    = 0x0F;
        public const byte OPCODE_SLOT_SWITCHED  = 0x10;
        public const byte OPCODE_ARM_ANIM       = 0x12;
        public const byte OPCODE_ENTITY_ACTION  = 0x13;
        public const byte OPCODE_NAMED_ADD      = 0x14;
        public const byte OPCODE_REMOVE_ENTITY  = 0x1D;
        public const byte OPCODE_REL_MOVE       = 0x1F;
        public const byte OPCODE_LOOK           = 0x20;
        public const byte OPCODE_REL_MOVE_LOOK  = 0x21;
        public const byte OPCODE_TELEPORT       = 0x22;
        public const byte OPCODE_PRE_CHUNK      = 0x32;
        public const byte OPCODE_CHUNK          = 0x33;
        public const byte OPCODE_BLOCK_CHANGE   = 0x35;
        public const byte OPCODE_CLOSE_WINDOW   = 0x65;
        public const byte OPCODE_WINDOW_CLICK   = 0x66;
        public const byte OPCODE_TRANSACTION    = 0x6A;
        public const byte OPCODE_UPDATE_SIGN    = 0x82;
        public const byte OPCODE_KICK           = 0xFF;

        public const byte FIELD_BYTE   = AlphaIndevParser.FIELD_BYTE;
        public const byte FIELD_SHORT  = AlphaIndevParser.FIELD_SHORT;
        public const byte FIELD_INT    = AlphaIndevParser.FIELD_INT;
        public const byte FIELD_FLOAT  = AlphaIndevParser.FIELD_FLOAT;
        public const byte FIELD_DOUBLE = AlphaIndevParser.FIELD_DOUBLE;
        public const byte FIELD_LONG   = AlphaIndevParser.FIELD_LONG;
        public const byte FIELD_STRING = AlphaIndevParser.FIELD_STRING;

        // ---- CPE / capabilities: none of these old clients support any of it -----
#if !LEGACY_ENTITY_API
        public override int MaxEntityID { get { return 127; } } // keeps the position buffer within bounds
#endif
        public override bool Supports(string extName, int version) { return false; }

        public override void SendAddTabEntry(byte id, string name, string nick, string group, byte groupRank) { }
        public override void SendRemoveTabEntry(byte id) { }
        public override bool SendSetUserType(byte type) { return false; }
        public override bool SendSetReach(float reach) { return false; }
        public override bool SendHoldThis(BlockID block, bool locked) { return false; }
        public override bool SendSetEnvColor(byte type, string hex) { return false; }
        public override void SendChangeModel(byte id, string model) { }
#if !LEGACY_ENTITY_API
        public override void SendEntityProperty(byte id, EntityProp prop, int value) { }
#endif
        public override bool SendSetWeather(byte weather) { return false; }
        public override bool SendSetTextColor(ColorDesc color) { return false; }
        public override bool SendDefineBlock(BlockDefinition def) { return false; }
        public override bool SendUndefineBlock(BlockDefinition def) { return false; }
        public override bool SendAddSelection(byte id, string label, Vec3U16 p1, Vec3U16 p2, ColorDesc color) { return false; }
        public override bool SendRemoveSelection(byte id) { return false; }
        public override bool SendCinematicGui(CinematicGui gui) { return false; }
#if !LEGACY_ENTITY_API
        public override bool SendToggleBlockList(bool toggle) { return false; }
#endif

        // ---- Little helpers ------------------------------------------------------
        protected static void WriteU16(ushort value, byte[] array, int index) { NetUtils.WriteU16(value, array, index); }
        protected static void WriteI32(int value, byte[] array, int index)     { NetUtils.WriteI32(value, array, index); }

        protected static void WriteF32(float value, byte[] buffer, int offset) {
            int num = *(int*)&value;
            WriteI32(num, buffer, offset);
        }

        protected static void WriteF64(double value, byte[] buffer, int offset) {
            long num = *(long*)&value;
            WriteI32((int)(num >> 32), buffer, offset + 0);
            WriteI32((int)(num >>  0), buffer, offset + 4);
        }

        protected abstract int CalcStringLength(string value);
        protected abstract void WriteString(byte[] buffer, int offset, string value);

        // vertical offset applied to OTHER entities so they line up with our (possibly shifted) world
        protected virtual int EntityYOffset { get { return 0; } }
        // yaw origin differs between MCGalaxy and old MC (128 units == 180 degrees)
        protected static byte PackYaw(byte rotY) { return (byte)(rotY + 128); }
        // old MC sends look angles as degrees; MCGalaxy uses a 0-255 byte
        protected static byte DegToByte(float degrees) {
            return (byte)(int)Math.Round(degrees / 360.0f * 256.0f);
        }

        protected static string CleanupColors(string value) {
            return LineWrapper.CleanupColors(value, false, false);
        }

        // Old vanilla clients use the section sign (U+00A7) as an inline colour code and
        // choke on stray ampersands, so translate MCGalaxy '&' colour codes.
        protected static string ColorEscape(string value) {
            return value.Replace('&', '§');
        }

        // Clamp + forward a block change, ignoring anything outside the world bounds.
        protected void TryBlockChange(int x, int y, int z, byte action, BlockID block) {
            if (x < 0 || y < 0 || z < 0) return;
            if (x > ushort.MaxValue || y > ushort.MaxValue || z > ushort.MaxValue) return;
            player.ProcessBlockchange((ushort)x, (ushort)y, (ushort)z, action, block);
        }

        // ---- Outbound packet plumbing ----------------------------------------------
        // MCGalaxy's TcpSocket copies every Send() into a fixed 4096 byte buffer, so any
        // single Send larger than that throws and kills the connection. This was the cause
        // of "large map loads crash the client": full chunk columns of varied terrain can
        // compress to more than 4096 bytes. Every packet we send must stay under budget.
        protected const int MAX_PACKET_SIZE = 4000;
        protected readonly object sendLock = new object();

        // All sends from this session go through this helper. The lock keeps multi-part
        // payloads (see SendLarge) from being interleaved with other packets.
        protected void SendPacket(byte[] data) {
            lock (sendLock) socket.Send(data, SendFlags.None);
        }

        // Sends a logical packet that may exceed the socket's send buffer size by slicing
        // it into multiple raw sends. TCP is a byte stream, so this is transparent to the
        // client - PROVIDED nothing else writes to the socket in between, hence the lock.
        protected void SendLarge(byte[] data) {
            if (data.Length <= MAX_PACKET_SIZE) { SendPacket(data); return; }

            lock (sendLock) {
                for (int offset = 0; offset < data.Length; offset += MAX_PACKET_SIZE)
                {
                    int len = Math.Min(MAX_PACKET_SIZE, data.Length - offset);
                    byte[] part = new byte[len];
                    Buffer.BlockCopy(data, offset, part, 0, len);
                    socket.Send(part, SendFlags.None);
                }
            }
        }

#region Packet senders
        public override void SendPing() {
            SendPacket(new byte[] { OPCODE_PING });
        }

        public override void SendSetSpawnpoint(Position pos, Orientation rot) {
            byte[] spawn = new byte[1 + 4 + 4 + 4];
            spawn[0] = OPCODE_SPAWN_POSITION;
            WriteI32(pos.BlockX, spawn, 1);
            WriteI32(pos.BlockY, spawn, 5);
            WriteI32(pos.BlockZ, spawn, 9);
            SendPacket(spawn);
        }

        public override void SendRemoveEntity(byte id) {
            byte[] data = new byte[1 + 4];
            data[0] = OPCODE_REMOVE_ENTITY;
            WriteI32(id, data, 1);
            SendPacket(data);
        }

        public override void SendChat(string message) {
            int bufferLen;
            char[] buffer = LineWrapper.CleanupColors(message, out bufferLen, false, false);

            List<string> lines = LineWrapper.Wordwrap(buffer, bufferLen, true);
            for (int i = 0; i < lines.Count; i++)
            {
                SendPacket(MakeChat(ColorEscape(lines[i])));
            }
        }

        public override void SendMessage(CpeMessageType type, string message) {
            if (type != CpeMessageType.Normal) return;
            message = ColorEscape(CleanupColors(message));
            SendPacket(MakeChat(message));
        }

        public override void SendKick(string reason, bool sync) {
            reason = ColorEscape(CleanupColors(reason));
            byte[] data = MakeKick(reason);
            lock (sendLock) socket.Send(data, sync ? SendFlags.Synchronous : SendFlags.None);
        }

        protected void SendHandshake(string serverID) {
            SendPacket(MakeHandshake(serverID));
        }

        public override void SendBlockchange(ushort x, ushort y, ushort z, BlockID block) {
            byte[] packet = new byte[1 + 4 + 1 + 4 + 1 + 1];
            byte raw = (byte)ConvertBlock(block);
            WriteBlockChange(packet, 0, raw, x, y, z);
            SendPacket(packet);
        }

        // Gives subclasses a chance to send the terrain around a position before the
        // client is moved there (otherwise it falls through ungenerated world)
        protected virtual void EnsureChunksAt(Position pos) { }

        public override void SendTeleport(byte id, Position pos, Orientation rot) {
            if (id == Entities.SelfID) {
                EnsureChunksAt(pos);
                SendPacket(MakeSelfMoveLook(pos, rot));
            } else {
                SendPacket(MakeEntityTeleport(id, pos, rot));
            }
        }

        bool sentMOTD;
        public override void SendMotd(string motd) {
            if (sentMOTD) return; // TODO work out how to properly resend the map
            sentMOTD = true;
            SendPacket(MakeLogin(motd));
        }

        public override void SendSpawnEntity(byte id, string name, string skin, Position pos, Orientation rot) {
            if (id == Entities.SelfID) {
                EnsureChunksAt(pos);
                SendPacket(MakeSelfMoveLook(pos, rot));
            } else {
                name = ColorEscape(CleanupColors(name));
                SendPacket(MakeNamedAdd(id, name, skin, pos, rot));
            }
        }

#if LEGACY_ENTITY_API
        // Position broadcasting on older MCGalaxy: one teleport packet per moved entity.
        public override void UpdatePlayerPositions() {
            Player[] players = PlayerInfo.Online.Items;
            Player dst = player;

            foreach (Player p in players)
            {
                if (dst == p || dst.level != p.level || !dst.CanSeeEntity(p)) continue;

                Orientation rot = p.Rot;
                Position pos    = p._tempPos;
                Position delta  = new Position(pos.X - p._lastPos.X, pos.Y - p._lastPos.Y, pos.Z - p._lastPos.Z);
                bool posChanged = delta.X  != 0 || delta.Y != 0 || delta.Z != 0;
                bool oriChanged = rot.RotY != p._lastRot.RotY   || rot.HeadX != p._lastRot.HeadX;
                if (posChanged || oriChanged)
                    SendTeleport(p.id, pos, rot);
            }
        }
#else
        // Position broadcasting on newer MCGalaxy: write directly into the shared entity
        // update buffer. That buffer only guarantees 16 bytes/entity and an absolute
        // teleport is 19 bytes, so MaxEntityID is capped low enough this can't overflow.
        public override unsafe void GetPositionPacket(ref byte* ptr, byte id, bool srcExtPos, bool extPos,
                                                      Position pos, Position oldPos, Orientation rot, Orientation oldRot) {
            Position delta = new Position(pos.X - oldPos.X, pos.Y - oldPos.Y, pos.Z - oldPos.Z);
            bool posChanged = delta.X != 0 || delta.Y != 0 || delta.Z != 0;
            bool oriChanged = rot.RotY != oldRot.RotY || rot.HeadX != oldRot.HeadX;
            bool absUpdate  = Math.Abs(delta.X) > 32 || Math.Abs(delta.Y) > 32 || Math.Abs(delta.Z) > 32;

            if (absUpdate) {
                int y = pos.Y + EntityYOffset;
                *ptr++ = OPCODE_TELEPORT;
                WriteI32P(ref ptr, id);
                WriteI32P(ref ptr, pos.X);
                WriteI32P(ref ptr, y);
                WriteI32P(ref ptr, pos.Z);
                *ptr++ = PackYaw(rot.RotY);
                *ptr++ = rot.HeadX;
            } else if (posChanged && oriChanged) {
                *ptr++ = OPCODE_REL_MOVE_LOOK;
                WriteI32P(ref ptr, id);
                *ptr++ = (byte)delta.X; *ptr++ = (byte)delta.Y; *ptr++ = (byte)delta.Z;
                *ptr++ = PackYaw(rot.RotY);
                *ptr++ = rot.HeadX;
            } else if (posChanged) {
                *ptr++ = OPCODE_REL_MOVE;
                WriteI32P(ref ptr, id);
                *ptr++ = (byte)delta.X; *ptr++ = (byte)delta.Y; *ptr++ = (byte)delta.Z;
            } else if (oriChanged) {
                *ptr++ = OPCODE_LOOK;
                WriteI32P(ref ptr, id);
                *ptr++ = PackYaw(rot.RotY);
                *ptr++ = rot.HeadX;
            }
        }

        static unsafe void WriteI32P(ref byte* ptr, int value) {
            *ptr++ = (byte)(value >> 24); *ptr++ = (byte)(value >> 16);
            *ptr++ = (byte)(value >> 8);  *ptr++ = (byte)value;
        }
#endif
#endregion


#region Packet builders
        byte[] MakeHandshake(string serverID) {
            byte[] data = new byte[1 + 2 + CalcStringLength(serverID)];
            data[0] = OPCODE_HANDSHAKE;
            WriteString(data, 1, serverID);
            return data;
        }

        byte[] MakeChat(string text) {
            byte[] data = new byte[1 + 2 + CalcStringLength(text)];
            data[0] = OPCODE_CHAT;
            WriteString(data, 1, text);
            return data;
        }

        byte[] MakeKick(string reason) {
            byte[] data = new byte[1 + 2 + CalcStringLength(reason)];
            data[0] = OPCODE_KICK;
            WriteString(data, 1, reason);
            return data;
        }

        protected abstract byte[] MakeLogin(string motd);
        protected abstract byte[] MakeSelfMoveLook(Position pos, Orientation rot);

        protected virtual byte[] MakeNamedAdd(byte id, string name, string skin, Position pos, Orientation rot) {
            int nameLen = CalcStringLength(name);
            byte[] data = new byte[1 + 4 + (2 + nameLen) + (4 + 4 + 4) + (1 + 1) + 2];
            int y = pos.Y + EntityYOffset;

            data[0] = OPCODE_NAMED_ADD;
            WriteI32(id, data, 1);
            WriteString(data, 5, name);

            WriteI32(pos.X, data,  7 + nameLen);
            WriteI32(y,     data, 11 + nameLen);
            WriteI32(pos.Z, data, 15 + nameLen);

            data[19 + nameLen] = PackYaw(rot.RotY);
            data[20 + nameLen] = rot.HeadX;
            WriteU16(0, data, 21 + nameLen); // currently held item
            return data;
        }

        protected virtual byte[] MakeEntityTeleport(byte id, Position pos, Orientation rot) {
            byte[] data = new byte[1 + 4 + (4 + 4 + 4) + (1 + 1)];
            int y = pos.Y + EntityYOffset;

            data[0] = OPCODE_TELEPORT;
            WriteI32(id,    data, 1);
            WriteI32(pos.X, data, 5);
            WriteI32(y,     data, 9);
            WriteI32(pos.Z, data, 13);

            data[17] = PackYaw(rot.RotY);
            data[18] = rot.HeadX;
            return data;
        }
#endregion


#region Shared packet handlers
        static readonly byte[] chat_fields = { FIELD_BYTE, FIELD_STRING };
        protected int HandleChat(byte[] buffer, int offset, int left) {
            FieldValue* values = stackalloc FieldValue[AlphaIndevParser.MAX_FIELDS];
            int size = parser.ParsePacket(buffer, offset, left, chat_fields, values);
            if (size < 0) return NeedMore(size, left);

            string text = parser.ReadString(buffer, values[1].I32);
            if (text != null) player.ProcessChat(text, false);
            return size;
        }

        static readonly byte[] state_fields = { FIELD_BYTE, FIELD_BYTE };
        protected int HandleSelfStateOnly(byte[] buffer, int offset, int left) {
            // NOTE: stackalloc must live in the frame that uses it, so it can't be hoisted
            // into a shared helper (that would return a dangling pointer).
            FieldValue* values = stackalloc FieldValue[AlphaIndevParser.MAX_FIELDS];
            int size = parser.ParsePacket(buffer, offset, left, state_fields, values);
            if (size < 0) return NeedMore(size, left);
            // bool onGround - nothing to do
            return size;
        }

        // Consumes a packet with the given fixed field layout but otherwise ignores it. Used
        // for packets we don't act on but MUST size correctly to keep the stream in sync.
        protected int HandleIgnored(byte[] buffer, int offset, int left, byte[] fields) {
            FieldValue* values = stackalloc FieldValue[AlphaIndevParser.MAX_FIELDS];
            int size = parser.ParsePacket(buffer, offset, left, fields, values);
            if (size < 0) return NeedMore(size, left);
            return size;
        }

        // Translates a parser sentinel into the value ProcessReceived expects:
        //   INCOMPLETE -> 0    (wait for more data)
        //   INVALID    -> kick and consume the rest of the buffer
        protected int NeedMore(int parseResult, int left) {
            if (parseResult == AlphaIndevParser.INVALID) {
                player.Leave("Malformed packet received", true);
                return left; // consume everything so ProcessReceived stops looping
            }
            return 0;
        }

        protected int HandleDisconnect(int left) {
            player.Leave("Disconnected", true);
            return left;
        }

        protected int UnknownOpcode(byte opcode, int left) {
            player.Leave("Unhandled opcode \"" + opcode + "\"!", true);
            return left; // consume rest of buffer; connection is closing
        }
#endregion


        public override byte[] MakeBulkBlockchange(BufferedBlockSender buffer) {
            const int size = 1 + 4 + 1 + 4 + 1 + 1;
            byte[] data = new byte[size * buffer.count];
            Level level = buffer.level;

            for (int i = 0; i < buffer.count; i++)
            {
                int index = buffer.indices[i];
                int x = (index % level.Width);
                int y = (index / level.Width) / level.Length;
                int z = (index / level.Width) % level.Length;

                // blocks[] holds MCGalaxy internal ids - must convert them for the client,
                // otherwise mass edits/physics send raw ids these clients render as garbage
                WriteBlockChange(data, i * size, (byte)ConvertBlock(buffer.blocks[i]), x, y, z);
            }
            return data;
        }

        protected virtual void WriteBlockChange(byte[] data, int offset, byte block, int x, int y, int z) {
            data[offset + 0] = OPCODE_BLOCK_CHANGE;
            WriteI32(x, data, offset + 1);
            data[offset + 5] = (byte)y;
            WriteI32(z, data, offset + 6);
            data[offset + 10] = block;
            data[offset + 11] = 0; // metadata
        }
    }

    // ==================================================================================
    //  Alpha + Beta protocol (they share almost everything; encoding & a few packets differ)
    // ==================================================================================
    unsafe class AlphaProtocol : AlphaIndevProtocol
    {
        const int ALPHA_PROTOCOL_VERSION = 2;
        const int BETA_PROTOCOL_VERSION  = 14;

        // Alpha and Beta both encode strings as UCS-2, so the string encoding can NOT be used
        // to tell them apart. The protocol version from the login packet is the reliable signal.
        bool isBeta;
        public bool IsBeta { get { return isBeta; } }

        public AlphaProtocol(INetSocket s, string name, bool utf8) : base(s, name, utf8) { }

        protected override int HandlePacket(byte[] buffer, int offset, int left) {
            switch (buffer[offset]) {
                case OPCODE_PING:           return 1;
                case OPCODE_LOGIN:          return HandleLogin(buffer, offset, left);
                case OPCODE_CHAT:           return HandleChat(buffer, offset, left);
                case OPCODE_USE_ENTITY:     return HandleIgnored(buffer, offset, left, useentity_fields);
                case OPCODE_RESPAWN:        return isBeta ? HandleRespawn(buffer, offset, left)
                                                          : HandleIgnored(buffer, offset, left, alpha_respawn_fields);
                case OPCODE_SELF_STATEONLY: return HandleSelfStateOnly(buffer, offset, left);
                case OPCODE_SELF_MOVE:      return HandleSelfMove(buffer, offset, left);
                case OPCODE_SELF_LOOK:      return HandleSelfLook(buffer, offset, left);
                case OPCODE_SELF_MOVE_LOOK: return HandleSelfMoveLook(buffer, offset, left);
                case OPCODE_BLOCK_DIG:      return HandleBlockDig(buffer, offset, left);
                case OPCODE_BLOCK_PLACE:    return HandleBlockPlace(buffer, offset, left);
                case OPCODE_SLOT_SWITCHED:  return isBeta ? HandleSlotSwitch(buffer, offset, left)
                                                          : HandleIgnored(buffer, offset, left, alpha_slot_fields);
                case OPCODE_ARM_ANIM:       return HandleIgnored(buffer, offset, left, anim_fields);
                case OPCODE_ENTITY_ACTION:  return HandleIgnored(buffer, offset, left, entityaction_fields);
                case OPCODE_CLOSE_WINDOW:   return HandleIgnored(buffer, offset, left, closewindow_fields);
                case OPCODE_WINDOW_CLICK:   return HandleWindowClick(buffer, offset, left);
                case OPCODE_TRANSACTION:    return HandleIgnored(buffer, offset, left, transaction_fields);
                case OPCODE_UPDATE_SIGN:    return HandleUpdateSign(buffer, offset, left);
                case OPCODE_KICK:           return HandleDisconnect(left);

                default:
                    return UnknownOpcode(buffer[offset], left);
            }
        }

        protected override int CalcStringLength(string value) { return parser.CalcStringLength(value); }
        protected override void WriteString(byte[] buffer, int offset, string value) { parser.WriteString(buffer, offset, value); }


#region Serverbound field layouts
        static readonly byte[] useentity_fields    = { FIELD_BYTE, FIELD_INT, FIELD_INT, FIELD_BYTE };
        // Beta respawn carries a dimension byte; Alpha (pre-Nether) sends a bare packet
        static readonly byte[] beta_respawn_fields  = { FIELD_BYTE, FIELD_BYTE };
        static readonly byte[] alpha_respawn_fields = { FIELD_BYTE };
        // Beta holding-change is a hotbar slot index; Alpha sent an (unused int + held block id)
        static readonly byte[] beta_slot_fields     = { FIELD_BYTE, FIELD_SHORT };
        static readonly byte[] alpha_slot_fields    = { FIELD_BYTE, FIELD_INT, FIELD_SHORT };
        static readonly byte[] anim_fields          = { FIELD_BYTE, FIELD_INT, FIELD_BYTE };
        static readonly byte[] entityaction_fields  = { FIELD_BYTE, FIELD_INT, FIELD_BYTE };
        static readonly byte[] closewindow_fields   = { FIELD_BYTE, FIELD_BYTE };
        static readonly byte[] transaction_fields   = { FIELD_BYTE, FIELD_BYTE, FIELD_SHORT, FIELD_BYTE };
        static readonly byte[] dig_fields           = { FIELD_BYTE, FIELD_BYTE, FIELD_INT, FIELD_BYTE, FIELD_INT, FIELD_BYTE };

        // Alpha login: byte op, int version, string username, string password
        static readonly byte[] alpha_login_fields   = { FIELD_BYTE, FIELD_INT, FIELD_STRING, FIELD_STRING };
        // Beta  login: byte op, int version, string username, long mapSeed, byte dimension
        static readonly byte[] beta_login_fields    = { FIELD_BYTE, FIELD_INT, FIELD_STRING, FIELD_LONG, FIELD_BYTE };

        // Alpha/Beta position updates use doubles
        static readonly byte[] move_fields          = { FIELD_BYTE, FIELD_DOUBLE, FIELD_DOUBLE, FIELD_DOUBLE, FIELD_DOUBLE, FIELD_BYTE };
        static readonly byte[] look_fields          = { FIELD_BYTE, FIELD_FLOAT, FIELD_FLOAT, FIELD_BYTE };
        static readonly byte[] movelook_fields      = { FIELD_BYTE, FIELD_DOUBLE, FIELD_DOUBLE, FIELD_DOUBLE, FIELD_DOUBLE, FIELD_FLOAT, FIELD_FLOAT, FIELD_BYTE };

        // Alpha block place: byte op, short blockId, int X, byte Y, int Z, byte direction
        static readonly byte[] alpha_place_fields   = { FIELD_BYTE, FIELD_SHORT, FIELD_INT, FIELD_BYTE, FIELD_INT, FIELD_BYTE };
        // Beta block place head: byte op, int X, byte Y, int Z, byte direction, short blockId (+ optional item data)
        static readonly byte[] beta_place_head      = { FIELD_BYTE, FIELD_INT, FIELD_BYTE, FIELD_INT, FIELD_BYTE, FIELD_SHORT };
#endregion


#region Login / movement handlers
        static readonly byte[] version_peek_fields = { FIELD_BYTE, FIELD_INT };
        int HandleLogin(byte[] buffer, int offset, int left) {
            // Peek at the protocol version first so we can pick the correct login layout.
            // Alpha and Beta look identical up to this point but diverge afterwards.
            FieldValue* peek = stackalloc FieldValue[AlphaIndevParser.MAX_FIELDS];
            int peeked = parser.ParsePacket(buffer, offset, left, version_peek_fields, peek);
            if (peeked < 0) return NeedMore(peeked, left);

            int version = peek[1].I32;
            if (version == BETA_PROTOCOL_VERSION)       isBeta = true;
            else if (version == ALPHA_PROTOCOL_VERSION) isBeta = false;
            else {
                player.Leave("Unsupported protocol version " + version + "!");
                return left;
            }

            byte[] fields = isBeta ? beta_login_fields : alpha_login_fields;
            FieldValue* values = stackalloc FieldValue[AlphaIndevParser.MAX_FIELDS];
            int size = parser.ParsePacket(buffer, offset, left, fields, values);
            if (size < 0) return NeedMore(size, left);

            string name = parser.ReadString(buffer, values[2].I32);
            if (name == null) { player.Leave("Invalid username", true); return left; }

            if (!player.ProcessLogin(name, "")) return left;

            for (byte b = 0; b < Block.CPE_COUNT; b++)
                fallback[b] = Block.ConvertClassic(b, Server.VERSION_0030);

            player.CompleteLoginProcess();
            return size;
        }

        int HandleSelfMove(byte[] buffer, int offset, int left) {
            FieldValue* values = stackalloc FieldValue[AlphaIndevParser.MAX_FIELDS];
            int size = parser.ParsePacket(buffer, offset, left, move_fields, values);
            if (size < 0) return NeedMore(size, left);

            // serverbound order is X, Y (feet), Stance (eyes), Z.
            // We store the stance (eye) height; the -51 EntityYOffset converts it back to
            // feet when other players are shown this entity, so the two stay in sync.
            double x = values[1].F64;
            double y = values[3].F64;
            double z = values[4].F64;

            ClampToWorldBorder(ref x, ref y, ref z);
            MaybeStreamChunks(x, z);
            Orientation rot = player.Rot;
            player.ProcessMovement((int)(x * 32), (int)(y * 32), (int)(z * 32),
                                   rot.RotY, rot.HeadX, -1);
            return size;
        }

        int HandleSelfLook(byte[] buffer, int offset, int left) {
            FieldValue* values = stackalloc FieldValue[AlphaIndevParser.MAX_FIELDS];
            int size = parser.ParsePacket(buffer, offset, left, look_fields, values);
            if (size < 0) return NeedMore(size, left);

            float yaw   = values[1].F32 + 180.0f;
            float pitch = values[2].F32;

            Position pos = player.Pos;
            player.ProcessMovement(pos.X, pos.Y, pos.Z, DegToByte(yaw), DegToByte(pitch), -1);
            return size;
        }

        int HandleSelfMoveLook(byte[] buffer, int offset, int left) {
            FieldValue* values = stackalloc FieldValue[AlphaIndevParser.MAX_FIELDS];
            int size = parser.ParsePacket(buffer, offset, left, movelook_fields, values);
            if (size < 0) return NeedMore(size, left);

            // serverbound order is X, Y (feet), Stance (eyes), Z, yaw, pitch.
            // Store the stance (eye) height - see HandleSelfMove for why.
            double x = values[1].F64;
            double y = values[3].F64;
            double z = values[4].F64;
            float yaw   = values[5].F32 + 180.0f;
            float pitch = values[6].F32;

            ClampToWorldBorder(ref x, ref y, ref z);
            MaybeStreamChunks(x, z);
            player.ProcessMovement((int)(x * 32), (int)(y * 32), (int)(z * 32),
                                   DegToByte(yaw), DegToByte(pitch), -1);
            return size;
        }

        int HandleBlockDig(byte[] buffer, int offset, int left) {
            FieldValue* values = stackalloc FieldValue[AlphaIndevParser.MAX_FIELDS];
            int size = parser.ParsePacket(buffer, offset, left, dig_fields, values);
            if (size < 0) return NeedMore(size, left);

            byte status = values[1].U8;
            int x = values[2].I32;
            int y = values[3].U8; // Y is a byte
            int z = values[4].I32;

            // status 2 = "finished digging" (block broken) in Alpha/Beta
            if (status == 2) {
                TryBlockChange(x, y, z, 0, Block.Air);
                if (isBeta) RefreshHeldTool();
            } else if (status == 4 && isBeta) {
                // status 4 = dropped held item (Q) - top the client's stack back up
                RestockHeldSlot();
            }
            return size;
        }

        int HandleSlotSwitch(byte[] buffer, int offset, int left) {
            FieldValue* values = stackalloc FieldValue[AlphaIndevParser.MAX_FIELDS];
            int size = parser.ParsePacket(buffer, offset, left, beta_slot_fields, values);
            if (size < 0) return NeedMore(size, left);

            short slot = (short)values[1].U16;
            if (slot >= 0 && slot <= 8) heldSlot = slot;
            return size;
        }

        int HandleRespawn(byte[] buffer, int offset, int left) {
            const int size = 1 + 1; // opcode + dimension
            if (left < size) return 0;

            // The client wiped its local world when it respawned, so acknowledge the
            // respawn and then resend the map, inventory and position from scratch.
            SendPacket(new byte[] { OPCODE_RESPAWN, 0 });
            Level lvl = player.level;
            if (lvl != null) {
                SendLevel(null, lvl);
                // SendTeleport streams the columns around the position first
                SendTeleport(Entities.SelfID, player.Pos, player.Rot);
            }
            return size;
        }

        int HandleBlockPlace(byte[] buffer, int offset, int left) {
            return IsBeta ? HandleBetaBlockPlace(buffer, offset, left)
                          : HandleAlphaBlockPlace(buffer, offset, left);
        }

        int HandleAlphaBlockPlace(byte[] buffer, int offset, int left) {
            FieldValue* values = stackalloc FieldValue[AlphaIndevParser.MAX_FIELDS];
            int size = parser.ParsePacket(buffer, offset, left, alpha_place_fields, values);
            if (size < 0) return NeedMore(size, left);

            short block = (short)values[1].U16;
            int x   = values[2].I32;
            int y   = values[3].U8;
            int z   = values[4].I32;
            byte dir = values[5].U8;

            short held = block < 0 ? (short)-1 : TranslateIncoming(block, 0);
            if (held < 0 && block >= 1) RevertPlacement(x, y, z, dir);
            else                        PlaceOrActivate(x, y, z, dir, held);
            return size;
        }

        int HandleBetaBlockPlace(byte[] buffer, int offset, int left) {
            FieldValue* values = stackalloc FieldValue[AlphaIndevParser.MAX_FIELDS];
            int size = parser.ParsePacket(buffer, offset, left, beta_place_head, values);
            if (size < 0) return NeedMore(size, left);

            int x   = values[1].I32;
            int y   = values[2].U8;
            int z   = values[3].I32;
            byte dir = values[4].U8;
            short block = (short)values[5].U16;
            short damage = 0;

            // a held item id >= 0 is followed by byte amount + short damage (3 extra bytes)
            if (block >= 0) {
                if (left < size + 3) return 0; // wait for the rest of the packet
                damage = (short)AlphaIndevParser.ReadU16(buffer, offset + size + 1);
                size += 3;
            }

            short held = block < 0 ? (short)-1 : TranslateIncoming(block, damage);
            if (held < 0 && block >= 1 && block <= BETA_MAX_BLOCK) {
                RevertPlacement(x, y, z, dir); // block we can't store - undo the prediction
            } else {
                PlaceOrActivate(x, y, z, dir, held);
            }

            // top the stack the client just placed from back up to a full 64,
            // so the "creative" inventory never runs out
            if (block >= 1 && block <= BETA_MAX_BLOCK) {
                if (hotbarContents != null) {
                    hotbarContents[heldSlot] = block;
                    hotbarDamage[heldSlot]   = damage;
                }
                SendSetSlot(INV_HOTBAR_START + heldSlot, block, damage);
            }
            return size;
        }

        // Offsets coordinates against the clicked block face; false for invalid faces
        static bool OffsetByFace(ref int x, ref int y, ref int z, byte dir) {
            switch (dir) {
                case 0: y--; return true;
                case 1: y++; return true;
                case 2: z--; return true;
                case 3: z++; return true;
                case 4: x--; return true;
                case 5: x++; return true;
                default: return false;
            }
        }

        // Interprets a block placement. Direction 0xFF (-1) means "right clicked air";
        // 'block' is the translated MCGalaxy block id (-1 = untranslatable/an item id).
        void PlaceOrActivate(int x, int y, int z, byte dir, short block) {
            if (dir == 0xFF) return;                        // right clicking air / using an item
            if (block < 1 || block >= Block.CPE_COUNT) return; // no MCGalaxy equivalent
            if (!OffsetByFace(ref x, ref y, ref z, dir)) return;

            TryBlockChange(x, y, z, 1, (BlockID)block);
        }

        // The client predicted a placement the server won't store - resend the actual
        // block so the client doesn't keep showing a ghost block
        void RevertPlacement(int x, int y, int z, byte dir) {
            if (dir == 0xFF || !OffsetByFace(ref x, ref y, ref z, dir)) return;

            Level lvl = player.level;
            if (lvl == null || !lvl.IsValidPos(x, y, z)) return;
            SendBlockchange((ushort)x, (ushort)y, (ushort)z,
                            lvl.FastGetBlock((ushort)x, (ushort)y, (ushort)z));
        }

        int HandleWindowClick(byte[] buffer, int offset, int left) {
            // byte op, byte windowId, short slot, byte rightClick, short action, bool shift,
            //   short itemId; IF itemId != -1: byte count, short uses
            const int head = 1 + 1 + 2 + 1 + 2 + 1 + 2;
            if (left < head) return 0;

            short itemId = (short)AlphaIndevParser.ReadU16(buffer, offset + head - 2);
            int size = head;
            if (itemId != -1) size += 3;
            if (left < size) return 0;

            // Acknowledge the click so the client doesn't consider the inventory
            // action unconfirmed (vanilla servers ack every window click)
            byte windowId = buffer[offset + 1];
            ushort action = AlphaIndevParser.ReadU16(buffer, offset + 5);
            byte[] resp = new byte[1 + 1 + 2 + 1];
            resp[0] = OPCODE_TRANSACTION;
            resp[1] = windowId;
            WriteU16(action, resp, 2);
            resp[4] = 1; // accepted
            SendPacket(resp);
            return size;
        }

        static readonly byte[] sign_fields = { FIELD_BYTE, FIELD_INT, FIELD_SHORT, FIELD_INT,
                                               FIELD_STRING, FIELD_STRING, FIELD_STRING, FIELD_STRING };
        int HandleUpdateSign(byte[] buffer, int offset, int left) {
            return HandleIgnored(buffer, offset, left, sign_fields);
        }
#endregion


#region Level / map sending
        // Old clients keep EVERY received chunk column in memory (~80 KB each), so sending
        // a whole 512x512 map (1024 columns) at once runs the client out of memory. Like a
        // real Beta server, only columns within VIEW_RADIUS chunks of the player are sent,
        // new ones stream in as the player moves, and columns beyond UNLOAD_RADIUS are
        // unloaded again - client memory stays bounded regardless of map size.
        // (radius 8 = 17x17 = at most 289 columns / ~24 MB held client side)
        const int VIEW_RADIUS   = 8;
        const int UNLOAD_RADIUS = VIEW_RADIUS + 2; // hysteresis so walking doesn't thrash

        readonly object chunkLock = new object();
        // key = (chunkX << 16) | chunkZ. Dictionary used as a set (HashSet would need a
        // System.Core reference on older .NET Framework based servers)
        Dictionary<int, bool> sentChunks = new Dictionary<int, bool>();
        byte[] convTable, convMetaTable;
        Level chunkLevel;
        int lastChunkX = int.MinValue, lastChunkZ = int.MinValue;

        public override void SendLevel(Level prev, Level level) {
            byte[] conv = new byte[Block.ExtendedCount];
            byte[] meta = new byte[Block.ExtendedCount];
            for (int j = 0; j < Block.ExtendedCount; j++)
            {
                byte classic = (byte)ConvertBlock((BlockID)j);
                if (classic < WIRE_ID.Length) {
                    conv[j] = WIRE_ID[classic];
                    meta[j] = WIRE_META[classic];
                } else {
                    conv[j] = classic;
                }
            }

            lock (chunkLock) {
                // unload all columns that were sent for the previous level
                foreach (int key in sentChunks.Keys)
                    SendPacket(MakePreChunk(key >> 16, key & 0xFFFF, false));
                sentChunks.Clear();

                convTable     = conv;
                convMetaTable = meta;
                chunkLevel    = level;
                lastChunkX = int.MinValue; lastChunkZ = int.MinValue;
            }

            StreamChunks(level, level.SpawnPos.BlockX >> 4, level.SpawnPos.BlockZ >> 4);
            SendInventory();
        }

        // Sends any missing columns within VIEW_RADIUS of the given chunk position
        // (nearest rings first) and unloads columns beyond UNLOAD_RADIUS
        void StreamChunks(Level lvl, int pcx, int pcz) {
            lock (chunkLock) {
                if (lvl != chunkLevel || convTable == null) return;

                List<int> far = new List<int>();
                foreach (int key in sentChunks.Keys)
                {
                    int cx = key >> 16, cz = key & 0xFFFF;
                    if (Math.Abs(cx - pcx) > UNLOAD_RADIUS || Math.Abs(cz - pcz) > UNLOAD_RADIUS)
                        far.Add(key);
                }
                foreach (int key in far)
                {
                    sentChunks.Remove(key);
                    SendPacket(MakePreChunk(key >> 16, key & 0xFFFF, false));
                }

                for (int r = 0; r <= VIEW_RADIUS; r++)
                    for (int dz = -r; dz <= r; dz++)
                        for (int dx = -r; dx <= r; dx++)
                        {
                            // only walk the outer ring at distance r (inner rings already done)
                            if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != r) continue;

                            int cx = pcx + dx, cz = pcz + dz;
                            if (cx < 0 || cz < 0 || cx >= lvl.ChunksX || cz >= lvl.ChunksZ) continue;

                            int key = (cx << 16) | cz;
                            if (sentChunks.ContainsKey(key)) continue;
                            sentChunks[key] = true;

                            SendPacket(MakePreChunk(cx, cz, true));
                            SendChunkColumn(lvl, convTable, convMetaTable, cx, cz, 0, 128);
                        }
            }
        }

        // MCGalaxy worlds are finite but these clients assume infinite terrain - walking
        // past the map edge puts them in nonexistent chunks and they fall/crash. Act as a
        // world border: clamp movement to the level bounds and rubber-band the client
        // back inside. Falling out of the bottom of the world rescues to the spawn.
        const double BORDER_MARGIN = 0.4; // keeps the player's bounding box on real terrain
        void ClampToWorldBorder(ref double x, ref double y, ref double z) {
            Level lvl = player.level;
            if (lvl == null) return;

            bool clamped = false;
            double maxX = lvl.Width  - BORDER_MARGIN;
            double maxZ = lvl.Length - BORDER_MARGIN;

            if (x < BORDER_MARGIN) { x = BORDER_MARGIN; clamped = true; }
            if (x > maxX)          { x = maxX;          clamped = true; }
            if (z < BORDER_MARGIN) { z = BORDER_MARGIN; clamped = true; }
            if (z > maxZ)          { z = maxZ;          clamped = true; }

            // fell out of the bottom of the world - rescue to the spawn point
            if (y < -8) {
                Position spawn = lvl.SpawnPos;
                x = spawn.X / 32.0; y = spawn.Y / 32.0; z = spawn.Z / 32.0;
                clamped = true;
            }

            if (!clamped) return;
            // send the client back inside (SendTeleport also streams the terrain there)
            Position pos = new Position((int)(x * 32), (int)(y * 32), (int)(z * 32));
            SendTeleport(Entities.SelfID, pos, player.Rot);
        }

        // Called from movement handlers - streams new columns when the player crosses
        // into a different chunk
        void MaybeStreamChunks(double x, double z) {
            Level lvl = player.level;
            if (lvl == null) return;

            int pcx = (int)Math.Floor(x) >> 4, pcz = (int)Math.Floor(z) >> 4;
            lock (chunkLock) {
                if (lvl != chunkLevel) return;
                if (pcx == lastChunkX && pcz == lastChunkZ) return;
                lastChunkX = pcx; lastChunkZ = pcz;
            }
            StreamChunks(lvl, pcx, pcz);
        }

        protected override void EnsureChunksAt(Position pos) {
            Level lvl = player.level;
            if (lvl == null) return;
            StreamChunks(lvl, pos.BlockX >> 4, pos.BlockZ >> 4);
        }

        // Sends the 16 x h x 16 region starting at world Y 'y0' of chunk column (cx, cz).
        // If the compressed packet would exceed the socket's send buffer limit, the region
        // is recursively halved vertically until every packet fits. (A 16x4x16 region fits
        // even if its data is completely incompressible, so this always terminates.)
        void SendChunkColumn(Level lvl, byte[] conv, byte[] convMeta, int cx, int cz, int y0, int h) {
            byte[] packet = MakeChunkPacket(lvl, conv, convMeta, cx, cz, y0, h);
            if (packet.Length <= MAX_PACKET_SIZE || h <= 4) {
                SendPacket(packet);
                return;
            }

            int h1 = h / 2;
            SendChunkColumn(lvl, conv, convMeta, cx, cz, y0,      h1);
            SendChunkColumn(lvl, conv, convMeta, cx, cz, y0 + h1, h - h1);
        }

        protected override byte[] MakeLogin(string motd) {
            return IsBeta ? MakeBetaLogin(motd) : MakeAlphaLogin(motd);
        }

        byte[] MakeAlphaLogin(string motd) {
            int nameLen = CalcStringLength(Server.Config.Name);
            int motdLen = CalcStringLength(motd);
            byte[] data = new byte[1 + 4 + (2 + nameLen) + (2 + motdLen)];

            data[0] = OPCODE_LOGIN;
            WriteI32(Entities.SelfID, data, 1);
            WriteString(data, 1 + 4,               Server.Config.Name);
            WriteString(data, 1 + 4 + 2 + nameLen, motd);
            return data;
        }

        byte[] MakeBetaLogin(string motd) {
            string name = Server.Config.Name;
            // old clients disconnect when receiving a server name with > 16 characters:
            //  "java.io.IOException: Received string length longer than maximum allowed (18 > 16)"
            if (name.Length > 16) name = name.Substring(0, 16);

            int nameLen = CalcStringLength(name);
            byte[] data = new byte[1 + 4 + (2 + nameLen) + (8 + 1)];

            data[0] = OPCODE_LOGIN;
            WriteI32(Entities.SelfID, data, 1);
            WriteString(data, 1 + 4, name);
            // U64 map seed, U8 dimension both left as 0
            return data;
        }

        protected override int EntityYOffset { get { return -51; } }

        protected override byte[] MakeSelfMoveLook(Position pos, Orientation rot) {
            byte[] data = new byte[1 + 8 + 8 + 8 + 8 + 4 + 4 + 1];
            float yaw   = rot.RotY  * 360.0f / 256.0f;
            float pitch = rot.HeadX * 360.0f / 256.0f;
            data[0] = OPCODE_SELF_MOVE_LOOK;

            WriteF64(pos.X / 32.0, data,  1);
            WriteF64(pos.Y / 32.0, data,  9); // Y (feet)
            WriteF64(pos.Y / 32.0, data, 17); // stance
            WriteF64(pos.Z / 32.0, data, 25);

            WriteF32(yaw,   data, 33);
            WriteF32(pitch, data, 37);
            data[41] = 1; // on ground
            return data;
        }

        byte[] MakePreChunk(int x, int z, bool load) {
            byte[] data = new byte[1 + 4 + 4 + 1];
            data[0] = OPCODE_PRE_CHUNK;
            WriteI32(x, data, 1);
            WriteI32(z, data, 5);
            data[9] = (byte)(load ? 1 : 0);
            return data;
        }

        byte[] MakeChunkPacket(Level lvl, byte[] conv, byte[] convMeta, int cx, int cz, int y0, int h) {
            int volume = 16 * 16 * h;
            byte[] block_data  = new byte[volume];
            byte[] block_meta  = new byte[volume / 2];
            byte[] block_light = new byte[volume / 2];
            byte[] sky_light   = new byte[volume / 2];

            int maxY = Math.Min(y0 + h, (int)lvl.Height);

            for (int Y = y0; Y < maxY; Y++)
                for (int ZZ = 0; ZZ < 16; ZZ++)
                    for (int XX = 0; XX < 16; XX++)
                    {
                        int X = (cx * 16) + XX, Z = (cz * 16) + ZZ;
                        if (!lvl.IsValidPos(X, Y, Z)) continue;

                        BlockID raw = lvl.FastGetBlock((ushort)X, (ushort)Y, (ushort)Z);
                        int i = (Y - y0) + (ZZ * h) + (XX * h * 16);
                        block_data[i] = conv[raw];

                        // metadata is a nibble array; carries e.g. wool colours
                        byte meta = convMeta[raw];
                        if (meta != 0)
                            block_meta[i >> 1] |= (byte)(meta << ((i & 1) * 4));
                    }

            // Make everything fully lit
            for (int i = 0; i < sky_light.Length; i++) sky_light[i] = 0xFF;

            MemoryStream tmp = new MemoryStream();
            using (ZLibStream dst = new ZLibStream(tmp))
            {
                dst.Write(block_data,  0,  block_data.Length);
                dst.Write(block_meta,  0,  block_meta.Length);
                dst.Write(block_light, 0, block_light.Length);
                dst.Write(sky_light,   0,   sky_light.Length);
            }

            byte[] chunk = tmp.ToArray();
            byte[] data  = new byte[1 + 4 + 2 + 4 + 1 + 1 + 1 + 4 + chunk.Length];

            data[0] = OPCODE_CHUNK;
            WriteI32(cx * 16,      data, 1); // X/Y/Z region origin
            WriteU16((ushort)y0,   data, 5);
            WriteI32(cz * 16,      data, 7);
            data[11] = 15;                   // X/Y/Z region size - 1
            data[12] = (byte)(h - 1);
            data[13] = 15;

            WriteI32(chunk.Length, data, 14);
            Array.Copy(chunk, 0, data, 18, chunk.Length);
            return data;
        }

        // Minimal zlib wrapper (2 byte header + deflate body + adler32 footer).
        class ZLibStream : Stream
        {
            Stream underlying;
            DeflateStream dst;
            bool wroteHeader;
            uint s1 = 1, s2 = 0;

            public ZLibStream(Stream tmp) {
                underlying = tmp;
                dst = new DeflateStream(tmp, CompressionMode.Compress, true);
            }

            public override bool CanRead  { get { return false; } }
            public override bool CanSeek  { get { return false; } }
            public override bool CanWrite { get { return true; } }

            static Exception ex = new NotSupportedException();
            public override void Flush() { }
            public override long Length { get { throw ex; } }
            public override long Position { get { throw ex; } set { throw ex; } }
            public override int Read(byte[] buffer, int offset, int count) { throw ex; }
            public override long Seek(long offset, SeekOrigin origin) { throw ex; }
            public override void SetLength(long length) { throw ex; }

            public override void Close() {
                dst.Close();
                WriteFooter();
                base.Close();
            }

            public override void Write(byte[] buffer, int offset, int count) {
                if (!wroteHeader) WriteHeader();

                for (int i = 0; i < count; i++)
                {
                    s1 = (s1 + buffer[offset + i]) % 65521;
                    s2 = (s2 + s1)                 % 65521;
                }
                dst.Write(buffer, offset, count);
            }

            void WriteHeader() {
                wroteHeader = true;
                underlying.Write(new byte[] { 0x78, 0x9C }, 0, 2);
            }

            void WriteFooter() {
                byte[] footer = new byte[4];
                uint adler32 = (s2 << 16) | s1;
                NetUtils.WriteI32((int)adler32, footer, 0);
                underlying.Write(footer, 0, footer.Length);
            }
        }
#endregion

#region Beta inventory
        // Beta survival clients join with an empty inventory and so cannot build anything.
        // Populate the hotbar + main inventory with stacks of buildable blocks at login,
        // and top the used stack back up to 64 whenever the client places a block, giving
        // an effectively infinite "creative style" block supply.
        const byte OPCODE_SET_SLOT     = 0x67;
        const byte OPCODE_WINDOW_ITEMS = 0x68;
        // player inventory window layout: 0 craft result, 1-4 craft grid, 5-8 armor,
        //   9-35 main inventory, 36-44 hotbar
        const int INV_MAIN_START   = 9;
        const int INV_HOTBAR_START = 36;
        const int INV_SIZE         = 45;
        const short BETA_MAX_BLOCK = 96; // highest placeable block id in beta 1.7.3

        // Blocks given use ids identical in Beta and classic; coloured wools use Beta's
        // wool-colour damage values. Diamond tools (gold can't harvest everything) make
        // digging much faster; dig speed itself is entirely client side.
        static readonly short[] hotbar_items  = { 1, 4, 5, 3, 17, 35, 278, 277, 279 };
        static readonly short[] hotbar_damage = { 0, 0, 0, 0, 0,  0,  0,   0,   0 };
        static readonly short[] main_items = {
            276, 359, 20, 12, 45, 13, 18, 19, 6, 37, 38, 39, 40,
            14, 15, 16, 41, 42, 44, 46, 47, 48, 49, 2, 35, 35, 35
        };
        static readonly short[] main_damage = {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 14, 11, 4
        };

        short heldSlot;          // hotbar index (0-8) tracked from holding-change packets
        short[] hotbarContents;  // what we believe each hotbar slot holds (for restocking)
        short[] hotbarDamage;    // damage/colour values for hotbarContents

        static byte StackCount(short item) {
            return item >= 256 ? (byte)1 : (byte)64; // ids >= 256 are items/tools, unstackable
        }

        void SendInventory() {
            if (!isBeta) return; // Alpha uses a different (pre-window) inventory system

            hotbarContents = (short[])hotbar_items.Clone();
            hotbarDamage   = (short[])hotbar_damage.Clone();

            short[] slots  = new short[INV_SIZE];
            short[] damage = new short[INV_SIZE];
            for (int i = 0; i < INV_SIZE; i++) slots[i] = -1;
            for (int i = 0; i < main_items.Length && INV_MAIN_START + i < INV_HOTBAR_START; i++)
            {
                slots[INV_MAIN_START + i]  = main_items[i];
                damage[INV_MAIN_START + i] = main_damage[i];
            }
            for (int i = 0; i < hotbar_items.Length && i < 9; i++)
            {
                slots[INV_HOTBAR_START + i]  = hotbar_items[i];
                damage[INV_HOTBAR_START + i] = hotbar_damage[i];
            }

            // window items packet: byte op, byte window, short count, then per-slot payload
            //   (short id, then byte count + short damage when id != -1)
            int dataLen = 1 + 1 + 2;
            for (int i = 0; i < INV_SIZE; i++) dataLen += slots[i] < 0 ? 2 : 5;

            byte[] data = new byte[dataLen];
            data[0] = OPCODE_WINDOW_ITEMS;
            data[1] = 0; // window 0 = player inventory
            WriteU16(INV_SIZE, data, 2);

            int o = 4;
            for (int i = 0; i < INV_SIZE; i++)
            {
                WriteU16((ushort)slots[i], data, o); o += 2;
                if (slots[i] >= 0) {
                    data[o++] = StackCount(slots[i]);
                    WriteU16((ushort)damage[i], data, o); o += 2;
                }
            }
            SendPacket(data);
        }

        void SendSetSlot(int slot, short item, short damage) {
            byte[] data = new byte[1 + 1 + 2 + 2 + 1 + 2];
            data[0] = OPCODE_SET_SLOT;
            data[1] = 0; // window 0 = player inventory
            WriteU16((ushort)slot, data, 2);
            WriteU16((ushort)item, data, 4);
            data[6] = StackCount(item);
            WriteU16((ushort)damage, data, 7);
            SendPacket(data);
        }

        void RestockHeldSlot() {
            if (hotbarContents == null) return;
            short item = hotbarContents[heldSlot];
            if (item > 0) SendSetSlot(INV_HOTBAR_START + heldSlot, item, hotbarDamage[heldSlot]);
        }

        // Tool wear is client side - refresh the held tool to full durability after digs,
        // otherwise tools would slowly wear out and break
        void RefreshHeldTool() {
            if (hotbarContents == null) return;
            short item = hotbarContents[heldSlot];
            if (item >= 256) SendSetSlot(INV_HOTBAR_START + heldSlot, item, 0);
        }
#endregion

#region Block id translation
        // MCGalaxy stores classic block ids, where 21-36 are the 16 coloured wools; in
        // Alpha/Beta those ids mean lapis/sandstone/beds/rails/pistons instead, and wool
        // colour lives in the metadata of the single wool block (35). These tables map
        // outgoing classic ids to the Beta wire id + metadata (all other ids match 1:1).
        static readonly byte[] WIRE_ID   = new byte[50];
        static readonly byte[] WIRE_META = new byte[50];
        // classic wool order: Red,Orange,Yellow,Lime,Green,Teal,Aqua,Cyan,Blue,Indigo,
        //                     Violet,Magenta,Pink,Black,Gray,White -> beta wool colours
        static readonly byte[] wool_meta = { 14, 1, 4, 5, 13, 9, 3, 9, 11, 10, 10, 2, 6, 15, 7, 0 };
        // beta wool colour -> classic wool id (brown -> CPE brown wool)
        static readonly byte[] wool_to_classic = { 36, 22, 32, 27, 23, 24, 33, 35, 35, 28, 31, 29, 57, 25, 21, 34 };

        static AlphaProtocol() {
            for (int i = 0; i < WIRE_ID.Length; i++) WIRE_ID[i] = (byte)i;
            for (int i = 0; i < 16; i++)
            {
                WIRE_ID[21 + i]   = 35;
                WIRE_META[21 + i] = wool_meta[i];
            }
        }

        protected override void WriteBlockChange(byte[] data, int offset, byte block, int x, int y, int z) {
            byte id = block, meta = 0;
            if (block < WIRE_ID.Length) {
                id   = WIRE_ID[block];
                meta = WIRE_META[block];
            }

            data[offset + 0] = OPCODE_BLOCK_CHANGE;
            WriteI32(x, data, offset + 1);
            data[offset + 5] = (byte)y;
            WriteI32(z, data, offset + 6);
            data[offset + 10] = id;
            data[offset + 11] = meta;
        }

        // Maps a block id (+ item damage) received from an Alpha/Beta client to the
        // MCGalaxy block to store. Returns -1 when there is no reasonable equivalent
        // (unknown blocks and item ids - the placement is then reverted client side).
        static short TranslateIncoming(short block, short damage) {
            if (block >= 1  && block <= 20) return block; // ids identical to classic
            if (block >= 37 && block <= 49) return block;

            switch (block) {
                case 35: return wool_to_classic[damage & 0xF];
                case 24: return 52;  // sandstone       -> CPE sandstone
                case 53:             // wooden stairs   -> planks
                case 72:             // wooden pressure plate
                case 85:             // fence
                case 96: return 5;   // trapdoor
                case 54:             // chest           -> CPE crate
                case 58: return 64;  // crafting table
                case 65: return 51;  // ladder          -> CPE rope
                case 50: return 39;  // torch           -> mushroom (closest small deco)
                default: return -1;
            }
        }
#endregion

        public override string ClientName() {
            return IsBeta ? "Beta 1.7.3" : "Alpha 1.1.1";
        }
    }

    // ==================================================================================
    //  Indev protocol
    // ==================================================================================
    unsafe class IndevProtocol : AlphaIndevProtocol
    {
        public const int PROTOCOL_VERSION = 9;

        // NOTE indev replaces the bottom 2 layers with lava. Although the second layer *can*
        // be replaced via SetBlock, the bottom layer is always hardcoded to lava, so the whole
        // world is shifted up instead.
        const int WORLD_SHIFT_BLOCKS = 2;
        const int WORLD_SHIFT_COORDS = 64;

        public IndevProtocol(INetSocket s, string name, bool utf8) : base(s, name, utf8) { }

        protected override int HandlePacket(byte[] buffer, int offset, int left) {
            switch (buffer[offset]) {
                case OPCODE_PING:           return 1;
                case OPCODE_LOGIN:          return HandleLogin(buffer, offset, left);
                case OPCODE_CHAT:           return HandleChat(buffer, offset, left);
                case OPCODE_USE_ENTITY:     return HandleIgnored(buffer, offset, left, useentity_fields);
                case OPCODE_SELF_STATEONLY: return HandleSelfStateOnly(buffer, offset, left);
                case OPCODE_SELF_MOVE:      return HandleSelfMove(buffer, offset, left);
                case OPCODE_SELF_LOOK:      return HandleSelfLook(buffer, offset, left);
                case OPCODE_SELF_MOVE_LOOK: return HandleSelfMoveLook(buffer, offset, left);
                case OPCODE_BLOCK_DIG:      return HandleBlockDig(buffer, offset, left);
                case OPCODE_BLOCK_PLACE:    return HandleBlockPlace(buffer, offset, left);
                case OPCODE_SLOT_SWITCHED:  return HandleIgnored(buffer, offset, left, slotswitch_fields);
                case OPCODE_ARM_ANIM:       return HandleIgnored(buffer, offset, left, anim_fields);
                case OPCODE_KICK:           return HandleDisconnect(left);

                default:
                    return UnknownOpcode(buffer[offset], left);
            }
        }

        protected override int CalcStringLength(string value) { return value.Length * 2; }

        protected override void WriteString(byte[] buffer, int offset, string value) {
            // Actually sending unicode tends to kill the client with
            //  java.lang.ArrayIndexOutOfBoundsException, so encode as cp437 in UCS-2 cells.
            WriteU16((ushort)value.Length, buffer, offset);
            offset += 2;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i] == '§' ? '§' : value[i].UnicodeToCp437();
                buffer[offset++] = (byte)(c >> 8);
                buffer[offset++] = (byte)c;
            }
        }


#region Serverbound field layouts
        static readonly byte[] useentity_fields  = { FIELD_BYTE, FIELD_INT, FIELD_INT, FIELD_BYTE };
        static readonly byte[] slotswitch_fields = { FIELD_BYTE, FIELD_SHORT };
        static readonly byte[] anim_fields       = { FIELD_BYTE, FIELD_INT, FIELD_BYTE };
        static readonly byte[] dig_fields        = { FIELD_BYTE, FIELD_BYTE, FIELD_INT, FIELD_BYTE, FIELD_INT, FIELD_BYTE };
        // Indev login: byte op, int version, string username, long mapSeed, string motd1, string motd2
        static readonly byte[] login_fields      = { FIELD_BYTE, FIELD_INT, FIELD_STRING, FIELD_LONG, FIELD_STRING, FIELD_STRING };
        static readonly byte[] move_fields       = { FIELD_BYTE, FIELD_FLOAT, FIELD_FLOAT, FIELD_FLOAT, FIELD_FLOAT, FIELD_BYTE };
        static readonly byte[] look_fields       = { FIELD_BYTE, FIELD_FLOAT, FIELD_FLOAT, FIELD_BYTE };
        static readonly byte[] movelook_fields   = { FIELD_BYTE, FIELD_FLOAT, FIELD_FLOAT, FIELD_FLOAT, FIELD_FLOAT, FIELD_FLOAT, FIELD_FLOAT, FIELD_BYTE };
        // Indev block place: byte op, int X, byte Y, int Z, byte direction, short blockId
        static readonly byte[] place_fields      = { FIELD_BYTE, FIELD_INT, FIELD_BYTE, FIELD_INT, FIELD_BYTE, FIELD_SHORT };
#endregion


        int HandleLogin(byte[] buffer, int offset, int left) {
            FieldValue* values = stackalloc FieldValue[AlphaIndevParser.MAX_FIELDS];
            int size = parser.ParsePacket(buffer, offset, left, login_fields, values);
            if (size < 0) return NeedMore(size, left);

            int version = values[1].I32;
            if (version != PROTOCOL_VERSION) {
                player.Leave("Unsupported protocol version " + version + "!");
                return left;
            }

            string name = parser.ReadString(buffer, values[2].I32);
            if (name == null) { player.Leave("Invalid username", true); return left; }

            if (!player.ProcessLogin(name, "")) return left;

            for (byte b = 0; b < Block.CPE_COUNT; b++)
                fallback[b] = Block.ConvertClassic(b, Server.VERSION_0030);

            player.CompleteLoginProcess();
            return size;
        }

        int HandleSelfMove(byte[] buffer, int offset, int left) {
            FieldValue* values = stackalloc FieldValue[AlphaIndevParser.MAX_FIELDS];
            int size = parser.ParsePacket(buffer, offset, left, move_fields, values);
            if (size < 0) return NeedMore(size, left);

            float x = values[1].F32;
            float y = values[2].F32; // feet
            float z = values[4].F32;

            y += 1.59375f; // feet -> 'head' position
            y -= WORLD_SHIFT_BLOCKS;

            Orientation rot = player.Rot;
            player.ProcessMovement((int)(x * 32), (int)(y * 32), (int)(z * 32),
                                   rot.RotY, rot.HeadX, -1);
            return size;
        }

        int HandleSelfLook(byte[] buffer, int offset, int left) {
            FieldValue* values = stackalloc FieldValue[AlphaIndevParser.MAX_FIELDS];
            int size = parser.ParsePacket(buffer, offset, left, look_fields, values);
            if (size < 0) return NeedMore(size, left);

            float yaw   = values[1].F32 + 180.0f;
            float pitch = values[2].F32;

            Position pos = player.Pos;
            player.ProcessMovement(pos.X, pos.Y, pos.Z, DegToByte(yaw), DegToByte(pitch), -1);
            return size;
        }

        int HandleSelfMoveLook(byte[] buffer, int offset, int left) {
            FieldValue* values = stackalloc FieldValue[AlphaIndevParser.MAX_FIELDS];
            int size = parser.ParsePacket(buffer, offset, left, movelook_fields, values);
            if (size < 0) return NeedMore(size, left);

            float x = values[1].F32;
            float y = values[2].F32; // feet
            float z = values[4].F32;
            float yaw   = values[5].F32 + 180.0f;
            float pitch = values[6].F32;

            y += 1.59375f; // feet -> 'head' position
            y -= WORLD_SHIFT_BLOCKS;

            player.ProcessMovement((int)(x * 32), (int)(y * 32), (int)(z * 32),
                                   DegToByte(yaw), DegToByte(pitch), -1);
            return size;
        }

        int HandleBlockDig(byte[] buffer, int offset, int left) {
            FieldValue* values = stackalloc FieldValue[AlphaIndevParser.MAX_FIELDS];
            int size = parser.ParsePacket(buffer, offset, left, dig_fields, values);
            if (size < 0) return NeedMore(size, left);

            byte status = values[1].U8;
            int x = values[2].I32;
            int y = values[3].U8;
            int z = values[4].I32;
            y -= WORLD_SHIFT_BLOCKS;

            if (status == 2) TryBlockChange(x, y, z, 0, Block.Air);
            return size;
        }

        int HandleBlockPlace(byte[] buffer, int offset, int left) {
            FieldValue* values = stackalloc FieldValue[AlphaIndevParser.MAX_FIELDS];
            int size = parser.ParsePacket(buffer, offset, left, place_fields, values);
            if (size < 0) return NeedMore(size, left);

            int x   = values[1].I32;
            int y   = values[2].U8;
            int z   = values[3].I32;
            byte dir = values[4].U8;
            short block = (short)values[5].U16;
            y -= WORLD_SHIFT_BLOCKS;

            // indev block ids match the classic set; reject item ids / unknown blocks
            if (dir == 0xFF || block < 1 || block > 49) return size;
            switch (dir) {
                case 0: y--; break;
                case 1: y++; break;
                case 2: z--; break;
                case 3: z++; break;
                case 4: x--; break;
                case 5: x++; break;
                default: return size;
            }
            TryBlockChange(x, y, z, 1, (BlockID)block);
            return size;
        }


        public override void SendSpawnEntity(byte id, string name, string skin, Position pos, Orientation rot) {
            // indev client disconnects when receiving an entity with a nametag > 16 characters
            if (name != null && name.Length > 16) name = Colors.StripUsed(name);
            if (name != null && name.Length > 16) name = name.Substring(0, 16);
            base.SendSpawnEntity(id, name, skin, pos, rot);
        }

        protected override int EntityYOffset { get { return -19 + WORLD_SHIFT_COORDS; } }

        byte[] GetBlocks(Level level, byte* conv) {
            // NOTE indev client always overwrites the bottom 2 layers with lava
            byte[] blocks = new byte[level.blocks.Length];
            int i = level.PosToInt(0, WORLD_SHIFT_BLOCKS, 0);

            for (int y = 0; y < level.Height - WORLD_SHIFT_BLOCKS; y++)
                for (int z = 0; z < level.Length; z++)
                    for (int x = 0; x < level.Width; x++)
                        blocks[i++] = conv[level.FastGetBlock((ushort)x, (ushort)y, (ushort)z)];
            return blocks;
        }

        public override void SendLevel(Level prev, Level level) {
            byte* conv = stackalloc byte[Block.ExtendedCount];
            for (int j = 0; j < Block.ExtendedCount; j++)
                conv[j] = (byte)ConvertBlock((BlockID)j);

            byte[] C_blks = CompressData(GetBlocks(level, conv));
            byte[] C_meta = CompressData(new byte[level.blocks.Length]);
            byte[] tmp = new byte[4];

            using (MemoryStream ms = new MemoryStream())
            {
                byte[] map_data = new byte[1 + 4 + 4 + 4];
                map_data[0] = OPCODE_PRE_CHUNK;
                WriteI32(C_blks.Length, map_data, 1);
                WriteI32(C_meta.Length, map_data, 5);
                WriteI32(100, map_data, 9);
                ms.Write(map_data, 0, map_data.Length);

                WriteI32(C_blks.Length, tmp, 0);
                ms.Write(tmp, 0, tmp.Length);
                ms.Write(C_blks, 0, C_blks.Length);

                WriteI32(C_meta.Length, tmp, 0);
                ms.Write(tmp, 0, tmp.Length);
                ms.Write(C_meta, 0, C_meta.Length);

                // The indev map payload is one giant logical packet, which is far larger
                // than the socket's 4096 byte send buffer - must send it in slices
                SendLarge(ms.ToArray());
            }

            byte[] final = new byte[1 + 4 + 4 + 4 + 4 + 4];
            final[0] = OPCODE_CHUNK;
            final[1] = 0x01;
            WriteI32(level.Width,  final,  5);
            WriteI32(level.Height, final,  9);
            WriteI32(level.Length, final, 13);
            SendPacket(final);

            SendSetSpawnpoint(level.SpawnPos, default(Orientation));
        }

        byte[] CompressData(byte[] data) {
            using (MemoryStream dst = new MemoryStream())
            {
                using (GZipStream gz = new GZipStream(dst, CompressionMode.Compress, true))
                {
                    byte[] buffer = new byte[4];
                    WriteI32(data.Length, buffer, 0);
                    gz.Write(buffer, 0, 4);
                    gz.Write(data, 0, data.Length);
                }
                return dst.ToArray();
            }
        }

        protected override byte[] MakeLogin(string motd) {
            int nameLen = CalcStringLength(Server.Config.Name);
            int motdLen = CalcStringLength(motd);
            byte[] data = new byte[1 + 14 + (2 + nameLen) + (2 + motdLen)];

            data[0] = OPCODE_LOGIN;
            // first 14 bytes are world time / entity id related, left as 0
            WriteString(data, 1 + 14,               Server.Config.Name);
            WriteString(data, 1 + 14 + 2 + nameLen, motd);
            return data;
        }

        protected override byte[] MakeSelfMoveLook(Position pos, Orientation rot) {
            byte[] data = new byte[1 + 4 + 4 + 4 + 4 + 4 + 4 + 1];
            float yaw   = rot.RotY  * 360.0f / 256.0f;
            float pitch = rot.HeadX * 360.0f / 256.0f;
            data[0] = OPCODE_SELF_MOVE_LOOK;

            int y = pos.Y + 83 + WORLD_SHIFT_COORDS;

            WriteF32(pos.X / 32.0f, data,  1);
            WriteF32(y / 32.0f,     data,  5); // Y (feet)
            WriteF32(y / 32.0f,     data,  9); // stance
            WriteF32(pos.Z / 32.0f, data, 13);

            WriteF32(yaw,   data, 17);
            WriteF32(pitch, data, 21);
            data[25] = 1;
            return data;
        }

        public override string ClientName() { return "Indev"; }

        protected override void WriteBlockChange(byte[] data, int offset, byte block, int x, int y, int z) {
            y += WORLD_SHIFT_BLOCKS;
            base.WriteBlockChange(data, offset, block, x, y, z);
        }
    }
}
