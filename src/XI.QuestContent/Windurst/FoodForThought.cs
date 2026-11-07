// Food for Thought (Log ID 2, Quest ID 14), a line-for-line port of scripts/quests/windurst/Food_for_Thought.lua (quest
// rework B3b). Kenapa-Keppa : !pos 27 -6 -199 238; Kerutoto : !pos 13 -5 -157 238; Ohbiru-Dohbiru : !pos 23 -5 -193 238.
// Three researchers want food: Kenapa a Hard-Boiled Egg (120 gil), Kerutoto a Slice of Grilled Hare (440 gil), Ohbiru
// tea, a tortilla and pamtam kelp (440 gil). Each delivery's finish completes the quest when the other two are done
// (kenapaProg 4, kerutotoProg 2, ohbiruProg 3), else marks its own as done. A delivery made before asking for the order
// plays one of two events at random (math.randomInt). Say It with Flowers' vars gate Kenapa's and Ohbiru's offer lines
// (the char vars Quest[2][50]Option and Quest[2][50]Prog, read raw). Overnight Delivery reads its completion and
// mustZone. Kept as the Lua file has it: Kerutoto's refusal (option 1 of 313) leaves kerutotoProg 256 and the offer at
// 314; 326 has no gil argument but still pays 440; the completed Ohbiru line is his default event (344), replaceDefault.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class FoodForThought : QuestScript
    {
        public FoodForThought() : base(QuestLog.Windurst, "FOOD_FOR_THOUGHT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Food_for_Thought";

        static void KenapaTradeEventFinish(IQuestContext q)
        {
            q.Trade.Confirm();
            q.Player.AddGil(120);

            if (q.Quest.Var("kerutotoProg") == 2 && q.Quest.Var("ohbiruProg") == 3)
            {
                if (q.Quest.Complete())
                {
                    q.Quest.SetMustZone();
                }
            }
            else
            {
                q.Quest.SetVar("kenapaProg", 4);
            }
        }

        static void OhbiruTradeEventFinish(IQuestContext q)
        {
            q.Trade.Confirm();
            q.Player.AddGil(440);

            if (q.Quest.Var("kerutotoProg") == 2 && q.Quest.Var("kenapaProg") == 4)
            {
                if (q.Quest.Complete())
                {
                    q.Quest.SetMustZone();
                }
            }
            else
            {
                q.Quest.SetVar("ohbiruProg", 3);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Windurst, Title = Title.FastFoodDeliverer });

            b.State(QuestStatus.Available)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.KenapaKeppa).OnTrigger(q =>
                {
                    // Getting the dialogue about the cactus in the quest "Say it with Flowers" takes priority and doesn't
                    // clear until they clear the quest.
                    if (q.Player.CharVar("Quest[2][50]Option") != 1)
                    {
                        return QuestReply.ProgressEvent(310);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Kerutoto).OnTrigger(q =>
                {
                    int kerutotoProgress = q.Quest.Var("kerutotoProg");
                    int ohbiruProgress = q.Quest.Var("ohbiruProg");

                    // Player knows the researchers are hungry and quest not refused
                    if (ohbiruProgress == 1 && kerutotoProgress != 256)
                    {
                        return QuestReply.ProgressEvent(313, 0, 4371);
                    }
                    // Player knows the researchers are hungry and quest refused previously
                    else if (ohbiruProgress == 1 && kerutotoProgress == 256)
                    {
                        return QuestReply.ProgressEvent(314, 0, 4371);
                    }
                    // Before Quest: Asks you to check on others.
                    else
                    {
                        return QuestReply.Event(312);
                    }
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.OhbiruDohbiru).OnTrigger(q =>
                {
                    int ohbiruProgress = q.Quest.Var("ohbiruProg");

                    // The player CANNOT have the quest "Say it with Flowers" Active or Ranpi will not talk to you about
                    // this quest
                    if (q.Player.CharVar("Quest[2][50]Prog") != 1)
                    {
                        if (ohbiruProgress == 0)
                        {
                            return QuestReply.ProgressEvent(308);
                        }
                        else if (ohbiruProgress == 1)
                        {
                            return QuestReply.ProgressEvent(309);
                        }
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWaters, 308, q => q.Quest.SetVar("ohbiruProg", 1))
                .OnEventFinish(Zone.WindurstWaters, 313, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                        q.Quest.SetVar("kerutotoProg", 1);
                    }
                    else if (q.Option == 1)
                    {
                        q.Quest.SetVar("kerutotoProg", 256);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 314, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                        q.Quest.SetVar("kerutotoProg", 1);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.KenapaKeppa)
                    .OnTrade(q =>
                    {
                        int kenapaProg = q.Quest.Var("kenapaProg");

                        if (q.Trade.HasExactly(Item.HardBoiledEgg))
                        {
                            // Traded item without receiving order
                            if (kenapaProg < 3)
                            {
                                if (q.Player.RandomInt(1, 100) <= 50)
                                {
                                    return QuestReply.ProgressEvent(331);
                                }
                                else
                                {
                                    return QuestReply.ProgressEvent(330, 120);
                                }
                            }
                            // Traded item after receiving order
                            else if (kenapaProg == 3)
                            {
                                return QuestReply.ProgressEvent(327, 120);
                            }
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(329);
                        }

                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int kenapaProg = q.Quest.Var("kenapaProg");

                        if (kenapaProg == 0)
                        {
                            return QuestReply.ProgressEvent(318);
                        }
                        else if (kenapaProg == 1)
                        {
                            return QuestReply.ProgressEvent(319);
                        }
                        else if (kenapaProg == 2)
                        {
                            return QuestReply.ProgressEvent(320, 0, Item.HardBoiledEgg);
                        }
                        else if (kenapaProg == 3)
                        {
                            int randEvent = q.Player.RandomInt(1, 3);

                            if (randEvent == 1)
                            {
                                return QuestReply.ProgressEvent(320, 0, Item.HardBoiledEgg); // Repeats Order
                            }
                            else if (randEvent == 2)
                            {
                                return QuestReply.ProgressEvent(321); // 'Or Whatever'
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(328); // '..<Grin>..'
                            }
                        }

                        return null;
                    })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Kerutoto)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.SliceOfGrilledHare) && q.Quest.Var("kerutotoProg") == 1)
                        {
                            return QuestReply.ProgressEvent(332, 440);
                        }

                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int kerutotoProgress = q.Quest.Var("kerutotoProg");

                        if (kerutotoProgress == 1)
                        {
                            return QuestReply.ProgressEvent(315, 0, Item.SliceOfGrilledHare);
                        }
                        else if (kerutotoProgress == 2)
                        {
                            return QuestReply.ProgressEvent(333);
                        }

                        return null;
                    })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.LeepeHoppe).OnTrigger(q => QuestReply.ProgressEvent(311))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.OhbiruDohbiru)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.CupOfWindurstianTea, Item.Tortilla, Item.ClumpOfPamtamKelp))
                        {
                            int ohbiruProgress = q.Quest.Var("ohbiruProg");

                            // Traded all 3 items & Didn't ask for order
                            if (ohbiruProgress < 2)
                            {
                                if (q.Player.RandomInt(1, 100) <= 50)
                                {
                                    return QuestReply.ProgressEvent(325, 440);
                                }
                                else
                                {
                                    return QuestReply.ProgressEvent(326);
                                }
                            }
                            // Traded all 3 items after receiving order
                            else if (ohbiruProgress == 2)
                            {
                                return QuestReply.ProgressEvent(322, 440);
                            }
                        }

                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int ohbiruProgress = q.Quest.Var("ohbiruProg");

                        if (ohbiruProgress < 2)
                        {
                            return QuestReply.ProgressEvent(316, 0, 4493, 624, 4408);
                        }
                        else if (ohbiruProgress == 2)
                        {
                            return QuestReply.ProgressEvent(317, 0, 4493, 624, 4408);
                        }
                        else if (ohbiruProgress == 3)
                        {
                            return QuestReply.ProgressEvent(324);
                        }

                        return null;
                    })
                .OnEventFinish(Zone.WindurstWaters, 316, q => q.Quest.SetVar("ohbiruProg", 2))
                .OnEventFinish(Zone.WindurstWaters, 318, q => q.Quest.SetVar("kenapaProg", 1))
                .OnEventFinish(Zone.WindurstWaters, 319, q => q.Quest.SetVar("kenapaProg", 2))
                .OnEventFinish(Zone.WindurstWaters, 320, q => q.Quest.SetVar("kenapaProg", 3))
                .OnEventFinish(Zone.WindurstWaters, 322, OhbiruTradeEventFinish)
                .OnEventFinish(Zone.WindurstWaters, 325, OhbiruTradeEventFinish)
                .OnEventFinish(Zone.WindurstWaters, 326, OhbiruTradeEventFinish)
                .OnEventFinish(Zone.WindurstWaters, 327, KenapaTradeEventFinish)
                .OnEventFinish(Zone.WindurstWaters, 330, KenapaTradeEventFinish)
                .OnEventFinish(Zone.WindurstWaters, 331, KenapaTradeEventFinish)
                .OnEventFinish(Zone.WindurstWaters, 332, q =>
                {
                    q.Trade.Confirm();
                    q.Player.AddGil(440);

                    if (q.Quest.Var("kenapaProg") == 4 && q.Quest.Var("ohbiruProg") == 3)
                    {
                        if (q.Quest.Complete())
                        {
                            q.Quest.SetMustZone();
                        }
                    }
                    else
                    {
                        q.Quest.SetVar("kerutotoProg", 2);
                    }
                });

            b.State(QuestStatus.Completed, when: q => q.Quest.MustZone)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Kerutoto).OnTrigger(QuestReply.Event(304).ReplaceDefault())
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.OhbiruDohbiru).OnTrigger(QuestReply.Event(344).ReplaceDefault());
        }
    }
}
