// Rank 5 Mission (Qu'Bia Arena, the nations' 5-1 Archlich Taber'quoan fight), a line-for-line port of
// scripts/battlefields/QuBia_Arena/rank_5_mission.lua (quest rework C3 N4). A BattlefieldMission of 15 minutes for six,
// trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, entered at BC_Entrance (index 0) and left
// at Burning_Circle, granting the Archmage Assassin title. The mission is the nation's ARCHLICH (14) of the char's own
// log, entered at status 11. entryRequirement: the nation's mission 14 completed or the New Fei'Yin Seal held (kept as
// the Lua has it: hasCompletedMission(getNation(), 14) passes a nation as a log id, which works because the nation ids
// 0/1/2 equal the log ids). Each area (stride 7) holds Archlich Taber'quoan, two Sorcerers (+1, +2) and four Warriors
// (+3 to +6, not spawned at the start), one superlinked group. The Archlich's death (allDeath) despawns every add still
// up, after telling each player's client to drop it (sendEntityUpdateToPlayer ENTITY_DESPAWN, UPDATE_NONE), and wins.
// onBattlefieldTick (after the base tick), while LOCKED, once GetSystemTime reaches the local var nextWarriorRespawn
// and the Archlich has a target: nextWarriorRespawn is set ten seconds on, and for each spawn point pair
// ({ +3, +5 }, { +4, +6 }) the first Warrior not spawned spawns, gets one enmity on the Archlich's target and engages
// it. The win sets battlefieldWin only while the mission is current; the missions' 32001 finishes
// (Missions/*/DarknessRising.cs, TheRuinsOfFeiYin.cs, TheFinalSeal.cs) read it.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.QuBiaArena
{
    public sealed class Rank5Mission : BattlefieldScript
    {
        public Rank5Mission() : base("RANK_5_MISSION") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/QuBia_Arena/rank_5_mission";

        const string Archlich = Mob.QubiaArena.ARCHLICH_TABERQUOAN;

        // xi.entityUpdate.ENTITY_DESPAWN, xi.updateType.UPDATE_NONE
        const int EntityDespawn = 4;
        const int UpdateNone = 0x00;

        // Each pair shares a home point and produces at most one Warrior per pulse.
        static readonly int[][] WarriorOffsetsBySpawnPoint = { new[] { 3, 5 }, new[] { 4, 6 } };

        static BattlefieldMobId A(int offset) => new BattlefieldMobId(Archlich, offset);

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.QubiaArena,
            BattlefieldId = Battlefield.Rank5Mission,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(15)
            TimeLimit = 15 * 60,
            Index = 0,
            EntryNpcs = new[] { Npc.QubiaArena.BCEntrance },
            ExitNpcs = new[] { Npc.QubiaArena.BurningCircle },
            // xi.mission.id.nation.ARCHLICH, of the char's nation's log (missionArea nil)
            Mission = 14,
            RequiredMissionStatus = 11,
            Title = Title.ArchmageAssassin,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { A(0) },
                        new[] { A(7) },
                        new[] { A(14) },
                    },
                    AllDeath = (q, archlich) =>
                    {
                        for (int offset = 1; offset <= 6; offset++)
                        {
                            var add = q.Engine.MobById(archlich.Id + offset);

                            if (add != null && add.IsSpawned)
                            {
                                // Retail removes remaining add models and nameplates in the Archlich death frame.
                                foreach (var player in q.Battlefield.Players())
                                {
                                    player.SendEntityUpdateToPlayer(add, EntityDespawn, UpdateNone);
                                }

                                q.Engine.DespawnMob(add.Id);
                            }
                        }

                        q.Battlefield.SetStatus(BattlefieldConst.StatusWon);
                    },
                    SuperlinkGroup = 1,
                },

                // Sorcerers
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { A(1), A(2) },
                        new[] { A(8), A(9) },
                        new[] { A(15), A(16) },
                    },
                    SuperlinkGroup = 1,
                },

                // Warriors
                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { A(3), A(4), A(5), A(6) },
                        new[] { A(10), A(11), A(12), A(13) },
                        new[] { A(17), A(18), A(19), A(20) },
                    },
                    Spawned = false,
                    SuperlinkGroup = 1,
                },
            },
        };

        public override bool EntryRequirement(IBattlefieldContext q, bool isRegistrant)
        {
            var player = q.Player;
            return player.HasCompletedMission(player.Nation, 14) ||
                   player.HasKeyItem(KeyItem.NewFeiyinSeal);
        }

        public override void OnBattlefieldTick(IBattlefieldContext q, int tick)
        {
            base.OnBattlefieldTick(q, tick);

            var battlefield = q.Battlefield;
            if (battlefield.Status != BattlefieldConst.StatusLocked)
            {
                return;
            }

            uint currentTime = q.Engine.GetSystemTime();
            if (currentTime < battlefield.LocalVar("nextWarriorRespawn"))
            {
                return;
            }

            int mobBaseId = q.Engine.ZoneMobId(Zone.QubiaArena, Archlich).Value + (battlefield.Area - 1) * 7;
            var archlich = q.Engine.MobById(mobBaseId);
            if (archlich == null)
            {
                return;
            }

            var archlichTarget = archlich.GetTarget();
            if (archlichTarget == null)
            {
                return;
            }

            battlefield.SetLocalVar("nextWarriorRespawn", currentTime + 10);

            foreach (var spawnPointOffsets in WarriorOffsetsBySpawnPoint)
            {
                foreach (var offset in spawnPointOffsets)
                {
                    var warrior = q.Engine.MobById(mobBaseId + offset);

                    if (warrior != null && !warrior.IsSpawned)
                    {
                        warrior.Spawn();
                        // Seed threat before directly entering the attack state.
                        warrior.AddEnmity(archlichTarget, 1, 0);
                        warrior.Engage(archlichTarget.TargId);
                        break;
                    }
                }
            }
        }
    }
}
