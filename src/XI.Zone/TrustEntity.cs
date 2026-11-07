using System.Collections.Generic;

namespace XI.Zone
{
    // CTrustEntity: a char's alter ego, a CMobEntity whose stats trustutils::LoadTrust fills from its mob_pools row
    // (spell id + 5000) and its master's level, in the zone's trust list under a dynamic targid. Its controller state
    // (CTrustController's follow, rest and combat movement) and the gambits its script adds are here too. Its lifecycle
    // is in ZoneHost.Trusts.cs, the gambits in ZoneHost.Gambits.cs.
    public sealed class TrustEntity : MobEntity
    {
        public readonly uint TrustId;
        public readonly bool Passive;     // passiveTrust_: a walking GEO bubble, never a target
        public bool Released;             // released_: its master let it go (the 2 s before it leaves)

        // CTrustController: the rest's timing (m_tickDelays, m_NumHealingTicks) and the end of the last fight
        public long LastHealTickMs, CombatEndMs;
        public long LastAttackedMs;       // CBattleEntity::LastAttacked
        public int NumHealingTicks;
        // PathOutToDistance: m_LastRepositionTime, m_failedRepositionAttempts, m_InTransit
        public long LastRepositionMs;
        public int FailedRepositionAttempts;
        public bool InTransit;
        public long LastTrustRangedMs = long.MinValue / 2; // CTrustController::m_LastRangedAttackTime

        // CGambitsContainer: what addGambit / setTrustTPSkillSettings set, m_lastAction and the TIMER predicates' last
        // trigger (by "gambit id:group:predicate")
        public readonly List<TrustGambit> Gambits = new List<TrustGambit>();
        public ushort TpTrigger, TpSelect, TpValue;
        // tp_skills: what LoadTrustStatsAndSkills makes of its mob skill list (the weapon skills, the mob skills)
        public readonly List<TrustSkill> TpSkills = new List<TrustSkill>();
        internal ZoneHost.WeaponSkillState WsState; // CWeaponSkillState: a weapon skill being used
        public ushort WeaponSkillInUse => WsState?.Skill.Id ?? 0;
        public long LastGambitActionMs;
        public readonly Dictionary<string, long> TimerConditionLastTrigger = new Dictionary<string, long>();

        public TrustEntity(CharData master, uint trustId, bool passive)
        {
            TrustId = trustId;
            Passive = passive;
            Ecosystem = XI.Zone.Ecosystem.Unclassified;
            Allegiance = CharData.AllegiancePlayer;
            MobSkillList = 0;
            PMaster = master;
            SpawnAnimation = 1; // the initial spawn has the special spawn-in animation
        }
    }

    // TrustSkill_t: a TP skill (G_REACTION WS or MS), its skillchain properties and its valid targets
    public sealed class TrustSkill
    {
        public ushort Reaction, SkillId, ValidTargets;
        public byte Primary, Secondary, Tertiary;
    }

    // Gambit_t as addGambit reads it: the target selector, the predicate groups ({ condition, argument } pairs with
    // their logic), the reactions ({ reaction, selector, argument }) and the retry delay
    public sealed class TrustGambit
    {
        public string Id = "";
        public ushort Target;
        public readonly List<(ushort Logic, List<(ushort Condition, uint Argument)> Predicates)> PredicateGroups =
            new List<(ushort, List<(ushort, uint)>)>();
        public readonly List<(ushort Reaction, ushort Select, uint Argument)> Actions = new List<(ushort, ushort, uint)>();
        public ushort RetryDelaySec;
        public long LastUsedMs;           // last_used
    }
}
