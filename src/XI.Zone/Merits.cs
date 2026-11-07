using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using XI.Persistence;

namespace XI.Zone
{
    // meritNameSpace::LoadMeritsList over data/merits.yaml (xi::data::datasets::merits): the merits sorted by id, each
    // category's slice of them, the level caps expanded across every level, and the merit each skill takes its bonus
    // from. The spell and weapon skill a merit grants are resolved by name on first use (ResolveGrants).
    public sealed class MeritTable
    {
        public const int Slots = 305;          // kMeritPacketSlots: 5 packets of 61
        public const int PerPacket = 61;       // MAX_MERITS_IN_PACKET
        private const int CategoryStride = 64; // kCategoryStride

        // xi::Merit / xi::MeritCategory, the ones the server reads
        public const ushort MaxHp = 0x0040, MaxMp = 0x0042, MaxMerit = 0x0044, Str = 0x0080, Assimilation = 0x0BC6;
        public const ushort CategoryOthers = 0x0140, CategoryWeaponSkills = 0x0680;

        public sealed class Entry
        {
            public ushort Id, Category, Value;
            public byte Upgrade;   // max upgrades
            public uint Jobs;      // 1 << (job - 1)
            public byte[] Costs;
            public string Spell, WeaponSkill;
            public ushort SpellId, WsUnlockId;
        }

        public sealed class CategoryData
        {
            public ushort Id, MaxUpgrades, Offset;
            public byte Count;
        }

        public readonly List<Entry> Entries = new List<Entry>();
        public readonly List<CategoryData> Categories = new List<CategoryData>();
        public readonly byte[] LevelCaps = new byte[256];
        public readonly Dictionary<int, ushort> MeritBySkill = new Dictionary<int, ushort>();
        private readonly Dictionary<ushort, int> indexById_ = new Dictionary<ushort, int>();
        private readonly Dictionary<ushort, CategoryData> categoryById_ = new Dictionary<ushort, CategoryData>();
        private bool grantsResolved_;

        public int IndexOf(ushort id) => indexById_.TryGetValue(id, out int i) ? i : -1;
        public bool Exists(ushort id) => indexById_.ContainsKey(id);
        public CategoryData CategoryOf(ushort id) => categoryById_.TryGetValue((ushort)(id & 0xFFC0), out var cat) ? cat : null;

        // nextUpgradeCost: the price of the next upgrade, the last one's once maxed, 0 when it cannot be upgraded
        public static byte NextCost(Entry e, byte count) =>
            e.Costs == null || e.Costs.Length == 0 || e.Upgrade == 0 ? (byte)0 : e.Costs[Math.Min(count, e.Upgrade - 1)];

        public static MeritTable Load(string dataDir, DataEnums enums)
        {
            var table = new MeritTable();
            var doc = MiniYaml.Load(Path.Combine(dataDir, "merits.yaml")).Map("merits");
            var jobIds = enums.Of("job");
            var skillIds = enums.Of("skill_type");

            var costs = new Dictionary<string, byte[]>();
            foreach (var (name, list) in doc.Map("upgrade_costs"))
                costs[name] = ((List<object>)list).ConvertAll(v => byte.Parse((string)v, CultureInfo.InvariantCulture)).ToArray();

            var caps = doc.List("level_caps");
            for (int i = 0; i < caps.Count; i++)
            {
                var entry = (Dictionary<string, object>)caps[i];
                int level = Int(entry.Str("level"));
                int next = i + 1 < caps.Count ? Int(((Dictionary<string, object>)caps[i + 1]).Str("level")) : table.LevelCaps.Length;
                for (int l = level; l < next; l++) table.LevelCaps[l] = (byte)Int(entry.Str("cap"));
            }

            foreach (var (_, catObj) in doc.Map("categories"))
            {
                var cat = (Dictionary<string, object>)catObj;
                ushort catId = U16(cat.Str("id"));
                var catJobs = cat.ContainsKey("jobs") ? cat.List("jobs") : null;
                var merits = cat.MapOrNull("merits");
                if (merits != null)
                {
                    foreach (var (_, meritObj) in merits)
                    {
                        var m = (Dictionary<string, object>)meritObj;
                        var cost = costs[m.Str("upgrade_cost")];
                        // the cost table sizes the merit unless it caps itself lower
                        byte upgrade = m.Str("max_upgrades") is string mu ? (byte)Int(mu) : (byte)cost.Length;
                        // a merit's own job list wins, then its category's, then every job but NONE and MON
                        var jobs = m.ContainsKey("jobs") ? m.List("jobs") : catJobs;
                        uint jobMask = 0;
                        if (jobs == null)
                        {
                            foreach (var (_, job) in jobIds)
                                if (job != 0 && job != 23) jobMask |= 1u << (job - 1);
                        }
                        else
                        {
                            foreach (var job in jobs) jobMask |= 1u << (jobIds[(string)job] - 1);
                        }
                        var e = new Entry
                        {
                            Id = U16(m.Str("id")), Category = catId, Value = (ushort)Int(m.Str("value")), Upgrade = upgrade,
                            Jobs = jobMask, Costs = cost, Spell = m.Str("spell"), WeaponSkill = m.Str("weapon_skill"),
                        };
                        table.Entries.Add(e);
                        foreach (var skill in m.List("skills")) table.MeritBySkill[skillIds[(string)skill]] = e.Id;
                    }
                }
                table.Categories.Add(new CategoryData { Id = catId, MaxUpgrades = (ushort)Int(cat.Str("max_upgrades")) });
            }

            table.Entries.Sort((a, b) => a.Id.CompareTo(b.Id));
            table.Categories.Sort((a, b) => a.Id.CompareTo(b.Id));
            if (table.Entries.Count > Slots) throw new FormatException($"data/merits.yaml holds {table.Entries.Count} merits, but only {Slots} fit the client's slots");
            int index = 0;
            foreach (var cat in table.Categories)
            {
                cat.Offset = (ushort)index;
                while (index < table.Entries.Count && table.Entries[index].Category == cat.Id) index++;
                cat.Count = (byte)(index - cat.Offset);
                table.categoryById_[cat.Id] = cat;
            }
            for (int i = 0; i < table.Entries.Count; i++) table.indexById_[table.Entries[i].Id] = i;
            return table;
        }

        // the spell_list / weapon_skills ids the merits name (a name that is not there grants nothing)
        public void ResolveGrants(IDatabase db)
        {
            if (grantsResolved_ || db == null) return;
            grantsResolved_ = true;
            var spells = Lookup(db, "SELECT name, spellid FROM spell_list", "spellid");
            var wsUnlocks = Lookup(db, "SELECT name, unlock_id FROM weapon_skills", "unlock_id");
            foreach (var e in Entries)
            {
                if (e.Spell != null && spells.TryGetValue(e.Spell, out ushort spell)) e.SpellId = spell;
                if (e.WeaponSkill != null && wsUnlocks.TryGetValue(e.WeaponSkill, out ushort ws)) e.WsUnlockId = ws;
            }
        }

        private static Dictionary<string, ushort> Lookup(IDatabase db, string query, string column)
        {
            var lookup = new Dictionary<string, ushort>();
            var r = db.Execute(query, new List<BoundValue>());
            while (r != null && r.Next())
            {
                ushort value = r.Get<ushort>(column);
                if (value != 0) lookup.TryAdd(r.Get<string>("name"), value);
            }
            return lookup;
        }

        private static int Int(string s) => int.Parse(s, CultureInfo.InvariantCulture);

        private static ushort U16(string s) =>
            s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? ushort.Parse(s.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : ushort.Parse(s, CultureInfo.InvariantCulture);
    }

    // CMeritPoints: the upgrades a char holds per merit slot (in the table's id order). The merit and limit points
    // themselves stay on CharData.
    public sealed class MeritPoints
    {
        public readonly MeritTable Table;
        public readonly byte[] Counts = new byte[MeritTable.Slots];

        public MeritPoints(MeritTable table) => Table = table;

        public byte Count(ushort id) => Table.IndexOf(id) is int i && i >= 0 ? Counts[i] : (byte)0;

        // the next upgrade's cost for the slot (0 for a slot past the merits)
        public byte Next(int index) => index < Table.Entries.Count ? MeritTable.NextCost(Table.Entries[index], Counts[index]) : (byte)0;

        public int CountInSameCategory(ushort id)
        {
            var cat = Table.CategoryOf(id);
            if (!Table.Exists(id) || cat == null) return 0;
            int total = 0;
            for (int i = 0; i < cat.Count; i++) total += Counts[cat.Offset + i];
            return total;
        }

        // CMeritPoints::GetMeritValue: the general categories apply to every job, the rest need the merit's job at 75+;
        // the upgrades counted are capped by level (not MaxMerit's), and weapon skill merits need 96
        public int Value(CharData c, ushort id)
        {
            int index = Table.IndexOf(id);
            if (index < 0) return 0;
            var e = Table.Entries[index];
            int value = 0;
            if (e.Category <= MeritTable.CategoryOthers || (c.MJob > 0 && (e.Jobs & 1u << (c.MJob - 1)) != 0 && c.MLvl >= 75))
                value = id == MeritTable.MaxMerit ? Counts[index] : Math.Min(Counts[index], Table.LevelCaps[c.MLvl]);
            if (e.Category == MeritTable.CategoryWeaponSkills && c.MLvl < 96) value = 0;
            return (ushort)(value * e.Value);
        }
    }
}
