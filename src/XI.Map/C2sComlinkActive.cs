// C# wire model for the Phase-3 map C2S body struct GP_CLI_COMMAND_GROUP_COMLINK_ACTIVE (0x0c4),
// src/map/packets/c2s/0x0c4_group_comlink_active.h via the GP_CLI_PACKET(...) macro (c2s/base.h).
//
// CRITICAL METHODOLOGY: every offset/size/mask below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_c2s_comlink_active_harness.cpp -> map_c2s_comlink_active_golden.txt), NOT
// hand-computed. This is the first Phase-3 body with a MULTI-NIBBLE bit-field run: the body opens with
// FOUR `uint16_t x : 4` fields (r, g, b, a). Under MSVC the bit-field allocation unit is the declared
// type (uint16_t), and 4*4 == 16 bits exactly fills one uint16 unit, so the four nibbles share bytes
// [4..5] (right after the 4-byte GP_CLI_HEADER). Which nibble is where is a compiler-owned little-endian
// bit-packing fact, and offsetof is ILLEGAL on a bit-field (C2618), so the oracle derived each nibble by
// the RUNTIME SENTINEL SCAN (set one field to 0xF, find the single byte that changed and whether it was
// 0x0F=low nibble or 0xF0=high nibble):
//   BF_r -> byte 4 low nibble (0x0F) | BF_g -> byte 4 high nibble (0xF0)
//   BF_b -> byte 5 low nibble (0x0F) | BF_a -> byte 5 high nibble (0xF0)
// The remaining fixed-width members use normal offsetof: ItemIndex@6, Category@7, ActiveFlg@8,
// padding00[3]@9, sComLinkName[15]@12, LinkshellId@27 -> CL_ALIGN=2 / CL_SIZE=28 (no tail pad). The two
// hand-authored uint8_t enums (ACTIVEFLG, LINKSHELLID) are pinned the same way as chunk 7/8 (re-declared
// in the oracle, underlying value printed, asserted vs the C# members). The GP_CLI_HEADER id:9|size:7|sync
// framing is shared with the other C2S structs (XI.Map.C2sPos owns the WriteHeader/ReadId helpers).

using System;

namespace XI.Map
{
    // GP_CLI_COMMAND_GROUP_COMLINK_ACTIVE_ACTIVEFLG. Hand-authored uint8_t literals; pinned vs ACTIVEFLG_*.
    public enum ComlinkActiveFlag : byte
    {
        Unequip = 0,          // ACTIVEFLG_Unequip=0
        EquipOrCreate = 1,    // ACTIVEFLG_EquipOrCreate=1
    }

    // GP_CLI_COMMAND_GROUP_COMLINK_ACTIVE_LINKSHELLID. Hand-authored uint8_t literals; pinned vs LINKSHELLID_*.
    public enum LinkshellId : byte
    {
        Linkshell1 = 1,       // LINKSHELLID_Linkshell1=1
        Linkshell2 = 2,       // LINKSHELLID_Linkshell2=2
    }

    public static class C2sComlinkActive
    {
        // GP_CLI_HEADER prefix (id:9|size:7|sync) -- same 4-byte framing as pos/recipe/lockstyle.
        public const int OffHeader = 0;         // header at struct start
        public const int HeaderSize = 4;        // HDR_SIZE=4
        public const int OffSync = 2;           // HDR_OFF_sync=2

        // Body (default packing). sizeof=28, alignof=2.
        public const int SizeOf = 28;           // CL_SIZE=28
        public const int AlignOf = 2;           // CL_ALIGN=2

        // The r/g/b/a nibble run occupies the 2-byte uint16 storage unit at bytes 4..5 (right after the
        // header). Sentinel-scan-derived positions from the oracle.
        public const int OffNibbles = 4;        // BF_*_off base: r/g in byte 4, b/a in byte 5
        public const int NibbleLowMask = 0x0F;  // a low-nibble field reads 0x0F (BF_r_val / BF_b_val)
        public const int NibbleHighMask = 0xF0; // a high-nibble field reads 0xF0 (BF_g_val / BF_a_val)

        public const int OffItemIndex = 6;      // OFF_CL.ItemIndex=6 (uint8)
        public const int OffCategory = 7;       // OFF_CL.Category=7 (uint8)
        public const int OffActiveFlg = 8;      // OFF_CL.ActiveFlg=8 (uint8)
        public const int OffPadding00 = 9;      // OFF_CL.padding00=9 (uint8[3])
        public const int Padding00Len = 3;      // CL_padding00_len=3
        public const int OffName = 12;          // OFF_CL.sComLinkName=12 (uint8[15])
        public const int NameLen = 15;          // CL_name_len=15
        public const int OffLinkshellId = 27;   // OFF_CL.LinkshellId=27 (uint8)

        // ---- r/g/b/a nibble accessors (byte 4 = r|g<<4, byte 5 = b|a<<4; masks from the oracle) ----
        public static byte GetR(ReadOnlySpan<byte> body) => (byte)(body[OffNibbles] & NibbleLowMask);
        public static byte GetG(ReadOnlySpan<byte> body) => (byte)(body[OffNibbles] >> 4);
        public static byte GetB(ReadOnlySpan<byte> body) => (byte)(body[OffNibbles + 1] & NibbleLowMask);
        public static byte GetA(ReadOnlySpan<byte> body) => (byte)(body[OffNibbles + 1] >> 4);

        // Write a single nibble, preserving the shared byte's other nibble (matches C++ bit-field assignment).
        public static void SetR(Span<byte> body, byte v) => body[OffNibbles] = (byte)((body[OffNibbles] & NibbleHighMask) | (v & NibbleLowMask));
        public static void SetG(Span<byte> body, byte v) => body[OffNibbles] = (byte)((body[OffNibbles] & NibbleLowMask) | ((v & NibbleLowMask) << 4));
        public static void SetB(Span<byte> body, byte v) => body[OffNibbles + 1] = (byte)((body[OffNibbles + 1] & NibbleHighMask) | (v & NibbleLowMask));
        public static void SetA(Span<byte> body, byte v) => body[OffNibbles + 1] = (byte)((body[OffNibbles + 1] & NibbleLowMask) | ((v & NibbleLowMask) << 4));

        // ---- uint8 scalar accessors ----------------------------------------------------------------
        public static byte GetItemIndex(ReadOnlySpan<byte> body) => body[OffItemIndex];
        public static void SetItemIndex(Span<byte> body, byte v) => body[OffItemIndex] = v;

        public static byte GetCategory(ReadOnlySpan<byte> body) => body[OffCategory];
        public static void SetCategory(Span<byte> body, byte v) => body[OffCategory] = v;

        public static byte GetActiveFlg(ReadOnlySpan<byte> body) => body[OffActiveFlg];
        public static void SetActiveFlg(Span<byte> body, byte v) => body[OffActiveFlg] = v;
        public static void SetActiveFlg(Span<byte> body, ComlinkActiveFlag f) => body[OffActiveFlg] = (byte)f;
        public static ComlinkActiveFlag GetComlinkActiveFlag(ReadOnlySpan<byte> body) => (ComlinkActiveFlag)body[OffActiveFlg];

        public static byte GetLinkshellId(ReadOnlySpan<byte> body) => body[OffLinkshellId];
        public static void SetLinkshellId(Span<byte> body, byte v) => body[OffLinkshellId] = v;
        public static void SetLinkshellId(Span<byte> body, LinkshellId id) => body[OffLinkshellId] = (byte)id;
        public static LinkshellId GetLinkshellIdEnum(ReadOnlySpan<byte> body) => (LinkshellId)body[OffLinkshellId];

        // ---- uint8[15] name field (fixed-width byte array) -----------------------------------------
        public static ReadOnlySpan<byte> GetName(ReadOnlySpan<byte> body) => body.Slice(OffName, NameLen);
    }
}
