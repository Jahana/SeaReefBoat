using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace XI.Zone
{
    // The data a mob is built from: ecosystems.yaml (ecosystem -> family -> species attributes), a zone's mobs.yaml
    // (templates, spawns and spawn slots) and regions.yaml (the polygons a mob spawns in). A template's spell list
    // (spell_list_id, or its own spells, which buildSpellList turns into a list) is resolved in MobSpells.cs.

    // StatRanksData / StatRanksOverrides: base HP/MP and the stat ranks (xi::StatRank, A = 1 .. G = 7)
    public sealed class StatRanks
    {
        public uint? Hp, Mp;
        public byte? Str, Dex, Vit, Agi, Int, Mnd, Chr, Def, Eva, Att, Acc;

        public void Merge(StatRanks o)
        {
            if (o == null) return;
            Hp = o.Hp ?? Hp; Mp = o.Mp ?? Mp;
            Str = o.Str ?? Str; Dex = o.Dex ?? Dex; Vit = o.Vit ?? Vit; Agi = o.Agi ?? Agi; Int = o.Int ?? Int;
            Mnd = o.Mnd ?? Mnd; Chr = o.Chr ?? Chr; Def = o.Def ?? Def; Eva = o.Eva ?? Eva; Att = o.Att ?? Att; Acc = o.Acc ?? Acc;
        }
    }

    // MobAttributesOverrides, and once merged onto the defaults, MobAttributesData: the getters below give the
    // resolved value (Element none, stats C with attack / accuracy A, speed 40)
    public sealed class MobAttributes
    {
        public uint? EntityFlags;
        public byte? AnimationSub, Animation, NameVis, NamePrefix, ModelSize, Hitbox;
        public ushort[] Look;
        public ushort? Moving;
        public ushort? Behavior;
        public uint? Respawn;
        public ushort? SpawnType;
        public (byte Start, byte End)? SpawnWindow;
        public uint? Immune;
        public byte? CombatSkill;
        public ushort? Delay, DamageMultiplier;
        public byte? MainJob, SubJob;
        public byte? Element;
        public StatRanks Stats = new StatRanks();
        public ushort? Detects;
        public byte? Speed, AnimationSpeed;
        public bool? Charmable, Aggressive, Links, TrueDetection;
        public Dictionary<Mod, short> Resists = new Dictionary<Mod, short>();
        public Dictionary<Mod, short> Mods = new Dictionary<Mod, short>();
        public Dictionary<MobMod, short> MobMods = new Dictionary<MobMod, short>();

        public byte StrRank => Stats.Str ?? 3;
        public byte DexRank => Stats.Dex ?? 3;
        public byte VitRank => Stats.Vit ?? 3;
        public byte AgiRank => Stats.Agi ?? 3;
        public byte IntRank => Stats.Int ?? 3;
        public byte MndRank => Stats.Mnd ?? 3;
        public byte ChrRank => Stats.Chr ?? 3;
        public byte DefRank => Stats.Def ?? 3;
        public byte EvaRank => Stats.Eva ?? 3;
        public byte AttRank => Stats.Att ?? 1;
        public byte AccRank => Stats.Acc ?? 1;
        public byte SpeedOrDefault => Speed ?? 40;

        // applyOverrides: a set field replaces, stats merge per field, the maps by key
        public void Merge(MobAttributes o)
        {
            if (o == null) return;
            EntityFlags = o.EntityFlags ?? EntityFlags;
            AnimationSub = o.AnimationSub ?? AnimationSub;
            Look = o.Look ?? Look;
            Animation = o.Animation ?? Animation;
            Moving = o.Moving ?? Moving;
            NameVis = o.NameVis ?? NameVis;
            NamePrefix = o.NamePrefix ?? NamePrefix;
            ModelSize = o.ModelSize ?? ModelSize;
            Hitbox = o.Hitbox ?? Hitbox;
            Behavior = o.Behavior ?? Behavior;
            Respawn = o.Respawn ?? Respawn;
            SpawnType = o.SpawnType ?? SpawnType;
            SpawnWindow = o.SpawnWindow ?? SpawnWindow;
            Immune = o.Immune ?? Immune;
            CombatSkill = o.CombatSkill ?? CombatSkill;
            Delay = o.Delay ?? Delay;
            DamageMultiplier = o.DamageMultiplier ?? DamageMultiplier;
            MainJob = o.MainJob ?? MainJob;
            SubJob = o.SubJob ?? SubJob;
            AnimationSpeed = o.AnimationSpeed ?? AnimationSpeed;
            Element = o.Element ?? Element;
            Stats.Merge(o.Stats);
            Detects = o.Detects ?? Detects;
            Speed = o.Speed ?? Speed;
            Charmable = o.Charmable ?? Charmable;
            Aggressive = o.Aggressive ?? Aggressive;
            Links = o.Links ?? Links;
            TrueDetection = o.TrueDetection ?? TrueDetection;
            foreach (var (k, v) in o.Resists) Resists[k] = v;
            foreach (var (k, v) in o.Mods) Mods[k] = v;
            foreach (var (k, v) in o.MobMods) MobMods[k] = v;
        }

        public MobAttributes Clone()
        {
            var copy = new MobAttributes();
            copy.Merge(this);
            return copy;
        }

        private static readonly (string Key, Mod Mod)[] Physical =
        {
            ("slashing", Mod.SLASH_SDT), ("piercing", Mod.PIERCE_SDT), ("blunt", Mod.IMPACT_SDT), ("h2h", Mod.HTH_SDT),
        };
        private static readonly (string Key, Mod Mod)[] Magic =
        {
            ("all", Mod.UDMGMAGIC), ("fire", Mod.FIRE_SDT), ("ice", Mod.ICE_SDT), ("wind", Mod.WIND_SDT), ("earth", Mod.EARTH_SDT),
            ("thunder", Mod.THUNDER_SDT), ("water", Mod.WATER_SDT), ("light", Mod.LIGHT_SDT), ("dark", Mod.DARK_SDT),
        };
        private static readonly (string Key, Mod Mod)[] RankElement =
        {
            ("fire", Mod.FIRE_RES_RANK), ("ice", Mod.ICE_RES_RANK), ("wind", Mod.WIND_RES_RANK), ("earth", Mod.EARTH_RES_RANK),
            ("thunder", Mod.THUNDER_RES_RANK), ("water", Mod.WATER_RES_RANK), ("light", Mod.LIGHT_RES_RANK), ("dark", Mod.DARK_RES_RANK),
        };
        private static readonly (string Key, Mod Mod)[] RankStatus =
        {
            ("paralyze", Mod.PARALYZE_RES_RANK), ("bind", Mod.BIND_RES_RANK), ("silence", Mod.SILENCE_RES_RANK),
            ("slow", Mod.SLOW_RES_RANK), ("poison", Mod.POISON_RES_RANK), ("light_sleep", Mod.LIGHT_SLEEP_RES_RANK),
            ("dark_sleep", Mod.DARK_SLEEP_RES_RANK), ("blind", Mod.BLIND_RES_RANK), ("stun", Mod.STUN_RES_RANK),
            ("gravity", Mod.GRAVITY_RES_RANK),
        };

        // convertAttributes
        public static MobAttributes Parse(Dictionary<string, object> src, DataEnums enums, uint context)
        {
            var a = new MobAttributes();
            if (src == null) return a;
            if (src.Str("element") is string element) a.Element = (byte)enums.Resolve("element", element);
            if (src.MapOrNull("stats") is { } stats)
            {
                a.Stats.Hp = U32(stats.Str("hp"));
                a.Stats.Mp = U32(stats.Str("mp"));
                byte? Rank(string key) => stats.Str(key) is string r ? (byte)enums.Resolve("stat_rank", r) : null;
                a.Stats.Str = Rank("str"); a.Stats.Dex = Rank("dex"); a.Stats.Vit = Rank("vit"); a.Stats.Agi = Rank("agi");
                a.Stats.Int = Rank("int"); a.Stats.Mnd = Rank("mnd"); a.Stats.Chr = Rank("chr"); a.Stats.Def = Rank("def");
                a.Stats.Eva = Rank("eva"); a.Stats.Att = Rank("att"); a.Stats.Acc = Rank("acc");
            }
            if (src.ContainsKey("detects")) a.Detects = (ushort)Flags(enums, "detects", src.List("detects"));
            if (src.MapOrNull("spawn") is { } spawn)
            {
                a.Respawn = U32(spawn.Str("respawn"));
                if (spawn.ContainsKey("type")) a.SpawnType = (ushort)Flags(enums, "spawn_type", spawn.List("type"));
                if (spawn.MapOrNull("window") is { } window && window.Str("start") is string start && window.Str("end") is string end)
                    a.SpawnWindow = (byte.Parse(start, CultureInfo.InvariantCulture), byte.Parse(end, CultureInfo.InvariantCulture));
            }
            if (src.MapOrNull("combat") is { } combat)
            {
                if (combat.Str("skill") is string skill) a.CombatSkill = (byte)enums.Resolve("skill_type", skill);
                a.Delay = U16(combat.Str("delay"));
                a.DamageMultiplier = U16(combat.Str("dmg_mult"));
            }
            var jobs = src.List("jobs");
            if (jobs.Count == 2)
            {
                a.MainJob = (byte)enums.Resolve("job", (string)jobs[0]);
                a.SubJob = (byte)enums.Resolve("job", (string)jobs[1]);
            }
            else if (jobs.Count != 0) throw new FormatException($"'{context}' jobs needs a main and a sub job");
            a.Speed = (byte?)U16(src.Str("speed"));
            a.AnimationSpeed = (byte?)U16(src.Str("animation_speed"));
            a.Charmable = Bool(src.Str("charmable"));
            if (src.MapOrNull("render") is { } render)
            {
                a.EntityFlags = U32(render.Str("entity_flags"));
                a.AnimationSub = (byte?)U16(render.Str("animation_sub"));
                if (render.Str("animation") is string animation) a.Animation = (byte)enums.Resolve("animation", animation);
                a.Moving = U16(render.Str("moving"));
                if (render.MapOrNull("look") is { } look) a.Look = NpcEntity.LookFields(look, context);
                a.NameVis = (byte?)U16(render.Str("name_vis"));
                a.NamePrefix = (byte?)U16(render.Str("name_prefix"));
                a.ModelSize = (byte?)U16(render.Str("model_size"));
                a.Hitbox = (byte?)U16(render.Str("hitbox"));
            }
            if (src.MapOrNull("behaviors") is { } behaviors)
            {
                if (behaviors.ContainsKey("flags")) a.Behavior = (ushort)Flags(enums, "behavior", behaviors.List("flags"));
                a.Aggressive = Bool(behaviors.Str("aggressive"));
                a.Links = Bool(behaviors.Str("links"));
                a.TrueDetection = Bool(behaviors.Str("true_detection"));
            }
            if (src.MapOrNull("resists") is { } resists)
            {
                void Take(Dictionary<string, object> group, (string Key, Mod Mod)[] keys)
                {
                    if (group == null) return;
                    foreach (var (key, mod) in keys)
                        if (group.Str(key) is string v) a.Resists[mod] = short.Parse(v, CultureInfo.InvariantCulture);
                }
                Take(resists.MapOrNull("dmg_physical"), Physical);
                Take(resists.MapOrNull("dmg_magic"), Magic);
                Take(resists.MapOrNull("rank_element"), RankElement);
                Take(resists.MapOrNull("rank_status"), RankStatus);
                if (resists.ContainsKey("immune_status")) a.Immune = (uint)Flags(enums, "immunity", resists.List("immune_status"));
            }
            if (src.MapOrNull("mods") is { } mods)
                foreach (var (k, v) in mods) a.Mods[(Mod)enums.Resolve("mod", k)] = short.Parse((string)v, CultureInfo.InvariantCulture);
            if (src.MapOrNull("mob_mods") is { } mobMods)
                foreach (var (k, v) in mobMods) a.MobMods[(MobMod)enums.Resolve("mob_mod", k)] = short.Parse((string)v, CultureInfo.InvariantCulture);
            return a;
        }

        // yaml::resolveFlags: the tokens OR'd together
        internal static int Flags(DataEnums enums, string name, List<object> tokens)
        {
            int flags = 0;
            foreach (var t in tokens) flags |= enums.Resolve(name, (string)t);
            return flags;
        }

        private static bool? Bool(string s) => s == null ? null : s == "true";
        private static ushort? U16(string s) => s == null ? null : ushort.Parse(s, CultureInfo.InvariantCulture);
        private static uint? U32(string s) => s == null ? null : uint.Parse(s, CultureInfo.InvariantCulture);
    }

    // ecosystems.yaml: a species' attributes are its ecosystem's, then its family's, then its own
    public sealed class SpeciesInfo
    {
        public ushort Id;
        public string Name = "";
        public byte Ecosystem;
        public ushort Family;
        public MobAttributes Attributes;
    }

    // xi::Ecosystem ids the mob code names
    public static class Ecosystem
    {
        public const byte Unclassified = 0, Amorph = 1, Aquan = 2, Arcana = 3, ArchaicMachine = 4, Beast = 5, Beastmen = 6,
            Bird = 7, Demon = 8, Dragon = 9, Elemental = 10, Empty = 11, Humanoid = 12, Lizard = 13, Luminian = 14, Luminion = 15,
            Plantoid = 16, SupremeBeings = 17, Undead = 18, Vermin = 19, Voragean = 20, Structures = 21, Weapons = 22;
    }

    public sealed class Ecosystems
    {
        public readonly Dictionary<string, SpeciesInfo> SpeciesByName = new Dictionary<string, SpeciesInfo>();

        public static Ecosystems Load(string dataDir, DataEnums enums)
        {
            var eco = new Ecosystems();
            var path = Path.Combine(dataDir, "ecosystems.yaml");
            if (!File.Exists(path)) return eco;
            foreach (var (ecoName, ecoValue) in MiniYaml.Load(path).Map("ecosystems"))
            {
                var e = (Dictionary<string, object>)ecoValue;
                byte ecoId = byte.Parse(e.Str("id"), CultureInfo.InvariantCulture);
                var ecoAttrs = MobAttributes.Parse(e.MapOrNull("attributes"), enums, ecoId);
                if (e.MapOrNull("families") is not { } families) continue;
                foreach (var (famName, famValue) in families)
                {
                    var f = (Dictionary<string, object>)famValue;
                    ushort famId = ushort.Parse(f.Str("id"), CultureInfo.InvariantCulture);
                    if (famId == 0) throw new FormatException($"family '{famName}' declares reserved id 0");
                    var famAttrs = MobAttributes.Parse(f.MapOrNull("attributes"), enums, famId);
                    if (f.MapOrNull("species") is not { } species) continue;
                    foreach (var (spName, spValue) in species)
                    {
                        var sp = (Dictionary<string, object>)spValue;
                        ushort spId = ushort.Parse(sp.Str("id"), CultureInfo.InvariantCulture);
                        if (spId == 0) throw new FormatException($"species '{spName}' declares reserved id 0");
                        var attrs = new MobAttributes();
                        attrs.Merge(ecoAttrs);
                        attrs.Merge(famAttrs);
                        attrs.Merge(MobAttributes.Parse(sp.MapOrNull("attributes"), enums, spId));
                        if (!eco.SpeciesByName.TryAdd(spName, new SpeciesInfo { Id = spId, Name = spName, Ecosystem = ecoId, Family = famId, Attributes = attrs }))
                            throw new FormatException($"duplicate species '{spName}'");
                    }
                }
            }
            return eco;
        }
    }

    // MobTemplateData
    public sealed class MobTemplate
    {
        public uint Id;               // the pool id
        public string Name = "", DisplayName = "";
        public string Species = "";
        public ushort Type, RoamFlags;
        public ushort SpellList, SkillList;
        public List<object> Spells = new List<object>(); // the template's inline "spells:" names (buildSpellList)
        public MobAttributes Attributes;
        public byte Allegiance;
        public string Content;
        public LootData Loot = new LootData();
    }

    // LootRollData: a drop roll's per-mille chance and either an item or a one_of (basis-point shares, or 1 each for an
    // even list)
    public sealed class LootRoll
    {
        public ushort Chance;
        public string Item = "";
        public List<(string Name, ushort Weight)> OneOf = new List<(string Name, ushort Weight)>();
    }

    // LootData: a template's drops, what can be stolen and what despoil takes (its weights, sorted by name)
    public sealed class LootData
    {
        public List<LootRoll> Drops = new List<LootRoll>();
        public List<string> Steal = new List<string>();
        public List<(string Name, ushort Weight)> Despoil = new List<(string Name, ushort Weight)>();

        public bool Empty => Drops.Count == 0 && Steal.Count == 0 && Despoil.Count == 0;

        // convertLoot
        public static LootData Parse(Dictionary<string, object> source, DataEnums enums, string key)
        {
            var loot = new LootData();
            if (source == null) return loot;
            foreach (var r in source.List("drops"))
            {
                var roll = r as Dictionary<string, object> ?? new Dictionary<string, object>();
                bool hasItem = roll.ContainsKey("item"), hasOneOf = roll.ContainsKey("one_of");
                if (hasItem == hasOneOf) throw new FormatException($"template '{key}' has a loot roll that must name either an item or a one_of");
                loot.Drops.Add(new LootRoll
                {
                    Chance = PerMille(roll.Str("chance"), enums, key),
                    Item = roll.Str("item", ""),
                    OneOf = ConvertOneOf(hasOneOf ? roll["one_of"] : null, key),
                });
            }
            if (source.TryGetValue("steal", out var steal))
            {
                if (steal is string one) loot.Steal.Add(one);
                else if (steal is List<object> names) foreach (var n in names) loot.Steal.Add((string)n);
            }
            if (source.MapOrNull("despoil") is { } despoil)
            {
                var names = new List<string>(despoil.Keys);
                names.Sort(StringComparer.Ordinal);
                foreach (var n in names) loot.Despoil.Add((n, ushort.Parse((string)despoil[n], CultureInfo.InvariantCulture)));
            }
            return loot;
        }

        // a drop_rate name resolves straight to its per-mille rate; a percentage converts
        private static ushort PerMille(string chance, DataEnums enums, string key)
        {
            if (!double.TryParse(chance, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent))
                return (ushort)enums.Resolve("drop_rate", chance);
            if (percent < 0.0 || percent > 100.0)
                throw new FormatException($"template '{key}' has a loot chance of {Pct(percent)}%, outside 0-100");
            return (ushort)Math.Round(percent * 10.0, MidpointRounding.AwayFromZero);
        }

        // shares are basis points, so a whole roll is 10000 and a percentage keeps two decimals
        private static List<(string Name, ushort Weight)> ConvertOneOf(object source, string key)
        {
            var members = new List<(string Name, ushort Weight)>();
            if (source == null) return members;
            if (source is List<object> even)
            {
                if (even.Count == 0) throw new FormatException($"template '{key}' has a one_of that names nothing");
                foreach (var n in even) members.Add(((string)n, (ushort)1));
                return members;
            }
            var shares = source as Dictionary<string, object> ?? new Dictionary<string, object>();
            var names = new List<string>(shares.Keys);
            names.Sort(StringComparer.Ordinal);
            uint total = 0;
            foreach (var n in names)
            {
                double percent = double.Parse((string)shares[n], CultureInfo.InvariantCulture);
                if (percent <= 0.0 || percent > 100.0)
                    throw new FormatException($"template '{key}' gives one_of member '{n}' a share of {Pct(percent)}%, outside 0-100");
                var basisPoints = (ushort)Math.Round(percent * 100.0, MidpointRounding.AwayFromZero);
                total += basisPoints;
                members.Add((n, basisPoints));
            }
            if (total != 10000) throw new FormatException($"template '{key}' has one_of shares totalling {Pct(total / 100.0)}%, not 100");
            return members;
        }

        private static string Pct(double v) => v.ToString(CultureInfo.InvariantCulture);
    }

    // MobSpawnData
    public sealed class MobSpawn
    {
        public uint Id;
        public ushort ActIndex;
        public string TemplateName = "", Script = "";
        public bool Placed;
        public float X, Y, Z;
        public byte Rotation;
        public byte MinLevel, MaxLevel;
        public List<string> Regions = new List<string>();
        public List<(float X, float Y, float Z)> Route = new List<(float, float, float)>();
        public MobAttributes Attributes;
    }

    public sealed class MobSlotMember
    {
        public ushort ActIndex;
        public byte Chance;
        public uint Cooldown;
    }

    public sealed class MobSlotData
    {
        public uint Id;
        public List<MobSlotMember> Members = new List<MobSlotMember>();
    }

    // zones/mobs Dataset::decode
    public sealed class MobsFile
    {
        public Dictionary<string, MobTemplate> Templates = new Dictionary<string, MobTemplate>();
        public List<MobSpawn> Spawns = new List<MobSpawn>();
        public List<MobSlotData> Slots = new List<MobSlotData>();

        public static MobsFile Load(string path, DataEnums enums)
        {
            var file = new MobsFile();
            if (!File.Exists(path)) return file;
            var doc = MiniYaml.Load(path);
            if (doc.MapOrNull("templates") is { } templates)
            {
                foreach (var (key, value) in templates)
                {
                    var t = (Dictionary<string, object>)value;
                    uint id = uint.Parse(t.Str("id"), CultureInfo.InvariantCulture);
                    if (id == 0) throw new FormatException($"template '{key}' declares reserved pool id 0");
                    string content = t.Str("content");
                    if (content != null) enums.Resolve("content", content);
                    // convertTemplate: a template names either its own spells or a spell_list_id, never both, and a
                    // spells list that names nothing is rejected
                    bool hasSpells = t.TryGetValue("spells", out var spellsVal);
                    string spellListId = t.Str("spell_list_id");
                    var inlineSpells = hasSpells && spellsVal is List<object> sl ? sl : new List<object>();
                    if (hasSpells && spellListId != null)
                        throw new FormatException($"template '{key}' names its own spells and a spell_list_id");
                    if (hasSpells && inlineSpells.Count == 0)
                        throw new FormatException($"template '{key}' has a spells list that names nothing");
                    file.Templates.TryAdd(key, new MobTemplate
                    {
                        Id = id,
                        Name = key,
                        DisplayName = t.Str("display_name", key),
                        Species = t.Str("species", ""),
                        Type = (ushort)MobAttributes.Flags(enums, "mob_type", t.List("type")),
                        RoamFlags = (ushort)MobAttributes.Flags(enums, "roam_flag", t.List("roam")),
                        SpellList = NpcEntity.U16(spellListId),
                        Spells = inlineSpells,
                        SkillList = NpcEntity.U16(t.Str("skill_list_id")),
                        Attributes = MobAttributes.Parse(t.MapOrNull("attributes"), enums, id),
                        Allegiance = (byte)enums.Resolve("allegiance", t.Str("allegiance")),
                        Content = content,
                        Loot = LootData.Parse(t.MapOrNull("loot"), enums, key),
                    });
                }
            }

            var seen = new HashSet<uint>();
            if (doc.MapOrNull("spawns") is { } spawns)
            {
                foreach (var (key, value) in spawns)
                {
                    uint id = uint.Parse(key, CultureInfo.InvariantCulture);
                    var s = value as Dictionary<string, object> ?? new Dictionary<string, object>();
                    var spawn = ConvertSpawn(id, s, enums);
                    if (!seen.Add(id)) throw new FormatException($"duplicate spawn id {id}");
                    if (spawn.TemplateName.Length == 0)
                    {
                        if (spawn.Script.Length == 0) throw new FormatException($"spawn {id} has neither a template nor a script");
                    }
                    else
                    {
                        if (!file.Templates.TryGetValue(spawn.TemplateName, out var declared))
                            throw new FormatException($"spawn {id} references unknown template '{spawn.TemplateName}'");
                        if (spawn.Script.Length == 0) spawn.Script = declared.Name;
                    }
                    file.Spawns.Add(spawn);
                }
            }

            if (doc.TryGetValue("slots", out var slotsValue) && slotsValue is List<object> slots)
            {
                foreach (var slotValue in slots)
                {
                    var slot = new MobSlotData { Id = (uint)file.Slots.Count + 1 };
                    int weighted = 0;
                    var members = (slotValue as Dictionary<string, object>)?.MapOrNull("members") ?? new Dictionary<string, object>();
                    foreach (var (key, value) in members)
                    {
                        uint id = uint.Parse(key, CultureInfo.InvariantCulture);
                        if (!seen.Contains(id)) throw new FormatException($"slot {slot.Id} lists {id}, which no spawn declares");
                        var m = value as Dictionary<string, object> ?? new Dictionary<string, object>();
                        byte chance = (byte)NpcEntity.U16(m.Str("chance"));
                        weighted += chance;
                        slot.Members.Add(new MobSlotMember
                        {
                            ActIndex = (ushort)(id & 0xFFF),
                            Chance = chance,
                            Cooldown = uint.Parse(m.Str("cooldown", "0"), CultureInfo.InvariantCulture),
                        });
                    }
                    if (weighted > 100) throw new FormatException($"slot {slot.Id} allocates {weighted}% across its weighted members");
                    file.Slots.Add(slot);
                }
            }
            return file;
        }

        // convertSpawn: at most one of at / region / path / circuit; a path walks back the way it came
        private static MobSpawn ConvertSpawn(uint id, Dictionary<string, object> s, DataEnums enums)
        {
            int placements = (s.ContainsKey("at") ? 1 : 0) + (s.ContainsKey("region") ? 1 : 0) + (s.ContainsKey("path") ? 1 : 0) +
                             (s.ContainsKey("circuit") ? 1 : 0);
            if (placements > 1) throw new FormatException($"spawn {id} sets more than one of at, region, path and circuit");
            var spawn = new MobSpawn
            {
                Id = id,
                ActIndex = (ushort)(id & 0xFFF),
                TemplateName = s.Str("template", ""),
                Script = s.Str("script", ""),
                Placed = placements > 0,
                Attributes = MobAttributes.Parse(s.MapOrNull("attributes"), enums, id),
            };
            if (s.TryGetValue("region", out var region))
            {
                if (region is string one) spawn.Regions.Add(one);
                else foreach (var r in (List<object>)region) spawn.Regions.Add((string)r);
                if (spawn.Regions.Count == 0) throw new FormatException($"spawn {id} lists no regions");
            }
            var at = Floats(s.List("at"));
            if (s.ContainsKey("at"))
            {
                if (at.Count < 3 || at.Count > 4) throw new FormatException($"spawn {id} position needs three values plus an optional facing");
                spawn.X = at[0]; spawn.Y = at[1]; spawn.Z = at[2];
                spawn.Rotation = at.Count > 3 ? (byte)at[3] : (byte)0;
            }
            var level = s.List("level");
            if (level.Count == 2)
            {
                spawn.MinLevel = byte.Parse((string)level[0], CultureInfo.InvariantCulture);
                spawn.MaxLevel = byte.Parse((string)level[1], CultureInfo.InvariantCulture);
            }
            if (s.ContainsKey("circuit")) spawn.Route = Route(s.List("circuit"), id);
            else if (s.ContainsKey("path"))
            {
                spawn.Route = Route(s.List("path"), id);
                for (int k = spawn.Route.Count - 1; k > 0; k--) spawn.Route.Add(spawn.Route[k - 1]);
            }
            return spawn;
        }

        private static List<(float, float, float)> Route(List<object> points, uint id)
        {
            if (points.Count < 2) throw new FormatException($"spawn {id} has a route of {points.Count} waypoints");
            var route = new List<(float, float, float)>();
            foreach (var p in points)
            {
                var v = Floats((List<object>)p);
                if (v.Count != 3) throw new FormatException($"spawn {id} has a waypoint with {v.Count} values, not x, y, z");
                route.Add((v[0], v[1], v[2]));
            }
            return route;
        }

        internal static List<float> Floats(List<object> l) => l.ConvertAll(o => float.Parse((string)o, CultureInfo.InvariantCulture));
    }

    // RoamRegion: a polygon in the XZ plane with optional holes. The C++ triangulates outer ring and holes together
    // with mapbox earcut; this ear-clips the outer ring and draws again when a point lands in a hole, which gives the
    // same uniform distribution (a different random sequence). The navmesh checks are in RoamRegion.Roam.cs.
    public sealed partial class RoamRegion
    {
        public string Name = "";
        private readonly List<(float X, float Y, float Z)> outer_;
        private readonly List<List<(float X, float Y, float Z)>> holes_;
        private readonly List<(int A, int B, int C, float AreaSum)> triangles_ = new List<(int, int, int, float)>();
        private readonly float minX_, maxX_, minZ_, maxZ_;

        public RoamRegion(string name, List<(float X, float Y, float Z)> outer, List<List<(float X, float Y, float Z)>> holes)
        {
            Name = name;
            outer_ = outer;
            holes_ = holes;
            if (outer.Count < 3) throw new FormatException($"region '{name}' has fewer than three points");
            minX_ = maxX_ = outer[0].X;
            minZ_ = maxZ_ = outer[0].Z;
            foreach (var p in outer)
            {
                minX_ = MathF.Min(minX_, p.X); maxX_ = MathF.Max(maxX_, p.X);
                minZ_ = MathF.Min(minZ_, p.Z); maxZ_ = MathF.Max(maxZ_, p.Z);
            }
            float sum = 0;
            foreach (var (a, b, c) in EarClip(outer))
            {
                sum += MathF.Abs(Cross(outer[a], outer[b], outer[c])) * 0.5f;
                triangles_.Add((a, b, c, sum));
            }
            if (triangles_.Count == 0) throw new FormatException($"region '{name}' has no area");
        }

        public float Area => triangles_[^1].AreaSum;

        // twice the signed area of abc in XZ
        private static float Cross((float X, float Y, float Z) a, (float X, float Y, float Z) b, (float X, float Y, float Z) c) =>
            (b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X);

        private static List<(int, int, int)> EarClip(List<(float X, float Y, float Z)> poly)
        {
            var idx = new List<int>();
            for (int k = 0; k < poly.Count; k++) idx.Add(k);
            float area = 0;
            for (int k = 0; k < poly.Count; k++)
            {
                var p = poly[k]; var q = poly[(k + 1) % poly.Count];
                area += p.X * q.Z - q.X * p.Z;
            }
            if (area < 0) idx.Reverse();
            var tris = new List<(int, int, int)>();
            int guard = 0;
            while (idx.Count > 3 && guard++ < poly.Count * poly.Count)
            {
                bool clipped = false;
                for (int k = 0; k < idx.Count; k++)
                {
                    int a = idx[(k + idx.Count - 1) % idx.Count], b = idx[k], c = idx[(k + 1) % idx.Count];
                    float cr = Cross(poly[a], poly[b], poly[c]);
                    if (cr <= 0) continue;
                    bool inside = false;
                    foreach (int o in idx)
                    {
                        if (o == a || o == b || o == c) continue;
                        if (InTriangle(poly[o], poly[a], poly[b], poly[c])) { inside = true; break; }
                    }
                    if (inside) continue;
                    tris.Add((a, b, c));
                    idx.RemoveAt(k);
                    clipped = true;
                    break;
                }
                if (!clipped)
                {
                    // only collinear or self-touching vertices are left: drop one without a triangle
                    idx.RemoveAt(0);
                }
            }
            if (idx.Count == 3 && Cross(poly[idx[0]], poly[idx[1]], poly[idx[2]]) != 0) tris.Add((idx[0], idx[1], idx[2]));
            return tris;
        }

        private static bool InTriangle((float X, float Y, float Z) p, (float X, float Y, float Z) a, (float X, float Y, float Z) b,
                                       (float X, float Y, float Z) c)
        {
            float d1 = Cross(a, b, p), d2 = Cross(b, c, p), d3 = Cross(c, a, p);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        private static bool InRing(List<(float X, float Y, float Z)> ring, float x, float z)
        {
            bool inside = false;
            for (int k = 0, j = ring.Count - 1; k < ring.Count; j = k++)
            {
                var a = ring[k]; var b = ring[j];
                if ((a.Z > z) != (b.Z > z) && x < (b.X - a.X) * (z - a.Z) / (b.Z - a.Z) + a.X) inside = !inside;
            }
            return inside;
        }

        public bool Contains(float x, float z)
        {
            if (x < minX_ || x > maxX_ || z < minZ_ || z > maxZ_) return false;
            if (!InRing(outer_, x, z)) return false;
            foreach (var hole in holes_)
                if (InRing(hole, x, z)) return false;
            return true;
        }

        // samplePoint: a triangle weighted by area, then a uniform point inside it, height interpolated
        public (float X, float Y, float Z) SamplePoint(Random rng)
        {
            (float X, float Y, float Z) point = default;
            for (int attempt = 0; attempt < 64; attempt++)
            {
                float pick = (float)rng.NextDouble() * Area;
                int t = triangles_.FindIndex(tri => tri.AreaSum >= pick);
                if (t < 0) t = triangles_.Count - 1;
                var (ia, ib, ic, _) = triangles_[t];
                var a = outer_[ia]; var b = outer_[ib]; var c = outer_[ic];
                float u = (float)rng.NextDouble(), v = (float)rng.NextDouble();
                if (u + v > 1) { u = 1 - u; v = 1 - v; }
                point = (a.X + u * (b.X - a.X) + v * (c.X - a.X), a.Y + u * (b.Y - a.Y) + v * (c.Y - a.Y),
                         a.Z + u * (b.Z - a.Z) + v * (c.Z - a.Z));
                bool inHole = false;
                foreach (var hole in holes_)
                    if (InRing(hole, point.X, point.Z)) { inHole = true; break; }
                if (!inHole) return point;
            }
            return point;
        }

        // regions.yaml beside zone.yaml
        public static Dictionary<string, RoamRegion> Load(string path)
        {
            var regions = new Dictionary<string, RoamRegion>();
            if (!File.Exists(path)) return regions;
            var doc = MiniYaml.Load(path).MapOrNull("regions");
            if (doc == null) return regions;
            foreach (var (name, value) in doc)
            {
                var r = (Dictionary<string, object>)value;
                var holes = new List<List<(float, float, float)>>();
                foreach (var h in r.List("holes")) holes.Add(Ring((List<object>)h, name));
                regions[name] = new RoamRegion(name, Ring(r.List("poly"), name), holes);
            }
            return regions;
        }

        private static List<(float, float, float)> Ring(List<object> points, string name)
        {
            var ring = new List<(float, float, float)>();
            foreach (var p in points)
            {
                var v = MobsFile.Floats((List<object>)p);
                if (v.Count != 3) throw new FormatException($"region '{name}' has a point with {v.Count} values, not x, y, z");
                ring.Add((v[0], v[1], v[2]));
            }
            return ring;
        }
    }
}
