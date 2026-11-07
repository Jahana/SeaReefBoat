// A Taste for Meat (Log ID 0, Quest ID 100), a line-for-line port of scripts/quests/sandoria/A_Taste_for_Meat.lua (quest
// rework B3b, San d'Oria group 6). Antreneau : !pos -71 -5 -39 232; Thierride : !pos -67 -5 -28 232. Never flagged: it is
// begun and completed in one finish. Available: Antreneau's 527 at Prog 0 (Prog 1), then 525; a trade to him plays 531
// at Prog 1 with a Slice of Hare Meat in it (npcUtil.tradeHas; 531 has no finish handler), else 532. Thierride's 526 at
// Prog 1 (nothing at Prog 0: his default 521); a trade of exactly five slices at Prog 1 plays 528 (begun and completed:
// 150 gil, fame +20, Rabbiter; the meat taken; the var Option 1), else 529. Completed: Antreneau's 530 while Option is
// 1 (a Slice of Grilled Hare and Option 0; with no room he starts 538), Thierride's 524 (replaceDefault). Antreneau's
// script reads the var Option (`Quest[0][100]Option`, Over the Hills and Far Away) and the status: they stay, so the C#
// quest keeps the var name Option. The Dismayed Customer needs it completed.
// Kept as the Lua has it: 531 has no finish handler (the meat is kept); 530's no-room arm starts 538 from inside the
// finish.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class ATasteForMeat : QuestScript
    {
        public ATasteForMeat() : base(QuestLog.Sandoria, "A_TASTE_FOR_MEAT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/A_Taste_for_Meat";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Sandoria, Gil = 150, Title = Title.Rabbiter });

            // This entire quest is not flagged; however, the quest is accepted and completed in the same step (on
            // trading 5 hare meat after progressing).
            b.State(QuestStatus.Available)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Antreneau)
                    .OnTrade(q =>
                    {
                        if (q.Quest.Step == 1 &&
                            q.Trade.Has(Item.SliceOfHareMeat))
                        {
                            return QuestReply.ProgressEvent(531);
                        }
                        else
                        {
                            return QuestReply.Event(532);
                        }
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Step == 0)
                        {
                            return QuestReply.ProgressEvent(527);
                        }
                        else
                        {
                            return QuestReply.Event(525);
                        }
                    })
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Thierride)
                    .OnTrade(q =>
                    {
                        if (q.Quest.Step == 1 &&
                            // npcUtil.tradeHasExactly(trade, { { xi.item.SLICE_OF_HARE_MEAT, 5 } })
                            q.Trade.HasExactly((Item.SliceOfHareMeat, 5)))
                        {
                            return QuestReply.ProgressEvent(528);
                        }
                        else
                        {
                            return QuestReply.Event(529);
                        }
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Step == 1)
                        {
                            return QuestReply.ProgressEvent(526);
                        }
                        return null;
                    })
                .OnEventFinish(Zone.PortSanDoria, 527, q => q.Quest.SetStep(1))
                .OnEventFinish(Zone.PortSanDoria, 528, q =>
                {
                    q.Quest.Begin();
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();

                        // This variable is set after quest has been cleared, and is cleaned up after receiving Grilled
                        // Hare item from Antreneau.
                        q.Quest.SetVar("Option", 1);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Antreneau).OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") == 1)
                    {
                        return QuestReply.ProgressEvent(530);
                    }
                    return null;
                })
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Thierride).OnTrigger(QuestReply.Event(524).ReplaceDefault())
                .OnEventFinish(Zone.PortSanDoria, 530, q =>
                {
                    if (q.Player.GiveItem(Item.SliceOfGrilledHare))
                    {
                        q.Quest.SetVar("Option", 0);
                    }
                    else
                    {
                        // player:startEvent(538)
                        q.Player.StartEvent(538);
                    }
                });
        }
    }
}
