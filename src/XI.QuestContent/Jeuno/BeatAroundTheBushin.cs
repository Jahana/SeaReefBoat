// Beat Around the Bushin (Log ID 3, Quest ID 67), ported from the Beat Around the Bushin branches of
// scripts/zones/Lower_Jeuno/npcs/Vola.lua and _6tc.lua (the door "Neptune's Spire") (quest rework B3b, Jeuno group 18;
// call-only: each script now calls this quest at the branches' spot). Vola : !pos 43 3 -45 245; _6tc : !pos 35 0 -15 245.
// Vola, after Fistful of Fury's arms (its own C#): available, MNK, main level 71 or more, Norg fame level 6 or more: 160
// (its finish, still available: BeatAroundTheBushin 1); not available: 214. Else her 212.
// The door _6tc: the var 1: 155 (its finish: begun, the var 2). Else (his script) the Tenshodo Members Card: 105 (the door
// opens), or ITS_LOCKED. Its trade, accepted, one item and nothing else: a Wyrm Beard with the var 2: 156 (its finish:
// the var 3, the trade taken); a Behemoth Tongue with 4: 157 (5); an Adamantoise Egg with 6: 158 (7); a Brown Belt with
// 7: 159 (its finish: no free slot: ITEM_CANNOT_BE_OBTAINED; else Black Belt (the title), a Black Belt (addItem,
// ITEM_OBTAINED), the var 0, Norg fame +60, the trade taken, completed).
// The var's 3 -> 4 (Bastok Markets Degenhard) and 5 -> 6 (Ru'Lude Gardens Maat) never name the quest: they stay in Lua.
// Kept as the Lua has it: the door's 155 reads the var alone (no status: a var 1 begins the quest again when completed,
// which sets its current bit and leaves it completed); 214 plays for accepted and completed alike; 160's finish sets the
// var on any option; 155-159's finishes read no status.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class BeatAroundTheBushin : QuestScript
    {
        const string Var = "BeatAroundTheBushin";

        public BeatAroundTheBushin() : base(QuestLog.Jeuno, "BEAT_AROUND_THE_BUSHIN") { }

        // _6tc.lua onTrade, accepted with the var at the arm's value: the item alone (trade:getItemCount() == 1)
        static QuestReply TradeOf(IQuestContext q, int item, int csid) =>
            q.Trade.HasItemQty(item, 1) && q.Trade.ItemCount == 1 ? QuestReply.Event(csid) : null;

        protected override void Build(QuestScriptBuilder b)
        {
            // Vola.lua onTrigger, after Fistful of Fury's arms: available, MNK, level 71 or more, Norg fame level 6 or more:
            // Start Quest "Beat Around the Bushin"
            b.State(QuestStatus.Available, when: q => q.Player.MainJob == Job.Mnk &&
                                                      q.Player.MainLevel >= 71 &&
                                                      q.Player.FameLevel(FameArea.Norg) >= 6)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Vola).Called().OnTrigger(QuestReply.Event(160));

            // not available: During & After Quest "Beat Around the Bushin"
            b.AnyState(when: q => q.Quest.Status != QuestStatus.Available)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Vola).Called().OnTrigger(QuestReply.Event(214));

            // _6tc.lua onTrade, accepted: the Wyrm Beard with the var 2: After trade Wyrm Beard
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar(Var) == 2)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno._6tc).Called().OnTrade(q => TradeOf(q, Item.WyrmBeard, 156));

            // the Behemoth Tongue with the var 4: After trade Behemoth Tongue
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar(Var) == 4)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno._6tc).Called().OnTrade(q => TradeOf(q, Item.BehemothTongue, 157));

            // the Adamantoise Egg with the var 6: After trade Adamantoise Egg
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar(Var) == 6)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno._6tc).Called().OnTrade(q => TradeOf(q, Item.AdamantoiseEgg, 158));

            // the Brown Belt with the var 7: After trade Brown Belt, Finish Quest "Beat around the Bushin"
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar(Var) == 7)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno._6tc).Called().OnTrade(q => TradeOf(q, Item.BrownBelt, 159));

            // _6tc.lua onTrigger, the var 1 (any status): Start Quest "Beat around the Bushin" (the door's 105 /
            // ITS_LOCKED stay in its script)
            b.AnyState(when: q => q.Player.CharVar(Var) == 1)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno._6tc).Called().OnTrigger(QuestReply.Event(155));

            // Vola.lua onEventFinish 160, still available: For the next quest "Beat around the Bushin"
            b.State(QuestStatus.Available)
                .OnEventFinish(Zone.LowerJeuno, 160, q => q.Player.SetCharVar(Var, 1));

            b.AnyState()
                // _6tc.lua onEventFinish 155-159
                .OnEventFinish(Zone.LowerJeuno, 155, q =>
                {
                    q.Quest.Begin();
                    q.Player.SetCharVar(Var, 2);
                })
                .OnEventFinish(Zone.LowerJeuno, 156, q =>
                {
                    q.Player.SetCharVar(Var, 3);
                    q.Trade.Complete();
                })
                .OnEventFinish(Zone.LowerJeuno, 157, q =>
                {
                    q.Player.SetCharVar(Var, 5);
                    q.Trade.Complete();
                })
                .OnEventFinish(Zone.LowerJeuno, 158, q =>
                {
                    q.Player.SetCharVar(Var, 7);
                    q.Trade.Complete();
                })
                .OnEventFinish(Zone.LowerJeuno, 159, q =>
                {
                    if (q.Player.FreeSlotCount == 0)
                    {
                        q.Text.Special(Text.LowerJeuno.ITEM_CANNOT_BE_OBTAINED, Item.BlackBelt);
                    }
                    else
                    {
                        q.Player.AddTitle(Title.BlackBelt);
                        q.Player.AddItem(Item.BlackBelt);
                        q.Text.Special(Text.LowerJeuno.ITEM_OBTAINED, Item.BlackBelt);
                        q.Player.SetCharVar(Var, 0);
                        q.Player.AddFame(FameArea.Norg, 60);
                        q.Trade.Complete();
                        // the raw player:completeQuest, which keeps the quest's own Quest[3][67] vars
                        q.Player.CompleteQuest(QuestLog.Jeuno, "BEAT_AROUND_THE_BUSHIN");
                    }
                });
        }
    }
}
