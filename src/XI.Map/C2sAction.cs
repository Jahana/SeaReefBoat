// C# wire model for the Phase-3 map C2S body struct GP_CLI_COMMAND_ACTION (0x01a),
// src/map/packets/c2s/0x01a_action.h via the GP_CLI_PACKET(...) macro. FIFTH Phase-3 union body and the
// archetypal TAGGED (discriminated) union: an ActionID tag + a 9-arm payload union. Offsets are HEADER-INCLUSIVE
// (the 4-byte GP_CLI_HEADER is bytes 0..3), matching C2sPos/C2sAuc/C2sExtendedJob.
//
// CRITICAL METHODOLOGY: every offset/size below is copied from the compiled MSVC layout oracle
// (csharp/oracle/map_c2s_action_union_harness.cpp -> map_c2s_action_union_golden.txt), NOT hand-computed.
//
// New facts this body pins (no prior union chunk 15/16/17/18/19 showed them):
//   - The FIRST FLOAT in a Phase-3 UNION ARM: ACTIONBUF_CASTMAGIC = { u32 SpellId@0; float PosX@4; float PosZ@8;
//     float PosY@12 } -> 16/4 (the pos C2S had floats in a plain body, never inside a union).
//   - FLOAT <-> u32 TYPE-PUNNING across arms: the raw uint32_t ActionBuf[4] arm views the SAME bytes as the
//     floats, so ActionBuf[1] (body offset 16) == the IEEE-754 bits of PosX -- the compiler emits PUN_PosX==PUN_Buf1==16.
//   - TYPEDEF-SHARED ARM TYPES: ACTIONBUF_JOBABILITY and ACTIONBUF_MONSTERSKILL are typedefs of _WEAPONSKILL, so
//     3 of the 9 arms are the SAME struct type at the SAME offsets (SkillId@0): write one, read the others.
//   - A 23-member uint16_t enum (GP_CLI_COMMAND_ACTION_ACTIONID) used as a fixed-width struct MEMBER ActionID@10 --
//     the largest enum-as-member set pinned so far. Plus four uint32_t status enums whose StatusId IS an arm member.
//   - Layout: UniqueNo@4 ActIndex@8 ActionID@10 union@12 -> ACT_SIZE=28, ACT_ALIGN=4. NO pad before the union
//     (12 already 4-aligned); the union is the LAST member -> NO tail after it (the chunk-17 case is absent here).

using System;

namespace XI.Map
{
    // GP_CLI_COMMAND_ACTION_ACTIONID (0x01a_action.h). Hand-authored uint16_t tag literals; pinned vs AID_* lines.
    // It is the DISCRIMINANT of the tagged union -- the value selects which payload arm below is meaningful.
    public enum ActionId : ushort
    {
        Talk          = 0x00,   // AID_Talk=0
        Attack        = 0x02,   // AID_Attack=2
        CastMagic     = 0x03,   // AID_CastMagic=3
        AttackOff     = 0x04,   // AID_AttackOff=4
        Help          = 0x05,   // AID_Help=5
        Weaponskill   = 0x07,   // AID_Weaponskill=7
        JobAbility    = 0x09,   // AID_JobAbility=9
        HomepointMenu = 0x0B,   // AID_HomepointMenu=11
        Assist        = 0x0C,   // AID_Assist=12
        RaiseMenu     = 0x0D,   // AID_RaiseMenu=13
        Fish          = 0x0E,   // AID_Fish=14
        ChangeTarget  = 0x0F,   // AID_ChangeTarget=15
        Shoot         = 0x10,   // AID_Shoot=16
        ChocoboDig    = 0x11,   // AID_ChocoboDig=17
        Dismount      = 0x12,   // AID_Dismount=18
        TractorMenu   = 0x13,   // AID_TractorMenu=19
        SendResRdy    = 0x14,   // AID_SendResRdy=20
        Quarry        = 0x15,   // AID_Quarry=21
        Sprint        = 0x16,   // AID_Sprint=22
        Scout         = 0x17,   // AID_Scout=23
        Blockaid      = 0x18,   // AID_Blockaid=24
        MonsterSkill  = 0x19,   // AID_MonsterSkill=25
        Mount         = 0x1A,   // AID_Mount=26
    }

    // The four uint32_t status enums (used as each menu/blockaid arm's StatusId member; literals pinned vs the oracle).
    public enum BlockaidStatus : uint { Disable = 0, Enable = 1, Toggle = 2 }              // BLKAID_*
    public enum HomepointMenuStatus : uint { Accept = 0, MonstrosityCancel = 1, MonstrosityRetry = 2 } // HPMENU_*
    public enum RaiseMenuStatus : uint { Accept = 0, Reject = 1 }                          // RAISE_*
    public enum TractorMenuStatus : uint { Accept = 0, Reject = 1 }                        // TRAC_*

    // ACTIONBUF_CASTMAGIC (union arm; the only arm with FLOATS). 16/4. Offsets arm-relative.
    public static class ActionBufCastMagic
    {
        public const int SizeOf = 16;        // CAST_SIZE=16
        public const int AlignOf = 4;        // CAST_ALIGN=4
        public const int OffSpellId = 0;     // OFF_CAST.SpellId=0 (u32)
        public const int OffPosX = 4;        // OFF_CAST.PosX=4 (float)
        public const int OffPosZ = 8;        // OFF_CAST.PosZ=8 (float)
        public const int OffPosY = 12;       // OFF_CAST.PosY=12 (float)
    }

    // ACTIONBUF_WEAPONSKILL -- also ACTIONBUF_JOBABILITY and ACTIONBUF_MONSTERSKILL are typedefs of THIS type,
    // so three union arms share one descriptor at identical offsets. 16/4. Offsets arm-relative.
    public static class ActionBufWeaponskill
    {
        public const int SizeOf = 16;        // WSKILL_SIZE=16 (== JA_SIZE==MS_SIZE)
        public const int AlignOf = 4;        // WSKILL_ALIGN=4
        public const int OffSkillId = 0;     // OFF_WSKILL.SkillId=0 (u32; same offset for the three typedef arms)
        public const int OffUnused = 4;      // u32[2]
        public const int OffUnknown = 12;    // OFF_WSKILL.unknown=12 (u32)
    }

    // ACTIONBUF_{HOMEPOINTMENU,TRACTORMENU,BLOCKAID} -- all { u32-enum StatusId@0; u32 unused[2]@4; u32 unknown@12 }.
    public static class ActionBufMenu
    {
        public const int SizeOf = 16;        // HPMENU_SIZE=16
        public const int AlignOf = 4;        // HPMENU_ALIGN=4
        public const int OffStatusId = 0;    // OFF_HPMENU.StatusId=0 (uint32-backed enum)
        public const int OffUnknown = 12;    // u32
    }

    // ACTIONBUF_MOUNT -- { u32 MountId@0; u32 unused[2]@4; u32 unknown@12 }. 16/4.
    public static class ActionBufMount
    {
        public const int SizeOf = 16;        // MOUNT_SIZE=16
        public const int AlignOf = 4;
        public const int OffMountId = 0;     // OFF_MOUNT.MountId=0 (u32)
    }

    // GP_CLI_COMMAND_ACTION (outer, header-inclusive, default packing). size=28, align=4.
    // The 9-arm payload union starts at OffUnion==12 (all arms alias there) and is the LAST member (no tail).
    public static class C2sAction
    {
        // GP_CLI_HEADER prefix (id:9|size:7|sync) -- same 4-byte framing as the other C2S structs.
        public const int OffHeader = 0;       // OFF_ACT.header=0
        public const int HeaderSize = 4;      // CHDR_SIZE=4
        public const int OffSync = 2;

        public const int SizeOf = 28;         // ACT_SIZE=28
        public const int AlignOf = 4;         // ACT_ALIGN=4

        public const int OffUniqueNo = 4;     // OFF_ACT.UniqueNo=4 (u32)
        public const int OffActIndex = 8;     // OFF_ACT.ActIndex=8 (u16)
        public const int OffActionID = 10;    // OFF_ACT.ActionID=10 (u16 ActionId tag)
        public const int OffUnion = 12;       // union start (12 already 4-aligned after ActionID@10+2 -> no pad)

        // every arm aliases at the union start (the union fact):
        public const int OffCastMagic = 12;    // OFF_ACT.CastMagic=12
        public const int OffWeaponskill = 12;  // OFF_ACT.Weaponskill=12 (== JobAbility == MonsterSkill -- typedef-shared)
        public const int OffJobAbility = 12;
        public const int OffMonsterSkill = 12;
        public const int OffHomepointMenu = 12;
        public const int OffTractorMenu = 12;
        public const int OffBlockAid = 12;
        public const int OffMount = 12;
        public const int OffActionBuf = 12;    // raw uint32[4] arm -- TYPE-PUNS the float arms at the same bytes

        // nested-inclusive offset helpers (union start + arm-relative field):
        public static int CastField(int off) => OffCastMagic + off;
        public static int WeaponField(int off) => OffWeaponskill + off;
        public static int MenuField(int off) => OffHomepointMenu + off;
        public static int ActionBufOffset(int i) => OffActionBuf + i * 4;

        public static int PosXOffset() => CastField(ActionBufCastMagic.OffPosX);       // 16 == PUN_PosX
        public static int PosZOffset() => CastField(ActionBufCastMagic.OffPosZ);        // 20
        public static int PosYOffset() => CastField(ActionBufCastMagic.OffPosY);        // 24
        public static int ActionBuf1Offset() => ActionBufOffset(1);                     // 16 == PUN_Buf1 (aliases PosX)

        // ---- leading scalars (little-endian) ------------------------------------------------------
        public static uint GetUniqueNo(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(OffUniqueNo, 4));
        public static void SetUniqueNo(Span<byte> b, uint v) => BitConverter.TryWriteBytes(b.Slice(OffUniqueNo, 4), v);
        public static ushort GetActIndex(ReadOnlySpan<byte> b) => BitConverter.ToUInt16(b.Slice(OffActIndex, 2));
        public static void SetActIndex(Span<byte> b, ushort v) => BitConverter.TryWriteBytes(b.Slice(OffActIndex, 2), v);
        public static ActionId GetActionID(ReadOnlySpan<byte> b) => (ActionId)BitConverter.ToUInt16(b.Slice(OffActionID, 2));
        public static void SetActionID(Span<byte> b, ActionId v) => BitConverter.TryWriteBytes(b.Slice(OffActionID, 2), (ushort)v);

        // ---- CastMagic arm (with the FLOATs) ------------------------------------------------------
        public static uint GetSpellId(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(CastField(ActionBufCastMagic.OffSpellId), 4));
        public static void SetSpellId(Span<byte> b, uint v) => BitConverter.TryWriteBytes(b.Slice(CastField(ActionBufCastMagic.OffSpellId), 4), v);
        public static float GetPosX(ReadOnlySpan<byte> b) => BitConverter.ToSingle(b.Slice(PosXOffset(), 4));
        public static void SetPosX(Span<byte> b, float v) => BitConverter.TryWriteBytes(b.Slice(PosXOffset(), 4), v);
        public static float GetPosZ(ReadOnlySpan<byte> b) => BitConverter.ToSingle(b.Slice(PosZOffset(), 4));
        public static void SetPosZ(Span<byte> b, float v) => BitConverter.TryWriteBytes(b.Slice(PosZOffset(), 4), v);
        public static float GetPosY(ReadOnlySpan<byte> b) => BitConverter.ToSingle(b.Slice(PosYOffset(), 4));
        public static void SetPosY(Span<byte> b, float v) => BitConverter.TryWriteBytes(b.Slice(PosYOffset(), 4), v);

        // ---- Weaponskill / JobAbility / MonsterSkill (typedef-shared: same bytes, same offsets) -----
        public static uint GetSkillId(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(WeaponField(ActionBufWeaponskill.OffSkillId), 4));
        public static void SetSkillId(Span<byte> b, uint v) => BitConverter.TryWriteBytes(b.Slice(WeaponField(ActionBufWeaponskill.OffSkillId), 4), v);
        public static uint GetWeaponskillUnknown(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(WeaponField(ActionBufWeaponskill.OffUnknown), 4));

        // ---- menu arms (StatusId is a uint32-backed enum) ------------------------------------------
        public static HomepointMenuStatus GetHomepointMenuStatus(ReadOnlySpan<byte> b) => (HomepointMenuStatus)BitConverter.ToUInt32(b.Slice(MenuField(ActionBufMenu.OffStatusId), 4));
        public static void SetHomepointMenuStatus(Span<byte> b, HomepointMenuStatus v) => BitConverter.TryWriteBytes(b.Slice(MenuField(ActionBufMenu.OffStatusId), 4), (uint)v);
        public static TractorMenuStatus GetTractorMenuStatus(ReadOnlySpan<byte> b) => (TractorMenuStatus)BitConverter.ToUInt32(b.Slice(OffTractorMenu + ActionBufMenu.OffStatusId, 4));
        public static BlockaidStatus GetBlockAidStatus(ReadOnlySpan<byte> b) => (BlockaidStatus)BitConverter.ToUInt32(b.Slice(OffBlockAid + ActionBufMenu.OffStatusId, 4));
        public static void SetBlockAidStatus(Span<byte> b, BlockaidStatus v) => BitConverter.TryWriteBytes(b.Slice(OffBlockAid + ActionBufMenu.OffStatusId, 4), (uint)v);

        // ---- Mount arm ----------------------------------------------------------------------------
        public static uint GetMountId(ReadOnlySpan<byte> b) => BitConverter.ToUInt32(b.Slice(OffMount + ActionBufMount.OffMountId, 4));

        // ---- raw ActionBuf[4] arm (TYPE-PUNS the structured/float arms at the same bytes) ----------
        public static uint GetActionBuf(ReadOnlySpan<byte> b, int i) => BitConverter.ToUInt32(b.Slice(ActionBufOffset(i), 4));
        public static void SetActionBuf(Span<byte> b, int i, uint v) => BitConverter.TryWriteBytes(b.Slice(ActionBufOffset(i), 4), v);
    }
}
