// Mom, the Adventurer? (Log ID 1, Quest ID 21), a line-for-line port of scripts/quests/bastok/Mom_the_Adventurer.lua
// (quest rework B3b). Nbu Latteh : !pos -114.777 -4 -113.301 235; Roh Latteh : !pos -11.823 6.999 -9.249 234. Nbu's
// 230 gives a Fire Crystal and begins it (not begun when the crystal does not fit). Accepted: Nbu's 231 until the
// letter; Parnika's 232; a Copper Ring traded to Roh plays 95 (the ring taken, the Letter from Roh Latteh). With the
// letter Nbu plays 233 (200 gil) or, once the letter has been seen, 234 (100 gil): completed (fame +10, Ringbearer),
// the letter deleted, the quest's mustZone set. Completed with the mustZone clear: the same 233 / 234 for a held
// letter; at Bastok fame level 1 with Prog 0 Nbu's 230 again (the crystal, Prog 1), Prog 1 his 231, and Roh takes a
// ring again at Prog 1, so the quest repeats at fame level 1 (the Lua says so). The Signpost Marks the Spot (C#) reads
// this quest's status and its mustZone and keeps both reads.
// Kept as the Lua file has it: repeatable at Bastok fame level 1, and a held letter completes it again whatever the
// fame; each completion gives the fame, the title and the gil again.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class MomTheAdventurer : QuestScript
    {
        public MomTheAdventurer() : base(QuestLog.Bastok, "MOM_THE_ADVENTURER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Mom_the_Adventurer";

        // handleEventFinish (233 and 234)
        static void HandleEventFinish(IQuestContext q)
        {
            int gilReward = q.Csid == 233 ? 200 : 100;

            if (q.Quest.Complete())
            {
                q.Player.DelKeyItem(KeyItem.LetterFromRohLatteh);
                q.Player.GiveCurrency("gil", gilReward);
                q.Quest.SetMustZone();
            }
        }

        // Nbu with the letter: 234 once it has been seen, else 233; null without it
        static QuestReply LetterEvent(IQuestContext q)
        {
            if (q.Player.HasKeyItem(KeyItem.LetterFromRohLatteh))
            {
                if (q.Player.SeenKeyItem(KeyItem.LetterFromRohLatteh))
                {
                    return QuestReply.ProgressEvent(234);
                }
                else
                {
                    return QuestReply.ProgressEvent(233);
                }
            }
            return null;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 10,
                FameArea = FameArea.Bastok,
                Title = Title.Ringbearer,
            });

            b.State(QuestStatus.Available)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.NbuLatteh).OnTrigger(QuestReply.ProgressEvent(230))
                .OnEventFinish(Zone.BastokMarkets, 230, q =>
                {
                    if (q.Player.GiveItem(Item.FireCrystal))
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.NbuLatteh).OnTrigger(q =>
                    LetterEvent(q) ?? QuestReply.Event(231))
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Parnika).OnTrigger(QuestReply.Event(232))
                .OnEventFinish(Zone.BastokMarkets, 233, HandleEventFinish)
                .OnEventFinish(Zone.BastokMarkets, 234, HandleEventFinish)
                .Npc(Zone.BastokMines, Npc.BastokMines.RohLatteh).OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.CopperRing, 1)) && !q.Player.HasKeyItem(KeyItem.LetterFromRohLatteh))
                    {
                        return QuestReply.ProgressEvent(95);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BastokMines, 95, q =>
                {
                    q.Trade.Complete();
                    q.Player.GiveKeyItem(KeyItem.LetterFromRohLatteh);
                });

            b.State(QuestStatus.Completed, when: q => !q.Quest.MustZone)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.NbuLatteh).OnTrigger(q =>
                {
                    // completion is allowed whatever the fame level
                    QuestReply letter = LetterEvent(q);
                    if (letter != null)
                    {
                        return letter;
                    }

                    // the repeat, at Bastok fame level 1
                    int questProgress = q.Quest.Step;
                    if (q.Player.FameLevel(FameArea.Bastok) == 1 && questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(230);
                    }
                    // now you are stuck
                    else if (questProgress == 1)
                    {
                        return QuestReply.Event(231);
                    }
                    return null;
                })
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Parnika).OnTrigger(QuestReply.Event(232))
                .OnEventFinish(Zone.BastokMarkets, 230, q =>
                {
                    if (q.Player.GiveItem(Item.FireCrystal))
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.BastokMarkets, 233, HandleEventFinish)
                .OnEventFinish(Zone.BastokMarkets, 234, HandleEventFinish)
                .Npc(Zone.BastokMines, Npc.BastokMines.RohLatteh).OnTrade(q =>
                {
                    if (q.Quest.Step == 1 &&
                        !q.Player.HasKeyItem(KeyItem.LetterFromRohLatteh) &&
                        q.Trade.Matches((Item.CopperRing, 1)))
                    {
                        return QuestReply.ProgressEvent(95);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BastokMines, 95, q =>
                {
                    q.Trade.Complete();
                    q.Player.GiveKeyItem(KeyItem.LetterFromRohLatteh);
                });
        }
    }
}
