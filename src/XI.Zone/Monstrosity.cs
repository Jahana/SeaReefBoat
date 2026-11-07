using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using XI.Map;
using XI.Persistence;

namespace XI.Zone
{
    // monstrosity.h / monstrosity.cpp: a character becomes a Monipulator by setting its job to MON and zoning. On
    // zone-in a MON character gets Monstrosity, loaded from char_monstrosity or created with the three starter families
    // and the race I instincts; it is null whenever the job is not MON. This file holds the dataset
    // (data/monstrosity.yaml), the per-char data, char_monstrosity and the pure rules; ZoneHost.Monstrosity.cs the
    // hooks, packets and bindings that need the zone.

    // xi::data::MonstrosityTpSkill: dat_skill_id is what the client sends, mob_skill_id is what runs
    public sealed class MonstrosityTpSkill
    {
        public ushort DatSkillId, MobSkillId, TpCost;
        public byte UnlockLevel;
        public long? ReadyTimeMs;
        public bool Enabled;
    }

    public sealed class MonstrosityInstinct
    {
        public byte Cost;
        public readonly Dictionary<Mod, short> Mods = new Dictionary<Mod, short>();
    }

    public sealed class MonstrositySpecies
    {
        public byte MonstrosityId, MainJob, SubJob, HpMainJob, HpSubJob, Size, Ecosystem;
        public ushort Look, HpScale;
        public ushort? MobSpecies, StanceSpecies;
        public byte[] StatRanks;    // STR..CHR, the yaml's own or (resolved at load) the mob species'; null for neither
        public byte? Speed, AnimationSpeed, Hitbox;
        public readonly Dictionary<Mod, short> Mods = new Dictionary<Mod, short>();
        public readonly List<MonstrosityTpSkill> TpSkills = new List<MonstrosityTpSkill>();
    }

    // one slot of Teyrnon's shop, or a level unlock: exactly one of species and variant
    public sealed class MonstrosityUnlock
    {
        public byte? Species, Variant;
        public ushort Infamy;
        public readonly List<(byte Family, byte Level)> Requirements = new List<(byte, byte)>();
    }

    // xi::data::Monstrosity
    public sealed class MonstrosityTable
    {
        // keyed by species code, which is what the client and char_monstrosity both use
        public readonly Dictionary<ushort, MonstrositySpecies> Species = new Dictionary<ushort, MonstrositySpecies>();
        public readonly Dictionary<ushort, MonstrosityInstinct> Instincts = new Dictionary<ushort, MonstrosityInstinct>();
        // level to the exp needed to clear it
        public readonly Dictionary<byte, uint> ExpTable = new Dictionary<byte, uint>();
        // keyed by menu page; the index within a page is the menu slot
        public readonly SortedDictionary<byte, List<MonstrosityUnlock>> TeyrnonShop = new SortedDictionary<byte, List<MonstrosityUnlock>>();
        public readonly List<MonstrosityUnlock> LevelUnlocks = new List<MonstrosityUnlock>();

        private const ushort DefaultHpScale = 240;

        // the ecosystem tokens (xi::Ecosystem is generated from ecosystems.yaml's keys)
        private static readonly Dictionary<string, byte> EcosystemTokens = new Dictionary<string, byte>
        {
            ["unclassified"] = Ecosystem.Unclassified, ["amorph"] = Ecosystem.Amorph, ["aquan"] = Ecosystem.Aquan,
            ["arcana"] = Ecosystem.Arcana, ["archaic_machine"] = Ecosystem.ArchaicMachine, ["beast"] = Ecosystem.Beast,
            ["beastmen"] = Ecosystem.Beastmen, ["bird"] = Ecosystem.Bird, ["demon"] = Ecosystem.Demon, ["dragon"] = Ecosystem.Dragon,
            ["elemental"] = Ecosystem.Elemental, ["empty"] = Ecosystem.Empty, ["humanoid"] = Ecosystem.Humanoid,
            ["lizard"] = Ecosystem.Lizard, ["luminian"] = Ecosystem.Luminian, ["luminion"] = Ecosystem.Luminion,
            ["plantoid"] = Ecosystem.Plantoid, ["supreme_beings"] = Ecosystem.SupremeBeings, ["undead"] = Ecosystem.Undead,
            ["vermin"] = Ecosystem.Vermin, ["voragean"] = Ecosystem.Voragean, ["structures"] = Ecosystem.Structures,
            ["weapons"] = Ecosystem.Weapons,
        };

        // Dataset::decode; speciesRanks resolves a mob_species id to its STR..CHR ranks (mobutils::GetSpeciesData)
        public static MonstrosityTable Load(string dataDir, DataEnums enums, Func<ushort, byte[]> speciesRanks = null)
        {
            var t = new MonstrosityTable();
            var path = Path.Combine(dataDir, "monstrosity.yaml");
            if (!File.Exists(path)) return t;
            var doc = MiniYaml.Load(path).Map("monstrosity");

            if (doc.MapOrNull("exp_table") is { } exp)
                foreach (var (k, v) in exp) t.ExpTable[byte.Parse(k, CultureInfo.InvariantCulture)] = uint.Parse((string)v, CultureInfo.InvariantCulture);
            // a missing level would read as 0 exp needed and level up on every kill
            for (byte level = 1; level < 100; level++)
                if (!t.ExpTable.TryGetValue(level, out var need) || need == 0) throw new FormatException($"exp_table is missing level {level}");

            if (doc.MapOrNull("teyrnon_shop") is { } shop)
                foreach (var (page, slots) in shop)
                {
                    var list = new List<MonstrosityUnlock>();
                    foreach (Dictionary<string, object> slot in (List<object>)slots)
                    {
                        var u = Unlock(slot, enums);
                        if (u.Species.HasValue == u.Variant.HasValue)
                            throw new FormatException($"teyrnon_shop page {page} slot {list.Count} must set exactly one of species and variant");
                        u.Infamy = ushort.Parse(slot.Str("infamy", "0"), CultureInfo.InvariantCulture);
                        list.Add(u);
                    }
                    t.TeyrnonShop[byte.Parse(page, CultureInfo.InvariantCulture)] = list;
                }

            foreach (Dictionary<string, object> entry in doc.List("level_unlocks"))
            {
                var u = Unlock(entry, enums);
                if (u.Species.HasValue == u.Variant.HasValue || entry.MapOrNull("requirements") == null)
                    throw new FormatException($"level_unlocks entry {t.LevelUnlocks.Count} must set requirements and exactly one of species and variant");
                t.LevelUnlocks.Add(u);
            }

            if (doc.MapOrNull("instincts") is { } instincts)
                foreach (var (key, value) in instincts)
                {
                    var m = (Dictionary<string, object>)value;
                    ushort id = (ushort)DataEnums.ParseInt(m.Str("id"));
                    var inst = new MonstrosityInstinct { Cost = (byte)DataEnums.ParseInt(m.Str("cost", "0")) };
                    Mods(m.MapOrNull("mods"), enums, inst.Mods);
                    if (!t.Instincts.TryAdd(id, inst)) throw new FormatException($"instinct '{key}' reuses id {id}");
                }

            if (doc.MapOrNull("species") is { } species)
                foreach (var (key, value) in species)
                {
                    var m = (Dictionary<string, object>)value;
                    ushort code = (ushort)DataEnums.ParseInt(m.Str("species_code"));
                    byte mjob = (byte)enums.Resolve("job", m.Str("mjob")), sjob = (byte)enums.Resolve("job", m.Str("sjob"));
                    var sp = new MonstrositySpecies
                    {
                        MonstrosityId = (byte)enums.Resolve("monstrosity_species", m.Str("family")),
                        MainJob = mjob,
                        SubJob = sjob,
                        HpMainJob = m.Str("hp_mjob") is { } hm ? (byte)enums.Resolve("job", hm) : mjob,
                        HpSubJob = m.Str("hp_sjob") is { } hs ? (byte)enums.Resolve("job", hs) : sjob,
                        Size = (byte)DataEnums.ParseInt(m.Str("size", "0")),
                        Look = (ushort)DataEnums.ParseInt(m.Str("look", "0")),
                        Ecosystem = EcosystemTokens.TryGetValue(m.Str("ecosystem", "unclassified"), out var eco)
                            ? eco : throw new FormatException($"unknown ecosystem '{m.Str("ecosystem")}'"),
                        MobSpecies = m.Str("mob_species") is { } ms ? (ushort)DataEnums.ParseInt(ms) : null,
                        HpScale = m.Str("hp_scale") is { } hp ? (ushort)DataEnums.ParseInt(hp) : DefaultHpScale,
                        Speed = m.Str("speed") is { } spd ? (byte)DataEnums.ParseInt(spd) : null,
                        AnimationSpeed = m.Str("animation_speed") is { } an ? (byte)DataEnums.ParseInt(an) : null,
                        Hitbox = m.Str("hitbox") is { } hb ? (byte)DataEnums.ParseInt(hb) : null,
                        StanceSpecies = m.Str("stance_species") is { } st ? (ushort)DataEnums.ParseInt(st) : null,
                    };
                    if (m.MapOrNull("stat_ranks") is { } ranks)
                    {
                        sp.StatRanks = new byte[7];
                        string[] keys = { "str", "dex", "vit", "agi", "int", "mnd", "chr" };
                        for (int k = 0; k < 7; k++) sp.StatRanks[k] = (byte)enums.Resolve("stat_rank", ranks.Str(keys[k]));
                    }
                    else if (sp.MobSpecies is ushort mob && speciesRanks != null) sp.StatRanks = speciesRanks(mob);
                    Mods(m.MapOrNull("mods"), enums, sp.Mods);
                    foreach (Dictionary<string, object> skill in m.List("tp_skills"))
                        sp.TpSkills.Add(new MonstrosityTpSkill
                        {
                            DatSkillId = (ushort)DataEnums.ParseInt(skill.Str("dat_skill_id")),
                            MobSkillId = (ushort)DataEnums.ParseInt(skill.Str("mob_skill_id")),
                            UnlockLevel = (byte)DataEnums.ParseInt(skill.Str("unlock_level", "0")),
                            TpCost = (ushort)DataEnums.ParseInt(skill.Str("tp_cost", "0")),
                            ReadyTimeMs = skill.Str("ready_time") is { } rt ? DataEnums.ParseInt(rt) : null,
                            Enabled = skill.Str("enabled") == "true",
                        });
                    if (!t.Species.TryAdd(code, sp)) throw new FormatException($"species '{key}' reuses species code {code}");
                }
            return t;
        }

        private static MonstrosityUnlock Unlock(Dictionary<string, object> m, DataEnums enums)
        {
            var u = new MonstrosityUnlock();
            if (m.Str("species") is { } s) u.Species = (byte)enums.Resolve("monstrosity_species", s);
            if (m.Str("variant") is { } v) u.Variant = (byte)enums.Resolve("monstrosity_variant", v);
            if (m.MapOrNull("requirements") is { } req)
                foreach (var (family, level) in req)
                    u.Requirements.Add(((byte)enums.Resolve("monstrosity_species", family), (byte)DataEnums.ParseInt((string)level)));
            return u;
        }

        private static void Mods(Dictionary<string, object> src, DataEnums enums, Dictionary<Mod, short> into)
        {
            if (src == null) return;
            foreach (var (name, value) in src) into[(Mod)enums.Resolve("mod", name)] = (short)DataEnums.ParseInt((string)value);
        }

        public MonstrositySpecies FindSpecies(ushort code) => Species.TryGetValue(code, out var s) ? s : null;
        public MonstrosityInstinct FindInstinct(ushort id) => Instincts.TryGetValue(id, out var i) ? i : null;
    }

    // monstrosity::MonstrosityData_t
    public sealed class MonstrosityData
    {
        public byte MonstrosityId = 0x01;      // Rabbit
        public ushort Species = 0x0001;        // Rabbit
        public ushort StanceSpecies;           // the form Fighting Stance shows, 0 outside the stance
        public ushort Flags = 0x0B44;          // TODO: Unknown flags
        public ushort Look = 0x010C;           // Rabbit
        public byte Size;                      // Small
        public byte NamePrefix1, NamePrefix2;
        public byte MainJob = Monstrosity.JobWar, SubJob = Monstrosity.JobWar;
        public uint CurrentExp;
        public readonly ushort[] EquippedInstincts = new ushort[12];
        public readonly byte[] Levels = new byte[128];
        public readonly byte[] Instincts = new byte[64];
        public readonly byte[] Variants = new byte[32];
        public bool Belligerency;
        public byte Rank;                      // cached for the status packet; SetPoints keeps it in step with infamy
        public ushort CombatSkill, EvasionSkill; // BuildSkillsTable's: an A+ skill and the species' evasion at its level
        public byte[] ListedSpells;            // the 0x0AA mask of the spells the species can cast at its level
        public long LastEquipChangeMs = long.MinValue / 2;
        public float EntryX, EntryY, EntryZ;
        public byte EntryRot;
        public ushort EntryZoneId;
        public byte EntryMainJob, EntrySubJob;

        // a new Monipulator has the three starter families at level 1, and owns the race I instincts
        public MonstrosityData()
        {
            foreach (var family in Monstrosity.StartingSpecies) Levels[family] = 1;
            foreach (var bit in Monstrosity.StartingInstincts)
                Instincts[Monstrosity.PurchasedInstinctsOffset + bit / 8] |= (byte)(1 << (bit % 8));
        }
    }

    public static class Monstrosity
    {
        public const byte JobWar = 1, JobMnk = 2, JobWhm = 3, JobBlm = 4, JobRdm = 5, JobPld = 7, JobMon = 23;
        public const ushort ZoneFeretory = 285;

        // purchased instincts sit in a gap of the family instinct bitfield
        public const int PurchasedInstinctsOffset = 20, PurchasedInstinctsBytes = 4;
        // each family unlocks an instinct at levels 30, 60 and 90, stored as a 2-bit count
        public const int LevelsPerInstinct = 30, LevelInstincts = 3;
        // variants are species codes from 256, purchasable instincts are instinct ids from 768
        public const int VariantSpeciesBase = 256, PurchasedInstinctBase = 768;
        // every change resends the whole inventory, so a client gets one per 250 ms
        public const long EquipChangeCooldownMs = 250;
        // the client allows instincts worth up to the species level plus this, and 20 more per rank
        public const int InstinctPointsOverLevel = 10, InstinctPointsPerRank = 20;
        // infamy stops rising here, higher while Belligerency is flagged
        public const int InfamyCap = 10000, InfamyCapBelligerency = 50000;
        // Mon to 10,000 infamy, NM to 20,000, HNM above
        public const int InfamyPerRank = 10000, TopRank = 2;
        // Fighting Stance α, which Gnole and Wamouracampa use to change form
        public const ushort FightingStanceSkill = 3123;
        // JP wiki: infamy and prestige are a tenth of the exp
        public const int ExpPerPoint = 10;

        // xi::MonstrositySpecies
        public const byte SpeciesRabbit = 1, SpeciesMandragora = 18, SpeciesLizard = 43, SpeciesAstoltianSlime = 126,
            SpeciesEorzeanSpriggan = 127;
        public static readonly byte[] StartingSpecies = { SpeciesRabbit, SpeciesMandragora, SpeciesLizard };
        // xi::MonstrosityInstinct HumeI .. GalkaI
        public static readonly byte[] StartingInstincts = { 0, 1, 2, 3, 4 };

        // Where leaving the Feretory drops a Monipulator, picked at random per zone. x, y, z, rot.
        public static readonly (ushort Zone, float X, float Y, float Z, float Rot)[] FeretoryExits =
        {
            (ZoneBhaflauThickets, 308.0f, -19.5f, 378.0f, 192.0f),
            (ZoneBhaflauThickets, 375.0f, -12.25f, -47.0f, 192.0f),
            (ZoneBuburimuPeninsula, 138.0f, -24.78f, 174.0f, 64.0f),
            (ZoneBuburimuPeninsula, 521.0f, 0.0f, 1.0f, 93.0f),
            (149, -286.0f, 4.0f, -200.0f, 0.0f),           // Davoi
            (101, 120.0f, 0.5f, -530.0f, 192.0f),          // East Ronfaure
            (101, 115.0f, -59.684f, 247.0f, 16.0f),
            (101, 515.0f, -30.0f, -40.0f, 96.0f),
            (101, 760.0f, -60.0f, 400.0f, 128.0f),
            (109, -483.0f, 25.0f, -318.0f, 0.0f),          // Pashhow Marshlands
            (126, -2.0f, -20.001f, 324.0f, 64.0f),         // Qufim Island
            (126, 161.0f, -20.0f, 37.0f, 192.0f),
            (126, -258.0f, -19.1f, 402.0f, 64.0f),
            (107, -115.0f, -0.136f, -165.0f, 64.0f),       // South Gustaberg
            (107, -530.0f, 39.3f, -405.0f, 0.0f),
            (103, 838.0f, 0.0f, -162.0f, 64.0f),           // Valkurm Dunes
            (103, -146.0f, -7.61f, 126.0f, 64.0f),
            (100, -600.0f, -60.0f, 400.0f, 32.0f),         // West Ronfaure
            (100, -120.0f, -20.0f, -120.0f, 112.0f),
            (115, -320.0f, -16.0f, 440.0f, 0.0f),          // West Sarutabaruta
            (125, 685.548f, -1.744f, -50.395f, 128.0f),    // Western Altepa Desert
        };
        private const ushort ZoneBhaflauThickets = 52, ZoneBuburimuPeninsula = 118, ZoneXarcabard = 112, ZoneUleguerandRange = 5;

        // JP wiki: the Odyssean Passage offers these, by recommended level. The client hides unvisited ones.
        public static readonly ushort[] PassageZones =
        {
            100, 101, 106, 107, 172, 115, 116, 192, 190, 143, 145, 140, 191, 166, 117, 141, 142, 108, 102, 103, 196, 193, 104,
            109, 118, 198, 119, 151, 149, 147, 105, 110, 120, 121, 126, 114, 24, 25, 111, 204, 195, 197, 200, 125, 127, 150,
            112, 169, 162, 161, 167, 5, 79, 205, 113, 52, 128, 33, 61, 35,
        };

        // matches xi.monstrosity.belligerencyCaps
        public static readonly ushort[] BelligerencyZones = { ZoneBuburimuPeninsula, ZoneXarcabard, ZoneUleguerandRange };

        // spells a Monipulator cannot cast even when its job knows them: Raise I-III, Reraise I-III, Arise, the
        // teleports, the recalls, Warp I-II, Retrace, Escape, Tractor
        public static readonly ushort[] BlockedSpells =
        {
            12, 13, 140, 135, 141, 142, 494, 120, 121, 122, 123, 124, 139, 81, 82, 83, 261, 262, 241, 263, 264,
        };

        public static bool IsPassageZone(ushort zone) => Array.IndexOf(PassageZones, zone) >= 0;

        public const ushort KeyItemGladiatorialWrit = 2351; // xi::KeyItem::GladiatorialWritOfSummons
        public const ushort EffectGestation = 575, EffectEncumbranceII = 177;
        // a gladiator's gear change locks every slot for 10 s
        public const long GladiatorEncumbranceMs = 10_000;
        // JP wiki: a Monipulator recovers this much HP and MP each tick out of combat
        public const int IdleRegenPercent = 5;

        // IsGladiatorInBelligerency: an adventurer holding the writ in a Belligerency zone (a Monipulator can carry the
        // writ in from its adventurer days)
        public static bool IsGladiatorInBelligerency(CharData c) =>
            c.Monstrosity == null && c.HasKeyItem(KeyItemGladiatorialWrit) && Array.IndexOf(BelligerencyZones, c.Zone) >= 0;

        public static bool IsMonipulatorInBelligerency(CharData c) =>
            c.Monstrosity is { Belligerency: true } && Array.IndexOf(BelligerencyZones, c.Zone) >= 0;

        // AreBelligerents: a flagged Monipulator and a gladiator, in a Belligerency zone, neither under Gestation
        public static bool AreBelligerents(BattleEntity first, BattleEntity second)
        {
            if (first is not CharData a || second is not CharData b) return false;
            if (a.StatusEffects.HasStatusEffect(EffectGestation) || b.StatusEffects.HasStatusEffect(EffectGestation)) return false;
            return IsMonipulatorInBelligerency(a) && IsGladiatorInBelligerency(b) || IsGladiatorInBelligerency(a) && IsMonipulatorInBelligerency(b);
        }

        // CanAssist: a pet or Trust goes by its master; Monipulators and adventurers help only their own kind
        public static bool CanAssist(BattleEntity initiator, CharData target) =>
            (initiator.PMaster ?? initiator) is not CharData source || (source.Monstrosity != null) == (target.Monstrosity != null);

        public static List<(float X, float Y, float Z, float Rot)> GetFeretoryExits(ushort zone)
        {
            var exits = new List<(float, float, float, float)>();
            foreach (var e in FeretoryExits)
                if (e.Zone == zone) exits.Add((e.X, e.Y, e.Z, e.Rot));
            return exits;
        }

        // applyLevelInstincts: each family's 2-bit count of level instincts, from its level
        public static void ApplyLevelInstincts(MonstrosityTable table, MonstrosityData data)
        {
            foreach (var species in table.Species.Values)
            {
                int family = species.MonstrosityId;
                // Slime and Spriggan sit past the end of the original array.
                int slot = family < SpeciesAstoltianSlime ? family : family + 128;
                int shift = slot * 2 % 8;
                int count = Math.Min(LevelInstincts, data.Levels[family] / LevelsPerInstinct);
                ref byte bits = ref data.Instincts[slot / 4];
                bits = (byte)((bits & ~(0x03 << shift)) | (count << shift));
            }
        }

        // grantLevelUnlocks: every family and variant whose level requirements are met; how many were new
        public static int GrantLevelUnlocks(MonstrosityTable table, MonstrosityData data)
        {
            int granted = 0;
            foreach (var unlock in table.LevelUnlocks)
            {
                if (!unlock.Requirements.TrueForAll(r => data.Levels[r.Family] >= r.Level)) continue;
                if (unlock.Species is byte s)
                {
                    if (data.Levels[s] == 0)
                    {
                        data.Levels[s] = 1;
                        granted++;
                    }
                }
                else if (unlock.Variant is byte v)
                {
                    byte mask = (byte)(1 << (v % 8));
                    if ((data.Variants[v / 8] & mask) == 0)
                    {
                        data.Variants[v / 8] |= mask;
                        granted++;
                    }
                }
            }
            return granted;
        }

        public static bool IsInstinctUnlocked(MonstrosityData data, ushort instinct)
        {
            if (instinct >= PurchasedInstinctBase)
            {
                int idx = instinct - PurchasedInstinctBase;
                int byteOffset = PurchasedInstinctsOffset + idx / 8;
                if (byteOffset >= PurchasedInstinctsOffset + PurchasedInstinctsBytes) return false;
                return (data.Instincts[byteOffset] >> (idx % 8) & 0x01) != 0;
            }
            // each family has three level instincts and a 2-bit count of how many are unlocked
            int slot = instinct / 3, tier = instinct % 3;
            int owned = data.Instincts[slot / 4] >> (slot * 2 % 8) & 0x03;
            return owned > tier;
        }

        public static bool IsVariantUnlocked(MonstrosityData data, int variant) => (data.Variants[variant / 8] >> (variant % 8) & 0x01) != 0;

        // rank follows the infamy held, and is Mon outside Belligerency (FFXIclopedia, retail)
        public static byte RankFor(MonstrosityData data, int infamy) =>
            data.Belligerency ? (byte)Math.Clamp((infamy - 1) / InfamyPerRank, 0, TopRank) : (byte)0;

        public static int InstinctsCost(MonstrosityTable table, ushort[] equipped)
        {
            int total = 0;
            foreach (var id in equipped)
                if (table.FindInstinct(id) is { } i) total += i.Cost;
            return (ushort)total;
        }

        public static int InstinctModTotal(MonstrosityTable table, ushort[] equipped, Mod mod)
        {
            int total = 0;
            foreach (var id in equipped)
                if (table.FindInstinct(id) is { } i && i.Mods.TryGetValue(mod, out var v)) total += v;
            return total;
        }

        public static int InstinctPointLimit(MonstrosityData data) =>
            data.Levels[data.MonstrosityId] + InstinctPointsOverLevel + data.Rank * InstinctPointsPerRank;

        // GetSpeciesJobs: the jobs and level a Monipulator fights with; its sub job is at the main level
        public static (byte MainJob, byte SubJob, byte Level) SpeciesJobs(MonstrosityData data) =>
            (data.MainJob, data.SubJob, Math.Max((byte)1, data.Levels[data.MonstrosityId]));

        // GetBaseDelay: the delay per hit; MNK species swing twice a round
        public static int BaseDelay(MonstrosityData data) => data.MainJob == JobMnk ? 180 : 240;

        // GetWeaponDelay: the round delay in ms
        public static int WeaponDelay(MonstrosityData data) => BaseDelay(data) * (data.MainJob == JobMnk ? 2 : 1) * 1000 / 60;

        // GetBaseDamage: fitted to retail hits, rounded low; a MNK species splits it over two fists
        public static int BaseDamage(MonstrosityData data, int ratingMod)
        {
            int perRound = SpeciesJobs(data).Level * 3 / 2 + 10;
            int damage = data.MainJob == JobMnk ? perRound / 2 : perRound;
            return Math.Clamp(damage + ratingMod, 1, 65535);
        }

        // CanCastSpell: Monipulators cannot call Trusts, nor cast the blocked spells
        public static bool CanCastSpell(Spell spell) => spell.Group != Spell.GroupTrust && Array.IndexOf(BlockedSpells, spell.Id) < 0;

        private const byte SpellReqMainJobOnly = 0x20;

        // ListsSpell: retail lists the learned spells the species' jobs can cast at the species level
        public static bool ListsSpell(MonstrosityData data, byte level, Spell spell) =>
            level >= spell.Job(data.MainJob) || level >= spell.Job(data.SubJob) && (spell.Requirements & SpellReqMainJobOnly) == 0;

        public static ushort DisplaySpecies(MonstrosityData data) => data.StanceSpecies != 0 ? data.StanceSpecies : data.Species;

        // GetPackedMonstrosityName. NOTE: changing this 0x8000 to 0xC000 hides the species name (a client quirk)
        public static uint PackedName(MonstrosityData data) =>
            data == null ? 0 : (uint)(data.NamePrefix2 << 24) + (uint)(data.NamePrefix1 << 16) + (ushort)(0x8000 | DisplaySpecies(data));

        public static uint ExpNextLevel(MonstrosityTable table, int level) =>
            level is > 0 and < 256 && table.ExpTable.TryGetValue((byte)level, out var v) ? v : 0;

        // ---- char_monstrosity ----

        // LoadMonstrosityData: the saved row, else a fresh Monipulator; the species' look, jobs and size either way
        public static MonstrosityData Load(IDatabase db, MonstrosityTable table, uint charId)
        {
            var data = new MonstrosityData();
            var r = db.Execute("SELECT charid, current_monstrosity_id, current_monstrosity_species, current_monstrosity_name_prefix_1, " +
                               "current_monstrosity_name_prefix_2, current_exp, equip, levels, instincts, variants, belligerency, " +
                               "entry_x, entry_y, entry_z, entry_rot, entry_zone_id, entry_mjob, entry_sjob " +
                               "FROM char_monstrosity WHERE charid = ? LIMIT 1", new List<BoundValue> { BoundValue.Of(charId) });
            if (r != null && r.Next())
            {
                data.MonstrosityId = r.Get<byte>("current_monstrosity_id");
                data.Species = r.Get<ushort>("current_monstrosity_species");
                data.NamePrefix1 = r.Get<byte>("current_monstrosity_name_prefix_1");
                data.NamePrefix2 = r.Get<byte>("current_monstrosity_name_prefix_2");
                data.CurrentExp = r.Get<uint>("current_exp");
                var equip = Blob(r, "equip");
                for (int i = 0; i < 12 && 2 * i + 1 < equip.Length; i++) data.EquippedInstincts[i] = BitConverter.ToUInt16(equip, 2 * i);
                Copy(Blob(r, "levels"), data.Levels);
                Copy(Blob(r, "instincts"), data.Instincts);
                Copy(Blob(r, "variants"), data.Variants);
                data.Belligerency = r.Get<uint>("belligerency") != 0;
                data.EntryX = r.Get<float>("entry_x");
                data.EntryY = r.Get<float>("entry_y");
                data.EntryZ = r.Get<float>("entry_z");
                data.EntryRot = r.Get<byte>("entry_rot");
                data.EntryZoneId = r.Get<ushort>("entry_zone_id");
                data.EntryMainJob = r.Get<byte>("entry_mjob");
                data.EntrySubJob = r.Get<byte>("entry_sjob");
                if (table.FindSpecies(data.Species) is { } species)
                {
                    data.Look = species.Look;
                    data.MainJob = species.MainJob;
                    data.SubJob = species.SubJob;
                    data.Size = species.Size;
                }
            }
            ApplyLevelInstincts(table, data);
            return data;
        }

        private static byte[] Blob(ResultSet r, string column) => r.IsNull(column) ? Array.Empty<byte>() : r.GetBlobBytes(column);

        private static void Copy(byte[] from, byte[] to) => Array.Copy(from, to, Math.Min(from.Length, to.Length));

        // SaveMonstrosityData
        public static void Save(IDatabase db, uint charId, MonstrosityData data)
        {
            var equip = new byte[24];
            for (int i = 0; i < 12; i++) BitConverter.GetBytes(data.EquippedInstincts[i]).CopyTo(equip, 2 * i);
            db.Execute("INSERT INTO char_monstrosity SET charid = ?, current_monstrosity_id = ?, current_monstrosity_species = ?, " +
                       "current_monstrosity_name_prefix_1 = ?, current_monstrosity_name_prefix_2 = ?, current_exp = ?, equip = ?, levels = ?, " +
                       "instincts = ?, variants = ?, belligerency = ?, entry_x = ?, entry_y = ?, entry_z = ?, entry_rot = ?, entry_zone_id = ?, " +
                       "entry_mjob = ?, entry_sjob = ? ON DUPLICATE KEY UPDATE " +
                       "current_monstrosity_id = VALUES(current_monstrosity_id), current_monstrosity_species = VALUES(current_monstrosity_species), " +
                       "current_monstrosity_name_prefix_1 = VALUES(current_monstrosity_name_prefix_1), " +
                       "current_monstrosity_name_prefix_2 = VALUES(current_monstrosity_name_prefix_2), current_exp = VALUES(current_exp), " +
                       "equip = VALUES(equip), levels = VALUES(levels), instincts = VALUES(instincts), variants = VALUES(variants), " +
                       "belligerency = VALUES(belligerency), entry_x = VALUES(entry_x), entry_y = VALUES(entry_y), entry_z = VALUES(entry_z), " +
                       "entry_rot = VALUES(entry_rot), entry_zone_id = VALUES(entry_zone_id), entry_mjob = VALUES(entry_mjob), " +
                       "entry_sjob = VALUES(entry_sjob)",
                new List<BoundValue>
                {
                    BoundValue.Of(charId), BoundValue.Of(data.MonstrosityId), BoundValue.Of(data.Species), BoundValue.Of(data.NamePrefix1),
                    BoundValue.Of(data.NamePrefix2), BoundValue.Of(data.CurrentExp), BoundValue.Of(equip), BoundValue.Of((byte[])data.Levels.Clone()),
                    BoundValue.Of((byte[])data.Instincts.Clone()), BoundValue.Of((byte[])data.Variants.Clone()), BoundValue.Of((byte)(data.Belligerency ? 1 : 0)),
                    BoundValue.Of(data.EntryX), BoundValue.Of(data.EntryY), BoundValue.Of(data.EntryZ), BoundValue.Of(data.EntryRot),
                    BoundValue.Of(data.EntryZoneId), BoundValue.Of(data.EntryMainJob), BoundValue.Of(data.EntrySubJob),
                });
        }

        // applySpeciesState: the species level wins over char_jobs (floor 1), MON on both jobs at that level, and the
        // species' ecosystem for correlation
        public static void ApplySpeciesState(MonstrosityTable table, CharData c)
        {
            var data = c.Monstrosity;
            byte level = Math.Max((byte)1, data.Levels[data.MonstrosityId]);
            data.Levels[data.MonstrosityId] = level;
            c.JobLevels[JobMon] = level;
            // Retail reports MON as the sub job too, at the same level.
            c.SJob = JobMon;
            c.MLvl = level;
            c.SLvl = level;
            c.EcoSystem = table.FindSpecies(data.Species)?.Ecosystem ?? Ecosystem.Unclassified;
        }

        // TryPopulateMonstrosityData: a MON char's data (saved back at once, as the C++ does)
        public static void TryPopulate(IDatabase db, MonstrosityTable table, bool enabled, CharData c)
        {
            if (!enabled || c.MJob != JobMon || table == null) return;
            c.Monstrosity = Load(db, table, c.Id);
            ApplySpeciesState(table, c);
            // TODO: Is this write still needed for !monstrosity?
            Save(db, c.Id, c.Monstrosity);
        }

        // ---- the species and instinct modifiers ----

        public static void AddInstinctMods(MonstrosityTable table, CharData c, ushort id)
        {
            if (table.FindInstinct(id) is { } i)
                foreach (var (mod, value) in i.Mods) c.AddModifier(mod, value);
        }

        public static void DelInstinctMods(MonstrosityTable table, CharData c, ushort id)
        {
            if (table.FindInstinct(id) is { } i)
                foreach (var (mod, value) in i.Mods) c.DelModifier(mod, value);
        }

        public static void AddAllInstinctMods(MonstrosityTable table, CharData c)
        {
            foreach (var id in c.Monstrosity.EquippedInstincts) AddInstinctMods(table, c, id);
        }

        public static void DelAllInstinctMods(MonstrosityTable table, CharData c)
        {
            foreach (var id in c.Monstrosity.EquippedInstincts) DelInstinctMods(table, c, id);
        }

        public static void AddSpeciesMods(MonstrosityTable table, CharData c)
        {
            if (table.FindSpecies(c.Monstrosity.Species) is { } s)
                foreach (var (mod, value) in s.Mods) c.AddModifier(mod, value);
        }

        public static void DelSpeciesMods(MonstrosityTable table, CharData c)
        {
            if (table.FindSpecies(c.Monstrosity.Species) is { } s)
                foreach (var (mod, value) in s.Mods) c.DelModifier(mod, value);
        }

        // unequipInstinctsOverLimit: retail unequips every instinct rather than picking which to drop
        public static bool UnequipInstinctsOverLimit(MonstrosityTable table, CharData c)
        {
            if (InstinctsCost(table, c.Monstrosity.EquippedInstincts) <= InstinctPointLimit(c.Monstrosity)) return false;
            DelAllInstinctMods(table, c);
            Array.Clear(c.Monstrosity.EquippedInstincts);
            return true;
        }

        // ---- packets ----

        // 0x063 GP_SERV_COMMAND_MISCDATA::MONSTROSITY1: empty outside MON (a MON char's must be at least partly
        // filled, or it loses its abilities and gets the wrong traits)
        public static BasicPacket Monstrosity1(CharData c, int infamy)
        {
            var w = GameOkPackets.MiscData(3, 220);
            var data = c?.Monstrosity;
            if (data == null) return w.Packet;
            w.U16(8, data.Species);
            w.U16(10, data.Flags);       // not the Belligerency flag
            w.U8(12, data.Rank);
            // TODO: unknown1[0] varies on retail with no known pattern.
            w.U8(16, 0xEC);
            w.U16(18, infamy);
            // the 2-bit level instinct counts; the purchased instincts are MONSTROSITY2's only
            w.Bytes(28, data.Instincts);
            w.Bytes(28 + PurchasedInstinctsOffset, new byte[PurchasedInstinctsBytes]);
            // mapped onto the family ids (00 doesn't exist, 01 is rabbit, 02 is behemoth, etc.)
            w.Bytes(92, data.Levels);
            return w.Packet;
        }

        // 0x063 GP_SERV_COMMAND_MISCDATA::MONSTROSITY2: the families SE added later, the purchased instincts, the variants
        public static BasicPacket Monstrosity2(CharData c)
        {
            var w = GameOkPackets.MiscData(4, 180);
            var data = c?.Monstrosity;
            if (data == null) return w.Packet;
            w.U8(0x86, data.Levels[SpeciesAstoltianSlime]);
            w.U8(0x87, data.Levels[SpeciesEorzeanSpriggan]);
            w.Bytes(0x88, data.Instincts.AsSpan(PurchasedInstinctsOffset, PurchasedInstinctsBytes));
            w.Bytes(0x94, data.Variants);
            return w.Packet;
        }
    }
}
