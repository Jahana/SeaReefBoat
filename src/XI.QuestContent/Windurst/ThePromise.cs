// The Promise (Log ID 2, Quest ID 78), a line-for-line port of scripts/quests/windurst/SOB7_The_Promise.lua (quest
// rework B3b). Kohlo-Lakolo : !pos -26.8 -6 190 240; Chamama (Windurst Waters). Once Wild Card is completed, at level 5
// and Windurst fame level 5, Kohlo-Lakolo's 513 begins it below Windurst rank 9 ("in time": Prog 1) or 532 at rank 9
// (Prog 0); otherwise his 505 / 535. Accepted: Chamama's 797 (the Chamama var 1), then 798; Shoalweed traded alone with
// the var at 1 and no sticker plays 799 (taken, the Invisible Man Sticker), then 800. Kohlo-Lakolo with the sticker:
// 522 (in time at Prog 1), 542 (Prog 1, rank 9), 534 (Prog 0): each completes the quest (fame 60, the Promise Badge;
// the sticker taken, the char var SOBfinalEvent 1); without it his reminders 514, 543, 533. The brigade: in-time and
// late lines (Prog 1 and below rank 9, or not), Pichichi's 530 (Prog 1 or rank 9), Shanruru's 529 (Prog 0 or rank 9).
// Completed: the brigade's final lines by SOBfinalEvent (540 / 590, ...); Kohlo-Lakolo with SOBfinalEvent 1 plays 584
// with the Dark Mana Orb (SOBfinalEvent 0) or 528 without it, and with SOBfinalEvent 0 a random 544 or 585. Kept as the
// Lua file has it: the offer reads this quest's char-var mustZone, which nothing sets; Pichichi's and Shanruru's lines
// return nil otherwise (their defaults).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class ThePromise : QuestScript
    {
        public ThePromise() : base(QuestLog.Windurst, "THE_PROMISE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/SOB7_The_Promise";

        static QuestReply TimedEvents(IQuestContext q, int inTime, int outATime)
        {
            if (q.Quest.Step == 1 &&
                q.Player.Rank(Nation.Windurst) < 9)
            {
                return QuestReply.Event(inTime);
            }
            else
            {
                return QuestReply.Event(outATime);
            }
        }

        static QuestReply FinalEvents(IQuestContext q, int inTime, int outATime)
        {
            if (q.Player.CharVar("SOBfinalEvent") == 1)
            {
                return QuestReply.Event(inTime);
            }
            else
            {
                return QuestReply.Event(outATime);
            }
        }

        // 522, 534 and 542 (the same body in Lua)
        static void CompleteQuest(IQuestContext q)
        {
            if (q.Quest.Complete())
            {
                q.Player.DelKeyItem(KeyItem.InvisibleManSticker);
                q.Player.SetCharVar("SOBfinalEvent", 1);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 60, FameArea = FameArea.Windurst, Item = Item.PromiseBadge });

            // Section: Quest is available.
            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.Windurst, "WILD_CARD"))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KohloLakolo).OnTrigger(q =>
                {
                    if (q.Player.MainLevel >= 5 &&
                        q.Player.FameLevel(FameArea.Windurst) >= 5 &&
                        !q.Quest.MustZone)
                    {
                        if (q.Player.Rank(Nation.Windurst) < 9)
                        {
                            return QuestReply.ProgressEvent(513, 0, KeyItem.InvisibleManSticker); // Quest starting event in time.
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(532, 0, KeyItem.InvisibleManSticker); // Quest starting event late.
                        }
                    }
                    else
                    {
                        if (q.Player.Rank(Nation.Windurst) < 9)
                        {
                            return QuestReply.Event(505); // Default text in time.
                        }
                        else
                        {
                            return QuestReply.Event(535); // Default text late.
                        }
                    }
                })
                .OnEventFinish(Zone.PortWindurst, 513, q =>
                {
                    q.Quest.Begin();
                    q.Quest.SetStep(1); // Start in time.
                })
                .OnEventFinish(Zone.PortWindurst, 532, q => q.Quest.Begin());

            // Section: Quest accepeted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KohloLakolo).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.InvisibleManSticker))
                    {
                        if (q.Quest.Step == 1)
                        {
                            if (q.Player.Rank(Nation.Windurst) < 9)
                            {
                                return QuestReply.ProgressEvent(522, 0, KeyItem.InvisibleManSticker); // Quest Complete. In time.
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(542, 0, KeyItem.InvisibleManSticker); // Quest Complete. Started in time, arrived late.
                            }
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(534, 0, KeyItem.InvisibleManSticker); // Quest Complete. Started late.
                        }
                    }
                    else
                    {
                        if (q.Quest.Step == 1)
                        {
                            if (q.Player.Rank(Nation.Windurst) < 9)
                            {
                                return QuestReply.Event(514); // Reminder. In time.
                            }
                            else
                            {
                                return QuestReply.Event(543, 0, KeyItem.InvisibleManSticker); // Reminder. Started in time, but are late.
                            }
                        }
                        else
                        {
                            return QuestReply.Event(533, 0, KeyItem.InvisibleManSticker); // Reminder. Started late.
                        }
                    }
                })
                .Npc(Zone.PortWindurst, Npc.PortWindurst.GomadaVulmada).OnTrigger(q => TimedEvents(q, 518, 528))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PapoHopo).OnTrigger(q => TimedEvents(q, 515, 525))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Pichichi).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1 ||
                        q.Player.Rank(Nation.Windurst) >= 9)
                    {
                        return QuestReply.Event(530);
                    }

                    return null;
                })
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PyoNzon).OnTrigger(q => TimedEvents(q, 517, 527))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Shanruru).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0 ||
                        q.Player.Rank(Nation.Windurst) >= 9)
                    {
                        return QuestReply.Event(529);
                    }

                    return null;
                })
                .Npc(Zone.PortWindurst, Npc.PortWindurst.YafaYaa).OnTrigger(q => TimedEvents(q, 516, 526))
                .OnEventFinish(Zone.PortWindurst, 522, CompleteQuest)
                .OnEventFinish(Zone.PortWindurst, 534, CompleteQuest)
                .OnEventFinish(Zone.PortWindurst, 542, CompleteQuest)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Chamama)
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasKeyItem(KeyItem.InvisibleManSticker))
                        {
                            return QuestReply.Event(800);
                        }
                        else if (q.Quest.Var("Chamama") > 0)
                        {
                            return QuestReply.Event(798, 0, Item.Shoalweed, KeyItem.InvisibleManSticker);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(797, 0, Item.Shoalweed, KeyItem.InvisibleManSticker);
                        }
                    })
                    .OnTrade(q =>
                    {
                        if (q.Quest.Var("Chamama") == 1 &&
                            q.Trade.HasExactly(Item.Shoalweed) &&
                            !q.Player.HasKeyItem(KeyItem.InvisibleManSticker))
                        {
                            return QuestReply.ProgressEvent(799, 0, 0, KeyItem.InvisibleManSticker);
                        }

                        return null;
                    })
                .OnEventFinish(Zone.WindurstWaters, 797, q => q.Quest.SetVar("Chamama", 1))
                .OnEventFinish(Zone.WindurstWaters, 799, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.InvisibleManSticker);
                });

            // Section: Quest completed.
            b.State(QuestStatus.Completed)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.GomadaVulmada).OnTrigger(q => FinalEvents(q, 540, 590))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KohloLakolo).OnTrigger(q =>
                {
                    if (q.Player.CharVar("SOBfinalEvent") == 1)
                    {
                        if (q.Player.HasKeyItem(KeyItem.DarkManaOrb))
                        {
                            return QuestReply.ProgressEvent(584);
                        }
                        else
                        {
                            return QuestReply.Event(528);
                        }
                    }
                    else
                    {
                        int random = q.Player.RandomInt(0, 1);
                        if (random == 0)
                        {
                            return QuestReply.Event(544);
                        }
                        else
                        {
                            return QuestReply.Event(585);
                        }
                    }
                })
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PapoHopo).OnTrigger(q => FinalEvents(q, 537, 587))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Pichichi).OnTrigger(q => FinalEvents(q, 536, 586))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PyoNzon).OnTrigger(q => FinalEvents(q, 539, 589))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Shanruru).OnTrigger(q => FinalEvents(q, 541, 591))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.YafaYaa).OnTrigger(q => FinalEvents(q, 538, 588))
                .OnEventFinish(Zone.PortWindurst, 584, q => q.Player.SetCharVar("SOBfinalEvent", 0));
        }
    }
}
