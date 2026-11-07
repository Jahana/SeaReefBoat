// Dependency-free parity verifier for the Phase-3 map packet HEADER framing in XI.Map.
// Replays the compiled MSVC oracle's map_packet_golden.txt (produced by map_packet_harness.cpp)
// through the C# BasicPacket accessors and asserts byte-for-byte equality of the 4 header bytes
// plus the getType/getSize/getSequence readback, for every HDR / HDRSWAP / HDRSEQ row, and the
// PACKET_SIZE / ENTITYUPDATE constants. Any mismatch => exit code 1. This is the Phase-3 header
// go/no-go evidence: the bit-packing is compiler-derived (opcode bit-8 overflows into byte-1 bit-0,
// the ((size+3)&~3)/2 size rounding uint8-truncates above 508, and setType/setSize have disjoint
// bit ranges so they commute) -- NOT hand-reasoned.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

internal static class Program
{
    static int _pass, _fail;

    static int Main(string[] argv)
    {
        string goldenPath = argv.Length > 0
            ? argv[0]
            : @"H:\023-FFXI-server\csharp\oracle\map_packet_golden.txt";

        if (!System.IO.File.Exists(goldenPath))
        {
            Console.Error.WriteLine($"golden file not found: {goldenPath}");
            return 2;
        }

        Console.WriteLine("== Map packet header framing parity: C# XI.Map.BasicPacket vs compiler oracle ==");

        // Constants first.
        Check("PACKET_SIZE", Val(GetLine(goldenPath, "PACKET_SIZE=")), XI.Map.BasicPacket.PacketSize.ToString());
        Check("ENTITY_SPAWN", Val(GetLine(goldenPath, "ENTITY_SPAWN=")), ((int)XI.Map.EntityUpdate.Spawn).ToString());
        Check("ENTITY_SHOW", Val(GetLine(goldenPath, "ENTITY_SHOW=")), ((int)XI.Map.EntityUpdate.Show).ToString());
        Check("ENTITY_UPDATE", Val(GetLine(goldenPath, "ENTITY_UPDATE=")), ((int)XI.Map.EntityUpdate.Update).ToString());
        Check("ENTITY_HIDE", Val(GetLine(goldenPath, "ENTITY_HIDE=")), ((int)XI.Map.EntityUpdate.Hide).ToString());
        Check("ENTITY_DESPAWN", Val(GetLine(goldenPath, "ENTITY_DESPAWN=")), ((int)XI.Map.EntityUpdate.Despawn).ToString());

        string[] lines = System.IO.File.ReadAllLines(goldenPath);
        int hdr = 0, swap = 0, seq = 0;
        foreach (string raw in lines)
        {
            if (raw.StartsWith("HDR ", StringComparison.Ordinal))
            {
                // HDR op=<u> size=<u> <8hex> type=<u> size=<u>
                int op = (int)LongField(raw, "op=");
                uint size = (uint)LongField(raw, "size=");
                string expBytes = Field(raw, 3);
                long expType = LongField(raw, "type=");
                long expSize = LongField2(raw, "size=");
                var p = new XI.Map.BasicPacket();
                p.SetOpcode((ushort)op);
                p.SetSize(size);
                AssertHeader($"HDR[{hdr}] op{op} size{size}", p, expBytes);
                Check($"HDR[{hdr}] getOpcode op{op} size{size}", expType.ToString(), p.GetOpcode().ToString());
                Check($"HDR[{hdr}] getSize op{op} size{size}", expSize.ToString(), p.GetSize().ToString());
                hdr++;
            }
            else if (raw.StartsWith("HDRSWAP ", StringComparison.Ordinal))
            {
                // setSize before setType -- must commute with the setType-first rows.
                int op = (int)LongField(raw, "op=");
                uint size = (uint)LongField(raw, "size=");
                string expBytes = Field(raw, 3);
                long expType = LongField(raw, "type=");
                long expSize = LongField2(raw, "size=");
                var p = new XI.Map.BasicPacket();
                p.SetSize(size);
                p.SetOpcode((ushort)op);
                AssertHeader($"HDRSWAP[{swap}] op{op} size{size}", p, expBytes);
                Check($"HDRSWAP[{swap}] getOpcode op{op} size{size}", expType.ToString(), p.GetOpcode().ToString());
                Check($"HDRSWAP[{swap}] getSize op{op} size{size}", expSize.ToString(), p.GetSize().ToString());
                swap++;
            }
            else if (raw.StartsWith("HDRSEQ ", StringComparison.Ordinal))
            {
                // HDRSEQ op=<u> seq=<u> <8hex> type=<u> seq=<u>
                int op = (int)LongField(raw, "op=");
                int sq = (int)LongField(raw, "seq=");
                string expBytes = Field(raw, 3);
                long expType = LongField(raw, "type=");
                long expSeq = LongField2(raw, "seq=");
                var p = new XI.Map.BasicPacket();
                p.SetOpcode((ushort)op);
                p.SetSequence((ushort)sq);
                AssertHeader($"HDRSEQ[{seq}] op{op} seq{sq}", p, expBytes);
                Check($"HDRSEQ[{seq}] getOpcode op{op} seq{sq}", expType.ToString(), p.GetOpcode().ToString());
                Check($"HDRSEQ[{seq}] getSequence op{op} seq{sq}", expSeq.ToString(), p.GetSequence().ToString());
                seq++;
            }
        }

        // Packet-id enums: magic_enum is NOT vendored in this tree, so no compiled oracle is
        // possible -- and it isn't needed, because every enumerator is an explicit hand-authored
        // uint16_t literal (the iron rule covers compiler-derived sizeof/offsetof/crypto bytes, not
        // literals). So instead of an oracle, independently re-parse the engine .h here (a different
        // implementation from the Python generator) and cross-check against the reflection of the
        // generated XI.Map enums: agreement proves the committed C# matches the engine source, and a
        // future .h edit without regeneration fails this gate.
        string enumsDir = argv.Length > 1
            ? argv[1]
            : @"H:\023-FFXI-server\server\src\map\enums";
        VerifyPacketIds("PacketC2S", System.IO.Path.Combine(enumsDir, "packet_c2s.h"), typeof(XI.Map.PacketC2S));
        VerifyPacketIds("PacketS2C", System.IO.Path.Combine(enumsDir, "packet_s2c.h"), typeof(XI.Map.PacketS2C));

        // Phase-3 first S2C body struct: 0x076 GP_SERV_COMMAND_GROUP_EFFECTS, verified against the
        // compiled layout oracle (its own golden file, sibling of the header golden).
        VerifyMapGroupEffects(System.IO.Path.GetDirectoryName(goldenPath));

        // Phase-3 first C2S body struct: 0x015 GP_CLI_COMMAND_POS (default packing + bit-field run).
        VerifyMapC2sPos(System.IO.Path.GetDirectoryName(goldenPath));

        // Phase-3 second C2S body struct: 0x053 GP_CLI_COMMAND_LOCKSTYLE (nested-items array).
        VerifyMapC2sLockstyle(System.IO.Path.GetDirectoryName(goldenPath));

        // Phase-3 third C2S body struct: 0x058 GP_CLI_COMMAND_RECIPE (mid-struct uint64 alignment).
        VerifyMapC2sRecipe(System.IO.Path.GetDirectoryName(goldenPath));

        // Phase-3 chunk 7: three simple fixed-width C2S body structs (0x0e7 reqlogout, 0x0ea sit,
        // 0x0f1 buffcancel) verified against one shared compiled layout oracle.
        VerifyMapC2sNumeric(System.IO.Path.GetDirectoryName(goldenPath));

        // Phase-3 chunk 8: four more fixed-width C2S body structs (0x059 effectend -- first byte-array
        // field, 0x0e8 camp, 0x0f0 rescue, 0x0f2 submapchange) verified against a second shared oracle.
        VerifyMapC2sNumeric2(System.IO.Path.GetDirectoryName(goldenPath));

        // Phase-3 chunk 9: first default-packed S2C body structs (0x05c pendingnum -- int32[8] array,
        // 0x060 musicvolume, 0x052 eventucoff) verified against a shared S2C layout oracle.
        VerifyMapS2cNumeric(System.IO.Path.GetDirectoryName(goldenPath));

        // Phase-3 chunk 10: S2C bodies with a fixed-width char[] string field (0x009 message -- char[128]
        // at UNALIGNED offset 9 -> 137 member span padded to 140; 0x05d pendingstr -- int32[9]+4 char[16]).
        VerifyMapS2cCharArray(System.IO.Path.GetDirectoryName(goldenPath));

        // Phase-3 chunk 11: 0x00b GP_SERV_COMMAND_LOGOUT (first S2C body with a NESTED fixed-width struct
        // member -- GP_SERV_LOGOUTSUB re-aligns to offset 4, 3 pad bytes) + 0x011 zone_transition (scalar C2S).
        VerifyMapLogout(System.IO.Path.GetDirectoryName(goldenPath));

        // Phase-3 chunk 12: 0x0c4 GP_CLI_COMMAND_GROUP_COMLINK_ACTIVE -- first multi-nibble bit-field run
        // (four uint16:4 r/g/b/a fill bytes 4..5), sentinel-scan-derived by the oracle.
        VerifyMapC2sComlinkActive(System.IO.Path.GetDirectoryName(goldenPath));

        // Phase-3 chunk 13: 0x052 GP_CLI_COMMAND_EQUIPSET_CHECK -- a nested struct (equipsetrequestitem_t)
        // whose byte 0 is a sentinel-scanned bit-field run and whose uint16 realigns, embedded BOTH as a
        // single member AND a 16-element array (stride 4, no inter-element padding).
        VerifyMapC2sEquipsetCheck(System.IO.Path.GetDirectoryName(goldenPath));

        // Phase-3 chunk 14: 0x0d3 GP_CLI_COMMAND_FAQ_GMCALL -- first STRADDLING bit-field run (six uint16
        // fields across bytes 4..7 spanning two uint16 allocation units) + first GP_CLI_PACKET_VLA body
        // (getMinSize() == offsetof(Data) == 8), both sentinel-scan-derived by the oracle.
        VerifyMapC2sFaqGmcall(System.IO.Path.GetDirectoryName(goldenPath));

        // Phase-3 chunk 15: 0x04b GP_SERV_COMMAND_PBX_RESULT -- first UNION body (anonymous union whose size
        // is the LARGER State arm -> alias Represent/State @12, PBX_SIZE=84; GC_PBOX nested union, both arms
        // alias @0; GP_POST_BOX_STATE embeds a GC_PBOX by value). Compiled layout oracle.
        VerifyMapS2cUnionPbx(System.IO.Path.GetDirectoryName(goldenPath));

        // Phase-3 chunk 16: 0x031 GP_SERV_COMMAND_RECIPE -- the second UNION body, exercising the facts pbx
        // could not: EQUAL-SIZED arms (Details/Type1_3 and List/Type2 both 48/align 2) so the union is 48, and
        // CROSS-ARM FIELD ALIASING (Details.itemcount[i] shares bytes with List.itemnum[8+i]; Details.unknown32
        // aliases List.itemnum_next; both arms keep Type@44). Also pins a self-contained hand-authored enum
        // (GP_SERV_COMMAND_RECIPE_TYPE) that is a real struct MEMBER (Type@44), literals vs compiler. Compiled oracle.
        VerifyMapS2cRecipeUnion(System.IO.Path.GetDirectoryName(goldenPath));

        // Phase-3 chunk 17: 0x075 GP_SERV_COMMAND_BATTLEFIELD -- the densest UNION body yet: a THREE-ARM union
        // (u8 Data[128] / scoreboard_data_t / progressbar_data_t) all alias @36, sized by the LARGEST arm (128)
        // and NOT at the struct end -- two uint16 tail members follow, so MesNumTitle lands at 36+128=164. One arm
        // is a nested struct (Scoreboard, +padding[96]), another holds a nested STRUCT ARRAY (Rows[6], stride 20)
        // + a trailing u32. Plus a self-contained uint8 bit-flag enum (OBJECTIVEUTILITY_TYPE) pinned vs compiler.
        VerifyMapS2cBattlefield(System.IO.Path.GetDirectoryName(goldenPath));

        // Phase-3 chunk 18: 0x04e GP_CLI_COMMAND_AUC -- FIRST C2S body containing a UNION (six-arm GP_AUC_PARAM).
        // New union facts: SIZE == max arm (12) AND ALIGN == max arm align (4, though Trans is align 2); two
        // 6-member-byte arms self-pad to 8 by their own align-4; a self-contained uint8 enum as the leading
        // scalar MEMBER (Command@4); trailing nested struct GP_AUC_BOX (40/4) at Parcel@20 with a u32 Price@24
        // that needs NO pad (already 4-aligned). Compiled layout oracle, header-inclusive offsets.
        VerifyMapC2sAucUnion(System.IO.Path.GetDirectoryName(goldenPath));

        // Phase-3 chunk 19: 0x102 GP_CLI_COMMAND_EXTENDED_JOB -- fourth UNION body (C2S), the first with a
        // BIT-FIELD STRUCT nested INSIDE a union arm (mon_flags0_t's eight uint8_t:1 fields at Flags0, reached at
        // body offset 10; masks/shifts sentinel-scan-derived since offsetof is illegal on bit-fields). Union arms
        // are EQUAL size (all 160) but UNEQUAL align (blu 1 / pup 1 / mon 2) -> union align == max == 2 (distinct
        // from AUC's mixed-size). Cross-arm alias at body offset 12: BLU.Spells[0] == PUP.Slots[0] == MON.SpeciesIndex
        // (u16). Plus a 14-member uint8 enum (AutomatonSlot) pinned vs compiler. Compiled oracle, header-inclusive.
        VerifyMapC2sExtJobUnion(System.IO.Path.GetDirectoryName(goldenPath));

        // Phase-3 chunk 20: 0x01a GP_CLI_COMMAND_ACTION -- fifth UNION body (C2S), the archetypal TAGGED union.
        // First FLOAT inside a union arm (ACTIONBUF_CASTMAGIC { u32 SpellId; float PosX/PosZ/PosY }) and the first
        // FLOAT<->u32 TYPE-PUN across arms: the raw ActionBuf[4] arm views the SAME bytes, so ActionBuf[1]@16 ==
        // the IEEE-754 bits of PosX@16 (compiler PUN_PosX==PUN_Buf1==16). 3 of the 9 arms are typedef-shared types
        // (Weaponskill==JobAbility==MonsterSkill). 23-member uint16 ActionID tag as struct member ActionID@10 +
        // four uint32 status enums. Layout UniqueNo@4/ActIndex@8/ActionID@10/union@12 -> 28/4, no pad before the
        // union and NO tail after it. Compiled oracle, header-inclusive; all expected derived from the golden.
        VerifyMapC2sActionUnion(System.IO.Path.GetDirectoryName(goldenPath));

        // Phase-3 chunk 21: 0x06f GROUP_LEAVE + 0x070 GROUP_BREAKUP -- FIRST engine-enum body port. Both are
        // GP_CLI_PACKET(name, PartyKind Kind;) where PartyKind (enum class : uint8_t { Party=0, Alliance=5 }) is
        // DEFINED IN A SEPARATE HEADER (src/map/enums/party_kind.h), forward-declared in the packet .h. The
        // compiled layout oracle only needs the enum's UNDERLYING type (the oracle proves a scoped enum member is
        // byte-identical to a uint8_t member: EQ_SIZE/ALIGN/OFFSET all 1), so both structs are 6/2 with Kind@4;
        // the enum LITERALS are pinned separately by an independent re-parse of party_kind.h (the chunk-2
        // packet-id technique) since magic_enum isn't vendored. This unblocks the ~50 deferred engine-enum bodies.
        VerifyMapC2sGroupParty(System.IO.Path.GetDirectoryName(goldenPath), System.IO.Path.Combine(enumsDir, "party_kind.h"));

        // Phase-3 chunk 22: 0x0dc GP_SERV_COMMAND_GROUP_SOLICIT_REQ -- SECOND engine-enum body and the first to
        // COMBINE three previously-isolated techniques in one struct: (1) the SAME separate-header PartyKind enum
        // (Kind@7, underlying uint8) whose layout-only-needs-underlying-type invariant chunk 21 proved is re-confirmed
        // in the S2C default-packing context via a byte-identical uint8_t twin (EQ_SIZE/ALIGN/KIND_OFFSET all 1);
        // (2) a char[16] NUL-terminated string field (sName@8), same accessor pattern as message/pendingstr; (3) a
        // CONSECUTIVE byte-run AnonFlag@6 + Kind@7 (both alignof 1, no realign between them) with RaceNo realigning
        // to 24 after sName. Layout UniqueNo@0/ActIndex@4/AnonFlag@6/Kind@7/sName[16]@8/RaceNo@24/padding1E[2]@26 ->
        // 28/align4 (26+2 no tail pad). Literals re-parsed from party_kind.h (the chunk-21 enum, reused unchanged).
        VerifyMapS2cGroupSolicitReq(System.IO.Path.GetDirectoryName(goldenPath), System.IO.Path.Combine(enumsDir, "party_kind.h"));

        // Phase-3 chunk 23: 0x0c8 GP_SERV_COMMAND_GROUP_TBL -- THIRD engine-enum body and the layout-DENSEST S2C
        // struct yet: it combines the reused PartyKind enum (Kind@0, the SAME chunk-21/22 engine enum, not
        // redefined) with a NESTED STRUCT ARRAY (GroupTbl[20] of GROUP_TBL, stride 12 no inter-element pad, starts
        // at body offset 4) that puts a SENTINEL-SCAN BIT-FIELD RUN INSIDE EACH ARRAY ELEMENT -- the genuinely-new
        // hazard: 7 uint8_t bit-fields (PartyNo:2 then six :1) whose widths sum to EXACTLY 8 -> all pack into ONE
        // byte at element offset 6, including the first 2-bit-width field (PartyNo mask 0x03) among 1-bit
        // neighbours; a realigning uint16 ZoneNo@8 follows padding07@7. GroupTbl entry 12/4; outer PacketData 244/4.
        // Layout oracle needs only the enum's underlying type (chunk-21 invariant); literals re-parsed from party_kind.h.
        VerifyMapS2cGroupTbl(System.IO.Path.GetDirectoryName(goldenPath), System.IO.Path.Combine(enumsDir, "party_kind.h"));

        // Phase-3 chunk 24: 0x029 GP_SERV_COMMAND_BATTLE_MESSAGE -- FOURTH engine-enum body and the first with a
        // UINT16-BACKED enum member (MsgStd : uint16_t, ~140 literals in src/map/enums/msg_std.h) extending the
        // chunk-21 underlying-type-substitution invariant to a NEW width: the oracle proves a uint16_t twin of the
        // whole struct is byte-identical to the MsgStd-member struct (BMQ_SIZE_EQ/ALIGN_EQ/MSGOFF_EQ all 1; twin
        // 24/4 MessageNum@20). Layout (body-relative): UniqueNoCas@0/UniqueNoTar@4/Data@8/Data2@12/ActIndexCas@16/
        // ActIndexTar@18/MessageNum@20(MsgStd/u16, packs tight after ActIndexTar, 20 already 2-aligned)/Type@22/
        // padding1B@23 -> 24/4 (no tail pad). The enum NAME->literal set is a .h-re-parse concern (compiler can't
        // reflect it, magic_enum not vendored); the gate validates the independent msg_std.h re-parse against the
        // compiler's underlying-cast for 15 representative enumerators across the value range.
        VerifyMapS2cBattleMessage(System.IO.Path.GetDirectoryName(goldenPath), System.IO.Path.Combine(enumsDir, "msg_std.h"));

        // Phase-3 chunk 25: 0x038 GP_SERV_COMMAND_SCHEDULOR -- FIFTH engine-enum body and the FIRST with a
        // UINT32-BACKED enum member (enum class FourCC : uint32_t, src/map/enums/four_cc.h, 26 HEX literals),
        // COMPLETING the chunk-21 underlying-type-substitution matrix across all three common widths: chunks
        // 21/22/23 proved uint8 (PartyKind), chunk 24 proved uint16 (MsgStd), this oracle proves uint32 -- a
        // uint32_t twin of the whole struct is byte-identical in size/align/id-offset (SCQ_SIZE_EQ/ALIGN_EQ/
        // IDOFF_EQ all 1; twin 16/4 id@8). Distinct new packing fact: the u32-backed enum `id` sits between two
        // u32s and TWO u16s (UniqueNoCas@0/UniqueNoTar@4/id@8/ActIndexCast@12/ActIndexTar@14 -> 16/4, no tail
        // pad) and is itself the 4-aligner; and it's the first engine enum whose values exceed int32 (e.g.
        // BasicAttack=0x306B7461), so the oracle prints (uint32_t) and the .h re-parse exercises the HEX path.
        // C# ports the full 26-member FourCC enum, cross-checked 3 ways (compiler underlying-cast == .h re-parse
        // == C# enum). Compiled layout oracle, S2C body-relative; every expected read from the golden.
        VerifyMapS2cSchedulor(System.IO.Path.GetDirectoryName(goldenPath), System.IO.Path.Combine(enumsDir, "four_cc.h"));

        // Phase-3 chunk 26: 0x071 GP_SERV_COMMAND_INFLUENCE::CAMPAIGN -- the layout-DENSEST offline body, combining
        // (1) an engine-enum member Mode (GP_SERV_COMMAND_INFLUENCE_MODE : uint8_t, Campaign=2/Colonization=3, from
        // src/map/packets/s2c/0x071_influence.h) with (2) EVERY remaining bit-field hazard: a struct-array element
        // (campaignzone_t Zones[13]) holding TWO uint32 bit-field runs (widths 1/3/10/10/8 then 10/10/12) SPLIT by four
        // plain u8 influence bytes -- the FIRST bit-field "run-reset" (run1 exhausts its 32-bit unit, four u8s, then a
        // FRESH uint32 starts run2; the compiler proves MaxFortifications bit-byte = element offset 8, NOT a
        // continuation of bytes 0-3); (3) a struct-of-structs-of-bitfields (campaignnations_t nests 7 campaignnation_t
        // each a 4/14/7/7 bit run); (4) new widths 5/7/10/12/14; (5) STRADDLING fields read as bit positions in the
        // containing uint32 (bitpos = compiler off*8 + shift, NOT a per-byte mask). The engine-enum uint8 substitution
        // invariant re-confirmed here (EQ_SIZE/ALIGN/MODE all 1 on the 200-byte twin). Compiled oracle, S2C
        // body-relative; every expected read from the golden.
        VerifyMapS2cInfluenceCampaign(System.IO.Path.GetDirectoryName(goldenPath),
            System.IO.Path.Combine(enumsDir, @"..\packets\s2c\0x071_influence.h"));

        // Phase-3 chunk 27: 0x071 GP_SERV_COMMAND_INFLUENCE::COLONIZATION -- the CAMPAIGN sibling, reusing the
        // chunk-26 InfluenceMode engine-enum member + BitWord straddling-field primitive and adding a trailing fixed
        // uint8 padding00[144]. colonizationzones_t nests NINE colonizationzone_t each a 7/3/3/19 bit run in one uint32;
        // CurrentBivouacs:3 lives at bit 7 (compiler off=0 shift=7) and STRADDLES the byte 0/1 boundary (bits 7,8,9),
        // so it must be read/written as a bit position in the containing uint32 -- NOT a per-byte mask. coalitionranks_t
        // is the NEGATIVE contrast: six 4-bit fields + an 8-bit pad all byte-aligned (bits 0/4/8/12/16/20/24, no
        // straddle), reached through the same uint32 word path. The uint8 engine-enum substitution invariant is
        // re-confirmed on the 200-byte twin (EQ_SIZE/EQ_ALIGN/EQ_MODE all 1). Compiled oracle, S2C body-relative;
        // every expected read from the golden.
        VerifyMapS2cInfluenceColonization(System.IO.Path.GetDirectoryName(goldenPath),
            System.IO.Path.Combine(enumsDir, @"..\packets\s2c\0x071_influence.h"));

        // Phase-3 chunk 28: 0x113 GP_SERV_COMMAND_CURRENCIES_1 -- the FIRST uint64 allocation-unit bit-field run and the
        // first pack(1) body whose MID-STRUCT uint64 alignment is COLLAPSED to 1 (contrast chunk 6's default-packed
        // uint64 forcing alignof 8 + 4 pad). plans_unit_t = five uint64_t:9 + one :19 == exactly 64 bits (intra-unit
        // bit positions 0/9/18/27/36/45, every 9-bit field STRADDLES a byte boundary INSIDE the uint64 -> read the whole
        // 8-byte unit via BitConverter.ToUInt64, shift+mask, NEVER a per-byte mask). The run starts at body byte 212
        // (NOT 8-aligned, 212%8==4) -- legal only because pack(1); a ~110-member dense int32/uint16/uint8 chain with NO
        // padding (the gapless 248-byte i*7+3 fill proves it). Compiled layout oracle, S2C body-relative; every expected
        // read from the golden.
        VerifyMapS2cCurrencies1(System.IO.Path.GetDirectoryName(goldenPath));
        VerifyMapS2cCurrencies2(System.IO.Path.GetDirectoryName(goldenPath));
        VerifyMapS2cEmoteList(System.IO.Path.GetDirectoryName(goldenPath));
        VerifyMapS2cUnity(System.IO.Path.GetDirectoryName(goldenPath));
        VerifyMapS2cAbilRecast(System.IO.Path.GetDirectoryName(goldenPath));
        VerifyMapS2cItemMax(System.IO.Path.GetDirectoryName(goldenPath));
        VerifyMapS2cNaraku(System.IO.Path.GetDirectoryName(goldenPath));
        VerifyMapS2cPacketControl(System.IO.Path.GetDirectoryName(goldenPath));
        VerifyMapS2cEnterZone(System.IO.Path.GetDirectoryName(goldenPath));
        VerifyMapS2cItemSame(System.IO.Path.GetDirectoryName(goldenPath),
            System.IO.Path.Combine(enumsDir, @"..\packets\s2c\0x01d_item_same.h"));
        VerifyMapS2cWpos(System.IO.Path.GetDirectoryName(goldenPath),
            System.IO.Path.Combine(enumsDir, @"..\packets\s2c\0x05b_wpos.h"));
        VerifyMapS2cEntityVis(System.IO.Path.GetDirectoryName(goldenPath));
        VerifyMapS2cItemTradeRes(System.IO.Path.GetDirectoryName(goldenPath),
            System.IO.Path.Combine(enumsDir, @"..\packets\s2c\0x022_item_trade_res.h"));
        VerifyMapS2cWpos2(System.IO.Path.GetDirectoryName(goldenPath));
        VerifyMapS2cEffect(System.IO.Path.GetDirectoryName(goldenPath),
            System.IO.Path.Combine(enumsDir, "synthesis_effect.h"));
        VerifyMapS2cEventStr(System.IO.Path.GetDirectoryName(goldenPath));
        VerifyMapS2cEventNum(System.IO.Path.GetDirectoryName(goldenPath));
        VerifyMapS2cItemAttr(System.IO.Path.GetDirectoryName(goldenPath),
            System.IO.Path.Combine(enumsDir, @"..\item_container.h"),
            System.IO.Path.Combine(enumsDir, "item_lockflg.h"));
        VerifyMapS2cItemSubContainer(System.IO.Path.GetDirectoryName(goldenPath));
        VerifyMapS2cItemSearch(System.IO.Path.GetDirectoryName(goldenPath));
        VerifyMapS2cBallistaScout(System.IO.Path.GetDirectoryName(goldenPath));

        Console.WriteLine($"\nRESULT: {_pass} passed, {_fail} failed  (rows: HDR={hdr} HDRSWAP={swap} HDRSEQ={seq})");
        return _fail == 0 ? 0 : 1;
    }

    static void AssertHeader(string name, XI.Map.BasicPacket p, string expHex)
    {
        var b = p.Buffer;
        string actual = $"{b[0]:X2}{b[1]:X2}{b[2]:X2}{b[3]:X2}";
        Check($"{name} bytes", expHex.ToUpperInvariant(), actual);
    }

    // Independently re-parses one engine packet-id enum header and cross-checks every name->value
    // against the C# enum via reflection. Brace-bounded at the enum's OWN body so the trailing
    // `magic_enum::customize::enum_range { min; max }` struct is never read as members. Explicit and
    // implicit (prev+1) enumerators are both handled, mirroring C++ semantics.
    static void VerifyPacketIds(string label, string path, Type enumType)
    {
        Console.WriteLine($"\n== {label} packet-id parity: generated C# enum vs independent .h re-parse ==");

        if (!System.IO.File.Exists(path))
        {
            Check($"{label} .h present", "present", "MISSING:" + path);
            return;
        }

        var cpp = ParseCppEnum(System.IO.File.ReadAllText(path), label);
        if (cpp.Count == 0)
        {
            Check($"{label} parsed-from-.h non-empty", "non-empty", "0 members");
            return;
        }

        // C# enum: name -> value (Enum.Parse by name keeps aliases distinct, unlike GetValues).
        var cs = new Dictionary<string, long>();
        foreach (var n in Enum.GetNames(enumType))
            cs[n] = Convert.ToInt64(Enum.Parse(enumType, n), CultureInfo.InvariantCulture);

        Check($"{label} member count", cpp.Count.ToString(), cs.Count.ToString());

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (name, val) in cpp)
        {
            seen.Add(name);
            if (cs.TryGetValue(name, out long got))
                Check($"{label}.{name}", val.ToString(), got.ToString());
            else
                Check($"{label}.{name}", val.ToString(), "MISSING-IN-C#");
        }
        foreach (var n in cs.Keys)
            if (!seen.Contains(n))
                Check($"{label} extra-member {n}", "present-in-.h", "C#-ONLY");
    }

    static readonly Regex CppEnumMemberRe = new Regex(@"^([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(0[xX][0-9A-Fa-f]+|\d+)\s*,?\s*$");
    static readonly Regex CppEnumImplicitRe = new Regex(@"^([A-Za-z_][A-Za-z0-9_]*)\s*,\s*$");

    static List<(string name, long val)> ParseCppEnum(string text, string enumName)
    {
        var res = new List<(string, long)>();

        int mk = text.IndexOf("enum class " + enumName, StringComparison.Ordinal);
        // An unscoped enum with a fixed underlying type (e.g. `enum CONTAINER_ID : uint8`) has no `class` keyword;
        // fall back to a plain `enum NAME` ONLY when the scoped search fails, so the 8+ prior scoped re-parses (which
        // always match `enum class`) keep their existing path untouched. The first `{` after the match bounds the enum's
        // own body (brace-depth below); the re-parse targets the enum's DEFINING header, not a forward-decl.
        if (mk < 0)
            mk = text.IndexOf("enum " + enumName, StringComparison.Ordinal);
        if (mk < 0) return res;

        int b0 = text.IndexOf('{', mk);
        if (b0 < 0) return res;

        int depth = 0, end = -1;
        for (int i = b0; i < text.Length; i++)
        {
            if (text[i] == '{') depth++;
            else if (text[i] == '}') { depth--; if (depth == 0) { end = i; break; } }
        }
        if (end < 0) return res;

        string body = text.Substring(b0 + 1, end - b0 - 1);
        long prev = -1;
        foreach (var rawLine in body.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            int c = line.IndexOf("//", StringComparison.Ordinal);
            if (c >= 0) line = line.Substring(0, c);
            line = line.Trim();
            if (line.Length == 0) continue;

            var m = CppEnumMemberRe.Match(line);
            if (m.Success)
            {
                long v = ParseNum(m.Groups[2].Value);
                res.Add((m.Groups[1].Value, v));
                prev = v;
                continue;
            }
            var im = CppEnumImplicitRe.Match(line);
            if (im.Success)
            {
                long v = prev + 1;
                res.Add((im.Groups[1].Value, v));
                prev = v;
            }
        }
        return res;
    }

    static long ParseNum(string t)
    {
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToInt64(t.Substring(2), 16);
        return long.Parse(t, CultureInfo.InvariantCulture);
    }

    // Field by whitespace index.
    static string Field(string line, int idx)
    {
        var t = line.Split(' ');
        return t[idx];
    }

    // First token of the form key=<int>; used for the pre-duplication occurrence.
    static long LongField(string line, string key)
    {
        foreach (var t in line.Split(' '))
            if (t.StartsWith(key, StringComparison.Ordinal))
                return long.Parse(t.Substring(key.Length), CultureInfo.InvariantCulture);
        throw new FormatException($"missing {key} in: {line}");
    }

    // Second token of the form key=<int> (the golden repeats size=/seq= once before and once after
    // the header bytes; the trailing one is the readback expectation).
    static long LongField2(string line, string key)
    {
        int seen = 0;
        foreach (var t in line.Split(' '))
        {
            if (t.StartsWith(key, StringComparison.Ordinal) && ++seen == 2)
                return long.Parse(t.Substring(key.Length), CultureInfo.InvariantCulture);
        }
        throw new FormatException($"missing 2nd {key} in: {line}");
    }

    static string GetLine(string path, string prefix)
    {
        foreach (var raw in System.IO.File.ReadAllLines(path))
            if (raw.StartsWith(prefix, StringComparison.Ordinal)) return raw;
        return "";
    }

    // Value after the first '=' on a KEY=VALUE line (used for PACKET_SIZE / ENTITY_* constants).
    static string Val(string line)
    {
        int i = line.IndexOf('=');
        return i < 0 ? "" : line.Substring(i + 1).Trim();
    }

    static byte[] HexToBytes(string hex)
    {
        var b = new byte[hex.Length / 2];
        for (int i = 0; i < b.Length; i++)
            b[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return b;
    }

    // Verifies the first Phase-3 S2C body struct (0x076 GP_SERV_COMMAND_GROUP_EFFECTS) against the
    // compiled layout oracle map_s2c_group_effects_golden.txt. Two INDEPENDENT cross-checks (not just
    // self-consistency): (a) read Members[1] fields with the C# accessors and with a raw read at the
    // compiler-emitted offsets -- equal only if my offsets == the engine's; (b) write via the setters
    // and verify each landed on the byte the compiler's offset says. Also the whole-struct i*7+3 fill.
    static void VerifyMapGroupEffects(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_group_effects_golden.txt");
        Console.WriteLine("\n== Group-effects (0x076) body-struct parity: C# XI.Map.S2cGroupEffects vs layout oracle ==");
        if (!System.IO.File.Exists(path))
        {
            Check("group_effects golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        Check("GE SIZE_partymemberbuffs_t", g["SIZE_partymemberbuffs_t"], XI.Map.PartyMemberBuffs.SizeOf.ToString());
        Check("GE ALIGN_partymemberbuffs_t", g["ALIGN_partymemberbuffs_t"], XI.Map.PartyMemberBuffs.AlignOf.ToString());
        Check("GE SIZE_PacketData", g["SIZE_PacketData"], XI.Map.S2cGroupEffects.SizeOf.ToString());
        Check("GE ALIGN_PacketData", g["ALIGN_PacketData"], XI.Map.S2cGroupEffects.AlignOf.ToString());
        Check("GE OFF.UniqueNo", g["OFF_pmb.UniqueNo"], XI.Map.PartyMemberBuffs.OffUniqueNo.ToString());
        Check("GE OFF.ActIndex", g["OFF_pmb.ActIndex"], XI.Map.PartyMemberBuffs.OffActIndex.ToString());
        Check("GE OFF.padding06", g["OFF_pmb.padding06"], XI.Map.PartyMemberBuffs.OffPadding06.ToString());
        Check("GE OFF.Bits", g["OFF_pmb.Bits"], XI.Map.PartyMemberBuffs.OffBits.ToString());
        Check("GE OFF.Buffs", g["OFF_pmb.Buffs"], XI.Map.PartyMemberBuffs.OffBuffs.ToString());
        Check("GE BUFFS_LEN", g["BUFFS_LEN"], XI.Map.PartyMemberBuffs.BuffsLen.ToString());
        Check("GE STRIDE_Members", g["STRIDE_Members"], XI.Map.S2cGroupEffects.MemberStride.ToString());
        Check("GE MEMBER_COUNT", g["MEMBER_COUNT"], XI.Map.S2cGroupEffects.MemberCount.ToString());
        Check("GE PD_FILL_SIZE", g["PD_FILL_SIZE"], XI.Map.S2cGroupEffects.SizeOf.ToString());
        Check("GE M1_UniqueNo_off", g["M1_UniqueNo_off"], XI.Map.S2cGroupEffects.UniqueNoOffset(1).ToString());
        Check("GE M1_Bits_off", g["M1_Bits_off"], XI.Map.S2cGroupEffects.BitsOffset(1).ToString());
        Check("GE M4_Buffs_off", g["M4_Buffs_off"], XI.Map.S2cGroupEffects.BuffsOffset(4).ToString());

        // Whole-struct deterministic fill: reproduce the engine's i*7+3 pattern and match the 240-byte hex.
        byte[] pdFill = HexToBytes(g["PD_FILL"]);
        Check("GE PD_FILL byte count", g["PD_FILL_SIZE"], pdFill.Length.ToString());
        var body = new byte[XI.Map.S2cGroupEffects.SizeOf];
        for (int i = 0; i < body.Length; i++) body[i] = unchecked((byte)(i * 7 + 3));
        var sb = new System.Text.StringBuilder(body.Length * 2);
        foreach (byte x in body) sb.Append(x.ToString("X2"));
        Check("GE PD_FILL deterministic pattern", g["PD_FILL"], sb.ToString());

        // Independent read-path cross-check: accessor (my offsets) vs raw read at compiler offsets.
        long m1u = long.Parse(g["M1_UniqueNo_off"], CultureInfo.InvariantCulture);
        long m1b = long.Parse(g["M1_Bits_off"], CultureInfo.InvariantCulture);
        Check("GE M1.UniqueNo accessor==raw@compiler-offset",
            BitConverter.ToUInt32(pdFill, (int)m1u).ToString(),
            XI.Map.S2cGroupEffects.GetUniqueNo(pdFill, 1).ToString());
        Check("GE M1.Bits accessor==raw@compiler-offset",
            BitConverter.ToUInt64(pdFill, (int)m1b).ToString(),
            XI.Map.S2cGroupEffects.GetBits(pdFill, 1).ToString());

        // Write-path cross-check: setters must land on the compiler's offsets.
        var buf = new byte[XI.Map.S2cGroupEffects.SizeOf];
        XI.Map.S2cGroupEffects.SetUniqueNo(buf, 2, 0xDEADBEEF);
        XI.Map.S2cGroupEffects.SetActIndex(buf, 2, 0x1234);
        XI.Map.S2cGroupEffects.SetBits(buf, 2, 0x0102030405060708UL);
        long stride = long.Parse(g["STRIDE_Members"], CultureInfo.InvariantCulture);
        long offU = long.Parse(g["OFF_pmb.UniqueNo"], CultureInfo.InvariantCulture);
        long offA = long.Parse(g["OFF_pmb.ActIndex"], CultureInfo.InvariantCulture);
        long offB = long.Parse(g["OFF_pmb.Bits"], CultureInfo.InvariantCulture);
        int m2 = (int)(2 * stride);
        Check("GE SetUniqueNo at compiler offset", "0xDEADBEEF", $"0x{BitConverter.ToUInt32(buf, (int)(m2 + offU)):X8}");
        Check("GE SetActIndex at compiler offset", "0x1234", $"0x{BitConverter.ToUInt16(buf, (int)(m2 + offA)):X4}");
        Check("GE SetBits at compiler offset", "0x0102030405060708", $"0x{BitConverter.ToUInt64(buf, (int)(m2 + offB)):X16}");
    }

    // Verifies the first Phase-3 C2S body struct (0x015 GP_CLI_COMMAND_POS) against the compiled
    // layout oracle map_c2s_pos_golden.txt. Default-packing + a bit-field run (offsetof illegal ->
    // oracle derived byte 21 by sentinel scan). Cross-checks my accessor OFFSETS against raw reads at
    // the compiler-emitted offsets (equal only if my offsets == the engine's), setter byte placement,
    // the whole-struct i*7+3 fill, the bit-field masks, and GP_CLI_HEADER's id:9/sync word1 being the
    // SAME low-9/word1 bits CBasicPacket uses for opcode/sequence.
    static void VerifyMapC2sPos(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_c2s_pos_golden.txt");
        Console.WriteLine("\n== C2s pos (0x015) body-struct parity: C# XI.Map.C2sPos vs layout oracle ==");
        if (!System.IO.File.Exists(path))
        {
            Check("c2s pos golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // GP_CLI_HEADER layout.
        Check("POS HDR_SIZE", g["HDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());
        Check("POS HDR_ALIGN", g["HDR_ALIGN"], XI.Map.C2sPos.HeaderAlign.ToString());
        Check("POS HDR_OFF_sync", g["HDR_OFF_sync"], XI.Map.C2sPos.OffSync.ToString());
        Check("POS HDR_id_mask_lo", g["HDR_id_mask_lo"], XI.Map.C2sPos.IdMaskLoByte.ToString());
        Check("POS HDR_id_mask_hi", g["HDR_id_mask_hi"], XI.Map.C2sPos.IdMaskHiByte.ToString());
        Check("POS HDR_size_mask_lo", g["HDR_size_mask_lo"], XI.Map.C2sPos.SizeMaskLoByte.ToString());
        Check("POS HDR_size_mask_hi", g["HDR_size_mask_hi"], XI.Map.C2sPos.SizeMaskHiByte.ToString());

        // Body struct layout (default-packing offsets/sizes).
        Check("POS POS_SIZE", g["POS_SIZE"], XI.Map.C2sPos.SizeOf.ToString());
        Check("POS POS_ALIGN", g["POS_ALIGN"], XI.Map.C2sPos.AlignOf.ToString());
        Check("POS OFF.header", g["OFF_POS.header"], XI.Map.C2sPos.OffHeader.ToString());
        Check("POS OFF.x", g["OFF_POS.x"], XI.Map.C2sPos.OffX.ToString());
        Check("POS OFF.z", g["OFF_POS.z"], XI.Map.C2sPos.OffZ.ToString());
        Check("POS OFF.y", g["OFF_POS.y"], XI.Map.C2sPos.OffY.ToString());
        Check("POS OFF.MovTime", g["OFF_POS.MovTime"], XI.Map.C2sPos.OffMovTime.ToString());
        Check("POS OFF.MoveFlame", g["OFF_POS.MoveFlame"], XI.Map.C2sPos.OffMoveFlame.ToString());
        Check("POS OFF.dir", g["OFF_POS.dir"], XI.Map.C2sPos.OffDir.ToString());
        Check("POS OFF.facetarget", g["OFF_POS.facetarget"], XI.Map.C2sPos.OffFacetarget.ToString());
        Check("POS OFF.TimeNow", g["OFF_POS.TimeNow"], XI.Map.C2sPos.OffTimeNow.ToString());
        Check("POS OFF.padding00", g["OFF_POS.padding00"], XI.Map.C2sPos.OffPadding00.ToString());

        // Bit-field byte + masks (BF_byte + masks were runtime-derived by the oracle; offsetof illegal).
        Check("POS BF_byte", ParseNum(g["BF_byte"]).ToString(), XI.Map.C2sPos.OffBitfield.ToString());
        Check("POS BF_TargetMode_mask", ParseNum(g["BF_TargetMode_mask"]).ToString(), XI.Map.C2sPos.TargetModeMask.ToString());
        Check("POS BF_RunMode_mask", ParseNum(g["BF_RunMode_mask"]).ToString(), XI.Map.C2sPos.RunModeMask.ToString());
        Check("POS BF_GroundMode_mask", ParseNum(g["BF_GroundMode_mask"]).ToString(), XI.Map.C2sPos.GroundModeMask.ToString());
        Check("POS BF_unused_mask", ParseNum(g["BF_unused_mask"]).ToString(), XI.Map.C2sPos.UnusedMask.ToString());

        // Whole-struct deterministic fill.
        byte[] posFill = HexToBytes(g["POS_FILL"]);
        Check("POS POS_FILL_SIZE", g["POS_FILL_SIZE"], posFill.Length.ToString());
        var body = new byte[XI.Map.C2sPos.SizeOf];
        for (int i = 0; i < body.Length; i++) body[i] = unchecked((byte)(i * 7 + 3));
        var sb = new System.Text.StringBuilder(body.Length * 2);
        foreach (byte x in body) sb.Append(x.ToString("X2"));
        Check("POS POS_FILL deterministic pattern", g["POS_FILL"], sb.ToString());

        // Compiler-emitted body offsets (raw reads use THESE, not my constants -- independent proof).
        int coX = (int)ParseNum(g["OFF_POS.x"]);
        int coZ = (int)ParseNum(g["OFF_POS.z"]);
        int coY = (int)ParseNum(g["OFF_POS.y"]);
        int coMov = (int)ParseNum(g["OFF_POS.MovTime"]);
        int coFace = (int)ParseNum(g["OFF_POS.facetarget"]);
        int coTime = (int)ParseNum(g["OFF_POS.TimeNow"]);
        int coDir = (int)ParseNum(g["OFF_POS.dir"]);
        int coBF = (int)ParseNum(g["BF_byte"]);

        // Independent read-path cross-check: my accessor offset == a raw read at the compiler offset
        // (compare the underlying raw bytes, so float formatting can't mask a wrong offset).
        Check("POS OffX==compiler",
            BitConverter.ToUInt32(posFill, XI.Map.C2sPos.OffX).ToString(),
            BitConverter.ToUInt32(posFill, coX).ToString());
        Check("POS OffY==compiler",
            BitConverter.ToUInt32(posFill, XI.Map.C2sPos.OffY).ToString(),
            BitConverter.ToUInt32(posFill, coY).ToString());
        Check("POS OffTimeNow==compiler",
            BitConverter.ToUInt32(posFill, XI.Map.C2sPos.OffTimeNow).ToString(),
            BitConverter.ToUInt32(posFill, coTime).ToString());
        Check("POS OffMovTime==compiler",
            BitConverter.ToUInt16(posFill, XI.Map.C2sPos.OffMovTime).ToString(),
            BitConverter.ToUInt16(posFill, coMov).ToString());
        Check("POS OffFacetarget==compiler",
            BitConverter.ToUInt16(posFill, XI.Map.C2sPos.OffFacetarget).ToString(),
            BitConverter.ToUInt16(posFill, coFace).ToString());

        // Float setter/getter round-trip (exact-representable values so the string form is stable),
        // plus a raw-bytes cross-check that SetX wrote to the SAME 4 bytes the compiler's x offset is.
        var bufF = new byte[XI.Map.C2sPos.SizeOf];
        XI.Map.C2sPos.SetX(bufF, 12.5f);
        XI.Map.C2sPos.SetZ(bufF, -3.25f);
        XI.Map.C2sPos.SetY(bufF, 0.0f);
        Check("POS SetX/GetX round-trip", "12.5", XI.Map.C2sPos.GetX(bufF).ToString("R"));
        Check("POS SetZ/GetZ round-trip", "-3.25", XI.Map.C2sPos.GetZ(bufF).ToString("R"));
        Check("POS SetY/GetY round-trip", "0", XI.Map.C2sPos.GetY(bufF).ToString("R"));
        Check("POS SetX at compiler offset",
            BitConverter.ToUInt32(bufF, XI.Map.C2sPos.OffX).ToString(),
            BitConverter.ToUInt32(bufF, coX).ToString());

        // Write-path cross-check: setters must land on the compiler's byte positions.
        var buf = new byte[XI.Map.C2sPos.SizeOf];
        XI.Map.C2sPos.SetTimeNow(buf, 0xDEADBEEF);
        XI.Map.C2sPos.SetMovTime(buf, 0x1234);
        XI.Map.C2sPos.SetDir(buf, -5);
        XI.Map.C2sPos.SetModes(buf, true, true, true);
        Check("POS SetTimeNow at compiler offset", "0xDEADBEEF", $"0x{BitConverter.ToUInt32(buf, coTime):X8}");
        Check("POS SetMovTime at compiler offset", "0x1234", $"0x{BitConverter.ToUInt16(buf, coMov):X4}");
        Check("POS SetDir at compiler offset", "0xFB", $"0x{buf[coDir]:X2}"); // -5 == 0xFB
        Check("POS SetModes bit-field byte", "0x07", $"0x{buf[coBF]:X2}");     // T|R|G = 0x01|0x02|0x04

        // GP_CLI_HEADER id:9 / sync word1 must be the SAME bits CBasicPacket uses for opcode/sequence.
        byte[] hb = new byte[4];
        XI.Map.C2sPos.WriteHeader(hb, 0x155, 0, 0x1234);
        var bp = new XI.Map.BasicPacket();
        Array.Copy(hb, bp.Buffer, 4);
        Check("POS header id == BasicPacket opcode", "341", bp.GetOpcode().ToString());   // 0x155
        Check("POS header sync == BasicPacket sequence", "4660", bp.GetSequence().ToString()); // 0x1234
        // and reading it back through the C2S accessors
        Check("POS ReadId roundtrip", "341", XI.Map.C2sPos.ReadId(hb).ToString());
        Check("POS ReadSync roundtrip", "4660", XI.Map.C2sPos.ReadSync(hb).ToString());
        // size=0 was written; with mode bits set above confirm Z accessor matches raw at compiler z.
        Check("POS OffZ==compiler",
            BitConverter.ToUInt32(posFill, XI.Map.C2sPos.OffZ).ToString(),
            BitConverter.ToUInt32(posFill, coZ).ToString());
    }

    // Verifies the second Phase-3 C2S body struct (0x053 GP_CLI_COMMAND_LOCKSTYLE) against the
    // compiled layout oracle map_c2s_lockstyle_golden.txt. The load-bearing fact is the nested-items
    // ARRAY: 16 lockstyleitem_t elements (uint16 ItemNo/padding01 realign to 4/6 after the four
    // uint8s) at byte 8, with NO inter-element padding (LS_ALIGN=2). Cross-checks my computed
    // element/field offsets against the compiler-emitted element offsets (two independent ways), the
    // whole-struct i*7+3 fill, the mode literals, and the shared GP_CLI_HEADER id:9/sync==opcode/seq.
    static void VerifyMapC2sLockstyle(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_c2s_lockstyle_golden.txt");
        Console.WriteLine("\n== C2s lockstyle (0x053) nested-array body parity: C# XI.Map.C2sLockstyle vs layout oracle ==");
        if (!System.IO.File.Exists(path))
        {
            Check("c2s lockstyle golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // Inner lockstyleitem_t (default packing: uint16s realign to 4/6 after the four uint8s).
        Check("LSI_SIZE", g["LSI_SIZE"], XI.Map.LockstyleItem.SizeOf.ToString());
        Check("LSI_ALIGN", g["LSI_ALIGN"], XI.Map.LockstyleItem.AlignOf.ToString());
        Check("OFF_lsi.ItemIndex", g["OFF_lsi.ItemIndex"], XI.Map.LockstyleItem.OffItemIndex.ToString());
        Check("OFF_lsi.EquipKind", g["OFF_lsi.EquipKind"], XI.Map.LockstyleItem.OffEquipKind.ToString());
        Check("OFF_lsi.Category", g["OFF_lsi.Category"], XI.Map.LockstyleItem.OffCategory.ToString());
        Check("OFF_lsi.padding00", g["OFF_lsi.padding00"], XI.Map.LockstyleItem.OffPadding00.ToString());
        Check("OFF_lsi.ItemNo", g["OFF_lsi.ItemNo"], XI.Map.LockstyleItem.OffItemNo.ToString());
        Check("OFF_lsi.padding01", g["OFF_lsi.padding01"], XI.Map.LockstyleItem.OffPadding01.ToString());

        // Outer struct + the scalar byte fields between the header and the array.
        Check("LS_SIZE", g["LS_SIZE"], XI.Map.C2sLockstyle.SizeOf.ToString());
        Check("LS_ALIGN", g["LS_ALIGN"], XI.Map.C2sLockstyle.AlignOf.ToString());
        Check("OFF_ls.header", g["OFF_ls.header"], XI.Map.C2sLockstyle.OffHeader.ToString());
        Check("OFF_ls.Count", g["OFF_ls.Count"], XI.Map.C2sLockstyle.OffCount.ToString());
        Check("OFF_ls.Mode", g["OFF_ls.Mode"], XI.Map.C2sLockstyle.OffMode.ToString());
        Check("OFF_ls.Flags", g["OFF_ls.Flags"], XI.Map.C2sLockstyle.OffFlags.ToString());
        Check("OFF_ls.padding00", g["OFF_ls.padding00"], XI.Map.C2sLockstyle.OffPadding00.ToString());
        Check("OFF_ls.Items", g["OFF_ls.Items"], XI.Map.C2sLockstyle.OffItems.ToString());
        Check("LS_ITEMS_COUNT", g["LS_ITEMS_COUNT"], XI.Map.C2sLockstyle.ItemCount.ToString());

        // Array element stride + per-element field offsets, proven against the compiler's own numbers.
        Check("LS_ITEM_STRIDE", g["LS_ITEM_STRIDE"], XI.Map.C2sLockstyle.ItemStride.ToString());
        Check("LS FieldOffset(1,ItemNo)", g["LS_I1_ItemNo_off"],
            XI.Map.C2sLockstyle.FieldOffset(1, XI.Map.LockstyleItem.OffItemNo).ToString());
        Check("LS FieldOffset(15,ItemIndex)", g["LS_I15_ItemIndex_off"],
            XI.Map.C2sLockstyle.FieldOffset(15, XI.Map.LockstyleItem.OffItemIndex).ToString());
        Check("LS FieldOffset(15,padding01)", g["LS_I15_padding01_off"],
            XI.Map.C2sLockstyle.FieldOffset(15, XI.Map.LockstyleItem.OffPadding01).ToString());
        // The last array byte must be struct-size-1: proves NO tail padding after the array.
        Check("LS last item last byte index", g["LS_last_item_last_off"],
            (XI.Map.C2sLockstyle.FieldOffset(15, XI.Map.LockstyleItem.OffPadding01) + 2).ToString());

        // Mode enum literals (engine hand-authored, but pinned against the compiled .h).
        Check("MODE_Disable", g["MODE_Disable"], ((int)XI.Map.LockstyleMode.Disable).ToString());
        Check("MODE_Continue", g["MODE_Continue"], ((int)XI.Map.LockstyleMode.Continue).ToString());
        Check("MODE_Query", g["MODE_Query"], ((int)XI.Map.LockstyleMode.Query).ToString());
        Check("MODE_Set", g["MODE_Set"], ((int)XI.Map.LockstyleMode.Set).ToString());
        Check("MODE_Enable", g["MODE_Enable"], ((int)XI.Map.LockstyleMode.Enable).ToString());

        // Whole-struct deterministic fill.
        byte[] lsFill = HexToBytes(g["LS_FILL"]);
        Check("LS_FILL_SIZE", g["LS_FILL_SIZE"], lsFill.Length.ToString());
        var body = new byte[XI.Map.C2sLockstyle.SizeOf];
        for (int i = 0; i < body.Length; i++) body[i] = unchecked((byte)(i * 7 + 3));
        var sb = new System.Text.StringBuilder(body.Length * 2);
        foreach (byte x in body) sb.Append(x.ToString("X2"));
        Check("LS LS_FILL deterministic pattern", g["LS_FILL"], sb.ToString());

        // Compiler-emitted element offsets used for the raw reads below (independent proof of my math).
        int coItems = (int)ParseNum(g["OFF_ls.Items"]);
        int coStride = (int)ParseNum(g["LS_ITEM_STRIDE"]);
        int coEquipKind = (int)ParseNum(g["OFF_lsi.EquipKind"]);
        int coMode = (int)ParseNum(g["OFF_ls.Mode"]);
        int coI1ItemNo = (int)ParseNum(g["LS_I1_ItemNo_off"]);
        int coI15Index = (int)ParseNum(g["LS_I15_ItemIndex_off"]);
        int coI15Pad01 = (int)ParseNum(g["LS_I15_padding01_off"]);

        // Read-path cross-check: C# accessor vs a raw read at the compiler offset.
        Check("LS GetItemNo(1)==raw@compiler",
            BitConverter.ToUInt16(lsFill, coI1ItemNo).ToString(),
            XI.Map.C2sLockstyle.GetItemNo(lsFill, 1).ToString());
        Check("LS GetItemIndex(15)==raw@compiler",
            lsFill[coI15Index].ToString(),
            XI.Map.C2sLockstyle.GetItemIndex(lsFill, 15).ToString());
        Check("LS GetItemPadding01(15)==raw@compiler",
            BitConverter.ToUInt16(lsFill, coI15Pad01).ToString(),
            XI.Map.C2sLockstyle.GetItemPadding01(lsFill, 15).ToString());
        Check("LS GetEquipKind(7)==raw@compiler",
            lsFill[coItems + 7 * coStride + coEquipKind].ToString(),
            XI.Map.C2sLockstyle.GetEquipKind(lsFill, 7).ToString());

        // Write-path cross-check: setters must land on the compiler's element field offsets.
        var buf = new byte[XI.Map.C2sLockstyle.SizeOf];
        XI.Map.C2sLockstyle.SetItemNo(buf, 1, 0xBEEF);
        XI.Map.C2sLockstyle.SetItemIndex(buf, 15, 0x5A);
        XI.Map.C2sLockstyle.SetItemPadding01(buf, 15, 0xCAFE);
        XI.Map.C2sLockstyle.SetMode(buf, XI.Map.LockstyleMode.Enable);
        Check("LS SetItemNo at compiler offset", "0xBEEF", $"0x{BitConverter.ToUInt16(buf, coI1ItemNo):X4}");
        Check("LS SetItemIndex at compiler offset", "0x5A", $"0x{buf[coI15Index]:X2}");
        Check("LS SetItemPadding01 at compiler offset", "0xCAFE", $"0x{BitConverter.ToUInt16(buf, coI15Pad01):X4}");
        Check("LS SetMode at compiler offset", "0x04", $"0x{buf[coMode]:X2}");
        Check("LS GetMode roundtrip", "4", XI.Map.C2sLockstyle.GetMode(buf).ToString());

        // Shared GP_CLI_HEADER id:9 / sync word1 are the SAME bits CBasicPacket uses (like pos).
        byte[] hb2 = new byte[4];
        XI.Map.C2sPos.WriteHeader(hb2, 0x155, 0, 0x1234);
        var bp2 = new XI.Map.BasicPacket();
        Array.Copy(hb2, bp2.Buffer, 4);
        Check("LS header id == BasicPacket opcode", "341", bp2.GetOpcode().ToString());
        Check("LS header sync == BasicPacket sequence", "4660", bp2.GetSequence().ToString());
    }

    // Verifies the third Phase-3 C2S body struct (0x058 GP_CLI_COMMAND_RECIPE) against the compiled
    // layout oracle map_c2s_recipe_golden.txt. The load-bearing compiler fact is that the mid-struct
    // uint64 recipe forces RCP_ALIGN=8: it realigns to offset 16 (after bool@4 + 4 uint8 +
    // padding00[3]@9..11 -> 4 inserted pad bytes) and pads the whole struct to RCP_SIZE=32. The recipe
    // port reuses the C2S header framing, so id:9/sync are re-proven against CBasicPacket here too.
    static void VerifyMapC2sRecipe(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_c2s_recipe_golden.txt");
        Console.WriteLine("\n== C2s recipe (0x058) uint64-alignment body parity: C# XI.Map.C2sRecipe vs layout oracle ==");
        if (!System.IO.File.Exists(path))
        {
            Check("c2s recipe golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // Header sync offset the port claims matches the compiler (id:9/size:7 masks are re-proven below
        // via the C2S WriteHeader -> BasicPacket opcode/sequence cross-check).
        Check("RCP HDR_OFF_sync", g["HDR_OFF_sync"], XI.Map.C2sRecipe.OffSync.ToString());

        // Body struct layout (default packing; the uint64 drives ALIGN=8 and SIZE=32).
        Check("RCP RCP_SIZE", g["RCP_SIZE"], XI.Map.C2sRecipe.SizeOf.ToString());
        Check("RCP RCP_ALIGN", g["RCP_ALIGN"], XI.Map.C2sRecipe.AlignOf.ToString());
        Check("RCP OFF.header", g["OFF_RCP.header"], XI.Map.C2sRecipe.OffHeader.ToString());
        Check("RCP OFF.bNew", g["OFF_RCP.bNew"], XI.Map.C2sRecipe.OffBoolNew.ToString());
        Check("RCP OFF.target", g["OFF_RCP.target"], XI.Map.C2sRecipe.OffTarget.ToString());
        Check("RCP OFF.result", g["OFF_RCP.result"], XI.Map.C2sRecipe.OffResult.ToString());
        Check("RCP OFF.synthesisType", g["OFF_RCP.synthesisType"], XI.Map.C2sRecipe.OffSynthesisType.ToString());
        Check("RCP OFF.synthesisAmount", g["OFF_RCP.synthesisAmount"], XI.Map.C2sRecipe.OffSynthesisAmount.ToString());
        Check("RCP OFF.padding00", g["OFF_RCP.padding00"], XI.Map.C2sRecipe.OffPadding00.ToString());
        Check("RCP padding00_len", g["RCP_padding00_len"], XI.Map.C2sRecipe.Padding00Len.ToString());
        Check("RCP OFF.recipe", g["OFF_RCP.recipe"], XI.Map.C2sRecipe.OffRecipe.ToString());
        Check("RCP OFF.craftType", g["OFF_RCP.craftType"], XI.Map.C2sRecipe.OffCraftType.ToString());
        Check("RCP OFF.padding01", g["OFF_RCP.padding01"], XI.Map.C2sRecipe.OffPadding01.ToString());

        // The uint64 realignment this slice exists to prove: recipe is 8-byte aligned (offset 16), NOT
        // the next byte after padding00[3] (which ends at 12) -> 4 internal pad bytes are inserted.
        Check("RCP recipe realigned (not byte-after-padding00)",
            "1",
            (XI.Map.C2sRecipe.OffRecipe != XI.Map.C2sRecipe.OffPadding00 + XI.Map.C2sRecipe.Padding00Len ? 1 : 0).ToString());
        Check("RCP recipe on 8-byte boundary",
            "0",
            (XI.Map.C2sRecipe.OffRecipe % 8).ToString());

        // Whole-struct deterministic fill (catches the 26..31 tail padding bytes the offset list alone misses).
        byte[] rcpFill = HexToBytes(g["RCP_FILL"]);
        Check("RCP RCP_FILL_SIZE", g["RCP_FILL_SIZE"], rcpFill.Length.ToString());
        var body = new byte[XI.Map.C2sRecipe.SizeOf];
        for (int i = 0; i < body.Length; i++) body[i] = unchecked((byte)(i * 7 + 3));
        var sb = new System.Text.StringBuilder(body.Length * 2);
        foreach (byte x in body) sb.Append(x.ToString("X2"));
        Check("RCP RCP_FILL deterministic pattern", g["RCP_FILL"], sb.ToString());

        // Compiler-emitted offsets for the raw reads below (independent proof of my offset math).
        int coRecipe = (int)ParseNum(g["OFF_RCP.recipe"]);
        int coBool = (int)ParseNum(g["OFF_RCP.bNew"]);
        int coTarget = (int)ParseNum(g["OFF_RCP.target"]);
        int coResult = (int)ParseNum(g["OFF_RCP.result"]);
        int coSynthType = (int)ParseNum(g["OFF_RCP.synthesisType"]);
        int coSynthAmt = (int)ParseNum(g["OFF_RCP.synthesisAmount"]);
        int coCraft = (int)ParseNum(g["OFF_RCP.craftType"]);
        int coPad01 = (int)ParseNum(g["OFF_RCP.padding01"]);

        // Read-path cross-check: accessor == a raw read at the compiler's offset.
        Check("RCP GetRecipe==raw@compiler",
            BitConverter.ToUInt64(rcpFill, coRecipe).ToString(),
            XI.Map.C2sRecipe.GetRecipe(rcpFill).ToString());
        Check("RCP GetBoolNew==raw@compiler",
            (rcpFill[coBool] != 0).ToString(),
            XI.Map.C2sRecipe.GetBoolNew(rcpFill).ToString());
        Check("RCP GetTarget==raw@compiler",
            rcpFill[coTarget].ToString(),
            XI.Map.C2sRecipe.GetTarget(rcpFill).ToString());
        Check("RCP GetResult==raw@compiler",
            rcpFill[coResult].ToString(),
            XI.Map.C2sRecipe.GetResult(rcpFill).ToString());
        Check("RCP GetSynthesisType==raw@compiler",
            rcpFill[coSynthType].ToString(),
            XI.Map.C2sRecipe.GetSynthesisType(rcpFill).ToString());
        Check("RCP GetSynthesisAmount==raw@compiler",
            rcpFill[coSynthAmt].ToString(),
            XI.Map.C2sRecipe.GetSynthesisAmount(rcpFill).ToString());
        Check("RCP GetCraftType==raw@compiler",
            rcpFill[coCraft].ToString(),
            XI.Map.C2sRecipe.GetCraftType(rcpFill).ToString());

        // Write-path cross-check: setters land on the compiler's byte positions.
        var buf = new byte[XI.Map.C2sRecipe.SizeOf];
        XI.Map.C2sRecipe.SetRecipe(buf, 0x0102030405060708UL);
        XI.Map.C2sRecipe.SetBoolNew(buf, true);
        XI.Map.C2sRecipe.SetTarget(buf, 0x21);
        XI.Map.C2sRecipe.SetResult(buf, 0x13);
        XI.Map.C2sRecipe.SetSynthesisType(buf, 0x11);
        XI.Map.C2sRecipe.SetSynthesisAmount(buf, 0x12);
        XI.Map.C2sRecipe.SetCraftType(buf, 0x42);
        Check("RCP SetRecipe at compiler offset", "0x0102030405060708", $"0x{BitConverter.ToUInt64(buf, coRecipe):X16}");
        Check("RCP SetBoolNew at compiler offset", "0x01", $"0x{buf[coBool]:X2}");   // bool -> byte 1
        Check("RCP SetTarget at compiler offset", "0x21", $"0x{buf[coTarget]:X2}");
        Check("RCP SetResult at compiler offset", "0x13", $"0x{buf[coResult]:X2}");
        Check("RCP SetSynthesisType at compiler offset", "0x11", $"0x{buf[coSynthType]:X2}");
        Check("RCP SetCraftType at compiler offset", "0x42", $"0x{buf[coCraft]:X2}");

        // bool is stored as a byte: any nonzero reads true, 0 reads false (engine bool semantics).
        var bf = new byte[XI.Map.C2sRecipe.SizeOf];
        bf[XI.Map.C2sRecipe.OffBoolNew] = 0x7F;
        Check("RCP bool truthy nonzero byte", "True", XI.Map.C2sRecipe.GetBoolNew(bf).ToString());
        bf[XI.Map.C2sRecipe.OffBoolNew] = 0x00;
        Check("RCP bool false zero byte", "False", XI.Map.C2sRecipe.GetBoolNew(bf).ToString());

        // Shared C2S GP_CLI_HEADER id:9 / sync word1 are the SAME bits CBasicPacket uses (via pos).
        byte[] hb = new byte[4];
        XI.Map.C2sPos.WriteHeader(hb, 0x155, 0, 0x1234);
        var bp = new XI.Map.BasicPacket();
        Array.Copy(hb, bp.Buffer, 4);
        Check("RCP header id == BasicPacket opcode", "341", bp.GetOpcode().ToString());
        Check("RCP header sync == BasicPacket sequence", "4660", bp.GetSequence().ToString());
    }

    // Verifies the three simple fixed-width Phase-3 C2S body structs (0x0e7 reqlogout, 0x0ea sit,
    // 0x0f1 buffcancel) against the shared compiled layout oracle map_c2s_numeric_golden.txt. Each body
    // is plain uint16/uint32 members under DEFAULT packing after the 4-byte GP_CLI_HEADER, so there is
    // no bit-field or mid-struct-uint64 subtlety here -- the value is (a) pinning the compiler-owned
    // sizeof/alignof/offsets (reqlogout/buffcancel align 2, sit align 4, all sizeof 8) against the C#
    // constants, (b) cross-checking every accessor against a raw read at the *oracle's* offset (two
    // independent ways), (c) proving the whole-struct deterministic byte-fill matches byte-for-byte, and
    // (d) pinning the hand-authored enum literals (reqlogout Mode/Kind, sit Mode) against the compiler
    // re-emitted values so a future .h renumber is caught.
    static void VerifyMapC2sNumeric(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_c2s_numeric_golden.txt");
        Console.WriteLine("\n== C2s numeric bodies (0x0e7 reqlogout / 0x0ea sit / 0x0f1 buffcancel) parity: C# XI.Map vs layout oracle ==");
        if (!System.IO.File.Exists(path))
        {
            Check("c2s numeric golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // Shared GP_CLI_HEADER framing cross-check (already pinned by pos/recipe, re-asserted here).
        Check("NUM HDR_SIZE", g["HDR_SIZE"], "4");
        Check("NUM HDR_OFF_sync", g["HDR_OFF_sync"], "2");

        // ---- 0x0e7 reqlogout --------------------------------------------------------------------
        Check("REQLOGOUT LOGOUT_SIZE", g["LOGOUT_SIZE"], XI.Map.C2sReqlogout.SizeOf.ToString());
        Check("REQLOGOUT LOGOUT_ALIGN", g["LOGOUT_ALIGN"], XI.Map.C2sReqlogout.AlignOf.ToString());
        Check("REQLOGOUT OFF.header", g["OFF_LOGOUT.header"], XI.Map.C2sReqlogout.OffHeader.ToString());
        Check("REQLOGOUT OFF.Mode", g["OFF_LOGOUT.Mode"], XI.Map.C2sReqlogout.OffMode.ToString());
        Check("REQLOGOUT OFF.Kind", g["OFF_LOGOUT.Kind"], XI.Map.C2sReqlogout.OffKind.ToString());
        // hand-authored enum literals pinned vs compiler re-emitted values.
        Check("REQLOGOUT MODE_Toggle", g["MODE_Toggle"], ((int)XI.Map.ReqlogoutMode.Toggle).ToString());
        Check("REQLOGOUT MODE_LogoutOn", g["MODE_LogoutOn"], ((int)XI.Map.ReqlogoutMode.LogoutOn).ToString());
        Check("REQLOGOUT MODE_Off", g["MODE_Off"], ((int)XI.Map.ReqlogoutMode.Off).ToString());
        Check("REQLOGOUT MODE_ShutdownOn", g["MODE_ShutdownOn"], ((int)XI.Map.ReqlogoutMode.ShutdownOn).ToString());
        Check("REQLOGOUT KIND_Logout", g["KIND_Logout"], ((int)XI.Map.ReqlogoutKind.Logout).ToString());
        Check("REQLOGOUT KIND_Shutdown", g["KIND_Shutdown"], ((int)XI.Map.ReqlogoutKind.Shutdown).ToString());
        byte[] logoutFill = HexToBytes(g["LOGOUT_FILL"]);
        Check("REQLOGOUT LOGOUT_FILL_SIZE", g["LOGOUT_FILL_SIZE"], logoutFill.Length.ToString());
        var lo = new byte[XI.Map.C2sReqlogout.SizeOf];
        for (int i = 0; i < lo.Length; i++) lo[i] = unchecked((byte)(i * 7 + 3));
        Check("REQLOGOUT deterministic fill", g["LOGOUT_FILL"], BitConverter.ToString(lo).Replace("-", ""));
        int coLoMode = (int)ParseNum(g["OFF_LOGOUT.Mode"]);
        int coLoKind = (int)ParseNum(g["OFF_LOGOUT.Kind"]);
        Check("REQLOGOUT GetMode==raw@compiler", BitConverter.ToUInt16(logoutFill, coLoMode).ToString(), XI.Map.C2sReqlogout.GetMode(logoutFill).ToString());
        Check("REQLOGOUT GetKind==raw@compiler", BitConverter.ToUInt16(logoutFill, coLoKind).ToString(), XI.Map.C2sReqlogout.GetKind(logoutFill).ToString());
        var lob = new byte[XI.Map.C2sReqlogout.SizeOf];
        XI.Map.C2sReqlogout.SetMode(lob, XI.Map.ReqlogoutMode.ShutdownOn);
        XI.Map.C2sReqlogout.SetKind(lob, XI.Map.ReqlogoutKind.Shutdown);
        Check("REQLOGOUT SetMode at compiler offset", "0x0003", $"0x{BitConverter.ToUInt16(lob, coLoMode):X4}");
        Check("REQLOGOUT SetKind at compiler offset", "0x0003", $"0x{BitConverter.ToUInt16(lob, coLoKind):X4}");
        Check("REQLOGOUT GetReqlogoutMode roundtrip", "ShutdownOn", XI.Map.C2sReqlogout.GetReqlogoutMode(lob).ToString());
        Check("REQLOGOUT GetReqlogoutKind roundtrip", "Shutdown", XI.Map.C2sReqlogout.GetReqlogoutKind(lob).ToString());

        // ---- 0x0ea sit --------------------------------------------------------------------------
        Check("SIT SIT_SIZE", g["SIT_SIZE"], XI.Map.C2sSit.SizeOf.ToString());
        Check("SIT SIT_ALIGN", g["SIT_ALIGN"], XI.Map.C2sSit.AlignOf.ToString());
        Check("SIT OFF.header", g["OFF_SIT.header"], XI.Map.C2sSit.OffHeader.ToString());
        Check("SIT OFF.Mode", g["OFF_SIT.Mode"], XI.Map.C2sSit.OffMode.ToString());
        // The uint32 Mode realigns to offset 4 (== header size); sizeof stays 8 (4+4, no tail pad).
        Check("SIT Mode on 4-byte boundary", "0", (XI.Map.C2sSit.OffMode % 4).ToString());
        Check("SIT SITMODE_Toggle", g["SITMODE_Toggle"], ((int)XI.Map.SitMode.Toggle).ToString());
        Check("SIT SITMODE_On", g["SITMODE_On"], ((int)XI.Map.SitMode.On).ToString());
        Check("SIT SITMODE_Off", g["SITMODE_Off"], ((int)XI.Map.SitMode.Off).ToString());
        byte[] sitFill = HexToBytes(g["SIT_FILL"]);
        Check("SIT SIT_FILL_SIZE", g["SIT_FILL_SIZE"], sitFill.Length.ToString());
        var si = new byte[XI.Map.C2sSit.SizeOf];
        for (int i = 0; i < si.Length; i++) si[i] = unchecked((byte)(i * 7 + 3));
        Check("SIT deterministic fill", g["SIT_FILL"], BitConverter.ToString(si).Replace("-", ""));
        int coSiMode = (int)ParseNum(g["OFF_SIT.Mode"]);
        Check("SIT GetMode==raw@compiler", BitConverter.ToUInt32(sitFill, coSiMode).ToString(), XI.Map.C2sSit.GetMode(sitFill).ToString());
        var sib = new byte[XI.Map.C2sSit.SizeOf];
        XI.Map.C2sSit.SetMode(sib, XI.Map.SitMode.On);
        Check("SIT SetMode at compiler offset", "0x00000001", $"0x{BitConverter.ToUInt32(sib, coSiMode):X8}");
        Check("SIT GetSitMode roundtrip", "On", XI.Map.C2sSit.GetSitMode(sib).ToString());

        // ---- 0x0f1 buffcancel -------------------------------------------------------------------
        Check("BUFFCANCEL BUFFCANCEL_SIZE", g["BUFFCANCEL_SIZE"], XI.Map.C2sBuffcancel.SizeOf.ToString());
        Check("BUFFCANCEL BUFFCANCEL_ALIGN", g["BUFFCANCEL_ALIGN"], XI.Map.C2sBuffcancel.AlignOf.ToString());
        Check("BUFFCANCEL OFF.header", g["OFF_BUFFCANCEL.header"], XI.Map.C2sBuffcancel.OffHeader.ToString());
        Check("BUFFCANCEL OFF.BuffNo", g["OFF_BUFFCANCEL.BuffNo"], XI.Map.C2sBuffcancel.OffBuffNo.ToString());
        Check("BUFFCANCEL OFF.padding00", g["OFF_BUFFCANCEL.padding00"], XI.Map.C2sBuffcancel.OffPadding00.ToString());
        byte[] bcFill = HexToBytes(g["BUFFCANCEL_FILL"]);
        Check("BUFFCANCEL BUFFCANCEL_FILL_SIZE", g["BUFFCANCEL_FILL_SIZE"], bcFill.Length.ToString());
        var bc = new byte[XI.Map.C2sBuffcancel.SizeOf];
        for (int i = 0; i < bc.Length; i++) bc[i] = unchecked((byte)(i * 7 + 3));
        Check("BUFFCANCEL deterministic fill", g["BUFFCANCEL_FILL"], BitConverter.ToString(bc).Replace("-", ""));
        int coBcBuffNo = (int)ParseNum(g["OFF_BUFFCANCEL.BuffNo"]);
        int coBcPad = (int)ParseNum(g["OFF_BUFFCANCEL.padding00"]);
        Check("BUFFCANCEL GetBuffNo==raw@compiler", BitConverter.ToUInt16(bcFill, coBcBuffNo).ToString(), XI.Map.C2sBuffcancel.GetBuffNo(bcFill).ToString());
        Check("BUFFCANCEL GetPadding00==raw@compiler", BitConverter.ToUInt16(bcFill, coBcPad).ToString(), XI.Map.C2sBuffcancel.GetPadding00(bcFill).ToString());
        var bcb = new byte[XI.Map.C2sBuffcancel.SizeOf];
        XI.Map.C2sBuffcancel.SetBuffNo(bcb, 0x1234);
        XI.Map.C2sBuffcancel.SetPadding00(bcb, 0xABCD);
        Check("BUFFCANCEL SetBuffNo at compiler offset", "0x1234", $"0x{BitConverter.ToUInt16(bcb, coBcBuffNo):X4}");
        Check("BUFFCANCEL SetPadding00 at compiler offset", "0xABCD", $"0x{BitConverter.ToUInt16(bcb, coBcPad):X4}");

        // Shared C2S GP_CLI_HEADER id:9 / sync word1 are the SAME bits CBasicPacket uses (via pos),
        // re-proven for all three since each body reuses that framing unchanged.
        byte[] hb = new byte[4];
        XI.Map.C2sPos.WriteHeader(hb, 0x0E7, 0, 0x4321);
        var bp = new XI.Map.BasicPacket();
        Array.Copy(hb, bp.Buffer, 4);
        Check("NUM header id == BasicPacket opcode", "231", bp.GetOpcode().ToString());
        Check("NUM header sync == BasicPacket sequence", "17185", bp.GetSequence().ToString());
    }

    // Verifies the second bundle of fixed-width Phase-3 C2S body structs (0x059 effectend, 0x0e8 camp,
    // 0x0f0 rescue, 0x0f2 submapchange) against the shared compiled oracle map_c2s_numeric2_golden.txt.
    // effectend is the first Phase-3 body with a raw uint8[8] byte-array field (its padding00) alongside a
    // uint32, pinning SIZE=16/ALIGN=4 with no padding. camp/rescue are a single uint32 (align 4), and
    // submapchange is two uint16 (align 2). Same cross-check discipline as the numeric trio: constants vs
    // compiler, accessor-vs-raw@compiler-offset (two independent ways), whole-struct deterministic fill,
    // enum-literal pinning, and the shared C2S header framing re-proven against CBasicPacket.
    static void VerifyMapC2sNumeric2(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_c2s_numeric2_golden.txt");
        Console.WriteLine("\n== C2s numeric bodies II (0x059 effectend / 0x0e8 camp / 0x0f0 rescue / 0x0f2 submapchange) parity: C# XI.Map vs layout oracle ==");
        if (!System.IO.File.Exists(path))
        {
            Check("c2s numeric2 golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        Check("NUM2 HDR_SIZE", g["HDR_SIZE"], "4");
        Check("NUM2 HDR_OFF_sync", g["HDR_OFF_sync"], "2");

        // ---- 0x059 effectend (uint32 + uint8[8] byte-array field) -------------------------------
        Check("EFFECTEND SIZE", g["EFFECTEND_SIZE"], XI.Map.C2sEffectend.SizeOf.ToString());
        Check("EFFECTEND ALIGN", g["EFFECTEND_ALIGN"], XI.Map.C2sEffectend.AlignOf.ToString());
        Check("EFFECTEND OFF.header", g["OFF_EFFECTEND.header"], XI.Map.C2sEffectend.OffHeader.ToString());
        Check("EFFECTEND OFF.effectpara", g["OFF_EFFECTEND.effectpara"], XI.Map.C2sEffectend.OffEffectpara.ToString());
        Check("EFFECTEND OFF.padding00", g["OFF_EFFECTEND.padding00"], XI.Map.C2sEffectend.OffPadding00.ToString());
        Check("EFFECTEND padding00_len", g["EFFECTEND_padding00_len"], XI.Map.C2sEffectend.Padding00Len.ToString());
        // no-padding layout proof: padding00 starts exactly at effectpara+4 and the array fills to sizeof.
        Check("EFFECTEND array packs tight after uint32",
            "1",
            (XI.Map.C2sEffectend.OffPadding00 == XI.Map.C2sEffectend.OffEffectpara + 4 &&
             XI.Map.C2sEffectend.OffPadding00 + XI.Map.C2sEffectend.Padding00Len == XI.Map.C2sEffectend.SizeOf ? 1 : 0).ToString());
        byte[] eeFill = HexToBytes(g["EFFECTEND_FILL"]);
        Check("EFFECTEND FILL_SIZE", g["EFFECTEND_FILL_SIZE"], eeFill.Length.ToString());
        var ee = new byte[XI.Map.C2sEffectend.SizeOf];
        for (int i = 0; i < ee.Length; i++) ee[i] = unchecked((byte)(i * 7 + 3));
        Check("EFFECTEND deterministic fill", g["EFFECTEND_FILL"], BitConverter.ToString(ee).Replace("-", ""));
        int coEePara = (int)ParseNum(g["OFF_EFFECTEND.effectpara"]);
        int coEePad = (int)ParseNum(g["OFF_EFFECTEND.padding00"]);
        Check("EFFECTEND GetEffectpara==raw@compiler", BitConverter.ToUInt32(eeFill, coEePara).ToString(), XI.Map.C2sEffectend.GetEffectpara(eeFill).ToString());
        // the byte-array field reads back exactly the compiler's bytes at its offset.
        Check("EFFECTEND GetPadding00==raw@compiler",
            BitConverter.ToString(eeFill, coEePad, XI.Map.C2sEffectend.Padding00Len).Replace("-", ""),
            BitConverter.ToString(XI.Map.C2sEffectend.GetPadding00(eeFill).ToArray()).Replace("-", ""));
        var eeb = new byte[XI.Map.C2sEffectend.SizeOf];
        XI.Map.C2sEffectend.SetEffectpara(eeb, 0xDEADBEEF);
        Check("EFFECTEND SetEffectpara at compiler offset", "0xDEADBEEF", $"0x{BitConverter.ToUInt32(eeb, coEePara):X8}");

        // ---- 0x0e8 camp -------------------------------------------------------------------------
        Check("CAMP SIZE", g["CAMP_SIZE"], XI.Map.C2sCamp.SizeOf.ToString());
        Check("CAMP ALIGN", g["CAMP_ALIGN"], XI.Map.C2sCamp.AlignOf.ToString());
        Check("CAMP OFF.header", g["OFF_CAMP.header"], XI.Map.C2sCamp.OffHeader.ToString());
        Check("CAMP OFF.Mode", g["OFF_CAMP.Mode"], XI.Map.C2sCamp.OffMode.ToString());
        Check("CAMP CAMP_MODE_Toggle", g["CAMP_MODE_Toggle"], ((int)XI.Map.CampMode.Toggle).ToString());
        Check("CAMP CAMP_MODE_On", g["CAMP_MODE_On"], ((int)XI.Map.CampMode.On).ToString());
        Check("CAMP CAMP_MODE_Off", g["CAMP_MODE_Off"], ((int)XI.Map.CampMode.Off).ToString());
        byte[] campFill = HexToBytes(g["CAMP_FILL"]);
        Check("CAMP FILL_SIZE", g["CAMP_FILL_SIZE"], campFill.Length.ToString());
        var ca = new byte[XI.Map.C2sCamp.SizeOf];
        for (int i = 0; i < ca.Length; i++) ca[i] = unchecked((byte)(i * 7 + 3));
        Check("CAMP deterministic fill", g["CAMP_FILL"], BitConverter.ToString(ca).Replace("-", ""));
        int coCaMode = (int)ParseNum(g["OFF_CAMP.Mode"]);
        Check("CAMP GetMode==raw@compiler", BitConverter.ToUInt32(campFill, coCaMode).ToString(), XI.Map.C2sCamp.GetMode(campFill).ToString());
        var cab = new byte[XI.Map.C2sCamp.SizeOf];
        XI.Map.C2sCamp.SetMode(cab, XI.Map.CampMode.On);
        Check("CAMP SetMode at compiler offset", "0x00000001", $"0x{BitConverter.ToUInt32(cab, coCaMode):X8}");
        Check("CAMP GetCampMode roundtrip", "On", XI.Map.C2sCamp.GetCampMode(cab).ToString());

        // ---- 0x0f0 rescue -----------------------------------------------------------------------
        Check("RESCUE SIZE", g["RESCUE_SIZE"], XI.Map.C2sRescue.SizeOf.ToString());
        Check("RESCUE ALIGN", g["RESCUE_ALIGN"], XI.Map.C2sRescue.AlignOf.ToString());
        Check("RESCUE OFF.header", g["OFF_RESCUE.header"], XI.Map.C2sRescue.OffHeader.ToString());
        Check("RESCUE OFF.State", g["OFF_RESCUE.State"], XI.Map.C2sRescue.OffState.ToString());
        byte[] rescFill = HexToBytes(g["RESCUE_FILL"]);
        Check("RESCUE FILL_SIZE", g["RESCUE_FILL_SIZE"], rescFill.Length.ToString());
        var rs = new byte[XI.Map.C2sRescue.SizeOf];
        for (int i = 0; i < rs.Length; i++) rs[i] = unchecked((byte)(i * 7 + 3));
        Check("RESCUE deterministic fill", g["RESCUE_FILL"], BitConverter.ToString(rs).Replace("-", ""));
        int coRsState = (int)ParseNum(g["OFF_RESCUE.State"]);
        Check("RESCUE GetState==raw@compiler", BitConverter.ToUInt32(rescFill, coRsState).ToString(), XI.Map.C2sRescue.GetState(rescFill).ToString());
        var rsb = new byte[XI.Map.C2sRescue.SizeOf];
        XI.Map.C2sRescue.SetState(rsb, 0x11223344);
        Check("RESCUE SetState at compiler offset", "0x11223344", $"0x{BitConverter.ToUInt32(rsb, coRsState):X8}");

        // ---- 0x0f2 submapchange (two uint16) ----------------------------------------------------
        Check("SUBMAPCHANGE SIZE", g["SUBMAPCHANGE_SIZE"], XI.Map.C2sSubmapchange.SizeOf.ToString());
        Check("SUBMAPCHANGE ALIGN", g["SUBMAPCHANGE_ALIGN"], XI.Map.C2sSubmapchange.AlignOf.ToString());
        Check("SUBMAPCHANGE OFF.header", g["OFF_SUBMAPCHANGE.header"], XI.Map.C2sSubmapchange.OffHeader.ToString());
        Check("SUBMAPCHANGE OFF.State", g["OFF_SUBMAPCHANGE.State"], XI.Map.C2sSubmapchange.OffState.ToString());
        Check("SUBMAPCHANGE OFF.SubMapNumber", g["OFF_SUBMAPCHANGE.SubMapNumber"], XI.Map.C2sSubmapchange.OffSubMapNumber.ToString());
        Check("SUBMAPCHANGE SUBMAP_STATE_General", g["SUBMAP_STATE_General"], ((int)XI.Map.SubmapchangeState.General).ToString());
        Check("SUBMAPCHANGE SUBMAP_STATE_Event", g["SUBMAP_STATE_Event"], ((int)XI.Map.SubmapchangeState.Event).ToString());
        byte[] smFill = HexToBytes(g["SUBMAPCHANGE_FILL"]);
        Check("SUBMAPCHANGE FILL_SIZE", g["SUBMAPCHANGE_FILL_SIZE"], smFill.Length.ToString());
        var sm = new byte[XI.Map.C2sSubmapchange.SizeOf];
        for (int i = 0; i < sm.Length; i++) sm[i] = unchecked((byte)(i * 7 + 3));
        Check("SUBMAPCHANGE deterministic fill", g["SUBMAPCHANGE_FILL"], BitConverter.ToString(sm).Replace("-", ""));
        int coSmState = (int)ParseNum(g["OFF_SUBMAPCHANGE.State"]);
        int coSmSub = (int)ParseNum(g["OFF_SUBMAPCHANGE.SubMapNumber"]);
        Check("SUBMAPCHANGE GetState==raw@compiler", BitConverter.ToUInt16(smFill, coSmState).ToString(), XI.Map.C2sSubmapchange.GetState(smFill).ToString());
        Check("SUBMAPCHANGE GetSubMapNumber==raw@compiler", BitConverter.ToUInt16(smFill, coSmSub).ToString(), XI.Map.C2sSubmapchange.GetSubMapNumber(smFill).ToString());
        var smb = new byte[XI.Map.C2sSubmapchange.SizeOf];
        XI.Map.C2sSubmapchange.SetState(smb, XI.Map.SubmapchangeState.Event);
        XI.Map.C2sSubmapchange.SetSubMapNumber(smb, 0x0042);
        Check("SUBMAPCHANGE SetState at compiler offset", "0x0002", $"0x{BitConverter.ToUInt16(smb, coSmState):X4}");
        Check("SUBMAPCHANGE SetSubMapNumber at compiler offset", "0x0042", $"0x{BitConverter.ToUInt16(smb, coSmSub):X4}");
        Check("SUBMAPCHANGE GetSubmapchangeState roundtrip", "Event", XI.Map.C2sSubmapchange.GetSubmapchangeState(smb).ToString());

        // Shared C2S GP_CLI_HEADER id:9 / sync word1 == CBasicPacket opcode/sequence (via pos), re-run.
        byte[] hb = new byte[4];
        XI.Map.C2sPos.WriteHeader(hb, 0x059, 0, 0x2468);
        var bp = new XI.Map.BasicPacket();
        Array.Copy(hb, bp.Buffer, 4);
        Check("NUM2 header id == BasicPacket opcode", "89", bp.GetOpcode().ToString());
        Check("NUM2 header sync == BasicPacket sequence", "9320", bp.GetSequence().ToString());
    }

    // Verifies the first default-packed Phase-3 S2C body structs (0x05c pendingnum, 0x060 musicvolume,
    // 0x052 eventucoff) against the shared compiled oracle map_s2c_numeric_golden.txt. Unlike the first
    // S2C body (group_effects, a pack(1) inner struct -> alignof 1), these PacketData run under MSVC
    // DEFAULT packing, so the compiler decides their sizeof/alignof from natural member alignment
    // (pendingnum int32[8] -> 32/4; musicvolume two uint16 -> 4/2; eventucoff uint32 enum -> 4/4). The
    // oracle ALSO emits the GP_SERV_HEADER id/size/sync masks, and the gate proves they are byte-identical
    // to the C2S/CBasicPacket framing (S2C and C2S share ONE header encoding). Bodies are indexed from
    // offset 0 (the engine places PacketData at buffer_.data()+4; the port's offsets are body-relative).
    static void VerifyMapS2cNumeric(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_numeric_golden.txt");
        Console.WriteLine("\n== S2c numeric bodies (0x05c pendingnum / 0x060 musicvolume / 0x052 eventucoff) parity: C# XI.Map vs layout oracle ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c numeric golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // GP_SERV_HEADER framing must equal the already-proven C2S/CBasicPacket header bit layout:
        // id:9 -> bytes [0xFF,0x01], size:7 -> [0x00,0xFE], sync@2, size 4 / align 2. (Cross-check that
        // S2C and C2S share one 4-byte header encoding, not a second implementation.)
        Check("SHDR SIZE == C2S header size", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());
        Check("SHDR OFF_sync == C2S header sync", g["SHDR_OFF_sync"], XI.Map.C2sPos.OffSync.ToString());
        Check("SHDR id_mask_lo", g["SHDR_id_mask_lo"], XI.Map.C2sPos.IdMaskLoByte.ToString());
        Check("SHDR id_mask_hi", g["SHDR_id_mask_hi"], XI.Map.C2sPos.IdMaskHiByte.ToString());
        Check("SHDR size_mask_lo", g["SHDR_size_mask_lo"], XI.Map.C2sPos.SizeMaskLoByte.ToString());
        Check("SHDR size_mask_hi", g["SHDR_size_mask_hi"], XI.Map.C2sPos.SizeMaskHiByte.ToString());

        // ---- 0x05c pendingnum (int32 num[8], default packing -> size 32 / align 4) ----------------
        Check("PN PN_SIZE", g["PN_SIZE"], XI.Map.S2cPendingnum.SizeOf.ToString());
        Check("PN PN_ALIGN", g["PN_ALIGN"], XI.Map.S2cPendingnum.AlignOf.ToString());
        Check("PN OFF.num", g["OFF_PN.num"], XI.Map.S2cPendingnum.OffNum.ToString());
        Check("PN num_len", g["PN_num_len"], XI.Map.S2cPendingnum.NumLen.ToString());
        Check("PN num_count", g["PN_num_count"], XI.Map.S2cPendingnum.NumCount.ToString());
        // no-padding proof: element count * 4 == byte length == sizeof.
        Check("PN int32 array packs tight (count*4==len==size)",
            "1",
            (XI.Map.S2cPendingnum.NumCount * 4 == XI.Map.S2cPendingnum.NumLen &&
             XI.Map.S2cPendingnum.NumLen == XI.Map.S2cPendingnum.SizeOf ? 1 : 0).ToString());
        byte[] pnFill = HexToBytes(g["PN_FILL"]);
        Check("PN PN_FILL_SIZE", g["PN_FILL_SIZE"], pnFill.Length.ToString());
        var pn = new byte[XI.Map.S2cPendingnum.SizeOf];
        for (int i = 0; i < pn.Length; i++) pn[i] = unchecked((byte)(i * 7 + 3));
        Check("PN deterministic fill", g["PN_FILL"], BitConverter.ToString(pn).Replace("-", ""));
        int coPnNum = (int)ParseNum(g["OFF_PN.num"]);
        // accessor == raw little-endian int32 read at the compiler offset, for every element.
        for (int idx = 0; idx < XI.Map.S2cPendingnum.NumCount; idx++)
        {
            Check($"PN GetNum({idx})==raw@compiler",
                BitConverter.ToInt32(pnFill, coPnNum + idx * 4).ToString(),
                XI.Map.S2cPendingnum.GetNum(pnFill, idx).ToString());
        }
        // setter lands on the compiler element offset; also proves the element stride (4) and negatives.
        var pnb = new byte[XI.Map.S2cPendingnum.SizeOf];
        XI.Map.S2cPendingnum.SetNum(pnb, 0, unchecked((int)0xF00DF00Du));
        XI.Map.S2cPendingnum.SetNum(pnb, 7, 123456);
        Check("PN SetNum(0) at compiler offset", "0xF00DF00D", $"0x{unchecked((uint)BitConverter.ToInt32(pnb, coPnNum)):X8}");
        Check("PN SetNum(7) at compiler offset (last element)", "123456", BitConverter.ToInt32(pnb, coPnNum + 7 * 4).ToString());
        Check("PN GetNum(0) roundtrip", unchecked((int)0xF00DF00Du).ToString(), XI.Map.S2cPendingnum.GetNum(pnb, 0).ToString());

        // ---- 0x060 musicvolume (uint16 time@0, uint16 volume@2; size 4 / align 2) -----------------
        Check("MV MV_SIZE", g["MV_SIZE"], XI.Map.S2cMusicvolume.SizeOf.ToString());
        Check("MV MV_ALIGN", g["MV_ALIGN"], XI.Map.S2cMusicvolume.AlignOf.ToString());
        Check("MV OFF.time", g["OFF_MV.time"], XI.Map.S2cMusicvolume.OffTime.ToString());
        Check("MV OFF.volume", g["OFF_MV.volume"], XI.Map.S2cMusicvolume.OffVolume.ToString());
        Check("MV volume right after time (no padding)",
            "1",
            (XI.Map.S2cMusicvolume.OffVolume == XI.Map.S2cMusicvolume.OffTime + 2 ? 1 : 0).ToString());
        byte[] mvFill = HexToBytes(g["MV_FILL"]);
        Check("MV MV_FILL_SIZE", g["MV_FILL_SIZE"], mvFill.Length.ToString());
        var mv = new byte[XI.Map.S2cMusicvolume.SizeOf];
        for (int i = 0; i < mv.Length; i++) mv[i] = unchecked((byte)(i * 7 + 3));
        Check("MV deterministic fill", g["MV_FILL"], BitConverter.ToString(mv).Replace("-", ""));
        int coMvTime = (int)ParseNum(g["OFF_MV.time"]);
        int coMvVol = (int)ParseNum(g["OFF_MV.volume"]);
        Check("MV GetTime==raw@compiler", BitConverter.ToUInt16(mvFill, coMvTime).ToString(), XI.Map.S2cMusicvolume.GetTime(mvFill).ToString());
        Check("MV GetVolume==raw@compiler", BitConverter.ToUInt16(mvFill, coMvVol).ToString(), XI.Map.S2cMusicvolume.GetVolume(mvFill).ToString());
        var mvb = new byte[XI.Map.S2cMusicvolume.SizeOf];
        XI.Map.S2cMusicvolume.SetTime(mvb, 0x03C0);
        XI.Map.S2cMusicvolume.SetVolume(mvb, 0x00FF);
        Check("MV SetTime at compiler offset", "0x03C0", $"0x{BitConverter.ToUInt16(mvb, coMvTime):X4}");
        Check("MV SetVolume at compiler offset", "0x00FF", $"0x{BitConverter.ToUInt16(mvb, coMvVol):X4}");

        // ---- 0x052 eventucoff (uint32 enum Mode@0; size 4 / align 4) + enum-literal pinning --------
        Check("EU EU_SIZE", g["EU_SIZE"], XI.Map.S2cEventucoff.SizeOf.ToString());
        Check("EU EU_ALIGN", g["EU_ALIGN"], XI.Map.S2cEventucoff.AlignOf.ToString());
        Check("EU OFF.Mode", g["OFF_EU.Mode"], XI.Map.S2cEventucoff.OffMode.ToString());
        Check("EU EU_FILL_SIZE", g["EU_FILL_SIZE"], HexToBytes(g["EU_FILL"]).Length.ToString());
        var eu = new byte[XI.Map.S2cEventucoff.SizeOf];
        for (int i = 0; i < eu.Length; i++) eu[i] = unchecked((byte)(i * 7 + 3));
        Check("EU deterministic fill", g["EU_FILL"], BitConverter.ToString(eu).Replace("-", ""));
        int coEuMode = (int)ParseNum(g["OFF_EU.Mode"]);
        Check("EU GetMode==raw@compiler", BitConverter.ToUInt32(HexToBytes(g["EU_FILL"]), coEuMode).ToString(), XI.Map.S2cEventucoff.GetMode(HexToBytes(g["EU_FILL"])).ToString());
        // hand-authored enum literals pinned vs compiler re-emitted underlying values.
        Check("EU EU_MODE_Standard", g["EU_MODE_Standard"], ((int)XI.Map.EventucoffMode.Standard).ToString());
        Check("EU EU_MODE_EventRecvPending", g["EU_MODE_EventRecvPending"], ((int)XI.Map.EventucoffMode.EventRecvPending).ToString());
        Check("EU EU_MODE_CancelEvent", g["EU_MODE_CancelEvent"], ((int)XI.Map.EventucoffMode.CancelEvent).ToString());
        Check("EU EU_MODE_CancelInput", g["EU_MODE_CancelInput"], ((int)XI.Map.EventucoffMode.CancelInput).ToString());
        Check("EU EU_MODE_Fishing", g["EU_MODE_Fishing"], ((int)XI.Map.EventucoffMode.Fishing).ToString());
        var eub = new byte[XI.Map.S2cEventucoff.SizeOf];
        XI.Map.S2cEventucoff.SetMode(eub, XI.Map.EventucoffMode.Fishing);
        Check("EU SetMode at compiler offset", "0x00000004", $"0x{BitConverter.ToUInt32(eub, coEuMode):X8}");
        Check("EU GetEventucoffMode roundtrip", "Fishing", XI.Map.S2cEventucoff.GetEventucoffMode(eub).ToString());
    }

    // Verifies the first fixed-width char[]-string-field Phase-3 S2C bodies (0x009 message, 0x05d
    // pendingstr) against the shared compiled oracle map_s2c_chararray_golden.txt. The load-bearing
    // compiler facts: message's char Data[128] lands at the UNALIGNED offset 9 (after u32+u16+u16+u8), so
    // the 9+128=137 member span is padded by alignof 4 up to MSG_SIZE=140 (3 tail-pad bytes); pendingstr
    // is int32 num[9] (36 bytes) then four char[16] buffers packed at 36/52/68/84 -> PSTR_SIZE=100 with no
    // tail pad. Cross-checks: constants vs compiler, accessor==raw-at-compiler-offset, whole-body fill,
    // the string-field unaligned-start / tail-pad arithmetic, and NUL-padded string round-trips (with the
    // raw string bytes proven to land at the compiler's Data offset -- two independent ways).
    static void VerifyMapS2cCharArray(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_chararray_golden.txt");
        Console.WriteLine("\n== S2c char[]-string bodies (0x009 message / 0x05d pendingstr) parity: C# XI.Map vs layout oracle ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c chararray golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        Check("CHARARR SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- 0x009 message ----------------------------------------------------------------------
        Check("MSG MSG_SIZE", g["MSG_SIZE"], XI.Map.S2cMessage.SizeOf.ToString());
        Check("MSG MSG_ALIGN", g["MSG_ALIGN"], XI.Map.S2cMessage.AlignOf.ToString());
        Check("MSG OFF.UniqueNo", g["OFF_MSG.UniqueNo"], XI.Map.S2cMessage.OffUniqueNo.ToString());
        Check("MSG OFF.ActIndex", g["OFF_MSG.ActIndex"], XI.Map.S2cMessage.OffActIndex.ToString());
        Check("MSG OFF.MesNo", g["OFF_MSG.MesNo"], XI.Map.S2cMessage.OffMesNo.ToString());
        Check("MSG OFF.Attr", g["OFF_MSG.Attr"], XI.Map.S2cMessage.OffAttr.ToString());
        Check("MSG OFF.Data", g["OFF_MSG.Data"], XI.Map.S2cMessage.OffData.ToString());
        Check("MSG Data_len", g["MSG_Data_len"], XI.Map.S2cMessage.DataLen.ToString());
        // the whole point: Data starts UNALIGNED (offset 9, not a multiple of the align 4), and the
        // 137-byte member span is padded up to 140 -> exactly 3 compiler-owned tail-pad bytes.
        Check("MSG Data at unaligned offset 9", "9", g["OFF_MSG.Data"]);
        Check("MSG tail-pad = size - (9+128)",
            "3",
            (XI.Map.S2cMessage.SizeOf - (XI.Map.S2cMessage.OffData + XI.Map.S2cMessage.DataLen)).ToString());
        byte[] msgFill = HexToBytes(g["MSG_FILL"]);
        Check("MSG MSG_FILL_SIZE", g["MSG_FILL_SIZE"], msgFill.Length.ToString());
        var msg = new byte[XI.Map.S2cMessage.SizeOf];
        for (int i = 0; i < msg.Length; i++) msg[i] = unchecked((byte)(i * 7 + 3));
        Check("MSG deterministic fill", g["MSG_FILL"], BitConverter.ToString(msg).Replace("-", ""));
        int coMsgUnique = (int)ParseNum(g["OFF_MSG.UniqueNo"]);
        int coMsgAct = (int)ParseNum(g["OFF_MSG.ActIndex"]);
        int coMsgMes = (int)ParseNum(g["OFF_MSG.MesNo"]);
        int coMsgAttr = (int)ParseNum(g["OFF_MSG.Attr"]);
        int coMsgData = (int)ParseNum(g["OFF_MSG.Data"]);
        Check("MSG GetUniqueNo==raw@compiler", BitConverter.ToUInt32(msgFill, coMsgUnique).ToString(), XI.Map.S2cMessage.GetUniqueNo(msgFill).ToString());
        Check("MSG GetActIndex==raw@compiler", BitConverter.ToUInt16(msgFill, coMsgAct).ToString(), XI.Map.S2cMessage.GetActIndex(msgFill).ToString());
        Check("MSG GetMesNo==raw@compiler", BitConverter.ToUInt16(msgFill, coMsgMes).ToString(), XI.Map.S2cMessage.GetMesNo(msgFill).ToString());
        Check("MSG GetAttr==raw@compiler", msgFill[coMsgAttr].ToString(), XI.Map.S2cMessage.GetAttr(msgFill).ToString());
        // the char[128] region reads back the compiler's exact bytes at offset 9.
        Check("MSG GetDataBytes==raw@compiler",
            BitConverter.ToString(msgFill, coMsgData, XI.Map.S2cMessage.DataLen).Replace("-", ""),
            BitConverter.ToString(XI.Map.S2cMessage.GetDataBytes(msgFill).ToArray()).Replace("-", ""));
        // scalar setters land on compiler offsets.
        var msb = new byte[XI.Map.S2cMessage.SizeOf];
        XI.Map.S2cMessage.SetUniqueNo(msb, 0xAABBCCDD);
        XI.Map.S2cMessage.SetActIndex(msb, 0x1234);
        XI.Map.S2cMessage.SetAttr(msb, 0x07);
        Check("MSG SetUniqueNo at compiler offset", "0xAABBCCDD", $"0x{BitConverter.ToUInt32(msb, coMsgUnique):X8}");
        Check("MSG SetActIndex at compiler offset", "0x1234", $"0x{BitConverter.ToUInt16(msb, coMsgAct):X4}");
        Check("MSG SetAttr at compiler offset", "0x07", $"0x{msb[coMsgAttr]:X2}");
        // string round-trip + NUL padding, with the raw bytes proven at the compiler Data offset.
        var msgS = new byte[XI.Map.S2cMessage.SizeOf];
        XI.Map.S2cMessage.SetDataString(msgS, "Help desk");
        Check("MSG string round-trip", "Help desk", XI.Map.S2cMessage.GetDataString(msgS));
        Check("MSG string byte[0] at compiler Data offset", "0x48", $"0x{msgS[coMsgData]:X2}"); // 'H'
        Check("MSG NUL terminator after string", "0x00", $"0x{msgS[coMsgData + 9]:X2}");        // len 9
        // a full-length (128) write fills the field with no NUL; read returns all 128.
        var msgF = new byte[XI.Map.S2cMessage.SizeOf];
        XI.Map.S2cMessage.SetDataString(msgF, new string('Z', 128));
        Check("MSG full-length string readback len", "128", XI.Map.S2cMessage.GetDataString(msgF).Length.ToString());

        // ---- 0x05d pendingstr -------------------------------------------------------------------
        Check("PSTR PSTR_SIZE", g["PSTR_SIZE"], XI.Map.S2cPendingstr.SizeOf.ToString());
        Check("PSTR PSTR_ALIGN", g["PSTR_ALIGN"], XI.Map.S2cPendingstr.AlignOf.ToString());
        Check("PSTR OFF.num", g["OFF_PSTR.num"], XI.Map.S2cPendingstr.OffNum.ToString());
        Check("PSTR OFF.string1", g["OFF_PSTR.string1"], XI.Map.S2cPendingstr.OffString0.ToString());
        Check("PSTR num_len", g["PSTR_num_len"], XI.Map.S2cPendingstr.NumLen.ToString());
        Check("PSTR num_count", g["PSTR_num_count"], XI.Map.S2cPendingstr.NumCount.ToString());
        Check("PSTR string1_len", g["PSTR_string1_len"], XI.Map.S2cPendingstr.StringLen.ToString());
        // string buffers pack back-to-back: string1 + index*16, and 36 + 4*16 == size (no tail pad).
        Check("PSTR string buffers stride 16 with no gap",
            "1",
            (XI.Map.S2cPendingstr.OffString0 + XI.Map.S2cPendingstr.StringCount * XI.Map.S2cPendingstr.StringStride == XI.Map.S2cPendingstr.SizeOf ? 1 : 0).ToString());
        Check("PSTR no tail pad (size % align)", "0", (XI.Map.S2cPendingstr.SizeOf % XI.Map.S2cPendingstr.AlignOf).ToString());
        byte[] pstrFill = HexToBytes(g["PSTR_FILL"]);
        Check("PSTR PSTR_FILL_SIZE", g["PSTR_FILL_SIZE"], pstrFill.Length.ToString());
        var pst = new byte[XI.Map.S2cPendingstr.SizeOf];
        for (int i = 0; i < pst.Length; i++) pst[i] = unchecked((byte)(i * 7 + 3));
        Check("PSTR deterministic fill", g["PSTR_FILL"], BitConverter.ToString(pst).Replace("-", ""));
        int coPstrNum = (int)ParseNum(g["OFF_PSTR.num"]);
        for (int idx = 0; idx < XI.Map.S2cPendingstr.NumCount; idx++)
        {
            Check($"PSTR GetNum({idx})==raw@compiler",
                BitConverter.ToInt32(pstrFill, coPstrNum + idx * 4).ToString(),
                XI.Map.S2cPendingstr.GetNum(pstrFill, idx).ToString());
        }
        var pstb = new byte[XI.Map.S2cPendingstr.SizeOf];
        XI.Map.S2cPendingstr.SetNum(pstb, 0, unchecked((int)0x80000000));
        XI.Map.S2cPendingstr.SetNum(pstb, 8, -1);
        Check("PSTR SetNum(0) at compiler offset", "0x80000000", $"0x{unchecked((uint)BitConverter.ToInt32(pstb, coPstrNum)):X8}");
        Check("PSTR SetNum(8) at compiler offset (last)", "-1", BitConverter.ToInt32(pstb, coPstrNum + 8 * 4).ToString());
        // char[16] string buffers: the compiler offsets 36/52/68/84 are proven here (index vs raw).
        int coPstrS1 = (int)ParseNum(g["OFF_PSTR.string1"]);
        Check("PSTR StringOffset(0) == compiler string1", g["OFF_PSTR.string1"], XI.Map.S2cPendingstr.StringOffset(0).ToString());
        int coPstrS4 = coPstrS1 + 3 * 16;
        Check("PSTR StringOffset(3) packs at compiler string4 offset",
            "1",
            (XI.Map.S2cPendingstr.StringOffset(3) == coPstrS4 ? 1 : 0).ToString());
        var psts = new byte[XI.Map.S2cPendingstr.SizeOf];
        XI.Map.S2cPendingstr.SetString(psts, 0, "abcd");
        XI.Map.S2cPendingstr.SetString(psts, 3, "hello");
        Check("PSTR GetString(0) round-trip", "abcd", XI.Map.S2cPendingstr.GetString(psts, 0));
        Check("PSTR GetString(3) round-trip", "hello", XI.Map.S2cPendingstr.GetString(psts, 3));
        Check("PSTR string[0] byte lands at compiler string1 offset", "0x61", $"0x{psts[coPstrS1]:X2}"); // 'a'
        Check("PSTR NUL terminator after string 0", "0x00", $"0x{psts[coPstrS1 + 4]:X2}");
    }

    // Verifies Phase-3 chunk 11 against the compiled oracle map_logout_golden.txt: the first S2C body with
    // a NESTED fixed-width struct member (0x00b GP_SERV_COMMAND_LOGOUT::PacketData, embedding
    // GP_SERV_LOGOUTSUB Iwasaki) and a simple scalar C2S control (0x011 zone_transition). The load-bearing
    // compiler fact is that Iwasaki's own alignof (4, from its uint32 ip) RE-ALIGNS it inside the outer
    // default-packed body: LogoutState@0 (u8) leaves 3 pad bytes, so Iwasaki@4 (NOT 1) and cliErrCode@20
    // (NOT 17), giving LOGOUT_SIZE=24 / LOGOUT_ALIGN=4. Cross-checks: nested-struct + outer-body constants
    // vs compiler, the re-alignment arithmetic (Iwasaki != 1, == 4), accessor==raw-at-compiler-offset for
    // the nested ip/port and the enum-backed u8/u32 fields, whole-struct fills, both hand-authored enum
    // literal sets (LogoutState 11 members, GameEcode 8) pinned vs the compiler's re-emitted values, and
    // the shared C2S header framing re-proven against CBasicPacket for the zone_transition control.
    static void VerifyMapLogout(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_logout_golden.txt");
        Console.WriteLine("\n== Logout nested-struct body (0x00b) + zone_transition control (0x011) parity: C# XI.Map vs layout oracle ==");
        if (!System.IO.File.Exists(path))
        {
            Check("logout golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        Check("LO SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- nested GP_SERV_LOGOUTSUB ------------------------------------------------------------
        Check("LSUB LOGOUTSUB_SIZE", g["LOGOUTSUB_SIZE"], XI.Map.LogoutSub.SizeOf.ToString());
        Check("LSUB LOGOUTSUB_ALIGN", g["LOGOUTSUB_ALIGN"], XI.Map.LogoutSub.AlignOf.ToString());
        Check("LSUB OFF.ip", g["OFF_LOGOUTSUB.ip"], XI.Map.LogoutSub.OffIp.ToString());
        Check("LSUB OFF.port", g["OFF_LOGOUTSUB.port"], XI.Map.LogoutSub.OffPort.ToString());
        Check("LSUB OFF.padding00", g["OFF_LOGOUTSUB.padding00"], XI.Map.LogoutSub.OffPadding00.ToString());
        Check("LSUB padding00_len", g["LOGOUTSUB_padding00_len"], XI.Map.LogoutSub.Padding00Len.ToString());

        // ---- PD_LOGOUT outer body (nested struct RE-ALIGNED) -------------------------------------
        Check("LOGOUT LOGOUT_SIZE", g["LOGOUT_SIZE"], XI.Map.S2cLogout.SizeOf.ToString());
        Check("LOGOUT LOGOUT_ALIGN", g["LOGOUT_ALIGN"], XI.Map.S2cLogout.AlignOf.ToString());
        Check("LOGOUT OFF.LogoutState", g["OFF_LOGOUT.LogoutState"], XI.Map.S2cLogout.OffLogoutState.ToString());
        Check("LOGOUT OFF.Iwasaki", g["OFF_LOGOUT.Iwasaki"], XI.Map.S2cLogout.OffIwasaki.ToString());
        Check("LOGOUT OFF.cliErrCode", g["OFF_LOGOUT.cliErrCode"], XI.Map.S2cLogout.OffCliErrCode.ToString());
        // the re-alignment this slice exists to prove: Iwasaki is at the compiler's 4 (== align), NOT right
        // after the u8 LogoutState (offset 1) -> 3 pad bytes; and cliErrCode follows Iwasaki+16 (== 20).
        Check("LOGOUT Iwasaki re-aligned to 4 (not byte-after-u8)",
            "1",
            (XI.Map.S2cLogout.OffIwasaki == 4 && XI.Map.S2cLogout.OffIwasaki != XI.Map.S2cLogout.OffLogoutState + 1 ? 1 : 0).ToString());
        Check("LOGOUT cliErrCode follows Iwasaki+16",
            "1",
            (XI.Map.S2cLogout.OffCliErrCode == XI.Map.S2cLogout.OffIwasaki + XI.Map.LogoutSub.SizeOf ? 1 : 0).ToString());
        Check("LOGOUT Iwasaki on its align boundary",
            "0",
            (XI.Map.S2cLogout.OffIwasaki % XI.Map.LogoutSub.AlignOf).ToString());
        byte[] loFill = HexToBytes(g["LOGOUT_FILL"]);
        Check("LOGOUT LOGOUT_FILL_SIZE", g["LOGOUT_FILL_SIZE"], loFill.Length.ToString());
        var lo = new byte[XI.Map.S2cLogout.SizeOf];
        for (int i = 0; i < lo.Length; i++) lo[i] = unchecked((byte)(i * 7 + 3));
        Check("LOGOUT deterministic fill", g["LOGOUT_FILL"], BitConverter.ToString(lo).Replace("-", ""));
        // compiler-emitted offsets for the nested/enum reads.
        int coLoIwasaki = (int)ParseNum(g["OFF_LOGOUT.Iwasaki"]);
        int coLoSubIp = coLoIwasaki + (int)ParseNum(g["OFF_LOGOUTSUB.ip"]);
        int coLoSubPort = coLoIwasaki + (int)ParseNum(g["OFF_LOGOUTSUB.port"]);
        int coLoState = (int)ParseNum(g["OFF_LOGOUT.LogoutState"]);
        int coLoErr = (int)ParseNum(g["OFF_LOGOUT.cliErrCode"]);
        // accessor == raw read at the compiler's nested-inclusive offset (two independent ways).
        Check("LOGOUT GetIp==raw@compiler(nested)",
            BitConverter.ToUInt32(loFill, coLoSubIp).ToString(),
            XI.Map.S2cLogout.GetIp(loFill).ToString());
        Check("LOGOUT GetPort==raw@compiler(nested)",
            BitConverter.ToUInt32(loFill, coLoSubPort).ToString(),
            XI.Map.S2cLogout.GetPort(loFill).ToString());
        Check("LOGOUT GetLogoutStateByte==raw@compiler",
            loFill[coLoState].ToString(),
            XI.Map.S2cLogout.GetLogoutStateByte(loFill).ToString());
        Check("LOGOUT GetCliErrCodeByte==raw@compiler",
            BitConverter.ToUInt32(loFill, coLoErr).ToString(),
            XI.Map.S2cLogout.GetCliErrCodeByte(loFill).ToString());
        // setters land on the compiler's nested/enum offsets.
        var lob = new byte[XI.Map.S2cLogout.SizeOf];
        XI.Map.S2cLogout.SetIp(lob, 0x0A010102);
        XI.Map.S2cLogout.SetPort(lob, 0x000054FF);
        XI.Map.S2cLogout.SetLogoutState(lob, XI.Map.LogoutState.Zonechange);
        XI.Map.S2cLogout.SetCliErrCode(lob, XI.Map.GameEcode.Zonedown);
        Check("LOGOUT SetIp at compiler nested offset", "0x0A010102", $"0x{BitConverter.ToUInt32(lob, coLoSubIp):X8}");
        Check("LOGOUT SetPort at compiler nested offset", "0x000054FF", $"0x{BitConverter.ToUInt32(lob, coLoSubPort):X8}");
        Check("LOGOUT SetLogoutState at compiler offset", "0x02", $"0x{lob[coLoState]:X2}");
        Check("LOGOUT SetCliErrCode at compiler offset", "0x00000002", $"0x{BitConverter.ToUInt32(lob, coLoErr):X8}");
        Check("LOGOUT GetLogoutState roundtrip", "Zonechange", XI.Map.S2cLogout.GetLogoutState(lob).ToString());
        Check("LOGOUT GetCliErrCode roundtrip", "Zonedown", XI.Map.S2cLogout.GetCliErrCode(lob).ToString());
        // hand-authored enum literals pinned vs compiler re-emitted underlying values.
        Check("LOGOUT LGS_NONE", g["LGS_NONE"], ((int)XI.Map.LogoutState.None).ToString());
        Check("LOGOUT LGS_LOGOUT", g["LGS_LOGOUT"], ((int)XI.Map.LogoutState.Logout).ToString());
        Check("LOGOUT LGS_ZONECHANGE", g["LGS_ZONECHANGE"], ((int)XI.Map.LogoutState.Zonechange).ToString());
        Check("LOGOUT LGS_MYROOM", g["LGS_MYROOM"], ((int)XI.Map.LogoutState.Myroom).ToString());
        Check("LOGOUT LGS_CANCELL", g["LGS_CANCELL"], ((int)XI.Map.LogoutState.Cancell).ToString());
        Check("LOGOUT LGS_POLEXIT", g["LGS_POLEXIT"], ((int)XI.Map.LogoutState.Polexit).ToString());
        Check("LOGOUT LGS_JOBEXIT", g["LGS_JOBEXIT"], ((int)XI.Map.LogoutState.Jobexit).ToString());
        Check("LOGOUT LGS_POLEXIT_MYROOM", g["LGS_POLEXIT_MYROOM"], ((int)XI.Map.LogoutState.PolexitMyroom).ToString());
        Check("LOGOUT LGS_TIMEOUT", g["LGS_TIMEOUT"], ((int)XI.Map.LogoutState.Timeout).ToString());
        Check("LOGOUT LGS_GMLOGOUT", g["LGS_GMLOGOUT"], ((int)XI.Map.LogoutState.Gmlogout).ToString());
        Check("LOGOUT LGS_END", g["LGS_END"], ((int)XI.Map.LogoutState.End).ToString());
        Check("LOGOUT EC_NOERR", g["EC_NOERR"], ((int)XI.Map.GameEcode.Noerr).ToString());
        Check("LOGOUT EC_RESERR", g["EC_RESERR"], ((int)XI.Map.GameEcode.Reserr).ToString());
        Check("LOGOUT EC_ZONEDOWN", g["EC_ZONEDOWN"], ((int)XI.Map.GameEcode.Zonedown).ToString());
        Check("LOGOUT EC_REGERR", g["EC_REGERR"], ((int)XI.Map.GameEcode.Regerr).ToString());
        Check("LOGOUT EC_CLIVERSERR", g["EC_CLIVERSERR"], ((int)XI.Map.GameEcode.Cliverserr).ToString());
        Check("LOGOUT EC_CLINOEXERR", g["EC_CLINOEXERR"], ((int)XI.Map.GameEcode.Clinoexerr).ToString());
        Check("LOGOUT EC_UNKNOWN", g["EC_UNKNOWN"], ((int)XI.Map.GameEcode.Unknown).ToString());
        Check("LOGOUT EC_MAX", g["EC_MAX"], ((int)XI.Map.GameEcode.Max).ToString());

        // ---- 0x011 zone_transition (scalar C2S control) ------------------------------------------
        Check("ZTRANS ZTRANS_SIZE", g["ZTRANS_SIZE"], XI.Map.C2sZoneTransition.SizeOf.ToString());
        Check("ZTRANS ZTRANS_ALIGN", g["ZTRANS_ALIGN"], XI.Map.C2sZoneTransition.AlignOf.ToString());
        Check("ZTRANS OFF.header", g["OFF_ZTRANS.header"], XI.Map.C2sZoneTransition.OffHeader.ToString());
        Check("ZTRANS OFF.unknown00", g["OFF_ZTRANS.unknown00"], XI.Map.C2sZoneTransition.OffUnknown00.ToString());
        Check("ZTRANS OFF.unknown01", g["OFF_ZTRANS.unknown01"], XI.Map.C2sZoneTransition.OffUnknown01.ToString());
        Check("ZTRANS OFF.padding00", g["OFF_ZTRANS.padding00"], XI.Map.C2sZoneTransition.OffPadding00.ToString());
        byte[] ztFill = HexToBytes(g["ZTRANS_FILL"]);
        Check("ZTRANS ZTRANS_FILL_SIZE", g["ZTRANS_FILL_SIZE"], ztFill.Length.ToString());
        var zt = new byte[XI.Map.C2sZoneTransition.SizeOf];
        for (int i = 0; i < zt.Length; i++) zt[i] = unchecked((byte)(i * 7 + 3));
        Check("ZTRANS deterministic fill", g["ZTRANS_FILL"], BitConverter.ToString(zt).Replace("-", ""));
        int coZtU0 = (int)ParseNum(g["OFF_ZTRANS.unknown00"]);
        int coZtU1 = (int)ParseNum(g["OFF_ZTRANS.unknown01"]);
        int coZtPad = (int)ParseNum(g["OFF_ZTRANS.padding00"]);
        Check("ZTRANS GetUnknown00==raw@compiler", ztFill[coZtU0].ToString(), XI.Map.C2sZoneTransition.GetUnknown00(ztFill).ToString());
        Check("ZTRANS GetUnknown01==raw@compiler", ztFill[coZtU1].ToString(), XI.Map.C2sZoneTransition.GetUnknown01(ztFill).ToString());
        Check("ZTRANS GetPadding00==raw@compiler", BitConverter.ToUInt16(ztFill, coZtPad).ToString(), XI.Map.C2sZoneTransition.GetPadding00(ztFill).ToString());
        var ztb = new byte[XI.Map.C2sZoneTransition.SizeOf];
        XI.Map.C2sZoneTransition.SetUnknown00(ztb, 0x11);
        XI.Map.C2sZoneTransition.SetPadding00(ztb, 0x2233);
        Check("ZTRANS SetUnknown00 at compiler offset", "0x11", $"0x{ztb[coZtU0]:X2}");
        Check("ZTRANS SetPadding00 at compiler offset", "0x2233", $"0x{BitConverter.ToUInt16(ztb, coZtPad):X4}");
        // shared C2S GP_CLI_HEADER id:9 / sync == BasicPacket opcode/sequence (via pos).
        byte[] hb = new byte[4];
        XI.Map.C2sPos.WriteHeader(hb, 0x011, 0, 0x0F0F);
        var bp = new XI.Map.BasicPacket();
        Array.Copy(hb, bp.Buffer, 4);
        Check("ZTRANS header id == BasicPacket opcode", "17", bp.GetOpcode().ToString());
        Check("ZTRANS header sync == BasicPacket sequence", "3855", bp.GetSequence().ToString());
    }

    // Verifies the first multi-nibble bit-field Phase-3 C2S body (0x0c4 GP_CLI_COMMAND_GROUP_COMLINK_ACTIVE)
    // against the compiled oracle map_c2s_comlink_active_golden.txt. The four `uint16:4` fields r/g/b/a
    // share one 2-byte uint16 storage unit at bytes 4..5 (right after the 4-byte GP_CLI_HEADER) under
    // DEFAULT packing. Because offsetof is ILLEGAL on a bit-field (C2618), the oracle derived each nibble's
    // byte + low/high position by a RUNTIME SENTINEL SCAN (set the field to 0xF, find the single byte that
    // changed: value 0x0F => low nibble, 0xF0 => high nibble). The gate asserts the C# nibble offsets/masks
    // equal the compiler's BF_*_off/_val, proves each nibble writes only its own half of the shared byte
    // (matching C++ bit-field assignment), pins the two hand-authored enums, the fixed-width member offsets,
    // and the whole-struct i*7+3 fill, plus the shared C2S header framing against CBasicPacket.
    static void VerifyMapC2sComlinkActive(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_c2s_comlink_active_golden.txt");
        Console.WriteLine("\n== C2s comlink_active (0x0c4) multi-nibble bit-field body parity: C# XI.Map vs layout oracle ==");
        if (!System.IO.File.Exists(path))
        {
            Check("c2s comlink_active golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        Check("CLA HDR_SIZE", g["HDR_SIZE"], XI.Map.C2sComlinkActive.HeaderSize.ToString());
        Check("CLA HDR_OFF_sync", g["HDR_OFF_sync"], XI.Map.C2sComlinkActive.OffSync.ToString());

        // Body layout (default packing; the nibble run fills bytes 4..5 of one uint16 unit).
        Check("CLA CL_SIZE", g["CL_SIZE"], XI.Map.C2sComlinkActive.SizeOf.ToString());
        Check("CLA CL_ALIGN", g["CL_ALIGN"], XI.Map.C2sComlinkActive.AlignOf.ToString());
        Check("CLA OFF.ItemIndex", g["OFF_CL.ItemIndex"], XI.Map.C2sComlinkActive.OffItemIndex.ToString());
        Check("CLA OFF.Category", g["OFF_CL.Category"], XI.Map.C2sComlinkActive.OffCategory.ToString());
        Check("CLA OFF.ActiveFlg", g["OFF_CL.ActiveFlg"], XI.Map.C2sComlinkActive.OffActiveFlg.ToString());
        Check("CLA OFF.padding00", g["OFF_CL.padding00"], XI.Map.C2sComlinkActive.OffPadding00.ToString());
        Check("CLA padding00_len", g["CL_padding00_len"], XI.Map.C2sComlinkActive.Padding00Len.ToString());
        Check("CLA OFF.name", g["OFF_CL.sComLinkName"], XI.Map.C2sComlinkActive.OffName.ToString());
        Check("CLA name_len", g["CL_name_len"], XI.Map.C2sComlinkActive.NameLen.ToString());
        Check("CLA OFF.LinkshellId", g["OFF_CL.LinkshellId"], XI.Map.C2sComlinkActive.OffLinkshellId.ToString());

        // The sentinel-scan nibble facts: r low byte4, g high byte4, b low byte5, a high byte5.
        Check("CLA nibble run base == byte 4", g["BF_r_off"], XI.Map.C2sComlinkActive.OffNibbles.ToString());
        Check("CLA r is low nibble (mask 0x0F)", g["BF_r_val"], XI.Map.C2sComlinkActive.NibbleLowMask.ToString());
        Check("CLA g is high nibble (mask 0xF0)", g["BF_g_val"], XI.Map.C2sComlinkActive.NibbleHighMask.ToString());
        Check("CLA b low-nibble byte == base+1", g["BF_b_off"], (XI.Map.C2sComlinkActive.OffNibbles + 1).ToString());
        Check("CLA b low mask", g["BF_b_val"], XI.Map.C2sComlinkActive.NibbleLowMask.ToString());
        Check("CLA a high-nibble byte == base+1", g["BF_a_off"], (XI.Map.C2sComlinkActive.OffNibbles + 1).ToString());
        Check("CLA a high mask", g["BF_a_val"], XI.Map.C2sComlinkActive.NibbleHighMask.ToString());

        // Whole-struct deterministic fill (catches padding bytes 9..11, 26, and the nibble bytes 4..5).
        byte[] clFill = HexToBytes(g["CL_FILL"]);
        Check("CLA CL_FILL_SIZE", g["CL_FILL_SIZE"], clFill.Length.ToString());
        var cl = new byte[XI.Map.C2sComlinkActive.SizeOf];
        for (int i = 0; i < cl.Length; i++) cl[i] = unchecked((byte)(i * 7 + 3));
        Check("CLA deterministic fill", g["CL_FILL"], BitConverter.ToString(cl).Replace("-", ""));

        // Read-path cross-check: accessor == the compiler's own extraction from the raw fill bytes.
        int coNib = (int)ParseNum(g["BF_r_off"]);
        Check("CLA GetR==raw low nibble@compiler", (clFill[coNib] & 0x0F).ToString(), XI.Map.C2sComlinkActive.GetR(clFill).ToString());
        Check("CLA GetG==raw high nibble@compiler", (clFill[coNib] >> 4).ToString(), XI.Map.C2sComlinkActive.GetG(clFill).ToString());
        Check("CLA GetB==raw low nibble@compiler(base+1)", (clFill[coNib + 1] & 0x0F).ToString(), XI.Map.C2sComlinkActive.GetB(clFill).ToString());
        Check("CLA GetA==raw high nibble@compiler(base+1)", (clFill[coNib + 1] >> 4).ToString(), XI.Map.C2sComlinkActive.GetA(clFill).ToString());
        Check("CLA GetItemIndex==raw@compiler", clFill[XI.Map.C2sComlinkActive.OffItemIndex].ToString(), XI.Map.C2sComlinkActive.GetItemIndex(clFill).ToString());
        Check("CLA GetCategory==raw@compiler", clFill[XI.Map.C2sComlinkActive.OffCategory].ToString(), XI.Map.C2sComlinkActive.GetCategory(clFill).ToString());
        Check("CLA GetLinkshellId==raw@compiler", clFill[XI.Map.C2sComlinkActive.OffLinkshellId].ToString(), XI.Map.C2sComlinkActive.GetLinkshellId(clFill).ToString());

        // Write-path: setting one nibble changes ONLY its half of the shared byte (C++ bit-field semantics),
        // and the other nibble survives. Prove both directions on byte 4 (r low, g high) and byte 5 (b/a).
        var nib = new byte[XI.Map.C2sComlinkActive.SizeOf];
        XI.Map.C2sComlinkActive.SetR(nib, 0xA);
        Check("CLA SetR writes low nibble only", "0x0A", $"0x{nib[coNib]:X2}");
        XI.Map.C2sComlinkActive.SetG(nib, 0x5);
        Check("CLA SetG preserves r + writes high", "0x5A", $"0x{nib[coNib]:X2}"); // 0x5 high | 0xA low
        Check("CLA GetR after SetG survives", "10", XI.Map.C2sComlinkActive.GetR(nib).ToString());   // 0x0A
        Check("CLA GetG after SetG", "5", XI.Map.C2sComlinkActive.GetG(nib).ToString());
        XI.Map.C2sComlinkActive.SetB(nib, 0xC);
        XI.Map.C2sComlinkActive.SetA(nib, 0x3);
        Check("CLA SetA/SetB byte5", "0x3C", $"0x{nib[coNib + 1]:X2}"); // 0x3 high | 0xC low
        Check("CLA GetB", "12", XI.Map.C2sComlinkActive.GetB(nib).ToString());
        Check("CLA GetA", "3", XI.Map.C2sComlinkActive.GetA(nib).ToString());

        // Hand-authored enum literals pinned vs compiler re-emitted underlying values.
        Check("CLA ACTIVEFLG_Unequip", g["ACTIVEFLG_Unequip"], ((int)XI.Map.ComlinkActiveFlag.Unequip).ToString());
        Check("CLA ACTIVEFLG_EquipOrCreate", g["ACTIVEFLG_EquipOrCreate"], ((int)XI.Map.ComlinkActiveFlag.EquipOrCreate).ToString());
        Check("CLA LINKSHELLID_Linkshell1", g["LINKSHELLID_Linkshell1"], ((int)XI.Map.LinkshellId.Linkshell1).ToString());
        Check("CLA LINKSHELLID_Linkshell2", g["LINKSHELLID_Linkshell2"], ((int)XI.Map.LinkshellId.Linkshell2).ToString());
        var efb = new byte[XI.Map.C2sComlinkActive.SizeOf];
        XI.Map.C2sComlinkActive.SetActiveFlg(efb, XI.Map.ComlinkActiveFlag.EquipOrCreate);
        XI.Map.C2sComlinkActive.SetLinkshellId(efb, XI.Map.LinkshellId.Linkshell2);
        Check("CLA SetActiveFlg at compiler offset", "0x01", $"0x{efb[XI.Map.C2sComlinkActive.OffActiveFlg]:X2}");
        Check("CLA SetLinkshellId at compiler offset", "0x02", $"0x{efb[XI.Map.C2sComlinkActive.OffLinkshellId]:X2}");
        Check("CLA GetComlinkActiveFlag roundtrip", "EquipOrCreate", XI.Map.C2sComlinkActive.GetComlinkActiveFlag(efb).ToString());
        Check("CLA GetLinkshellIdEnum roundtrip", "Linkshell2", XI.Map.C2sComlinkActive.GetLinkshellIdEnum(efb).ToString());

        // Shared C2S GP_CLI_HEADER id:9 / sync word1 == CBasicPacket opcode/sequence (via pos).
        byte[] hb = new byte[4];
        XI.Map.C2sPos.WriteHeader(hb, 0x0C4, 0, 0x1357);
        var bp = new XI.Map.BasicPacket();
        Array.Copy(hb, bp.Buffer, 4);
        Check("CLA header id == BasicPacket opcode", "196", bp.GetOpcode().ToString());
        Check("CLA header sync == BasicPacket sequence", "4951", bp.GetSequence().ToString());
    }

    // Verifies the layout-dense Phase-3 C2S body 0x052 GP_CLI_COMMAND_EQUIPSET_CHECK against the compiled
    // oracle map_c2s_equipset_check_golden.txt. It fuses two packing techniques: (a) a nested struct
    // equipsetrequestitem_t whose byte 0 is a sentinel-scan-derived bit-field run (HasItemFlg mask 0x01
    // shift 0 / RemoveItemFlg mask 0x02 shift 1 / Category mask 0xFC shift 2) and whose uint16 ItemNo
    // realigns to offset 2 (ERI size 4 / align 2); (b) that struct embedded BOTH as a single member
    // (ItemChange@8) and a 16-element array (Equipment@12, stride 4, no inter-element padding) -> ESC size
    // 76 / align 2. Cross-checks: every constant vs compiler, the element/field offset math against the
    // compiler's ESC_IC_ItemNo/ESC_E1_ItemNo/ESC_E15_ItemIndex, whole-struct fill (catches padding bytes
    // 5..7 and the stride), bit-field read-modify-write preserving the sibling bits, and accessor==raw at
    // the compiler's nested-inclusive offset for both the single member and an array element.
    static void VerifyMapC2sEquipsetCheck(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_c2s_equipset_check_golden.txt");
        Console.WriteLine("\n== C2s equipset_check (0x052) nested bit-field + 16-array body parity: C# XI.Map vs layout oracle ==");
        if (!System.IO.File.Exists(path))
        {
            Check("c2s equipset_check golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        Check("ESC HDR_SIZE", g["HDR_SIZE"], XI.Map.C2sEquipsetCheck.HeaderSize.ToString());
        Check("ESC HDR_OFF_sync", g["HDR_OFF_sync"], XI.Map.C2sEquipsetCheck.OffSync.ToString());

        // inner equipsetrequestitem_t layout
        Check("ERI ERI_SIZE", g["ERI_SIZE"], XI.Map.EquipsetRequestItem.SizeOf.ToString());
        Check("ERI ERI_ALIGN", g["ERI_ALIGN"], XI.Map.EquipsetRequestItem.AlignOf.ToString());
        Check("ERI OFF.ItemIndex", g["OFF_ERI.ItemIndex"], XI.Map.EquipsetRequestItem.OffItemIndex.ToString());
        Check("ERI OFF.ItemNo", g["OFF_ERI.ItemNo"], XI.Map.EquipsetRequestItem.OffItemNo.ToString());
        // the uint16 realigns to offset 2, NOT right after the bit-byte -- so the bit-byte(1)+ItemIndex(1)
        // fill exactly to 2 and ItemNo is on its 2-byte boundary.
        Check("ERI ItemNo on 2-byte boundary", "0", (XI.Map.EquipsetRequestItem.OffItemNo % 2).ToString());
        // sentinel-scan bit-field masks/shifts vs compiler.
        Check("ERI BF HasItemFlg off", g["BF_HasItemFlg_off"], XI.Map.EquipsetRequestItem.OffBitByte.ToString());
        Check("ERI BF HasItemFlg mask", g["BF_HasItemFlg_mask"], XI.Map.EquipsetRequestItem.HasItemFlgMask.ToString());
        Check("ERI BF HasItemFlg shift", g["BF_HasItemFlg_shift"], XI.Map.EquipsetRequestItem.HasItemFlgShift.ToString());
        Check("ERI BF RemoveItemFlg off", g["BF_RemoveItemFlg_off"], XI.Map.EquipsetRequestItem.OffBitByte.ToString());
        Check("ERI BF RemoveItemFlg mask", g["BF_RemoveItemFlg_mask"], XI.Map.EquipsetRequestItem.RemoveItemFlgMask.ToString());
        Check("ERI BF RemoveItemFlg shift", g["BF_RemoveItemFlg_shift"], XI.Map.EquipsetRequestItem.RemoveItemFlgShift.ToString());
        Check("ERI BF Category off", g["BF_Category_off"], XI.Map.EquipsetRequestItem.OffBitByte.ToString());
        Check("ERI BF Category mask", g["BF_Category_mask"], XI.Map.EquipsetRequestItem.CategoryMask.ToString());
        Check("ERI BF Category shift", g["BF_Category_shift"], XI.Map.EquipsetRequestItem.CategoryShift.ToString());
        Check("ERI BF Category mask == width<<shift",
            "1",
            ((XI.Map.EquipsetRequestItem.CategoryWidth << XI.Map.EquipsetRequestItem.CategoryShift) == XI.Map.EquipsetRequestItem.CategoryMask ? 1 : 0).ToString());

        // outer GP_CLI_COMMAND_EQUIPSET_CHECK layout
        Check("ESC ESC_SIZE", g["ESC_SIZE"], XI.Map.C2sEquipsetCheck.SizeOf.ToString());
        Check("ESC ESC_ALIGN", g["ESC_ALIGN"], XI.Map.C2sEquipsetCheck.AlignOf.ToString());
        Check("ESC OFF.header", g["OFF_ESC.header"], XI.Map.C2sEquipsetCheck.OffHeader.ToString());
        Check("ESC OFF.EquipKind", g["OFF_ESC.EquipKind"], XI.Map.C2sEquipsetCheck.OffEquipKind.ToString());
        Check("ESC OFF.padding00", g["OFF_ESC.padding00"], XI.Map.C2sEquipsetCheck.OffPadding00.ToString());
        Check("ESC padding00_len", g["ESC_padding00_len"], XI.Map.C2sEquipsetCheck.Padding00Len.ToString());
        Check("ESC OFF.ItemChange", g["OFF_ESC.ItemChange"], XI.Map.C2sEquipsetCheck.OffItemChange.ToString());
        Check("ESC OFF.Equipment", g["OFF_ESC.Equipment"], XI.Map.C2sEquipsetCheck.OffEquipment.ToString());
        Check("ESC Equipment_count", g["ESC_Equipment_count"], XI.Map.C2sEquipsetCheck.EquipmentCount.ToString());
        Check("ESC ITEM_STRIDE", g["ESC_ITEM_STRIDE"], XI.Map.C2sEquipsetCheck.ItemStride.ToString());
        // stride == inner sizeof (no inter-element padding), and the array fills to sizeof (no tail pad).
        Check("ESC stride == EquipsetRequestItem.SizeOf",
            "1",
            (XI.Map.C2sEquipsetCheck.ItemStride == XI.Map.EquipsetRequestItem.SizeOf ? 1 : 0).ToString());
        Check("ESC array fills to size (no tail pad)",
            "1",
            (XI.Map.C2sEquipsetCheck.OffEquipment + XI.Map.C2sEquipsetCheck.EquipmentCount * XI.Map.C2sEquipsetCheck.ItemStride == XI.Map.C2sEquipsetCheck.SizeOf ? 1 : 0).ToString());
        // element/field offset math vs compiler-emitted absolute offsets (proves FieldOffset == compiler).
        Check("ESC FieldOffset(IC-relative ItemChange.ItemNo) == compiler IC_ItemNo",
            g["ESC_IC_ItemNo"],
            (XI.Map.C2sEquipsetCheck.OffItemChange + XI.Map.EquipsetRequestItem.OffItemNo).ToString());
        Check("ESC Equipment[1].ItemNo offset == compiler E1_ItemNo",
            g["ESC_E1_ItemNo"],
            XI.Map.C2sEquipsetCheck.FieldOffset(1, XI.Map.EquipsetRequestItem.OffItemNo).ToString());
        Check("ESC Equipment[15].ItemIndex offset == compiler E15_ItemIndex",
            g["ESC_E15_ItemIndex"],
            XI.Map.C2sEquipsetCheck.FieldOffset(15, XI.Map.EquipsetRequestItem.OffItemIndex).ToString());

        // whole-struct fill (catches padding bytes 5..7 and the array stride/gaps).
        byte[] escFill = HexToBytes(g["ESC_FILL"]);
        Check("ESC ESC_FILL_SIZE", g["ESC_FILL_SIZE"], escFill.Length.ToString());
        var esc = new byte[XI.Map.C2sEquipsetCheck.SizeOf];
        for (int i = 0; i < esc.Length; i++) esc[i] = unchecked((byte)(i * 7 + 3));
        Check("ESC deterministic fill", g["ESC_FILL"], BitConverter.ToString(esc).Replace("-", ""));

        // inner-struct fill cross-check (the bit-byte + ItemIndex + ItemNo of ERI_FILL).
        byte[] eriFill = HexToBytes(g["ERI_FILL"]);
        Check("ERI ERI_FILL_SIZE", g["ERI_FILL_SIZE"], eriFill.Length.ToString());
        Check("ERI GetItemNo==raw@compiler(ERI_FILL)", BitConverter.ToUInt16(eriFill, XI.Map.EquipsetRequestItem.OffItemNo).ToString(), XI.Map.C2sEquipsetCheck.GetItemNo(eriFill, XI.Map.EquipsetRequestItem.OffItemNo).ToString());
        Check("ERI GetItemIndex==raw@compiler(ERI_FILL)", eriFill[XI.Map.EquipsetRequestItem.OffItemIndex].ToString(), XI.Map.C2sEquipsetCheck.GetItemIndex(eriFill, XI.Map.EquipsetRequestItem.OffItemIndex).ToString());

        // read-path: single-member + array-element bit-field/no accessors == raw read at the compiler offset.
        int coIc = (int)ParseNum(g["OFF_ESC.ItemChange"]);
        int coBitByte = coIc + (int)ParseNum(g["BF_HasItemFlg_off"]);
        int coItemNoIc = coIc + (int)ParseNum(g["OFF_ERI.ItemNo"]);
        Check("ESC IcGetCategory==raw@compiler", (((escFillBit(escFill, coBitByte)) & XI.Map.EquipsetRequestItem.CategoryMask) >> XI.Map.EquipsetRequestItem.CategoryShift).ToString(), XI.Map.C2sEquipsetCheck.IcGetCategory(escFill).ToString());
        Check("ESC IcGetItemNo==raw@compiler", BitConverter.ToUInt16(escFill, coItemNoIc).ToString(), XI.Map.C2sEquipsetCheck.IcGetItemNo(escFill).ToString());
        int coE5 = (int)ParseNum(g["OFF_ESC.Equipment"]) + 5 * (int)ParseNum(g["ESC_ITEM_STRIDE"]);
        Check("ESC Eq[5].GetItemNo==raw@compiler", BitConverter.ToUInt16(escFill, coE5 + (int)ParseNum(g["OFF_ERI.ItemNo"])).ToString(), XI.Map.C2sEquipsetCheck.EqGetItemNo(escFill, 5).ToString());
        Check("ESC Eq[5].GetCategory==raw@compiler", (((escFill[coE5 + (int)ParseNum(g["BF_Category_off"])] & XI.Map.EquipsetRequestItem.CategoryMask) >> XI.Map.EquipsetRequestItem.CategoryShift)).ToString(), XI.Map.C2sEquipsetCheck.EqGetCategory(escFill, 5).ToString());

        // write-path bit-field semantics on the shared byte: setting one field preserves the siblings, and
        // a read-back after all three writes returns exactly the three authored values.
        var wb = new byte[XI.Map.C2sEquipsetCheck.SizeOf];
        XI.Map.C2sEquipsetCheck.IcSetHasItemFlg(wb, true);
        XI.Map.C2sEquipsetCheck.IcSetRemoveItemFlg(wb, false);
        XI.Map.C2sEquipsetCheck.IcSetCategory(wb, 0x15); // 0b010101 -> bits 2..7
        Check("ESC IC bit-byte composed value", "0x55", $"0x{wb[coBitByte]:X2}"); // 0x01 | (0x15<<2)=0x54
        Check("ESC IC GetHasItemFlg roundtrip", "True", XI.Map.C2sEquipsetCheck.IcGetHasItemFlg(wb).ToString());
        Check("ESC IC GetRemoveItemFlg roundtrip", "False", XI.Map.C2sEquipsetCheck.IcGetRemoveItemFlg(wb).ToString());
        Check("ESC IC GetCategory roundtrip", "21", XI.Map.C2sEquipsetCheck.IcGetCategory(wb).ToString());
        XI.Map.C2sEquipsetCheck.IcSetItemNo(wb, 0xBEEF);
        Check("ESC IC SetItemNo at compiler offset", "0xBEEF", $"0x{BitConverter.ToUInt16(wb, coItemNoIc):X4}");
        // array element write preserves nothing outside its own element: set Equipment[3] Category, prove
        // Equipment[4] (next element's bit-byte) is untouched and the compiler stride kept them apart.
        var ab = new byte[XI.Map.C2sEquipsetCheck.SizeOf];
        XI.Map.C2sEquipsetCheck.EqSetCategory(ab, 3, 0x2A);
        int coE3 = (int)ParseNum(g["OFF_ESC.Equipment"]) + 3 * (int)ParseNum(g["ESC_ITEM_STRIDE"]);
        int coE4 = (int)ParseNum(g["OFF_ESC.Equipment"]) + 4 * (int)ParseNum(g["ESC_ITEM_STRIDE"]);
        Check("ESC Eq[3] Category at compiler element offset", "0xA8", $"0x{ab[coE3 + (int)ParseNum(g["BF_Category_off"])]:X2}"); // 0x2A<<2 = 0xA8
        Check("ESC Eq[4] bit-byte untouched by Eq[3] write", "0x00", $"0x{ab[coE4]:X2}");
        Check("ESC Eq[3] GetCategory roundtrip", "42", XI.Map.C2sEquipsetCheck.EqGetCategory(ab, 3).ToString());

        // shared C2S GP_CLI_HEADER id:9 / sync == BasicPacket opcode/sequence (via pos).
        byte[] hb = new byte[4];
        XI.Map.C2sPos.WriteHeader(hb, 0x052, 0, 0x0525);
        var bp = new XI.Map.BasicPacket();
        Array.Copy(hb, bp.Buffer, 4);
        Check("ESC header id == BasicPacket opcode", "82", bp.GetOpcode().ToString());
        Check("ESC header sync == BasicPacket sequence", "1317", bp.GetSequence().ToString());
    }

    // Verifies the Phase-3 C2S body 0x0d3 GP_CLI_COMMAND_FAQ_GMCALL against the compiled oracle
    // map_c2s_faq_gmcall_golden.txt. Two new surfaces: (1) the first STRADDLING bit-field run -- six
    // `uint16:w` fields spanning two uint16 allocation units (type:4+vers:4+pktId:8 fill unit0 bytes 4..5;
    // seq:7+eos:1+blkNum:8 fill unit1 bytes 6..7), each field byte-contained but the RUN crossing the unit
    // boundary; and (2) the first GP_CLI_PACKET_VLA body, whose getMinSize() == offsetof(Data) == 8. Since
    // offsetof is illegal on bit-fields (C2618), each field's byte+mask+shift is sentinel-scan-derived and
    // the gate asserts the C# masks/offsets equal the compiler's BF_* facts, proves read-modify-write
    // preserves siblings, pins the two hand-authored enums, the VLA Data offset/length, the whole-struct
    // fill's first 8 bytes, and the shared C2S header framing against CBasicPacket.
    static void VerifyMapC2sFaqGmcall(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_c2s_faq_gmcall_golden.txt");
        Console.WriteLine("\n== C2s faq_gmcall (0x0d3) straddling bit-field + VLA body parity: C# XI.Map vs layout oracle ==");
        if (!System.IO.File.Exists(path))
        {
            Check("c2s faq_gmcall golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        Check("GMC HDR_SIZE", g["HDR_SIZE"], XI.Map.C2sFaqGmcall.HeaderSize.ToString());
        Check("GMC HDR_OFF_sync", g["HDR_OFF_sync"], XI.Map.C2sFaqGmcall.OffSync.ToString());

        // Body layout (default packing); the VLA Data trails at offset 8 (== getMinSize()).
        Check("GMC GMC_SIZE", g["GMC_SIZE"], XI.Map.C2sFaqGmcall.SizeOf.ToString());
        Check("GMC GMC_ALIGN", g["GMC_ALIGN"], XI.Map.C2sFaqGmcall.AlignOf.ToString());
        Check("GMC OFF.header", g["OFF_GMC.header"], XI.Map.C2sFaqGmcall.OffHeader.ToString());
        Check("GMC OFF.Data", g["OFF_GMC.Data"], XI.Map.C2sFaqGmcall.OffData.ToString());
        Check("GMC Data_len", g["GMC_Data_len"], XI.Map.C2sFaqGmcall.DataLen.ToString());
        // the VLA getMinSize() contract == offsetof(Data): the port's MinSize equals the compiler's Data offset.
        Check("GMC MinSize == compiler offsetof(Data)",
            g["OFF_GMC.Data"], XI.Map.C2sFaqGmcall.MinSize.ToString());

        // Straddling bit-field run: each field's byte + mask + shift vs the compiler's sentinel scan.
        Check("GMC BF type off", g["BF_type_off"], XI.Map.C2sFaqGmcall.OffTypeVers.ToString());
        Check("GMC BF type mask", g["BF_type_mask"], XI.Map.C2sFaqGmcall.TypeMask.ToString());
        Check("GMC BF type shift", g["BF_type_shift"], XI.Map.C2sFaqGmcall.TypeShift.ToString());
        Check("GMC BF vers off", g["BF_vers_off"], XI.Map.C2sFaqGmcall.OffTypeVers.ToString());
        Check("GMC BF vers mask", g["BF_vers_mask"], XI.Map.C2sFaqGmcall.VersMask.ToString());
        Check("GMC BF vers shift", g["BF_vers_shift"], XI.Map.C2sFaqGmcall.VersShift.ToString());
        Check("GMC BF pktId off", g["BF_pktId_off"], XI.Map.C2sFaqGmcall.OffPktId.ToString());
        Check("GMC BF pktId mask", g["BF_pktId_mask"], XI.Map.C2sFaqGmcall.PktIdMask.ToString());
        Check("GMC BF seq off", g["BF_seq_off"], XI.Map.C2sFaqGmcall.OffSeqEos.ToString());
        Check("GMC BF seq mask", g["BF_seq_mask"], XI.Map.C2sFaqGmcall.SeqMask.ToString());
        Check("GMC BF eos off", g["BF_eos_off"], XI.Map.C2sFaqGmcall.OffSeqEos.ToString());
        Check("GMC BF eos mask", g["BF_eos_mask"], XI.Map.C2sFaqGmcall.EosMask.ToString());
        Check("GMC BF eos shift", g["BF_eos_shift"], XI.Map.C2sFaqGmcall.EosShift.ToString());
        Check("GMC BF blkNum off", g["BF_blkNum_off"], XI.Map.C2sFaqGmcall.OffBlkNum.ToString());
        Check("GMC BF blkNum mask", g["BF_blkNum_mask"], XI.Map.C2sFaqGmcall.BlkNumMask.ToString());
        // the run genuinely straddles a uint16 allocation-unit boundary: pktId (byte5, in unit0) and seq
        // (byte6, unit1's low byte) are on the two sides of the 4th..5th-byte boundary; vers(0xF0)+type(0x0F)
        // share a byte; eos(0x80)+seq(0x7F) share a byte (the 7-bit + 1-bit split).
        Check("GMC vers+type masks partition byte4",
            "1",
            (((XI.Map.C2sFaqGmcall.TypeMask | XI.Map.C2sFaqGmcall.VersMask) & 0xFF) == 0xFF &&
             (XI.Map.C2sFaqGmcall.TypeMask & XI.Map.C2sFaqGmcall.VersMask) == 0 ? 1 : 0).ToString());
        Check("GMC seq+eos masks partition byte6 (7-bit + 1-bit)",
            "1",
            (((XI.Map.C2sFaqGmcall.SeqMask | XI.Map.C2sFaqGmcall.EosMask) & 0xFF) == 0xFF &&
             (XI.Map.C2sFaqGmcall.SeqMask & XI.Map.C2sFaqGmcall.EosMask) == 0 ? 1 : 0).ToString());

        // Whole-struct deterministic fill (first 8 bytes: header + the straddling run).
        Check("GMC GMC_FILL_SIZE", g["GMC_FILL_SIZE"], XI.Map.C2sFaqGmcall.SizeOf.ToString());
        var gmc = new byte[XI.Map.C2sFaqGmcall.SizeOf];
        for (int i = 0; i < gmc.Length; i++) gmc[i] = unchecked((byte)(i * 7 + 3));
        var gmcHead = BitConverter.ToString(gmc, 0, 8).Replace("-", "");
        Check("GMC fill head (header+straddling run)", g["GMC_FILL_HEAD"], gmcHead);

        // Read-path: each accessor == the compiler's own mask/shift extraction from the fill's raw bytes.
        int coType = (int)ParseNum(g["BF_type_off"]);
        int coPktId = (int)ParseNum(g["BF_pktId_off"]);
        int coSeq = (int)ParseNum(g["BF_seq_off"]);
        int coBlk = (int)ParseNum(g["BF_blkNum_off"]);
        int coData = (int)ParseNum(g["OFF_GMC.Data"]);
        Check("GMC GetType==raw@compiler", (((gmc[coType]) & XI.Map.C2sFaqGmcall.TypeMask) >> XI.Map.C2sFaqGmcall.TypeShift).ToString(), XI.Map.C2sFaqGmcall.GetType(gmc).ToString());
        Check("GMC GetVers==raw@compiler", (((gmc[coType]) & XI.Map.C2sFaqGmcall.VersMask) >> XI.Map.C2sFaqGmcall.VersShift).ToString(), XI.Map.C2sFaqGmcall.GetVers(gmc).ToString());
        Check("GMC GetPktId==raw@compiler", (((gmc[coPktId]) & XI.Map.C2sFaqGmcall.PktIdMask) >> XI.Map.C2sFaqGmcall.PktIdShift).ToString(), XI.Map.C2sFaqGmcall.GetPktId(gmc).ToString());
        Check("GMC GetSeq==raw@compiler", (((gmc[coSeq]) & XI.Map.C2sFaqGmcall.SeqMask) >> XI.Map.C2sFaqGmcall.SeqShift).ToString(), XI.Map.C2sFaqGmcall.GetSeq(gmc).ToString());
        Check("GMC GetEos==raw@compiler", (((gmc[coSeq]) & XI.Map.C2sFaqGmcall.EosMask) != 0).ToString(), XI.Map.C2sFaqGmcall.GetEos(gmc).ToString());
        Check("GMC GetBlkNum==raw@compiler", (((gmc[coBlk]) & XI.Map.C2sFaqGmcall.BlkNumMask) >> XI.Map.C2sFaqGmcall.BlkNumShift).ToString(), XI.Map.C2sFaqGmcall.GetBlkNum(gmc).ToString());
        Check("GMC GetData==raw@compiler(first 4 bytes)", BitConverter.ToString(gmc, coData, 4).Replace("-", ""), BitConverter.ToString(XI.Map.C2sFaqGmcall.GetData(gmc).Slice(0, 4).ToArray()).Replace("-", ""));

        // Write-path: setting each field changes only its own bits and preserves siblings + the VLA bytes.
        var wb = new byte[XI.Map.C2sFaqGmcall.SizeOf];
        XI.Map.C2sFaqGmcall.SetType(wb, 0xA);
        XI.Map.C2sFaqGmcall.SetVers(wb, 0x5);
        Check("GMC SetType+SetVers compose byte4", "0x5A", $"0x{wb[coType]:X2}"); // 0x5 high | 0xA low
        Check("GMC GetType after SetVers survives", "10", XI.Map.C2sFaqGmcall.GetType(wb).ToString());
        XI.Map.C2sFaqGmcall.SetPktId(wb, 0x9C);
        Check("GMC SetPktId writes whole byte5", "0x9C", $"0x{wb[coPktId]:X2}");
        XI.Map.C2sFaqGmcall.SetSeq(wb, 0x55); // 7-bit: 0x55 & 0x7F = 0x55
        XI.Map.C2sFaqGmcall.SetEos(wb, true);
        Check("GMC SetSeq+SetEos compose byte6", "0xD5", $"0x{wb[coSeq]:X2}"); // 0x80 | 0x55
        Check("GMC GetSeq survives SetEos", "85", XI.Map.C2sFaqGmcall.GetSeq(wb).ToString()); // 0x55
        XI.Map.C2sFaqGmcall.SetBlkNum(wb, 0x07);
        Check("GMC SetBlkNum writes whole byte7", "0x07", $"0x{wb[coBlk]:X2}");
        Check("GMC GetEos roundtrip", "True", XI.Map.C2sFaqGmcall.GetEos(wb).ToString());
        Check("GMC GetBlkNum roundtrip", "7", XI.Map.C2sFaqGmcall.GetBlkNum(wb).ToString());
        // the straddling run leaves bytes 8..11 (the VLA start) zeroed by the field writes above.
        Check("GMC VLA bytes untouched by bit-field writes", "0x00000000", $"0x{BitConverter.ToUInt32(wb, coData):X8}");

        // hand-authored enum literals pinned vs compiler re-emitted underlying values.
        Check("GMC FAQTYPE_AddHistory", g["FAQTYPE_AddHistory"], ((int)XI.Map.FaqGmcallType.AddHistory).ToString());
        Check("GMC FAQTYPE_GMCall", g["FAQTYPE_GMCall"], ((int)XI.Map.FaqGmcallType.GMCall).ToString());
        Check("GMC FAQTYPE_GMNotice", g["FAQTYPE_GMNotice"], ((int)XI.Map.FaqGmcallType.GMNotice).ToString());
        Check("GMC BLKTYPE_Position", g["BLKTYPE_Position"], ((int)XI.Map.GmReportBlockType.Position).ToString());
        Check("GMC BLKTYPE_Version", g["BLKTYPE_Version"], ((int)XI.Map.GmReportBlockType.Version).ToString());
        Check("GMC BLKTYPE_Error", g["BLKTYPE_Error"], ((int)XI.Map.GmReportBlockType.Error).ToString());
        Check("GMC BLKTYPE_StringParam", g["BLKTYPE_StringParam"], ((int)XI.Map.GmReportBlockType.StringParam).ToString());
        Check("GMC BLKTYPE_LobbyHistory", g["BLKTYPE_LobbyHistory"], ((int)XI.Map.GmReportBlockType.LobbyHistory).ToString());

        // Shared C2S GP_CLI_HEADER id:9 / sync word1 == CBasicPacket opcode/sequence (via pos).
        byte[] hb = new byte[4];
        XI.Map.C2sPos.WriteHeader(hb, 0x0D3, 0, 0x2B2B);
        var bp = new XI.Map.BasicPacket();
        Array.Copy(hb, bp.Buffer, 4);
        Check("GMC header id == BasicPacket opcode", "211", bp.GetOpcode().ToString());
        Check("GMC header sync == BasicPacket sequence", "11051", bp.GetSequence().ToString());
    }

    // Verifies the first UNION-bearing Phase-3 S2C body (0x04b GP_SERV_COMMAND_PBX_RESULT) against the
    // compiled oracle map_s2c_union_pbx_golden.txt. The load-bearing, hand-reasoner-trap facts a union
    // introduces (chunks 3-14 had no unions): a union's size/align is the MAX of its arms, not the sum, and
    // all arms ALIAS at offset 0. This nests that three levels deep: GC_PBOX (union{SEND;RECV}) -> both arms
    // @0, size 28; GP_POST_BOX_STATE embeds a GC_PBOX by value (box_state@4) -> size 72; the PacketData's
    // ANONYMOUS union{u32 Represent; GP_POST_BOX_STATE State} is sized by the LARGER State arm (72), so
    // Represent@12 == State@12 and PBX_SIZE=84 (12+72), NOT 16 which a u32-sized union would give. Cross-
    // checks: every nested size/align/offset vs compiler, the two aliasing facts (Send==Recv==0, Represent==
    // State), the nested-inclusive member offsets (State_ItemNo=44, State_Data=56), accessor==raw@compiler
    // for an outer scalar AND a member INSIDE the union's State arm (proving the alias offset math), whole-
    // struct i*7+3 fills for both GP_POST_BOX_STATE and PacketData, and the shared 4-byte S2C header size.
    static void VerifyMapS2cUnionPbx(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_union_pbx_golden.txt");
        Console.WriteLine("\n== S2c union body (0x04b pbx_result) parity: C# XI.Map vs layout oracle ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c union pbx golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        Check("PBX SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- union arms GC_PBOX_SEND / GC_PBOX_RECV ----------------------------------------------
        Check("SEND SEND_SIZE", g["SEND_SIZE"], XI.Map.PbxSend.SizeOf.ToString());
        Check("SEND SEND_ALIGN", g["SEND_ALIGN"], XI.Map.PbxSend.AlignOf.ToString());
        Check("SEND OFF.To", g["OFF_SEND.To"], XI.Map.PbxSend.OffTo.ToString());
        Check("SEND To_len", g["SEND_To_len"], XI.Map.PbxSend.ToLen.ToString());
        Check("SEND OFF.RequestID", g["OFF_SEND.RequestID"], XI.Map.PbxSend.OffRequestID.ToString());
        // To[16]@0 then u32 RequestID@16 -- no padding (16 is already 4-aligned).
        Check("SEND RequestID packs right after To[16]",
            "1",
            (XI.Map.PbxSend.OffRequestID == XI.Map.PbxSend.OffTo + XI.Map.PbxSend.ToLen ? 1 : 0).ToString());
        Check("RECV RECV_SIZE", g["RECV_SIZE"], XI.Map.PbxRecv.SizeOf.ToString());
        Check("RECV OFF.From", g["OFF_RECV.From"], XI.Map.PbxRecv.OffFrom.ToString());
        Check("RECV OFF.OpponentPBoxNo", g["OFF_RECV.OpponentPBoxNo"], XI.Map.PbxRecv.OffOpponentPBoxNo.ToString());

        // ---- GC_PBOX (union): size == max(SEND,RECV), BOTH arms alias at 0 -----------------------
        Check("PBOX PBOX_SIZE", g["PBOX_SIZE"], XI.Map.PboxUnion.SizeOf.ToString());
        Check("PBOX PBOX_ALIGN", g["PBOX_ALIGN"], XI.Map.PboxUnion.AlignOf.ToString());
        Check("PBOX OFF.Send", g["OFF_PBOX.Send"], XI.Map.PboxUnion.OffSend.ToString());
        Check("PBOX OFF.Recv", g["OFF_PBOX.Recv"], XI.Map.PboxUnion.OffRecv.ToString());
        // the union fact: both arms offset 0, and the size is the MAX of the arms (both 28 here).
        Check("PBOX both arms alias at 0",
            "1",
            (XI.Map.PboxUnion.OffSend == 0 && XI.Map.PboxUnion.OffRecv == 0 ? 1 : 0).ToString());
        Check("PBOX size == max(SEND_SIZE,RECV_SIZE)",
            "1",
            (XI.Map.PboxUnion.SizeOf == Math.Max((int)ParseNum(g["SEND_SIZE"]), (int)ParseNum(g["RECV_SIZE"])) ? 1 : 0).ToString());

        // ---- GP_POST_BOX_STATE (embeds GC_PBOX by value) -----------------------------------------
        Check("STATE STATE_SIZE", g["STATE_SIZE"], XI.Map.PostBoxState.SizeOf.ToString());
        Check("STATE STATE_ALIGN", g["STATE_ALIGN"], XI.Map.PostBoxState.AlignOf.ToString());
        Check("STATE OFF.Stat", g["OFF_STATE.Stat"], XI.Map.PostBoxState.OffStat.ToString());
        Check("STATE OFF.box_state", g["OFF_STATE.box_state"], XI.Map.PostBoxState.OffBoxState.ToString());
        Check("STATE OFF.ItemNo", g["OFF_STATE.ItemNo"], XI.Map.PostBoxState.OffItemNo.ToString());
        Check("STATE OFF.Kind", g["OFF_STATE.Kind"], XI.Map.PostBoxState.OffKind.ToString());
        Check("STATE OFF.Stack", g["OFF_STATE.Stack"], XI.Map.PostBoxState.OffStack.ToString());
        Check("STATE OFF.Data", g["OFF_STATE.Data"], XI.Map.PostBoxState.OffData.ToString());
        Check("STATE Data_len", g["STATE_Data_len"], XI.Map.PostBoxState.DataLen.ToString());
        // box_state (a 28-byte union) sits right after Stat@0(4) at 4; ItemNo follows box_state at 4+28=32.
        Check("STATE box_state after Stat (no pad)",
            "1",
            (XI.Map.PostBoxState.OffBoxState == XI.Map.PostBoxState.OffStat + 4 ? 1 : 0).ToString());
        Check("STATE ItemNo follows box_state+28",
            "1",
            (XI.Map.PostBoxState.OffItemNo == XI.Map.PostBoxState.OffBoxState + XI.Map.PboxUnion.SizeOf ? 1 : 0).ToString());
        byte[] stateFill = HexToBytes(g["STATE_FILL"]);
        Check("STATE STATE_FILL_SIZE", g["STATE_FILL_SIZE"], stateFill.Length.ToString());
        var st = new byte[XI.Map.PostBoxState.SizeOf];
        for (int i = 0; i < st.Length; i++) st[i] = unchecked((byte)(i * 7 + 3));
        Check("STATE deterministic fill", g["STATE_FILL"], BitConverter.ToString(st).Replace("-", ""));

        // ---- PacketData (anonymous union sized by the LARGER State arm) --------------------------
        Check("PBX PBX_SIZE", g["PBX_SIZE"], XI.Map.S2cPbxResult.SizeOf.ToString());
        Check("PBX PBX_ALIGN", g["PBX_ALIGN"], XI.Map.S2cPbxResult.AlignOf.ToString());
        Check("PBX OFF.Command", g["OFF_PBX.Command"], XI.Map.S2cPbxResult.OffCommand.ToString());
        Check("PBX OFF.BoxNo", g["OFF_PBX.BoxNo"], XI.Map.S2cPbxResult.OffBoxNo.ToString());
        Check("PBX OFF.ItemStacks", g["OFF_PBX.ItemStacks"], XI.Map.S2cPbxResult.OffItemStacks.ToString());
        Check("PBX OFF.Result", g["OFF_PBX.Result"], XI.Map.S2cPbxResult.OffResult.ToString());
        Check("PBX OFF.ResParam3", g["OFF_PBX.ResParam3"], XI.Map.S2cPbxResult.OffResParam3.ToString());
        Check("PBX OFF.Represent", g["OFF_PBX.Represent"], XI.Map.S2cPbxResult.OffRepresent.ToString());
        Check("PBX OFF.State", g["OFF_PBX.State"], XI.Map.S2cPbxResult.OffState.ToString());
        Check("PBX State_ItemNo", g["OFF_PBX.State_ItemNo"], XI.Map.S2cPbxResult.StateItemNoOffset().ToString());
        Check("PBX State_Data", g["OFF_PBX.State_Data"], XI.Map.S2cPbxResult.StateDataOffset().ToString());
        // the headline union fact: Represent (u32) and State (72-byte struct) ALIAS at the union start (12),
        // and the union is sized by the LARGER State arm, so PBX_SIZE = 12 + 72 = 84, NOT 12 + 4 = 16.
        Check("PBX Represent == State (union alias at offset 0 of the union)",
            "1",
            (XI.Map.S2cPbxResult.OffRepresent == XI.Map.S2cPbxResult.OffState ? 1 : 0).ToString());
        Check("PBX union sized by LARGER State arm (not the u32)",
            "1",
            (XI.Map.S2cPbxResult.SizeOf == XI.Map.S2cPbxResult.OffUnion + XI.Map.PostBoxState.SizeOf ? 1 : 0).ToString());
        Check("PBX size 84 (a u32-sized union would wrongly give 16)",
            g["PBX_SIZE"],
            "84");
        // ItemStacks re-aligns to offset 4 (u32) after BoxNo@1 (3 pad bytes).
        Check("PBX ItemStacks realigned to 4 (pad after BoxNo)",
            "1",
            (XI.Map.S2cPbxResult.OffItemStacks == 4 && XI.Map.S2cPbxResult.OffItemStacks != XI.Map.S2cPbxResult.OffBoxNo + 1 ? 1 : 0).ToString());

        byte[] pbxFill = HexToBytes(g["PBX_FILL"]);
        Check("PBX PBX_FILL_SIZE", g["PBX_FILL_SIZE"], pbxFill.Length.ToString());
        var pbx = new byte[XI.Map.S2cPbxResult.SizeOf];
        for (int i = 0; i < pbx.Length; i++) pbx[i] = unchecked((byte)(i * 7 + 3));
        Check("PBX deterministic fill", g["PBX_FILL"], BitConverter.ToString(pbx).Replace("-", ""));

        // read-path cross-checks against the compiler's raw bytes (two independent ways):
        int coItemStacks = (int)ParseNum(g["OFF_PBX.ItemStacks"]);
        int coStateItemNo = (int)ParseNum(g["OFF_PBX.State_ItemNo"]);
        int coStateData = (int)ParseNum(g["OFF_PBX.State_Data"]);
        Check("PBX GetItemStacks==raw@compiler", BitConverter.ToInt32(pbxFill, coItemStacks).ToString(), XI.Map.S2cPbxResult.GetItemStacks(pbxFill).ToString());
        // a member INSIDE the anonymous union's State arm, at the nested-inclusive compiler offset:
        Check("PBX GetStateItemNo==raw@compiler(nested union member)", BitConverter.ToUInt16(pbxFill, coStateItemNo).ToString(), XI.Map.S2cPbxResult.GetStateItemNo(pbxFill).ToString());
        Check("PBX GetStateStat==raw@compiler", BitConverter.ToUInt32(pbxFill, (int)ParseNum(g["OFF_PBX.State"])).ToString(), XI.Map.S2cPbxResult.GetStateStat(pbxFill).ToString());
        // Represent (u32 arm) and State.Stat (the nested struct's first u32) alias at 12 -> same raw bytes.
        Check("PBX Represent arm aliases State.Stat",
            BitConverter.ToUInt32(pbxFill, coStateItemNo - 32).ToString(), // 12 == StateItemNo(44) - 32
            XI.Map.S2cPbxResult.GetRepresent(pbxFill).ToString());
        Check("PBX GetStateData==raw@compiler(28 bytes)",
            BitConverter.ToString(pbxFill, coStateData, XI.Map.PostBoxState.DataLen).Replace("-", ""),
            BitConverter.ToString(XI.Map.S2cPbxResult.GetStateData(pbxFill).ToArray()).Replace("-", ""));

        // write-path: setters land on the compiler offsets; the nested-inclusive union member math is proven.
        var pb = new byte[XI.Map.S2cPbxResult.SizeOf];
        XI.Map.S2cPbxResult.SetCommand(pb, 0x03);
        XI.Map.S2cPbxResult.SetItemStacks(pb, 0x11223344);
        XI.Map.S2cPbxResult.SetStateItemNo(pb, 0xABCD);
        Check("PBX SetCommand at compiler offset", "0x03", $"0x{pb[0]:X2}");
        Check("PBX SetItemStacks at compiler offset", "0x11223344", $"0x{BitConverter.ToInt32(pb, coItemStacks):X8}");
        Check("PBX SetStateItemNo at compiler nested offset", "0xABCD", $"0x{BitConverter.ToUInt16(pb, coStateItemNo):X4}");
        Check("PBX GetItemStacks roundtrip", "287454020", XI.Map.S2cPbxResult.GetItemStacks(pb).ToString());
        Check("PBX GetStateItemNo roundtrip", "43981", XI.Map.S2cPbxResult.GetStateItemNo(pb).ToString());

        // shared S2C header framing == C2S/CBasicPacket (re-asserted via the union body's own SHDR_SIZE).
        Check("PBX header framing == C2S size", "4", g["SHDR_SIZE"]);
    }

    static void VerifyMapS2cRecipeUnion(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_recipe_union_golden.txt");
        Console.WriteLine("\n== S2c recipe union body (0x031) parity: C# XI.Map vs layout oracle ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c recipe union golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        Check("RCP SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- hand-authored enum literals (used as the Type member; compiler can't reflect bare names) ----
        Check("RCP ENUM RecipeDetail1", g["ENUM_RecipeDetail1"], ((ushort)XI.Map.RecipeType.RecipeDetail1).ToString());
        Check("RCP ENUM RecipeList", g["ENUM_RecipeList"], ((ushort)XI.Map.RecipeType.RecipeList).ToString());
        Check("RCP ENUM RecipeDetail2", g["ENUM_RecipeDetail2"], ((ushort)XI.Map.RecipeType.RecipeDetail2).ToString());
        Check("RCP ENUM Unknown", g["ENUM_Unknown"], ((ushort)XI.Map.RecipeType.Unknown).ToString());
        Check("RCP enum underlying size 2 (uint16_t)", g["ENUM_underlying_size"], "2");

        // ---- Details arm (TYPE1_3): all uint16, size 48 / align 2 ----
        Check("DET DET_SIZE", g["DET_SIZE"], XI.Map.RecipeDetails.SizeOf.ToString());
        Check("DET DET_ALIGN", g["DET_ALIGN"], XI.Map.RecipeDetails.AlignOf.ToString());
        Check("DET OFF.productitem", g["OFF_DET.productitem"], XI.Map.RecipeDetails.OffProductItem.ToString());
        Check("DET OFF.need_key_item", g["OFF_DET.need_key_item"], XI.Map.RecipeDetails.OffNeedKeyItem.ToString());
        Check("DET OFF.itemnum", g["OFF_DET.itemnum"], XI.Map.RecipeDetails.OffItemNum.ToString());
        Check("DET itemnum_len", g["DET_itemnum_len"], XI.Map.RecipeDetails.ItemNumLen.ToString());
        Check("DET itemnum_count", g["DET_itemnum_count"], XI.Map.RecipeDetails.ItemNumCount.ToString());
        Check("DET OFF.itemcount", g["OFF_DET.itemcount"], XI.Map.RecipeDetails.OffItemCount.ToString());
        Check("DET itemcount_len", g["DET_itemcount_len"], XI.Map.RecipeDetails.ItemCountLen.ToString());
        Check("DET OFF.Type", g["OFF_DET.Type"], XI.Map.RecipeDetails.OffType.ToString());
        Check("DET OFF.unknown32", g["OFF_DET.unknown32"], XI.Map.RecipeDetails.OffUnknown32.ToString());
        // every uint16 -> itemnum packs tight after need_key_item (10 + 2), itemcount after itemnum (12+16).
        Check("DET itemnum after need_key_item (no pad)",
            "1",
            (XI.Map.RecipeDetails.OffItemNum == XI.Map.RecipeDetails.OffNeedKeyItem + 2 ? 1 : 0).ToString());
        Check("DET itemcount after itemnum+16",
            "1",
            (XI.Map.RecipeDetails.OffItemCount == XI.Map.RecipeDetails.OffItemNum + XI.Map.RecipeDetails.ItemNumLen ? 1 : 0).ToString());
        // productitem@0 .. unknown32@46, +2 -> 48 == sizeof (pure uint16, no padding anywhere).
        Check("DET unknown32+2==SizeOf (gapless uint16 body)",
            "1",
            (XI.Map.RecipeDetails.OffUnknown32 + 2 == XI.Map.RecipeDetails.SizeOf ? 1 : 0).ToString());

        // ---- List arm (TYPE2): all uint16, size 48 / align 2 ----
        Check("LST LST_SIZE", g["LST_SIZE"], XI.Map.RecipeListArm.SizeOf.ToString());
        Check("LST LST_ALIGN", g["LST_ALIGN"], XI.Map.RecipeListArm.AlignOf.ToString());
        Check("LST OFF.unused04", g["OFF_LST.unused04"], XI.Map.RecipeListArm.OffUnused04.ToString());
        Check("LST unused04_len", g["LST_unused04_len"], XI.Map.RecipeListArm.Unused04Len.ToString());
        Check("LST OFF.itemnum", g["OFF_LST.itemnum"], XI.Map.RecipeListArm.OffItemNum.ToString());
        Check("LST itemnum_len", g["LST_itemnum_len"], XI.Map.RecipeListArm.ItemNumLen.ToString());
        Check("LST itemnum_count", g["LST_itemnum_count"], XI.Map.RecipeListArm.ItemNumCount.ToString());
        Check("LST OFF.Type", g["OFF_LST.Type"], XI.Map.RecipeListArm.OffType.ToString());
        Check("LST OFF.itemnum_next", g["OFF_LST.itemnum_next"], XI.Map.RecipeListArm.OffItemNumNext.ToString());
        // itemnum[16] starts at 12 right after unused04[6] (6*2==12), and Type follows itemnum (12+32==44).
        Check("LST itemnum after unused04 (no pad)",
            "1",
            (XI.Map.RecipeListArm.OffItemNum == XI.Map.RecipeListArm.OffUnused04 + XI.Map.RecipeListArm.Unused04Len ? 1 : 0).ToString());
        Check("LST Type after itemnum+32",
            "1",
            (XI.Map.RecipeListArm.OffType == XI.Map.RecipeListArm.OffItemNum + XI.Map.RecipeListArm.ItemNumLen ? 1 : 0).ToString());
        Check("LST itemnum_next+2==SizeOf",
            "1",
            (XI.Map.RecipeListArm.OffItemNumNext + 2 == XI.Map.RecipeListArm.SizeOf ? 1 : 0).ToString());

        // ---- PD_RECIPE anonymous union: EQUAL arms (both 48), both alias @0, size == max == 48 ----
        Check("RCP RCP_SIZE", g["RCP_SIZE"], XI.Map.S2cRecipe.SizeOf.ToString());
        Check("RCP RCP_ALIGN", g["RCP_ALIGN"], XI.Map.S2cRecipe.AlignOf.ToString());
        Check("RCP OFF.Details", g["OFF_RCP.Details"], XI.Map.S2cRecipe.OffDetails.ToString());
        Check("RCP OFF.List", g["OFF_RCP.List"], XI.Map.S2cRecipe.OffList.ToString());
        Check("RCP both arms alias at 0",
            "1",
            (XI.Map.S2cRecipe.OffDetails == 0 && XI.Map.S2cRecipe.OffList == 0 ? 1 : 0).ToString());
        // equal-sized arms: the union is max(48,48)==48 == each arm (the "max" fact, here with no disparity).
        Check("RCP union size == max(Details,List)",
            "1",
            (XI.Map.S2cRecipe.SizeOf == Math.Max((int)ParseNum(g["DET_SIZE"]), (int)ParseNum(g["LST_SIZE"])) ? 1 : 0).ToString());
        Check("RCP union size == each arm (arms equal)",
            "1",
            (XI.Map.S2cRecipe.SizeOf == XI.Map.RecipeDetails.SizeOf && XI.Map.RecipeDetails.SizeOf == XI.Map.RecipeListArm.SizeOf ? 1 : 0).ToString());

        // nested-inclusive offsets (union start @0 + arm field offset) for the C# accessors:
        Check("RCP OFF.Details_itemnum", g["OFF_RCP.Details_itemnum"], XI.Map.S2cRecipe.DetailsItemNumOffset(0).ToString());
        Check("RCP OFF.Details_itemcount", g["OFF_RCP.Details_itemcount"], XI.Map.S2cRecipe.DetailsItemCountOffset(0).ToString());
        Check("RCP OFF.Details_Type", g["OFF_RCP.Details_Type"], (XI.Map.S2cRecipe.OffDetails + XI.Map.RecipeDetails.OffType).ToString());
        Check("RCP OFF.List_itemnum", g["OFF_RCP.List_itemnum"], XI.Map.S2cRecipe.ListItemNumOffset(0).ToString());
        Check("RCP OFF.List_itemnum_next", g["OFF_RCP.List_itemnum_next"], (XI.Map.S2cRecipe.OffList + XI.Map.RecipeListArm.OffItemNumNext).ToString());

        // THE headline cross-arm fact: Details.itemcount[0] and List.itemnum[8] are DIFFERENT field names at
        // the SAME byte offset -- true C union aliasing, not two disjoint structs.
        Check("RCP cross-arm alias Details.itemcount[0]==List.itemnum[8]",
            g["ALIAS_Details_itemcount0"],
            g["ALIAS_List_itemnum8"]);
        Check("RCP alias offset is 28", "28", XI.Map.S2cRecipe.DetailsItemCountOffset(0).ToString());
        Check("RCP List.itemnum[8] resolves to 28", "28", XI.Map.S2cRecipe.ListItemNumOffset(8).ToString());

        // whole-struct fills (all three are the SAME 48-byte gapless body since every member is uint16):
        foreach (var tag in new[] { "DET", "LST", "RCP" })
        {
            byte[] fill = HexToBytes(g[tag + "_FILL"]);
            Check($"{tag} {tag}_FILL_SIZE", g[$"{tag}_FILL_SIZE"], fill.Length.ToString());
            var local = new byte[fill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check($"{tag} deterministic fill", g[$"{tag}_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }
        // all three fills are byte-identical (same 48-byte body viewed three ways):
        Check("RCP DET/LST/RCP fills identical",
            "1",
            (g["DET_FILL"] == g["LST_FILL"] && g["LST_FILL"] == g["RCP_FILL"] ? 1 : 0).ToString());

        // read-path cross-checks against the compiler's raw bytes (two independent ways):
        byte[] rcpFill = HexToBytes(g["RCP_FILL"]);
        Check("RCP GetDetailsProductItem==raw@compiler", BitConverter.ToUInt16(rcpFill, (int)ParseNum(g["OFF_RCP.Details_itemnum"]) - 12).ToString(), XI.Map.S2cRecipe.GetDetailsProductItem(rcpFill).ToString());
        Check("RCP GetDetailsItemNum[3]==raw@compiler", BitConverter.ToUInt16(rcpFill, (int)ParseNum(g["OFF_RCP.Details_itemnum"]) + 3 * 2).ToString(), XI.Map.S2cRecipe.GetDetailsItemNum(rcpFill, 3).ToString());
        Check("RCP GetDetailsItemCount[5]==raw@compiler", BitConverter.ToUInt16(rcpFill, (int)ParseNum(g["OFF_RCP.Details_itemcount"]) + 5 * 2).ToString(), XI.Map.S2cRecipe.GetDetailsItemCount(rcpFill, 5).ToString());
        Check("RCP GetListUnused04[2]==raw@compiler", BitConverter.ToUInt16(rcpFill, XI.Map.RecipeListArm.OffUnused04 + 2 * 2).ToString(), XI.Map.S2cRecipe.GetListUnused04(rcpFill, 2).ToString());
        Check("RCP GetListItemNum[11]==raw@compiler", BitConverter.ToUInt16(rcpFill, (int)ParseNum(g["OFF_RCP.List_itemnum"]) + 11 * 2).ToString(), XI.Map.S2cRecipe.GetListItemNum(rcpFill, 11).ToString());
        // the Type member: reading the enum literal from the compiler's raw bytes at offset 44.
        Check("RCP GetDetailsType==raw@compiler(Type@44)", ((XI.Map.RecipeType)BitConverter.ToUInt16(rcpFill, 44)).ToString(), XI.Map.S2cRecipe.GetDetailsType(rcpFill).ToString());

        // ---- runtime CROSS-ARM aliasing: write via one arm, read back via the other arm ----
        var rb = new byte[XI.Map.S2cRecipe.SizeOf];
        XI.Map.S2cRecipe.SetDetailsItemCount(rb, 2, 0xBEEF);
        Check("RCP SetDetailsItemCount[2] visible via GetListItemNum[10]",
            "48879", XI.Map.S2cRecipe.GetListItemNum(rb, 10).ToString());   // 0xBEEF == 48879; itemnum[8+2]==itemcount[2]
        Check("RCP SetDetailsItemCount[2] readback via own arm", "48879", XI.Map.S2cRecipe.GetDetailsItemCount(rb, 2).ToString());

        XI.Map.S2cRecipe.SetDetailsUnknown32(rb, 0x1234);
        Check("RCP SetDetailsUnknown32 visible via GetListItemNumNext",
            "4660", XI.Map.S2cRecipe.GetListItemNumNext(rb).ToString());    // unknown32@46 aliases itemnum_next@46
        XI.Map.S2cRecipe.SetListItemNumNext(rb, 0x5678);
        Check("RCP SetListItemNumNext visible via GetDetailsUnknown32",
            "22136", XI.Map.S2cRecipe.GetDetailsUnknown32(rb).ToString());

        XI.Map.S2cRecipe.SetListType(rb, XI.Map.RecipeType.RecipeList);
        Check("RCP SetListType visible via GetDetailsType (Type@44 shared)",
            "RecipeList", XI.Map.S2cRecipe.GetDetailsType(rb).ToString());

        // Details.itemnum[7]@26 and List.itemnum[7]@26 alias; List.itemnum[15]@42 reaches into Details.itemcount[7].
        XI.Map.S2cRecipe.SetListItemNum(rb, 15, 0x00AA);
        Check("RCP SetList.itemnum[15]@42 visible via GetDetailsItemCount[7]@42",
            "170", XI.Map.S2cRecipe.GetDetailsItemCount(rb, 7).ToString());

        // write-path: every accessor's own round-trip (value stored, value read back).
        var wb = new byte[XI.Map.S2cRecipe.SizeOf];
        XI.Map.S2cRecipe.SetDetailsProductItem(wb, 0xCAFE);
        XI.Map.S2cRecipe.SetDetailsNeedSkill(wb, 0, 0x0001);
        XI.Map.S2cRecipe.SetDetailsNeedSkill(wb, 2, 0x0003);
        XI.Map.S2cRecipe.SetDetailsItemNum(wb, 7, 0x00FF);
        XI.Map.S2cRecipe.SetListUnused04(wb, 5, 0x0005);
        XI.Map.S2cRecipe.SetDetailsType(wb, XI.Map.RecipeType.RecipeDetail2);
        Check("RCP GetDetailsProductItem roundtrip", "51966", XI.Map.S2cRecipe.GetDetailsProductItem(wb).ToString());
        Check("RCP GetDetailsNeedSkill[0] roundtrip", "1", XI.Map.S2cRecipe.GetDetailsNeedSkill(wb, 0).ToString());
        Check("RCP GetDetailsNeedSkill[2] roundtrip", "3", XI.Map.S2cRecipe.GetDetailsNeedSkill(wb, 2).ToString());
        Check("RCP GetDetailsItemNum[7] roundtrip", "255", XI.Map.S2cRecipe.GetDetailsItemNum(wb, 7).ToString());
        Check("RCP GetListUnused04[5] roundtrip", "5", XI.Map.S2cRecipe.GetListUnused04(wb, 5).ToString());
        Check("RCP GetListType roundtrip (via Details.Type shared byte)", "RecipeDetail2", XI.Map.S2cRecipe.GetListType(wb).ToString());

        // shared S2C header framing == C2S/CBasicPacket (re-asserted via the recipe union body's SHDR_SIZE).
        Check("RCP header framing == C2S size", "4", g["SHDR_SIZE"]);
    }

    static void VerifyMapS2cBattlefield(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_battlefield_union_golden.txt");
        Console.WriteLine("\n== S2c battlefield union body (0x075) parity: C# XI.Map vs layout oracle ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c battlefield union golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        Check("BF SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- hand-authored uint8 bit-flag enum literals ----
        Check("BF ENUM COUNTDOWN", g["ENUM_COUNTDOWN"], ((byte)XI.Map.ObjectiveUtilityType.Countdown).ToString());
        Check("BF ENUM PROGRESS", g["ENUM_PROGRESS"], ((byte)XI.Map.ObjectiveUtilityType.Progress).ToString());
        Check("BF ENUM HELP", g["ENUM_HELP"], ((byte)XI.Map.ObjectiveUtilityType.Help).ToString());
        Check("BF ENUM FENCE", g["ENUM_FENCE"], ((byte)XI.Map.ObjectiveUtilityType.Fence).ToString());
        Check("BF enum underlying size 1 (uint8)", g["ENUM_underlying_size"], "1");

        // ---- Scoreboard arm (scoreboard_data_t, a nested struct that is a union arm) ----
        Check("BF SBR_SIZE", g["SBR_SIZE"], XI.Map.ScoreboardArm.SizeOf.ToString());
        Check("BF SBR_ALIGN", g["SBR_ALIGN"], XI.Map.ScoreboardArm.AlignOf.ToString());
        Check("BF OFF_SBR.MarchlandScore", g["OFF_SBR.MarchlandScore"], XI.Map.ScoreboardArm.OffMarchlandScore.ToString());
        Check("BF OFF_SBR.StrongholdScore", g["OFF_SBR.StrongholdScore"], XI.Map.ScoreboardArm.OffStrongholdScore.ToString());
        Check("BF OFF_SBR.MarchlandProgress", g["OFF_SBR.MarchlandProgress"], XI.Map.ScoreboardArm.OffMarchlandProgress.ToString());
        Check("BF OFF_SBR.StrongholdNameOverride", g["OFF_SBR.StrongholdNameOverride"], XI.Map.ScoreboardArm.OffStrongholdNameOverride.ToString());
        Check("BF OFF_SBR.padding", g["OFF_SBR.padding"], XI.Map.ScoreboardArm.OffPadding.ToString());
        Check("BF SBR_padding_len", g["SBR_padding_len"], XI.Map.ScoreboardArm.PaddingLen.ToString());
        // padding[96] is the last member: 8 u32 (0..31) then padding@32, and 32+96==128 == sizeof (gapless).
        Check("BF SBR padding after StrongholdNameOverride+4",
            "1",
            (XI.Map.ScoreboardArm.OffPadding == XI.Map.ScoreboardArm.OffStrongholdNameOverride + 4 ? 1 : 0).ToString());
        Check("BF SBR padding+96==SizeOf (gapless)",
            "1",
            (XI.Map.ScoreboardArm.OffPadding + XI.Map.ScoreboardArm.PaddingLen == XI.Map.ScoreboardArm.SizeOf ? 1 : 0).ToString());

        // ---- progressbar_row_t (element of the struct array living in the Progress arm) ----
        Check("BF ROW_SIZE", g["ROW_SIZE"], XI.Map.ProgressbarRow.SizeOf.ToString());
        Check("BF ROW_ALIGN", g["ROW_ALIGN"], XI.Map.ProgressbarRow.AlignOf.ToString());
        Check("BF OFF_ROW.Progress", g["OFF_ROW.Progress"], XI.Map.ProgressbarRow.OffProgress.ToString());
        Check("BF OFF_ROW.Name", g["OFF_ROW.Name"], XI.Map.ProgressbarRow.OffName.ToString());
        Check("BF ROW_Name_len", g["ROW_Name_len"], XI.Map.ProgressbarRow.NameLen.ToString());
        // Name[16] (int8, alignof 1) packs at offset 4 right after the u32 Progress -- no realignment.
        Check("BF ROW Name after Progress+4 (u8 no realign)",
            "1",
            (XI.Map.ProgressbarRow.OffName == XI.Map.ProgressbarRow.OffProgress + 4 ? 1 : 0).ToString());
        Check("BF ROW Name+16==SizeOf (no tail pad)",
            "1",
            (XI.Map.ProgressbarRow.OffName + XI.Map.ProgressbarRow.NameLen == XI.Map.ProgressbarRow.SizeOf ? 1 : 0).ToString());

        // ---- Progress arm (progressbar_data_t: a nested STRUCT ARRAY Rows[6] + trailing u32) ----
        Check("BF PB_SIZE", g["PB_SIZE"], XI.Map.ProgressbarArm.SizeOf.ToString());
        Check("BF PB_ALIGN", g["PB_ALIGN"], XI.Map.ProgressbarArm.AlignOf.ToString());
        Check("BF OFF_PB.Rows", g["OFF_PB.Rows"], XI.Map.ProgressbarArm.OffRows.ToString());
        Check("BF PB_Rows_len", g["PB_Rows_len"], XI.Map.ProgressbarArm.RowsLen.ToString());
        Check("BF PB_Rows_count", g["PB_Rows_count"], XI.Map.ProgressbarArm.RowsCount.ToString());
        Check("BF OFF_PB.unused", g["OFF_PB.unused"], XI.Map.ProgressbarArm.OffUnused.ToString());
        // the array stride fact: RowsLen == RowsCount * sizeof(row) == 6*20 == 120 -> NO inter-element padding.
        Check("BF PB RowsLen == RowsCount * ROW_SIZE (stride 20, no pad)",
            "1",
            (XI.Map.ProgressbarArm.RowsLen == XI.Map.ProgressbarArm.RowsCount * XI.Map.ProgressbarRow.SizeOf ? 1 : 0).ToString());
        Check("BF PB RowStride == ROW_SIZE", "1",
            (XI.Map.ProgressbarArm.RowStride == XI.Map.ProgressbarRow.SizeOf ? 1 : 0).ToString());
        Check("BF PB unused after Rows+120", "1",
            (XI.Map.ProgressbarArm.OffUnused == XI.Map.ProgressbarArm.OffRows + XI.Map.ProgressbarArm.RowsLen ? 1 : 0).ToString());
        Check("BF PB unused+4==SizeOf", "1",
            (XI.Map.ProgressbarArm.OffUnused + 4 == XI.Map.ProgressbarArm.SizeOf ? 1 : 0).ToString());

        // ---- outer PacketData: leading scalars + three-arm union in the MIDDLE + u16 tail ----
        Check("BF BF_SIZE", g["BF_SIZE"], XI.Map.S2cBattlefield.SizeOf.ToString());
        Check("BF BF_ALIGN", g["BF_ALIGN"], XI.Map.S2cBattlefield.AlignOf.ToString());
        Check("BF OFF_BF.Mode", g["OFF_BF.Mode"], XI.Map.S2cBattlefield.OffMode.ToString());
        Check("BF OFF_BF.DurationWarn", g["OFF_BF.DurationWarn"], XI.Map.S2cBattlefield.OffDurationWarn.ToString());
        Check("BF OFF_BF.FenceX", g["OFF_BF.FenceX"], XI.Map.S2cBattlefield.OffFenceX.ToString());
        Check("BF OFF_BF.FenceRotation", g["OFF_BF.FenceRotation"], XI.Map.S2cBattlefield.OffFenceRotation.ToString());
        Check("BF OFF_BF.Flags", g["OFF_BF.Flags"], XI.Map.S2cBattlefield.OffFlags.ToString());
        Check("BF OFF_BF.padding27", g["OFF_BF.padding27"], XI.Map.S2cBattlefield.OffPadding27.ToString());

        // the THREE union arms all alias at the same body offset (36) -- the multi-arm union fact.
        Check("BF OFF_BF.Data", g["OFF_BF.Data"], XI.Map.S2cBattlefield.OffData.ToString());
        Check("BF OFF_BF.Scoreboard", g["OFF_BF.Scoreboard"], XI.Map.S2cBattlefield.OffScoreboard.ToString());
        Check("BF OFF_BF.Progress", g["OFF_BF.Progress"], XI.Map.S2cBattlefield.OffProgress.ToString());
        Check("BF BF_Data_len", g["BF_Data_len"], XI.Map.S2cBattlefield.DataLen.ToString());
        Check("BF all three arms alias at OffUnion",
            "1",
            (XI.Map.S2cBattlefield.OffData == XI.Map.S2cBattlefield.OffUnion
             && XI.Map.S2cBattlefield.OffScoreboard == XI.Map.S2cBattlefield.OffUnion
             && XI.Map.S2cBattlefield.OffProgress == XI.Map.S2cBattlefield.OffUnion ? 1 : 0).ToString());
        // union start packs right after the last leading byte (padding27@35 -> 36 is already 4-aligned).
        Check("BF union start after padding27 (no pad)", "1",
            (XI.Map.S2cBattlefield.OffUnion == XI.Map.S2cBattlefield.OffPadding27 + 1 ? 1 : 0).ToString());
        // union sized by the LARGEST arm (max(128 raw-Data, 128 Scoreboard, 124 Progress) == 128).
        Check("BF union size == largest arm (128, not the smallest 124)", "1",
            (XI.Map.S2cBattlefield.DataLen == Math.Max((int)ParseNum(g["SBR_SIZE"]),
                        Math.Max((int)ParseNum(g["PB_SIZE"]), (int)ParseNum(g["BF_Data_len"]))) ? 1 : 0).ToString());

        // TAIL AFTER THE UNION -- the union's SIZE (128) pushes MesNumTitle to 36+128==164 (not 36+124==160).
        Check("BF OFF_BF.MesNumTitle", g["OFF_BF.MesNumTitle"], XI.Map.S2cBattlefield.OffMesNumTitle.ToString());
        Check("BF OFF_BF.MesNumDescription", g["OFF_BF.MesNumDescription"], XI.Map.S2cBattlefield.OffMesNumDescription.ToString());
        Check("BF MesNumTitle == OffUnion + unionSize(128) (tail sized past LARGEST arm)",
            "1",
            (XI.Map.S2cBattlefield.OffMesNumTitle == XI.Map.S2cBattlefield.OffUnion + XI.Map.S2cBattlefield.DataLen ? 1 : 0).ToString());
        Check("BF MesNumTitle is NOT at OffUnion+124 (smallest-arm mistake)",
            "1",
            (XI.Map.S2cBattlefield.OffMesNumTitle != XI.Map.S2cBattlefield.OffUnion + XI.Map.ProgressbarArm.SizeOf ? 1 : 0).ToString());
        Check("BF MesNumDescription == MesNumTitle+2", "1",
            (XI.Map.S2cBattlefield.OffMesNumDescription == XI.Map.S2cBattlefield.OffMesNumTitle + 2 ? 1 : 0).ToString());
        Check("BF SizeOf == MesNumDescription+2 (168, gapless overall)", "1",
            (XI.Map.S2cBattlefield.SizeOf == XI.Map.S2cBattlefield.OffMesNumDescription + 2 ? 1 : 0).ToString());

        // nested-inclusive offsets (union start + arm field offset) for the C# accessors:
        Check("BF OFF_BF.Scoreboard_MarchlandScore", g["OFF_BF.Scoreboard_MarchlandScore"], XI.Map.S2cBattlefield.ScoreboardMarchlandScoreOffset().ToString());
        Check("BF OFF_BF.Scoreboard_padding", g["OFF_BF.Scoreboard_padding"], XI.Map.S2cBattlefield.ScoreboardPaddingOffset().ToString());
        Check("BF OFF_BF.Progress_unused", g["OFF_BF.Progress_unused"], XI.Map.S2cBattlefield.ProgressUnusedOffset().ToString());
        // the cross-arm raw-vs-structured alias: Data[4] (raw byte) and Rows[0].Name both live at body offset 40.
        Check("BF ALIAS_Data_4 == ALIAS_Row0_Name", g["ALIAS_Data_4"], g["ALIAS_Row0_Name"]);
        Check("BF RowOffset(0)+Name == 40 (compiler ALIAS_Data_4)", g["ALIAS_Data_4"],
            (XI.Map.S2cBattlefield.RowOffset(0) + XI.Map.ProgressbarRow.OffName).ToString());
        Check("BF RowOffset(5) == OffUnion + 5*RowStride",
            "136", XI.Map.S2cBattlefield.RowOffset(5).ToString());   // 36 + 5*20 == 136

        // whole-struct fills for the three nested views + the outer body (all gapless i*7+3).
        foreach (var kv in new (string tag, int size)[]
                 { ("SBR", XI.Map.ScoreboardArm.SizeOf), ("PB", XI.Map.ProgressbarArm.SizeOf), ("BF", XI.Map.S2cBattlefield.SizeOf) })
        {
            byte[] fill = HexToBytes(g[kv.tag + "_FILL"]);
            Check($"{kv.tag} {kv.tag}_FILL_SIZE", g[$"{kv.tag}_FILL_SIZE"], fill.Length.ToString());
            var local = new byte[fill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check($"{kv.tag} deterministic fill", g[$"{kv.tag}_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }

        // read-path: accessor == raw read at the COMPILER's nested-inclusive offset (two independent ways).
        byte[] bf = HexToBytes(g["BF_FILL"]);
        Check("BF GetMode==raw@compiler", BitConverter.ToUInt32(bf, (int)ParseNum(g["OFF_BF.Mode"])).ToString(), XI.Map.S2cBattlefield.GetMode(bf).ToString());
        // a member inside the union's Scoreboard arm, at its nested-inclusive compiler offset (36):
        Check("BF GetScoreboardMarchlandScore==raw@compiler(union arm member)",
            BitConverter.ToInt32(bf, (int)ParseNum(g["OFF_BF.Scoreboard_MarchlandScore"])).ToString(),
            XI.Map.S2cBattlefield.GetScoreboardMarchlandScore(bf).ToString());
        Check("BF GetRowProgress[2]==raw@compiler(struct-array stride)",
            BitConverter.ToUInt32(bf, XI.Map.S2cBattlefield.RowOffset(2)).ToString(),
            XI.Map.S2cBattlefield.GetRowProgress(bf, 2).ToString());
        Check("BF GetProgressUnused==raw@compiler(156)",
            BitConverter.ToUInt32(bf, (int)ParseNum(g["OFF_BF.Progress_unused"])).ToString(),
            XI.Map.S2cBattlefield.GetProgressUnused(bf).ToString());
        // the tail u16 members, reached at their compiler offsets (164/166) -- proves the union-size-derived layout.
        Check("BF GetMesNumTitle==raw@compiler(tail after union)",
            BitConverter.ToUInt16(bf, (int)ParseNum(g["OFF_BF.MesNumTitle"])).ToString(),
            XI.Map.S2cBattlefield.GetMesNumTitle(bf).ToString());
        Check("BF GetMesNumDescription==raw@compiler(tail)",
            BitConverter.ToUInt16(bf, (int)ParseNum(g["OFF_BF.MesNumDescription"])).ToString(),
            XI.Map.S2cBattlefield.GetMesNumDescription(bf).ToString());
        // cross-arm raw-vs-structured read: the raw Data span and the structured Rows[0].Name view the SAME bytes.
        Check("BF GetRowName[0]==raw Data[4..19] (cross-arm alias)",
            BitConverter.ToString(bf, (int)ParseNum(g["ALIAS_Data_4"]), XI.Map.ProgressbarRow.NameLen).Replace("-", ""),
            BitConverter.ToString(XI.Map.S2cBattlefield.GetRowName(bf, 0).ToArray()).Replace("-", ""));

        // runtime CROSS-ARM aliasing: write through one arm's accessor, read it back through another arm.
        var rb = new byte[XI.Map.S2cBattlefield.SizeOf];
        XI.Map.S2cBattlefield.SetScoreboardMarchlandScore(rb, unchecked((int)0x11223344));   // writes body[36..39]
        Check("BF SetScoreboard.MarchlandScore visible via raw Data[0..3] (same union arm bytes)",
            "44332211", BitConverter.ToString(rb, XI.Map.S2cBattlefield.OffData, 4).Replace("-", ""));       // 0x11223344 stored LE = 44 33 22 11
        Check("BF SetScoreboard.MarchlandScore roundtrip", "287454020",
            XI.Map.S2cBattlefield.GetScoreboardMarchlandScore(rb).ToString());
        // write raw Data bytes, read the structured Scoreboard int32 (union alias, opposite direction):
        var wb = new byte[XI.Map.S2cBattlefield.SizeOf];
        wb[XI.Map.S2cBattlefield.OffData + 0] = 0x78; wb[XI.Map.S2cBattlefield.OffData + 1] = 0x56;
        wb[XI.Map.S2cBattlefield.OffData + 2] = 0x34; wb[XI.Map.S2cBattlefield.OffData + 3] = 0x12;
        Check("BF raw Data[0..3] read via GetScoreboardMarchlandScore == 0x12345678",
            "305419896", XI.Map.S2cBattlefield.GetScoreboardMarchlandScore(wb).ToString());

        // struct-array stride: SetRowProgress[3] lands at RowOffset(3) == OffUnion + 3*20 == 96.
        var sb = new byte[XI.Map.S2cBattlefield.SizeOf];
        XI.Map.S2cBattlefield.SetRowProgress(sb, 3, 0x0BADF00D);
        Check("BF SetRowProgress[3] at compiler stride offset (96)", "0x0BADF00D",
            $"0x{BitConverter.ToUInt32(sb, XI.Map.S2cBattlefield.RowOffset(3)):X8}");
        Check("BF GetRowProgress[3] roundtrip", "195948557", XI.Map.S2cBattlefield.GetRowProgress(sb, 3).ToString());

        // tail + scalar setter landing / round-trips.
        var tb = new byte[XI.Map.S2cBattlefield.SizeOf];
        XI.Map.S2cBattlefield.SetMode(tb, 0xAABBCCDD);
        XI.Map.S2cBattlefield.SetFenceX(tb, -123456);
        XI.Map.S2cBattlefield.SetMesNumTitle(tb, 0x002A);
        XI.Map.S2cBattlefield.SetMesNumDescription(tb, 0x002B);
        XI.Map.S2cBattlefield.SetProgressUnused(tb, 0x0F0F0F0F);
        Check("BF SetMode roundtrip", "2864434397", XI.Map.S2cBattlefield.GetMode(tb).ToString());
        Check("BF SetFenceX roundtrip (int32 signed)", "-123456", XI.Map.S2cBattlefield.GetFenceX(tb).ToString());
        Check("BF SetProgressUnused at compiler offset 156", "0x0F0F0F0F",
            $"0x{BitConverter.ToUInt32(tb, XI.Map.S2cBattlefield.ProgressUnusedOffset()):X8}");
        // the tail u16 writes must NOT touch the union's last bytes (union ends at 163, title at 164).
        Check("BF tail write leaves union last byte (163) at 0", "0", XI.Map.S2cBattlefield.GetData(tb, 127).ToString());
        Check("BF GetMesNumTitle roundtrip", "42", XI.Map.S2cBattlefield.GetMesNumTitle(tb).ToString());
        Check("BF GetMesNumDescription roundtrip", "43", XI.Map.S2cBattlefield.GetMesNumDescription(tb).ToString());

        // shared S2C header framing == C2S/CBasicPacket (re-asserted via the battlefield union body's SHDR_SIZE).
        Check("BF header framing == C2S size", "4", g["SHDR_SIZE"]);
    }

    static void VerifyMapC2sAucUnion(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_c2s_auc_union_golden.txt");
        Console.WriteLine("\n== C2s AUC union body (0x04e) parity: C# XI.Map vs layout oracle ==");
        if (!System.IO.File.Exists(path))
        {
            Check("c2s auc union golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // ---- header framing shared with the other C2S structs ----
        Check("AUC CHDR_SIZE", g["CHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- self-contained hand-authored uint8 enum (used as the leading scalar MEMBER Command@4) ----
        Check("AUC ENUM Open", g["ENUM_Open"], ((byte)XI.Map.AucCommand.Open).ToString());
        Check("AUC ENUM AskCommit", g["ENUM_AskCommit"], ((byte)XI.Map.AucCommand.AskCommit).ToString());
        Check("AUC ENUM Info", g["ENUM_Info"], ((byte)XI.Map.AucCommand.Info).ToString());
        Check("AUC ENUM WorkCheck", g["ENUM_WorkCheck"], ((byte)XI.Map.AucCommand.WorkCheck).ToString());
        Check("AUC ENUM LotIn", g["ENUM_LotIn"], ((byte)XI.Map.AucCommand.LotIn).ToString());
        Check("AUC ENUM LotCancel", g["ENUM_LotCancel"], ((byte)XI.Map.AucCommand.LotCancel).ToString());
        Check("AUC ENUM LotCheck", g["ENUM_LotCheck"], ((byte)XI.Map.AucCommand.LotCheck).ToString());
        Check("AUC ENUM Bid", g["ENUM_Bid"], ((byte)XI.Map.AucCommand.Bid).ToString());
        Check("AUC enum underlying size 1 (uint8)", g["ENUM_underlying_size"], "1");

        // ---- six union arms: sizes/aligns + a representative member offset each ----
        Check("AUC LOT_SIZE", g["LOT_SIZE"], XI.Map.AucParamLot.SizeOf.ToString());
        Check("AUC LOT_ALIGN", g["LOT_ALIGN"], XI.Map.AucParamLot.AlignOf.ToString());
        Check("AUC OFF_LOT.LimitPrice", g["OFF_LOT.LimitPrice"], XI.Map.AucParamLot.OffLimitPrice.ToString());
        Check("AUC OFF_LOT.ItemWorkIndex", g["OFF_LOT.ItemWorkIndex"], XI.Map.AucParamLot.OffItemWorkIndex.ToString());
        Check("AUC OFF_LOT.ItemStacks", g["OFF_LOT.ItemStacks"], XI.Map.AucParamLot.OffItemStacks.ToString());
        Check("AUC BID_SIZE", g["BID_SIZE"], XI.Map.AucParamBid.SizeOf.ToString());
        Check("AUC OFF_BID.BidPrice", g["OFF_BID.BidPrice"], XI.Map.AucParamBid.OffBidPrice.ToString());
        Check("AUC OFF_BID.ItemNo", g["OFF_BID.ItemNo"], XI.Map.AucParamBid.OffItemNo.ToString());
        Check("AUC SUM_SIZE", g["SUM_SIZE"], XI.Map.AucParamSummary.SizeOf.ToString());
        Check("AUC OFF_SUM.Kind", g["OFF_SUM.Kind"], XI.Map.AucParamSummary.OffKind.ToString());
        Check("AUC OFF_SUM.ItemNo", g["OFF_SUM.ItemNo"], XI.Map.AucParamSummary.OffItemNo.ToString());
        Check("AUC HIS_SIZE", g["HIS_SIZE"], XI.Map.AucParamHistory.SizeOf.ToString());
        Check("AUC ASK_SIZE", g["ASK_SIZE"], XI.Map.AucParamAskCommit.SizeOf.ToString());
        Check("AUC OFF_ASK.ItemNo", g["OFF_ASK.ItemNo"], XI.Map.AucParamAskCommit.OffItemNo.ToString());
        Check("AUC OFF_ASK.ItemStacks", g["OFF_ASK.ItemStacks"], XI.Map.AucParamAskCommit.OffItemStacks.ToString());
        Check("AUC TRA_SIZE", g["TRA_SIZE"], XI.Map.AucParamTrans.SizeOf.ToString());
        Check("AUC TRA_ALIGN", g["TRA_ALIGN"], XI.Map.AucParamTrans.AlignOf.ToString());
        Check("AUC OFF_TRA.Signature", g["OFF_TRA.Signature"], XI.Map.AucParamTrans.OffSignature.ToString());
        Check("AUC OFF_TRA.TotalFragments", g["OFF_TRA.TotalFragments"], XI.Map.AucParamTrans.OffTotalFragments.ToString());
        // THE key self-padding fact: Summary is u32+u16 = 6 member-bytes, but its OWN align-4 pads it to 8.
        Check("AUC Summary self-pads 6 -> 8 (member-sum 6, sizeof 8)",
            "1",
            (XI.Map.AucParamSummary.OffItemNo + 2 == 6 && (int)ParseNum(g["SUM_SIZE"]) == 8 ? 1 : 0).ToString());

        // ---- GP_AUC_PARAM (six-arm union): size == MAX arm size, align == MAX arm align, all arms @0 ----
        Check("AUC PARAM_SIZE", g["PARAM_SIZE"], XI.Map.AucParam.SizeOf.ToString());
        Check("AUC PARAM_ALIGN", g["PARAM_ALIGN"], XI.Map.AucParam.AlignOf.ToString());
        Check("AUC OFF_PARAM.LotIn", g["OFF_PARAM.LotIn"], XI.Map.AucParam.OffLotIn.ToString());
        Check("AUC OFF_PARAM.Summary", g["OFF_PARAM.Summary"], XI.Map.AucParam.OffSummary.ToString());
        Check("AUC OFF_PARAM.Trans", g["OFF_PARAM.Trans"], XI.Map.AucParam.OffTrans.ToString());
        // size = max of all six arm sizes (mixed 8 vs 12) == 12.
        int maxArmSize = Math.Max(Math.Max(Math.Max((int)ParseNum(g["LOT_SIZE"]), (int)ParseNum(g["BID_SIZE"])),
                                 Math.Max((int)ParseNum(g["SUM_SIZE"]), (int)ParseNum(g["HIS_SIZE"]))),
                                 Math.Max((int)ParseNum(g["ASK_SIZE"]), (int)ParseNum(g["TRA_SIZE"])));
        Check("AUC union size == max arm size (12, NOT sum of arms)",
            maxArmSize.ToString(), XI.Map.AucParam.SizeOf.ToString());
        // NEW fact chunks 15/16/17 could not show: union ALIGN == MAX arm align. Trans is align 2, the others 4.
        Check("AUC union align == max arm align (4, though Trans arm is only 2)",
            "1",
            (XI.Map.AucParam.AlignOf == 4 && XI.Map.AucParamTrans.AlignOf == 2 ? 1 : 0).ToString());
        Check("AUC Trans arm align (2) is LESS than the union align (4)",
            "1",
            (XI.Map.AucParamTrans.AlignOf < XI.Map.AucParam.AlignOf ? 1 : 0).ToString());
        // all six arms alias at offset 0.
        Check("AUC all arms alias at offset 0",
            "1",
            (XI.Map.AucParam.OffLotIn == 0 && XI.Map.AucParam.OffBid == 0 && XI.Map.AucParam.OffSummary == 0
             && XI.Map.AucParam.OffHistory == 0 && XI.Map.AucParam.OffAskCommit == 0 && XI.Map.AucParam.OffTrans == 0 ? 1 : 0).ToString());

        // ---- GP_AUC_BOX (trailing nested struct, Parcel) ----
        Check("AUC BOX_SIZE", g["BOX_SIZE"], XI.Map.AucBox.SizeOf.ToString());
        Check("AUC BOX_ALIGN", g["BOX_ALIGN"], XI.Map.AucBox.AlignOf.ToString());
        Check("AUC OFF_BOX.Stat", g["OFF_BOX.Stat"], XI.Map.AucBox.OffStat.ToString());
        Check("AUC OFF_BOX.Name", g["OFF_BOX.Name"], XI.Map.AucBox.OffName.ToString());
        Check("AUC BOX_Name_len", g["BOX_Name_len"], XI.Map.AucBox.NameLen.ToString());
        Check("AUC OFF_BOX.ItemNo", g["OFF_BOX.ItemNo"], XI.Map.AucBox.OffItemNo.ToString());
        Check("AUC OFF_BOX.ItemQuantity", g["OFF_BOX.ItemQuantity"], XI.Map.AucBox.OffItemQuantity.ToString());
        Check("AUC OFF_BOX.ItemCategory", g["OFF_BOX.ItemCategory"], XI.Map.AucBox.OffItemCategory.ToString());
        Check("AUC OFF_BOX.Price", g["OFF_BOX.Price"], XI.Map.AucBox.OffPrice.ToString());
        Check("AUC OFF_BOX.TimeStamp", g["OFF_BOX.TimeStamp"], XI.Map.AucBox.OffTimeStamp.ToString());
        // ItemNo packs right after Name[16] (4+16==20, already 2-aligned -- no pad).
        Check("AUC BOX ItemNo after Name+16 (no pad)", "1",
            (XI.Map.AucBox.OffItemNo == XI.Map.AucBox.OffName + XI.Map.AucBox.NameLen ? 1 : 0).ToString());
        // The subtle "already aligned so NO extra pad" case: u32 Price@24 follows the byte ItemCategory@23 at 24,
        // which is ALREADY a 4-boundary (23 + 1 == 24, 24 % 4 == 0). A hand-reasoner padding 23->24 would get 24
        // anyway; one forcing a 4-realign from a non-multiple would get 28. The compiler says 24 == ItemCategory+1.
        Check("AUC BOX Price@24 already 4-aligned (no surprise pad after ItemCategory@23)", "1",
            (XI.Map.AucBox.OffPrice == XI.Map.AucBox.OffItemCategory + 1 && XI.Map.AucBox.OffPrice % 4 == 0 ? 1 : 0).ToString());
        Check("AUC BOX TimeStamp+4==SizeOf (gapless tail)", "1",
            (XI.Map.AucBox.OffTimeStamp + 4 == XI.Map.AucBox.SizeOf ? 1 : 0).ToString());

        // ---- outer GP_CLI_COMMAND_AUC (header-inclusive) ----
        Check("AUC AUC_SIZE", g["AUC_SIZE"], XI.Map.C2sAuc.SizeOf.ToString());
        Check("AUC AUC_ALIGN", g["AUC_ALIGN"], XI.Map.C2sAuc.AlignOf.ToString());
        Check("AUC OFF_AUC.Command", g["OFF_AUC.Command"], XI.Map.C2sAuc.OffCommand.ToString());
        Check("AUC OFF_AUC.ResultStatus", g["OFF_AUC.ResultStatus"], XI.Map.C2sAuc.OffResultStatus.ToString());
        Check("AUC OFF_AUC.Param", g["OFF_AUC.Param"], XI.Map.C2sAuc.OffParam.ToString());
        Check("AUC OFF_AUC.Parcel", g["OFF_AUC.Parcel"], XI.Map.C2sAuc.OffParcel.ToString());
        // Param realigns to offset 8 (4-aligned) after the four byte-members Command@4..ResultStatus@7.
        Check("AUC Param at 8 (4-aligned after the byte run)", "1",
            (XI.Map.C2sAuc.OffParam == 8 && XI.Map.C2sAuc.OffParam % 4 == 0 ? 1 : 0).ToString());
        // Parcel == Param + union SizeOf (the union's SIZE sets where the box lands -- cf. chunk 17 tail).
        Check("AUC Parcel == Param + union size (8 + 12 == 20)",
            ((int)ParseNum(g["OFF_AUC.Param"]) + XI.Map.AucParam.SizeOf).ToString(), XI.Map.C2sAuc.OffParcel.ToString());
        // whole struct gapless: Parcel + BOX_SIZE == AUC_SIZE (20 + 40 == 60).
        Check("AUC Parcel+BOX==SizeOf (60, gapless overall)", "1",
            (XI.Map.C2sAuc.OffParcel + XI.Map.AucBox.SizeOf == XI.Map.C2sAuc.SizeOf ? 1 : 0).ToString());

        // ---- nested-inclusive accessor offsets vs compiler (union-alias + box-base) ----
        Check("AUC LotLimitPriceOffset == OFF_AUC.Param_LotIn_LimitPrice",
            g["OFF_AUC.Param_LotIn_LimitPrice"], XI.Map.C2sAuc.LotLimitPriceOffset().ToString());
        Check("AUC TransSignatureOffset == OFF_AUC.Param_Trans_Signature",
            g["OFF_AUC.Param_Trans_Signature"], XI.Map.C2sAuc.TransSignatureOffset().ToString());
        Check("AUC ParcelPriceOffset == OFF_AUC.Parcel_Price",
            g["OFF_AUC.Parcel_Price"], XI.Map.C2sAuc.ParcelPriceOffset().ToString());
        Check("AUC ParcelNameOffset == OFF_AUC.Parcel_Name",
            g["OFF_AUC.Parcel_Name"], XI.Map.C2sAuc.ParcelNameOffset().ToString());
        // the union alias: LotIn.LimitPrice (u32) and Trans.Signature (u16) both reach byte 8.
        Check("AUC LotIn.LimitPrice and Trans.Signature alias at body offset 8",
            XI.Map.C2sAuc.LotLimitPriceOffset().ToString(), XI.Map.C2sAuc.TransSignatureOffset().ToString());

        // ---- whole-struct fills ----
        foreach (var kv in new (string tag, int size)[]
                 { ("PARAM", XI.Map.AucParam.SizeOf), ("BOX", XI.Map.AucBox.SizeOf), ("AUC", XI.Map.C2sAuc.SizeOf) })
        {
            byte[] fill = HexToBytes(g[kv.tag + "_FILL"]);
            Check($"{kv.tag} {kv.tag}_FILL_SIZE", g[$"{kv.tag}_FILL_SIZE"], fill.Length.ToString());
            var local = new byte[fill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check($"{kv.tag} deterministic fill", g[$"{kv.tag}_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }

        // ---- read-path: accessor == raw read at the COMPILER's nested-inclusive offset (two independent ways) ----
        byte[] af = HexToBytes(g["AUC_FILL"]);
        Check("AUC GetCommand==raw@compiler", af[(int)ParseNum(g["OFF_AUC.Command"])].ToString(), ((byte)XI.Map.C2sAuc.GetCommand(af)).ToString());
        Check("AUC GetLotLimitPrice==raw@compiler(union arm u32)",
            BitConverter.ToUInt32(af, (int)ParseNum(g["OFF_AUC.Param_LotIn_LimitPrice"])).ToString(),
            XI.Map.C2sAuc.GetLotLimitPrice(af).ToString());
        Check("AUC GetTransSignature==raw@compiler(union alias u16)",
            BitConverter.ToUInt16(af, (int)ParseNum(g["OFF_AUC.Param_Trans_Signature"])).ToString(),
            XI.Map.C2sAuc.GetTransSignature(af).ToString());
        Check("AUC GetParcelPrice==raw@compiler(box u32)",
            BitConverter.ToUInt32(af, (int)ParseNum(g["OFF_AUC.Parcel_Price"])).ToString(),
            XI.Map.C2sAuc.GetParcelPrice(af).ToString());
        Check("AUC GetParcelItemNo==raw@compiler(box u16@40)",
            BitConverter.ToUInt16(af, XI.Map.C2sAuc.ParcelField(XI.Map.AucBox.OffItemNo)).ToString(),
            XI.Map.C2sAuc.GetParcelItemNo(af).ToString());
        Check("AUC GetParcelTimeStamp==raw@compiler(box u32@56)",
            BitConverter.ToUInt32(af, XI.Map.C2sAuc.ParcelField(XI.Map.AucBox.OffTimeStamp)).ToString(),
            XI.Map.C2sAuc.GetParcelTimeStamp(af).ToString());
        Check("AUC GetParcelName==raw@compiler(box Name[16])",
            BitConverter.ToString(af, (int)ParseNum(g["OFF_AUC.Parcel_Name"]), XI.Map.AucBox.NameLen).Replace("-", ""),
            BitConverter.ToString(XI.Map.C2sAuc.GetParcelName(af).ToArray()).Replace("-", ""));

        // ---- RUNTIME CROSS-ARM aliasing: write via one union arm, read via another (SAME 12 bytes @8) ----
        uint lv = 0xAABBCCDD;
        var rb = new byte[XI.Map.C2sAuc.SizeOf];
        XI.Map.C2sAuc.SetLotLimitPrice(rb, lv);
        Check("AUC SetLotIn.LimitPrice visible via Bid.BidPrice (u32 alias @8)", lv.ToString(), XI.Map.C2sAuc.GetBidBidPrice(rb).ToString());
        Check("AUC SetLotIn.LimitPrice visible via Summary.Kind (u32 alias @8)", lv.ToString(), XI.Map.C2sAuc.GetSummaryKind(rb).ToString());
        Check("AUC SetLotIn.LimitPrice visible via AskCommit.Commission (u32 alias @8)", lv.ToString(), XI.Map.C2sAuc.GetAskCommission(rb).ToString());
        // a u16 write (Trans.Signature @8) clobbers only the LOW half of the u32 seen via LotIn.LimitPrice:
        XI.Map.C2sAuc.SetTransSignature(rb, 0x1234);
        Check("AUC SetTrans.Signature u16 -> LotIn.LimitPrice LOW half (high half preserved)",
            $"0x{((lv & 0xFFFF0000) | 0x1234):X8}", $"0x{XI.Map.C2sAuc.GetLotLimitPrice(rb):X8}");

        // Summary is only 8 bytes; writing its Kind must not disturb bytes 16..19 (still inside the 12-byte union).
        var nb2 = new byte[XI.Map.C2sAuc.SizeOf];
        XI.Map.C2sAuc.SetSummaryKind(nb2, 0x55667788);
        Check("AUC SetSummary.Kind roundtrip", "0x55667788", $"0x{XI.Map.C2sAuc.GetSummaryKind(nb2):X8}");

        // ---- setter landing on the compiler's nested-inclusive offset + round-trips ----
        var sb = new byte[XI.Map.C2sAuc.SizeOf];
        uint pv = 0xDEADBEEF;
        XI.Map.C2sAuc.SetParcelPrice(sb, pv);
        Check("AUC SetParcelPrice lands at compiler offset 44", pv.ToString(),
            BitConverter.ToUInt32(sb, (int)ParseNum(g["OFF_AUC.Parcel_Price"])).ToString());
        XI.Map.C2sAuc.SetCommand(sb, XI.Map.AucCommand.WorkCheck);
        Check("AUC SetCommand roundtrip", ((byte)XI.Map.AucCommand.WorkCheck).ToString(), ((byte)XI.Map.C2sAuc.GetCommand(sb)).ToString());
        XI.Map.C2sAuc.SetParcelItemNo(sb, 0x4455);
        Check("AUC SetParcelItemNo lands at compiler offset 40", "0x4455",
            $"0x{BitConverter.ToUInt16(sb, XI.Map.C2sAuc.ParcelField(XI.Map.AucBox.OffItemNo)):X4}");
        var nameBytes = System.Text.Encoding.ASCII.GetBytes("HELLO");
        XI.Map.C2sAuc.SetParcelName(sb, nameBytes);
        Check("AUC SetParcelName writes at compiler Parcel_Name offset",
            "HELLO", System.Text.Encoding.ASCII.GetString(sb, (int)ParseNum(g["OFF_AUC.Parcel_Name"]), 5));
        XI.Map.C2sAuc.SetParcelTimeStamp(sb, 0x778899AA);
        Check("AUC SetParcelTimeStamp roundtrip", "0x778899AA", $"0x{XI.Map.C2sAuc.GetParcelTimeStamp(sb):X8}");

        // shared C2S header framing == C2S/CBasicPacket (re-asserted via the AUC union body's CHDR_SIZE).
        Check("AUC header framing == C2S size", "4", g["CHDR_SIZE"]);
    }

    static void VerifyMapC2sExtJobUnion(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_c2s_extjob_union_golden.txt");
        Console.WriteLine("\n== C2s extended-job union body (0x102) parity: C# XI.Map vs layout oracle ==");
        if (!System.IO.File.Exists(path))
        {
            Check("c2s extjob union golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // ---- header framing shared with the other C2S structs ----
        Check("EJ CHDR_SIZE", g["CHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- 14-member hand-authored uint8 enum (largest literal set pinned so far) ----
        foreach (var ev in new (string key, XI.Map.AutomatonSlot val)[]
                 {
                     ("Head", XI.Map.AutomatonSlot.Head), ("Frame", XI.Map.AutomatonSlot.Frame),
                     ("Att1", XI.Map.AutomatonSlot.Attachment1), ("Att2", XI.Map.AutomatonSlot.Attachment2),
                     ("Att3", XI.Map.AutomatonSlot.Attachment3), ("Att4", XI.Map.AutomatonSlot.Attachment4),
                     ("Att5", XI.Map.AutomatonSlot.Attachment5), ("Att6", XI.Map.AutomatonSlot.Attachment6),
                     ("Att7", XI.Map.AutomatonSlot.Attachment7), ("Att8", XI.Map.AutomatonSlot.Attachment8),
                     ("Att9", XI.Map.AutomatonSlot.Attachment9), ("Att10", XI.Map.AutomatonSlot.Attachment10),
                     ("Att11", XI.Map.AutomatonSlot.Attachment11), ("Att12", XI.Map.AutomatonSlot.Attachment12),
                 })
        {
            Check($"EJ ENUM {ev.key}", g[$"ENUM_{ev.key}"], ((byte)ev.val).ToString());
        }
        Check("EJ enum underlying size 1 (uint8)", g["ENUM_underlying_size"], "1");

        // ---- BLU arm (all uint8 -> align 1) ----
        Check("EJ BLU_SIZE", g["BLU_SIZE"], XI.Map.BluData.SizeOf.ToString());
        Check("EJ BLU_ALIGN", g["BLU_ALIGN"], XI.Map.BluData.AlignOf.ToString());
        Check("EJ OFF_BLU.SpellId", g["OFF_BLU.SpellId"], XI.Map.BluData.OffSpellId.ToString());
        Check("EJ OFF_BLU.JobIndex", g["OFF_BLU.JobIndex"], XI.Map.BluData.OffJobIndex.ToString());
        Check("EJ OFF_BLU.Spells", g["OFF_BLU.Spells"], XI.Map.BluData.OffSpells.ToString());
        Check("EJ BLU_Spells_len", g["BLU_Spells_len"], XI.Map.BluData.SpellsLen.ToString());
        Check("EJ OFF_BLU.unused00", g["OFF_BLU.unused00"], XI.Map.BluData.OffUnused00.ToString());
        // every uint8 -> gapless: JobIndex@4 follows the u8[2] padding, Spells@8, unused00@28==Spells+20.
        Check("EJ BLU unused00 after Spells+20 (gapless u8)", "1",
            (XI.Map.BluData.OffUnused00 == XI.Map.BluData.OffSpells + XI.Map.BluData.SpellsLen ? 1 : 0).ToString());

        // ---- PUP arm (all uint8 -> align 1) ----
        Check("EJ PUP_SIZE", g["PUP_SIZE"], XI.Map.PupData.SizeOf.ToString());
        Check("EJ PUP_ALIGN", g["PUP_ALIGN"], XI.Map.PupData.AlignOf.ToString());
        Check("EJ OFF_PUP.ItemId", g["OFF_PUP.ItemId"], XI.Map.PupData.OffItemId.ToString());
        Check("EJ OFF_PUP.Slots", g["OFF_PUP.Slots"], XI.Map.PupData.OffSlots.ToString());
        Check("EJ PUP_Slots_len", g["PUP_Slots_len"], XI.Map.PupData.SlotsLen.ToString());
        Check("EJ PUP_Slots_count", g["PUP_Slots_count"], XI.Map.PupData.SlotsLen.ToString());   // 14 == enum members
        Check("EJ OFF_PUP.unused00", g["OFF_PUP.unused00"], XI.Map.PupData.OffUnused00.ToString());
        Check("EJ PUP unused00 after Slots+14 (gapless u8)", "1",
            (XI.Map.PupData.OffUnused00 == XI.Map.PupData.OffSlots + XI.Map.PupData.SlotsLen ? 1 : 0).ToString());
        // 14 Pup Slots matches the 14 AutomatonSlot enumerators.
        Check("EJ PUP Slots count == AutomatonSlot enumerator count (14)", "14", XI.Map.PupData.SlotsLen.ToString());

        // ---- MON arm (has u16 members -> align 2; nests the bit struct) ----
        Check("EJ MON_SIZE", g["MON_SIZE"], XI.Map.MonData.SizeOf.ToString());
        Check("EJ MON_ALIGN", g["MON_ALIGN"], XI.Map.MonData.AlignOf.ToString());
        Check("EJ OFF_MON.unknown00", g["OFF_MON.unknown00"], XI.Map.MonData.OffUnknown00.ToString());
        Check("EJ OFF_MON.Flags0", g["OFF_MON.Flags0"], XI.Map.MonData.OffFlags0.ToString());
        Check("EJ OFF_MON.unknown01", g["OFF_MON.unknown01"], XI.Map.MonData.OffUnknown01.ToString());
        Check("EJ OFF_MON.SpeciesIndex", g["OFF_MON.SpeciesIndex"], XI.Map.MonData.OffSpeciesIndex.ToString());
        Check("EJ OFF_MON.Slots", g["OFF_MON.Slots"], XI.Map.MonData.OffSlots.ToString());
        Check("EJ MON_Slots_len", g["MON_Slots_len"], XI.Map.MonData.SlotsLen.ToString());
        Check("EJ MON_Slots_count", g["MON_Slots_count"], XI.Map.MonData.SlotsCount.ToString());
        Check("EJ OFF_MON.Descriptor1Index", g["OFF_MON.Descriptor1Index"], XI.Map.MonData.OffDescriptor1Index.ToString());
        Check("EJ OFF_MON.unused00", g["OFF_MON.unused00"], XI.Map.MonData.OffUnused00.ToString());
        // Flags0 (mon_flags0_t, 1 byte) packs at 6 right after unknown00[6]; unknown01 follows at 7.
        Check("EJ MON Flags0 after unknown00[6] (bit struct, align 1)", "1",
            (XI.Map.MonData.OffFlags0 == XI.Map.MonData.OffUnknown00 + 6 ? 1 : 0).ToString());
        // SpeciesIndex(u16)@8 realigns right after the two bytes Flags0@6/unknown01@7 -- 8 is already 2-aligned.
        Check("EJ MON SpeciesIndex@8 (no pad after the two u8 at 6/7, already 2-aligned)", "1",
            (XI.Map.MonData.OffSpeciesIndex == 8 && XI.Map.MonData.OffUnknown01 + 1 == 8 ? 1 : 0).ToString());
        Check("EJ MON Slots u16[12] len 24 stride 2", "1",
            (XI.Map.MonData.SlotsLen == XI.Map.MonData.SlotsCount * 2 ? 1 : 0).ToString());
        Check("EJ MON unused00+122==SizeOf (gapless tail)", "1",
            (XI.Map.MonData.OffUnused00 + 122 == XI.Map.MonData.SizeOf ? 1 : 0).ToString());

        // ---- mon_flags0_t: the bit struct is size 1 / align 1 (all its fields fit in ONE byte) ----
        Check("EJ FLG_SIZE", g["FLG_SIZE"], XI.Map.MonFlags0.SizeOf.ToString());
        Check("EJ FLG_ALIGN", g["FLG_ALIGN"], XI.Map.MonFlags0.AlignOf.ToString());
        // each bit-field: byte offset (0), mask, and shift vs the compiler's SENTINEL SCAN (offsetof illegal).
        foreach (var bf in new (string key, int mask, int shift)[]
                 {
                     ("SpeciesFlag", XI.Map.MonFlags0.SpeciesMask, XI.Map.MonFlags0.SpeciesShift),
                     ("unknown_0_1", XI.Map.MonFlags0.Unknown01Mask, XI.Map.MonFlags0.Unknown01Shift),
                     ("InstinctFlag", XI.Map.MonFlags0.InstinctMask, XI.Map.MonFlags0.InstinctShift),
                     ("Descriptor1Flag", XI.Map.MonFlags0.Desc1Mask, XI.Map.MonFlags0.Desc1Shift),
                     ("Descriptor2Flag", XI.Map.MonFlags0.Desc2Mask, XI.Map.MonFlags0.Desc2Shift),
                     ("unknown_1_1", XI.Map.MonFlags0.Unknown11Mask, XI.Map.MonFlags0.Unknown11Shift),
                     ("unknown_1_2", XI.Map.MonFlags0.Unknown12Mask, XI.Map.MonFlags0.Unknown12Shift),
                     ("unknown_1_3", XI.Map.MonFlags0.Unknown13Mask, XI.Map.MonFlags0.Unknown13Shift),
                 })
        {
            Check($"EJ BF {bf.key} off", g[$"BF_{bf.key}_off"], "0");
            Check($"EJ BF {bf.key} mask", g[$"BF_{bf.key}_mask"], bf.mask.ToString());
            Check($"EJ BF {bf.key} shift", g[$"BF_{bf.key}_shift"], bf.shift.ToString());
            // the mask is exactly (1 << shift) -- proves the sentinel-scan consistency AND the bit width of 1.
            Check($"EJ BF {bf.key} mask==1<<shift", "1", ((bf.mask == (1 << bf.shift)) ? 1 : 0).ToString());
        }

        // ---- EXTJOB_DATA union: THREE arms of EQUAL size (160) but UNEQUAL align (1,1,2) -> size 160/align 2 ----
        Check("EJ DATA_SIZE", g["DATA_SIZE"], XI.Map.ExtJobData.SizeOf.ToString());
        Check("EJ DATA_ALIGN", g["DATA_ALIGN"], XI.Map.ExtJobData.AlignOf.ToString());
        Check("EJ OFF_DATA.bluData", g["OFF_DATA.bluData"], XI.Map.ExtJobData.OffBluData.ToString());
        Check("EJ OFF_DATA.pupData", g["OFF_DATA.pupData"], XI.Map.ExtJobData.OffPupData.ToString());
        Check("EJ OFF_DATA.monData", g["OFF_DATA.monData"], XI.Map.ExtJobData.OffMonData.ToString());
        Check("EJ all three arms alias at offset 0",
            "1",
            (XI.Map.ExtJobData.OffBluData == 0 && XI.Map.ExtJobData.OffPupData == 0 && XI.Map.ExtJobData.OffMonData == 0 ? 1 : 0).ToString());
        // union size == max arm size (all 160) == each arm (equal arms).
        Check("EJ union size == max arm size == each arm (160)",
            "1",
            (XI.Map.ExtJobData.SizeOf == XI.Map.BluData.SizeOf && XI.Map.BluData.SizeOf == XI.Map.MonData.SizeOf ? 1 : 0).ToString());
        // the distinct NEW fact: union ALIGN == max arm align. blu/pup are align 1, mon is align 2 -> union 2.
        Check("EJ union align == max arm align (2, from the mon_data_t arm)",
            "1",
            (XI.Map.ExtJobData.AlignOf == 2 && XI.Map.BluData.AlignOf == 1 && XI.Map.MonData.AlignOf == 2 ? 1 : 0).ToString());
        Check("EJ union align == max(blu,pup,mon) arm aligns", "1",
            (XI.Map.ExtJobData.AlignOf == Math.Max(XI.Map.BluData.AlignOf, Math.Max(XI.Map.PupData.AlignOf, XI.Map.MonData.AlignOf)) ? 1 : 0).ToString());

        // ---- outer GP_CLI_COMMAND_EXTENDED_JOB (header-inclusive) ----
        Check("EJ EJ_SIZE", g["EJ_SIZE"], XI.Map.C2sExtendedJob.SizeOf.ToString());
        Check("EJ EJ_ALIGN", g["EJ_ALIGN"], XI.Map.C2sExtendedJob.AlignOf.ToString());
        Check("EJ OFF_EJ.header", g["OFF_EJ.header"], XI.Map.C2sExtendedJob.OffHeader.ToString());
        Check("EJ OFF_EJ.Data", g["OFF_EJ.Data"], XI.Map.C2sExtendedJob.OffData.ToString());
        // Data starts right after the 4-byte header (header align 2, Data 4-aligned, no pad).
        Check("EJ Data at 4 (== header size)", "1",
            (XI.Map.C2sExtendedJob.OffData == 4 && XI.Map.C2sExtendedJob.OffData == XI.Map.C2sExtendedJob.HeaderSize ? 1 : 0).ToString());
        // whole struct: SizeOf == OffData + union SizeOf (4 + 160 == 164); union align (2) == whole align (2).
        Check("EJ SizeOf == OffData + union size (164)", "1",
            (XI.Map.C2sExtendedJob.SizeOf == XI.Map.C2sExtendedJob.OffData + XI.Map.ExtJobData.SizeOf ? 1 : 0).ToString());
        Check("EJ whole struct align == union align (2)",
            g["EJ_ALIGN"], XI.Map.ExtJobData.AlignOf.ToString());

        // nested-inclusive accessor offsets vs compiler:
        Check("EJ BluSpellsOffset == OFF_EJ.Data_BLU_Spells", g["OFF_EJ.Data_BLU_Spells"], XI.Map.C2sExtendedJob.BluSpellsOffset().ToString());
        Check("EJ PupSlotsOffset == OFF_EJ.Data_PUP_Slots", g["OFF_EJ.Data_PUP_Slots"], XI.Map.C2sExtendedJob.PupSlotsOffset().ToString());
        Check("EJ MonFlags0Offset == OFF_EJ.Data_MON_Flags0", g["OFF_EJ.Data_MON_Flags0"], XI.Map.C2sExtendedJob.MonFlags0Offset().ToString());
        Check("EJ MonSpeciesIndexOffset == OFF_EJ.Data_MON_SpeciesIndex", g["OFF_EJ.Data_MON_SpeciesIndex"], XI.Map.C2sExtendedJob.MonSpeciesIndexOffset().ToString());
        // THE cross-arm alias the three arm layouts share: BLU.Spells[0], PUP.Slots[0] and MON.SpeciesIndex@12.
        Check("EJ cross-arm alias BluSpells[0]==PupSlots[0]==MonSpeciesIndex (body 12)",
            "1",
            (XI.Map.C2sExtendedJob.BluSpellsOffset() == XI.Map.C2sExtendedJob.PupSlotsOffset()
             && XI.Map.C2sExtendedJob.PupSlotsOffset() == XI.Map.C2sExtendedJob.MonSpeciesIndexOffset() ? 1 : 0).ToString());

        // whole-struct fills for the three arms + the union + the outer body (all gapless i*7+3).
        foreach (var kv in new (string tag, int size)[]
                 {
                     ("BLU", XI.Map.BluData.SizeOf), ("PUP", XI.Map.PupData.SizeOf), ("MON", XI.Map.MonData.SizeOf),
                     ("DATA", XI.Map.ExtJobData.SizeOf), ("EJ", XI.Map.C2sExtendedJob.SizeOf),
                 })
        {
            byte[] fill = HexToBytes(g[kv.tag + "_FILL"]);
            Check($"{kv.tag} {kv.tag}_FILL_SIZE", g[$"{kv.tag}_FILL_SIZE"], fill.Length.ToString());
            var local = new byte[fill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check($"{kv.tag} deterministic fill", g[$"{kv.tag}_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }

        // read-path: accessor == raw read at the COMPILER's nested-inclusive offset (two independent ways).
        byte[] ej = HexToBytes(g["EJ_FILL"]);
        Check("EJ GetSpellId==raw@compiler(u8@4)", ej[(int)ParseNum(g["OFF_EJ.Data"])].ToString(), XI.Map.C2sExtendedJob.GetSpellId(ej).ToString());
        Check("EJ GetSpeciesIndex==raw@compiler(MON u16@12)",
            BitConverter.ToUInt16(ej, (int)ParseNum(g["OFF_EJ.Data_MON_SpeciesIndex"])).ToString(),
            XI.Map.C2sExtendedJob.GetSpeciesIndex(ej).ToString());
        // read the bit struct's raw byte at its compiler offset (10) and compare to the accessor.
        Check("EJ GetFlags0Byte==raw@compiler(bit struct@10)",
            ej[(int)ParseNum(g["OFF_EJ.Data_MON_Flags0"])].ToString(), XI.Map.C2sExtendedJob.GetFlags0Byte(ej).ToString());

        // runtime CROSS-ARM aliasing: write a BLU u8 byte, see it via the PUP u8 slot AND the MON u16 low half.
        var rb = new byte[XI.Map.C2sExtendedJob.SizeOf];
        XI.Map.C2sExtendedJob.SetBluSpell(rb, 0, 0xAB);   // writes body[12]
        Check("EJ SetBluSpell[0]@12 visible via GetPupSlot[0]@12 (cross-arm)", "171", XI.Map.C2sExtendedJob.GetPupSlot(rb, 0).ToString());
        Check("EJ SetBluSpell[0]@12 == SpeciesIndex LOW byte (u16 overlaps)", "171",
            (XI.Map.C2sExtendedJob.GetSpeciesIndex(rb) & 0xFF).ToString());
        // writing the MON u16 SpeciesIndex overwrites BOTH body[12] (BLU.Spells[0]) and the PUP Slot view.
        XI.Map.C2sExtendedJob.SetSpeciesIndex(rb, 0x1234);
        Check("EJ SetSpeciesIndex(0x1234) -> GetBluSpell[0]==low byte 0x34", "52", XI.Map.C2sExtendedJob.GetBluSpell(rb, 0).ToString());
        Check("EJ SetSpeciesIndex(0x1234) -> GetPupSlot[0]==low byte 0x34", "52", XI.Map.C2sExtendedJob.GetPupSlot(rb, 0).ToString());

        // bit-field RMW preserving siblings inside the Flags0 byte (the bit struct nested in the union arm).
        var fb = new byte[XI.Map.C2sExtendedJob.SizeOf];
        XI.Map.C2sExtendedJob.SetSpeciesFlag(fb, true);      // mask 0x01
        XI.Map.C2sExtendedJob.SetInstinctFlag(fb, true);     // mask 0x04
        XI.Map.C2sExtendedJob.SetDescriptor2Flag(fb, true);  // mask 0x10 -> 0x15 == 21
        Check("EJ Flags0 bit composition Species|Instinct|Desc2", "21", XI.Map.C2sExtendedJob.GetFlags0Byte(fb).ToString());
        XI.Map.C2sExtendedJob.SetSpeciesFlag(fb, false);     // clears 0x01 -> 0x14 == 20
        Check("EJ clearing SpeciesFlag preserves Instinct+Desc2", "20", XI.Map.C2sExtendedJob.GetFlags0Byte(fb).ToString());
        Check("EJ GetInstinctFlag still true after clearing Species", "True", XI.Map.C2sExtendedJob.GetInstinctFlag(fb).ToString());
        Check("EJ GetSpeciesFlag now false", "False", XI.Map.C2sExtendedJob.GetSpeciesFlag(fb).ToString());

        // setter landing on the compiler's nested-inclusive offset + round-trips.
        var wb = new byte[XI.Map.C2sExtendedJob.SizeOf];
        XI.Map.C2sExtendedJob.SetSpeciesIndex(wb, 0x5678);
        Check("EJ SetSpeciesIndex lands at compiler offset 12", "0x5678",
            $"0x{BitConverter.ToUInt16(wb, (int)ParseNum(g["OFF_EJ.Data_MON_SpeciesIndex"])):X4}");
        XI.Map.C2sExtendedJob.SetPupSlot(wb, 5, 0x99);   // PUP Slots@8 -> body[17]
        Check("EJ SetPupSlot[5] writes body[17] (PUP arm Slots@8+5)", "153",
            wb[(int)ParseNum(g["OFF_EJ.Data_PUP_Slots"]) + 5].ToString());
        Check("EJ GetPupSlot[5] roundtrip", "153", XI.Map.C2sExtendedJob.GetPupSlot(wb, 5).ToString());
        XI.Map.C2sExtendedJob.SetBluJobIndex(wb, 0x07);
        Check("EJ GetBluJobIndex roundtrip", "7", XI.Map.C2sExtendedJob.GetBluJobIndex(wb).ToString());
        XI.Map.C2sExtendedJob.SetDescriptor1Index(wb, 0x02);
        Check("EJ GetDescriptor1Index roundtrip", "2", XI.Map.C2sExtendedJob.GetDescriptor1Index(wb).ToString());

        // shared C2S header framing == C2S/CBasicPacket (re-asserted via the extended-job union body's CHDR_SIZE).
        Check("EJ header framing == C2S size", "4", g["CHDR_SIZE"]);
    }

    static void VerifyMapC2sActionUnion(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_c2s_action_union_golden.txt");
        Console.WriteLine("\n== C2s action union body (0x01a) parity: C# XI.Map vs layout oracle ==");
        if (!System.IO.File.Exists(path))
        {
            Check("c2s action union golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        Check("ACT CHDR_SIZE", g["CHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());
        Check("ACT float sizeof 4", g["SIZEOF_float"], "4");

        // ---- 23-member uint16_t ActionID tag enum (used as struct MEMBER ActionID@10) ----
        foreach (var ev in new (string key, XI.Map.ActionId val)[]
                 {
                     ("Talk", XI.Map.ActionId.Talk), ("Attack", XI.Map.ActionId.Attack),
                     ("CastMagic", XI.Map.ActionId.CastMagic), ("AttackOff", XI.Map.ActionId.AttackOff),
                     ("Help", XI.Map.ActionId.Help), ("Weaponskill", XI.Map.ActionId.Weaponskill),
                     ("JobAbility", XI.Map.ActionId.JobAbility), ("HomepointMenu", XI.Map.ActionId.HomepointMenu),
                     ("Assist", XI.Map.ActionId.Assist), ("RaiseMenu", XI.Map.ActionId.RaiseMenu),
                     ("Fish", XI.Map.ActionId.Fish), ("ChangeTarget", XI.Map.ActionId.ChangeTarget),
                     ("Shoot", XI.Map.ActionId.Shoot), ("ChocoboDig", XI.Map.ActionId.ChocoboDig),
                     ("Dismount", XI.Map.ActionId.Dismount), ("TractorMenu", XI.Map.ActionId.TractorMenu),
                     ("SendResRdy", XI.Map.ActionId.SendResRdy), ("Quarry", XI.Map.ActionId.Quarry),
                     ("Sprint", XI.Map.ActionId.Sprint), ("Scout", XI.Map.ActionId.Scout),
                     ("Blockaid", XI.Map.ActionId.Blockaid), ("MonsterSkill", XI.Map.ActionId.MonsterSkill),
                     ("Mount", XI.Map.ActionId.Mount),
                 })
        {
            Check($"ACT AID {ev.key}", g[$"AID_{ev.key}"], ((ushort)ev.val).ToString());
        }
        Check("ACT ActionID underlying size 2 (uint16)", g["AID_underlying_size"], "2");

        // ---- the four uint32_t status enums (used as arm MEMBER StatusId) ----
        Check("ACT BLKAID Disable", g["BLKAID_Disable"], ((uint)XI.Map.BlockaidStatus.Disable).ToString());
        Check("ACT BLKAID Enable", g["BLKAID_Enable"], ((uint)XI.Map.BlockaidStatus.Enable).ToString());
        Check("ACT BLKAID Toggle", g["BLKAID_Toggle"], ((uint)XI.Map.BlockaidStatus.Toggle).ToString());
        Check("ACT HPMENU Accept", g["HPMENU_Accept"], ((uint)XI.Map.HomepointMenuStatus.Accept).ToString());
        Check("ACT HPMENU MonstrosityCancel", g["HPMENU_MonstrosityCancel"], ((uint)XI.Map.HomepointMenuStatus.MonstrosityCancel).ToString());
        Check("ACT HPMENU MonstrosityRetry", g["HPMENU_MonstrosityRetry"], ((uint)XI.Map.HomepointMenuStatus.MonstrosityRetry).ToString());
        Check("ACT RAISE Accept", g["RAISE_Accept"], ((uint)XI.Map.RaiseMenuStatus.Accept).ToString());
        Check("ACT RAISE Reject", g["RAISE_Reject"], ((uint)XI.Map.RaiseMenuStatus.Reject).ToString());
        Check("ACT TRAC Accept", g["TRAC_Accept"], ((uint)XI.Map.TractorMenuStatus.Accept).ToString());
        Check("ACT TRAC Reject", g["TRAC_Reject"], ((uint)XI.Map.TractorMenuStatus.Reject).ToString());

        // ---- CastMagic arm: THE first FLOAT in a Phase-3 union arm ----
        Check("ACT CAST_SIZE", g["CAST_SIZE"], XI.Map.ActionBufCastMagic.SizeOf.ToString());
        Check("ACT CAST_ALIGN", g["CAST_ALIGN"], XI.Map.ActionBufCastMagic.AlignOf.ToString());
        Check("ACT OFF_CAST.SpellId", g["OFF_CAST.SpellId"], XI.Map.ActionBufCastMagic.OffSpellId.ToString());
        Check("ACT OFF_CAST.PosX", g["OFF_CAST.PosX"], XI.Map.ActionBufCastMagic.OffPosX.ToString());
        Check("ACT OFF_CAST.PosZ", g["OFF_CAST.PosZ"], XI.Map.ActionBufCastMagic.OffPosZ.ToString());
        Check("ACT OFF_CAST.PosY", g["OFF_CAST.PosY"], XI.Map.ActionBufCastMagic.OffPosY.ToString());
        // the three floats pack tightly after the u32 SpellId at 4/8/12 (alignof float 4, no padding).
        Check("ACT CAST floats gapless at 4/8/12",
            "1",
            (XI.Map.ActionBufCastMagic.OffPosX == 4 && XI.Map.ActionBufCastMagic.OffPosZ == 8
             && XI.Map.ActionBufCastMagic.OffPosY == 12 ? 1 : 0).ToString());
        Check("ACT CAST PosY+4==SizeOf (gapless tail)", "1",
            (XI.Map.ActionBufCastMagic.OffPosY + 4 == XI.Map.ActionBufCastMagic.SizeOf ? 1 : 0).ToString());

        // ---- Weaponskill / JobAbility / MonsterSkill (typedef-shared: SAME type, SAME offsets) ----
        Check("ACT WSKILL_SIZE", g["WSKILL_SIZE"], XI.Map.ActionBufWeaponskill.SizeOf.ToString());
        Check("ACT WSKILL_ALIGN", g["WSKILL_ALIGN"], XI.Map.ActionBufWeaponskill.AlignOf.ToString());
        Check("ACT OFF_WSKILL.SkillId", g["OFF_WSKILL.SkillId"], XI.Map.ActionBufWeaponskill.OffSkillId.ToString());
        Check("ACT OFF_WSKILL.unknown", g["OFF_WSKILL.unknown"], XI.Map.ActionBufWeaponskill.OffUnknown.ToString());
        // the typedef fact: JobAbility and MonsterSkill have the SAME size + SkillId offset as Weaponskill.
        Check("ACT JA_SIZE == WSKILL_SIZE (typedef-shared)", g["WSKILL_SIZE"], g["JA_SIZE"]);
        Check("ACT MS_SIZE == WSKILL_SIZE (typedef-shared)", g["WSKILL_SIZE"], g["MS_SIZE"]);
        Check("ACT OFF_JA_SkillId == OFF_WSKILL.SkillId", g["OFF_WSKILL.SkillId"], g["OFF_JA_SkillId"]);
        Check("ACT OFF_MS_SkillId == OFF_WSKILL.SkillId", g["OFF_WSKILL.SkillId"], g["OFF_MS_SkillId"]);
        // the C# uses ONE descriptor for all three typedef arms -> its offsets equal every compiler arm's offset.
        Check("ACT one descriptor matches all three typedef arms",
            "1",
            ((int)ParseNum(g["WSKILL_SIZE"]) == XI.Map.ActionBufWeaponskill.SizeOf
             && (int)ParseNum(g["JA_SIZE"]) == XI.Map.ActionBufWeaponskill.SizeOf
             && (int)ParseNum(g["MS_SIZE"]) == XI.Map.ActionBufWeaponskill.SizeOf ? 1 : 0).ToString());

        // ---- menu / mount arms ----
        Check("ACT HPMENU_SIZE", g["HPMENU_SIZE"], XI.Map.ActionBufMenu.SizeOf.ToString());
        Check("ACT HPMENU_ALIGN", g["HPMENU_ALIGN"], XI.Map.ActionBufMenu.AlignOf.ToString());
        Check("ACT OFF_HPMENU.StatusId", g["OFF_HPMENU.StatusId"], XI.Map.ActionBufMenu.OffStatusId.ToString());
        Check("ACT MOUNT_SIZE", g["MOUNT_SIZE"], XI.Map.ActionBufMount.SizeOf.ToString());
        Check("ACT OFF_MOUNT.MountId", g["OFF_MOUNT.MountId"], XI.Map.ActionBufMount.OffMountId.ToString());
        Check("ACT raw ActionBuf[4] size 16", g["ACTBUF_SIZE"], "16");

        // ---- outer GP_CLI_COMMAND_ACTION ----
        Check("ACT ACT_SIZE", g["ACT_SIZE"], XI.Map.C2sAction.SizeOf.ToString());
        Check("ACT ACT_ALIGN", g["ACT_ALIGN"], XI.Map.C2sAction.AlignOf.ToString());
        Check("ACT OFF_ACT.UniqueNo", g["OFF_ACT.UniqueNo"], XI.Map.C2sAction.OffUniqueNo.ToString());
        Check("ACT OFF_ACT.ActIndex", g["OFF_ACT.ActIndex"], XI.Map.C2sAction.OffActIndex.ToString());
        Check("ACT OFF_ACT.ActionID", g["OFF_ACT.ActionID"], XI.Map.C2sAction.OffActionID.ToString());
        // every one of the 9 arms aliases at the union start (12).
        foreach (var arm in new[] { "CastMagic", "Weaponskill", "JobAbility", "MonsterSkill", "HomepointMenu", "BlockAid", "Mount", "ActionBuf" })
            Check($"ACT OFF_ACT.{arm} aliases union start", g["OFF_ACT.CastMagic"], g[$"OFF_ACT.{arm}"]);
        Check("ACT all arms alias at OffUnion (12)", "1",
            ((int)ParseNum(g["OFF_ACT.CastMagic"]) == XI.Map.C2sAction.OffUnion ? 1 : 0).ToString());
        // NO pad before the union: 12 == ActionID@10 + sizeof(u16) -- 12 already 4-aligned, arm needs no realign.
        Check("ACT union@12 with NO pad after ActionID@10+2", "1",
            (XI.Map.C2sAction.OffUnion == XI.Map.C2sAction.OffActionID + 2 ? 1 : 0).ToString());
        // NO tail after the union: SizeOf == OffUnion + 16 (the union is the LAST member, cf. chunk 17's tail).
        Check("ACT SizeOf == OffUnion + arm size (28, union is last -> no tail)", "1",
            (XI.Map.C2sAction.SizeOf == XI.Map.C2sAction.OffUnion + XI.Map.ActionBufCastMagic.SizeOf ? 1 : 0).ToString());
        // ActionID@10 follows the two u16s ActIndex@8 -> 10 packs tight.
        Check("ACT ActionID@10 right after ActIndex@8+2", "1",
            (XI.Map.C2sAction.OffActionID == XI.Map.C2sAction.OffActIndex + 2 ? 1 : 0).ToString());

        // nested-inclusive offset helpers:
        Check("ACT PosXOffset == OFF_ACT.CastMagic_PosX", g["OFF_ACT.CastMagic_PosX"], XI.Map.C2sAction.PosXOffset().ToString());
        Check("ACT ActionBuf1Offset == OFF_ACT.ActionBuf_1", g["OFF_ACT.ActionBuf_1"], XI.Map.C2sAction.ActionBuf1Offset().ToString());
        // THE type-pun alias the C# exposes: PosX (float, offset 16) and ActionBuf[1] (u32, offset 16) SAME bytes.
        Check("ACT type-pun PosX == ActionBuf[1] (compiler PUN_PosX==PUN_Buf1)", g["PUN_PosX"], g["PUN_Buf1"]);
        Check("ACT PosXOffset == ActionBuf1Offset (float and u32 view the same bytes)",
            XI.Map.C2sAction.PosXOffset().ToString(), XI.Map.C2sAction.ActionBuf1Offset().ToString());
        Check("ACT type-pun at body offset 16", "16", XI.Map.C2sAction.PosXOffset().ToString());

        // whole-struct fill determinism.
        byte[] af = HexToBytes(g["ACT_FILL"]);
        Check("ACT ACT_FILL_SIZE", g["ACT_FILL_SIZE"], af.Length.ToString());
        var local = new byte[af.Length];
        for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
        Check("ACT deterministic fill", g["ACT_FILL"], BitConverter.ToString(local).Replace("-", ""));

        // read-path: accessor == raw read at the COMPILER's offset (two independent ways).
        Check("ACT GetUniqueNo==raw@compiler", BitConverter.ToUInt32(af, (int)ParseNum(g["OFF_ACT.UniqueNo"])).ToString(), XI.Map.C2sAction.GetUniqueNo(af).ToString());
        Check("ACT GetActIndex==raw@compiler", BitConverter.ToUInt16(af, (int)ParseNum(g["OFF_ACT.ActIndex"])).ToString(), XI.Map.C2sAction.GetActIndex(af).ToString());
        Check("ACT GetActionID==raw@compiler(tag u16@10)",
            ((XI.Map.ActionId)BitConverter.ToUInt16(af, (int)ParseNum(g["OFF_ACT.ActionID"]))).ToString(),
            XI.Map.C2sAction.GetActionID(af).ToString());
        // the FLOAT accessor reads the compiler's raw float at PosX@16.
        Check("ACT GetPosX==raw@compiler(float arm)",
            BitConverter.ToSingle(af, (int)ParseNum(g["OFF_ACT.CastMagic_PosX"])).ToString("R"),
            XI.Map.C2sAction.GetPosX(af).ToString("R"));
        // cross-arm TYPE-PUN read: PosX (float) and ActionBuf[1] (u32) are the SAME 4 bytes -> bit-equal.
        Check("ACT GetPosX bits == GetActionBuf[1] (float<->u32 type-pun)",
            BitConverter.SingleToUInt32Bits(BitConverter.ToSingle(af, (int)ParseNum(g["OFF_ACT.CastMagic_PosX"]))).ToString(),
            XI.Map.C2sAction.GetActionBuf(af, 1).ToString());

        // runtime cross-arm TYPE-PUNNING: write a float via the CastMagic arm, read the u32 bits via ActionBuf.
        var fb = new byte[XI.Map.C2sAction.SizeOf];
        float pv = 3.5f;   // exact in IEEE-754 (1.75 * 2^1), no rounding ambiguity
        XI.Map.C2sAction.SetPosX(fb, pv);
        Check("ACT SetPosX(3.5f) bit-identical via GetActionBuf[1]",
            BitConverter.SingleToUInt32Bits(pv).ToString(), XI.Map.C2sAction.GetActionBuf(fb, 1).ToString());
        // and the reverse: write raw u32 bits, read them back as the float.
        uint bits = BitConverter.SingleToUInt32Bits(pv);
        var gb = new byte[XI.Map.C2sAction.SizeOf];
        XI.Map.C2sAction.SetActionBuf(gb, 1, bits);
        Check("ACT SetActionBuf[1](bits) reads back via GetPosX as 3.5", "3.5", XI.Map.C2sAction.GetPosX(gb).ToString("R"));

        // typedef-shared arm aliasing: SetSkillId (Weaponskill view) is visible at the SAME bytes as CastMagic.SpellId
        // and as the raw ActionBuf[0] -- all u32 at union offset 0.
        var wb = new byte[XI.Map.C2sAction.SizeOf];
        XI.Map.C2sAction.SetSkillId(wb, 0xDEADBEEF);
        Check("ACT SetSkillId@12 visible via GetSpellId (typedef/u32 arms alias)", "3735928559", XI.Map.C2sAction.GetSpellId(wb).ToString());
        Check("ACT SetSkillId@12 visible via GetActionBuf[0] (raw u32 arm)", "3735928559", XI.Map.C2sAction.GetActionBuf(wb, 0).ToString());
        Check("ACT SetSkillId@12 visible via GetMountId (Mount arm alias)", "3735928559", XI.Map.C2sAction.GetMountId(wb).ToString());
        // writing the menu StatusId (u32 enum arm) reaches the same bytes as the SpellId / SkillId arms.
        XI.Map.C2sAction.SetHomepointMenuStatus(wb, XI.Map.HomepointMenuStatus.MonstrosityRetry);
        Check("ACT SetHomepointMenuStatus(2) reads back via GetHomepointMenuStatus", "MonstrosityRetry", XI.Map.C2sAction.GetHomepointMenuStatus(wb).ToString());

        // leading-scalar setter landing / round-trips.
        var tb = new byte[XI.Map.C2sAction.SizeOf];
        XI.Map.C2sAction.SetUniqueNo(tb, 0x11223344);
        XI.Map.C2sAction.SetActIndex(tb, 0xABCD);
        XI.Map.C2sAction.SetActionID(tb, XI.Map.ActionId.CastMagic);
        Check("ACT SetUniqueNo at compiler offset 4", "0x11223344", $"0x{BitConverter.ToUInt32(tb, (int)ParseNum(g["OFF_ACT.UniqueNo"])):X8}");
        Check("ACT GetUniqueNo roundtrip", "287454020", XI.Map.C2sAction.GetUniqueNo(tb).ToString());
        Check("ACT GetActIndex roundtrip", "43981", XI.Map.C2sAction.GetActIndex(tb).ToString());
        // the tag value lands at ActionID@10 as a raw u16; setting it must NOT touch the union bytes (from 12).
        Check("ACT SetActionID lands at compiler offset 10",
            ((ushort)XI.Map.ActionId.CastMagic).ToString(), BitConverter.ToUInt16(tb, (int)ParseNum(g["OFF_ACT.ActionID"])).ToString());
        Check("ACT GetActionID roundtrip (tag)", "CastMagic", XI.Map.C2sAction.GetActionID(tb).ToString());

        // shared C2S header framing == C2S/CBasicPacket (re-asserted via the action union body's CHDR_SIZE).
        Check("ACT header framing == C2S size", "4", g["CHDR_SIZE"]);
    }

    static void VerifyMapC2sGroupParty(string dir, string enumHeaderPath)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_c2s_group_party_enum_golden.txt");
        Console.WriteLine("\n== C2s group party enum body (0x06f/0x070) parity: C# XI.Map vs layout oracle + .h re-parse ==");
        if (!System.IO.File.Exists(path))
        {
            Check("c2s group party enum golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        Check("GP CHDR_SIZE", g["CHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- PartyKind literals: C# enum values vs the compiler's underlying-type cast ----
        Check("GP ENUM Party", g["ENUM_Party"], ((byte)XI.Map.PartyKind.Party).ToString());
        Check("GP ENUM Alliance", g["ENUM_Alliance"], ((byte)XI.Map.PartyKind.Alliance).ToString());
        Check("GP enum underlying size 1 (uint8_t)", g["ENUM_underlying_size"], "1");
        // the literal set is NON-sequential (0 and 5) -- a hand-authored enum, so the .h is ground truth and the
        // compiler only reflects the underlying value; re-assert Alliance is NOT the implicit prev+1 (would be 1).
        Check("GP Alliance is non-sequential (5, not implicit 1)",
            "1",
            ((byte)XI.Map.PartyKind.Alliance == 5 && (byte)XI.Map.PartyKind.Alliance != (byte)XI.Map.PartyKind.Party + 1 ? 1 : 0).ToString());

        // ---- 0x06f GROUP_LEAVE layout (PartyKind Kind member) ----
        Check("GP LEAVE_SIZE", g["LEAVE_SIZE"], XI.Map.C2sGroupLeave.SizeOf.ToString());
        Check("GP LEAVE_ALIGN", g["LEAVE_ALIGN"], XI.Map.C2sGroupLeave.AlignOf.ToString());
        Check("GP OFF_LEAVE.Kind", g["OFF_LEAVE.Kind"], XI.Map.C2sGroupLeave.OffKind.ToString());
        // Kind packs at offset 4 == the 4-byte header size (uint8 has alignof 1, no realign after the header).
        Check("GP LEAVE Kind at header end (4, no realign)",
            "1",
            (XI.Map.C2sGroupLeave.OffKind == XI.Map.C2sGroupLeave.HeaderSize ? 1 : 0).ToString());
        // size 6 = 4 (header) + 1 (Kind) padded to the struct's align 2 -> 6 (ONE tail-pad byte at index 5).
        Check("GP LEAVE size 6 (4+1 Kind, tail-padded to align 2)",
            "1",
            (XI.Map.C2sGroupLeave.SizeOf == 6 && XI.Map.C2sGroupLeave.OffKind + 1 < XI.Map.C2sGroupLeave.SizeOf ? 1 : 0).ToString());

        // ---- 0x070 GROUP_BREAKUP layout (identical single-PartyKind shape) ----
        Check("GP BREAKUP_SIZE", g["BREAKUP_SIZE"], XI.Map.C2sGroupBreakup.SizeOf.ToString());
        Check("GP BREAKUP_ALIGN", g["BREAKUP_ALIGN"], XI.Map.C2sGroupBreakup.AlignOf.ToString());
        Check("GP OFF_BREAKUP.Kind", g["OFF_BREAKUP.Kind"], XI.Map.C2sGroupBreakup.OffKind.ToString());
        // the two bodies are byte-identical (same single PartyKind member):
        Check("GP LEAVE and BREAKUP are byte-identical layout",
            "1",
            (XI.Map.C2sGroupLeave.SizeOf == XI.Map.C2sGroupBreakup.SizeOf
             && XI.Map.C2sGroupLeave.AlignOf == XI.Map.C2sGroupBreakup.AlignOf
             && XI.Map.C2sGroupLeave.OffKind == XI.Map.C2sGroupBreakup.OffKind ? 1 : 0).ToString());

        // ==== THE ENGINE-ENUM TECHNIQUE'S LOAD-BEARING FACT (what unblocks the ~50 deferred bodies) ====
        // The compiled oracle emitted EQ_SIZE/EQ_ALIGN/EQ_OFFSET == 1 proving a scoped `enum class : uint8_t`
        // member lays out byte-for-byte identically to a plain `uint8_t` member. So a LAYOUT oracle needs only
        // the enum's underlying type; the literal NAMES are pinned by the independent .h re-parse (below).
        Check("GP enum-member == uint8_t twin (compiler EQ_SIZE)", "1", g["EQ_SIZE"]);
        Check("GP enum-member == uint8_t twin (compiler EQ_ALIGN)", "1", g["EQ_ALIGN"]);
        Check("GP enum-member == uint8_t twin (compiler EQ_OFFSET)", "1", g["EQ_OFFSET"]);
        // the twin's compiler size/offset equal the real struct's (the substitution is exact, not approximate):
        Check("GP uint8_t twin size == real struct size (6)",
            g["LEAVE_U8_SIZE"], g["LEAVE_SIZE"]);
        Check("GP uint8_t twin Kind offset == real struct Kind offset (4)",
            g["OFF_LEAVE_U8.Kind"], g["OFF_LEAVE.Kind"]);
        Check("GP twin size matches C# struct (underlying-type substitution is exact)",
            g["LEAVE_U8_SIZE"], XI.Map.C2sGroupLeave.SizeOf.ToString());

        // ---- whole-struct fills (both 6-byte i*7+3, incl the one tail-pad byte) + byte-identical bodies ----
        foreach (var tag in new[] { "LEAVE", "BREAKUP" })
        {
            byte[] fill = HexToBytes(g[tag + "_FILL"]);
            Check($"GP {tag}_FILL_SIZE", g[$"{tag}_FILL_SIZE"], fill.Length.ToString());
            var local = new byte[fill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check($"GP {tag} deterministic fill", g[$"{tag}_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }
        Check("GP LEAVE_FILL == BREAKUP_FILL (identical bodies)", g["LEAVE_FILL"], g["BREAKUP_FILL"]);

        // read-path: accessor == raw read at the compiler's nested offset (Kind@4). GetKindRaw avoids the
        // undefined-enum-name issue (the fill byte 0x1F is not a named PartyKind), so compare the RAW byte.
        byte[] lf = HexToBytes(g["LEAVE_FILL"]);
        Check("GP GetKindRaw==raw@compiler(Kind@4)", lf[(int)ParseNum(g["OFF_LEAVE.Kind"])].ToString(), XI.Map.C2sGroupLeave.GetKindRaw(lf).ToString());
        Check("GP BREAK GetKindRaw==raw@compiler(Kind@4)", lf[(int)ParseNum(g["OFF_BREAKUP.Kind"])].ToString(), XI.Map.C2sGroupBreakup.GetKindRaw(lf).ToString());

        // write-path: SetKind touches ONLY the Kind byte -- the header (0..3) and tail pad (5) are untouched.
        var wb = new byte[XI.Map.C2sGroupLeave.SizeOf];
        for (int i = 0; i < wb.Length; i++) wb[i] = 0xEE;   // known filler incl. header + tail-pad bytes
        XI.Map.C2sGroupLeave.SetKind(wb, XI.Map.PartyKind.Party);
        Check("GP SetKind(Party=0) writes only Kind byte", "0", wb[XI.Map.C2sGroupLeave.OffKind].ToString());
        Check("GP SetKind(Party) preserves header byte 0", "238", wb[0].ToString());
        Check("GP SetKind(Party) preserves tail-pad byte 5", "238", wb[5].ToString());
        Check("GP GetKind(Party) roundtrip", "Party", XI.Map.C2sGroupLeave.GetKind(wb).ToString());
        XI.Map.C2sGroupLeave.SetKind(wb, XI.Map.PartyKind.Alliance);
        Check("GP SetKind(Alliance=5) writes the Kind byte", "5", wb[XI.Map.C2sGroupLeave.OffKind].ToString());
        Check("GP GetKind(Alliance) roundtrip", "Alliance", XI.Map.C2sGroupLeave.GetKind(wb).ToString());
        Check("GP SetKind(Alliance) still leaves tail-pad byte 5", "238", wb[5].ToString());
        // GROUP_BREAKUP setter path (same single-member body).
        var bb = new byte[XI.Map.C2sGroupBreakup.SizeOf];
        XI.Map.C2sGroupBreakup.SetKind(bb, XI.Map.PartyKind.Alliance);
        Check("GP BREAKUP SetKind(Alliance) at compiler Kind offset 4", "5",
            bb[(int)ParseNum(g["OFF_BREAKUP.Kind"])].ToString());

        // ==== independent .h re-parse of the SEPARATE-HEADER enum (the chunk-2 packet-id technique) ====
        // Cross-checks the committed C# XI.Map.PartyKind against a SECOND parser of src/map/enums/party_kind.h
        // (magic_enum isn't vendored, so literals are .h ground truth, not compiler-derived). Reuses VerifyPacketIds.
        VerifyPacketIds("PartyKind", enumHeaderPath, typeof(XI.Map.PartyKind));
        // AND the third-way agreement the technique relies on: the .h re-parse VALUES == the compiler's
        // underlying-type ENUM_* values (so both independent sources concur with the C# enum).
        if (System.IO.File.Exists(enumHeaderPath))
        {
            var re = ParseCppEnum(System.IO.File.ReadAllText(enumHeaderPath), "PartyKind");
            foreach (var (name, val) in re)
            {
                string key = "ENUM_" + name;
                if (g.ContainsKey(key))
                    Check($"GP re-parse {name} == compiler underlying value", g[key], val.ToString());
            }
        }

        // shared C2S header framing == C2S/CBasicPacket (re-asserted via the group-party enum body's CHDR_SIZE).
        Check("GP header framing == C2S size", "4", g["CHDR_SIZE"]);
    }

    static void VerifyMapS2cGroupSolicitReq(string dir, string enumHeaderPath)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_solicit_req_enum_golden.txt");
        Console.WriteLine("\n== S2c group_solicit_req (0x0dc) parity: C# XI.Map vs layout oracle + .h re-parse ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c solicit_req enum golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; engine adds the 4-byte header).
        Check("SR SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- the SEPARATE-HEADER enum (PartyKind, same as chunk 21) re-pinned in THIS body's oracle ----
        Check("SR ENUM Party", g["ENUM_Party"], ((byte)XI.Map.PartyKind.Party).ToString());
        Check("SR ENUM Alliance", g["ENUM_Alliance"], ((byte)XI.Map.PartyKind.Alliance).ToString());
        Check("SR enum underlying size 1 (uint8_t)", g["ENUM_underlying_size"], "1");

        // ---- outer body layout (default packing, body-relative): UniqueNo/ActIndex/AnonFlag/Kind/sName/RaceNo/padding1E ----
        Check("SR SR_SIZE", g["SR_SIZE"], XI.Map.S2cGroupSolicitReq.SizeOf.ToString());
        Check("SR SR_ALIGN", g["SR_ALIGN"], XI.Map.S2cGroupSolicitReq.AlignOf.ToString());
        Check("SR OFF.UniqueNo", g["OFF_SR.UniqueNo"], XI.Map.S2cGroupSolicitReq.OffUniqueNo.ToString());
        Check("SR OFF.ActIndex", g["OFF_SR.ActIndex"], XI.Map.S2cGroupSolicitReq.OffActIndex.ToString());
        Check("SR OFF.AnonFlag", g["OFF_SR.AnonFlag"], XI.Map.S2cGroupSolicitReq.OffAnonFlag.ToString());
        Check("SR OFF.Kind", g["OFF_SR.Kind"], XI.Map.S2cGroupSolicitReq.OffKind.ToString());
        Check("SR OFF.sName", g["OFF_SR.sName"], XI.Map.S2cGroupSolicitReq.OffSName.ToString());
        Check("SR sName_len", g["SR_sName_len"], XI.Map.S2cGroupSolicitReq.SNameLen.ToString());
        Check("SR OFF.RaceNo", g["OFF_SR.RaceNo"], XI.Map.S2cGroupSolicitReq.OffRaceNo.ToString());
        Check("SR OFF.padding1E", g["OFF_SR.padding1E"], XI.Map.S2cGroupSolicitReq.OffPadding1E.ToString());
        Check("SR padding1E_len", g["SR_padding1E_len"], XI.Map.S2cGroupSolicitReq.Padding1ELen.ToString());

        // combined-hazard #3: CONSECUTIVE byte-run AnonFlag@6 + Kind@7 (both alignof 1, no realign between them)
        // -- a hand-reasoner aligning the enum to 2 would wrongly put Kind at 8 (shifting sName/RaceNo).
        Check("SR Kind packs right after AnonFlag (consecutive byte-run, no realign)",
            "1",
            (XI.Map.S2cGroupSolicitReq.OffKind == XI.Map.S2cGroupSolicitReq.OffAnonFlag + 1 ? 1 : 0).ToString());
        Check("SR Kind@7 (not realigned to 8)", "1", (XI.Map.S2cGroupSolicitReq.OffKind == 7 ? 1 : 0).ToString());
        // char[16] packs at the running byte offset right after the enum (alignof 1, no realign).
        Check("SR sName after Kind+1 (char alignof 1)",
            "1",
            (XI.Map.S2cGroupSolicitReq.OffSName == XI.Map.S2cGroupSolicitReq.OffKind + 1 ? 1 : 0).ToString());
        // RaceNo REALIGNS to a 2-boundary right after the 16-byte sName (8+16==24, already 2-aligned).
        Check("SR RaceNo after sName+16 (24, realigns to 2)",
            "1",
            (XI.Map.S2cGroupSolicitReq.OffRaceNo == XI.Map.S2cGroupSolicitReq.OffSName + XI.Map.S2cGroupSolicitReq.SNameLen
             && XI.Map.S2cGroupSolicitReq.OffRaceNo % 2 == 0 ? 1 : 0).ToString());
        Check("SR padding1E after RaceNo+2", "1",
            (XI.Map.S2cGroupSolicitReq.OffPadding1E == XI.Map.S2cGroupSolicitReq.OffRaceNo + 2 ? 1 : 0).ToString());
        // NO tail pad: 26+2 == 28 == SizeOf (already a 4-multiple, no rounding needed).
        Check("SR padding1E+2==SizeOf (no tail pad)", "1",
            (XI.Map.S2cGroupSolicitReq.OffPadding1E + XI.Map.S2cGroupSolicitReq.Padding1ELen == XI.Map.S2cGroupSolicitReq.SizeOf ? 1 : 0).ToString());

        // combined-hazard #1: the chunk-21 engine-enum substitution invariant RE-CONFIRMED in the S2C packing
        // context -- a uint8_t twin (Kind -> uint8_t) is byte-identical in size/align AND the Kind offset.
        Check("SR enum-member == uint8_t twin (compiler EQ_SIZE)", "1", g["EQ_SIZE"]);
        Check("SR enum-member == uint8_t twin (compiler EQ_ALIGN)", "1", g["EQ_ALIGN"]);
        Check("SR enum-member == uint8_t twin (compiler EQ_KIND_OFFSET)", "1", g["EQ_KIND_OFFSET"]);
        Check("SR twin size == real struct size (28)", g["SRU8_SIZE"], g["SR_SIZE"]);
        Check("SR twin Kind offset == real Kind offset (7)", g["OFF_SRU8.Kind"], g["OFF_SR.Kind"]);

        // whole-struct fills (both byte-identical -- the twin proves the substitution is exact):
        foreach (var tag in new[] { "SR", "SRU8" })
        {
            byte[] fill = HexToBytes(g[tag + "_FILL"]);
            Check($"SR {tag}_FILL_SIZE", g[$"{tag}_FILL_SIZE"], fill.Length.ToString());
            var local = new byte[fill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check($"SR {tag} deterministic fill", g[$"{tag}_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }
        Check("SR SR_FILL == SRU8_FILL (enum/uint8 twin byte-identical)", g["SR_FILL"], g["SRU8_FILL"]);

        // read-path: accessor == raw read at the compiler's offset (two independent ways).
        byte[] sr = HexToBytes(g["SR_FILL"]);
        Check("SR GetUniqueNo==raw@compiler", BitConverter.ToUInt32(sr, (int)ParseNum(g["OFF_SR.UniqueNo"])).ToString(), XI.Map.S2cGroupSolicitReq.GetUniqueNo(sr).ToString());
        Check("SR GetActIndex==raw@compiler", BitConverter.ToUInt16(sr, (int)ParseNum(g["OFF_SR.ActIndex"])).ToString(), XI.Map.S2cGroupSolicitReq.GetActIndex(sr).ToString());
        Check("SR GetAnonFlag==raw@compiler(u8@6)", sr[(int)ParseNum(g["OFF_SR.AnonFlag"])].ToString(), XI.Map.S2cGroupSolicitReq.GetAnonFlag(sr).ToString());
        // the enum member's RAW byte (the fill byte at Kind@7 is 0x34, not a named PartyKind -> compare raw):
        Check("SR GetKindRaw==raw@compiler(enum u8@7)", sr[(int)ParseNum(g["OFF_SR.Kind"])].ToString(), XI.Map.S2cGroupSolicitReq.GetKindRaw(sr).ToString());
        // the char[16] string field -- raw bytes at the compiler's sName offset, and the NUL-bounded read.
        Check("SR GetSNameBytes==raw@compiler(char[16]@8)",
            BitConverter.ToString(sr, (int)ParseNum(g["OFF_SR.sName"]), XI.Map.S2cGroupSolicitReq.SNameLen).Replace("-", ""),
            BitConverter.ToString(XI.Map.S2cGroupSolicitReq.GetSNameBytes(sr).ToArray()).Replace("-", ""));
        Check("SR GetRaceNo==raw@compiler(u16@24)", BitConverter.ToUInt16(sr, (int)ParseNum(g["OFF_SR.RaceNo"])).ToString(), XI.Map.S2cGroupSolicitReq.GetRaceNo(sr).ToString());

        // consecutive byte-run write isolation: SetAnonFlag must not disturb Kind@7 and vice versa.
        var rb = new byte[XI.Map.S2cGroupSolicitReq.SizeOf];
        XI.Map.S2cGroupSolicitReq.SetAnonFlag(rb, 0x11);
        XI.Map.S2cGroupSolicitReq.SetKind(rb, XI.Map.PartyKind.Alliance);
        Check("SR SetAnonFlag/SetKind independent (6=0x11, 7=5)",
            "1",
            (rb[6] == 0x11 && rb[7] == 5 ? 1 : 0).ToString());
        Check("SR GetKind roundtrip (Alliance)", "Alliance", XI.Map.S2cGroupSolicitReq.GetKind(rb).ToString());
        XI.Map.S2cGroupSolicitReq.SetKind(rb, XI.Map.PartyKind.Party);
        Check("SR GetKind roundtrip (Party) preserves AnonFlag@6",
            "17",
            (rb[XI.Map.S2cGroupSolicitReq.OffAnonFlag]).ToString());

        // char[16] string field: full-length write leaves NO NUL; short write is NUL-padded; NUL-bounded read.
        var sb = new byte[XI.Map.S2cGroupSolicitReq.SizeOf];
        var nm = "PLAYER_ONE";   // 10 bytes
        XI.Map.S2cGroupSolicitReq.SetSNameString(sb, nm);
        Check("SR SetSNameString lands at compiler sName offset 8",
            "PLAYER_ONE",
            System.Text.Encoding.ASCII.GetString(sb, (int)ParseNum(g["OFF_SR.sName"]), nm.Length));
        Check("SR GetSNameString roundtrip", "PLAYER_ONE", XI.Map.S2cGroupSolicitReq.GetSNameString(sb));
        Check("SR short write NUL-pads to end of field",
            "1",
            (sb[XI.Map.S2cGroupSolicitReq.OffSName + nm.Length] == 0 ? 1 : 0).ToString());
        // a NUL-terminated read stops at the first NUL even with trailing garbage:
        var nb = new byte[XI.Map.S2cGroupSolicitReq.SizeOf];
        for (int i = 0; i < nb.Length; i++) nb[i] = (byte)'X';
        nb[XI.Map.S2cGroupSolicitReq.OffSName + 3] = 0;   // NUL at field byte 3 -> reads "XXX"
        Check("SR GetSNameString NUL-bounded read", "XXX", XI.Map.S2cGroupSolicitReq.GetSNameString(nb));
        // the string setter is bounded to the 16-byte field: write 16 -> full field, RaceNo@24 UNTOUCHED.
        var fb = new byte[XI.Map.S2cGroupSolicitReq.SizeOf];
        XI.Map.S2cGroupSolicitReq.SetRaceNo(fb, 0x1234);
        XI.Map.S2cGroupSolicitReq.SetSNameString(fb, "ABCDEFGHIJKLMNOP");   // exactly 16, no NUL
        Check("SR full-16 string write leaves no NUL in field",
            "1",
            (fb[XI.Map.S2cGroupSolicitReq.OffSName + 15] == (byte)'P' ? 1 : 0).ToString());
        Check("SR string write does NOT clobber RaceNo@24 (bounded to field)", "4660", XI.Map.S2cGroupSolicitReq.GetRaceNo(fb).ToString());

        // leading-scalar setter landing / round-trips (compiler-offset + read-back).
        var wb = new byte[XI.Map.S2cGroupSolicitReq.SizeOf];
        XI.Map.S2cGroupSolicitReq.SetUniqueNo(wb, 0xCAFEBABE);
        XI.Map.S2cGroupSolicitReq.SetActIndex(wb, 0x002A);
        XI.Map.S2cGroupSolicitReq.SetRaceNo(wb, 0x00FF);
        Check("SR SetUniqueNo at compiler offset 0", "0xCAFEBABE", $"0x{BitConverter.ToUInt32(wb, (int)ParseNum(g["OFF_SR.UniqueNo"])):X8}");
        Check("SR GetUniqueNo roundtrip", "3405691582", XI.Map.S2cGroupSolicitReq.GetUniqueNo(wb).ToString());
        Check("SR GetActIndex roundtrip", "42", XI.Map.S2cGroupSolicitReq.GetActIndex(wb).ToString());
        Check("SR SetRaceNo lands at compiler offset 24", "255", BitConverter.ToUInt16(wb, (int)ParseNum(g["OFF_SR.RaceNo"])).ToString());
        Check("SR GetRaceNo roundtrip", "255", XI.Map.S2cGroupSolicitReq.GetRaceNo(wb).ToString());

        // ==== independent .h re-parse of the reused separate-header enum (chunk-21 technique, re-applied) ====
        VerifyPacketIds("PartyKind", enumHeaderPath, typeof(XI.Map.PartyKind));
        if (System.IO.File.Exists(enumHeaderPath))
        {
            var re = ParseCppEnum(System.IO.File.ReadAllText(enumHeaderPath), "PartyKind");
            foreach (var (name, val) in re)
            {
                string key = "ENUM_" + name;
                if (g.ContainsKey(key))
                    Check($"SR re-parse {name} == compiler underlying value", g[key], val.ToString());
            }
        }
    }

    static void VerifyMapS2cGroupTbl(string dir, string enumHeaderPath)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_group_tbl_enum_golden.txt");
        Console.WriteLine("\n== S2c group_tbl (0x0c8) parity: C# XI.Map vs layout oracle + .h re-parse ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c group_tbl enum golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; engine adds the 4-byte header).
        Check("GT SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- the REUSED separate-header enum (PartyKind, same as chunk 21/22) ----
        Check("GT ENUM Party", g["ENUM_Party"], ((byte)XI.Map.PartyKind.Party).ToString());
        Check("GT ENUM Alliance", g["ENUM_Alliance"], ((byte)XI.Map.PartyKind.Alliance).ToString());
        Check("GT enum underlying size 1 (uint8_t)", g["ENUM_underlying_size"], "1");

        // ---- GROUP_TBL (nested struct / array element) layout 12/4 ----
        Check("GT GT_SIZE", g["GT_SIZE"], XI.Map.GroupTblEntry.SizeOf.ToString());
        Check("GT GT_ALIGN", g["GT_ALIGN"], XI.Map.GroupTblEntry.AlignOf.ToString());
        Check("GT OFF.UniqueNo", g["OFF_GT.UniqueNo"], XI.Map.GroupTblEntry.OffUniqueNo.ToString());
        Check("GT OFF.ActIndex", g["OFF_GT.ActIndex"], XI.Map.GroupTblEntry.OffActIndex.ToString());
        Check("GT OFF.padding07", g["OFF_GT.padding07"], XI.Map.GroupTblEntry.OffPadding07.ToString());
        Check("GT OFF.ZoneNo", g["OFF_GT.ZoneNo"], XI.Map.GroupTblEntry.OffZoneNo.ToString());
        Check("GT OFF.padding0A", g["OFF_GT.padding0A"], XI.Map.GroupTblEntry.OffPadding0A.ToString());
        // ZoneNo (u16) realigns to a 2-boundary right after the bit-field byte + padding07 (6+1=7 -> 8).
        Check("GT ZoneNo@8 realigns to 2 after padding07@7", "1",
            (XI.Map.GroupTblEntry.OffZoneNo == 8 && XI.Map.GroupTblEntry.OffPadding07 == 7 ? 1 : 0).ToString());
        Check("GT padding0A@10 == ZoneNo+2", "1",
            (XI.Map.GroupTblEntry.OffPadding0A == XI.Map.GroupTblEntry.OffZoneNo + 2 ? 1 : 0).ToString());
        Check("GT entry gapless: padding0A+2==SizeOf (12)", "1",
            (XI.Map.GroupTblEntry.OffPadding0A + 2 == XI.Map.GroupTblEntry.SizeOf ? 1 : 0).ToString());

        // ---- THE new hazard: SEVEN-field bit-field run inside the element, ALL in ONE byte (element offset 6) ----
        // (offsetof is ILLEGAL on a bit-field -> each byte/mask/shift came from the oracle's per-field sentinel scan.)
        foreach (var bf in new (string key, int mask, int shift, int width)[]
                 {
                     ("PartyNo", XI.Map.GroupTblEntry.PartyNoMask, XI.Map.GroupTblEntry.PartyNoShift, 2),
                     ("PartyLeaderFlg", XI.Map.GroupTblEntry.PartyLeaderMask, XI.Map.GroupTblEntry.PartyLeaderShift, 1),
                     ("AllianceLeaderFlg", XI.Map.GroupTblEntry.AllianceLeaderMask, XI.Map.GroupTblEntry.AllianceLeaderShift, 1),
                     ("PartyRFlg", XI.Map.GroupTblEntry.PartyRMask, XI.Map.GroupTblEntry.PartyRShift, 1),
                     ("AllianceRFlg", XI.Map.GroupTblEntry.AllianceRMask, XI.Map.GroupTblEntry.AllianceRShift, 1),
                     ("unknown06", XI.Map.GroupTblEntry.Unknown06Mask, XI.Map.GroupTblEntry.Unknown06Shift, 1),
                     ("unknown07", XI.Map.GroupTblEntry.Unknown07Mask, XI.Map.GroupTblEntry.Unknown07Shift, 1),
                 })
        {
            Check($"GT BF {bf.key} off", g[$"BF_{bf.key}_off"], XI.Map.GroupTblEntry.OffBitfield.ToString());
            Check($"GT BF {bf.key} mask", g[$"BF_{bf.key}_mask"], bf.mask.ToString());
            Check($"GT BF {bf.key} shift", g[$"BF_{bf.key}_shift"], bf.shift.ToString());
            // the compiler emitted the width too: mask == ((1<<width)-1) << shift.
            Check($"GT BF {bf.key} mask==((1<<width)-1)<<shift", "1",
                (((((1 << bf.width) - 1) << bf.shift)) == bf.mask ? 1 : 0).ToString());
        }
        // ALL seven fields share ONE byte (off=6), and their widths sum to exactly 8 (no straddle, no gap).
        Check("GT all 7 bit-fields live in the single byte at element offset 6", "1",
            ((int)ParseNum(g["BF_PartyNo_off"]) == 6 && (int)ParseNum(g["BF_unknown07_off"]) == 6 ? 1 : 0).ToString());
        Check("GT bit-field widths sum to 8 (one full uint8 unit)", "1",
            (2 + 1 + 1 + 1 + 1 + 1 + 1 == 8 && XI.Map.GroupTblEntry.OffBitfield + 1 == XI.Map.GroupTblEntry.OffPadding07 ? 1 : 0).ToString());
        // PartyNo is the low 2-bit field (mask 0x03, shift 0) -- the first 2-bit-width field in a run so far.
        Check("GT PartyNo is a 2-bit low field (mask 0x03, shift 0)", "1",
            (XI.Map.GroupTblEntry.PartyNoMask == 0x03 && XI.Map.GroupTblEntry.PartyNoShift == 0 ? 1 : 0).ToString());

        // ---- PD_GROUP_TBL: engine-enum head + struct array (GroupTbl[20]) ----
        Check("GT PD_SIZE", g["PD_SIZE"], XI.Map.S2cGroupTbl.SizeOf.ToString());
        Check("GT PD_ALIGN", g["PD_ALIGN"], XI.Map.S2cGroupTbl.AlignOf.ToString());
        Check("GT OFF.Kind", g["OFF_PD.Kind"], XI.Map.S2cGroupTbl.OffKind.ToString());
        Check("GT OFF.padding05", g["OFF_PD.padding05"], XI.Map.S2cGroupTbl.OffPadding05.ToString());
        Check("GT padding05_len", g["PD_padding05_len"], XI.Map.S2cGroupTbl.Padding05Len.ToString());
        Check("GT OFF.GroupTbl", g["OFF_PD.GroupTbl"], XI.Map.S2cGroupTbl.OffGroupTbl.ToString());
        Check("GT GroupTbl_count", g["PD_GroupTbl_count"], XI.Map.S2cGroupTbl.GroupTblCount.ToString());
        Check("GT GroupTbl_len", g["PD_GroupTbl_len"], XI.Map.S2cGroupTbl.GroupTblLen.ToString());
        // the array packs right after the 1-byte enum + 3-byte padding (0+1+3 == 4, already align-4 start).
        Check("GT array starts at Kind+1+padding05(3) (4-aligned, no realign)", "1",
            (XI.Map.S2cGroupTbl.OffGroupTbl == XI.Map.S2cGroupTbl.OffKind + 1 + XI.Map.S2cGroupTbl.Padding05Len ? 1 : 0).ToString());
        // stride == element size -> NO inter-element padding; and 4 + 20*12 == 244 == SizeOf (no tail pad).
        Check("GT stride == element size (12, no inter-element pad)", g["GT_STRIDE"], XI.Map.GroupTblEntry.SizeOf.ToString());
        Check("GT array_len == count*stride", "1",
            (XI.Map.S2cGroupTbl.GroupTblLen == XI.Map.S2cGroupTbl.GroupTblCount * XI.Map.GroupTblEntry.SizeOf ? 1 : 0).ToString());
        Check("GT array+SizeOf gapless (4 + 20*12 == 244)", "1",
            (XI.Map.S2cGroupTbl.OffGroupTbl + XI.Map.S2cGroupTbl.GroupTblLen == XI.Map.S2cGroupTbl.SizeOf ? 1 : 0).ToString());
        Check("GT ElementOffset(1) == compiler GroupTbl1_UniqueNo", g["OFF_PD.GroupTbl1_UniqueNo"], XI.Map.S2cGroupTbl.ElementOffset(1).ToString());
        Check("GT last element ends at SizeOf (no tail pad)", g["OFF_PD.GroupTbl19_last"], XI.Map.S2cGroupTbl.SizeOf.ToString());

        // nested-inclusive offsets for the accessors (array start + element*stride + field):
        Check("GT GroupTbl0_UniqueNo == ElementField(0,UniqueNo)", g["OFF_PD.GroupTbl0_UniqueNo"], XI.Map.S2cGroupTbl.ElementField(0, XI.Map.GroupTblEntry.OffUniqueNo).ToString());
        Check("GT GroupTbl0_bitbyte == ElementField(0,bitbyte)", g["OFF_PD.GroupTbl0_bitbyte"], XI.Map.S2cGroupTbl.ElementField(0, XI.Map.GroupTblEntry.OffBitfield).ToString());
        Check("GT GroupTbl0_ZoneNo == ElementField(0,ZoneNo)", g["OFF_PD.GroupTbl0_ZoneNo"], XI.Map.S2cGroupTbl.ElementField(0, XI.Map.GroupTblEntry.OffZoneNo).ToString());

        // whole-struct fills: GROUP_TBL (12) + PD (244). The PD fill proves the array is gapless (i*7+3 contiguous).
        foreach (var kv in new (string tag, int size)[] { ("GT", XI.Map.GroupTblEntry.SizeOf), ("PD", XI.Map.S2cGroupTbl.SizeOf) })
        {
            byte[] fill = HexToBytes(g[kv.tag + "_FILL"]);
            Check($"{kv.tag} {kv.tag}_FILL_SIZE", g[$"{kv.tag}_FILL_SIZE"], fill.Length.ToString());
            var local = new byte[fill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check($"{kv.tag} deterministic fill", g[$"{kv.tag}_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }
        // THE read-path buffer is the 244-byte PD fill: the GroupTbl array is at body offset 4, so element 0's
        // field X sits at the COMPILER's nested-inclusive offset OFF_PD.GroupTbl0_<X> (== 4 + element-offset X).
        // Using the bare 12-byte element fill with a PD-relative accessor would read the WRONG byte (the array
        // accessor adds OffGroupTbl==4), which is exactly the trap this chunk must not repeat.
        byte[] pdf = HexToBytes(g["PD_FILL"]);
        Check("GT GetUniqueNo[0]==raw@compiler(PD body 4)", BitConverter.ToUInt32(pdf, (int)ParseNum(g["OFF_PD.GroupTbl0_UniqueNo"])).ToString(), XI.Map.S2cGroupTbl.GetUniqueNo(pdf, 0).ToString());
        Check("GT GetUniqueNo[1]==raw@compiler(GroupTbl1 stride)",
            BitConverter.ToUInt32(pdf, (int)ParseNum(g["OFF_PD.GroupTbl1_UniqueNo"])).ToString(),
            XI.Map.S2cGroupTbl.GetUniqueNo(pdf, 1).ToString());
        Check("GT GetZoneNo[0]==raw@compiler(PD body 12)", BitConverter.ToUInt16(pdf, (int)ParseNum(g["OFF_PD.GroupTbl0_ZoneNo"])).ToString(), XI.Map.S2cGroupTbl.GetZoneNo(pdf, 0).ToString());
        // the element-0 bitbyte is at PD body offset 10 (OFF_PD.GroupTbl0_bitbyte == 4 + 6); the 2-bit PartyNo mask.
        Check("GT GetPartyNo[0]==masked raw@compiler(PD body 10)",
            (pdf[(int)ParseNum(g["OFF_PD.GroupTbl0_bitbyte"])] & XI.Map.GroupTblEntry.PartyNoMask).ToString(),
            XI.Map.S2cGroupTbl.GetPartyNo(pdf, 0).ToString());
        Check("GT GetPartyLeaderFlg[0]==masked raw@compiler(PD body 10)",
            (((pdf[(int)ParseNum(g["OFF_PD.GroupTbl0_bitbyte"])] & XI.Map.GroupTblEntry.PartyLeaderMask) != 0) ? "True" : "False"),
            XI.Map.S2cGroupTbl.GetPartyLeaderFlg(pdf, 0).ToString());
        Check("GT GetActIndex[0]==raw@compiler(PD body 8)", BitConverter.ToUInt16(pdf, (int)ParseNum(g["OFF_PD.GroupTbl0_bitbyte"]) - 2).ToString(), XI.Map.S2cGroupTbl.GetActIndex(pdf, 0).ToString());

        // 2-bit PartyNo RMW preserves the six 1-bit flags in the SAME byte (the genuinely-new hazard).
        var wb = new byte[XI.Map.S2cGroupTbl.SizeOf];
        // set all six flags on in element 0, then set PartyNo=2 -> PartyNo occupies bits 0-1, flags stay in 2-7.
        XI.Map.S2cGroupTbl.SetPartyLeaderFlg(wb, 0, true);   // 0x04
        XI.Map.S2cGroupTbl.SetAllianceRFlg(wb, 0, true);     // 0x20
        XI.Map.S2cGroupTbl.SetPartyNo(wb, 0, 2);            // bits0-1 = 0b10 -> 0x02
        Check("GT SetPartyNo(2) + 2 flags => byte6 == 0x26", "38", wb[10].ToString());   // 0x04|0x20|0x02 == 0x26
        Check("GT GetPartyNo preserves flags", "2", XI.Map.S2cGroupTbl.GetPartyNo(wb, 0).ToString());
        Check("GT PartyLeaderFlg survives SetPartyNo", "True", XI.Map.S2cGroupTbl.GetPartyLeaderFlg(wb, 0).ToString());
        Check("GT AllianceRFlg survives SetPartyNo", "True", XI.Map.S2cGroupTbl.GetAllianceRFlg(wb, 0).ToString());
        // clearing PartyNo must keep the flags (RMW on only the 0x03 mask):
        XI.Map.S2cGroupTbl.SetPartyNo(wb, 0, 0);
        Check("GT SetPartyNo(0) clears only low 2 bits (byte6 -> 0x24)", "36", wb[10].ToString());

        // cross-element isolation: writing element 5's bit-field byte must not touch element 6 (stride 12).
        var eb = new byte[XI.Map.S2cGroupTbl.SizeOf];
        XI.Map.S2cGroupTbl.SetPartyNo(eb, 5, 3);
        XI.Map.S2cGroupTbl.SetAllianceLeaderFlg(eb, 5, true);
        Check("GT SetPartyNo[5]+flag[5] at element offset (4+5*12+6)", "1",
            (eb[4 + 5 * 12 + 6] == (byte)(0x03 | XI.Map.GroupTblEntry.AllianceLeaderMask) ? 1 : 0).ToString());
        Check("GT element 6 bitbyte untouched by element 5 write", "0", XI.Map.S2cGroupTbl.GetPartyNo(eb, 6).ToString());
        Check("GT element 5 PartyNo roundtrip", "3", XI.Map.S2cGroupTbl.GetPartyNo(eb, 5).ToString());

        // leading enum + fixed setters / landing.
        var bb = new byte[XI.Map.S2cGroupTbl.SizeOf];
        XI.Map.S2cGroupTbl.SetKind(bb, XI.Map.PartyKind.Alliance);
        XI.Map.S2cGroupTbl.SetUniqueNo(bb, 0, 0x11223344);
        XI.Map.S2cGroupTbl.SetActIndex(bb, 0, 0x0009);
        XI.Map.S2cGroupTbl.SetZoneNo(bb, 1, 0x00AB);   // element 1 ZoneNo at body 4+12+8 = 24
        Check("GT SetKind(Alliance) writes byte0", "5", bb[XI.Map.S2cGroupTbl.OffKind].ToString());
        Check("GT GetKind roundtrip", "Alliance", XI.Map.S2cGroupTbl.GetKind(bb).ToString());
        Check("GT SetUniqueNo[0] at body offset 4", "0x11223344", $"0x{BitConverter.ToUInt32(bb, XI.Map.S2cGroupTbl.ElementField(0, 0)):X8}");
        Check("GT SetZoneNo[1] at compiler stride offset 24", "171", BitConverter.ToUInt16(bb, 24).ToString());
        Check("GT GetZoneNo[1] roundtrip", "171", XI.Map.S2cGroupTbl.GetZoneNo(bb, 1).ToString());
        // the padding05 read returns the compiler's exact 3 bytes.
        Check("GT GetPadding05==raw@compiler(1..3)",
            BitConverter.ToString(pdf, (int)ParseNum(g["OFF_PD.padding05"]), XI.Map.S2cGroupTbl.Padding05Len).Replace("-", ""),
            BitConverter.ToString(XI.Map.S2cGroupTbl.GetPadding05(pdf).ToArray()).Replace("-", ""));

        // ==== independent .h re-parse of the REUSED engine enum (same party_kind.h as chunk 21/22) ====
        VerifyPacketIds("PartyKind", enumHeaderPath, typeof(XI.Map.PartyKind));
        if (System.IO.File.Exists(enumHeaderPath))
        {
            var re = ParseCppEnum(System.IO.File.ReadAllText(enumHeaderPath), "PartyKind");
            foreach (var (name, val) in re)
            {
                string key = "ENUM_" + name;
                if (g.ContainsKey(key))
                    Check($"GT re-parse {name} == compiler underlying value", g[key], val.ToString());
            }
        }

        // shared S2C header framing == C2S/CBasicPacket (re-asserted via the group_tbl body's SHDR_SIZE).
        Check("GT header framing == C2S size", "4", g["SHDR_SIZE"]);
    }

    static void VerifyMapS2cBattleMessage(string dir, string enumHeaderPath)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_battle_message_enum_golden.txt");
        Console.WriteLine("\n== S2c battle_message (0x029) parity: C# XI.Map vs layout oracle + msg_std.h re-parse ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c battle_message enum golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; engine adds the 4-byte header).
        Check("BM SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- MsgStd underlying width (uint16) + the 15 compiler-underlying-cast representative literals ----
        Check("BM enum underlying size 2 (uint16_t)", g["ENUM_underlying_size"], "2");
        // the C# body models MessageNum as its underlying ushort (a scoped enum's storage == underlying integer,
        // proven by the uint16_t twin below); the enum NAME->literal set is validated by the independent .h re-parse.
        string[] reps = { "CouldNotEnter", "CannotBeProcessed", "NotPartyLeader", "DiceRoll", "Examine",
                          "CharacterInfoHidden", "CharacterInfoShown", "NoLongerAfflicted", "GainsEffect",
                          "EffectWearsOff", "StyleLockIsOn", "StyleLockIsOff", "AnErrorHasOccured",
                          "LevelSyncActivated", "TreasureHunterProc" };
        foreach (var n in reps)
            Check($"BM compiler emitted ENUM_{n}", "true", (g.ContainsKey("ENUM_" + n) ? "true" : "false"));
        // the re-parse (ground truth from msg_std.h) == compiler underlying-cast for every representative literal
        // (two independent derivations agreeing -- the chunk-21 technique extended to a NEW width at FULL enum scale):
        if (System.IO.File.Exists(enumHeaderPath))
        {
            var re = ParseCppEnum(System.IO.File.ReadAllText(enumHeaderPath), "MsgStd");
            // the full enum (~140 literals) parses and every representative matches the compiler's underlying cast:
            Check("BM msg_std.h re-parse non-empty (full enum)", "true", (re.Count > 100 ? "true" : "false"));
            foreach (var (name, val) in re)
            {
                string key = "ENUM_" + name;
                if (g.ContainsKey(key))
                    Check($"BM re-parse {name} == compiler underlying value", g[key], val.ToString());
            }
        }
        else
        {
            Check("BM msg_std.h re-parse target present", "present", "MISSING:" + enumHeaderPath);
        }

        // ---- PD_BATTLE_MESSAGE body layout (default packing, body-relative) ----
        Check("BM BM_SIZE", g["BM_SIZE"], XI.Map.S2cBattleMessage.SizeOf.ToString());
        Check("BM BM_ALIGN", g["BM_ALIGN"], XI.Map.S2cBattleMessage.AlignOf.ToString());
        Check("BM OFF.UniqueNoCas", g["OFF_BM.UniqueNoCas"], XI.Map.S2cBattleMessage.OffUniqueNoCas.ToString());
        Check("BM OFF.UniqueNoTar", g["OFF_BM.UniqueNoTar"], XI.Map.S2cBattleMessage.OffUniqueNoTar.ToString());
        Check("BM OFF.Data", g["OFF_BM.Data"], XI.Map.S2cBattleMessage.OffData.ToString());
        Check("BM OFF.Data2", g["OFF_BM.Data2"], XI.Map.S2cBattleMessage.OffData2.ToString());
        Check("BM OFF.ActIndexCas", g["OFF_BM.ActIndexCas"], XI.Map.S2cBattleMessage.OffActIndexCas.ToString());
        Check("BM OFF.ActIndexTar", g["OFF_BM.ActIndexTar"], XI.Map.S2cBattleMessage.OffActIndexTar.ToString());
        Check("BM OFF.MessageNum", g["OFF_BM.MessageNum"], XI.Map.S2cBattleMessage.OffMessageNum.ToString());
        Check("BM OFF.Type", g["OFF_BM.Type"], XI.Map.S2cBattleMessage.OffType.ToString());
        Check("BM OFF.padding1B", g["OFF_BM.padding1B"], XI.Map.S2cBattleMessage.OffPadding1B.ToString());
        // the u16-backed enum packs tight: MessageNum@20 == ActIndexTar@18+2 (already 2-aligned, no realign).
        Check("BM MessageNum packs right after ActIndexTar+2 (u16 enum, no realign)", "1",
            (XI.Map.S2cBattleMessage.OffMessageNum == XI.Map.S2cBattleMessage.OffActIndexTar + 2 ? 1 : 0).ToString());
        // the trailing bytes follow the enum exactly (Type@22 == MessageNum+2, padding1B@23 == Type+1).
        Check("BM Type == MessageNum+2", "1",
            (XI.Map.S2cBattleMessage.OffType == XI.Map.S2cBattleMessage.OffMessageNum + 2 ? 1 : 0).ToString());
        Check("BM padding1B == Type+1", "1",
            (XI.Map.S2cBattleMessage.OffPadding1B == XI.Map.S2cBattleMessage.OffType + 1 ? 1 : 0).ToString());
        // gapless: padding1B+1 == SizeOf (24, no tail pad; struct align 4 satisfied by 24%4==0).
        Check("BM padding1B+1==SizeOf (no tail pad)", "1",
            (XI.Map.S2cBattleMessage.OffPadding1B + 1 == XI.Map.S2cBattleMessage.SizeOf ? 1 : 0).ToString());
        Check("BM four u32 then two u16 then u16 enum tight (16=4*4)", "1",
            (XI.Map.S2cBattleMessage.OffActIndexCas == 16 ? 1 : 0).ToString());

        // ==== THE engine-enum substitution invariant RE-PROVEN at the NEW uint16 width ====
        // The uint16_t twin of the WHOLE struct is byte-identical to the MsgStd-member struct (size/align/MessageNum offset).
        Check("BM enum-member == uint16_t twin (compiler BMQ_SIZE_EQ)", "1", g["BMQ_SIZE_EQ"]);
        Check("BM enum-member == uint16_t twin (compiler BMQ_ALIGN_EQ)", "1", g["BMQ_ALIGN_EQ"]);
        Check("BM enum-member == uint16_t twin (compiler BMQ_MSGOFF_EQ)", "1", g["BMQ_MSGOFF_EQ"]);
        Check("BM twin size == real struct size (24)", g["BMQ_SIZE"], g["BM_SIZE"]);
        Check("BM twin align == real struct align (4)", g["BMQ_ALIGN"], g["BM_ALIGN"]);
        Check("BM twin MessageNum offset == real (20)", g["OFF_BMQ.MessageNum"], g["OFF_BM.MessageNum"]);
        // the C# model's MessageNum underlying width is the enum's uint16 (2), not 1 or 4 -- the compiler says 2.
        Check("BM MessageNum underlying width is the enum's uint16 (2)", g["ENUM_underlying_size"], "2");

        // whole-struct fill determinism (24 bytes, i*7+3 -- the body is gapless: 24 % 4 == 0, no padding).
        byte[] bm = HexToBytes(g["BM_FILL"]);
        Check("BM BM_FILL_SIZE", g["BM_FILL_SIZE"], bm.Length.ToString());
        var local = new byte[bm.Length];
        for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
        Check("BM deterministic fill", g["BM_FILL"], BitConverter.ToString(local).Replace("-", ""));

        // read-path: accessor == raw read at the compiler's offset (two independent ways).
        Check("BM GetUniqueNoCas==raw@compiler", BitConverter.ToUInt32(bm, (int)ParseNum(g["OFF_BM.UniqueNoCas"])).ToString(), XI.Map.S2cBattleMessage.GetUniqueNoCas(bm).ToString());
        Check("BM GetActIndexTar==raw@compiler", BitConverter.ToUInt16(bm, (int)ParseNum(g["OFF_BM.ActIndexTar"])).ToString(), XI.Map.S2cBattleMessage.GetActIndexTar(bm).ToString());
        // the uint16-backed enum member read as raw u16 at offset 20 (its underlying width, not the enum name).
        Check("BM GetMessageNum==raw@compiler(u16@20)",
            BitConverter.ToUInt16(bm, (int)ParseNum(g["OFF_BM.MessageNum"])).ToString(),
            XI.Map.S2cBattleMessage.GetMessageNum(bm).ToString());
        Check("BM GetType==raw@compiler(u8@22)", bm[(int)ParseNum(g["OFF_BM.Type"])].ToString(), XI.Map.S2cBattleMessage.GetType(bm).ToString());
        Check("BM GetPadding1B==raw@compiler(u8@23)", bm[(int)ParseNum(g["OFF_BM.padding1B"])].ToString(), XI.Map.S2cBattleMessage.GetPadding1B(bm).ToString());

        // runtime: write a MsgStd representative value (GainsEffect=205) through the underlying u16 accessor and
        // confirm it lands at MessageNum@20 WITHOUT disturbing the neighbouring u16 (ActIndexTar@18) or the u8s (22/23).
        var wb = new byte[XI.Map.S2cBattleMessage.SizeOf];
        XI.Map.S2cBattleMessage.SetActIndexTar(wb, 0x1111);
        XI.Map.S2cBattleMessage.SetType(wb, 0x22);
        XI.Map.S2cBattleMessage.SetMessageNum(wb, 205);   // MsgStd::GainsEffect underlying value
        Check("BM SetMessageNum lands at compiler offset 20", "205", BitConverter.ToUInt16(wb, (int)ParseNum(g["OFF_BM.MessageNum"])).ToString());
        Check("BM SetMessageNum preserves ActIndexTar@18 (adjacent u16)", "4369", XI.Map.S2cBattleMessage.GetActIndexTar(wb).ToString());
        Check("BM SetMessageNum preserves Type@22 (adjacent u8)", "34", XI.Map.S2cBattleMessage.GetType(wb).ToString());
        Check("BM GetMessageNum roundtrip", "205", XI.Map.S2cBattleMessage.GetMessageNum(wb).ToString());

        // leading scalar setter landing / round-trips (compiler-offset + read-back).
        var sb = new byte[XI.Map.S2cBattleMessage.SizeOf];
        XI.Map.S2cBattleMessage.SetUniqueNoCas(sb, 0xCAFEBABE);
        XI.Map.S2cBattleMessage.SetUniqueNoTar(sb, 0xDEADBEEF);
        XI.Map.S2cBattleMessage.SetData(sb, 0x12345678);
        XI.Map.S2cBattleMessage.SetData2(sb, 0x0F0F0F0F);
        XI.Map.S2cBattleMessage.SetActIndexCas(sb, 0x0007);
        Check("BM SetUniqueNoCas at compiler offset 0", "3405691582", BitConverter.ToUInt32(sb, (int)ParseNum(g["OFF_BM.UniqueNoCas"])).ToString());
        Check("BM GetUniqueNoCas roundtrip", ((uint)0xCAFEBABE).ToString(), XI.Map.S2cBattleMessage.GetUniqueNoCas(sb).ToString());
        Check("BM GetUniqueNoTar roundtrip", ((uint)0xDEADBEEF).ToString(), XI.Map.S2cBattleMessage.GetUniqueNoTar(sb).ToString());
        Check("BM GetData roundtrip", ((uint)0x12345678).ToString(), XI.Map.S2cBattleMessage.GetData(sb).ToString());
        Check("BM GetData2 roundtrip", ((uint)0x0F0F0F0F).ToString(), XI.Map.S2cBattleMessage.GetData2(sb).ToString());
        Check("BM GetActIndexCas roundtrip", ((ushort)0x0007).ToString(), XI.Map.S2cBattleMessage.GetActIndexCas(sb).ToString());
    }

    static void VerifyMapS2cSchedulor(string dir, string enumHeaderPath)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_schedulor_enum_golden.txt");
        Console.WriteLine("\n== S2c schedulor (0x038) parity: C# XI.Map vs layout oracle + four_cc.h re-parse ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c schedulor enum golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; engine adds the 4-byte header).
        Check("SC SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- FourCC underlying width (uint32, the NEW width) + all 26 compiler-underlying-cast literals ----
        Check("SC enum underlying size 4 (uint32_t)", g["ENUM_underlying_size"], "4");
        // the FIRST engine enum whose literals are HEX and EXCEED int32 range -- cross-check every value the
        // compiler emitted (uint32_t cast) against the C# FourCC member, then against the independent .h re-parse.
        foreach (var (name, val) in new (string key, XI.Map.FourCC member)[]
                 {
                     ("BasicAttack", XI.Map.FourCC.BasicAttack), ("SkillUse", XI.Map.FourCC.SkillUse),
                     ("SkillInterrupt", XI.Map.FourCC.SkillInterrupt), ("ItemUse", XI.Map.FourCC.ItemUse),
                     ("ItemInterrupt", XI.Map.FourCC.ItemInterrupt), ("RangedStart", XI.Map.FourCC.RangedStart),
                     ("RangedInterrupt", XI.Map.FourCC.RangedInterrupt), ("RangedFinish", XI.Map.FourCC.RangedFinish),
                     ("WhiteMagicCast", XI.Map.FourCC.WhiteMagicCast), ("BlackMagicCast", XI.Map.FourCC.BlackMagicCast),
                     ("BlueMagicCast", XI.Map.FourCC.BlueMagicCast), ("SongMagicCast", XI.Map.FourCC.SongMagicCast),
                     ("NinjutsuMagicCast", XI.Map.FourCC.NinjutsuMagicCast), ("SummonMagicCast", XI.Map.FourCC.SummonMagicCast),
                     ("GeomancyMagicCast", XI.Map.FourCC.GeomancyMagicCast), ("TrustMagicCast", XI.Map.FourCC.TrustMagicCast),
                     ("WhiteMagicInterrupt", XI.Map.FourCC.WhiteMagicInterrupt), ("BlackMagicInterrupt", XI.Map.FourCC.BlackMagicInterrupt),
                     ("BlueMagicInterrupt", XI.Map.FourCC.BlueMagicInterrupt), ("SongMagicInterrupt", XI.Map.FourCC.SongMagicInterrupt),
                     ("NinjutsuMagicInterrupt", XI.Map.FourCC.NinjutsuMagicInterrupt), ("SummonMagicInterrupt", XI.Map.FourCC.SummonMagicInterrupt),
                     ("GeomancyMagicInterrupt", XI.Map.FourCC.GeomancyMagicInterrupt), ("TrustMagicInterrupt", XI.Map.FourCC.TrustMagicInterrupt),
                     ("FadeOut", XI.Map.FourCC.FadeOut), ("Sweating", XI.Map.FourCC.Sweating),
                 })
        {
            Check($"SC ENUM {name} (C# == compiler underlying-cast)", g[$"ENUM_{name}"], ((uint)val).ToString());
        }
        // (These FourCC codes are byte-swapped ASCII tags; the compiler prints (uint32_t) but every value here is
        // < int32.MaxValue, so there is no signed/unsigned boundary effect -- the ENUM_* cross-checks above pin
        // each exact decimal, which is the load-bearing fact, not a range claim.)
        // the independent .h re-parse (ground truth from four_cc.h) == compiler underlying-cast for every literal.
        if (System.IO.File.Exists(enumHeaderPath))
        {
            var re = ParseCppEnum(System.IO.File.ReadAllText(enumHeaderPath), "FourCC");
            Check("SC four_cc.h re-parse count == 26", "26", re.Count.ToString());
            foreach (var (name, val) in re)
            {
                string key = "ENUM_" + name;
                if (g.ContainsKey(key))
                    Check($"SC re-parse {name} == compiler underlying value", g[key], val.ToString());
            }
        }
        else
        {
            Check("SC four_cc.h re-parse target present", "present", "MISSING:" + enumHeaderPath);
        }
        // and the C# FourCC enum matches the independent .h re-parse (the chunk-2 packet-id technique).
        VerifyPacketIds("FourCC", enumHeaderPath, typeof(XI.Map.FourCC));

        // ---- PD_SCHEDULOR body layout (default packing, body-relative) ----
        Check("SC SC_SIZE", g["SC_SIZE"], XI.Map.S2cSchedulor.SizeOf.ToString());
        Check("SC SC_ALIGN", g["SC_ALIGN"], XI.Map.S2cSchedulor.AlignOf.ToString());
        Check("SC OFF.UniqueNoCas", g["OFF_SC.UniqueNoCas"], XI.Map.S2cSchedulor.OffUniqueNoCas.ToString());
        Check("SC OFF.UniqueNoTar", g["OFF_SC.UniqueNoTar"], XI.Map.S2cSchedulor.OffUniqueNoTar.ToString());
        Check("SC OFF.id", g["OFF_SC.id"], XI.Map.S2cSchedulor.OffId.ToString());
        Check("SC OFF.ActIndexCast", g["OFF_SC.ActIndexCast"], XI.Map.S2cSchedulor.OffActIndexCast.ToString());
        Check("SC OFF.ActIndexTar", g["OFF_SC.ActIndexTar"], XI.Map.S2cSchedulor.OffActIndexTar.ToString());
        // the u32-backed enum packs tight: id@8 == UniqueNoTar@4+4 (already 4-aligned, no realign).
        Check("SC id packs right after UniqueNoTar+4 (u32 enum, no realign)", "1",
            (XI.Map.S2cSchedulor.OffId == XI.Map.S2cSchedulor.OffUniqueNoTar + 4 ? 1 : 0).ToString());
        // the two u16s follow the enum exactly (ActIndexCast@12 == id+4, ActIndexTar@14 == ActIndexCast+2).
        Check("SC ActIndexCast == id+4", "1",
            (XI.Map.S2cSchedulor.OffActIndexCast == XI.Map.S2cSchedulor.OffId + 4 ? 1 : 0).ToString());
        Check("SC ActIndexTar == ActIndexCast+2", "1",
            (XI.Map.S2cSchedulor.OffActIndexTar == XI.Map.S2cSchedulor.OffActIndexCast + 2 ? 1 : 0).ToString());
        // gapless: ActIndexTar+2 == SizeOf (16, no tail pad; struct align 4 from the u32s/u32-enum, 16%4==0).
        Check("SC ActIndexTar+2==SizeOf (no tail pad)", "1",
            (XI.Map.S2cSchedulor.OffActIndexTar + 2 == XI.Map.S2cSchedulor.SizeOf ? 1 : 0).ToString());
        // the u32-backed enum id IS the 4-aligner (struct align 4 even without a separate u32 -- but here u32s too).
        Check("SC struct align == enum underlying align (4)",
            g["SC_ALIGN"], g["ENUM_underlying_size"]);

        // ==== THE engine-enum substitution invariant COMPLETED at the uint32 width ====
        // The uint32_t twin of the WHOLE struct is byte-identical to the FourCC-member struct (size/align/id-offset).
        Check("SC enum-member == uint32_t twin (compiler SCQ_SIZE_EQ)", "1", g["SCQ_SIZE_EQ"]);
        Check("SC enum-member == uint32_t twin (compiler SCQ_ALIGN_EQ)", "1", g["SCQ_ALIGN_EQ"]);
        Check("SC enum-member == uint32_t twin (compiler SCQ_IDOFF_EQ)", "1", g["SCQ_IDOFF_EQ"]);
        Check("SC twin size == real struct size (16)", g["SCQ_SIZE"], g["SC_SIZE"]);
        Check("SC twin align == real struct align (4)", g["SCQ_ALIGN"], g["SC_ALIGN"]);
        Check("SC twin id offset == real (8)", g["OFF_SCQ.id"], g["OFF_SC.id"]);
        // the id underlying width is the enum's uint32 (4), not 1/2 -- the compiler says 4.
        Check("SC id underlying width is the enum's uint32 (4)", g["ENUM_underlying_size"], "4");

        // whole-struct fill determinism (16 bytes, i*7+3 -- gapless body: 16 % 4 == 0, no padding).
        byte[] sc = HexToBytes(g["SC_FILL"]);
        Check("SC SC_FILL_SIZE", g["SC_FILL_SIZE"], sc.Length.ToString());
        var local = new byte[sc.Length];
        for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
        Check("SC deterministic fill", g["SC_FILL"], BitConverter.ToString(local).Replace("-", ""));

        // read-path: accessor == raw read at the compiler's offset (two independent ways).
        Check("SC GetUniqueNoCas==raw@compiler", BitConverter.ToUInt32(sc, (int)ParseNum(g["OFF_SC.UniqueNoCas"])).ToString(), XI.Map.S2cSchedulor.GetUniqueNoCas(sc).ToString());
        Check("SC GetUniqueNoTar==raw@compiler", BitConverter.ToUInt32(sc, (int)ParseNum(g["OFF_SC.UniqueNoTar"])).ToString(), XI.Map.S2cSchedulor.GetUniqueNoTar(sc).ToString());
        // the uint32-backed enum member read as raw u32 at offset 8 (its underlying width).
        Check("SC GetIdRaw==raw@compiler(u32@8)",
            BitConverter.ToUInt32(sc, (int)ParseNum(g["OFF_SC.id"])).ToString(),
            XI.Map.S2cSchedulor.GetIdRaw(sc).ToString());
        Check("SC GetActIndexCast==raw@compiler(u16@12)", BitConverter.ToUInt16(sc, (int)ParseNum(g["OFF_SC.ActIndexCast"])).ToString(), XI.Map.S2cSchedulor.GetActIndexCast(sc).ToString());
        Check("SC GetActIndexTar==raw@compiler(u16@14)", BitConverter.ToUInt16(sc, (int)ParseNum(g["OFF_SC.ActIndexTar"])).ToString(), XI.Map.S2cSchedulor.GetActIndexTar(sc).ToString());

        // runtime: write a FourCC representative (BasicAttack, a >int32 hex value) through the underlying u32
        // accessor and confirm it lands at id@8 WITHOUT disturbing the neighbouring u16s (ActIndexCast@12/Tar@14).
        var wb = new byte[XI.Map.S2cSchedulor.SizeOf];
        XI.Map.S2cSchedulor.SetActIndexCast(wb, 0x1111);
        XI.Map.S2cSchedulor.SetActIndexTar(wb, 0x2222);
        XI.Map.S2cSchedulor.SetId(wb, XI.Map.FourCC.BasicAttack);
        Check("SC SetId lands at compiler offset 8 (u32)", g["ENUM_BasicAttack"], BitConverter.ToUInt32(wb, (int)ParseNum(g["OFF_SC.id"])).ToString());
        Check("SC SetId preserves ActIndexCast@12 (adjacent u16)", ((ushort)0x1111).ToString(), XI.Map.S2cSchedulor.GetActIndexCast(wb).ToString());
        Check("SC SetId preserves ActIndexTar@14 (adjacent u16)", ((ushort)0x2222).ToString(), XI.Map.S2cSchedulor.GetActIndexTar(wb).ToString());
        Check("SC GetId roundtrip", "BasicAttack", XI.Map.S2cSchedulor.GetId(wb).ToString());
        // a second enum value (a >int32 hex tag) round-trips through the underlying width.
        XI.Map.S2cSchedulor.SetId(wb, XI.Map.FourCC.FadeOut);
        Check("SC SetId(FadeOut) roundtrip == compiler underlying value", g["ENUM_FadeOut"], XI.Map.S2cSchedulor.GetIdRaw(wb).ToString());

        // leading scalar setter landing / round-trips (compiler-offset + read-back).
        var sb = new byte[XI.Map.S2cSchedulor.SizeOf];
        XI.Map.S2cSchedulor.SetUniqueNoCas(sb, 0xCAFEBABE);
        XI.Map.S2cSchedulor.SetUniqueNoTar(sb, 0xDEADBEEF);
        XI.Map.S2cSchedulor.SetActIndexTar(sb, 0x002A);
        Check("SC SetUniqueNoCas at compiler offset 0", ((uint)0xCAFEBABE).ToString(), BitConverter.ToUInt32(sb, (int)ParseNum(g["OFF_SC.UniqueNoCas"])).ToString());
        Check("SC GetUniqueNoCas roundtrip", ((uint)0xCAFEBABE).ToString(), XI.Map.S2cSchedulor.GetUniqueNoCas(sb).ToString());
        Check("SC GetUniqueNoTar roundtrip", ((uint)0xDEADBEEF).ToString(), XI.Map.S2cSchedulor.GetUniqueNoTar(sb).ToString());
        Check("SC SetActIndexTar at compiler offset 14", ((ushort)0x002A).ToString(), BitConverter.ToUInt16(sb, (int)ParseNum(g["OFF_SC.ActIndexTar"])).ToString());
        Check("SC GetActIndexTar roundtrip", ((ushort)0x002A).ToString(), XI.Map.S2cSchedulor.GetActIndexTar(sb).ToString());
    }

    // Phase-3 chunk 26: 0x071 GP_SERV_COMMAND_INFLUENCE::CAMPAIGN::PacketData -- the layout-DENSEST offline body.
    // Ties EVERY C# bit position/width to the compiler sentinel-scan (bitpos = off*8 + shift, made word-relative for
    // the campaignzone_t run2 whose compiler off is ELEMENT-relative), proves the run-reset (run2's MaxFortifications
    // sits at element byte 8 == Run2WordOffset, NOT a continuation of bytes 0-3), re-confirms the engine-enum uint8
    // substitution invariant on the 200-byte twin, cross-checks the InfluenceMode literals three ways (compiler
    // underlying-cast == 0x071_influence.h re-parse == C# enum), and validates straddling-field read/write against a
    // raw uint32 read taken at the compiler offset (per-byte masks would be WRONG here).
    static void VerifyMapS2cInfluenceCampaign(string dir, string enumHeaderPath)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_influence_campaign_enum_golden.txt");
        Console.WriteLine("\n== S2c influence_campaign (0x071) parity: C# XI.Map vs layout oracle + 0x071_influence.h re-parse ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c influence_campaign enum golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // compiler-derived bit position of a bit-field, made WORD-relative: (off*8 + shift) - wordBaseOff*8.
        // wordBaseOff is the element-relative byte where the containing uint32 unit starts (0 for run1/CCA/Nation,
        // 8 for campaignzone_t run2 whose sentinel off is reported element-relative).
        long BfBit(string structName, string field, int wordBaseOff)
            => (long)(ParseNum(g[$"BF_{structName}_{field}_off"]) * 8 + ParseNum(g[$"BF_{structName}_{field}_shift"]) - wordBaseOff * 8);
        long BfWidth(string structName, string field) => ParseNum(g[$"BF_{structName}_{field}_width"]);

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; the engine adds the 4-byte header).
        Check("IC SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- engine enum GP_SERV_COMMAND_INFLUENCE_MODE: underlying width uint8 + the two literals ----
        Check("IC enum underlying size 1 (uint8_t)", g["ENUM_underlying_size"], "1");
        Check("IC ENUM Campaign (C# == compiler underlying-cast)", g["ENUM_Campaign"], ((byte)XI.Map.InfluenceMode.Campaign).ToString());
        Check("IC ENUM Colonization (C# == compiler underlying-cast)", g["ENUM_Colonization"], ((byte)XI.Map.InfluenceMode.Colonization).ToString());
        if (System.IO.File.Exists(enumHeaderPath))
        {
            var re = ParseCppEnum(System.IO.File.ReadAllText(enumHeaderPath), "GP_SERV_COMMAND_INFLUENCE_MODE");
            Check("IC 0x071_influence.h re-parse count == 2", "2", re.Count.ToString());
            foreach (var (name, val) in re)
            {
                string key = "ENUM_" + name;
                if (g.ContainsKey(key))
                    Check($"IC re-parse {name} == compiler underlying value", g[key], val.ToString());
            }
        }
        else
        {
            Check("IC 0x071_influence.h re-parse target present", "present", "MISSING:" + enumHeaderPath);
        }
        VerifyPacketIds("GP_SERV_COMMAND_INFLUENCE_MODE", enumHeaderPath, typeof(XI.Map.InfluenceMode));

        // ---- sub-struct sizes / aligns / fixed offsets (compiler == C# descriptors) ----
        Check("IC CCA_SIZE", g["CCA_SIZE"], XI.Map.ControlledAreasEntry.SizeOf.ToString());
        Check("IC CCA_ALIGN", g["CCA_ALIGN"], XI.Map.ControlledAreasEntry.AlignOf.ToString());
        Check("IC CN_SIZE", g["CN_SIZE"], XI.Map.NationEntry.SizeOf.ToString());
        Check("IC CN_ALIGN", g["CN_ALIGN"], XI.Map.NationEntry.AlignOf.ToString());
        Check("IC CZ_SIZE", g["CZ_SIZE"], XI.Map.ZoneEntry.SizeOf.ToString());
        Check("IC CZ_ALIGN", g["CZ_ALIGN"], XI.Map.ZoneEntry.AlignOf.ToString());
        // campaignnations_t = 7 campaignnation_t (stride 4) -> 28/4.
        Check("IC CNS_SIZE", g["CNS_SIZE"], (XI.Map.S2cInfluenceCampaign.NationsCount * XI.Map.NationEntry.SizeOf).ToString());
        Check("IC OFF_CNS.Sandoria", g["OFF_CNS.Sandoria"], "0");
        Check("IC OFF_CNS.Bastok == nation[1]*4 (CNS-relative)", g["OFF_CNS.Bastok"], (XI.Map.S2cInfluenceCampaign.NationWord(1) - XI.Map.S2cInfluenceCampaign.OffNations).ToString());
        Check("IC OFF_CNS.DarkKindred == nation[6]*4 (CNS-relative)", g["OFF_CNS.DarkKindred"], (XI.Map.S2cInfluenceCampaign.NationWord(6) - XI.Map.S2cInfluenceCampaign.OffNations).ToString());
        // the four plain u8 influence bytes live at element offsets 4 and 7.
        Check("IC OFF_CZ.InfluenceSandoria", g["OFF_CZ.InfluenceSandoria"], XI.Map.ZoneEntry.OffInfluenceSandoria.ToString());
        Check("IC OFF_CZ.InfluenceBeastman", g["OFF_CZ.InfluenceBeastman"], XI.Map.ZoneEntry.OffInfluenceBeastman.ToString());

        // ---- ALL 17 bit-fields: width AND compiler-derived word-relative bit position == C# constant ----
        const string CCA = "campaigncontrolledareas_t", CN = "campaignnation_t", CZ = "campaignzone_t";
        var caFields = new (string field, int bit, int width)[]
        {
            ("Sandoria", XI.Map.ControlledAreasEntry.SandoriaBit, XI.Map.ControlledAreasEntry.SandoriaWidth),
            ("Bastok", XI.Map.ControlledAreasEntry.BastokBit, XI.Map.ControlledAreasEntry.BastokWidth),
            ("Windurst", XI.Map.ControlledAreasEntry.WindurstBit, XI.Map.ControlledAreasEntry.WindurstWidth),
            ("Beastman", XI.Map.ControlledAreasEntry.BeastmanBit, XI.Map.ControlledAreasEntry.BeastmanWidth),
            ("unused", XI.Map.ControlledAreasEntry.UnusedBit, XI.Map.ControlledAreasEntry.UnusedWidth),
        };
        foreach (var (field, bit, width) in caFields)
        {
            Check($"IC CCA {field} width (== compiler)", BfWidth(CCA, field).ToString(), width.ToString());
            Check($"IC CCA {field} bit == compiler off*8+shift", BfBit(CCA, field, 0).ToString(), bit.ToString());
        }
        var cnFields = new (string field, int bit, int width)[]
        {
            ("Reconnaissance", XI.Map.NationEntry.ReconnaissanceBit, XI.Map.NationEntry.ReconnaissanceWidth),
            ("unused", XI.Map.NationEntry.UnusedBit, XI.Map.NationEntry.UnusedWidth),
            ("Morale", XI.Map.NationEntry.MoraleBit, XI.Map.NationEntry.MoraleWidth),
            ("Prosperity", XI.Map.NationEntry.ProsperityBit, XI.Map.NationEntry.ProsperityWidth),
        };
        foreach (var (field, bit, width) in cnFields)
        {
            Check($"IC CN {field} width (== compiler)", BfWidth(CN, field).ToString(), width.ToString());
            Check($"IC CN {field} bit == compiler off*8+shift", BfBit(CN, field, 0).ToString(), bit.ToString());
        }
        var czRun1 = new (string field, int bit, int width)[]
        {
            ("unused00", XI.Map.ZoneEntry.Unused00Bit, XI.Map.ZoneEntry.Unused00Width),
            ("Owner", XI.Map.ZoneEntry.OwnerBit, XI.Map.ZoneEntry.OwnerWidth),
            ("CurrentFortifications", XI.Map.ZoneEntry.CurrentFortificationsBit, XI.Map.ZoneEntry.CurrentFortificationsWidth),
            ("CurrentResources", XI.Map.ZoneEntry.CurrentResourcesBit, XI.Map.ZoneEntry.CurrentResourcesWidth),
            ("Heroism", XI.Map.ZoneEntry.HeroismBit, XI.Map.ZoneEntry.HeroismWidth),
        };
        foreach (var (field, bit, width) in czRun1)
        {
            Check($"IC CZ-r1 {field} width (== compiler)", BfWidth(CZ, field).ToString(), width.ToString());
            Check($"IC CZ-r1 {field} bit == compiler off*8+shift", BfBit(CZ, field, 0).ToString(), bit.ToString());
        }
        // run2: the sentinel off is ELEMENT-relative, so subtract the run2 word base (8) to get the word-relative bit.
        var czRun2 = new (string field, int bit, int width)[]
        {
            ("MaxFortifications", XI.Map.ZoneEntry.MaxFortificationsBit, XI.Map.ZoneEntry.MaxFortificationsWidth),
            ("MaxResources", XI.Map.ZoneEntry.MaxResourcesBit, XI.Map.ZoneEntry.MaxResourcesWidth),
            ("unused01", XI.Map.ZoneEntry.Unused01Bit, XI.Map.ZoneEntry.Unused01Width),
        };
        foreach (var (field, bit, width) in czRun2)
        {
            Check($"IC CZ-r2 {field} width (== compiler)", BfWidth(CZ, field).ToString(), width.ToString());
            Check($"IC CZ-r2 {field} bit == compiler (off-8)*8+shift", BfBit(CZ, field, XI.Map.ZoneEntry.Run2WordOffset).ToString(), bit.ToString());
        }
        // the two straddling run1 fields (width>1 byte) and run2 fields must start in a byte >0 boundary-crossing.
        Check("IC CZ CurrentFortifications is STRADDLING (compiler off=0 shift=4, width=10 -> crosses byte 0/1)", "1",
            (ParseNum(g[$"BF_{CZ}_CurrentFortifications_off"]) * 8 + ParseNum(g[$"BF_{CZ}_CurrentFortifications_shift"]) + XI.Map.ZoneEntry.CurrentFortificationsWidth > 8 ? 1 : 0).ToString());

        // ---- outer PacketData layout (default packing, body-relative) ----
        Check("IC PD_SIZE", g["PD_SIZE"], XI.Map.S2cInfluenceCampaign.SizeOf.ToString());
        Check("IC PD_ALIGN", g["PD_ALIGN"], XI.Map.S2cInfluenceCampaign.AlignOf.ToString());
        Check("IC OFF.Mode", g["OFF_PD.Mode"], XI.Map.S2cInfluenceCampaign.OffMode.ToString());
        Check("IC OFF.padding05", g["OFF_PD.padding05"], XI.Map.S2cInfluenceCampaign.OffPadding05.ToString());
        Check("IC OFF.Length", g["OFF_PD.Length"], XI.Map.S2cInfluenceCampaign.OffLength.ToString());
        Check("IC OFF.padding07", g["OFF_PD.padding07"], XI.Map.S2cInfluenceCampaign.OffPadding07.ToString());
        Check("IC OFF.ZoneOffset", g["OFF_PD.ZoneOffset"], XI.Map.S2cInfluenceCampaign.OffZoneOffset.ToString());
        Check("IC OFF.padding0A", g["OFF_PD.padding0A"], XI.Map.S2cInfluenceCampaign.OffPadding0A.ToString());
        Check("IC PD_padding0A_len", g["PD_padding0A_len"], XI.Map.S2cInfluenceCampaign.Padding0ALen.ToString());
        Check("IC OFF.AlliedNotes", g["OFF_PD.AlliedNotes"], XI.Map.S2cInfluenceCampaign.OffAlliedNotes.ToString());
        Check("IC OFF.ControlledAreas", g["OFF_PD.ControlledAreas"], XI.Map.S2cInfluenceCampaign.OffControlledAreas.ToString());
        Check("IC OFF.Nations", g["OFF_PD.Nations"], XI.Map.S2cInfluenceCampaign.OffNations.ToString());
        Check("IC OFF.Zones", g["OFF_PD.Zones"], XI.Map.S2cInfluenceCampaign.OffZones.ToString());
        Check("IC PD_Zones_len", g["PD_Zones_len"], (XI.Map.S2cInfluenceCampaign.ZonesCount * XI.Map.ZoneEntry.SizeOf).ToString());
        Check("IC PD_Zones_count", g["PD_Zones_count"], XI.Map.S2cInfluenceCampaign.ZonesCount.ToString());
        // gapless / no-tail-pad: Zones@44 + 13*12 == 200 == SizeOf.
        Check("IC Zones last byte == SizeOf (no tail pad)", g["OFF_PD.Zones12_last"], XI.Map.S2cInfluenceCampaign.SizeOf.ToString());
        Check("IC OffZones + count*stride == SizeOf", "1",
            (XI.Map.S2cInfluenceCampaign.OffZones + XI.Map.S2cInfluenceCampaign.ZonesCount * XI.Map.ZoneEntry.SizeOf == XI.Map.S2cInfluenceCampaign.SizeOf ? 1 : 0).ToString());
        // nested-inclusive offsets the accessors use.
        Check("IC CA word == OffControlledAreas", g["OFF_PD.CA_Sandoria_bitbyte"], XI.Map.S2cInfluenceCampaign.CAWord().ToString());
        Check("IC nation Bastok word", g["OFF_PD.Nations_Bastok"], XI.Map.S2cInfluenceCampaign.NationWord(1).ToString());
        Check("IC zone[1] run1 word", g["OFF_PD.Zones1"], XI.Map.S2cInfluenceCampaign.ZoneRun1Word(1).ToString());

        // ==== THE run-reset proof (the genuinely-new hazard) ====
        // MaxFortifications (run2's first bit) is at ELEMENT byte 8 == Run2WordOffset -- i.e. a FRESH uint32 unit that
        // starts AFTER the four u8 influence bytes, NOT continuing run1's bytes 0-3. The compiler emitted its bit byte
        // (OFF_PD.Zones0_MaxFort_bitbyte) = Zones@44 + 8 = 52.
        Check("IC RUN-RESET: MaxFortifications sentinel byte (element-rel off==Run2WordOffset)", XI.Map.ZoneEntry.Run2WordOffset.ToString(),
            ParseNum(g[$"BF_{CZ}_MaxFortifications_off"]).ToString());
        Check("IC RUN-RESET: Zones0 MaxFort bitbyte == OffZones + Run2WordOffset", g["OFF_PD.Zones0_MaxFort_bitbyte"],
            XI.Map.S2cInfluenceCampaign.ZoneRun2Word(0).ToString());
        Check("IC RUN-RESET: run2 word is 8 bytes past run1 word (4 run1 + 4 u8)", "1",
            (XI.Map.S2cInfluenceCampaign.ZoneRun2Word(0) - XI.Map.S2cInfluenceCampaign.ZoneRun1Word(0) == 8 ? 1 : 0).ToString());
        Check("IC RUN-RESET: run1 (1+3+10+10+8) == 32 bits exactly", "32",
            (XI.Map.ZoneEntry.Unused00Width + XI.Map.ZoneEntry.OwnerWidth + XI.Map.ZoneEntry.CurrentFortificationsWidth + XI.Map.ZoneEntry.CurrentResourcesWidth + XI.Map.ZoneEntry.HeroismWidth).ToString());
        Check("IC RUN-RESET: run2 (10+10+12) == 32 bits exactly", "32",
            (XI.Map.ZoneEntry.MaxFortificationsWidth + XI.Map.ZoneEntry.MaxResourcesWidth + XI.Map.ZoneEntry.Unused01Width).ToString());

        // ---- engine-enum uint8 substitution invariant on the WHOLE 200-byte body ----
        Check("IC enum-member Mode == uint8_t twin (compiler EQ_SIZE)", "1", g["EQ_SIZE"]);
        Check("IC enum-member Mode == uint8_t twin (compiler EQ_ALIGN)", "1", g["EQ_ALIGN"]);
        Check("IC enum-member Mode == uint8_t twin (compiler EQ_MODE offset)", "1", g["EQ_MODE"]);
        Check("IC twin size == real size", g["U8_SIZE"], g["PD_SIZE"]);
        Check("IC twin align == real align", g["U8_ALIGN"], g["PD_ALIGN"]);
        Check("IC twin Mode offset == real", g["OFF_U8.Mode"], g["OFF_PD.Mode"]);

        // ---- whole-struct fill determinism (i*7+3), for the array element AND the 200-byte body ----
        byte[] czFill = HexToBytes(g["CZ_FILL"]);
        Check("IC CZ_FILL_SIZE", g["CZ_FILL_SIZE"], czFill.Length.ToString());
        {
            var local = new byte[czFill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check("IC CZ deterministic fill (12B gapless i*7+3)", g["CZ_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }
        byte[] pdFill = HexToBytes(g["PD_FILL"]);
        Check("IC PD_FILL_SIZE", g["PD_FILL_SIZE"], pdFill.Length.ToString());
        {
            var local = new byte[pdFill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check("IC PD deterministic fill (200B gapless i*7+3)", g["PD_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }

        // ==== straddling-field read: accessor == raw uint32 read at the compiler offset (NOT a per-byte mask) ====
        // Embed the standalone 12-byte campaignzone_t fill at zone[0] of a 200-byte body, then compare each accessor
        // to a raw uint32 extraction taken at the compiler's derived bit position. run1 word @ element byte 0.
        var zbody = new byte[XI.Map.S2cInfluenceCampaign.SizeOf];
        Array.Copy(czFill, 0, zbody, XI.Map.S2cInfluenceCampaign.OffZones, XI.Map.ZoneEntry.SizeOf);
        uint czWord = BitConverter.ToUInt32(zbody, XI.Map.S2cInfluenceCampaign.ZoneRun1Word(0));
        Check("IC CZ[r1] CurrentResources==raw@compiler(bit (off*8+shift) in u32)",
            ((czWord >> (int)BfBit(CZ, "CurrentResources", 0)) & ((1u << XI.Map.ZoneEntry.CurrentResourcesWidth) - 1u)).ToString(),
            XI.Map.S2cInfluenceCampaign.GetZoneCurrentResources(zbody, 0).ToString());
        uint czWord2 = BitConverter.ToUInt32(zbody, XI.Map.S2cInfluenceCampaign.ZoneRun2Word(0));
        Check("IC CZ[r2] MaxFortifications==raw@compiler (FRESH u32 at byte 8)",
            ((czWord2 >> (int)BfBit(CZ, "MaxFortifications", XI.Map.ZoneEntry.Run2WordOffset)) & ((1u << XI.Map.ZoneEntry.MaxFortificationsWidth) - 1u)).ToString(),
            XI.Map.S2cInfluenceCampaign.GetZoneMaxFortifications(zbody, 0).ToString());
        // on the outer body, zone[0]'s run1/run2 words sit at the compiler's nested-inclusive offsets.
        uint z0r1 = BitConverter.ToUInt32(pdFill, XI.Map.S2cInfluenceCampaign.ZoneRun1Word(0));
        Check("IC PD zone[0] CurrentFortifications==raw@compiler (u32 at run1 word)",
            ((z0r1 >> (int)BfBit(CZ, "CurrentFortifications", 0)) & ((1u << XI.Map.ZoneEntry.CurrentFortificationsWidth) - 1u)).ToString(),
            XI.Map.S2cInfluenceCampaign.GetZoneCurrentFortifications(pdFill, 0).ToString());
        uint z0r2 = BitConverter.ToUInt32(pdFill, XI.Map.S2cInfluenceCampaign.ZoneRun2Word(0));
        Check("IC PD zone[0] MaxResources==raw@compiler (u32 at run2 word)",
            ((z0r2 >> (int)BfBit(CZ, "MaxResources", XI.Map.ZoneEntry.Run2WordOffset)) & ((1u << XI.Map.ZoneEntry.MaxResourcesWidth) - 1u)).ToString(),
            XI.Map.S2cInfluenceCampaign.GetZoneMaxResources(pdFill, 0).ToString());
        // the four influence bytes read as plain u8 at their compiler element offsets, unaffected by run2.
        Check("IC PD zone[0] InfluenceSandoria==raw byte@4", pdFill[XI.Map.S2cInfluenceCampaign.ZoneByte(0, XI.Map.ZoneEntry.OffInfluenceSandoria)].ToString(),
            XI.Map.S2cInfluenceCampaign.GetZoneInfluenceSandoria(pdFill, 0).ToString());
        // ControlledAreas Sandoria/Bastok read against the raw word at OffControlledAreas.
        uint caWord = BitConverter.ToUInt32(pdFill, XI.Map.S2cInfluenceCampaign.CAWord());
        Check("IC PD CA Bastok==raw@compiler (straddles byte 0/1)",
            ((caWord >> (int)BfBit(CCA, "Bastok", 0)) & ((1u << XI.Map.ControlledAreasEntry.BastokWidth) - 1u)).ToString(),
            XI.Map.S2cInfluenceCampaign.GetCABastok(pdFill).ToString());
        // Nation[1] (Bastok) Prosperity (bit 25 width 7, top of the word) against the raw word at NationWord(1).
        uint n1Word = BitConverter.ToUInt32(pdFill, XI.Map.S2cInfluenceCampaign.NationWord(1));
        Check("IC PD nation[1] Prosperity==raw@compiler (bit 25 width 7)",
            ((n1Word >> (int)BfBit(CN, "Prosperity", 0)) & ((1u << XI.Map.NationEntry.ProsperityWidth) - 1u)).ToString(),
            XI.Map.S2cInfluenceCampaign.GetNationProsperity(pdFill, 1).ToString());

        // ==== RMW preserves siblings + sibling-field isolation (write a run1 straddler via zone[0], neighbours survive) ====
        // All writes go through the real S2cInfluenceCampaign zone-index accessors on a full 200-byte body at index 0,
        // so the run-reset word (@ OffZones+8) is exercised exactly as the port uses it.
        int r1w = XI.Map.S2cInfluenceCampaign.ZoneRun1Word(0);
        int r2w = XI.Map.S2cInfluenceCampaign.ZoneRun2Word(0);
        var zb = new byte[XI.Map.S2cInfluenceCampaign.SizeOf];
        XI.Map.S2cInfluenceCampaign.SetZoneCurrentFortifications(zb, 0, 0x3FF);   // widest run1 straddler, all 10 bits
        Check("IC RMW SetCurrentFortifications(0x3FF) reads back", ((uint)0x3FF).ToString(), XI.Map.S2cInfluenceCampaign.GetZoneCurrentFortifications(zb, 0).ToString());
        Check("IC RMW SetCurrentFortifications clears the other run1 bits (raw word == 0x3FF<<bitpos)",
            (((uint)0x3FF << (int)BfBit(CZ, "CurrentFortifications", 0))).ToString(),
            BitConverter.ToUInt32(zb, r1w).ToString());
        XI.Map.S2cInfluenceCampaign.SetZoneOwner(zb, 0, 0x7);                     // now set the 3-bit Owner -- must NOT touch CurrentFort
        Check("IC RMW sibling isolation: SetOwner leaves CurrentFortifications intact", ((uint)0x3FF).ToString(), XI.Map.S2cInfluenceCampaign.GetZoneCurrentFortifications(zb, 0).ToString());
        Check("IC RMW sibling isolation: SetOwner reads back 0x7", ((uint)0x7).ToString(), XI.Map.S2cInfluenceCampaign.GetZoneOwner(zb, 0).ToString());
        // run2 field write must land in the FRESH uint32 at byte 8, leaving run1 (bytes 0-3) and influence bytes (4-7).
        XI.Map.S2cInfluenceCampaign.SetZoneMaxFortifications(zb, 0, 0x123);
        Check("IC run-reset write: MaxFortifications lands in word@8 (raw u32@8 == 0x123)",
            ((uint)0x123).ToString(), BitConverter.ToUInt32(zb, r2w).ToString());
        Check("IC run-reset write: MaxFortifications did NOT disturb run1 word (still Owner=7/CurFort=0x3FF)",
            (((uint)0x7 << (int)BfBit(CZ, "Owner", 0)) | ((uint)0x3FF << (int)BfBit(CZ, "CurrentFortifications", 0))).ToString(),
            BitConverter.ToUInt32(zb, r1w).ToString());

        // outer-body scalar setters land at the compiler offset, no neighbouring disturbance.
        var wb = new byte[XI.Map.S2cInfluenceCampaign.SizeOf];
        XI.Map.S2cInfluenceCampaign.SetMode(wb, XI.Map.InfluenceMode.Colonization);
        Check("IC SetMode(Colonization) lands byte 0 == compiler underlying", g["ENUM_Colonization"], wb[XI.Map.S2cInfluenceCampaign.OffMode].ToString());
        Check("IC SetMode preserves Length@2 (untouched)", ((ushort)0).ToString(), XI.Map.S2cInfluenceCampaign.GetLength(wb).ToString());
        XI.Map.S2cInfluenceCampaign.SetLength(wb, 0xBEEF);
        Check("IC SetLength lands at compiler offset 2 (u16)", ((ushort)0xBEEF).ToString(), BitConverter.ToUInt16(wb, (int)ParseNum(g["OFF_PD.Length"])).ToString());
        Check("IC GetLength roundtrip", ((ushort)0xBEEF).ToString(), XI.Map.S2cInfluenceCampaign.GetLength(wb).ToString());
        XI.Map.S2cInfluenceCampaign.SetAlliedNotes(wb, -123456);
        Check("IC SetAlliedNotes(i32) lands at compiler offset 8", "-123456", BitConverter.ToInt32(wb, (int)ParseNum(g["OFF_PD.AlliedNotes"])).ToString());
        Check("IC GetAlliedNotes roundtrip (signed)", "-123456", XI.Map.S2cInfluenceCampaign.GetAlliedNotes(wb).ToString());
        // zone[5] run1+run2 write via the outer accessors, then read raw at the compiler nested-inclusive offset.
        XI.Map.S2cInfluenceCampaign.SetZoneCurrentResources(wb, 5, 0x2AB);
        XI.Map.S2cInfluenceCampaign.SetZoneMaxResources(wb, 5, 0x155);
        Check("IC outer SetZoneCurrentResources(z5) raw @ run1 word", ((uint)0x2AB).ToString(),
            (BitConverter.ToUInt32(wb, XI.Map.S2cInfluenceCampaign.ZoneRun1Word(5)) >> (int)BfBit(CZ, "CurrentResources", 0) & ((1u << XI.Map.ZoneEntry.CurrentResourcesWidth) - 1u)).ToString());
        Check("IC outer SetZoneMaxResources(z5) raw @ run2 word", ((uint)0x155).ToString(),
            (BitConverter.ToUInt32(wb, XI.Map.S2cInfluenceCampaign.ZoneRun2Word(5)) >> (int)BfBit(CZ, "MaxResources", XI.Map.ZoneEntry.Run2WordOffset) & ((1u << XI.Map.ZoneEntry.MaxResourcesWidth) - 1u)).ToString());
        Check("IC z5 CurrentResources/MaxResources roundtrip through both words",
            XI.Map.S2cInfluenceCampaign.GetZoneCurrentResources(wb, 5).ToString(), ((uint)0x2AB).ToString());
    }

    // Phase-3 chunk 27: 0x071 GP_SERV_COMMAND_INFLUENCE::COLONIZATION::PacketData -- the CAMPAIGN sibling. Reuses the
    // chunk-26 InfluenceMode engine-enum (a uint8_t twin of the WHOLE 200-byte body is byte-identical, EQ all 1) and
    // BitWord straddling path. Ties EVERY C# bit position/width to the compiler sentinel-scan (bitpos = off*8 + shift);
    // proves colonizationzone_t's CurrentBivouacs:3 (bit 7) STRADDLES the byte 0/1 boundary -- reading it as a per-byte
    // mask is provably WRONG (the negative check) -- while coalitionranks_t's six 4-bit fields + 8-bit pad are all
    // byte-aligned (the negative contrast), and validates the trailing fixed uint8 padding00[144] (Zones end==Bayld@52,
    // Bayld+4==padding00@56, 56+144==200 no tail pad).
    static void VerifyMapS2cInfluenceColonization(string dir, string enumHeaderPath)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_influence_colonization_enum_golden.txt");
        Console.WriteLine("\n== S2c influence_colonization (0x071) parity: C# XI.Map vs layout oracle + 0x071_influence.h re-parse ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c influence_colonization enum golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // every bit-field here lives in a uint32 unit at element byte 0 (no run2), so wordBaseOff is always 0.
        long BfBit(string structName, string field)
            => ParseNum(g[$"BF_{structName}_{field}_off"]) * 8 + ParseNum(g[$"BF_{structName}_{field}_shift"]);
        long BfWidth(string structName, string field) => ParseNum(g[$"BF_{structName}_{field}_width"]);

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; the engine adds the 4-byte header).
        Check("CL SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- engine enum GP_SERV_COMMAND_INFLUENCE_MODE (REUSED from chunk 26): underlying width uint8 + literals ----
        Check("CL enum underlying size 1 (uint8_t)", g["ENUM_underlying_size"], "1");
        Check("CL ENUM Campaign (C# == compiler underlying-cast)", g["ENUM_Campaign"], ((byte)XI.Map.InfluenceMode.Campaign).ToString());
        Check("CL ENUM Colonization (C# == compiler underlying-cast)", g["ENUM_Colonization"], ((byte)XI.Map.InfluenceMode.Colonization).ToString());
        if (System.IO.File.Exists(enumHeaderPath))
        {
            var re = ParseCppEnum(System.IO.File.ReadAllText(enumHeaderPath), "GP_SERV_COMMAND_INFLUENCE_MODE");
            Check("CL 0x071_influence.h re-parse count == 2", "2", re.Count.ToString());
            foreach (var (name, val) in re)
            {
                string key = "ENUM_" + name;
                if (g.ContainsKey(key))
                    Check($"CL re-parse {name} == compiler underlying value", g[key], val.ToString());
            }
        }
        else
        {
            Check("CL 0x071_influence.h re-parse target present", "present", "MISSING:" + enumHeaderPath);
        }
        VerifyPacketIds("GP_SERV_COMMAND_INFLUENCE_MODE", enumHeaderPath, typeof(XI.Map.InfluenceMode));

        // ---- sub-struct sizes / aligns / fixed offsets (compiler == C# descriptors) ----
        Check("CL CR_SIZE (coalitionranks_t)", g["CR_SIZE"], XI.Map.CoalitionRanksEntry.SizeOf.ToString());
        Check("CL CR_ALIGN", g["CR_ALIGN"], XI.Map.CoalitionRanksEntry.AlignOf.ToString());
        Check("CL CZ_SIZE (colonizationzone_t)", g["CZ_SIZE"], XI.Map.ColonizationZoneEntry.SizeOf.ToString());
        Check("CL CZ_ALIGN", g["CZ_ALIGN"], XI.Map.ColonizationZoneEntry.AlignOf.ToString());
        // colonizationzones_t = 9 colonizationzone_t (stride 4) -> 36/4.
        Check("CL CZS_SIZE", g["CZS_SIZE"], (XI.Map.S2cInfluenceColonization.ZonesCount * XI.Map.ColonizationZoneEntry.SizeOf).ToString());
        Check("CL OFF_CZS.unused", g["OFF_CZS.unused"], "0");
        Check("CL OFF_CZS.RaKaznar == zone[8]*4 (CZS-relative)", g["OFF_CZS.RaKaznar"],
            (XI.Map.S2cInfluenceColonization.ZoneWord(8) - XI.Map.S2cInfluenceColonization.OffZones).ToString());

        // ---- coalitionranks_t bit-fields: all byte-aligned (no straddle) -- the negative contrast ----
        const string CR = "coalitionranks_t", CZ = "colonizationzone_t";
        var crFields = new (string field, int bit, int width)[]
        {
            ("Pioneers", XI.Map.CoalitionRanksEntry.PioneersBit, XI.Map.CoalitionRanksEntry.PioneersWidth),
            ("Peacekeepers", XI.Map.CoalitionRanksEntry.PeacekeepersBit, XI.Map.CoalitionRanksEntry.PeacekeepersWidth),
            ("Couriers", XI.Map.CoalitionRanksEntry.CouriersBit, XI.Map.CoalitionRanksEntry.CouriersWidth),
            ("Scouts", XI.Map.CoalitionRanksEntry.ScoutsBit, XI.Map.CoalitionRanksEntry.ScoutsWidth),
            ("Inventors", XI.Map.CoalitionRanksEntry.InventorsBit, XI.Map.CoalitionRanksEntry.InventorsWidth),
            ("Mummers", XI.Map.CoalitionRanksEntry.MummersBit, XI.Map.CoalitionRanksEntry.MummersWidth),
            ("unused", XI.Map.CoalitionRanksEntry.UnusedBit, XI.Map.CoalitionRanksEntry.UnusedWidth),
        };
        foreach (var (field, bit, width) in crFields)
        {
            Check($"CL CR {field} width (== compiler)", BfWidth(CR, field).ToString(), width.ToString());
            Check($"CL CR {field} bit == compiler off*8+shift", BfBit(CR, field).ToString(), bit.ToString());
            // byte-aligned: (bit % 8) + width <= 8, so the field sits entirely within one byte (no straddle).
            Check($"CL CR {field} byte-aligned (no straddle)", "1", (((bit % 8) + width) <= 8 ? 1 : 0).ToString());
        }
        // six 4-bit + one 8-bit == 32 bits, one uint32 unit.
        Check("CL CR rank bits sum to 32 (one uint32 unit)", "32",
            (XI.Map.CoalitionRanksEntry.PioneersWidth * 6 + XI.Map.CoalitionRanksEntry.UnusedWidth).ToString());

        // ---- colonizationzone_t bit-fields: widths 7/3/3/19 -- CurrentBivouacs STRADDLES byte 0/1 ----
        var czFields = new (string field, int bit, int width)[]
        {
            ("ColonizationRate", XI.Map.ColonizationZoneEntry.ColonizationRateBit, XI.Map.ColonizationZoneEntry.ColonizationRateWidth),
            ("CurrentBivouacs", XI.Map.ColonizationZoneEntry.CurrentBivouacsBit, XI.Map.ColonizationZoneEntry.CurrentBivouacsWidth),
            ("MaxBivouacs", XI.Map.ColonizationZoneEntry.MaxBivouacsBit, XI.Map.ColonizationZoneEntry.MaxBivouacsWidth),
            ("unused", XI.Map.ColonizationZoneEntry.UnusedBit, XI.Map.ColonizationZoneEntry.UnusedWidth),
        };
        foreach (var (field, bit, width) in czFields)
        {
            Check($"CL CZ {field} width (== compiler)", BfWidth(CZ, field).ToString(), width.ToString());
            Check($"CL CZ {field} bit == compiler off*8+shift", BfBit(CZ, field).ToString(), bit.ToString());
        }
        Check("CL CZ zone bits sum to 32 (one uint32 unit)", "32",
            (XI.Map.ColonizationZoneEntry.ColonizationRateWidth + XI.Map.ColonizationZoneEntry.CurrentBivouacsWidth + XI.Map.ColonizationZoneEntry.MaxBivouacsWidth + XI.Map.ColonizationZoneEntry.UnusedWidth).ToString());
        // THE straddle: CurrentBivouacs bit 7, width 3 -> bits 7,8,9 cross the byte 0/1 boundary (7+3 > 8).
        Check("CL CZ CurrentBivouacs STRADDLES byte 0/1 (bit 7 width 3 -> last bit 9 >= 8)", "1",
            (XI.Map.ColonizationZoneEntry.CurrentBivouacsBit + XI.Map.ColonizationZoneEntry.CurrentBivouacsWidth > 8 ? 1 : 0).ToString());
        // the compiler's sentinel mask for a straddler is ONLY its first-byte portion (0x80 = bit 7 of byte 0), which is
        // the whole reason a per-byte mask would silently drop bits 8/9 -- prove the compiler mask is the low byte only.
        Check("CL CZ CurrentBivouacs compiler mask is byte-0-only 0x80 (a per-byte mask drops bits 8/9)", "128",
            g[$"BF_{CZ}_CurrentBivouacs_mask"]);

        // ---- outer PacketData layout (default packing, body-relative) ----
        Check("CL PD_SIZE", g["PD_SIZE"], XI.Map.S2cInfluenceColonization.SizeOf.ToString());
        Check("CL PD_ALIGN", g["PD_ALIGN"], XI.Map.S2cInfluenceColonization.AlignOf.ToString());
        Check("CL OFF.Mode", g["OFF_PD.Mode"], XI.Map.S2cInfluenceColonization.OffMode.ToString());
        Check("CL OFF.padding05", g["OFF_PD.padding05"], XI.Map.S2cInfluenceColonization.OffPadding05.ToString());
        Check("CL OFF.Length", g["OFF_PD.Length"], XI.Map.S2cInfluenceColonization.OffLength.ToString());
        Check("CL OFF.unknown00", g["OFF_PD.unknown00"], XI.Map.S2cInfluenceColonization.OffUnknown00.ToString());
        Check("CL OFF.unknown01", g["OFF_PD.unknown01"], XI.Map.S2cInfluenceColonization.OffUnknown01.ToString());
        Check("CL OFF.Ranks", g["OFF_PD.Ranks"], XI.Map.S2cInfluenceColonization.OffRanks.ToString());
        Check("CL OFF.Zones", g["OFF_PD.Zones"], XI.Map.S2cInfluenceColonization.OffZones.ToString());
        Check("CL PD_Zones_len", g["PD_Zones_len"], (XI.Map.S2cInfluenceColonization.ZonesCount * XI.Map.ColonizationZoneEntry.SizeOf).ToString());
        Check("CL PD_Zones_count", g["PD_Zones_count"], XI.Map.S2cInfluenceColonization.ZonesCount.ToString());
        Check("CL OFF.Bayld", g["OFF_PD.Bayld"], XI.Map.S2cInfluenceColonization.OffBayld.ToString());
        Check("CL OFF.padding00", g["OFF_PD.padding00"], XI.Map.S2cInfluenceColonization.OffPadding00.ToString());
        Check("CL PD_padding00_len", g["PD_padding00_len"], XI.Map.S2cInfluenceColonization.Padding00Len.ToString());
        // trailing-array gapless chain: Ranks@12, Zones@16, Zones end==Bayld@52, Bayld+4==padding00@56, +144==200.
        Check("CL Ranks word == OffRanks", g["OFF_PD.Ranks_bitbyte"], XI.Map.S2cInfluenceColonization.RanksWord().ToString());
        Check("CL zone[1] word == OffZones+4", g["OFF_PD.Zones1"], XI.Map.S2cInfluenceColonization.ZoneWord(1).ToString());
        Check("CL zone[2] word == OffZones+8", g["OFF_PD.Zones2_ColonizationRate"], XI.Map.S2cInfluenceColonization.ZoneWord(2).ToString());
        Check("CL Zones last byte == Bayld (no gap)", g["OFF_PD.Zones8_last"], XI.Map.S2cInfluenceColonization.OffBayld.ToString());
        Check("CL OffZones + count*4 == Bayld", "1",
            (XI.Map.S2cInfluenceColonization.OffZones + XI.Map.S2cInfluenceColonization.ZonesCount * XI.Map.ColonizationZoneEntry.SizeOf == XI.Map.S2cInfluenceColonization.OffBayld ? 1 : 0).ToString());
        Check("CL Bayld+4 == padding00 (trailing array start)", "1",
            (XI.Map.S2cInfluenceColonization.OffBayld + 4 == XI.Map.S2cInfluenceColonization.OffPadding00 ? 1 : 0).ToString());
        Check("CL padding00 + 144 == SizeOf (no tail pad)", "1",
            (XI.Map.S2cInfluenceColonization.OffPadding00 + XI.Map.S2cInfluenceColonization.Padding00Len == XI.Map.S2cInfluenceColonization.SizeOf ? 1 : 0).ToString());

        // ---- engine-enum uint8 substitution invariant on the WHOLE 200-byte body ----
        Check("CL enum-member Mode == uint8_t twin (compiler EQ_SIZE)", "1", g["EQ_SIZE"]);
        Check("CL enum-member Mode == uint8_t twin (compiler EQ_ALIGN)", "1", g["EQ_ALIGN"]);
        Check("CL enum-member Mode == uint8_t twin (compiler EQ_MODE offset)", "1", g["EQ_MODE"]);
        Check("CL twin size == real size", g["U8_SIZE"], g["PD_SIZE"]);
        Check("CL twin align == real align", g["U8_ALIGN"], g["PD_ALIGN"]);
        Check("CL twin Mode offset == real", g["OFF_U8.Mode"], g["OFF_PD.Mode"]);

        // ---- whole-struct fill determinism (i*7+3): the two 4-byte sub-structs AND the 200-byte body ----
        byte[] czFill = HexToBytes(g["CZ_FILL"]);
        Check("CL CZ_FILL_SIZE", g["CZ_FILL_SIZE"], czFill.Length.ToString());
        {
            var local = new byte[czFill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check("CL CZ deterministic fill (4B gapless i*7+3)", g["CZ_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }
        byte[] crFill = HexToBytes(g["CR_FILL"]);
        Check("CL CR_FILL_SIZE", g["CR_FILL_SIZE"], crFill.Length.ToString());
        {
            var local = new byte[crFill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check("CL CR deterministic fill (4B gapless i*7+3)", g["CR_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }
        byte[] pdFill = HexToBytes(g["PD_FILL"]);
        Check("CL PD_FILL_SIZE", g["PD_FILL_SIZE"], pdFill.Length.ToString());
        {
            var local = new byte[pdFill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check("CL PD deterministic fill (200B gapless i*7+3)", g["PD_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }

        // ==== accessor == raw uint32 read at the compiler offset (two independent ways) ====
        // The straddling CurrentBivouacs@bit7 read from zone[0]'s word (OffZones+0=16) via the FULL uint32.
        uint z0 = BitConverter.ToUInt32(pdFill, XI.Map.S2cInfluenceColonization.ZoneWord(0));
        Check("CL PD zone[0] CurrentBivouacs==raw@compiler (uint32 >>7 &7, NOT per-byte)",
            ((z0 >> (int)BfBit(CZ, "CurrentBivouacs")) & ((1u << XI.Map.ColonizationZoneEntry.CurrentBivouacsWidth) - 1u)).ToString(),
            XI.Map.S2cInfluenceColonization.GetZoneCurrentBivouacs(pdFill, 0).ToString());
        Check("CL PD zone[0] ColonizationRate==raw@compiler (bit 0 width 7)",
            ((z0 >> (int)BfBit(CZ, "ColonizationRate")) & ((1u << XI.Map.ColonizationZoneEntry.ColonizationRateWidth) - 1u)).ToString(),
            XI.Map.S2cInfluenceColonization.GetZoneColonizationRate(pdFill, 0).ToString());
        Check("CL PD zone[0] MaxBivouacs==raw@compiler (bit 10 width 3)",
            ((z0 >> (int)BfBit(CZ, "MaxBivouacs")) & ((1u << XI.Map.ColonizationZoneEntry.MaxBivouacsWidth) - 1u)).ToString(),
            XI.Map.S2cInfluenceColonization.GetZoneMaxBivouacs(pdFill, 0).ToString());
        // coalitionranks_t byte-aligned Pioneers read from the word at OffRanks (12).
        uint rw = BitConverter.ToUInt32(pdFill, XI.Map.S2cInfluenceColonization.RanksWord());
        Check("CL PD Ranks Pioneers==raw@compiler (bit 0 width 4)",
            ((rw >> (int)BfBit(CR, "Pioneers")) & 0xFu).ToString(), XI.Map.S2cInfluenceColonization.GetRankPioneers(pdFill).ToString());
        Check("CL PD Ranks Mummers==raw@compiler (bit 20 width 4)",
            ((rw >> (int)BfBit(CR, "Mummers")) & 0xFu).ToString(), XI.Map.S2cInfluenceColonization.GetRankMummers(pdFill).ToString());

        // ==== THE straddle negative check: a per-byte mask would DROP bits 8/9 and read the WRONG value ====
        {
            var z = new byte[XI.Map.S2cInfluenceColonization.SizeOf];
            XI.Map.S2cInfluenceColonization.SetZoneCurrentBivouacs(z, 0, 0x7);   // all three bits (7,8,9) set
            int wordOff = XI.Map.S2cInfluenceColonization.ZoneWord(0);
            uint correct = XI.Map.S2cInfluenceColonization.GetZoneCurrentBivouacs(z, 0);
            int naivePerByte = (z[wordOff] & 0x80) >> 7;                          // byte-0-only mask captures bit 7 ONLY
            Check("CL straddle: correct uint32 read == 0x7", "7", correct.ToString());
            Check("CL straddle: per-byte mask reads 1 (DROPS bits 8/9) -- NOT equal to correct", "1",
                (naivePerByte != correct ? naivePerByte.ToString() : "EQUAL-BAD"));
        }

        // ==== RMW preserves siblings + cross-zone isolation (straddling writes) ====
        {
            var z = new byte[XI.Map.S2cInfluenceColonization.SizeOf];
            XI.Map.S2cInfluenceColonization.SetZoneColonizationRate(z, 0, 0x55);  // 7-bit, bits 0..6
            XI.Map.S2cInfluenceColonization.SetZoneCurrentBivouacs(z, 0, 0x5);    // 3-bit straddler, bits 7..9
            XI.Map.S2cInfluenceColonization.SetZoneMaxBivouacs(z, 0, 0x3);        // 3-bit, bits 10..12
            Check("CL RMW ColonizationRate preserved after writing straddler+Max", ((uint)0x55).ToString(),
                XI.Map.S2cInfluenceColonization.GetZoneColonizationRate(z, 0).ToString());
            Check("CL RMW CurrentBivouacs preserved (0x5) after MaxBivouacs write", ((uint)0x5).ToString(),
                XI.Map.S2cInfluenceColonization.GetZoneCurrentBivouacs(z, 0).ToString());
            Check("CL RMW MaxBivouacs reads back (0x3)", ((uint)0x3).ToString(),
                XI.Map.S2cInfluenceColonization.GetZoneMaxBivouacs(z, 0).ToString());
            // the three fields together occupy bits 0..12 -- the whole 32-bit word equals their packed bit layout.
            uint expectWord = (0x55u << 0) | (0x5u << 7) | (0x3u << 10);
            Check("CL RMW packed word == compiler bit-positions composed", expectWord.ToString(),
                BitConverter.ToUInt32(z, XI.Map.S2cInfluenceColonization.ZoneWord(0)).ToString());
            // cross-zone isolation: writing zone 3 leaves zone 4's word == 0.
            var zz = new byte[XI.Map.S2cInfluenceColonization.SizeOf];
            XI.Map.S2cInfluenceColonization.SetZoneCurrentBivouacs(zz, 3, 0x7);
            Check("CL cross-zone isolation: zone[3] write leaves zone[4] word 0", "0",
                BitConverter.ToUInt32(zz, XI.Map.S2cInfluenceColonization.ZoneWord(4)).ToString());
            Check("CL cross-zone: zone[3] value survives", "7", XI.Map.S2cInfluenceColonization.GetZoneCurrentBivouacs(zz, 3).ToString());
        }

        // outer-body scalar setters land at the compiler offset, no neighbouring disturbance.
        var wb = new byte[XI.Map.S2cInfluenceColonization.SizeOf];
        XI.Map.S2cInfluenceColonization.SetMode(wb, XI.Map.InfluenceMode.Campaign);
        Check("CL SetMode(Campaign) lands byte 0 == compiler underlying", g["ENUM_Campaign"], wb[XI.Map.S2cInfluenceColonization.OffMode].ToString());
        XI.Map.S2cInfluenceColonization.SetLength(wb, 0xCAFE);
        Check("CL SetLength lands at compiler offset 2 (u16)", ((ushort)0xCAFE).ToString(), BitConverter.ToUInt16(wb, (int)ParseNum(g["OFF_PD.Length"])).ToString());
        XI.Map.S2cInfluenceColonization.SetUnknown00(wb, 0xDEADBEEF);
        Check("CL SetUnknown00 lands at compiler offset 4 (u32)", ((uint)0xDEADBEEF).ToString(), BitConverter.ToUInt32(wb, (int)ParseNum(g["OFF_PD.unknown00"])).ToString());
        Check("CL GetUnknown00 roundtrip", ((uint)0xDEADBEEF).ToString(), XI.Map.S2cInfluenceColonization.GetUnknown00(wb).ToString());
        XI.Map.S2cInfluenceColonization.SetBayld(wb, -987654);
        Check("CL SetBayld(i32) lands at compiler offset 52", "-987654", BitConverter.ToInt32(wb, (int)ParseNum(g["OFF_PD.Bayld"])).ToString());
        Check("CL GetBayld roundtrip (signed)", "-987654", XI.Map.S2cInfluenceColonization.GetBayld(wb).ToString());
        // SetBayld must NOT touch the trailing padding00[144] that starts right after it.
        Check("CL SetBayld leaves padding00[0] untouched", ((byte)0).ToString(), wb[XI.Map.S2cInfluenceColonization.OffPadding00].ToString());

        // the trailing padding00 span is exactly 144 bytes and reaches the struct end (bounds check on Slice).
        Check("CL GetPadding00 length == 144", XI.Map.S2cInfluenceColonization.Padding00Len.ToString(),
            XI.Map.S2cInfluenceColonization.GetPadding00(pdFill).Length.ToString());
        Check("CL GetPadding00[0] == pdFill byte@56 (raw)", pdFill[XI.Map.S2cInfluenceColonization.OffPadding00].ToString(),
            XI.Map.S2cInfluenceColonization.GetPadding00(pdFill)[0].ToString());
    }

    // Phase-3 chunk 28: 0x113 GP_SERV_COMMAND_CURRENCIES_1::PacketData -- FIRST uint64 allocation-unit bit-field run +
    // FIRST pack(1) body whose MID-STRUCT uint64 alignment collapses to 1 (contrast chunk 6 recipe alignof 8 + 4 pad).
    // Ties each C# intra-unit bit position/width to the compiler sentinel-scan, proves the 8-byte unit is read as ONE
    // BitConverter.ToUInt64 (a per-byte mask provably drops the upper bits of a straddling 9-bit field), pins the run's
    // NON-8-aligned start byte (legal only because pack(1)), and asserts the ~110-field gapless scalar chain (the
    // 248-byte i*7+3 fill has NO padding anywhere).
    static void VerifyMapS2cCurrencies1(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_currencies_1_golden.txt");
        Console.WriteLine("\n== S2c currencies_1 (0x113) parity: C# XI.Map vs layout oracle (uint64 bit-field run + pack(1) align collapse) ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c currencies_1 golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; the engine adds the 4-byte header).
        Check("CU SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- the STANDALONE uint64 allocation unit: size/align + intra-unit bit positions (relative to unit byte 0) ----
        Check("CU PLANS_SIZE (one uint64 = 8 bytes)", g["PLANS_SIZE"], XI.Map.PlansUnit.SizeOf.ToString());
        Check("CU PLANS_ALIGN (natural uint64 align 8)", g["PLANS_ALIGN"], XI.Map.PlansUnit.AlignOf.ToString());
        // 5x :9 + 1x :19 == exactly 64 bits (one uint64 allocation unit).
        Check("CU plans widths sum to 64 (5*9+19)", "1", (XI.Map.PlansUnit.SumsTo64() ? 1 : 0).ToString());
        var plansFields = new (string key, int bit, int width)[]
        {
            ("bloodshed_plans_stored", XI.Map.PlansUnit.BloodshedBit, XI.Map.PlansUnit.BloodshedWidth),
            ("umbrage_plans_stored", XI.Map.PlansUnit.UmbrageBit, XI.Map.PlansUnit.UmbrageWidth),
            ("ritualistic_plans_stored", XI.Map.PlansUnit.RitualisticBit, XI.Map.PlansUnit.RitualisticWidth),
            ("tutelary_plans_stored", XI.Map.PlansUnit.TutelaryBit, XI.Map.PlansUnit.TutelaryWidth),
            ("primacy_plans_stored", XI.Map.PlansUnit.PrimacyBit, XI.Map.PlansUnit.PrimacyWidth),
            ("unused", XI.Map.PlansUnit.UnusedBit, XI.Map.PlansUnit.UnusedWidth),
        };
        foreach (var (key, bit, width) in plansFields)
        {
            long coff = ParseNum(g[$"BF_plans_unit_t_{key}_off"]);
            long cshift = ParseNum(g[$"BF_plans_unit_t_{key}_shift"]);
            Check($"CU plans {key} bit == compiler off*8+shift", (coff * 8 + cshift).ToString(), bit.ToString());
            Check($"CU plans {key} width == compiler", g[$"BF_plans_unit_t_{key}_width"], width.ToString());
        }
        // every 9-bit field STRADDLES a byte boundary inside the uint64 except bloodshed@bit0: (bit%8)+width>8.
        Check("CU umbrage (bit 9 width 9) straddles a byte inside the unit", "1",
            (((XI.Map.PlansUnit.UmbrageBit % 8) + XI.Map.PlansUnit.UmbrageWidth > 8) ? 1 : 0).ToString());
        Check("CU primacy (bit 36 width 9) straddles a byte inside the unit", "1",
            (((XI.Map.PlansUnit.PrimacyBit % 8) + XI.Map.PlansUnit.PrimacyWidth > 8) ? 1 : 0).ToString());

        // ---- the OUTER body: pack(1) collapses the WHOLE struct's align to 1 despite the mid-struct uint64 run ----
        Check("CU PD_SIZE", g["PD_SIZE"], XI.Map.S2cCurrencies1.SizeOf.ToString());
        // THE contrast to chunk 6: under DEFAULT packing a mid-struct uint64 forced alignof 8; pack(1) collapses it to 1.
        Check("CU PD_ALIGN == 1 (pack(1) collapses the mid-struct uint64; NOT 8)", "1", XI.Map.S2cCurrencies1.AlignOf.ToString());
        Check("CU PD_ALIGN compiler == 1 (not 8)", g["PD_ALIGN"], "1");
        // the run's start byte is 212 -- NON-8-aligned (212 % 8 == 4) -- legal ONLY because pack(1).
        Check("CU OffPlans == compiler rems_tale_10 + 1", (ParseNum(g["OFF_PD.rems_tale_chapters_10_stored"]) + 1).ToString(),
            XI.Map.S2cCurrencies1.OffPlans.ToString());
        Check("CU OffPlans NON-8-aligned (212 % 8 == 4) -- pack(1) makes it legal", "1",
            ((XI.Map.S2cCurrencies1.OffPlans % 8) == 4 ? 1 : 0).ToString());
        // the run is 8 bytes wide: reclamation_marks (first u16 after) == OffPlans + 8.
        Check("CU reclamation_marks == OffPlans + 8 (the unit is 8 bytes)", g["OFF_PD.reclamation_marks"],
            (XI.Map.S2cCurrencies1.OffPlans + 8).ToString());
        // the BODY scan's absolute off -> intra-unit bit must match the standalone unit's (off*8+shift - 212*8).
        long bodyBaseBit = ParseNum(g["BF_PD_CUR_bloodshed_plans_stored_off"]) * 8;
        foreach (var (key, bit, _) in new (string, int, int)[]
                 {
                     ("bloodshed_plans_stored", XI.Map.PlansUnit.BloodshedBit, 9),
                     ("umbrage_plans_stored", XI.Map.PlansUnit.UmbrageBit, 9),
                     ("primacy_plans_stored", XI.Map.PlansUnit.PrimacyBit, 9),
                     ("unused", XI.Map.PlansUnit.UnusedBit, 19),
                 })
        {
            long absoff = ParseNum(g[$"BF_PD_CUR_{key}_off"]);
            long absshift = ParseNum(g[$"BF_PD_CUR_{key}_shift"]);
            Check($"CU body-scan {key} intra-unit bit == standalone (abs off*8+shift - {bodyBaseBit})",
                (absoff * 8 + absshift - bodyBaseBit).ToString(), bit.ToString());
        }

        // ---- dense scalar chain: representative compiler offsets + the gapless no-tail-pad end ----
        Check("CU OFF conquest_sandoria", g["OFF_PD.conquest_points_sandoria"], XI.Map.S2cCurrencies1.OffConquestSandoria.ToString());
        Check("CU OFF cinders", g["OFF_PD.cinders"], XI.Map.S2cCurrencies1.OffCinders.ToString());
        Check("CU OFF synergy_fire", g["OFF_PD.synergy_fewell_fire"], XI.Map.S2cCurrencies1.OffSynergyFire.ToString());
        Check("CU OFF synergy_dark == fire+7 (u8 gapless run)", g["OFF_PD.synergy_fewell_dark"], XI.Map.S2cCurrencies1.OffSynergyDark.ToString());
        Check("CU OFF unity_accolades", g["OFF_PD.unity_accolades"], XI.Map.S2cCurrencies1.OffUnityAccolades.ToString());
        Check("CU OFF deeds", g["OFF_PD.deeds"], XI.Map.S2cCurrencies1.OffDeeds.ToString());
        Check("CU OFF padding01", g["OFF_PD.padding01"], XI.Map.S2cCurrencies1.OffPadding01.ToString());
        // u8 synergy run is byte-adjacent (no pack padding): dark == fire + 7.
        Check("CU synergy dark==fire+7 (pack(1) u8 adjacency)", "1",
            (XI.Map.S2cCurrencies1.OffSynergyDark == XI.Map.S2cCurrencies1.OffSynergyFire + 7 ? 1 : 0).ToString());
        // the chain ends gapless: padding01 + 2 == SizeOf (align 1 -> NO tail pad).
        Check("CU padding01 + 2 == SizeOf (no tail pad, align 1)", "1",
            (XI.Map.S2cCurrencies1.OffPadding01 + 2 == XI.Map.S2cCurrencies1.SizeOf ? 1 : 0).ToString());
        // SizeOf == 248 == the raw member sum (pack(1) inserts NO padding anywhere). The pack(1) collapse of the
        // uint64's natural alignment is the load-bearing fact (NOT "size not a multiple of 8" -- 248 IS 31*8):
        // the struct's align (1) is strictly less than the uint64 unit's natural align (8).
        Check("CU struct align 1 < uint64 natural align 8 (pack(1) collapsed it)", "1",
            (XI.Map.S2cCurrencies1.AlignOf < XI.Map.PlansUnit.AlignOf ? 1 : 0).ToString());

        // ==== whole-struct fill determinism (i*7+3), GAPLESS -- pack(1) means NO padding byte anywhere ====
        byte[] plansFill = HexToBytes(g["PLANS_FILL"]);
        Check("CU PLANS_FILL_SIZE", g["PLANS_FILL_SIZE"], plansFill.Length.ToString());
        {
            var local = new byte[plansFill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check("CU plans deterministic fill (8B gapless i*7+3)", g["PLANS_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }
        byte[] pdFill = HexToBytes(g["PD_FILL"]);
        Check("CU PD_FILL_SIZE", g["PD_FILL_SIZE"], pdFill.Length.ToString());
        {
            var local = new byte[pdFill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check("CU PD deterministic fill (248B gapless i*7+3 -- proves NO internal padding)", g["PD_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }

        // ==== uint64 run: accessor == raw uint64 read at the compiler offset (two independent ways) ====
        ulong unitRaw = BitConverter.ToUInt64(pdFill, XI.Map.S2cCurrencies1.OffPlans);
        Check("CU GetPlansWord == raw uint64 at compiler OffPlans", unitRaw.ToString(), XI.Map.S2cCurrencies1.GetPlansWord(pdFill).ToString());
        Check("CU bloodshed==raw@compiler (uint64 >>0 &0x1FF)", ((unitRaw >> XI.Map.PlansUnit.BloodshedBit) & ((1UL << XI.Map.PlansUnit.BloodshedWidth) - 1)).ToString(),
            XI.Map.S2cCurrencies1.GetPlanBloodshed(pdFill).ToString());
        Check("CU umbrage==raw@compiler (uint64 >>9 &0x1FF)", ((unitRaw >> XI.Map.PlansUnit.UmbrageBit) & ((1UL << XI.Map.PlansUnit.UmbrageWidth) - 1)).ToString(),
            XI.Map.S2cCurrencies1.GetPlanUmbrage(pdFill).ToString());
        Check("CU unused==raw@compiler (uint64 >>45 &0x7FFFF)", ((unitRaw >> XI.Map.PlansUnit.UnusedBit) & ((1UL << XI.Map.PlansUnit.UnusedWidth) - 1)).ToString(),
            XI.Map.S2cCurrencies1.GetPlanUnused(pdFill).ToString());
        // scalar accessor-vs-raw at compiler offsets.
        Check("CU GetI32 cinders==raw@compiler", BitConverter.ToInt32(pdFill, (int)ParseNum(g["OFF_PD.cinders"])).ToString(),
            XI.Map.S2cCurrencies1.GetI32(pdFill, XI.Map.S2cCurrencies1.OffCinders).ToString());
        Check("CU GetU16 deeds==raw@compiler", BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.deeds"])).ToString(),
            XI.Map.S2cCurrencies1.GetU16(pdFill, XI.Map.S2cCurrencies1.OffDeeds).ToString());
        Check("CU GetU8 rems5==raw@compiler", pdFill[(int)ParseNum(g["OFF_PD.rems_tale_chapters_5_stored"])].ToString(),
            XI.Map.S2cCurrencies1.GetU8(pdFill, XI.Map.S2cCurrencies1.OffRemsTale5).ToString());

        // ==== THE straddle NEGATIVE check: a per-byte mask on the uint64 drops a 9-bit field's high bits ====
        {
            var b = new byte[XI.Map.S2cCurrencies1.SizeOf];
            XI.Map.S2cCurrencies1.SetPlanUmbrage(b, 0x1FF);   // all 9 bits set (intra bits 9..17, crosses unit bytes 1/2)
            uint correct = XI.Map.S2cCurrencies1.GetPlanUmbrage(b);
            // a buggy per-byte reader looks at ONE unit byte only and misses the other -> never equals the full 0x1FF.
            int whichByte = XI.Map.PlansUnit.UmbrageBit / 8;   // byte 1 of the unit
            uint perByte = (uint)((b[XI.Map.S2cCurrencies1.OffPlans + whichByte] >> (XI.Map.PlansUnit.UmbrageBit % 8)) & 0xFF);
            Check("CU straddle: correct uint64 read == 0x1FF", ((uint)0x1FF).ToString(), correct.ToString());
            Check("CU straddle: per-byte read < 0x1FF (DROPS the crossing bits) -- NOT equal", "1",
                (perByte != correct ? 1 : 0).ToString());
        }

        // ==== RMW preserves siblings + cross-run isolation ====
        {
            var b = new byte[XI.Map.S2cCurrencies1.SizeOf];
            XI.Map.S2cCurrencies1.SetPlanBloodshed(b, 0x1FF);
            XI.Map.S2cCurrencies1.SetPlanUmbrage(b, 0x123);   // must NOT disturb bloodshed
            Check("CU RMW SetPlanUmbrage preserves bloodshed (0x1FF)", ((uint)0x1FF).ToString(), XI.Map.S2cCurrencies1.GetPlanBloodshed(b).ToString());
            Check("CU RMW SetPlanUmbrage reads back (0x123)", ((uint)0x123).ToString(), XI.Map.S2cCurrencies1.GetPlanUmbrage(b).ToString());
            // the two 9-bit fields packed at intra bits 0 and 9 compose into the raw uint64 exactly as the compiler says.
            ulong expect = (0x1FFUL << XI.Map.PlansUnit.BloodshedBit) | (0x123UL << XI.Map.PlansUnit.UmbrageBit);
            Check("CU RMW packed uint64 == compiler intra-bit-positions composed", expect.ToString(), XI.Map.S2cCurrencies1.GetPlansWord(b).ToString());
            // writing the run leaves the field AFTER it (reclamation_marks) and BEFORE it (rems_tale_10) untouched.
            XI.Map.S2cCurrencies1.SetU16(b, XI.Map.S2cCurrencies1.OffReclamationMarks, 0xBEEF);
            Check("CU scalar SetU16 reclamation lands at OffPlans+8", ((ushort)0xBEEF).ToString(),
                BitConverter.ToUInt16(b, XI.Map.S2cCurrencies1.OffPlans + 8).ToString());
            Check("CU SetU16 reclamation leaves the uint64 run untouched", expect.ToString(), XI.Map.S2cCurrencies1.GetPlansWord(b).ToString());
        }

        // outer-body scalar setters land at the compiler offset, no neighbouring disturbance.
        {
            var b = new byte[XI.Map.S2cCurrencies1.SizeOf];
            XI.Map.S2cCurrencies1.SetI32(b, XI.Map.S2cCurrencies1.OffConquestSandoria, -55555);
            Check("CU SetI32 conquest_sandoria lands at compiler offset 0", "-55555",
                BitConverter.ToInt32(b, (int)ParseNum(g["OFF_PD.conquest_points_sandoria"])).ToString());
            XI.Map.S2cCurrencies1.SetI32(b, XI.Map.S2cCurrencies1.OffUnityAccolades, 123456789);
            Check("CU SetI32 unity_accolades roundtrip (after the uint64 run)", "123456789",
                XI.Map.S2cCurrencies1.GetI32(b, XI.Map.S2cCurrencies1.OffUnityAccolades).ToString());
            Check("CU SetI32 unity_accolades leaves the uint64 run's unit zero", "0",
                BitConverter.ToUInt64(b, XI.Map.S2cCurrencies1.OffPlans).ToString());
        }
    }

    static void VerifyMapS2cCurrencies2(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_currencies_2_golden.txt");
        Console.WriteLine("\n== S2c currencies_2 (0x118) parity: C# XI.Map vs layout oracle (DEFAULT-packed dense scalar chain, padding absorbs alignment) ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c currencies_2 golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        long BfBit(string key) => ParseNum(g[$"OFF_PD.{key}"]);

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; the engine adds the 4-byte header).
        Check("CU2 SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- THE chunk-29 packing headline: DEFAULT (natural) packing, alignof 4 (largest member int32). ----
        Check("CU2 PD_SIZE", g["PD_SIZE"], XI.Map.S2cCurrencies2.SizeOf.ToString());
        Check("CU2 PD_ALIGN compiler == model AlignOf", g["PD_ALIGN"], XI.Map.S2cCurrencies2.AlignOf.ToString());
        // the contrast to chunk 28: currencies_1 was pack(1) -> AlignOf 1; currencies_2 has no #pragma pack -> AlignOf 4.
        Check("CU2 default packing AlignOf(4) != currencies_1 pack(1) AlignOf(1)", "1",
            ((XI.Map.S2cCurrencies2.AlignOf == ParseNum(g["PD_ALIGN"]) && XI.Map.S2cCurrencies2.AlignOf != XI.Map.S2cCurrencies1.AlignOf) ? 1 : 0).ToString());
        Check("CU2 SizeOf % AlignOf == 0 (natural tail)", "1", (XI.Map.S2cCurrencies2.SizeOf % XI.Map.S2cCurrencies2.AlignOf == 0 ? 1 : 0).ToString());

        // ---- hole-free verdict: MEMBER_BYTES == PD_SIZE means natural alignment inserted NO silent pad byte. ----
        Check("CU2 MEMBER_BYTES == PD_SIZE (compiler: no alignment hole)", g["MEMBER_BYTES"], g["PD_SIZE"]);
        Check("CU2 PAD_INSERTED == 0", g["PAD_INSERTED"], "0");

        // ---- THE padding-absorbs-alignment proof (derived only from golden offsets, never hand-computed):
        //   padding00[3] is exactly the 3 bytes needed to bring the running u8 chain up to the 4-aligned silver_aman,
        //   and padding02 is exactly the 2 bytes needed to reach the 4-aligned temenos_units. ----
        long offDarkness = ParseNum(g["OFF_PD.darkness_spheres_set"]);   // last real u8
        long offPad00 = ParseNum(g["OFF_PD.padding00"]);
        long offSilver = ParseNum(g["OFF_PD.silver_aman_vouchers_stored"]); // first int32 after padding00
        Check("CU2 padding00 immediately follows darkness_spheres (no gap)", "1", (offPad00 == offDarkness + 1 ? 1 : 0).ToString());
        Check("CU2 darkness + 1 + Padding00Len == silver_aman (padding00 is the align filler)", "1",
            (offDarkness + 1 + XI.Map.S2cCurrencies2.Padding00Len == offSilver ? 1 : 0).ToString());
        Check("CU2 silver_aman is 4-aligned (needs the padding00[3])", "1", (offSilver % 4 == 0 ? 1 : 0).ToString());
        long offIsAccolades = ParseNum(g["OFF_PD.is_accolades"]);
        long offPad02 = ParseNum(g["OFF_PD.padding02"]);
        long offTemenos = ParseNum(g["OFF_PD.temenos_units"]);
        Check("CU2 padding02 == is_accolades + 2", "1", (offPad02 == offIsAccolades + 2 ? 1 : 0).ToString());
        Check("CU2 padding02 + 2 == temenos_units (padding02 is the align filler)", "1", (offPad02 + 2 == offTemenos ? 1 : 0).ToString());
        Check("CU2 temenos_units is 4-aligned (needs the padding02)", "1", (offTemenos % 4 == 0 ? 1 : 0).ToString());

        // ---- every field offset: model const == compiler OFF_PD.* AND accessor == raw read at the compiler offset. ----
        byte[] pdFill = HexToBytes(g["PD_FILL"]);
        Check("CU2 PD_FILL_SIZE", g["PD_FILL_SIZE"], pdFill.Length.ToString());
        {
            var local = new byte[pdFill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check("CU2 whole-struct deterministic fill (156B i*7+3 gapless -- confirms SizeOf + PAD_INSERTED==0)",
                g["PD_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }

        // (golden key, model Off* const, width: 4/2/1). Every member of the fixed-width chain.
        var fields = new (string key, int model, int w)[]
        {
            ("bayld", XI.Map.S2cCurrencies2.OffBayld, 4),
            ("kinetic_units", XI.Map.S2cCurrencies2.OffKineticUnits, 2),
            ("coalition_imprimaturs", XI.Map.S2cCurrencies2.OffCoalitionImprimaturs, 1),
            ("mystical_canteens", XI.Map.S2cCurrencies2.OffMysticalCanteens, 1),
            ("obsidian_fragments", XI.Map.S2cCurrencies2.OffObsidianFragments, 4),
            ("lebondopt_wings_stored", XI.Map.S2cCurrencies2.OffLebondoptWings, 2),
            ("pulchridopt_wings_stored", XI.Map.S2cCurrencies2.OffPulchridoptWings, 2),
            ("mweya_plasm_corpuscles", XI.Map.S2cCurrencies2.OffMweyaPlasmCorpuscles, 4),
            ("ghastly_stones_stored", XI.Map.S2cCurrencies2.OffGhastlyStones0, 1),
            ("ghastly_stones_plus1_stored", XI.Map.S2cCurrencies2.OffGhastlyStones1, 1),
            ("ghastly_stones_plus2_stored", XI.Map.S2cCurrencies2.OffGhastlyStones2, 1),
            ("verdigris_stones_stored", XI.Map.S2cCurrencies2.OffVerdigrisStones0, 1),
            ("verdigris_stones_plus1_stored", XI.Map.S2cCurrencies2.OffVerdigrisStones1, 1),
            ("verdigris_stones_plus2_stored", XI.Map.S2cCurrencies2.OffVerdigrisStones2, 1),
            ("wailing_stones_stored", XI.Map.S2cCurrencies2.OffWailingStones0, 1),
            ("wailing_stones_plus1_stored", XI.Map.S2cCurrencies2.OffWailingStones1, 1),
            ("wailing_stones_plus2_stored", XI.Map.S2cCurrencies2.OffWailingStones2, 1),
            ("snowslit_stones_stored", XI.Map.S2cCurrencies2.OffSnowslitStones0, 1),
            ("snowslit_stones_plus1_stored", XI.Map.S2cCurrencies2.OffSnowslitStones1, 1),
            ("snowslit_stones_plus2_stored", XI.Map.S2cCurrencies2.OffSnowslitStones2, 1),
            ("snowtip_stones_stored", XI.Map.S2cCurrencies2.OffSnowtipStones0, 1),
            ("snowtip_stones_plus1_stored", XI.Map.S2cCurrencies2.OffSnowtipStones1, 1),
            ("snowtip_stones_plus2_stored", XI.Map.S2cCurrencies2.OffSnowtipStones2, 1),
            ("snowdim_stones_stored", XI.Map.S2cCurrencies2.OffSnowdimStones0, 1),
            ("snowdim_stones_plus1_stored", XI.Map.S2cCurrencies2.OffSnowdimStones1, 1),
            ("snowdim_stones_plus2_stored", XI.Map.S2cCurrencies2.OffSnowdimStones2, 1),
            ("snoworb_stones_stored", XI.Map.S2cCurrencies2.OffSnoworbStones0, 1),
            ("snoworb_stones_plus1_stored", XI.Map.S2cCurrencies2.OffSnoworbStones1, 1),
            ("snoworb_stones_plus2_stored", XI.Map.S2cCurrencies2.OffSnoworbStones2, 1),
            ("leafslit_stones_stored", XI.Map.S2cCurrencies2.OffLeafslitStones0, 1),
            ("leafslit_stones_plus1_stored", XI.Map.S2cCurrencies2.OffLeafslitStones1, 1),
            ("leafslit_stones_plus2_stored", XI.Map.S2cCurrencies2.OffLeafslitStones2, 1),
            ("leaftip_stones_stored", XI.Map.S2cCurrencies2.OffLeaftipStones0, 1),
            ("leaftip_stones_plus1_stored", XI.Map.S2cCurrencies2.OffLeaftipStones1, 1),
            ("leaftip_stones_plus2_stored", XI.Map.S2cCurrencies2.OffLeaftipStones2, 1),
            ("leafdim_stones_stored", XI.Map.S2cCurrencies2.OffLeafdimStones0, 1),
            ("leafdim_stones_plus1_stored", XI.Map.S2cCurrencies2.OffLeafdimStones1, 1),
            ("leafdim_stones_plus2_stored", XI.Map.S2cCurrencies2.OffLeafdimStones2, 1),
            ("leaforb_stones_stored", XI.Map.S2cCurrencies2.OffLeaforbStones0, 1),
            ("leaforb_stones_plus1_stored", XI.Map.S2cCurrencies2.OffLeaforbStones1, 1),
            ("leaforb_stones_plus2_stored", XI.Map.S2cCurrencies2.OffLeaforbStones2, 1),
            ("duskslit_stones_stored", XI.Map.S2cCurrencies2.OffDuskslitStones0, 1),
            ("duskslit_stones_plus1_stored", XI.Map.S2cCurrencies2.OffDuskslitStones1, 1),
            ("duskslit_stones_plus2_stored", XI.Map.S2cCurrencies2.OffDuskslitStones2, 1),
            ("dusktip_stones_stored", XI.Map.S2cCurrencies2.OffDusktipStones0, 1),
            ("dusktip_stones_plus1_stored", XI.Map.S2cCurrencies2.OffDusktipStones1, 1),
            ("dusktip_stones_plus2_stored", XI.Map.S2cCurrencies2.OffDusktipStones2, 1),
            ("duskdim_stones_stored", XI.Map.S2cCurrencies2.OffDuskdimStones0, 1),
            ("duskdim_stones_plus1_stored", XI.Map.S2cCurrencies2.OffDuskdimStones1, 1),
            ("duskdim_stones_plus2_stored", XI.Map.S2cCurrencies2.OffDuskdimStones2, 1),
            ("duskorb_stones_stored", XI.Map.S2cCurrencies2.OffDuskorbStones0, 1),
            ("duskorb_stones_plus1_stored", XI.Map.S2cCurrencies2.OffDuskorbStones1, 1),
            ("duskorb_stones_plus2_stored", XI.Map.S2cCurrencies2.OffDuskorbStones2, 1),
            ("pellucid_stones_stored", XI.Map.S2cCurrencies2.OffPellucidStones, 1),
            ("fern_stones_stored", XI.Map.S2cCurrencies2.OffFernStones, 1),
            ("taupe_stones_stored", XI.Map.S2cCurrencies2.OffTaupeStones, 1),
            ("mellidopt_wings_stored", XI.Map.S2cCurrencies2.OffMellidoptWings, 2),
            ("escha_beads", XI.Map.S2cCurrencies2.OffEschaBeads, 2),
            ("escha_silt", XI.Map.S2cCurrencies2.OffEschaSilt, 4),
            ("potpourri", XI.Map.S2cCurrencies2.OffPotpourri, 4),
            ("hallmarks", XI.Map.S2cCurrencies2.OffHallmarks, 4),
            ("total_hallmarks", XI.Map.S2cCurrencies2.OffTotalHallmarks, 4),
            ("badges_of_gallantry", XI.Map.S2cCurrencies2.OffBadgesOfGallantry, 4),
            ("crafter_points", XI.Map.S2cCurrencies2.OffCrafterPoints, 4),
            ("fire_crystals_set", XI.Map.S2cCurrencies2.OffFireCrystalsSet, 1),
            ("ice_crystals_set", XI.Map.S2cCurrencies2.OffIceCrystalsSet, 1),
            ("wind_crystals_set", XI.Map.S2cCurrencies2.OffWindCrystalsSet, 1),
            ("earth_crystals_set", XI.Map.S2cCurrencies2.OffEarthCrystalsSet, 1),
            ("lightning_crystals_set", XI.Map.S2cCurrencies2.OffLightningCrystalsSet, 1),
            ("water_crystals_set", XI.Map.S2cCurrencies2.OffWaterCrystalsSet, 1),
            ("light_crystals_set", XI.Map.S2cCurrencies2.OffLightCrystalsSet, 1),
            ("dark_crystals_set", XI.Map.S2cCurrencies2.OffDarkCrystalsSet, 1),
            ("mc_i_sr01s_set", XI.Map.S2cCurrencies2.OffMcISr01sSet, 1),
            ("mc_i_sr02s_set", XI.Map.S2cCurrencies2.OffMcISr02sSet, 1),
            ("mc_i_sr03s_set", XI.Map.S2cCurrencies2.OffMcISr03sSet, 1),
            ("liquefactions_spheres_set", XI.Map.S2cCurrencies2.OffLiquefactionsSpheres, 1),
            ("induration_spheres_set", XI.Map.S2cCurrencies2.OffIndurationSpheres, 1),
            ("dentonation_spheres_set", XI.Map.S2cCurrencies2.OffDenotationSpheres, 1),
            ("scission_spheres_set", XI.Map.S2cCurrencies2.OffScissionSpheres, 1),
            ("impaction_spheres_set", XI.Map.S2cCurrencies2.OffImpactionSpheres, 1),
            ("reverberation_spheres_set", XI.Map.S2cCurrencies2.OffReverberationSpheres, 1),
            ("transfixion_spheres_set", XI.Map.S2cCurrencies2.OffTransfixionSpheres, 1),
            ("compression_spheres_set", XI.Map.S2cCurrencies2.OffCompressionSpheres, 1),
            ("fusion_spheres_set", XI.Map.S2cCurrencies2.OffFusionSpheres, 1),
            ("distortion_spheres_set", XI.Map.S2cCurrencies2.OffDistortionSpheres, 1),
            ("fragmentation_spheres_set", XI.Map.S2cCurrencies2.OffFragmentationSpheres, 1),
            ("gravitation_spheres_set", XI.Map.S2cCurrencies2.OffGravitationSpheres, 1),
            ("light_spheres_set", XI.Map.S2cCurrencies2.OffLightSpheres, 1),
            ("darkness_spheres_set", XI.Map.S2cCurrencies2.OffDarknessSpheres, 1),
            ("padding00", XI.Map.S2cCurrencies2.OffPadding00, 1),
            ("silver_aman_vouchers_stored", XI.Map.S2cCurrencies2.OffSilverAmanVouchers, 4),
            ("domain_points", XI.Map.S2cCurrencies2.OffDomainPoints, 4),
            ("domain_points_earned_today", XI.Map.S2cCurrencies2.OffDomainPointsEarnedToday, 4),
            ("mog_segments", XI.Map.S2cCurrencies2.OffMogSegments, 4),
            ("gallimaufry", XI.Map.S2cCurrencies2.OffGallimaufry, 4),
            ("is_accolades", XI.Map.S2cCurrencies2.OffIsAccolades, 2),
            ("padding02", XI.Map.S2cCurrencies2.OffPadding02, 2),
            ("temenos_units", XI.Map.S2cCurrencies2.OffTemenosUnits, 4),
            ("apollyon_units", XI.Map.S2cCurrencies2.OffApollyonUnits, 4),
        };
        foreach (var (key, model, w) in fields)
        {
            long coff = BfBit(key);
            // (1) model Off* const == compiler offset
            Check($"CU2 OFF {key}", g[$"OFF_PD.{key}"], model.ToString());
            // (2) accessor at the model const == raw little-endian read at the compiler offset (two independent ways)
            string raw, acc;
            switch (w)
            {
                case 4:
                    raw = BitConverter.ToInt32(pdFill, (int)coff).ToString();
                    acc = XI.Map.S2cCurrencies2.GetI32(pdFill, model).ToString();
                    break;
                case 2:
                    raw = BitConverter.ToUInt16(pdFill, (int)coff).ToString();
                    acc = XI.Map.S2cCurrencies2.GetU16(pdFill, model).ToString();
                    break;
                default:
                    raw = pdFill[(int)coff].ToString();
                    acc = XI.Map.S2cCurrencies2.GetU8(pdFill, model).ToString();
                    break;
            }
            Check($"CU2 read {key} == raw@compiler", raw, acc);
        }
        Check("CU2 field-offset table count == 99 (dense chain fully transcribed)", "99", fields.Length.ToString());

        // padding00[3] slice accessor == raw 3-byte window at the compiler offset.
        {
            var slice = XI.Map.S2cCurrencies2.GetPadding00(pdFill);
            Check("CU2 GetPadding00 length", XI.Map.S2cCurrencies2.Padding00Len.ToString(), slice.Length.ToString());
            Check("CU2 GetPadding00 bytes == raw@compiler",
                BitConverter.ToString(pdFill, (int)BfBit("padding00"), XI.Map.S2cCurrencies2.Padding00Len).Replace("-", ""),
                BitConverter.ToString(slice.ToArray()).Replace("-", ""));
        }

        // ---- write-back (RMW) at compiler offsets, reading back through the RAW offset (not the same accessor). ----
        {
            var b = new byte[XI.Map.S2cCurrencies2.SizeOf];
            // the int32 immediately AFTER padding00 -- the alignment-sensitive field.
            XI.Map.S2cCurrencies2.SetI32(b, XI.Map.S2cCurrencies2.OffSilverAmanVouchers, -987654321);
            Check("CU2 SetI32 silver_aman lands at compiler offset", "-987654321",
                BitConverter.ToInt32(b, (int)BfBit("silver_aman_vouchers_stored")).ToString());
            // the int32 immediately AFTER padding02.
            XI.Map.S2cCurrencies2.SetI32(b, XI.Map.S2cCurrencies2.OffTemenosUnits, 0x7FFFFFFF);
            Check("CU2 SetI32 temenos lands at compiler offset (after padding02)", ((int)0x7FFFFFFF).ToString(),
                BitConverter.ToInt32(b, (int)BfBit("temenos_units")).ToString());
            XI.Map.S2cCurrencies2.SetU16(b, XI.Map.S2cCurrencies2.OffIsAccolades, 0xCAFE);
            Check("CU2 SetU16 is_accolades roundtrip (raw)", ((ushort)0xCAFE).ToString(),
                BitConverter.ToUInt16(b, (int)BfBit("is_accolades")).ToString());
            XI.Map.S2cCurrencies2.SetU8(b, XI.Map.S2cCurrencies2.OffTaupeStones, 0x5A);
            Check("CU2 SetU8 taupe roundtrip (raw)", "90", b[(int)BfBit("taupe_stones_stored")].ToString());
            // writing silver_aman does NOT disturb the padding00 bytes before it (they stay zero) -- neighbour isolation.
            XI.Map.S2cCurrencies2.SetI32(b, XI.Map.S2cCurrencies2.OffDomainPoints, 777);
            Check("CU2 SetI32 domain_points leaves padding00[0] zero (neighbour isolation)", "0",
                b[(int)BfBit("padding00")].ToString());
        }
    }

    static void VerifyMapS2cEmoteList(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_emote_list_golden.txt");
        Console.WriteLine("\n== S2c emote_list (0x11A) parity: C# XI.Map vs layout oracle (first uint16 bit-field run + 22-wide uint32 flag word + max-align default packing) ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c emote_list golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        long BfBit(string structName, string field) => ParseNum(g[$"BF_{structName}_{field}_off"]) * 8 + ParseNum(g[$"BF_{structName}_{field}_shift"]);

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; the engine adds the 4-byte header).
        Check("EMO SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- the two standalone bit-field units: size/align (a uint32 run and THE FIRST uint16 run) ----
        Check("EMO JE_SIZE (uint32 allocation unit)", g["JE_SIZE"], XI.Map.JobEmotesEntry.SizeOf.ToString());
        Check("EMO JE_ALIGN", g["JE_ALIGN"], XI.Map.JobEmotesEntry.AlignOf.ToString());
        Check("EMO CH_SIZE (FIRST uint16 allocation unit)", g["CH_SIZE"], XI.Map.ChairEmotesEntry.SizeOf.ToString());
        Check("EMO CH_ALIGN (uint16 align 2, NOT 4)", g["CH_ALIGN"], XI.Map.ChairEmotesEntry.AlignOf.ToString());
        // each run sums to EXACTLY its allocation unit's width.
        Check("EMO jobemotes widths sum to 32 (22*1 + 10)", "1", (XI.Map.JobEmotesEntry.SumsTo32() ? 1 : 0).ToString());
        Check("EMO chairemotes widths sum to 16 (11*1 + 5)", "1", (XI.Map.ChairEmotesEntry.SumsTo16() ? 1 : 0).ToString());

        // ---- every jobemotes bit position == compiler off*8+shift AND width == compiler ----
        var jobFields = new (string key, int model)[]
        {
            ("WAR", XI.Map.JobEmotesEntry.WARBit), ("MNK", XI.Map.JobEmotesEntry.MNKBit),
            ("WHM", XI.Map.JobEmotesEntry.WHMBit), ("BLM", XI.Map.JobEmotesEntry.BLMBit),
            ("RDM", XI.Map.JobEmotesEntry.RDMBit), ("THF", XI.Map.JobEmotesEntry.THFBit),
            ("PLD", XI.Map.JobEmotesEntry.PLDBit), ("DRK", XI.Map.JobEmotesEntry.DRKBit),
            ("BST", XI.Map.JobEmotesEntry.BSTBit), ("BRD", XI.Map.JobEmotesEntry.BRDBit),
            ("RNG", XI.Map.JobEmotesEntry.RNGBit), ("SAM", XI.Map.JobEmotesEntry.SAMBit),
            ("NIN", XI.Map.JobEmotesEntry.NINBit), ("DRG", XI.Map.JobEmotesEntry.DRGBit),
            ("SMN", XI.Map.JobEmotesEntry.SMNBit), ("BLU", XI.Map.JobEmotesEntry.BLUBit),
            ("COR", XI.Map.JobEmotesEntry.CORBit), ("PUP", XI.Map.JobEmotesEntry.PUPBit),
            ("DNC", XI.Map.JobEmotesEntry.DNCBit), ("SCH", XI.Map.JobEmotesEntry.SCHBit),
            ("GEO", XI.Map.JobEmotesEntry.GEOBit), ("RUN", XI.Map.JobEmotesEntry.RUNBit),
            ("unused", XI.Map.JobEmotesEntry.UnusedBit),
        };
        foreach (var (key, model) in jobFields)
        {
            Check($"EMO jobemotes {key} bit == compiler off*8+shift", BfBit("jobemotes_t", key).ToString(), model.ToString());
            Check($"EMO jobemotes {key} width == compiler", g[$"BF_jobemotes_t_{key}_width"],
                (key == "unused" ? XI.Map.JobEmotesEntry.UnusedWidth : XI.Map.JobEmotesEntry.FlagWidth).ToString());
        }
        // the trailing :10 sits at bit 22 -> bits 22..31 CROSS byte 2/3 (22%8 + 10 > 8): read the whole uint32.
        Check("EMO jobemotes unused (bit 22 width 10) straddles bytes 2/3 of the uint32", "1",
            (((XI.Map.JobEmotesEntry.UnusedBit % 8) + XI.Map.JobEmotesEntry.UnusedWidth > 8) ? 1 : 0).ToString());

        // ---- every chairemotes bit position == compiler off*8+shift AND width == compiler (uint16 unit) ----
        var chairFields = new (string key, int model)[]
        {
            ("Chair1", XI.Map.ChairEmotesEntry.Chair1Bit), ("Chair2", XI.Map.ChairEmotesEntry.Chair2Bit),
            ("Chair3", XI.Map.ChairEmotesEntry.Chair3Bit), ("Chair4", XI.Map.ChairEmotesEntry.Chair4Bit),
            ("Chair5", XI.Map.ChairEmotesEntry.Chair5Bit), ("Chair6", XI.Map.ChairEmotesEntry.Chair6Bit),
            ("Chair7", XI.Map.ChairEmotesEntry.Chair7Bit), ("Chair8", XI.Map.ChairEmotesEntry.Chair8Bit),
            ("Chair9", XI.Map.ChairEmotesEntry.Chair9Bit), ("Chair10", XI.Map.ChairEmotesEntry.Chair10Bit),
            ("Chair11", XI.Map.ChairEmotesEntry.Chair11Bit), ("unused", XI.Map.ChairEmotesEntry.UnusedBit),
        };
        foreach (var (key, model) in chairFields)
        {
            Check($"EMO chairemotes {key} bit == compiler off*8+shift", BfBit("chairemotes_t", key).ToString(), model.ToString());
            Check($"EMO chairemotes {key} width == compiler", g[$"BF_chairemotes_t_{key}_width"],
                (key == "unused" ? XI.Map.ChairEmotesEntry.UnusedWidth : XI.Map.ChairEmotesEntry.FlagWidth).ToString());
        }
        // THE uint16-unit fact: bit 8 is INSIDE the 16-bit unit but in byte 1 -- a byte-0-only reader misses it.
        Check("EMO chair9 (bit 8) is in the uint16 unit's HIGH byte (bit/8==1)", "1",
            ((XI.Map.ChairEmotesEntry.Chair9Bit / 8) == 1 ? 1 : 0).ToString());

        // ---- the outer body: DEFAULT packing takes the MAX member align (4), NOT pack(1)'s collapse to 1 ----
        Check("EMO PD_SIZE", g["PD_SIZE"], XI.Map.S2cEmoteList.SizeOf.ToString());
        Check("EMO PD_ALIGN == 4 (max of JE_ALIGN=4 / CH_ALIGN=2 / u16=2)", g["PD_ALIGN"], XI.Map.S2cEmoteList.AlignOf.ToString());
        // the contrast to chunk 28 (pack(1) collapsed a uint64 to align 1): here the uint32 member sets align 4.
        Check("EMO whole-struct align == max(JE_ALIGN, CH_ALIGN) (DEFAULT packing, NOT pack(1))", "1",
            (XI.Map.S2cEmoteList.AlignOf == Math.Max(XI.Map.JobEmotesEntry.AlignOf, XI.Map.ChairEmotesEntry.AlignOf) ? 1 : 0).ToString());
        // the two units + padding00 are laid back-to-back at the compiler offsets (no realign between them).
        Check("EMO OFF JobEmotes", g["OFF_PD.JobEmotes"], XI.Map.S2cEmoteList.OffJobEmotes.ToString());
        Check("EMO OFF Chairs == JobEmotes + JE_SIZE (u16 needs no realign at 4)", g["OFF_PD.Chairs"], XI.Map.S2cEmoteList.OffChairs.ToString());
        Check("EMO OFF Chairs == compiler JobEmotes + 4", (ParseNum(g["OFF_PD.JobEmotes"]) + 4).ToString(), XI.Map.S2cEmoteList.OffChairs.ToString());
        Check("EMO OFF padding00 == Chairs + CH_SIZE", g["OFF_PD.padding00"], XI.Map.S2cEmoteList.OffPadding00.ToString());
        Check("EMO OFF padding00 == compiler Chairs + 2", (ParseNum(g["OFF_PD.Chairs"]) + 2).ToString(), XI.Map.S2cEmoteList.OffPadding00.ToString());
        // the chain ends gapless: padding00 + 2 == SizeOf (8), and SizeOf % AlignOf == 0 -> no tail pad.
        Check("EMO padding00 + 2 == SizeOf (no tail pad)", "1",
            (XI.Map.S2cEmoteList.OffPadding00 + 2 == XI.Map.S2cEmoteList.SizeOf ? 1 : 0).ToString());
        Check("EMO SizeOf % AlignOf == 0 (natural tail)", "1", (XI.Map.S2cEmoteList.SizeOf % XI.Map.S2cEmoteList.AlignOf == 0 ? 1 : 0).ToString());

        // ==== whole-struct / unit fill determinism (i*7+3), gapless: JE[4] CH[2] PD[8] ====
        foreach (var (tag, sizeKey, fillKey) in new (string, string, string)[]
                 { ("JE", "JE_FILL_SIZE", "JE_FILL"), ("CH", "CH_FILL_SIZE", "CH_FILL"), ("PD", "PD_FILL_SIZE", "PD_FILL") })
        {
            byte[] f = HexToBytes(g[fillKey]);
            Check($"EMO {tag}_FILL_SIZE", g[sizeKey], f.Length.ToString());
            var local = new byte[f.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check($"EMO {tag} deterministic fill (gapless i*7+3)", g[fillKey], BitConverter.ToString(local).Replace("-", ""));
        }

        byte[] pdFill = HexToBytes(g["PD_FILL"]);

        // ---- jobemotes (uint32) accessor == raw whole-word read at the compiler offset (two independent ways) ----
        ulong jeWord = BitConverter.ToUInt32(pdFill, XI.Map.S2cEmoteList.OffJobEmotes);
        Check("EMO GetJobWord == raw uint32 at compiler OffJobEmotes", jeWord.ToString(), XI.Map.S2cEmoteList.GetJobWord(pdFill).ToString());
        Check("EMO WAR(bit0)==raw (uint32 >>0 &1)", ((jeWord >> XI.Map.JobEmotesEntry.WARBit) & 1).ToString(), XI.Map.S2cEmoteList.GetJobWAR(pdFill).ToString());
        Check("EMO MNK(bit1)==raw (uint32 >>1 &1)", ((jeWord >> XI.Map.JobEmotesEntry.MNKBit) & 1).ToString(), XI.Map.S2cEmoteList.GetJobMNK(pdFill).ToString());
        Check("EMO RUN(bit21)==raw (uint32 >>21 &1)", ((jeWord >> XI.Map.JobEmotesEntry.RUNBit) & 1).ToString(), XI.Map.S2cEmoteList.GetJobRUN(pdFill).ToString());
        Check("EMO job unused(bit22 width10)==raw (uint32 >>22 &0x3FF)",
            ((jeWord >> XI.Map.JobEmotesEntry.UnusedBit) & ((1UL << XI.Map.JobEmotesEntry.UnusedWidth) - 1)).ToString(),
            XI.Map.S2cEmoteList.GetJobUnused(pdFill).ToString());

        // ---- chairemotes (uint16) accessor == raw whole-word read at the compiler offset (two independent ways) ----
        ulong chWord = BitConverter.ToUInt16(pdFill, XI.Map.S2cEmoteList.OffChairs);
        Check("EMO GetChairWord == raw uint16 at compiler OffChairs", chWord.ToString(), XI.Map.S2cEmoteList.GetChairWord(pdFill).ToString());
        Check("EMO Chair1(bit0)==raw (uint16 >>0 &1)", ((chWord >> XI.Map.ChairEmotesEntry.Chair1Bit) & 1).ToString(), XI.Map.S2cEmoteList.GetChair1(pdFill).ToString());
        Check("EMO Chair8(bit7)==raw (uint16 >>7 &1)", ((chWord >> XI.Map.ChairEmotesEntry.Chair8Bit) & 1).ToString(), XI.Map.S2cEmoteList.GetChair8(pdFill).ToString());
        Check("EMO Chair9(bit8)==raw (uint16 >>8 &1) -- unit's HIGH byte", ((chWord >> XI.Map.ChairEmotesEntry.Chair9Bit) & 1).ToString(), XI.Map.S2cEmoteList.GetChair9(pdFill).ToString());
        Check("EMO Chair11(bit10)==raw (uint16 >>10 &1)", ((chWord >> XI.Map.ChairEmotesEntry.Chair11Bit) & 1).ToString(), XI.Map.S2cEmoteList.GetChair11(pdFill).ToString());
        Check("EMO chair unused(bit11 width5)==raw (uint16 >>11 &0x1F)",
            ((chWord >> XI.Map.ChairEmotesEntry.UnusedBit) & ((1UL << XI.Map.ChairEmotesEntry.UnusedWidth) - 1)).ToString(),
            XI.Map.S2cEmoteList.GetChairUnused(pdFill).ToString());
        // padding00 (plain u16) == raw at compiler offset.
        Check("EMO GetPadding00 == raw uint16 at compiler offset",
            BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.padding00"])).ToString(),
            XI.Map.S2cEmoteList.GetPadding00(pdFill).ToString());

        // ==== THE uint16-unit NEGATIVE check: a byte-0-only reader DROPS bit 8 (Chair9 lives in the unit's byte 1) ====
        {
            var b = new byte[XI.Map.S2cEmoteList.SizeOf];
            XI.Map.S2cEmoteList.SetChairFlag(b, XI.Map.ChairEmotesEntry.Chair9Bit, 1);   // set bit 8 of the uint16 unit @4
            uint correct = XI.Map.S2cEmoteList.GetChair9(b);                              // whole-uint16 read == 1
            // a buggy reader that only masks the unit's LOW byte sees bit 8 == 0 (it's in byte 1).
            uint perLowByte = (uint)((b[XI.Map.S2cEmoteList.OffChairs] >> (XI.Map.ChairEmotesEntry.Chair9Bit % 8)) & 0xFF);
            Check("EMO uint16 straddle: correct whole-word read of bit 8 == 1", "1", correct.ToString());
            Check("EMO uint16 straddle: byte-0-only read of bit 8 == 0 (DROPS it) -- differs", "1",
                (perLowByte != correct ? 1 : 0).ToString());
        }

        // ==== THE uint32 :10 straddle NEGATIVE check: a byte-2-only reader drops the 2 bits in byte 3 ====
        {
            var b = new byte[XI.Map.S2cEmoteList.SizeOf];
            XI.Map.S2cEmoteList.SetJobUnused(b, 0x3FF);   // all 10 bits (intra 22..31, crosses the uint32's bytes 2/3)
            uint correct = XI.Map.S2cEmoteList.GetJobUnused(b);
            // a buggy byte-2-only reader (the field's LSB byte) keeps only bits 22..23 (the low byte's top 2 bits).
            uint perByte = (uint)((b[XI.Map.S2cEmoteList.OffJobEmotes + XI.Map.JobEmotesEntry.UnusedBit / 8]
                                   >> (XI.Map.JobEmotesEntry.UnusedBit % 8)) & 0xFF);
            Check("EMO uint32 :10 straddle: correct whole-word read == 0x3FF", ((uint)0x3FF).ToString(), correct.ToString());
            Check("EMO uint32 :10 straddle: byte-2-only read < 0x3FF (DROPS bits 24..31) -- differs", "1",
                (perByte != correct ? 1 : 0).ToString());
        }

        // ==== RMW preserves siblings in each unit + cross-unit isolation (jobemotes word vs chairemotes word) ====
        {
            var b = new byte[XI.Map.S2cEmoteList.SizeOf];
            XI.Map.S2cEmoteList.SetJobFlag(b, XI.Map.JobEmotesEntry.WARBit, 1);
            XI.Map.S2cEmoteList.SetJobFlag(b, XI.Map.JobEmotesEntry.MNKBit, 1);
            XI.Map.S2cEmoteList.SetJobUnused(b, 0x2A);
            Check("EMO job RMW WAR still 1", "1", XI.Map.S2cEmoteList.GetJobWAR(b).ToString());
            Check("EMO job RMW MNK still 1", "1", XI.Map.S2cEmoteList.GetJobMNK(b).ToString());
            Check("EMO job RMW unused reads back 0x2A", ((uint)0x2A).ToString(), XI.Map.S2cEmoteList.GetJobUnused(b).ToString());
            // packed uint32 == compiler intra-bit-positions composed.
            ulong expectJe = (1UL << XI.Map.JobEmotesEntry.WARBit) | (1UL << XI.Map.JobEmotesEntry.MNKBit) | (0x2AUL << XI.Map.JobEmotesEntry.UnusedBit);
            Check("EMO job RMW packed uint32 == compiler intra-bit-positions composed", expectJe.ToString(), XI.Map.S2cEmoteList.GetJobWord(b).ToString());
            // the uint32 jobemotes writes must NOT touch the uint16 chairemotes word (@4) -- separate allocation units.
            Check("EMO writing jobemotes leaves the chairemotes uint16 word zero (cross-unit isolation)", "0",
                XI.Map.S2cEmoteList.GetChairWord(b).ToString());

            XI.Map.S2cEmoteList.SetChairFlag(b, XI.Map.ChairEmotesEntry.Chair1Bit, 1);
            XI.Map.S2cEmoteList.SetChairFlag(b, XI.Map.ChairEmotesEntry.Chair9Bit, 1);   // high byte of the uint16 unit
            XI.Map.S2cEmoteList.SetChairUnused(b, 0x15);
            Check("EMO chair RMW Chair1 still 1", "1", XI.Map.S2cEmoteList.GetChair1(b).ToString());
            Check("EMO chair RMW Chair9 still 1 (bit 8)", "1", XI.Map.S2cEmoteList.GetChair9(b).ToString());
            Check("EMO chair RMW unused reads back 0x15", ((uint)0x15).ToString(), XI.Map.S2cEmoteList.GetChairUnused(b).ToString());
            ulong expectCh = (1UL << XI.Map.ChairEmotesEntry.Chair1Bit) | (1UL << XI.Map.ChairEmotesEntry.Chair9Bit) | (0x15UL << XI.Map.ChairEmotesEntry.UnusedBit);
            Check("EMO chair RMW packed uint16 == compiler intra-bit-positions composed", expectCh.ToString(), XI.Map.S2cEmoteList.GetChairWord(b).ToString());
            // the chairemotes writes must NOT disturb the jobemotes uint32 word.
            Check("EMO writing chairemotes leaves jobemotes uint32 unchanged", expectJe.ToString(), XI.Map.S2cEmoteList.GetJobWord(b).ToString());
        }

        // padding00 setter lands at the compiler offset, no neighbour (the chairemotes word before it) disturbed.
        {
            var b = new byte[XI.Map.S2cEmoteList.SizeOf];
            XI.Map.S2cEmoteList.SetPadding00(b, 0xBEEF);
            Check("EMO SetPadding00 lands at compiler offset 6", ((ushort)0xBEEF).ToString(),
                BitConverter.ToUInt16(b, (int)ParseNum(g["OFF_PD.padding00"])).ToString());
            Check("EMO SetPadding00 leaves the chairemotes uint16 word zero (neighbour isolation)", "0",
                XI.Map.S2cEmoteList.GetChairWord(b).ToString());
        }
    }

    static void VerifyMapS2cUnity(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_unity_golden.txt");
        Console.WriteLine("\n== S2c unity (0x110) parity: C# XI.Map vs layout oracle (first 24-bit uint32 bit-field + plain scalar + trailing uint8[6] in one default-packed body) ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c unity golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // a bit-field's LSB within the scanned struct == compiler off*8 + shift (no addressable storage for offsetof).
        long BfBit(string field) => ParseNum(g[$"BF_PD_UNITY_{field}_off"]) * 8 + ParseNum(g[$"BF_PD_UNITY_{field}_shift"]);

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; the engine adds the 4-byte header).
        Check("UNI SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- the outer body: DEFAULT packing, whole-struct align == max member align (the uint32 word -> 4) ----
        Check("UNI PD_SIZE", g["PD_SIZE"], XI.Map.S2cUnity.SizeOf.ToString());
        Check("UNI PD_ALIGN == 4 (from the uint32 bit-field word, NOT pack(1))", g["PD_ALIGN"], XI.Map.S2cUnity.AlignOf.ToString());
        Check("UNI hole-free: PAD_INSERTED == 0", "0", g["PAD_INSERTED"]);
        Check("UNI MEMBER_BYTES == PD_SIZE", g["MEMBER_BYTES"], g["PD_SIZE"]);
        // the two declared bit-fields exactly fill the uint32 allocation unit (24 + 8 == 32).
        Check("UNI sparks(24) + unused00(8) sum to the uint32 unit", "1", (XI.Map.S2cUnity.BitFieldsSumTo32() ? 1 : 0).ToString());

        // ---- the two bit-field intra-word bit positions AND widths vs the compiler ----
        Check("UNI sparks bit == compiler off*8+shift", BfBit("Sparks").ToString(), XI.Map.S2cUnity.SparksBit.ToString());
        Check("UNI sparks width == compiler", g["BF_PD_UNITY_Sparks_width"], XI.Map.S2cUnity.SparksWidth.ToString());
        Check("UNI unused00 bit == compiler off*8+shift", BfBit("unused00").ToString(), XI.Map.S2cUnity.Unused00Bit.ToString());
        Check("UNI unused00 width == compiler", g["BF_PD_UNITY_unused00_width"], XI.Map.S2cUnity.Unused00Width.ToString());
        // THE straddle fact for a 24-bit field: bits 0..23 cross the uint32's bytes 0/1/2 -> a whole-word read is mandatory.
        Check("UNI sparks (bit 0 width 24) straddles bytes 0/1/2 of the uint32", "1",
            (((XI.Map.S2cUnity.SparksBit % 8) + XI.Map.S2cUnity.SparksWidth > 8) ? 1 : 0).ToString());

        // ---- plain member compiler offsets == model Off* consts (two ways: model const == golden key directly) ----
        var plain = new (string key, int modelOff)[]
        {
            ("Deeds", XI.Map.S2cUnity.OffDeeds),
            ("Plaudits", XI.Map.S2cUnity.OffPlaudits),
            ("RoEUnityShared", XI.Map.S2cUnity.OffRoEUnityShared),
            ("RoEUnityLeader", XI.Map.S2cUnity.OffRoEUnityLeader),
            ("unknown0E", XI.Map.S2cUnity.OffUnknown0E),
        };
        foreach (var (key, modelOff) in plain)
            Check($"UNI OFF {key}", g[$"OFF_PD.{key}"], modelOff.ToString());
        Check("UNI unknown0E array length", g["ARR_unknown0E_LEN"], XI.Map.S2cUnity.Unknown0ELen.ToString());
        // the uint8[6] runs to the end: unknown0E + 6 == SizeOf; SizeOf % AlignOf == 0 -> natural tail.
        Check("UNI unknown0E + 6 == SizeOf (array reaches the end)", "1",
            (XI.Map.S2cUnity.OffUnknown0E + XI.Map.S2cUnity.Unknown0ELen == XI.Map.S2cUnity.SizeOf ? 1 : 0).ToString());
        Check("UNI SizeOf % AlignOf == 0 (natural tail)", "1",
            (XI.Map.S2cUnity.SizeOf % XI.Map.S2cUnity.AlignOf == 0 ? 1 : 0).ToString());

        // ==== whole-struct fill determinism (gapless i*7+3) ====
        byte[] pdFill = HexToBytes(g["PD_FILL"]);
        Check("UNI PD_FILL_SIZE", g["PD_FILL_SIZE"], pdFill.Length.ToString());
        {
            var local = new byte[pdFill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check("UNI PD deterministic fill (gapless i*7+3)", g["PD_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }

        // ---- bit-field accessors == raw whole-word read at the compiler offset (two independent ways) ----
        uint word = BitConverter.ToUInt32(pdFill, (int)ParseNum(g["OFF_PD.unknown0E"]) - 10); // word sits at body offset 0
        Check("UNI GetWord == raw uint32 at offset 0", word.ToString(), XI.Map.S2cUnity.GetWord(pdFill).ToString());
        Check("UNI GetSparks == raw (word >> 0 & 0xFFFFFF)",
            (word & ((1u << XI.Map.S2cUnity.SparksWidth) - 1u)).ToString(), XI.Map.S2cUnity.GetSparks(pdFill).ToString());
        Check("UNI GetUnused00 == raw (word >> 24 & 0xFF)",
            ((word >> XI.Map.S2cUnity.Unused00Bit) & 0xFFu).ToString(), XI.Map.S2cUnity.GetUnused00(pdFill).ToString());

        // ---- plain scalar accessors == raw BitConverter at the compiler offset ----
        Check("UNI GetU16 Deeds == raw at compiler offset",
            BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.Deeds"])).ToString(),
            XI.Map.S2cUnity.GetU16(pdFill, XI.Map.S2cUnity.OffDeeds).ToString());
        Check("UNI GetU16 Plaudits == raw at compiler offset",
            BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.Plaudits"])).ToString(),
            XI.Map.S2cUnity.GetU16(pdFill, XI.Map.S2cUnity.OffPlaudits).ToString());
        Check("UNI GetU8 RoEUnityShared == raw at compiler offset",
            pdFill[(int)ParseNum(g["OFF_PD.RoEUnityShared"])].ToString(),
            XI.Map.S2cUnity.GetU8(pdFill, XI.Map.S2cUnity.OffRoEUnityShared).ToString());
        Check("UNI GetU8 RoEUnityLeader == raw at compiler offset",
            pdFill[(int)ParseNum(g["OFF_PD.RoEUnityLeader"])].ToString(),
            XI.Map.S2cUnity.GetU8(pdFill, XI.Map.S2cUnity.OffRoEUnityLeader).ToString());
        Check("UNI GetUnknown0E slice == raw bytes 10..15 at compiler offset",
            BitConverter.ToString(pdFill, (int)ParseNum(g["OFF_PD.unknown0E"]), XI.Map.S2cUnity.Unknown0ELen),
            BitConverter.ToString(XI.Map.S2cUnity.GetUnknown0E(pdFill).ToArray()));

        // ==== THE 24-bit STRADDLE NEGATIVE check: a byte-0-only reader drops the 16 bits in bytes 1/2 ====
        {
            var b = new byte[XI.Map.S2cUnity.SizeOf];
            XI.Map.S2cUnity.SetSparks(b, 0xFFFFFFu);                 // all 24 bits set (spans bytes 0/1/2)
            uint correct = XI.Map.S2cUnity.GetSparks(b);             // whole-uint32 masked read == 0xFFFFFF
            // a buggy byte-0-only reader sees just the low 8 bits (0xFF), missing bytes 1/2.
            uint byte0Only = (uint)(b[XI.Map.S2cUnity.WordOffset] & 0xFF);
            Check("UNI 24-bit straddle: correct whole-word read == 0xFFFFFF", ((uint)0xFFFFFF).ToString(), correct.ToString());
            Check("UNI 24-bit straddle: byte-0-only read < 0xFFFFFF (DROPS bits 8..23) -- differs", "1",
                (byte0Only != correct ? 1 : 0).ToString());
        }

        // ==== RMW: the two bit-fields share one uint32 word -> each write must preserve the other (sibling isolation) ====
        {
            var b = new byte[XI.Map.S2cUnity.SizeOf];
            XI.Map.S2cUnity.SetSparks(b, 0x123456u);
            XI.Map.S2cUnity.SetUnused00(b, 0xABu);
            Check("UNI RMW sparks still 0x123456 after writing unused00", ((uint)0x123456).ToString(), XI.Map.S2cUnity.GetSparks(b).ToString());
            Check("UNI RMW unused00 still 0xAB after writing sparks", ((uint)0xAB).ToString(), XI.Map.S2cUnity.GetUnused00(b).ToString());
            uint expect = (0xABu << XI.Map.S2cUnity.Unused00Bit) | 0x123456u;
            Check("UNI RMW packed uint32 == compiler intra-bit-positions composed", expect.ToString(), XI.Map.S2cUnity.GetWord(b).ToString());
            // the uint32 word occupies bytes 0..3 only -> the first plain scalar (Deeds@4) must stay zero.
            Check("UNI writing the bit-field word leaves Deeds (offset 4) zero (word/scalar isolation)", "0",
                BitConverter.ToUInt16(b, XI.Map.S2cUnity.OffDeeds).ToString());
        }

        // ---- plain scalar setters land at the compiler offset without disturbing the word / neighbours ----
        {
            var b = new byte[XI.Map.S2cUnity.SizeOf];
            XI.Map.S2cUnity.SetU16(b, XI.Map.S2cUnity.OffDeeds, 0x1122);
            XI.Map.S2cUnity.SetU8(b, XI.Map.S2cUnity.OffRoEUnityLeader, 0x7F);
            Check("UNI SetU16 Deeds lands at compiler offset 4", ((ushort)0x1122).ToString(),
                BitConverter.ToUInt16(b, (int)ParseNum(g["OFF_PD.Deeds"])).ToString());
            Check("UNI SetU8 RoEUnityLeader lands at compiler offset 9", ((byte)0x7F).ToString(),
                b[(int)ParseNum(g["OFF_PD.RoEUnityLeader"])].ToString());
            Check("UNI scalar setters leave the uint32 bit-field word zero", "0", XI.Map.S2cUnity.GetWord(b).ToString());
        }

        // ---- the trailing uint8[6] slice setter touches exactly bytes 10..15, not the preceding leader byte ----
        {
            var b = new byte[XI.Map.S2cUnity.SizeOf];
            var v = new byte[] { 1, 2, 3, 4, 5, 6 };
            XI.Map.S2cUnity.SetUnknown0E(b, v);
            Check("UNI SetUnknown0E writes bytes 10..15", "01-02-03-04-05-06",
                BitConverter.ToString(XI.Map.S2cUnity.GetUnknown0E(b).ToArray()));
            Check("UNI SetUnknown0E leaves RoEUnityLeader (byte 9) untouched", "0",
                XI.Map.S2cUnity.GetU8(b, XI.Map.S2cUnity.OffRoEUnityLeader).ToString());
        }
    }

    static void VerifyMapS2cAbilRecast(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_abil_recast_golden.txt");
        Console.WriteLine("\n== S2c abil_recast (0x119) parity: C# XI.Map vs layout oracle (largest nested struct-array 31x8 + no-realign-after-array because stride is a multiple of 4) ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c abil_recast golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; the engine adds the 4-byte header).
        Check("ABR SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- the standalone element recasttimer_t: dense u16/u8/u8/u16/u16, align 2, hole-free ----
        Check("ABR RT_SIZE (element)", g["RT_SIZE"], XI.Map.RecastTimer.SizeOf.ToString());
        Check("ABR RT_ALIGN (element, 2 from the uint16s)", g["RT_ALIGN"], XI.Map.RecastTimer.AlignOf.ToString());
        Check("ABR RT element hole-free: RT_PAD_INSERTED == 0", "0", g["RT_PAD_INSERTED"]);
        Check("ABR RT_MEMBER_BYTES == RT_SIZE", g["RT_MEMBER_BYTES"], g["RT_SIZE"]);
        // every element field offset == model Off* const == compiler OFF_RT.*
        Check("ABR OFF_RT.Timer", g["OFF_RT.Timer"], XI.Map.RecastTimer.OffTimer.ToString());
        Check("ABR OFF_RT.Calc1", g["OFF_RT.Calc1"], XI.Map.RecastTimer.OffCalc1.ToString());
        Check("ABR OFF_RT.TimerId", g["OFF_RT.TimerId"], XI.Map.RecastTimer.OffTimerId.ToString());
        // THE element's no-silent-pad fact: Calc2 (u16) lands at byte 4 because the two u8 fill bytes 2..3 exactly.
        Check("ABR OFF_RT.Calc2 == 4 (u16 already 2-aligned after the two u8s, no pad)", g["OFF_RT.Calc2"], XI.Map.RecastTimer.OffCalc2.ToString());
        Check("ABR OFF_RT.Calc2 == Calc1 + 2 (running sum, dense)", (ParseNum(g["OFF_RT.Calc1"]) + 2).ToString(), XI.Map.RecastTimer.OffCalc2.ToString());
        Check("ABR OFF_RT.padding06", g["OFF_RT.padding06"], XI.Map.RecastTimer.OffPadding06.ToString());
        Check("ABR OFF_RT.padding06 + 2 == RT_SIZE (element reaches its end)", "1",
            (ParseNum(g["OFF_RT.padding06"]) + 2 == ParseNum(g["RT_SIZE"]) ? 1 : 0).ToString());

        // ---- the outer body: DEFAULT packing, whole-struct align == max member align (the two trailing uint32s -> 4) ----
        Check("ABR PD_SIZE", g["PD_SIZE"], XI.Map.S2cAbilRecast.SizeOf.ToString());
        Check("ABR PD_ALIGN == 4 (max member align, from the trailing uint32s)", g["PD_ALIGN"], XI.Map.S2cAbilRecast.AlignOf.ToString());
        Check("ABR PD whole-align > element align (2 -> 4, the trailing u32 raises it)", "1",
            (ParseNum(g["PD_ALIGN"]) > ParseNum(g["RT_ALIGN"]) ? 1 : 0).ToString());
        Check("ABR hole-free: PAD_INSERTED == 0", "0", g["PAD_INSERTED"]);
        Check("ABR MEMBER_BYTES == PD_SIZE", g["MEMBER_BYTES"], g["PD_SIZE"]);

        // ---- the array: stride == element size, no inter-element pad, biggest count yet (31) ----
        Check("ABR OFF_PD.Timers == 0", g["OFF_PD.Timers"], XI.Map.S2cAbilRecast.OffTimers.ToString());
        Check("ABR ARR_Timers_COUNT == 31 (largest struct-array yet)", g["ARR_Timers_COUNT"], XI.Map.S2cAbilRecast.TimerCount.ToString());
        Check("ABR ARR_Timers_STRIDE == RT_SIZE (element size == stride, no gap)", g["ARR_Timers_STRIDE"], XI.Map.S2cAbilRecast.TimerStride.ToString());
        Check("ABR ARR_Timers_STRIDE == RecastTimer.SizeOf (model)", g["ARR_Timers_STRIDE"], XI.Map.RecastTimer.SizeOf.ToString());
        Check("ABR Timers[1] == Timers[0] + stride (no inter-element pad)", (ParseNum(g["OFF_PD.Timers"]) + ParseNum(g["ARR_Timers_STRIDE"])).ToString(), g["OFF_PD.Timers[1]"]);
        Check("ABR Timers[30] == Timers[1] + 29*stride", (ParseNum(g["OFF_PD.Timers[1]"]) + 29 * ParseNum(g["ARR_Timers_STRIDE"])).ToString(), g["OFF_PD.Timers[30]"]);
        Check("ABR ARR_Timers_LEN == COUNT * STRIDE (248)", "1", (XI.Map.S2cAbilRecast.TimersLen == XI.Map.S2cAbilRecast.TimerCount * XI.Map.S2cAbilRecast.TimerStride ? 1 : 0).ToString());
        Check("ABR ARR_Timers_LEN == 248", g["ARR_Timers_LEN"], XI.Map.S2cAbilRecast.TimersLen.ToString());

        // ---- THE no-realign-after-array fact: stride 8 (multiple of 4) -> 31*8=248 is already 4-aligned ----
        Check("ABR MOUNT_OFFSET == TimersLen (248)", g["MOUNT_OFFSET"], g["ARR_Timers_LEN"]);
        Check("ABR OFF_PD.MountRecast == 248", g["OFF_PD.MountRecast"], XI.Map.S2cAbilRecast.OffMountRecast.ToString());
        Check("ABR OFF_PD.MountRecast == TimersLen (no pad between array and u32)", g["OFF_PD.MountRecast"], XI.Map.S2cAbilRecast.TimersLen.ToString());
        Check("ABR MOUNT_MOD4 == 0 (248 already 4-aligned, so NO realign)", "0", g["MOUNT_MOD4"]);
        // and the arithmetic reason it holds: the element stride 8 divides 4-alignment -> any count*8 is 4-aligned.
        Check("ABR stride 8 is a multiple of 4 (WHY any element count stays 4-aligned)", "1",
            (XI.Map.S2cAbilRecast.TimerStride % 4 == 0 ? 1 : 0).ToString());
        Check("ABR MountRecastId == MountRecast + 4", (ParseNum(g["OFF_PD.MountRecast"]) + 4).ToString(), XI.Map.S2cAbilRecast.OffMountRecastId.ToString());
        Check("ABR OFF_PD.MountRecastId == 252", g["OFF_PD.MountRecastId"], XI.Map.S2cAbilRecast.OffMountRecastId.ToString());
        // chain ends gapless: MountRecastId + 4 == SizeOf; SizeOf % AlignOf == 0 -> natural tail.
        Check("ABR MountRecastId + 4 == SizeOf", (ParseNum(g["OFF_PD.MountRecastId"]) + 4).ToString(), g["PD_SIZE"]);
        Check("ABR SizeOf % AlignOf == 0 (natural tail)", "1", (XI.Map.S2cAbilRecast.SizeOf % XI.Map.S2cAbilRecast.AlignOf == 0 ? 1 : 0).ToString());

        // ==== element / whole-struct fill determinism (gapless i*7+3) ====
        foreach (var (tag, sizeKey, fillKey) in new (string, string, string)[]
                 { ("RT", "RT_FILL_SIZE", "RT_FILL"), ("PD", "PD_FILL_SIZE", "PD_FILL") })
        {
            byte[] f = HexToBytes(g[fillKey]);
            Check($"ABR {tag}_FILL_SIZE", g[sizeKey], f.Length.ToString());
            var local = new byte[f.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check($"ABR {tag} deterministic fill (gapless i*7+3)", g[fillKey], BitConverter.ToString(local).Replace("-", ""));
        }
        byte[] pdFill = HexToBytes(g["PD_FILL"]);

        // ---- element field accessors == raw read at the compiler element-offset + inner offset (two independent ways) ----
        // element 0, element 1, and the LAST element 30 -- 30 proves the array stride carries through to the boundary.
        foreach (var idx in new int[] { 0, 1, 30 })
        {
            int elemBase = (int)(ParseNum(g["OFF_PD.Timers"]) + idx * ParseNum(g["ARR_Timers_STRIDE"]));
            Check($"ABR GetTimer[{idx}] == raw u16 at {elemBase}+off",
                BitConverter.ToUInt16(pdFill, elemBase + (int)ParseNum(g["OFF_RT.Timer"])).ToString(),
                XI.Map.S2cAbilRecast.GetTimer(pdFill, idx).ToString());
            Check($"ABR GetCalc1[{idx}] == raw u8",
                pdFill[elemBase + (int)ParseNum(g["OFF_RT.Calc1"])].ToString(),
                XI.Map.S2cAbilRecast.GetCalc1(pdFill, idx).ToString());
            Check($"ABR GetTimerId[{idx}] == raw u8",
                pdFill[elemBase + (int)ParseNum(g["OFF_RT.TimerId"])].ToString(),
                XI.Map.S2cAbilRecast.GetTimerId(pdFill, idx).ToString());
            Check($"ABR GetCalc2[{idx}] == raw u16 at {elemBase}+4",
                BitConverter.ToUInt16(pdFill, elemBase + (int)ParseNum(g["OFF_RT.Calc2"])).ToString(),
                XI.Map.S2cAbilRecast.GetCalc2(pdFill, idx).ToString());
            Check($"ABR GetPadding06[{idx}] == raw u16 at {elemBase}+6",
                BitConverter.ToUInt16(pdFill, elemBase + (int)ParseNum(g["OFF_RT.padding06"])).ToString(),
                XI.Map.S2cAbilRecast.GetPadding06(pdFill, idx).ToString());
        }
        // GetTimerRow slice == the raw 8 bytes at the compiler element base (element slice == compiler stride window).
        Check("ABR GetTimerRow[30] == raw bytes 240..247",
            BitConverter.ToString(pdFill, (int)(ParseNum(g["OFF_PD.Timers[30]"])), XI.Map.S2cAbilRecast.TimerStride),
            BitConverter.ToString(XI.Map.S2cAbilRecast.GetTimerRow(pdFill, 30).ToArray()));

        // ---- trailing uint32 accessors == raw at the compiler offset ----
        Check("ABR GetMountRecast == raw u32 at 248",
            BitConverter.ToUInt32(pdFill, (int)ParseNum(g["OFF_PD.MountRecast"])).ToString(),
            XI.Map.S2cAbilRecast.GetMountRecast(pdFill).ToString());
        Check("ABR GetMountRecastId == raw u32 at 252",
            BitConverter.ToUInt32(pdFill, (int)ParseNum(g["OFF_PD.MountRecastId"])).ToString(),
            XI.Map.S2cAbilRecast.GetMountRecastId(pdFill).ToString());

        // ==== setters land at the compiler element offset without disturbing the neighbouring fields ====
        {
            var b = new byte[XI.Map.S2cAbilRecast.SizeOf];
            XI.Map.S2cAbilRecast.SetCalc1(b, 5, 0x33);        // u8 at byte 2 of element 5 (the neighbour to preserve)
            XI.Map.S2cAbilRecast.SetTimer(b, 5, 0x1122);      // u16 at bytes 0..1 (must NOT clobber Calc1)
            XI.Map.S2cAbilRecast.SetCalc2(b, 5, 0x4455);      // u16 at bytes 4..5 (must NOT clobber Calc1)
            Check("ABR SetTimer[5] lands at compiler offset 40", ((ushort)0x1122).ToString(),
                BitConverter.ToUInt16(b, 5 * XI.Map.RecastTimer.SizeOf + (int)ParseNum(g["OFF_RT.Timer"])).ToString());
            Check("ABR SetCalc2[5] lands at compiler offset 44", ((ushort)0x4455).ToString(),
                BitConverter.ToUInt16(b, 5 * XI.Map.RecastTimer.SizeOf + (int)ParseNum(g["OFF_RT.Calc2"])).ToString());
            // the two u16 writes at bytes 0..1 and 4..5 leave the u8 Calc1 (byte 2) intact (intra-element isolation).
            Check("ABR u16 SetTimer/SetCalc2 preserve the adjacent u8 Calc1[5] == 0x33", ((byte)0x33).ToString(),
                XI.Map.S2cAbilRecast.GetCalc1(b, 5).ToString());
            // and must not touch the neighbouring elements.
            Check("ABR SetTimer[5] leaves Timer[6] zero (cross-element isolation)", "0", XI.Map.S2cAbilRecast.GetTimer(b, 6).ToString());
            // round-trips: every element field reads back what was written through the stride arithmetic.
            Check("ABR SetTimer[5] round-trip", ((ushort)0x1122).ToString(), XI.Map.S2cAbilRecast.GetTimer(b, 5).ToString());
            Check("ABR SetCalc2[5] round-trip", ((ushort)0x4455).ToString(), XI.Map.S2cAbilRecast.GetCalc2(b, 5).ToString());
        }

        // ---- the trailing u32 writes land at the compiler offset and don't disturb the array / each other ----
        {
            var b = new byte[XI.Map.S2cAbilRecast.SizeOf];
            XI.Map.S2cAbilRecast.SetMountRecast(b, 0xDEADBEEF);
            XI.Map.S2cAbilRecast.SetMountRecastId(b, 0x1234);
            Check("ABR SetMountRecast lands at compiler offset 248", ((uint)0xDEADBEEF).ToString(), XI.Map.S2cAbilRecast.GetMountRecast(b).ToString());
            Check("ABR SetMountRecastId lands at compiler offset 252", ((uint)0x1234).ToString(), XI.Map.S2cAbilRecast.GetMountRecastId(b).ToString());
            Check("ABR SetMountRecast leaves the last array element (30) zero", "0", XI.Map.S2cAbilRecast.GetTimer(b, 30).ToString());
            Check("ABR writing both mount u32s leaves the whole 248-byte array zero", "1",
                (System.Linq.Enumerable.All<byte>(XI.Map.S2cAbilRecast.GetTimerRow(b, 0).ToArray(), x => x == 0) &&
                 BitConverter.ToUInt64(b, 240) == 0UL ? 1 : 0).ToString());
        }
    }

    static void VerifyMapS2cItemMax(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_item_max_golden.txt");
        Console.WriteLine("\n== S2c item_max (0x01C) parity: C# XI.Map vs layout oracle (first fixed uint16[] array member + compiler-decides-u16-array-alignment) ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c item_max golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; the engine adds the 4-byte header).
        Check("ITM SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- the outer body: DEFAULT packing, whole-struct align == 2 (the uint16 element, NOT a uint32 4) ----
        Check("ITM PD_SIZE", g["PD_SIZE"], XI.Map.S2cItemMax.SizeOf.ToString());
        Check("ITM PD_ALIGN == 2 (from the uint16 ItemNum2 elements)", g["PD_ALIGN"], XI.Map.S2cItemMax.AlignOf.ToString());
        Check("ITM hole-free: PAD_INSERTED == 0", "0", g["PAD_INSERTED"]);
        Check("ITM MEMBER_BYTES == PD_SIZE", g["MEMBER_BYTES"], g["PD_SIZE"]);
        Check("ITM SizeOf % AlignOf == 0 (natural tail)", "1", (XI.Map.S2cItemMax.SizeOf % XI.Map.S2cItemMax.AlignOf == 0 ? 1 : 0).ToString());

        // ---- ItemNum[18] (uint8): offset + element stride 1 (no inter-element pad) + count/len/elemsize ----
        Check("ITM OFF ItemNum", g["OFF_PD.ItemNum"], XI.Map.S2cItemMax.OffItemNum.ToString());
        Check("ITM ItemNum[1] == ItemNum + 1 (u8 stride)", (ParseNum(g["OFF_PD.ItemNum"]) + 1).ToString(), g["OFF_PD.ItemNum[1]"]);
        Check("ITM ARR_ItemNum_COUNT", g["ARR_ItemNum_COUNT"], XI.Map.S2cItemMax.ItemNumCount.ToString());
        Check("ITM ARR_ItemNum_LEN == COUNT (u8, elemsize 1)", g["ARR_ItemNum_LEN"], XI.Map.S2cItemMax.ItemNumCount.ToString());
        Check("ITM ARR_ItemNum_ELEMSIZE == 1", g["ARR_ItemNum_ELEMSIZE"], "1");

        // ---- padding00[14] (uint8): the declared real-bytes segment that brings the running u8 offset to 32 ----
        Check("ITM OFF padding00 == ItemNum + 18", (ParseNum(g["OFF_PD.ItemNum"]) + 18).ToString(), XI.Map.S2cItemMax.OffPadding00.ToString());
        Check("ITM ARR_padding00_COUNT", g["ARR_padding00_COUNT"], XI.Map.S2cItemMax.Padding00Len.ToString());

        // ---- THE first fixed uint16[] member: base 2-alignment is decided by the compiler, NOT by hand ----
        Check("ITM OFF ItemNum2 == 32", g["OFF_PD.ItemNum2"], XI.Map.S2cItemMax.OffItemNum2.ToString());
        // no-silent-pad proof: the u16 array base equals the running u8 offset (18+14==32), which is already 2-aligned.
        Check("ITM ItemNum2 == U8_RUN_OFFSET (32, the running u8 byte offset)", g["OFF_PD.ItemNum2"], g["U8_RUN_OFFSET"]);
        Check("ITM U8_RUN_OFFSET == ItemNum + ItemNum_LEN + padding00_LEN (0+18+14)", (ParseNum(g["OFF_PD.ItemNum"]) + ParseNum(g["ARR_ItemNum_LEN"]) + ParseNum(g["ARR_padding00_LEN"])).ToString(), g["U8_RUN_OFFSET"]);
        Check("ITM the running u8 offset 32 is 2-aligned (so NO pad before the u16 array)", "0", (ParseNum(g["OFF_PD.ItemNum2"]) % 2).ToString());
        // stride 2, no inter-element pad; count 18, elemsize 2, len 36.
        Check("ITM ItemNum2[1] == ItemNum2 + 2 (u16 stride, no gap)", (ParseNum(g["OFF_PD.ItemNum2"]) + 2).ToString(), g["OFF_PD.ItemNum2[1]"]);
        Check("ITM ARR_ItemNum2_COUNT == 18", g["ARR_ItemNum2_COUNT"], XI.Map.S2cItemMax.ItemNum2Count.ToString());
        Check("ITM ARR_ItemNum2_ELEMSIZE == 2", g["ARR_ItemNum2_ELEMSIZE"], XI.Map.S2cItemMax.ItemNum2Stride.ToString());
        Check("ITM ARR_ItemNum2_LEN == COUNT * 2 (36)", (ParseNum(g["ARR_ItemNum2_COUNT"]) * 2).ToString(), g["ARR_ItemNum2_LEN"]);

        // ---- padding01[28] (uint8): reaches the end (32 + 36 == 68, + 28 == 96 == SizeOf) ----
        Check("ITM OFF padding01 == ItemNum2 + 36", (ParseNum(g["OFF_PD.ItemNum2"]) + 36).ToString(), XI.Map.S2cItemMax.OffPadding01.ToString());
        Check("ITM ARR_padding01_COUNT", g["ARR_padding01_COUNT"], XI.Map.S2cItemMax.Padding01Len.ToString());
        Check("ITM padding01 + 28 == SizeOf (array chain reaches the end, gapless)", "1",
            (XI.Map.S2cItemMax.OffPadding01 + XI.Map.S2cItemMax.Padding01Len == XI.Map.S2cItemMax.SizeOf ? 1 : 0).ToString());

        // ==== whole-struct fill determinism (gapless i*7+3 over the 96-byte body) ====
        byte[] pdFill = HexToBytes(g["PD_FILL"]);
        Check("ITM PD_FILL_SIZE", g["PD_FILL_SIZE"], pdFill.Length.ToString());
        {
            var local = new byte[pdFill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check("ITM PD deterministic fill (gapless i*7+3, proves no internal pad)", g["PD_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }

        // ---- u8 element accessors == raw at the compiler offset (two independent ways) ----
        foreach (var idx in new int[] { 0, 1, 17 })
        {
            int off = (int)(ParseNum(g["OFF_PD.ItemNum"]) + idx);
            Check($"ITM GetItemNum[{idx}] == raw u8 at {off}", pdFill[off].ToString(), XI.Map.S2cItemMax.GetItemNum(pdFill, idx).ToString());
        }
        // ---- the first uint16[] element accessors == raw BitConverter at the compiler base + index*stride ----
        foreach (var idx in new int[] { 0, 1, 17 })
        {
            int off = (int)(ParseNum(g["OFF_PD.ItemNum2"]) + idx * ParseNum(g["ARR_ItemNum2_ELEMSIZE"]));
            Check($"ITM GetItemNum2[{idx}] == raw u16 at {off}",
                BitConverter.ToUInt16(pdFill, off).ToString(), XI.Map.S2cItemMax.GetItemNum2(pdFill, idx).ToString());
        }
        // ---- padding slices == raw byte ranges at the compiler offsets ----
        Check("ITM GetPadding00 == raw bytes 18..31", BitConverter.ToString(pdFill, (int)ParseNum(g["OFF_PD.padding00"]), XI.Map.S2cItemMax.Padding00Len),
            BitConverter.ToString(XI.Map.S2cItemMax.GetPadding00(pdFill).ToArray()));
        Check("ITM GetPadding01 == raw bytes 68..95", BitConverter.ToString(pdFill, (int)ParseNum(g["OFF_PD.padding01"]), XI.Map.S2cItemMax.Padding01Len),
            BitConverter.ToString(XI.Map.S2cItemMax.GetPadding01(pdFill).ToArray()));
        Check("ITM GetItemNumSlice == raw bytes 0..17", BitConverter.ToString(pdFill, 0, XI.Map.S2cItemMax.ItemNumCount),
            BitConverter.ToString(XI.Map.S2cItemMax.GetItemNumSlice(pdFill).ToArray()));

        // ==== setters land at the compiler offset without disturbing neighbours ====
        {
            var b = new byte[XI.Map.S2cItemMax.SizeOf];
            XI.Map.S2cItemMax.SetItemNum(b, 5, 0x33);            // u8 @ 0+5
            XI.Map.S2cItemMax.SetItemNum2(b, 3, 0x1122);          // u16 @ 32 + 3*2 == 38
            XI.Map.S2cItemMax.SetItemNum2(b, 17, 0xABCD);         // u16 @ 32 + 17*2 == 66 (last element)
            Check("ITM SetItemNum[5] lands at compiler offset 5", ((byte)0x33).ToString(),
                b[(int)(ParseNum(g["OFF_PD.ItemNum"]) + 5)].ToString());
            Check("ITM SetItemNum2[3] lands at compiler offset 38", ((ushort)0x1122).ToString(),
                BitConverter.ToUInt16(b, (int)(ParseNum(g["OFF_PD.ItemNum2"]) + 6)).ToString());
            Check("ITM SetItemNum2[17] lands at compiler offset 66 (last u16 element)", ((ushort)0xABCD).ToString(),
                BitConverter.ToUInt16(b, (int)(ParseNum(g["OFF_PD.ItemNum2"]) + 34)).ToString());
            // a u16 write into the ItemNum2 array must not spill into the trailing padding01[0] (byte 68).
            Check("ITM SetItemNum2[17] leaves padding01[0] (byte 68) zero", "0",
                XI.Map.S2cItemMax.GetU8(b, (int)ParseNum(g["OFF_PD.padding01"])).ToString());
            // and a u8 write into ItemNum must not touch the u16 array base.
            Check("ITM SetItemNum[5] leaves ItemNum2[0] (byte 32) zero", "0", XI.Map.S2cItemMax.GetItemNum2(b, 0).ToString());
            // round-trips through the stride arithmetic.
            Check("ITM SetItemNum2[3] round-trip", ((ushort)0x1122).ToString(), XI.Map.S2cItemMax.GetItemNum2(b, 3).ToString());
        }
    }

    static void VerifyMapS2cNaraku(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_naraku_golden.txt");
        Console.WriteLine("\n== S2c naraku (0x006) parity: C# XI.Map vs framing oracle (FIRST empty PacketData -- C++ sizeof==1 artifact vs the is_empty_v header-only wire branch) ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c naraku golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // shared S2C header framing == C2S/CBasicPacket (the empty packet still carries the 4-byte header).
        Check("NRK SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- the empty struct: C++ sizeof/align are LANGUAGE ARTIFACTS, the wire body contribution is ZERO ----
        Check("NRK PD_SIZE == 1 (the C++ empty-struct sizeof artifact)", g["PD_SIZE"], XI.Map.S2cNaraku.CppSizeOfEmptyStruct.ToString());
        Check("NRK PD_ALIGN", g["PD_ALIGN"], XI.Map.S2cNaraku.AlignOf.ToString());
        Check("NRK PD_WIRE_BODY == 0 (the real body bytes on the wire)", g["PD_WIRE_BODY"], XI.Map.S2cNaraku.BodySize.ToString());
        // the discriminator std::is_empty_v<PacketData> is what the engine's ctor branches on -> header-only.
        Check("NRK PD_IS_EMPTY == 1", "1", (XI.Map.S2cNaraku.IsHeaderOnly ? 1 : 0).ToString());
        Check("NRK PD_IS_EMPTY matches the compiler", g["PD_IS_EMPTY"], "1");

        // ---- THE load-bearing distinction: the framed packet is header-only (4), NOT the naive header + sizeof(1) (5) ----
        Check("NRK PD_PACKET_SIZE == 4 (the engine's is_empty_v setSize branch)", g["PD_PACKET_SIZE"], XI.Map.S2cNaraku.FramedPacketSize.ToString());
        Check("NRK PD_NAIVE_HDR_PLUS_SIZEOF == 5 (the spurious-byte trap value the oracle pins)", g["PD_NAIVE_HDR_PLUS_SIZEOF"], "5");
        // negative proof: the real framed size differs from the naive header+sizeof sum -- a naive port would desync.
        Check("NRK framed packet (4) != naive header+sizeof(empty) (5): the is_empty_v branch wins", "1",
            (XI.Map.S2cNaraku.FramedPacketSize != (int)ParseNum(g["PD_NAIVE_HDR_PLUS_SIZEOF"]) ? 1 : 0).ToString());
        // the wire body contribution (0) differs from the C++ sizeof artifact (1): the core C++-vs-wire fact.
        Check("NRK BodySize (wire 0) != CppSizeOfEmptyStruct (C++ 1)", "1",
            (XI.Map.S2cNaraku.BodySize != XI.Map.S2cNaraku.CppSizeOfEmptyStruct ? 1 : 0).ToString());
        // the framed packet == header only == C2sPos.HeaderSize, and == header + BodySize(0).
        Check("NRK FramedPacketSize == C2sPos.HeaderSize", XI.Map.C2sPos.HeaderSize.ToString(), XI.Map.S2cNaraku.FramedPacketSize.ToString());
        Check("NRK FramedPacketSize == HeaderSize + BodySize(0)", "1",
            (XI.Map.S2cNaraku.FramedPacketSize == XI.Map.S2cNaraku.HeaderSize + XI.Map.S2cNaraku.BodySize ? 1 : 0).ToString());

        // ==== the NON-EMPTY uint8 twin for the discriminator contrast (sizeof 1 but is_empty_v == false -> sum branch) ====
        Check("NRK TWIN_SIZE == 1 (one uint8 member)", g["TWIN_SIZE"], "1");
        Check("NRK TWIN_IS_EMPTY == 0 (the discriminator differs from the empty struct)", g["TWIN_IS_EMPTY"], "0");
        // empty struct and one-byte struct BOTH have C++ sizeof 1, yet only naraku is header-only:
        Check("NRK empty (4) and non-empty twin (5) framed sizes differ though both sizeof==1", "1",
            (ParseNum(g["PD_PACKET_SIZE"]) != ParseNum(g["TWIN_PACKET_SIZE"]) ? 1 : 0).ToString());
        // the twin DOES use the header + sizeof sum branch (4 + 1 == 5).
        Check("NRK TWIN_PACKET_SIZE == SHDR_SIZE + TWIN_SIZE (the else branch, 5)", "1",
            (ParseNum(g["TWIN_PACKET_SIZE"]) == ParseNum(g["SHDR_SIZE"]) + ParseNum(g["TWIN_SIZE"]) ? 1 : 0).ToString());
        Check("NRK TWIN_WIRE_BODY == 1 (a non-empty struct's C++ sizeof IS its wire body)", g["TWIN_WIRE_BODY"], "1");
    }

    static void VerifyMapS2cPacketControl(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_packet_control_golden.txt");
        Console.WriteLine("\n== S2c packet_control (0x005) parity: C# XI.Map vs layout oracle (first native uint32 scalar + uint32[] array body, dense DEFAULT-packed 24/4) ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c packet_control golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; the engine adds the 4-byte header).
        Check("PC SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- the outer body: DEFAULT packing, whole-struct align == 4 (the uint32 members), dense/hole-free ----
        Check("PC PD_SIZE", g["PD_SIZE"], XI.Map.S2cPacketControl.SizeOf.ToString());
        Check("PC PD_ALIGN == 4 (from the uint32 members)", g["PD_ALIGN"], XI.Map.S2cPacketControl.AlignOf.ToString());
        Check("PC hole-free: PAD_INSERTED == 0", "0", g["PAD_INSERTED"]);
        Check("PC MEMBER_BYTES == PD_SIZE", g["MEMBER_BYTES"], g["PD_SIZE"]);
        Check("PC SizeOf % AlignOf == 0 (natural tail)", "1", (XI.Map.S2cPacketControl.SizeOf % XI.Map.S2cPacketControl.AlignOf == 0 ? 1 : 0).ToString());

        // ---- uint32 PacketCnt (the leading scalar): offset 0 ----
        Check("PC OFF PacketCnt", g["OFF_PD.PacketCnt"], XI.Map.S2cPacketControl.OffPacketCnt.ToString());

        // ---- THE first native uint32[] S2C body member: base 4-alignment decided by the compiler, NOT by hand ----
        Check("PC OFF padding08 == 4", g["OFF_PD.padding08"], XI.Map.S2cPacketControl.OffPadding08.ToString());
        // no-silent-pad proof: the u32 array base equals the running offset after the scalar (4), already 4-aligned.
        Check("PC padding08 == SCALAR_RUN_OFFSET (4, the running byte offset)", g["OFF_PD.padding08"], g["SCALAR_RUN_OFFSET"]);
        Check("PC SCALAR_RUN_OFFSET == sizeof(PacketCnt) (4)", g["SCALAR_RUN_OFFSET"], "4");
        Check("PC the running offset 4 is 4-aligned (so NO pad before the u32 array)", "0", (ParseNum(g["OFF_PD.padding08"]) % 4).ToString());
        // stride 4, no inter-element pad; count 5, elemsize 4, len 20.
        Check("PC padding08[1] == padding08 + 4 (u32 stride, no gap)", (ParseNum(g["OFF_PD.padding08"]) + 4).ToString(), g["OFF_PD.padding08[1]"]);
        Check("PC ARR_padding08_COUNT == 5", g["ARR_padding08_COUNT"], XI.Map.S2cPacketControl.Padding08Count.ToString());
        Check("PC ARR_padding08_ELEMSIZE == 4", g["ARR_padding08_ELEMSIZE"], XI.Map.S2cPacketControl.Padding08Stride.ToString());
        Check("PC ARR_padding08_LEN == COUNT * 4 (20)", (ParseNum(g["ARR_padding08_COUNT"]) * 4).ToString(), g["ARR_padding08_LEN"]);
        Check("PC ARR_padding08_LEN == model Padding08Len", g["ARR_padding08_LEN"], XI.Map.S2cPacketControl.Padding08Len.ToString());
        // the array reaches the end: base 4 + len 20 == 24 == SizeOf.
        Check("PC padding08 + LEN == SizeOf (24, gapless to the end)", "1",
            (XI.Map.S2cPacketControl.OffPadding08 + XI.Map.S2cPacketControl.Padding08Len == XI.Map.S2cPacketControl.SizeOf ? 1 : 0).ToString());

        // ==== whole-struct fill determinism (gapless i*7+3 over the 24-byte body) ====
        byte[] pdFill = HexToBytes(g["PD_FILL"]);
        Check("PC PD_FILL_SIZE", g["PD_FILL_SIZE"], pdFill.Length.ToString());
        {
            var local = new byte[pdFill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check("PC PD deterministic fill (gapless i*7+3, proves no internal pad)", g["PD_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }

        // ---- scalar + u32[] element accessors == raw BitConverter at the compiler offsets (two independent ways) ----
        Check("PC GetPacketCnt == raw u32 at compiler offset 0",
            BitConverter.ToUInt32(pdFill, (int)ParseNum(g["OFF_PD.PacketCnt"])).ToString(), XI.Map.S2cPacketControl.GetPacketCnt(pdFill).ToString());
        foreach (var idx in new int[] { 0, 1, 4 })
        {
            int off = (int)(ParseNum(g["OFF_PD.padding08"]) + idx * ParseNum(g["ARR_padding08_ELEMSIZE"]));
            Check($"PC GetPadding08[{idx}] == raw u32 at {off}",
                BitConverter.ToUInt32(pdFill, off).ToString(), XI.Map.S2cPacketControl.GetPadding08(pdFill, idx).ToString());
        }
        // the Padding08Offset helper's stride arithmetic agrees with the compiler base + index*elemsize.
        foreach (var idx in new int[] { 0, 2, 4 })
        {
            int compilerOff = (int)(ParseNum(g["OFF_PD.padding08"]) + idx * ParseNum(g["ARR_padding08_ELEMSIZE"]));
            Check($"PC Padding08Offset({idx}) == compiler base + {idx}*stride", compilerOff.ToString(), XI.Map.S2cPacketControl.Padding08Offset(idx).ToString());
        }
        // ---- the padding slice == raw byte range 4..23 at the compiler offsets ----
        Check("PC GetPadding08Slice == raw bytes 4..23", BitConverter.ToString(pdFill, (int)ParseNum(g["OFF_PD.padding08"]), XI.Map.S2cPacketControl.Padding08Len),
            BitConverter.ToString(XI.Map.S2cPacketControl.GetPadding08Slice(pdFill).ToArray()));

        // ==== setters land at the compiler offset without disturbing neighbours ====
        {
            var b = new byte[XI.Map.S2cPacketControl.SizeOf];
            XI.Map.S2cPacketControl.SetPacketCnt(b, 0xDEADBEEF);   // u32 @ 0
            XI.Map.S2cPacketControl.SetPadding08(b, 3, 0x11223344); // u32 @ 4 + 3*4 == 16
            XI.Map.S2cPacketControl.SetPadding08(b, 4, 0xAABBCCDD); // u32 @ 4 + 4*4 == 20 (last element)
            Check("PC SetPacketCnt lands at compiler offset 0", ((uint)0xDEADBEEF).ToString(),
                BitConverter.ToUInt32(b, (int)ParseNum(g["OFF_PD.PacketCnt"])).ToString());
            Check("PC SetPadding08[3] lands at compiler offset 16", ((uint)0x11223344).ToString(),
                BitConverter.ToUInt32(b, (int)(ParseNum(g["OFF_PD.padding08"]) + 12)).ToString());
            Check("PC SetPadding08[4] lands at compiler offset 20 (last u32 element)", ((uint)0xAABBCCDD).ToString(),
                BitConverter.ToUInt32(b, (int)(ParseNum(g["OFF_PD.padding08"]) + 16)).ToString());
            // the scalar write must not spill into padding08[0] (byte 4).
            Check("PC SetPacketCnt leaves padding08[0] (byte 4) zero", "0", XI.Map.S2cPacketControl.GetPadding08(b, 0).ToString());
            // an array element write must not touch the leading scalar.
            Check("PC SetPadding08[3] leaves PacketCnt untouched (still 0xDEADBEEF)", ((uint)0xDEADBEEF).ToString(), XI.Map.S2cPacketControl.GetPacketCnt(b).ToString());
            // round-trips through the stride arithmetic.
            Check("PC SetPadding08[3] round-trip", ((uint)0x11223344).ToString(), XI.Map.S2cPacketControl.GetPadding08(b, 3).ToString());
            Check("PC SetPacketCnt round-trip", ((uint)0xDEADBEEF).ToString(), XI.Map.S2cPacketControl.GetPacketCnt(b).ToString());
        }
    }

    static void VerifyMapS2cEnterZone(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_enterzone_golden.txt");
        Console.WriteLine("\n== S2c enterzone (0x008) parity: C# XI.Map vs layout oracle (FIRST whole-body-is-one-uint8[]-array, pure u8 alignof 1, 48 bytes) ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c enterzone golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; the engine adds the 4-byte header).
        Check("ENZ SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- the outer body: DEFAULT packing, a PURE uint8 body so alignof == 1, dense/hole-free ----
        Check("ENZ PD_SIZE", g["PD_SIZE"], XI.Map.S2cEnterZone.SizeOf.ToString());
        Check("ENZ PD_ALIGN == 1 (pure uint8 body, no u16/u32 raises it)", g["PD_ALIGN"], XI.Map.S2cEnterZone.AlignOf.ToString());
        Check("ENZ hole-free: PAD_INSERTED == 0", "0", g["PAD_INSERTED"]);
        Check("ENZ MEMBER_BYTES == PD_SIZE", g["MEMBER_BYTES"], g["PD_SIZE"]);
        Check("ENZ SizeOf % AlignOf == 0 (natural tail)", "1", (XI.Map.S2cEnterZone.SizeOf % XI.Map.S2cEnterZone.AlignOf == 0 ? 1 : 0).ToString());

        // ---- THE whole body is ONE uint8[] array: the compiler proves sizeof(PD) == sizeof(array) ----
        Check("ENZ BODY_IS_PURE_ARRAY == 1 (the array IS the entire PacketData)", "1", g["BODY_IS_PURE_ARRAY"]);
        Check("ENZ sizeof(array) LEN == PD_SIZE (whole body == the array)", g["ARR_EnterZoneTbl_LEN"], g["PD_SIZE"]);

        // ---- uint8 EnterZoneTbl[48]: offset 0 + element stride 1 (no inter-element pad) + count/len/elemsize ----
        Check("ENZ OFF EnterZoneTbl", g["OFF_PD.EnterZoneTbl"], XI.Map.S2cEnterZone.OffEnterZoneTbl.ToString());
        Check("ENZ EnterZoneTbl[1] == EnterZoneTbl + 1 (u8 stride)", (ParseNum(g["OFF_PD.EnterZoneTbl"]) + 1).ToString(), g["OFF_PD.EnterZoneTbl[1]"]);
        Check("ENZ ARR_EnterZoneTbl_COUNT", g["ARR_EnterZoneTbl_COUNT"], XI.Map.S2cEnterZone.EnterZoneTblCount.ToString());
        Check("ENZ ARR_EnterZoneTbl_LEN == COUNT (u8, elemsize 1)", g["ARR_EnterZoneTbl_LEN"], XI.Map.S2cEnterZone.EnterZoneTblCount.ToString());
        Check("ENZ ARR_EnterZoneTbl_ELEMSIZE == 1", g["ARR_EnterZoneTbl_ELEMSIZE"], "1");
        Check("ENZ model EnterZoneTblLen == golden LEN", g["ARR_EnterZoneTbl_LEN"], XI.Map.S2cEnterZone.EnterZoneTblLen.ToString());
        // the single array reaches the end: base 0 + len 48 == 48 == SizeOf.
        Check("ENZ EnterZoneTbl + LEN == SizeOf (gapless to the end)", "1",
            (XI.Map.S2cEnterZone.OffEnterZoneTbl + XI.Map.S2cEnterZone.EnterZoneTblLen == XI.Map.S2cEnterZone.SizeOf ? 1 : 0).ToString());

        // ==== whole-struct fill determinism (gapless i*7+3 over the 48-byte body) ====
        byte[] pdFill = HexToBytes(g["PD_FILL"]);
        Check("ENZ PD_FILL_SIZE", g["PD_FILL_SIZE"], pdFill.Length.ToString());
        {
            var local = new byte[pdFill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check("ENZ PD deterministic fill (gapless i*7+3, proves no internal pad)", g["PD_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }

        // ---- the EnterZoneTblOffset helper's stride arithmetic agrees with the compiler base + index*elemsize ----
        foreach (var idx in new int[] { 0, 24, 47 })
        {
            int compilerOff = (int)(ParseNum(g["OFF_PD.EnterZoneTbl"]) + idx * ParseNum(g["ARR_EnterZoneTbl_ELEMSIZE"]));
            Check($"ENZ EnterZoneTblOffset({idx}) == compiler base + {idx}*stride", compilerOff.ToString(), XI.Map.S2cEnterZone.EnterZoneTblOffset(idx).ToString());
        }

        // ---- u8 element accessors == raw at the compiler offset (two independent ways) ----
        foreach (var idx in new int[] { 0, 1, 24, 47 })
        {
            int off = (int)(ParseNum(g["OFF_PD.EnterZoneTbl"]) + idx * ParseNum(g["ARR_EnterZoneTbl_ELEMSIZE"]));
            Check($"ENZ GetEnterZoneTbl[{idx}] == raw u8 at {off}", pdFill[off].ToString(), XI.Map.S2cEnterZone.GetEnterZoneTbl(pdFill, idx).ToString());
        }
        // ---- the whole-body slice == raw bytes 0..47 (BODY_IS_PURE_ARRAY: the slice IS the body) ----
        Check("ENZ GetEnterZoneTblSlice == raw bytes 0..47 (whole body)", BitConverter.ToString(pdFill, (int)ParseNum(g["OFF_PD.EnterZoneTbl"]), XI.Map.S2cEnterZone.EnterZoneTblLen),
            BitConverter.ToString(XI.Map.S2cEnterZone.GetEnterZoneTblSlice(pdFill).ToArray()));
        Check("ENZ GetEnterZoneTblSlice length == SizeOf (48)", XI.Map.S2cEnterZone.SizeOf.ToString(), XI.Map.S2cEnterZone.GetEnterZoneTblSlice(pdFill).Length.ToString());

        // ==== setters land at the compiler offset without disturbing neighbours ====
        {
            var b = new byte[XI.Map.S2cEnterZone.SizeOf];
            XI.Map.S2cEnterZone.SetEnterZoneTbl(b, 0, 0x11);    // u8 @ 0
            XI.Map.S2cEnterZone.SetEnterZoneTbl(b, 24, 0x22);   // u8 @ 24
            XI.Map.S2cEnterZone.SetEnterZoneTbl(b, 47, 0x33);   // u8 @ 47 (last element)
            Check("ENZ SetEnterZoneTbl[0] lands at compiler offset 0", ((byte)0x11).ToString(), b[0].ToString());
            Check("ENZ SetEnterZoneTbl[24] lands at compiler offset 24", ((byte)0x22).ToString(),
                b[(int)(ParseNum(g["OFF_PD.EnterZoneTbl"]) + 24)].ToString());
            Check("ENZ SetEnterZoneTbl[47] lands at compiler offset 47 (last u8 element)", ((byte)0x33).ToString(),
                b[(int)(ParseNum(g["OFF_PD.EnterZoneTbl"]) + 47)].ToString());
            // a single-byte write must not touch a neighbour (stride 1 -> adjacent byte is the next element, must stay zero).
            Check("ENZ SetEnterZoneTbl[24] leaves element 25 zero", "0", XI.Map.S2cEnterZone.GetEnterZoneTbl(b, 25).ToString());
            Check("ENZ SetEnterZoneTbl[0] leaves element 1 zero", "0", XI.Map.S2cEnterZone.GetEnterZoneTbl(b, 1).ToString());
            // round-trips through the stride arithmetic.
            Check("ENZ SetEnterZoneTbl[24] round-trip", ((byte)0x22).ToString(), XI.Map.S2cEnterZone.GetEnterZoneTbl(b, 24).ToString());
            Check("ENZ SetEnterZoneTbl[47] round-trip", ((byte)0x33).ToString(), XI.Map.S2cEnterZone.GetEnterZoneTbl(b, 47).ToString());
            // the whole-slice setter-free path: only 3 bytes are set, so the slice must have exactly 3 non-zero.
            int nz = 0;
            foreach (var byteVal in XI.Map.S2cEnterZone.GetEnterZoneTblSlice(b).ToArray()) if (byteVal != 0) nz++;
            Check("ENZ slice has exactly 3 set bytes after three element writes", "3", nz.ToString());
        }
    }

    static void VerifyMapS2cItemSame(string dir, string enumHeaderPath)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_item_same_enum_golden.txt");
        Console.WriteLine("\n== S2c item_same (0x01D) parity: C# XI.Map vs layout oracle + 0x01d_item_same.h enum re-parse (engine-enum + declared padding00[3] bridging to the trailing u32, vs a no-pad counterfactual) ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c item_same enum golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; the engine adds the 4-byte header).
        Check("IS SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- the outer body: DEFAULT packing, alignof 4 (the trailing uint32), dense/hole-free (declared padding fills the gap) ----
        Check("IS PD_SIZE", g["PD_SIZE"], XI.Map.S2cItemSame.SizeOf.ToString());
        Check("IS PD_ALIGN == 4 (from the trailing uint32 Flags)", g["PD_ALIGN"], XI.Map.S2cItemSame.AlignOf.ToString());
        Check("IS hole-free: PAD_INSERTED == 0 (declared padding00 fills the gap, no silent pad)", "0", g["PAD_INSERTED"]);
        Check("IS MEMBER_BYTES == PD_SIZE", g["MEMBER_BYTES"], g["PD_SIZE"]);
        Check("IS SizeOf % AlignOf == 0 (natural tail)", "1", (XI.Map.S2cItemSame.SizeOf % XI.Map.S2cItemSame.AlignOf == 0 ? 1 : 0).ToString());

        // ---- uint8 State (the enum member, layout == a plain uint8): offset 0 ----
        Check("IS OFF State", g["OFF_PD.State"], XI.Map.S2cItemSame.OffState.ToString());

        // ---- uint8 padding00[3]: offset 1 + element stride 1 (no inter-element pad) + count/len/elemsize ----
        Check("IS OFF padding00 == State + 1 (u8, no gap)", (ParseNum(g["OFF_PD.State"]) + 1).ToString(), XI.Map.S2cItemSame.OffPadding00.ToString());
        Check("IS padding00[1] == padding00 + 1 (u8 stride)", (ParseNum(g["OFF_PD.padding00"]) + 1).ToString(), g["OFF_PD.padding00[1]"]);
        Check("IS ARR_padding00_COUNT", g["ARR_padding00_COUNT"], XI.Map.S2cItemSame.Padding00Len.ToString());
        Check("IS ARR_padding00_ELEMSIZE == 1", g["ARR_padding00_ELEMSIZE"], "1");

        // ---- THE padding-bridge: State(1) + padding00(3) == the running offset the trailing u32 Flags needs (4) ----
        Check("IS OFF Flags == State + 1 + padding00_LEN (0+1+3)", (ParseNum(g["OFF_PD.State"]) + 1 + ParseNum(g["ARR_padding00_LEN"])).ToString(), XI.Map.S2cItemSame.OffFlags.ToString());
        Check("IS PADDING_BRIDGE (compiler State+padding run) == OFF Flags", g["PADDING_BRIDGE"], g["OFF_PD.Flags"]);
        Check("IS model PaddingBridge == compiler OFF Flags (4)", XI.Map.S2cItemSame.PaddingBridge.ToString(), g["OFF_PD.Flags"]);
        Check("IS the trailing u32 Flags is 4-aligned (so NO silent pad, PAD_INSERTED==0)", "0", (ParseNum(g["OFF_PD.Flags"]) % 4).ToString());

        // ---- chunk-21 engine-enum underlying-type substitution RE-CONFIRMED (uint8): the enum-member struct == uint8_t twin ----
        Check("IS enum underlying substitution ESIZE_EQ == 1", "1", g["ESIZE_EQ"]);
        Check("IS enum underlying substitution EALIGN_EQ == 1", "1", g["EALIGN_EQ"]);
        Check("IS enum underlying substitution ESTATE_OFF_EQ == 1", "1", g["ESTATE_OFF_EQ"]);

        // ---- THE genuinely-new pin: the NO-PADDING counterfactual reaches the SAME Flags@4 & SAME size 8 via 3 SILENT pad ----
        // real struct PAD_INSERTED==0 (declared padding00 fills the alignment gap); dropping it, Flags STILL at 4 but now
        // via the compiler's SILENT pad -> NOPAD_PAD_INSERTED==3. Same offset+size, different hole provenance.
        Check("IS NOPAD_SIZE == PD_SIZE (removing the declared padding does not change the size, 8)", g["NOPAD_SIZE"], g["PD_SIZE"]);
        Check("IS NOPAD_ALIGN == PD_ALIGN (4, still aligned by the u32)", g["NOPAD_ALIGN"], g["PD_ALIGN"]);
        Check("IS NOPAD_OFF_Flags == OFF Flags (the u32 lands at 4 whether the padding is declared or silent)", g["NOPAD_OFF_Flags"], g["OFF_PD.Flags"]);
        Check("IS real PAD_INSERTED 0 != NOPAD_PAD_INSERTED 3 (declared padding vs silent pad, same layout)", "1",
            (ParseNum(g["PAD_INSERTED"]) != ParseNum(g["NOPAD_PAD_INSERTED"]) ? 1 : 0).ToString());
        Check("IS NOPAD_PAD_INSERTED == 3 (the 3 bytes the declared padding00 absorbs)", "3", g["NOPAD_PAD_INSERTED"]);

        // ---- the two enum literals: C# enum underlying byte == compiler (uint8_t)-cast, cross-checked 3 ways ----
        Check("IS ENUM StillLoading (C# == compiler underlying-cast)", g["ENUM_StillLoading"], ((byte)XI.Map.ItemSameState.StillLoading).ToString());
        Check("IS ENUM AllLoaded (C# == compiler underlying-cast)", g["ENUM_AllLoaded"], ((byte)XI.Map.ItemSameState.AllLoaded).ToString());
        if (System.IO.File.Exists(enumHeaderPath))
        {
            var re = ParseCppEnum(System.IO.File.ReadAllText(enumHeaderPath), "GP_SERV_COMMAND_ITEM_SAME_STATE");
            Check("IS 0x01d_item_same.h re-parse count == 2", "2", re.Count.ToString());
            foreach (var (name, val) in re)
            {
                string key = "ENUM_" + name;
                if (g.ContainsKey(key))
                    Check($"IS re-parse {name} == compiler underlying value", g[key], val.ToString());
            }
        }
        else
        {
            Check("IS 0x01d_item_same.h re-parse target present", "present", "MISSING:" + enumHeaderPath);
        }
        VerifyPacketIds("GP_SERV_COMMAND_ITEM_SAME_STATE", enumHeaderPath, typeof(XI.Map.ItemSameState));

        // ==== whole-struct fill determinism (gapless i*7+3 over the 8-byte body) ====
        byte[] pdFill = HexToBytes(g["PD_FILL"]);
        Check("IS PD_FILL_SIZE", g["PD_FILL_SIZE"], pdFill.Length.ToString());
        {
            var local = new byte[pdFill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check("IS PD deterministic fill (gapless i*7+3, proves no internal pad)", g["PD_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }

        // ---- accessors == raw at the compiler offsets (two independent ways) ----
        Check("IS GetStateRaw == raw u8 at compiler offset 0", pdFill[(int)ParseNum(g["OFF_PD.State"])].ToString(), XI.Map.S2cItemSame.GetStateRaw(pdFill).ToString());
        Check("IS GetState (enum) == (ItemSameState) raw u8 at 0", ((XI.Map.ItemSameState)pdFill[(int)ParseNum(g["OFF_PD.State"])]).ToString(), XI.Map.S2cItemSame.GetState(pdFill).ToString());
        Check("IS GetPadding00 == raw bytes 1..3", BitConverter.ToString(pdFill, (int)ParseNum(g["OFF_PD.padding00"]), XI.Map.S2cItemSame.Padding00Len),
            BitConverter.ToString(XI.Map.S2cItemSame.GetPadding00(pdFill).ToArray()));
        Check("IS GetFlags == raw u32 at compiler offset 4", BitConverter.ToUInt32(pdFill, (int)ParseNum(g["OFF_PD.Flags"])).ToString(), XI.Map.S2cItemSame.GetFlags(pdFill).ToString());

        // ==== setters land at the compiler offset without disturbing neighbours ====
        {
            var b = new byte[XI.Map.S2cItemSame.SizeOf];
            XI.Map.S2cItemSame.SetState(b, XI.Map.ItemSameState.AllLoaded); // u8 @ 0 -> 1
            XI.Map.S2cItemSame.SetFlags(b, 0xDEADBEEF);                      // u32 @ 4
            XI.Map.S2cItemSame.SetU8(b, (int)ParseNum(g["OFF_PD.padding00"]), 0x55); // padding byte 1
            Check("IS SetState lands at compiler offset 0", ((byte)XI.Map.ItemSameState.AllLoaded).ToString(), b[0].ToString());
            Check("IS SetFlags lands at compiler offset 4", ((uint)0xDEADBEEF).ToString(), BitConverter.ToUInt32(b, (int)ParseNum(g["OFF_PD.Flags"])).ToString());
            // the u32 write must not touch the enum byte or the padding (State@0 and padding00 are BEFORE Flags).
            Check("IS SetFlags leaves State (byte 0) == AllLoaded (1)", ((byte)XI.Map.ItemSameState.AllLoaded).ToString(), XI.Map.S2cItemSame.GetStateRaw(b).ToString());
            // writing one padding byte must not spill into Flags (byte 4).
            Check("IS SetU8(padding[0]) leaves Flags byte 4 untouched", ((uint)0xDEADBEEF).ToString(), XI.Map.S2cItemSame.GetFlags(b).ToString());
            // round-trips.
            Check("IS SetState round-trip", XI.Map.ItemSameState.AllLoaded.ToString(), XI.Map.S2cItemSame.GetState(b).ToString());
            Check("IS SetFlags round-trip", ((uint)0xDEADBEEF).ToString(), XI.Map.S2cItemSame.GetFlags(b).ToString());
        }
    }

    static void VerifyMapS2cWpos(string dir, string enumHeaderPath)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_wpos_enum_golden.txt");
        Console.WriteLine("\n== S2c wpos (0x05B) parity: C# XI.Map vs layout oracle + 0x05b_wpos.h enum re-parse (first S2C body leading with FLOATs + a signed char dir + a mid-struct uint8 engine enum POSMODE) ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c wpos enum golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; the engine adds the 4-byte header).
        Check("WP SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- the outer body: DEFAULT packing, alignof 4 (floats + uint32s), dense/hole-free ----
        Check("WP PD_SIZE", g["PD_SIZE"], XI.Map.S2cWpos.SizeOf.ToString());
        Check("WP PD_ALIGN == 4 (from the floats/uint32s)", g["PD_ALIGN"], XI.Map.S2cWpos.AlignOf.ToString());
        Check("WP hole-free: PAD_INSERTED == 0", "0", g["PAD_INSERTED"]);
        Check("WP MEMBER_BYTES == PD_SIZE", g["MEMBER_BYTES"], g["PD_SIZE"]);
        Check("WP SizeOf % AlignOf == 0 (natural tail)", "1", (XI.Map.S2cWpos.SizeOf % XI.Map.S2cWpos.AlignOf == 0 ? 1 : 0).ToString());

        // ---- float / signed-char facts ----
        Check("WP FLOAT_SIZE == 4", g["FLOAT_SIZE"], "4");
        Check("WP FLOAT_ALIGN == 4", g["FLOAT_ALIGN"], "4");
        Check("WP CHAR_IS_SIGNED == 1 (dir is a signed char -> sbyte)", "1", g["CHAR_IS_SIGNED"]);

        // ---- every compiler-transcribed offset == the model const ----
        Check("WP OFF x", g["OFF_PD.x"], XI.Map.S2cWpos.OffX.ToString());
        Check("WP OFF y == x + 4 (float stride)", (ParseNum(g["OFF_PD.x"]) + 4).ToString(), XI.Map.S2cWpos.OffY.ToString());
        Check("WP OFF z == y + 4 (float stride)", (ParseNum(g["OFF_PD.y"]) + 4).ToString(), XI.Map.S2cWpos.OffZ.ToString());
        Check("WP OFF UniqueNo == z + 4", (ParseNum(g["OFF_PD.z"]) + 4).ToString(), XI.Map.S2cWpos.OffUniqueNo.ToString());
        Check("WP OFF ActIndex == UniqueNo + 4", (ParseNum(g["OFF_PD.UniqueNo"]) + 4).ToString(), XI.Map.S2cWpos.OffActIndex.ToString());
        Check("WP OFF Mode == ActIndex + 2 (u16 then u8)", (ParseNum(g["OFF_PD.ActIndex"]) + 2).ToString(), XI.Map.S2cWpos.OffMode.ToString());
        Check("WP OFF dir == Mode + 1", (ParseNum(g["OFF_PD.Mode"]) + 1).ToString(), XI.Map.S2cWpos.OffDir.ToString());
        // THE trailing-u32-after-u8-pair fact: Mode(1)+dir(1) reach the already-4-aligned running offset -> no silent pad.
        Check("WP OFF padding18 == dir + 1", (ParseNum(g["OFF_PD.dir"]) + 1).ToString(), XI.Map.S2cWpos.OffPadding18.ToString());
        Check("WP U8_PAIR_RUN_OFFSET == Mode + 1 + 1 (the 18+2==20 run)", (ParseNum(g["OFF_PD.Mode"]) + 2).ToString(), g["U8_PAIR_RUN_OFFSET"]);
        Check("WP padding18 offset is 4-aligned (no silent pad)", "0", (ParseNum(g["OFF_PD.padding18"]) % 4).ToString());
        Check("WP padding18 + 4 == SizeOf (reaches the end, gapless)", "1",
            (XI.Map.S2cWpos.OffPadding18 + 4 == XI.Map.S2cWpos.SizeOf ? 1 : 0).ToString());

        // ---- chunk-21 engine-enum underlying-type substitution RE-CONFIRMED at uint8 vs a uint8_t twin of the whole struct ----
        Check("WP POSMODE underlying size 1 (uint8_t)", g["ENUM_underlying_size"], "1");
        Check("WP enum underlying substitution EQ_SIZE == 1", "1", g["EQ_SIZE"]);
        Check("WP enum underlying substitution EQ_ALIGN == 1", "1", g["EQ_ALIGN"]);
        Check("WP enum underlying substitution EMODE_OFF == 1", "1", g["EMODE_OFF"]);

        // ---- the 9 POSMODE literals: C# enum underlying byte == compiler (uint8_t)-cast (note skips 0x04/0x07) ----
        Check("WP ENUM NORMAL (C# == compiler cast)", g["ENUM_NORMAL"], ((byte)XI.Map.Posmode.NORMAL).ToString());
        Check("WP ENUM EVENT", g["ENUM_EVENT"], ((byte)XI.Map.Posmode.EVENT).ToString());
        Check("WP ENUM CLEAR", g["ENUM_CLEAR"], ((byte)XI.Map.Posmode.CLEAR).ToString());
        Check("WP ENUM POP", g["ENUM_POP"], ((byte)XI.Map.Posmode.POP).ToString());
        Check("WP ENUM RESET (non-sequential, skips 0x04)", g["ENUM_RESET"], ((byte)XI.Map.Posmode.RESET).ToString());
        Check("WP ENUM MATERIALIZE", g["ENUM_MATERIALIZE"], ((byte)XI.Map.Posmode.MATERIALIZE).ToString());
        Check("WP ENUM LOCK (non-sequential, skips 0x07)", g["ENUM_LOCK"], ((byte)XI.Map.Posmode.LOCK).ToString());
        Check("WP ENUM UNLOCK", g["ENUM_UNLOCK"], ((byte)XI.Map.Posmode.UNLOCK).ToString());
        Check("WP ENUM ROTATE", g["ENUM_ROTATE"], ((byte)XI.Map.Posmode.ROTATE).ToString());
        if (System.IO.File.Exists(enumHeaderPath))
        {
            var re = ParseCppEnum(System.IO.File.ReadAllText(enumHeaderPath), "POSMODE");
            Check("WP 0x05b_wpos.h POSMODE re-parse count == 9", "9", re.Count.ToString());
            foreach (var (name, val) in re)
            {
                string key = "ENUM_" + name;
                if (g.ContainsKey(key))
                    Check($"WP re-parse {name} == compiler underlying value", g[key], val.ToString());
            }
        }
        else
        {
            Check("WP 0x05b_wpos.h re-parse target present", "present", "MISSING:" + enumHeaderPath);
        }
        VerifyPacketIds("POSMODE", enumHeaderPath, typeof(XI.Map.Posmode));

        // ==== whole-struct fill determinism (gapless i*7+3 over the 24-byte body) ====
        byte[] pdFill = HexToBytes(g["PD_FILL"]);
        Check("WP PD_FILL_SIZE", g["PD_FILL_SIZE"], pdFill.Length.ToString());
        {
            var local = new byte[pdFill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check("WP PD deterministic fill (gapless i*7+3, proves no internal pad)", g["PD_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }

        // ---- float / scalar accessors == raw at the compiler offsets (two independent ways) ----
        // read cross-checks use the SAME bytes on both sides, so even a non-finite bit pattern yields identical ToString.
        Check("WP GetX == raw float at compiler offset", BitConverter.ToSingle(pdFill, (int)ParseNum(g["OFF_PD.x"])).ToString(), XI.Map.S2cWpos.GetX(pdFill).ToString());
        Check("WP GetY == raw float at compiler offset", BitConverter.ToSingle(pdFill, (int)ParseNum(g["OFF_PD.y"])).ToString(), XI.Map.S2cWpos.GetY(pdFill).ToString());
        Check("WP GetZ == raw float at compiler offset", BitConverter.ToSingle(pdFill, (int)ParseNum(g["OFF_PD.z"])).ToString(), XI.Map.S2cWpos.GetZ(pdFill).ToString());
        Check("WP GetUniqueNo == raw u32 at compiler offset", BitConverter.ToUInt32(pdFill, (int)ParseNum(g["OFF_PD.UniqueNo"])).ToString(), XI.Map.S2cWpos.GetUniqueNo(pdFill).ToString());
        Check("WP GetActIndex == raw u16 at compiler offset", BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.ActIndex"])).ToString(), XI.Map.S2cWpos.GetActIndex(pdFill).ToString());
        Check("WP GetModeRaw == raw u8 at compiler offset (enum underlying)", pdFill[(int)ParseNum(g["OFF_PD.Mode"])].ToString(), XI.Map.S2cWpos.GetModeRaw(pdFill).ToString());
        Check("WP GetDir == raw SIGNED byte at compiler offset", unchecked((sbyte)pdFill[(int)ParseNum(g["OFF_PD.dir"])]).ToString(), XI.Map.S2cWpos.GetDir(pdFill).ToString());
        Check("WP GetPadding18 == raw u32 at compiler offset", BitConverter.ToUInt32(pdFill, (int)ParseNum(g["OFF_PD.padding18"])).ToString(), XI.Map.S2cWpos.GetPadding18(pdFill).ToString());

        // ==== setters land at the compiler offset without disturbing neighbours (finite floats to keep ToString stable) ====
        {
            var b = new byte[XI.Map.S2cWpos.SizeOf];
            // fresh-buffer isolation: writing only X (the first float) must leave y@4 and z@8 byte-zero.
            {
                var bx = new byte[XI.Map.S2cWpos.SizeOf];
                XI.Map.S2cWpos.SetX(bx, 1.5f);
                Check("WP SetX lands 1.5f at compiler offset 0", (1.5f).ToString(), BitConverter.ToSingle(bx, (int)ParseNum(g["OFF_PD.x"])).ToString());
                Check("WP SetX leaves y@4 byte-zero (float isolation)", "0", BitConverter.ToSingle(bx, (int)ParseNum(g["OFF_PD.y"])).ToString());
                Check("WP SetX leaves z@8 byte-zero (float isolation)", "0", BitConverter.ToSingle(bx, (int)ParseNum(g["OFF_PD.z"])).ToString());
            }
            XI.Map.S2cWpos.SetX(b, 1.5f);            // float @ 0
            XI.Map.S2cWpos.SetY(b, -3.25f);          // float @ 4
            XI.Map.S2cWpos.SetUniqueNo(b, 0xDEADBEEF); // u32 @ 12
            XI.Map.S2cWpos.SetActIndex(b, 0x1234);   // u16 @ 16
            XI.Map.S2cWpos.SetMode(b, XI.Map.Posmode.ROTATE); // u8 @ 18 -> 0x0A
            XI.Map.S2cWpos.SetDir(b, -5);            // signed char @ 19
            Check("WP SetY lands (round-trip)", (-3.25f).ToString(), XI.Map.S2cWpos.GetY(b).ToString());
            Check("WP SetUniqueNo lands at compiler offset 12", ((uint)0xDEADBEEF).ToString(), BitConverter.ToUInt32(b, (int)ParseNum(g["OFF_PD.UniqueNo"])).ToString());
            Check("WP SetActIndex lands at compiler offset 16", ((ushort)0x1234).ToString(), BitConverter.ToUInt16(b, (int)ParseNum(g["OFF_PD.ActIndex"])).ToString());
            // the signed char: -5 stored as byte 0xFB, read back as sbyte -5 (NOT 251).
            Check("WP SetDir(-5) stores byte 0xFB at compiler offset 19", ((byte)0xFB).ToString(), b[(int)ParseNum(g["OFF_PD.dir"])].ToString());
            Check("WP GetDir reads -5 (SIGNED, not 251)", ((sbyte)(-5)).ToString(), XI.Map.S2cWpos.GetDir(b).ToString());
            // a write into the u8 enum byte must not touch the adjacent signed dir byte or the trailing u32.
            Check("WP SetMode leaves padding18@20 zero (isolation)", "0", XI.Map.S2cWpos.GetPadding18(b).ToString());
            Check("WP SetMode==Rotate round-trip", XI.Map.Posmode.ROTATE.ToString(), XI.Map.S2cWpos.GetMode(b).ToString());
            // the trailing u32 write must not disturb Mode/dir.
            XI.Map.S2cWpos.SetPadding18(b, 0x11223344);
            Check("WP SetPadding18 lands at compiler offset 20", ((uint)0x11223344).ToString(), BitConverter.ToUInt32(b, (int)ParseNum(g["OFF_PD.padding18"])).ToString());
            Check("WP SetPadding18 leaves Mode still Rotate (0x0A)", ((byte)XI.Map.Posmode.ROTATE).ToString(), XI.Map.S2cWpos.GetModeRaw(b).ToString());
            Check("WP SetPadding18 leaves dir still -5", ((sbyte)(-5)).ToString(), XI.Map.S2cWpos.GetDir(b).ToString());
        }
    }

    static void VerifyMapS2cEventNum(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_eventnum_golden.txt");
        Console.WriteLine("\n== S2c eventnum (0x034) parity: C# XI.Map vs layout oracle (FIRST SIGNED fixed-width array int32_t num[8]: negative element round-trips via ToInt32; leading-u32-scalar->4-aligned array no pad; six-u16-run after the array) ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c eventnum golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; the engine adds the 4-byte header).
        Check("EVN SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- the outer body: DEFAULT packing, alignof 4 (u32 + int32 array), dense/hole-free ----
        Check("EVN PD_SIZE", g["PD_SIZE"], XI.Map.S2cEventNum.SizeOf.ToString());
        Check("EVN PD_ALIGN == 4 (from the uint32 + int32 array)", g["PD_ALIGN"], XI.Map.S2cEventNum.AlignOf.ToString());
        Check("EVN hole-free: PAD_INSERTED == 0", "0", g["PAD_INSERTED"]);
        Check("EVN MEMBER_BYTES == PD_SIZE", g["MEMBER_BYTES"], g["PD_SIZE"]);
        Check("EVN SizeOf % AlignOf == 0 (natural tail)", "1", (XI.Map.S2cEventNum.SizeOf % XI.Map.S2cEventNum.AlignOf == 0 ? 1 : 0).ToString());

        // ---- signed-array-element facts (the genuinely-new width+sign) ----
        Check("EVN INT32_SIZE == 4", g["INT32_SIZE"], "4");
        Check("EVN INT32_ALIGN == 4", g["INT32_ALIGN"], "4");
        Check("EVN INT32_IS_SIGNED == 1 (num[8] is a SIGNED int32 array -- first of its kind)", "1", g["INT32_IS_SIGNED"]);

        // ---- leading u32 + the scalar-then-array fact (UniqueNo@0 -> running 4, already 4-aligned -> num@4 no pad) ----
        Check("EVN OFF UniqueNo", g["OFF_PD.UniqueNo"], XI.Map.S2cEventNum.OffUniqueNo.ToString());
        Check("EVN SCALAR_RUN_OFFSET == 4 (the leading u32 scalar's running offset)", g["SCALAR_RUN_OFFSET"], "4");
        Check("EVN OFF num == UniqueNo + 4 (the int32 array lands at the already-4-aligned 4)", (ParseNum(g["OFF_PD.UniqueNo"]) + 4).ToString(), XI.Map.S2cEventNum.OffNum.ToString());
        Check("EVN OFF num == SCALAR_RUN_OFFSET (no silent pad before the signed array)", g["SCALAR_RUN_OFFSET"], g["OFF_PD.num"]);
        Check("EVN num offset is 4-aligned", "0", (ParseNum(g["OFF_PD.num"]) % 4).ToString());

        // ---- int32 num[8]: stride via num[1], count/elem/len ----
        Check("EVN num[1] == num + 4 (int32 element stride, no inter-element pad)", (ParseNum(g["OFF_PD.num"]) + 4).ToString(), g["OFF_PD.num[1]"]);
        Check("EVN ARR_num_COUNT == 8", g["ARR_num_COUNT"], XI.Map.S2cEventNum.NumCount.ToString());
        Check("EVN ARR_num_ELEMSIZE == 4", g["ARR_num_ELEMSIZE"], XI.Map.S2cEventNum.NumStride.ToString());
        Check("EVN ARR_num_LEN == COUNT * 4 (32)", (ParseNum(g["ARR_num_COUNT"]) * 4).ToString(), g["ARR_num_LEN"]);
        Check("EVN ARR_num_LEN == model NumLen", g["ARR_num_LEN"], XI.Map.S2cEventNum.NumLen.ToString());
        // the array ends at 4+32==36 (NUM_ARRAY_END == the first u16 ActIndex offset), already 2-aligned -> no pad.
        Check("EVN NUM_ARRAY_END == num + 32 (the signed array ends at 36)", (ParseNum(g["OFF_PD.num"]) + ParseNum(g["ARR_num_LEN"])).ToString(), g["NUM_ARRAY_END"]);
        Check("EVN OFF ActIndex == NUM_ARRAY_END (u16-run starts where the array ends, no pad)", g["NUM_ARRAY_END"], XI.Map.S2cEventNum.OffActIndex.ToString());

        // ---- six consecutive uint16 members (dense stride 2) ----
        Check("EVN OFF ActIndex", g["OFF_PD.ActIndex"], XI.Map.S2cEventNum.OffActIndex.ToString());
        Check("EVN OFF EventNum == ActIndex + 2", (ParseNum(g["OFF_PD.ActIndex"]) + 2).ToString(), XI.Map.S2cEventNum.OffEventNum.ToString());
        Check("EVN OFF EventPara == EventNum + 2", (ParseNum(g["OFF_PD.EventNum"]) + 2).ToString(), XI.Map.S2cEventNum.OffEventPara.ToString());
        Check("EVN OFF Mode == EventPara + 2", (ParseNum(g["OFF_PD.EventPara"]) + 2).ToString(), XI.Map.S2cEventNum.OffMode.ToString());
        Check("EVN OFF EventNum2 == Mode + 2", (ParseNum(g["OFF_PD.Mode"]) + 2).ToString(), XI.Map.S2cEventNum.OffEventNum2.ToString());
        Check("EVN OFF EventPara2 == EventNum2 + 2", (ParseNum(g["OFF_PD.EventNum2"]) + 2).ToString(), XI.Map.S2cEventNum.OffEventPara2.ToString());
        // the u16-run ends at EventPara2 + 2 == 48 == SizeOf (U16_RUN_END), reaching the end with no tail pad.
        Check("EVN U16_RUN_END == EventPara2 + 2 (the 46+2==48 end)", (ParseNum(g["OFF_PD.EventPara2"]) + 2).ToString(), g["U16_RUN_END"]);
        Check("EVN U16_RUN_END == SizeOf (48, gapless to the end)", g["U16_RUN_END"], g["PD_SIZE"]);

        // ---- C# NumOffset(index) == compiler num + index*4 for every element (two independent ways) ----
        for (int i = 0; i < 8; i++)
            Check($"EVN NumOffset({i}) == num + {i}*4", (ParseNum(g["OFF_PD.num"]) + i * 4).ToString(), XI.Map.S2cEventNum.NumOffset(i).ToString());

        // ==== whole-struct fill determinism (gapless i*7+3 over the 48-byte body) ====
        byte[] pdFill = HexToBytes(g["PD_FILL"]);
        Check("EVN PD_FILL_SIZE", g["PD_FILL_SIZE"], pdFill.Length.ToString());
        {
            var local = new byte[pdFill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check("EVN PD deterministic fill (gapless i*7+3, proves no internal pad)", g["PD_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }

        // ---- accessors == raw at the compiler offsets. The signed array reads via ToInt32, never ToUInt32. ----
        Check("EVN GetUniqueNo == raw u32 at compiler offset", BitConverter.ToUInt32(pdFill, (int)ParseNum(g["OFF_PD.UniqueNo"])).ToString(), XI.Map.S2cEventNum.GetUniqueNo(pdFill).ToString());
        for (int i = 0; i < 8; i++)
            Check($"EVN GetNum({i}) == raw SIGNED int32 at num+{i}*4", BitConverter.ToInt32(pdFill, (int)ParseNum(g["OFF_PD.num"]) + i * 4).ToString(), XI.Map.S2cEventNum.GetNum(pdFill, i).ToString());
        Check("EVN GetNumSlice[0] == raw byte at OFF num", pdFill[(int)ParseNum(g["OFF_PD.num"])].ToString(), XI.Map.S2cEventNum.GetNumSlice(pdFill)[0].ToString());
        Check("EVN GetActIndex == raw u16 at compiler offset", BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.ActIndex"])).ToString(), XI.Map.S2cEventNum.GetActIndex(pdFill).ToString());
        Check("EVN GetEventNum == raw u16 at compiler offset", BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.EventNum"])).ToString(), XI.Map.S2cEventNum.GetEventNum(pdFill).ToString());
        Check("EVN GetEventPara == raw u16 at compiler offset", BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.EventPara"])).ToString(), XI.Map.S2cEventNum.GetEventPara(pdFill).ToString());
        Check("EVN GetMode == raw u16 at compiler offset", BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.Mode"])).ToString(), XI.Map.S2cEventNum.GetMode(pdFill).ToString());
        Check("EVN GetEventNum2 == raw u16 at compiler offset", BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.EventNum2"])).ToString(), XI.Map.S2cEventNum.GetEventNum2(pdFill).ToString());
        Check("EVN GetEventPara2 == raw u16 at compiler offset", BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.EventPara2"])).ToString(), XI.Map.S2cEventNum.GetEventPara2(pdFill).ToString());

        // ==== setters land at the compiler offsets; THE signed-element round-trip is the key new discriminator ====
        {
            var b = new byte[XI.Map.S2cEventNum.SizeOf];
            XI.Map.S2cEventNum.SetUniqueNo(b, 0xDEADBEEF);
            // writing only the leading u32 must leave the first signed-array element (num[0]@4) zero.
            Check("EVN SetUniqueNo leaves num[0]@4 zero (scalar/array isolation)", "0", XI.Map.S2cEventNum.GetNum(b, 0).ToString());
            // THE signed discriminator: a NEGATIVE element stores 0xF9FFFFFF and reads back -7 (NOT 4294967289).
            XI.Map.S2cEventNum.SetNum(b, 0, -7);
            Check("EVN SetNum(0,-7) stores the SIGNED int32 bits at num+0", unchecked((int)0xFFFFFFF9u).ToString(), BitConverter.ToInt32(b, (int)ParseNum(g["OFF_PD.num"])).ToString());
            Check("EVN GetNum(0) reads -7 (SIGNED, not 4294967289)", (-7).ToString(), XI.Map.S2cEventNum.GetNum(b, 0).ToString());
            // a negative element must not disturb its neighbour (num[1]) nor the most-negative int boundary.
            XI.Map.S2cEventNum.SetNum(b, 1, -2147483648); // int.MinValue -> 0x80000000
            Check("EVN SetNum(1,int.MinValue) stores 0x80000000 (most-negative)", unchecked((int)0x80000000).ToString(), BitConverter.ToInt32(b, (int)ParseNum(g["OFF_PD.num"]) + 4).ToString());
            Check("EVN GetNum(1) reads int.MinValue back (SIGNED)", int.MinValue.ToString(), XI.Map.S2cEventNum.GetNum(b, 1).ToString());
            Check("EVN SetNum(1) leaves num[0] still -7 (element isolation)", (-7).ToString(), XI.Map.S2cEventNum.GetNum(b, 0).ToString());
            // a positive element too, proving the accessor is width-correct across the sign boundary.
            XI.Map.S2cEventNum.SetNum(b, 7, 123456);
            Check("EVN SetNum(7,123456) lands at num+7*4 (last element)", (123456).ToString(), BitConverter.ToInt32(b, (int)ParseNum(g["OFF_PD.num"]) + 7 * 4).ToString());
            Check("EVN SetNum(7) leaves num[0] still -7", (-7).ToString(), XI.Map.S2cEventNum.GetNum(b, 0).ToString());
            // the six u16 members land without disturbing the signed array (num[7]) or each other.
            XI.Map.S2cEventNum.SetActIndex(b, 0x1234);
            Check("EVN SetActIndex lands at compiler offset 36", ((ushort)0x1234).ToString(), BitConverter.ToUInt16(b, (int)ParseNum(g["OFF_PD.ActIndex"])).ToString());
            Check("EVN SetActIndex leaves num[7] still 123456 (array/u16 isolation)", (123456).ToString(), XI.Map.S2cEventNum.GetNum(b, 7).ToString());
            XI.Map.S2cEventNum.SetEventPara2(b, 0x5678);
            Check("EVN SetEventPara2 lands at compiler offset 46 (last member)", ((ushort)0x5678).ToString(), BitConverter.ToUInt16(b, (int)ParseNum(g["OFF_PD.EventPara2"])).ToString());
            Check("EVN SetEventPara2 leaves EventNum2 (offset 44) zero (u16 isolation)", "0", XI.Map.S2cEventNum.GetEventNum2(b).ToString());
            // writing the last byte (47) via EventPara2 must be in-bounds of SizeOf (48): no overflow, and UniqueNo survives.
            Check("EVN EventPara2@46+2 == SizeOf (in-bounds, last member reaches the end)", "1",
                (XI.Map.S2cEventNum.OffEventPara2 + 2 == XI.Map.S2cEventNum.SizeOf ? 1 : 0).ToString());
            Check("EVN full-set round-trip leaves UniqueNo intact", ((uint)0xDEADBEEF).ToString(), XI.Map.S2cEventNum.GetUniqueNo(b).ToString());
        }
    }

    static void VerifyMapS2cBallistaScout(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_ballista_scout_golden.txt");
        Console.WriteLine("\n== S2c ballista SCOUT (0x0e6) parity: C# XI.Map vs layout oracle (FIRST float[] array RookPosition[4]; a leading uint16 bit-field run Flags:6+Mode:10 where Mode STRADDLES the byte 0/1 boundary -- sentinel-scan-derived masks/shift + the per-byte-mask NEGATIVE check; alignof 4 from the floats/u32s) ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c ballista scout golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; the engine adds the 4-byte header).
        Check("BLS SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- the outer body: DEFAULT packing, alignof 4 from the floats/uint32s, dense/hole-free ----
        Check("BLS PD_SIZE", g["PD_SIZE"], XI.Map.S2cBallistaScout.SizeOf.ToString());
        Check("BLS PD_ALIGN == 4 (floats + uint32s, NOT the leading u16 bitword)", g["PD_ALIGN"], XI.Map.S2cBallistaScout.AlignOf.ToString());
        Check("BLS hole-free: PAD_INSERTED == 0", "0", g["PAD_INSERTED"]);
        Check("BLS MEMBER_BYTES == PD_SIZE", g["MEMBER_BYTES"], g["PD_SIZE"]);
        Check("BLS SizeOf % AlignOf == 0 (natural tail)", "1", (XI.Map.S2cBallistaScout.SizeOf % XI.Map.S2cBallistaScout.AlignOf == 0 ? 1 : 0).ToString());
        Check("BLS FLOAT_SIZE == 4", g["FLOAT_SIZE"], "4");
        Check("BLS FLOAT_ALIGN == 4", g["FLOAT_ALIGN"], "4");

        // ==== THE genuinely-new straddling-bit-field facts (sentinel-scan; offsetof ILLEGAL on bit-fields) ====
        // The bit-field run Flags:6 + Mode:10 == EXACTLY one uint16 allocation unit at body byte 0.
        Check("BLS BF_RUN_SUM_BITS == 16 (6+10 fills one uint16 unit)", g["BF_RUN_SUM_BITS"], "16");
        Check("BLS BF_UNIT_BYTES == 2 (the whole run is one uint16)", g["BF_UNIT_BYTES"], "2");
        Check("BLS BF_Flags_byte == 0", g["BF_Flags_byte"], "0");
        Check("BLS BF_Flags_shift == 0", g["BF_Flags_shift"], "0");
        Check("BLS BF_Flags_lowmask == 0x3F (width 6)", g["BF_Flags_lowmask"], "0x3F");
        Check("BLS Flags:6 does NOT straddle (whole field in byte 0)", "0", g["BF_Flags_straddle"]);
        Check("BLS model FlagsMask(0x3F)==width-6 mask", ((uint)((1u << XI.Map.S2cBallistaScout.FlagsWidth) - 1u)).ToString(), XI.Map.S2cBallistaScout.FlagsMask.ToString());
        // Mode is the STRADDLER: compiler shift 6, low byte 0xC0, changes BOTH bytes, straddle==1.
        Check("BLS BF_Mode_shift == 6 (within the whole uint16)", g["BF_Mode_shift"], "6");
        Check("BLS BF_Mode_byte == 0", g["BF_Mode_byte"], "0");
        Check("BLS BF_Mode_lowmask == 0xC0 (the LOW-byte-only mask -- what a buggy per-byte reader would use)", g["BF_Mode_lowmask"], "0xC0");
        Check("BLS BF_Mode_lastbyte == 1 (Mode reaches byte 1)", "1", g["BF_Mode_lastbyte"]);
        Check("BLS BF_Mode_straddle == 1 (THE negative-check discriminator)", "1", g["BF_Mode_straddle"]);
        Check("BLS model ModeStraddles == true", "True", XI.Map.S2cBallistaScout.ModeStraddles.ToString());
        Check("BLS model ModeMask(0x3FF)==width-10 mask", ((uint)((1u << XI.Map.S2cBallistaScout.ModeWidth) - 1u)).ToString(), XI.Map.S2cBallistaScout.ModeMask.ToString());

        // ---- every scalar/float-array offset == the model const ----
        Check("BLS OFF padding06 == bitword(2) then u16 -> 2", "2", XI.Map.S2cBallistaScout.OffPadding06.ToString());
        Check("BLS OFF padding06", g["OFF_PD.padding06"], XI.Map.S2cBallistaScout.OffPadding06.ToString());
        Check("BLS OFF RookUniqueNo == padding06 + 2 (u16 then u32, 4-aligned)", (ParseNum(g["OFF_PD.padding06"]) + 2).ToString(), XI.Map.S2cBallistaScout.OffRookUniqueNo.ToString());
        Check("BLS BITWORD_RUN_OFFSET == RookUniqueNo (the bitword+padding06 reach 4)", g["OFF_PD.RookUniqueNo"], g["BITWORD_RUN_OFFSET"]);
        Check("BLS OFF RookUniqueNo", g["OFF_PD.RookUniqueNo"], XI.Map.S2cBallistaScout.OffRookUniqueNo.ToString());
        Check("BLS OFF padding0C == RookUniqueNo + 4", (ParseNum(g["OFF_PD.RookUniqueNo"]) + 4).ToString(), XI.Map.S2cBallistaScout.OffPadding0C.ToString());
        Check("BLS OFF padding0C", g["OFF_PD.padding0C"], XI.Map.S2cBallistaScout.OffPadding0C.ToString());
        // the first float array: base 12 (already 4-aligned, no pad after the u32), stride 4 via [1], LEN 16.
        Check("BLS OFF RookPosition == padding0C + 4 (float array packs at 4-aligned 12, no pad)", (ParseNum(g["OFF_PD.padding0C"]) + 4).ToString(), XI.Map.S2cBallistaScout.OffRookPosition.ToString());
        Check("BLS OFF RookPosition", g["OFF_PD.RookPosition"], XI.Map.S2cBallistaScout.OffRookPosition.ToString());
        Check("BLS RookPosition offset is 4-aligned", "0", (ParseNum(g["OFF_PD.RookPosition"]) % 4).ToString());
        Check("BLS OFF RookPosition[1] == RookPosition + 4 (float stride, no inter-element pad)", (ParseNum(g["OFF_PD.RookPosition"]) + 4).ToString(), g["OFF_PD.RookPosition[1]"]);
        Check("BLS ARR_RookPosition_COUNT == 4", g["ARR_RookPosition_COUNT"], XI.Map.S2cBallistaScout.RookPosCount.ToString());
        Check("BLS ARR_RookPosition_ELEMSIZE == 4", g["ARR_RookPosition_ELEMSIZE"], XI.Map.S2cBallistaScout.RookPosStride.ToString());
        Check("BLS ARR_RookPosition_LEN == COUNT * 4 (16)", (ParseNum(g["ARR_RookPosition_COUNT"]) * 4).ToString(), g["ARR_RookPosition_LEN"]);
        Check("BLS ARR_RookPosition_LEN == model RookPosLen", g["ARR_RookPosition_LEN"], XI.Map.S2cBallistaScout.RookPosLen.ToString());
        // the trailing scalar float reaches the end: 28 + 4 == 32 == SizeOf.
        Check("BLS OFF RookDistance == RookPosition + LEN (12+16==28)", (ParseNum(g["OFF_PD.RookPosition"]) + ParseNum(g["ARR_RookPosition_LEN"])).ToString(), XI.Map.S2cBallistaScout.OffRookDistance.ToString());
        Check("BLS RookDistance + 4 == SizeOf (28+4==32, gapless)", "1", (XI.Map.S2cBallistaScout.OffRookDistance + 4 == XI.Map.S2cBallistaScout.SizeOf ? 1 : 0).ToString());
        // C# RookPositionOffset(i) == compiler RookPosition + i*4 for every element (two independent ways).
        for (int i = 0; i < 4; i++)
            Check($"BLS RookPositionOffset({i}) == RookPosition + {i}*4", (ParseNum(g["OFF_PD.RookPosition"]) + i * 4).ToString(), XI.Map.S2cBallistaScout.RookPositionOffset(i).ToString());

        // ==== whole-struct fill determinism (gapless i*7+3 over the 32-byte body) ====
        byte[] pdFill = HexToBytes(g["PD_FILL"]);
        Check("BLS PD_FILL_SIZE", g["PD_FILL_SIZE"], pdFill.Length.ToString());
        {
            var local = new byte[pdFill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check("BLS PD deterministic fill (gapless i*7+3, proves no internal pad)", g["PD_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }

        // ---- accessors == raw at the compiler offsets (two independent ways) ----
        Check("BLS GetBitword == raw u16 at body byte 0", BitConverter.ToUInt16(pdFill, 0).ToString(), XI.Map.S2cBallistaScout.GetBitword(pdFill).ToString());
        Check("BLS GetFlags == (raw u16 >> 0) & 0x3F at compiler byte 0", ((BitConverter.ToUInt16(pdFill, 0) >> 0) & 0x3F).ToString(), XI.Map.S2cBallistaScout.GetFlags(pdFill).ToString());
        Check("BLS GetMode == (raw u16 >> 6) & 0x3FF at compiler byte 0 (whole-word read)", ((BitConverter.ToUInt16(pdFill, 0) >> 6) & 0x3FF).ToString(), XI.Map.S2cBallistaScout.GetMode(pdFill).ToString());
        Check("BLS GetPadding06 == raw u16 at compiler offset", BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.padding06"])).ToString(), XI.Map.S2cBallistaScout.GetPadding06(pdFill).ToString());
        Check("BLS GetRookUniqueNo == raw u32 at compiler offset", BitConverter.ToUInt32(pdFill, (int)ParseNum(g["OFF_PD.RookUniqueNo"])).ToString(), XI.Map.S2cBallistaScout.GetRookUniqueNo(pdFill).ToString());
        Check("BLS GetPadding0C == raw u32 at compiler offset", BitConverter.ToUInt32(pdFill, (int)ParseNum(g["OFF_PD.padding0C"])).ToString(), XI.Map.S2cBallistaScout.GetPadding0C(pdFill).ToString());
        for (int i = 0; i < 4; i++)
            Check($"BLS GetRookPosition({i}) == raw float at RookPosition+{i}*4", BitConverter.ToSingle(pdFill, (int)ParseNum(g["OFF_PD.RookPosition"]) + i * 4).ToString(), XI.Map.S2cBallistaScout.GetRookPosition(pdFill, i).ToString());
        Check("BLS GetRookDistance == raw float at compiler offset", BitConverter.ToSingle(pdFill, (int)ParseNum(g["OFF_PD.RookDistance"])).ToString(), XI.Map.S2cBallistaScout.GetRookDistance(pdFill).ToString());
        Check("BLS GetRookPositionSlice[0] == raw byte at OFF RookPosition", pdFill[(int)ParseNum(g["OFF_PD.RookPosition"])].ToString(), XI.Map.S2cBallistaScout.GetRookPositionSlice(pdFill)[0].ToString());

        // ==== setters land; THE straddle NEGATIVE check + bit-field RMW neighbour isolation ====
        {
            // THE negative check: after SetMode(0x3FF) the WHOLE-uint16 read gives 0x3FF, but a per-byte (byte-0-only)
            // mask ((b[0] & 0xC0) >> 6) gives only 3 -- proving Mode's bits 8..15 (in byte 1) are silently dropped by a
            // per-byte reader. This is the discriminator that proves the bit-field must be read as a whole uint16.
            var bs = new byte[XI.Map.S2cBallistaScout.SizeOf];
            XI.Map.S2cBallistaScout.SetMode(bs, 0x3FF);
            uint correctMode = XI.Map.S2cBallistaScout.GetMode(bs);
            uint perByteMode = (uint)((bs[0] & 0xC0) >> 6);
            Check("BLS SetMode(0x3FF) whole-word reads 0x3FF", ((uint)0x3FF).ToString(), correctMode.ToString());
            Check("BLS SetMode(0x3FF) per-byte mask reads only 3 (NEGATIVE: drops bits 8..15)", ((uint)3).ToString(), perByteMode.ToString());
            Check("BLS straddle proven: whole-word != per-byte mask", "1", (correctMode != perByteMode ? 1 : 0).ToString());
            // Flags:6 stays entirely in byte 0, so a whole-word and a byte-0 read agree (the non-straddle contrast).
            XI.Map.S2cBallistaScout.SetFlags(bs, 0x3F);
            uint correctFlags = XI.Map.S2cBallistaScout.GetFlags(bs);
            uint byteFlags = (uint)(bs[0] & 0x3F);
            Check("BLS SetFlags(0x3F) whole-word == byte-0 read (Flags non-straddle)", correctFlags.ToString(), byteFlags.ToString());
            Check("BLS SetFlags(0x3F) reads back 0x3F", ((uint)0x3F).ToString(), correctFlags.ToString());
            // the two bit-fields RMW the SAME uint16 without clobbering each other.
            XI.Map.S2cBallistaScout.SetFlags(bs, 0x2A); // bits 0..5
            XI.Map.S2cBallistaScout.SetMode(bs, 0x155); // bits 6..15
            Check("BLS SetFlags(0x2A) leaves Mode 0x155 intact (bitword RMW isolation)", ((uint)0x155).ToString(), XI.Map.S2cBallistaScout.GetMode(bs).ToString());
            Check("BLS SetMode(0x155) leaves Flags 0x2A intact (bitword RMW isolation)", ((uint)0x2A).ToString(), XI.Map.S2cBallistaScout.GetFlags(bs).ToString());
            // the whole bitword equals the reassembled fields.
            ushort expectedWord = (ushort)((0x2A & 0x3F) | ((0x155 & 0x3FF) << 6));
            Check("BLS GetBitword == reassembled Flags|Mode<<6", expectedWord.ToString(), XI.Map.S2cBallistaScout.GetBitword(bs).ToString());

            // now the scalars + float array on a fresh buffer, verifying the bitword survives each write and vice-versa.
            var b = new byte[XI.Map.S2cBallistaScout.SizeOf];
            XI.Map.S2cBallistaScout.SetFlags(b, 0x11);
            XI.Map.S2cBallistaScout.SetMode(b, 0x22);
            // a write into padding06 (byte 2) must not disturb the bitword at bytes 0..1.
            XI.Map.S2cBallistaScout.SetPadding06(b, 0x1234);
            Check("BLS SetPadding06 leaves Flags still 0x11 (bitword/padding06 isolation)", ((uint)0x11).ToString(), XI.Map.S2cBallistaScout.GetFlags(b).ToString());
            Check("BLS SetPadding06 leaves Mode still 0x22", ((uint)0x22).ToString(), XI.Map.S2cBallistaScout.GetMode(b).ToString());
            XI.Map.S2cBallistaScout.SetRookUniqueNo(b, 0xDEADBEEF);
            Check("BLS SetRookUniqueNo lands at compiler offset 4", ((uint)0xDEADBEEF).ToString(), BitConverter.ToUInt32(b, (int)ParseNum(g["OFF_PD.RookUniqueNo"])).ToString());
            Check("BLS SetRookUniqueNo leaves padding06 still 0x1234 (u32/u16 isolation)", ((ushort)0x1234).ToString(), XI.Map.S2cBallistaScout.GetPadding06(b).ToString());
            // finite floats keep the raw-vs-accessor ToString cross-check stable.
            XI.Map.S2cBallistaScout.SetRookPosition(b, 0, 1.5f);
            XI.Map.S2cBallistaScout.SetRookPosition(b, 3, -2.25f);
            Check("BLS SetRookPosition(0,1.5f) lands at RookPosition+0", (1.5f).ToString(), BitConverter.ToSingle(b, (int)ParseNum(g["OFF_PD.RookPosition"])).ToString());
            Check("BLS SetRookPosition(3,-2.25f) lands at RookPosition+12", (-2.25f).ToString(), BitConverter.ToSingle(b, (int)ParseNum(g["OFF_PD.RookPosition"]) + 12).ToString());
            Check("BLS SetRookPosition(3) leaves RookPosition[1] zero (element isolation)", "0", XI.Map.S2cBallistaScout.GetRookPosition(b, 1).ToString());
            XI.Map.S2cBallistaScout.SetRookDistance(b, 7.75f);
            Check("BLS SetRookDistance lands at compiler offset 28", (7.75f).ToString(), BitConverter.ToSingle(b, (int)ParseNum(g["OFF_PD.RookDistance"])).ToString());
            // the trailing float write must not disturb the float array's last element.
            Check("BLS SetRookDistance leaves RookPosition[3] still -2.25f", (-2.25f).ToString(), XI.Map.S2cBallistaScout.GetRookPosition(b, 3).ToString());
            // fresh-buffer isolation: setting only RookPosition[2] leaves the bitword and the scalars byte-zero.
            {
                var bx = new byte[XI.Map.S2cBallistaScout.SizeOf];
                XI.Map.S2cBallistaScout.SetRookPosition(bx, 2, 4.25f);
                Check("BLS SetRookPosition(2) leaves the bitword zero (fresh)", "0", XI.Map.S2cBallistaScout.GetBitword(bx).ToString());
                Check("BLS SetRookPosition(2) leaves RookDistance@28 zero (fresh)", "0", XI.Map.S2cBallistaScout.GetRookDistance(bx).ToString());
                Check("BLS SetRookPosition(2) leaves RookPosition[1] zero (element isolation)", "0", XI.Map.S2cBallistaScout.GetRookPosition(bx, 1).ToString());
            }
            // fresh-buffer: setting only the bitword's Mode byte must leave the float array + padding06 zero.
            {
                var bx = new byte[XI.Map.S2cBallistaScout.SizeOf];
                XI.Map.S2cBallistaScout.SetMode(bx, 0x3FF);
                Check("BLS SetMode leaves padding06@2 zero (bitword isolation)", "0", XI.Map.S2cBallistaScout.GetPadding06(bx).ToString());
                Check("BLS SetMode leaves RookPosition[0]@12 zero (bitword isolation)", "0", XI.Map.S2cBallistaScout.GetRookPosition(bx, 0).ToString());
            }
        }
    }

    static void VerifyMapS2cItemSearch(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_itemsearch_golden.txt");
        Console.WriteLine("\n== S2c itemsearch (0x049) parity: C# XI.Map vs layout oracle (FIRST trailing uint8[64] -- the largest fixed u8 array; whole align 2 comes from the LEADING u16, the trailing align-1 array does NOT raise it -- ALIGN_FROM_SCALAR_NOT_ARRAY; the array ends EXACTLY at SizeOf, PAD_INSERTED=0) ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c itemsearch golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; the engine adds the 4-byte header).
        Check("ITS SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- the outer body: DEFAULT packing, alignof 2 from the LEADING u16 (not the trailing u8 array) ----
        Check("ITS PD_SIZE", g["PD_SIZE"], XI.Map.S2cItemSearch.SizeOf.ToString());
        Check("ITS PD_ALIGN == 2 (the leading uint16, NOT the trailing u8 array)", g["PD_ALIGN"], XI.Map.S2cItemSearch.AlignOf.ToString());
        Check("ITS hole-free: PAD_INSERTED == 0", "0", g["PAD_INSERTED"]);
        Check("ITS MEMBER_BYTES == PD_SIZE", g["MEMBER_BYTES"], g["PD_SIZE"]);
        Check("ITS SizeOf % AlignOf == 0 (68 % 2 == 0, no tail pad)", "1", (XI.Map.S2cItemSearch.SizeOf % XI.Map.S2cItemSearch.AlignOf == 0 ? 1 : 0).ToString());

        // ==== THE genuinely-new whole-align fact: a leading u16 sets align 2; the trailing align-1 u8 array does NOT raise it ====
        Check("ITS U16_ALIGN == 2", g["U16_ALIGN"], "2");
        Check("ITS U8_ARRAY_ALIGN == 1 (a u8[]'s align == its element's == 1)", g["U8_ARRAY_ALIGN"], "1");
        Check("ITS ALIGN_FROM_SCALAR_NOT_ARRAY == 1 (whole align from the u16, not the array)", "1", g["ALIGN_FROM_SCALAR_NOT_ARRAY"]);
        Check("ITS model AlignOf(2) == compiler PD_ALIGN == U16_ALIGN", g["U16_ALIGN"], XI.Map.S2cItemSearch.AlignOf.ToString());
        Check("ITS model AlignOf(2) != the u8 array align(1) (the array never raises it)", "1",
            (XI.Map.S2cItemSearch.AlignOf != (int)ParseNum(g["U8_ARRAY_ALIGN"]) ? 1 : 0).ToString());

        // ---- leading scalar run + the trailing u8 array base/stride/count/len ----
        Check("ITS OFF ItemNo", g["OFF_PD.ItemNo"], XI.Map.S2cItemSearch.OffItemNo.ToString());
        Check("ITS OFF Flag == ItemNo + 2 (u16 then u8)", (ParseNum(g["OFF_PD.ItemNo"]) + 2).ToString(), XI.Map.S2cItemSearch.OffFlag.ToString());
        Check("ITS OFF padding07 == Flag + 1", (ParseNum(g["OFF_PD.Flag"]) + 1).ToString(), XI.Map.S2cItemSearch.OffPadding07.ToString());
        Check("ITS U8_RUN_OFFSET == ItemNo + 2 + 1 + 1 (the u16+2u8 run -> 4)", (ParseNum(g["OFF_PD.ItemNo"]) + 4).ToString(), g["U8_RUN_OFFSET"]);
        Check("ITS OFF ItemName == padding07 + 1 (u8 array packs at the running offset 4, no pad)", (ParseNum(g["OFF_PD.padding07"]) + 1).ToString(), XI.Map.S2cItemSearch.OffItemName.ToString());
        Check("ITS OFF ItemName == U8_RUN_OFFSET", g["U8_RUN_OFFSET"], g["OFF_PD.ItemName"]);
        Check("ITS ItemName[1] == ItemName + 1 (u8 stride, no inter-element pad)", (ParseNum(g["OFF_PD.ItemName"]) + 1).ToString(), g["OFF_PD.ItemName[1]"]);
        Check("ITS ARR_ItemName_COUNT == 64", g["ARR_ItemName_COUNT"], XI.Map.S2cItemSearch.ItemNameCount.ToString());
        Check("ITS ARR_ItemName_ELEMSIZE == 1", g["ARR_ItemName_ELEMSIZE"], XI.Map.S2cItemSearch.ItemNameStride.ToString());
        Check("ITS ARR_ItemName_LEN == COUNT (64)", g["ARR_ItemName_COUNT"], g["ARR_ItemName_LEN"]);
        Check("ITS ARR_ItemName_LEN == model ItemNameLen", g["ARR_ItemName_LEN"], XI.Map.S2cItemSearch.ItemNameLen.ToString());
        // THE array reaches the struct end EXACTLY: base 4 + len 64 == 68 == SizeOf (ITEMNAME_ARRAY_END), no tail pad.
        Check("ITS ITEMNAME_ARRAY_END == ItemName + LEN (4+64==68)", (ParseNum(g["OFF_PD.ItemName"]) + ParseNum(g["ARR_ItemName_LEN"])).ToString(), g["ITEMNAME_ARRAY_END"]);
        Check("ITS ITEMNAME_ARRAY_END == SizeOf (the trailing array ends at the struct tail)", g["ITEMNAME_ARRAY_END"], g["PD_SIZE"]);

        // ---- C# ItemNameOffset(index) == compiler ItemName + index*1 for a spread of cells (two independent ways) ----
        foreach (int i in new[] { 0, 1, 31, 62, 63 })
            Check($"ITS ItemNameOffset({i}) == ItemName + {i}", (ParseNum(g["OFF_PD.ItemName"]) + i).ToString(), XI.Map.S2cItemSearch.ItemNameOffset(i).ToString());

        // ==== whole-struct fill determinism (gapless i*7+3 over the 68-byte body) ====
        byte[] pdFill = HexToBytes(g["PD_FILL"]);
        Check("ITS PD_FILL_SIZE", g["PD_FILL_SIZE"], pdFill.Length.ToString());
        {
            var local = new byte[pdFill.Length];
            for (int k = 0; k < local.Length; k++) local[k] = unchecked((byte)(k * 7 + 3));
            Check("ITS PD deterministic fill (gapless i*7+3, proves no internal pad)", g["PD_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }

        // ---- accessors == raw at the compiler offsets (two independent ways) ----
        Check("ITS GetItemNo == raw u16 at compiler offset", BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.ItemNo"])).ToString(), XI.Map.S2cItemSearch.GetItemNo(pdFill).ToString());
        Check("ITS GetFlag == raw u8 at compiler offset", pdFill[(int)ParseNum(g["OFF_PD.Flag"])].ToString(), XI.Map.S2cItemSearch.GetFlag(pdFill).ToString());
        Check("ITS GetPadding07 == raw u8 at compiler offset", pdFill[(int)ParseNum(g["OFF_PD.padding07"])].ToString(), XI.Map.S2cItemSearch.GetPadding07(pdFill).ToString());
        foreach (int i in new[] { 0, 1, 31, 62, 63 })
            Check($"ITS GetItemName({i}) == raw u8 at ItemName+{i}", pdFill[(int)ParseNum(g["OFF_PD.ItemName"]) + i].ToString(), XI.Map.S2cItemSearch.GetItemName(pdFill, i).ToString());
        Check("ITS GetItemNameSlice[0] == raw byte at OFF ItemName", pdFill[(int)ParseNum(g["OFF_PD.ItemName"])].ToString(), XI.Map.S2cItemSearch.GetItemNameSlice(pdFill)[0].ToString());
        Check("ITS GetItemNameSlice last == raw byte at ItemName+63", pdFill[(int)ParseNum(g["OFF_PD.ItemName"]) + 63].ToString(), XI.Map.S2cItemSearch.GetItemNameSlice(pdFill)[63].ToString());

        // ==== setters land at the compiler offsets without disturbing neighbours / the scalar-array boundary ====
        {
            var b = new byte[XI.Map.S2cItemSearch.SizeOf];
            // u16 @0 lands, and leaves the later bytes zero (nothing written to them yet -- check BEFORE their setters).
            XI.Map.S2cItemSearch.SetItemNo(b, 0x1234);   // u16 @0
            Check("ITS SetItemNo lands at compiler offset 0", ((ushort)0x1234).ToString(), BitConverter.ToUInt16(b, (int)ParseNum(g["OFF_PD.ItemNo"])).ToString());
            // writing the leading u16 must not touch Flag@2 (a u16 stays within its 2 bytes).
            Check("ITS SetItemNo leaves Flag@2 zero (u16 width stays at 2 bytes)", "0", XI.Map.S2cItemSearch.GetFlag(b).ToString());
            Check("ITS SetItemNo leaves padding07@3 zero", "0", XI.Map.S2cItemSearch.GetPadding07(b).ToString());
            // u8 Flag @2 lands and leaves padding07@3 (its later neighbour) zero -- check BEFORE SetPadding07.
            XI.Map.S2cItemSearch.SetFlag(b, 0xAA);       // u8 @2
            Check("ITS SetFlag lands at compiler offset 2", ((byte)0xAA).ToString(), b[(int)ParseNum(g["OFF_PD.Flag"])].ToString());
            Check("ITS SetFlag leaves padding07@3 zero (byte-run isolation)", "0", XI.Map.S2cItemSearch.GetPadding07(b).ToString());
            Check("ITS SetFlag leaves ItemNo still 0x1234 (isolation)", ((ushort)0x1234).ToString(), XI.Map.S2cItemSearch.GetItemNo(b).ToString());
            // u8 padding07 @3 lands and must NOT spill into the array's first byte (offset 4) -- check BEFORE SetItemName.
            XI.Map.S2cItemSearch.SetPadding07(b, 0x55);  // u8 @3
            Check("ITS SetPadding07 lands at compiler offset 3", ((byte)0x55).ToString(), b[(int)ParseNum(g["OFF_PD.padding07"])].ToString());
            Check("ITS SetPadding07 leaves ItemName[0]@4 zero (scalar/array boundary)", "0", XI.Map.S2cItemSearch.GetItemName(b, 0).ToString());
            // array element writes: land at base+index, leave the adjacent scalar and neighbour zero.
            XI.Map.S2cItemSearch.SetItemName(b, 0, 0xAB);
            Check("ITS SetItemName(0) lands at ItemName base 4", ((byte)0xAB).ToString(), b[(int)ParseNum(g["OFF_PD.ItemName"])].ToString());
            Check("ITS SetItemName(0) leaves padding07@3 still 0x55 (boundary isolation)", ((byte)0x55).ToString(), XI.Map.S2cItemSearch.GetPadding07(b).ToString());
            Check("ITS SetItemName(0) leaves ItemName[1] zero (element isolation)", "0", XI.Map.S2cItemSearch.GetItemName(b, 1).ToString());
            XI.Map.S2cItemSearch.SetItemName(b, 63, 0xCD);  // last byte of the array (offset 67 == SizeOf-1)
            Check("ITS SetItemName(63) lands at ItemName+63 (last byte, offset 67)", ((byte)0xCD).ToString(), b[(int)ParseNum(g["OFF_PD.ItemName"]) + 63].ToString());
            Check("ITS SetItemName(63) leaves ItemName[62] zero (element isolation)", "0", XI.Map.S2cItemSearch.GetItemName(b, 62).ToString());
            Check("ITS SetItemName(63) leaves ItemNo still 0x1234 (full round-trip)", ((ushort)0x1234).ToString(), XI.Map.S2cItemSearch.GetItemNo(b).ToString());
            // fresh-buffer isolation: writing ONLY the array's first byte must leave the leading scalars zero.
            {
                var bx = new byte[XI.Map.S2cItemSearch.SizeOf];
                XI.Map.S2cItemSearch.SetItemName(bx, 0, 0x77);
                Check("ITS SetItemName(0) leaves ItemNo@0 zero (fresh)", "0", XI.Map.S2cItemSearch.GetItemNo(bx).ToString());
                Check("ITS SetItemName(0) leaves Flag@2 zero (fresh)", "0", XI.Map.S2cItemSearch.GetFlag(bx).ToString());
                Check("ITS SetItemName(0) leaves ItemName[1] zero (fresh)", "0", XI.Map.S2cItemSearch.GetItemName(bx, 1).ToString());
            }
        }
    }

    static void VerifyMapS2cItemSubContainer(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_item_subcontainer_golden.txt");
        Console.WriteLine("\n== S2c item_subcontainer (0x026) parity: C# XI.Map vs layout oracle (fixed u8 unknown01[2] array IMMEDIATELY followed by nine consecutive u16 model_id_*: the array END 4+2==6 is 2-aligned -> first u16 packs no pad; reused UNSCOPED enum CONTAINER_ID substitution re-confirmed; alignof 2 with NO uint32) ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c item_subcontainer golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; the engine adds the 4-byte header).
        Check("ISC SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- the outer body: DEFAULT packing, alignof 2 (NO uint32 member -- only u8s + a u8 array + nine u16s) ----
        Check("ISC PD_SIZE", g["PD_SIZE"], XI.Map.S2cItemSubContainer.SizeOf.ToString());
        Check("ISC PD_ALIGN == 2 (the uint16s, not a uint32 -- there is none)", g["PD_ALIGN"], XI.Map.S2cItemSubContainer.AlignOf.ToString());
        Check("ISC hole-free: PAD_INSERTED == 0", "0", g["PAD_INSERTED"]);
        Check("ISC MEMBER_BYTES == PD_SIZE", g["MEMBER_BYTES"], g["PD_SIZE"]);
        Check("ISC SizeOf % AlignOf == 0 (natural tail)", "1", (XI.Map.S2cItemSubContainer.SizeOf % XI.Map.S2cItemSubContainer.AlignOf == 0 ? 1 : 0).ToString());
        // the reused unscoped enum's underlying type is still one byte.
        Check("ISC CONTAINER_ID (unscoped, reused) underlying == 1", g["CONTAINER_ID_underlying"], "1");

        // ---- leading u8 run (is_used/container/index/unknown00 @0/1/2/3) ----
        Check("ISC OFF is_used", g["OFF_PD.is_used"], XI.Map.S2cItemSubContainer.OffIsUsed.ToString());
        Check("ISC OFF container == is_used + 1 (unscoped enum byte after a u8)", (ParseNum(g["OFF_PD.is_used"]) + 1).ToString(), XI.Map.S2cItemSubContainer.OffContainer.ToString());
        Check("ISC OFF index == container + 1", (ParseNum(g["OFF_PD.container"]) + 1).ToString(), XI.Map.S2cItemSubContainer.OffIndex.ToString());
        Check("ISC OFF unknown00 == index + 1", (ParseNum(g["OFF_PD.index"]) + 1).ToString(), XI.Map.S2cItemSubContainer.OffUnknown00.ToString());
        Check("ISC OFF unknown01 == unknown00 + 1", (ParseNum(g["OFF_PD.unknown00"]) + 1).ToString(), XI.Map.S2cItemSubContainer.OffUnknown01.ToString());

        // ==== THE u8 ARRAY followed by a u16-RUN: the array's END offset (not a scalar byte) decides the 2-alignment ====
        Check("ISC unknown01[1] == unknown01 + 1 (u8 stride, no inter-element pad)", (ParseNum(g["OFF_PD.unknown01"]) + 1).ToString(), g["OFF_PD.unknown01[1]"]);
        Check("ISC ARR_unknown01_LEN == 2", g["ARR_unknown01_LEN"], XI.Map.S2cItemSubContainer.Unknown01Len.ToString());
        Check("ISC ARR_unknown01_COUNT == 2", g["ARR_unknown01_COUNT"], "2");
        Check("ISC ARR_unknown01_ELEMSIZE == 1", g["ARR_unknown01_ELEMSIZE"], "1");
        // THE genuinely-new boundary: the array END is base + LEN == 6, which is already 2-aligned -> first u16 no pad.
        Check("ISC U8_ARRAY_END == unknown01 + LEN (4+2==6)", (ParseNum(g["OFF_PD.unknown01"]) + ParseNum(g["ARR_unknown01_LEN"])).ToString(), g["U8_ARRAY_END"]);
        Check("ISC OFF model_id_race_hair == U8_ARRAY_END (the u16-run starts at the array end, no pad)", g["U8_ARRAY_END"], XI.Map.S2cItemSubContainer.OffModelIdBase.ToString());
        Check("ISC FIRST_U16_MOD2 == 0 (the array end is 2-aligned -> no pad)", "0", g["FIRST_U16_MOD2"]);
        // nine consecutive u16 model ids, dense stride 2, no inter-element pad.
        Check("ISC OFF model_id_head == race_hair + 2", (ParseNum(g["OFF_PD.model_id_race_hair"]) + 2).ToString(), g["OFF_PD.model_id_head"]);
        Check("ISC OFF model_id_range == race_hair + 8*2", (ParseNum(g["OFF_PD.model_id_race_hair"]) + 8 * 2).ToString(), g["OFF_PD.model_id_range"]);
        Check("ISC OFF model_id_range == race_hair + 8*2 == model base+16", (XI.Map.S2cItemSubContainer.OffModelIdBase + 8 * XI.Map.S2cItemSubContainer.ModelIdStride).ToString(), g["OFF_PD.model_id_range"]);
        // the trailing u8 pair sits right after the u16-run.
        Check("ISC OFF race == model_id_range + 2 (u16-run end -> trailing u8)", (ParseNum(g["OFF_PD.model_id_range"]) + 2).ToString(), XI.Map.S2cItemSubContainer.OffRace.ToString());
        Check("ISC OFF pose == race + 1", (ParseNum(g["OFF_PD.race"]) + 1).ToString(), XI.Map.S2cItemSubContainer.OffPose.ToString());
        Check("ISC race == OffModelIdBase + 9*2 (the compiler's run length)", (XI.Map.S2cItemSubContainer.OffModelIdBase + XI.Map.S2cItemSubContainer.ModelIdCount * XI.Map.S2cItemSubContainer.ModelIdStride).ToString(), XI.Map.S2cItemSubContainer.OffRace.ToString());
        // the whole body reaches the end with no tail pad (26 % 2 == 0).
        Check("ISC pose + 1 == SizeOf (25+1==26, gapless to the end)", "1",
            (XI.Map.S2cItemSubContainer.OffPose + 1 == XI.Map.S2cItemSubContainer.SizeOf ? 1 : 0).ToString());

        // ==== chunk-21 underlying-type substitution RE-CONFIRMED for the reused UNSCOPED enum, on this fresh body ====
        Check("ISC enum underlying substitution EQ_SIZE == 1 (unscoped CONTAINER_ID -> uint8 twin, whole struct)", "1", g["EQ_SIZE"]);
        Check("ISC enum underlying substitution EQ_ALIGN == 1", "1", g["EQ_ALIGN"]);
        Check("ISC ECATEGORY_OFF == 1 (UNSCOPED enum member == uint8_t at offset 1)", "1", g["ECATEGORY_OFF"]);

        // ---- C# ModelIdOffset(index) == compiler race_hair base + index*2 for every element (two independent ways) ----
        for (int i = 0; i < 9; i++)
            Check($"ISC ModelIdOffset({i}) == race_hair + {i}*2", (ParseNum(g["OFF_PD.model_id_race_hair"]) + i * 2).ToString(), XI.Map.S2cItemSubContainer.ModelIdOffset(i).ToString());

        // ==== whole-struct fill determinism (gapless i*7+3 over the 26-byte body) ====
        byte[] pdFill = HexToBytes(g["PD_FILL"]);
        Check("ISC PD_FILL_SIZE", g["PD_FILL_SIZE"], pdFill.Length.ToString());
        {
            var local = new byte[pdFill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check("ISC PD deterministic fill (gapless i*7+3, proves no internal pad)", g["PD_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }

        // ---- accessors == raw at the compiler offsets (two independent ways) ----
        Check("ISC GetIsUsed == raw u8 at compiler offset", pdFill[(int)ParseNum(g["OFF_PD.is_used"])].ToString(), XI.Map.S2cItemSubContainer.GetIsUsed(pdFill).ToString());
        Check("ISC GetContainerRaw == raw u8 at compiler offset", pdFill[(int)ParseNum(g["OFF_PD.container"])].ToString(), XI.Map.S2cItemSubContainer.GetContainerRaw(pdFill).ToString());
        Check("ISC GetContainer == (ContainerId) raw byte at compiler offset", ((XI.Map.ContainerId)pdFill[(int)ParseNum(g["OFF_PD.container"])]).ToString(), XI.Map.S2cItemSubContainer.GetContainer(pdFill).ToString());
        Check("ISC GetIndex == raw u8 at compiler offset", pdFill[(int)ParseNum(g["OFF_PD.index"])].ToString(), XI.Map.S2cItemSubContainer.GetIndex(pdFill).ToString());
        Check("ISC GetUnknown00 == raw u8 at compiler offset", pdFill[(int)ParseNum(g["OFF_PD.unknown00"])].ToString(), XI.Map.S2cItemSubContainer.GetUnknown00(pdFill).ToString());
        Check("ISC GetUnknown01[0] == raw u8 at compiler offset", pdFill[(int)ParseNum(g["OFF_PD.unknown01"])].ToString(), XI.Map.S2cItemSubContainer.GetUnknown01(pdFill, 0).ToString());
        Check("ISC GetUnknown01[1] == raw u8 at unknown01+1", pdFill[(int)ParseNum(g["OFF_PD.unknown01"]) + 1].ToString(), XI.Map.S2cItemSubContainer.GetUnknown01(pdFill, 1).ToString());
        Check("ISC GetUnknown01Slice[0] == raw byte at OFF unknown01", pdFill[(int)ParseNum(g["OFF_PD.unknown01"])].ToString(), XI.Map.S2cItemSubContainer.GetUnknown01Slice(pdFill)[0].ToString());
        // the nine u16 model ids == raw u16 at the compiler base + index*2.
        for (int i = 0; i < 9; i++)
            Check($"ISC GetModelId({i}) == raw u16 at race_hair+{i}*2", BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.model_id_race_hair"]) + i * 2).ToString(), XI.Map.S2cItemSubContainer.GetModelId(pdFill, i).ToString());
        Check("ISC GetModelIdRaceHair == raw u16 at base", BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.model_id_race_hair"])).ToString(), XI.Map.S2cItemSubContainer.GetModelIdRaceHair(pdFill).ToString());
        Check("ISC GetModelIdRange == raw u16 at base+8*2 (last model id)", BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.model_id_race_hair"]) + 8 * 2).ToString(), XI.Map.S2cItemSubContainer.GetModelIdRange(pdFill).ToString());
        Check("ISC GetRace == raw u8 at compiler offset", pdFill[(int)ParseNum(g["OFF_PD.race"])].ToString(), XI.Map.S2cItemSubContainer.GetRace(pdFill).ToString());
        Check("ISC GetPose == raw u8 at compiler offset", pdFill[(int)ParseNum(g["OFF_PD.pose"])].ToString(), XI.Map.S2cItemSubContainer.GetPose(pdFill).ToString());

        // ==== setters land at the compiler offsets without disturbing neighbours / the array-end boundary ====
        {
            var b = new byte[XI.Map.S2cItemSubContainer.SizeOf];
            XI.Map.S2cItemSubContainer.SetIsUsed(b, 1);
            XI.Map.S2cItemSubContainer.SetContainer(b, XI.Map.ContainerId.LOC_MOGSAFE); // byte @1 -> 1
            Check("ISC SetContainer(LOC_MOGSAFE) stores byte 1 at offset 1", ((byte)1).ToString(), b[(int)ParseNum(g["OFF_PD.container"])].ToString());
            Check("ISC GetContainer reads back LOC_MOGSAFE", XI.Map.ContainerId.LOC_MOGSAFE.ToString(), XI.Map.S2cItemSubContainer.GetContainer(b).ToString());
            XI.Map.S2cItemSubContainer.SetModelId(b, 0, 0x1234);     // u16 @6
            XI.Map.S2cItemSubContainer.SetModelId(b, 8, 0x5678);     // u16 @22 (last model id)
            XI.Map.S2cItemSubContainer.SetRace(b, 0xAA);             // u8 @24
            XI.Map.S2cItemSubContainer.SetPose(b, 0x9F);             // u8 @25
            Check("ISC SetModelId(0) lands at U8_ARRAY_END (compiler offset 6)", ((ushort)0x1234).ToString(), BitConverter.ToUInt16(b, (int)ParseNum(g["U8_ARRAY_END"])).ToString());
            // writing the first model id (offset 6/7) must NOT touch the u8 array's last byte (offset 5).
            Check("ISC SetModelId(0) leaves unknown01[1]@5 zero (array/u16 boundary isolation)", "0", XI.Map.S2cItemSubContainer.GetUnknown01(b, 1).ToString());
            Check("ISC SetModelId(8) lands at base+8*2 (offset 22)", ((ushort)0x5678).ToString(), BitConverter.ToUInt16(b, (int)ParseNum(g["OFF_PD.model_id_race_hair"]) + 8 * 2).ToString());
            Check("ISC SetModelId(8) leaves model id 7 (offset 20) zero (element isolation)", "0", XI.Map.S2cItemSubContainer.GetModelId(b, 7).ToString());
            // the trailing u8 pair write must not disturb the last model id (a u16 at 22).
            Check("ISC SetRace(0xAA) lands at offset 24", ((byte)0xAA).ToString(), b[(int)ParseNum(g["OFF_PD.race"])].ToString());
            Check("ISC SetRace leaves model_id_range (offset 22) still 0x5678 (u16/u8 isolation)", ((ushort)0x5678).ToString(), XI.Map.S2cItemSubContainer.GetModelIdRange(b).ToString());
            XI.Map.S2cItemSubContainer.SetPose(b, 0x9F);
            Check("ISC SetPose(0x9F) lands at offset 25 (last byte)", ((byte)0x9F).ToString(), b[(int)ParseNum(g["OFF_PD.pose"])].ToString());
            Check("ISC SetPose leaves race (offset 24) still 0xAA (u8 pair isolation)", ((byte)0xAA).ToString(), XI.Map.S2cItemSubContainer.GetRace(b).ToString());
            Check("ISC SetIsUsed leaves container LOC_MOGSAFE (u8-run isolation)", XI.Map.ContainerId.LOC_MOGSAFE.ToString(), XI.Map.S2cItemSubContainer.GetContainer(b).ToString());
            // fresh-buffer isolation: writing only model id 4 leaves the u8 array and the trailing pair zero.
            {
                var bx = new byte[XI.Map.S2cItemSubContainer.SizeOf];
                XI.Map.S2cItemSubContainer.SetModelId(bx, 4, 0x4444);
                Check("ISC SetModelId(4) leaves unknown01[1]@5 zero (fresh)", "0", XI.Map.S2cItemSubContainer.GetUnknown01(bx, 1).ToString());
                Check("ISC SetModelId(4) leaves race@24 zero (fresh)", "0", XI.Map.S2cItemSubContainer.GetRace(bx).ToString());
                Check("ISC SetModelId(4) leaves model id 3 (offset 12) zero (element isolation)", "0", XI.Map.S2cItemSubContainer.GetModelId(bx, 3).ToString());
            }
        }
    }

    static void VerifyMapS2cItemAttr(string dir, string containerHeaderPath, string lockflgHeaderPath)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_item_attr_enum_golden.txt");
        Console.WriteLine("\n== S2c item_attr (0x020) parity: C# XI.Map vs layout oracle + TWO .h enum re-parses (FIRST body with TWO engine enums of DIFFERENT kinds -- unscoped CONTAINER_ID : uint8 + scoped ItemLockFlg : uint8_t + trailing uint8 Attr[24] + 3-byte tail pad from an align-1 array ending at 37) ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c item_attr golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; the engine adds the 4-byte header).
        Check("IAT SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- the outer body: DEFAULT packing, alignof 4 (the two leading u32s), 3-byte COMPILER tail pad ----
        Check("IAT PD_SIZE", g["PD_SIZE"], XI.Map.S2cItemAttr.SizeOf.ToString());
        Check("IAT PD_ALIGN == 4 (from the two uint32s)", g["PD_ALIGN"], XI.Map.S2cItemAttr.AlignOf.ToString());
        Check("IAT HAS_TAIL_PAD == 1 (a trailing align-1 u8 array ending at 37 -> rounds to 40)", "1", g["HAS_TAIL_PAD"]);
        Check("IAT PAD_INSERTED == 3 (non-zero, the compiler's own tail pad)", "3", g["PAD_INSERTED"]);
        Check("IAT MEMBER_BYTES (37) != PD_SIZE (40)", "1", (ParseNum(g["MEMBER_BYTES"]) != ParseNum(g["PD_SIZE"]) ? 1 : 0).ToString());
        Check("IAT MEMBER_BYTES == 37 (u32+u32+u16+u8+u8+u8+u8[24])", g["MEMBER_BYTES"], "37");

        // ---- THE tail-pad arithmetic, purely from compiler values ----
        Check("IAT TAIL_PAD_BYTES == PD_SIZE - LAST_MEMBER_RUN_OFFSET (40-37)", (ParseNum(g["PD_SIZE"]) - ParseNum(g["LAST_MEMBER_RUN_OFFSET"])).ToString(), g["TAIL_PAD_BYTES"]);
        Check("IAT PAD_INSERTED == TAIL_PAD_BYTES (all pad is the tail)", g["PAD_INSERTED"], g["TAIL_PAD_BYTES"]);
        Check("IAT LAST_MEMBER_RUN_OFFSET == Attr base + 24 (13+24==37)", (ParseNum(g["OFF_PD.Attr"]) + 24).ToString(), g["LAST_MEMBER_RUN_OFFSET"]);
        Check("IAT RUN_MOD_ALIGN == 1 (37 not 4-aligned -> why pad is needed)", (ParseNum(g["LAST_MEMBER_RUN_OFFSET"]) % ParseNum(g["PD_ALIGN"])).ToString(), g["RUN_MOD_ALIGN"]);
        Check("IAT model LastMemberRunOffset(37) == compiler", g["LAST_MEMBER_RUN_OFFSET"], XI.Map.S2cItemAttr.LastMemberRunOffset.ToString());
        Check("IAT model TailPadBytes(3) == compiler TAIL_PAD_BYTES", g["TAIL_PAD_BYTES"], XI.Map.S2cItemAttr.TailPadBytes.ToString());
        Check("IAT SizeOf % AlignOf == 0 (40 rounds up to a multiple of 4)", "0", (XI.Map.S2cItemAttr.SizeOf % XI.Map.S2cItemAttr.AlignOf).ToString());

        // ---- both enums underlying size 1 (unscoped : uint8 AND scoped : uint8_t) ----
        Check("IAT CONTAINER_ID (unscoped) underlying size == 1", g["CONTAINER_ID_underlying"], "1");
        Check("IAT ItemLockFlg (scoped) underlying size == 1", g["ItemLockFlg_underlying"], "1");

        // ---- every compiler-transcribed offset == the model const ----
        Check("IAT OFF ItemNum", g["OFF_PD.ItemNum"], XI.Map.S2cItemAttr.OffItemNum.ToString());
        Check("IAT OFF Price == ItemNum + 4 (u32)", (ParseNum(g["OFF_PD.ItemNum"]) + 4).ToString(), XI.Map.S2cItemAttr.OffPrice.ToString());
        Check("IAT OFF ItemNo == Price + 4 (u32 then u16)", (ParseNum(g["OFF_PD.Price"]) + 4).ToString(), XI.Map.S2cItemAttr.OffItemNo.ToString());
        // THE UNSCOPED enum member Category (byte) right after a u16 -- 8+2==10, no pad (u8 packs at the running offset).
        Check("IAT OFF Category == ItemNo + 2 (unscoped enum underlying byte after a u16)", (ParseNum(g["OFF_PD.ItemNo"]) + 2).ToString(), XI.Map.S2cItemAttr.OffCategory.ToString());
        Check("IAT OFF ItemIndex == Category + 1", (ParseNum(g["OFF_PD.Category"]) + 1).ToString(), XI.Map.S2cItemAttr.OffItemIndex.ToString());
        // The SCOPED enum member LockFlg (byte) -- a consecutive byte-run with ItemIndex, no realign.
        Check("IAT OFF LockFlg == ItemIndex + 1 (scoped enum underlying byte, byte-run)", (ParseNum(g["OFF_PD.ItemIndex"]) + 1).ToString(), XI.Map.S2cItemAttr.OffLockFlg.ToString());
        Check("IAT OFF Attr == LockFlg + 1 (u8 array base after the two enum bytes)", (ParseNum(g["OFF_PD.LockFlg"]) + 1).ToString(), XI.Map.S2cItemAttr.OffAttr.ToString());
        // Attr[24]: stride 1 via Attr[1]; LEN/COUNT/ELEMSIZE.
        Check("IAT Attr[1] == Attr + 1 (u8 stride, no inter-element pad)", (ParseNum(g["OFF_PD.Attr"]) + 1).ToString(), g["OFF_PD.Attr[1]"]);
        Check("IAT ARR_Attr_COUNT == 24", g["ARR_Attr_COUNT"], XI.Map.S2cItemAttr.AttrCount.ToString());
        Check("IAT ARR_Attr_ELEMSIZE == 1", g["ARR_Attr_ELEMSIZE"], "1");
        Check("IAT ARR_Attr_LEN == COUNT (24)", g["ARR_Attr_COUNT"], g["ARR_Attr_LEN"]);
        Check("IAT ARR_Attr_LEN == model AttrLen", g["ARR_Attr_LEN"], XI.Map.S2cItemAttr.AttrLen.ToString());
        // the Attr array reaches the last named byte: Attr base 13 + 24 == 37 == LAST_MEMBER_RUN_OFFSET.
        Check("IAT Attr base + AttrLen == LAST_MEMBER_RUN_OFFSET (13+24==37)", "1",
            (XI.Map.S2cItemAttr.OffAttr + XI.Map.S2cItemAttr.AttrLen == XI.Map.S2cItemAttr.LastMemberRunOffset ? 1 : 0).ToString());
        // the 3 tail pad bytes then reach SizeOf: 37 + 3 == 40.
        Check("IAT LAST_MEMBER_RUN_OFFSET + TailPadBytes == SizeOf (37+3==40)", "1",
            (XI.Map.S2cItemAttr.LastMemberRunOffset + XI.Map.S2cItemAttr.TailPadBytes == XI.Map.S2cItemAttr.SizeOf ? 1 : 0).ToString());

        // ==== chunk-21 underlying-type substitution RE-CONFIRMED for a MIX of unscoped + scoped enum members ====
        // The oracle replaces BOTH enum members with plain uint8_t and asserts the whole 40-byte struct is byte-identical.
        Check("IAT enum underlying substitution EQ_SIZE == 1 (unscoped+scoped -> uint8 twin, whole struct)", "1", g["EQ_SIZE"]);
        Check("IAT enum underlying substitution EQ_ALIGN == 1", "1", g["EQ_ALIGN"]);
        Check("IAT ECATEGORY_OFF == 1 (UNSCOPED enum member == uint8_t at offset 10)", "1", g["ECATEGORY_OFF"]);
        Check("IAT ELOCKFLG_OFF == 1 (SCOPED enum member == uint8_t at offset 12)", "1", g["ELOCKFLG_OFF"]);
        Check("IAT EATTR_OFF == 1 (the Attr array base survives the substitution)", "1", g["EATTR_OFF"]);

        // ---- the 19 CONTAINER_ID literals (unscoped enum): C# underlying byte == compiler (uint8_t)-cast ----
        Check("IAT ENUM LOC_INVENTORY", g["ENUM_LOC_INVENTORY"], ((byte)XI.Map.ContainerId.LOC_INVENTORY).ToString());
        Check("IAT ENUM LOC_MOGSAFE", g["ENUM_LOC_MOGSAFE"], ((byte)XI.Map.ContainerId.LOC_MOGSAFE).ToString());
        Check("IAT ENUM LOC_STORAGE", g["ENUM_LOC_STORAGE"], ((byte)XI.Map.ContainerId.LOC_STORAGE).ToString());
        Check("IAT ENUM LOC_TEMPITEMS", g["ENUM_LOC_TEMPITEMS"], ((byte)XI.Map.ContainerId.LOC_TEMPITEMS).ToString());
        Check("IAT ENUM LOC_MOGLOCKER", g["ENUM_LOC_MOGLOCKER"], ((byte)XI.Map.ContainerId.LOC_MOGLOCKER).ToString());
        Check("IAT ENUM LOC_MOGSATCHEL", g["ENUM_LOC_MOGSATCHEL"], ((byte)XI.Map.ContainerId.LOC_MOGSATCHEL).ToString());
        Check("IAT ENUM LOC_MOGSACK", g["ENUM_LOC_MOGSACK"], ((byte)XI.Map.ContainerId.LOC_MOGSACK).ToString());
        Check("IAT ENUM LOC_MOGCASE", g["ENUM_LOC_MOGCASE"], ((byte)XI.Map.ContainerId.LOC_MOGCASE).ToString());
        Check("IAT ENUM LOC_WARDROBE", g["ENUM_LOC_WARDROBE"], ((byte)XI.Map.ContainerId.LOC_WARDROBE).ToString());
        Check("IAT ENUM LOC_MOGSAFE2", g["ENUM_LOC_MOGSAFE2"], ((byte)XI.Map.ContainerId.LOC_MOGSAFE2).ToString());
        Check("IAT ENUM LOC_WARDROBE2", g["ENUM_LOC_WARDROBE2"], ((byte)XI.Map.ContainerId.LOC_WARDROBE2).ToString());
        Check("IAT ENUM LOC_WARDROBE8", g["ENUM_LOC_WARDROBE8"], ((byte)XI.Map.ContainerId.LOC_WARDROBE8).ToString());
        Check("IAT ENUM LOC_RECYCLEBIN", g["ENUM_LOC_RECYCLEBIN"], ((byte)XI.Map.ContainerId.LOC_RECYCLEBIN).ToString());
        Check("IAT ENUM MAX_CONTAINER_ID", g["ENUM_MAX_CONTAINER_ID"], ((byte)XI.Map.ContainerId.MAX_CONTAINER_ID).ToString());
        // ---- the 6 ItemLockFlg literals (scoped enum): C# underlying byte == compiler (uint8_t)-cast (non-sequential) ----
        Check("IAT ENUM Normal", g["ENUM_Normal"], ((byte)XI.Map.ItemLockFlg.Normal).ToString());
        Check("IAT ENUM NoDrop (non-sequential 0x05)", g["ENUM_NoDrop"], ((byte)XI.Map.ItemLockFlg.NoDrop).ToString());
        Check("IAT ENUM NoSelect (0x0F=15)", g["ENUM_NoSelect"], ((byte)XI.Map.ItemLockFlg.NoSelect).ToString());
        Check("IAT ENUM Linkshell (0x13=19)", g["ENUM_Linkshell"], ((byte)XI.Map.ItemLockFlg.Linkshell).ToString());
        Check("IAT ENUM Unknown0 (0x19=25)", g["ENUM_Unknown0"], ((byte)XI.Map.ItemLockFlg.Unknown0).ToString());
        Check("IAT ENUM Mannequin (0x1B=27)", g["ENUM_Mannequin"], ((byte)XI.Map.ItemLockFlg.Mannequin).ToString());

        // ---- independent re-parse of BOTH enum DEFINING headers (unscoped + scoped), each cross-checked vs compiler ----
        void ReParseEnum(string label, string hdrPath, string countKey, int expectedCount, Dictionary<string, string> gg)
        {
            if (System.IO.File.Exists(hdrPath))
            {
                var re = ParseCppEnum(System.IO.File.ReadAllText(hdrPath), label);
                Check($"IAT {label} re-parse count == {expectedCount}", expectedCount.ToString(), re.Count.ToString());
                foreach (var (name, val) in re)
                {
                    string key = "ENUM_" + name;
                    if (gg.ContainsKey(key))
                        Check($"IAT {label} re-parse {name} == compiler underlying value", gg[key], val.ToString());
                }
            }
            else
            {
                Check($"IAT {label} re-parse target present", "present", "MISSING:" + hdrPath);
            }
        }
        ReParseEnum("CONTAINER_ID", containerHeaderPath, "ARR_String", 19, g);   // UNSCOPED enum -> the new ParseCppEnum fallback path
        ReParseEnum("ItemLockFlg", lockflgHeaderPath, "ItemLockFlg_underlying", 6, g); // SCOPED enum -> the existing enum class path
        VerifyPacketIds("CONTAINER_ID", containerHeaderPath, typeof(XI.Map.ContainerId));
        VerifyPacketIds("ItemLockFlg", lockflgHeaderPath, typeof(XI.Map.ItemLockFlg));

        // ==== whole-struct fill determinism (gapless i*7+3 over the 40-byte body INCLUDING the 3 tail-pad bytes) ====
        byte[] pdFill = HexToBytes(g["PD_FILL"]);
        Check("IAT PD_FILL_SIZE", g["PD_FILL_SIZE"], pdFill.Length.ToString());
        {
            var local = new byte[pdFill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check("IAT PD deterministic fill (gapless i*7+3 INCLUDING the tail pad)", g["PD_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }

        // ---- accessors == raw at the compiler offsets (two independent ways) ----
        Check("IAT GetItemNum == raw u32 at compiler offset", BitConverter.ToUInt32(pdFill, (int)ParseNum(g["OFF_PD.ItemNum"])).ToString(), XI.Map.S2cItemAttr.GetItemNum(pdFill).ToString());
        Check("IAT GetPrice == raw u32 at compiler offset", BitConverter.ToUInt32(pdFill, (int)ParseNum(g["OFF_PD.Price"])).ToString(), XI.Map.S2cItemAttr.GetPrice(pdFill).ToString());
        Check("IAT GetItemNo == raw u16 at compiler offset", BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.ItemNo"])).ToString(), XI.Map.S2cItemAttr.GetItemNo(pdFill).ToString());
        Check("IAT GetCategoryRaw == raw u8 at compiler offset", pdFill[(int)ParseNum(g["OFF_PD.Category"])].ToString(), XI.Map.S2cItemAttr.GetCategoryRaw(pdFill).ToString());
        Check("IAT GetItemIndex == raw u8 at compiler offset", pdFill[(int)ParseNum(g["OFF_PD.ItemIndex"])].ToString(), XI.Map.S2cItemAttr.GetItemIndex(pdFill).ToString());
        Check("IAT GetLockFlgRaw == raw u8 at compiler offset", pdFill[(int)ParseNum(g["OFF_PD.LockFlg"])].ToString(), XI.Map.S2cItemAttr.GetLockFlgRaw(pdFill).ToString());
        // Attr indexed + slice reads == raw byte at the compiler base + index (stride 1).
        for (int i = 0; i < 24; i += 6)
            Check($"IAT GetAttr({i}) == raw u8 at Attr+{i}", pdFill[(int)ParseNum(g["OFF_PD.Attr"]) + i].ToString(), XI.Map.S2cItemAttr.GetAttr(pdFill, i).ToString());
        Check("IAT GetAttr last (23) == raw u8 at Attr+23", pdFill[(int)ParseNum(g["OFF_PD.Attr"]) + 23].ToString(), XI.Map.S2cItemAttr.GetAttr(pdFill, 23).ToString());
        Check("IAT GetAttrSlice[0] == raw byte at OFF Attr", pdFill[(int)ParseNum(g["OFF_PD.Attr"])].ToString(), XI.Map.S2cItemAttr.GetAttrSlice(pdFill)[0].ToString());
        // the enum-typed accessors project the raw underlying byte into the enum value.
        Check("IAT GetCategory == (ContainerId) raw byte at offset 10", ((XI.Map.ContainerId)pdFill[(int)ParseNum(g["OFF_PD.Category"])]).ToString(), XI.Map.S2cItemAttr.GetCategory(pdFill).ToString());
        Check("IAT GetLockFlg == (ItemLockFlg) raw byte at offset 12", ((XI.Map.ItemLockFlg)pdFill[(int)ParseNum(g["OFF_PD.LockFlg"])]).ToString(), XI.Map.S2cItemAttr.GetLockFlg(pdFill).ToString());

        // ==== setters land at the compiler offsets without disturbing neighbours; the tail bytes stay dead ====
        {
            var b = new byte[XI.Map.S2cItemAttr.SizeOf];
            XI.Map.S2cItemAttr.SetItemNum(b, 0xDEADBEEF);                 // u32 @ 0
            XI.Map.S2cItemAttr.SetPrice(b, 0x0BADC0DE);                   // u32 @ 4
            XI.Map.S2cItemAttr.SetItemNo(b, 0x1234);                      // u16 @ 8
            XI.Map.S2cItemAttr.SetCategory(b, XI.Map.ContainerId.LOC_MOGSAFE); // byte @ 10 -> 1
            XI.Map.S2cItemAttr.SetItemIndex(b, 7);                       // byte @ 11
            XI.Map.S2cItemAttr.SetLockFlg(b, XI.Map.ItemLockFlg.Mannequin);   // byte @ 12 -> 0x1B
            XI.Map.S2cItemAttr.SetAttr(b, 0, 0xAA);                       // byte @ 13
            XI.Map.S2cItemAttr.SetAttr(b, 23, 0xBB);                      // byte @ 36 (last named byte)
            Check("IAT SetItemNum lands at compiler offset 0", ((uint)0xDEADBEEF).ToString(), BitConverter.ToUInt32(b, (int)ParseNum(g["OFF_PD.ItemNum"])).ToString());
            Check("IAT SetPrice lands at compiler offset 4", ((uint)0x0BADC0DE).ToString(), BitConverter.ToUInt32(b, (int)ParseNum(g["OFF_PD.Price"])).ToString());
            Check("IAT SetItemNo lands at compiler offset 8", ((ushort)0x1234).ToString(), BitConverter.ToUInt16(b, (int)ParseNum(g["OFF_PD.ItemNo"])).ToString());
            Check("IAT SetCategory(LOC_MOGSAFE) stores byte 1 at offset 10", ((byte)1).ToString(), b[(int)ParseNum(g["OFF_PD.Category"])].ToString());
            Check("IAT GetCategory reads back LOC_MOGSAFE", XI.Map.ContainerId.LOC_MOGSAFE.ToString(), XI.Map.S2cItemAttr.GetCategory(b).ToString());
            Check("IAT SetItemIndex lands at compiler offset 11", ((byte)7).ToString(), b[(int)ParseNum(g["OFF_PD.ItemIndex"])].ToString());
            Check("IAT SetLockFlg(Mannequin) stores byte 0x1B at offset 12", ((byte)0x1B).ToString(), b[(int)ParseNum(g["OFF_PD.LockFlg"])].ToString());
            Check("IAT GetLockFlg reads back Mannequin", XI.Map.ItemLockFlg.Mannequin.ToString(), XI.Map.S2cItemAttr.GetLockFlg(b).ToString());
            // SetCategory must not disturb the adjacent ItemIndex byte; SetLockFlg must not disturb Attr[0].
            Check("IAT SetCategory leaves ItemIndex still 7 (byte-run isolation)", ((byte)7).ToString(), XI.Map.S2cItemAttr.GetItemIndex(b).ToString());
            Check("IAT SetLockFlg leaves Attr[0] still 0xAA (enum<->array isolation)", ((byte)0xAA).ToString(), XI.Map.S2cItemAttr.GetAttr(b, 0).ToString());
            // Attr write isolation: setting Attr[23] must not touch Attr[22] nor spill into the tail pad bytes.
            Check("IAT SetAttr(23) leaves Attr[22] zero (element isolation)", "0", XI.Map.S2cItemAttr.GetAttr(b, 22).ToString());
            // THE tail dead-space isolation: bytes 37, 38, 39 (the compiler tail pad) must stay zero -- no accessor owns them.
            Check("IAT SetAttr(23) leaves tail byte 37 zero (tail dead-space)", "0", b[37].ToString());
            Check("IAT SetAttr(23) leaves tail byte 38 zero (tail dead-space)", "0", b[38].ToString());
            Check("IAT SetAttr(23) leaves tail byte 39 zero (tail dead-space)", "0", b[39].ToString());
            Check("IAT tail pad start == LAST_MEMBER_RUN_OFFSET (byte 37 is the first pad byte)", XI.Map.S2cItemAttr.LastMemberRunOffset.ToString(), "37");
        }
    }

    static void VerifyMapS2cEventStr(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_eventstr_golden.txt");
        Console.WriteLine("\n== S2c eventstr (0x033) parity: C# XI.Map vs layout oracle (FIRST 2D char array String[4][16]: row stride 16 / col stride 1, four-u16-run to 12, trailing u32 Data[8] after the 4-aligned char-array end) ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c eventstr golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; the engine adds the 4-byte header).
        Check("EVS SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- the outer body: DEFAULT packing, alignof 4 (uint32s), dense/hole-free ----
        Check("EVS PD_SIZE", g["PD_SIZE"], XI.Map.S2cEventStr.SizeOf.ToString());
        Check("EVS PD_ALIGN == 4 (from the uint32s)", g["PD_ALIGN"], XI.Map.S2cEventStr.AlignOf.ToString());
        Check("EVS hole-free: PAD_INSERTED == 0", "0", g["PAD_INSERTED"]);
        Check("EVS MEMBER_BYTES == PD_SIZE", g["MEMBER_BYTES"], g["PD_SIZE"]);
        Check("EVS SizeOf % AlignOf == 0 (natural tail)", "1", (XI.Map.S2cEventStr.SizeOf % XI.Map.S2cEventStr.AlignOf == 0 ? 1 : 0).ToString());
        Check("EVS CHAR_SIZE == 1", g["CHAR_SIZE"], "1");
        Check("EVS CHAR_ALIGN == 1", g["CHAR_ALIGN"], "1");

        // ---- leading scalar offsets == the model consts ----
        Check("EVS OFF UniqueNo", g["OFF_PD.UniqueNo"], XI.Map.S2cEventStr.OffUniqueNo.ToString());
        Check("EVS OFF ActIndex == UniqueNo + 4", (ParseNum(g["OFF_PD.UniqueNo"]) + 4).ToString(), XI.Map.S2cEventStr.OffActIndex.ToString());
        Check("EVS OFF EventNum == ActIndex + 2 (u16 run)", (ParseNum(g["OFF_PD.ActIndex"]) + 2).ToString(), XI.Map.S2cEventStr.OffEventNum.ToString());
        Check("EVS OFF EventPara == EventNum + 2", (ParseNum(g["OFF_PD.EventNum"]) + 2).ToString(), XI.Map.S2cEventStr.OffEventPara.ToString());
        Check("EVS OFF Mode == EventPara + 2", (ParseNum(g["OFF_PD.EventPara"]) + 2).ToString(), XI.Map.S2cEventStr.OffMode.ToString());
        // THE four-u16-run reaching the already-4-aligned offset 12 -- the char array (align 1) packs there with NO pad.
        Check("EVS U16_RUN_OFFSET == Mode + 2 (the 10+2==12 run)", (ParseNum(g["OFF_PD.Mode"]) + 2).ToString(), g["U16_RUN_OFFSET"]);
        Check("EVS OFF String == Mode + 2 (four u16s reach 4-aligned 12)", (ParseNum(g["OFF_PD.Mode"]) + 2).ToString(), XI.Map.S2cEventStr.OffString.ToString());
        Check("EVS String offset is 4-aligned (no silent pad before the char array)", "0", (ParseNum(g["OFF_PD.String"]) % 4).ToString());

        // ==== THE FIRST 2D char array: row stride 16, col stride 1, contiguous 4*16==64 ====
        Check("EVS OFF String", g["OFF_PD.String"], XI.Map.S2cEventStr.OffString.ToString());
        Check("EVS ARR_String_ROWS == 4", g["ARR_String_ROWS"], XI.Map.S2cEventStr.StringRows.ToString());
        Check("EVS ARR_String_COLS == 16", g["ARR_String_COLS"], XI.Map.S2cEventStr.StringCols.ToString());
        Check("EVS ARR_String_ROWSTRIDE == 16", g["ARR_String_ROWSTRIDE"], XI.Map.S2cEventStr.StringRowStride.ToString());
        Check("EVS ARR_String_LEN == ROWS * ROWSTRIDE (64)", (ParseNum(g["ARR_String_ROWS"]) * ParseNum(g["ARR_String_ROWSTRIDE"])).ToString(), g["ARR_String_LEN"]);
        Check("EVS ARR_String_LEN == model StringLen", g["ARR_String_LEN"], XI.Map.S2cEventStr.StringLen.ToString());
        // the ROW stride: offsetof(String[1]) - offsetof(String[0]) == 16 (each row is a char[16]).
        Check("EVS OFFROW String[1] == String + ROWSTRIDE (16)", (ParseNum(g["OFF_PD.String"]) + ParseNum(g["ARR_String_ROWSTRIDE"])).ToString(), g["OFFROW.String[1]"]);
        // the COL stride: String[0][1] - String[0][0] == 1.
        Check("EVS OFFCOL String[0][0] == base String", g["OFF_PD.String"], g["OFFCOL.String[0][0]"]);
        Check("EVS OFFCOL String[0][1] == String[0][0] + 1 (col stride 1)", (ParseNum(g["OFFCOL.String[0][0]"]) + 1).ToString(), g["OFFCOL.String[0][1]"]);
        Check("EVS OFFCOL String[1][0] == String + ROWSTRIDE (row-major)", (ParseNum(g["OFF_PD.String"]) + ParseNum(g["ARR_String_ROWSTRIDE"])).ToString(), g["OFFCOL.String[1][0]"]);
        // THE last element: String[3][15] == base + 3*16 + 15 == 75 (proves contiguous row-major packing).
        Check("EVS OFFCOL String[3][15] == base + 3*rowstride + 15 (last byte 75)",
            (ParseNum(g["OFF_PD.String"]) + 3 * ParseNum(g["ARR_String_ROWSTRIDE"]) + 15).ToString(), g["OFFCOL.String[3][15]"]);
        // C# StringOffset(row,col) == compiler OFFCOL for the discriminating cells (two independent ways).
        Check("EVS StringOffset(0,0) == OFFCOL String[0][0]", g["OFFCOL.String[0][0]"], XI.Map.S2cEventStr.StringOffset(0, 0).ToString());
        Check("EVS StringOffset(1,0) == OFFCOL String[1][0]", g["OFFCOL.String[1][0]"], XI.Map.S2cEventStr.StringOffset(1, 0).ToString());
        Check("EVS StringOffset(3,15) == OFFCOL String[3][15]", g["OFFCOL.String[3][15]"], XI.Map.S2cEventStr.StringOffset(3, 15).ToString());
        Check("EVS StringOffset(2,7) == base + 2*16 + 7", (ParseNum(g["OFF_PD.String"]) + 2 * 16 + 7).ToString(), XI.Map.S2cEventStr.StringOffset(2, 7).ToString());

        // ==== trailing uint32 Data[8]: base 76, stride 4, and the char-array-end (76) is 4-aligned -> no pad ====
        Check("EVS STRING_END == String base + 64 (the char array ends at 76)", (ParseNum(g["OFF_PD.String"]) + ParseNum(g["ARR_String_LEN"])).ToString(), g["STRING_END"]);
        Check("EVS OFF Data == STRING_END (already 4-aligned, no silent pad)", g["STRING_END"], XI.Map.S2cEventStr.OffData.ToString());
        Check("EVS OFF Data == OFF_PD.Data", g["STRING_END"], g["OFF_PD.Data"]);
        Check("EVS Data offset is 4-aligned (no silent pad)", "0", (ParseNum(g["OFF_PD.Data"]) % 4).ToString());
        Check("EVS DATA1 == Data + 4 (u32 stride, no inter-element pad)", (ParseNum(g["OFF_PD.Data"]) + 4).ToString(), g["DATA1"]);
        Check("EVS ARR_Data_COUNT == 8", g["ARR_Data_COUNT"], XI.Map.S2cEventStr.DataCount.ToString());
        Check("EVS ARR_Data_ELEMSIZE == 4", g["ARR_Data_ELEMSIZE"], XI.Map.S2cEventStr.DataStride.ToString());
        Check("EVS ARR_Data_LEN == COUNT * 4 (32)", (ParseNum(g["ARR_Data_COUNT"]) * 4).ToString(), g["ARR_Data_LEN"]);
        Check("EVS ARR_Data_LEN == model DataLen", g["ARR_Data_LEN"], XI.Map.S2cEventStr.DataLen.ToString());
        // the Data array reaches the end: base 76 + len 32 == 108 == SizeOf.
        Check("EVS Data + LEN == SizeOf (108, gapless to the end)", "1",
            (XI.Map.S2cEventStr.OffData + XI.Map.S2cEventStr.DataLen == XI.Map.S2cEventStr.SizeOf ? 1 : 0).ToString());

        // ==== whole-struct fill determinism (gapless i*7+3 over the 108-byte body) ====
        byte[] pdFill = HexToBytes(g["PD_FILL"]);
        Check("EVS PD_FILL_SIZE", g["PD_FILL_SIZE"], pdFill.Length.ToString());
        {
            var local = new byte[pdFill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check("EVS PD deterministic fill (gapless i*7+3, proves no internal pad)", g["PD_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }

        // ---- scalar accessors == raw at the compiler offsets ----
        Check("EVS GetUniqueNo == raw u32 at compiler offset", BitConverter.ToUInt32(pdFill, (int)ParseNum(g["OFF_PD.UniqueNo"])).ToString(), XI.Map.S2cEventStr.GetUniqueNo(pdFill).ToString());
        Check("EVS GetActIndex == raw u16 at compiler offset", BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.ActIndex"])).ToString(), XI.Map.S2cEventStr.GetActIndex(pdFill).ToString());
        Check("EVS GetEventNum == raw u16 at compiler offset", BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.EventNum"])).ToString(), XI.Map.S2cEventStr.GetEventNum(pdFill).ToString());
        Check("EVS GetEventPara == raw u16 at compiler offset", BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.EventPara"])).ToString(), XI.Map.S2cEventStr.GetEventPara(pdFill).ToString());
        Check("EVS GetMode == raw u16 at compiler offset", BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.Mode"])).ToString(), XI.Map.S2cEventStr.GetMode(pdFill).ToString());
        // 2D char array element reads == raw byte at the compiler OFFCOL (row-major stride carry).
        Check("EVS GetStringChar(0,0) == raw byte at OFFCOL String[0][0]", pdFill[(int)ParseNum(g["OFFCOL.String[0][0]"])].ToString(), XI.Map.S2cEventStr.GetStringChar(pdFill, 0, 0).ToString());
        Check("EVS GetStringChar(1,0) == raw byte at OFFCOL String[1][0] (row stride)", pdFill[(int)ParseNum(g["OFFCOL.String[1][0]"])].ToString(), XI.Map.S2cEventStr.GetStringChar(pdFill, 1, 0).ToString());
        Check("EVS GetStringChar(3,15) == raw byte at OFFCOL String[3][15] (last cell)", pdFill[(int)ParseNum(g["OFFCOL.String[3][15]"])].ToString(), XI.Map.S2cEventStr.GetStringChar(pdFill, 3, 15).ToString());
        Check("EVS GetStringChar(2,5) == raw byte at base+2*16+5", pdFill[(int)ParseNum(g["OFF_PD.String"]) + 2 * 16 + 5].ToString(), XI.Map.S2cEventStr.GetStringChar(pdFill, 2, 5).ToString());
        // row slice: GetStringRow(r) == the compiler OFFCOL String[r][0] first byte (proves the row base).
        Check("EVS GetStringRow(1)[0] == OFFCOL String[1][0] byte", pdFill[(int)ParseNum(g["OFFCOL.String[1][0]"])].ToString(), XI.Map.S2cEventStr.GetStringRow(pdFill, 1)[0].ToString());
        // whole String matrix slice first byte == OFF String.
        Check("EVS GetStringSlice[0] == OFF String byte", pdFill[(int)ParseNum(g["OFF_PD.String"])].ToString(), XI.Map.S2cEventStr.GetStringSlice(pdFill)[0].ToString());
        // trailing u32 array indexed + slice reads == raw at the compiler offsets.
        Check("EVS GetData(0) == raw u32 at OFF Data", BitConverter.ToUInt32(pdFill, (int)ParseNum(g["OFF_PD.Data"])).ToString(), XI.Map.S2cEventStr.GetData(pdFill, 0).ToString());
        Check("EVS GetData(1) == raw u32 at DATA1 (stride 4)", BitConverter.ToUInt32(pdFill, (int)ParseNum(g["DATA1"])).ToString(), XI.Map.S2cEventStr.GetData(pdFill, 1).ToString());
        Check("EVS GetData(7) == raw u32 at base+7*4 (last element)", BitConverter.ToUInt32(pdFill, (int)ParseNum(g["OFF_PD.Data"]) + 7 * 4).ToString(), XI.Map.S2cEventStr.GetData(pdFill, 7).ToString());
        Check("EVS GetDataSlice[0] == OFF Data byte", pdFill[(int)ParseNum(g["OFF_PD.Data"])].ToString(), XI.Map.S2cEventStr.GetDataSlice(pdFill)[0].ToString());

        // ==== setters land at the compiler offsets without disturbing neighbours / cross-cells ====
        {
            var b = new byte[XI.Map.S2cEventStr.SizeOf];
            XI.Map.S2cEventStr.SetUniqueNo(b, 0xDEADBEEF);
            XI.Map.S2cEventStr.SetMode(b, 0x1234);
            Check("EVS SetUniqueNo lands at compiler offset 0", ((uint)0xDEADBEEF).ToString(), BitConverter.ToUInt32(b, (int)ParseNum(g["OFF_PD.UniqueNo"])).ToString());
            Check("EVS SetMode lands at compiler offset 10", ((ushort)0x1234).ToString(), BitConverter.ToUInt16(b, (int)ParseNum(g["OFF_PD.Mode"])).ToString());
            // 2D char array: write into row 1 col 3, prove it lands at base+1*16+3 and does NOT touch row 0 or row 2.
            XI.Map.S2cEventStr.SetStringChar(b, 1, 3, 0xAB);
            Check("EVS SetStringChar(1,3) lands at base+16+3 (row-major)", ((byte)0xAB).ToString(), b[(int)ParseNum(g["OFF_PD.String"]) + 1 * 16 + 3].ToString());
            Check("EVS SetStringChar(1,3) leaves row 0 col 3 zero (cross-row isolation)", "0", XI.Map.S2cEventStr.GetStringChar(b, 0, 3).ToString());
            Check("EVS SetStringChar(1,3) leaves row 1 col 4 zero (cross-col isolation)", "0", XI.Map.S2cEventStr.GetStringChar(b, 1, 4).ToString());
            XI.Map.S2cEventStr.SetStringChar(b, 3, 15, 0xCD);
            Check("EVS SetStringChar(3,15) lands at OFFCOL String[3][15]", ((byte)0xCD).ToString(), b[(int)ParseNum(g["OFFCOL.String[3][15]"])].ToString());
            // the char array's LAST byte (75) and the Data array's FIRST byte (76) are adjacent -- writing 3/15 must not spill into Data[0].
            Check("EVS SetStringChar(3,15) leaves Data[0]@76 zero (array-boundary isolation)", "0", XI.Map.S2cEventStr.GetData(b, 0).ToString());
            // trailing u32 array: write Data[7], prove it lands at base+7*4 and leaves Data[6] and the char array untouched.
            XI.Map.S2cEventStr.SetData(b, 7, 0x11223344);
            Check("EVS SetData(7) lands at base+7*4 (last element)", ((uint)0x11223344).ToString(), BitConverter.ToUInt32(b, (int)ParseNum(g["OFF_PD.Data"]) + 7 * 4).ToString());
            Check("EVS SetData(7) leaves Data[6] zero (element isolation)", "0", XI.Map.S2cEventStr.GetData(b, 6).ToString());
            Check("EVS SetData(7) leaves String[3][15] still 0xCD (array-boundary isolation)", ((byte)0xCD).ToString(), XI.Map.S2cEventStr.GetStringChar(b, 3, 15).ToString());
            Check("EVS SetData(7) leaves UniqueNo still 0xDEADBEEF (isolation)", ((uint)0xDEADBEEF).ToString(), XI.Map.S2cEventStr.GetUniqueNo(b).ToString());
            // fresh-buffer isolation: writing only row 2 leaves every other row byte-zero.
            {
                var bx = new byte[XI.Map.S2cEventStr.SizeOf];
                XI.Map.S2cEventStr.SetStringChar(bx, 2, 0, 0x55);
                Check("EVS SetStringChar(2,0) leaves row 0 zero (fresh-buffer)", "0", XI.Map.S2cEventStr.GetStringChar(bx, 0, 0).ToString());
                Check("EVS SetStringChar(2,0) leaves row 3 zero (fresh-buffer)", "0", XI.Map.S2cEventStr.GetStringChar(bx, 3, 0).ToString());
                Check("EVS SetStringChar(2,0) leaves Data[0] zero (fresh-buffer)", "0", XI.Map.S2cEventStr.GetData(bx, 0).ToString());
            }
        }
    }

    static void VerifyMapS2cEffect(string dir, string enumHeaderPath)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_effect_enum_golden.txt");
        Console.WriteLine("\n== S2c effect (0x030) parity: C# XI.Map vs layout oracle + synthesis_effect.h enum re-parse (FIRST engine enum with a SIGNED int16 underlying type + two int8_t members + trailing-u16-after-int8-pair) ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c effect golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; the engine adds the 4-byte header).
        Check("EF SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- the outer body: DEFAULT packing, alignof 4 (uint32), dense/hole-free ----
        Check("EF PD_SIZE", g["PD_SIZE"], XI.Map.S2cEffect.SizeOf.ToString());
        Check("EF PD_ALIGN == 4 (from the uint32 UniqueNo)", g["PD_ALIGN"], XI.Map.S2cEffect.AlignOf.ToString());
        Check("EF hole-free: PAD_INSERTED == 0", "0", g["PAD_INSERTED"]);
        Check("EF MEMBER_BYTES == PD_SIZE", g["MEMBER_BYTES"], g["PD_SIZE"]);
        Check("EF SizeOf % AlignOf == 0 (natural tail)", "1", (XI.Map.S2cEffect.SizeOf % XI.Map.S2cEffect.AlignOf == 0 ? 1 : 0).ToString());

        // ---- signed-enum / signed-int8 facts ----
        Check("EF SynthesisEffect underlying size == 2 (int16)", g["ENUM_underlying_size"], "2");
        Check("EF ENUM_underlying_is_signed == 1 (FIRST engine enum with a SIGNED underlying)", "1", g["ENUM_underlying_is_signed"]);
        Check("EF INT8_SIZE == 1", g["INT8_SIZE"], "1");
        Check("EF INT8_IS_SIGNED == 1 (Type/Status are explicitly signed int8_t)", "1", g["INT8_IS_SIGNED"]);

        // ---- every compiler-transcribed offset == the model const ----
        Check("EF OFF UniqueNo", g["OFF_PD.UniqueNo"], XI.Map.S2cEffect.OffUniqueNo.ToString());
        Check("EF OFF ActIndex == UniqueNo + 4", (ParseNum(g["OFF_PD.UniqueNo"]) + 4).ToString(), XI.Map.S2cEffect.OffActIndex.ToString());
        Check("EF OFF EffectNum == ActIndex + 2 (u16 then int16-enum)", (ParseNum(g["OFF_PD.ActIndex"]) + 2).ToString(), XI.Map.S2cEffect.OffEffectNum.ToString());
        Check("EF OFF Type == EffectNum + 2 (int16 then int8)", (ParseNum(g["OFF_PD.EffectNum"]) + 2).ToString(), XI.Map.S2cEffect.OffType.ToString());
        Check("EF OFF Status == Type + 1", (ParseNum(g["OFF_PD.Type"]) + 1).ToString(), XI.Map.S2cEffect.OffStatus.ToString());
        // THE trailing-u16-after-int8-pair fact: Type(1)+Status(1) reach the already-2-aligned running offset -> no silent pad.
        Check("EF OFF Timer == Status + 1", (ParseNum(g["OFF_PD.Status"]) + 1).ToString(), XI.Map.S2cEffect.OffTimer.ToString());
        Check("EF I8_PAIR_RUN_OFFSET == Type + 1 + 1 (the 8+2==10 run)", (ParseNum(g["OFF_PD.Type"]) + 2).ToString(), g["I8_PAIR_RUN_OFFSET"]);
        Check("EF Timer offset is 2-aligned (no silent pad)", "0", (ParseNum(g["OFF_PD.Timer"]) % 2).ToString());
        Check("EF Timer + 2 == SizeOf (reaches the end, gapless)", "1",
            (XI.Map.S2cEffect.OffTimer + 2 == XI.Map.S2cEffect.SizeOf ? 1 : 0).ToString());

        // ---- chunk-21 engine-enum underlying-type substitution RE-CONFIRMED for a SIGNED int16 underlying vs an int16_t twin ----
        Check("EF enum underlying substitution EQ_SIZE == 1", "1", g["EQ_SIZE"]);
        Check("EF enum underlying substitution EQ_ALIGN == 1", "1", g["EQ_ALIGN"]);
        Check("EF enum underlying substitution EEFFECTNUM_OFF == 1", "1", g["EEFFECTNUM_OFF"]);
        Check("EF enum underlying substitution ETIMER_OFF == 1 (twin keeps the trailing u16 @10)", "1", g["ETIMER_OFF"]);

        // ---- the 9 SynthesisEffect literals: C# enum underlying short == compiler (int16_t)-cast (all positive, non-sequential: skips 1..15) ----
        Check("EF ENUM None (C# == compiler cast)", g["ENUM_None"], ((short)XI.Map.SynthesisEffect.None).ToString());
        Check("EF ENUM Water (non-sequential, 0x10=16)", g["ENUM_Water"], ((short)XI.Map.SynthesisEffect.Water).ToString());
        Check("EF ENUM Wind", g["ENUM_Wind"], ((short)XI.Map.SynthesisEffect.Wind).ToString());
        Check("EF ENUM Fire", g["ENUM_Fire"], ((short)XI.Map.SynthesisEffect.Fire).ToString());
        Check("EF ENUM Earth", g["ENUM_Earth"], ((short)XI.Map.SynthesisEffect.Earth).ToString());
        Check("EF ENUM Lightning", g["ENUM_Lightning"], ((short)XI.Map.SynthesisEffect.Lightning).ToString());
        Check("EF ENUM Ice", g["ENUM_Ice"], ((short)XI.Map.SynthesisEffect.Ice).ToString());
        Check("EF ENUM Light", g["ENUM_Light"], ((short)XI.Map.SynthesisEffect.Light).ToString());
        Check("EF ENUM Dark", g["ENUM_Dark"], ((short)XI.Map.SynthesisEffect.Dark).ToString());
        if (System.IO.File.Exists(enumHeaderPath))
        {
            var re = ParseCppEnum(System.IO.File.ReadAllText(enumHeaderPath), "SynthesisEffect");
            Check("EF synthesis_effect.h SynthesisEffect re-parse count == 9", "9", re.Count.ToString());
            foreach (var (name, val) in re)
            {
                string key = "ENUM_" + name;
                if (g.ContainsKey(key))
                    Check($"EF re-parse {name} == compiler underlying value", g[key], val.ToString());
            }
        }
        else
        {
            Check("EF synthesis_effect.h re-parse target present", "present", "MISSING:" + enumHeaderPath);
        }
        VerifyPacketIds("SynthesisEffect", enumHeaderPath, typeof(XI.Map.SynthesisEffect));

        // ==== whole-struct fill determinism (gapless i*7+3 over the 12-byte body) ====
        byte[] pdFill = HexToBytes(g["PD_FILL"]);
        Check("EF PD_FILL_SIZE", g["PD_FILL_SIZE"], pdFill.Length.ToString());
        {
            var local = new byte[pdFill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check("EF PD deterministic fill (gapless i*7+3, proves no internal pad)", g["PD_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }

        // ---- accessors == raw at the compiler offsets (two independent ways). Signed reads use ToInt16 / (sbyte). ----
        Check("EF GetUniqueNo == raw u32 at compiler offset", BitConverter.ToUInt32(pdFill, (int)ParseNum(g["OFF_PD.UniqueNo"])).ToString(), XI.Map.S2cEffect.GetUniqueNo(pdFill).ToString());
        Check("EF GetActIndex == raw u16 at compiler offset", BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.ActIndex"])).ToString(), XI.Map.S2cEffect.GetActIndex(pdFill).ToString());
        Check("EF GetEffectNumRaw == raw SIGNED int16 at compiler offset (short, never ushort)", BitConverter.ToInt16(pdFill, (int)ParseNum(g["OFF_PD.EffectNum"])).ToString(), XI.Map.S2cEffect.GetEffectNumRaw(pdFill).ToString());
        Check("EF GetEffectNum == (SynthesisEffect) raw int16 at compiler offset", ((XI.Map.SynthesisEffect)BitConverter.ToInt16(pdFill, (int)ParseNum(g["OFF_PD.EffectNum"]))).ToString(), XI.Map.S2cEffect.GetEffectNum(pdFill).ToString());
        Check("EF GetType == raw SIGNED byte at compiler offset", unchecked((sbyte)pdFill[(int)ParseNum(g["OFF_PD.Type"])]).ToString(), XI.Map.S2cEffect.GetType(pdFill).ToString());
        Check("EF GetStatus == raw SIGNED byte at compiler offset", unchecked((sbyte)pdFill[(int)ParseNum(g["OFF_PD.Status"])]).ToString(), XI.Map.S2cEffect.GetStatus(pdFill).ToString());
        Check("EF GetTimer == raw u16 at compiler offset", BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.Timer"])).ToString(), XI.Map.S2cEffect.GetTimer(pdFill).ToString());

        // ==== setters land at the compiler offset without disturbing neighbours ====
        {
            var b = new byte[XI.Map.S2cEffect.SizeOf];
            XI.Map.S2cEffect.SetUniqueNo(b, 0xDEADBEEF);       // u32 @ 0
            XI.Map.S2cEffect.SetActIndex(b, 0x1234);           // u16 @ 4
            XI.Map.S2cEffect.SetEffectNum(b, XI.Map.SynthesisEffect.Dark); // int16 @ 6 -> 0x0017
            XI.Map.S2cEffect.SetType(b, -5);                   // int8 @ 8
            XI.Map.S2cEffect.SetStatus(b, -128);               // int8 @ 9 -> 0x80 (most-negative, signed)
            XI.Map.S2cEffect.SetTimer(b, 0x5678);              // u16 @ 10
            Check("EF SetUniqueNo lands at compiler offset 0", ((uint)0xDEADBEEF).ToString(), BitConverter.ToUInt32(b, (int)ParseNum(g["OFF_PD.UniqueNo"])).ToString());
            Check("EF SetActIndex lands at compiler offset 4", ((ushort)0x1234).ToString(), BitConverter.ToUInt16(b, (int)ParseNum(g["OFF_PD.ActIndex"])).ToString());
            // the SIGNED int16-backed enum: Dark=0x0017 stored LE at offset 6, read back via the raw short path.
            Check("EF SetEffectNum(Dark) stores 0x17 0x00 (LE int16) at compiler offset 6", ((byte)0x17).ToString(), b[(int)ParseNum(g["OFF_PD.EffectNum"])].ToString());
            Check("EF GetEffectNum reads back Dark (round-trip)", XI.Map.SynthesisEffect.Dark.ToString(), XI.Map.S2cEffect.GetEffectNum(b).ToString());
            // a signed underlying type is provably SIGNED: write a negative raw int16 and read it back as negative, not unsigned.
            XI.Map.S2cEffect.SetEffectNumRaw(b, -2);           // int16 @ 6 -> 0xFFFE
            Check("EF SetEffectNumRaw(-2) stores 0xFE 0xFF (LE int16)", ((byte)0xFE).ToString(), b[(int)ParseNum(g["OFF_PD.EffectNum"])].ToString());
            Check("EF GetEffectNumRaw reads -2 (SIGNED int16, not 65534)", ((short)(-2)).ToString(), XI.Map.S2cEffect.GetEffectNumRaw(b).ToString());
            Check("EF SetEffectNumRaw leaves Timer@10 untouched (int16<->u16 isolation)", ((ushort)0x5678).ToString(), XI.Map.S2cEffect.GetTimer(b).ToString());
            XI.Map.S2cEffect.SetEffectNum(b, XI.Map.SynthesisEffect.Water);
            // the int8_t members: -5 -> 0xFB, -128 -> 0x80, read back SIGNED (not 251 / not 128).
            Check("EF SetType(-5) stores byte 0xFB at compiler offset 8", ((byte)0xFB).ToString(), b[(int)ParseNum(g["OFF_PD.Type"])].ToString());
            Check("EF GetType reads -5 (SIGNED, not 251)", ((sbyte)(-5)).ToString(), XI.Map.S2cEffect.GetType(b).ToString());
            Check("EF SetStatus(-128) stores byte 0x80 at compiler offset 9", ((byte)0x80).ToString(), b[(int)ParseNum(g["OFF_PD.Status"])].ToString());
            Check("EF GetStatus reads -128 (SIGNED, not 128)", ((sbyte)(-128)).ToString(), XI.Map.S2cEffect.GetStatus(b).ToString());
            // the trailing u16 write must not disturb the two int8s.
            Check("EF SetTimer leaves Type still -5", ((sbyte)(-5)).ToString(), XI.Map.S2cEffect.GetType(b).ToString());
            Check("EF SetTimer leaves Status still -128", ((sbyte)(-128)).ToString(), XI.Map.S2cEffect.GetStatus(b).ToString());
            // the int8 pair isolation: writing Type must not touch Status (adjacent byte).
            XI.Map.S2cEffect.SetType(b, 42);
            Check("EF SetType(42) leaves Status still -128 (i8 pair isolation)", ((sbyte)(-128)).ToString(), XI.Map.S2cEffect.GetStatus(b).ToString());
            // fresh-buffer isolation: writing only EffectNum leaves the neighbouring ActIndex u16 and the Timer u16 zero.
            {
                var bx = new byte[XI.Map.S2cEffect.SizeOf];
                XI.Map.S2cEffect.SetEffectNum(bx, XI.Map.SynthesisEffect.Ice);
                Check("EF SetEffectNum leaves ActIndex@4 zero (enum isolation)", "0", XI.Map.S2cEffect.GetActIndex(bx).ToString());
                Check("EF SetEffectNum leaves Timer@10 zero (enum isolation)", "0", XI.Map.S2cEffect.GetTimer(bx).ToString());
            }
        }
    }

    static void VerifyMapS2cWpos2(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_wpos2_golden.txt");
        Console.WriteLine("\n== S2c wpos2 (0x065) parity: C# XI.Map vs layout oracle (0x05b wpos SIBLING -- identical 24/4, Mode declared as PLAIN uint8 here vs the POSMODE enum in wpos -> chunk-21 substitution proved on a REAL engine pair, no enum re-parse) ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c wpos2 golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; the engine adds the 4-byte header).
        Check("WP2 SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- the outer body: DEFAULT packing, alignof 4 (floats + uint32s), dense/hole-free ----
        Check("WP2 PD_SIZE", g["PD_SIZE"], XI.Map.S2cWpos2.SizeOf.ToString());
        Check("WP2 PD_ALIGN == 4 (from the floats/uint32s)", g["PD_ALIGN"], XI.Map.S2cWpos2.AlignOf.ToString());
        Check("WP2 hole-free: PAD_INSERTED == 0", "0", g["PAD_INSERTED"]);
        Check("WP2 MEMBER_BYTES == PD_SIZE", g["MEMBER_BYTES"], g["PD_SIZE"]);
        Check("WP2 SizeOf % AlignOf == 0 (natural tail)", "1", (XI.Map.S2cWpos2.SizeOf % XI.Map.S2cWpos2.AlignOf == 0 ? 1 : 0).ToString());

        // ---- float / signed-char facts (the sibling shares them with wpos) ----
        Check("WP2 FLOAT_SIZE == 4", g["FLOAT_SIZE"], "4");
        Check("WP2 FLOAT_ALIGN == 4", g["FLOAT_ALIGN"], "4");
        Check("WP2 CHAR_IS_SIGNED == 1 (dir is a signed char -> sbyte)", "1", g["CHAR_IS_SIGNED"]);

        // ---- every compiler-transcribed offset == the model const ----
        Check("WP2 OFF x", g["OFF_PD.x"], XI.Map.S2cWpos2.OffX.ToString());
        Check("WP2 OFF y == x + 4 (float stride)", (ParseNum(g["OFF_PD.x"]) + 4).ToString(), XI.Map.S2cWpos2.OffY.ToString());
        Check("WP2 OFF z == y + 4 (float stride)", (ParseNum(g["OFF_PD.y"]) + 4).ToString(), XI.Map.S2cWpos2.OffZ.ToString());
        Check("WP2 OFF UniqueNo == z + 4", (ParseNum(g["OFF_PD.z"]) + 4).ToString(), XI.Map.S2cWpos2.OffUniqueNo.ToString());
        Check("WP2 OFF ActIndex == UniqueNo + 4", (ParseNum(g["OFF_PD.UniqueNo"]) + 4).ToString(), XI.Map.S2cWpos2.OffActIndex.ToString());
        Check("WP2 OFF Mode == ActIndex + 2 (u16 then u8)", (ParseNum(g["OFF_PD.ActIndex"]) + 2).ToString(), XI.Map.S2cWpos2.OffMode.ToString());
        Check("WP2 OFF dir == Mode + 1", (ParseNum(g["OFF_PD.Mode"]) + 1).ToString(), XI.Map.S2cWpos2.OffDir.ToString());
        Check("WP2 OFF padding18 == dir + 1", (ParseNum(g["OFF_PD.dir"]) + 1).ToString(), XI.Map.S2cWpos2.OffPadding18.ToString());
        Check("WP2 U8_PAIR_RUN_OFFSET == Mode + 1 + 1 (the 18+2==20 run)", (ParseNum(g["OFF_PD.Mode"]) + 2).ToString(), g["U8_PAIR_RUN_OFFSET"]);
        Check("WP2 padding18 offset is 4-aligned (no silent pad)", "0", (ParseNum(g["OFF_PD.padding18"]) % 4).ToString());
        Check("WP2 padding18 + 4 == SizeOf (reaches the end, gapless)", "1",
            (XI.Map.S2cWpos2.OffPadding18 + 4 == XI.Map.S2cWpos2.SizeOf ? 1 : 0).ToString());

        // ==== THE genuinely-new pin: the chunk-21 underlying-type substitution proved on a REAL engine pair ====
        // 0x05b_wpos.h declares `POSMODE Mode`; 0x065_wpos2.h declares `uint8_t Mode` at the SAME slot. The oracle
        // compiles BOTH real structs and asserts they are byte-for-byte identical (not a hand-authored twin).
        Check("WP2 the real wpos (enum Mode) twin has the same size", g["WPOS_SIZE"], g["PD_SIZE"]);
        Check("WP2 the real wpos (enum Mode) twin has the same align", g["WPOS_ALIGN"], g["PD_ALIGN"]);
        Check("WP2 PAIR_EQ_SIZE == 1 (enum-Mode wpos == plain-u8-Mode wpos2, whole struct)", "1", g["PAIR_EQ_SIZE"]);
        Check("WP2 PAIR_EQ_ALIGN == 1", "1", g["PAIR_EQ_ALIGN"]);
        // the discriminating slot itself: enum Mode @18 vs plain uint8 Mode @18 -> same offset (the substitution crux).
        Check("WP2 PAIR_EQ_Mode == 1 (POSMODE member and uint8_t member share offset 18)", "1", g["PAIR_EQ_Mode"]);
        Check("WP2 PAIR_EQ_x == 1", "1", g["PAIR_EQ_x"]);
        Check("WP2 PAIR_EQ_y == 1", "1", g["PAIR_EQ_y"]);
        Check("WP2 PAIR_EQ_z == 1", "1", g["PAIR_EQ_z"]);
        Check("WP2 PAIR_EQ_UniqueNo == 1", "1", g["PAIR_EQ_UniqueNo"]);
        Check("WP2 PAIR_EQ_ActIndex == 1", "1", g["PAIR_EQ_ActIndex"]);
        Check("WP2 PAIR_EQ_dir == 1", "1", g["PAIR_EQ_dir"]);
        Check("WP2 PAIR_EQ_padding18 == 1", "1", g["PAIR_EQ_padding18"]);
        Check("WP2 plain uint8 Mode underlying size == 1", g["MODE_UNDERLYING_size"], "1");
        Check("WP2 POSMODE enum underlying size == 1 (matches the plain uint8 slot)", g["MODE_ENUM_underlying_size"], "1");
        // the two real structs produce the SAME 24-byte i*7+3 fill image -- byte-for-byte identical wire layout.
        Check("WP2 PD_FILL == PD_WPOS_FILL (real engine pair byte-identical)", g["PD_FILL"], g["PD_WPOS_FILL"]);
        // and the sibling's layout matches the ALREADY-PORTED 0x05b wpos model exactly.
        Check("WP2 S2cWpos2.SizeOf == S2cWpos.SizeOf (0x05b sibling)", XI.Map.S2cWpos.SizeOf.ToString(), XI.Map.S2cWpos2.SizeOf.ToString());
        Check("WP2 S2cWpos2.OffMode == S2cWpos.OffMode (same enum/byte slot)", XI.Map.S2cWpos.OffMode.ToString(), XI.Map.S2cWpos2.OffMode.ToString());
        Check("WP2 S2cWpos2.OffPadding18 == S2cWpos.OffPadding18", XI.Map.S2cWpos.OffPadding18.ToString(), XI.Map.S2cWpos2.OffPadding18.ToString());

        // ==== whole-struct fill determinism (gapless i*7+3 over the 24-byte body) ====
        byte[] pdFill = HexToBytes(g["PD_FILL"]);
        Check("WP2 PD_FILL_SIZE", g["PD_FILL_SIZE"], pdFill.Length.ToString());
        {
            var local = new byte[pdFill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check("WP2 PD deterministic fill (gapless i*7+3, proves no internal pad)", g["PD_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }

        // ---- float / scalar accessors == raw at the compiler offsets (two independent ways) ----
        Check("WP2 GetX == raw float at compiler offset", BitConverter.ToSingle(pdFill, (int)ParseNum(g["OFF_PD.x"])).ToString(), XI.Map.S2cWpos2.GetX(pdFill).ToString());
        Check("WP2 GetY == raw float at compiler offset", BitConverter.ToSingle(pdFill, (int)ParseNum(g["OFF_PD.y"])).ToString(), XI.Map.S2cWpos2.GetY(pdFill).ToString());
        Check("WP2 GetZ == raw float at compiler offset", BitConverter.ToSingle(pdFill, (int)ParseNum(g["OFF_PD.z"])).ToString(), XI.Map.S2cWpos2.GetZ(pdFill).ToString());
        Check("WP2 GetUniqueNo == raw u32 at compiler offset", BitConverter.ToUInt32(pdFill, (int)ParseNum(g["OFF_PD.UniqueNo"])).ToString(), XI.Map.S2cWpos2.GetUniqueNo(pdFill).ToString());
        Check("WP2 GetActIndex == raw u16 at compiler offset", BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.ActIndex"])).ToString(), XI.Map.S2cWpos2.GetActIndex(pdFill).ToString());
        Check("WP2 GetMode == raw u8 at compiler offset (plain uint8 Mode)", pdFill[(int)ParseNum(g["OFF_PD.Mode"])].ToString(), XI.Map.S2cWpos2.GetMode(pdFill).ToString());
        Check("WP2 GetDir == raw SIGNED byte at compiler offset", unchecked((sbyte)pdFill[(int)ParseNum(g["OFF_PD.dir"])]).ToString(), XI.Map.S2cWpos2.GetDir(pdFill).ToString());
        Check("WP2 GetPadding18 == raw u32 at compiler offset", BitConverter.ToUInt32(pdFill, (int)ParseNum(g["OFF_PD.padding18"])).ToString(), XI.Map.S2cWpos2.GetPadding18(pdFill).ToString());

        // ==== setters land at the compiler offset without disturbing neighbours (finite floats to keep ToString stable) ====
        {
            var b = new byte[XI.Map.S2cWpos2.SizeOf];
            // fresh-buffer isolation: writing only X (the first float) must leave y@4 and z@8 byte-zero.
            {
                var bx = new byte[XI.Map.S2cWpos2.SizeOf];
                XI.Map.S2cWpos2.SetX(bx, 1.5f);
                Check("WP2 SetX lands 1.5f at compiler offset 0", (1.5f).ToString(), BitConverter.ToSingle(bx, (int)ParseNum(g["OFF_PD.x"])).ToString());
                Check("WP2 SetX leaves y@4 byte-zero (float isolation)", "0", BitConverter.ToSingle(bx, (int)ParseNum(g["OFF_PD.y"])).ToString());
                Check("WP2 SetX leaves z@8 byte-zero (float isolation)", "0", BitConverter.ToSingle(bx, (int)ParseNum(g["OFF_PD.z"])).ToString());
            }
            XI.Map.S2cWpos2.SetX(b, 1.5f);              // float @ 0
            XI.Map.S2cWpos2.SetY(b, -3.25f);            // float @ 4
            XI.Map.S2cWpos2.SetUniqueNo(b, 0xDEADBEEF); // u32 @ 12
            XI.Map.S2cWpos2.SetActIndex(b, 0x1234);     // u16 @ 16
            XI.Map.S2cWpos2.SetMode(b, 0x9F);           // PLAIN uint8 @ 18 (a raw byte value, not an enum literal)
            XI.Map.S2cWpos2.SetDir(b, -5);              // signed char @ 19
            Check("WP2 SetY lands (round-trip)", (-3.25f).ToString(), XI.Map.S2cWpos2.GetY(b).ToString());
            Check("WP2 SetUniqueNo lands at compiler offset 12", ((uint)0xDEADBEEF).ToString(), BitConverter.ToUInt32(b, (int)ParseNum(g["OFF_PD.UniqueNo"])).ToString());
            Check("WP2 SetActIndex lands at compiler offset 16", ((ushort)0x1234).ToString(), BitConverter.ToUInt16(b, (int)ParseNum(g["OFF_PD.ActIndex"])).ToString());
            // the PLAIN uint8 Mode: a raw byte value round-trips through the accessor (no enum coercion).
            Check("WP2 SetMode(0x9F) stores byte 0x9F at compiler offset 18", ((byte)0x9F).ToString(), b[(int)ParseNum(g["OFF_PD.Mode"])].ToString());
            Check("WP2 GetMode reads back 0x9F (plain uint8)", ((byte)0x9F).ToString(), XI.Map.S2cWpos2.GetMode(b).ToString());
            // the signed char: -5 stored as byte 0xFB, read back as sbyte -5 (NOT 251).
            Check("WP2 SetDir(-5) stores byte 0xFB at compiler offset 19", ((byte)0xFB).ToString(), b[(int)ParseNum(g["OFF_PD.dir"])].ToString());
            Check("WP2 GetDir reads -5 (SIGNED, not 251)", ((sbyte)(-5)).ToString(), XI.Map.S2cWpos2.GetDir(b).ToString());
            // a write into the u8 Mode byte must not touch the adjacent signed dir byte or the trailing u32.
            Check("WP2 SetMode leaves padding18@20 zero (isolation)", "0", XI.Map.S2cWpos2.GetPadding18(b).ToString());
            // the trailing u32 write must not disturb Mode/dir.
            XI.Map.S2cWpos2.SetPadding18(b, 0x11223344);
            Check("WP2 SetPadding18 lands at compiler offset 20", ((uint)0x11223344).ToString(), BitConverter.ToUInt32(b, (int)ParseNum(g["OFF_PD.padding18"])).ToString());
            Check("WP2 SetPadding18 leaves Mode still 0x9F", ((byte)0x9F).ToString(), XI.Map.S2cWpos2.GetMode(b).ToString());
            Check("WP2 SetPadding18 leaves dir still -5", ((sbyte)(-5)).ToString(), XI.Map.S2cWpos2.GetDir(b).ToString());
        }
    }

    static void VerifyMapS2cEntityVis(string dir)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_entity_vis_golden.txt");
        Console.WriteLine("\n== S2c entity_vis (0x077) parity: C# XI.Map vs layout oracle (FIRST declared-padding-bridge to a uint32[] array, dense DEFAULT-packed 132/4) ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c entity_vis golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; the engine adds the 4-byte header).
        Check("EV SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- the outer body: DEFAULT packing, whole-struct alignof 4 (from the u32 array element), dense/hole-free ----
        Check("EV PD_SIZE", g["PD_SIZE"], XI.Map.S2cEntityVis.SizeOf.ToString());
        Check("EV PD_ALIGN == 4 (from the uint32 array element, not the leading u8s)", g["PD_ALIGN"], XI.Map.S2cEntityVis.AlignOf.ToString());
        Check("EV hole-free: PAD_INSERTED == 0", "0", g["PAD_INSERTED"]);
        Check("EV MEMBER_BYTES == PD_SIZE", g["MEMBER_BYTES"], g["PD_SIZE"]);
        Check("EV SizeOf % AlignOf == 0 (natural tail)", "1", (XI.Map.S2cEntityVis.SizeOf % XI.Map.S2cEntityVis.AlignOf == 0 ? 1 : 0).ToString());

        // ---- uint8 Flags (the leading scalar): offset 0 ----
        Check("EV OFF Flags", g["OFF_PD.Flags"], XI.Map.S2cEntityVis.OffFlags.ToString());

        // ---- uint8 padding05[3] (compiler OFF_PD.padding05=1): stride 1, count 3 ----
        Check("EV OFF padding05 == Flags + 1 (u8)", (ParseNum(g["OFF_PD.Flags"]) + 1).ToString(), XI.Map.S2cEntityVis.OffPadding05.ToString());
        Check("EV padding05[1] == padding05 + 1 (u8 stride)", (ParseNum(g["OFF_PD.padding05"]) + 1).ToString(), g["OFF_PD.padding05[1]"]);
        Check("EV ARR_padding05_COUNT == 3", g["ARR_padding05_COUNT"], XI.Map.S2cEntityVis.Padding05Len.ToString());
        Check("EV ARR_padding05_ELEMSIZE == 1", g["ARR_padding05_ELEMSIZE"], "1");

        // ---- THE genuinely-new pin: the declared padding05 BRIDGES the running offset to the u32 array's 4-align ----
        Check("EV OFF UniqueNo == PADDING_BRIDGE (4, the u32[] lands where the declared padding leaves it)", g["OFF_PD.UniqueNo"], g["PADDING_BRIDGE"]);
        Check("EV OFF UniqueNo == Flags + 1 + padding05_LEN (0+1+3)", (ParseNum(g["OFF_PD.Flags"]) + 1 + ParseNum(g["ARR_padding05_LEN"])).ToString(), XI.Map.S2cEntityVis.OffUniqueNo.ToString());
        Check("EV model PaddingBridge == compiler OFF UniqueNo (4)", XI.Map.S2cEntityVis.PaddingBridge.ToString(), g["OFF_PD.UniqueNo"]);
        Check("EV the u32 array base is 4-aligned (no silent pad -> PAD_INSERTED==0)", "0", (ParseNum(g["OFF_PD.UniqueNo"]) % 4).ToString());

        // ---- uint32 UniqueNo[32] (THE first padding-bridge-to-a-u32-array member): stride 4, no inter-element pad ----
        Check("EV UniqueNo[1] == UniqueNo + 4 (u32 stride, no gap)", (ParseNum(g["OFF_PD.UniqueNo"]) + 4).ToString(), g["OFF_PD.UniqueNo[1]"]);
        Check("EV ARR_UniqueNo_COUNT == 32 (largest fixed u32[] in an S2C body)", g["ARR_UniqueNo_COUNT"], XI.Map.S2cEntityVis.UniqueNoCount.ToString());
        Check("EV ARR_UniqueNo_ELEMSIZE == 4", g["ARR_UniqueNo_ELEMSIZE"], XI.Map.S2cEntityVis.UniqueNoStride.ToString());
        Check("EV ARR_UniqueNo_LEN == COUNT * 4 (128)", (ParseNum(g["ARR_UniqueNo_COUNT"]) * 4).ToString(), g["ARR_UniqueNo_LEN"]);
        Check("EV ARR_UniqueNo_LEN == model UniqueNoLen", g["ARR_UniqueNo_LEN"], XI.Map.S2cEntityVis.UniqueNoLen.ToString());
        // the array reaches the end: base 4 + len 128 == 132 == SizeOf.
        Check("EV UniqueNo + LEN == SizeOf (132, gapless to the end)", "1",
            (XI.Map.S2cEntityVis.OffUniqueNo + XI.Map.S2cEntityVis.UniqueNoLen == XI.Map.S2cEntityVis.SizeOf ? 1 : 0).ToString());

        // ==== whole-struct fill determinism (gapless i*7+3 over the 132-byte body) ====
        byte[] pdFill = HexToBytes(g["PD_FILL"]);
        Check("EV PD_FILL_SIZE", g["PD_FILL_SIZE"], pdFill.Length.ToString());
        {
            var local = new byte[pdFill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check("EV PD deterministic fill (gapless i*7+3, proves no internal pad)", g["PD_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }

        // ---- the UniqueNoOffset helper's stride arithmetic agrees with the compiler base + index*elemsize ----
        foreach (var idx in new int[] { 0, 16, 31 })
        {
            int compilerOff = (int)(ParseNum(g["OFF_PD.UniqueNo"]) + idx * ParseNum(g["ARR_UniqueNo_ELEMSIZE"]));
            Check($"EV UniqueNoOffset({idx}) == compiler base + {idx}*stride", compilerOff.ToString(), XI.Map.S2cEntityVis.UniqueNoOffset(idx).ToString());
        }

        // ---- Flags u8 + padding slice + u32[] element accessors == raw at the compiler offsets (two independent ways) ----
        Check("EV GetFlags == raw u8 at compiler offset 0", pdFill[(int)ParseNum(g["OFF_PD.Flags"])].ToString(), XI.Map.S2cEntityVis.GetFlags(pdFill).ToString());
        Check("EV GetPadding05 == raw bytes 1..3", BitConverter.ToString(pdFill, (int)ParseNum(g["OFF_PD.padding05"]), XI.Map.S2cEntityVis.Padding05Len),
            BitConverter.ToString(XI.Map.S2cEntityVis.GetPadding05(pdFill).ToArray()));
        foreach (var idx in new int[] { 0, 16, 31 })
        {
            int off = (int)(ParseNum(g["OFF_PD.UniqueNo"]) + idx * ParseNum(g["ARR_UniqueNo_ELEMSIZE"]));
            Check($"EV GetUniqueNo[{idx}] == raw u32 at {off}",
                BitConverter.ToUInt32(pdFill, off).ToString(), XI.Map.S2cEntityVis.GetUniqueNo(pdFill, idx).ToString());
        }
        Check("EV GetUniqueNoSlice == raw bytes 4..131", BitConverter.ToString(pdFill, (int)ParseNum(g["OFF_PD.UniqueNo"]), XI.Map.S2cEntityVis.UniqueNoLen),
            BitConverter.ToString(XI.Map.S2cEntityVis.GetUniqueNoSlice(pdFill).ToArray()));

        // ==== setters land at the compiler offset without disturbing neighbours ====
        {
            var b = new byte[XI.Map.S2cEntityVis.SizeOf];
            XI.Map.S2cEntityVis.SetFlags(b, 0x11);           // u8 @ 0
            XI.Map.S2cEntityVis.SetU8(b, 3, 0x22);           // padding05 last byte (byte 3) -- must NOT spill into UniqueNo[0]@4
            XI.Map.S2cEntityVis.SetUniqueNo(b, 16, 0xAABBCCDD); // u32 @ 4 + 16*4 == 68
            XI.Map.S2cEntityVis.SetUniqueNo(b, 31, 0x11223344); // u32 @ 4 + 31*4 == 128 (last element)
            Check("EV SetFlags lands at compiler offset 0", ((byte)0x11).ToString(), b[0].ToString());
            // THE bridge isolation: writing padding05's last byte (3) must leave the u32 array's first element (bytes 4..7) zero.
            Check("EV SetU8(padding05 byte 3) leaves UniqueNo[0]@4 zero (bridge no-spill)", "0", XI.Map.S2cEntityVis.GetUniqueNo(b, 0).ToString());
            Check("EV SetUniqueNo[16] lands at compiler offset 68", ((uint)0xAABBCCDD).ToString(),
                BitConverter.ToUInt32(b, (int)(ParseNum(g["OFF_PD.UniqueNo"]) + 64)).ToString());
            Check("EV SetUniqueNo[31] lands at compiler offset 128 (last u32 element)", ((uint)0x11223344).ToString(),
                BitConverter.ToUInt32(b, (int)(ParseNum(g["OFF_PD.UniqueNo"]) + 124)).ToString());
            // a u32 array write must not touch the leading scalar byte or the padding.
            Check("EV SetUniqueNo leaves Flags byte 0 untouched (0x11)", ((byte)0x11).ToString(), XI.Map.S2cEntityVis.GetFlags(b).ToString());
            Check("EV SetUniqueNo leaves padding05 byte 3 untouched (0x22)", ((byte)0x22).ToString(), b[3].ToString());
            // round-trips through the stride arithmetic.
            Check("EV SetUniqueNo[16] round-trip", ((uint)0xAABBCCDD).ToString(), XI.Map.S2cEntityVis.GetUniqueNo(b, 16).ToString());
            Check("EV SetFlags round-trip", ((byte)0x11).ToString(), XI.Map.S2cEntityVis.GetFlags(b).ToString());
        }
    }

    static void VerifyMapS2cItemTradeRes(string dir, string enumHeaderPath)
    {
        string path = System.IO.Path.Combine(dir ?? "", "map_s2c_item_trade_res_enum_golden.txt");
        Console.WriteLine("\n== S2c item_trade_res (0x022) parity: C# XI.Map vs layout oracle + 0x022_item_trade_res.h enum re-parse (FIRST fixed-width S2C body with a COMPILER tail pad: u32 + u32-backed enum + u16 -> 10 named bytes padded to 12) ==");
        if (!System.IO.File.Exists(path))
        {
            Check("s2c item_trade_res enum golden present", "present", "MISSING:" + path);
            return;
        }

        var g = new Dictionary<string, string>();
        foreach (var raw in System.IO.File.ReadAllLines(path))
        {
            int eq = raw.IndexOf('=');
            if (eq > 0) g[raw.Substring(0, eq)] = raw.Substring(eq + 1);
        }

        // shared S2C header framing == C2S/CBasicPacket (body-relative port; the engine adds the 4-byte header).
        Check("ITR SHDR_SIZE", g["SHDR_SIZE"], XI.Map.C2sPos.HeaderSize.ToString());

        // ---- the outer body: DEFAULT packing, alignof 4, WITH a compiler-inserted TAIL PAD (the genuinely-new pin) ----
        Check("ITR PD_SIZE", g["PD_SIZE"], XI.Map.S2cItemTradeRes.SizeOf.ToString());
        Check("ITR PD_ALIGN == 4 (from the u32 scalar AND the u32-backed enum)", g["PD_ALIGN"], XI.Map.S2cItemTradeRes.AlignOf.ToString());
        Check("ITR HAS_TAIL_PAD == 1 (first fixed-width S2C body with compiler pad)", "1", g["HAS_TAIL_PAD"]);
        Check("ITR PAD_INSERTED == 2 (NON-ZERO: size 12 != member sum 10)", "2", g["PAD_INSERTED"]);
        Check("ITR MEMBER_BYTES == 10 (u32+u32+u16 named sum, less than PD_SIZE)", "10", g["MEMBER_BYTES"]);
        Check("ITR MEMBER_BYTES != PD_SIZE (the pad is real, not absorbed by a declared array)", "1",
            (ParseNum(g["MEMBER_BYTES"]) != ParseNum(g["PD_SIZE"]) ? 1 : 0).ToString());

        // ---- THE tail-pad arithmetic (all compiler-derived, never hand-summed) ----
        Check("ITR TAIL_PAD_BYTES == PD_SIZE - LAST_MEMBER_RUN_OFFSET (12-10)", (ParseNum(g["PD_SIZE"]) - ParseNum(g["LAST_MEMBER_RUN_OFFSET"])).ToString(), g["TAIL_PAD_BYTES"]);
        Check("ITR PAD_INSERTED == TAIL_PAD_BYTES (the only pad IS the tail)", g["PAD_INSERTED"], g["TAIL_PAD_BYTES"]);
        Check("ITR LAST_MEMBER_RUN_OFFSET == ActIndex + 2 (8+2)", (ParseNum(g["OFF_PD.ActIndex"]) + 2).ToString(), g["LAST_MEMBER_RUN_OFFSET"]);
        Check("ITR the last member's run offset 10 is NOT 4-aligned (RUN_MOD_ALIGN==2 -> why pad is needed)", "2", g["RUN_MOD_ALIGN"]);
        Check("ITR model LastMemberRunOffset == compiler (10)", g["LAST_MEMBER_RUN_OFFSET"], XI.Map.S2cItemTradeRes.LastMemberRunOffset.ToString());
        Check("ITR model TailPadBytes == compiler TAIL_PAD_BYTES (2)", g["TAIL_PAD_BYTES"], XI.Map.S2cItemTradeRes.TailPadBytes.ToString());
        Check("ITR PD_SIZE % PD_ALIGN == 0 (struct rounds UP to its align: 10 -> 12)", "0", (ParseNum(g["PD_SIZE"]) % ParseNum(g["PD_ALIGN"])).ToString());

        // ---- the three member offsets ----
        Check("ITR OFF UniqueNo", g["OFF_PD.UniqueNo"], XI.Map.S2cItemTradeRes.OffUniqueNo.ToString());
        Check("ITR OFF Kind (u32-backed enum: packs tight after the u32 at 4)", (ParseNum(g["OFF_PD.UniqueNo"]) + 4).ToString(), XI.Map.S2cItemTradeRes.OffKind.ToString());
        Check("ITR OFF ActIndex (u16 after the u32 enum, at 8)", (ParseNum(g["OFF_PD.Kind"]) + 4).ToString(), XI.Map.S2cItemTradeRes.OffActIndex.ToString());

        // ---- chunk-21 engine-enum underlying-type substitution RE-CONFIRMED at the uint32 width ----
        Check("ITR Kind underlying size 4 (uint32_t)", g["ENUM_underlying_size"], "4");
        Check("ITR enum underlying substitution EQ_SIZE == 1", "1", g["EQ_SIZE"]);
        Check("ITR enum underlying substitution EQ_ALIGN == 1", "1", g["EQ_ALIGN"]);
        Check("ITR enum underlying substitution EKIND_OFF == 1", "1", g["EKIND_OFF"]);

        // ---- the 10 sequential literals: C# enum underlying uint == compiler (uint32_t)-cast ----
        Check("ITR ENUM Start", g["ENUM_Start"], ((uint)XI.Map.ItemTradeResKind.Start).ToString());
        Check("ITR ENUM Cancell", g["ENUM_Cancell"], ((uint)XI.Map.ItemTradeResKind.Cancell).ToString());
        Check("ITR ENUM Make", g["ENUM_Make"], ((uint)XI.Map.ItemTradeResKind.Make).ToString());
        Check("ITR ENUM MakeCancell", g["ENUM_MakeCancell"], ((uint)XI.Map.ItemTradeResKind.MakeCancell).ToString());
        Check("ITR ENUM ErrEtc", g["ENUM_ErrEtc"], ((uint)XI.Map.ItemTradeResKind.ErrEtc).ToString());
        Check("ITR ENUM ErrNoSearchYou", g["ENUM_ErrNoSearchYou"], ((uint)XI.Map.ItemTradeResKind.ErrNoSearchYou).ToString());
        Check("ITR ENUM ErrNowReq", g["ENUM_ErrNowReq"], ((uint)XI.Map.ItemTradeResKind.ErrNowReq).ToString());
        Check("ITR ENUM ErrYouTrade", g["ENUM_ErrYouTrade"], ((uint)XI.Map.ItemTradeResKind.ErrYouTrade).ToString());
        Check("ITR ENUM ErrLogout", g["ENUM_ErrLogout"], ((uint)XI.Map.ItemTradeResKind.ErrLogout).ToString());
        Check("ITR ENUM End", g["ENUM_End"], ((uint)XI.Map.ItemTradeResKind.End).ToString());
        if (System.IO.File.Exists(enumHeaderPath))
        {
            var re = ParseCppEnum(System.IO.File.ReadAllText(enumHeaderPath), "GP_ITEM_TRADE_RES_KIND");
            Check("ITR 0x022_item_trade_res.h re-parse count == 10", "10", re.Count.ToString());
            foreach (var (name, val) in re)
            {
                string key = "ENUM_" + name;
                if (g.ContainsKey(key))
                    Check($"ITR re-parse {name} == compiler underlying value", g[key], val.ToString());
            }
        }
        else
        {
            Check("ITR 0x022_item_trade_res.h re-parse target present", "present", "MISSING:" + enumHeaderPath);
        }
        VerifyPacketIds("GP_ITEM_TRADE_RES_KIND", enumHeaderPath, typeof(XI.Map.ItemTradeResKind));

        // ==== whole-struct fill determinism (gapless i*7+3 over the 12-byte body, INCLUDING the 2 tail-pad bytes) ====
        byte[] pdFill = HexToBytes(g["PD_FILL"]);
        Check("ITR PD_FILL_SIZE", g["PD_FILL_SIZE"], pdFill.Length.ToString());
        {
            var local = new byte[pdFill.Length];
            for (int i = 0; i < local.Length; i++) local[i] = unchecked((byte)(i * 7 + 3));
            Check("ITR PD deterministic fill (gapless i*7+3 over 12 bytes incl tail pad)", g["PD_FILL"], BitConverter.ToString(local).Replace("-", ""));
        }

        // ---- accessors == raw at the compiler offsets (two independent ways) ----
        Check("ITR GetUniqueNo == raw u32 at compiler offset 0", BitConverter.ToUInt32(pdFill, (int)ParseNum(g["OFF_PD.UniqueNo"])).ToString(), XI.Map.S2cItemTradeRes.GetUniqueNo(pdFill).ToString());
        Check("ITR GetKindRaw == raw u32 at compiler offset 4 (enum underlying width)", BitConverter.ToUInt32(pdFill, (int)ParseNum(g["OFF_PD.Kind"])).ToString(), XI.Map.S2cItemTradeRes.GetKindRaw(pdFill).ToString());
        Check("ITR GetKind (enum) == (ItemTradeResKind) raw u32 at 4", ((XI.Map.ItemTradeResKind)BitConverter.ToUInt32(pdFill, (int)ParseNum(g["OFF_PD.Kind"]))).ToString(), XI.Map.S2cItemTradeRes.GetKind(pdFill).ToString());
        Check("ITR GetActIndex == raw u16 at compiler offset 8", BitConverter.ToUInt16(pdFill, (int)ParseNum(g["OFF_PD.ActIndex"])).ToString(), XI.Map.S2cItemTradeRes.GetActIndex(pdFill).ToString());

        // ==== setters land at the compiler offset + THE tail-pad dead-space isolation ====
        {
            var b = new byte[XI.Map.S2cItemTradeRes.SizeOf];
            XI.Map.S2cItemTradeRes.SetUniqueNo(b, 0xDEADBEEF);   // u32 @ 0
            XI.Map.S2cItemTradeRes.SetKind(b, XI.Map.ItemTradeResKind.ErrLogout); // u32 @ 4 -> 8
            XI.Map.S2cItemTradeRes.SetActIndex(b, 0x1234);       // u16 @ 8 -> 0x1234; tail bytes 10..11 stay ZERO
            Check("ITR SetUniqueNo lands at compiler offset 0", ((uint)0xDEADBEEF).ToString(), BitConverter.ToUInt32(b, (int)ParseNum(g["OFF_PD.UniqueNo"])).ToString());
            Check("ITR SetKind lands at compiler offset 4 (underlying uint32)", ((uint)XI.Map.ItemTradeResKind.ErrLogout).ToString(), BitConverter.ToUInt32(b, (int)ParseNum(g["OFF_PD.Kind"])).ToString());
            Check("ITR SetActIndex lands at compiler offset 8", ((ushort)0x1234).ToString(), BitConverter.ToUInt16(b, (int)ParseNum(g["OFF_PD.ActIndex"])).ToString());
            // THE tail-pad isolation: writing the last NAMED member (ActIndex, bytes 8..9) must leave the 2 compiler
            // tail-pad bytes (10..11) zero -- no named member owns them, so they are real dead space in the wire body.
            Check("ITR tail-pad byte 10 is dead (unwritten by any accessor)", "0", b[10].ToString());
            Check("ITR tail-pad byte 11 is dead (unwritten by any accessor)", "0", b[11].ToString());
            // neighbour isolation: the u32 enum write must not touch the trailing u16 or the leading u32.
            Check("ITR SetKind leaves UniqueNo@0 untouched (0xDEADBEEF)", ((uint)0xDEADBEEF).ToString(), XI.Map.S2cItemTradeRes.GetUniqueNo(b).ToString());
            Check("ITR SetActIndex leaves Kind@4 still ErrLogout (8)", ((uint)XI.Map.ItemTradeResKind.ErrLogout).ToString(), XI.Map.S2cItemTradeRes.GetKindRaw(b).ToString());
            // round-trips.
            Check("ITR SetKind round-trip", XI.Map.ItemTradeResKind.ErrLogout.ToString(), XI.Map.S2cItemTradeRes.GetKind(b).ToString());
            Check("ITR SetActIndex round-trip", ((ushort)0x1234).ToString(), XI.Map.S2cItemTradeRes.GetActIndex(b).ToString());
        }
    }

    static byte escFillBit(byte[] fill, int off) => fill[off];

    static void Check(string name, string expected, string actual)
    {
        bool ok = string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"[{(ok ? "PASS" : "FAIL")}] {name}: expected {expected} actual {actual}");
    }
}
