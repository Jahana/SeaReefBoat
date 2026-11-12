// The Secret Weapon (Horlais Peak, San d'Oria 7-2's fight), a line-for-line port of
// scripts/battlefields/Horlais_Peak/the_secret_weapon.lua (quest rework C3 N5). A BattlefieldMission of 30 minutes for
// six, trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, entered at BC_Entrance (index 3) and
// left at Burning_Circle; no title, no experience. The mission is the San d'Oria log's The Secret Weapon, entered at
// San d'Oria status 2. Its first group, by id from Darokbok of Clan Reaper five to an area (+0 to +4, +6 to +10, +12 to
// +16), is superlinked and wins the fight when they all die; its second, one to an area (+5, +11, +17), is not spawned
// at the start. The win sets battlefieldWin only while the mission is current; the mission's 32001 finish
// (Missions/Sandoria/TheSecretWeapon.cs, scripts/missions/sandoria/7_2_The_Secret_Weapon.lua) reads it.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.HorlaisPeak
{
    public sealed class TheSecretWeapon : BattlefieldScript
    {
        public TheSecretWeapon() : base("THE_SECRET_WEAPON") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Horlais_Peak/the_secret_weapon";

        static BattlefieldMobId[] Ids(int first) => new[]
        {
            new BattlefieldMobId("DAROKBOK_OF_CLAN_REAPER", first),
            new BattlefieldMobId("DAROKBOK_OF_CLAN_REAPER", first + 1),
            new BattlefieldMobId("DAROKBOK_OF_CLAN_REAPER", first + 2),
            new BattlefieldMobId("DAROKBOK_OF_CLAN_REAPER", first + 3),
            new BattlefieldMobId("DAROKBOK_OF_CLAN_REAPER", first + 4),
        };

        public override BattlefieldData Data { get; } = new BattlefieldMissionData
        {
            ZoneId = Zone.HorlaisPeak,
            BattlefieldId = Battlefield.TheSecretWeapon,
            CanLoseExp = false,
            IsMission = true,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 3,
            EntryNpcs = new[] { Npc.HorlaisPeak.BCEntrance },
            ExitNpcs = new[] { Npc.HorlaisPeak.BurningCircle },
            MissionArea = XI.Quests.MissionLog.Sandoria,
            Mission = XI.QuestContent.Mission.Sandoria.TheSecretWeapon,
            MissionStatusArea = XI.Quests.MissionLog.Sandoria,
            RequiredMissionStatus = 2,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[] { Ids(0), Ids(6), Ids(12) },
                    Superlink = true,
                    AllDeath = (q, mob) => q.Battlefield.SetStatus(BattlefieldConst.StatusWon),
                },

                new BattlefieldGroup
                {
                    AreaMobIds = new[]
                    {
                        new[] { new BattlefieldMobId("DAROKBOK_OF_CLAN_REAPER", 5) },
                        new[] { new BattlefieldMobId("DAROKBOK_OF_CLAN_REAPER", 11) },
                        new[] { new BattlefieldMobId("DAROKBOK_OF_CLAN_REAPER", 17) },
                    },
                    Spawned = false,
                },
            },
        };
    }
}
