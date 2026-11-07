// Groceries (Log ID 1, Quest ID 37), a line-for-line port of scripts/quests/bastok/Groceries.lua (quest rework B2).
// Tami : !pos 62.617 0 -68.222 234; Zelman : !pos 17.095 7.704 -52.995 172. Kept as the Lua file has it: Zelman's event
// 161 (Tami's Note never looked at) leads to Tami's event 112, which gives 10 fame and 10 gil and sets Prog back to 0, so
// the first section matches again and Tami offers event 110 once more (quest:begin on an accepted quest, the note again).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class Groceries : QuestScript
    {
        public Groceries() : base(QuestLog.Bastok, "GROCERIES") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Groceries";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 10, FameArea = FameArea.Bastok, Item = Item.RabbitMantle });

            // the offer, and the start over after event 112
            b.State(QuestStatus.Available)
                .Or(QuestStatus.Accepted, step: 0)
                .Npc(Zone.BastokMines, Npc.BastokMines.Tami).OnTrigger(QuestReply.ProgressEvent(110))
                .OnEventFinish(Zone.BastokMines, 110, q =>
                {
                    q.Quest.Begin();

                    q.Player.GiveKeyItem(KeyItem.TamisNote);
                    q.Quest.SetStep(1);
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMines, Npc.BastokMines.Tami)
                    .OnTrade(q =>
                    {
                        if (q.Quest.Step == 3 && q.Trade.Matches((Item.StripOfMeatJerky, 1)))
                        {
                            return QuestReply.ProgressEvent(113);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int questProgress = q.Quest.Step;

                        if (questProgress == 1)
                        {
                            return QuestReply.MessageName(Text.BastokMines.TAMI_MY_HUSBAND);
                        }
                        else if (questProgress == 2)
                        {
                            return QuestReply.ProgressEvent(112);
                        }
                        else if (questProgress == 3)
                        {
                            return QuestReply.Event(111);
                        }
                        return null;
                    })
                .OnEventFinish(Zone.BastokMines, 112, q =>
                {
                    q.Player.AddFame(FameArea.Bastok, 10);
                    q.Player.GiveCurrency("gil", 10);

                    q.Quest.SetStep(0);
                })
                .OnEventFinish(Zone.BastokMines, 113, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                })
                // Zelman: event 162 when the note was looked at in the key item menu, 161 when not
                .Npc(Zone.ZeruhnMines, Npc.ZeruhnMines.Zelman).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 1)
                    {
                        if (q.Player.SeenKeyItem(KeyItem.TamisNote))
                        {
                            return QuestReply.ProgressEvent(162);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(161);
                        }
                    }
                    else if (questProgress >= 2)
                    {
                        return QuestReply.MessageName(Text.ZeruhnMines.ZELMAN_CANT_RUN_AROUND);
                    }
                    return null;
                })
                .OnEventFinish(Zone.ZeruhnMines, 161, q =>
                {
                    q.Quest.SetStep(2);
                    q.Player.DelKeyItem(KeyItem.TamisNote);
                })
                .OnEventFinish(Zone.ZeruhnMines, 162, q =>
                {
                    q.Quest.SetStep(3);
                    q.Player.DelKeyItem(KeyItem.TamisNote);
                });
        }
    }
}
