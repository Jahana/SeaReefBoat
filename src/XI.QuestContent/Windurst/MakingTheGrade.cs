// Making the Grade (Log ID 2, Quest ID 4), a line-for-line port of scripts/quests/windurst/Making_the_Grade.lua (quest
// rework B3b). Fuepepe : !pos 161 -2 161 238; Koru-Moru : !pos -120 -6 124 239; Chomoro-Kyotoro : !pos 133 -5 167 238.
// Offered by Fuepepe (442, option 1 begins it) once Teacher's Pet is completed, while Let Sleeping Dogs Lie is not
// accepted, at Windurst fame level 3 and level 10 (the March 8, 2007 update). Accepted: the Pile of Answer Sheets
// traded alone to Fuepepe at Prog 0: 455 (Prog 1; the sheets are not taken); traded alone to Koru-Moru (Windurst
// Walls) at Prog 1: 285 (the sheets taken, the Tattered Test Sheet, Prog 2), at any other Prog 287; Koru-Moru's 287 at
// Prog 0 with the sheets held, 286 from Prog 2; Chomoro-Kyotoro's 454 / 457 / 460 (Prog 2: the test sheet taken, Prog
// 3) / 461; Fuepepe's 443 / 456 / 458 (from Prog 2: completed, fame 30, the Scroll of Aspir; the quest's mustZone); the
// school's other lines (444-453). Completed with the mustZone clear: Fuepepe's 459 (importantEvent).
// Kept as the Lua file has it: 455 does not take the sheets (tradeHasExactly confirms them, no confirmTrade follows);
// Fuepepe's 458 completes the quest from Prog 2 (before Chomoro-Kyotoro's 460), the Tattered Test Sheet then kept;
// nothing in the scripts gives the Pile of Answer Sheets.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class MakingTheGrade : QuestScript
    {
        public MakingTheGrade() : base(QuestLog.Windurst, "MAKING_THE_GRADE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Making_the_Grade";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 30, FameArea = FameArea.Windurst, Item = Item.ScrollOfAspir });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Windurst, "TEACHERS_PET") &&
                    q.Player.QuestStatus(QuestLog.Windurst, "LET_SLEEPING_DOGS_LIE") != QuestStatus.Accepted &&
                    q.Player.FameLevel(FameArea.Windurst) >= 3 &&
                    q.Player.MainLevel >= 10) // Level requirement added in the March 8, 2007 update: https://www.playonline.com/pcd/update/ff11us/20070308c2bbd1/detail.html
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Fuepepe).OnTrigger(QuestReply.ProgressEvent(442))
                .OnEventFinish(Zone.WindurstWaters, 442, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.ChomoroKyotoro).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.Event(454);
                    }
                    else if (questProgress == 1)
                    {
                        return QuestReply.Event(457);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(460);
                    }
                    else
                    {
                        return QuestReply.Event(461);
                    }
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Fuepepe)
                    .OnTrade(q =>
                    {
                        if (
                            q.Trade.HasExactly(Item.PileOfAnswerSheets) &&
                            q.Quest.Step == 0)
                        {
                            return QuestReply.ProgressEvent(455);
                        }

                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int questProgress = q.Quest.Step;

                        if (questProgress == 0)
                        {
                            return QuestReply.Event(443);
                        }
                        else if (questProgress == 1)
                        {
                            return QuestReply.Event(456);
                        }
                        else if (questProgress >= 2)
                        {
                            return QuestReply.Event(458);
                        }

                        return null;
                    })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Akkeke).OnTrigger(QuestReply.Event(453))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.FoiMui).OnTrigger(QuestReply.Event(449))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Kirarara).OnTrigger(QuestReply.Event(447))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.KokoLihzeh).OnTrigger(QuestReply.Event(451))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.MashuuAjuu).OnTrigger(QuestReply.Event(448))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.MorenoToeno).OnTrigger(QuestReply.Event(444))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.PakuNakku).OnTrigger(QuestReply.Event(452))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.PechiruMashiru).OnTrigger(QuestReply.Event(445))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Rukuku).OnTrigger(QuestReply.Event(450))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Tauwawa).OnTrigger(QuestReply.Event(446))
                .OnEventFinish(Zone.WindurstWaters, 455, q => q.Quest.SetStep(1))
                .OnEventFinish(Zone.WindurstWaters, 458, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Quest.SetMustZone();
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 460, q =>
                {
                    q.Player.DelKeyItem(KeyItem.TatteredTestSheet);
                    q.Quest.SetStep(3);
                })
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KoruMoru)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.PileOfAnswerSheets))
                        {
                            if (q.Quest.Step == 1)
                            {
                                return QuestReply.ProgressEvent(285);
                            }
                            else
                            {
                                return QuestReply.Event(287);
                            }
                        }

                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int questProgress = q.Quest.Step;

                        if (
                            questProgress == 0 &&
                            q.Player.HasItem(Item.PileOfAnswerSheets))
                        {
                            return QuestReply.Event(287);
                        }
                        else if (questProgress >= 2)
                        {
                            return QuestReply.Event(286);
                        }

                        return null;
                    })
                .OnEventFinish(Zone.WindurstWalls, 285, q =>
                {
                    q.Trade.Confirm();

                    q.Player.GiveKeyItem(KeyItem.TatteredTestSheet);
                    q.Quest.SetStep(2);
                });

            b.State(QuestStatus.Completed, when: q =>
                    !q.Quest.MustZone)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Fuepepe).OnTrigger(QuestReply.Event(459).ImportantEvent());
        }
    }
}
