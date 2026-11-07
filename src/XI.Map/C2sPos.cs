// C# wire model for the first Phase-3 map C2S (client->server) BODY struct: GP_CLI_COMMAND_POS
// (0x015), src/map/packets/c2s/0x015_pos.h via the GP_CLI_PACKET(...) macro (c2s/base.h).
//
// CRITICAL METHODOLOGY: every offset/size/bit-mask below is copied from the compiled MSVC layout
// oracle (csharp/oracle/map_c2s_pos_harness.cpp -> map_c2s_pos_golden.txt), NOT hand-computed. The
// engine's C2S struct runs under MSVC DEFAULT packing (the GP_CLI_PACKET macro adds no #pragma pack),
// so a float/uint16/int8/uint32 mix gets real alignment padding (e.g. facetarget pushed to offset 22
// by the 4-byte float alignment above it, sizeof=32). And the TargetMode/RunMode/GroundMode/unused
// bit-field run makes offsetof ILLEGAL on those members (MSVC C2618), so the oracle derived its byte
// (offset 21) and per-flag masks by a runtime sentinel scan. tests/XI.Verify.Map re-reads that golden
// and asserts these constants + a deterministic byte-fill match it exactly; any engine layout change
// requires rebuilding the oracle.
//
// GP_CLI_HEADER (c2s/base.h) = { uint16 id:9; uint16 size:7; uint16 sync; }: word0 packs a 9-bit id
// and a 7-bit size, word1 is the sync. This is the SAME 32-bit word0 layout as the map CBasicPacket
// opcode framing (XI.Map.BasicPacket), which the gate cross-checks.

using System;

namespace XI.Map
{
    // The packet is read/written over CBasicPacket's 0x1FF buffer; here `body` is a Span positioned at
    // the packet start (header at [0]).
    public static class C2sPos
    {
        // GP_CLI_HEADER (4 bytes, alignof 2). word0 = (id & 0x1FF) | ((size & 0x7F) << 9); word1 = sync.
        public const int HeaderSize = 4;          // HDR_SIZE=4
        public const int HeaderAlign = 2;         // HDR_ALIGN=2
        public const int OffSync = 2;             // HDR_OFF_sync=2
        public const int IdMask = 0x1FF;          // id:9  -> bytes [0xFF,0x01]
        public const int SizeShift = 9;           // size:7 -> bytes [0x00,0xFE]
        public const int SizeMask = 0x7F;
        public const int IdMaskLoByte = 0xFF;     // HDR_id_mask_lo=255
        public const int IdMaskHiByte = 0x01;     // HDR_id_mask_hi=1
        public const int SizeMaskLoByte = 0x00;   // HDR_size_mask_lo=0
        public const int SizeMaskHiByte = 0xFE;   // HDR_size_mask_hi=254

        // GP_CLI_COMMAND_POS body (default-packed). sizeof=32, alignof=4.
        public const int SizeOf = 32;             // POS_SIZE=32
        public const int AlignOf = 4;             // POS_ALIGN=4
        public const int OffHeader = 0;           // OFF_POS.header=0
        public const int OffX = 4;                // OFF_POS.x=4
        public const int OffZ = 8;                // OFF_POS.z=8
        public const int OffY = 12;               // OFF_POS.y=12
        public const int OffMovTime = 16;         // OFF_POS.MovTime=16
        public const int OffMoveFlame = 18;       // OFF_POS.MoveFlame=18
        public const int OffDir = 20;             // OFF_POS.dir=20
        public const int OffBitfield = 21;        // BF_byte=21 (runtime-derived; offsetof illegal here)
        public const int OffFacetarget = 22;      // OFF_POS.facetarget=22
        public const int OffTimeNow = 24;         // OFF_POS.TimeNow=24
        public const int OffPadding00 = 28;       // OFF_POS.padding00=28

        // Bit-field masks in the OffBitfield byte (MSVC LSB-first).
        public const int TargetModeMask = 0x01;   // BF_TargetMode_mask=0x01
        public const int RunModeMask = 0x02;      // BF_RunMode_mask=0x02
        public const int GroundModeMask = 0x04;   // BF_GroundMode_mask=0x04
        public const int UnusedMask = 0xF8;       // BF_unused_mask=0xF8

        // ---- GP_CLI_HEADER read/write -------------------------------------------------------------
        public static void WriteHeader(Span<byte> body, ushort id, ushort size, ushort sync)
        {
            ushort word0 = (ushort)((id & IdMask) | ((size & SizeMask) << SizeShift));
            BitConverter.TryWriteBytes(body.Slice(0, 2), word0);
            BitConverter.TryWriteBytes(body.Slice(OffSync, 2), sync);
        }

        public static ushort ReadId(ReadOnlySpan<byte> body) =>
            (ushort)(BitConverter.ToUInt16(body.Slice(0, 2)) & IdMask);

        public static ushort ReadSize(ReadOnlySpan<byte> body) =>
            (ushort)((BitConverter.ToUInt16(body.Slice(0, 2)) >> SizeShift) & SizeMask);

        public static ushort ReadSync(ReadOnlySpan<byte> body) =>
            BitConverter.ToUInt16(body.Slice(OffSync, 2));

        // ---- body field accessors (little-endian, matching CBasicPacket::ref<T>) ------------------
        public static float GetX(ReadOnlySpan<byte> body) => BitConverter.ToSingle(body.Slice(OffX, 4));
        public static float GetZ(ReadOnlySpan<byte> body) => BitConverter.ToSingle(body.Slice(OffZ, 4));
        public static float GetY(ReadOnlySpan<byte> body) => BitConverter.ToSingle(body.Slice(OffY, 4));
        public static void SetX(Span<byte> body, float v) => BitConverter.TryWriteBytes(body.Slice(OffX, 4), v);
        public static void SetZ(Span<byte> body, float v) => BitConverter.TryWriteBytes(body.Slice(OffZ, 4), v);
        public static void SetY(Span<byte> body, float v) => BitConverter.TryWriteBytes(body.Slice(OffY, 4), v);

        public static ushort GetMovTime(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffMovTime, 2));
        public static void SetMovTime(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffMovTime, 2), v);
        public static ushort GetMoveFlame(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffMoveFlame, 2));
        public static void SetMoveFlame(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffMoveFlame, 2), v);

        // dir is int8_t.
        public static sbyte GetDir(ReadOnlySpan<byte> body) => unchecked((sbyte)body[OffDir]);
        public static void SetDir(Span<byte> body, sbyte v) => body[OffDir] = unchecked((byte)v);

        public static ushort GetFacetarget(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffFacetarget, 2));
        public static void SetFacetarget(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffFacetarget, 2), v);

        public static uint GetTimeNow(ReadOnlySpan<byte> body) => BitConverter.ToUInt32(body.Slice(OffTimeNow, 4));
        public static void SetTimeNow(Span<byte> body, uint v) => BitConverter.TryWriteBytes(body.Slice(OffTimeNow, 4), v);

        // ---- bit-field accessors (TargetMode/RunMode/GroundMode share one byte) -------------------
        public static bool GetTargetMode(ReadOnlySpan<byte> body) => (body[OffBitfield] & TargetModeMask) != 0;
        public static bool GetRunMode(ReadOnlySpan<byte> body) => (body[OffBitfield] & RunModeMask) != 0;
        public static bool GetGroundMode(ReadOnlySpan<byte> body) => (body[OffBitfield] & GroundModeMask) != 0;

        // Set the three mode flags, preserving the 5 unused bits exactly like writing the C++ bit-fields.
        public static void SetModes(Span<byte> body, bool targetMode, bool runMode, bool groundMode)
        {
            byte v = body[OffBitfield];
            v = (byte)((v & ~TargetModeMask) | (targetMode ? TargetModeMask : 0));
            v = (byte)((v & ~RunModeMask) | (runMode ? RunModeMask : 0));
            v = (byte)((v & ~GroundModeMask) | (groundMode ? GroundModeMask : 0));
            body[OffBitfield] = v;
        }
    }
}
