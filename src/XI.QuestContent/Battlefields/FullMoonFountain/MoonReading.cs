// Moon Reading (Full Moon Fountain, Windurst Mission 9-2's fight), a line-for-line port of
// scripts/battlefields/Full_Moon_Fountain/moon_reading.lua (quest rework C3 N6). A BattlefieldMission of 30 minutes for
// six, trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, entered at MS_Entrance (index 1) and
// left at Moon_Spiral. The mission is the Windurst log's Moon Reading, entered at Windurst status 2. Each area (stride
// 6 from ACE_OF_CUPS) holds phase 1's four (+0 to +3), one superlinked group, and phase 2's two (+4, +5), not spawned
// at the start, superlinked. Phase 1's allDeath plays 32004 (1, 0, 1, 0, 1) to each player of the battlefield.
// onEventFinishBattlefield (that cutscene's finish): unless one of phase 2's two is up already, the two are spawned;
// then Ajido-Marujido (targid 33) is put in the battlefield as an ally, the char moved to the area's spot and Ajido
// spawned at his, level 75. Phase 2's allDeath wins the fight. The win sets battlefieldWin only while the mission is
// current; the mission's 32001 finish (Missions/Windurst/MoonReading.cs) reads it. Kept as the Lua has it: the hook
// reads no csid; its bail-out reads GetMobByID(id):isSpawned() with no nil check; phase 1's allDeath plays 32004 again
// on each call (no once guard, unlike Heir to the Light's); area 3's Ajido spot (-379.826, -51.248, 376.227) is 80
// yalms east of the char's (-459.974, -51.423, 373.86), where areas 1 and 2 put him beside the char.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.FullMoonFountain
{
    public sealed class MoonReading : BattlefieldScript
    {
        public MoonReading() : base("MOON_READING") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Full_Moon_Fountain/moon_reading";

        const string AceOfCups = Mob.FullMoonFountain.ACE_OF_CUPS;

        // the player's and Ajido's spots, by area (playerCoords, ajidoCoords)
        static readonly (double X, double Y, double Z, int Rot)[] PlayerCoords =
        {
            (340.220, 48.557, -386.114, 190),
            (-59.877, 10.577, 13.853, 190),
            (-459.974, -51.423, 373.86, 190),
        };

        static readonly (double X, double Y, double Z, int Rot)[] AjidoCoords =
        {
            (340.117, 48.752, -383.747, 64),
            (-59.98, 10.752, 16.22, 64),
            (-379.826, -51.248, 376.227, 64),
        };

        // fullMoonFountainID.mob.ACE_OF_CUPS + first to + last
        static BattlefieldMobId[] A(int first, int last)
        {
            var ids = new BattlefieldMobId[last - first + 1];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = new BattlefieldMobId(AceOfCups, first + i);
            }

            return ids;
        }

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.FullMoonFountain,
            BattlefieldId = Battlefield.MoonReading,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 1,
            EntryNpcs = new[] { Npc.FullMoonFountain.MSEntrance },
            ExitNpcs = new[] { Npc.FullMoonFountain.MoonSpiral },
            MissionArea = XI.Quests.MissionLog.Windurst,
            Mission = XI.QuestContent.Mission.Windurst.MoonReading,
            MissionStatusArea = XI.Quests.MissionLog.Windurst,
            RequiredMissionStatus = 2,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[] { A(0, 3), A(6, 9), A(12, 15) },
                    Superlink = true,
                    AllDeath = (q, mob) =>
                    {
                        var players = q.Battlefield.Players();

                        foreach (var player in players)
                        {
                            player.StartEvent(32004, 1, 0, 1, 0, 1);
                        }
                    },
                },

                new BattlefieldGroup
                {
                    AreaMobIds = new[] { A(4, 5), A(10, 11), A(16, 17) },
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
            int phaseTwoOffset =
                q.Engine.ZoneMobId(Zone.FullMoonFountain, AceOfCups).Value + (battlefield.Area - 1) * 6 + 4;

            // Bail out if anyone else got here first
            for (int phaseTwoMobId = phaseTwoOffset; phaseTwoMobId <= phaseTwoOffset + 1; phaseTwoMobId++)
            {
                if (q.Engine.MobById(phaseTwoMobId).IsSpawned)
                {
                    return;
                }
            }

            // Set up the Arena for Phase 2
            for (int phaseTwoMobId = phaseTwoOffset; phaseTwoMobId <= phaseTwoOffset + 1; phaseTwoMobId++)
            {
                q.Engine.SpawnMob(phaseTwoMobId);
            }

            // Spawn Ajido (Ally)
            var ajido = q.Player.CurrentBattlefield.InsertEntity(33, true, true);
            var p = PlayerCoords[battlefieldArea - 1];
            q.Player.SetPos(p.X, p.Y, p.Z, p.Rot);
            var a = AjidoCoords[battlefieldArea - 1];
            ajido.SetSpawn(a.X, a.Y, a.Z, a.Rot);
            ajido.Spawn();
            ajido.SetMobLevel(75);
        }
    }
}
