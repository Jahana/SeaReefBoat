// The Medicine Woman (Log ID 0, Quest ID 30), a line-for-line port of scripts/quests/sandoria/The_Medicine_Woman.lua (quest
// rework B3b, San d'Oria group 2). Abeaule : !pos -136 -2 56 231; Amaura : !pos -85 -6 89 230. Offered once The Trader in
// the Forest is completed, at San d'Oria fame level 3: 613 (Prog 0; option 0 begins it, any other option sets Prog 1),
// then 615 (option 0 begins it and clears Prog). Accepted: Abeaule's ABEAULE_DIALOG_HOME, or 614 with the Cold Medicine
// (completed: fame +30, 2100 gil, Traveling Medicine Man; the medicine deleted). Amaura (Southern San d'Oria): 636 with
// neither key item (option 0: Amaura's Formula), AMAURA_DIALOG_COMEBACK with the formula, AMAURA_DIALOG_DELIVER with the
// medicine; with the formula exactly a Malboro Vine, a Chunk of Zinc Ore and an Insect Wing play 637 (the trade
// confirmed, the formula deleted, the Cold Medicine). The scripts of Antreneau, Diary and Nenne read the status (stay).
// The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class TheMedicineWoman : QuestScript
    {
        public TheMedicineWoman() : base(QuestLog.Sandoria, "THE_MEDICINE_WOMAN") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/The_Medicine_Woman";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 30, FameArea = FameArea.Sandoria, Gil = 2100, Title = Title.TravelingMedicineMan });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Sandoria, "THE_TRADER_IN_THE_FOREST") &&
                    q.Player.FameLevel(FameArea.Sandoria) >= 3)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Abeaule).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(613);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(615);
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 613, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                    else
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 615, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                        q.Quest.SetStep(0);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Abeaule).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.ColdMedicine))
                    {
                        return QuestReply.ProgressEvent(614);
                    }
                    else
                    {
                        return QuestReply.MessageText(Text.NorthernSanDoria.ABEAULE_DIALOG_HOME);
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 614, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.ColdMedicine);
                    }
                })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Amaura)
                    .OnTrade(q =>
                    {
                        if (q.Player.HasKeyItem(KeyItem.AmaurasFormula) &&
                            // npcUtil.tradeHasExactly(trade, { xi.item.MALBORO_VINE, xi.item.CHUNK_OF_ZINC_ORE, xi.item.INSECT_WING })
                            q.Trade.HasExactly((Item.MalboroVine, 1), (Item.ChunkOfZincOre, 1), (Item.InsectWing, 1)))
                        {
                            return QuestReply.ProgressEvent(637);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        bool hasFormula = q.Player.HasKeyItem(KeyItem.AmaurasFormula);
                        bool hasColdMedicine = q.Player.HasKeyItem(KeyItem.ColdMedicine);

                        if (!hasFormula &&
                            !hasColdMedicine)
                        {
                            return QuestReply.ProgressEvent(636);
                        }
                        else if (hasFormula)
                        {
                            return QuestReply.MessageText(Text.SouthernSanDoria.AMAURA_DIALOG_COMEBACK);
                        }
                        else if (hasColdMedicine)
                        {
                            return QuestReply.MessageText(Text.SouthernSanDoria.AMAURA_DIALOG_DELIVER);
                        }
                        return null;
                    })
                .OnEventFinish(Zone.SouthernSanDoria, 636, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.GiveKeyItem(KeyItem.AmaurasFormula);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 637, q =>
                {
                    q.Trade.Confirm();
                    q.Player.DelKeyItem(KeyItem.AmaurasFormula);
                    q.Player.GiveKeyItem(KeyItem.ColdMedicine);
                });
        }
    }
}
