// An Empty Vessel (Log ID 6, Quest ID 5), a line-for-line port of scripts/quests/ahtUrhgan/An_Empty_Vessel.lua (quest
// rework B3b Aht Urhgan, group 1). Aht Urhgan Whitegate: Waoud !pos 65 -6 -78 (no script; default 61). Aydeewa
// Subterrane: trigger area 1 (the cuboid 378 -3 338 .. 382 3 342; the zone's own handler does nothing). Available from
// main level ADVANCED_JOB_LEVEL (30): Waoud plays 60 (gil) when no mustZone is set and Timer <= the Vana'diel day, else
// 63. 60's updates: an option under 40 that is one of the ten right answers adds one to the local var numCorrect;
// option 40 shows the result: under ten, updateEvent(gil, 0, 0, 0, 0, 0, 0, the serpent of floor(n / 2) + 1); ten or
// more, Option = randomInt(1, 3) (the item asked for) and updateEvent(gil, 0, 0, 0, 0, 0, Option, 70). 60's finish
// clears numCorrect; option 50 begins it (mustZone, Timer = day + 1); option 45 with 1000 gil sets the mustZone and
// Timer and takes the 1000 gil (PAY_DIVINATION). Accepted: Waoud's trade at Prog 1 of the item asked for, alone, plays
// 67 ({ [6] = Option }; Prog 2, the item kept); his trigger, with no mustZone and Timer <= day: Prog 0 65 (Prog 1),
// Prog 1 66 ({ [6] = Option }), Prog 2 68, another Prog nil; else 64. Aydeewa's area 1 at Prog 2 with the item: 3 (the
// item); option 13 completes it (Bearer of the Mark of Zahak; BLU unlocked, the Mark of Zahak and the BLU gesture added
// with no message, completeEvent 1 after the cleanup), then setPos Whitegate 148 -2 0; option 7 (Yasfel refused) clears
// Prog, Timer and Option, deletes the quest and setPos Whitegate -17.5 -6 39.6. Completed: Waoud 69 while completeEvent
// is 1 (option 1 clears it), else 78 (gil) under the same wait as 60, else 63; 78's update option 40: updateEvent(gil,
// 0, 0, 0, 0, 0, 0, randomInt(1, 5) * 10); its finish option 1 with 1000 gil: mustZone, Timer, the 1000 gil.
// Kept as the Lua has it: a right answer given twice counts twice; 69 spends no divination; 3's setPos runs whether the
// completion worked or not; option 7 leaves the mustZone; requiredItemList[0] (Option unset at Prog 1 or 2, a GM's
// only) is nil: the C# reads item 0, which no trade or bag holds.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class AnEmptyVessel : QuestScript
    {
        public AnEmptyVessel() : base(QuestLog.AhtUrghan, "AN_EMPTY_VESSEL") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/An_Empty_Vessel";

        // requiredItemList, the Lua's [1] .. [3]
        static readonly int[] RequiredItemList = { Item.SirensTear, Item.PinchOfValkurmSunsand, Item.DangrufStone };

        // requiredItemList[option]: nil (0 here) outside 1 .. 3
        static int RequiredItem(int option) =>
            option >= 1 && option <= RequiredItemList.Length ? RequiredItemList[option - 1] : 0;

        static readonly int[] CorrectOptions = { 2, 6, 9, 12, 13, 18, 21, 24, 26, 30 };

        static readonly int[] SuccessParams = { 10, 20, 30, 40, 60 }; // Spring, Stone, Gale, Flame, Sky

        static bool WaitOver(IQuestContext q) =>
            !q.Quest.MustZone && q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay;

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Title = Title.BearerOfTheMarkOfZahak });
            b.Steps(1, 2);

            b.State(QuestStatus.Available, when: q => q.Player.MainLevel >= q.Settings.MainNumber("ADVANCED_JOB_LEVEL"))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Waoud).OnTrigger(q =>
                {
                    if (WaitOver(q))
                    {
                        return QuestReply.ProgressEvent(60, q.Player.Gil);
                    }
                    else
                    {
                        return QuestReply.Event(63);
                    }
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 60, q =>
                {
                    int numCorrect = q.Quest.LocalVar("numCorrect");

                    // Search Correct Option list for provided option, and update answers.
                    if (q.Option < 40)
                    {
                        foreach (int correctOption in CorrectOptions)
                        {
                            if (q.Option == correctOption)
                            {
                                q.Quest.SetLocalVar("numCorrect", numCorrect + 1);
                                break;
                            }
                        }
                    }
                    // Display test results.
                    else if (q.Option == 40)
                    {
                        if (numCorrect < 10)
                        {
                            q.Player.UpdateEvent(q.Player.Gil, 0, 0, 0, 0, 0, 0, SuccessParams[numCorrect / 2]);
                        }
                        else
                        {
                            int requiredItem = q.Player.RandomInt(1, 3);

                            q.Quest.SetVar("Option", requiredItem);
                            q.Player.UpdateEvent(q.Player.Gil, 0, 0, 0, 0, 0, requiredItem, 70);
                        }
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 60, q =>
                {
                    q.Quest.SetLocalVar("numCorrect", 0);

                    if (q.Option == 50)
                    {
                        q.Quest.Begin();
                        q.Quest.SetMustZone();
                        q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay + 1);
                    }
                    // Divination completed without an offer.
                    else if (q.Option == 45 &&
                        q.Player.Gil >= 1000)
                    {
                        q.Quest.SetMustZone();
                        q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay + 1);

                        q.Player.DelGil(1000);
                        q.Text.Special(Text.AhtUrhganWhitegate.PAY_DIVINATION);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Waoud)
                    .OnTrade(q =>
                    {
                        int requiredItem = q.Quest.Var("Option");

                        // The item is not taken. Yasfel asks for it again in Aydeewa.
                        if (q.Quest.Step == 1 &&
                            q.Trade.Matches((RequiredItem(requiredItem), 1)))
                        {
                            return QuestReply.ProgressEvent(67).Option(6, requiredItem);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int questProgress = q.Quest.Step;

                        if (WaitOver(q))
                        {
                            if (questProgress == 0)
                            {
                                return QuestReply.ProgressEvent(65);
                            }
                            else if (questProgress == 1)
                            {
                                return QuestReply.Event(66).Option(6, q.Quest.Var("Option"));
                            }
                            else if (questProgress == 2)
                            {
                                return QuestReply.Event(68);
                            }
                            return null;
                        }
                        else
                        {
                            return QuestReply.Event(64);
                        }
                    })
                // Event 65 has no menu. The hint follows on the next conversation.
                .OnEventFinish(Zone.AhtUrhganWhitegate, 65, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 67, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnTriggerAreaEnter(Zone.AydeewaSubterrane, 1, q =>
                {
                    int requiredItemId = RequiredItem(q.Quest.Var("Option"));

                    if (q.Quest.Step == 2 &&
                        q.Player.HasItem(requiredItemId))
                    {
                        return QuestReply.ProgressEvent(3, requiredItemId);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AydeewaSubterrane, 3, q =>
                {
                    if (q.Option == 13)
                    {
                        if (q.Quest.Complete())
                        {
                            // Note: Messages for receiving the below items are handled by the event.
                            q.Player.UnlockJob(Job.Blu);
                            q.Player.AddKeyItem(KeyItem.MarkOfZahak);
                            q.Player.AddKeyItem(KeyItem.JobGestureBlueMage);

                            // quest:complete() wipes quest vars.
                            q.Quest.SetVar("completeEvent", 1);
                        }

                        q.Player.SetPos(148, -2, 0, 128, Zone.AhtUrhganWhitegate);
                    }
                    // Player refused Yasfel.
                    else if (q.Option == 7)
                    {
                        q.Quest.SetStep(0);
                        q.Quest.SetVar("Timer", 0);
                        q.Quest.SetVar("Option", 0);

                        q.Player.DelQuest(QuestLog.AhtUrghan, "AN_EMPTY_VESSEL");
                        q.Player.SetPos(-17.5, -6, 39.6, 171, Zone.AhtUrhganWhitegate);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Waoud).OnTrigger(q =>
                {
                    if (q.Quest.Var("completeEvent") == 1)
                    {
                        return QuestReply.ProgressEvent(69);
                    }
                    else if (WaitOver(q))
                    {
                        return QuestReply.Event(78, q.Player.Gil);
                    }
                    else
                    {
                        return QuestReply.Event(63);
                    }
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 78, q =>
                {
                    if (q.Option == 40)
                    {
                        q.Player.UpdateEvent(q.Player.Gil, 0, 0, 0, 0, 0, 0, q.Player.RandomInt(1, 5) * 10);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 69, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.SetVar("completeEvent", 0);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 78, q =>
                {
                    if (q.Option == 1 && q.Player.Gil >= 1000)
                    {
                        q.Quest.SetMustZone();
                        q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay + 1);

                        q.Player.DelGil(1000);
                        q.Text.Special(Text.AhtUrhganWhitegate.PAY_DIVINATION);
                    }
                });
        }
    }
}
