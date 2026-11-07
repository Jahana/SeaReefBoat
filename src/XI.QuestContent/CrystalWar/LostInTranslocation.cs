// Lost in Translocation (Log ID 7, Quest ID 0), a line-for-line port of
// scripts/quests/crystalWar/Lost_in_Translocation.lua (quest rework B3b Crystal War, draft). Batallia Downs [S]:
// Thorben !pos 175.346 8.038 -419.244 (default 108). The Eldieme Necropolis [S]: Erik !pos 258.643 -33.249 99.901
// (default 1), Gravestone !pos 254.428 -32.999 20.001 (default NAMES_CARVED_ON_STONE), Sarcophagus_map_quest !pos
// 336.594 -33.500 -56.728 (default SARCOPHAGUS_SEALED). Available: Thorben: 103, whose finish begins it. Accepted:
// Thorben: the three map pieces: 107, whose finish completes it (2000 gil, 2000 exp, the Map of Grauberg) and takes
// the pieces; else Option 1: 106 (an Event); else one or two pieces: 105 (-> Option 1); else 104 (an Event). Erik /
// Gravestone / Sarcophagus_map_quest: without the left / middle / right piece: 3 / 4 / 5, whose finish gives it. Kept
// as the Lua has it: 103 begins on any option; the pieces are given at any Prog; Option is set once (105) and 106
// answers from then on until all three; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class LostInTranslocation : QuestScript
    {
        public LostInTranslocation() : base(QuestLog.CrystalWar, "LOST_IN_TRANSLOCATION") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/Lost_in_Translocation";

        // the Lua's mapKeyItems
        static readonly int[] MapKeyItems =
        {
            KeyItem.LeftMapPiece,
            KeyItem.MiddleMapPiece,
            KeyItem.RightMapPiece,
        };

        // the Lua's getNumMapPieces
        static int GetNumMapPieces(IQuestContext q)
        {
            int numKeyItems = 0;

            foreach (int keyItem in MapKeyItems)
            {
                if (q.Player.HasKeyItem(keyItem))
                {
                    numKeyItems = numKeyItems + 1;
                }
            }

            return numKeyItems;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Gil = 2000,
                Exp = 2000,
                KeyItem = KeyItem.MapOfGrauberg,
            });

            b.State(QuestStatus.Available)
                .Npc(Zone.BatalliaDownsS, Npc.BatalliaDownsS.Thorben).OnTrigger(QuestReply.ProgressEvent(103))
                .OnEventFinish(Zone.BatalliaDownsS, 103, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BatalliaDownsS, Npc.BatalliaDownsS.Thorben).OnTrigger(q =>
                {
                    int numMapPieces = GetNumMapPieces(q);

                    if (numMapPieces == 3)
                    {
                        return QuestReply.ProgressEvent(107);
                    }
                    else if (q.Quest.Var("Option") == 1)
                    {
                        return QuestReply.Event(106);
                    }
                    else if (numMapPieces > 0)
                    {
                        return QuestReply.ProgressEvent(105);
                    }
                    else
                    {
                        return QuestReply.Event(104);
                    }
                })
                .OnEventFinish(Zone.BatalliaDownsS, 105, q =>
                {
                    q.Quest.SetVar("Option", 1);
                })
                .OnEventFinish(Zone.BatalliaDownsS, 107, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.LeftMapPiece);
                        q.Player.DelKeyItem(KeyItem.MiddleMapPiece);
                        q.Player.DelKeyItem(KeyItem.RightMapPiece);
                    }
                })
                .Npc(Zone.TheEldiemeNecropolisS, Npc.TheEldiemeNecropolisS.Erik).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.LeftMapPiece))
                    {
                        return QuestReply.ProgressEvent(3);
                    }
                    return null;
                })
                .Npc(Zone.TheEldiemeNecropolisS, Npc.TheEldiemeNecropolisS.Gravestone).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.MiddleMapPiece))
                    {
                        return QuestReply.ProgressEvent(4);
                    }
                    return null;
                })
                .Npc(Zone.TheEldiemeNecropolisS, Npc.TheEldiemeNecropolisS.Sarcophagusmapquest).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.RightMapPiece))
                    {
                        return QuestReply.ProgressEvent(5);
                    }
                    return null;
                })
                .OnEventFinish(Zone.TheEldiemeNecropolisS, 3, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.LeftMapPiece);
                })
                .OnEventFinish(Zone.TheEldiemeNecropolisS, 4, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.MiddleMapPiece);
                })
                .OnEventFinish(Zone.TheEldiemeNecropolisS, 5, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.RightMapPiece);
                });
        }
    }
}
