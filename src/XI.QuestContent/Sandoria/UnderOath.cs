// Under Oath (Log ID 0, Quest ID 92, PLD AF3), ported from the quest branches of
// scripts/zones/Chateau_dOraguille/npcs/_6h0.lua and scripts/zones/Southern_San_dOria/npcs/Vemalpeau.lua (quest rework
// B3b, San d'Oria group 14; call-only, with A Boy's Dream, which shares the door). _6h0 : !pos -38 -3 73 233;
// Vemalpeau : !pos 157 -1 66 230. The quest's state is the status and the char var UnderOathCS (not a quest var).
// The door _6h0, first in its chain: UnderOathCS 8 (any status): 89; else a PLD of main level AF2_QUEST_LEVEL with
// A Boy's Dream completed and this quest available: 90. 90 with option 1: begun, UnderOathCS 0. 89's finish: no free
// slot: ITEM_CANNOT_BE_OBTAINED (Gallant Surcoat); else the Gallant Surcoat (addItem), ITEM_OBTAINED, UnderOathCS 9,
// San d'Oria fame 20, the title Paragon of Paladin Excellence (setTitle), completed. (A Boy's Dream's 88 finish also
// begins this quest for a PLD.) Vemalpeau: accepted and UnderOathCS 0: 7 (finish: 1); UnderOathCS 3 with
// Mique's Paintbrush: 5 (finish: 4, the paintbrush taken); UnderOathCS 4 with the Strange Sheet of Paper: 3;
// UnderOathCS 9 with the Knight's Confession: 2 (finish: UnderOathCS 0, the confession taken); else his script's 1.
// The door keeps its own arms after this quest's: the Trion trust (574), A Boy's Dream's 88, rank 10's 62. Stay Lua
// (char var / key item only, they never name the quest): Najjar (1: 16 -> 2), Ullasa (2: 40 -> 3),
// Castle Zvahl Baileys' Treasure Coffer (3: the paintbrush), Southern San d'Oria's qm3 (4: 41, option 1: the sheet),
// Exoroche (4 with the sheet: 77 -> 5; 5: 79; 6 with the confession: 51; 8: 19), Davoi's Village Well (5: the two NMs;
// the Well Weight traded: 113 -> the confession, 6, the sheet taken; 6: 112), Ailbeche (6 with
// the confession: 59 -> 7; 8: 13), Jugner Forest's trigger area 1 (7: 14 -> 8). Read only: roe_records, Vingijard,
// the door's TrustMemory.
// Kept as the Lua has it: 89 and Vemalpeau's arms after the first read no status; 90 begins only on option 1; 89's
// finish checks the free slots itself, gives the surcoat with addItem and completes with the raw player:completeQuest
// (no quest:cleanup, so a Quest[0][92] var survives it); Vemalpeau's 3 changes nothing; his 2 (after 89 set 9) clears
// UnderOathCS to 0.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class UnderOath : QuestScript
    {
        const string Cs = "UnderOathCS";

        public UnderOath() : base(QuestLog.Sandoria, "UNDER_OATH") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // _6h0.lua onTrigger: the two Under Oath arms, first in the door's chain
            b.AnyState(when: q => q.Player.CharVar(Cs) == 8)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille._6h0).Called().OnTrigger(QuestReply.Event(89));
            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainJob == Job.Pld &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF2_QUEST_LEVEL") &&
                    q.Player.QuestStatus(QuestLog.Sandoria, "A_BOYS_DREAM") == QuestStatus.Completed)
                // start
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille._6h0).Called().OnTrigger(QuestReply.Event(90));

            // Vemalpeau.lua onTrigger: his quest arms (his else, 1, stays in his script)
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar(Cs) == 0)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Vemalpeau).Called()
                    .OnTrigger(QuestReply.Event(7)); // the boy missing
            b.AnyState(when: q => q.Player.CharVar(Cs) == 3 && q.Player.HasKeyItem(KeyItem.MiquesPaintbrush))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Vemalpeau).Called()
                    .OnTrigger(QuestReply.Event(5)); // upset about the paintbrush
            b.AnyState(when: q => q.Player.CharVar(Cs) == 4 && q.Player.HasKeyItem(KeyItem.StrangeSheetOfPaper))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Vemalpeau).Called()
                    .OnTrigger(QuestReply.Event(3)); // the commanding officer
            b.AnyState(when: q => q.Player.CharVar(Cs) == 9 && q.Player.HasKeyItem(KeyItem.KnightsConfession))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Vemalpeau).Called()
                    .OnTrigger(QuestReply.Event(2)); // thanks you and concludes the quest

            b.AnyState()
                // _6h0.lua onEventFinish 90, 89
                .OnEventFinish(Zone.ChateauDoraguille, 90, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Player.SetCharVar(Cs, 0);
                    }
                })
                .OnEventFinish(Zone.ChateauDoraguille, 89, q =>
                {
                    if (q.Player.FreeSlotCount == 0)
                    {
                        q.Text.Special(Text.ChateauDoraguille.ITEM_CANNOT_BE_OBTAINED, Item.GallantSurcoat);
                    }
                    else
                    {
                        q.Player.AddItem(Item.GallantSurcoat);
                        q.Text.Special(Text.ChateauDoraguille.ITEM_OBTAINED, Item.GallantSurcoat);
                        q.Player.SetCharVar(Cs, 9);
                        q.Player.AddFame(FameArea.Sandoria, 20);
                        q.Player.SetTitle(Title.ParagonOfPaladinExcellence);
                        // the raw player:completeQuest: no quest:cleanup, as the Lua
                        q.Player.CompleteQuest(QuestLog.Sandoria, "UNDER_OATH");
                    }
                })
                // Vemalpeau.lua onEventFinish 7, 5, 2
                .OnEventFinish(Zone.SouthernSanDoria, 7, q =>
                {
                    q.Player.SetCharVar(Cs, 1);
                })
                .OnEventFinish(Zone.SouthernSanDoria, 5, q =>
                {
                    q.Player.SetCharVar(Cs, 4);
                    q.Player.DelKeyItem(KeyItem.MiquesPaintbrush);
                })
                .OnEventFinish(Zone.SouthernSanDoria, 2, q =>
                {
                    q.Player.SetCharVar(Cs, 0);
                    q.Player.DelKeyItem(KeyItem.KnightsConfession);
                });
        }
    }
}
