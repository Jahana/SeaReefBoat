// Promotion: First Lieutenant (Log ID 6, Quest ID 98), a line-for-line port of
// scripts/quests/ahtUrhgan/Promotion_First_Lieutenant.lua (quest rework B3b Aht Urhgan, group 4). Aht Urhgan Whitegate:
// Abquhbah !pos 35.5 -6.6 -58 (his script: 255), trigger area 12 (25 -7 -127 .. 30 -5 -123; also The Prankster's).
// Vars: Prog (the game), Wait (the day of the last game; 0 before the tuition), Stage (1 after a failed try).
// Available with AssaultPromotion >= 25 and Promotion: Second Lieutenant completed: area 12: 5080 (text_table 0), whose
// finish begins it. Prog 0, Wait 0: Abquhbah 5087 (an Event, once per zone); a trade of at least five Imperial Gold
// Pieces: 5081, whose finish takes them (confirmTrade) and sets Wait to today. Prog 0, Wait > 0: Abquhbah: Wait before
// today: 5082 ([0] = 1 after a failed try), whose finish at option 1 sets Prog 1 and Stage 0, else Stage 1, and Wait
// today; else 5088 (an Event). Prog 1: Wait <= today: 5083 (option 1: Prog 2), else 5088 ([0] = 1). Prog 2: Wait <=
// today: 5084 (option 0: Prog 3), else 5088. Prog 3: 5089 ([0] = 1 when Wait is before today; importantOnce), whose
// finish between 15:00 and 16:59 sets Prog 4 and needToZone. Prog 4: between 15:00 and 16:59 with no needToZone: 5085,
// whose finish completes it (the FL Wildcat Badge, First Lieutenant), takes the SL Wildcat Badge, says FIRST_LIEUTENANT
// and sets AssaultPromotion 0; else 5089 ([0] = 1, an Event).
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): 5085's FIRST_LIEUTENANT is sent; the Lua's
// was a quest:messageSpecial action, built in a finish and dropped (no message).
// Kept as the Lua has it: 5080 begins it on any option; the second and third games play the same day as the one before
// (Wait <= today), the first only a day after the tuition (Wait < today); 5084 passes at option 0 (the others at 1);
// 5085's completion result unread.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class PromotionFirstLieutenant : QuestScript
    {
        public PromotionFirstLieutenant() : base(QuestLog.AhtUrghan, "PROMOTION_FIRST_LIEUTENANT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Promotion_First_Lieutenant";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.FlWildcatBadge,
                Title = Title.FirstLieutenant,
            });
            b.Steps(1, 2, 3, 4);

            // Trigger to start the quest
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CharVar("AssaultPromotion") >= 25 &&
                    q.Player.HasCompletedQuest(QuestLog.AhtUrghan, "PROMOTION_SECOND_LIEUTENANT"))
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 12, q => QuestReply.ProgressEvent(5080).TextTable(0))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5080, q => q.Quest.Begin());

            // Trigger for a reminder; trade the coins for the academy tuition
            b.State(QuestStatus.Accepted, step: 0, when: q => q.Quest.Var("Wait") == 0)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Abquhbah)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasQty((Item.ImperialGoldPiece, 5)))
                        {
                            return QuestReply.ProgressEvent(5081).TextTable(0);
                        }
                        return null;
                    })
                    .OnTrigger(q => QuestReply.Event(5087).TextTable(0).OncePerZone())
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5081, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetVar("Wait", q.Player.VanadielUniqueDay);
                });

            // Minigame Counterespionage
            b.State(QuestStatus.Accepted, step: 0, when: q => q.Quest.Var("Wait") > 0)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Abquhbah).OnTrigger(q =>
                {
                    int wait = q.Quest.Var("Wait");
                    int redo = q.Quest.Var("Stage") == 0 ? 0 : 1;

                    if (wait < q.Player.VanadielUniqueDay)
                    {
                        return QuestReply.ProgressEvent(5082).Option(0, redo).TextTable(0);
                    }
                    else
                    {
                        return QuestReply.Event(5088).TextTable(0);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5082, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.SetStep(1);
                        q.Quest.SetVar("Stage", 0);
                    }
                    else
                    {
                        q.Quest.SetVar("Stage", 1);
                    }

                    q.Quest.SetVar("Wait", q.Player.VanadielUniqueDay);
                });

            // Minigame Cipher
            b.State(QuestStatus.Accepted, step: 1)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Abquhbah).OnTrigger(q =>
                {
                    int wait = q.Quest.Var("Wait");
                    int redo = q.Quest.Var("Stage") == 0 ? 0 : 1;

                    if (wait <= q.Player.VanadielUniqueDay)
                    {
                        return QuestReply.ProgressEvent(5083).Option(0, redo).TextTable(0);
                    }
                    else
                    {
                        return QuestReply.Event(5088).Option(0, 1).TextTable(0);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5083, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.SetStep(2);
                        q.Quest.SetVar("Stage", 0);
                    }
                    else
                    {
                        q.Quest.SetVar("Stage", 1);
                    }

                    q.Quest.SetVar("Wait", q.Player.VanadielUniqueDay);
                });

            // Minigame rock, paper, scissors
            b.State(QuestStatus.Accepted, step: 2)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Abquhbah).OnTrigger(q =>
                {
                    int wait = q.Quest.Var("Wait");
                    int redo = q.Quest.Var("Stage") == 0 ? 0 : 1;

                    if (wait <= q.Player.VanadielUniqueDay)
                    {
                        return QuestReply.ProgressEvent(5084).Option(0, redo).TextTable(0);
                    }
                    else
                    {
                        return QuestReply.Event(5088).TextTable(0);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5084, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetStep(3);
                        q.Quest.SetVar("Stage", 0);
                    }
                    else
                    {
                        q.Quest.SetVar("Stage", 1);
                    }

                    q.Quest.SetVar("Wait", q.Player.VanadielUniqueDay);
                });

            // Graduation setup
            b.State(QuestStatus.Accepted, step: 3)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Abquhbah).OnTrigger(q =>
                {
                    int wait = q.Quest.Var("Wait") < q.Player.VanadielUniqueDay ? 1 : 0;

                    return QuestReply.Event(5089).Option(0, wait).TextTable(0).ImportantOnce();
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5089, q =>
                {
                    int gameHour = q.Player.VanadielHour;
                    if (gameHour >= 15 && gameHour < 17)
                    {
                        q.Quest.SetStep(4);
                        q.Player.SetNeedToZone(true);
                    }
                });

            // Graduation setup after a zone change
            b.State(QuestStatus.Accepted, step: 4)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Abquhbah).OnTrigger(q =>
                {
                    int gameHour = q.Player.VanadielHour;

                    if (gameHour >= 15 && gameHour < 17 && !q.Player.NeedToZone)
                    {
                        return QuestReply.ProgressEvent(5085).TextTable(0);
                    }
                    else
                    {
                        return QuestReply.Event(5089).Option(0, 1).TextTable(0);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5085, q =>
                {
                    q.Quest.Complete();
                    q.Player.DelKeyItem(KeyItem.SlWildcatBadge);
                    // the Lua's quest:messageSpecial: built and dropped (fixed, user ruling 2026-11-04)
                    q.Text.Special(Text.AhtUrhganWhitegate.FIRST_LIEUTENANT);
                    q.Player.SetCharVar("AssaultPromotion", 0);
                });
        }
    }
}
