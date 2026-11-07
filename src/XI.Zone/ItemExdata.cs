using System;
using System.Collections.Generic;
using System.Globalization;
using XI.Script;

namespace XI.Zone
{
    // items/exdata.cpp: Exdata::getType (the layout an item's m_extra takes) and Exdata::fromTable, which writes what a
    // Lua table gives into that layout (addItem's `exdata`). The table is read out of Lua first (FromLua): string or
    // integer keys, number, boolean, string or table values. A layout's fromTable keeps whatever a key leaves out, and a
    // key of the wrong Lua type counts as left out (Exdata::get_or); a value goes into its field truncated, as the
    // bitfield assignment. An augment layout then applies the augments it holds (ApplyAugment). Exdata::toTable
    // (CLuaItem::getExData) reads the layout back into a table (ToTable), pushed to Lua by ToLua.
    public static class ItemExdata
    {
        // Exdata::Type
        public enum ExdataType : byte
        {
            None = 0, Augment = 1, Usable = 2, Mannequin = 3, Furniture = 4, FlowerPot = 5, Linkshell = 6, Fish = 7,
            BettingSlip = 8, SoulPlate = 9, SoulReflector = 10, AssaultLog = 11, LotteryTicket = 12, Tabula = 13, Evolith = 14,
            CraftingSet = 15, BrennerBook = 16, GlowingLamp = 17, LegionPass = 18, Serialized = 19, PerpetualHourglass = 20,
            ChocoboEgg = 21, ChocoboCard = 22, Escutcheon = 23, RaceCertificate = 24, MeebleGrimoire = 25, HoneymoonTicket = 26,
            WeaponUnlock = 27,
        }

        // the item ids getType keys on (items/item_id.h)
        private const ushort LegionPass = 3528, PerpetualHourglass = 4237, WyvernCodex = 1128, GriffonCodex = 1129,
            BallistaRedbook = 5300, BallistaWhitebookPage = 5303, BrennerBluebook = 5323, BrennerBlackbookPage = 5326,
            SoulPlate = 2477, GaugerPlate = 2480, FiendPlate = 2560, SoulReflector = 2478, OfficialSoulReflector = 2500,
            ChocobetTicket = 2479, RaceCompletionCertificate = 2481, VcsHoneymoonTicket = 2344, DiligenceGrimoire = 3875,
            SanctityGrimoire = 3879, LeujaoamObservationLog = 2491, IlrusiTravelLedger = 2495, BonanzaPearl = 4089,
            MogBonanzaMarble = 2559, MazeTabulaM01 = 28672, MazeTabulaM03 = 28674, MazeTabulaR01 = 28704, MazeTabulaR03 = 28706,
            Evolith = 2783, WoodworkingSet25 = 9412, CookingSet95 = 9483, GlowingLamp = 5414, ChocoboEggFaintly = 2312,
            ChocoboEggSlightly = 2314, ChocoboEggABit = 2317, ChocoboEggSomewhat = 2319, VcsRegistrationCard = 2313,
            ChococardM = 2339, ChococardF = 2342, CraRacingForm = 2402, JoinersAspis = 26423, ChefsShield = 26461,
            LuShangsFishingRodP1 = 19320, EbisuFishingRodP1 = 19321;

        // AugmentKindFlags / AugmentSubKindFlags (enums/exdata.h)
        public const byte KindHasAugments = 0x02, KindBundled = 0x03;
        public const byte SubKindStandard = 0x03, SubKindEscutcheon = 0x08, SubKindSerialized = 0x10, SubKindMezzotint = 0x20,
            SubKindTrial = 0x40, SubKindEvolith = 0x80;

        // Exdata::getType, following the client's order: `isFish` is fishingutils::IsFish
        public static ExdataType GetType(Item item, ItemData items, Func<ushort, bool> isFish)
        {
            if (item == null) return ExdataType.None;
            ushort id = item.ItemId;
            if (LinkshellItem.Is(item)) return ExdataType.Linkshell;
            if (isFish?.Invoke(id) == true) return ExdataType.Fish;
            if (id == LegionPass) return ExdataType.LegionPass;
            if (id == PerpetualHourglass) return ExdataType.PerpetualHourglass;
            if (id == WyvernCodex || id == GriffonCodex || id >= BallistaRedbook && id <= BallistaWhitebookPage ||
                id >= BrennerBluebook && id <= BrennerBlackbookPage)
                return ExdataType.BrennerBook;
            if (id == SoulPlate || id == GaugerPlate || id == FiendPlate) return ExdataType.SoulPlate;
            if (id == SoulReflector || id == OfficialSoulReflector) return ExdataType.SoulReflector;
            if (id == ChocobetTicket) return ExdataType.BettingSlip;
            if (id == RaceCompletionCertificate) return ExdataType.RaceCertificate;
            if (id == VcsHoneymoonTicket) return ExdataType.HoneymoonTicket;
            if (id >= DiligenceGrimoire && id <= SanctityGrimoire) return ExdataType.MeebleGrimoire;
            if (id >= LeujaoamObservationLog && id <= IlrusiTravelLedger) return ExdataType.AssaultLog;
            if (id == BonanzaPearl || id == MogBonanzaMarble) return ExdataType.LotteryTicket;
            if (id >= MazeTabulaM01 && id <= MazeTabulaM03 || id >= MazeTabulaR01 && id <= MazeTabulaR03) return ExdataType.Tabula;
            if (id == Evolith) return ExdataType.Evolith;
            if (id >= WoodworkingSet25 && id <= CookingSet95) return ExdataType.CraftingSet;
            if (id == GlowingLamp) return ExdataType.GlowingLamp;
            if (id == ChocoboEggFaintly || id == ChocoboEggSlightly || id >= ChocoboEggABit && id <= ChocoboEggSomewhat)
                return ExdataType.ChocoboEgg;
            if (id == VcsRegistrationCard || id == ChococardM || id == ChococardF || id == CraRacingForm) return ExdataType.ChocoboCard;
            // an escutcheon (+4 is the finished item, no crafting exdata)
            if (id >= JoinersAspis && id <= ChefsShield && (id - JoinersAspis) % 5 != 4) return ExdataType.Escutcheon;
            // a flowerpot before a furnishing: it is one
            var furnishing = items?.Furnishing(id);
            if (furnishing?.IsFlowerpot == true) return ExdataType.FlowerPot;
            if (furnishing != null) return ZoneHost.IsMannequin(item) ? ExdataType.Mannequin : ExdataType.Furniture;
            if (id == LuShangsFishingRodP1 || id == EbisuFishingRodP1) return ExdataType.Serialized;
            var equip = items?.Equipment(id);
            // an unlockable weapon before the charged items and augments
            if (equip?.Weapon is { } weapon && weapon.IsUnlockable(item)) return ExdataType.WeaponUnlock;
            // ITEM_CHARGED: timer exdata
            if (equip?.Charged == true) return ExdataType.Usable;
            // equipment: augments
            if (equip != null) return ExdataType.Augment;
            return ExdataType.None;
        }

        // ---- the Lua table ----

        // the table at `idx` read out of Lua, in lua_next order; tables nest a few deep, functions and userdata are left out
        public static Dictionary<object, object> FromLua(LuaState l, int idx, int depth = 0)
        {
            var t = new Dictionary<object, object>();
            if (idx < 0) idx = l.Top + idx + 1;
            if (!l.IsTable(idx) || depth > 8) return t;
            l.PushNil();
            while (l.Next(idx))
            {
                object key = l.IsNumber(-2) ? Key(l.ToNumber(-2)) : l.IsString(-2) ? l.ToStringValue(-2) : null;
                object value = l.IsNumber(-1) ? l.ToNumber(-1)
                             : l.Type(-1) == 1 ? l.ToBoolean(-1)
                             : l.IsString(-1) ? l.ToStringValue(-1)
                             : l.IsTable(-1) ? FromLua(l, -1, depth + 1)
                             : null;
                if (key != null && value != null) t[key] = value;
                l.Pop(1);
            }
            return t;
        }

        // an integral number key is an integer, as Lua's array part
        private static object Key(double d) => d == Math.Floor(d) && Math.Abs(d) < 1e15 ? (object)(long)d : d;

        private static double? Num(Dictionary<object, object> t, object key) => t != null && t.TryGetValue(key, out var v) && v is double d ? d : null;
        private static bool? Bool(Dictionary<object, object> t, object key) => t != null && t.TryGetValue(key, out var v) && v is bool b ? b : null;
        private static string Str(Dictionary<object, object> t, object key) => t != null && t.TryGetValue(key, out var v) ? v as string : null;
        private static Dictionary<object, object> Tbl(Dictionary<object, object> t, object key) =>
            t != null && t.TryGetValue(key, out var v) ? v as Dictionary<object, object> : null;

        // ---- the layout ----

        // a little-endian bitfield: `width` bits from bit `bit` of the exdata (a byte-aligned field is one too)
        public static long GetBits(byte[] b, int bit, int width)
        {
            long v = 0;
            for (int i = 0; i < width; i++, bit++)
                if (bit >> 3 < b.Length && (b[bit >> 3] & 1 << (bit & 7)) != 0) v |= 1L << i;
            return v;
        }

        public static void SetBits(byte[] b, int bit, int width, long value)
        {
            for (int i = 0; i < width; i++, bit++, value >>= 1)
            {
                if (bit >> 3 >= b.Length) return;
                if ((value & 1) != 0) b[bit >> 3] |= (byte)(1 << (bit & 7));
                else b[bit >> 3] &= (byte)~(1 << (bit & 7));
            }
        }

        // field = get_or<T>(t, key, field): a number rounded (sol2's llround) into the field
        private static void Field(byte[] b, Dictionary<object, object> t, object key, int bit, int width)
        {
            if (Num(t, key) is double d) SetBits(b, bit, width, LuaState.LlRound(d));
        }

        // field = get_or<bool>(t, key, field) ? 1 : 0
        private static void Flag(byte[] b, Dictionary<object, object> t, object key, int bit)
        {
            if (Bool(t, key) is bool v) SetBits(b, bit, 1, v ? 1 : 0);
        }

        // Exdata::encodeSignature: EncodeStringSignature's first N bytes over the field
        private static void Signature(byte[] b, Dictionary<object, object> t, int at, int size = 12)
        {
            if (Str(t, "signature") is string sig) Array.Copy(ItemSignature.Encode(sig), 0, b, at, size);
        }

        // ---- fromTable ----

        // Exdata::fromTable: false for an item with no typed exdata (the caller then writes raw bytes)
        public static bool FromTable(Item item, Dictionary<object, object> data, ItemData items, Func<ushort, bool> isFish)
        {
            var type = GetType(item, items, isFish);
            if (type == ExdataType.None) return false;
            var b = item.Extra;
            switch (type)
            {
                case ExdataType.LegionPass:
                    Field(b, data, "timestamp", 0, 32);
                    Field(b, data, "title", 32, 32);
                    Signature(b, data, 12);
                    break;
                case ExdataType.PerpetualHourglass:
                    Field(b, data, "flags", 16, 3);
                    Field(b, data, "startTime", 96, 32);
                    Field(b, data, "endTime", 64, 32);
                    Field(b, data, "zoneId", 128, 16);
                    break;
                case ExdataType.BettingSlip:
                    Field(b, data, "raceId", 0, 18);
                    Field(b, data, "raceGrade", 18, 6);
                    Field(b, data, "racePairingL", 24, 4);
                    Field(b, data, "racePairingR", 28, 4);
                    Field(b, data, "quills", 32, 10);
                    break;
                case ExdataType.AssaultLog:
                    if (Tbl(data, "flags") is { } flags)
                        for (int i = 0; i < 10; i++) Flag(b, flags, (long)(i + 1), i);
                    break;
                case ExdataType.BrennerBook:
                    b[11] = 1; // always Mode 1
                    Field(b, data, "timeValue", 0, 32);
                    Field(b, data, "level", 32, 32);
                    break;
                case ExdataType.MeebleGrimoire:
                    // clears[type][level]: a 3-bit count each, 5 types of 4 levels
                    if (Tbl(data, "clears") is { } clears)
                        for (int t = 0; t < 5; t++)
                        {
                            if (Tbl(clears, (long)(t + 1)) is not { } levels) continue;
                            for (int level = 0; level < 4; level++)
                            {
                                int bit = (t * 4 + level) * 3;
                                byte count = Num(levels, (long)(level + 1)) is double d ? (byte)LuaState.LlRound(d) : (byte)GetBits(b, bit, 3);
                                SetBits(b, bit, 3, Math.Min(count, (byte)7));
                            }
                        }
                    Field(b, data, "count", 64, 8);
                    Field(b, data, "zone", 96, 8);
                    break;
                case ExdataType.HoneymoonTicket:
                    Field(b, data, "plan", 0, 8);
                    if (b[0] >= 1 && b[0] <= 4) Array.Copy(ItemSignature.Encode("Plan" + (char)('A' + b[0] - 1)), 0, b, 12, 12);
                    break;
                case ExdataType.RaceCertificate:
                    Field(b, data, "raceId", 0, 18);
                    Field(b, data, "raceGrade", 18, 6);
                    break;
                case ExdataType.LotteryTicket:
                    Field(b, data, "number", 0, 24);
                    Field(b, data, "title", 24, 8);
                    break;
                case ExdataType.Tabula:
                    TabulaFromTable(b, data);
                    break;
                case ExdataType.Evolith:
                    Field(b, data, "augment", 0, 10);
                    Field(b, data, "shape", 10, 4);
                    Field(b, data, "element", 14, 3);
                    Field(b, data, "bonus", 17, 4);
                    Signature(b, data, 12);
                    break;
                case ExdataType.CraftingSet:
                    Field(b, data, "quality", 16, 16);
                    Signature(b, data, 12);
                    break;
                case ExdataType.GlowingLamp:
                    // the chamber id is kept past CHAMBER_ID_OFFSET (0x1D), 0 staying 0
                    if (Num(data, "chamberId") is double chamber)
                    {
                        ushort id = (ushort)LuaState.LlRound(chamber);
                        BitConverter.TryWriteBytes(b.AsSpan(0, 2), (ushort)(id == 0 ? 0 : id + 0x1D));
                    }
                    Field(b, data, "flags", 16, 3);
                    Field(b, data, "startTime", 96, 32);
                    Field(b, data, "endTime", 64, 32);
                    break;
                case ExdataType.ChocoboEgg:
                    if (Tbl(data, "dna") is { } eggDna)
                        for (int i = 0; i < 3; i++) Field(b, eggDna, (long)(i + 1), i * 3, 3);
                    Field(b, data, "ability", 9, 4);
                    Field(b, data, "plan", 14, 2);
                    Flag(b, data, "isBred", 31);
                    break;
                case ExdataType.ChocoboCard:
                    ChocoboCardFromTable(b, data);
                    break;
                case ExdataType.Fish:
                    Field(b, data, "size", 0, 16);
                    Field(b, data, "weight", 16, 16);
                    Flag(b, data, "isRanked", 32);
                    break;
                case ExdataType.Escutcheon:
                    b[0] = KindHasAugments;
                    b[1] = SubKindEscutcheon | SubKindStandard;
                    Field(b, data, "status", 32, 8);
                    Field(b, data, "bonusObjective", 40, 8);
                    Field(b, data, "craftsmanship", 48, 16);
                    Field(b, data, "stage", 64, 4);
                    Field(b, data, "successDownPenalty", 68, 16);
                    Signature(b, data, 12);
                    break;
                case ExdataType.SoulPlate:
                    if (Str(data, "signature") is string name)
                    {
                        Array.Clear(b, 0, 14);
                        PackSoultrapperName(name, b);
                    }
                    Field(b, data, "zoneId", 112, 16);
                    Field(b, data, "familyId", 128, 16);
                    Field(b, data, "poolId", 144, 16);
                    Field(b, data, "level", 160, 7);
                    Field(b, data, "feralSkill", 167, 12);
                    Field(b, data, "feralPoints", 179, 7);
                    Field(b, data, "quality", 186, 6);
                    break;
                case ExdataType.SoulReflector:
                    SoulReflectorFromTable(b, data);
                    break;
                case ExdataType.WeaponUnlock:
                    Field(b, data, "unlockPoints", 0, 16);
                    break;
                case ExdataType.Furniture:
                    Flag(b, data, "on2ndFloor", 8);
                    Flag(b, data, "installed", 14);
                    Field(b, data, "x", 48, 8);
                    Field(b, data, "z", 56, 8);
                    Field(b, data, "y", 64, 8);
                    Field(b, data, "rotation", 72, 8);
                    Signature(b, data, 10);
                    break;
                case ExdataType.FlowerPot:
                    Field(b, data, "step", 0, 8);
                    Flag(b, data, "dried", 15);
                    Field(b, data, "crystal1", 16, 8);
                    Field(b, data, "crystal2", 24, 8);
                    Field(b, data, "kind", 32, 8);
                    Flag(b, data, "examined", 40);
                    Field(b, data, "strength", 41, 7);
                    Field(b, data, "x", 48, 8);
                    Field(b, data, "z", 56, 8);
                    Field(b, data, "y", 64, 8);
                    Field(b, data, "rotation", 72, 8);
                    Field(b, data, "timePlanted", 96, 32);
                    Field(b, data, "timeNextStep", 128, 32);
                    break;
                case ExdataType.Mannequin:
                    string[] keys = { "x", "z", "y", "rotation", "main", "sub", "ranged", "head", "body", "hands", "legs", "feet", "race", "pose" };
                    for (int i = 0; i < keys.Length; i++) Field(b, data, keys[i], (6 + i) * 8, 8);
                    break;
                case ExdataType.Linkshell:
                    Field(b, data, "groupId", 0, 32);
                    Field(b, data, "groupKey", 32, 16);
                    if (Tbl(data, "color") is { } color)
                    {
                        Field(b, color, "r", 48, 4);
                        Field(b, color, "g", 52, 4);
                        Field(b, color, "b", 56, 4);
                        Field(b, color, "a", 60, 4);
                    }
                    Field(b, data, "flag", 64, 8);
                    if (Str(data, "name") is string lsName)
                    {
                        Array.Clear(b, 9, 15);
                        Array.Copy(LinkshellItem.Encode(lsName), 0, b, 9, 15);
                    }
                    break;
                case ExdataType.Serialized:
                    b[0] = KindHasAugments;
                    b[1] = SubKindSerialized;
                    Field(b, data, "serverIndex", 32, 8);
                    Field(b, data, "serialNumber", 48, 16);
                    Signature(b, data, 12);
                    break;
                case ExdataType.Usable:
                    b[0] = 1; // Header: the client reads the timer data only with it
                    Field(b, data, "remainingCharges", 8, 8);
                    Field(b, data, "flags", 16, 16);
                    Field(b, data, "timeValue1", 32, 32);
                    Field(b, data, "timeValue2", 64, 32);
                    Signature(b, data, 12);
                    break;
                case ExdataType.Augment:
                    AugmentFromTable(item, data, items);
                    break;
            }
            item.Signature = ItemSignature.Get(item);
            return true;
        }

        // the Augment case: the table's own kind and sub kind pick the layout (0 when it gives none)
        private static void AugmentFromTable(Item item, Dictionary<object, object> data, ItemData items)
        {
            var b = item.Extra;
            // get_or<AugmentKindFlags> / <AugmentSubKindFlags>: enums (LuaState.EnumOf)
            byte kind = Num(data, "augmentKind") is double k ? (byte)LuaState.EnumOf(k) : (byte)0;
            byte subKind = Num(data, "augmentSubKind") is double s ? (byte)LuaState.EnumOf(s) : (byte)0;
            if (kind == KindBundled)
            {
                // AugmentBundle (Odyssey, JSE necks, Unity belts)
                b[0] = KindBundled;
                b[1] = SubKindStandard | SubKindEvolith;
                Field(b, data, "type", 32, 2);
                Field(b, data, "rank", 50, 5);
                Field(b, data, "accumulatedRP", 34, 16);
                Field(b, data, "maxRankTier", 55, 2);
                Field(b, data, "rpCurve", 58, 2);
                Field(b, data, "augmentIndex", 64, 32);
                Signature(b, data, 12);
            }
            else if ((subKind & SubKindMezzotint) != 0)
            {
                // AugmentMezzotint (Delve, Geas Fete): three client LUT indices
                Field(b, data, "augmentKind", 0, 8);
                Field(b, data, "augmentSubKind", 8, 8);
                Field(b, data, "type", 16, 2);
                Field(b, data, "rank", 18, 5);
                Field(b, data, "accumulatedRP", 32, 16);
                if (Tbl(data, "augments") is { } augments)
                    for (int i = 0; i < 3; i++)
                        if (Tbl(augments, (long)(i + 1)) is { } entry)
                        {
                            Field(b, entry, "index", (6 + i * 2) * 8, 8);
                            Field(b, entry, "value", (7 + i * 2) * 8, 8);
                        }
                Signature(b, data, 12);
            }
            else
            {
                // AugmentTrial (four augments, then the trial) or AugmentStandard (five), each applied once set
                bool trial = (subKind & SubKindTrial) != 0;
                int slots = trial ? 4 : 5;
                Field(b, data, "augmentKind", 0, 8);
                Field(b, data, "augmentSubKind", 8, 8);
                if (Tbl(data, "augments") is { } augments)
                    for (int i = 0; i < slots; i++)
                        if (Tbl(augments, (long)(i + 1)) is { } entry)
                        {
                            Field(b, entry, "id", (2 + i * 2) * 8, 11);
                            Field(b, entry, "value", (2 + i * 2) * 8 + 11, 5);
                        }
                if (trial && Tbl(data, "trial") is { } trialTable)
                {
                    Field(b, trialTable, "id", 80, 15);
                    Flag(b, trialTable, "completed", 95);
                }
                Signature(b, data, 12);
                for (int slot = 0; slot < slots; slot++)
                    if ((BitConverter.ToUInt16(b, 2 + slot * 2) & 0x7FF) != 0) items?.ApplyAugment(item, slot);
            }
        }

        // ChocoboCard: STR / END / DSC (Trait 1, RP 4, Rank 3), RCP (RP 5, Rank 3), then the DNA, abilities and looks
        private static void ChocoboCardFromTable(byte[] b, Dictionary<object, object> data)
        {
            string[] stats = { "strength", "endurance", "discernment" };
            for (int i = 0; i < 3; i++)
                if (Tbl(data, stats[i]) is { } stat)
                {
                    Flag(b, stat, "trait", i * 8);
                    Field(b, stat, "rp", i * 8 + 1, 4);
                    Field(b, stat, "rank", i * 8 + 5, 3);
                }
            if (Tbl(data, "receptivity") is { } rcp)
            {
                Field(b, rcp, "rp", 24, 5);
                Field(b, rcp, "rank", 29, 3);
            }
            if (Tbl(data, "dna") is { } dna)
                for (int i = 0; i < 3; i++) Field(b, dna, (long)(i + 1), 32 + i * 3, 3);
            if (Tbl(data, "abilities") is { } abilities)
            {
                Field(b, abilities, 1L, 41, 4);
                Field(b, abilities, 2L, 45, 4);
            }
            Field(b, data, "temperament", 49, 3);
            Field(b, data, "weather", 52, 4);
            Field(b, data, "gender", 56, 1);
            Field(b, data, "color", 57, 3);
            Field(b, data, "size", 60, 3);
            if (Str(data, "name") is string name) Array.Copy(ItemSignature.Encode(name), 0, b, 12, 12);
        }

        // Tabula: Voucher 7 bits, AnchorBits 25 (bit 24 is grid position 0), then the rune stream: twelve 9-bit rune ids,
        // twelve 2-bit rotations, 7 bits of uses. A runes table clears the grid and fills it in table order.
        private static void TabulaFromTable(byte[] b, Dictionary<object, object> data)
        {
            const int StreamAt = 32, NumRunes = 12, IdBits = 9, RotBits = 2, RotAt = NumRunes * IdBits, UsesAt = RotAt + NumRunes * RotBits;
            Field(b, data, "voucher", 0, 7);
            if (Tbl(data, "runes") is { } runes)
            {
                long anchors = 0;
                Array.Clear(b, 4, 20);
                int slot = 0;
                foreach (var entry in runes.Values)
                {
                    if (slot >= NumRunes) break;
                    if (entry is not Dictionary<object, object> rune) continue;
                    if (Num(rune, "id") is double id) SetBits(b, StreamAt + slot * IdBits, IdBits, (ushort)LuaState.LlRound(id));
                    if (Num(rune, "rotation") is double rot) SetBits(b, StreamAt + RotAt + slot * RotBits, RotBits, (byte)LuaState.LlRound(rot));
                    if (Num(rune, "position") is double posD && (byte)LuaState.LlRound(posD) is byte pos && pos < 25) anchors |= 1L << (24 - pos);
                    slot++;
                }
                SetBits(b, 7, 25, anchors);
            }
            if (Num(data, "uses") is double uses) SetBits(b, StreamAt + UsesAt, 7, (byte)LuaState.LlRound(uses));
        }

        // SoulReflector: names, pool, exp and temper in the first 59 bits, then seven feral skill slots (id 12, level 7)
        private static void SoulReflectorFromTable(byte[] b, Dictionary<object, object> data)
        {
            Field(b, data, "nameFirst", 0, 6);
            Field(b, data, "nameLast", 6, 6);
            Field(b, data, "poolId", 12, 16);
            Field(b, data, "exp", 28, 8);
            Field(b, data, "discipline", 36, 8);
            Field(b, data, "temperament", 44, 4);
            Field(b, data, "aggressiveness", 48, 4);
            Field(b, data, "level", 52, 7);
            if (Tbl(data, "feralSkills") is { } ferals)
                for (int i = 0; i < 7; i++)
                    if (Tbl(ferals, (long)(i + 1)) is { } slot)
                    {
                        int at = 59 + i * 19;
                        Field(b, slot, "skillId", at, 12);
                        Field(b, slot, "level", at + 12, 7);
                    }
        }

        // PackSoultrapperName: up to 15 chars without underscores, 8-bit chars packed to 7 bits
        private static void PackSoultrapperName(string name, byte[] output)
        {
            if (name.Length > 15) name = name.Substring(0, 15);
            name = name.Replace("_", "");
            int shift = 1, loops = 0, total = name.Length;
            for (int i = 0; i <= 15; i++)
            {
                byte current = i < total ? (byte)name[i] : (byte)0;
                byte next = i + 1 < total ? (byte)name[i + 1] : (byte)0;
                byte left = current;
                for (int j = 0; j < shift; j++)
                {
                    left = (byte)(left << 1);
                    if (j + 1 != shift && (left & 128) != 0) left ^= 128;
                }
                byte right = (byte)(next >> (7 - shift));
                if (i - loops < 14) output[i - loops] = (byte)(left | right);
                if (shift == 7)
                {
                    shift = 1;
                    loops++;
                    i++;
                }
                else shift++;
            }
        }

        // ---- toTable ----

        private static long U(byte[] b, int bit, int width) => GetBits(b, bit, width);
        private static bool B(byte[] b, int bit) => GetBits(b, bit, 1) != 0;
        // Exdata::decodeSignature: DecodeStringSignature over the N signature bytes
        private static string Sig(byte[] b, int at, int size = 12) => ItemSignature.Decode(b.AsSpan(at, size));

        // Exdata::toTable: the item's exdata as its type's keys; null for an item with no typed exdata (the caller then
        // gives the raw bytes)
        public static Dictionary<object, object> ToTable(Item item, ItemData items, Func<ushort, bool> isFish)
        {
            var type = GetType(item, items, isFish);
            var b = item.Extra;
            var t = new Dictionary<object, object>();
            switch (type)
            {
                case ExdataType.LegionPass:
                    t["timestamp"] = U(b, 0, 32);
                    t["title"] = U(b, 32, 32);
                    t["signature"] = Sig(b, 12);
                    break;
                case ExdataType.PerpetualHourglass:
                    t["flags"] = U(b, 16, 3);
                    t["startTime"] = U(b, 96, 32);
                    t["endTime"] = U(b, 64, 32);
                    t["zoneId"] = U(b, 128, 16);
                    break;
                case ExdataType.BettingSlip:
                    t["raceId"] = U(b, 0, 18);
                    t["raceGrade"] = U(b, 18, 6);
                    t["racePairingL"] = U(b, 24, 4);
                    t["racePairingR"] = U(b, 28, 4);
                    t["quills"] = U(b, 32, 10);
                    break;
                case ExdataType.AssaultLog:
                {
                    var flags = new Dictionary<object, object>();
                    for (int i = 0; i < 10; i++) flags[(long)(i + 1)] = B(b, i);
                    t["flags"] = flags;
                    break;
                }
                case ExdataType.BrennerBook:
                    t["timeValue"] = U(b, 0, 32);
                    t["level"] = U(b, 32, 32);
                    break;
                case ExdataType.MeebleGrimoire:
                {
                    var clears = new Dictionary<object, object>();
                    for (int kind = 0; kind < 5; kind++)
                    {
                        var levels = new Dictionary<object, object>();
                        for (int level = 0; level < 4; level++) levels[(long)(level + 1)] = U(b, (kind * 4 + level) * 3, 3);
                        clears[(long)(kind + 1)] = levels;
                    }
                    t["clears"] = clears;
                    t["count"] = U(b, 64, 8);
                    t["zone"] = U(b, 96, 8);
                    break;
                }
                case ExdataType.HoneymoonTicket:
                    t["plan"] = U(b, 0, 8);
                    break;
                case ExdataType.RaceCertificate:
                    t["raceId"] = U(b, 0, 18);
                    t["raceGrade"] = U(b, 18, 6);
                    break;
                case ExdataType.LotteryTicket:
                    t["number"] = U(b, 0, 24);
                    t["title"] = U(b, 24, 8);
                    break;
                case ExdataType.Tabula:
                    TabulaToTable(b, t);
                    break;
                case ExdataType.Evolith:
                    t["augment"] = U(b, 0, 10);
                    t["shape"] = U(b, 10, 4);
                    t["element"] = U(b, 14, 3);
                    t["bonus"] = U(b, 17, 4);
                    t["signature"] = Sig(b, 12);
                    break;
                case ExdataType.CraftingSet:
                    t["quality"] = U(b, 16, 16);
                    t["signature"] = Sig(b, 12);
                    break;
                case ExdataType.GlowingLamp:
                {
                    // the chamber id is kept past CHAMBER_ID_OFFSET (0x1D)
                    long chamber = U(b, 0, 16);
                    t["chamberId"] = chamber >= 0x1D ? chamber - 0x1D : 0L;
                    t["flags"] = U(b, 16, 3);
                    t["startTime"] = U(b, 96, 32);
                    t["endTime"] = U(b, 64, 32);
                    break;
                }
                case ExdataType.ChocoboEgg:
                {
                    var dna = new Dictionary<object, object>();
                    for (int i = 0; i < 3; i++) dna[(long)(i + 1)] = U(b, i * 3, 3);
                    t["dna"] = dna;
                    t["ability"] = U(b, 9, 4);
                    t["plan"] = U(b, 14, 2);
                    t["isBred"] = B(b, 31);
                    break;
                }
                case ExdataType.ChocoboCard:
                    ChocoboCardToTable(b, t);
                    break;
                case ExdataType.Fish:
                    t["size"] = U(b, 0, 16);
                    t["weight"] = U(b, 16, 16);
                    t["isRanked"] = B(b, 32);
                    break;
                case ExdataType.Escutcheon:
                    t["status"] = U(b, 32, 8);
                    t["bonusObjective"] = U(b, 40, 8);
                    t["craftsmanship"] = U(b, 48, 16);
                    t["stage"] = U(b, 64, 4);
                    t["successDownPenalty"] = U(b, 68, 16);
                    t["signature"] = Sig(b, 12);
                    break;
                case ExdataType.SoulPlate:
                    t["signature"] = UnpackSoultrapperName(b);
                    t["zoneId"] = U(b, 112, 16);
                    t["familyId"] = U(b, 128, 16);
                    t["poolId"] = U(b, 144, 16);
                    t["level"] = U(b, 160, 7);
                    t["feralSkill"] = U(b, 167, 12);
                    t["feralPoints"] = U(b, 179, 7);
                    t["quality"] = U(b, 186, 6);
                    break;
                case ExdataType.SoulReflector:
                {
                    t["nameFirst"] = U(b, 0, 6);
                    t["nameLast"] = U(b, 6, 6);
                    t["poolId"] = U(b, 12, 16);
                    t["exp"] = U(b, 28, 8);
                    t["discipline"] = U(b, 36, 8);
                    t["temperament"] = U(b, 44, 4);
                    t["aggressiveness"] = U(b, 48, 4);
                    t["level"] = U(b, 52, 7);
                    var skills = new Dictionary<object, object>();
                    for (int i = 0; i < 7; i++)
                    {
                        int at = 59 + i * 19;
                        skills[(long)(i + 1)] = new Dictionary<object, object> { ["skillId"] = U(b, at, 12), ["level"] = U(b, at + 12, 7) };
                    }
                    t["feralSkills"] = skills;
                    break;
                }
                case ExdataType.WeaponUnlock:
                    t["unlockPoints"] = U(b, 0, 16);
                    break;
                case ExdataType.Furniture:
                    t["on2ndFloor"] = B(b, 8);
                    t["installed"] = B(b, 14);
                    t["x"] = U(b, 48, 8);
                    t["z"] = U(b, 56, 8);
                    t["y"] = U(b, 64, 8);
                    t["rotation"] = U(b, 72, 8);
                    t["signature"] = Sig(b, 10);
                    break;
                case ExdataType.FlowerPot:
                    t["step"] = U(b, 0, 8);
                    t["dried"] = B(b, 15);
                    t["crystal1"] = U(b, 16, 8);
                    t["crystal2"] = U(b, 24, 8);
                    t["kind"] = U(b, 32, 8);
                    t["examined"] = B(b, 40);
                    t["strength"] = U(b, 41, 7);
                    t["x"] = U(b, 48, 8);
                    t["z"] = U(b, 56, 8);
                    t["y"] = U(b, 64, 8);
                    t["rotation"] = U(b, 72, 8);
                    t["timePlanted"] = U(b, 96, 32);
                    t["timeNextStep"] = U(b, 128, 32);
                    break;
                case ExdataType.Mannequin:
                {
                    string[] keys = { "x", "z", "y", "rotation", "main", "sub", "ranged", "head", "body", "hands", "legs", "feet", "race", "pose" };
                    for (int i = 0; i < keys.Length; i++) t[keys[i]] = U(b, (6 + i) * 8, 8);
                    break;
                }
                case ExdataType.Linkshell:
                    t["groupId"] = U(b, 0, 32);
                    t["groupKey"] = U(b, 32, 16);
                    t["color"] = new Dictionary<object, object> { ["r"] = U(b, 48, 4), ["g"] = U(b, 52, 4), ["b"] = U(b, 56, 4), ["a"] = U(b, 60, 4) };
                    t["flag"] = U(b, 64, 8);
                    t["name"] = LinkshellItem.Decode(b.AsSpan(9, 15));
                    break;
                case ExdataType.Serialized:
                    t["augmentKind"] = U(b, 0, 8);
                    t["augmentSubKind"] = U(b, 8, 8);
                    t["serverIndex"] = U(b, 32, 8);
                    t["serialNumber"] = U(b, 48, 16);
                    t["signature"] = Sig(b, 12);
                    break;
                case ExdataType.Usable:
                    // ItemTimerInfo
                    t["remainingCharges"] = U(b, 8, 8);
                    t["flags"] = U(b, 16, 16);
                    t["timeValue1"] = U(b, 32, 32);
                    t["timeValue2"] = U(b, 64, 32);
                    t["signature"] = Sig(b, 12);
                    break;
                case ExdataType.Augment:
                    AugmentToTable(b, t);
                    break;
                default:
                    return null;
            }
            return t;
        }

        // the Augment case: the exdata's own kind and sub kind pick the layout
        private static void AugmentToTable(byte[] b, Dictionary<object, object> t)
        {
            byte kind = b[0], subKind = b[1];
            t["augmentKind"] = (long)kind;
            t["augmentSubKind"] = (long)subKind;
            if (kind == KindBundled)
            {
                // AugmentBundle
                t["type"] = U(b, 32, 2);
                t["rank"] = U(b, 50, 5);
                t["accumulatedRP"] = U(b, 34, 16);
                t["maxRankTier"] = U(b, 55, 2);
                t["rpCurve"] = U(b, 58, 2);
                t["augmentIndex"] = U(b, 64, 32);
            }
            else if ((subKind & SubKindMezzotint) != 0)
            {
                // AugmentMezzotint
                t["type"] = U(b, 16, 2);
                t["rank"] = U(b, 18, 5);
                t["accumulatedRP"] = U(b, 32, 16);
                var augments = new Dictionary<object, object>();
                for (int i = 0; i < 3; i++)
                    augments[(long)(i + 1)] = new Dictionary<object, object> { ["index"] = U(b, (6 + i * 2) * 8, 8), ["value"] = U(b, (7 + i * 2) * 8, 8) };
                t["augments"] = augments;
            }
            else
            {
                // AugmentTrial (four augments, then the trial) or AugmentStandard (five)
                bool trial = (subKind & SubKindTrial) != 0;
                var augments = new Dictionary<object, object>();
                for (int i = 0; i < (trial ? 4 : 5); i++)
                    augments[(long)(i + 1)] = new Dictionary<object, object> { ["id"] = U(b, (2 + i * 2) * 8, 11), ["value"] = U(b, (2 + i * 2) * 8 + 11, 5) };
                t["augments"] = augments;
                if (trial) t["trial"] = new Dictionary<object, object> { ["id"] = U(b, 80, 15), ["completed"] = B(b, 95) };
            }
            t["signature"] = Sig(b, 12);
        }

        // ChocoboCard::toTable (statToTable for STR / END / DSC)
        private static void ChocoboCardToTable(byte[] b, Dictionary<object, object> t)
        {
            string[] stats = { "strength", "endurance", "discernment" };
            for (int i = 0; i < 3; i++)
                t[stats[i]] = new Dictionary<object, object> { ["trait"] = B(b, i * 8), ["rp"] = U(b, i * 8 + 1, 4), ["rank"] = U(b, i * 8 + 5, 3) };
            t["receptivity"] = new Dictionary<object, object> { ["rp"] = U(b, 24, 5), ["rank"] = U(b, 29, 3) };
            var dna = new Dictionary<object, object>();
            for (int i = 0; i < 3; i++) dna[(long)(i + 1)] = U(b, 32 + i * 3, 3);
            t["dna"] = dna;
            t["abilities"] = new Dictionary<object, object> { [1L] = U(b, 41, 4), [2L] = U(b, 45, 4) };
            t["temperament"] = U(b, 49, 3);
            t["weather"] = U(b, 52, 4);
            t["gender"] = U(b, 56, 1);
            t["color"] = U(b, 57, 3);
            t["size"] = U(b, 60, 3);
            t["name"] = Sig(b, 12);
        }

        // Tabula::toTable: the anchored grid positions in order, each taking the next rune slot; a slot with rune id 0
        // is left out (but used up)
        private static void TabulaToTable(byte[] b, Dictionary<object, object> t)
        {
            const int StreamAt = 32, GridSize = 25, NumRunes = 12, IdBits = 9, RotBits = 2, RotAt = NumRunes * IdBits, UsesAt = RotAt + NumRunes * RotBits;
            t["voucher"] = U(b, 0, 7);
            long anchors = U(b, 7, 25);
            var runes = new Dictionary<object, object>();
            int slot = 0;
            for (int pos = 0; pos < GridSize && slot < NumRunes; pos++)
            {
                if ((anchors >> (GridSize - 1 - pos) & 1) == 0) continue;
                long runeId = U(b, StreamAt + slot * IdBits, IdBits);
                if (runeId > 0)
                {
                    long rotation = U(b, StreamAt + RotAt + slot * RotBits, RotBits);
                    runes[(long)(runes.Count + 1)] = new Dictionary<object, object> { ["id"] = runeId, ["rotation"] = rotation, ["position"] = (long)pos };
                }
                slot++;
            }
            t["runes"] = runes;
            t["uses"] = U(b, StreamAt + UsesAt, 7);
        }

        // UnpackSoultrapperName: 7-bit chars from the 14 signature bytes, ending at the first outside '0'..'z'
        private static string UnpackSoultrapperName(byte[] input)
        {
            const int BufSize = 14;
            var output = new System.Text.StringBuilder();
            int bitsLeft = 0, pos = 0;
            byte cur = 0;
            for (;;)
            {
                int c = 0;
                for (int bit = 6; bit >= 0; --bit)
                {
                    if (bitsLeft == 0)
                    {
                        if (pos >= BufSize) return output.ToString();
                        cur = input[pos++];
                        bitsLeft = 8;
                    }
                    if ((cur & 0x80) != 0) c |= 1 << bit;
                    cur = (byte)(cur << 1);
                    --bitsLeft;
                }
                if (c == 0 || c < '0' || c > 'z') break;
                output.Append((char)c);
            }
            return output.ToString();
        }

        // the table pushed onto the Lua stack: string or integer keys, number, boolean, string or table values
        public static void ToLua(LuaState l, Dictionary<object, object> t)
        {
            l.NewTable();
            foreach (var (key, value) in t)
            {
                l.Push(key);
                if (value is Dictionary<object, object> sub) ToLua(l, sub);
                else l.Push(value);
                l.SetTable(-3);
            }
        }

        // the raw fallback's keyObj.as<uint8>() / valObj.as<uint8>(): a number rounded (sol2's llround), a numeric string
        // read as one, anything else 0
        public static byte RawByte(object o) => o switch
        {
            long n => (byte)n,
            double d => (byte)LuaState.LlRound(d),
            string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => (byte)LuaState.LlRound(d),
            _ => 0,
        };
    }
}
