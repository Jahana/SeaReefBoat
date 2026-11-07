using System;

namespace XI.Zone
{
    // The part of CBaseEntity / CBattleEntity a char and a mob share: identity, position, jobs and levels, health,
    // modifiers and the animation. A char's own state stays in CharData, a mob's in MobEntity.
    public abstract class BattleEntity
    {
        public uint Id;
        public ushort TargId;
        public string Name = "";

        // CBaseEntity::m_localVars
        public readonly System.Collections.Generic.Dictionary<string, uint> LocalVars = new System.Collections.Generic.Dictionary<string, uint>();
        // PAI->EventHandler: the scripts' listeners
        public readonly AiEventHandler Events = new AiEventHandler();

        // loc: zone and position. Internal y is height; packets put it in their "z" field.
        public ushort Zone;
        public Instance Instance; // PInstance: the instance it is in, null outside one
        public float X, Y, Z;
        public byte Rotation;
        // loc.p.moving (the client's MoveFlame for a char)
        public ushort Moving;

        public byte MJob, MLvl;
        // CBattleEntity::m_sjob / m_slvl as set (SetSJob / SetSLevel); SJob and SLvl read them as CBattleEntity::GetSJob /
        // GetSLevel do: no sub job, and level 0, under Obliviscence or SJ Restriction (GetSJob(true) is SJobRaw)
        public byte SJobRaw, SLvlRaw;
        public byte SJob { get => SubjobRestricted ? (byte)0 : SJobRaw; set => SJobRaw = value; }
        public byte SLvl { get => SubjobRestricted ? (byte)0 : SLvlRaw; set => SLvlRaw = value; }
        private static readonly ushort[] SubjobRestrictions = { EffectId.Obliviscence, EffectId.SjRestriction };
        private bool SubjobRestricted => StatusEffects != null && StatusEffects.HasStatusEffect(SubjobRestrictions);

        // MaxHp/MaxMp are GetMaxHP/GetMaxMP (health.modhp, after UpdateHealth); BaseMaxHp/BaseMaxMp are
        // health.maxhp/maxmp
        public int Hp, Mp, Tp, MaxHp, MaxMp, BaseMaxHp, BaseMaxMp;
        // health.latenthp / latentmp: UpdateHealth's base HP/MP before food, which the HP/MP%-of-visible-gear latents read
        public int LatentHp, LatentMp;

        // m_modStat
        public readonly short[] ModStat = new short[Mods.Count];
        public short GetMod(Mod mod) => ModStat[(int)mod];

        // the CMagicState on top while a spell is cast (ZoneHost.Magic.cs)
        public MagicCastState MagicState;
        public MobSkillState SkillState;  // the CMobSkillState on top of the AI's state stack while a skill readies / lands
        public long SwingAtMs;            // the attack state's next swing
        // the CInactiveState on top while stunned, asleep or otherwise kept from acting (ZoneHost.Inactive.cs)
        public InactiveState InactiveState;

        // CBattleEntity::addModifier / delModifier / setModifier
        public void AddModifier(Mod mod, short amount) { if (mod != 0) ModStat[(int)mod] += amount; }
        public void DelModifier(Mod mod, short amount) { if (mod != 0) ModStat[(int)mod] -= amount; }
        public void SetModifier(Mod mod, short value) { if (mod != 0) ModStat[(int)mod] = value; }

        // CBattleEntity::TraitList and hasTrait (addTrait / delTrait are ZoneHost.Traits)
        public readonly System.Collections.Generic.List<Trait> TraitList = new System.Collections.Generic.List<Trait>();
        public bool HasTrait(int traitId) => TraitList.Exists(t => t.Id == traitId);

        // updatemask: what the next tick re-sends (UPDATE_POS / UPDATE_STATUS / UPDATE_HP ...)
        public byte UpdateMask;

        // speed / animationSpeed as the entity update sends them
        public byte Speed, AnimationSpeed;
        public byte DeathType;                      // m_DeathType (setDeathType)

        public byte Animation;                     // xi::Animation: 0 none, 1 attack, 3 death, 33 healing

        // m_unkillable: HP never drops below 1; m_battleStartTime: when the attack state last started
        public bool Unkillable;
        public long BattleStartMs;

        // CBaseEntity::PBattlefield: the battlefield the entity is physically inside (ZoneHost.Battlefield.cs)
        public Battlefield PBattlefield;
        // m_battleID: entities interact only while theirs match (setBattleID); 0 again at each spawn
        public ushort BattleId;

        // StatusEffectContainer
        public readonly StatusEffectContainer StatusEffects;
        protected BattleEntity()
        {
            StatusEffects = new StatusEffectContainer(this);
            Notoriety = new NotorietyContainer(this);
        }

        // PNotorietyContainer: the mobs with this entity on their enmity list
        public readonly NotorietyContainer Notoriety;

        // PRecastContainer: a char's CharRecastContainer comes with its load; other entities get one when a script
        // first adds a recast
        public RecastContainer Recasts;

        // CBattleEntity::PPet / PMaster: the master<->pet link a script's setPet makes (mob pets via xi.pet.setMobPet)
        public BattleEntity PPet, PMaster;
        // CBaseEntity::losCache_: the last line-of-sight answers (ZoneHost.XiMesh.cs)
        public LineOfSightCache LosCache;
        // isCharmed and charmTime: charmed (a mob by a BST, a char by a mob) and, for a charmed mob, until when
        public bool IsCharmed;
        public long CharmTimeMs;
        // CBattleEntity::m_petMod: the mods this master gives its pet, by PetModType (ZoneHost.AddPetModifier)
        public readonly System.Collections.Generic.Dictionary<(byte PetModType, Mod Mod), short> PetMods =
            new System.Collections.Generic.Dictionary<(byte, Mod), short>();

        public byte Hpp => Percent(Hp, MaxHp);
        public byte Mpp => Percent(Mp, MaxMp);
        public bool IsAlive => Hp > 0;

        // GetHPP: 0 at 0, else at least 1 of floor(cur/max*100) in float math
        protected static byte Percent(int cur, int max) =>
            cur == 0 || max == 0 ? (byte)0 : (byte)Math.Max(1, (int)MathF.Floor((float)cur / max * 100.0f));
    }
}
