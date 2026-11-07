// Save My Sister (Log ID 3, Quest ID 1), a line-for-line port of scripts/quests/jeuno/Save_My_Sister.lua (quest rework
// B3b). Baudin : !pos -75 0 80 244; Mailloquetat : !pos -31 -1 8 244; Neraf-Najiruf : !pos -36 2 60 243; the four
// Braziers of The Eldieme Necropolis (F-9, H-7, F-7, H-9: offsets 0-3 from IDs.lua npc.BRAZIER). The quest stays
// available until it is completed (it is not in the log before). Crest of Davoi completed and Jeuno fame level 4:
// Baudin's 172 or Mailloquetat's 159 set Prog 1; Baudin's 105 Prog 2; Neraf-Najiruf's 98 (option 0) gives the Ducal
// Guard's Lantern, Prog 3; the braziers in order (Stage 0-3: the brazier whose offset is Stage) light the lantern, a
// wrong one puts it out (Stage 0); the fourth sets Prog 4; Baudin's 107 completes it (3000 gil, Holy Mace, Exorcist in
// Training, fame +13 in San d'Oria, Bastok and Windurst). Completed: Baudin's 176 (replaceDefault), and Neraf-Najiruf's
// 97 takes the lantern, which the quest never deletes (kept: the Lua does so).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class SaveMySister : QuestScript
    {
        public SaveMySister() : base(QuestLog.Jeuno, "SAVE_MY_SISTER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Save_My_Sister";

        // brazierMessages[stage] = { the right brazier's text, a wrong brazier's text }
        static readonly ZoneText[][] BrazierMessages =
        {
            new[] { Text.TheEldiemeNecropolis.THE_LIGHT_DIMLY, Text.TheEldiemeNecropolis.REFUSE_TO_LIGHT },
            new[] { Text.TheEldiemeNecropolis.THE_LIGHT_HAS_INTENSIFIED, Text.TheEldiemeNecropolis.LANTERN_GOES_OUT },
            new[] { Text.TheEldiemeNecropolis.THE_LIGHT_HAS_INTENSIFIED, Text.TheEldiemeNecropolis.LANTERN_GOES_OUT },
            new[] { Text.TheEldiemeNecropolis.THE_LIGHT_IS_FULLY_LIT, Text.TheEldiemeNecropolis.LANTERN_GOES_OUT },
        };

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Gil = 3000, Item = Item.HolyMace, Title = Title.ExorcistInTraining });
            b.Steps(1, 2, 3, 4);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Jeuno, "CREST_OF_DAVOI") &&
                    q.Player.FameLevel(FameArea.Jeuno) >= 4)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Baudin).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(172);
                    }
                    else if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(105);
                    }
                    else if (questProgress == 3)
                    {
                        return QuestReply.Event(27);
                    }
                    else if (questProgress == 4)
                    {
                        return QuestReply.ProgressEvent(107);
                    }
                    return null;
                })
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Mailloquetat).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(159);
                    }
                    else
                    {
                        return QuestReply.Event(158);
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 105, q => q.Quest.SetStep(2))
                .OnEventFinish(Zone.UpperJeuno, 107, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 13);
                        q.Player.AddFame(FameArea.Bastok, 13);
                        q.Player.AddFame(FameArea.Windurst, 13);
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 159, q => q.Quest.SetStep(1))
                .OnEventFinish(Zone.UpperJeuno, 172, q => q.Quest.SetStep(1))
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.NerafNajiruf).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(98);
                    }
                    else if (questProgress >= 3)
                    {
                        return QuestReply.Event(99);
                    }
                    return null;
                })
                .OnEventFinish(Zone.RuludeGardens, 98, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.GiveKeyItem(KeyItem.DucalGuardsLantern);
                        q.Quest.SetStep(3);
                    }
                })
                .Npc(Zone.TheEldiemeNecropolis, Npc.TheEldiemeNecropolis.Brazier).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(44);
                    }
                    return null;
                })
                .OnEventFinish(Zone.TheEldiemeNecropolis, 44, q =>
                {
                    if (q.Option == 0)
                    {
                        int lanternStage = q.Quest.Var("Stage");
                        int lanternOffset = q.Npc.Id - q.Player.NpcIdOf("BRAZIER");

                        if (lanternStage == lanternOffset)
                        {
                            q.Text.Special(BrazierMessages[lanternStage][0], 0, 0, 0, KeyItem.DucalGuardsLanternLit);

                            if (lanternStage < 3)
                            {
                                q.Quest.IncrementVar("Stage", 1);
                            }
                            else
                            {
                                q.Quest.SetStep(4);
                            }
                        }
                        else
                        {
                            q.Text.Special(BrazierMessages[lanternStage][1], 0, 0, 0, KeyItem.DucalGuardsLanternLit);
                            q.Quest.SetVar("Stage", 0);
                        }
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Baudin).OnTrigger(QuestReply.Event(176).ReplaceDefault())
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.NerafNajiruf).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.DucalGuardsLantern))
                    {
                        return QuestReply.ProgressEvent(97);
                    }
                    return null;
                })
                .OnEventFinish(Zone.RuludeGardens, 97, q => q.Player.DelKeyItem(KeyItem.DucalGuardsLantern));
        }
    }
}
