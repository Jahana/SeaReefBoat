using System;
using System.Collections.Generic;

namespace XI.Zone
{
    // xi::MobMod (data/enums/mob_mod.yaml)
    public enum MobMod : ushort
    {
        None = 0, GilMin = 1, GilMax = 2, MpBase = 3, SightRange = 4, SoundRange = 5, BuffChance = 6, GaChance = 7, HealChance = 8,
        HpHealChance = 9, Sublink = 10, LinkRadius = 11, SeesThroughIllusion = 12, SevereSpellChance = 13, SkillList = 14, MugGil = 15,
        Detection = 16, NoDespawn = 17, Var = 18, CanShieldBlock = 19, NoH2hPenalty = 20, PetSpellList = 21, NaChance = 22,
        Immunity = 23, GradualRage = 24, BuildResist = 25, Superlink = 26, SpellList = 27, ExpBonus = 28, Assist = 29,
        SpecialSkill = 30, RoamDistance = 31, DontRoamHome = 32, SpecialCool = 33, MagicCool = 34, StandbackCool = 35, RoamCool = 36,
        AlwaysAggro = 37, NoDrops = 38, SharePos = 39, TeleportCd = 40, TeleportStart = 41, TeleportEnd = 42, TeleportType = 43,
        DualWield = 44, AddEffect = 45, AutoSpikes = 46, SpawnLeash = 47, ShareTarget = 48, CheckAsNm = 49, RoamResetFacing = 50,
        RoamTurns = 51, RoamRate = 52, Behavior = 53, GilBonus = 54, IdleDespawn = 55, HpStandback = 56, MagicDelay = 57,
        SpecialDelay = 58, BaseDamageModifier = 59, SpawnAnimationsub = 60, HpScale = 61, NoStandback = 62, AttackSkillList = 63,
        Charmable = 64, NoMove = 65, MultiHit = 66, NoAggro = 67, AlliHate = 68, NoLink = 69, NoRest = 70, Leader = 71,
        MagicRange = 72, TargetDistanceOffset = 73, OneWayLinking = 74, CanParry = 75, NoWidescan = 76, TrustDistance = 77,
        StandbackRange = 78, CannotGuard = 79, SkipAllegianceCheck = 80, AbilityResponse = 81, RunSpeedMult = 82, ClaimType = 83,
        NoSpellCost = 84, AstralPetOffset = 85, BaseDamageMultiplier = 86, DamageOffset = 87, RangedDamageOffset = 88,
        AvatarPetid = 89, AvatarAstralDelay = 90, H2hSingleSwing = 91, AoeHitAll = 92, RangedAttackRange = 93,
        FollowLeashRange = 94, FollowStopRange = 95, TrustShieldSize = 96, Bodyguard = 97, NoStuckTeleport = 98, RoamTurnsMin = 99,
        IgnorePets = 100,
    }

    // xi::MobType, xi::SpawnType and xi::RoamFlag bits
    public static class MobType
    {
        public const ushort Normal = 0, Notorious = 0x02, Fished = 0x04, Called = 0x08, Battlefield = 0x10, Event = 0x20;
    }

    public static class SpawnType
    {
        public const ushort Normal = 0, AtNight = 0x01, AtEvening = 0x02, Weather = 0x04, Fog = 0x08, MoonPhase = 0x10,
            Lottery = 0x20, Windowed = 0x40, Scripted = 0x80;
    }

    public static class RoamFlag
    {
        public const ushort None = 0, Worm = 0x40, Ambush = 0x80, Scripted = 0x100, Ignore = 0x200, Stealth = 0x400, Follow = 0x800;
    }

    // CMobEntity as zoneutils::InsertMobs and mobutils::InitializeMob build it. Its AI is ZoneHost.MobAi.cs (roaming
    // and paths ZoneHost.MobRoam.cs), its stats and traits MobStats.cs, its spell lists MobSpells.cs, its loot
    // ZoneHost.Loot.cs and its script ZoneHost.MobScripts.cs; the status effects are BattleEntity's.
    // not sealed: a player pet (PetEntity) is one, as CPetEntity is a CMobEntity
    public class MobEntity : BattleEntity, IPathMover
    {
        float IPathMover.X { get => X; set => X = value; }
        float IPathMover.Y { get => Y; set => Y = value; }
        float IPathMover.Z { get => Z; set => Z = value; }
        byte IPathMover.Rotation { get => Rotation; set => Rotation = value; }
        ushort IPathMover.Moving { get => Moving; set => Moving = value; }
        byte IPathMover.Speed => Speed;
        byte IPathMover.Animation => Animation;
        BattleEntity IPathMover.PathBattleTarget => BattleTarget;
        void IPathMover.MarkPositionDirty() => UpdateMask |= EntityUpdatePacket.UpdatePos;
        float IPathMover.PathHitboxRadius => ModelHitboxSize * 0.5f;

        public const int MobModCount = 101;
        public const byte NameVisHideName = 0x08;

        public string PacketName = "";   // the template's display_name
        public uint Pool;                // the template id
        public ushort[] Look = new ushort[10];
        public byte AnimationSub;
        public byte Status = NpcEntity.StatusDisappear;
        public byte NameVis, NamePrefix, ModelSize, Allegiance;
        public bool IsRenamed;           // isRenamed: renameEntity's name goes out with every update
        public bool IsAggroable;         // m_isAggroable: other mobs may aggro it while it fights for the players
        public bool CallForHelpBlocked;  // m_CallForHelpBlocked: a char's call for help is refused
        public bool ItemStolen, ItemDespoiled; // m_ItemStolen / m_ItemDespoiled: the steal or despoil already taken
        public uint EntityFlags;
        public byte SpawnAnimation; // xi::SpawnAnimation: 1 Special (setSpawnAnimation)
        public float ModelHitboxSize;
        // CMobController::targetLosCache_: the line of sight to the battle target, one raycast a tick
        public BattleEntity LosTickTarget;
        public long LosTickAt = -1;
        public bool LosTickResult;
        // m_disableScent: rain, squall or blizzards wash out a scent tracker's trail (CZoneEntities::WeatherChange)
        public bool DisableScent;
        public bool PriorityRender;
        public bool Hidden;        // SPECIALFLAG_HIDDEN (an ambusher)
        // m_flags' EntityFlags::Untargetable
        public bool Untargetable
        {
            get => (EntityFlags & EntityFlagUntargetable) != 0;
            set => EntityFlags = value ? EntityFlags | EntityFlagUntargetable : EntityFlags & ~EntityFlagUntargetable;
        }

        public ushort Type = MobType.Normal;
        public ushort Species, Family;
        public byte Ecosystem;
        public byte Element;
        public byte StrRank = 3, DexRank = 3, VitRank = 3, AgiRank = 3, IntRank = 3, MndRank = 3, ChrRank = 3;
        public byte DefRank = 3, EvaRank = 3, AttRank = 3, AccRank = 3;
        public byte BaseSpeed;

        public readonly short[] MobMods = new short[MobModCount];
        private readonly short[] mobModsSave_ = new short[MobModCount];
        private readonly short[] modStatSave_ = new short[Mods.Count];

        // stats.STR .. CHR and WorkingSkills.skill
        public readonly ushort[] Stats = new ushort[7];
        public readonly ushort[] WorkingSkills = new ushort[Skill.Max];

        public ushort Behavior;
        public uint Immunity;
        public bool Aggro, Link, TrueDetection;
        // PParty: the link party CZoneEntities::FindPartyForMob put it in (shared by its members), and the
        // controller's linkScanThisTick_
        public List<MobEntity> LinkParty;
        // m_bcnmID / m_battlefieldID: the battlefield and the area that last took the mob in (InsertEntity)
        public ushort BcnmId;
        public byte BattlefieldArea;
        // m_bReleaseTargIDOnDisappear: a dynamic entity leaves the zone once it disappears (ZoneHost.DynamicEntities.cs)
        public bool ReleaseTargIdOnDisappear;
        public bool LinkScanThisTick;
        public ushort RoamFlags;
        public ushort MobSkillList, SpellListId;
        // m_SpellListContainer (the shared source list, from a spell_list_id or a template's inline spells) and
        // SpellContainer (this mob's level-filtered, bucketed castable spells; CMobSpellContainer)
        public MobSpellList SpellList;
        public readonly MobSpellContainer SpellContainer = new MobSpellContainer();
        public float MaxRoamDistance = 50.0f;

        public uint HpModifier, MpModifier;
        public float MpScale = 1.0f;

        // the mob's weapons: main / sub / ranged damage and the main's skill and delay
        public byte WeaponSkill;
        public ushort MainDelay = 480, BaseDelay = 480; // the main CItemWeapon's m_delay (8000 ms) and m_baseDelay
        public ushort RangedBaseDelay = 480; // CItemWeapon's m_baseDelay, 300 for SetupRangedAttack
        public ushort MainDamage, SubDamage, RangedDamage;
        // m_fTPModifierOverrides: a mob skill's fTP table set from script (setfTPModifierOverride)
        public readonly Dictionary<ushort, float[]> FtpOverrides = new Dictionary<ushort, float[]>();
        public ushort DamageMultiplier = 100;

        public uint OwnerId;
        public ushort BattleTargId;      // m_TargID: the targid the mob faces
        public long NextUpdateMs;        // m_nextUpdateTimer
        public long FadeAtMs;            // a CDespawnState under way: when the mob fades out, 0 if none
        public uint RespawnTimeSec = 300;
        public ushort SpawnType; // xi::SpawnType bits
        public (byte Start, byte End)? SpawnWindow;
        public byte MinLevel = 1, MaxLevel = 1;
        public float SpawnX, SpawnY, SpawnZ;
        public byte SpawnRotation;
        public List<RoamRegion> Regions = new List<RoamRegion>();
        public RoamRegion RoamRegion;
        public List<(float X, float Y, float Z)> Route = new List<(float, float, float)>();
        public SpawnSlot Slot;
        public bool AllowRespawn, CanSpawn;
        public long DespawnAtMs; // SetDespawnTime: 0 while no despawn is pending

        // combat: lastAttackerId_, the death state's despawn, the queued defeat message (due m_DropItemTime after
        // the death: 1 s from each spawn, a killing spell's animation time), the controller's m_DeclaimTime, and the
        // enmity list as who has any and how much damage they dealt
        public uint LastAttackerId;
        public long DeathAtMs, DefeatMsgAtMs, DeclaimTimeMs;
        public long DropItemTimeMs = 1000;
        public readonly EnmityContainer Enmity;
        // the controller and its CAttackState: the target, the swing timer, the deaggro timer, m_HiPCLvl
        public BattleEntity BattleTarget;
        public bool Engaged;
        public long SwingDelayMs, DeaggroTimeMs, NextAiTickMs;
        // m_neutral (no aggro for a while after spawning or disengaging) and the controller's m_NeutralTime,
        // m_LastActionTime and m_mobHealTime
        public bool Neutral;
        // the controller's m_AutoAttackEnabled / m_MagicCastingEnabled / m_WeaponSkillEnabled, and m_LastRoamScript
        public bool AutoAttackEnabled = true, MagicCastingEnabled = true, MobAbilityEnabled = true;
        // the controller's m_RangedAttackEnabled, m_LastRangedAttackTime and CRangeState (ZoneHost.MobRanged.cs)
        public bool RangedAttackEnabled;
        public long LastRangedAttackMs = long.MinValue / 2;
        internal ZoneHost.RangedState RangedState;
        public bool InRangedAttack => RangedState != null;
        public long RangedEntryMs => RangedState?.EntryMs ?? -1;
        public long RangedAimMs => RangedState?.AimMs ?? -1;
        public bool RangedAimDone => RangedState?.Completed ?? false;
        public long LastRoamScriptMs;
        // the controller's chase (Move): the re-path timers and the stuck count, and the directness probe's cache
        public long RePathCooldownEndMs = long.MinValue / 2, LostSightRePathCooldownEndMs = long.MinValue / 2;
        public (float X, float Y, float Z) LastRePathTarget, LastRePathMobPos;
        public int StuckRePathCount;
        public BattleEntity LastDirectProbeTarget;
        public (float X, float Y, float Z) LastDirectProbePos, LastDirectProbeTargetPos;
        public bool LastDirectProbeWasDirect = true;

        // the controller's m_LastSpecialTime (IsSpecialSkillReady / TrySpecialSkill)
        public long LastSpecialMs = long.MinValue / 2;
        public long NeutralAtMs, LastActionMs, MobHealAtMs;
        // PAI->PathFind (made on first use, ZoneHost.MobRoam.cs), the controller's m_WaitTime and m_IsPathingHome
        public PathFind PathFind;
        // the controller's followTarget_ and m_followType (ZoneHost.MobFollow.cs)
        public BattleEntity FollowTarget;
        public byte FollowType;
        public long WaitTimeMs;
        public bool IsPathingHome;
        public byte HiPcLvl;
        public byte HiPartySize;         // m_HiPartySize: the most chars that shared its EXP
        public bool GiveExp = true;      // m_giveExp: set on each spawn
        // m_UsedSkillIds: the mob skills it used (id -> its level then), what a BLU may learn from the kill
        public readonly SortedDictionary<ushort, byte> UsedSkillIds = new SortedDictionary<ushort, byte>();
        public byte LastHitAtkType;      // BattleHistory.lastHitTaken_atkType (xi::AttackType)
        public MobTemplate Template;     // the template it was built from (its loot)
        public bool TemplateLoot;        // m_DropList: the template's loot, until setDropID replaces it
        public uint DropId;              // m_DropID: a mob_droplist id
        public short ThLvl, GilfinderLevel; // m_THLvl / m_GilfinderLevel: the best its attackers brought
        public int TpThreshold;
        internal ZoneHost.AbilityState AbilityState; // CAbilityState: a job ability being used (a trust's, useJobAbility's)
        public ushort AbilityInUse => AbilityState?.Ability.Id ?? 0;
        public long NextMagicMs;          // CMobController::m_nextMagicTime
        public bool FirstSpell;           // m_firstSpell: the aggro spell comes first after engaging
        public long LastMobSkillMs;       // the controller's m_LastMobSkillTime
        public bool IsDead => DeathAtMs != 0;
        public bool CallForHelp => (EntityFlags & EntityFlagCallForHelp) != 0;
        public const uint EntityFlagCallForHelp = 0x20, EntityFlagUntargetable = 0x800;

        // CBattleEntity starts a WAR/WAR; a species or template may name other jobs
        public MobEntity()
        {
            MJob = SJob = 1;
            Enmity = new EnmityContainer(this);
        }

        public bool IsSpawned => Status != NpcEntity.StatusDisappear;
        public bool IsNotorious => (Type & MobType.Notorious) != 0;

        public short GetMobMod(MobMod m) => MobMods[(int)m];
        public void SetMobMod(MobMod m, short value) => MobMods[(int)m] = value;
        public void AddMobMod(MobMod m, short value) => MobMods[(int)m] += value;

        // defaultMobMod: only a mob mod nothing has set yet
        public void DefaultMobMod(MobMod m, short value)
        {
            if (MobMods[(int)m] == 0) MobMods[(int)m] = value;
        }

        public void SaveModifiers() => Array.Copy(ModStat, modStatSave_, ModStat.Length);
        public void RestoreModifiers() => Array.Copy(modStatSave_, ModStat, ModStat.Length);
        public void SaveMobModifiers() => Array.Copy(MobMods, mobModsSave_, MobMods.Length);
        // CBattleEntity::savePetModifiers: the pet-type mods set on a mob pet go into the saved mods, so a restore keeps
        // them
        public void SavePetModifiers(Mod[] mods)
        {
            foreach (var mod in mods) modStatSave_[(int)mod] = ModStat[(int)mod];
        }
        public void RestoreMobModifiers() => Array.Copy(mobModsSave_, MobMods, MobMods.Length);

        public bool IsDualWielding => GetMobMod(MobMod.DualWield) > 0;

        // CBaseEntity::HideName
        public void HideName(bool hide)
        {
            if (hide) NameVis |= NameVisHideName;
            else NameVis &= unchecked((byte)~NameVisHideName);
            UpdateMask |= EntityUpdatePacket.UpdateHp;
        }

        // CMobEntity::FadeOut: CBaseEntity::FadeOut, and the enmity list goes (with it the mob from its targets'
        // notoriety)
        public void FadeOut()
        {
            Status = NpcEntity.StatusDisappear;
            UpdateMask |= EntityUpdatePacket.UpdateHp;
            Enmity.Clear();
        }
    }

    // SpawnSlot: mobs that share one spawn point, at most one of them up at a time. A member with a chance only
    // wins the roll that follows a kill, and a member that was killed waits out its cooldown.
    public sealed class SpawnSlot
    {
        public sealed class Entry
        {
            public MobEntity Mob;
            public byte SpawnChance;
            public long CooldownMs;
            public long ReadyAtMs;
        }

        public readonly uint Id;
        public readonly List<Entry> Entries = new List<Entry>();
        private bool lotteryOpen_;

        public SpawnSlot(uint id) => Id = id;

        public void AddMob(MobEntity mob, byte chance, long cooldownMs)
        {
            Entries.Add(new Entry { Mob = mob, SpawnChance = chance, CooldownMs = cooldownMs });
            mob.Slot = this;
        }

        // SpawnSlot::TrySpawn. canSpawnNow is SpawnHandler::canSpawnNow, spawnCheck luautils::OnMobSpawnCheck (not 0
        // keeps a member out of the roll; a specific mob is not asked, as in the C++).
        public bool TrySpawn(uint? specificMobId, Func<MobEntity, bool> canSpawnNow, Func<MobEntity, int> spawnCheck, Action<MobEntity> spawn,
            long nowMs, Random rng)
        {
            if (specificMobId is uint specific)
            {
                var it = Entries.Find(e => e.Mob.Id == specific);
                if (it == null) return false;
                if (!it.Mob.IsSpawned && canSpawnNow(it.Mob))
                {
                    it.Mob.AllowRespawn = true;
                    spawn(it.Mob);
                    lotteryOpen_ = false;
                    return true;
                }
                return false;
            }

            var chanceSpawns = new List<(uint Chance, MobEntity Mob)>();
            var remaining = new List<MobEntity>();
            uint totalChance = 0;
            foreach (var e in Entries)
            {
                if (e.Mob.IsSpawned) return false;
                if (!canSpawnNow(e.Mob)) continue;
                if (e.ReadyAtMs > nowMs) continue;
                if (!lotteryOpen_ && e.SpawnChance > 0) continue;
                if (spawnCheck(e.Mob) != 0) continue;
                if (e.SpawnChance > 0)
                {
                    totalChance += e.SpawnChance;
                    chanceSpawns.Add((e.SpawnChance, e.Mob));
                }
                else remaining.Add(e.Mob);
            }
            if (chanceSpawns.Count == 0 && remaining.Count == 0) return false;

            MobEntity allowed = null;
            if (totalChance > 0)
            {
                uint roll = (uint)rng.Next(100);
                if (roll < totalChance)
                {
                    uint accumulated = 0;
                    foreach (var (chance, mob) in chanceSpawns)
                    {
                        accumulated += chance;
                        if (roll < accumulated) { allowed = mob; break; }
                    }
                }
            }
            if (allowed == null)
            {
                if (remaining.Count == 0) return false;
                allowed = remaining[rng.Next(remaining.Count)];
            }
            allowed.AllowRespawn = true;
            spawn(allowed);
            lotteryOpen_ = false;
            return true;
        }

        public void OnKill(MobEntity mob, long nowMs)
        {
            lotteryOpen_ = true;
            var e = Entries.Find(x => x.Mob == mob);
            if (e != null) e.ReadyAtMs = nowMs + e.CooldownMs;
        }
    }
}
