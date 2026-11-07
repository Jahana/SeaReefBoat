// Wondering Minstrel (Log ID 2, Quest ID 6), a line-for-line port of scripts/quests/windurst/Wondering_Minstrel.lua
// (quest rework B3b, Windurst group 12). Jatan-Paratan : !pos -59.998 -4.105 19.775 238;
// Yuli Yaam : !pos -62.241 -3.500 21.683 238; Yung Yaam : !pos -65.622 -3.800 27.019 238;
// Aramu-Paramu : !pos -66.924 -3.800 26.038 238; Ruslan : !pos -19.293 -0.100 -56.648 245. At Windurst fame level 5:
// Jatan-Paratan's 633 (Prog 0; option 3: Prog 1), then 634 (option 0: begun); Yuli Yaam 637, Yung Yaam 636. Accepted:
// Ruslan (Lower Jeuno) 10009 (0, Rosewood Lumber) at Prog 1 (option 0: Prog 2), else 10010; a Piece of Rosewood Lumber
// alone (tradeMatches: exactly one) traded to Jatan-Paratan at Prog 2: 638, completed (fame 50, the Faerie Piccolo,
// Down Piper Pipe-upperer; the lumber taken; needToZone); Jatan-Paratan 635, Aramu-Paramu 683, Yuli Yaam 637, Yung
// Yaam 636. Completed: Ruslan 10011 one time in three (math.randomInt); while needToZone holds: Aramu-Paramu 684,
// Jatan-Paratan 639, Yuli Yaam 641, Yung Yaam 643.
// Ruslan's completed line draws the script RNG once per talk (Player.RandomInt is math.randomInt), as the Lua does.
// Kept as the Lua file has it: 633's other options and 634's other options change nothing; with a full bag 638's
// completion fails and nothing else happens (the lumber kept: 638 plays again on a new trade); tradeMatches wants
// exactly one lumber and nothing else.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class WonderingMinstrel : QuestScript
    {
        public WonderingMinstrel() : base(QuestLog.Windurst, "WONDERING_MINSTREL") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Wondering_Minstrel";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 50,
                FameArea = FameArea.Windurst,
                Item = Item.FaeriePiccolo,
                Title = Title.DownPiperPipeUpperer,
            });

            // Section: Quest is available.
            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.Windurst) >= 5)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.JatanParatan).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(633); // Asks what the player made of his playing. Correct option neded to trigger next event.
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(634); // Offers the quest
                    }
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.YuliYaam).OnTrigger(QuestReply.Event(637))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.YungYaam).OnTrigger(QuestReply.Event(636))
                .OnEventFinish(Zone.WindurstWaters, 633, q =>
                {
                    if (q.Option == 3)
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 634, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Ruslan).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(10009, 0, Item.PieceOfRosewoodLumber); // Asks for the lumber.
                    }
                    else
                    {
                        return QuestReply.Event(10010, 0, Item.PieceOfRosewoodLumber); // Rminder.
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 10009, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetStep(2);
                    }
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.JatanParatan)
                    .OnTrade(q =>
                    {
                        // The lumber means nothing to him until Ruslan has explained what it is for.
                        if (
                            q.Quest.Step == 2 &&
                            q.Trade.Matches((Item.PieceOfRosewoodLumber, 1)))
                        {
                            return QuestReply.ProgressEvent(638); // Finishes the quest
                        }

                        return null;
                    })
                    .OnTrigger(q => QuestReply.Event(635))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.AramuParamu).OnTrigger(QuestReply.Event(683))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.YuliYaam).OnTrigger(QuestReply.Event(637))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.YungYaam).OnTrigger(QuestReply.Event(636))
                .OnEventFinish(Zone.WindurstWaters, 638, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        q.Player.SetNeedToZone(true);
                    }
                });

            // Section: Quest completed.
            b.State(QuestStatus.Completed)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Ruslan).OnTrigger(q =>
                {
                    if (q.Player.RandomInt(1, 3) == 1)
                    {
                        return QuestReply.Event(10011);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.AramuParamu).OnTrigger(q =>
                {
                    if (q.Player.NeedToZone)
                    {
                        return QuestReply.Event(684);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.JatanParatan).OnTrigger(q =>
                {
                    if (q.Player.NeedToZone)
                    {
                        return QuestReply.Event(639);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.YuliYaam).OnTrigger(q =>
                {
                    if (q.Player.NeedToZone)
                    {
                        return QuestReply.Event(641);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.YungYaam).OnTrigger(q =>
                {
                    if (q.Player.NeedToZone)
                    {
                        return QuestReply.Event(643);
                    }

                    return null;
                });
        }
    }
}
