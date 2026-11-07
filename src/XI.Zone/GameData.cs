using System;
using System.Collections.Generic;
using XI.Persistence;

namespace XI.Zone
{
    // xi::SkillType ids the stat code names
    public static class Skill
    {
        public const int None = 0, HandToHand = 1, Dagger = 2, Sword = 3, GreatSword = 4, Axe = 5, GreatAxe = 6, Scythe = 7,
            Polearm = 8, Katana = 9, GreatKatana = 10, Club = 11, Staff = 12, AutomatonMelee = 22, AutomatonMagic = 24, Archery = 25,
            Marksmanship = 26, Throwing = 27, Evasion = 29, DivineMagic = 32, EnhancingMagic = 34, EnfeeblingMagic = 35,
            DarkMagic = 37, StringInstrument = 41, WindInstrument = 42, BlueMagic = 43, Handbell = 45, Fishing = 48,
            Woodworking = 49, Smithing = 50, Goldsmithing = 51, Clothcraft = 52, Leathercraft = 53, Bonecraft = 54, Alchemy = 55,
            Cooking = 56;
        public const int Max = 64; // MAX_SKILLTYPE
    }

    // CItemWeapon: the item_weapon row, with setSkillType's two-handed / ranged classification
    public sealed class WeaponInfo
    {
        public byte Skill;
        public sbyte SubSkill;
        public short IlvlSkill, IlvlParry, IlvlMacc;
        public uint DmgType, Delay, Damage; // Delay is the item_weapon delay (getBaseDelay); DelayMs is getDelay
        public byte Hit;
        public ushort UnlockPoints; // m_wsunlockpoints: the weapon skill points a wsnm / ksnm / nyzul weapon needs to break
        public EquipmentInfo Equip; // the item the weapon is; null for the unarmed weapons

        public int DelayMs => (ushort)Math.Min(Delay * 1000.0f / 60.0f, 65535.0f);

        // CItemWeapon::getDPS (itemutils::LoadItemList): the damage a minute over the delay, to the hundredth; a hand-to-hand
        // weapon's delay without the two fists' 240 and its damage with the base 3. None for a weapon of no damage or delay.
        public double Dps
        {
            get
            {
                int dmg = (int)Damage, delay = (int)Delay;
                if (!((dmg > 0 || HandToHand) && delay > 0)) return 0.0;
                if (HandToHand)
                {
                    delay -= 240;
                    dmg += 3;
                }
                return Math.Round(dmg * 60.0 / delay * 100, MidpointRounding.AwayFromZero) / 100;
            }
        }

        public bool TwoHanded => Skill is Zone.Skill.GreatSword or Zone.Skill.GreatAxe or Zone.Skill.Scythe or Zone.Skill.Polearm
            or Zone.Skill.GreatKatana or Zone.Skill.Staff;
        public bool Ranged => Skill is Zone.Skill.Archery or Zone.Skill.Marksmanship or Zone.Skill.Throwing;
        public bool HandToHand => Skill == Zone.Skill.HandToHand;
        // CItemWeapon::getTotalUnlockPointsNeeded of the item: its own m_wsunlockpoints (setWeaponskillPointsNeeded),
        // else the template's
        public ushort UnlockPointsOf(Item item) => item?.UnlockPointsNeeded ?? UnlockPoints;

        // CItemWeapon::isUnlockable, of the item's total
        public bool IsUnlockable(Item item) => Skill != Zone.Skill.None && UnlockPointsOf(item) > 0;

        // CItemWeapon::isUnlocked: the item's WeaponUnlock exdata holds the points so far, its total reached
        public bool IsUnlocked(Item item) =>
            item != null && IsUnlockable(item) && CurrentUnlockPoints(item) == UnlockPointsOf(item);

        public static ushort CurrentUnlockPoints(Item item) => (ushort)(item.Extra[0] | item.Extra[1] << 8);

        public bool Instrument => Skill is Zone.Skill.StringInstrument or Zone.Skill.WindInstrument or Zone.Skill.Handbell;

        // the weapon half of EquipmentInfo.CopyOwn
        internal WeaponInfo CopyFor(EquipmentInfo equip)
        {
            var w = (WeaponInfo)MemberwiseClone();
            w.Equip = equip;
            return w;
        }

        // xi::items::unarmed / unarmedH2H: the weapons m_Weapons[SLOT_MAIN] holds with nothing equipped; a new
        // CItemWeapon's delay is 8000 ms (base 480)
        public static readonly WeaponInfo Unarmed = new WeaponInfo { Skill = Zone.Skill.None, Damage = 3, Delay = 480 };
        public static readonly WeaponInfo UnarmedH2H = new WeaponInfo
        {
            Skill = Zone.Skill.HandToHand, Damage = 0, Delay = 480, DmgType = DamageType.HandToHand,
        };
    }

    // xi::DamageType
    public static class DamageType
    {
        public const uint None = 0, Piercing = 1, Slashing = 2, Blunt = 3, HandToHand = 4;
    }

    // CItemEquipment: the item_equipment row and its item_mods
    public sealed class EquipmentInfo
    {
        public ushort ItemId;
        public byte ReqLvl, ILvl, SuperiorLevel, ShieldSize;
        public byte ShieldAbsorption; // m_absorption: the share of a blocked hit the shield takes, from its DEF and size
        public uint Jobs;
        public ushort ModelId, ScriptType, EquipSlots, RemoveSlots, RemoveSlotLook;
        public List<(Mod Mod, short Value)> Mods { get; private set; } = new List<(Mod, short)>();
        // CItemEquipment::petModList: the item_mods_pet rows (and an augment's pet rows), which the wearer gives its pet
        public List<(Mod Mod, byte PetType, short Value)> PetMods { get; private set; } = new List<(Mod, byte, short)>();
        // CItemEquipment::latentList (itemLatent: ConditionsID, ConditionsValue, ModValue, ModPower): the item_latents rows
        public readonly List<(ushort LatentId, ushort LatentParam, Mod Mod, short Value)> Latents = new List<(ushort, ushort, Mod, short)>();
        public WeaponInfo Weapon; // null unless an item_weapon row exists
        public bool Charged;      // ITEM_CHARGED: the item_usable row has valid targets (an enchanted item)
        public uint UseDelay, ReuseDelay; // item_usable useDelay / reuseDelay, in seconds
        public byte MaxCharges;           // item_usable maxCharges

        public bool IsShield => ShieldSize > 0 && ShieldSize <= 6;

        // A copy of the template of its own, its weapon pointing back at it. C++ keeps a CItemEquipment per item, so
        // CItemEquipment::addModifier / addPetModifier change that item alone; the C# templates are shared, so an
        // augmented item keeps a copy of its own (Item.Equip, ItemData.SetAugmentMod) and an item-only latent changes
        // that one, or else the copy it puts in the char's slot (LatentEffect.Activate).
        internal bool OwnCopy { get; private set; }

        internal EquipmentInfo CopyOwn()
        {
            var e = (EquipmentInfo)MemberwiseClone();
            e.OwnCopy = true;
            e.Mods = new List<(Mod, short)>(Mods);
            e.PetMods = new List<(Mod, byte, short)>(PetMods);
            e.Weapon = Weapon?.CopyFor(e);
            return e;
        }

        // CItemEquipment::addModifier: a shield's DEF sets its absorption
        public void AddModifier(Mod mod, short value)
        {
            if (IsShield && mod == Mod.DEF)
            {
                int pdt = (byte)(value / 2) + ShieldSize switch { 1 => 22, 2 or 6 => 40, 3 => 50, 4 or 5 => 55, _ => 0 };
                ShieldAbsorption = Math.Min((byte)pdt, (byte)100);
            }
            Mods.Add((mod, value));
        }

        // CItemEquipment::getModifier: the total over every row of that mod
        public short GetModifier(Mod mod)
        {
            int total = 0;
            foreach (var m in Mods)
                if (m.Mod == mod) total += m.Value;
            return (short)total;
        }

        // isEquippableByRace: EQUIPMENT_ONLY_RACE is a race bit set, 0 for every race
        public bool EquippableByRace(int race)
        {
            int raceMod = GetModifier(Mod.EQUIPMENT_ONLY_RACE);
            return raceMod <= 0 || (raceMod & 1 << (race - 1)) != 0;
        }
    }

    // AugmentDataRow: one augments row of an augment id (SetAugmentMod reads the multiplier as a uint8)
    public readonly record struct AugmentRow(ushort Multiplier, Mod Mod, short Value, byte IsPet, byte PetType);

    // CItemFurnishing's template (item_furnishing); IsFlowerpot: a CItemFlowerpot (item_basic type ITEM_FLOWERPOT 9)
    public sealed class FurnishingInfo
    {
        public byte Storage, Element, Aura, Placement, SizeX, SizeY;
        public ushort Moghancement, Height;
        public bool IsFlowerpot;
    }

    // enums/furnishing_placement.h
    public static class FurnishingPlacement
    {
        public const byte Floor = 0, Surface = 1, Wall = 2, OnTable = 3;
    }

    // itemutils' equipment templates, read per item on first use rather than all at startup
    public sealed class ItemData
    {
        private readonly IDatabase db_;
        private readonly Dictionary<ushort, EquipmentInfo> equipment_ = new Dictionary<ushort, EquipmentInfo>();

        public ItemData(IDatabase db) { db_ = db; }

        private WeaponSkillTable weaponSkills_;

        // battleutils::LoadWeaponSkillsList, read on first use
        public WeaponSkillTable WeaponSkills => weaponSkills_ ??= WeaponSkillTable.Load(db_);

        private Dictionary<ushort, FurnishingInfo> furnishings_;

        // the item_furnishing rows, all read on first use; null when the item is not a furnishing
        public FurnishingInfo Furnishing(ushort itemId)
        {
            if (furnishings_ == null)
            {
                furnishings_ = new Dictionary<ushort, FurnishingInfo>();
                var r = db_.Execute("SELECT f.itemid, f.storage, f.moghancement, f.element, f.aura, f.placement, f.size_x, f.size_y, f.height, " +
                                    "b.type FROM item_furnishing f LEFT JOIN item_basic b ON b.itemid = f.itemid", new List<BoundValue>());
                while (r != null && r.Next())
                    furnishings_[r.Get<ushort>("itemid")] = new FurnishingInfo
                    {
                        Storage = r.Get<byte>("storage"), Moghancement = r.Get<ushort>("moghancement"), Element = r.Get<byte>("element"),
                        Aura = r.Get<byte>("aura"), Placement = r.Get<byte>("placement"), SizeX = r.Get<byte>("size_x"),
                        SizeY = r.Get<byte>("size_y"), Height = r.Get<ushort>("height"), IsFlowerpot = !r.IsNull("type") && r.Get<byte>("type") == 9,
                    };
            }
            return furnishings_.TryGetValue(itemId, out var f) ? f : null;
        }

        private Dictionary<ushort, List<AugmentRow>> augments_;

        // CItemEquipment::LoadAugmentData's sAugmentData: each augment id's augments rows, all read on first use
        public Dictionary<ushort, List<AugmentRow>> Augments
        {
            get
            {
                if (augments_ != null) return augments_;
                augments_ = new Dictionary<ushort, List<AugmentRow>>();
                var r = db_.Execute("SELECT augmentId, multiplier, modId, value, isPet, petType FROM augments", new List<BoundValue>());
                while (r != null && r.Next())
                {
                    ushort id = r.Get<ushort>("augmentId");
                    if (!augments_.TryGetValue(id, out var rows)) augments_[id] = rows = new List<AugmentRow>();
                    rows.Add(new AugmentRow((ushort)r.Get<short>("multiplier"), (Mod)r.Get<ushort>("modId"), r.Get<short>("value"),
                                            r.Get<byte>("isPet"), r.Get<byte>("petType")));
                }
                return augments_;
            }
        }

        // ShowErrorFmt's place for the augment refusals
        public Action<string> Log;

        // the CItemEquipment an item is: its own copy once an augment added to it, else the template
        public EquipmentInfo EquipmentOf(Item item) => item.Equip ?? Equipment(item.ItemId);

        // charutils::LoadInventory's augment pass: a non-charged equipment applies each of its first four augments that
        // is set (getAugment, the raw 16 bits)
        public void ApplyAugments(Item item)
        {
            if (Equipment(item.ItemId) is not { Charged: false }) return;
            for (int slot = 0; slot < 4; slot++)
                if (BitConverter.ToUInt16(item.Extra, 2 + slot * 2) != 0) ApplyAugment(item, slot);
        }

        // CItemEquipment::ApplyAugment: Exdata::AugmentStandard's Augments[slot] (Id 11 bits, Value 5 bits)
        public void ApplyAugment(Item item, int slot)
        {
            ushort augment = BitConverter.ToUInt16(item.Extra, 2 + slot * 2);
            SetAugmentMod(item, (ushort)(augment & 0x7FF), (byte)(augment >> 11));
        }

        // CItemEquipment::SetAugmentMod: an id the augments table lacks is refused; any other but 0 makes the item
        // ITEM_AUGMENTED with HasAugments / Standard in its exdata. Each row adds its value raised by the augment's
        // (away from zero) times the multiplier (at least 1) to the item's own mods, or its pet mods; Mod NONE rows are
        // skipped.
        public void SetAugmentMod(Item item, ushort type, byte value)
        {
            if (!Augments.TryGetValue(type, out var rows))
            {
                Log?.Invoke($"Invalid augment type {type} requested for item {item.ItemId}");
                return;
            }
            if (type != 0)
            {
                item.Augmented = true;
                item.Extra[0] |= 0x02; // AugmentKindFlags::HasAugments
                item.Extra[1] |= 0x03; // AugmentSubKindFlags::Standard
            }
            foreach (var row in rows)
            {
                short modValue = (short)((row.Value > 0 ? row.Value + value : row.Value - value) * Math.Max((byte)row.Multiplier, (byte)1));
                if (row.Mod == Mod.NONE) continue;
                var own = item.Equip ??= Equipment(item.ItemId)?.CopyOwn();
                if (own == null) return;
                if (row.IsPet == 0) own.AddModifier(row.Mod, modValue);
                else own.PetMods.Add((row.Mod, row.PetType, modValue));
            }
        }

        private ResultSet Query(string sql, ushort itemId) => db_.Execute(sql, new List<BoundValue> { BoundValue.Of(itemId) });

        // null when the item is not equipment
        public EquipmentInfo Equipment(ushort itemId)
        {
            if (equipment_.TryGetValue(itemId, out var e)) return e;
            var r = Query("SELECT level, ilevel, jobs, MId, shieldSize, scriptType, slot, rslot, rslotlook, su_level FROM item_equipment WHERE itemId = ?", itemId);
            if (r != null && r.Next())
            {
                e = new EquipmentInfo
                {
                    ItemId = itemId, ReqLvl = r.Get<byte>("level"), ILvl = r.Get<byte>("ilevel"), Jobs = r.Get<uint>("jobs"),
                    ModelId = r.Get<ushort>("MId"), ShieldSize = r.Get<byte>("shieldSize"), ScriptType = r.Get<ushort>("scriptType"),
                    EquipSlots = r.Get<ushort>("slot"), RemoveSlots = r.Get<ushort>("rslot"), RemoveSlotLook = r.Get<ushort>("rslotlook"),
                    SuperiorLevel = r.Get<byte>("su_level"),
                };
                r = Query("SELECT skill, subskill, ilvl_skill, ilvl_parry, ilvl_macc, dmgType, hit, delay, dmg, unlock_points FROM item_weapon WHERE itemId = ?", itemId);
                if (r != null && r.Next())
                {
                    e.Weapon = new WeaponInfo
                    {
                        Skill = r.Get<byte>("skill"), SubSkill = r.Get<sbyte>("subskill"), IlvlSkill = r.Get<short>("ilvl_skill"),
                        IlvlParry = r.Get<short>("ilvl_parry"), IlvlMacc = r.Get<short>("ilvl_macc"), DmgType = r.Get<uint>("dmgType"),
                        Hit = r.Get<byte>("hit"), Delay = r.Get<uint>("delay"), Damage = r.Get<uint>("dmg"), Equip = e,
                        UnlockPoints = (ushort)r.Get<short>("unlock_points"),
                    };
                }
                r = Query("SELECT validTargets, useDelay, reuseDelay, maxCharges FROM item_usable WHERE itemid = ?", itemId);
                if (r != null && r.Next())
                {
                    e.Charged = r.Get<ushort>("validTargets") != 0;
                    e.UseDelay = r.Get<uint>("useDelay");
                    e.ReuseDelay = r.Get<uint>("reuseDelay");
                    e.MaxCharges = r.Get<byte>("maxCharges");
                }
                r = Query("SELECT modId, value FROM item_mods WHERE itemId = ?", itemId);
                while (r != null && r.Next())
                    e.AddModifier((Mod)r.Get<ushort>("modId"), r.Get<short>("value"));
                // itemutils::LoadItemList's item_mods_pet pass: CItemEquipment::addPetModifier
                r = Query("SELECT modId, value, petType FROM item_mods_pet WHERE itemId = ?", itemId);
                while (r != null && r.Next())
                    e.PetMods.Add(((Mod)r.Get<ushort>("modId"), r.Get<byte>("petType"), r.Get<short>("value")));
                // itemutils::LoadItemList's item_latents pass: CItemEquipment::addLatent
                r = Query("SELECT modId, value, latentId, latentParam FROM item_latents WHERE itemId = ?", itemId);
                while (r != null && r.Next())
                    e.Latents.Add(((ushort)r.Get<short>("latentId"), (ushort)r.Get<short>("latentParam"), (Mod)r.Get<ushort>("modId"),
                                   r.Get<short>("value")));
            }
            equipment_[itemId] = e;
            return e;
        }
    }

    // battleutils' g_SkillTable / g_SkillRanks and charutils' g_ExpPerLevel
    public sealed class GrowthTables
    {
        private static readonly string[] JobColumns =
            { "war", "mnk", "whm", "blm", "rdm", "thf", "pld", "drk", "bst", "brd", "rng", "sam", "nin", "drg", "smn", "blu", "cor", "pup", "dnc", "sch", "geo", "run" };

        private readonly ushort[,] skillCaps_ = new ushort[100, 14];
        private readonly byte[,] skillRanks_ = new byte[Skill.Max, CharData.MaxJobs];
        private readonly uint[] expPerLevel_ = new uint[100];

        public static GrowthTables Load(IDatabase db)
        {
            var t = new GrowthTables();
            var none = new List<BoundValue>();
            var r = db.Execute("SELECT r0,r1,r2,r3,r4,r5,r6,r7,r8,r9,r10,r11,r12,r13 FROM skill_caps ORDER BY level LIMIT 100", none);
            for (int x = 0; r != null && r.Next() && x < 100; x++)
                for (int y = 0; y < 14; y++)
                    t.skillCaps_[x, y] = r.Get<ushort>("r" + y);

            // the MON column is never read: monstrosity reuses other jobs' ranks
            r = db.Execute("SELECT skillid," + string.Join(",", JobColumns) + " FROM skill_ranks LIMIT 64", none);
            while (r != null && r.Next())
            {
                int skill = Math.Clamp((int)r.Get<byte>("skillid"), 0, Skill.Max - 1);
                for (int j = 0; j < JobColumns.Length; j++)
                    t.skillRanks_[skill, j + 1] = (byte)Math.Clamp((int)r.Get<byte>(JobColumns[j]), 0, 11);
            }

            t.LoadExpPerLevel(db);
            return t;
        }

        // charutils::LoadExpTable's exp_base part: the experience to the next level, each level the table has
        // (ReloadExperienceData reads it again)
        public void LoadExpPerLevel(IDatabase db)
        {
            var r = db.Execute("SELECT level, exp FROM exp_base LIMIT 100", new List<BoundValue>());
            while (r != null && r.Next())
            {
                int level = r.Get<byte>("level") - 1;
                if (level >= 0 && level < 100) expPerLevel_[level] = r.Get<ushort>("exp");
            }
        }

        public byte GetSkillRank(int skill, int job) => skillRanks_[skill, job];

        // battleutils::GetMaxSkill(SkillType, Job, level) and (rank, level)
        public ushort GetMaxSkill(int skill, int job, int level) => GetMaxSkillByRank(skillRanks_[skill, job], Math.Min(level, 99));
        public ushort GetMaxSkillByRank(int rank, int level) => skillCaps_[Math.Clamp(level, 0, 99), rank];

        // charutils::GetExpNEXTLevel
        public uint GetExpNextLevel(int level) => level > 0 && level < 100 ? expPerLevel_[level] : 0;
    }

    // the settings the stat formulas read, at their settings/default values unless given
    public sealed class StatSettings
    {
        public float TwoHandedStrAttack = 1.0f, HandToHandStrAttack = 1.0f, OneHandMainStrAttack = 0.75f,
            OneHandOffStrAttack = 0.5f, RangedStrAttack = 1.0f, VitDefMultiplier = 1.5f;
        public float TwoHandedDexAccuracy = 0.75f, HandToHandDexAccuracy = 0.75f, OneHandMainDexAccuracy = 0.75f,
            OneHandOffDexAccuracy = 0.75f, RangedAgiAccuracy = 0.75f;
        public bool DisableGearScaling;
        public bool UseOldCounterstance; // main.USE_OLD_COUNTERSTANCE: Counterstance DEF = 1 + VIT / 2 (+ Minne)
        public bool LevelSyncEnable = true; // map.LEVEL_SYNC_ENABLE
        public bool EquipFromOtherContainers; // the satchel, sack and case (client addons only)
        public int MaxMeritPoints = 30;
        public int MaxFakeEntries = 15; // the fishing contest's placeholder fishers
        public bool MogHouse2F = true;
        public bool MogGarden = true;                // main.ENABLE_MOG_GARDEN: off, the exit to it goes to the area entered from
        public bool EnableMonstrosity = true;        // main.ENABLE_MONSTROSITY
        public float AnimationSpeedDivisor = 1.0f;   // map.ANIMATION_SPEED_DIVISOR
        // map.GARDEN_*_MATTERS: the planting day's element, the moon phase, the pot and the mog house's auras add to a harvest
        public bool GardenDayMatters, GardenMoonphaseMatters, GardenPotMatters, GardenMhAuraMatters;
        public bool ItemRecycleBin = true;
        public bool LeakExtDataOnItemMove = true; // an emptied slot's ITEM_ATTR keeps the moved item's exdata, as retail
        public float DropRateMultiplier = 1f; // map.DROP_RATE_MULTIPLIER
        // map.MOB_GIL_MULTIPLIER, map.ALL_MOBS_GIL_BONUS (gil per mob level for every mob) capped at map.MAX_GIL_BONUS
        public float MobGilMultiplier = 1f;
        public byte AllMobsGilBonus;
        public uint MaxGilBonus = 9999;
        public byte LightluggageBlock = 4; // sort packets within a second of the last sort before the char is logged out
        public bool AuditPlayerVendor;     // log NPC shop purchases and sales to audit_vendor
        public bool AuditPlayerTrades;     // log player and NPC trades to audit_trade
        public bool AuditPlayerDbox;       // log delivery box receipts to audit_dbox
        public int DeliveryBoxMaxInflight = 128; // queued incoming delivery box items a char may have
        // the auction house fee: a base plus a percentage of the price, for a single item or a stack, capped
        public uint AhBaseFeeSingle = 1, AhBaseFeeStacks = 4, AhMaxFee = 10000;
        public float AhTaxRateSingle = 1.0f, AhTaxRateStacks = 0.5f;
        public byte AhListLimit = 7; // open listings a char may have (0 no limit)
        public bool AuditPlayerBazaar;     // log bazaar purchases to audit_bazaar
        public bool DebugBazaars;          // logging.DEBUG_BAZAARS: the bazaar interactions' debug lines
        public double CraftChanceMultiplier = 1.0, CraftHqChanceMultiplier = 1.0;
        public bool CraftModernSystem = true;
        public int CraftCommonCap = 700, CraftSpecializationPoints = 400;
        // chat: map.AUDIT_CHAT gates the per-channel AUDIT_<TYPE>s; AUDIT_GM_CMD is the lowest command permission
        // logged to audit_gm (0 none)
        public bool AuditChat, AuditSay, AuditShout, AuditTell, AuditYell, AuditLinkshell, AuditUnity, AuditParty;
        public bool AuditBallista, AuditAssistE, AuditAssistJ;
        public int AuditGmCmd;
        public int YellCooldown = 30;
        public bool BlockTellToHiddenGm;
        public bool AssistChannelEnabled = true;
        // main.ASSIST_CHANNEL_MEMBERSHIP_LENGTH (playtime hours), main.ASSIST_CHANNEL_RETURNEE_LOGIN_GAP (days)
        public int AssistChannelMembershipLength = 240, AssistChannelReturneeLoginGap = 48;
        public int ZonePlayerCap = 700, ZonePlayerGmReserved = 5;
        public bool AllJobsWidescan = true;
        public double ExpRate = 1.0, CapacityRate = 1.0; // map.EXP_RATE (combat and capacity points), map.CAPACITY_RATE
        public int WsPointsBase = 5, WsPointsSkillchain = 2; // map.WS_POINTS_BASE, map.WS_POINTS_SKILLCHAIN
        public int SpeedLimit = 80, MountSpeed = 80; // map.SPEED_LIMIT, map.MOUNT_SPEED
        public bool PreventUnengagedWs;                    // map.PREVENT_UNENGAGED_WS
        public double AbilityRecastMultiplier = 1.0;       // map.ABILITY_RECAST_MULTIPLIER
        public bool BloodPactSharedTimer;                  // map.BLOOD_PACT_SHARED_TIMER
        public bool DisableTreasureHunterProcs;            // map.DISABLE_TREASURE_HUNTER_PROCS
        public bool KeepJugpetThroughZoning;               // map.KEEP_JUGPET_THROUGH_ZONING
        public int SubjobRatio = 1;                      // map.SUBJOB_RATIO: 0 none, 1 half, 2 two thirds, 3 equal
        // a death's EXP loss: from map.EXP_LOSS_LEVEL, less map.EXP_RETAIN, times map.EXP_LOSS_RATE; the pre-Abyssea
        // tiers (map.USE_PRE_ABYSSEA_EXP_LOSS_TIERS) take 10% up to level 24
        public byte ExpLossLevel = 31, ExpRetain;
        public float ExpLossRate = 1f;
        public bool UsePreAbysseaExpLossTiers;
        public bool SelfUnstuckEnabled;                  // map.SELF_UNSTUCK_ENABLED, map.SELF_UNSTUCK_COOLDOWN (seconds)
        public int SelfUnstuckCooldown = 86400;
        public int MinimumLevelConquestInfluenceLoss = 6; // map.MINIMUM_LEVEL_CONQUEST_INFUENCE_LOSS
        public bool FishingEnable;                        // map.FISHING_ENABLE, map.FISHING_MIN_LEVEL, map.FISHING_SKILL_MULTIPLIER
        public int FishingMinLevel = 1;
        public double FishingSkillMultiplier = 1.0;
        public float PlayerTpMultiplier = 1.0f;           // map.PLAYER_TP_MULTIPLIER
        public double SkillupChanceMultiplier = 1.0;      // map.SKILLUP_CHANCE_MULTIPLIER, map.SKILLUP_AMOUNT_MULTIPLIER
        public int SkillupAmountMultiplier = 1;
        public float MobTpMultiplier = 1.0f;              // map.MOB_TP_MULTIPLIER
        public float PetTpMultiplier = 1.0f;              // map.PET_TP_MULTIPLIER
        public float TrustTpMultiplier = 1.0f;            // map.TRUST_TP_MULTIPLIER
        public bool HideReadiesTarget;                    // map.HIDE_READIES_TARGET
        public float SpellRecastReductionCap = 80f;       // map.SPELL_RECAST_REDUCTION_CAP (%)
        public int EnmityCap = 30000;                     // map.ENMITY_CAP
        public int MobAdditionalTimeToDeaggro;            // map.MOB_ADDITIONAL_TIME_TO_DEAGGRO (s)
        public float DelayReductionCap = 0.80f;           // main.DELAY_REDUCTION_CAP
        public bool UseAdoulinWeaponSkillChanges = true;  // main.USE_ADOULIN_WEAPON_SKILL_CHANGES
        public bool EnableSmnMagicCastTimeMerit = true;   // main.ENABLE_SMN_MAGIC_CAST_TIME_MERIT
        public int HealingTickDelay = 10;                 // map.HEALING_TICK_DELAY (seconds)
        public int RangedAttackFreePhaseDelay = 500;      // main.RANGED_ATTACK_FREE_PHASE_DELAY (ms)
        public float MobRunSpeedMultiplier = 2.5f;        // map.MOB_RUN_SPEED_MULTIPLIER
        public bool MobNoDespawn;                         // map.MOB_NO_DESPAWN
        // map.ALTER_EGO_HP / MP / STAT / SKILL_MULTIPLIER, main.ENABLE_TRUST_ALTER_EGO_EXPO
        public float AlterEgoHpMultiplier = 1.0f, AlterEgoMpMultiplier = 1.0f, AlterEgoStatMultiplier = 1.0f, AlterEgoSkillMultiplier = 1.0f;
        public int TrustAlterEgoExpo;
        // map.BATTLE_CAP_TWEAK, map.LV_CAP_MISSION_BCNM, main.MAX_LEVEL: a battlefield's level cap
        public int BattleCapTweak;
        public bool LvCapMissionBcnm;
        public int MaxLevel = 99;
        // main.NORMAL_MOB_MAX_LEVEL_RANGE_MIN / MAX: a server may flatten normal mob levels (0 is uncapped)
        public int NormalMobMaxLevelRangeMin, NormalMobMaxLevelRangeMax;

        public static StatSettings From(XI.Common.Settings s)
        {
            var d = new StatSettings();
            if (s == null) return d;
            float F(string key, float fallback) => (float)s.GetDouble(key, fallback);
            d.TwoHandedStrAttack = F("main.TWO_HANDED_STR_ATTACK_MULTIPLIER", d.TwoHandedStrAttack);
            d.HandToHandStrAttack = F("main.HAND_TO_HAND_STR_ATTACK_MULTIPLIER", d.HandToHandStrAttack);
            d.OneHandMainStrAttack = F("main.ONE_HAND_MAIN_HAND_STR_ATTACK_MULTIPLIER", d.OneHandMainStrAttack);
            d.OneHandOffStrAttack = F("main.ONE_HAND_OFF_HAND_STR_ATTACK_MULTIPLIER", d.OneHandOffStrAttack);
            d.RangedStrAttack = F("main.RANGED_STR_ATTACK_MULTIPLIER", d.RangedStrAttack);
            d.VitDefMultiplier = F("main.PLAYER_ALLIES_VIT_DEF_MULTIPLIER", d.VitDefMultiplier);
            d.TwoHandedDexAccuracy = F("main.TWO_HANDED_DEX_ACCURACY_MULTIPLIER", d.TwoHandedDexAccuracy);
            d.HandToHandDexAccuracy = F("main.HAND_TO_HAND_DEX_ACCURACY_MULTIPLIER", d.HandToHandDexAccuracy);
            d.OneHandMainDexAccuracy = F("main.ONE_HAND_MAIN_HAND_DEX_ACCURACY_MULTIPLIER", d.OneHandMainDexAccuracy);
            d.OneHandOffDexAccuracy = F("main.ONE_HAND_OFF_HAND_DEX_ACCURACY_MULTIPLIER", d.OneHandOffDexAccuracy);
            d.RangedAgiAccuracy = F("main.RANGED_AGI_ACCURACY_MULTIPLIER", d.RangedAgiAccuracy);
            d.DisableGearScaling = s.GetBool("map.DISABLE_GEAR_SCALING", d.DisableGearScaling);
            d.UseOldCounterstance = s.GetBool("main.USE_OLD_COUNTERSTANCE", d.UseOldCounterstance);
            d.LevelSyncEnable = s.GetBool("map.LEVEL_SYNC_ENABLE", d.LevelSyncEnable);
            d.EquipFromOtherContainers = s.GetBool("main.EQUIP_FROM_OTHER_CONTAINERS", d.EquipFromOtherContainers);
            d.MaxMeritPoints = s.GetInt("map.MAX_MERIT_POINTS", d.MaxMeritPoints);
            d.MaxFakeEntries = s.GetInt("main.MAX_FAKE_ENTRIES", d.MaxFakeEntries);
            // settings::get<bool> takes a number as number != 0
            d.MogHouse2F = s.GetBool("main.ENABLE_MOG_HOUSE_2F", s.GetDouble("main.ENABLE_MOG_HOUSE_2F", 1) != 0);
            d.MogGarden = s.GetBool("main.ENABLE_MOG_GARDEN", s.GetDouble("main.ENABLE_MOG_GARDEN", 1) != 0);
            d.EnableMonstrosity = s.GetBool("main.ENABLE_MONSTROSITY", s.GetDouble("main.ENABLE_MONSTROSITY", 1) != 0);
            d.AnimationSpeedDivisor = F("map.ANIMATION_SPEED_DIVISOR", d.AnimationSpeedDivisor);
            d.ItemRecycleBin = s.GetBool("map.ENABLE_ITEM_RECYCLE_BIN", d.ItemRecycleBin);
            d.GardenDayMatters = s.GetBool("map.GARDEN_DAY_MATTERS", d.GardenDayMatters);
            d.GardenMoonphaseMatters = s.GetBool("map.GARDEN_MOONPHASE_MATTERS", d.GardenMoonphaseMatters);
            d.GardenPotMatters = s.GetBool("map.GARDEN_POT_MATTERS", d.GardenPotMatters);
            d.GardenMhAuraMatters = s.GetBool("map.GARDEN_MH_AURA_MATTERS", d.GardenMhAuraMatters);
            d.LeakExtDataOnItemMove = s.GetBool("map.LEAK_EXT_DATA_ON_ITEM_MOVE", d.LeakExtDataOnItemMove);
            d.DropRateMultiplier = (float)s.GetDouble("map.DROP_RATE_MULTIPLIER", d.DropRateMultiplier);
            d.MobGilMultiplier = (float)s.GetDouble("map.MOB_GIL_MULTIPLIER", d.MobGilMultiplier);
            d.AllMobsGilBonus = (byte)s.GetInt("map.ALL_MOBS_GIL_BONUS", d.AllMobsGilBonus);
            d.MaxGilBonus = (uint)s.GetInt("map.MAX_GIL_BONUS", (int)d.MaxGilBonus);
            d.LightluggageBlock = (byte)s.GetInt("map.LIGHTLUGGAGE_BLOCK", d.LightluggageBlock);
            d.AuditPlayerVendor = s.GetBool("map.AUDIT_PLAYER_VENDOR", d.AuditPlayerVendor);
            d.AuditPlayerTrades = s.GetBool("map.AUDIT_PLAYER_TRADES", d.AuditPlayerTrades);
            d.AuditPlayerDbox = s.GetBool("map.AUDIT_PLAYER_DBOX", d.AuditPlayerDbox);
            d.DeliveryBoxMaxInflight = s.GetInt("map.DELIVERY_BOX_MAX_INFLIGHT", d.DeliveryBoxMaxInflight);
            d.AhBaseFeeSingle = (uint)s.GetInt("map.AH_BASE_FEE_SINGLE", (int)d.AhBaseFeeSingle);
            d.AhBaseFeeStacks = (uint)s.GetInt("map.AH_BASE_FEE_STACKS", (int)d.AhBaseFeeStacks);
            d.AhTaxRateSingle = F("map.AH_TAX_RATE_SINGLE", d.AhTaxRateSingle);
            d.AhTaxRateStacks = F("map.AH_TAX_RATE_STACKS", d.AhTaxRateStacks);
            d.AhMaxFee = (uint)s.GetInt("map.AH_MAX_FEE", (int)d.AhMaxFee);
            d.AhListLimit = (byte)s.GetInt("map.AH_LIST_LIMIT", d.AhListLimit);
            d.AuditPlayerBazaar = s.GetBool("map.AUDIT_PLAYER_BAZAAR", d.AuditPlayerBazaar);
            d.DebugBazaars = s.GetBool("logging.DEBUG_BAZAARS", d.DebugBazaars);
            d.CraftChanceMultiplier = s.GetDouble("map.CRAFT_CHANCE_MULTIPLIER", d.CraftChanceMultiplier);
            d.CraftHqChanceMultiplier = s.GetDouble("map.CRAFT_HQ_CHANCE_MULTIPLIER", d.CraftHqChanceMultiplier);
            d.CraftModernSystem = s.GetBool("map.CRAFT_MODERN_SYSTEM", d.CraftModernSystem);
            d.CraftCommonCap = s.GetInt("map.CRAFT_COMMON_CAP", d.CraftCommonCap);
            d.CraftSpecializationPoints = s.GetInt("map.CRAFT_SPECIALIZATION_POINTS", d.CraftSpecializationPoints);
            d.AuditChat = s.GetBool("map.AUDIT_CHAT", d.AuditChat);
            d.AuditSay = s.GetBool("map.AUDIT_SAY", d.AuditSay);
            d.AuditShout = s.GetBool("map.AUDIT_SHOUT", d.AuditShout);
            d.AuditTell = s.GetBool("map.AUDIT_TELL", d.AuditTell);
            d.AuditYell = s.GetBool("map.AUDIT_YELL", d.AuditYell);
            d.AuditLinkshell = s.GetBool("map.AUDIT_LINKSHELL", d.AuditLinkshell);
            d.AuditUnity = s.GetBool("map.AUDIT_UNITY", d.AuditUnity);
            d.AuditParty = s.GetBool("map.AUDIT_PARTY", d.AuditParty);
            d.AuditBallista = s.GetBool("map.AUDIT_BALLISTA", d.AuditBallista);
            d.AuditAssistE = s.GetBool("map.AUDIT_ASSISTE", d.AuditAssistE);
            d.AuditAssistJ = s.GetBool("map.AUDIT_ASSISTJ", d.AuditAssistJ);
            // settings::get<uint8> of a bool is 0 or 1
            d.AuditGmCmd = s.GetInt("map.AUDIT_GM_CMD", s.GetBool("map.AUDIT_GM_CMD") ? 1 : 0);
            d.YellCooldown = s.GetInt("map.YELL_COOLDOWN", d.YellCooldown);
            d.BlockTellToHiddenGm = s.GetBool("map.BLOCK_TELL_TO_HIDDEN_GM", d.BlockTellToHiddenGm);
            d.AssistChannelEnabled = s.GetBool("main.ASSIST_CHANNEL_ENABLED", d.AssistChannelEnabled);
            d.AssistChannelMembershipLength = s.GetInt("main.ASSIST_CHANNEL_MEMBERSHIP_LENGTH", d.AssistChannelMembershipLength);
            d.AssistChannelReturneeLoginGap = s.GetInt("main.ASSIST_CHANNEL_RETURNEE_LOGIN_GAP", d.AssistChannelReturneeLoginGap);
            d.ZonePlayerCap = s.GetInt("map.ZONE_PLAYER_CAP", d.ZonePlayerCap);
            d.ZonePlayerGmReserved = s.GetInt("map.ZONE_PLAYER_GM_RESERVED", d.ZonePlayerGmReserved);
            d.AllJobsWidescan = s.GetBool("map.ALL_JOBS_WIDESCAN", d.AllJobsWidescan);
            d.ExpRate = s.GetDouble("map.EXP_RATE", d.ExpRate);
            d.CapacityRate = s.GetDouble("map.CAPACITY_RATE", d.CapacityRate);
            d.SubjobRatio = s.GetInt("map.SUBJOB_RATIO", d.SubjobRatio);
            d.WsPointsBase = s.GetInt("map.WS_POINTS_BASE", d.WsPointsBase);
            d.WsPointsSkillchain = s.GetInt("map.WS_POINTS_SKILLCHAIN", d.WsPointsSkillchain);
            d.SpeedLimit = s.GetInt("map.SPEED_LIMIT", d.SpeedLimit);
            d.MountSpeed = s.GetInt("map.MOUNT_SPEED", d.MountSpeed);
            d.PreventUnengagedWs = s.GetBool("map.PREVENT_UNENGAGED_WS", d.PreventUnengagedWs);
            d.AbilityRecastMultiplier = s.GetDouble("map.ABILITY_RECAST_MULTIPLIER", d.AbilityRecastMultiplier);
            d.BloodPactSharedTimer = s.GetBool("map.BLOOD_PACT_SHARED_TIMER", d.BloodPactSharedTimer);
            d.DisableTreasureHunterProcs = s.GetBool("map.DISABLE_TREASURE_HUNTER_PROCS", d.DisableTreasureHunterProcs);
            d.KeepJugpetThroughZoning = s.GetBool("map.KEEP_JUGPET_THROUGH_ZONING", d.KeepJugpetThroughZoning);
            d.ExpLossLevel = (byte)s.GetInt("map.EXP_LOSS_LEVEL", d.ExpLossLevel);
            d.ExpRetain = (byte)s.GetInt("map.EXP_RETAIN", d.ExpRetain);
            d.ExpLossRate = (float)s.GetDouble("map.EXP_LOSS_RATE", d.ExpLossRate);
            d.UsePreAbysseaExpLossTiers = s.GetBool("map.USE_PRE_ABYSSEA_EXP_LOSS_TIERS", d.UsePreAbysseaExpLossTiers);
            d.SelfUnstuckEnabled = s.GetBool("map.SELF_UNSTUCK_ENABLED", d.SelfUnstuckEnabled);
            d.SelfUnstuckCooldown = s.GetInt("map.SELF_UNSTUCK_COOLDOWN", d.SelfUnstuckCooldown);
            d.MinimumLevelConquestInfluenceLoss = s.GetInt("map.MINIMUM_LEVEL_CONQUEST_INFUENCE_LOSS", d.MinimumLevelConquestInfluenceLoss);
            d.FishingEnable = s.GetBool("map.FISHING_ENABLE", d.FishingEnable);
            d.FishingMinLevel = s.GetInt("map.FISHING_MIN_LEVEL", d.FishingMinLevel);
            d.FishingSkillMultiplier = s.GetDouble("map.FISHING_SKILL_MULTIPLIER", d.FishingSkillMultiplier);
            d.PlayerTpMultiplier = F("map.PLAYER_TP_MULTIPLIER", d.PlayerTpMultiplier);
            d.SkillupChanceMultiplier = s.GetDouble("map.SKILLUP_CHANCE_MULTIPLIER", d.SkillupChanceMultiplier);
            d.SkillupAmountMultiplier = s.GetInt("map.SKILLUP_AMOUNT_MULTIPLIER", d.SkillupAmountMultiplier);
            d.MobTpMultiplier = F("map.MOB_TP_MULTIPLIER", d.MobTpMultiplier);
            d.PetTpMultiplier = F("map.PET_TP_MULTIPLIER", d.PetTpMultiplier);
            d.TrustTpMultiplier = F("map.TRUST_TP_MULTIPLIER", d.TrustTpMultiplier);
            d.HideReadiesTarget = s.GetBool("map.HIDE_READIES_TARGET", d.HideReadiesTarget);
            d.SpellRecastReductionCap = F("map.SPELL_RECAST_REDUCTION_CAP", d.SpellRecastReductionCap);
            d.EnmityCap = (int)F("map.ENMITY_CAP", d.EnmityCap);
            d.MobAdditionalTimeToDeaggro = (int)F("map.MOB_ADDITIONAL_TIME_TO_DEAGGRO", d.MobAdditionalTimeToDeaggro);
            d.DelayReductionCap = F("main.DELAY_REDUCTION_CAP", d.DelayReductionCap);
            d.UseAdoulinWeaponSkillChanges = s.GetBool("main.USE_ADOULIN_WEAPON_SKILL_CHANGES", d.UseAdoulinWeaponSkillChanges);
            d.EnableSmnMagicCastTimeMerit = s.GetBool("main.ENABLE_SMN_MAGIC_CAST_TIME_MERIT", d.EnableSmnMagicCastTimeMerit);
            d.HealingTickDelay = s.GetInt("map.HEALING_TICK_DELAY", d.HealingTickDelay);
            d.RangedAttackFreePhaseDelay = s.GetInt("main.RANGED_ATTACK_FREE_PHASE_DELAY", d.RangedAttackFreePhaseDelay);
            d.MobRunSpeedMultiplier = F("map.MOB_RUN_SPEED_MULTIPLIER", d.MobRunSpeedMultiplier);
            d.MobNoDespawn = s.GetBool("map.MOB_NO_DESPAWN", d.MobNoDespawn);
            d.AlterEgoHpMultiplier = F("map.ALTER_EGO_HP_MULTIPLIER", d.AlterEgoHpMultiplier);
            d.AlterEgoMpMultiplier = F("map.ALTER_EGO_MP_MULTIPLIER", d.AlterEgoMpMultiplier);
            d.AlterEgoStatMultiplier = F("map.ALTER_EGO_STAT_MULTIPLIER", d.AlterEgoStatMultiplier);
            d.AlterEgoSkillMultiplier = F("map.ALTER_EGO_SKILL_MULTIPLIER", d.AlterEgoSkillMultiplier);
            d.TrustAlterEgoExpo = s.GetInt("main.ENABLE_TRUST_ALTER_EGO_EXPO", d.TrustAlterEgoExpo);
            d.BattleCapTweak = s.GetInt("map.BATTLE_CAP_TWEAK", d.BattleCapTweak);
            d.LvCapMissionBcnm = s.GetBool("map.LV_CAP_MISSION_BCNM", d.LvCapMissionBcnm);
            d.MaxLevel = s.GetInt("main.MAX_LEVEL", d.MaxLevel);
            d.NormalMobMaxLevelRangeMin = s.GetInt("main.NORMAL_MOB_MAX_LEVEL_RANGE_MIN", d.NormalMobMaxLevelRangeMin);
            d.NormalMobMaxLevelRangeMax = s.GetInt("main.NORMAL_MOB_MAX_LEVEL_RANGE_MAX", d.NormalMobMaxLevelRangeMax);
            return d;
        }
    }
}
