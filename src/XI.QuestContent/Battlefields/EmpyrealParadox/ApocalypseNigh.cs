// Apocalypse Nigh (Empyreal Paradox BCNM), a line-for-line port of
// scripts/battlefields/Empyreal_Paradox/apocalypse_nigh.lua (quest rework C3, Q3a). A BattlefieldQuest of the Jeuno
// quest Apocalypse Nigh: 30 minutes for six, trusts allowed, capped at xi.settings.main.MAX_LEVEL, index 1, entered at
// TR_Entrance when Quest[3][89]Prog is 3 and left at Transcendental_Radiance (a kept data quirk: the zone data's three
// Transcendental_Radiance are cutscene_only, so the exit NPC can't be triggered and onExitTrigger never runs). Its one
// group is Kamlanaut and Ealdnarche (two ids to an area), superlinked; their death wins the fight. onEventFinishWin
// sets the char down at 540 0 -514 (rotation 63) in the zone.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.EmpyrealParadox
{
    public sealed class ApocalypseNigh : BattlefieldScript
    {
        public ApocalypseNigh() : base("APOCALYPSE_NIGH") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Empyreal_Paradox/apocalypse_nigh";

        static BattlefieldMobId[] Ids(int first) => new[]
        {
            new BattlefieldMobId("KAMLANAUT", first),
            new BattlefieldMobId("KAMLANAUT", first + 1),
        };

        public override BattlefieldData Data { get; } = new BattlefieldQuestData
        {
            ZoneId = Zone.EmpyrealParadox,
            BattlefieldId = Battlefield.ApocalypseNigh,
            AllowTrusts = true,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 1,
            EntryNpcs = new[] { Npc.EmpyrealParadox.TREntrance },
            ExitNpcs = new[] { Npc.EmpyrealParadox.TranscendentalRadiance },

            QuestArea = XI.Quests.QuestLog.Jeuno,
            Quest = "APOCALYPSE_NIGH",
            RequiredVar = "Quest[3][89]Prog",
            RequiredValue = 3,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[] { Ids(0), Ids(2), Ids(4) },
                    Superlink = true,

                    AllDeath = (q, mob) =>
                    {
                        q.Battlefield.SetStatus(BattlefieldConst.StatusWon);
                    },
                },
            },
        };

        public override void OnEventFinishWin(IBattlefieldContext q, int csid, int option) =>
            q.Player.SetPos(540, 0, -514, 63, Zone.EmpyrealParadox);
    }
}
