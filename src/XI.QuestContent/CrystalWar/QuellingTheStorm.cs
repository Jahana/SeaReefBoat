// Quelling the Storm (Log ID 7, Quest ID 42), a line-for-line port of
// scripts/quests/crystalWar/WOTG_BAS_7_Quelling_the_Storm.lua (quest rework B3b Crystal War group 4). Bastok Markets
// [S]: Gentle Tiger !pos -203.9 -10 2.2 (default 109), Paul !pos -192.9 -4 53.1, Biggorf !pos -210.1 2 -140.9,
// Wilhelmina !pos -158.1 -4 -120.1, Blatherix !pos -309.8 -12 -42.8 (none of them a default). Vunkerl Inlet [S]: qm8
// !pos -356.3 -32.1 286 (no default).
// Available, Fire in the Hole completed and the current WotG mission Crossroads of Time or later: the zone-in to Bastok
// Markets [S] from North Gustaberg [S]: 179, whose finish begins it. Accepted, by Prog: 0: Gentle Tiger 180 (-> 1). 1:
// Gentle Tiger 186 (an Event); Paul 193 / Biggorf 194 / Wilhelmina 192 while his or her var is 0, whose finish sets it
// and, once all three are 1, Prog 2 (checkTownNpcProgress); 196 / 197 / 195 (Events) once set. 2: Paul 196, Biggorf
// 197, Wilhelmina 195 (Events); Gentle Tiger 181 (-> 3). 3: Gentle Tiger 187 (an Event); Blatherix 182 (-> 4). 4:
// Gentle Tiger 187; Blatherix 188 (an Event); a Goblin Mess Tin, a Goblin Mushpot and a Pinch of Twinkle Powder alone:
// 183, whose finish confirms the trade, Prog 5. 5: Blatherix 189 (an Event); Gentle Tiger 184 (-> 6). 6: Vunkerl Inlet
// [S] qm8 113 (-> 7); Gentle Tiger 190, Blatherix 189 (Events). 7: Gentle Tiger 191 (an Event); Blatherix 185, whose
// finish completes it (a Goblin Belt).
// Kept as the Lua has it: 179 begins on any option; the town NPCs' vars stay after the quest; 185 completes with no
// check of its result; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class QuellingTheStorm : QuestScript
    {
        public QuellingTheStorm() : base(QuestLog.CrystalWar, "QUELLING_THE_STORM") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_BAS_7_Quelling_the_Storm";

        static void CheckTownNpcProgress(IQuestContext q)
        {
            if (q.Quest.Var("Paul") == 1 &&
                q.Quest.Var("Biggorf") == 1 &&
                q.Quest.Var("Wilhelmina") == 1)
            {
                q.Quest.SetStep(2);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4, 5, 6, 7);

            b.Reward(new QuestReward
            {
                Item = Item.GoblinBelt,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.CrystalWar, "FIRE_IN_THE_HOLE") &&
                    q.Player.CurrentMission(MissionLog.Wotg) >= Mission.Wotg.CrossroadsOfTime)
                .OnZoneIn(Zone.BastokMarketsS, q =>
                {
                    if (q.Player.PreviousZone == Zone.NorthGustabergS)
                    {
                        return QuestReply.Event(179);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BastokMarketsS, 179, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, step: 0)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.ProgressEvent(180))
                .OnEventFinish(Zone.BastokMarketsS, 180, q =>
                {
                    q.Quest.SetStep(1);
                });

            b.State(QuestStatus.Accepted, step: 1)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(186))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Paul).OnTrigger(q =>
                {
                    if (q.Quest.Var("Paul") == 0)
                    {
                        return QuestReply.ProgressEvent(193);
                    }
                    else
                    {
                        return QuestReply.Event(196);
                    }
                })
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Biggorf).OnTrigger(q =>
                {
                    if (q.Quest.Var("Biggorf") == 0)
                    {
                        return QuestReply.ProgressEvent(194);
                    }
                    else
                    {
                        return QuestReply.Event(197);
                    }
                })
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Wilhelmina).OnTrigger(q =>
                {
                    if (q.Quest.Var("Wilhelmina") == 0)
                    {
                        return QuestReply.ProgressEvent(192);
                    }
                    else
                    {
                        return QuestReply.Event(195);
                    }
                })
                .OnEventFinish(Zone.BastokMarketsS, 192, q =>
                {
                    q.Quest.SetVar("Wilhelmina", 1);
                    CheckTownNpcProgress(q);
                })
                .OnEventFinish(Zone.BastokMarketsS, 193, q =>
                {
                    q.Quest.SetVar("Paul", 1);
                    CheckTownNpcProgress(q);
                })
                .OnEventFinish(Zone.BastokMarketsS, 194, q =>
                {
                    q.Quest.SetVar("Biggorf", 1);
                    CheckTownNpcProgress(q);
                });

            b.State(QuestStatus.Accepted, step: 2)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Paul).OnTrigger(QuestReply.Event(196))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Biggorf).OnTrigger(QuestReply.Event(197))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Wilhelmina).OnTrigger(QuestReply.Event(195))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.ProgressEvent(181))
                .OnEventFinish(Zone.BastokMarketsS, 181, q =>
                {
                    q.Quest.SetStep(3);
                });

            b.State(QuestStatus.Accepted, step: 3)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(187))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Blatherix).OnTrigger(QuestReply.ProgressEvent(182))
                .OnEventFinish(Zone.BastokMarketsS, 182, q =>
                {
                    q.Quest.SetStep(4);
                });

            b.State(QuestStatus.Accepted, step: 4)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(187))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Blatherix).OnTrigger(QuestReply.Event(188))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Blatherix).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(
                            Item.GoblinMessTin,
                            Item.GoblinMushpot,
                            Item.PinchOfTwinklePowder))
                    {
                        return QuestReply.ProgressEvent(183);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BastokMarketsS, 183, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetStep(5);
                });

            b.State(QuestStatus.Accepted, step: 5)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Blatherix).OnTrigger(QuestReply.Event(189))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.ProgressEvent(184))
                .OnEventFinish(Zone.BastokMarketsS, 184, q =>
                {
                    q.Quest.SetStep(6);
                });

            b.State(QuestStatus.Accepted, step: 6)
                .Npc(Zone.VunkerlInletS, Npc.VunkerlInletS.qm8).OnTrigger(QuestReply.ProgressEvent(113))
                .OnEventFinish(Zone.VunkerlInletS, 113, q =>
                {
                    q.Quest.SetStep(7);
                })
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(190))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Blatherix).OnTrigger(QuestReply.Event(189));

            b.State(QuestStatus.Accepted, step: 7)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(191))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Blatherix).OnTrigger(QuestReply.ProgressEvent(185))
                .OnEventFinish(Zone.BastokMarketsS, 185, q =>
                {
                    q.Quest.Complete();
                });
        }
    }
}
