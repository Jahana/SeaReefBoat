using System;
using System.Collections.Generic;
using XI.Common;
using XI.Ipc;
using XI.Map;
using XI.Persistence;

namespace XI.Zone
{
    // GP_CLI_COMMAND_FISHING_2 (0x110) and fishingutils: casting (ACTION Fish calls StartFishing), the hook check, the
    // mini-game's end, the release and its skill-up, with the 0x115 GP_SERV_COMMAND_FISH, 0x027 TALKNUMWORK2 and
    // 0x038 SCHEDULOR packets. The fishing tables load on first use, with the fishing_mob table (LoadFishMobs) and
    // the Brigand's Chart chests (LoadChests): a hooked mob is spawned near the angler and engages (CatchMonster), a
    // hooked chest is set down behind the angler (CatchChest). The weather modifier is GetWeatherModifier
    // (ZoneHost.Weather.cs); the fishing skill moghancement is its Mod.FISH (Moghancement.cs). Not ported, as in the
    // C++: the fishing item moghancement (commented out in FishingCheck, a TODO in changeMoghancement) and the fish
    // hour/moon/month patterns (never loaded, so every fish gets the default 50 base hook chance). The hook pools
    // are walked in fish and mob id order where C++ walks std::map<fish_t*> and std::map<fishmob_t*> in pointer order.
    public sealed partial class ZoneHost
    {
        // FISHMESSAGEOFFSET: added to the zone's FISHING_MESSAGE_OFFSET text id
        private const ushort FishMsgNoRod = 1, FishMsgNoBait = 2, FishMsgCannotFishMoment = 3, FishMsgNoCatch = 4,
            FishMsgMonster = 5, FishMsgLineBreak = 6, FishMsgRodBreak = 7, FishMsgHookedSmall = 8, FishMsgLost = 9,
            FishMsgCatchInvFull = 0x0A, FishMsgCatchMulti = 0x0E, FishMsgRodBreakTooBig = 0x11, FishMsgRodBreakTooHeavy = 0x12, FishMsgLostTooSmall = 0x13,
            FishMsgLostLowSkill = 0x14, FishMsgGiveUpBaitLoss = 0x24, FishMsgGiveUp = 0x25, FishMsgCatch = 0x27,
            FishMsgWarning = 0x28, FishMsgGood = 0x29, FishMsgHookedLarge = 0x32, FishMsgHookedItem = 0x33,
            FishMsgHookedMonster = 0x34, FishMsgKeen = 0x35, FishMsgEpic = 0x36, FishMsgLostTooBig = 0x3C, FishMsgCatchChest = 0x40;

        private const byte CatchNone = 0, CatchSmallFish = 1, CatchBigFish = 2, CatchItemType = 3, CatchMob = 4, CatchChest = 5;
        private const byte SuccessNone = 0, SuccessCatchItem = 1, SuccessCatchSmall = 2, SuccessCatchLarge = 3,
            SuccessCatchMob = 5, SuccessLineBreak = 6, SuccessRodBreak = 7, SuccessCatchChest = 9;
        // FISHINGNM flags
        private const uint FishNmRegenEasy = 0x01, FishNmRegenDifficult = 0x02, FishNmHealEasy = 0x04, FishNmHealDifficult = 0x08,
            FishNmAttackEasy = 0x10, FishNmAttackDifficult = 0x20, FishNmResetRespawnOnFail = 0x40;
        private const byte FailNone = 0, FailLineSnap = 2, FailRodBreak = 3, FailRodBreakTooBig = 4, FailRodBreakTooHeavy = 5,
            FailLost = 6, FailLostTooSmall = 7, FailLostLowSkill = 8, FailLostTooBig = 9;
        private const byte SenseGood = 1, SenseBad = 2, SenseTerrible = 3, SenseNoSkill = 4, SenseKeen = 7, SenseEpic = 8;
        private const byte FishSizeSmall = 0, FishSizeLarge = 1, BaitTypeLure = 1;
        private const uint RodFlagSmallPenalty = 0x01, RodFlagLargePenalty = 0x02, RodFlagLegendaryBonus = 0x04;
        private const uint LegendaryHalfTime = 0x01, LegendaryNoRodTimeBonus = 0x02, LegendaryEbisuTimeBonusOnly = 0x04,
            LegendaryAddTimeBonus = 0x08;
        private const uint BaitFlagPoorFish = 0x08, BaitFlagGoldArrow = 0x10, BaitFlagShellfishAffinity = 0x40, FishFlagShellfish = 0x01;

        private const ushort FishermansTunica = 13808, AnglersTunica = 13809, FishermansApron = 14400, FishermansSmock = 11337,
            FishermansGloves = 14070, AnglersGloves = 14071, FishermansHose = 14292, AnglersHose = 14293,
            FishermansBoots = 14171, AnglersBoots = 14172, Waders = 14195, FishersRope = 11768;
        private const ushort DrillCalamary = 17006, DwarfPugil = 17007, LuShang = 17386, Ebisu = 17011, LuShang1 = 19320, Ebisu1 = 19321;
        private const int KeyItemMooching = 1978;
        private const int SlotBody = 5, SlotHands = 6, SlotLegs = 7, SlotFeet = 8, SlotWaist = 10;
        private const ushort ZoneValkurmDunes = 103, ZoneBuburimuPeninsula = 118;
        private const byte FishingMode2CheckHook = 2, FishingModeEndMiniGame = 3, FishingModeRelease = 4, FishingModePotentialTimeout = 5;

        internal sealed class FishData
        {
            public ushort Id, MinLength, MaxLength, Rarity, ReqKeyItem;
            public byte MaxSkill, Difficulty, BaseDelay, BaseMove, Ranking, SizeType, Log, Quest, MaxHook;
            public uint Flags, LegendaryFlags;
            public bool Legendary, IsItem, QuestOnly;
        }

        // fishmob_t
        internal sealed class FishMobData
        {
            public uint MobId, NmFlags;
            public string Name = "";
            public byte Log, Quest, AreaId, Level, Difficulty, BaseDelay, BaseMove, Ranking;
            public bool Nm, QuestOnly;
            public ushort Rarity, MinRespawn, MaxRespawn, ReqBaitId, AltBaitId, ReqKeyItem, ZoneId, MinLength, MaxLength;
        }

        internal sealed class RodData
        {
            public ushort Id, BrokenRodId, MinRank, MaxRank;
            public byte SizeType, FishAttack, LgdBonusAtk, FishRecovery, FishTime, LgdBonusTime, SmDelayBonus, SmMoveBonus,
                LgDelayBonus, LgMoveBonus, Multiplier;
            public uint Flags;
            public bool Breakable, Legendary;
        }

        internal sealed class BaitData
        {
            public ushort Id;
            public byte Type, MaxHook;
            public uint Flags;
        }

        internal struct AreaVector
        {
            public float X, Y, Z;
            public AreaVector(float x, float y, float z) { X = x; Y = y; Z = z; }
        }

        internal sealed class FishingArea
        {
            public byte AreaId, Type, Height, Radius, Difficulty;
            public AreaVector[] Bounds = Array.Empty<AreaVector>();
            public AreaVector Center;
        }

        internal sealed class FishStock
        {
            public int Quantity, Max, Restock;
        }

        // fishresponse_t (and catchresponse_t's fail reasons)
        public sealed class FishResponse
        {
            public bool Hooked, Legendary, Epic, Nm;
            public byte AreaId, CatchType, CatchLevel, CatchDifficulty, CatchSizeType, Count, Sense, HookSense, SuccessType;
            public byte Distance; // a hooked chest's distance from the angler
            public sbyte Angle;   // and its angle off directly behind
            public uint CatchId, Special, FishingToken, NmFlags;
            public ushort Stamina, Delay, Regen, Response, AttackDmg, Heal, TimeLimit, Length, Weight;
            public (byte Reason, byte Chance) Lose, Break, Snap;
        }

        private sealed class FishingTables
        {
            public readonly Dictionary<ushort, SortedDictionary<byte, FishingArea>> Areas = new();
            public readonly Dictionary<ushort, FishData> Fish = new();
            public readonly Dictionary<ushort, RodData> Rods = new();
            public readonly Dictionary<ushort, BaitData> Baits = new();
            public readonly Dictionary<ushort, Dictionary<ushort, byte>> Affinities = new();
            public readonly Dictionary<ushort, SortedDictionary<ushort, ushort>> Groups = new();
            public readonly Dictionary<(ushort Zone, byte Area), ushort> CatchLists = new();
            public readonly Dictionary<(ushort Zone, byte Area), SortedDictionary<ushort, FishStock>> Pools = new();
            public readonly Dictionary<ushort, SortedDictionary<uint, FishMobData>> Mobs = new();
            public readonly Dictionary<ushort, List<uint>> Chests = new();
        }

        private FishingTables fishing_;

        private int XiRand(int min, int max) => max <= min ? min : min + RandInt(max - min);
        private int XiRand(int max) => XiRand(0, max);
        private float XiRandF(float min, float max) => max <= min ? min : min + (float)(Random01() * (max - min));

        private void RegisterFishingHandler()
        {
            Register(0x110, (s, c, p) => ValidateFishing(c, p), (s, c, p) => OnFishing(s, c, p));
        }

        // GP_CLI_COMMAND_FISHING_2: UniqueNo u32 @4, para i32 @8, ActIndex u16 @12, mode u8 @14, para2 i32 @16
        private PacketValidator ValidateFishing(CharData c, BasicPacket p)
        {
            var cfg = ChatCfg;
            int para = (int)U32(p, 8), para2 = (int)U32(p, 16);
            byte mode = p.Buffer[14];
            return Pv(c).BlockedBy(BlockedState.InEvent)
                        .MustEqual(cfg.FishingEnable ? 1 : 0, 1, "Fishing is disabled")
                        .MustEqual(c.MLvl >= cfg.FishingMinLevel ? 1 : 0, 1, "Character below fishing minimum level")
                        .MustEqual(U32(p, 4), c.Id, "Character id mismatch")
                        .MustEqual(U16(p, 12), c.TargId, "Character targid mismatch")
                        .Range("mode", mode, FishingMode2CheckHook, FishingModePotentialTimeout)
                        .Custom(pv =>
                        {
                            switch (mode)
                            {
                                case FishingMode2CheckHook:
                                case FishingModeRelease:
                                    pv.MustEqual(para, 0, "para must be 0").MustEqual(para2, 0, "para2 must be 0");
                                    break;
                                case FishingModeEndMiniGame:
                                    pv.Range("para", para, 0, 300);
                                    if (para2 != 0 && c.HookedFish != null)
                                        pv.MustEqual(para2, c.HookedFish.Special, "para2 not equal to current hooked fish special");
                                    break;
                                case FishingModePotentialTimeout:
                                    pv.Range("para", para, 0, 10).MustEqual(para2, 0, "para2 must be 0");
                                    break;
                            }
                        });
        }

        private void OnFishing(MapSession s, CharData c, BasicPacket p) =>
            FishingAction(s, c, p.Buffer[14], U32(p, 8), U32(p, 16));

        // ---------------------------------------------------------------- tables

        private FishingTables Fishing => fishing_ ??= LoadFishingTables();

        private FishingTables LoadFishingTables()
        {
            var t = new FishingTables();
            var r = Exec("SELECT fa.areaid, fa.bound_type, fa.bound_height, fa.bounds, fa.center_x, fa.center_y, fa.center_z, " +
                         "fa.bound_radius, fa.zoneid, fz.difficulty FROM fishing_area fa LEFT JOIN fishing_zone fz ON fz.zoneid = fa.zoneid");
            while (r.Next())
            {
                var area = new FishingArea
                {
                    AreaId = r.Get<byte>("areaid"),
                    Type = r.Get<byte>("bound_type"),
                    Height = r.Get<byte>("bound_height"),
                    Center = new AreaVector(r.Get<float>("center_x"), r.Get<float>("center_y"), r.Get<float>("center_z")),
                    Radius = r.Get<byte>("bound_radius"),
                    Difficulty = r.Get<byte>("difficulty"),
                };
                if (!r.IsNull("bounds"))
                {
                    var blob = r.GetBlobBytes("bounds");
                    var tmp = new AreaVector[32];
                    int actual = 0;
                    for (int i = 0; i < 32 && (i + 1) * 12 <= blob.Length; i++)
                    {
                        tmp[i] = new AreaVector(BitConverter.ToSingle(blob, i * 12), BitConverter.ToSingle(blob, i * 12 + 4),
                                                BitConverter.ToSingle(blob, i * 12 + 8));
                        if (tmp[i].X != 0 || tmp[i].Y != 0 || tmp[i].Z != 0) actual = i + 1;
                    }
                    if (actual > 0) area.Bounds = tmp[..actual];
                }
                ushort zone = r.Get<ushort>("zoneid");
                if (!t.Areas.TryGetValue(zone, out var byId)) t.Areas[zone] = byId = new SortedDictionary<byte, FishingArea>();
                byId[area.AreaId] = area;
            }

            r = Exec("SELECT fishid, skill_level, difficulty, base_delay, base_move, min_length, max_length, size_type, log, quest, " +
                     "flags, legendary, legendary_flags, item, max_hook, rarity, required_keyitem, quest_only, ranking " +
                     "FROM fishing_fish WHERE disabled = 0 AND ranking < 99");
            while (r.Next())
            {
                var f = new FishData
                {
                    Id = r.Get<ushort>("fishid"),
                    MaxSkill = r.Get<byte>("skill_level"),
                    Difficulty = r.Get<byte>("difficulty"),
                    BaseDelay = r.Get<byte>("base_delay"),
                    BaseMove = r.Get<byte>("base_move"),
                    MinLength = r.Get<ushort>("min_length"),
                    MaxLength = r.Get<ushort>("max_length"),
                    SizeType = r.Get<byte>("size_type"),
                    Log = r.Get<byte>("log"),
                    Quest = r.Get<byte>("quest"),
                    Flags = r.Get<uint>("flags"),
                    Legendary = r.Get<bool>("legendary"),
                    LegendaryFlags = r.Get<uint>("legendary_flags"),
                    IsItem = r.Get<bool>("item"),
                    MaxHook = r.Get<byte>("max_hook"),
                    Rarity = r.Get<ushort>("rarity"),
                    ReqKeyItem = r.Get<ushort>("required_keyitem"),
                    QuestOnly = r.Get<bool>("quest_only"),
                    Ranking = r.Get<byte>("ranking"),
                };
                t.Fish[f.Id] = f;
            }

            r = Exec("SELECT rodid, size_type, fish_attack, lgd_bonus_attack, fish_recovery, fish_time, lgd_bonus_time, sm_delay_Bonus, " +
                     "sm_move_bonus, lg_delay_bonus, lg_move_bonus, multiplier, breakable, broken_rodid, flags, legendary, min_rank, max_rank " +
                     "FROM fishing_rod");
            while (r.Next())
            {
                var rod = new RodData
                {
                    Id = r.Get<ushort>("rodid"),
                    SizeType = r.Get<byte>("size_type"),
                    FishAttack = r.Get<byte>("fish_attack"),
                    LgdBonusAtk = r.Get<byte>("lgd_bonus_attack"),
                    FishRecovery = r.Get<byte>("fish_recovery"),
                    FishTime = r.Get<byte>("fish_time"),
                    LgdBonusTime = r.Get<byte>("lgd_bonus_time"),
                    SmDelayBonus = r.Get<byte>("sm_delay_Bonus"),
                    SmMoveBonus = r.Get<byte>("sm_move_bonus"),
                    LgDelayBonus = r.Get<byte>("lg_delay_bonus"),
                    LgMoveBonus = r.Get<byte>("lg_move_bonus"),
                    Multiplier = r.Get<byte>("multiplier"),
                    Breakable = r.Get<bool>("breakable"),
                    BrokenRodId = r.Get<ushort>("broken_rodid"),
                    Flags = r.Get<uint>("flags"),
                    Legendary = r.Get<bool>("legendary"),
                    MinRank = r.Get<ushort>("min_rank"),
                    MaxRank = r.Get<ushort>("max_rank"),
                };
                t.Rods[rod.Id] = rod;
            }

            r = Exec("SELECT baitid, type, maxhook, flags FROM fishing_bait");
            while (r.Next())
            {
                var bait = new BaitData { Id = r.Get<ushort>("baitid"), Type = r.Get<byte>("type"), MaxHook = r.Get<byte>("maxhook"), Flags = r.Get<uint>("flags") };
                t.Baits[bait.Id] = bait;
            }

            r = Exec("SELECT baitid, fishid, power FROM fishing_bait_affinity");
            while (r.Next())
            {
                ushort bait = r.Get<ushort>("baitid");
                if (!t.Affinities.TryGetValue(bait, out var powers)) t.Affinities[bait] = powers = new Dictionary<ushort, byte>();
                powers[r.Get<ushort>("fishid")] = r.Get<byte>("power");
            }

            r = Exec("SELECT groupid, fishid, rarity FROM fishing_group");
            while (r.Next())
            {
                ushort group = r.Get<ushort>("groupid");
                if (!t.Groups.TryGetValue(group, out var fish)) t.Groups[group] = fish = new SortedDictionary<ushort, ushort>();
                fish[r.Get<ushort>("fishid")] = r.Get<ushort>("rarity");
            }

            r = Exec("SELECT zoneid, areaid, groupid FROM fishing_catch");
            while (r.Next()) t.CatchLists[(r.Get<ushort>("zoneid"), r.Get<byte>("areaid"))] = r.Get<ushort>("groupid");

            // CreateFishingPools
            r = Exec("SELECT fc.zoneid, fc.areaid, fg.fishid, fg.pool_size, fg.restock_rate FROM fishing_group fg JOIN fishing_catch fc USING(groupid)");
            while (r.Next())
            {
                var key = (r.Get<ushort>("zoneid"), r.Get<byte>("areaid"));
                if (!t.Pools.TryGetValue(key, out var stock)) t.Pools[key] = stock = new SortedDictionary<ushort, FishStock>();
                int size = r.Get<int>("pool_size");
                stock[r.Get<ushort>("fishid")] = new FishStock { Quantity = size, Max = size, Restock = r.Get<int>("restock_rate") };
            }

            // LoadFishMobs
            r = Exec("SELECT mobid, name, level, difficulty, base_delay, base_move, log, quest, nm, nm_flags, rarity, min_respawn, " +
                     "required_keyitem, required_baitid, areaid, zoneid, quest_only, min_length, max_length, ranking, max_respawn, " +
                     "alternative_baitid FROM fishing_mob WHERE disabled=0 ORDER BY mobid ASC");
            while (r.Next())
            {
                var mob = new FishMobData
                {
                    MobId = r.Get<uint>("mobid"),
                    Name = r.Get<string>("name"),
                    Level = r.Get<byte>("level"),
                    Difficulty = r.Get<byte>("difficulty"),
                    BaseDelay = r.Get<byte>("base_delay"),
                    BaseMove = r.Get<byte>("base_move"),
                    Log = r.Get<byte>("log"),
                    Quest = r.Get<byte>("quest"),
                    Nm = r.Get<bool>("nm"),
                    NmFlags = r.Get<uint>("nm_flags"),
                    Rarity = r.Get<ushort>("rarity"),
                    MinRespawn = r.Get<ushort>("min_respawn"),
                    ReqKeyItem = r.Get<ushort>("required_keyitem"),
                    ReqBaitId = r.Get<ushort>("required_baitid"),
                    AreaId = r.Get<byte>("areaid"),
                    ZoneId = r.Get<ushort>("zoneid"),
                    QuestOnly = r.Get<bool>("quest_only"),
                    MinLength = r.Get<ushort>("min_length"),
                    MaxLength = r.Get<ushort>("max_length"),
                    Ranking = r.Get<byte>("ranking"),
                    MaxRespawn = r.Get<ushort>("max_respawn"),
                    AltBaitId = r.Get<ushort>("alternative_baitid"),
                };
                if (!t.Mobs.TryGetValue(mob.ZoneId, out var mobs)) t.Mobs[mob.ZoneId] = mobs = new SortedDictionary<uint, FishMobData>();
                mobs[mob.MobId] = mob;
            }

            // LoadChests: the Brigand's Chart chests
            r = Exec("SELECT distinct npcid, (npcid & 0xFFF000) >> 12 as zoneid FROM npc_list WHERE name = 'Jade_Etui'");
            while (r.Next())
            {
                ushort zone = r.Get<ushort>("zoneid");
                if (!t.Chests.TryGetValue(zone, out var chests)) t.Chests[zone] = chests = new List<uint>();
                chests.Add(r.Get<uint>("npcid"));
            }
            return t;
        }

        // GetMobPool: the zone's mobs that are not quest-only
        private List<FishMobData> GetMobPool(ushort zone)
        {
            var pool = new List<FishMobData>();
            if (Fishing.Mobs.TryGetValue(zone, out var mobs))
                foreach (var mob in mobs.Values)
                    if (!mob.QuestOnly) pool.Add(mob);
            return pool;
        }

        // GetChestPool
        private List<uint> GetChestPool(ushort zone) =>
            Fishing.Chests.TryGetValue(zone, out var chests) ? new List<uint>(chests) : new List<uint>();

        private NpcEntity FishChestNpc(uint id) => HeldZone((ushort)(id >> 12 & 0xFFF))?.NpcById.TryGetValue(id, out var npc) == true ? npc : null;

        private static uint FishVar(Dictionary<string, uint> vars, string name) => vars.TryGetValue(name, out uint v) ? v : 0;

        // CBattleEntity::isAlive: not at 0 HP, gone, dying or despawning
        private static bool FishMobAlive(MobEntity m) => m.Hp > 0 && m.IsSpawned && !m.IsDead && m.FadeAtMs == 0;

        // RestockFishingAreas: on each Vana'diel time-of-day change
        private void RestockFishingAreas()
        {
            if (fishing_ == null) return;
            foreach (var pool in fishing_.Pools.Values)
                foreach (var stock in pool.Values)
                    if (stock.Quantity < stock.Max) stock.Quantity = Math.Min(stock.Max, stock.Quantity + stock.Restock);
        }

        private bool IsFishPoolDepleted(ushort zone, byte area, ushort fishId) =>
            !Fishing.Pools.TryGetValue((zone, area), out var pool) || !pool.TryGetValue(fishId, out var stock) || stock.Quantity == 0;

        private void ReduceFishPool(ushort zone, byte area, uint fishId)
        {
            if (Fishing.Fish.TryGetValue((ushort)fishId, out var fish) && fish.QuestOnly) return;
            if (Fishing.Pools.TryGetValue((zone, area), out var pool) && pool.TryGetValue((ushort)fishId, out var stock) && stock.Quantity > 0)
                stock.Quantity--;
        }

        private ushort FishingMessageOffset(CharData c) => (ushort)(scripts_?.GetTextIdVariable(c.Zone, "FISHING_MESSAGE_OFFSET") ?? 0);

        // ---------------------------------------------------------------- moon, gear and skill

        private static float MoonPattern1(byte x) => Math.Clamp(0.5f * MathF.Cos(1.75f * x + 0.10f) + 0.5f, 0f, 1f);
        private static float MoonPattern2(byte x) => Math.Clamp(0.5f * MathF.Cos(1.75f * x + 3.30f) + 0.5f, 0f, 1f);
        private static float MoonPattern3(byte x) => Math.Clamp(1.0f - (x / 7), 0f, 1f);
        private static float MoonPattern4(byte x) => Math.Clamp(0.5f * MathF.Cos(0.90f * x + 3.14f) + 0.5f, 0f, 1f);
        private static float MoonPattern5(byte x) => Math.Clamp(0.5f * MathF.Cos(0.90f * x + 0.00f) + 0.5f, 0f, 1f);

        // GetMoonPhase: the eight-step moon from the 0-100 phase and its direction
        private byte FishingMoonPhase()
        {
            long ticks = VanaTicksNow();
            uint p = VanaTime.MoonPhase(ticks);
            byte d = VanaTime.MoonDirection(ticks);
            if (p <= 5 || (p <= 10 && d == 1)) return 0;
            if (p >= 7 && p <= 38 && d == 2) return 1;
            if (p >= 40 && p <= 55 && d == 2) return 2;
            if (p >= 57 && p <= 88 && d == 2) return 3;
            if (p >= 95 || (p >= 90 && d == 2)) return 4;
            if (p >= 62 && p <= 93 && d == 1) return 5;
            if (p >= 45 && p <= 60 && d == 1) return 6;
            if (p >= 12 && p <= 42 && d == 1) return 7;
            return 0;
        }

        private byte LuckyMoonModifier() => (byte)(1 + (byte)MathF.Floor(MoonPattern1(FishingMoonPhase()) * 3));

        // GetFishingGear: only the fishing pieces count
        private static (ushort Body, ushort Hands, ushort Legs, ushort Feet, ushort Waist) FishingGear(CharData c)
        {
            ushort Id(int slot) => c.Equipped[slot]?.ItemId ?? 0;
            ushort body = Id(SlotBody), hands = Id(SlotHands), legs = Id(SlotLegs),
                   feet = Id(SlotFeet), waist = Id(SlotWaist);
            return (body is FishermansTunica or AnglersTunica or FishermansApron or FishermansSmock ? body : (ushort)0,
                    hands is FishermansGloves or AnglersGloves ? hands : (ushort)0,
                    legs is FishermansHose or AnglersHose ? legs : (ushort)0,
                    feet is FishermansBoots or AnglersBoots ? feet : (ushort)0,
                    waist == FishersRope ? waist : (ushort)0);
        }

        private static bool IsLiveBait(BaitData bait) => bait.Id == DrillCalamary || bait.Id == DwarfPugil;

        private static byte FishingSkill(CharData c) => (byte)(c.RealSkills[Skill.Fishing] / 10 + c.GetMod(Mod.FISH));

        private static bool IsFishingWeapon(CharData c, int slot) => c.Equipped[slot]?.Weapon?.Skill == Skill.Fishing;

        // GetHookTime: the wait before a bite can be checked
        private byte HookTime(CharData c)
        {
            int wait = 13;
            byte moon = FishingMoonPhase();
            if (moon == 0 || moon == 4) wait -= 4;
            uint hour = VanaTime.GetHour(VanaTicksNow());
            if (hour == 5 || hour == 17) wait -= 1;
            if (FishingGear(c).Waist == FishersRope) wait -= 1;
            return (byte)Math.Max(7, wait);
        }

        // ---------------------------------------------------------------- calculations

        private ushort CalculateStamina(int skill, byte count)
        {
            float mult = 1.0f + 0.1f * (count - 1);
            int modSkill = (int)MathF.Floor(mult * skill);
            return (ushort)(XiRand(95, 105) * ((modSkill + 36) / 2));
        }

        private static ushort CalculateAttack(bool legendary, byte difficulty, RodData rod) =>
            (ushort)MathF.Floor(difficulty * ((rod.FishAttack + (legendary ? rod.LgdBonusAtk : 0)) / 100f) * 20f);

        private static ushort CalculateHeal(bool legendary, byte difficulty, RodData rod)
        {
            ushort attack = CalculateAttack(legendary, difficulty, rod);
            return (ushort)(MathF.Floor(attack / 20f * (rod.FishRecovery / 100f)) * 10f);
        }

        private static ushort CalculateRegen(byte skill, RodData rod, byte catchType, byte size, byte catchSkill, bool legendary, bool nm)
        {
            byte regen = 128, drainDiff = 12, regenDiff = 24;
            byte regenMod = rod.Id == Ebisu ? (byte)11 : (byte)0;
            if (size > 0 && rod.Id != Ebisu) regen += 1;
            if (rod.Id is LuShang or Ebisu or LuShang1 or Ebisu1)
            {
                if (legendary) regen -= rod.Id is LuShang or LuShang1 ? (byte)1 : (byte)2;
                if (catchType == CatchMob) regen -= 3;
            }
            if (catchType <= CatchMob && !nm)
            {
                if (catchSkill <= skill + regenMod - drainDiff)
                {
                    float div = rod.Id == LuShang ? 1.4f : rod.Id == Ebisu ? 1.3f : 1.5f;
                    byte drain = (byte)(1 + (byte)MathF.Floor((skill + regenMod - drainDiff - catchSkill) / div));
                    regen -= Math.Min(drain, regen);
                }
                else if (catchType < CatchItemType && catchSkill - regenMod >= skill + regenDiff)
                {
                    float mult = rod.Id == LuShang ? 0.45f : rod.Id == Ebisu ? 0.4f : 0.5f;
                    regen += (byte)(1 + (byte)MathF.Floor((catchSkill - regenMod - (skill + regenDiff)) * mult));
                }
            }
            if (catchType == CatchChest && skill > catchSkill) regen -= (byte)((skill - catchSkill) / 5);
            return (ushort)Math.Clamp((int)regen, 0, 182);
        }

        private static byte CalculateHookTimeLimit(CharData c, bool legendary, uint legendaryFlags, byte size, RodData rod, BaitData bait)
        {
            byte time = rod.FishTime;
            if ((size == FishSizeLarge && (rod.Flags & RodFlagLargePenalty) != 0) ||
                (size == FishSizeSmall && (rod.Flags & RodFlagSmallPenalty) != 0))
                time -= 10;
            if (legendary && (rod.Flags & RodFlagLegendaryBonus) != 0) time += 10;
            if (c.HasKeyItem(KeyItemMooching) && IsLiveBait(bait)) time += 30;
            if (c.GetMod(Mod.ALBATROSS_RING_EFFECT) > 0) time += 30;
            if (legendary)
            {
                if ((legendaryFlags & LegendaryNoRodTimeBonus) != 0 ||
                    ((legendaryFlags & LegendaryEbisuTimeBonusOnly) != 0 && rod.Id == Ebisu))
                    time += rod.LgdBonusTime;
                if ((legendaryFlags & LegendaryHalfTime) != 0) time -= (byte)(rod.FishTime / 2);
                if ((legendaryFlags & LegendaryAddTimeBonus) != 0) time += (byte)(rod.Multiplier & 10);
            }
            return time;
        }

        private uint CalculateLuckyTiming(CharData c, byte skill, byte catchSkill, byte size, RodData rod, BaitData bait, bool legendary)
        {
            byte lucky = 10;
            float penalty = 0, bonus = 0;
            if (catchSkill > skill + 7) penalty += catchSkill - (skill + 7);
            else if (skill + 10 > catchSkill) bonus += (skill + 10 - catchSkill) / 20;

            byte moon = LuckyMoonModifier();
            bonus += moon * 5 + moon * XiRand(1, 5);

            uint hour = VanaTime.GetHour(VanaTicksNow());
            if (hour == 6 || hour == 7 || (hour >= 16 && hour <= 18)) bonus += 9;
            else if (hour >= 8 && hour <= 15) bonus += 3;
            else bonus += 6;

            if (legendary && rod.Legendary)
            {
                if (rod.Id == LuShang) bonus += 6;
                else if (rod.Id == Ebisu) bonus += 8;
                if (rod.SizeType == size) bonus += 4;
                else if (rod.SizeType > size) bonus += 2;
            }

            var gear = FishingGear(c);
            if (gear.Body == FishermansTunica) bonus += 0.5f;
            else if (gear.Body == AnglersTunica) bonus += 1;
            else if (gear.Body == FishermansApron || gear.Body == FishermansSmock) bonus += 3;
            foreach (ushort piece in new[] { gear.Hands, gear.Legs, gear.Feet })
            {
                if (piece is FishermansGloves or FishermansHose or FishermansBoots) bonus += 0.5f;
                else if (piece is AnglersGloves or AnglersHose or AnglersBoots) bonus += 1;
                else if (piece == Waders) bonus += 2;
            }

            if ((bait.Flags & BaitFlagGoldArrow) != 0) bonus *= 1.25f;
            lucky += (byte)MathF.Floor(bonus);
            lucky -= (byte)MathF.Floor(Math.Min(penalty, lucky));
            return Math.Max((byte)5, lucky);
        }

        private ushort CalculateHookChance(byte skill, FishData fish, BaitData bait, RodData rod)
        {
            ushort hc = 50; // floor(25 * (month 0.75 + hour 0.75 * 2 + moon 1.25 * 3) / 3): no fish patterns are loaded
            byte power = Fishing.Affinities.TryGetValue(bait.Id, out var powers) && powers.TryGetValue(fish.Id, out byte pw) ? pw : (byte)0;
            switch (power)
            {
                case 1: hc += (ushort)(bait.Type == BaitTypeLure ? 30 : 35); break;
                case 2: hc += (ushort)(bait.Type == BaitTypeLure ? 60 : 65); break;
                case 3: hc += (ushort)(bait.Type == BaitTypeLure ? 75 : 80); break;
            }
            if (fish.MaxSkill > skill) hc -= Math.Min((ushort)Math.Floor((fish.MaxSkill - skill) * 0.25), hc);
            if (skill - 10 > fish.MaxSkill) hc -= Math.Min((ushort)Math.Floor((skill - 10 - fish.MaxSkill) * 0.15), hc);
            if (!rod.Legendary)
            {
                if (fish.SizeType < rod.SizeType) hc -= Math.Min((ushort)3, hc);
                else if (rod.SizeType < fish.SizeType) hc -= Math.Min((ushort)5, hc);
            }
            if ((bait.Flags & BaitFlagShellfishAffinity) != 0 && (fish.Flags & FishFlagShellfish) != 0) hc += 50;
            if (fish.Rarity < 1000) hc = (ushort)MathF.Floor(hc * (fish.Rarity / 1000.0f));
            return Math.Clamp(hc, (ushort)20, (ushort)120);
        }

        private static ushort CalculateDelayOrMove(CharData c, byte baseValue, byte size, RodData rod, byte count, bool delay)
        {
            float mult = 1.0f + 0.1f * (count - 1.0f);
            byte v = (byte)MathF.Floor(baseValue * mult);
            if (size == FishSizeSmall) v += delay ? rod.SmDelayBonus : rod.SmMoveBonus;
            else v += delay ? rod.LgDelayBonus : rod.LgMoveBonus;
            if (c.GetMod(Mod.PENGUIN_RING_EFFECT) > 0) v += 2;
            return Math.Min((byte)15, v);
        }

        private static (byte Reason, byte Chance) CalculateLoseChance(byte skill, byte catchType, byte size, byte maxSkill, byte ranking, RodData rod)
        {
            byte tooBig = 0, tooSmall = 0, lowSkill = 0;
            if (!rod.Legendary)
            {
                if (size > rod.SizeType && ranking > rod.MaxRank)
                {
                    tooBig = 50;
                    if (skill < maxSkill) tooBig += (byte)(maxSkill - skill);
                    else if (skill > maxSkill) tooBig -= Math.Min((byte)(skill - maxSkill), tooBig);
                }
                else if (size < rod.SizeType && ranking < rod.MinRank)
                {
                    tooSmall = 50;
                    if (skill < maxSkill) tooSmall += (byte)(maxSkill - skill);
                    else if (skill > maxSkill) tooSmall -= Math.Min((byte)(skill - maxSkill), tooSmall);
                }
            }
            if (catchType < CatchItemType && skill + 7 < maxSkill) lowSkill = (byte)MathF.Floor((maxSkill - (skill + 7)) * 0.8f);

            if (tooBig > 0 && tooBig > lowSkill) return (FailLostTooBig, Math.Min(tooBig, (byte)50));
            if (tooSmall > 0 && tooSmall > lowSkill) return (FailLostTooSmall, Math.Min(tooSmall, (byte)50));
            if (catchType < CatchItemType && lowSkill > 0) return (FailLostLowSkill, Math.Min(lowSkill, (byte)55));
            return (FailNone, 0);
        }

        private static (byte Reason, byte Chance) CalculateSnapChance(byte skill, byte size, byte maxSkill, bool legendary, byte ranking, RodData rod)
        {
            byte lvl = skill + 10 > maxSkill ? (byte)2 : (byte)0, sizePen = 0, lgd = 0;
            if (!rod.Legendary && size > rod.SizeType) sizePen = 2;
            if (legendary)
            {
                if (!rod.Legendary) sizePen += 3;
                else lgd = 1;
            }
            byte total = (byte)(rod.MaxRank + lvl + lgd - sizePen);
            if (ranking > total)
                return (FailLineSnap, Math.Min((byte)MathF.Floor((byte)(ranking - total) * 8.5f), (byte)55));
            return (FailNone, 0);
        }

        private static (byte Reason, byte Chance) CalculateBreakChance(byte skill, byte size, byte maxSkill, bool legendary, byte ranking, RodData rod)
        {
            if (!rod.Breakable) return (FailNone, 0);
            byte lvl = skill + 10 > maxSkill ? (byte)2 : (byte)0, sizePen = 0, lgd = 0;
            if (!rod.Legendary && size > rod.SizeType) sizePen = 2;
            else if (rod.Legendary && size == 1) lgd = 1;
            if (!rod.Legendary && legendary) sizePen = 5;
            if (ranking > rod.MaxRank + lvl + lgd)
            {
                byte diff = (byte)(ranking - (rod.MaxRank + lvl + lgd));
                return (FailRodBreak, Math.Min((byte)MathF.Floor((diff + sizePen) * 1.3f), (byte)55));
            }
            return (FailNone, 0);
        }

        private (ushort Length, ushort Weight, bool Epic) CalculateBigFishStats(ushort minLength, ushort maxLength)
        {
            if (maxLength <= 1) return (0, 0, false);
            float w = XiRandF(4.65f, 5.15f);
            ushort length = (ushort)((XiRand(minLength, maxLength) + XiRand(minLength, maxLength)) / 2);
            ushort weight = (ushort)(short)MathF.Floor(length * w);
            return (length, weight, length > (minLength + maxLength) / 2 && w >= 5);
        }

        private byte CalculateFishSense(FishResponse response, byte skill, byte catchType, byte size, byte maxSkill, bool legendary,
                                        ushort minLength, ushort maxLength, byte ranking, RodData rod)
        {
            byte sense = SenseGood;
            if (catchType == CatchBigFish)
            {
                var big = CalculateBigFishStats(minLength, maxLength);
                response.Length = big.Length;
                response.Weight = big.Weight;
                response.Epic = big.Epic;
            }
            var lose = CalculateLoseChance(skill, catchType, size, maxSkill, ranking, rod);
            var snap = CalculateSnapChance(skill, size, maxSkill, legendary, ranking, rod);
            var brk = CalculateBreakChance(skill, size, maxSkill, legendary, ranking, rod);
            if (lose.Chance > 0 && snap.Chance == 0 && brk.Chance == 0)
            {
                if (lose.Reason == FailLostTooSmall) sense = SenseGood;
                else if (lose.Chance < 20) sense = (byte)(SenseNoSkill + XiRand(2));
                else if (lose.Chance < 45) sense = SenseNoSkill + 1;
                else sense = SenseNoSkill + 2;
            }
            else if (snap.Chance > 0 || brk.Chance > 0)
            {
                if (snap.Chance < 30 && brk.Chance < 30) sense = SenseBad;
                else if (snap.Chance < 45 && brk.Chance < 45) sense = (byte)(SenseBad + XiRand(2));
                else sense = SenseTerrible;
            }
            response.Lose = lose;
            response.Snap = snap;
            response.Break = brk;
            return sense;
        }

        private ushort CalculateCriticalBite(byte skill, byte fishSkill, RodData rod)
        {
            int ebisu = rod.Id == Ebisu ? 40 : 0;
            if (fishSkill - 4 > skill + ebisu) return 0;
            int check = Math.Max(0, fishSkill - 4);
            ushort chance = (ushort)(5 + Math.Max(skill + ebisu - check, 0) * 2);
            chance += (ushort)(10 * (2 - 2 * MoonPattern3(FishingMoonPhase())));
            return Math.Min(chance, (ushort)70);
        }

        // CalculateMobModifiers: an NM's random regen and heal bonuses and attack penalty
        private (byte Regen, byte AttackPenalty, byte Heal) CalculateMobModifiers(FishMobData mob)
        {
            byte regen = 0, attack = 0, heal = 0;
            if ((mob.NmFlags & FishNmRegenEasy) != 0) regen += (byte)XiRand(0, 1);
            if ((mob.NmFlags & FishNmRegenDifficult) != 0) regen += (byte)XiRand(1, 2);
            if ((mob.NmFlags & FishNmHealEasy) != 0) heal += (byte)XiRand(0, 10);
            if ((mob.NmFlags & FishNmHealDifficult) != 0) heal += (byte)XiRand(15, 30);
            if ((mob.NmFlags & FishNmAttackEasy) != 0) attack += (byte)XiRand(0, 10);
            if ((mob.NmFlags & FishNmAttackDifficult) != 0) attack += (byte)XiRand(15, 30);
            return (regen, attack, heal);
        }

        // ---------------------------------------------------------------- area geometry

        private static bool OnSegment(AreaVector p, AreaVector q, AreaVector r) =>
            q.X <= Math.Max(p.X, r.X) && q.X >= Math.Min(p.X, r.X) && q.Z <= Math.Max(p.Z, r.Z) && q.Z >= Math.Min(p.Z, r.Z);

        private static int Orientation(AreaVector p, AreaVector q, AreaVector r)
        {
            float val = MathF.Round(q.Z - p.Z, MidpointRounding.AwayFromZero) * (r.X - q.X) - (q.X - p.X) * (r.Z - q.Z);
            if (val == 0) return 0;
            return val > 0 ? 1 : 2;
        }

        private static bool DoIntersect(AreaVector p1, AreaVector q1, AreaVector p2, AreaVector q2)
        {
            int o1 = Orientation(p1, q1, p2), o2 = Orientation(p1, q1, q2), o3 = Orientation(p2, q2, p1), o4 = Orientation(p2, q2, q1);
            if (o1 != o2 && o3 != o4) return true;
            if (o1 == 0 && OnSegment(p1, p2, q1)) return true;
            if (o2 == 0 && OnSegment(p1, q2, q1)) return true;
            if (o3 == 0 && OnSegment(p2, p1, q2)) return true;
            return o4 == 0 && OnSegment(p2, q1, q2);
        }

        private static bool IsInsidePoly(AreaVector[] polygon, AreaVector p, float posY, byte height)
        {
            if (p.Y < posY - height / 2 || p.Y > posY + height / 2) return false;
            int n = polygon.Length;
            if (n < 3) return false;
            var extreme = new AreaVector(10000, p.Z, 0);
            int count = 0, i = 0;
            do
            {
                int next = (i + 1) % n;
                if (DoIntersect(polygon[i], polygon[next], p, extreme))
                {
                    if (Orientation(polygon[i], p, polygon[next]) == 0) return OnSegment(polygon[i], p, polygon[next]);
                    count++;
                }
                i = next;
            } while (i != 0);
            return (count & 1) != 0;
        }

        private static bool IsInsideCylinder(AreaVector center, AreaVector p, ushort radius, byte height)
        {
            if (p.Y < center.Y - height / 2 || p.Y > center.Y + height / 2) return false;
            float dx = Math.Abs(p.X - center.X);
            if (dx > radius) return false;
            float dz = Math.Abs(p.Z - center.Z);
            if (dz > radius) return false;
            if (dx + dz <= radius) return true;
            return dx * dx + dz * dz <= radius * radius;
        }

        private FishingArea GetFishingArea(CharData c)
        {
            if (c.MoghouseId != 0)
            {
                log_($"fishingutils::GetFishingArea() - Player {c.Name} is attempting to fish from Mog House");
                return null;
            }
            if (!Fishing.Areas.TryGetValue(c.Zone, out var areas)) return null;
            var loc = new AreaVector(c.X, c.Y, c.Z);
            foreach (var area in areas.Values)
            {
                switch (area.Type)
                {
                    case 0:
                        return area;
                    case 1:
                        if (IsInsideCylinder(area.Center, loc, area.Radius, area.Height)) return area;
                        break;
                    case 2:
                        if (IsInsidePoly(area.Bounds, loc, area.Center.Y, area.Height)) return area;
                        break;
                }
            }
            return null;
        }

        private FishingArea ChartArea(ushort zone) =>
            Fishing.Areas.TryGetValue(zone, out var areas) && areas.TryGetValue(2, out var area) ? area : null;

        private static bool LocalVarIs(CharData c, string name, uint value) => c.LocalVars.TryGetValue(name, out uint v) && v == value;

        // ---------------------------------------------------------------- the catch roll

        private FishResponse FishingCheck(CharData c, byte skill, RodData rod, BaitData bait, FishingArea area)
        {
            var t = Fishing;
            var response = new FishResponse { Distance = 2 };
            byte moon = FishingMoonPhase();
            float fishMoon = MoonPattern4(moon), itemMoon = MoonPattern2(moon), mobMoon = MoonPattern3(moon), noMoon = MoonPattern5(moon);

            ushort fishW, itemW, mobW, chestW = 0, noW;
            if (AreaOf(c).Types.Contains("city"))
            {
                fishW = (ushort)MathF.Floor(15 * fishMoon);
                itemW = (ushort)(25 + (ushort)MathF.Floor(20 * itemMoon));
                mobW = 0;
                noW = (ushort)(30 + (ushort)MathF.Floor(15 * noMoon));
            }
            else
            {
                fishW = (ushort)MathF.Floor(25 * fishMoon);
                itemW = (ushort)(10 + (ushort)MathF.Floor(15 * itemMoon));
                mobW = (ushort)(15 + (ushort)MathF.Floor(15 * mobMoon));
                noW = (ushort)(15 + (ushort)MathF.Floor(20 * noMoon));
            }

            ushort fishTotal = 0, itemTotal = 0;
            var fishHookPool = new List<(FishData Fish, ushort Chance)>();
            var itemHookPool = new List<(FishData Fish, ushort Chance)>();
            var noCatch = new HashSet<ushort>();
            FishData fishSel = null, itemSel = null;
            var mobHookPool = new List<FishMobData>();
            FishMobData mobSel = null;
            uint chestSel = 0;
            sbyte chestAngle = 0;

            var fishPool = new List<FishData>();
            var itemPool = new List<FishData>();
            if (t.CatchLists.TryGetValue((c.Zone, area.AreaId), out ushort groupId) && t.Groups.TryGetValue(groupId, out var group))
            {
                t.Affinities.TryGetValue(bait.Id, out var affinities);
                foreach (ushort fishId in group.Keys)
                {
                    if (!t.Fish.TryGetValue(fishId, out var f)) continue;
                    if (f.IsItem) itemPool.Add(f);
                    else if (affinities != null && affinities.ContainsKey(fishId)) fishPool.Add(f);
                }
            }

            if (fishPool.Count > 0)
            {
                ushort maxChance = 0;
                foreach (var f in fishPool)
                {
                    if ((skill >= f.MaxSkill || f.MaxSkill - skill <= 100) && (f.ReqKeyItem == 0 || c.HasKeyItem(f.ReqKeyItem)))
                    {
                        if (!f.QuestOnly && IsFishPoolDepleted(c.Zone, area.AreaId, f.Id)) noCatch.Add(f.Id);
                        ushort hc = CalculateHookChance(skill, f, bait, rod);
                        fishHookPool.Add((f, hc));
                        fishTotal += hc;
                        maxChance = Math.Max(maxChance, hc);
                    }
                }
                fishW = (ushort)Math.Clamp(maxChance + fishW, 10, 120);
            }

            foreach (var item in itemPool)
            {
                if (!(item.QuestOnly || item.ReqKeyItem == 0 || c.HasKeyItem(item.ReqKeyItem))) continue;
                if (item.Quest < 255 && item.Log < 255)
                {
                    if (QuestStatus(c, item.Log, item.Quest) == 1)
                    {
                        itemHookPool.Add((item, 100));
                        itemW += 1000;
                    }
                }
                else
                {
                    if (!item.QuestOnly && IsFishPoolDepleted(c.Zone, area.AreaId, item.Id)) noCatch.Add(item.Id);
                    ushort hc = (ushort)MathF.Floor(100 * (item.Rarity / 1000.0f));
                    itemTotal += hc;
                    itemHookPool.Add((item, hc));
                }
            }

            // the hookable mobs: down and not on another line; an NM in its area with the right bait, by quest, rarity or
            // respawn (its required_keyitem is loaded but never checked, as in the C++)
            foreach (var mob in GetMobPool(c.Zone))
            {
                var pm = MobById(mob.MobId);
                if (pm == null || FishVar(pm.LocalVars, "hooked") != 0 || FishMobAlive(pm)) continue;
                if (mob.Nm && (mob.AreaId == 0 || mob.AreaId == area.AreaId) &&
                    ((mob.ReqBaitId == 0 || mob.ReqBaitId == bait.Id) || (mob.AltBaitId > 0 && mob.AltBaitId == bait.Id)))
                {
                    bool mobAdd = false;
                    if (mob.Quest < 255 && mob.Log < 255)
                    {
                        if (QuestStatus(c, mob.Log, mob.Quest) == 1)
                        {
                            mobAdd = true;
                            mobW += 1000;
                        }
                    }
                    else if (mob.Rarity < 1000)
                    {
                        ushort randomizer = (ushort)XiRand(1, 1000);
                        randomizer -= Math.Clamp((ushort)MathF.Floor((1 - MoonPattern2(FishingMoonPhase())) * 100), (ushort)0, randomizer);
                        if (randomizer <= mob.Rarity)
                        {
                            mobAdd = true;
                            mobW += 50;
                        }
                    }
                    else if (mob.MaxRespawn > 0)
                    {
                        uint respawnTime = FishVar(pm.LocalVars, "lastTOD") + FishVar(pm.LocalVars, "respawnTime");
                        if (UnixNow() > respawnTime)
                        {
                            mobAdd = true;
                            mobW += 50;
                        }
                    }
                    else mobAdd = true;
                    if (mobAdd) mobHookPool.Add(mob);
                }
                else if (!mob.Nm && (mob.AreaId == 0 || mob.AreaId == area.AreaId)) mobHookPool.Add(mob);
            }

            fishW = (ushort)MathF.Floor(fishW * WeatherFishingModifier(c));
            if (area.Difficulty > 0) noW += (ushort)(area.Difficulty * XiRand(20, 30));

            ushort body = FishingGear(c).Body;
            if ((body == FishermansApron || body == FishermansSmock) && itemW > 0)
            {
                ushort sub = (ushort)MathF.Floor(itemW * 0.25f);
                if (sub > 0)
                {
                    itemW -= sub;
                    noW += sub;
                }
            }

            if ((bait.Flags & BaitFlagPoorFish) != 0 && fishW > 0)
            {
                fishW -= (ushort)MathF.Floor(fishW * 0.25f);
                itemW = (ushort)(fishW + (ushort)MathF.Floor(fishW * 0.10f));
                noW += (ushort)MathF.Floor(noW * 0.25f);
            }

            if (fishHookPool.Count > 0)
            {
                ushort agg = 0, select = (ushort)XiRand(fishTotal);
                foreach (var (f, hc) in fishHookPool)
                {
                    agg += hc;
                    if (select < agg && !noCatch.Contains(f.Id))
                    {
                        fishSel = f;
                        if ((rod.Id == LuShang || rod.Id == Ebisu) && skill > f.MaxSkill + 7)
                        {
                            byte skilldiff = (byte)(skill - f.MaxSkill);
                            byte initial = rod.Id == LuShang ? (byte)10 : (byte)15;
                            byte divisor = rod.Id == LuShang ? (byte)15 : (byte)13;
                            byte mult = (byte)(1 + skilldiff / divisor);
                            fishW += (ushort)(initial + (byte)(skilldiff * mult / (f.SizeType + 1)));
                        }
                        break;
                    }
                }
            }
            else
            {
                noW += (ushort)(fishW / 2);
                fishW = 0;
            }

            if (itemHookPool.Count > 0)
            {
                ushort agg = 0, select = (ushort)XiRand(itemTotal);
                foreach (var (item, hc) in itemHookPool)
                {
                    agg += hc;
                    if (select < agg && !noCatch.Contains(item.Id))
                    {
                        itemSel = item;
                        break;
                    }
                }
            }
            else
            {
                noW += (ushort)(itemW / 2);
                itemW = 0;
            }

            // Brigand's Chart: only its pugil can be hooked
            bool brigandsChart = c.Zone == ZoneBuburimuPeninsula && LocalVarIs(c, "bChartActive", 1);
            if (brigandsChart)
            {
                mobHookPool.Clear();
                if (t.Mobs.TryGetValue(c.Zone, out var zoneMobs))
                {
                    foreach (var mob in zoneMobs.Values)
                    {
                        if (mob.Name != "Puffer_Pugil_Brigand") continue;
                        var pm = MobById(mob.MobId);
                        if (pm != null && FishVar(pm.LocalVars, "hooked") == 0 && !FishMobAlive(pm)) mobHookPool.Add(mob);
                        break;
                    }
                }
            }

            if (mobHookPool.Count > 0) mobSel = mobHookPool[XiRand(mobHookPool.Count)];
            else
            {
                noW += (ushort)(mobW / 2);
                mobW = 0;
            }

            var chestPool = GetChestPool(c.Zone);
            if (chestPool.Count > 0)
            {
                if (brigandsChart)
                {
                    foreach (uint chestId in chestPool)
                    {
                        var chest = FishChestNpc(chestId);
                        if (chest != null && FishVar(chest.LocalVars, "owner") == 0)
                        {
                            chestSel = chestId;
                            break;
                        }
                    }
                }
            }
            else
            {
                noW += chestW;
                chestW = 0;
            }

            if (c.Zone == ZoneValkurmDunes && LocalVarIs(c, "pChartActive", 1) && area.AreaId == 2)
            {
                fishW = 0;
                itemW = 100;
                mobW = chestW = noW = 0;
            }
            else if (brigandsChart)
            {
                fishW = itemW = noW = 0;
                mobW = mobHookPool.Count > 0 ? (ushort)25 : (ushort)0;
                chestW = chestSel > 0 ? (ushort)75 : (ushort)0;
            }

            if (fishW == 0 && itemW == 0 && mobW == 0 && chestW == 0) noW = 1000;

            ushort total = (ushort)(fishW + itemW + mobW + chestW + noW);
            ushort selector = (ushort)XiRand(total);

            if (fishSel != null && selector < fishW)
            {
                var f = fishSel;
                response.Hooked = true;
                response.CatchId = f.Id;
                response.CatchType = f.SizeType == FishSizeSmall ? CatchSmallFish : CatchBigFish;
                response.CatchLevel = f.MaxSkill;
                response.CatchDifficulty = f.Difficulty;
                response.CatchSizeType = f.SizeType;
                response.Legendary = f.Legendary;
                response.Count = f.MaxHook > 1 && bait.MaxHook > 1 ? (byte)XiRand(1, bait.MaxHook) : (byte)1;
                response.Stamina = CalculateStamina(f.MaxSkill, response.Count);
                response.Delay = CalculateDelayOrMove(c, f.BaseDelay, f.SizeType, rod, response.Count, true);
                response.Regen = CalculateRegen(skill, rod, response.CatchType, f.SizeType, f.MaxSkill, f.Legendary, false);
                response.Response = CalculateDelayOrMove(c, f.BaseMove, f.SizeType, rod, response.Count, false);
                response.AttackDmg = CalculateAttack(f.Legendary, f.Difficulty, rod);
                response.Heal = CalculateHeal(f.Legendary, f.Difficulty, rod);
                response.TimeLimit = CalculateHookTimeLimit(c, f.Legendary, f.LegendaryFlags, f.SizeType, rod, bait);
                response.Sense = CalculateFishSense(response, skill, response.CatchType, f.SizeType, f.MaxSkill, f.Legendary,
                                                    f.MinLength, f.MaxLength, f.Ranking, rod);
                response.HookSense = f.SizeType == FishSizeSmall ? (byte)0 : (byte)1;

                if (response.CatchSizeType == FishSizeLarge)
                {
                    var big = CalculateBigFishStats(f.MinLength, f.MaxLength);
                    response.Length = big.Length;
                    response.Weight = big.Weight;
                    if (big.Epic) response.Sense = SenseEpic;
                }

                if (response.Sense == SenseGood && XiRand(100) < CalculateCriticalBite(skill, f.MaxSkill, rod))
                {
                    response.HookSense += 2;
                    response.Sense = SenseKeen;
                }

                response.Special = CalculateLuckyTiming(c, skill, f.MaxSkill, f.SizeType, rod, bait, f.Legendary);
                if (response.Sense == SenseKeen)
                {
                    response.Special += 50;
                    response.Heal = (ushort)MathF.Floor(response.Heal * 0.7f);
                }
            }
            else if (itemSel != null && selector < itemW + fishW)
            {
                var item = itemSel;
                response.Hooked = true;
                response.CatchId = item.Id;
                response.CatchType = CatchItemType;
                response.CatchLevel = item.MaxSkill;
                response.CatchDifficulty = item.Difficulty;
                response.CatchSizeType = item.SizeType;
                response.Legendary = false;
                response.Count = 1;
                response.Stamina = CalculateStamina(item.MaxSkill, 1);
                response.Delay = CalculateDelayOrMove(c, item.BaseDelay, item.SizeType, rod, 1, true);
                response.Regen = CalculateRegen(skill, rod, response.CatchType, item.SizeType, item.MaxSkill, false, false);
                response.Response = CalculateDelayOrMove(c, item.BaseMove, item.SizeType, rod, 1, false);
                response.AttackDmg = CalculateAttack(item.Legendary, item.Difficulty, rod);
                response.Heal = CalculateHeal(item.Legendary, item.Difficulty, rod);
                response.TimeLimit = CalculateHookTimeLimit(c, item.Legendary, item.LegendaryFlags, item.SizeType, rod, bait);
                response.Sense = CalculateFishSense(response, skill, response.CatchType, item.SizeType, item.MaxSkill, false,
                                                    item.MinLength, item.MaxLength, item.Ranking, rod);
                response.HookSense = item.SizeType == FishSizeSmall ? (byte)0 : (byte)1;
                response.Special = CalculateLuckyTiming(c, skill, item.MaxSkill, item.SizeType, rod, bait, false);
            }
            else if (mobSel != null && selector < itemW + fishW + mobW)
            {
                var pm = MobById(mobSel.MobId);
                if (pm != null && FishVar(pm.LocalVars, "hooked") == 0)
                {
                    pm.LocalVars["hooked"] = 1;
                    pm.LocalVars["hookedTime"] = (uint)UnixNow();
                    var mob = mobSel;
                    response.Hooked = true;
                    response.CatchId = mob.MobId;
                    response.CatchType = CatchMob;
                    response.CatchLevel = mob.Level;
                    response.CatchDifficulty = mob.Difficulty;
                    response.CatchSizeType = FishSizeLarge;
                    response.Legendary = false;
                    response.Count = 1;
                    response.Stamina = CalculateStamina(mob.Level, 1);
                    response.Delay = CalculateDelayOrMove(c, mob.BaseDelay, response.CatchSizeType, rod, 1, true);
                    response.Regen = CalculateRegen(skill, rod, response.CatchType, response.CatchSizeType, mob.Level, false, mob.Nm);
                    response.Response = CalculateDelayOrMove(c, mob.BaseMove, response.CatchSizeType, rod, 1, false);
                    response.AttackDmg = CalculateAttack(false, mob.Difficulty, rod);
                    response.Heal = CalculateHeal(false, mob.Difficulty, rod);
                    response.TimeLimit = CalculateHookTimeLimit(c, false, 0, response.CatchSizeType, rod, bait);
                    response.Sense = CalculateFishSense(response, skill, CatchMob, FishSizeLarge, mob.Level, false,
                                                        mob.MinLength, mob.MaxLength, mob.Ranking, rod);
                    response.HookSense = 1;
                    response.Special = CalculateLuckyTiming(c, skill, mob.Level, FishSizeLarge, rod, bait, false);
                    var modifiers = CalculateMobModifiers(mob);
                    response.Regen += modifiers.Regen;
                    response.AttackDmg -= modifiers.AttackPenalty;
                    response.Heal += modifiers.Heal;
                }
            }
            else if (chestSel > 0 && selector < itemW + fishW + mobW + chestW)
            {
                response.Hooked = true;
                response.CatchId = chestSel;
                response.CatchType = CatchChest;
                response.CatchLevel = 1;
                response.CatchDifficulty = 1;
                response.CatchSizeType = FishSizeLarge;
                response.Legendary = false;
                response.Count = 1;
                response.Stamina = CalculateStamina(-14, 1);
                response.Delay = CalculateDelayOrMove(c, 10, response.CatchSizeType, rod, 1, true);
                response.Regen = CalculateRegen(skill, rod, response.CatchType, response.CatchSizeType, 1, false, false);
                response.Response = CalculateDelayOrMove(c, 15, response.CatchSizeType, rod, 1, false);
                response.AttackDmg = CalculateAttack(false, 16, rod);
                response.Heal = CalculateHeal(false, 16, rod);
                response.TimeLimit = CalculateHookTimeLimit(c, false, 0, response.CatchSizeType, rod, bait);
                response.Sense = CalculateFishSense(response, skill, CatchChest, FishSizeLarge, 1, false, 1, 1, 1, rod);
                response.Angle = chestAngle;
                response.HookSense = 1;
                response.Special = CalculateLuckyTiming(c, skill, 16, FishSizeLarge, rod, bait, false);
            }

            response.AreaId = area.AreaId;
            response.FishingToken = c.FishingToken;
            return response;
        }

        // ---------------------------------------------------------------- outcomes

        private void FishTalk(MapSession s, CharData c, ushort offset) =>
            Push(s, EventPackets.TalkNum(c.Id, c.TargId, true, (ushort)(FishingMessageOffset(c) + offset), true, 0));

        private int LoseCatch(MapSession s, CharData c, byte failType)
        {
            c.UpdateMask |= CharStats.UpdateHp;
            switch (failType)
            {
                case FailLineSnap: c.Animation = Anim.NewFishingLineBreak; FishTalk(s, c, FishMsgLineBreak); break;
                case FailRodBreak: c.Animation = Anim.NewFishingRodBreak; FishTalk(s, c, FishMsgRodBreak); break;
                case FailRodBreakTooBig: c.Animation = Anim.NewFishingRodBreak; FishTalk(s, c, FishMsgRodBreakTooBig); break;
                case FailRodBreakTooHeavy: c.Animation = Anim.NewFishingRodBreak; FishTalk(s, c, FishMsgRodBreakTooHeavy); break;
                case FailLostTooSmall: c.Animation = Anim.NewFishingStop; FishTalk(s, c, FishMsgLostTooSmall); break;
                case FailLostLowSkill: c.Animation = Anim.NewFishingStop; FishTalk(s, c, FishMsgLostLowSkill); break;
                case FailLostTooBig: c.Animation = Anim.NewFishingStop; FishTalk(s, c, FishMsgLostTooBig); break;
                default: c.Animation = Anim.NewFishingStop; FishTalk(s, c, FishMsgLost); break;
            }
            return 1;
        }

        private void CatchNothing(MapSession s, CharData c)
        {
            c.Animation = Anim.NewFishingStop;
            c.UpdateMask |= CharStats.UpdateHp;
            FishTalk(s, c, FishMsgNoCatch);
        }

        // GP_SERV_COMMAND_TALKNUMWORK2 (0x027): the catch message with the item id and count
        private static BasicPacket TalkNumWork2(CharData c, uint itemId, ushort message, uint count)
        {
            var w = new PacketWriter(0x027, 0x70);
            w.U32(4, c.Id);
            w.U16(8, c.TargId);
            w.U16(10, (ushort)(message + 0x8000));
            w.U32(16, itemId);
            w.U32(20, count);
            w.Name(32, c.Name, 32);
            return w.Packet;
        }

        // GP_SERV_COMMAND_FISH (0x115): the mini-game parameters
        private static BasicPacket FishPacket(FishResponse r)
        {
            var w = new PacketWriter(0x115, 24);
            w.U16(4, r.Stamina);
            w.U16(6, r.Delay);
            w.U16(8, r.Regen);
            w.U16(10, r.Response);
            w.U16(12, r.AttackDmg);
            w.U16(14, r.Heal);
            w.U16(16, r.TimeLimit);
            w.U8(18, r.HookSense);
            w.U32(20, r.Special);
            return w.Packet;
        }

        // GP_SERV_COMMAND_SCHEDULOR (0x038): an entity animation by FourCC, here the char on itself
        private static BasicPacket Schedulor(CharData c, FourCC id)
        {
            var w = new PacketWriter(0x038, 20);
            w.U32(4, c.Id);
            w.U32(8, c.Id);
            w.U32(12, (uint)id);
            w.U16(16, c.TargId);
            w.U16(18, c.TargId);
            return w.Packet;
        }

        private void CatchFish(MapSession s, CharData c, ushort fishId, bool big, ushort length, ushort weight, byte count)
        {
            ushort off = FishingMessageOffset(c);
            c.Animation = Anim.NewFishingCaught;
            c.UpdateMask |= CharStats.UpdateHp;
            if (FreeSlots(c, Container.Inventory) == 0)
            {
                PushCharInRange(c, TalkNumWork2(c, fishId, (ushort)(off + FishMsgCatchInvFull), count), true);
                return;
            }
            if (!Fishing.Fish.ContainsKey(fishId))
            {
                log_($"Invalid ItemID {fishId} for fished item");
                c.Animation = Anim.NewFishingStop;
                c.UpdateMask |= CharStats.UpdateHp;
                FishTalk(s, c, FishMsgLost);
                return;
            }
            var fish = new Item { ItemId = fishId, Quantity = count };
            if (big && length > 1 && weight > 1)
            {
                BitConverter.TryWriteBytes(fish.Extra.AsSpan(0), length);
                BitConverter.TryWriteBytes(fish.Extra.AsSpan(2), weight);
            }
            using (var t = new ItemTransaction(this, c))
            {
                if (t.GiveItem(Container.Inventory, fish) == null || !t.Commit())
                {
                    log_($"fishingutils: {c.Name} did not receive the fish they caught");
                    return;
                }
            }
            PushCharInRange(c, TalkNumWork2(c, fishId, (ushort)(off + (count > 1 ? FishMsgCatchMulti : FishMsgCatch)), count), true);
        }

        private void CatchItem(MapSession s, CharData c, ushort itemId, byte count)
        {
            ushort off = FishingMessageOffset(c);
            c.Animation = Anim.NewFishingCaught;
            c.UpdateMask |= CharStats.UpdateHp;
            if (FreeSlots(c, Container.Inventory) == 0)
            {
                PushCharInRange(c, TalkNumWork2(c, itemId, (ushort)(off + FishMsgCatchInvFull), count), true);
                return;
            }
            if (ItemBasic(itemId) == null)
            {
                log_($"Invalid ItemID {itemId} for fished item");
                c.Animation = Anim.NewFishingStop;
                c.UpdateMask |= CharStats.UpdateHp;
                FishTalk(s, c, FishMsgLost);
                return;
            }
            using (var t = new ItemTransaction(this, c))
            {
                if (t.Give(Container.Inventory, itemId, count) == null || !t.Commit())
                {
                    log_($"fishingutils: {c.Name} did not receive the item they caught");
                    return;
                }
            }
            PushCharInRange(c, TalkNumWork2(c, itemId, (ushort)(off + (count > 1 ? FishMsgCatchMulti : FishMsgCatch)), count), true);
        }

        // GP_SERV_COMMAND_TALKNUMNAME (0x043): a message naming the char
        private static BasicPacket TalkNumName(CharData c, ushort message)
        {
            var w = new PacketWriter(0x043, 0x20);
            w.U32(4, c.Id);
            w.U16(8, c.TargId);
            w.U16(10, (ushort)(message + 0x8000));
            w.Name(16, c.Name, 16);
            return w.Packet;
        }

        // CanFishMob: a hooked mob still down
        private static bool CanFishMob(MobEntity m) =>
            m != null && !FishMobAlive(m) && m.Status == NpcEntity.StatusDisappear && FishVar(m.LocalVars, "hooked") == 1;

        // CatchMonster: the mob spawns two yalms behind the angler and, unless sneak keeps it off, engages and is claimed
        private int CatchMonster(MapSession s, CharData c, uint mobId)
        {
            var pm = MobById(mobId);
            FishMobData mob = Fishing.Mobs.TryGetValue(c.Zone, out var mobs) && mobs.TryGetValue(mobId, out var fm) ? fm : null;
            if (pm == null || mob == null)
            {
                log_($"Invalid MobID {mobId} for fished monster");
                c.Animation = Anim.NewFishingStop;
                c.UpdateMask |= CharStats.UpdateHp;
                FishTalk(s, c, FishMsgLost);
                return 0;
            }
            if (FishMobAlive(pm) || (mob.QuestOnly && FishVar(pm.LocalVars, "catchable") == 0))
            {
                c.Animation = Anim.NewFishingStop;
                c.UpdateMask |= CharStats.UpdateHp;
                FishTalk(s, c, FishMsgLost);
                return 0;
            }

            c.Animation = Anim.NewFishingMonster;
            c.UpdateMask |= CharStats.UpdateHp;
            PushCharInRange(c, TalkNumName(c, (ushort)(FishingMessageOffset(c) + FishMsgMonster)), true);

            double radians = c.Rotation * Math.PI / 128;
            float x = c.X - 2.0f * (float)Math.Cos(radians), y = c.Y - 0.5f, z = c.Z + 2.0f * (float)Math.Sin(radians);
            (pm.SpawnX, pm.SpawnY, pm.SpawnZ) = (x, y, z);
            pm.SpawnRotation = WorldAngle(x, z, 0, c.X, c.Z);
            SpawnMob(AreaOf(pm), pm);
            pm.SetMobMod(MobMod.Charmable, 0);
            pm.SetMobMod(MobMod.IdleDespawn, 180);
            long now = NowMs();
            pm.DespawnAtMs = now + 180_000;
            pm.LocalVars["hooked"] = 0;
            pm.LocalVars["respawnTime"] = mob.MaxRespawn > mob.MinRespawn ? (uint)XiRand(mob.MinRespawn, mob.MaxRespawn) : mob.MaxRespawn;

            if ((mob.Log < 255 && mob.Quest < 255) || mob.QuestOnly ||
                (pm.TrueDetection && (pm.GetMobMod(MobMod.Detection) & DetectsScent) != 0) || !c.StatusEffects.HasStatusEffect(EffectId.Sneak))
            {
                MobEngage(pm, c, now);
                ClaimMob(pm, c, now);
            }
            return 1;
        }

        // CatchChest: the chest is set down behind the angler, owned by them
        private int CatchChestNpc(MapSession s, CharData c, uint npcId, byte distance, sbyte angle)
        {
            var chest = FishChestNpc(npcId);
            if (chest == null)
            {
                log_($"Invalid NpcID {npcId} for fished chest");
                c.Animation = Anim.NewFishingStop;
                c.UpdateMask |= CharStats.UpdateHp;
                FishTalk(s, c, FishMsgLost);
                return 0;
            }

            c.Animation = Anim.NewFishingCaught;
            c.UpdateMask |= CharStats.UpdateHp;
            PushCharInRange(c, TalkNumName(c, (ushort)(FishingMessageOffset(c) + FishMsgCatchChest)), true);

            double radians = (c.Rotation + angle) * Math.PI / 128;
            chest.X = c.X - distance * (float)Math.Cos(radians);
            chest.Y = c.Y;
            chest.Z = c.Z + distance * (float)Math.Sin(radians);
            chest.Rotation = c.Rotation;
            chest.Status = NpcEntity.StatusNormal;
            chest.LocalVars["owner"] = c.Id;
            chest.UpdateMask |= EntityUpdatePacket.UpdateCombat;
            return 1;
        }

        // UnhookMob: the hooked mob can be hooked again; a lost NM with RESET_RESPAWN_ON_FAIL restarts its respawn (the
        // response's nm flags are never set in the C++, so it never does)
        private void UnhookMob(CharData c, bool lost)
        {
            var f = c.HookedFish;
            if (f == null || f.CatchType != CatchMob) return;
            if (MobById(f.CatchId) is not { } pm) return;
            pm.LocalVars["hooked"] = 0;
            if (lost && f.Nm && (f.NmFlags & FishNmResetRespawnOnFail) != 0) pm.LocalVars["lastTOD"] = (uint)UnixNow();
        }

        private void ReelInCatch(MapSession s, CharData c)
        {
            var f = c.HookedFish;
            switch (f.CatchType)
            {
                case CatchNone:
                    f.SuccessType = SuccessNone;
                    CatchNothing(s, c);
                    break;
                case CatchSmallFish:
                    f.SuccessType = SuccessCatchSmall;
                    CatchFish(s, c, (ushort)f.CatchId, false, 0, 0, f.Count);
                    break;
                case CatchBigFish:
                    f.SuccessType = SuccessCatchLarge;
                    CatchFish(s, c, (ushort)f.CatchId, true, f.Length, f.Weight, f.Count);
                    break;
                case CatchItemType:
                    f.SuccessType = SuccessCatchItem;
                    CatchItem(s, c, (ushort)f.CatchId, f.Count);
                    break;
                case CatchMob:
                    f.SuccessType = SuccessCatchMob;
                    CatchMonster(s, c, f.CatchId);
                    break;
                case CatchChest:
                    f.SuccessType = SuccessCatchChest;
                    CatchChestNpc(s, c, f.CatchId, f.Distance, f.Angle);
                    break;
            }
        }

        // BaitLoss: false when the ammo is not bait, or a lure is kept
        private bool BaitLoss(MapSession s, CharData c, bool removeFly, bool sendUpdate)
        {
            var info = c.Equipped[CharStats.SlotAmmo];
            var bait = CharStats.EquippedItem(c, CharStats.SlotAmmo);
            if (info?.Weapon == null || bait == null) return true;
            if (info.Weapon.Skill != Skill.Fishing) return false;
            if (!removeFly && StackSize(bait.ItemId) == 1) return false;
            if (c.HookedFish == null)
            {
                log_("PChar->hookedFish was null.");
                return true;
            }
            if (c.HookedFish.SuccessType == SuccessCatchItem) return true;

            Container loc = bait.Location;
            byte slot = bait.Slot;
            if (bait.Quantity == 1 && loader_.Stats is CharStats stats) stats.UnequipItem(c, CharStats.SlotAmmo);
            using var t = new ItemTransaction(this, c);
            if (!t.Take(loc, slot, 1) || !t.Commit()) log_($"fishingutils: {c.Name} did not lose bait in slot {slot}");
            else if (sendUpdate) Push(s, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
            return true;
        }

        private void RodBreak(MapSession s, CharData c)
        {
            var info = c.Equipped[CharStats.SlotRanged];
            var rodItem = CharStats.EquippedItem(c, CharStats.SlotRanged);
            if (info?.Weapon == null || rodItem == null) return;
            if (!Fishing.Rods.TryGetValue(info.ItemId, out var rod)) return;
            if (!rod.Breakable || rod.BrokenRodId == 0) return;

            BaitLoss(s, c, true, false);
            Container loc = rodItem.Location;
            byte slot = rodItem.Slot;
            if (loader_.Stats is CharStats stats) stats.UnequipItem(c, CharStats.SlotRanged);
            using (var t = new ItemTransaction(this, c))
            {
                if (!t.Take(loc, slot, 1) || t.Give(loc, rod.BrokenRodId, 1) == null || !t.Commit())
                    log_($"fishingutils: {c.Name} kept an unbroken rod in slot {slot}");
            }
            Push(s, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
        }

        // SendHookResponse: false when a hooked mob can no longer be fished (CancelOnMobLoadFailBait::Yes)
        private bool SendHookResponse(MapSession s, CharData c, FishResponse r)
        {
            switch (r.CatchType)
            {
                case CatchSmallFish: FishTalk(s, c, FishMsgHookedSmall); break;
                case CatchBigFish: FishTalk(s, c, FishMsgHookedLarge); break;
                case CatchItemType:
                case CatchChest: FishTalk(s, c, FishMsgHookedItem); break;
                case CatchMob:
                    if (CanFishMob(MobById(c.HookedFish.CatchId))) FishTalk(s, c, FishMsgHookedMonster);
                    else
                    {
                        CatchNothing(s, c);
                        return false;
                    }
                    break;
            }
            return true;
        }

        private void SendSenseMessage(MapSession s, CharData c, FishResponse r)
        {
            if (r.Sense >= SenseGood && r.Sense <= SenseNoSkill + 2) FishTalk(s, c, (ushort)(FishMsgGood + r.Sense - SenseGood));
            else if (r.Sense == SenseKeen)
                Push(s, EventPackets.TalkNumWork(c.Id, c.TargId, c.Name, true, (ushort)(FishingMessageOffset(c) + FishMsgKeen),
                                                 r.CatchId, 3, 3, 3, false));
            else if (r.Sense == SenseEpic) FishTalk(s, c, FishMsgEpic);
        }

        // FishingSkillup: a release after a catch above the char's level can raise fishing
        private void FishingSkillup(MapSession s, CharData c, byte catchLevel, byte successType)
        {
            if (successType == SuccessNone) return;
            int rank = c.RealSkillRank[Skill.Fishing];
            int maxSkill = (rank + 1) * 100;
            int charSkill = c.RealSkills[Skill.Fishing];
            int charSkillLevel = charSkill / 10;
            byte levelDiff = catchLevel > charSkillLevel ? (byte)(catchLevel - charSkillLevel) : (byte)0;
            if (catchLevel <= charSkillLevel || levelDiff > 50) return;

            int skillRoll = 90, bonusChanceRoll = 8;
            if (charSkillLevel < 50 && c.Equipped[CharStats.SlotRanged]?.Weapon != null && c.Equipped[CharStats.SlotRanged].ItemId == LuShang)
                skillRoll += 20;

            double normDist = Math.Exp(-0.5 * Math.Log(2 * Math.PI) - Math.Log(5) - Math.Pow(levelDiff - 11, 2) / 50);
            int distMod = (int)Math.Floor(normDist * 200);
            int maxChance = Math.Max(4, distMod + (100 - charSkillLevel) / 10 - charSkillLevel / 10);
            maxChance = (int)(maxChance * ChatCfg.FishingSkillMultiplier);

            long ticks = VanaTicksNow();
            byte phase = (byte)VanaTime.MoonPhase(ticks);
            switch (VanaTime.MoonDirection(ticks))
            {
                case 0:
                    if (phase == 0) { skillRoll -= 20; bonusChanceRoll -= 3; }
                    else if (phase == 100) { skillRoll += 10; bonusChanceRoll += 3; }
                    break;
                case 1:
                    if (phase <= 10) { skillRoll -= 15; bonusChanceRoll -= 2; }
                    else if (phase >= 95 && phase <= 100) { skillRoll += 5; bonusChanceRoll += 2; }
                    break;
                case 2:
                    if (phase <= 5) { skillRoll -= 10; bonusChanceRoll -= 1; }
                    else if (phase >= 90 && phase <= 100) bonusChanceRoll += 1;
                    break;
            }

            if (!AreaOf(c).Types.Contains("city")) skillRoll -= 10;
            if (charSkillLevel < 50) skillRoll -= 20 - (byte)(charSkillLevel / 3);
            int maxSkillAmount = Math.Min(1 + levelDiff / 5, 3);

            if (XiRand(skillRoll) >= maxChance) return;
            int amount = 1;
            if (XiRand(bonusChanceRoll) == 1) amount = XiRand(1, maxSkillAmount);
            if (amount + charSkill > maxSkill) amount = maxSkill - charSkill;
            if (amount <= 0) return;

            c.RealSkills[Skill.Fishing] += (ushort)amount;
            Push(s, SelfPackets.BattleMessage(c, Skill.Fishing, amount, MsgSkillGain));
            if (charSkill / 10 < (charSkill + amount) / 10)
            {
                c.WorkingSkills[Skill.Fishing] += 0x20;
                if (c.RealSkills[Skill.Fishing] >= maxSkill) c.WorkingSkills[Skill.Fishing] |= 0x8000;
                Push(s, SelfPackets.CliStatus2(c));
                Push(s, SelfPackets.BattleMessage(c, Skill.Fishing, (charSkill + amount) / 10, MsgSkillLevelUp));
            }
            SaveCharSkill(c, Skill.Fishing);
        }

        // ---------------------------------------------------------------- entry points

        // InterruptFishing: leaving mid-cast or mid-fight loses the bait and drops the catch
        private void InterruptFishing(MapSession s, CharData c)
        {
            if (c.Animation == Anim.NewFishingFish) BaitLoss(s, c, false, true);
            c.Animation = Anim.None;
            c.UpdateMask |= 0x1F;
            UnhookMob(c, false);
            c.HookedFish = null;
            Push(s, EventPackets.Eventucoff(c, EventucoffMode.Fishing));
        }

        // StartFishing: ACTION Fish casts the line
        private void StartFishing(MapSession s, CharData c)
        {
            var cfg = ChatCfg;
            if (!cfg.FishingEnable)
            {
                log_("Fishing is currently disabled");
                Push(s, ChatStd(c, ChatMessageSystem1, CborRaw.Bytes("Fishing is currently disabled")));
                Push(s, EventPackets.Eventucoff(c, EventucoffMode.Fishing));
                return;
            }
            if (c.MLvl < cfg.FishingMinLevel)
            {
                Push(s, ChatStd(c, ChatMessageSystem1, CborRaw.Bytes("Your level is too low to fish.")));
                Push(s, EventPackets.Eventucoff(c, EventucoffMode.Fishing));
                return;
            }

            // charutils::RemoveInvisible
            c.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Invisible);
            long now = UnixNow();
            uint vanaTime = LoginPackets.VanadielTimestamp(now);
            if (c.NextFishTime > vanaTime)
            {
                FishTalk(s, c, FishMsgCannotFishMoment);
                Push(s, EventPackets.Eventucoff(c, EventucoffMode.Fishing));
                return;
            }
            SetCharVar(c, "[Fish]LastCastTime", (int)now, 0);
            c.LastCastTime = vanaTime;
            c.NextFishTime = vanaTime + 5;

            var area = GetFishingArea(c);
            if (area == null || area.AreaId == 0)
            {
                Push(s, SystemMessage(MsgStdCannotUseCommandAtTheMoment));
                Push(s, EventPackets.Eventucoff(c, EventucoffMode.Fishing));
                return;
            }

            c.FishingToken = (uint)(1 + XiRand(9999));
            c.HookedFish = new FishResponse { Hooked = false, SuccessType = SuccessNone };

            if (c.Animation != Anim.None)
            {
                FishTalk(s, c, FishMsgCannotFishMoment);
                Push(s, SystemMessage(MsgStdCannotUseCommandAtTheMoment));
                Push(s, EventPackets.Eventucoff(c, EventucoffMode.Fishing));
                return;
            }
            if (!IsFishingWeapon(c, CharStats.SlotRanged))
            {
                FishTalk(s, c, FishMsgNoRod);
                Push(s, EventPackets.Eventucoff(c, EventucoffMode.Fishing));
                return;
            }
            if (!IsFishingWeapon(c, CharStats.SlotAmmo))
            {
                FishTalk(s, c, FishMsgNoBait);
                Push(s, EventPackets.Eventucoff(c, EventucoffMode.Fishing));
                return;
            }

            if (Fishing.Rods.ContainsKey(c.Equipped[CharStats.SlotRanged].ItemId) && Fishing.Baits.ContainsKey(c.Equipped[CharStats.SlotAmmo].ItemId))
            {
                c.HookDelay = HookTime(c);
                c.Animation = Anim.NewFishingStart;
                c.UpdateMask |= CharStats.UpdateHp;
            }
            else
            {
                Push(s, SystemMessage(MsgStdCannotUseCommandAtTheMoment));
                Push(s, EventPackets.Eventucoff(c, EventucoffMode.Fishing));
            }
        }

        // FishingAction: the 0x110 modes
        private void FishingAction(MapSession s, CharData c, byte mode, uint para, uint para2)
        {
            var cfg = ChatCfg;
            uint stamina = para, special = para2;
            if (!cfg.FishingEnable || c.MLvl < cfg.FishingMinLevel)
            {
                log_("Fishing is currently disabled, but somehow we have someone commencing a fishing action");
                c.Animation = Anim.NewFishingStop;
                return;
            }

            uint vanaTime = LoginPackets.VanadielTimestamp(UnixNow());
            // ending the mini-game clears the token before the client asks to release, which must still clean up
            if (c.FishingToken == 0 && mode != FishingModeRelease)
            {
                c.Animation = Anim.NewFishingStop;
                return;
            }

            switch (mode)
            {
                case FishingMode2CheckHook:
                {
                    if (c.Animation != Anim.NewFishingStart || vanaTime < c.LastCastTime + c.HookDelay - 2)
                    {
                        CatchNothing(s, c);
                        return;
                    }

                    var area = GetFishingArea(c);
                    if (c.Zone == ZoneValkurmDunes && LocalVarIs(c, "pChartActive", 1)) area = ChartArea(ZoneValkurmDunes);
                    else if (c.Zone == ZoneBuburimuPeninsula && LocalVarIs(c, "bChartActive", 1)) area = ChartArea(ZoneBuburimuPeninsula);

                    c.HookedFish = null;
                    FishResponse response = null;
                    var rodInfo = c.Equipped[CharStats.SlotRanged];
                    var baitInfo = c.Equipped[CharStats.SlotAmmo];
                    if (area != null && rodInfo?.Weapon != null && baitInfo?.Weapon != null &&
                        Fishing.Rods.TryGetValue(rodInfo.ItemId, out var rod) && Fishing.Baits.TryGetValue(baitInfo.ItemId, out var bait))
                    {
                        response = FishingCheck(c, FishingSkill(c), rod, bait, area);
                        c.HookedFish = response;
                        if (response.CatchType > CatchNone && response.CatchType < CatchMob)
                            ReduceFishPool(c.Zone, area.AreaId, response.CatchId);
                    }

                    if (response == null || area == null || response.FishingToken != c.FishingToken)
                    {
                        CatchNothing(s, c);
                        Push(s, CharStatus(c));
                        Push(s, GameOkPackets.CharSync(c));
                    }
                    else if (response.Hooked && response.CatchType > 0 && response.CatchId > 0)
                    {
                        if (!SendHookResponse(s, c, response)) return;
                        SendSenseMessage(s, c, response);
                        Push(s, Schedulor(c, FourCC.Sweating));
                        c.UpdateMask |= CharStats.UpdateHp;
                        c.Animation = Anim.NewFishingFish;
                        Push(s, FishPacket(response));
                    }
                    else CatchNothing(s, c);
                    break;
                }

                case FishingModeEndMiniGame:
                {
                    var f = c.HookedFish;
                    if (stamina <= 4)
                    {
                        var rodInfo = c.Equipped[CharStats.SlotRanged];
                        if (f == null || rodInfo?.Weapon == null)
                        {
                            LoseCatch(s, c, FailNone);
                            UnhookMob(c, true);
                            BaitLoss(s, c, false, true);
                        }
                        else
                        {
                            // ReelCheck
                            bool caught = true, lineBreak = false, rodBreak = false;
                            byte failReason = FailNone;
                            if (XiRand(100) < f.Lose.Chance) { caught = false; failReason = f.Lose.Reason; }
                            else if (XiRand(100) < f.Snap.Chance) { caught = false; lineBreak = true; failReason = f.Snap.Reason; }
                            else if (XiRand(100) < f.Break.Chance) { caught = false; rodBreak = true; failReason = f.Break.Reason; }

                            if (f.FishingToken != c.FishingToken || f.Special != special)
                            {
                                LoseCatch(s, c, FailNone);
                                UnhookMob(c, true);
                                BaitLoss(s, c, false, true);
                            }
                            else if (caught)
                            {
                                c.FishingToken = 0;
                                ReelInCatch(s, c);
                                BaitLoss(s, c, false, true);
                            }
                            else
                            {
                                if (lineBreak)
                                {
                                    BaitLoss(s, c, true, true);
                                    f.SuccessType = SuccessLineBreak;
                                }
                                else if (rodBreak)
                                {
                                    RodBreak(s, c);
                                    f.SuccessType = SuccessRodBreak;
                                }
                                LoseCatch(s, c, failReason);
                                UnhookMob(c, true);
                            }
                        }
                    }
                    else if (stamina <= 0x14)
                    {
                        c.Animation = Anim.NewFishingLineBreak;
                        c.UpdateMask |= CharStats.UpdateHp;
                        BaitLoss(s, c, false, true);
                        FishTalk(s, c, FishMsgLostLowSkill);
                        if (f != null) f.SuccessType = SuccessNone;
                    }
                    else if (stamina <= 0x64)
                    {
                        c.Animation = Anim.NewFishingLineBreak;
                        c.UpdateMask |= CharStats.UpdateHp;
                        BaitLoss(s, c, true, true);
                        FishTalk(s, c, FishMsgLineBreak);
                        if (f != null) f.SuccessType = SuccessNone;
                    }
                    else if (stamina <= 0x100)
                    {
                        c.Animation = Anim.NewFishingStop;
                        c.UpdateMask |= CharStats.UpdateHp;
                        c.LastCastTime = 0;
                        if (f != null && f.Hooked && BaitLoss(s, c, false, true))
                        {
                            FishTalk(s, c, FishMsgGiveUpBaitLoss);
                            f.SuccessType = SuccessNone;
                        }
                        else if (f != null && !f.Hooked)
                        {
                            FishTalk(s, c, FishMsgGiveUp);
                            f.SuccessType = SuccessNone;
                        }
                    }
                    else
                    {
                        c.Animation = Anim.NewFishingStop;
                        c.UpdateMask |= CharStats.UpdateHp;
                        BaitLoss(s, c, false, true);
                        FishTalk(s, c, FishMsgLost);
                        if (f != null) f.SuccessType = SuccessNone;
                    }
                    c.FishingToken = 0;
                    break;
                }

                case FishingModePotentialTimeout:
                    FishTalk(s, c, FishMsgWarning);
                    return;

                default:
                {
                    var f = c.HookedFish;
                    if (f != null)
                    {
                        UnhookMob(c, false);
                        // no skill-ups for items or mobs
                        if (f.CatchType == CatchSmallFish || f.CatchType == CatchBigFish)
                        {
                            int chances = 1 + c.GetMod(Mod.PELICAN_RING_EFFECT);
                            for (int i = 0; i < chances; i++) FishingSkillup(s, c, f.CatchLevel, f.SuccessType);
                        }
                        c.HookedFish = null;
                    }
                    c.Animation = Anim.None;
                    c.UpdateMask |= CharStats.UpdateHp;
                    break;
                }
            }
        }
    }
}
