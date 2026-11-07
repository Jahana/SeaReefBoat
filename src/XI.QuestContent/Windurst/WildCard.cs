// Wild Card (Log ID 2, Quest ID 77), a line-for-line port of scripts/quests/windurst/SOB6_Wild_Card.lua (quest rework
// B3b). Honoi-Gomoi : !pos -195 -11 -120 238. Once Crying Over Onions is completed, Honoi-Gomoi's 780 begins it at level
// 5 and Windurst fame level 5 (779 otherwise). Accepted: Kohlo-Lakolo's 545 (Prog 0: Prog 1), Papo-Hopo's 509; the House
// of the Hero at Prog 1 plays 386 below Windurst rank 9 (Prog 2: meet the Joker) or 388 at rank 9 (Prog 3: meet
// Apururu). At Prog 2 or 3 without the Joker Card, a Toraimarai Canal Treasure Coffer opened by trade gives the Joker
// Card (the treasure trade with the key item as its bypass reward). With the card, the House of the Hero plays 387
// (below rank 9) or 389 (rank 9): the card taken, 8000 gil, Prog 4; at Prog 3 Apururu's 600 does the same (Windurst
// Woods). Honoi-Gomoi's 782 at Prog 4 completes it (fame 60, Dream Dweller), 781 before. Completed: Honoi-Gomoi's 783
// (replaceDefault). Kept as the Lua file has it: the offer reads this quest's char-var mustZone, which nothing sets; 782
// sets the player local var '[2][78]mustZone', which nothing reads; Apururu's 600 pays whether the card is held or not;
// the reward has no item, so the completion cannot fail.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class WildCard : QuestScript
    {
        public WildCard() : base(QuestLog.Windurst, "WILD_CARD") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/SOB6_Wild_Card";

        // 387, 389 and 600 (the same body in Lua)
        static void PayOut(IQuestContext q)
        {
            q.Player.DelKeyItem(KeyItem.JokerCard);
            q.Player.GiveCurrency("gil", 8000);
            q.Quest.SetStep(4);
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 60, FameArea = FameArea.Windurst, Title = Title.DreamDweller });

            // Section: Quest is available.
            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.Windurst, "CRYING_OVER_ONIONS"))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.HonoiGomoi).OnTrigger(q =>
                {
                    if (q.Player.MainLevel >= 5 &&
                        q.Player.FameLevel(FameArea.Windurst) >= 5 &&
                        !q.Quest.MustZone)
                    {
                        return QuestReply.ProgressEvent(780); // Quest starting event.
                    }
                    else
                    {
                        return QuestReply.Event(779); // Default text.
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 780, q => q.Quest.Begin());

            // Section: Quest accepeted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.KohloLakolo).OnTrigger(QuestReply.ProgressEvent(545))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.PapoHopo).OnTrigger(QuestReply.Event(509))
                .OnEventFinish(Zone.PortWindurst, 545, q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .Npc(Zone.ToraimaraiCanal, Npc.ToraimaraiCanal.TreasureCoffer).OnTrade(q =>
                {
                    int questProgress = q.Quest.Step;
                    if (!q.Player.HasKeyItem(KeyItem.JokerCard) &&
                        (questProgress == 2 || questProgress == 3))
                    {
                        q.Trade.TreasureTrade(2, KeyItem.JokerCard);

                        return QuestReply.NoAction();
                    }

                    return null;
                })
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls._6n2).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1) // First meeting at house of hero.
                    {
                        if (q.Player.Rank(Nation.Windurst) < 9)
                        {
                            return QuestReply.ProgressEvent(386); // Meet Joker.
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(388); // Meet Apururu.
                        }
                    }
                    else if (q.Player.HasKeyItem(KeyItem.JokerCard)) // Second meeting at house of hero.
                    {
                        if (q.Player.Rank(Nation.Windurst) < 9)
                        {
                            return QuestReply.ProgressEvent(387, 0, KeyItem.JokerCard); // Meet Joker after meeting Joker first.
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(389); // Meet Apururu after meeting Joker first.
                        }
                    }

                    return null;
                })
                // 2 = Met Joker. Sets 2nd CS in Windurst Walls with either Joker or Apururu.
                .OnEventFinish(Zone.WindurstWalls, 386, q => q.Quest.SetStep(2))
                .OnEventFinish(Zone.WindurstWalls, 387, PayOut)
                // 3 = Met Apururu. Sets 2nd CS in Windurst Woods with Apururu.
                .OnEventFinish(Zone.WindurstWalls, 388, q => q.Quest.SetStep(3))
                .OnEventFinish(Zone.WindurstWalls, 389, PayOut)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.HonoiGomoi).OnTrigger(q =>
                {
                    if (q.Quest.Step == 4)
                    {
                        return QuestReply.ProgressEvent(782); // Quest Complete.
                    }
                    else
                    {
                        return QuestReply.Event(781); // Reminder text.
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 782, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetLocalVar("[2][78]mustZone", 1);
                    }
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Apururu).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(600); // 2nd meeting with Apururu after meeting him in Hero's hause.
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWoods, 600, PayOut);

            // Section: Quest completed.
            b.State(QuestStatus.Completed)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.HonoiGomoi).OnTrigger(QuestReply.Event(783).ReplaceDefault());
        }
    }
}
