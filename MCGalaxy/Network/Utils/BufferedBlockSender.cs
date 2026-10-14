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
using BlockID = System.UInt16;
using BlockRaw = System.Byte;

namespace MCGalaxy.Network 
{
    /// <summary> Helper class for efficiently sending many block changes. </summary>
    /// <remarks> Sends block changes as either a single CPE BulkBlockUpdate packet,
    /// or 256 SetBlock packets combined as a single byte array to reduce overhead. </remarks>
    public sealed class BufferedBlockSender 
    {
        public Level level;
        public Player player;
        // fields below should not be modified by code outside of BufferedBlockSender
        public int[] indices    = new int[256];
        public BlockID[] blocks = new BlockID[256];
        public int count;
        
        public BufferedBlockSender() { }
        
        /// <summary> Constructs a bulk sender that will send block changes to all players on that level </summary>
        public BufferedBlockSender(Level level) {
            this.level = level;
        }
        
        /// <summary> Constructs a bulk sender that will only send block changes to that player </summary>
        public BufferedBlockSender(Player player) {
            this.player = player;
            this.level  = player.level;
        }
        
        /// <summary> Adds a block change to list of buffered changes </summary>
        /// <remarks> This method automatically calls Flush() when buffer limit is reached </remarks>
        public void Add(int index, BlockID block) {
            indices[count] = index;
            if (Block.IsPhysicsType(block)) {
                blocks[count] = Block.Convert(block);
            } else {
                blocks[count] = block;
            }
            
            count++;
            if (count == 256) Flush();
        }
        
        /// <summary> Sends buffered block changes to target player(s) </summary>
        public void Flush() {
            if (count == 0) return;
            
            if (player != null) SendPlayer();
            else SendLevel();
            count = 0;
        }
        
        void SendLevel() {
            PacketCache cache = new PacketCache();
            Player[] players  = PlayerInfo.Online.Items;
            foreach (Player p in players) 
            {
                if (p.level != level) continue;
                
                byte[] packet = MakePacket(p, cache);
                p.Socket.Send(packet, SendFlags.LowPriority);
            }
        }
        
        void SendPlayer() {
            byte[] packet = MakePacket(player, new PacketCache());
            player.Socket.Send(packet, SendFlags.LowPriority);
        }
        
        #region Packet construction
        
        /// <summary> Packets already constructed for this batch of block changes </summary>
        /// <remarks> Most players share the same packet format, so avoid constructing the same packet repeatedly </remarks>
        sealed class PacketCache 
        {
            public byte[] bulk, normal, classic;
            // [0] = for clients supporting all blocks, [1] = for clients that need fallback for some blocks
            public byte[][] ext = new byte[2][], extBulk = new byte[2][];
        }
        
        byte[] MakePacket(Player p, PacketCache cache) {
            IGameSession s = p.Session;
            #if TEN_BIT_BLOCKS
            if (s.hasExtBlocks) {
                // Clients may only support ExtendedBlocks v1 (i.e. up to 767), even if server supports more
                int i = s.MaxRawBlock >= Block.MaxRaw ? 0 : 1;
                
                if (s.hasBulkBlockUpdate && count >= 150) {
                    if (cache.extBulk[i] == null) cache.extBulk[i] = MakeBulkExt(s.MaxRawBlock);
                    return cache.extBulk[i];
                } else {
                    if (cache.ext[i] == null) cache.ext[i] = MakeExt(s.MaxRawBlock);
                    return cache.ext[i];
                }
            }
            #endif
            
            // Different clients support varying types of blocks
            if (s.hasBulkBlockUpdate && s.hasBlockDefs && count >= 160) {
                if (cache.bulk == null) cache.bulk = MakeBulk();
                return cache.bulk;
            } else if (s.hasBlockDefs) {
                // supports all 255 blocks (classicube enhanced client)
                if (cache.normal == null) cache.normal = MakeNormal();
                return cache.normal;
            } else if (!s.hasCustomBlocks && s.ProtocolVersion == Server.VERSION_0030) {
                // supports original 45 blocks (classic client)
                if (cache.classic == null) cache.classic = MakeLimited(s.fallback);
                return cache.classic;
            } else {
                // other support combination (CPE only, preclassic, etc)
                //  don't bother trying to optimise for this case
                return s.MakeBulkBlockchange(this);
            }
        }

        #if TEN_BIT_BLOCKS
        BlockID ToExtRaw(BlockID block, BlockID maxRaw) {
            BlockID raw = Block.ToRaw(block);
            return raw <= maxRaw ? raw : level.GetFallback(block);
        }
        
        byte[] MakeBulkExt(BlockID maxRaw) {
            // ExtendedBlocks v1 sends upper 2 bits of each block id, v2 sends upper 4 bits
            bool v2 = maxRaw > 1023;
            byte[] data = new byte[2 + 256 * 5 + (v2 ? 256 / 2 : 256 / 4)];
            data[0] = Opcode.CpeBulkBlockUpdate;
            data[1] = (byte)(count - 1);
            
            for (int i = 0, j = 2; i < count; i++) 
            {
                int index = indices[i];
                data[j++] = (byte)(index >> 24); data[j++] = (byte)(index >> 16);
                data[j++] = (byte)(index >> 8);  data[j++] = (byte)index;
            }
            
            int lo = 2 + 256 * sizeof(int), hi = lo + 256;
            for (int i = 0; i < count; i++) 
            {
                BlockID raw = ToExtRaw(blocks[i], maxRaw);
                data[lo + i] = (BlockRaw)raw;
                
                if (v2) {
                    data[hi + (i >> 1)] |= (byte)(((raw >> 8) & 0x0F) << ((i & 1) * 4));
                } else {
                    data[hi + (i >> 2)] |= (byte)(((raw >> 8) & 0x03) << ((i & 3) * 2));
                }
            }
            return data;
        }
        
        byte[] MakeExt(BlockID maxRaw) {
            byte[] data = new byte[count * 9];
            for (int i = 0, j = 0; i < count; i++) 
            {
                int index = indices[i];
                int x = (index % level.Width);
                int y = (index / level.Width) / level.Length;
                int z = (index / level.Width) % level.Length;
                
                data[j++] = Opcode.SetBlock;
                data[j++] = (byte)(x >> 8); data[j++] = (byte)x;
                data[j++] = (byte)(y >> 8); data[j++] = (byte)y;
                data[j++] = (byte)(z >> 8); data[j++] = (byte)z;
                BlockID raw = ToExtRaw(blocks[i], maxRaw);
                data[j++] = (byte)(raw >> 8);
                data[j++] = (byte)raw;
            }
            return data;
        }
        #endif


        internal byte[] MakeBulk() {
            byte[] data = new byte[2 + 256 * 5];
            data[0] = Opcode.CpeBulkBlockUpdate;
            data[1] = (byte)(count - 1);
            for (int i = 0, j = 2; i < count; i++) 
            {
                int index = indices[i];
                data[j++] = (byte)(index >> 24); data[j++] = (byte)(index >> 16);
                data[j++] = (byte)(index >> 8);  data[j++] = (byte)index;
            }
            for (int i = 0, j = 2 + 256 * sizeof(int); i < count; i++) 
            {
                #if TEN_BIT_BLOCKS
                BlockID block = blocks[i];
                data[j++] = block <= 511 ? (BlockRaw)block : level.GetFallback(block);
                #else
                data[j++] = (BlockRaw)blocks[i];
                #endif
            }
            return data;
        }
        
        internal byte[] MakeNormal() {
            byte[] data = new byte[count * 8];
            for (int i = 0, j = 0; i < count; i++) 
            {
                int index = indices[i];
                int x = (index % level.Width);
                int y = (index / level.Width) / level.Length;
                int z = (index / level.Width) % level.Length;
                
                data[j++] = Opcode.SetBlock;
                data[j++] = (byte)(x >> 8); data[j++] = (byte)x;
                data[j++] = (byte)(y >> 8); data[j++] = (byte)y;
                data[j++] = (byte)(z >> 8); data[j++] = (byte)z;
                #if TEN_BIT_BLOCKS
                BlockID block = blocks[i];
                data[j++] = block <= 511 ? (BlockRaw)block : level.GetFallback(block);
                #else
                data[j++] = (BlockRaw)blocks[i];
                #endif
            }
            return data;
        }

        internal byte[] MakeLimited(byte[] fallback) {
            byte[] data = new byte[count * 8];
            for (int i = 0, j = 0; i < count; i++) 
            {
                int index = indices[i];
                int x = (index % level.Width);
                int y = (index / level.Width) / level.Length;
                int z = (index / level.Width) % level.Length;
                
                data[j++] = Opcode.SetBlock;
                data[j++] = (byte)(x >> 8); data[j++] = (byte)x;
                data[j++] = (byte)(y >> 8); data[j++] = (byte)y;
                data[j++] = (byte)(z >> 8); data[j++] = (byte)z;
                data[j++] = fallback[level.GetFallback(blocks[i])];
            }
            return data;
        }
        #endregion
    }
}
