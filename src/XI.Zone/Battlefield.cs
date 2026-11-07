using System;
using System.Collections.Generic;
using XI.Script;

namespace XI.Zone
{
    // CBattlefield: one arena of a zone's battlefield (BCNM), from registration to cleanup -- the registered and
    // entered players, the required / additional enemies, the NPCs and allies, the record, the status, the rules,
    // the time limit and the wipe time, its local vars and mob groups. Its behaviour (InsertEntity, RemoveEntity,
    // onTick, Cleanup, CheckInProgress, handleDeath) and the handler are in ZoneHost.Battlefield.cs.
    public sealed class Battlefield
    {
        // BCRULES
        public const ushort RulesAllowSubjobs = 0x01, RulesLoseExp = 0x02;
        // BATTLEFIELDMOBCONDITION
        public const byte ConditionNone = 0x00, ConditionSpawnedAtStart = 0x01, ConditionWinRequirement = 0x02, ConditionDisappearAtStart = 0x04;
        // BATTLEFIELD_LEAVE_CODE
        public const byte LeaveExit = 1, LeaveWin = 2, LeaveWarpDc = 3, LeaveLose = 4;
        // BATTLEFIELD_STATUS
        public const byte StatusOpen = 0, StatusLocked = 1, StatusWon = 2, StatusLost = 3;
        // BATTLEFIELD_RETURN_CODE
        public const byte ReturnWait = 1, ReturnCutscene = 2, ReturnIncrementRequest = 3, ReturnLocked = 4, ReturnReqsNotMet = 5,
                          ReturnBattlefieldFull = 6;

        // BattlefieldMob_t
        public sealed class Mob
        {
            public MobEntity PMob;
            public byte Condition;
        }

        // BattlefieldGroup: the mobs and the Lua callbacks
        public sealed class Group
        {
            public readonly List<uint> MobIds = new List<uint>();
            public LuaRef DeathCallback, RandomDeathCallback, AllDeathCallback, SetupCallback;
            public uint RandomMobId;
        }

        public readonly ushort Id;
        public readonly ZoneInfo Zone;
        public string Name = "";
        public uint InitiatorId;
        public string InitiatorName;
        public byte Area;
        // BattlefieldRecord_t
        public string RecordName = "Someone";
        public long RecordTimeMs = 24 * 3600 * 1000L;
        public int RecordPartySize = 6;
        public byte Status { get; internal set; } = StatusOpen;
        public ushort Rules;
        public readonly long StartMs;
        public long TickMs, FightTickMs;
        public long TimeLimitMs, FinishTimeMs;
        // m_WipeTime (seconds since timer::start_time, as the scripts set it) and m_LastPromptTime (seconds)
        public uint WipeTimeSec, LastTimeUpdateSec;
        public int MaxParticipants = 8;
        public byte LevelCap;
        public uint ArmouryCrate;
        public bool IsMission;
        public bool ShowTimer = true;

        public long CleanupTimeMs;
        public bool CleanedPlayers, CleanupFlag, Attacked;

        public readonly SortedSet<uint> RegisteredPlayers = new SortedSet<uint>();
        public readonly SortedSet<uint> EnteredPlayers = new SortedSet<uint>();
        public readonly List<NpcEntity> NpcList = new List<NpcEntity>();
        public readonly List<Mob> RequiredEnemyList = new List<Mob>();
        public readonly List<Mob> AdditionalEnemyList = new List<Mob>();
        public readonly List<MobEntity> AllyList = new List<MobEntity>();
        public readonly Dictionary<string, ulong> LocalVars = new Dictionary<string, ulong>();
        public readonly List<Group> Groups = new List<Group>();

        public Battlefield(ushort id, ZoneInfo zone, byte area, CharData initiator, long nowMs)
        {
            Id = id;
            Zone = zone;
            Area = area;
            InitiatorId = initiator.Id;
            InitiatorName = initiator.Name;
            StartMs = TickMs = nowMs;
            RegisteredPlayers.Add(initiator.Id);
        }

        public long TimeInsideMs => TickMs - StartMs;
        public long RemainingTimeMs => TimeLimitMs > TimeInsideMs ? TimeLimitMs - TimeInsideMs : 0;
        public int PlayerCount => EnteredPlayers.Count;
        public bool IsOccupied => EnteredPlayers.Count > 0;

        public ulong GetLocalVar(string name) => LocalVars.GetValueOrDefault(name);

        public void SetRecord(string name, long timeMs, int partySize)
        {
            RecordName = !string.IsNullOrEmpty(name) ? name : InitiatorName;
            RecordTimeMs = timeMs;
            RecordPartySize = partySize;
        }

        public bool IsEntered(CharData c) => EnteredPlayers.Contains(c.Id);
        public bool IsRegistered(CharData c) => c != null && RegisteredPlayers.Contains(c.Id);

        // The Battlefield effect is the players clearance and names the battlefield it was granted for
        public bool HasClearance(CharData c) =>
            c.StatusEffects.GetStatusEffect(EffectBattlefield) is { } e && e.Power == Id && e.SubPower == Area;

        // CanCleanup: once flagged, or with nobody inside
        public bool CanCleanup(bool cleanup = false)
        {
            if (cleanup) CleanupFlag = true;
            return CleanupFlag || EnteredPlayers.Count == 0;
        }

        public IEnumerable<MobEntity> Enemies()
        {
            foreach (var m in RequiredEnemyList.ToArray()) yield return m.PMob;
            foreach (var m in AdditionalEnemyList.ToArray()) yield return m.PMob;
        }

        public const ushort EffectBattlefield = 254;

        // setPlayerEntered / hasPlayerEntered: the clearance effect's tier
        public static void SetPlayerEntered(CharData c, bool entered)
        {
            var e = c.StatusEffects.GetStatusEffect(EffectBattlefield);
            if (e != null) e.Tier = (ushort)(entered ? 1 : 0);
        }

        public static bool HasPlayerEntered(CharData c) => c.StatusEffects.GetStatusEffect(EffectBattlefield) is { Tier: 1 };
    }

    // CBattlefieldHandler: a zone's battlefields by area, how many it may hold, and the players to kick out
    public sealed class BattlefieldHandler
    {
        public readonly SortedDictionary<byte, Battlefield> Battlefields = new SortedDictionary<byte, Battlefield>();
        public readonly List<(uint CharId, long AtMs)> OrphanedPlayers = new List<(uint, long)>();
        public int MaxBattlefields = 3;
    }
}
