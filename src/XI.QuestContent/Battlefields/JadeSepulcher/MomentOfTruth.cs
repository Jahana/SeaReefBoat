// Moment of Truth (Jade Sepulcher BCNM), a line-for-line port of
// scripts/battlefields/Jade_Sepulcher/moment_of_truth.lua (quest rework C3, Q3b). A BattlefieldQuest of the Aht
// Urhgan quest The Beast Within - Limit Break (BLU): 30 minutes for six, index 3, entered at _1v0 when
// Quest[6][30]Prog is 4 and left at _1v1 to _1v3. One group, six Shadowhand Kajeel-Ja to an area; their death wins
// the fight. onBattlefieldWin replaces the framework's: battlefieldWin is set for an accepted quest only, the skip
// flag is 1 for a completed quest only, and the event 32001 gets a ninth argument, 2.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.JadeSepulcher
{
    public sealed class MomentOfTruth : BattlefieldScript
    {
        public MomentOfTruth() : base("MOMENT_OF_TRUTH") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Jade_Sepulcher/moment_of_truth";

        static BattlefieldMobId[] Ids(int first) => new[]
        {
            new BattlefieldMobId("SHADOWHAND_KAJEEL_JA", first),
            new BattlefieldMobId("SHADOWHAND_KAJEEL_JA", first + 1),
            new BattlefieldMobId("SHADOWHAND_KAJEEL_JA", first + 2),
            new BattlefieldMobId("SHADOWHAND_KAJEEL_JA", first + 3),
            new BattlefieldMobId("SHADOWHAND_KAJEEL_JA", first + 4),
            new BattlefieldMobId("SHADOWHAND_KAJEEL_JA", first + 5),
        };

        public override BattlefieldData Data { get; } = new BattlefieldQuestData
        {
            ZoneId = Zone.JadeSepulcher,
            BattlefieldId = Battlefield.MomentOfTruth,
            MaxPlayers = 6,
            // utils.minutes(30)
            TimeLimit = 30 * 60,
            Index = 3,
            EntryNpcs = new[] { Npc.JadeSepulcher._1v0 },
            ExitNpcs = new[] { Npc.JadeSepulcher._1v1, Npc.JadeSepulcher._1v2, Npc.JadeSepulcher._1v3 },

            QuestArea = XI.Quests.QuestLog.AhtUrghan,
            Quest = "MOMENT_OF_TRUTH",
            RequiredVar = "Quest[6][30]Prog",
            RequiredValue = 4,

            Groups =
            {
                new BattlefieldGroup
                {
                    AreaMobIds = new[] { Ids(0), Ids(6), Ids(12) },

                    AllDeath = (q, mob) =>
                    {
                        q.Battlefield.SetStatus(BattlefieldConst.StatusWon);
                    },
                },
            },
        };

        public override void OnBattlefieldWin(IBattlefieldContext q)
        {
            var player = q.Player;
            var qd = (BattlefieldQuestData)Data;
            var status = player.QuestStatus(qd.QuestArea, qd.Quest);

            if (status == XI.Quests.QuestStatus.Accepted)
            {
                player.SetLocalVar("battlefieldWin", q.Battlefield.Id);
            }

            var record = q.Battlefield.Record();
            int canSkipCS = status == XI.Quests.QuestStatus.Completed ? 1 : 0;

            player.StartEvent(32001, q.Battlefield.Area, record.ClearTime, record.PartySize,
                q.Battlefield.TimeInside, player.ZoneId, Data.Index, canSkipCS, 2);
        }
    }
}
