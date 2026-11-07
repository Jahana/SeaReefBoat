// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_ABIL_RECAST::PacketData (0x119),
// src/map/packets/s2c/0x119_abil_recast.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cUnity / S2cCurrencies2 convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_abil_recast_harness.cpp -> map_s2c_abil_recast_golden.txt), NOT hand-computed.
//
// THE genuinely-new packing facts this chunk exists to pin (contrast chunk 0x053 lockstyle, the C2S analog):
//   (1) LARGEST nested STRUCT-ARRAY yet: recasttimer_t Timers[31] (lockstyle 16, equipset 16, group_tbl 20). The
//       element recasttimer_t is a DENSE u16/u8/u8/u16/u16 chain: Timer@0 Calc1@2 TimerId@3 Calc2@4 padding06@6 ->
//       size 8 / align 2. The two uint8 (Calc1, TimerId) fill bytes 2..3 EXACTLY, so Calc2 lands on an already-
//       2-aligned byte 4 with NO silent pad (the oracle proves RT_PAD_INSERTED=0 / RT_MEMBER_BYTES==RT_SIZE==8).
//   (2) THE no-realign-after-struct-array fact: the trailing uint32 MountRecast sits at 31*8 == 248 and 248 % 4 == 0
//       -> NO pad between the array and the first u32 (the oracle pins MOUNT_MOD4=0). This holds for ANY element
//       count because the element STRIDE (8) is itself a multiple of 4, so count*8 is always 4-aligned.
//   (3) Whole-struct alignof = MAX member align = 4 (from the two trailing uint32s, NOT the align-2 element);
//       PD_SIZE = 248 + 4 + 4 == 256; 256 % 4 == 0 -> no tail pad; PAD_INSERTED=0 (hole-free).
//
// Layout (DEFAULT packing, body-relative). sizeof=256, alignof=4:
//   [000..247] recasttimer_t Timers[31] (stride 8, element: u16 Timer@0 u8 Calc1@2 u8 TimerId@3 u16 Calc2@4 u16 padding06@6)
//   [248..251] uint32 MountRecast        [252..255] uint32 MountRecastId

using System;

namespace XI.Map
{
    // recasttimer_t (inner struct, DEFAULT packing): one ability/mount recast row. sizeof=8, alignof=2.
    // Dense u16/u8/u8/u16/u16 chain -- the two uint8s fill bytes 2..3 so Calc2 is already 2-aligned (no silent pad).
    public static class RecastTimer
    {
        public const int SizeOf  = 8;   // RT_SIZE=8
        public const int AlignOf = 2;   // RT_ALIGN=2 (from the uint16s)

        public const int OffTimer     = 0;   // OFF_RT.Timer=0     (uint16)
        public const int OffCalc1     = 2;   // OFF_RT.Calc1=2     (uint8)
        public const int OffTimerId   = 3;   // OFF_RT.TimerId=3   (uint8)
        public const int OffCalc2     = 4;   // OFF_RT.Calc2=4     (uint16, already 2-aligned after the two u8s)
        public const int OffPadding06 = 6;   // OFF_RT.padding06=6 (uint16)
    }

    // GP_SERV_COMMAND_ABIL_RECAST::PacketData (outer, body-relative, DEFAULT packing). sizeof=256, alignof=4.
    public static class S2cAbilRecast
    {
        public const int SizeOf  = 256; // PD_SIZE=256 (MEMBER_BYTES==256, PAD_INSERTED==0)
        public const int AlignOf = 4;   // PD_ALIGN=4 -- from the two trailing uint32s (max member align)

        // The nested recast array: starts at body 0, 31 elements, stride == sizeof(recasttimer_t) (no inter-element pad).
        public const int OffTimers   = 0;    // OFF_PD.Timers=0
        public const int TimerCount  = 31;   // ARR_Timers_COUNT=31
        public const int TimerStride = 8;    // ARR_Timers_STRIDE=8 == RecastTimer.SizeOf
        public const int TimersLen   = 248;  // ARR_Timers_LEN=248 == 31*8

        // The two trailing uint32 mount fields sit right after the array -- no pad (248 is already 4-aligned).
        public const int OffMountRecast   = 248; // OFF_PD.MountRecast=248 == TimersLen
        public const int OffMountRecastId = 252; // OFF_PD.MountRecastId=252

        // Byte offset (from body start) of element `index` and of a field within it: index*8 + innerOffset, NO gap
        // (the oracle pinned Timers[1]=8 and Timers[30]=240, i.e. strict stride-8 packing).
        public static int ElementOffset(int index) => OffTimers + index * TimerStride;
        public static int FieldOffset(int index, int innerOffset) => ElementOffset(index) + innerOffset;

        // ---- element-field accessors (index 0..30) ----
        public static ushort GetTimer(ReadOnlySpan<byte> b, int index) =>
            BitConverter.ToUInt16(b.Slice(FieldOffset(index, RecastTimer.OffTimer), 2));
        public static void SetTimer(Span<byte> b, int index, ushort v) =>
            BitConverter.TryWriteBytes(b.Slice(FieldOffset(index, RecastTimer.OffTimer), 2), v);

        public static byte GetCalc1(ReadOnlySpan<byte> b, int index) => b[FieldOffset(index, RecastTimer.OffCalc1)];
        public static void SetCalc1(Span<byte> b, int index, byte v) => b[FieldOffset(index, RecastTimer.OffCalc1)] = v;

        public static byte GetTimerId(ReadOnlySpan<byte> b, int index) => b[FieldOffset(index, RecastTimer.OffTimerId)];
        public static void SetTimerId(Span<byte> b, int index, byte v) => b[FieldOffset(index, RecastTimer.OffTimerId)] = v;

        public static ushort GetCalc2(ReadOnlySpan<byte> b, int index) =>
            BitConverter.ToUInt16(b.Slice(FieldOffset(index, RecastTimer.OffCalc2), 2));
        public static void SetCalc2(Span<byte> b, int index, ushort v) =>
            BitConverter.TryWriteBytes(b.Slice(FieldOffset(index, RecastTimer.OffCalc2), 2), v);

        public static ushort GetPadding06(ReadOnlySpan<byte> b, int index) =>
            BitConverter.ToUInt16(b.Slice(FieldOffset(index, RecastTimer.OffPadding06), 2));
        public static void SetPadding06(Span<byte> b, int index, ushort v) =>
            BitConverter.TryWriteBytes(b.Slice(FieldOffset(index, RecastTimer.OffPadding06), 2), v);

        // ---- trailing uint32 mount fields ----
        public static uint GetU32(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt32(b.Slice(off, 4));
        public static void SetU32(Span<byte> b, int off, uint v) => BitConverter.TryWriteBytes(b.Slice(off, 4), v);

        public static uint GetMountRecast(ReadOnlySpan<byte> b)     => GetU32(b, OffMountRecast);
        public static void SetMountRecast(Span<byte> b, uint v)     => SetU32(b, OffMountRecast, v);
        public static uint GetMountRecastId(ReadOnlySpan<byte> b)   => GetU32(b, OffMountRecastId);
        public static void SetMountRecastId(Span<byte> b, uint v)   => SetU32(b, OffMountRecastId, v);

        // ---- generic little-endian scalar accessors keyed by a compiler offset (cross-check surface) ----
        public static ushort GetU16(ReadOnlySpan<byte> b, int off) => BitConverter.ToUInt16(b.Slice(off, 2));
        public static byte   GetU8(ReadOnlySpan<byte> b, int off)  => b[off];

        // ---- expose a whole element as a slice (8 real wire bytes) ----
        public static ReadOnlySpan<byte> GetTimerRow(ReadOnlySpan<byte> b, int index) => b.Slice(ElementOffset(index), TimerStride);
    }
}
