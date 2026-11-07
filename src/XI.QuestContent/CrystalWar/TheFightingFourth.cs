// The Fighting Fourth (Log ID 7, Quest ID 7), a line-for-line port of
// scripts/quests/crystalWar/WOTG_BAS_0_The_Fighting_Fourth.lua (quest rework B3b Crystal War, group 1). The Eldieme
// Necropolis [S]: Turbulent Storm !pos 422.5 -48 -47.3 (script: 9 with an allegiance, 8 with the RED letter, else 7; its
// own finish 7 on option 0 gives the Blue Recommendation Letter). Bastok Markets [S]: Adelbrecht !pos -325.8 -12.6 -77
// (a script with no onTrigger). North Gustaberg [S]: Gebhardt !pos 206.3 -20.8 669.1 (default 110), Roderich !pos -400 40
// -90.4 (default 111), Barricade !pos -515 38 583.3 (default NO_WAY_PAST_BARRIER).
// Available: Turbulent Storm: with the Blue Recommendation Letter or the Battle Rations, 8; else 7, whose finish on
// option 0 gives the letter (events: the script answers on the lookup's other turn). Adelbrecht: 139 (0, 0) with the
// letter, 139 (1, 23) without; 139 on option 1 takes the letter, gives the Battle Rations and begins it. Accepted (Prog):
// Adelbrecht 140 (0) / 141 (1) / 142 (2), events whose finishes on option 1 quit the quest (140 also takes the rations
// with ARE_TAKEN_AWAY_FROM_CHAR; all three give the letter back with ITEM_RETURNED_TO_CHAR; delQuest then cleanup); 143
// (3): with no allegiance the Sprinter's Shoes first (nothing more when they do not fit); completed (Fourth Division
// Soldier), the Bronze Ribbon of Service (no allegiance), allegiance 2, NOW_ALLIED_WITH (2). Gebhardt: 102 (0), whose
// finish takes the rations (no message) and sets Prog 1; 103 / 107 / 108 (1 / 2 / 3). Roderich: 104 (1) -> Prog 2; 105 /
// 109 (2 / 3). Barricade: 106 (2) -> Prog 3. Completed: Turbulent Storm 9, Adelbrecht 162, Gebhardt 108, Roderich 109
// (events, replaceDefault).
// Kept as the Lua has it: no allegiance check before it begins; 139 without the letter still begins it on option 1
// (delKeyItem of a letter not held); 140-142 quit at the Prog read (no check of the rations at 140); 141 / 142 give the
// letter back while the rations are kept; Turbulent Storm's 7 finishes twice (the quest's and its script's: the script's
// giveKeyItem sees the letter held); Adelbrecht, Gebhardt and Roderich answer nil at a Prog outside their lists.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class TheFightingFourth : QuestScript
    {
        public TheFightingFourth() : base(QuestLog.CrystalWar, "THE_FIGHTING_FOURTH") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_BAS_0_The_Fighting_Fourth";

        static void RemoveRations(IQuestContext q)
        {
            // Note the messaging specific to this interaction!
            q.Player.DelKeyItem(KeyItem.BattleRations);
            q.Text.Special(Text.BastokMarketsS.ARE_TAKEN_AWAY_FROM_CHAR, KeyItem.BattleRations);
        }

        static void ReturnLetter(IQuestContext q)
        {
            // Note the messaging specific to this interaction!
            q.Player.GiveKeyItem(KeyItem.BlueRecommendationLetter, Text.BastokMarketsS.ITEM_RETURNED_TO_CHAR);
        }

        static void QuitQuest(IQuestContext q)
        {
            q.Player.DelQuest(QuestLog.CrystalWar, "THE_FIGHTING_FOURTH");
            q.Quest.Cleanup();
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Title = Title.FourthDivisionSoldier,
            });

            // TODO: Conditions to not have a different campaign allegiance?
            b.State(QuestStatus.Available)
                .Npc(Zone.TheEldiemeNecropolisS, Npc.TheEldiemeNecropolisS.TurbulentStorm).OnTrigger(q =>
                {
                    // TODO: Conditions to not have a different campaign allegiance?

                    bool hasLetter = q.Player.HasKeyItem(KeyItem.BlueRecommendationLetter);
                    bool hasRations = q.Player.HasKeyItem(KeyItem.BattleRations);

                    // TODO: Verify the check for rations exists on retail
                    if (hasLetter || hasRations)
                    {
                        // You won't be considered a member of the Fighting Fourth until you take that blue recommendation letter to Bastok...
                        return QuestReply.Event(8);
                    }
                    else
                    {
                        // Hold it right there. You don't look like one of ours.
                        return QuestReply.Event(7);
                    }
                })
                .OnEventFinish(Zone.TheEldiemeNecropolisS, 7, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.GiveKeyItem(KeyItem.BlueRecommendationLetter);
                    }
                })
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Adelbrecht).OnTrigger(q =>
                {
                    // TODO: Conditions to not have a different campaign allegiance?

                    if (q.Player.HasKeyItem(KeyItem.BlueRecommendationLetter))
                    {
                        // Greetings, civillian. The Seventh Cohors of the Republican Legion's Fourth Division is currently recruiting new troops.
                        // Args (from caps): With recommendation letter
                        return QuestReply.ProgressEvent(139, 0, 0);
                    }
                    else
                    {
                        // Greetings, civillian. The Seventh Cohors of the Republican Legion's Fourth Division is currently recruiting new troops.
                        // Args (from caps): Without recommendation letter (after first prompt)
                        return QuestReply.ProgressEvent(139, 1, 23);
                    }
                })
                .OnEventFinish(Zone.BastokMarketsS, 139, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.DelKeyItem(KeyItem.BlueRecommendationLetter);
                        q.Player.GiveKeyItem(KeyItem.BattleRations);
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Adelbrecht).OnTrigger(q =>
                {
                    int prog = q.Quest.Step;

                    if (prog == 0)
                    {
                        // I thought you didn't have any questions!
                        return QuestReply.Event(140);
                    }
                    else if (prog == 1)
                    {
                        // Good work, rookie. You've passed the test with flying colors! ...Or at least that's what I'd say...
                        return QuestReply.Event(141);
                    }
                    else if (prog == 2)
                    {
                        // I heard the news about the ambush.
                        return QuestReply.Event(142);
                    }
                    else if (prog == 3)
                    {
                        // Welcome back, rookie. I read the praefectus's report.
                        return QuestReply.ProgressEvent(143);
                    }

                    return null;
                })
                .OnEventFinish(Zone.BastokMarketsS, 140, q =>
                {
                    if (q.Option == 1)
                    {
                        RemoveRations(q);
                        ReturnLetter(q);
                        QuitQuest(q);
                    }
                })
                .OnEventFinish(Zone.BastokMarketsS, 141, q =>
                {
                    if (q.Option == 1)
                    {
                        ReturnLetter(q);
                        QuitQuest(q);
                    }
                })
                .OnEventFinish(Zone.BastokMarketsS, 142, q =>
                {
                    if (q.Option == 1)
                    {
                        ReturnLetter(q);
                        QuitQuest(q);
                    }
                })
                .OnEventFinish(Zone.BastokMarketsS, 143, q =>
                {
                    bool hasNoAllegiance = q.Player.CampaignAllegiance == 0;

                    if (hasNoAllegiance && !q.Player.GiveItem(Item.SprintersShoes))
                    {
                        return;
                    }

                    if (q.Quest.Complete())
                    {
                        if (hasNoAllegiance)
                        {
                            q.Player.GiveKeyItem(KeyItem.BronzeRibbonOfService);
                        }

                        q.Player.SetCampaignAllegiance(2);
                        q.Text.Special(Text.BastokMarketsS.NOW_ALLIED_WITH, 2);
                    }
                })
                .Npc(Zone.NorthGustabergS, Npc.NorthGustabergS.Gebhardt).OnTrigger(q =>
                {
                    int prog = q.Quest.Step;

                    if (prog == 0)
                    {
                        // Rations!? Hand those over, rookie!
                        return QuestReply.ProgressEvent(102);
                    }
                    else if (prog == 1)
                    {
                        return QuestReply.Event(103); // Reminder
                    }
                    else if (prog == 2)
                    {
                        // Those turtlebacks intercepted the communication!?
                        return QuestReply.Event(107);
                    }
                    else if (prog == 3)
                    {
                        // Thanks to your diligence, the Republic is safe for another day.
                        return QuestReply.Event(108);
                    }

                    return null;
                })
                .Npc(Zone.NorthGustabergS, Npc.NorthGustabergS.Roderich).OnTrigger(q =>
                {
                    int prog = q.Quest.Step;

                    if (prog == 1)
                    {
                        // Centurion Gebhardt asked you to come all the way here...?
                        return QuestReply.ProgressEvent(104);
                    }
                    else if (prog == 2)
                    {
                        return QuestReply.Event(105); // Reminder
                    }
                    else if (prog == 3)
                    {
                        // I just heard the report!
                        return QuestReply.Event(109);
                    }

                    return null;
                })
                .Npc(Zone.NorthGustabergS, Npc.NorthGustabergS.Barricade).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        // Stop right there! I think you have something of mine!
                        return QuestReply.ProgressEvent(106);
                    }

                    return null;
                })
                .OnEventFinish(Zone.NorthGustabergS, 102, q =>
                {
                    q.Player.DelKeyItem(KeyItem.BattleRations); // TODO: Confirm on retail that these are actually taken, there's no message
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.NorthGustabergS, 104, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.NorthGustabergS, 106, q =>
                {
                    q.Quest.SetStep(3);
                });

            // TODO: Are these new default texts forever, or only as long as you have this campaign allegiance?
            b.State(QuestStatus.Completed)
                // I heard about the news, recruit.
                .Npc(Zone.TheEldiemeNecropolisS, Npc.TheEldiemeNecropolisS.TurbulentStorm).OnTrigger(QuestReply.Event(9).ReplaceDefault())
                // What are you doing soldier?
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Adelbrecht).OnTrigger(QuestReply.Event(162).ReplaceDefault())
                // Thanks to your diligence, the Republic is safe for another day.
                .Npc(Zone.NorthGustabergS, Npc.NorthGustabergS.Gebhardt).OnTrigger(QuestReply.Event(108).ReplaceDefault())
                // I just heard the report!
                .Npc(Zone.NorthGustabergS, Npc.NorthGustabergS.Roderich).OnTrigger(QuestReply.Event(109).ReplaceDefault());
        }
    }
}
