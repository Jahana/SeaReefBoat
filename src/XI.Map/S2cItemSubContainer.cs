// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_ITEM_SUBCONTAINER::PacketData (0x026),
// src/map/packets/s2c/0x026_item_subcontainer.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cItemAttr / S2cEventStr convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_item_subcontainer_harness.cpp -> map_s2c_item_subcontainer_golden.txt), NOT hand-computed.
//
// THE genuinely-new facts this chunk pins:
//   (1) A fixed uint8 unknown01[2] ARRAY IMMEDIATELY followed by NINE consecutive uint16 members (model_id_*). The
//       array ends at 4+2==6 which is ALREADY 2-aligned, so the first u16 packs at 6 with NO silent pad -- and the
//       member the compiler tests for alignment is the array's END (base + count*elemsize), a u8-array-end parity
//       distinct from the chunk-44/45 scalar-u8-then-u16 boundary.
//   (2) The reused UNSCOPED engine enum `CONTAINER_ID container` (chunk 45's XI.Map.ContainerId -- NOT redefined here):
//       the chunk-21 underlying-type substitution is RE-CONFIRMED in this fresh body vs a uint8_t twin (EQ_SIZE/EQ_ALIGN/
//       ECATEGORY_OFF all 1). Its literals were already pinned in chunk 45, so this chunk needs NO new .h re-parse --
//       only the layout.
//   (3) The whole body has NO uint32 member, so alignof == 2 (from the uint16s), NOT 4; MEMBER_BYTES = 4 + 2 + 18 + 2
//       == 26 == size, 26 % 2 == 0 -> PAD_INSERTED == 0 (hole-free, no tail pad).
//
// Layout (DEFAULT packing, body-relative). sizeof=26, alignof=2:
//   [00]      uint8   is_used
//   [01]      uint8   container  (enum CONTAINER_ID : uint8 -- unscoped, reused from chunk 45)
//   [02]      uint8   index
//   [03]      uint8   unknown00
//   [04..05]  uint8   unknown01[2]  (the align-1 array; its END is 6, already 2-aligned)
//   [06..07]  uint16  model_id_race_hair
//   [08..09]  uint16  model_id_head
//   [10..11]  uint16  model_id_body
//   [12..13]  uint16  model_id_hands
//   [14..15]  uint16  model_id_legs
//   [16..17]  uint16  model_id_feet
//   [18..19]  uint16  model_id_main
//   [20..21]  uint16  model_id_sub
//   [22..23]  uint16  model_id_range
//   [24]      uint8   race
//   [25]      uint8   pose

using System;

namespace XI.Map
{
    // GP_SERV_COMMAND_ITEM_SUBCONTAINER::PacketData (outer, body-relative, DEFAULT packing). sizeof=26, alignof=2.
    public static class S2cItemSubContainer
    {
        public const int SizeOf  = 26; // PD_SIZE=26 (MEMBER_BYTES==26, PAD_INSERTED==0)
        public const int AlignOf = 2;  // PD_ALIGN=2 -- from the uint16s (there is NO uint32 member)

        // ---- leading u8 run (is_used / container / index / unknown00 @0/1/2/3) ----
        public const int OffIsUsed   = 0;  // OFF_PD.is_used=0
        public const int OffContainer = 1; // OFF_PD.container=1 (CONTAINER_ID unscoped enum, underlying byte)
        public const int OffIndex    = 2;  // OFF_PD.index=2
        public const int OffUnknown00 = 3; // OFF_PD.unknown00=3

        // ---- uint8 unknown01[2]: base 4, stride 1, its END 4+2==6 is 2-aligned -> the u16-run packs there no pad ----
        public const int OffUnknown01  = 4;  // OFF_PD.unknown01=4
        public const int Unknown01Len  = 2;  // ARR_unknown01_LEN=2 (== COUNT 2 * ELEMSIZE 1)

        // ---- nine consecutive uint16 model ids (6..23, dense stride 2, no inter-element pad) ----
        public const int OffModelIdBase = 6;      // OFF_PD.model_id_race_hair=6 (== the u8 array end, 2-aligned)
        public const int ModelIdCount   = 9;      // nine model_id_* members
        public const int ModelIdStride  = 2;      // uint16 stride, no pad
        public const int OffRace = 24;            // OFF_PD.race=24 (== model base 6 + 9*2)
        public const int OffPose = 25;            // OFF_PD.pose=25

        // ---- leading u8 accessors ----
        public static byte GetIsUsed(ReadOnlySpan<byte> b) => b[OffIsUsed];
        public static void SetIsUsed(Span<byte> b, byte v) => b[OffIsUsed] = v;

        // ---- container: the reused UNSCOPED enum (XI.Map.ContainerId), modelled at its underlying byte ----
        public static ContainerId GetContainer(ReadOnlySpan<byte> b) => (ContainerId)b[OffContainer];
        public static void        SetContainer(Span<byte> b, ContainerId v) => b[OffContainer] = (byte)v;
        public static byte        GetContainerRaw(ReadOnlySpan<byte> b) => b[OffContainer];

        public static byte GetIndex(ReadOnlySpan<byte> b) => b[OffIndex];
        public static void SetIndex(Span<byte> b, byte v) => b[OffIndex] = v;
        public static byte GetUnknown00(ReadOnlySpan<byte> b) => b[OffUnknown00];
        public static void SetUnknown00(Span<byte> b, byte v) => b[OffUnknown00] = v;

        // ---- uint8 unknown01[2] (real wire bytes, NOT droppable) ----
        public static byte GetUnknown01(ReadOnlySpan<byte> b, int index)
        {
            if ((uint)index >= (uint)Unknown01Len) throw new ArgumentOutOfRangeException(nameof(index));
            return b[OffUnknown01 + index];
        }
        public static void SetUnknown01(Span<byte> b, int index, byte v)
        {
            if ((uint)index >= (uint)Unknown01Len) throw new ArgumentOutOfRangeException(nameof(index));
            b[OffUnknown01 + index] = v;
        }
        public static ReadOnlySpan<byte> GetUnknown01Slice(ReadOnlySpan<byte> b) => b.Slice(OffUnknown01, Unknown01Len);

        // ---- the nine consecutive uint16 model ids, indexed by compiler stride ----
        public static int ModelIdOffset(int index)
        {
            if ((uint)index >= (uint)ModelIdCount) throw new ArgumentOutOfRangeException(nameof(index));
            return OffModelIdBase + index * ModelIdStride;
        }
        public static ushort GetModelId(ReadOnlySpan<byte> b, int index) => BitConverter.ToUInt16(b.Slice(ModelIdOffset(index), 2));
        public static void   SetModelId(Span<byte> b, int index, ushort v) => BitConverter.TryWriteBytes(b.Slice(ModelIdOffset(index), 2), v);

        // named accessors for the first/last (and a middle) model id, keyed by their compiler offsets
        public static ushort GetModelIdRaceHair(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(OffModelIdBase, 2));
        public static void   SetModelIdRaceHair(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(OffModelIdBase, 2), v);
        public static ushort GetModelIdRange(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(OffModelIdBase + 8 * ModelIdStride, 2));
        public static void   SetModelIdRange(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(OffModelIdBase + 8 * ModelIdStride, 2), v);

        // ---- trailing u8 pair (race / pose) ----
        public static byte GetRace(ReadOnlySpan<byte> b) => b[OffRace];
        public static void SetRace(Span<byte> b, byte v) => b[OffRace] = v;
        public static byte GetPose(ReadOnlySpan<byte> b) => b[OffPose];
        public static void SetPose(Span<byte> b, byte v) => b[OffPose] = v;

        // ---- generic little-endian scalar accessors keyed by a compiler offset (cross-check surface) ----
        public static byte   GetU8(ReadOnlySpan<byte> b, int off) => b[off];
        public static void   SetU8(Span<byte> b, int off, byte v) => b[off] = v;
        public static ushort GetU16(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt16(b.Slice(off, 2));
        public static void   SetU16(Span<byte> b, int off, ushort v) => BitConverter.TryWriteBytes(b.Slice(off, 2), v);
    }
}
