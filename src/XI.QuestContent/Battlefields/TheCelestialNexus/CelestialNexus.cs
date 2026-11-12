// The Celestial Nexus (Zilart 16's fight), a line-for-line port of
// scripts/battlefields/The_Celestial_Nexus/celestial_nexus.lua (quest rework C3, C6). A BattlefieldMission of 30
// minutes for six, trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, entered at _513 (index 0)
// and left at _514 or _515. The mission is the Zilart log's The Celestial Nexus, entered at Zilart status 0.
// Its groups: phase 1's Eald'narche (EALDNARCHE + 0, 5, 10 by area), whose death despawns his two Orbitals (his id + 3
// and + 4) and plays 32004 (the area) to each player of the battlefield; the Exoplates (+ 1, 6, 11); the Orbitals (+ 3
// and 4, 8 and 9, 13 and 14), not spawned at start; phase 2's Eald'narche (+ 2, 7, 12), not spawned at start, whose
// death wins the fight. A 32004 finish (onEventFinishBattlefield) reads phase 2's id of the char's area from the
// groups; when that mob is up already it does nothing, else it despawns phase 1's Eald'narche, spawns phase 2, keeps
// the char's targid in phase 2's targetId local var and arms a 30000 ms timer on it whose callback (engage) makes
// phase 2 engage that targid. The win sets battlefieldWin only while the mission is current; the mission's 32001
// finish (Missions/Zilart/TheCelestialNexus.cs) reads it. Kept as the Lua has it: the zone's IDs.lua has no
// TIME_IN_THE_BATTLEFIELD_IS_UP, so the framework's time-up raises on every tick and the fight never ends by time.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.TheCelestialNexus
{
    public sealed class CelestialNexus : BattlefieldScript
    {
        public CelestialNexus() : base("CELESTIAL_NEXUS")
        {
            Data = new BattlefieldMissionData
            {
                ZoneId = Zone.TheCelestialNexus,
                BattlefieldId = Battlefield.CelestialNexus,
                CanLoseExp = false,
                IsMission = true,
                AllowTrusts = true,
                MaxPlayers = 6,
                LevelCapSetting = "MAX_LEVEL",
                // utils.minutes(30)
                TimeLimit = 30 * 60,
                Index = 0,
                EntryNpcs = new[] { Npc.TheCelestialNexus._513 },
                ExitNpcs = new[] { Npc.TheCelestialNexus._514, Npc.TheCelestialNexus._515 },

                MissionArea = XI.Quests.MissionLog.Zilart,
                Mission = XI.QuestContent.Mission.Zilart.TheCelestialNexus,
                MissionStatusArea = XI.Quests.MissionLog.Zilart,
                RequiredMissionStatus = 0,

                Groups =
                {
                    // Phase 1 - Eald'narche
                    new BattlefieldGroup
                    {
                        AreaMobIds = new[]
                        {
                            new[] { new BattlefieldMobId(Ealdnarche) },
                            new[] { new BattlefieldMobId(Ealdnarche, 5) },
                            new[] { new BattlefieldMobId(Ealdnarche, 10) },
                        },

                        Death = (q, mob, _) =>
                        {
                            int mobId = mob.Id;
                            for (int orbitalId = mobId + 3; orbitalId <= mobId + 4; orbitalId++)
                            {
                                q.Engine.DespawnMob(orbitalId);
                            }

                            var players = q.Battlefield.Players();
                            foreach (var player in players)
                            {
                                player.StartEvent(32004, q.Battlefield.Area);
                            }
                        },
                    },

                    // Phase 1 - Exoplates
                    new BattlefieldGroup
                    {
                        AreaMobIds = new[]
                        {
                            new[] { new BattlefieldMobId(Ealdnarche, 1) },
                            new[] { new BattlefieldMobId(Ealdnarche, 6) },
                            new[] { new BattlefieldMobId(Ealdnarche, 11) },
                        },
                    },

                    // Phase 1 - Orbitals
                    new BattlefieldGroup
                    {
                        AreaMobIds = new[]
                        {
                            new[] { new BattlefieldMobId(Ealdnarche, 3), new BattlefieldMobId(Ealdnarche, 4) },
                            new[] { new BattlefieldMobId(Ealdnarche, 8), new BattlefieldMobId(Ealdnarche, 9) },
                            new[] { new BattlefieldMobId(Ealdnarche, 13), new BattlefieldMobId(Ealdnarche, 14) },
                        },

                        Spawned = false,
                    },

                    // Phase 2 - Eald'narche
                    new BattlefieldGroup
                    {
                        AreaMobIds = new[]
                        {
                            new[] { new BattlefieldMobId(Ealdnarche, 2) },
                            new[] { new BattlefieldMobId(Ealdnarche, 7) },
                            new[] { new BattlefieldMobId(Ealdnarche, 12) },
                        },

                        Spawned = false,
                        Death = (q, mob, _) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                    },
                },
            };

            // phase 2's 30 s timer: phaseTwo:engage(mobArg:getLocalVar('targetId')), phaseTwo the closure's upvalue
            engage_ = Data.Callback("engage", (q, a) =>
            {
                var phaseTwo = a.CapturedEntity(0);
                phaseTwo.Engage((int)a.Entity.LocalVar("targetId"));
            });
        }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/The_Celestial_Nexus/celestial_nexus";

        // celestialNexusID.mob.EALDNARCHE
        const string Ealdnarche = "EALDNARCHE";

        readonly BattlefieldCallback engage_;

        public override BattlefieldData Data { get; }

        // content.groups[group]['mobIds'][area][1]: the group's first mob id in that area (group 1-based, as the Lua)
        int GroupMobId(IBattlefieldContext q, int group, int area)
        {
            var id = Data.Groups[group - 1].AreaMobIds[area - 1][0];
            return q.Engine.ZoneMobId(Data.ZoneId, id.Key).Value + id.Offset;
        }

        public override void OnEventFinishBattlefield(IBattlefieldContext q, int csid, int option)
        {
            if (csid == 32004)
            {
                var battlefield = q.Player.CurrentBattlefield;
                int phaseTwoId = GroupMobId(q, 4, battlefield.Area);
                var phaseTwo = q.Engine.MobById(phaseTwoId);

                if (phaseTwo != null)
                {
                    if (phaseTwo.IsSpawned)
                    {
                        return;
                    }

                    q.Engine.DespawnMob(GroupMobId(q, 1, battlefield.Area));
                    q.Engine.SpawnMob(phaseTwoId);

                    phaseTwo.SetLocalVar("targetId", (uint)q.Player.TargId);
                    phaseTwo.Timer(30000, engage_, phaseTwo);
                }
            }
        }
    }
}
