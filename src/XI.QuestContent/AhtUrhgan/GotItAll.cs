// Got It All (Log ID 6, Quest ID 3), a line-for-line port of scripts/quests/ahtUrhgan/Got_It_All.lua (quest rework
// B3b Aht Urhgan, draft). Aht Urhgan Whitegate (no scripts): Tehf Kimasnahya !pos -89.9 0 6.2 (default 529), Ekhu
// Pesshyadha !pos -13 2 103.4 (default 532), Zabahf !pos -90.1 0 10.1 (default 530); trigger area 1 (57 -1 -70 ..
// 62 1 -65; the zone's own switch plays 526 there at the char var gotitallCS 5, and the zone's onEventFinish for
// 526 sets gotitallCS 6 and the char at 60 0 -71 too). Available (no other condition): Tehf Kimasnahya 520 (its
// finish begins it, Prog 1); Ekhu Pesshyadha 532, Zabahf 533 (Events). Accepted: Tehf Kimasnahya 521 (an Event) at
// Prog 0-3, 525 at Prog 4, 534 (an Event) at Prog 5, 527 at Prog 6, at Prog 7 528 when the char need not zone, else
// 539 (an Event); Ekhu Pesshyadha 522 at Prog 1, 536 (an Event) at Prog 2, 524 at Prog 3, 537 (an Event) from Prog
// 4; Zabahf 523 at Prog 2 or 3, 538 / 540 / 535 (Events) at Prog 5 / 6 / 7, else 533 (an Event); area 1 at Prog 5:
// 526. Finishes: 522 Prog 2; 523 Prog 3; 524 the Vial of Luminous Water and, when given, Prog 4; 525 option 0: Prog
// 5, the vial taken; 526 Prog 6; 527 Prog 7 and the zoning flag; 528 completes it (a Bibiki Seashell). The era
// module got_it_all replaces 527's finish (also Stage = JstMidnight()). Completed: Ekhu Pesshyadha 531 (an Event)
// while Three Men and a Closet is not completed, else nil. Kept as the Lua has it: 520 begins it on any option;
// Zabahf answers 533 at Prog 1 and 4; 523 at Prog 3 sets Prog 3 again; Tehf Kimasnahya returns nil from Prog 8
// (none reached).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class GotItAll : QuestScript
    {
        public GotItAll() : base(QuestLog.AhtUrghan, "GOT_IT_ALL") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Got_It_All";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.BibikiSeashell,
            });
            b.Steps(1, 2, 3, 4, 5, 6, 7);

            // Section: Begin quest
            b.State(QuestStatus.Available)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.TehfKimasnahya).OnTrigger(QuestReply.ProgressEvent(520))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.EkhuPesshyadha).OnTrigger(QuestReply.Event(532))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Zabahf).OnTrigger(QuestReply.Event(533))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 520, q =>
                {
                    q.Quest.Begin();
                    q.Quest.SetStep(1);
                });

            // Section: Questing (the era module got_it_all replaces 527 here: state 2)
            b.State(QuestStatus.Accepted)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.TehfKimasnahya).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress < 4)
                    {
                        return QuestReply.Event(521);
                    }
                    else if (progress == 4)
                    {
                        return QuestReply.ProgressEvent(525);
                    }
                    else if (progress == 5)
                    {
                        return QuestReply.Event(534);
                    }
                    else if (progress == 6)
                    {
                        return QuestReply.ProgressEvent(527);
                    }
                    else if (progress == 7)
                    {
                        if (!q.Player.NeedToZone)
                        {
                            return QuestReply.ProgressEvent(528);
                        }
                        else
                        {
                            return QuestReply.Event(539);
                        }
                    }
                    return null;
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.EkhuPesshyadha).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 1)
                    {
                        return QuestReply.ProgressEvent(522);
                    }
                    else if (progress == 2)
                    {
                        return QuestReply.Event(536);
                    }
                    else if (progress == 3)
                    {
                        return QuestReply.ProgressEvent(524);
                    }
                    else if (progress >= 4)
                    {
                        return QuestReply.Event(537);
                    }
                    return null;
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Zabahf).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 2 ||
                        progress == 3)
                    {
                        return QuestReply.ProgressEvent(523);
                    }
                    else if (progress == 5)
                    {
                        return QuestReply.Event(538);
                    }
                    else if (progress == 6)
                    {
                        return QuestReply.Event(540);
                    }
                    else if (progress == 7)
                    {
                        return QuestReply.Event(535);
                    }
                    else
                    {
                        return QuestReply.Event(533);
                    }
                })
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 1, q => q.Quest.Step == 5 ? QuestReply.ProgressEvent(526) : null)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 522, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 523, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 524, q =>
                {
                    if (q.Player.GiveKeyItem(KeyItem.VialOfLuminousWater))
                    {
                        q.Quest.SetStep(4);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 525, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetStep(5);
                        q.Player.DelKeyItem(KeyItem.VialOfLuminousWater);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 526, q =>
                {
                    q.Quest.SetStep(6);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 527, q =>
                {
                    q.Quest.SetStep(7);
                    q.Player.SetNeedToZone(true);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 528, q =>
                {
                    q.Quest.Complete();
                });

            // Section: After completion
            b.State(QuestStatus.Completed)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.EkhuPesshyadha).OnTrigger(q =>
                {
                    if (q.Player.QuestStatus(QuestLog.AhtUrghan, "THREE_MEN_AND_A_CLOSET") != QuestStatus.Completed)
                    {
                        return QuestReply.Event(531);
                    }
                    return null;
                });
        }
    }
}
