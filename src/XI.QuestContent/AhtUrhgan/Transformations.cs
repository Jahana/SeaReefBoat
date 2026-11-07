// Transformations (BLU AF3; Log ID 6, Quest ID 23), a line-for-line port of
// scripts/quests/ahtUrhgan/BLU_AF3_Transformations.lua (quest rework B3b Aht Urhgan, group 1). Aht Urhgan Whitegate:
// Waoud !pos 65 -6 -78 (no script; default 61), Imperial Whitegate !pos 154 -4 0 (no script; default
// GATE_IS_FIRMLY_CLOSED). Alzadaal Undersea Ruins: blank_transformations !pos -530 0 650 (no script; default
// NOTHING_OUT_OF_ORDINARY), Nepionic Soulflayer (NEPIONIC_SOULFLAYER), trigger areas 24 (52 -1 774 .. 67 1 778) and 25
// (134 -1 -584 .. 146 1 -577) (the zone's own switch has no case for either). Available with Omens completed, main job
// BLU at AF3_QUEST_LEVEL (50): Waoud nil while Transformations' mustZone is set or GetSystemTime() < Timer (Omens' 716
// sets both), else 720 (gil) at Prog 0, 721 (gil) after; 720's option 1 with 1000 gil takes it (PAY_DIVINATION) and
// sets Prog 1, 721's takes it only; Imperial Whitegate at Prog 1: 722, which begins it and sets the char var
// [BLUAF]Remaining 7 (the Magus armour Lathuya crafts). Accepted: Waoud 723 (gil), option 1 with 1000 gil taking it;
// area 24 at Prog 1: 2 (Prog 2); area 25 at Prog 2: 3 (Prog 3); blank_transformations at Prog 3 with the Soulflayer not
// up: 4 (popFromQM, the qm not hidden), at Prog 4: 5 (completed: Magus Keffiyeh, Paragon of Blue Mage Excellence; The
// Beast Within's Timer = now + 60); the Soulflayer's death at Prog 3: Prog 4.
// Kept as the Lua has it: 720 keeps the quest available (Prog 1 only); 4 pops whether or not it can (popFromQM's own
// checks); 5 sets The Beast Within's Timer from GetSystemTime() (the era module transformations sets JstMidnight()),
// and no mustZone for it.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class Transformations : QuestScript
    {
        public Transformations() : base(QuestLog.AhtUrghan, "TRANSFORMATIONS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/BLU_AF3_Transformations";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.MagusKeffiyeh,
                Title = Title.ParagonOfBlueMageExcellence,
            });
            b.Steps(1, 2, 3, 4);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.AhtUrghan, "OMENS") &&
                    q.Player.MainJob == Job.Blu &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF3_QUEST_LEVEL"))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Waoud).OnTrigger(q =>
                {
                    if (q.Quest.MustZone ||
                        q.Player.SystemTime < q.Quest.Var("Timer"))
                    {
                        return null;
                    }

                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(720, q.Player.Gil);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(721, q.Player.Gil);
                    }
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.ImperialWhitegate).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(722);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 720, q =>
                {
                    if (q.Option == 1 &&
                        q.Player.Gil >= 1000)
                    {
                        q.Player.DelGil(1000);
                        q.Text.Special(Text.AhtUrhganWhitegate.PAY_DIVINATION);

                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 721, q =>
                {
                    if (q.Option == 1 &&
                        q.Player.Gil >= 1000)
                    {
                        q.Player.DelGil(1000);
                        q.Text.Special(Text.AhtUrhganWhitegate.PAY_DIVINATION);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 722, q =>
                {
                    q.Quest.Begin();
                    q.Player.SetCharVar("[BLUAF]Remaining", 7); // Player can now craft BLU armor
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Waoud).OnTrigger(q =>
                    QuestReply.ProgressEvent(723, q.Player.Gil))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 723, q =>
                {
                    if (q.Option == 1 &&
                        q.Player.Gil >= 1000)
                    {
                        q.Player.DelGil(1000);
                        q.Text.Special(Text.AhtUrhganWhitegate.PAY_DIVINATION);
                    }
                })
                .Npc(Zone.AlzadaalUnderseaRuins, Npc.AlzadaalUnderseaRuins.blanktransformations).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 3 &&
                        !q.Player.MobIsSpawned(Mob.AlzadaalUnderseaRuins.NEPIONIC_SOULFLAYER))
                    {
                        return QuestReply.ProgressEvent(4);
                    }
                    else if (questProgress == 4)
                    {
                        return QuestReply.ProgressEvent(5);
                    }
                    return null;
                })
                .Npc(Zone.AlzadaalUnderseaRuins, Npc.AlzadaalUnderseaRuins.NepionicSoulflayer).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        q.Quest.SetStep(4);
                    }
                })
                .OnTriggerAreaEnter(Zone.AlzadaalUnderseaRuins, 24, q =>
                    q.Quest.Step == 1 ? QuestReply.ProgressEvent(2) : null)
                .OnTriggerAreaEnter(Zone.AlzadaalUnderseaRuins, 25, q =>
                    q.Quest.Step == 2 ? QuestReply.ProgressEvent(3) : null)
                .OnEventFinish(Zone.AlzadaalUnderseaRuins, 2, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.AlzadaalUnderseaRuins, 3, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.AlzadaalUnderseaRuins, 4, q =>
                {
                    q.Player.PopFromQm(Mob.AlzadaalUnderseaRuins.NEPIONIC_SOULFLAYER, 0);
                })
                .OnEventFinish(Zone.AlzadaalUnderseaRuins, 5, q =>
                {
                    if (q.Quest.Complete())
                    {
                        // 1 minute wait time
                        q.Player.SetQuestVar(QuestLog.AhtUrghan, "THE_BEAST_WITHIN", "Timer", q.Player.SystemTime + 60);
                    }
                });
        }
    }
}
