// Wings of Gold (Log ID 3, Quest ID 60), a line-for-line port of scripts/quests/jeuno/Wings_of_Gold.lua (quest rework
// B3b). Brutus : !pos -55 8 95 244. BST AF1: Path of the Beastmaster completed, AF1_QUEST_LEVEL, a beastmaster.
// Brutus offers 139 (long; option 1 begins it, another sets Prog 1) then 137 (short; option 1 begins it). Accepted:
// a treasure chest of Middle or Upper Delkfutt's Tower gives the Guiding Bell (the treasure trade's key item bypass;
// noAction whatever it returns) while the bell is not held; Brutus answers 136 until the bell, then 138: completed
// (Barbaroi Axe), fame +7 in San d'Oria, Bastok and Windurst, the bell deleted. Completed (until Scattered into Shadow
// is): Brutus 134 (replaceDefault). The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class WingsOfGold : QuestScript
    {
        public WingsOfGold() : base(QuestLog.Jeuno, "WINGS_OF_GOLD") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Wings_of_Gold";

        // the two towers' chests
        static QuestReply ChestTrade(IQuestContext q)
        {
            if (!q.Player.HasKeyItem(KeyItem.GuidingBell))
            {
                q.Trade.TreasureTrade(2, KeyItem.GuidingBell);

                return QuestReply.NoAction();
            }
            return null;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.BarbaroiAxe });
            b.Steps(1);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Jeuno, "PATH_OF_THE_BEASTMASTER") &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF1_QUEST_LEVEL") &&
                    q.Player.MainJob == Job.Bst)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Brutus).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(139);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(137);
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 137, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 139, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                    else
                    {
                        q.Quest.SetStep(1); // If declined then future Brutus quest start will use short dialog
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.MiddleDelkfuttsTower, Npc.MiddleDelkfuttsTower.TreasureChest).OnTrade(ChestTrade)
                .Npc(Zone.UpperDelkfuttsTower, Npc.UpperDelkfuttsTower.TreasureChest).OnTrade(ChestTrade)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Brutus).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.GuidingBell))
                    {
                        return QuestReply.Event(136);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(138);
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 138, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 7);
                        q.Player.AddFame(FameArea.Bastok, 7);
                        q.Player.AddFame(FameArea.Windurst, 7);
                        q.Player.DelKeyItem(KeyItem.GuidingBell);
                    }
                });

            b.State(QuestStatus.Completed, when: q => !q.Player.HasCompletedQuest(QuestLog.Jeuno, "SCATTERED_INTO_SHADOW"))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Brutus).OnTrigger(QuestReply.Event(134).ReplaceDefault());
        }
    }
}
