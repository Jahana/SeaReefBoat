// The Tenshodo Showdown (Log ID 2, Quest ID 69), a line-for-line port of
// scripts/quests/windurst/THF_AF1_The_Tenshodo_Showdown.lua (quest rework B3b). Nanaa Mihgo : !pos 62 -4 240 241;
// Harnek : !pos 44 0 -19 245; Elfriede : !pos 61 -15 10 248. A THF at AF1_QUEST_LEVEL: Nanaa Mihgo's 496 begins it (the
// Letter from the Tenshodo). Harnek (Lower Jeuno) with the letter: 10021 (the letter taken, the Tenshodo Envelope,
// Prog 1). Elfriede (Selbina): 10002 at Prog 1 (Prog 2), 10003 at Prog 2; Quadav Stew traded alone at Prog 2: 10004
// (taken, the envelope taken, the Signed Envelope, Prog 3). Harnek with the signed envelope: 10022 completes it (fame
// 20, the Marauder's Knife; the signed envelope taken). Nanaa Mihgo's reminders 497 (Prog 0), 498 (Prog 1). Completed,
// while As Thick as Thieves is available to a THF below AF2_QUEST_LEVEL: Nanaa Mihgo's 503 (replaceDefault). The Lua
// file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class TheTenshodoShowdown : QuestScript
    {
        public TheTenshodoShowdown() : base(QuestLog.Windurst, "THE_TENSHODO_SHOWDOWN") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/THF_AF1_The_Tenshodo_Showdown";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Windurst, Item = Item.MaraudersKnife });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainJob == Job.Thf &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF1_QUEST_LEVEL"))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.NanaaMihgo).OnTrigger(QuestReply.ProgressEvent(496))
                .OnEventFinish(Zone.WindurstWoods, 496, q =>
                {
                    q.Quest.Begin();
                    q.Player.GiveKeyItem(KeyItem.LetterFromTheTenshodo);
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Harnek).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.LetterFromTheTenshodo))
                    {
                        return QuestReply.ProgressEvent(10021, 0, KeyItem.LetterFromTheTenshodo, KeyItem.TenshodoEnvelope);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.SignedEnvelope))
                    {
                        return QuestReply.ProgressEvent(10022);
                    }

                    return null;
                })
                .OnEventFinish(Zone.LowerJeuno, 10021, q =>
                {
                    q.Player.DelKeyItem(KeyItem.LetterFromTheTenshodo);
                    q.Player.GiveKeyItem(KeyItem.TenshodoEnvelope);
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.LowerJeuno, 10022, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.SignedEnvelope);
                    }
                })
                .Npc(Zone.Selbina, Npc.Selbina.Elfriede)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.BowlOfQuadavStew) &&
                            q.Quest.Step == 2)
                        {
                            return QuestReply.ProgressEvent(10004, 0, KeyItem.TenshodoEnvelope, Item.BowlOfQuadavStew);
                        }

                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int questProgress = q.Quest.Step;

                        if (questProgress == 1)
                        {
                            return QuestReply.ProgressEvent(10002, 0, KeyItem.TenshodoEnvelope, Item.BowlOfQuadavStew);
                        }
                        else if (questProgress == 2)
                        {
                            return QuestReply.ProgressEvent(10003, 0, 0, Item.BowlOfQuadavStew);
                        }

                        return null;
                    })
                .OnEventFinish(Zone.Selbina, 10002, q => q.Quest.SetStep(2))
                .OnEventFinish(Zone.Selbina, 10004, q =>
                {
                    q.Trade.Confirm();
                    q.Player.DelKeyItem(KeyItem.TenshodoEnvelope);
                    q.Player.GiveKeyItem(KeyItem.SignedEnvelope);
                    q.Quest.SetStep(3);
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.NanaaMihgo).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(497);
                    }
                    else if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(498);
                    }

                    return null;
                });

            b.State(QuestStatus.Completed, when: q =>
                    q.Player.QuestStatus(QuestLog.Windurst, "AS_THICK_AS_THIEVES") == QuestStatus.Available &&
                    q.Player.MainJob == Job.Thf &&
                    q.Player.MainLevel < q.Settings.MainNumber("AF2_QUEST_LEVEL"))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.NanaaMihgo).OnTrigger(QuestReply.Event(503).ReplaceDefault());
        }
    }
}
