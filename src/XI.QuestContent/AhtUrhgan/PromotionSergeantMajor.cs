// Promotion: Sergeant Major (Log ID 6, Quest ID 95), a line-for-line port of
// scripts/quests/ahtUrhgan/Promotion_Sergeant_Major.lua (quest rework B3b Aht Urhgan, group 4). Aht Urhgan Whitegate:
// Naja Salaheem !pos 26 -8 -45.5 (no script, no default).
// Vars: Prog (the Vana'diel day of the last game), Stage (0 first game failed, 1 passed, 2 second failed, 3 passed, 4
// third failed, 5 passed).
// Available with AssaultPromotion >= 25 and Promotion: Sergeant completed: Naja 5048, whose finish begins it, sets Prog
// to today and, at option 2, Stage 1. Accepted, a day after Prog: Stage 0: 5049 (Prog today; option 2: Stage 1); Stage
// 1: 5050 (Prog today; option 2: Stage 3, else 2); Stage 2: 5051 (option 2: Stage 3); Stage 3: 5052 (option 2: Stage 5,
// else 4); Stage 4: 5053 (option 2: Stage 5); Stage 5: 5054, whose finish completes it (the SM Wildcat Badge, Sergeant
// Major) and then sets AssaultPromotion 0, takes the S Wildcat Badge and shows PROMOTION_SERGEANT_MAJOR. On Prog's own
// day no section answers (Naja: nothing).
// Kept as the Lua has it: 5048 begins it on any option; every game's finish sets Prog to today whatever the option.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class PromotionSergeantMajor : QuestScript
    {
        public PromotionSergeantMajor() : base(QuestLog.AhtUrghan, "PROMOTION_SERGEANT_MAJOR") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Promotion_Sergeant_Major";

        // vars.Stage == stage and vars.Prog < VanadielUniqueDay(): Prog holds a day, not a step
        static bool GameDue(IQuestCheck q, int stage) =>
            q.Quest.Var("Stage") == stage && q.Quest.Var("Prog") < q.Player.VanadielUniqueDay;

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.SmWildcatBadge,
                Title = Title.SergeantMajor,
            });

            // Section 1: available, AssaultPromotion >= 25, Sergeant completed
            b.State(QuestStatus.Available, when: q =>
                    q.Player.CharVar("AssaultPromotion") >= 25 &&
                    q.Player.HasCompletedQuest(QuestLog.AhtUrghan, "PROMOTION_SERGEANT"))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem)
                    .OnTrigger(q => QuestReply.ProgressEvent(5048))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5048, q =>
                {
                    q.Quest.Begin();
                    q.Quest.SetVar("Prog", q.Player.VanadielUniqueDay);
                    if (q.Option == 2)
                    {
                        q.Quest.SetVar("Stage", 1);
                    }
                });

            // Section 2: didn't pass the 1st game and has to redo
            b.State(QuestStatus.Accepted, when: q => GameDue(q, 0))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem)
                    .OnTrigger(q => QuestReply.ProgressEvent(5049))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5049, q =>
                {
                    q.Quest.SetVar("Prog", q.Player.VanadielUniqueDay);
                    if (q.Option == 2)
                    {
                        // passed 1st mini game, onto 2nd
                        q.Quest.SetVar("Stage", 1);
                    }
                });

            // Section 3: passed the 1st mini game, start the 2nd
            b.State(QuestStatus.Accepted, when: q => GameDue(q, 1))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem)
                    .OnTrigger(q => QuestReply.ProgressEvent(5050))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5050, q =>
                {
                    q.Quest.SetVar("Prog", q.Player.VanadielUniqueDay);
                    if (q.Option == 2)
                    {
                        // passed mini game, onto 3rd game
                        q.Quest.SetVar("Stage", 3);
                    }
                    else
                    {
                        // failed and has to do again
                        q.Quest.SetVar("Stage", 2);
                    }
                });

            // Section 4: didn't pass the 2nd game, redo
            b.State(QuestStatus.Accepted, when: q => GameDue(q, 2))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem)
                    .OnTrigger(q => QuestReply.ProgressEvent(5051))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5051, q =>
                {
                    q.Quest.SetVar("Prog", q.Player.VanadielUniqueDay);
                    if (q.Option == 2)
                    {
                        // passed mini game, onto 3rd game
                        q.Quest.SetVar("Stage", 3);
                    }
                });

            // Section 5: passed the 2nd mini game, start the 3rd
            b.State(QuestStatus.Accepted, when: q => GameDue(q, 3))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem)
                    .OnTrigger(q => QuestReply.ProgressEvent(5052))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5052, q =>
                {
                    q.Quest.SetVar("Prog", q.Player.VanadielUniqueDay);
                    if (q.Option == 2)
                    {
                        // passed mini game, You did it!
                        q.Quest.SetVar("Stage", 5);
                    }
                    else
                    {
                        // failed and has to do again
                        q.Quest.SetVar("Stage", 4);
                    }
                });

            // Section 6: didn't pass the 3rd game, redo
            b.State(QuestStatus.Accepted, when: q => GameDue(q, 4))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem)
                    .OnTrigger(q => QuestReply.ProgressEvent(5053))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5053, q =>
                {
                    q.Quest.SetVar("Prog", q.Player.VanadielUniqueDay);
                    if (q.Option == 2)
                    {
                        // passed mini game, complete
                        q.Quest.SetVar("Stage", 5);
                    }
                });

            // Section 7: passed all games and a game day's wait
            b.State(QuestStatus.Accepted, when: q => GameDue(q, 5))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem)
                    .OnTrigger(q => QuestReply.ProgressEvent(5054))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5054, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetCharVar("AssaultPromotion", 0);
                        q.Player.DelKeyItem(KeyItem.SWildcatBadge);
                        q.Text.Special(Text.AhtUrhganWhitegate.PROMOTION_SERGEANT_MAJOR);
                    }
                });
        }
    }
}
