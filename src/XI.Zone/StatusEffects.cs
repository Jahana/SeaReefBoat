using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace XI.Zone
{
    // xi::StatusEffect: the ids the container itself names
    public static class EffectId
    {
        public const ushort Ko = 0, Weakness = 1, SleepI = 2, Petrification = 7, Stun = 10, Slow = 13, CharmI = 14, CharmII = 17, SleepII = 19,
                            Terror = 28, Haste = 33, BlazeSpikes = 34, IceSpikes = 35, Blink = 36, Stoneskin = 37, ShockSpikes = 38,
                            CopyImage = 66, DamageSpikes = 153, Penalty = 159, Enchantment = 162, DreadSpikes = 173, Lullaby = 193,
                            Food = 251, None = 255, AlliedTags = 267, LevelSync = 269, Aftermath = 273, Visitant = 285, FireManeuver = 300, DarkManeuver = 307,
                            Reprisal = 403, DelugeSpikes = 573, Gestation = 575, GaleSpikes = 605, ClodSpikes = 606, GlintSpikes = 607,
                            MightyStrikes = 44, Boost = 45, SneakAttack = 65, Sneak = 71, Deodorize = 70, Hide = 76, Illusion = 258, TrickAttack = 87, Mounted = 252, AllMiss = 791,
                            Healing = 794, LeaveGame = 795, Teleport = 797;
        public const ushort Poison = 3, Paralysis = 4, Blindness = 5, Silence = 6, CurseI = 9, Bind = 11, Weight = 12, CurseII = 20,
                            Requiem = 192, Elegy = 194, Nocturne = 223;
        public const ushort Bolster = 513;
        public const ushort SjRestriction = 157, Obliviscence = 260;
        public const ushort Max = 814; // MAX_EFFECTID
    }

    // xi::Immunity: the bits a mob's m_Immunity carries (a char has none)
    public static class Immunity
    {
        public const uint Addle = 0x1, Gravity = 0x2, Bind = 0x4, Stun = 0x8, Silence = 0x10, Paralyze = 0x20, Blind = 0x40, Slow = 0x80,
                          Poison = 0x100, Elegy = 0x200, Requiem = 0x400, LightSleep = 0x800, DarkSleep = 0x1000, Aspir = 0x2000,
                          Terror = 0x4000, Dispel = 0x8000, Petrify = 0x10000, Plague = 0x20000, Curse = 0x40000;
    }

    // xi::StatusEffectFlag (data/enums/status_effect_flag.yaml)
    public static class EffectFlag
    {
        public const uint None = 0, Dispelable = 0x1, Erasable = 0x2, Attack = 0x4, Empathy = 0x8, Damage = 0x10, Death = 0x20,
                          MagicBegin = 0x40, MagicEnd = 0x80, OnZone = 0x100, NoLossMessage = 0x200, Invisible = 0x400,
                          Detectable = 0x800, NoRest = 0x1000, PreventAction = 0x2000, Waltzable = 0x4000, Food = 0x8000,
                          Song = 0x10000, Roll = 0x20000, SynthSupport = 0x40000, Confrontation = 0x80000, Logout = 0x100000,
                          Bloodpact = 0x200000, OnJobChange = 0x400000, NoCancel = 0x800000, Influence = 0x1000000,
                          OfflineTick = 0x2000000, Aura = 0x4000000, HideTimer = 0x8000000, OnZonePathos = 0x10000000,
                          AlwaysExpiring = 0x20000000, OnAttack = 0x40000000;
    }

    // xi::EffectOverwrite
    public enum EffectOverwrite : byte { EqualHigher = 0, Higher = 1, Never = 2, Always = 3, IgnoreDuplicate = 4, TierHigher = 5 }

    public enum EffectNotice { ShowMessage, Silent }

    // EffectSourceType
    public static class EffectSource
    {
        public const ushort None = 0, EquippedItem = 1, TemporaryItem = 2, Mob = 3, Food = 4, CorsairRoll = 5;
    }

    // effects::EffectParams_t
    public sealed class EffectParams
    {
        public string Name = "";
        public uint Flag;
        public ushort Type, NegativeId, BlockId, RemoveId;
        public EffectOverwrite Overwrite;
        public byte Element;
        public long MinDurationMs;
        public ushort SortKey = 10000;
        public ushort WearOffMessageId = EffectTable.MsgStdEffectWearsOff;
    }

    // effects::LoadEffectsParameters: data/status_effects.yaml, whose exclusion_group/negative/block/remove name other
    // effects by key. The effect scripts are loaded when first called rather than up front.
    public sealed class EffectTable
    {
        public const ushort MsgStdEffectWearsOff = 206;

        public readonly EffectParams[] Params = new EffectParams[EffectId.Max];

        public EffectParams this[ushort id] => Params[id];

        public static EffectTable Load(string dataDir, DataEnums enums)
        {
            var t = new EffectTable();
            for (int i = 0; i < EffectId.Max; i++) t.Params[i] = new EffectParams();

            var flagValues = new Dictionary<string, uint>();
            var flagMap = MiniYaml.Load(Path.Combine(dataDir, "enums", "status_effect_flag.yaml")).MapOrNull("values");
            if (flagMap != null)
                foreach (var (k, v) in flagMap) flagValues[k] = ParseUInt((string)v);

            var effects = MiniYaml.Load(Path.Combine(dataDir, "status_effects.yaml")).Map("status_effects");
            var ids = new Dictionary<string, ushort>();
            foreach (var (key, v) in effects) ids[key] = (ushort)ParseUInt(((Dictionary<string, object>)v).Str("id"));
            ushort Ref(Dictionary<string, object> e, string field) => e.Str(field) is string k ? ids[k] : (ushort)0;

            foreach (var (key, v) in effects)
            {
                var e = (Dictionary<string, object>)v;
                ushort id = ids[key];
                if (id >= EffectId.Max) continue;
                var p = t.Params[id];
                p.Name = e.Str("name", key);
                if (e.TryGetValue("flags", out var flags) && flags is List<object> list)
                    foreach (var f in list) p.Flag |= flagValues[(string)f];
                p.Type = Ref(e, "exclusion_group");
                p.NegativeId = Ref(e, "negative");
                p.BlockId = Ref(e, "block");
                p.RemoveId = Ref(e, "remove");
                p.Overwrite = (EffectOverwrite)enums.Resolve("effect_overwrite", e.Str("overwrite"));
                p.Element = (byte)enums.Resolve("element", e.Str("element"));
                p.MinDurationMs = 1000L * ParseUInt(e.Str("min_duration", "0"));
                uint sortKey = ParseUInt(e.Str("sort_key", "0"));
                p.SortKey = sortKey == 0 ? (ushort)10000 : (ushort)sortKey;
                uint wearOff = ParseUInt(e.Str("wear_off_message_id", "0"));
                p.WearOffMessageId = wearOff == 0 ? MsgStdEffectWearsOff : (ushort)wearOff;
            }
            return t;
        }

        private static uint ParseUInt(string s) =>
            s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? uint.Parse(s.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                : uint.Parse(s, CultureInfo.InvariantCulture);
    }

    // CStatusEffect. Times are NowMs() milliseconds.
    public sealed class StatusEffect
    {
        public ushort Id;
        public uint SubId;
        public ushort Icon, Power, SubPower, SubIcon, Tier;
        public uint Flags;
        public uint OriginId;
        public ushort SourceType;
        public uint SourceTypeParam;
        public ushort Type;
        public byte Slot;
        public long TickMs, DurationMs, StartMs;
        public int TickCount;
        public string Name = "";
        public bool Deleted;
        public BattleEntity Owner;
        public readonly List<(Mod Mod, short Amount)> Mods = new List<(Mod, short)>();

        public StatusEffect(ushort id, ushort icon, ushort power, long tickMs, long durationMs, uint subId = 0, ushort subPower = 0,
                            ushort subIcon = 0, ushort tier = 0, uint flags = 0, ushort sourceType = 0, uint sourceTypeParam = 0,
                            uint originId = 0, byte slot = 0)
        {
            Id = id;
            Icon = icon;
            Power = power;
            TickMs = tickMs;
            DurationMs = durationMs;
            SubId = subId;
            SubPower = subPower;
            SubIcon = subIcon;
            Tier = tier;
            Flags = flags;
            SourceType = sourceType;
            SourceTypeParam = sourceTypeParam;
            OriginId = originId;
            Slot = slot;
        }

        public bool HasFlag(uint flag) => (Flags & flag) != 0;

        // CStatusEffect::MarkPersistDirty: a char's char_effects is rewritten on the next persist sweep
        public void MarkPersistDirty()
        {
            if (Owner is CharData c) c.Persist |= CharPersist.Effects;
        }

        // SetIcon / SetSubIcon do nothing (beyond a warning) until the effect has an owner
        public void SetIcon(ushort icon)
        {
            if (Owner == null) return;
            Icon = icon;
            MarkPersistDirty();
            Owner.StatusEffects.UpdateStatusIcons();
        }

        public void SetSubIcon(ushort subIcon)
        {
            if (Owner == null) return;
            SubIcon = subIcon;
            Owner.StatusEffects.UpdateStatusIcons();
        }

        public void SetStartTime(long ms)
        {
            TickCount = 0;
            StartMs = ms;
        }

        // the owner gets the mod at once when the effect is already applied
        public void AddMod(Mod mod, short amount)
        {
            if (Owner != null) Owner.AddModifier(mod, amount);
            for (int i = 0; i < Mods.Count; i++)
            {
                if (Mods[i].Mod != mod) continue;
                Mods[i] = (mod, (short)(Mods[i].Amount + amount));
                return;
            }
            Mods.Add((mod, amount));
        }

        public StatusEffect Copy() =>
            new StatusEffect(Id, Icon, Power, TickMs, DurationMs, SubId, SubPower, SubIcon, Tier, Flags, SourceType, SourceTypeParam, OriginId);
    }

    // what the container reaches outside itself: the scripts, the wear-off message, the clock
    internal interface IStatusEffectHost
    {
        long NowMs();
        string EffectScriptName(StatusEffect e, EffectParams p);
        void OnEffectGain(BattleEntity c, StatusEffect e);
        void OnEffectTick(BattleEntity c, StatusEffect e);
        void HandleAura(BattleEntity owner, StatusEffect aura);
        void OnEffectLose(BattleEntity c, StatusEffect e);
        void EffectWornOff(BattleEntity c, StatusEffect e, ushort messageId);
        void EffectListener(BattleEntity c, string eventName, StatusEffect e); // EFFECT_GAIN / EFFECT_LOSE / EFFECTS_TICK
        void EffectGone(StatusEffect e); // out of the list for good
        void ManeuverChange(BattleEntity c, ushort effectId, bool gain); // a char's maneuver to its automaton's attachments
        void CharmedDespawnPet(CharData c); // HandleEffectGainSideEffects: a charmed char's pet goes (petutils::DespawnPet)
    }

    // CStatusEffectContainer for any battle entity (a char, a mob, a pet or a trust). Effects stay in the list, marked
    // deleted, until DeleteStatusEffects sweeps them (on expiry, the tick, or a zone change); until then their icons
    // still count. Only a char keeps icons (its own 0x063 and the party's 0x076: a pet's or trust's effects have none),
    // char_effects rows and latents; a pet or trust tells only the effect's origin of a wear-off.
    public sealed class StatusEffectContainer
    {
        private readonly BattleEntity owner_;
        private readonly List<StatusEffect> effects_ = new List<StatusEffect>();
        internal IStatusEffectHost Host;
        internal EffectTable Table;

        public readonly byte[] Icons = new byte[32];
        public ulong Bits;

        private const ushort ElementLight = 7, ElementDark = 8;

        // CBattleEntity::hasImmunity: only a mob's (TYPE_MOB) or a pet's (TYPE_PET) count, never a trust's
        private bool HasImmunity(uint immunity) => owner_ is MobEntity m and not TrustEntity && (m.Immunity & immunity) != 0;

        // CBattleEntity::isDead: no HP, gone (Disappear), or in the death or despawn state (a mob, pet or trust)
        internal static bool IsDead(BattleEntity e) => e is MobEntity m
            ? m.Hp <= 0 || m.Status == NpcEntity.StatusDisappear || m.DeathAtMs != 0 || m.FadeAtMs != 0
            : e.Hp <= 0;

        public StatusEffectContainer(BattleEntity owner)
        {
            owner_ = owner;
            Array.Fill(Icons, (byte)0xFF);
        }

        public IReadOnlyList<StatusEffect> All => effects_;

        // statusOrdering: sort key, slot, newest maneuver first, id
        private int Compare(StatusEffect a, StatusEffect b)
        {
            ushort ak = Table[a.Id].SortKey, bk = Table[b.Id].SortKey;
            if (ak != bk) return ak.CompareTo(bk);
            if (a.Slot != b.Slot) return a.Slot.CompareTo(b.Slot);
            static bool ByStart(ushort id) => id >= EffectId.FireManeuver && id <= EffectId.DarkManeuver;
            if (ByStart(a.Id) && ByStart(b.Id) && a.StartMs != b.StartMs) return b.StartMs.CompareTo(a.StartMs);
            return a.Id.CompareTo(b.Id);
        }

        // multiset insert: after any equal element
        private void Insert(StatusEffect e)
        {
            int i = effects_.Count;
            while (i > 0 && Compare(e, effects_[i - 1]) < 0) i--;
            effects_.Insert(i, e);
        }

        private StatusEffect[] Snapshot() => effects_.ToArray();

        public int GetEffectsCount(ushort id)
        {
            int n = 0;
            foreach (var e in effects_) if (e.Id == id && !e.Deleted) n++;
            return n;
        }

        public int GetEffectsCountWithFlag(uint flag)
        {
            int n = 0;
            foreach (var e in effects_) if (e.HasFlag(flag) && e.DurationMs > 0 && !e.Deleted) n++;
            return n;
        }

        // CStatusEffectContainer::ApplyBardEffect: a song of the same id and tier is overwritten in its slot; a
        // bard's songs on the target (the song's SubId names its singer) are held to maxSongs by overwriting the one
        // that ends first. As in C++, a song just overwritten still counts (it is only marked deleted).
        public bool ApplyBardEffect(StatusEffect e, int maxSongs)
        {
            int numOfEffects = 0;
            StatusEffect oldestSong = null;
            foreach (var existing in Snapshot())
            {
                if (existing.Id < EffectId.Requiem || existing.Id > EffectId.Nocturne) continue; // is an active brd effect
                if (existing.Tier == e.Tier && existing.Id == e.Id)
                {
                    // same tier/type, overwrite: use same slot as the one it replaces
                    e.Slot = existing.Slot;
                    DelStatusEffectByTier(e.Id, e.Tier);
                }
                if (existing.SubId == e.SubId)
                {
                    // YOUR BRD effect
                    numOfEffects++;
                    if (oldestSong == null || existing.StartMs + existing.DurationMs < oldestSong.StartMs + oldestSong.DurationMs) oldestSong = existing;
                }
            }

            if (numOfEffects < maxSongs)
            {
                // use lowest available slot, unless it's replacing an existing song
                if (e.Slot == 0) e.Slot = GetLowestFreeSlot();
                AddStatusEffect(e);
                return true;
            }
            if (oldestSong != null)
            {
                // overwrite oldest
                e.Slot = oldestSong.Slot;
                DelStatusEffectByTier(oldestSong.Id, oldestSong.Tier);
                AddStatusEffect(e);
                return true;
            }
            return false;
        }

        public byte GetLowestFreeSlot()
        {
            byte lowest = 1;
            foreach (var e in effects_)
            {
                if (e.Slot == lowest && !e.Deleted) lowest++;
                else if (e.Slot > lowest) break;
            }
            return lowest;
        }

        // CStatusEffectContainer::CanGainStatusEffect: the immunities (a mob's or pet's), no charm on a pet (anything with
        // a master), the blocking and the negative effect, then the overwrite rule against the one already there
        public bool CanGainStatusEffect(StatusEffect e)
        {
            switch (e.Id)
            {
                case EffectId.SleepI or EffectId.SleepII or EffectId.Lullaby:
                    if (e.SubPower == ElementLight && HasImmunity(Immunity.LightSleep)) return false;
                    if (e.SubPower == ElementDark && HasImmunity(Immunity.DarkSleep)) return false;
                    break;
                case EffectId.Weight: if (HasImmunity(Immunity.Gravity)) return false; break;
                case EffectId.Bind: if (HasImmunity(Immunity.Bind)) return false; break;
                case EffectId.Stun: if (HasImmunity(Immunity.Stun)) return false; break;
                case EffectId.Silence: if (HasImmunity(Immunity.Silence)) return false; break;
                case EffectId.Paralysis: if (HasImmunity(Immunity.Paralyze)) return false; break;
                case EffectId.Blindness: if (HasImmunity(Immunity.Blind)) return false; break;
                case EffectId.Slow: if (HasImmunity(Immunity.Slow)) return false; break;
                case EffectId.Poison: if (HasImmunity(Immunity.Poison)) return false; break;
                case EffectId.Elegy: if (HasImmunity(Immunity.Elegy)) return false; break;
                case EffectId.Requiem: if (HasImmunity(Immunity.Requiem)) return false; break;
                case EffectId.Terror: if (HasImmunity(Immunity.Terror)) return false; break;
                case EffectId.Petrification: if (HasImmunity(Immunity.Petrify)) return false; break;
                case EffectId.CurseI or EffectId.CurseII: if (HasImmunity(Immunity.Curse)) return false; break;
                case EffectId.BlazeSpikes or EffectId.IceSpikes or EffectId.ShockSpikes or EffectId.Reprisal or EffectId.DelugeSpikes
                    or EffectId.GaleSpikes or EffectId.GlintSpikes or EffectId.DamageSpikes or EffectId.DreadSpikes or EffectId.ClodSpikes:
                    // Geirskogul aftermath
                    if (GetStatusEffect(EffectId.Aftermath) is StatusEffect am && (am.Power == 8 || am.Power == 22)) return false;
                    break;
            }

            // make sure pets can't be charmed
            if ((e.Id == EffectId.CharmI || e.Id == EffectId.CharmII) && owner_.PMaster != null) return false;

            var p = Table[e.Id];
            if (p.BlockId != 0 && HasStatusEffect(p.BlockId)) return false;

            if (p.NegativeId != 0 && GetStatusEffect(p.NegativeId) is StatusEffect negative)
            {
                if (e.Id == EffectId.Haste && negative.Id == EffectId.Slow && negative.SubPower == 1) return true; // slow i remote
                if (e.Tier != 0 && negative.Tier != 0)
                    return e.Tier == negative.Tier ? e.Id > p.NegativeId : e.Tier > negative.Tier;
                return e.Power >= negative.Power;
            }

            if (GetStatusEffect(e.Id) is not StatusEffect existing) return true;
            switch (p.Overwrite)
            {
                case EffectOverwrite.Always or EffectOverwrite.IgnoreDuplicate:
                    return true;
                case EffectOverwrite.EqualHigher:
                    if (e.Tier != 0 && existing.Tier != 0) return e.Tier >= existing.Tier;
                    return e.Power >= existing.Power;
                case EffectOverwrite.Higher:
                    if (e.Tier != 0 && existing.Tier != 0) return e.Tier > existing.Tier;
                    return e.Power > existing.Power;
                case EffectOverwrite.TierHigher:
                    if (e.Tier != 0 && existing.Tier != 0) return e.Tier > existing.Tier;
                    return false;
                default:
                    return false;
            }
        }

        private void OverwriteStatusEffect(StatusEffect e)
        {
            var p = Table[e.Id];
            if (p.Overwrite != EffectOverwrite.IgnoreDuplicate) DelStatusEffectSilent(e.Id);
            if (p.RemoveId > EffectId.Ko) DelStatusEffectSilent(p.RemoveId);
            if (p.NegativeId > EffectId.Ko) DelStatusEffectSilent(p.NegativeId);
        }

        // AddStatusEffect: the notice is not used on the way in
        public bool AddStatusEffect(StatusEffect e, EffectNotice notice = EffectNotice.ShowMessage)
        {
            if (Table == null || e.Id >= EffectId.Max || !CanGainStatusEffect(e)) return false;

            var p = Table[e.Id];
            if (e.DurationMs < p.MinDurationMs) e.DurationMs = p.MinDurationMs;
            OverwriteStatusEffect(e);
            SetEffectParams(e);
            DelStatusEffectsByType(e.Type);
            e.SetStartTime(Host?.NowMs() ?? 0);
            Insert(e);

            HandleEffectGainSideEffects(e);
            Host?.OnEffectGain(owner_, e);
            Host?.EffectListener(owner_, "EFFECT_GAIN", e);
            // the owner after the gain script, so its effect:addMod() is not applied twice
            e.Owner = owner_;
            foreach (var (mod, amount) in e.Mods) owner_.AddModifier(mod, amount);
            if (owner_ is CharData) Host?.ManeuverChange(owner_, e.Id, true);

            if (owner_.MaxHp != 0) CharStats.UpdateHealth(owner_);
            if (owner_ is CharData gainer) gainer.Persist |= CharPersist.Effects;
            if (owner_ is CharData && e.Icon != 0) UpdateStatusIcons();
            if (owner_ is CharData pc && owner_.MaxHp != 0) // not in the middle of logging in
            {
                CheckLatents(pc);
                CharStats.UpdateHealth(owner_);
            }
            owner_.UpdateMask |= CharStats.UpdateHp;
            return true;
        }

        // CStatusEffectContainer::HandleEffectGainSideEffects: a living char charmed lets its pet go; a sleep that comes
        // with a prevent-action effect would take the Sleep I icon, but SetIcon on an effect with no owner yet does nothing
        private void HandleEffectGainSideEffects(StatusEffect e)
        {
            if (IsDead(owner_)) return;
            // this should actually go into a char charm AI
            if (owner_ is CharData c && (e.Id == EffectId.CharmI || e.Id == EffectId.CharmII) && c.PPet != null) Host?.CharmedDespawnPet(c);
            if (HasPreventActionEffect(false) && (e.Id == EffectId.SleepII || e.Id == EffectId.Lullaby)) e.SetIcon(EffectId.SleepI);
        }

        private void SetEffectParams(StatusEffect e)
        {
            if (e.Id == EffectId.None && e.SubId == 0) return; // "None-type Effect has SubID of 0"
            var p = Table[e.Id];
            e.Name = Host?.EffectScriptName(e, p) ?? "effects/" + p.Name;
            e.Flags |= p.Flag;
            e.Type = p.Type;
        }

        public void DeleteStatusEffects()
        {
            bool icons = false, removed = false;
            for (int i = effects_.Count - 1; i >= 0; i--)
            {
                if (!effects_[i].Deleted) continue;
                if (effects_[i].Icon != 0) icons = true;
                Host?.EffectGone(effects_[i]);
                effects_.RemoveAt(i);
                removed = true;
            }
            if (!removed) return;
            if (icons) UpdateStatusIcons();
            if (owner_ is CharData pc) CheckLatents(pc);
            CharStats.UpdateHealth(owner_);
        }

        // the latents an effect's gain or loss can change (CLatentEffectContainer::CheckLatentsFoodEffect /
        // CheckLatentsStatusEffect / CheckLatentsRollSong)
        private static void CheckLatents(CharData c)
        {
            c.Latents.CheckLatentsFoodEffect();
            c.Latents.CheckLatentsStatusEffect();
            c.Latents.CheckLatentsRollSong();
        }

        public void RemoveStatusEffect(StatusEffect e, EffectNotice notice = EffectNotice.ShowMessage)
        {
            if (e.Deleted) return;
            e.Deleted = true;
            Host?.OnEffectLose(owner_, e);
            Host?.EffectListener(owner_, "EFFECT_LOSE", e);
            foreach (var (mod, amount) in e.Mods) owner_.DelModifier(mod, amount);
            if (owner_ is CharData loser) loser.Persist |= CharPersist.Effects;
            if (notice != EffectNotice.Silent && e.Icon != 0 && !e.HasFlag(EffectFlag.NoLossMessage))
                Host?.EffectWornOff(owner_, e, e.Id < EffectId.Max ? Table[e.Id].WearOffMessageId : EffectTable.MsgStdEffectWearsOff);
            if (owner_ is CharData) Host?.ManeuverChange(owner_, e.Id, false);
        }

        private bool DelFirst(Func<StatusEffect, bool> match, EffectNotice notice)
        {
            foreach (var e in Snapshot())
            {
                if (e.Deleted || !match(e)) continue;
                RemoveStatusEffect(e, notice);
                return true;
            }
            return false;
        }

        public bool DelStatusEffect(ushort id) => DelFirst(e => e.Id == id, EffectNotice.ShowMessage);
        public bool DelStatusEffectSilent(ushort id) => DelFirst(e => e.Id == id, EffectNotice.Silent);
        public bool DelStatusEffect(ushort id, uint subId) => DelFirst(e => e.Id == id && e.SubId == subId, EffectNotice.ShowMessage);

        public bool DelStatusEffectBySource(ushort id, ushort sourceType, uint sourceTypeParam) =>
            DelFirst(e => e.Id == id && e.SourceType == sourceType && e.SourceTypeParam == sourceTypeParam, EffectNotice.ShowMessage);

        public bool DelStatusEffectByTier(ushort id, ushort tier) => DelFirst(e => e.Id == id && e.Tier == tier, EffectNotice.Silent);

        // the effects with a duration go at once, with no message and no icon refresh
        public void KillAllStatusEffect()
        {
            foreach (var e in Snapshot())
            {
                if (e.DurationMs == 0) continue;
                Host?.OnEffectLose(owner_, e);
                foreach (var (mod, amount) in e.Mods) owner_.DelModifier(mod, amount);
                effects_.Remove(e);
                Host?.EffectGone(e);
            }
            CharStats.UpdateHealth(owner_);
        }

        // what the client may cancel (0x0F1): every effect with the icon but the no_cancel ones
        public void DelStatusEffectsByIcon(ushort icon)
        {
            foreach (var e in Snapshot())
                if (e.Icon == icon && !e.HasFlag(EffectFlag.NoCancel)) RemoveStatusEffect(e);
        }

        public void DelStatusEffectsByType(ushort type)
        {
            if (type == 0) return;
            foreach (var e in Snapshot())
                if (e.Type == type) RemoveStatusEffect(e, EffectNotice.Silent);
        }

        public void DelStatusEffectsByFlag(uint flag, EffectNotice notice = EffectNotice.ShowMessage)
        {
            foreach (var e in Snapshot())
            {
                if (!e.HasFlag(flag)) continue;
                // a Diabolos NM Nightmare (tier 5) only goes by an explicit cure
                if ((flag & EffectFlag.Damage) != 0 && e.Id == EffectId.SleepI && e.Tier >= 5) continue;
                RemoveStatusEffect(e, notice);
            }
        }

        private List<StatusEffect> Active(Func<StatusEffect, bool> match)
        {
            var list = new List<StatusEffect>();
            foreach (var e in effects_)
                if (match(e) && e.DurationMs > 0 && !e.Deleted) list.Add(e);
            return list;
        }

        public ushort EraseStatusEffect(Random rng)
        {
            var list = Active(e => e.HasFlag(EffectFlag.Erasable));
            if (list.Count == 0) return EffectId.None;
            var e = list[rng.Next(list.Count)];
            RemoveStatusEffect(e);
            return e.Id;
        }

        public ushort HealingWaltz(Random rng)
        {
            var list = Active(e => e.HasFlag(EffectFlag.Waltzable) || e.HasFlag(EffectFlag.Erasable));
            if (list.Count == 0) return EffectId.None;
            var e = list[rng.Next(list.Count)];
            RemoveStatusEffect(e);
            return e.Id;
        }

        public int EraseAllStatusEffect()
        {
            var list = Active(e => e.HasFlag(EffectFlag.Erasable));
            foreach (var e in list) RemoveStatusEffect(e);
            return list.Count;
        }

        public ushort DispelStatusEffect(uint flag, Random rng)
        {
            var list = Active(e => e.HasFlag(flag));
            if (list.Count == 0) return EffectId.None;
            var e = list[rng.Next(list.Count)];
            RemoveStatusEffect(e, EffectNotice.Silent);
            return e.Id;
        }

        public int DispelAllStatusEffect(uint flag)
        {
            var list = Active(e => e.HasFlag(flag));
            foreach (var e in list) RemoveStatusEffect(e, EffectNotice.Silent);
            return list.Count;
        }

        // a copy of a random effect with the flag, which is removed
        public StatusEffect StealStatusEffect(uint flag, EffectNotice notice, Random rng)
        {
            var list = Active(e => e.HasFlag(flag));
            if (list.Count == 0) return null;
            var old = list[rng.Next(list.Count)];
            var copy = old.Copy();
            RemoveStatusEffect(old, notice);
            return copy;
        }

        public bool HasStatusEffect(ushort id) => GetStatusEffect(id) != null;
        public bool HasStatusEffectWithSubId(ushort id, uint subId) => GetStatusEffect(id, subId) != null;

        public bool HasStatusEffect(params ushort[] ids)
        {
            foreach (var e in effects_)
                if (!e.Deleted && Array.IndexOf(ids, e.Id) >= 0) return true;
            return false;
        }

        public bool HasStatusEffectByFlag(uint flag)
        {
            foreach (var e in effects_) if (e.HasFlag(flag) && !e.Deleted) return true;
            return false;
        }

        public StatusEffect GetStatusEffect(ushort id)
        {
            foreach (var e in effects_) if (e.Id == id && !e.Deleted) return e;
            return null;
        }

        public StatusEffect GetStatusEffect(ushort id, uint subId)
        {
            foreach (var e in effects_) if (e.Id == id && e.SubId == subId && !e.Deleted) return e;
            return null;
        }

        public StatusEffect GetStatusEffectBySource(ushort id, ushort sourceType, uint sourceTypeParam)
        {
            foreach (var e in effects_)
                if (e.Id == id && e.SourceType == sourceType && e.SourceTypeParam == sourceTypeParam && !e.Deleted) return e;
            return null;
        }

        // the first 32 iconed effects, deleted ones included: icon & 0xFF, and two bits each for icons 256+ and 512+
        public void UpdateStatusIcons()
        {
            if (owner_ is not CharData c) return;
            Bits = 0;
            Array.Fill(Icons, (byte)0xFF);
            int count = 0;
            foreach (var e in effects_)
            {
                if (e.Icon == 0) continue;
                if (e.Icon >= 256 && e.Icon < 512) Bits |= 1UL << (count * 2);
                if (e.Icon >= 512) Bits |= 1UL << (count * 2 + 1);
                Icons[count] = (byte)e.Icon;
                if (++count == 32) break;
            }
            c.EffectsChanged = true;
            if (c.Party != null) c.Party.EffectsChanged = true;
        }

        // ---- persistence -------------------------------------------------------------------------------------

        // BuildPersistRows: what char_effects keeps across the transition. Times are in seconds; the timestamp is the
        // start in unix time.
        public List<PersistedEffect> BuildPersistRows(bool logout, long nowMs, long unixNow)
        {
            var rows = new List<PersistedEffect>();
            foreach (var e in effects_)
            {
                if (logout ? e.HasFlag(EffectFlag.Logout) : e.HasFlag(EffectFlag.OnZone)) continue;
                if (e.Deleted) continue;
                long durationSec = FloorDiv(e.DurationMs, 1000);
                long realSec = FloorDiv(e.StartMs + e.DurationMs - nowMs, 1000);
                if (!(realSec > 0 || durationSec == 0)) continue;

                // the shadows left
                if (e.Id == EffectId.CopyImage) e.SubPower = (ushort)owner_.GetMod(Mod.UTSUSEMI);
                else if (e.Id == EffectId.Blink) e.Power = (ushort)owner_.GetMod(Mod.BLINK);

                uint duration = 0;
                if (durationSec > 0)
                {
                    if (e.HasFlag(EffectFlag.OfflineTick)) duration = (uint)durationSec;
                    else if (realSec > 0) duration = (uint)realSec;
                    else continue;
                }
                rows.Add(new PersistedEffect
                {
                    EffectId = e.Id, Icon = e.Icon, Power = e.Power, Tick = (uint)FloorDiv(e.TickMs, 1000), Duration = duration,
                    SubId = e.SubId, SubPower = e.SubPower, Tier = e.Tier, Flags = e.Flags,
                    Timestamp = (uint)(unixNow + FloorDiv(e.StartMs - nowMs, 1000)),
                    SourceType = e.SourceType, SourceTypeParam = e.SourceTypeParam, OriginId = e.OriginId,
                });
            }
            return rows;
        }

        // DropEffectsForTransition: the logout (or zone) flagged effects go silently
        public void DropEffectsForTransition(bool logout)
        {
            foreach (var e in Snapshot())
                if (logout ? e.HasFlag(EffectFlag.Logout) : e.HasFlag(EffectFlag.OnZone)) RemoveStatusEffect(e, EffectNotice.Silent);
            DeleteStatusEffects();
        }

        private static long FloorDiv(long a, long b) => (long)Math.Floor((double)a / b);

        // ---- ticks -------------------------------------------------------------------------------------------

        public void CheckEffectsExpiry(long now)
        {
            foreach (var e in Snapshot())
                if (e.DurationMs != 0 && e.StartMs + e.DurationMs <= now) RemoveStatusEffect(e);
            DeleteStatusEffects();
        }

        public void TickEffects(long now)
        {
            if (!IsDead(owner_))
            {
                foreach (var e in Snapshot())
                {
                    if (e.TickMs == 0 || e.TickCount >= (now - e.StartMs) / e.TickMs) continue;
                    if (e.HasFlag(EffectFlag.Aura)) Host?.HandleAura(owner_, e);
                    e.TickCount++;
                    Host?.OnEffectTick(owner_, e);
                }
            }
            DeleteStatusEffects();
            Host?.EffectListener(owner_, "EFFECTS_TICK", null);
        }

        // battleutils::HandleStoneskin
        public int HandleStoneskin(int damage)
        {
            if (damage <= 0 || GetStatusEffect(EffectId.Stoneskin) is not StatusEffect stoneskin) return damage;
            if (stoneskin.Power > damage)
            {
                stoneskin.Power = (ushort)(stoneskin.Power - damage);
                return 0;
            }
            DelStatusEffect(EffectId.Stoneskin);
            return damage - stoneskin.Power;
        }

        public bool HasPreventActionEffect(bool ignoreCharm = false) =>
            ignoreCharm
                ? HasStatusEffect(EffectId.SleepI, EffectId.SleepII, EffectId.Petrification, EffectId.Lullaby, EffectId.Penalty, EffectId.Stun, EffectId.Terror)
                : HasStatusEffect(EffectId.SleepI, EffectId.SleepII, EffectId.Petrification, EffectId.Lullaby, EffectId.CharmI, EffectId.CharmII,
                                  EffectId.Penalty, EffectId.Stun, EffectId.Terror);

        public ushort GetConfrontationEffect()
        {
            foreach (var e in effects_) if (e.HasFlag(EffectFlag.Confrontation)) return e.Power;
            return 0;
        }

        // CopyConfrontationEffect: a copy of each confrontation-flagged effect onto the entity
        public void CopyConfrontationEffect(BattleEntity target)
        {
            foreach (var e in effects_.ToArray())
                if (e.HasFlag(EffectFlag.Confrontation)) target.StatusEffects.AddStatusEffect(e.Copy());
        }

        public ushort GetConfrontationSubPower()
        {
            foreach (var e in effects_) if (e.HasFlag(EffectFlag.Confrontation)) return e.SubPower;
            return 0;
        }

        public bool IsAsleep() => HasStatusEffect(EffectId.SleepI, EffectId.SleepII, EffectId.Lullaby);

        public void WakeUp()
        {
            DelStatusEffect(EffectId.SleepI);
            DelStatusEffect(EffectId.SleepII);
            DelStatusEffect(EffectId.Lullaby);
        }
    }

    // CharPersist: the parts of a char the persist sweep writes
    public static class CharPersist
    {
        public const byte Position = 0x01, Effects = 0x02;
    }

    // effects::PersistedEffect: one char_effects row
    public sealed class PersistedEffect
    {
        public ushort EffectId, Icon, Power, SubPower, Tier, SourceType;
        public uint Tick, Duration, SubId, Flags, Timestamp, SourceTypeParam, OriginId;
    }
}
