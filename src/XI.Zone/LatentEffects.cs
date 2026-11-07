using System;
using System.Collections.Generic;
using XI.Common;
using XI.Map;

namespace XI.Zone
{
    // xi::Latent (enums/latent.h): an item_latents row's latentId
    public static class Latent
    {
        public const ushort HpUnderPercent = 0, HpOverPercent = 1, HpUnderTpUnder100 = 2, HpOverTpUnder100 = 3, MpUnderPercent = 4,
                            MpUnder = 5, TpUnder = 6, TpOver = 7, Subjob = 8, PetId = 9, WeaponDrawn = 10, WeaponSheathed = 11,
                            SignetBonus = 12, StatusEffectActive = 13, NoFoodActive = 14, PartyMembers = 15, PartyMembersInZone = 16,
                            SanctionRegenBonus = 17, SanctionRefreshBonus = 18, SigilRegenBonus = 19, SigilRefreshBonus = 20,
                            AvatarInParty = 21, JobInParty = 22, Zone = 23, SynthTrainee = 24, SongRollActive = 25, TimeOfDay = 26,
                            HourOfDay = 27, Firesday = 28, Earthsday = 29, Watersday = 30, Windsday = 31, Darksday = 32, Iceday = 34,
                            Lightningsday = 35, Lightsday = 36, MoonPhase = 37, JobMultiple = 38, JobMultipleAtNight = 39,
                            EquippedInSlot = 40, DuringWs = 41, WeatherCondition = 42, WeaponDrawnHpUnder = 43, NationCitizen = 44,
                            MpUnderVisibleGear = 45, HpOverVisibleGear = 46, WeaponBroken = 47, InDynamis = 48, FoodActive = 49,
                            JobLevelBelow = 50, JobLevelAbove = 51, WeatherElement = 52, NationControl = 53, ZoneHomeNation = 54,
                            MpOver = 55, WeaponDrawnMpOver = 56, ElevenRollActive = 57, InAssault = 58, VsEcosystem = 59,
                            VsSpecies = 60, VsFamily = 61, Mainjob = 62, InAdoulin = 63, InGarrison = 64, SanctionFoodBonus = 65,
                            SigilFoodBonus = 66;
    }

    // what the latent container reaches outside the char: the stat rebuilds, the client, the clock, the zone's weather
    // and region, conquest and the garrison script
    internal interface ILatentHost
    {
        CharStats LatentStats { get; }
        void LatentPush(CharData c, BasicPacket p);
        long LatentVanaTicks();
        // battleutils::GetWeather(PEntity, ignoreScholar = false[, weather]): the zone's current weather when none is given
        ushort LatentWeather(CharData c, ushort? weather = null);
        bool LatentZoneHasType(ushort zone, string type);
        byte LatentRegionOwner(byte region);
        bool LatentInGarrison(CharData c);
        void LatentWarning(string message);
    }

    // CLatentEffect: one item_latents row (or a script's addLatent) on a char. Its mod goes on the char while the
    // condition holds; the item-only mods go on the item in its slot instead.
    public sealed class LatentEffect
    {
        private readonly CharData owner_;
        private EquipmentInfo item_; // m_PItem

        public ushort ConditionsId, ConditionsValue;
        public byte Slot;
        public Mod ModValue;
        public short ModPower;
        public bool Activated { get; private set; }

        public LatentEffect(CharData owner, ushort conditionsId, ushort conditionsValue, byte slot, Mod modValue, short modPower)
        {
            owner_ = owner;
            ConditionsId = conditionsId;
            ConditionsValue = conditionsValue;
            Slot = slot;
            ModValue = modValue;
            ModPower = modPower;
        }

        // CLatentEffect::ModOnItemOnly
        public static bool ModOnItemOnly(Mod mod) => mod is Mod.DMG_RATING or Mod.ITEM_ADDEFFECT_TYPE or Mod.ITEM_SUBEFFECT
            or Mod.ITEM_ADDEFFECT_DMG or Mod.ITEM_ADDEFFECT_CHANCE or Mod.ITEM_ADDEFFECT_ELEMENT or Mod.ITEM_ADDEFFECT_STATUS
            or Mod.ITEM_ADDEFFECT_POWER or Mod.ITEM_ADDEFFECT_DURATION or Mod.ADDS_WEAPONSKILL or Mod.MOVE_SPEED_GEAR_BONUS
            or Mod.CRITHITRATE_ONLY_WEP;

        // CLatentEffect::SkillMod
        public static bool SkillMod(Mod mod) => (mod >= Mod.HTH && mod <= Mod.STAFF) || (mod >= Mod.AUTO_MELEE_SKILL && mod <= Mod.HANDBELL_SKILL);

        // CLatentEffect::Activate: an additional effect / damage mod goes on the item in the slot (when there is one),
        // anything else on the char
        public bool Activate()
        {
            if (Activated) return false;
            var host = owner_.Latents.Host;
            if (ModOnItemOnly(ModValue))
            {
                var item = owner_.Latents.OwnEquip(Slot);
                if (item != null)
                {
                    item.Mods.Add((ModValue, ModPower)); // CItemEquipment::addModifier
                    host?.LatentStats?.BuildWeaponSkills(owner_);
                    host?.LatentPush(owner_, LoginPackets.CommandData(owner_));
                    item_ = item;
                }
            }
            else
            {
                owner_.AddModifier(ModValue, ModPower);
                if (SkillMod(ModValue)) host?.LatentStats?.BuildSkillsTable(owner_);
            }
            Activated = true;
            return true;
        }

        // CLatentEffect::Deactivate
        public bool Deactivate()
        {
            if (!Activated) return false;
            var host = owner_.Latents.Host;
            if (ModOnItemOnly(ModValue))
            {
                if (item_ != null)
                {
                    // CItemEquipment::delModifier: the first row of that mod and amount
                    int at = item_.Mods.FindIndex(m => m.Mod == ModValue && m.Value == ModPower);
                    if (at >= 0) item_.Mods.RemoveAt(at);
                    host?.LatentStats?.BuildWeaponSkills(owner_);
                    host?.LatentPush(owner_, LoginPackets.CommandData(owner_));
                }
            }
            else
            {
                owner_.DelModifier(ModValue, ModPower);
                if (SkillMod(ModValue)) host?.LatentStats?.BuildSkillsTable(owner_);
            }
            Activated = false;
            return true;
        }
    }

    // CLatentEffectContainer: a char's latents, each tied to the equipment slot of the item it came from (MAX_SLOTTYPE for
    // a script's). The Check* methods are the C++ entry points, each re-evaluating the latents its condition kinds touch;
    // if any latent turned on or off the char's health is recomputed.
    // The party leader's trusts count as members. CheckLatentsWeather is
    // called by the weather change (ZoneHost.Weather: CZoneEntities::WeatherChange).
    public sealed class LatentEffectContainer
    {
        // AvatarInParty: a live avatar (pet id below 21) of the latent's pet id, or any with 21
        private static bool LiveAvatar(BattleEntity p, ushort v) =>
            p is PetEntity pet && pet.IsAlive && pet.DeathAtMs == 0 && pet.Status != NpcEntity.StatusDisappear && pet.PetId < 21 &&
            (pet.PetId == v || v == 21);

        private const byte MaxSlotType = 18; // MAX_SLOTTYPE
        private const ushort ZoneUnknown = 0;
        private const ushort ZoneWesternAdoulin = 256, ZoneEasternAdoulin = 257, ZoneMogGarden = 280, ZoneSilverKnife = 283,
                             ZoneCelenniaMemorialLibrary = 284;
        private const byte RegionSandoria = 19, RegionBastok = 20, RegionWindurst = 21, RegionLimbus = 27, RegionWestAhtUrhgan = 28,
                           RegionAlzadaal = 32, RegionRonfaureFront = 33, RegionValdeauniaFront = 40;
        private const ushort EffectFishingImagery = 235, EffectCookingImagery = 243, EffectSignet = 253, EffectSanction = 256,
                             EffectSigil = 268, EffectFightersRoll = 310, EffectNaturalistsRoll = 339, EffectRuneistsRoll = 600;
        private const uint DayFiresday = 0, DayEarthsday = 1, DayWatersday = 2, DayWindsday = 3, DayIceday = 4, DayLightningday = 5,
                           DayLightsday = 6, DayDarksday = 7;
        private static readonly int[] HiddenGearSlots = { 9, 11, 12, 13, 14, 15, 10 }; // NECK, EAR1, EAR2, RING1, RING2, BACK, WAIST

        private readonly CharData owner_;
        private readonly List<LatentEffect> list_ = new List<LatentEffect>();

        internal ILatentHost Host;
        // CCharEntity::retriggerLatents: an equip (0x050 / 0x051) asks for every slot to be rechecked after the parse
        public bool RetriggerLatents;

        public LatentEffectContainer(CharData owner) => owner_ = owner;

        public IReadOnlyList<LatentEffect> All => list_;

        // the item a slot's item-only latent changes: the item's own copy (an augmented one's) or the char's own copy of
        // the template there (see EquipmentInfo.CopyOwn), made on first use and swapped into the slot and its m_Weapons
        // entry; null for an empty or unknown slot
        internal EquipmentInfo OwnEquip(int slot)
        {
            if (slot < 0 || slot >= owner_.Equipped.Length) return null;
            var e = owner_.Equipped[slot];
            if (e == null || e.OwnCopy) return e;
            var copy = e.CopyOwn();
            owner_.Equipped[slot] = copy;
            if (e.Weapon != null)
            {
                switch (slot)
                {
                    case CharStats.SlotMain: if (owner_.MainWeapon == e.Weapon) owner_.MainWeapon = copy.Weapon; break;
                    case CharStats.SlotSub: if (owner_.SubWeapon == e.Weapon) owner_.SubWeapon = copy.Weapon; break;
                    case CharStats.SlotRanged: if (owner_.RangedWeapon == e.Weapon) owner_.RangedWeapon = copy.Weapon; break;
                    case CharStats.SlotAmmo: if (owner_.AmmoWeapon == e.Weapon) owner_.AmmoWeapon = copy.Weapon; break;
                }
            }
            return copy;
        }

        // CLatentEffectContainer::AddLatentEffects: the item's latents, if the char is at its level (the JobLevelAbove
        // comparison is against the condition's value, as the C++)
        public void AddLatentEffects(IEnumerable<(ushort LatentId, ushort LatentParam, Mod Mod, short Value)> latents, byte reqLvl, byte slot)
        {
            foreach (var l in latents)
                if (owner_.MLvl >= reqLvl || l.LatentParam == Latent.JobLevelAbove)
                    list_.Add(new LatentEffect(owner_, l.LatentId, l.LatentParam, slot, l.Mod, l.Value));
        }

        // CLatentEffectContainer::DelLatentEffects: erasing a latent (~CLatentEffect) deactivates it
        public void DelLatentEffects(byte reqLvl, byte slot)
        {
            for (int i = 0; i < list_.Count; i++)
            {
                if (list_[i].Slot != slot) continue;
                list_[i].Deactivate();
                list_.RemoveAt(i--);
            }
        }

        // CLatentEffectContainer::HasAllLatentsActive
        public bool HasAllLatentsActive(byte slot)
        {
            foreach (var l in list_)
                if (!l.Activated && l.Slot == slot) return false;
            return true;
        }

        // CLatentEffectContainer::AddLatentEffect: a script's latent, in no slot
        public void AddLatentEffect(ushort conditionId, ushort conditionValue, Mod mod, short modValue) =>
            list_.Add(new LatentEffect(owner_, conditionId, conditionValue, MaxSlotType, mod, modValue));

        // CLatentEffectContainer::DelLatentEffect: the first latent matching all four
        public bool DelLatentEffect(ushort conditionId, ushort conditionValue, Mod mod, short modValue)
        {
            for (int i = 0; i < list_.Count; i++)
            {
                var l = list_[i];
                if (l.ConditionsId == conditionId && l.ConditionsValue == conditionValue && l.ModValue == mod && l.ModPower == modValue)
                {
                    l.Deactivate();
                    list_.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        private static bool Is(LatentEffect l, params ushort[] ids) => Array.IndexOf(ids, l.ConditionsId) >= 0;

        // CLatentEffectContainer::CheckLatentsHP
        public void CheckLatentsHP() => ProcessLatentEffects(l =>
            Is(l, Latent.HpUnderPercent, Latent.HpOverPercent, Latent.HpUnderTpUnder100, Latent.HpOverTpUnder100,
               Latent.SanctionRegenBonus, Latent.SigilRegenBonus, Latent.HpOverVisibleGear) && ProcessLatentEffect(l));

        // CLatentEffectContainer::CheckLatentsTP
        public void CheckLatentsTP() => ProcessLatentEffects(l =>
            Is(l, Latent.TpUnder, Latent.TpOver, Latent.HpUnderTpUnder100, Latent.HpOverTpUnder100, Latent.SanctionRefreshBonus,
               Latent.SigilRefreshBonus) && ProcessLatentEffect(l));

        // CLatentEffectContainer::CheckLatentsWS
        public void CheckLatentsWS(bool isDuringWs) => ProcessLatentEffects(l =>
            l.ConditionsId == Latent.DuringWs && ProcessLatentEffect(l, isDuringWs));

        // CLatentEffectContainer::CheckLatentsMP
        public void CheckLatentsMP() => ProcessLatentEffects(l =>
            Is(l, Latent.MpUnderPercent, Latent.MpUnder, Latent.MpOver, Latent.WeaponDrawnMpOver, Latent.MpUnderVisibleGear) &&
            ProcessLatentEffect(l));

        // CLatentEffectContainer::CheckLatentsEquip: every latent of the slot
        public void CheckLatentsEquip(byte slot) => ProcessLatentEffects(l => l.Slot == slot && ProcessLatentEffect(l));

        // CLatentEffectContainer::CheckLatentsWeaponDraw: the weapon going out or away
        public void CheckLatentsWeaponDraw(bool drawn) => ProcessLatentEffects(l =>
        {
            if (drawn)
            {
                switch (l.ConditionsId)
                {
                    case Latent.WeaponDrawn: return l.Activate();
                    case Latent.WeaponDrawnMpOver: return owner_.Mp > l.ConditionsValue ? l.Activate() : l.Deactivate();
                    case Latent.WeaponDrawnHpUnder: return owner_.Hp < l.ConditionsValue ? l.Activate() : l.Deactivate();
                    case Latent.WeaponSheathed: return l.Deactivate();
                }
            }
            else
            {
                switch (l.ConditionsId)
                {
                    case Latent.WeaponDrawn:
                    case Latent.WeaponDrawnMpOver:
                    case Latent.WeaponDrawnHpUnder:
                        return l.Deactivate();
                    case Latent.WeaponSheathed: return l.Activate();
                }
            }
            return false;
        });

        // CLatentEffectContainer::CheckLatentsStatusEffect
        public void CheckLatentsStatusEffect() => ProcessLatentEffects(l =>
            Is(l, Latent.StatusEffectActive, Latent.WeatherCondition, Latent.WeatherElement, Latent.NationControl, Latent.InGarrison,
               Latent.SanctionFoodBonus, Latent.SigilFoodBonus) && ProcessLatentEffect(l));

        // CLatentEffectContainer::CheckLatentsFoodEffect
        public void CheckLatentsFoodEffect() => ProcessLatentEffects(l =>
            Is(l, Latent.FoodActive, Latent.NoFoodActive) && ProcessLatentEffect(l));

        // CLatentEffectContainer::CheckLatentsRollSong
        public void CheckLatentsRollSong() => ProcessLatentEffects(l =>
            Is(l, Latent.SongRollActive, Latent.ElevenRollActive) && ProcessLatentEffect(l));

        // CLatentEffectContainer::CheckLatentsDay
        public void CheckLatentsDay() => ProcessLatentEffects(l => l.ConditionsId == Latent.TimeOfDay && ProcessLatentEffect(l));

        // CLatentEffectContainer::CheckLatentsMoonPhase
        public void CheckLatentsMoonPhase() => ProcessLatentEffects(l => l.ConditionsId == Latent.MoonPhase && ProcessLatentEffect(l));

        // CLatentEffectContainer::CheckLatentsWeekDay
        public void CheckLatentsWeekDay() => ProcessLatentEffects(l =>
            Is(l, Latent.Firesday, Latent.Earthsday, Latent.Watersday, Latent.Windsday, Latent.Darksday, Latent.Iceday,
               Latent.Lightningsday, Latent.Lightsday) && ProcessLatentEffect(l));

        // CLatentEffectContainer::CheckLatentsHours
        public void CheckLatentsHours() => ProcessLatentEffects(l => l.ConditionsId == Latent.HourOfDay && ProcessLatentEffect(l));

        // CLatentEffectContainer::CheckLatentsPartyMembers: the party's size as the caller counts it. The C++ reads
        // PParty->members for the in-zone count even when the caller has just cleared PParty (DisbandParty); here no
        // party counts no members.
        public void CheckLatentsPartyMembers(int members, int trustCount) => ProcessLatentEffects(l =>
        {
            int totalMembers = members + trustCount;
            switch (l.ConditionsId)
            {
                case Latent.PartyMembers:
                    return l.ConditionsValue <= totalMembers ? l.Activate() : l.Deactivate();
                case Latent.PartyMembersInZone:
                    if (l.ConditionsValue <= totalMembers)
                    {
                        int inZone = 0;
                        var party = owner_.Party;
                        for (int m = 0; party != null && m < members && m < party.Members.Count; m++)
                            if (party.Members[m] is { } member && ZoneHost.SameArea(member, owner_)) inZone++;
                        var leader = party?.Leader;
                        if (leader != null && ZoneHost.SameArea(owner_, leader)) inZone += trustCount;
                        return inZone == l.ConditionsValue ? l.Activate() : l.Deactivate();
                    }
                    return l.Deactivate();
            }
            return false;
        });

        // CLatentEffectContainer::CheckLatentsPartyJobs
        public void CheckLatentsPartyJobs() => ProcessLatentEffects(l => l.ConditionsId == Latent.JobInParty && ProcessLatentEffect(l));

        // CLatentEffectContainer::CheckLatentsPartyAvatar
        public void CheckLatentsPartyAvatar() => ProcessLatentEffects(l => l.ConditionsId == Latent.AvatarInParty && ProcessLatentEffect(l));

        // CLatentEffectContainer::CheckLatentsJobLevel
        public void CheckLatentsJobLevel() => ProcessLatentEffects(l =>
            Is(l, Latent.JobMultiple, Latent.JobMultipleAtNight, Latent.JobLevelBelow, Latent.JobLevelAbove, Latent.InGarrison) &&
            ProcessLatentEffect(l));

        // CLatentEffectContainer::CheckLatentsPetType
        public void CheckLatentsPetType() => ProcessLatentEffects(l => l.ConditionsId == Latent.PetId && ProcessLatentEffect(l));

        // CLatentEffectContainer::CheckLatentsTime: empty in the C++ too ("this isn't called anywhere")
        public void CheckLatentsTime()
        {
        }

        // CLatentEffectContainer::CheckLatentsWeaponBreak
        public void CheckLatentsWeaponBreak(byte slot) => ProcessLatentEffects(l =>
            l.ConditionsId == Latent.WeaponBroken && l.ConditionsValue == slot && ProcessLatentEffect(l));

        // CLatentEffectContainer::CheckLatentsZone
        public void CheckLatentsZone() => ProcessLatentEffects(l =>
            Is(l, Latent.Zone, Latent.InAssault, Latent.InDynamis, Latent.InAdoulin, Latent.WeatherCondition, Latent.WeatherElement,
               Latent.NationControl, Latent.NationCitizen, Latent.ZoneHomeNation, Latent.SanctionFoodBonus, Latent.SigilFoodBonus) &&
            ProcessLatentEffect(l));

        // CLatentEffectContainer::CheckLatentsWeather(): the zone's current weather
        public void CheckLatentsWeather()
        {
            if (Host == null) return;
            CheckLatentsWeather(Host.LatentWeather(owner_));
        }

        // CLatentEffectContainer::CheckLatentsWeather(weather)
        public void CheckLatentsWeather(ushort weather) => ProcessLatentEffects(l =>
        {
            if (Host == null) return false;
            if (l.ConditionsId == Latent.WeatherElement)
                return ApplyLatentEffect(l, l.ConditionsValue == MobSpawnHandler.WeatherElement(Host.LatentWeather(owner_, weather)));
            if (l.ConditionsId == Latent.WeatherCondition)
                return ApplyLatentEffect(l, l.ConditionsValue == Host.LatentWeather(owner_, weather));
            return false;
        });

        // CLatentEffectContainer::CheckLatentsTargetChange
        public void CheckLatentsTargetChange() => ProcessLatentEffects(l =>
            Is(l, Latent.SignetBonus, Latent.VsEcosystem, Latent.VsSpecies, Latent.VsFamily) && ProcessLatentEffect(l));

        // CLatentEffectContainer::ProcessLatentEffects: the char's health again if any latent changed
        private void ProcessLatentEffects(Func<LatentEffect, bool> logic)
        {
            bool update = false;
            // a latent's activation can rebuild nothing in this list, but iterate a snapshot anyway
            foreach (var l in list_.ToArray())
                if (logic(l)) update = true;
            if (update) CharStats.UpdateHealth(owner_);
        }

        private static float Percent(int value, int max) => (float)value / max * 100;

        // CLatentEffectContainer::ProcessLatentEffect: the latent's condition now, applied
        public bool ProcessLatentEffect(LatentEffect l, bool isDuringWs = false)
        {
            if (Host == null) return false; // no zone to read the condition against
            var c = owner_;
            ushort playerZoneId = c.Zone;
            if (playerZoneId == ZoneUnknown) return false;

            bool expression = false;
            long vanaTime = Host.LatentVanaTicks();
            byte region = ZoneRegions.Of(c.Zone);
            ushort v = l.ConditionsValue;

            switch (l.ConditionsId)
            {
                case Latent.HpUnderPercent:
                    expression = Percent(c.Hp, c.MaxHp) <= v;
                    break;
                case Latent.HpOverPercent:
                    expression = Percent(c.Hp, c.MaxHp) >= v;
                    break;
                case Latent.HpUnderTpUnder100:
                    expression = Percent(c.Hp, c.MaxHp) <= v && c.Tp < 1000;
                    break;
                case Latent.HpOverTpUnder100:
                    expression = Percent(c.Hp, c.MaxHp) >= v && c.Tp < 1000;
                    break;
                case Latent.MpUnderPercent:
                    expression = c.MaxMp != 0 && Percent(c.Mp, c.MaxMp) <= v;
                    break;
                case Latent.MpUnder:
                    expression = c.Mp <= v;
                    break;
                case Latent.TpUnder:
                    expression = c.Tp < v;
                    break;
                case Latent.TpOver:
                    expression = c.Tp > v;
                    break;
                case Latent.Subjob:
                    expression = c.SJob == v;
                    break;
                case Latent.PetId:
                    expression = c.PPet is PetEntity pet && pet.PetId == v;
                    break;
                case Latent.WeaponDrawn:
                    expression = c.Animation == Anim.Attack;
                    break;
                case Latent.WeaponSheathed:
                    expression = c.Animation != Anim.Attack;
                    break;
                case Latent.SignetBonus:
                {
                    var target = c.BattleTarget;
                    expression = target != null && c.MLvl >= target.MLvl && region < RegionWestAhtUrhgan;
                    break;
                }
                case Latent.SanctionRegenBonus:
                    expression = region >= RegionWestAhtUrhgan && region <= RegionAlzadaal && Percent(c.Hp, c.MaxHp) < v;
                    break;
                case Latent.SanctionRefreshBonus:
                    expression = region >= RegionWestAhtUrhgan && region <= RegionAlzadaal && Percent(c.Mp, c.MaxMp) < v;
                    break;
                case Latent.SigilRegenBonus:
                    expression = region >= RegionRonfaureFront && region <= RegionValdeauniaFront && Percent(c.Hp, c.MaxHp) < v;
                    break;
                case Latent.SigilRefreshBonus:
                    expression = region >= RegionRonfaureFront && region <= RegionValdeauniaFront && Percent(c.Mp, c.MaxMp) < v;
                    break;
                case Latent.SanctionFoodBonus:
                    expression = region >= RegionWestAhtUrhgan && region <= RegionAlzadaal;
                    break;
                case Latent.SigilFoodBonus:
                    expression = region >= RegionRonfaureFront && region <= RegionValdeauniaFront;
                    break;
                case Latent.StatusEffectActive:
                    expression = c.StatusEffects.HasStatusEffect(v);
                    break;
                case Latent.NoFoodActive:
                    expression = !c.StatusEffects.HasStatusEffect(EffectId.Food);
                    break;
                case Latent.PartyMembers:
                {
                    int partyCount = 0, trustCount = 0;
                    if (c.Party?.Leader is { } leader)
                    {
                        trustCount = leader.PTrusts.Count;
                        partyCount = c.Party.Members.Count;
                    }
                    expression = v <= partyCount + trustCount;
                    break;
                }
                case Latent.PartyMembersInZone:
                {
                    int inZone = 0;
                    if (c.Party?.Leader is { } leader)
                    {
                        foreach (var member in c.Party.Members)
                            if (ZoneHost.SameArea(member, c)) inZone++;
                        if (ZoneHost.SameArea(c, leader)) inZone += leader.PTrusts.Count;
                    }
                    expression = v <= inZone;
                    break;
                }
                case Latent.AvatarInParty:
                    // a live avatar (pet id below 21) among the members' pets, or the char's own without a party
                    if (c.Party != null)
                    {
                        foreach (var member in c.Party.Members)
                        {
                            if (LiveAvatar(member.PPet, v))
                            {
                                expression = true;
                                break;
                            }
                        }
                    }
                    else expression = LiveAvatar(c.PPet, v);
                    break;
                case Latent.JobInParty:
                    if (c.Party != null)
                    {
                        foreach (var member in c.Party.Members)
                        {
                            if (member.Id != c.Id && member.MJob == v)
                            {
                                expression = true;
                                break;
                            }
                        }
                        if (c.Party.Leader == null)
                        {
                            expression = false;
                            break;
                        }
                        foreach (var trust in c.Party.Leader.PTrusts)
                        {
                            if (trust.MJob == v)
                            {
                                expression = true;
                                break;
                            }
                        }
                    }
                    break;
                case Latent.Zone:
                    expression = v == c.Zone;
                    break;
                case Latent.SynthTrainee:
                {
                    expression = v < c.RealSkills.Length && c.RealSkills[v] / 10 < 40;
                    for (ushort id = EffectFishingImagery; expression && id <= EffectCookingImagery; id++)
                        if (c.StatusEffects.HasStatusEffect(id)) expression = false;
                    break;
                }
                case Latent.SongRollActive:
                    expression = c.StatusEffects.HasStatusEffectByFlag(EffectFlag.Roll | EffectFlag.Song);
                    break;
                case Latent.TimeOfDay:
                {
                    uint hour = VanaTime.GetHour(vanaTime);
                    switch (v)
                    {
                        case 0: expression = hour >= 6 && hour < 18; break;  // daytime: 06:00 to 18:00
                        case 1: expression = hour >= 18 || hour < 6; break;  // nighttime: 18:00 to 06:00
                        case 2: expression = hour >= 17 || hour < 7; break;  // dusk - dawn: 17:00 to 7:00
                    }
                    break;
                }
                case Latent.HourOfDay:
                {
                    uint hour = VanaTime.GetHour(vanaTime);
                    switch (v)
                    {
                        case 1: expression = hour == 4; break;                // new day
                        case 2: expression = hour >= 6 && hour < 7; break;    // dawn
                        case 3: expression = hour >= 7 && hour < 17; break;   // day
                        case 4: expression = hour >= 16 && hour < 18; break;  // dusk
                        case 5: expression = hour >= 18 && hour < 20; break;  // evening
                        case 6: expression = hour >= 20 || hour < 4; break;   // dead of night
                    }
                    break;
                }
                case Latent.Firesday:
                    expression = VanaTime.GetWeekDay(vanaTime) == DayFiresday;
                    break;
                case Latent.Earthsday:
                    expression = VanaTime.GetWeekDay(vanaTime) == DayEarthsday;
                    break;
                case Latent.Watersday:
                    expression = VanaTime.GetWeekDay(vanaTime) == DayWatersday;
                    break;
                case Latent.Windsday:
                    expression = VanaTime.GetWeekDay(vanaTime) == DayWindsday;
                    break;
                case Latent.Darksday:
                    expression = VanaTime.GetWeekDay(vanaTime) == DayDarksday;
                    break;
                case Latent.Iceday:
                    expression = VanaTime.GetWeekDay(vanaTime) == DayIceday;
                    break;
                case Latent.Lightningsday:
                    expression = VanaTime.GetWeekDay(vanaTime) == DayLightningday;
                    break;
                case Latent.Lightsday:
                    expression = VanaTime.GetWeekDay(vanaTime) == DayLightsday;
                    break;
                case Latent.MoonPhase:
                {
                    uint phase = VanaTime.MoonPhase(vanaTime);
                    uint direction = VanaTime.MoonDirection(vanaTime); // 1 = waning, 2 = waxing, 0 = neither
                    switch (v)
                    {
                        case 0: expression = phase <= 5 || (phase <= 10 && direction == 1); break;            // New Moon
                        case 1: expression = phase >= 7 && phase <= 38 && direction == 2; break;              // Waxing Crescent
                        case 2: expression = phase >= 40 && phase <= 55 && direction == 2; break;             // First Quarter
                        case 3: expression = phase >= 57 && phase <= 88 && direction == 2; break;             // Waxing Gibbous
                        case 4: expression = phase >= 95 || (phase >= 90 && direction == 2); break;           // Full Moon
                        case 5: expression = phase >= 62 && phase <= 93 && direction == 1; break;             // Waning Gibbous
                        case 6: expression = phase >= 45 && phase <= 60 && direction == 1; break;             // Last Quarter
                        case 7: expression = phase >= 12 && phase <= 43 && direction == 1; break;             // Waning Crescent
                    }
                    break;
                }
                case Latent.JobMultiple:
                    expression = v == 0 ? c.MLvl % 2 == 1 : c.MLvl % v == 0;
                    break;
                case Latent.JobMultipleAtNight:
                    expression = (v == 0 ? c.MLvl % 2 == 1 : c.MLvl % v == 0) && VanaTime.GetTotd(vanaTime) == Totd.Night;
                    break;
                case Latent.WeaponDrawnHpUnder:
                    expression = c.Hp < v && c.Animation == Anim.Attack;
                    break;
                case Latent.MpUnderVisibleGear:
                {
                    // the MP of the gear that does not show, out of latentmp
                    uint totalMpMod = 0;
                    foreach (int slot in HiddenGearSlots)
                        if (c.Equipped[slot] is { } equip) totalMpMod += (uint)equip.GetModifier(Mod.MP);
                    expression = c.Mp <= (float)(uint)(c.LatentMp - totalMpMod) * (v / 100f);
                    break;
                }
                case Latent.HpOverVisibleGear:
                {
                    uint totalHpMod = 0;
                    foreach (int slot in HiddenGearSlots)
                        if (c.Equipped[slot] is { } equip) totalHpMod += (uint)equip.GetModifier(Mod.HP);
                    expression = c.Hp >= (float)(uint)(c.LatentHp - totalHpMod) * (v / 100f);
                    break;
                }
                case Latent.WeaponBroken:
                {
                    // CItemWeapon::isUnlocked on the item in the latent's slot
                    switch (l.Slot)
                    {
                        case CharStats.SlotMain:
                        case CharStats.SlotSub:
                        case CharStats.SlotRanged:
                            var weapon = c.Equipped[l.Slot]?.Weapon;
                            expression = weapon != null && weapon.IsUnlocked(CharStats.EquippedItem(c, l.Slot));
                            break;
                    }
                    break;
                }
                case Latent.InDynamis:
                    expression = Host.LatentZoneHasType(c.Zone, "dynamis");
                    break;
                case Latent.InAssault:
                    expression = Host.LatentZoneHasType(c.Zone, "instanced") && region >= RegionWestAhtUrhgan && region <= RegionAlzadaal;
                    break;
                case Latent.InAdoulin:
                    expression = c.Zone is ZoneWesternAdoulin or ZoneEasternAdoulin or ZoneMogGarden or ZoneSilverKnife or ZoneCelenniaMemorialLibrary;
                    break;
                case Latent.InGarrison:
                    expression = Host.LatentInGarrison(c) && c.MLvl >= v;
                    break;
                case Latent.FoodActive:
                    expression = c.StatusEffects.GetStatusEffect(EffectId.Food) is { } food && food.SourceTypeParam == v;
                    break;
                case Latent.JobLevelBelow:
                    expression = c.MLvl < v;
                    break;
                case Latent.JobLevelAbove:
                    expression = c.MLvl >= v;
                    break;
                case Latent.WeatherCondition:
                    expression = v == Host.LatentWeather(c);
                    break;
                case Latent.WeatherElement:
                    expression = v == MobSpawnHandler.WeatherElement(Host.LatentWeather(c));
                    break;
                case Latent.NationControl:
                {
                    // the zone the char is in (or going to)
                    byte currentRegion = ZoneRegions.Of(playerZoneId);
                    bool hasSignet = c.StatusEffects.HasStatusEffect(EffectSignet);
                    bool hasSanction = c.StatusEffects.HasStatusEffect(EffectSanction);
                    bool hasSigil = c.StatusEffects.HasStatusEffect(EffectSigil);
                    bool regionAlwaysOutOfControl = currentRegion >= RegionSandoria && currentRegion <= RegionLimbus; // zoneutils::IsAlwaysOutOfNationControl
                    switch (v)
                    {
                        case 0: // under own nation's control
                            expression = currentRegion < RegionWestAhtUrhgan && Host.LatentRegionOwner(currentRegion) == c.Nation &&
                                         (hasSignet || hasSanction || hasSigil);
                            break;
                        case 1: // outside of own nation's control
                            expression = currentRegion < RegionWestAhtUrhgan &&
                                         (regionAlwaysOutOfControl || c.Nation != Host.LatentRegionOwner(currentRegion)) &&
                                         (hasSignet || hasSanction || hasSigil);
                            break;
                    }
                    break;
                }
                case Latent.NationCitizen:
                    expression = c.Nation == v;
                    break;
                case Latent.ZoneHomeNation:
                {
                    byte currentRegion = ZoneRegions.Of(playerZoneId);
                    switch (v)
                    {
                        case RegionSandoria: expression = c.Nation == 0 && currentRegion == v; break;
                        case RegionBastok: expression = c.Nation == 1 && currentRegion == v; break;
                        case RegionWindurst: expression = c.Nation == 2 && currentRegion == v; break;
                    }
                    break;
                }
                case Latent.MpOver:
                    expression = c.Mp >= v;
                    break;
                case Latent.WeaponDrawnMpOver:
                    expression = c.Mp > v && c.Animation == Anim.Attack;
                    break;
                case Latent.ElevenRollActive:
                    expression = CheckForElevenRoll(c);
                    break;
                case Latent.VsEcosystem:
                    if (c.BattleTarget is MobEntity ecoTarget) expression = ecoTarget.Ecosystem == v;
                    else if (c.BattleTarget is CharData ecoChar) expression = ecoChar.EcoSystem == v;
                    break;
                case Latent.VsSpecies:
                    if (c.BattleTarget is MobEntity speciesTarget) expression = speciesTarget.Species == v;
                    break;
                case Latent.VsFamily:
                    if (c.BattleTarget is MobEntity familyTarget) expression = familyTarget.Family == v;
                    break;
                case Latent.Mainjob:
                    expression = c.MJob == v;
                    break;
                case Latent.EquippedInSlot:
                    expression = l.Slot == v;
                    break;
                case Latent.DuringWs:
                    expression = isDuringWs;
                    break;
                default:
                    Host.LatentWarning($"Latent ID {l.ConditionsId} unhandled in ProcessLatentEffect");
                    return false;
            }
            return ApplyLatentEffect(l, expression);
        }

        // CStatusEffectContainer::CheckForElevenRoll: a roll (Fighter's to Naturalist's, or Runeist's) that came up 11
        private static bool CheckForElevenRoll(CharData c)
        {
            foreach (var e in c.StatusEffects.All)
            {
                if (((e.Id >= EffectFightersRoll && e.Id <= EffectNaturalistsRoll) || e.Id == EffectRuneistsRoll) && e.SubPower == 11)
                    return true;
            }
            return false;
        }

        // CLatentEffectContainer::ApplyLatentEffect
        private static bool ApplyLatentEffect(LatentEffect l, bool expression) => expression ? l.Activate() : l.Deactivate();
    }
}
