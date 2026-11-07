// Storm on the Horizon (Log ID 7, Quest ID 35), a line-for-line port of
// scripts/quests/crystalWar/WOTG_BAS_5_Storm_on_the_Horizon.lua (quest rework B3b Crystal War group 4). Bastok Markets
// [S]: Blatherix !pos -310 -12 -43 (no default), Gentle Tiger !pos -203 -9 0 (default 109). North Gustaberg [S]:
// Monument !pos 300 -63 497 (no default). Grauberg [S]: qm5 !pos 260 33 515 (no default).
// Available, Burden of Suspicion completed (the Prog var is set while the quest is still available): Blatherix: Prog 0:
// 37, whose finish gives an Elixir and, when it fits, sets Prog 1, Timer = VanadielUniqueDay() + 1 and the quest's
// mustZone; else 38 (an Event). The zone-in to Bastok Markets [S] from North Gustaberg [S] at Prog 1 with the mustZone
// clear and the Timer at or before today: 56, whose finish begins it and sets Prog 2. Accepted: Gentle Tiger: Prog 2:
// 57 (-> 3); Prog 3: 58 (an Event); else nil. Monument: Prog 3: 4 (-> 4). qm5: Prog 4: 16, whose finish completes it
// (an Icarus Wing).
// Kept as the Lua has it: Prog 1 and the Timer are written while the quest is available; 56 begins on any option;
// Gentle Tiger answers nil at Prog 0 / 1 / 4+, the Monument and qm5 at any Prog but theirs; 16 completes with no check
// of its result (no completion when the wing does not fit); no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class StormOnTheHorizon : QuestScript
    {
        public StormOnTheHorizon() : base(QuestLog.CrystalWar, "STORM_ON_THE_HORIZON") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_BAS_5_Storm_on_the_Horizon";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4);

            b.Reward(new QuestReward
            {
                Item = Item.IcarusWing,
            });

            // Zone into Bastok Markets [S] from North Gustaberg [S]
            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.CrystalWar, "BURDEN_OF_SUSPICION"))
                .OnZoneIn(Zone.BastokMarketsS, q =>
                {
                    if (q.Quest.Step == 1 &&
                        q.Player.PreviousZone == Zone.NorthGustabergS &&
                        !q.Quest.MustZone &&
                        q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay)
                    {
                        return QuestReply.Event(56);
                    }
                    return null;
                })
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Blatherix).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(37);
                    }
                    else
                    {
                        return QuestReply.Event(38);
                    }
                })
                .OnEventFinish(Zone.BastokMarketsS, 37, q =>
                {
                    if (q.Player.GiveItem(Item.Elixir))
                    {
                        q.Quest.SetStep(1);
                        q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay + 1);
                        q.Quest.SetMustZone();
                    }
                })
                .OnEventFinish(Zone.BastokMarketsS, 56, q =>
                {
                    q.Quest.Begin();
                    q.Quest.SetStep(2);
                });

            // Gentle Tiger -> Monument -> Grauberg ??? reward/completion
            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(57);
                    }
                    else if (questProgress == 3)
                    {
                        return QuestReply.Event(58);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BastokMarketsS, 57, q =>
                {
                    q.Quest.SetStep(3);
                })
                .Npc(Zone.NorthGustabergS, Npc.NorthGustabergS.Monument).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(4);
                    }
                    return null;
                })
                .OnEventFinish(Zone.NorthGustabergS, 4, q =>
                {
                    q.Quest.SetStep(4);
                })
                .Npc(Zone.GraubergS, Npc.GraubergS.qm5).OnTrigger(q =>
                {
                    if (q.Quest.Step == 4)
                    {
                        return QuestReply.ProgressEvent(16);
                    }
                    return null;
                })
                .OnEventFinish(Zone.GraubergS, 16, q =>
                {
                    q.Quest.Complete();
                });
        }
    }
}
