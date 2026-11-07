// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_PBX_RESULT::PacketData (0x04b),
// src/map/packets/s2c/0x04b_pbx_result.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cGroupEffects/S2cPendingnum convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_union_pbx_harness.cpp -> map_s2c_union_pbx_golden.txt), NOT hand-computed. This
// is the first Phase-3 body containing a UNION -- the last packing hazard not yet exercised. A union's
// size/alignment is NOT the sum of its members: all arms ALIAS at offset 0 and the union's size is the MAX
// of its arms. That fact cascades through this struct:
//   - GC_PBOX_SEND  { u8 To[16]@0; u32 RequestID@16; u32 RequestTime@20; i32 ItemWorkNo@24 } -> 28/4
//   - GC_PBOX_RECV  { u8 From[16]@0; u32 RequestID@16; u32 RequestTime@20; i32 OpponentPBoxNo@24 } -> 28/4
//   - GC_PBOX = union{SEND Send@0; RECV Recv@0} -> size == max(28,28) == 28 (both arms at offset 0)
//   - GP_POST_BOX_STATE { u32 Stat@0; GC_PBOX box_state@4; u16 ItemNo@32; u16 padding22@34; i32 Kind@36;
//                          u32 Stack@40; u8 Data[28]@44 } -> 72/4
//   - PacketData: Command@0, BoxNo@1 (3 pad -> 4-aligned ItemStacks@4), Result@8, ResParam1..3@9..11, then
//       the ANONYMOUS union { u32 Represent; GP_POST_BOX_STATE State } at offset 12. The union's size is set
//       by the LARGER State arm (72), so Represent@12 == State@12 (alias) and the whole body is PBX_SIZE=84
//       (12 + 72). A hand-reasoner sizing the union to the u32 Represent would get 16 -- the oracle decides.
// The two forward-declared engine enums (GP_CLI_COMMAND_PBX_*) appear only in constructor signatures, NOT
// in any member, so the body layout is pure fixed-width and the oracle is self-contained.

using System;

namespace XI.Map
{
    // GC_PBOX_SEND (union arm, default-packed). sizeof=28, alignof=4. Offsets are arm-relative.
    public static class PbxSend
    {
        public const int SizeOf = 28;      // SEND_SIZE=28
        public const int AlignOf = 4;      // SEND_ALIGN=4
        public const int OffTo = 0;        // OFF_SEND.To=0 (uint8[16])
        public const int ToLen = 16;       // SEND_To_len=16
        public const int OffRequestID = 16;   // OFF_SEND.RequestID=16 (uint32)
        public const int OffRequestTime = 20; // uint32
        public const int OffItemWorkNo = 24;  // int32
    }

    // GC_PBOX_RECV (union arm, default-packed). sizeof=28, alignof=4.
    public static class PbxRecv
    {
        public const int SizeOf = 28;      // RECV_SIZE=28
        public const int AlignOf = 4;      // RECV_ALIGN=4
        public const int OffFrom = 0;      // OFF_RECV.From=0 (uint8[16])
        public const int FromLen = 16;
        public const int OffRequestID = 16;    // uint32
        public const int OffRequestTime = 20;  // uint32
        public const int OffOpponentPBoxNo = 24; // OFF_RECV.OpponentPBoxNo=24 (int32)
    }

    // GC_PBOX = union { SEND Send; RECV Recv }. sizeof=28 (== max arm), both arms @0.
    // The two arms share an identical byte layout (16-byte name + RequestID + RequestTime + one i32), so
    // the union view reads the name at 0 and the differing last scalar at 24 depending on which arm.
    public static class PboxUnion
    {
        public const int SizeOf = 28;      // PBOX_SIZE=28
        public const int AlignOf = 4;      // PBOX_ALIGN=4
        public const int OffSend = 0;      // OFF_PBOX.Send=0
        public const int OffRecv = 0;      // OFF_PBOX.Recv=0 (aliases Send -- the union fact)

        public static ReadOnlySpan<byte> GetName(ReadOnlySpan<byte> body, int base_) => body.Slice(base_, 16);
        public static uint GetRequestID(ReadOnlySpan<byte> body, int base_) => BitConverter.ToUInt32(body.Slice(base_ + 16, 4));
        public static void SetRequestID(Span<byte> body, int base_, uint v) => BitConverter.TryWriteBytes(body.Slice(base_ + 16, 4), v);
        // the two arms' differing trailing int32 (ItemWorkNo for Send, OpponentPBoxNo for Recv) alias at +24.
        public static int GetTailI32(ReadOnlySpan<byte> body, int base_) => BitConverter.ToInt32(body.Slice(base_ + 24, 4));
        public static void SetTailI32(Span<byte> body, int base_, int v) => BitConverter.TryWriteBytes(body.Slice(base_ + 24, 4), v);
    }

    // GP_POST_BOX_STATE (embeds GC_PBOX by value). sizeof=72, alignof=4. Offsets are state-relative.
    public static class PostBoxState
    {
        public const int SizeOf = 72;      // STATE_SIZE=72
        public const int AlignOf = 4;      // STATE_ALIGN=4
        public const int OffStat = 0;      // OFF_STATE.Stat=0 (uint32)
        public const int OffBoxState = 4;  // OFF_STATE.box_state=4 (GC_PBOX union)
        public const int OffItemNo = 32;   // OFF_STATE.ItemNo=32 (uint16)
        public const int OffKind = 36;     // OFF_STATE.Kind=36 (int32)
        public const int OffStack = 40;    // OFF_STATE.Stack=40 (uint32)
        public const int OffData = 44;     // OFF_STATE.Data=44 (uint8[28])
        public const int DataLen = 28;     // STATE_Data_len=28
    }

    // GP_SERV_COMMAND_PBX_RESULT::PacketData (outer, default packing). sizeof=84, alignof=4.
    // The anonymous union { u32 Represent; GP_POST_BOX_STATE State } sits at OffUnion=12, sized by the
    // LARGER State arm (72) -> Represent and State alias at 12, total 12 + 72 = 84.
    public static class S2cPbxResult
    {
        public const int SizeOf = 84;          // PBX_SIZE=84
        public const int AlignOf = 4;          // PBX_ALIGN=4

        public const int OffCommand = 0;       // OFF_PBX.Command=0 (uint8)
        public const int OffBoxNo = 1;         // OFF_PBX.BoxNo=1 (int8) -- 3 pad bytes -> ItemStacks@4
        public const int OffItemStacks = 4;    // OFF_PBX.ItemStacks=4 (int32)
        public const int OffResult = 8;        // OFF_PBX.Result=8 (int8)
        public const int OffResParam1 = 9;     // int8
        public const int OffResParam2 = 10;    // int8
        public const int OffResParam3 = 11;    // int8

        public const int OffUnion = 12;        // OFF_PBX.Represent=OFF_PBX.State=12 (the anonymous union)
        public const int OffRepresent = 12;    // uint32 arm == union start
        public const int OffState = 12;        // GP_POST_BOX_STATE arm == union start (aliases Represent)

        // The union's State arm nests at OffState; a member inside it is OffState + (state-relative offset).
        public static int StateItemNoOffset() => OffState + PostBoxState.OffItemNo;   // == 44 (OFF_PBX.State_ItemNo)
        public static int StateDataOffset() => OffState + PostBoxState.OffData;      // == 56 (OFF_PBX.State_Data)

        // ---- outer scalar accessors (little-endian) -----------------------------------------------
        public static byte GetCommand(ReadOnlySpan<byte> body) => body[OffCommand];
        public static void SetCommand(Span<byte> body, byte v) => body[OffCommand] = v;

        public static sbyte GetBoxNo(ReadOnlySpan<byte> body) => unchecked((sbyte)body[OffBoxNo]);
        public static void SetBoxNo(Span<byte> body, sbyte v) => body[OffBoxNo] = unchecked((byte)v);

        public static int GetItemStacks(ReadOnlySpan<byte> body) => BitConverter.ToInt32(body.Slice(OffItemStacks, 4));
        public static void SetItemStacks(Span<byte> body, int v) => BitConverter.TryWriteBytes(body.Slice(OffItemStacks, 4), v);

        public static sbyte GetResult(ReadOnlySpan<byte> body) => unchecked((sbyte)body[OffResult]);
        public static void SetResult(Span<byte> body, sbyte v) => body[OffResult] = unchecked((byte)v);

        // ---- anonymous-union u32 arm (Represent) --------------------------------------------------
        public static uint GetRepresent(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffRepresent, 4));
        public static void SetRepresent(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(OffRepresent, 4), v);

        // ---- anonymous-union State arm (a GP_POST_BOX_STATE at OffState) --------------------------
        public static uint GetStateStat(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffState + PostBoxState.OffStat, 4));
        public static void SetStateStat(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(OffState + PostBoxState.OffStat, 4), v);

        public static ushort GetStateItemNo(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(StateItemNoOffset(), 2));
        public static void SetStateItemNo(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(StateItemNoOffset(), 2), v);

        public static int GetStateKind(ReadOnlySpan<byte> body) => BitConverter.ToInt32(body.Slice(OffState + PostBoxState.OffKind, 4));
        public static void SetStateKind(Span<byte> body, int v) => BitConverter.TryWriteBytes(body.Slice(OffState + PostBoxState.OffKind, 4), v);

        public static ReadOnlySpan<byte> GetStateData(ReadOnlySpan<byte> body) => body.Slice(StateDataOffset(), PostBoxState.DataLen);
    }
}
