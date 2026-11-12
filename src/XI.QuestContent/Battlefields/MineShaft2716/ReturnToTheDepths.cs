// Return to the Depths (Mine Shaft #2716 BCNM), a line-for-line port of
// scripts/battlefields/Mine_Shaft_2716/return_to_the_depths.lua (quest rework C3, Q3a). A BattlefieldQuest of the
// Bastok quest Return to the Depths: 30 minutes for six, capped at xi.settings.main.MAX_LEVEL, index 1, entered at _0d0
// when Quest[1][78]Prog is 9 and left at _0d1 to _0d3. Its one group is the Twilotak, seven to an area, all spawned,
// superlinked; the flows kill Twilotak, whose onMobDeath (Twilotak.lua) wins; the group's allDeath sets WON when all
// seven of an area are dead (#adds). Every method is BattlefieldQuest's or Battlefield's: the file overrides none.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.MineShaft2716
{
    public sealed class ReturnToTheDepths : BattlefieldScript
    {
        public ReturnToTheDepths() : base("RETURN_TO_THE_DEPTHS") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Mine_Shaft_2716/return_to_the_depths";

        static BattlefieldMobId[] Ids(int first)
        {
            var ids = new BattlefieldMobId[7];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = new BattlefieldMobId("TWILOTAK", first + i);
            }

            return ids;
        }

        public override BattlefieldData Data { get; } = new BattlefieldQuestData
        {
            ZoneId = Zone.MineShaft2716,
            BattlefieldId = Battlefield.ReturnToTheDepths,
            MaxPlayers = 6,
            LevelCapSetting = "MAX_LEVEL",
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 1,
            EntryNpcs = new[] { Npc.MineShaft2716._0d0 },
            ExitNpcs = new[] { Npc.MineShaft2716._0d1, Npc.MineShaft2716._0d2, Npc.MineShaft2716._0d3 },

            QuestArea = XI.Quests.QuestLog.Bastok,
            Quest = "RETURN_TO_THE_DEPTHS",
            RequiredVar = "Quest[1][78]Prog",
            RequiredValue = 9,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[] { Ids(0), Ids(7), Ids(14) },
                    Superlink = true,

                    AllDeath = (q, mob) =>
                    {
                        q.Battlefield.SetStatus(BattlefieldConst.StatusWon);
                    },
                },
            },
        };
    }
}
