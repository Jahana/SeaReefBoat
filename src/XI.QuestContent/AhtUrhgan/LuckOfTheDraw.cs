// Luck of the Draw (COR unlock; Log ID 6, Quest ID 6), a line-for-line port of scripts/quests/ahtUrhgan/Luck_of_the_Draw.lua
// (quest rework B3b Aht Urhgan, group 2). Aht Urhgan Whitegate: Ratihb !pos 75 -6 -137 (no script; default 603), Mafwahb
// !pos 151 -2 -3 (no script; default 647). Arrapago Reef: qm6 !pos 469 -12 112 (no script; default
// NOTHING_OUT_OF_ORDINARY). Talacca Cove: qm1 !pos -62 -10 -139 (no script, no default), _1l0 the Rock Slab !pos -100 -9
// -87 (the battlefields' entry; default messageSpecial -1).
// Available from main level ADVANCED_JOB_LEVEL (30): Ratihb 547, whose finish begins it at Prog 1. Accepted: Mafwahb Prog
// 1 548 (Prog 2), Prog 2 647 (an Event: his default's id); qm6 Prog 2: the cutscene 211 (Prog 3); Talacca's qm1 Prog 3: 2
// (Prog 4, the Forgotten Hexagun); _1l0 Prog 4: 3 (completed: a Corsair Die, the COR gesture key item, Seagull Phratrie
// Crew Member; the hexagun taken, COR unlocked, YOU_CAN_NOW_BECOME_A_CORSAIR). Completed, while Equipped for All
// Occasions' Stage is 0 and that quest is not completed: Ratihb 552, whose finish sets Equipped for All Occasions' Stage
// 1 (the var that quest's Ratihb reads; its own completion cleans it).
// Kept as the Lua has it: 547 begins it on any option; 552 plays again until Stage is set (any option sets it).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class LuckOfTheDraw : QuestScript
    {
        public LuckOfTheDraw() : base(QuestLog.AhtUrghan, "LUCK_OF_THE_DRAW") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Luck_of_the_Draw";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.CorsairDie,
                KeyItem = KeyItem.JobGestureCorsair,
                Title = Title.SeagullPhratrieCrewMember,
            });
            b.Steps(1, 2, 3, 4);

            // Section: Quest available
            b.State(QuestStatus.Available, when: q => q.Player.MainLevel >= q.Settings.MainNumber("ADVANCED_JOB_LEVEL"))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Ratihb).OnTrigger(q => QuestReply.ProgressEvent(547))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 547, q =>
                {
                    q.Quest.Begin();
                    q.Quest.SetStep(1);
                });

            // Section: Quest accepted
            b.State(QuestStatus.Accepted)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Mafwahb).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(548);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.Event(647);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 548, q =>
                {
                    q.Quest.SetStep(2);
                })
                .Npc(Zone.ArrapagoReef, Npc.ArrapagoReef.qm6).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressCutscene(211);
                    }
                    return null;
                })
                .OnEventFinish(Zone.ArrapagoReef, 211, q =>
                {
                    q.Quest.SetStep(3);
                })
                .Npc(Zone.TalaccaCove, Npc.TalaccaCove.qm1).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(2);
                    }
                    return null;
                })
                .Npc(Zone.TalaccaCove, Npc.TalaccaCove._1l0).OnTrigger(q =>
                {
                    if (q.Quest.Step == 4)
                    {
                        return QuestReply.ProgressEvent(3);
                    }
                    return null;
                })
                .OnEventFinish(Zone.TalaccaCove, 2, q =>
                {
                    q.Quest.SetStep(4);
                    q.Player.GiveKeyItem(KeyItem.ForgottenHexagun);
                })
                .OnEventFinish(Zone.TalaccaCove, 3, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.ForgottenHexagun);
                        q.Player.UnlockJob(Job.Cor);
                        q.Text.Special(Text.TalaccaCove.YOU_CAN_NOW_BECOME_A_CORSAIR);
                    }
                });

            // Section: Quest complete.
            // Event 552 is a one-time-only event that can occur after completing "Luck of the Draw" but before finishing
            // Equipped for all Occasions. This charvar is cleaned up on complete of 'Equipped for all Occasions' when
            // quest:complete() is called.
            b.State(QuestStatus.Completed, when: q =>
                    // xi.quest.getVar(player, AHT_URHGAN, EQUIPPED_FOR_ALL_OCCASIONS, 'Stage'): player:getVar('Quest[6][24]Stage')
                    q.Player.CharVar("Quest[6][24]Stage") == 0 &&
                    !q.Player.HasCompletedQuest(QuestLog.AhtUrghan, "EQUIPPED_FOR_ALL_OCCASIONS"))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Ratihb).OnTrigger(q => QuestReply.ProgressEvent(552))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 552, q =>
                {
                    q.Player.SetQuestVar(QuestLog.AhtUrghan, "EQUIPPED_FOR_ALL_OCCASIONS", "Stage", 1);
                });
        }
    }
}
