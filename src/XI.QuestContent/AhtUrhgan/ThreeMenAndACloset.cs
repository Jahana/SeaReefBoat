// Three Men and a Closet (Log ID 6, Quest ID 31), a line-for-line port of
// scripts/quests/ahtUrhgan/Three_Men_and_a_Closet.lua (quest rework B3b Aht Urhgan, draft). Aht Urhgan Whitegate
// (no scripts): Kubhe Ijyuhla !pos 23.3 0 21.5 (default 662), Ratihb !pos 75.2 -6 -137.2 (default 603), Tehf
// Kimasnahya !pos -89.9 0 6.2 (default 529). Wajaom Woodlands: its zone-in. Available with Got It All completed:
// Kubhe Ijyuhla 836, whose finish begins it. Accepted: Kubhe Ijyuhla 837 / 838 / 839 / 842 / 845 at Prog 0 / 1 / 2
// / 3 / 4 (838, 845 progress events, the others Events); Tehf Kimasnahya 843 at Prog 3, 844 (an Event) at Prog 4;
// Ratihb 840 at Prog 2, 841 (an Event) at Prog 3; a Wajaom Woodlands zone-in from Whitegate at Prog 0: 510 (Prog
// 1). Finishes: 838 Prog 2; 840 Prog 3; 843 option 1: Prog 4; 845 completes it (an Imperial Bronze Piece).
// Completed: Kubhe Ijyuhla 846, Tehf Kimasnahya 844 (Events). Kept as the Lua has it: 836 begins it on any option;
// Kubhe Ijyuhla, Tehf Kimasnahya and Ratihb return nil outside their Progs; the zone-in returns the bare csid 510.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class ThreeMenAndACloset : QuestScript
    {
        public ThreeMenAndACloset() : base(QuestLog.AhtUrghan, "THREE_MEN_AND_A_CLOSET") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Three_Men_and_a_Closet";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.ImperialBronzePiece,
            });
            b.Steps(1, 2, 3, 4);

            // Section: Begin quest
            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.AhtUrghan, "GOT_IT_ALL") == QuestStatus.Completed)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.KubheIjyuhla).OnTrigger(q => QuestReply.ProgressEvent(836))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 836, q =>
                {
                    q.Quest.Begin();
                });

            // Section: Questing
            b.State(QuestStatus.Accepted)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.KubheIjyuhla).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 0)
                    {
                        return QuestReply.Event(837);
                    }
                    else if (progress == 1)
                    {
                        return QuestReply.ProgressEvent(838);
                    }
                    else if (progress == 2)
                    {
                        return QuestReply.Event(839);
                    }
                    else if (progress == 3)
                    {
                        return QuestReply.Event(842);
                    }
                    else if (progress == 4)
                    {
                        return QuestReply.ProgressEvent(845);
                    }
                    return null;
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.TehfKimasnahya).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 3)
                    {
                        return QuestReply.ProgressEvent(843);
                    }
                    else if (progress == 4)
                    {
                        return QuestReply.Event(844);
                    }
                    return null;
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Ratihb).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 2)
                    {
                        return QuestReply.ProgressEvent(840);
                    }
                    else if (progress == 3)
                    {
                        return QuestReply.Event(841);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 838, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 840, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 843, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.SetStep(4);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 845, q =>
                {
                    q.Quest.Complete();
                })
                // prevZone is player:getPreviousZone() (the same value: both are CharData.PrevZone)
                .OnZoneIn(Zone.WajaomWoodlands, q =>
                    q.Player.PreviousZone == Zone.AhtUrhganWhitegate && q.Quest.Step == 0 ? QuestReply.Event(510) : null)
                .OnEventFinish(Zone.WajaomWoodlands, 510, q =>
                {
                    q.Quest.SetStep(1);
                });

            // Section: Completed quest
            b.State(QuestStatus.Completed)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.KubheIjyuhla).OnTrigger(QuestReply.Event(846))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.TehfKimasnahya).OnTrigger(QuestReply.Event(844));
        }
    }
}
