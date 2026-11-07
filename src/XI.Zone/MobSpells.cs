using System;
using System.Collections.Generic;
using XI.Persistence;

namespace XI.Zone
{
    // A mob_spell_lists row (CMobSpellList / MobSpell_t): one spell and the level band it is castable in.
    public readonly struct MobSpell
    {
        public readonly ushort SpellId;
        public readonly ushort MinLevel, MaxLevel;
        public MobSpell(ushort spellId, ushort minLevel, ushort maxLevel)
        {
            SpellId = spellId;
            MinLevel = minLevel;
            MaxLevel = maxLevel;
        }
    }

    // CMobSpellList (mob_spell_list.h): a shared spell list, either a mob_spell_lists id or a template's inline
    // "spells:" (then Id is null). AddSpell appends in load order; GetSpellMinLevel is the first band's min, else 255.
    public sealed class MobSpellList
    {
        public const ushort MaxMobSpellListId = 5000; // MAX_MOBSPELLLIST_ID

        public readonly ushort? Id;
        public readonly List<MobSpell> Spells = new List<MobSpell>();

        public MobSpellList(ushort? id) => Id = id;

        public void AddSpell(ushort spellId, ushort minLvl, ushort maxLvl) => Spells.Add(new MobSpell(spellId, minLvl, maxLvl));

        public ushort GetSpellMinLevel(ushort spellId)
        {
            foreach (var s in Spells)
                if (s.SpellId == spellId) return s.MinLevel;
            return 255;
        }
    }

    // CMobSpellContainer (mob_spell_container.h): the mob's castable spells, split into the buckets the mob AI picks
    // from. AddSpell classifies a spell by the CSpell predicates (the same order as the C++ if-chain); the random
    // Get*Spell pickers are the casting AI, in ZoneHost.MobMagic.cs. HasMPSpells and the Has* queries match
    // the C++ const methods. RemoveSpell does not touch the severe list and the m_hasSpells reset ignores it, as in C++.
    public sealed class MobSpellContainer
    {
        public readonly List<ushort> GaList = new List<ushort>();
        public readonly List<ushort> DamageList = new List<ushort>();
        public readonly List<ushort> BuffList = new List<ushort>();
        public readonly List<ushort> DebuffList = new List<ushort>();
        public readonly List<ushort> HealList = new List<ushort>();
        public readonly List<ushort> NaList = new List<ushort>();
        public readonly List<ushort> RaiseList = new List<ushort>();
        public readonly List<ushort> SevereList = new List<ushort>();

        public bool HasSpells { get; private set; }

        public void ClearSpells()
        {
            GaList.Clear();
            DamageList.Clear();
            BuffList.Clear();
            DebuffList.Clear();
            HealList.Clear();
            NaList.Clear();
            RaiseList.Clear();
            SevereList.Clear();
            HasSpells = false;
        }

        // CMobSpellContainer::AddSpell: a spell::GetSpell that is gone (a spell id mob_spell_lists names but spell_list
        // does not have) is logged and skipped, exactly as the C++ ShowDebug branch.
        public void AddSpell(ushort spellId, Spell spell, Action<string> log)
        {
            if (spell == null)
            {
                log?.Invoke($"Missing spellID = {spellId}, given to mob. Check spell_list.sql");
                return;
            }

            HasSpells = true;
            byte aoe = spell.Aoe;
            if (aoe > 0 && spell.CanTargetEnemy) GaList.Add(spellId);
            else if (spell.IsSevere) SevereList.Add(spellId);
            else if (spell.CanTargetEnemy && !spell.IsSevere) DamageList.Add(spellId);
            else if (spell.IsDebuff) DebuffList.Add(spellId);
            else if (spell.IsNa) NaList.Add(spellId);
            else if (spell.IsRaise) RaiseList.Add(spellId);
            else if (spell.IsHeal) HealList.Add(spellId);
            else if (spell.IsBuff) BuffList.Add(spellId);
            // else: "Where does this spell go?" (C++ ShowDebug) - the spell is castable but fits no bucket
        }

        public void RemoveSpell(ushort spellId)
        {
            GaList.Remove(spellId);
            DamageList.Remove(spellId);
            BuffList.Remove(spellId);
            DebuffList.Remove(spellId);
            HealList.Remove(spellId);
            NaList.Remove(spellId);
            RaiseList.Remove(spellId);
            HasSpells = !(GaList.Count == 0 && DamageList.Count == 0 && BuffList.Count == 0 && DebuffList.Count == 0 &&
                          HealList.Count == 0 && NaList.Count == 0 && RaiseList.Count == 0);
        }

        public bool HasGaSpells() => GaList.Count > 0;
        public bool HasDamageSpells() => DamageList.Count > 0;
        public bool HasBuffSpells() => BuffList.Count > 0;
        public bool HasDebuffSpells() => DebuffList.Count > 0;
        public bool HasHealSpells() => HealList.Count > 0;
        public bool HasNaSpells() => NaList.Count > 0;
        public bool HasRaiseSpells() => RaiseList.Count > 0;
        public bool HasSevereSpells() => SevereList.Count > 0;
        public bool HasNaSpell(ushort spellId) => NaList.Contains(spellId);

        // HasMPSpells: any damage or buff spell that costs MP (the C++ hasMPCost over those two lists only).
        public bool HasMPSpells(Func<ushort, Spell> getSpell)
        {
            foreach (var id in DamageList)
                if (getSpell(id)?.HasMpCost == true) return true;
            foreach (var id in BuffList)
                if (getSpell(id)?.HasMpCost == true) return true;
            return false;
        }
    }

    // mobutils (the spell-list half) + mobSpellList (the registry) on the zone host. The registry is loaded once from
    // mob_spell_lists; each mob keeps a shared source list (MobEntity.SpellList) and computes its own bucketed container
    // (MobEntity.SpellContainer) from it at the mob's level.
    public sealed partial class ZoneHost
    {
        private Dictionary<ushort, MobSpellList> mobSpellLists_;
        private Dictionary<string, ushort> spellIdsByName_;

        // mobSpellList::LoadMobSpellList: id 0 is the empty list; the rest come from mob_spell_lists (spell_list_id <
        // 5000) joined to spell_list, ordered by min_level as in the C++ query.
        private void LoadMobSpellLists()
        {
            if (mobSpellLists_ != null) return;
            mobSpellLists_ = new Dictionary<ushort, MobSpellList> { [0] = new MobSpellList(0) };
            var r = Exec("SELECT mob_spell_lists.spell_list_id, mob_spell_lists.spell_id, mob_spell_lists.min_level, " +
                         "mob_spell_lists.max_level FROM mob_spell_lists JOIN spell_list ON spell_list.spellid = mob_spell_lists.spell_id " +
                         "WHERE spell_list_id < ? ORDER BY min_level ASC", BoundValue.Of(MobSpellList.MaxMobSpellListId));
            while (r != null && r.Next())
            {
                ushort listId = r.Get<ushort>("spell_list_id");
                ushort spellId = r.Get<ushort>("spell_id");
                ushort min = r.Get<byte>("min_level");
                ushort max = r.Get<byte>("max_level");
                if (!mobSpellLists_.TryGetValue(listId, out var list))
                    mobSpellLists_[listId] = list = new MobSpellList(listId);
                list.AddSpell(spellId, min, max);
            }
        }

        // mobSpellList::GetMobSpellList
        private MobSpellList GetMobSpellList(ushort listId)
        {
            LoadMobSpellLists();
            if (mobSpellLists_.TryGetValue(listId, out var list)) return list;
            log_($"Mob spell list ID {listId} does not exist.");
            return null;
        }

        // spell::lookupIdByName: the name -> lowest id (a HashMap try_emplace over the list in id order)
        private ushort? SpellIdByName(string name)
        {
            LoadSpells();
            if (spellIdsByName_ == null)
            {
                spellIdsByName_ = new Dictionary<string, ushort>(StringComparer.Ordinal);
                var ids = new List<ushort>(spells_.Keys);
                ids.Sort();
                foreach (var id in ids) spellIdsByName_.TryAdd(spells_[id].Name, id);
            }
            return spellIdsByName_.TryGetValue(name, out var found) ? found : (ushort?)null;
        }

        // zoneutils::buildSpellList: a template's inline "spells:" names, each castable at every level (0..255). A name
        // spell_list does not have is critical (the C++ logs and exits); the shipped data only names real spells, so log
        // and skip (an exit is not an option here). The list has no id (a template's own list, not a registry id).
        private MobSpellList BuildSpellList(ushort zoneId, string templateName, List<object> spells)
        {
            var list = new MobSpellList(null);
            foreach (var o in spells)
            {
                string name = (string)o;
                if (SpellIdByName(name) is ushort id) list.AddSpell(id, 0, 255);
                else log_($"buildSpellList: template '{templateName}' in zone {zoneId} names unknown spell '{name}'");
            }
            return list;
        }

        // spell::GetSpell without a script load (the container classifies on the row only)
        private Spell SpellRow(ushort id)
        {
            LoadSpells();
            return spells_.TryGetValue(id, out var s) ? s : null;
        }

        // mobutils::RecalculateSpellContainer: recompute the bucketed container from the mob's source list at its level
        private void RecalculateSpellContainer(MobEntity m)
        {
            m.SpellContainer.ClearSpells();
            if (m.SpellList == null) return;
            foreach (var e in m.SpellList.Spells)
                if (m.MLvl >= e.MinLevel && m.MLvl <= e.MaxLevel)
                    m.SpellContainer.AddSpell(e.SpellId, SpellRow(e.SpellId), log_);
        }

        // mobutils::GetAvailableSpells (called on spawn after the stats, from mobutils::CalculateMobStats): the mob's
        // non-defaulted cast chances, then the level-filtered container, then the no-MP warning.
        private void GetAvailableSpells(MobEntity m)
        {
            if (m.SpellList == null) return;
            m.DefaultMobMod(MobMod.MagicCool, 35);
            m.DefaultMobMod(MobMod.GaChance, 35);
            m.DefaultMobMod(MobMod.NaChance, 40);
            m.DefaultMobMod(MobMod.SevereSpellChance, 20);
            m.DefaultMobMod(MobMod.BuffChance, 35);
            m.DefaultMobMod(MobMod.HealChance, 40);
            m.DefaultMobMod(MobMod.HpHealChance, 40);
            RecalculateSpellContainer(m);
            if (m.MaxMp == 0 && m.SpellContainer.HasMPSpells(SpellRow))
                log_($"mobutils::GetAvailableSpells Mob ({m.Id}) has no mp for casting spells!");
        }

        // mobutils::CalculateMobStats' spell-list mob-mods: PetSpellList gives the mob's pet a list, SpellList overrides
        // the mob's own. A named id that does not exist clears the source list (as the C++ assigns nullptr), after
        // GetMobSpellList logs it.
        private void ApplySpellListMobMod(MobEntity m)
        {
            if (m.GetMobMod(MobMod.PetSpellList) != 0 && m.PPet is MobEntity pet)
                pet.SpellList = GetMobSpellList((ushort)m.GetMobMod(MobMod.PetSpellList));
            if (m.GetMobMod(MobMod.SpellList) != 0)
                m.SpellList = GetMobSpellList((ushort)m.GetMobMod(MobMod.SpellList));
        }

        // the spawn hook MobStats calls after CalculateMobStats: the mob-mod override, then GetAvailableSpells
        private void ResolveMobSpells(MobEntity m)
        {
            ApplySpellListMobMod(m);
            GetAvailableSpells(m);
        }

        // mobutils::SetSpellList (CLuaBaseEntity::setSpellList): point the mob at a registry list and recompute its
        // container. A list id that does not exist leaves the mob's spells untouched (the C++ only assigns on success).
        private void SetMobSpellList(MobEntity m, ushort listId)
        {
            var list = GetMobSpellList(listId);
            if (list == null) return;
            m.SpellList = list;
            RecalculateSpellContainer(m);
        }
    }
}
