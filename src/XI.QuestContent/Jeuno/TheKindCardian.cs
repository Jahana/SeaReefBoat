// The Kind Cardian (Log ID 3, Quest ID 8), ported from the The Kind Cardian branches of
// scripts/zones/Lower_Jeuno/npcs/Panta-Putta.lua, Naruru.lua, Teigero-Bangero.lua, scripts/zones/Upper_Jeuno/npcs/
// Monberaux.lua and scripts/zones/Windurst_Woods/npcs/Apururu.lua (quest rework B3b, Jeuno group 17; call-only: each
// script now calls this quest at the branches' spot). Panta-Putta : !pos -61 0 -140 245; Naruru : !pos -56 0.1 -138 245;
// Teigero-Bangero : !pos -58 0 -143 245; Monberaux : !pos -43 0 -1 244; Apururu : !pos -11 -2 13 241.
// Begun by The Lost Cardian's completion (Monberaux's 33 / 34 finish: player:addQuest). Its var theKindCardianVar:
// Apururu (Windurst Woods), her first arms: accepted: the var 0: 392 (its finish, option 1: the var 1); 1: 393; 2: 398;
// any other var: nothing at all (no Can Cardians Cry?, no trust: the lookup then plays her 274). Her trade, accepted, a
// Ten of Cups card among the items (npcUtil.tradeHas): 397 (its finish: Two of Swords taken, the var 2, Windurst fame
// +30, the trade confirmed). Then her script's Can Cardians Cry? arms (Windurst group 22, still Lua) and her trust arm.
// Panta-Putta, after The Lost Cardian's arm: accepted and the var 2: 35 (its finish: no free slot:
// ITEM_CANNOT_BE_OBTAINED; else Bringer of Bliss, Two of Swords taken, the var 0, a Green Cape (addItem, ITEM_OBTAINED),
// fame +13 in San d'Oria, Bastok and Windurst, completed); completed: 76. Else her 78.
// Naruru, after The Lost Cardian's arm: Cook's Pride completed and this quest not completed: 71; completed: 72. Else her 98.
// Teigero-Bangero, last: accepted: 66; completed: 67. Else nothing (his script ends; the lookup's default 34).
// Monberaux, after The Lost Cardian's arms: The Lost Cardian completed and this quest accepted: 32.
// Kept as the Lua has it: Apururu's accepted arm with a var outside 0-2 plays nothing at all (no Can Cardians Cry?, no
// trust); 392 sets the var on option 1 only; 397's finish reads no status and takes Two of Swords, which 35 then takes
// again (already gone); Naruru's 71 plays for an accepted quest in any var; no finish reads the status.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class TheKindCardian : QuestScript
    {
        const string Var = "theKindCardianVar";

        public TheKindCardian() : base(QuestLog.Jeuno, "THE_KIND_CARDIAN") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Apururu.lua onTrigger: accepted, the var 0 / 1 / 2: 392 / 393 / 398
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar(Var) == 0)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Apururu).Called().OnTrigger(QuestReply.Event(392));
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar(Var) == 1)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Apururu).Called().OnTrigger(QuestReply.Event(393));
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar(Var) == 2)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Apururu).Called().OnTrigger(QuestReply.Event(398));

            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Apururu).Called()
                    // any other var: the arm is taken and plays nothing (no Can Cardians Cry?, no trust)
                    .OnTrigger(QuestReply.NoAction())
                    // Apururu.lua onTrade: accepted, the Ten of Cups card among the items
                    .OnTrade(q =>
                    {
                        if (q.Trade.Has(Item.TenOfCupsCard))
                        {
                            return QuestReply.Event(397);
                        }

                        return null; // Can Cardians Cry?'s arm next
                    });

            // Panta-Putta.lua onTrigger: accepted, the var 2: Finish quest "The kind cardien"
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar(Var) == 2)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.PantaPutta).Called().OnTrigger(QuestReply.Event(35));

            // completed: New standard dialog after "The kind cardien"
            b.State(QuestStatus.Completed)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.PantaPutta).Called().OnTrigger(QuestReply.Event(76));

            // Naruru.lua onTrigger: Cook's Pride completed, not completed: During quests "The kind cardien"
            b.AnyState(when: q => TheLostCardian.CooksPrideCompleted(q) && q.Quest.Status != QuestStatus.Completed)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Naruru).Called().OnTrigger(QuestReply.Event(71));

            // completed: New standard dialog after the quest "The kind cardien"
            b.State(QuestStatus.Completed)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Naruru).Called().OnTrigger(QuestReply.Event(72));

            // Teigero-Bangero.lua onTrigger: accepted: During quest "The kind cardien"; completed: New standard dialog
            b.State(QuestStatus.Accepted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.TeigeroBangero).Called().OnTrigger(QuestReply.Event(66));
            b.State(QuestStatus.Completed)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.TeigeroBangero).Called().OnTrigger(QuestReply.Event(67));

            // Monberaux.lua onTrigger: The Lost Cardian completed, accepted: 32
            b.State(QuestStatus.Accepted, when: q => q.Player.QuestStatus(QuestLog.Jeuno, "THE_LOST_CARDIAN") == QuestStatus.Completed)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Monberaux).Called().OnTrigger(QuestReply.Event(32));

            b.AnyState()
                // Apururu.lua onEventFinish 392 / 397
                .OnEventFinish(Zone.WindurstWoods, 392, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.SetCharVar(Var, 1);
                    }
                })
                .OnEventFinish(Zone.WindurstWoods, 397, q =>
                {
                    q.Player.DelKeyItem(KeyItem.TwoOfSwords);
                    q.Player.SetCharVar(Var, 2);
                    q.Player.AddFame(FameArea.Windurst, 30);
                    q.Trade.Confirm();
                })
                // Panta-Putta.lua onEventFinish 35
                .OnEventFinish(Zone.LowerJeuno, 35, q =>
                {
                    if (q.Player.FreeSlotCount == 0)
                    {
                        q.Text.Special(Text.LowerJeuno.ITEM_CANNOT_BE_OBTAINED, Item.GreenCape);
                    }
                    else
                    {
                        q.Player.AddTitle(Title.BringerOfBliss);
                        q.Player.DelKeyItem(KeyItem.TwoOfSwords);
                        q.Player.SetCharVar(Var, 0);
                        q.Player.AddItem(Item.GreenCape);
                        q.Text.Special(Text.LowerJeuno.ITEM_OBTAINED, Item.GreenCape); // Green Cape
                        q.Player.AddFame(FameArea.Sandoria, 13);
                        q.Player.AddFame(FameArea.Bastok, 13);
                        q.Player.AddFame(FameArea.Windurst, 13);
                        // the raw player:completeQuest, which keeps the quest's own Quest[3][8] vars
                        q.Player.CompleteQuest(QuestLog.Jeuno, "THE_KIND_CARDIAN");
                    }
                });
        }
    }
}
