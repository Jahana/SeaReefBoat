using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // monstrosity.cpp's zone side: the dataset (lazy, with ecosystems.yaml's ranks), the job change into and out of MON,
    // the zone-in (the mods, Gestation, the derived state), the full update after GAMEOK, a species change, the
    // Belligerency flag and the rank infamy gives, and the CLuaBaseEntity bindings (get / setMonstrosityData, the entry,
    // the shop, the exits). Not ported here (later batches): the MON stats' consumers past CalculateStats (abilities,
    // traits, skills, exp and infamy from kills, combat), the 0x01A monster skill and 0x102 equip packets, Belligerency
    // between players, the party / trade restrictions, the death menu, the idle regen.
    public sealed partial class ZoneHost
    {
        private const ushort EffectMedicine = 155, EffectGestation = 575;
        private MonstrosityTable monstrosity_;

        // monstrosity::LoadStaticData, on first use
        private MonstrosityTable MonTable()
        {
            if (monstrosity_ != null) return monstrosity_;
            ecosystems_ ??= Ecosystems.Load(dataDir_, enums_);
            var byId = new Dictionary<ushort, SpeciesInfo>();
            foreach (var sp in ecosystems_.SpeciesByName.Values) byId[sp.Id] = sp;
            monstrosity_ = MonstrosityTable.Load(dataDir_, enums_, id =>
            {
                if (!byId.TryGetValue(id, out var sp)) return null;
                var a = sp.Attributes;
                return new[] { a.StrRank, a.DexRank, a.VitRank, a.AgiRank, a.IntRank, a.MndRank, a.ChrRank };
            });
            return monstrosity_;
        }

        private bool MonstrosityEnabled => ChatCfg.EnableMonstrosity;

        // the 0x063 MONSTROSITY1 page with the infamy held (empty outside MON)
        private BasicPacket MonPage1(CharData c) => Monstrosity.Monstrosity1(c, c?.Monstrosity != null ? GetPoints(c, "infamy") : 0);

        // WriteMonstrosityData
        private void WriteMonstrosityData(CharData c)
        {
            if (c.Monstrosity != null) Monstrosity.Save(db_, c.Id, c.Monstrosity);
        }

        // delAllEffectsButMedicine: JP wiki, timed effects drop on a species change, variants included, and Medicated
        // survives; permanent effects stay
        private static void DelAllEffectsButMedicine(CharData c)
        {
            var ids = new List<ushort>();
            foreach (var e in c.StatusEffects.All)
                if (!e.Deleted && e.Id != EffectMedicine && e.DurationMs > 0) ids.Add(e.Id);
            foreach (var id in ids) c.StatusEffects.DelStatusEffectSilent(id);
        }

        // refreshDerivedState: the species' look, speed (most walk at twice their movement speed) and hitbox, the rank,
        // the traits, the level instincts, and any level unlock not yet granted
        private void RefreshMonstrosityState(CharData c)
        {
            var table = MonTable();
            var data = c.Monstrosity;
            if (table.FindSpecies(data.Species) is { } species)
            {
                data.Look = species.Look;
                c.BaseSpeedOverride = species.Speed;
                byte baseSpeed = species.Speed ?? MountUtils.BaseSpeed;
                c.AnimationSpeed = species.AnimationSpeed ?? (byte)Math.Min(baseSpeed * 2, 255);
                c.ModelHitboxOverride = species.Hitbox is byte h ? h / 10.0f : null;
                MountUtils.UpdateSpeed(c);
            }
            data.Rank = Monstrosity.RankFor(data, GetPoints(c, "infamy"));
            BuildCharTraits(c);
            Monstrosity.ApplyLevelInstincts(table, data);
            // covers levels reached before the unlock existed, or set directly
            if (Monstrosity.GrantLevelUnlocks(table, data) > 0) WriteMonstrosityData(c);
            RefreshMonstrositySpells(c);
        }

        // monstrosity::HandleJobChange: the data, instinct mods and the species' ecosystem exist only while the job is MON
        private void MonstrosityJobChange(CharData c, byte newJob)
        {
            var table = MonTable();
            if (newJob != Monstrosity.JobMon)
            {
                if (c.Monstrosity != null)
                {
                    Monstrosity.DelAllInstinctMods(table, c);
                    Monstrosity.DelSpeciesMods(table, c);
                    c.Monstrosity = null;
                    c.EcoSystem = Ecosystem.Humanoid;
                    c.BaseSpeedOverride = null;
                    c.AnimationSpeed = (byte)Math.Clamp(MountUtils.BaseSpeed / ChatCfg.AnimationSpeedDivisor, 0f, 255f);
                    c.ModelHitboxOverride = null;
                    MountUtils.UpdateSpeed(c);
                }
                return;
            }
            if (c.Monstrosity != null) return;
            Monstrosity.TryPopulate(db_, table, MonstrosityEnabled, c);
            if (c.Monstrosity == null) return;
            // JP wiki: entering Monstrosity leaves any party.
            if (c.Party != null) LeaveParty(c);
            Monstrosity.AddAllInstinctMods(table, c);
            Monstrosity.AddSpeciesMods(table, c);
        }

        // monstrosity::HandleZoneIn: the mods back on, Gestation outside the Feretory (18 h, a minute under
        // Belligerency, ticking so it can warn 10 s before it ends), the derived state; the packets wait for GAMEOK
        private void MonstrosityZoneIn(CharData c)
        {
            if (!MonstrosityEnabled || c.Monstrosity == null) return;
            var table = MonTable();
            Monstrosity.AddAllInstinctMods(table, c);
            Monstrosity.AddSpeciesMods(table, c);
            // TODO: Does logging in outside the Feretory give Gestation again after it was clicked off? And under Belligerency?
            if (c.Zone != Monstrosity.ZoneFeretory)
            {
                long duration = c.Monstrosity.Belligerency ? 60_000 : 18 * 3600_000L;
                // Gestation blocks attacking in packet validation, so attacking is not a break flag; the effect announces
                // wearing off, so Logout / NoLossMessage stay unset
                const uint gestationFlags = EffectFlag.Invisible | EffectFlag.Death | EffectFlag.MagicBegin | EffectFlag.OnZone;
                c.StatusEffects.AddStatusEffect(new StatusEffect(EffectGestation, EffectGestation, 0, 10_000, duration, flags: gestationFlags),
                                                EffectNotice.Silent);
            }
            RefreshMonstrosityState(c);
            c.UpdateMask |= CharStats.UpdateLook;
        }

        // monstrosity::SendFullMonstrosityUpdate: the client builds its monster move list from this set
        private void SendFullMonstrosityUpdate(CharData c)
        {
            if (c.Monstrosity == null || loader_.Stats is not { } stats) return;
            RefreshMonstrosityState(c);
            // TODO (C++): the species box on the UI should never be empty; a bad state should fall back to a Lv1 Bunny.
            PushTo(c, GameOkPackets.JobInfo(c, UnixNow()));
            PushTo(c, CharStatus(c));
            if (SessionOf(c) is MapSession s) SendExtendedJobPackets(s, c);
            PushTo(c, SelfPackets.CliStatus(c, stats, UnityStatus(c)));
            PushTo(c, SelfPackets.CliStatus2(c));
            PushTo(c, GameOkPackets.StatusIcons(c, NowMs(), UnixNow()));
            PushTo(c, SelfPackets.Merits(c, stats));
            PushTo(c, MonPage1(c));
            PushTo(c, Monstrosity.Monstrosity2(c));
            PushTo(c, SelfPackets.JobPoints(c));
            PushTo(c, LoginPackets.GrapList(c));
            PushTo(c, LoginPackets.MagicData(c));
            PushTo(c, LoginPackets.CommandData(c));
            PushTo(c, LoginPackets.GroupAttr(c));
            PushTo(c, AbilRecast(c));
            PushTo(c, CharSyncOf(c));
            c.UpdateMask |= LoginPackets.UpdateAllChar;
        }

        // changeSpecies: whether the family changed, or null when the species cannot be taken (the current one, one
        // unknown, a family not owned, a variant not unlocked)
        private bool? ChangeSpeciesState(CharData c, ushort code)
        {
            var table = MonTable();
            var data = c.Monstrosity;
            // picking the current species again would only strip buffs
            if (code == data.Species) return null;
            byte previousId = data.MonstrosityId;
            if (table.FindSpecies(code) is not { } species || data.Levels[species.MonstrosityId] == 0) return null;
            if (code >= Monstrosity.VariantSpeciesBase && !Monstrosity.IsVariantUnlocked(data, code - Monstrosity.VariantSpeciesBase)) return null;

            Monstrosity.DelSpeciesMods(table, c);
            data.Species = code;
            data.StanceSpecies = 0;
            Monstrosity.AddSpeciesMods(table, c);
            data.MonstrosityId = species.MonstrosityId;
            data.MainJob = species.MainJob;
            data.SubJob = species.SubJob;
            data.Size = species.Size;

            bool familyChanged = data.MonstrosityId != previousId;
            if (familyChanged)
            {
                Monstrosity.ApplySpeciesState(table, c);
                // JP wiki: a new family starts from zero exp, and switching back does not restore it. A variant keeps it.
                c.JobExp[Monstrosity.JobMon] = 0;
                // retail keeps instincts equipped across species, unless the new level cannot afford them
                Monstrosity.UnequipInstinctsOverLimit(table, c);
            }
            // JP wiki: Teyrnon's buffs are lost on a species change.
            DelAllEffectsButMedicine(c);
            // every species, variants included, brings its own jobs and stats
            if (loader_.Stats is { } stats)
            {
                stats.BuildSkillsTable(c);
                loader_.CalculateStats(c);
                loader_.BuildAbilityTable(c);
                stats.CheckUnarmedWeapon(c);
            }
            CharStats.UpdateHealth(c);
            return familyChanged;
        }

        // monstrosity::ChangeSpecies, with sendSpeciesChangePackets in retail's order
        private void ChangeMonstrositySpecies(CharData c, ushort code)
        {
            if (ChangeSpeciesState(c, code) is not bool familyChanged || loader_.Stats is not { } stats) return;
            WriteMonstrosityData(c);
            RefreshMonstrosityState(c);
            var s = SessionOf(c);
            if (s != null) SendInventory(s, c);
            ResyncEquipment(c);
            PushTo(c, LoginPackets.GrapList(c));
            PushTo(c, LoginPackets.EquipClear());
            // the job table only changes when the level does, and retail only sends it then
            if (familyChanged) PushTo(c, GameOkPackets.JobInfo(c, UnixNow()));
            if (s != null) SendExtendedJobPackets(s, c);
            PushTo(c, LoginPackets.GroupAttr(c));
            PushTo(c, LoginPackets.CommandData(c));
            PushTo(c, GameOkPackets.StatusIcons(c, NowMs(), UnixNow()));
            PushTo(c, SelfPackets.CliStatus(c, stats, UnityStatus(c)));
            PushTo(c, SelfPackets.CliStatus2(c));
            PushTo(c, AbilRecast(c));
            PushTo(c, SelfPackets.Merits(c, stats));
            PushTo(c, MonPage1(c));
            PushTo(c, Monstrosity.Monstrosity2(c));
            PushTo(c, SelfPackets.JobPoints(c));
            PushTo(c, GameOkPackets.Homepoints(c));
            PushTo(c, LoginPackets.MagicData(c));
            var unity = new List<BasicPacket>();
            GameOkPackets.AddUnity(unity, LoadUnity(), UnixNow());
            foreach (var p in unity) PushTo(c, p);
            PushTo(c, CharStatus(c));
            PushTo(c, CharSyncOf(c));
            c.UpdateMask |= LoginPackets.UpdateAllChar;
        }

        private const byte DeathMenuMonstrosityCancel = 1, DeathMenuMonstrosityRetry = 2;

        // monstrosity::HandleDeathMenu: back on its feet, then to the Feretory's entrance (Cancel) or a random listed exit
        // of the zone it fell in (Retry: the zone again, the zone script's default entry without one)
        private void MonstrosityDeathMenu(CharData c, byte type)
        {
            if (c.Monstrosity == null || type != DeathMenuMonstrosityCancel && type != DeathMenuMonstrosityRetry) return;
            c.Hp = c.MaxHp;
            c.Mp = c.MaxMp;
            c.Animation = Anim.None;
            c.UpdateMask |= CharStats.UpdateHp;
            if (type == DeathMenuMonstrosityCancel)
            {
                OnMonstrosityReturnToEntrance(c);
                return;
            }
            var exits = Monstrosity.GetFeretoryExits(c.Zone);
            var (x, y, z, rot) = exits.Count == 0 ? default : exits[MobRng.Next(exits.Count)];
            (c.X, c.Y, c.Z, c.Rotation) = (x, y, z, (byte)rot);
            c.DeathAtMs = 0;
            if (SessionOf(c) is not { } s) return;
            s.PacketList.Clear();
            c.Boundary = 0;
            zoneChanges_[s] = c.Zone;
            SavePetForZoning(c);
        }

        // monstrosity::HandleBelligerentDefeat: the loser forfeits a tenth of its points, and the winner's alliance splits
        // up to a fifth more (capped at twice what the winner holds) among those fighting the loser. TODO in C++ too: the
        // gladiator's treasure casket.
        private void MonstrosityBelligerentDefeat(CharData loser, CharData winner)
        {
            if (!Monstrosity.AreBelligerents(winner, loser)) return;
            static string CurrencyOf(CharData c) => c.Monstrosity != null ? "infamy" : "prestige";
            string loserCurrency = CurrencyOf(loser);
            int loserHeld = GetPoints(loser, loserCurrency);
            int spoils = Math.Min(loserHeld / 5, GetPoints(winner, CurrencyOf(winner)) * 2);
            SetPoints(loser, loserCurrency, loserHeld - loserHeld / 10 - spoils);
            // every member counts towards the split, but only those fighting the loser collect
            var members = new List<CharData>(ForAlliance(winner));
            int share = spoils / members.Count;
            foreach (var member in members)
            {
                if (member.Zone != loser.Zone || !Monstrosity.AreBelligerents(member, loser)) continue;
                if (member.Monstrosity != null) GainInfamy(member, share);
                else SetPoints(member, "prestige", GetPoints(member, "prestige") + share);
            }
        }

        // monstrosity::HandleGladiatorGearChange: retail locks every slot for 10 s after a gladiator changes gear in a
        // Belligerency zone
        private void GladiatorGearChange(CharData c)
        {
            if (!Monstrosity.IsGladiatorInBelligerency(c) || c.StatusEffects.HasStatusEffect(Monstrosity.EffectEncumbranceII)) return;
            c.StatusEffects.AddStatusEffect(new StatusEffect(Monstrosity.EffectEncumbranceII, Monstrosity.EffectEncumbranceII, 0xFFFF, 0,
                Monstrosity.GladiatorEncumbranceMs));
        }

        // monstrosity::ApplyIdleRegen: JP wiki, a Monipulator recovers HP and MP out of combat, even moving; slip damage
        // stops it. TODO in C++ too: retail trickles 3% every 10 s for a while after a fight first.
        private void MonstrosityIdleRegen(CharData c)
        {
            if (c.AttackTarget != null || c.Notoriety.HasEnmity() || c.GetMod(Mod.REGEN_DOWN) > 0) return;
            c.Hp = Math.Min(c.MaxHp, c.Hp + c.MaxHp * Monstrosity.IdleRegenPercent / 100);
            c.Mp = Math.Min(c.MaxMp, c.Mp + c.MaxMp * Monstrosity.IdleRegenPercent / 100);
        }

        // monstrosity::HandleMonsterSkillActionPacket: an enabled move of the species, learned by the family's level, with
        // the TP for it (or Meikyo Shisui); it readies for its own time and costs its own TP (CAIContainer::Internal_MobSkill)
        private void MonsterSkillAction(CharData c, ushort targId, ushort datSkillId)
        {
            if (c.Monstrosity is not { } data) return;
            byte level = data.Levels[data.MonstrosityId];
            var skill = MonTable().FindSpecies(data.Species)?.TpSkills.Find(e => e.Enabled && e.DatSkillId == datSkillId && e.UnlockLevel <= level);
            if (skill == null || c.Tp < skill.TpCost && !c.StatusEffects.HasStatusEffect(EffectMeikyoShisui))
            {
                PushTo(c, SelfPackets.BattleMessage(c, 0, 0, MsgUnableToUseJobAbility2));
                return;
            }
            var target = AbilityEntityByTargId(c, targId);
            if (target != null && IsUntargetable(target) || !CanChangeState(c)) return;
            StartMobSkill(c, target, skill.MobSkillId, skill.ReadyTimeMs, NowMs(), skill.TpCost);
        }

        // monstrosity::HandleMobSkillFired: Fighting Stance redraws the model in (or out of) its stance species
        private void MonstrosityMobSkillFired(CharData c, ushort mobSkillId)
        {
            if (mobSkillId != Monstrosity.FightingStanceSkill) return;
            var data = c.Monstrosity;
            if (MonTable().FindSpecies(data.Species) is not { StanceSpecies: ushort stance }) return;
            data.StanceSpecies = data.StanceSpecies != 0 ? (ushort)0 : stance;
            PushTo(c, LoginPackets.EquipClear());
            PushTo(c, LoginPackets.GrapList(c));
            PushTo(c, CharStatus(c));
            c.UpdateMask |= CharStats.UpdateLook;
        }

        private const byte MonFlagSpecies = 0x01, MonFlagInstinct = 0x04, MonFlagDescriptor1 = 0x08, MonFlagDescriptor2 = 0x10;

        // monstrosity::HandleEquipChangePacket (mon_data_t: Flags0 @0x0A, SpeciesIndex @0x0C, Slots u16[12] @0x10,
        // Descriptor1Index @0x28, Descriptor2Index @0x29): one change each 250 ms; a species change, else the instincts
        // (each one known and owned, no duplicates, within the point limit), else a name descriptor; saved and resent
        private void MonstrosityEquipChange(CharData c, BasicPacket p)
        {
            byte flags = p.Buffer[0x0A];
            if ((flags & (MonFlagSpecies | MonFlagInstinct | MonFlagDescriptor1 | MonFlagDescriptor2)) == 0) return;
            var data = c.Monstrosity;
            long now = NowMs();
            if (now < data.LastEquipChangeMs + Monstrosity.EquipChangeCooldownMs) return;
            data.LastEquipChangeMs = now;
            if ((flags & MonFlagSpecies) != 0)
            {
                ChangeMonstrositySpecies(c, BitConverter.ToUInt16(p.Buffer, 0x0C));
                return;
            }
            var table = MonTable();
            if ((flags & MonFlagInstinct) != 0)
            {
                var previous = (ushort[])data.EquippedInstincts.Clone();
                var equipped = (ushort[])previous.Clone();
                for (int i = 0; i < equipped.Length; i++)
                {
                    ushort slot = BitConverter.ToUInt16(p.Buffer, 0x10 + i * 2);
                    if (slot == 0xFFFF) equipped[i] = 0;
                    else if (slot != 0)
                    {
                        if (table.FindInstinct(slot) == null || !Monstrosity.IsInstinctUnlocked(data, slot)) return;
                        equipped[i] = slot;
                    }
                }
                var set = new List<ushort>(equipped);
                set.RemoveAll(id => id == 0);
                bool duplicates = new HashSet<ushort>(set).Count != set.Count;
                if (equipped.AsSpan().SequenceEqual(previous) ||
                    Monstrosity.InstinctsCost(table, equipped) > Monstrosity.InstinctPointLimit(data) || duplicates) return;
                for (int i = 0; i < equipped.Length; i++)
                {
                    if (equipped[i] == previous[i]) continue;
                    Monstrosity.DelInstinctMods(table, c, previous[i]);
                    Monstrosity.AddInstinctMods(table, c, equipped[i]);
                }
                Array.Copy(equipped, data.EquippedInstincts, equipped.Length);
                // instinct HP is scaled by the species
                loader_.CalculateStats(c);
                CharStats.UpdateHealth(c);
            }
            else if ((flags & MonFlagDescriptor1) != 0) data.NamePrefix1 = p.Buffer[0x28];
            else if ((flags & MonFlagDescriptor2) != 0) data.NamePrefix2 = p.Buffer[0x29];
            WriteMonstrosityData(c);
            SendInstinctChangePackets(c);
        }

        // sendInstinctChangePackets
        private void SendInstinctChangePackets(CharData c)
        {
            if (loader_.Stats is not { } stats) return;
            RefreshMonstrosityState(c);
            if (SessionOf(c) is MapSession s) SendInventory(s, c);
            PushTo(c, LoginPackets.EquipClear());
            if (SessionOf(c) is MapSession ms) SendExtendedJobPackets(ms, c);
            PushTo(c, LoginPackets.GroupAttr(c));
            PushTo(c, SelfPackets.CliStatus(c, stats, UnityStatus(c)));
            PushTo(c, SelfPackets.CliStatus2(c));
            PushTo(c, AbilRecast(c));
            PushTo(c, SelfPackets.Merits(c, stats));
            PushTo(c, MonPage1(c));
            PushTo(c, SelfPackets.JobPoints(c));
            PushTo(c, CharStatus(c));
            PushTo(c, CharSyncOf(c));
            c.UpdateMask |= LoginPackets.UpdateAllChar;
        }

        // CCharEntity::resyncEquipment: EQUIP_CLEAR, then each equipped slot's 0x050 again
        private void ResyncEquipment(CharData c)
        {
            PushTo(c, LoginPackets.EquipClear());
            foreach (var e in c.Equip.FindAll(e => e.EquipSlot < 16)) PushTo(c, LoginPackets.EquipList(e.InvSlot, e.EquipSlot, e.Container));
        }

        // charutils::SendInventory
        private void SendInventory(MapSession s, CharData c)
        {
            var list = new List<BasicPacket>();
            GameOkPackets.AddInventory(list, c);
            foreach (var p in list) Push(s, p);
        }

        // monstrosity::SetBelligerencyFlag
        private void SetBelligerencyFlag(CharData c, bool flag)
        {
            if (c.Monstrosity == null) return;
            c.Monstrosity.Belligerency = flag;
            UpdateMonstrosityRank(c, GetPoints(c, "infamy"));
            WriteMonstrosityData(c);
        }

        // monstrosity::UpdateRank: a lower rank affords fewer instinct points
        private void UpdateMonstrosityRank(CharData c, int infamy)
        {
            var data = c.Monstrosity;
            byte rank = Monstrosity.RankFor(data, infamy);
            if (rank == data.Rank) return;
            data.Rank = rank;
            if (Monstrosity.UnequipInstinctsOverLimit(MonTable(), c))
            {
                WriteMonstrosityData(c);
                loader_.CalculateStats(c);
                CharStats.UpdateHealth(c);
                SendInstinctChangePackets(c);
                return;
            }
            PushTo(c, CharStatus(c));
        }

        // gainInfamy: up to the cap (higher under Belligerency), never taking away what is held above it; crossing into
        // a higher rank announces it
        private void GainInfamy(CharData c, int infamy)
        {
            if (infamy <= 0) return;
            int cap = c.Monstrosity.Belligerency ? Monstrosity.InfamyCapBelligerency : Monstrosity.InfamyCap;
            int held = GetPoints(c, "infamy");
            byte previousRank = c.Monstrosity.Rank;
            SetPoints(c, "infamy", Math.Min(held + infamy, Math.Max(cap, held)));
            if (c.Monstrosity.Rank > previousRank) PushTo(c, SelfPackets.BattleMessage(c, 0, 0, MsgNewfoundPuissance));
        }

        private const ushort MsgNewfoundPuissance = 680, MsgPossessNewMonster = 677, MsgLearnedNewInstinct = 678,
            MsgLearnsAbility = 45;
        private const int SpellShantotto = 896; // the first Trust: the 0x0AA filter stops there
        private const int PrestigeCapFromKills = 10000;

        // monstrosity::HandleLevelUp: the family's level follows MON's (already raised), with the unlocks it reaches, the
        // species' TP moves of that level and an instinct each 30 levels
        private void MonstrosityHandleLevelUp(CharData c)
        {
            var table = MonTable();
            var data = c.Monstrosity;
            byte mLvl = c.JobLevels[Monstrosity.JobMon];
            data.Levels[data.MonstrosityId] = mLvl;
            // TODO: What does retail's 32 mean?
            for (int count = Monstrosity.GrantLevelUnlocks(table, data); count > 0; count--)
                PushTo(c, SelfPackets.BattleMessage(c, 32, 0, MsgPossessNewMonster));
            // the client shows its lightbulb on this message; retail sends it for moves not enabled yet too
            if (table.FindSpecies(data.Species) is { } species)
                foreach (var skill in species.TpSkills)
                    if (skill.UnlockLevel == mLvl) PushTo(c, SelfPackets.BattleMessage(c, skill.MobSkillId, 0, MsgLearnsAbility));
            if (mLvl % Monstrosity.LevelsPerInstinct == 0 && mLvl <= Monstrosity.LevelsPerInstinct * Monstrosity.LevelInstincts)
            {
                Monstrosity.ApplyLevelInstincts(table, data);
                PushTo(c, SelfPackets.BattleMessage(c, 0, 0, MsgLearnedNewInstinct));
            }
            WriteMonstrosityData(c);
        }

        // the 0x0AA mask (monstrosity::ListsSpell) over the spells before the Trusts, at the species' level
        private void RefreshMonstrositySpells(CharData c)
        {
            LoadSpells();
            var mask = new byte[c.SpellList.Length];
            Array.Fill(mask, (byte)0xFF);
            for (int id = 0; id < SpellShantotto && id / 8 < mask.Length; id++)
                if (!spells_.TryGetValue((ushort)id, out var spell) || !Monstrosity.ListsSpell(c.Monstrosity, c.MLvl, spell))
                    mask[id / 8] &= (byte)~(1 << (id % 8));
            c.Monstrosity.ListedSpells = mask;
        }

        // monstrosity::AddPrestige: a gladiator's kills pay a tenth of the exp, up to the cap (never taking away what is
        // held above it)
        private void AddPrestige(CharData c, uint exp)
        {
            if (!Monstrosity.IsGladiatorInBelligerency(c)) return;
            int prestige = (int)(exp / Monstrosity.ExpPerPoint);
            if (prestige <= 0) return;
            int held = GetPoints(c, "prestige");
            SetPoints(c, "prestige", Math.Min(held + prestige, Math.Max(PrestigeCapFromKills, held)));
        }

        // luautils::OnMonstrosityReturnToEntrance: xi.monstrosity.onMonstrosityReturnToEntrance(player)
        private void OnMonstrosityReturnToEntrance(CharData c)
        {
            if (lua_ == null) return;
            int top = lua_.Top;
            try
            {
                lua_.GetPath("xi.monstrosity.onMonstrosityReturnToEntrance");
                if (lua_.Type(-1) != LuaNative.LUA_TFUNCTION)
                {
                    log_("luautils::onMonstrosityReturnToEntrance: function not found");
                    return;
                }
                lua_.Push(c);
                lua_.Call(1, 0);
            }
            catch (LuaException e)
            {
                log_($"luautils::onMonstrosityReturnToEntrance: {e.Message}");
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // ---- the Lua tables ----

        // luautils::GetMonstrosityLuaTable: the arrays keep 0-based keys, matching the C++
        private static void PushMonstrosityTable(LuaState l, MonstrosityData data)
        {
            l.NewTable();
            void Num(string key, double v)
            {
                l.Push(v);
                l.SetField(-2, key);
            }
            void Arr(string key, byte[] values)
            {
                l.NewTable();
                for (int i = 0; i < values.Length; i++)
                {
                    l.Push((double)values[i]);
                    l.RawSetI(-2, i);
                }
                l.SetField(-2, key);
            }
            Num("monstrosityId", data.MonstrosityId);
            Num("species", data.Species);
            Num("flags", data.Flags);
            Num("entry_x", data.EntryX);
            Num("entry_y", data.EntryY);
            Num("entry_z", data.EntryZ);
            Num("entry_rot", data.EntryRot);
            Num("entry_zone_id", data.EntryZoneId);
            Num("entry_mjob", data.EntryMainJob);
            Num("entry_sjob", data.EntrySubJob);
            Arr("levels", data.Levels);
            Arr("instincts", data.Instincts);
            Arr("variants", data.Variants);
        }

        // luautils::SetMonstrosityLuaTable: each field kept when left out; the levels set, the instincts and variants
        // OR-ed in (an index past the array throws, as std::array::at does)
        private static void ReadMonstrosityTable(LuaState l, int t, MonstrosityData data)
        {
            data.MonstrosityId = (byte)FieldInt(l, t, "monstrosityId", data.MonstrosityId);
            data.Species = (ushort)FieldInt(l, t, "species", data.Species);
            data.Flags = (ushort)FieldInt(l, t, "flags", data.Flags);
            data.EntryX = (float)FieldNumber(l, t, "entry_x", data.EntryX);
            data.EntryY = (float)FieldNumber(l, t, "entry_y", data.EntryY);
            data.EntryZ = (float)FieldNumber(l, t, "entry_z", data.EntryZ);
            data.EntryRot = (byte)FieldInt(l, t, "entry_rot", data.EntryRot);
            data.EntryZoneId = (ushort)FieldInt(l, t, "entry_zone_id", data.EntryZoneId);
            data.EntryMainJob = (byte)FieldInt(l, t, "entry_mjob", data.EntryMainJob);
            data.EntrySubJob = (byte)FieldInt(l, t, "entry_sjob", data.EntrySubJob);
            void Arr(string key, byte[] into, bool or)
            {
                l.GetField(t, key);
                if (l.Type(-1) == LuaNative.LUA_TTABLE)
                {
                    int idx = l.Top;
                    ForEachTablePair(l, idx, () =>
                    {
                        int k = (byte)l.ToIntegral(-2); // keyObj.as<uint8>()
                        if (k < 0 || k >= into.Length) throw new LuaException($"array::at: {key}[{k}] is out of range");
                        byte v = (byte)l.ToIntegral(-1);
                        into[k] = or ? (byte)(into[k] | v) : v;
                    });
                }
                l.Pop(1);
            }
            Arr("levels", data.Levels, false);
            Arr("instincts", data.Instincts, true);
            Arr("variants", data.Variants, true);
        }

        private void RegisterMonstrosityBindings(Dictionary<string, LuaFunction> m)
        {
            // getMonstrosityData: the Monipulator's data, else the saved progress (a fresh one without a row); nil for
            // a non-char
            m["getMonstrosityData"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    l.PushNil();
                    return 1;
                }
                PushMonstrosityTable(l, c.Monstrosity ?? Monstrosity.Load(db_, MonTable(), c.Id));
                return 1;
            };
            // setMonstrosityData(table): a Monipulator's data saved and sent in full; outside MON only the saved progress
            m["setMonstrosityData"] = l =>
            {
                if (Entity(l) is not CharData c || l.Type(2) != LuaNative.LUA_TTABLE) return 0;
                if (c.Monstrosity != null)
                {
                    ReadMonstrosityTable(l, 2, c.Monstrosity);
                    WriteMonstrosityData(c);
                    SendFullMonstrosityUpdate(c);
                    return 0;
                }
                var data = Monstrosity.Load(db_, MonTable(), c.Id);
                ReadMonstrosityTable(l, 2, data);
                Monstrosity.Save(db_, c.Id, data);
                return 0;
            };
            m["getBelligerencyFlag"] = l => Ret(l, Entity(l) is CharData { Monstrosity: { } data } && data.Belligerency);
            m["setBelligerencyFlag"] = l =>
            {
                if (Entity(l) is CharData c) SetBelligerencyFlag(c, l.ToBoolean(2));
                return 0;
            };
            // changeMonstrositySpecies(code): a species the Monipulator owns, as the Feretory menu would take it
            m["changeMonstrositySpecies"] = l =>
            {
                if (Entity(l) is CharData { Monstrosity: not null } c) ChangeMonstrositySpecies(c, (ushort)l.ToIntArg(2));
                return 0;
            };
            m["getMonstrositySize"] = l => Ret(l, Entity(l) is CharData { Monstrosity: { } data } ? data.Size : 0);
            // setMonstrosityEntryData(x, y, z, rot, zone, mjob, sjob): where and as what the char entered; outside MON
            // only the saved progress changes
            m["setMonstrosityEntryData"] = l =>
            {
                if (Entity(l) is not CharData c) return 0;
                var data = c.Monstrosity ?? Monstrosity.Load(db_, MonTable(), c.Id);
                data.EntryX = (float)l.ToNumber(2);
                data.EntryY = (float)l.ToNumber(3);
                data.EntryZ = (float)l.ToNumber(4);
                data.EntryRot = (byte)l.ToIntArg(5);
                data.EntryZoneId = (ushort)l.ToIntArg(6);
                data.EntryMainJob = (byte)l.ToIntArg(7);
                data.EntrySubJob = (byte)l.ToIntArg(8);
                Monstrosity.Save(db_, c.Id, data);
                return 0;
            };
            // getMonstrosityShop: Teyrnon's pages, each a 0-based list of { infamyCost, requirements = { {family, level} },
            // monSpecies or monVariant }
            m["getMonstrosityShop"] = l =>
            {
                l.NewTable();
                foreach (var (page, slots) in MonTable().TeyrnonShop)
                {
                    l.NewTable();
                    for (int i = 0; i < slots.Count; i++)
                    {
                        var slot = slots[i];
                        l.NewTable();
                        l.Push((double)slot.Infamy);
                        l.SetField(-2, "infamyCost");
                        l.NewTable();
                        for (int r = 0; r < slot.Requirements.Count; r++)
                        {
                            l.NewTable();
                            l.Push((double)slot.Requirements[r].Family);
                            l.RawSetI(-2, 1);
                            l.Push((double)slot.Requirements[r].Level);
                            l.RawSetI(-2, 2);
                            l.RawSetI(-2, r + 1);
                        }
                        l.SetField(-2, "requirements");
                        if (slot.Species is byte sp)
                        {
                            l.Push((double)sp);
                            l.SetField(-2, "monSpecies");
                        }
                        if (slot.Variant is byte v)
                        {
                            l.Push((double)v);
                            l.SetField(-2, "monVariant");
                        }
                        l.RawSetI(-2, i);
                    }
                    l.RawSetI(-2, page);
                }
                return 1;
            };
            m["isMonstrosityPassageZone"] = l => Ret(l, Monstrosity.IsPassageZone((ushort)l.ToEnumArg(2)));
            // getMonstrosityExits(zone): the Feretory's drop points in that zone, each { x, y, z, rot }
            m["getMonstrosityExits"] = l =>
            {
                var exits = Monstrosity.GetFeretoryExits((ushort)l.ToEnumArg(2));
                l.NewTable();
                for (int i = 0; i < exits.Count; i++)
                {
                    var (x, y, z, rot) = exits[i];
                    l.NewTable();
                    l.Push((double)x);
                    l.RawSetI(-2, 1);
                    l.Push((double)y);
                    l.RawSetI(-2, 2);
                    l.Push((double)z);
                    l.RawSetI(-2, 3);
                    l.Push((double)(byte)rot);
                    l.RawSetI(-2, 4);
                    l.RawSetI(-2, i + 1);
                }
                return 1;
            };
        }
    }
}
