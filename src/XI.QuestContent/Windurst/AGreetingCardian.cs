// A Greeting Cardian (Log ID 2, Quest ID 43), a line-for-line port of scripts/quests/windurst/A_Greeting_Cardian.lua
// (quest rework B3b). Kororo : !pos -11.883 -2.750 5.508 241; Spare One : !pos -8.201 -2.750 10.018 241; Boizo-Naizo :
// !pos -9.581 -2.750 -26.062 241; Five of Diamonds : !pos -220.954 0.999 -122.708 239; Five of Hearts : !pos 126.369
// -2.002 45.531 238; Five of Clubs : !pos 97.481 -4.000 151.498 240; Five of Spades : !pos -478.100 -32.120 47.394 118.
// Only Spare One offers the greeting menu. Event 295 arms the quest. The log stays empty until Kororo's offer is
// accepted. He offers it after a zone change. Event 296 is a nudge, not the offer. The greeting chosen on 295 is
// replayed later. Available once The All-New C-2000 is completed, at Windurst fame level 3: Spare One's 295 (options 1-3
// teach a greeting: Greeting, Wait = now + 60, the quest's mustZone), then 495 (0, the greeting); Kororo's 296 while no
// greeting is taught, then (mustZone clear, the wait over) 298, option 1 begins it. Accepted: Five of Spades (Buburimu
// Peninsula) 1 (118, the greeting) at Prog 0 (Prog 1), then the greeting line FIVEOFSPADES_GREETING_OFFSET + greeting
// (messageName); Five of Clubs 448, Five of Diamonds 339, Five of Hearts 686, Boizo-Naizo 302; Kororo 299, at Prog 1
// 303: completed (fame 30, the Tourmaline Earring; needToZone). Completed: Boizo-Naizo 307, Kororo 304. The era module
// era_quest_a_greeting_cardian (Modules/Era/Windurst/AGreetingCardian.cs) sets Wait to JST midnight after 295.
// Kept as the Lua file has it: 295's finish teaches nothing on an escape (option 0 or above 3); Kororo answers nil while
// the mustZone or the wait holds.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class AGreetingCardian : QuestScript
    {
        public AGreetingCardian() : base(QuestLog.Windurst, "A_GREETING_CARDIAN") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/A_Greeting_Cardian";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 30, FameArea = FameArea.Windurst, Item = Item.TourmalineEarring });

            // Section: Quest is available.
            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Windurst, "THE_ALL_NEW_C_2000") &&
                    q.Player.FameLevel(FameArea.Windurst) >= 3)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Kororo).OnTrigger(q =>
                {
                    // Points at Spare One until a greeting is taught.
                    if (q.Quest.Var("Greeting") == 0)
                    {
                        return QuestReply.Event(296);
                    }

                    // The offer needs the zone change and the timer. Kororo gives his default line until then.
                    if (
                        !q.Quest.MustZone &&
                        q.Player.SystemTime >= q.Quest.Var("Wait"))
                    {
                        return QuestReply.ProgressEvent(298);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.SpareOne).OnTrigger(q =>
                {
                    int greeting = q.Quest.Var("Greeting");

                    // Spare One repeats the greeting he was taught.
                    if (greeting != 0)
                    {
                        return QuestReply.Event(495, 0, greeting);
                    }

                    return QuestReply.ProgressEvent(295);
                })
                .OnEventFinish(Zone.WindurstWoods, 295, q =>
                {
                    // The menu offers three greetings. Escaping the cutscene teaches nothing.
                    if (q.Option < 1 || q.Option > 3)
                    {
                        return;
                    }

                    q.Quest.SetVar("Greeting", q.Option);
                    q.Quest.SetVar("Wait", q.Player.SystemTime + 60); // 1 minute wait time
                    q.Quest.SetMustZone();
                })
                .OnEventFinish(Zone.WindurstWoods, 298, q =>
                {
                    // Declining changes nothing. The offer can be taken again.
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.BuburimuPeninsula, Npc.BuburimuPeninsula.FiveofSpades).OnTrigger(q =>
                {
                    int greeting = q.Quest.Var("Greeting");

                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(1, Zone.BuburimuPeninsula, greeting);
                    }

                    // Five of Spades keeps the taught greeting until Kororo corrects him.
                    // quest:messageName(buburimuID.text.FIVEOFSPADES_GREETING_OFFSET + greeting): a text name plus an offset, by
                    // text id (QuestReply.MessageName(int), quest rework P5)
                    return QuestReply.MessageName(q.Text.Id(Text.BuburimuPeninsula.FIVEOFSPADES_GREETING_OFFSET) + greeting);
                })
                .OnEventFinish(Zone.BuburimuPeninsula, 1, q => q.Quest.SetStep(1))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.FiveofClubs).OnTrigger(QuestReply.Event(448))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.FiveofDiamonds).OnTrigger(QuestReply.Event(339))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.FiveofHearts).OnTrigger(QuestReply.Event(686))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.BoizoNaizo).OnTrigger(QuestReply.Event(302))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Kororo).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(303);
                    }

                    return QuestReply.Event(299);
                })
                .OnEventFinish(Zone.WindurstWoods, 303, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetNeedToZone(true);
                    }
                });

            // Section: Quest completed.
            b.State(QuestStatus.Completed)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.BoizoNaizo).OnTrigger(QuestReply.Event(307))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Kororo).OnTrigger(QuestReply.Event(304));
        }
    }
}
