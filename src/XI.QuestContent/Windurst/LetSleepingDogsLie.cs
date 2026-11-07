// Let Sleeping Dogs Lie (Log ID 2, Quest ID 46), a line-for-line port of scripts/quests/windurst/Let_Sleeping_Dogs_Lie.lua
// (quest rework B3b). Paku-Nakku !pos 127.189 -5.750 164.648; Pechiru-Mashiru !pos 163.589 -1.249 157.589; Maabu-Sonbu
// !pos -107.345 -3.250 108.138; qm_pepper !pos -429.603 24.760 473.015. At Windurst fame level 4, Paku-Nakku's 481
// begins it; the school children's lines meanwhile (Akkeke 484, Chomoro-Kyotoro 489, Foi-Mui 486, Kirarara 485, Koko
// Lihzeh 488, Mashuu-Ajuu 483, Rukuku 487). Accepted: Maabu-Sonbu (Port Windurst) 319 at Prog 0 (Prog 1), 320 at Prog 1,
// 321 at Prog 2 (the item traded); Blazing Peppers (else a Remedy) traded to Paku-Nakku at Prog 1: Option 1 (2), 494
// (Prog 2, the item taken); Pechiru-Mashiru's 495 at Prog 2 (Prog 3); Paku-Nakku's 499 at Prog 3, option 2: the title
// Spoilsport, Prog 4; Pechiru-Mashiru's 497 at Prog 4 completes it (fame 40, the Hypno Staff). The children's lines at
// Prog <= 3 and at Prog 4. Kept as the Lua file has it: the trade handler sets Option before its event plays (the
// event's finish is not needed for it); tradeHas is not exact (Blazing Peppers with a Remedy: Option 1, only the
// peppers taken); 497's finish ignores a failed completion (the bag full: Prog stays 4); 499 with any option but 2 does
// nothing (Prog stays 3); the qm_pepper named in the header has no handler here.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class LetSleepingDogsLie : QuestScript
    {
        public LetSleepingDogsLie() : base(QuestLog.Windurst, "LET_SLEEPING_DOGS_LIE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Let_Sleeping_Dogs_Lie";

        // a school child's line: Prog <= 3 / Prog 4 / else nil (the same shape for each child in Lua)
        static QuestReply ByProgress(IQuestContext q, int upTo3, int at4)
        {
            int progress = q.Quest.Step;

            if (progress <= 3)
            {
                return QuestReply.Event(upTo3);
            }
            else if (progress == 4)
            {
                return QuestReply.Event(at4);
            }

            return null;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 40, FameArea = FameArea.Windurst, Item = Item.HypnoStaff });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.Windurst) >= 4)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.PakuNakku).OnTrigger(q => QuestReply.ProgressEvent(481))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Akkeke).OnTrigger(QuestReply.Event(484))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.ChomoroKyotoro).OnTrigger(QuestReply.Event(489))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.FoiMui).OnTrigger(QuestReply.Event(486))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Kirarara).OnTrigger(QuestReply.Event(485))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.KokoLihzeh).OnTrigger(QuestReply.Event(488))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.MashuuAjuu).OnTrigger(QuestReply.Event(483))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Rukuku).OnTrigger(QuestReply.Event(487))
                .OnEventFinish(Zone.WindurstWaters, 481, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.MaabuSonbu).OnTrigger(q =>
                {
                    int option = q.Quest.Var("Option");
                    int progress = q.Quest.Step;

                    if (progress == 0)
                    {
                        return QuestReply.ProgressEvent(319, 0, Item.Remedy, Item.BlazingPeppers, Item.Sickle);
                    }
                    else if (progress == 1)
                    {
                        return QuestReply.Event(320, 0, Item.Remedy, Item.BlazingPeppers, Item.Sickle);
                    }
                    else if (progress == 2 && option == 1)
                    {
                        return QuestReply.Event(321, 0, Item.BlazingPeppers);
                    }
                    else if (progress == 2 && option == 2)
                    {
                        return QuestReply.Event(321, 0, Item.Remedy);
                    }

                    return null;
                })
                .OnEventFinish(Zone.PortWindurst, 319, q => q.Quest.SetStep(1))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Akkeke).OnTrigger(q => ByProgress(q, 484, 502))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.ChomoroKyotoro).OnTrigger(q => ByProgress(q, 489, 507))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Fuepepe).OnTrigger(q =>
                {
                    if (q.Quest.Step <= 3)
                    {
                        return QuestReply.Event(493);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.FoiMui).OnTrigger(q => ByProgress(q, 486, 504))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Kirarara).OnTrigger(q => ByProgress(q, 485, 503))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.KokoLihzeh).OnTrigger(q => ByProgress(q, 488, 506))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.MashuuAjuu).OnTrigger(q => ByProgress(q, 483, 501))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.MorenoToeno).OnTrigger(q =>
                {
                    if (q.Quest.Step <= 3)
                    {
                        return QuestReply.Event(491);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.PakuNakku)
                    .OnTrigger(q =>
                    {
                        int progress = q.Quest.Step;

                        if (progress <= 2)
                        {
                            return QuestReply.Event(482);
                        }
                        else if (progress == 3)
                        {
                            return QuestReply.ProgressEvent(499);
                        }
                        else if (progress == 4)
                        {
                            return QuestReply.Event(500);
                        }

                        return null;
                    })
                    .OnTrade(q =>
                    {
                        if (q.Quest.Step == 1)
                        {
                            if (q.Trade.Has(Item.BlazingPeppers))
                            {
                                q.Quest.SetVar("Option", 1); // Traded Peppers
                                return QuestReply.ProgressEvent(494, 0, 1);
                            }
                            else if (q.Trade.Has(Item.Remedy))
                            {
                                q.Quest.SetVar("Option", 2); // Traded a Remedy
                                return QuestReply.ProgressEvent(494);
                            }
                        }

                        return null;
                    })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.PechiruMashiru).OnTrigger(q =>
                {
                    int option = q.Quest.Var("Option");
                    int progress = q.Quest.Step;

                    if (progress == 0)
                    {
                        return QuestReply.Event(490);
                    }
                    else if (progress == 1)
                    {
                        return QuestReply.Event(492);
                    }
                    else if (progress == 2)
                    {
                        if (option == 1)
                        {
                            return QuestReply.ProgressEvent(495, 0, Item.Remedy, Item.BlazingPeppers, 1); // Player had traded Peppers to Paku-Nakku
                        }
                        else if (option == 2)
                        {
                            return QuestReply.ProgressEvent(495, 0, Item.Remedy, Item.BlazingPeppers, 0); // Player had traded a Remedy to Paku-Nakku
                        }
                    }
                    else if (progress == 3)
                    {
                        return QuestReply.Event(496);
                    }
                    else if (progress == 4)
                    {
                        return QuestReply.ProgressEvent(497);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Rukuku).OnTrigger(q => ByProgress(q, 487, 505))
                .OnEventFinish(Zone.WindurstWaters, 494, q =>
                {
                    q.Quest.SetStep(2);
                    q.Trade.Confirm();
                })
                .OnEventFinish(Zone.WindurstWaters, 495, q => q.Quest.SetStep(3))
                .OnEventFinish(Zone.WindurstWaters, 497, q => q.Quest.Complete())
                // The title comes with the spoiled ending, not with the reward.
                .OnEventFinish(Zone.WindurstWaters, 499, q =>
                {
                    if (q.Option == 2)
                    {
                        q.Player.AddTitle(Title.Spoilsport);
                        q.Quest.SetStep(4);
                    }
                });
        }
    }
}
