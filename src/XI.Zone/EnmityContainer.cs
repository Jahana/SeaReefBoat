using System;
using System.Collections.Generic;

namespace XI.Zone
{
    // CEnmityContainer: a mob's hate list, cumulative (CE) and volatile (VE) enmity per entity. Cures, SetActive,
    // SetCE / SetVE and the cure helpers are in EnmityContainer.Cure.cs. A new entry puts the holder in its owner's
    // notoriety container (NotorietyContainer.cs) and Clear takes it out. A charmed mob keeps a list of its own (a mob
    // hitting it lands there) and stands on others' lists like a pet (IgnorePets, withMaster); pets and trusts hold
    // none. LogoutReset lets a char that left go of its entries' owner, GetHighestEnmity finding it again by id.
    public sealed partial class EnmityContainer
    {
        public sealed class Entry
        {
            public BattleEntity Owner;
            public int CE, VE;
            public bool Active;
        }

        // DecayEnmity's per-tick VE loss: 60 / kLogicUpdateRate (2.5 ticks a second)
        public const int DecayPerTick = 24;

        private readonly MobEntity holder_;
        private readonly Dictionary<uint, Entry> list_ = new Dictionary<uint, Entry>();

        public int EnmityCap = 30000; // map.ENMITY_CAP

        public EnmityContainer(MobEntity holder) => holder_ = holder;

        public int Count => list_.Count;
        public IEnumerable<Entry> Entries => list_.Values;
        public bool HasId(uint id) => list_.ContainsKey(id);
        public Entry Get(uint id) => list_.GetValueOrDefault(id);
        public int CE(BattleEntity e) => list_.TryGetValue(e.Id, out var o) ? o.CE : 0;
        public int VE(BattleEntity e) => list_.TryGetValue(e.Id, out var o) ? o.VE : 0;

        // Clear: id 0 empties the list; the holder leaves each owner's notoriety
        public void Clear(uint id = 0)
        {
            if (id == 0)
            {
                foreach (var entry in list_.Values) entry.Owner?.Notoriety.Remove(holder_);
                list_.Clear();
                return;
            }
            if (list_.Remove(id, out var removed)) removed.Owner?.Notoriety.Remove(holder_);
            tameable_ = true;
        }

        // LogoutReset: the entry stays but loses its owner (CZoneEntities::DecreaseZoneCounter, the char's entity going
        // away); UpdateEnmity takes the owner back and GetHighestEnmity looks the char up by id
        public void LogoutReset(uint id)
        {
            if (list_.TryGetValue(id, out var o)) o.Owner = null;
        }

        // LowerEnmityByPercent: CE and VE cut by percent, what was cut handed to hateReceiver
        public void LowerEnmityByPercent(BattleEntity e, byte percent, BattleEntity hateReceiver)
        {
            if (!list_.TryGetValue(e.Id, out var o)) return;
            float mod = percent / 100.0f;
            int ce = (int)(o.CE * mod);
            o.CE -= Math.Max(ce, 0);
            int ve = (int)(o.VE * mod);
            o.VE -= Math.Max(ve, 0);
            if (hateReceiver != null) UpdateEnmity(hateReceiver, ce, ve);
        }

        // IgnorePets: a mob with MOBMOD_IGNORE_PETS hates no char's pet (nor a char's charmed mob)
        private bool IgnorePets(BattleEntity e) =>
            holder_.GetMobMod(MobMod.IgnorePets) != 0 && (e is PetEntity || e is MobEntity && e.PMaster is CharData);

        // AddBaseEnmity: a 0 / 0 entry, unless one is there already
        public void AddBaseEnmity(BattleEntity e)
        {
            if (!ZoneHost.SameArea(e, holder_) || IgnorePets(e)) return;
            list_.TryAdd(e.Id, new Entry { Owner = e, Active = true });
            e.Notoriety.Add(holder_);
        }

        // CalculateEnmityBonus: the ENMITY mod with a char's Enmity Increase / Decrease merits (and Muted Soul off it
        // under Souleater), -50..+100 percent
        private static float Bonus(BattleEntity e)
        {
            int enmityBonus = e.GetMod(Mod.ENMITY);
            if (e is CharData c && c.Merits != null)
            {
                enmityBonus += c.Merits.Value(c, MeritEnmityIncrease) - c.Merits.Value(c, MeritEnmityDecrease);
                if (c.StatusEffects.HasStatusEffect(EffectSouleater)) enmityBonus -= c.Merits.Value(c, MeritMutedSoul);
            }
            return (100.0f + Math.Clamp(enmityBonus, -50, 100)) / 100.0f;
        }

        // IsWithinEnmityRange: 25 yalms, 28 for an NM, in the same zone
        public bool InRange(BattleEntity e)
        {
            if (!ZoneHost.SameArea(e, holder_)) return false;
            float range = holder_.IsNotorious ? 28.0f : 25.0f;
            float dx = e.X - holder_.X, dy = e.Y - holder_.Y, dz = e.Z - holder_.Z;
            return dx * dx + dy * dy + dz * dz <= range * range;
        }

        // UpdateEnmity: out of range adds nothing; a first entry while nobody is active carries +200 CE / +900 VE. A
        // direct action brings the attacker's treasure hunter (8 at most for a THF main, 4 otherwise) and gilfinder.
        // withMaster: a pet's first entry brings its master in at base enmity (only the master, when pets are ignored).
        public void UpdateEnmity(BattleEntity e, int ce, int ve, bool directAction = true, bool withMaster = false, bool tameable = false)
        {
            // pets and trusts have no enmity of their own (a charmed mob does)
            if (holder_ is PetEntity or TrustEntity) return;
            if (IgnorePets(e))
            {
                if (withMaster && e.PMaster != null && ce >= 0 && ve >= 0) AddBaseEnmity(e.PMaster);
                return;
            }
            if (!InRange(e)) ce = ve = 0;
            if (directAction && holder_ is MobEntity mob)
            {
                short th = Math.Min((short)(e.MJob != MobStats.JobThf ? 4 : 8), e.GetMod(Mod.TREASURE_HUNTER));
                mob.ThLvl = Math.Max(mob.ThLvl, th);
                mob.GilfinderLevel = Math.Max(mob.GilfinderLevel, e.GetMod(Mod.GILFINDER));
            }
            float bonus = Bonus(e);
            if (list_.TryGetValue(e.Id, out var o))
            {
                o.Owner ??= e;
                int newCe = (int)(o.CE + (ce > 0 ? ce * bonus : ce));
                int newVe = (int)(o.VE + (ve > 0 ? ve * bonus : ve));
                o.CE = Math.Clamp(newCe, 0, EnmityCap);
                o.VE = Math.Clamp(newVe, 0, EnmityCap);
                if (ce >= 0 && ve >= 0) o.Active = true;
            }
            else if (ce >= 0 && ve >= 0)
            {
                bool initial = true;
                foreach (var entry in list_.Values)
                    if (entry.Active)
                    {
                        initial = false;
                        break;
                    }
                if (initial)
                {
                    ce += 200;
                    ve += 900;
                }
                ce = Math.Clamp((int)(ce * bonus), 0, EnmityCap);
                ve = Math.Clamp((int)(ve * bonus), 0, EnmityCap);
                list_[e.Id] = new Entry { Owner = e, CE = ce, VE = ve, Active = true };
                e.Notoriety.Add(holder_);
                // add master to the enmity list (pet and charmed mob)
                if (withMaster && e.PMaster != null && (e is PetEntity || e is MobEntity && e.PMaster is CharData)) AddBaseEnmity(e.PMaster);
            }
            if (!tameable) tameable_ = false;
        }

        // battleutils::GetEnmityModDamage
        public static int EnmityModDamage(int level) => level * 31 / 50 + 6;

        // UpdateEnmityFromDamage: damage dealt to the holder, scaled by the holder's level
        public void UpdateEnmityFromDamage(BattleEntity e, int damage)
        {
            if (e.Id == holder_.Id) return;
            damage = Math.Max(damage, 1);
            int mod = EnmityModDamage(holder_.MLvl);
            UpdateEnmity(e, (int)(80.0f / mod * damage), (int)(240.0f / mod * damage));
            holder_.HiPcLvl = Math.Max(holder_.HiPcLvl, e.MLvl);
        }

        // UpdateEnmityFromAttack: the holder hitting someone takes CE off them, by the share of their max HP
        // UpdateEnmityFromCover: a covered blow puts 200 CE on the cover user and takes 10% off the covered target
        public void UpdateEnmityFromCover(BattleEntity coverTarget, BattleEntity coverUser)
        {
            if (coverTarget == null || coverUser == null) return;
            SetCE(coverUser, CE(coverUser) + 200);
            LowerEnmityByPercent(coverTarget, 10, null);
        }

        public void UpdateEnmityFromAttack(BattleEntity e, int damage)
        {
            if (!list_.TryGetValue(e.Id, out var o) || e.MaxHp <= 0) return;
            float reduction = (100.0f - Math.Min(e.GetMod(Mod.ENMITY_LOSS_REDUCTION), (short)100)) / 100.0f;
            int ce = (int)(-1800.0f * damage / e.MaxHp * reduction);
            o.CE = Math.Clamp(o.CE + ce, 0, EnmityCap);
        }

        // GetHighestEnmity: the most CE + VE among the active entries of another allegiance (an entry without its owner,
        // after LogoutReset, whatever its allegiance), a tie going to the holder's battle target; an entry without its
        // owner is the char of its id if one is logged in (zoneutils::GetChar: charById); an owner that is gone (valid
        // says no) is struck off and the search runs again
        public BattleEntity GetHighestEnmity(Func<BattleEntity, bool> valid, Func<uint, BattleEntity> charById = null)
        {
            while (list_.Count > 0)
            {
                uint highest = 0, bestId = 0;
                Entry best = null;
                foreach (var (id, o) in list_)
                {
                    uint enmity = (uint)(o.CE + o.VE);
                    if (enmity < highest || !o.Active || o.Owner != null && AllegianceOf(o.Owner) == holder_.Allegiance) continue;
                    if (enmity == highest && best != null && holder_.BattleTarget != null && best.Owner == holder_.BattleTarget) continue;
                    highest = enmity;
                    best = o;
                    bestId = id;
                }
                if (best == null) return null;
                var owner = best.Owner ?? charById?.Invoke(bestId);
                if (owner != null && valid(owner)) return owner;
                list_.Remove(bestId);
            }
            return null;
        }

        // a pet or trust takes its master's allegiance when it spawns (petutils / trustutils)
        public static int AllegianceOf(BattleEntity e) => e switch
        {
            MobEntity m => m.Allegiance,
            CharData c => c.Allegiance,
            { PMaster: { } master } => AllegianceOf(master),
            _ => CharData.AllegiancePlayer,
        };

        // DecayEnmity: each logic tick takes 24 VE off everyone
        public void DecayEnmity()
        {
            foreach (var o in list_.Values) o.VE -= Math.Min(o.VE, DecayPerTick);
        }
    }
}
