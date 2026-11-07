// C# wire model for the Phase-3 map S2C BODY struct: GP_SERV_COMMAND_RECIPE::PacketData (0x031),
// src/map/packets/s2c/0x031_recipe.h. Body-only (the engine's data<T>() places PacketData at
// buffer_.data() + sizeof(GP_SERV_HEADER) == 4), matching the S2cPbxResult/S2cGroupEffects convention.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_s2c_recipe_union_harness.cpp -> map_s2c_recipe_union_golden.txt), NOT hand-computed.
//
// This is the SECOND Phase-3 body containing a UNION, and it exercises the union facts that chunk 15
// (0x04b, whose union was sized by its LARGER arm) could NOT:
//   - EQUAL-SIZED arms: both arms are size 48 / align 2, so the union is max(48,48) == 48.
//   - CROSS-ARM FIELD ALIASING: the two arms NAME DIFFERENT fields that share the SAME bytes. The union's
//     whole body is one 48-byte little-endian buffer that both arms overlay at offset 0:
//       RecipeDetails (GP_SERV_COMMAND_RECIPE_TYPE1_3, all uint16_t):
//         productitem@0 need_skill_1@2 need_skill_2@4 need_skill_3@6 need_item@8 need_key_item@10
//         itemnum[8]@12 (12..27) itemcount[8]@28 (28..43) Type@44 unknown32@46  -> 48/2
//       RecipeListArm (GP_SERV_COMMAND_RECIPE_TYPE2, all uint16_t):
//         unused04[6]@0 (0..11) itemnum[16]@12 (12..43) Type@44 itemnum_next@46  -> 48/2
//     Concrete aliases the gate proves byte-for-byte:
//       * Details.itemcount[i]@28+2i  ==  List.itemnum[8+i]@28+2i   (different names, SAME byte)
//       * Details.unknown32@46        ==  List.itemnum_next@46
//       * Details.Type@44             ==  List.Type@44              (both arms keep Type at 44)
//
// It is also the first body where a HAND-AUTHORED enum literal is a fixed-width STRUCT MEMBER with a
// compiler offset: the engine defines `enum class GP_SERV_COMMAND_RECIPE_TYPE : uint16_t` in THIS header
// (RecipeDetail1=1, RecipeList=2, RecipeDetail2=3, Unknown=4) and stores it in Type@44. magic_enum is not
// vendored, so the oracle re-declares the enum and prints (uint16_t)Enum::X for each enumerator; the gate
// asserts the C# RecipeType members against those compiler-emitted values.

using System;

namespace XI.Map
{
    // GP_SERV_COMMAND_RECIPE_TYPE (hand-authored uint16_t enum, used as the Type member). Values pinned vs
    // oracle ENUM_* lines (compiler can't reflect a bare literal's NAME -> the .h is the ground truth).
    public enum RecipeType : ushort
    {
        RecipeDetail1 = 1,   // ENUM_RecipeDetail1=1
        RecipeList    = 2,   // ENUM_RecipeList=2
        RecipeDetail2 = 3,   // ENUM_RecipeDetail2=3
        Unknown       = 4,   // ENUM_Unknown=4
    }

    // GP_SERV_COMMAND_RECIPE_TYPE1_3 (the Details union arm). sizeof=48, alignof=2; all uint16_t.
    // Offsets are arm-relative (== body-relative, since the arm aliases at body offset 0).
    public static class RecipeDetails
    {
        public const int SizeOf = 48;      // DET_SIZE=48
        public const int AlignOf = 2;      // DET_ALIGN=2
        public const int OffProductItem = 0;    // OFF_DET.productitem=0
        public const int OffNeedSkill1 = 2;     // OFF_DET.need_skill_1=2
        public const int OffNeedSkill2 = 4;     // need_skill_2@4
        public const int OffNeedSkill3 = 6;     // need_skill_3@6
        public const int OffNeedItem = 8;       // need_item@8
        public const int OffNeedKeyItem = 10;   // OFF_DET.need_key_item=10
        public const int OffItemNum = 12;       // OFF_DET.itemnum=12
        public const int ItemNumLen = 16;       // DET_itemnum_len=16
        public const int ItemNumCount = 8;      // DET_itemnum_count=8
        public const int OffItemCount = 28;     // OFF_DET.itemcount=28
        public const int ItemCountLen = 16;     // DET_itemcount_len=16
        public const int OffType = 44;          // OFF_DET.Type=44 (RecipeType)
        public const int OffUnknown32 = 46;     // OFF_DET.unknown32=46
    }

    // GP_SERV_COMMAND_RECIPE_TYPE2 (the List union arm). sizeof=48, alignof=2; all uint16_t.
    public static class RecipeListArm
    {
        public const int SizeOf = 48;      // LST_SIZE=48
        public const int AlignOf = 2;      // LST_ALIGN=2
        public const int OffUnused04 = 0;       // OFF_LST.unused04=0
        public const int Unused04Len = 12;      // LST_unused04_len=12 (uint16[6])
        public const int OffItemNum = 12;       // OFF_LST.itemnum=12
        public const int ItemNumLen = 32;       // LST_itemnum_len=32
        public const int ItemNumCount = 16;     // LST_itemnum_count=16
        public const int OffType = 44;          // OFF_LST.Type=44 (RecipeType)
        public const int OffItemNumNext = 46;   // OFF_LST.itemnum_next=46
    }

    // GP_SERV_COMMAND_RECIPE::PacketData = anonymous union { Details; List }. sizeof=48, alignof=2.
    // Both arms alias at body offset 0 (equal size 48), so a member of either arm is read directly from the
    // shared body at its arm-relative offset. Accessors are prefixed Details*/List* to name the reading arm;
    // because the bytes are shared, a value written through one arm's accessor is visible through the other.
    public static class S2cRecipe
    {
        public const int SizeOf = 48;          // RCP_SIZE=48
        public const int AlignOf = 2;          // RCP_ALIGN=2

        public const int OffDetails = 0;       // OFF_RCP.Details=0
        public const int OffList = 0;          // OFF_RCP.List=0 (aliases Details -- both arms @0, union)

        // ---- Details arm (RecipeDetails) accessors, base == OffDetails (== 0) ----------------------
        public static ushort GetDetailsProductItem(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffDetails + RecipeDetails.OffProductItem, 2));
        public static void SetDetailsProductItem(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffDetails + RecipeDetails.OffProductItem, 2), v);

        // need_skill_1/2/3 are consecutive uint16 at 2/4/6: index i -> 2 + 2i (i = 0..2).
        public static ushort GetDetailsNeedSkill(ReadOnlySpan<byte> body, int i) => BitConverter.ToUInt16(body.Slice(OffDetails + RecipeDetails.OffNeedSkill1 + i * 2, 2));
        public static void SetDetailsNeedSkill(Span<byte> body, int i, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffDetails + RecipeDetails.OffNeedSkill1 + i * 2, 2), v);

        public static ushort GetDetailsNeedItem(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffDetails + RecipeDetails.OffNeedItem, 2));
        public static ushort GetDetailsNeedKeyItem(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffDetails + RecipeDetails.OffNeedKeyItem, 2));

        public static int DetailsItemNumOffset(int i) => OffDetails + RecipeDetails.OffItemNum + i * 2;     // 12 + 2i
        public static ushort GetDetailsItemNum(ReadOnlySpan<byte> body, int i) => BitConverter.ToUInt16(body.Slice(DetailsItemNumOffset(i), 2));
        public static void SetDetailsItemNum(Span<byte> body, int i, ushort v) => BitConverter.TryWriteBytes(body.Slice(DetailsItemNumOffset(i), 2), v);

        public static int DetailsItemCountOffset(int i) => OffDetails + RecipeDetails.OffItemCount + i * 2; // 28 + 2i
        public static ushort GetDetailsItemCount(ReadOnlySpan<byte> body, int i) => BitConverter.ToUInt16(body.Slice(DetailsItemCountOffset(i), 2));
        public static void SetDetailsItemCount(Span<byte> body, int i, ushort v) => BitConverter.TryWriteBytes(body.Slice(DetailsItemCountOffset(i), 2), v);

        public static RecipeType GetDetailsType(ReadOnlySpan<byte> body) => (RecipeType)BitConverter.ToUInt16(body.Slice(OffDetails + RecipeDetails.OffType, 2));
        public static void SetDetailsType(Span<byte> body, RecipeType v) => BitConverter.TryWriteBytes(body.Slice(OffDetails + RecipeDetails.OffType, 2), (ushort)v);

        public static ushort GetDetailsUnknown32(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffDetails + RecipeDetails.OffUnknown32, 2));
        public static void SetDetailsUnknown32(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffDetails + RecipeDetails.OffUnknown32, 2), v);

        // ---- List arm (RecipeListArm) accessors, base == OffList (== 0) -----------------------------
        public static ushort GetListUnused04(ReadOnlySpan<byte> body, int i) => BitConverter.ToUInt16(body.Slice(OffList + RecipeListArm.OffUnused04 + i * 2, 2));
        public static void SetListUnused04(Span<byte> body, int i, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffList + RecipeListArm.OffUnused04 + i * 2, 2), v);

        public static int ListItemNumOffset(int i) => OffList + RecipeListArm.OffItemNum + i * 2;           // 12 + 2i
        public static ushort GetListItemNum(ReadOnlySpan<byte> body, int i) => BitConverter.ToUInt16(body.Slice(ListItemNumOffset(i), 2));
        public static void SetListItemNum(Span<byte> body, int i, ushort v) => BitConverter.TryWriteBytes(body.Slice(ListItemNumOffset(i), 2), v);

        public static RecipeType GetListType(ReadOnlySpan<byte> body) => (RecipeType)BitConverter.ToUInt16(body.Slice(OffList + RecipeListArm.OffType, 2));
        public static void SetListType(Span<byte> body, RecipeType v) => BitConverter.TryWriteBytes(body.Slice(OffList + RecipeListArm.OffType, 2), (ushort)v);

        public static ushort GetListItemNumNext(ReadOnlySpan<byte> body) => BitConverter.ToUInt16(body.Slice(OffList + RecipeListArm.OffItemNumNext, 2));
        public static void SetListItemNumNext(Span<byte> body, ushort v) => BitConverter.TryWriteBytes(body.Slice(OffList + RecipeListArm.OffItemNumNext, 2), v);
    }
}
