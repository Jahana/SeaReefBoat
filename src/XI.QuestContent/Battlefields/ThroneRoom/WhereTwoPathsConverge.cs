// Where Two Paths Converge (Throne Room, Bastok Mission 9-2's Zeid fight), a line-for-line port of
// scripts/battlefields/Throne_Room/where_two_paths_converge.lua (quest rework C3 N6). !pos -111 -6 0 165. A
// BattlefieldMission of 30 minutes for six, no experience lost, capped at xi.settings.main.MAX_LEVEL, entered at _4l1
// (index 1) and left at _4l2, _4l3 or _4l4. The mission is the Bastok log's Where Two Paths Converge, entered at status
// 1 (skippable at 4). Its groups: phase 1's Zeid, whose setup makes him targetable and unkillable and arms the
// ZEID_COMBAT_TICK listener (COMBAT_TICK, the callback zeidCombatTick): below 70% HP, once (ChangingPhase), for each
// player of the battlefield he is made untargetable, the player gets 32004 (3, 3, 1, 3, 3, 3, 3, 3) and he is
// despawned; phase 2's Zeid_2, not spawned at the start, whose death wins the fight; Shadow_of_Rage, not spawned at
// the start, with no handler. onEventFinishBattlefield (32004's finish): unless phase 2 (ZEID_BCNM_OFFSET + (area - 1)
// * 4 + 1) is up already, it is spawned and given phase 1's HP percentage of its max HP; then Volker (targid 28) is put
// in the battlefield as an ally, the char moved to the area's spot and Volker spawned at his, level 75. The win sets
// battlefieldWin only while the mission is current; the mission's 32001 finish
// (Missions/Bastok/WhereTwoPathsConverge.cs) reads it. Kept as the Lua has it: the listener's loop makes Zeid
// untargetable and despawns him once per player (the later calls act on a despawned mob); the hook reads no csid.
using System;
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.ThroneRoom
{
    public sealed class WhereTwoPathsConverge : BattlefieldScript
    {
        // the player's and Volker's spots, by area (playerCoords, volkerCoords)
        static readonly (double X, double Y, double Z, int Rot)[] PlayerCoords =
        {
            (-443, -167, -239, 127),
            (-762.949, -407, -478.991, 127),
            (-1082.787, -647, -718.976, 127),
        };

        static readonly (double X, double Y, double Z, int Rot)[] VolkerCoords =
        {
            (-451.155, -167, -240.004, 125),
            (-770.805, -407, -480.077, 125),
            (-1090.97, -647, -720.077, 125),
        };

        private readonly BattlefieldCallback zeidCombatTick_;

        public WhereTwoPathsConverge() : base("WHERE_TWO_PATHS_CONVERGE")
        {
            Data = new BattlefieldMissionData
            {
                ZoneId = Zone.ThroneRoom,
                BattlefieldId = Battlefield.WhereTwoPathsConverge,
                CanLoseExp = false,
                MaxPlayers = 6,
                LevelCapSetting = "MAX_LEVEL",
                // utils.minutes(30)
                TimeLimit = 30 * 60,
                Index = 1,
                EntryNpcs = new[] { Npc.ThroneRoom._4l1 },
                ExitNpcs = new[] { Npc.ThroneRoom._4l2, Npc.ThroneRoom._4l3, Npc.ThroneRoom._4l4 },

                MissionArea = XI.Quests.MissionLog.Bastok,
                Mission = XI.QuestContent.Mission.Bastok.WhereTwoPathsConverge,
                RequiredMissionStatus = 1,
                SkipMissionStatus = 4,

                Groups =
                {
                    // Phase 1
                    new BattlefieldGroup
                    {
                        Mobs = new[] { Npc.ThroneRoom.Zeid },
                        Setup = (q, mobs) =>
                        {
                            foreach (var mob in mobs)
                            {
                                mob.SetUntargetable(false);
                                mob.SetUnkillable(true);
                                mob.RemoveListener("ZEID_COMBAT_TICK");

                                mob.AddListener("COMBAT_TICK", "ZEID_COMBAT_TICK", zeidCombatTick_);
                            }
                        },
                    },

                    // Phase 2
                    new BattlefieldGroup
                    {
                        Mobs = new[] { Npc.ThroneRoom.Zeid2 },
                        Spawned = false,
                        Death = (q, mob, deathCount) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                    },

                    new BattlefieldGroup
                    {
                        Mobs = new[] { Npc.ThroneRoom.ShadowofRage },
                        Spawned = false,
                    },
                },
            };

            // phase 1's COMBAT_TICK listener: below 70% HP, once, every player gets the phase change cutscene
            zeidCombatTick_ = Data.Callback("zeidCombatTick", (q, a) =>
            {
                var zeid = a.Entity;
                if (zeid.HPP < 70 && zeid.LocalVar("ChangingPhase") == 0)
                {
                    zeid.SetLocalVar("ChangingPhase", 1);

                    var players = zeid.GetBattlefield().Players();

                    foreach (var player in players)
                    {
                        zeid.SetUntargetable(true);
                        player.StartEvent(32004, 3, 3, 1, 3, 3, 3, 3, 3);
                        q.Engine.DespawnMob(zeid.Id);
                    }
                }
            });
        }

        public override BattlefieldData Data { get; }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Throne_Room/where_two_paths_converge";

        public override void OnEventFinishBattlefield(IBattlefieldContext q, int csid, int option)
        {
            int area = q.Player.CurrentBattlefield.Area;
            int zeidId = q.Engine.ZoneMobId(Zone.ThroneRoom, Mob.ThroneRoom.ZEID_BCNM_OFFSET).Value + (area - 1) * 4;

            // Bail out if anyone else got here first
            int zeid2Id = zeidId + 1;
            var zeid2Mob = q.Engine.MobById(zeid2Id);
            if (zeid2Mob != null && zeid2Mob.IsSpawned)
            {
                return;
            }

            var zeid2 = q.Engine.SpawnMob(zeid2Id);

            // Set Phase 2 Zeid's HP to be same HPP as previous phase.
            var zeid1 = q.Engine.MobById(zeidId);

            if (zeid1 != null && zeid2 != null)
            {
                double hpp = zeid1.HPP / 100.0;
                zeid2.SetHP((int)Math.Floor(zeid2.MaxHP * hpp));
            }

            // Spawn Volker helper NPC.
            var volker = q.Player.CurrentBattlefield.InsertEntity(28, true, true);
            var p = PlayerCoords[area - 1];
            q.Player.SetPos(p.X, p.Y, p.Z, p.Rot);
            var v = VolkerCoords[area - 1];
            volker.SetSpawn(v.X, v.Y, v.Z, v.Rot);
            volker.Spawn();
            volker.SetMobLevel(75);
        }
    }
}
