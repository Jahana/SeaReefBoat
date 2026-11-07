// Sin Hunting (Log ID 2, Quest ID 72, RNG AF1), a line-for-line port of scripts/quests/windurst/RNG_AF1_Sin_Hunting.lua
// (quest rework B3b). Perih Vashai : !pos 117 -3 92 241; Perchond : !pos -182.844 4 -164.948 166; Alexius : !pos 105 1
// 382 104; qm2 (???) : !pos -10.946 -1 313.810 104. The pinch of glittersand drops from the Evil Weapons around the
// Waters of Oblivion. A RNG at AF1_QUEST_LEVEL: Perih Vashai's 523, option 0, begins it (the Chieftainness's Twinstone
// Earring). Accepted: Perih Vashai's 524 (527 at Prog 4), Kapeh Myohrye's 526, Muhk Johldy's 525. Perchond (Ranguemont
// Pass): 3 at Prog 0 (option 0: Prog 1), 4 at Prog 1, 6 at Prog 2-3; the glittersand alone at Prog 1: 5 (option 0: the
// trade taken, Perchond's Envelope, Prog 2). Alexius (Jugner Forest): 10 at Prog 2 (option 0: Prog 3), 11 at Prog 3. The
// qm2 at Prog 3: 13 (option 0: Prog 4). Perih Vashai's 527 completes it (fame 20, the Sniping Bow; both key items
// taken). Completed: Kapeh Myohrye's 530 and Muhk Johldy's 529 (replaceDefault). The era module
// era_quest_sin_hunting (Modules/Era/Windurst/SinHunting.cs) limits the qm2 to the full moon. The Lua file has no known
// bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class SinHunting : QuestScript
    {
        public SinHunting() : base(QuestLog.Windurst, "SIN_HUNTING") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/RNG_AF1_Sin_Hunting";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Windurst, Item = Item.SnipingBow });

            // Section: Quest is available.
            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainJob == Job.Rng &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF1_QUEST_LEVEL"))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.PerihVashai).OnTrigger(QuestReply.ProgressEvent(523))
                .OnEventFinish(Zone.WindurstWoods, 523, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.ChieftainnesssTwinstoneEarring);
                    }
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.PerihVashai).OnTrigger(q =>
                {
                    if (q.Quest.Step == 4)
                    {
                        return QuestReply.ProgressEvent(527);
                    }
                    else
                    {
                        return QuestReply.Event(524);
                    }
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.KapehMyohrye).OnTrigger(QuestReply.Event(526))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.MuhkJohldy).OnTrigger(QuestReply.Event(525))
                .OnEventFinish(Zone.WindurstWoods, 527, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.ChieftainnesssTwinstoneEarring);
                        q.Player.DelKeyItem(KeyItem.PerchondsEnvelope);
                    }
                })
                .Npc(Zone.RanguemontPass, Npc.RanguemontPass.Perchond)
                    .OnTrade(q =>
                    {
                        if (
                            q.Quest.Step == 1 &&
                            q.Trade.Matches((Item.PinchOfGlittersand, 1)))
                        {
                            return QuestReply.ProgressEvent(5);
                        }

                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int prog = q.Quest.Step;

                        if (prog == 0)
                        {
                            return QuestReply.ProgressEvent(3, 0, Item.PinchOfGlittersand);
                        }
                        else if (prog == 1)
                        {
                            return QuestReply.Event(4, 0, Item.PinchOfGlittersand);
                        }
                        else if (prog == 2 || prog == 3)
                        {
                            return QuestReply.Event(6); // Reminder to deliver the envelope.
                        }

                        return null;
                    })
                .OnEventFinish(Zone.RanguemontPass, 3, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.RanguemontPass, 5, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Trade.Complete();
                        q.Player.GiveKeyItem(KeyItem.PerchondsEnvelope);
                        q.Quest.SetStep(2);
                    }
                })
                .Npc(Zone.JugnerForest, Npc.JugnerForest.Alexius).OnTrigger(q =>
                {
                    int prog = q.Quest.Step;

                    if (prog == 2)
                    {
                        return QuestReply.ProgressEvent(10);
                    }
                    else if (prog == 3)
                    {
                        return QuestReply.Event(11);
                    }

                    return null;
                })
                .Npc(Zone.JugnerForest, Npc.JugnerForest.qm2).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(13, 0, Item.PinchOfGlittersand);
                    }

                    return null;
                })
                .OnEventFinish(Zone.JugnerForest, 10, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetStep(3);
                    }
                })
                .OnEventFinish(Zone.JugnerForest, 13, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetStep(4);
                    }
                });

            // Section: Quest completed.
            b.State(QuestStatus.Completed)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.KapehMyohrye).OnTrigger(QuestReply.Event(530).ReplaceDefault())
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.MuhkJohldy).OnTrigger(QuestReply.Event(529).ReplaceDefault());
        }
    }
}
