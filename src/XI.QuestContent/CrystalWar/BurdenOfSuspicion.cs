// Burden of Suspicion (Log ID 7, Quest ID 20), a line-for-line port of
// scripts/quests/crystalWar/WOTG_BAS_4_Burden_of_Suspicion.lua (quest rework B3b Crystal War group 3). Bastok Markets
// [S]: Gentle Tiger !pos -203 -9 0 (default 109), Engelhart !pos -79 -4 -123 (default 104). The Eldieme Necropolis [S]:
// Sarcophagus !pos 346.9 -32.4 -126 (no script, no default).
// Available, Light in the Darkness completed: Gentle Tiger: with the quest's mustZone clear and its Timer at or before
// VanadielUniqueDay() (Light in the Darkness's completion sets both: the Timer to the next day): 30, whose finish
// begins it and takes the Warning Letter; else nil. Accepted (Prog): Gentle Tiger: 1: 28 (an Event); 2: 34, whose
// finish completes it (Assistant Detective); else 31 (an Event). Engelhart: 0: 32 (-> 1); 2: 36 (an Event); else 33
// (an Event). Sarcophagus: 1: 17 (-> 2).
// Kept as the Lua has it: 30 begins on any option and takes a Warning Letter not held with no check; Gentle Tiger's 28
// at Prog 1 ("Retail accurate, but odd."); Sarcophagus answers nil at any Prog but 1; 34 completes with no check of
// its result (a title cannot fail); no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class BurdenOfSuspicion : QuestScript
    {
        public BurdenOfSuspicion() : base(QuestLog.CrystalWar, "BURDEN_OF_SUSPICION") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_BAS_4_Burden_of_Suspicion";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2);

            b.Reward(new QuestReward
            {
                Title = Title.AssistantDetective,
            });

            // Talk to Gentle Tiger in Bastok Markets (S)
            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.CrystalWar, "LIGHT_IN_THE_DARKNESS"))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(q =>
                {
                    if (!q.Quest.MustZone &&
                        q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay)
                    {
                        return QuestReply.ProgressEvent(30);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BastokMarketsS, 30, q =>
                {
                    q.Quest.Begin();
                    q.Player.DelKeyItem(KeyItem.WarningLetter);
                });

            // Continue the investigation in Bastok Markets (S) and The Eldieme Necropolis [S]
            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 1)
                    {
                        return QuestReply.Event(28); // Retail accurate, but odd.
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(34);
                    }
                    else
                    {
                        return QuestReply.Event(31);
                    }
                })
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Engelhart).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(32);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.Event(36);
                    }
                    else
                    {
                        return QuestReply.Event(33);
                    }
                })
                .OnEventFinish(Zone.BastokMarketsS, 32, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.BastokMarketsS, 34, q =>
                {
                    q.Quest.Complete();
                })
                .Npc(Zone.TheEldiemeNecropolisS, Npc.TheEldiemeNecropolisS.Sarcophagus).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(17);
                    }
                    return null;
                })
                .OnEventFinish(Zone.TheEldiemeNecropolisS, 17, q =>
                {
                    q.Quest.SetStep(2);
                });
        }
    }
}
