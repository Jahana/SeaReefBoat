// Heir to the Light (Qu'Bia Arena, San d'Oria Mission 9-2's fight), a line-for-line port of
// scripts/battlefields/QuBia_Arena/heir_to_the_light.lua (quest rework C3 N6). A BattlefieldMission of 30 minutes for
// six, trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, entered at BC_Entrance (index 4) and
// left at Burning_Circle. The mission is the San d'Oria log's The Heir to the Light, entered at San d'Oria status 3.
// Each area (stride 14 from WARLORD_ROJGNOJ) holds phase 1's eleven (+3 to +13), one superlinked group, and phase 2's
// three (+0 to +2), not spawned at the start, superlinked. Phase 1's allDeath, once per fight (the battlefield's local
// var arrivalCS), plays to each player 32005 (the area, 0, 4, 0, 15) with the char var Mission[0][23]Intro at 1 or the
// mission completed, else 32004 (the area, 0, 4). onEventFinishBattlefield (that cutscene's finish): unless one of
// phase 2's three is up already, the three are spawned; then Trion (targid 75) is put in the battlefield as an ally,
// the char moved to the area's spot and Trion spawned at his, level 75. Phase 2's allDeath wins the fight. The win
// sets battlefieldWin only while the mission is current; the mission's 32001 finish
// (Missions/Sandoria/TheHeirToTheLight.cs) reads it, and its 32004 finish sets Intro. Kept as the Lua has it: the hook
// reads no csid; its bail-out reads GetMobByID(id):isSpawned() with no nil check.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.QuBiaArena
{
    public sealed class HeirToTheLight : BattlefieldScript
    {
        public HeirToTheLight() : base("HEIR_TO_THE_LIGHT") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/QuBia_Arena/heir_to_the_light";

        const string Warlord = Mob.QubiaArena.WARLORD_ROJGNOJ;

        // the player's and Trion's spots, by area (playerCoords, trionCoords)
        static readonly (double X, double Y, double Z, int Rot)[] PlayerCoords =
        {
            (-398.196, -201.625, 416.836, 68),
            (1.877, -1.625, 16.935, 68),
            (401.832, 198.375, -382.841, 68),
        };

        static readonly (double X, double Y, double Z, int Rot)[] TrionCoords =
        {
            (-399.92, -201.62, 411.64, 63),
            (0.08, -1.62, 11.64, 63),
            (400.10, 198.37, -388.32, 63),
        };

        // qubiaID.mob.WARLORD_ROJGNOJ + first to + last
        static BattlefieldMobId[] W(int first, int last)
        {
            var ids = new BattlefieldMobId[last - first + 1];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = new BattlefieldMobId(Warlord, first + i);
            }

            return ids;
        }

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.QubiaArena,
            BattlefieldId = Battlefield.HeirToTheLight,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 4,
            EntryNpcs = new[] { Npc.QubiaArena.BCEntrance },
            ExitNpcs = new[] { Npc.QubiaArena.BurningCircle },
            MissionArea = XI.Quests.MissionLog.Sandoria,
            Mission = XI.QuestContent.Mission.Sandoria.TheHeirToTheLight,
            MissionStatusArea = XI.Quests.MissionLog.Sandoria,
            RequiredMissionStatus = 3,
            // TODO: On retail the loss event plays with params { [0] = 70, [2] = 4, [6] = zone } and mode
            // flags 4360 (captured), displaying "You were unable to protect Prince Trion. Now leaving the
            // battlefield." during the exit fade.  Sending the params works, but repeat viewers get a
            // client-side skip prompt, and declining it softlocks the client: the reply to the resulting
            // GP_CLI_COMMAND_EVENTENDXZY update sends position packets retail does not (unhandled updates
            // default to a position update in luautils::OnEventUpdate).  Add lossEventParams once that
            // reply is fixed.

            Groups =
            {
                // Phase 1
                new BattlefieldGroup
                {
                    AreaMobIds = new[] { W(3, 13), W(17, 27), W(31, 41) },
                    Superlink = true,
                    AllDeath = (q, mob) =>
                    {
                        var battlefield = q.Battlefield;

                        // Simultaneous kills invoke this once per death; play the cutscene only once.
                        if (battlefield.LocalVar("arrivalCS") == 1)
                        {
                            return;
                        }

                        battlefield.SetLocalVar("arrivalCS", 1);

                        int battlefieldArea = battlefield.Area;
                        var players = battlefield.Players();

                        foreach (var player in players)
                        {
                            if (player.CharVar("Mission[0][23]Intro") == 1 ||
                                player.HasCompletedMission(MissionLog.Sandoria,
                                    XI.QuestContent.Mission.Sandoria.TheHeirToTheLight))
                            {
                                player.StartEvent(32005, battlefieldArea, 0, 4, 0, 15);
                            }
                            else
                            {
                                player.StartEvent(32004, battlefieldArea, 0, 4);
                            }
                        }
                    },
                },

                new BattlefieldGroup
                {
                    AreaMobIds = new[] { W(0, 2), W(14, 16), W(28, 30) },
                    Spawned = false,
                    Superlink = true,
                    AllDeath = (q, mob) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                },
            },
        };

        public override void OnEventFinishBattlefield(IBattlefieldContext q, int csid, int option)
        {
            var battlefield = q.Player.CurrentBattlefield;
            int battlefieldArea = battlefield.Area;
            int phaseTwoOffset = q.Engine.ZoneMobId(Zone.QubiaArena, Warlord).Value + (battlefield.Area - 1) * 14;

            // Bail out if anyone else got here first
            for (int phaseTwoMobId = phaseTwoOffset; phaseTwoMobId <= phaseTwoOffset + 2; phaseTwoMobId++)
            {
                if (q.Engine.MobById(phaseTwoMobId).IsSpawned)
                {
                    return;
                }
            }

            // Set up the Arena for Phase 2
            for (int phaseTwoMobId = phaseTwoOffset; phaseTwoMobId <= phaseTwoOffset + 2; phaseTwoMobId++)
            {
                q.Engine.SpawnMob(phaseTwoMobId);
            }

            // Spawn Trion (Ally)
            var trion = q.Player.CurrentBattlefield.InsertEntity(75, true, true);
            var p = PlayerCoords[battlefieldArea - 1];
            q.Player.SetPos(p.X, p.Y, p.Z, p.Rot);
            var t = TrionCoords[battlefieldArea - 1];
            trion.SetSpawn(t.X, t.Y, t.Z, t.Rot);
            trion.Spawn();
            trion.SetMobLevel(75);
        }
    }
}
