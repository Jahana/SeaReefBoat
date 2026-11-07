// Chocobo's Wounds (Log ID 3, Quest ID 4), a line-for-line port of scripts/quests/jeuno/Chocobos_Wounds.lua (quest
// rework B3b). Brutus : !pos -55 8 95 244; Chocobo : !pos -61.42 8.2 93 244; Osker : !pos -61.42 8.2 94.2 244;
// _6t2 : !pos -88.2 -7.65 -168.8 245. From level 20 Brutus offers 71 (its argument 1, or 0 once declined: Declined);
// option 1 begins it at Prog 1. Accepted: the chocobo eats Gausebit Wildgrass six times (Prog 1-6: events 57, 58, 99,
// 59, 60, 64), each feeding starting a 45-second Timer (73 while it runs); from Prog 3 the grass must be traded alone
// (tradeHasExactly) and is taken; 64 completes it (the Chocobo License key item, Chocobo Trainer, fame +7 in San
// d'Oria, Bastok and Windurst). Gysahl Greens play 76, any other trade noAction. Brutus 65 / 66 / 102, Osker by Prog,
// the chocobo 62 / 63, the door _6t2 63 from Prog 4. Completed (until Save My Son is): Brutus 22, the chocobo 61,
// Osker 53.
// Lua: at Prog 0 (only a GM's accepted state) eventTable[questProgress] is nil, which is no error at the index:
// progressEvent(nil) / event(nil) builds an Event action with a nil id, and only Event:perform's startEvent(nil)
// errors, when that action wins the lookup (the chocobo's trade 57-64 is a progress action and always wins; Osker's
// event wins because his script acts only for A New Dawn). A Lua-bug arm, not recorded; the C# lookups throw at the
// index instead (KeyNotFound), see the branch table.
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class ChocobosWounds : QuestScript
    {
        public ChocobosWounds() : base(QuestLog.Jeuno, "CHOCOBOS_WOUNDS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Chocobos_Wounds";

        // the chocobo's feeding events, by Prog
        static readonly Dictionary<int, int> FeedEvents = new Dictionary<int, int>
        {
            [1] = 57,
            [2] = 58,
            [3] = 99,
            [4] = 59,
            [5] = 60,
            [6] = 64,
        };

        // Osker's events, by Prog
        static readonly Dictionary<int, int> OskerEvents = new Dictionary<int, int>
        {
            [1] = 103,
            [2] = 51,
            [3] = 52,
            [4] = 49,
            [5] = 46,
            [6] = 47,
        };

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { KeyItem = KeyItem.ChocoboLicense, Title = Title.ChocoboTrainer });
            b.Steps(1, 2, 3, 4, 5, 6);

            b.State(QuestStatus.Available, when: q => q.Player.MainLevel >= 20)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno._6t2).OnTrigger(QuestReply.Event(64))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Brutus).OnTrigger(q =>
                {
                    int declinedOption = q.Quest.Var("Declined") == 1 ? 0 : 1;
                    return QuestReply.ProgressEvent(71, declinedOption);
                })
                .OnEventFinish(Zone.UpperJeuno, 71, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Quest.SetStep(1);
                    }
                    // Dialogue changes if the player fails to choose the correct option.
                    else
                    {
                        q.Quest.SetVar("Declined", 1);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno._6t2).OnTrigger(q =>
                {
                    if (q.Quest.Step > 3)
                    {
                        return QuestReply.Event(63);
                    }
                    return null;
                })
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Brutus).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 1)
                    {
                        return QuestReply.Event(65);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.Event(66);
                    }
                    else
                    {
                        return QuestReply.Event(102);
                    }
                })
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Chocobo)
                    .OnTrade(q =>
                    {
                        if (q.Trade.ItemQty(Item.BunchOfGysahlGreens) > 0)
                        {
                            return QuestReply.Event(76);
                        }

                        if (q.Trade.ItemQty(Item.ClumpOfGausebitWildgrass) == 0)
                        {
                            return QuestReply.NoAction();
                        }

                        if (q.Quest.Var("Timer") > q.Player.SystemTime)
                        {
                            return QuestReply.Event(73);
                        }

                        int questProgress = q.Quest.Step;
                        if (questProgress <= 2)
                        {
                            return QuestReply.ProgressEvent(FeedEvents[questProgress]);
                        }

                        if (q.Trade.HasExactly(Item.ClumpOfGausebitWildgrass))
                        {
                            return QuestReply.ProgressEvent(FeedEvents[questProgress]);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Step <= 3)
                        {
                            return QuestReply.Event(62);
                        }
                        else
                        {
                            return QuestReply.Event(63);
                        }
                    })
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Osker).OnTrigger(q => QuestReply.Event(OskerEvents[q.Quest.Step]))
                .OnEventFinish(Zone.UpperJeuno, 57, q =>
                {
                    q.Quest.SetStep(2);
                    q.Quest.SetVar("Timer", q.Player.SystemTime + 45);
                })
                .OnEventFinish(Zone.UpperJeuno, 58, q =>
                {
                    q.Quest.SetStep(3);
                    q.Quest.SetVar("Timer", q.Player.SystemTime + 45);
                })
                .OnEventFinish(Zone.UpperJeuno, 59, q =>
                {
                    q.Quest.SetStep(5);
                    q.Quest.SetVar("Timer", q.Player.SystemTime + 45);
                    q.Trade.Confirm();
                })
                .OnEventFinish(Zone.UpperJeuno, 60, q =>
                {
                    q.Quest.SetStep(6);
                    q.Quest.SetVar("Timer", q.Player.SystemTime + 45);
                    q.Trade.Confirm();
                })
                .OnEventFinish(Zone.UpperJeuno, 64, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 7);
                        q.Player.AddFame(FameArea.Bastok, 7);
                        q.Player.AddFame(FameArea.Windurst, 7);
                        q.Trade.Confirm();
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 99, q =>
                {
                    q.Quest.SetStep(4);
                    q.Quest.SetVar("Timer", q.Player.SystemTime + 45);
                    q.Trade.Confirm();
                });

            b.State(QuestStatus.Completed, when: q => !q.Player.HasCompletedQuest(QuestLog.Jeuno, "SAVE_MY_SON"))
                // Always used except for importantOnce() for Chocobo on the Loose (10094)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Brutus).OnTrigger(QuestReply.Event(22))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Chocobo).OnTrigger(QuestReply.Event(61))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Osker).OnTrigger(QuestReply.Event(53));
        }
    }
}
