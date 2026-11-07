// A Test of True Love (Log ID 1, Quest ID 62), a line-for-line port of scripts/quests/bastok/A_Test_of_True_Love.lua
// (quest rework B3b). Carmelo : !pos -146.476 -7.48 -10.889 236. The three pages come from Treasure Chests: Un Moment
// (Castle Zvahl Baileys or Keep), L'Ancienne (Labyrinth of Onzozo) and L'Ephemere (Sea Serpent Grotto). Without its page
// a chest's trade is xi.treasure.onTrade with the page as the reward (bypass type 2), and the quest takes the trade;
// with the page the chest's own script has it. The completion sets Lovers in the Dusk's mustZone and takes the pages.
// The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class ATestOfTrueLove : QuestScript
    {
        public ATestOfTrueLove() : base(QuestLog.Bastok, "A_TEST_OF_TRUE_LOVE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/A_Test_of_True_Love";

        private static readonly int[] PageKeyItems =
        {
            KeyItem.UnMoment,
            KeyItem.Lephemere,
            KeyItem.Lancienne,
        };

        private static int GetNumPages(IQuestPlayerView player)
        {
            int numPages = 0;

            foreach (int keyItemId in PageKeyItems)
            {
                if (player.HasKeyItem(keyItemId))
                {
                    numPages = numPages + 1;
                }
            }

            return numPages;
        }

        // a chest's trade without the page: the treasure trade with the page as its reward, taken by the quest
        private static QuestReply ChestTrade(IQuestContext q, int page)
        {
            if (!q.Player.HasKeyItem(page))
            {
                q.Trade.TreasureTrade(2, page);

                return QuestReply.NoAction();
            }
            return null;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 70, FameArea = FameArea.Bastok, Gil = 10000 });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Bastok, "LOVE_AND_ICE") &&
                    q.Player.FameLevel(FameArea.Bastok) >= 6 &&
                    !q.Quest.MustZone)
                .Npc(Zone.PortBastok, Npc.PortBastok.Carmelo).OnTrigger(QuestReply.ProgressEvent(270))
                .OnEventFinish(Zone.PortBastok, 270, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.CastleZvahlBaileys, Npc.CastleZvahlBaileys.TreasureChest).OnTrade(q => ChestTrade(q, KeyItem.UnMoment))
                .Npc(Zone.CastleZvahlKeep, Npc.CastleZvahlKeep.TreasureChest).OnTrade(q => ChestTrade(q, KeyItem.UnMoment))
                .Npc(Zone.LabyrinthOfOnzozo, Npc.LabyrinthOfOnzozo.TreasureChest).OnTrade(q => ChestTrade(q, KeyItem.Lancienne))
                .Npc(Zone.PortBastok, Npc.PortBastok.Carmelo).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return GetNumPages(q.Player) == 3 ? QuestReply.ProgressEvent(272) : QuestReply.Event(271);
                    }
                    else if (questProgress == 1)
                    {
                        return !q.Quest.MustZone ? QuestReply.ProgressEvent(274) : QuestReply.Event(273);
                    }
                    return null;
                })
                .OnEventFinish(Zone.PortBastok, 272, q =>
                {
                    q.Quest.SetStep(1);
                    q.Quest.SetMustZone();
                })
                .OnEventFinish(Zone.PortBastok, 274, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetQuestMustZone(QuestLog.Bastok, "LOVERS_IN_THE_DUSK");

                        foreach (int keyItemId in PageKeyItems)
                        {
                            q.Player.DelKeyItem(keyItemId);
                        }
                    }
                })
                .Npc(Zone.SeaSerpentGrotto, Npc.SeaSerpentGrotto.TreasureChest).OnTrade(q => ChestTrade(q, KeyItem.Lephemere));
        }
    }
}
