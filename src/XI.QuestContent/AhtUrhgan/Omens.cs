// Omens (BLU AF2; Log ID 6, Quest ID 22), a line-for-line port of scripts/quests/ahtUrhgan/BLU_AF2_Omens.lua (quest
// rework B3b Aht Urhgan, group 1). Aht Urhgan Whitegate: Waoud !pos 65 -6 -78 (no script; default 61), Lathuya !pos -95
// -6 32 (no script; default 770; the hidden quest Crafted Magus Artifact once Transformations is accepted). Aydeewa
// Subterrane: blank_omens !pos 342 37 -25 (no script; default NOTHING_OUT_OF_ORDINARY). Navukgo Execution Chamber: the
// battlefield Omens (scripts/battlefields/Navukgo_Execution_Chamber/omens.lua: index 2, entry _1s0, requiredVar Prog
// 0). Available with Beginnings completed, main job BLU at AF2_QUEST_LEVEL (50): Waoud nil while Omens' mustZone is set
// or GetSystemTime() < Timer (Beginnings' 707 sets both), else 710, whose finish begins it. Accepted: Waoud Prog 1 712
// (the Sealed Immortal Envelope, Prog 2), Prog 0 711 (gil), Prog 2 713 (gil), each 711 / 713 option 1 with 1000 gil
// taking it (PAY_DIVINATION); Lathuya Prog 2 714 (Prog 3), Prog 3 715, Prog 4 716 (completed: Magus Charuqs, Immortal
// Lion; the envelope taken, Transformations' mustZone, its Timer = now + 60); blank_omens Prog 3 9 (Prog 4); the
// battlefield's 32001 with battlefieldWin 1122 at Prog 0: Prog 1. Completed: Lathuya 771 (replaceDefault).
// Kept as the Lua has it: 710 begins it on any option; 716 sets Transformations' Timer from GetSystemTime() (the era
// module omens sets JstMidnight()); 712's key item given whatever the char holds.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class Omens : QuestScript
    {
        public Omens() : base(QuestLog.AhtUrghan, "OMENS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/BLU_AF2_Omens";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.MagusCharuqs,
                Title = Title.ImmortalLion,
            });
            b.Steps(1, 2, 3, 4);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.AhtUrghan, "BEGINNINGS") &&
                    q.Player.MainJob == Job.Blu &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF2_QUEST_LEVEL"))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Waoud).OnTrigger(q =>
                {
                    if (q.Quest.MustZone ||
                        q.Player.SystemTime < q.Quest.Var("Timer"))
                    {
                        return null;
                    }

                    return QuestReply.ProgressEvent(710);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 710, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Waoud).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(712);
                    }
                    else if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(711, q.Player.Gil);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(713, q.Player.Gil);
                    }
                    return null;
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Lathuya).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(714);
                    }
                    else if (questProgress == 3)
                    {
                        return QuestReply.ProgressEvent(715);
                    }
                    else if (questProgress == 4)
                    {
                        return QuestReply.ProgressEvent(716);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 711, q =>
                {
                    if (q.Option == 1 &&
                        q.Player.Gil >= 1000)
                    {
                        q.Player.DelGil(1000);
                        q.Text.Special(Text.AhtUrhganWhitegate.PAY_DIVINATION);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 712, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.SealedImmortalEnvelope);
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 713, q =>
                {
                    if (q.Option == 1 &&
                        q.Player.Gil >= 1000)
                    {
                        q.Player.DelGil(1000);
                        q.Text.Special(Text.AhtUrhganWhitegate.PAY_DIVINATION);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 714, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 716, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.SealedImmortalEnvelope);

                        q.Player.SetQuestMustZone(QuestLog.AhtUrghan, "TRANSFORMATIONS");
                        // 1 minute wait time
                        q.Player.SetQuestVar(QuestLog.AhtUrghan, "TRANSFORMATIONS", "Timer", q.Player.SystemTime + 60);
                    }
                })
                .Npc(Zone.AydeewaSubterrane, Npc.AydeewaSubterrane.blankomens).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(9);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AydeewaSubterrane, 9, q =>
                {
                    q.Quest.SetStep(4);
                })
                .OnEventFinish(Zone.NavukgoExecutionChamber, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.Omens &&
                        q.Quest.Step == 0)
                    {
                        q.Quest.SetStep(1);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Lathuya)
                    .OnTrigger(QuestReply.Event(771).ReplaceDefault());
        }
    }
}
