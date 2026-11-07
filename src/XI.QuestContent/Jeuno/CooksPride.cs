// Cook's Pride (Log ID 3, Quest ID 16), ported from the Cook's Pride branches of scripts/zones/Lower_Jeuno/npcs/Naruru.lua
// and Teigero-Bangero.lua (quest rework B3b, Jeuno group 17; call-only: each script now calls this quest at the
// branches' spot). Naruru : !pos -56 0.1 -138 245; Teigero-Bangero : !pos -58 0 -143 245.
// Naruru, her first arms: The Wonder Magic Set completed and this quest available: CooksPrideVar 0: 189 (long), else 188
// (short) (their finish, option 0: begun; 189's option 1: the var 1); accepted without the Super Soup Pot: 186; the pot
// held (any status): 187 (its finish: no free slot: ITEM_CANNOT_BE_OBTAINED; else Mercy Errand Runner, the pot taken,
// the var 0, 3000 gil (npcUtil.giveCurrency), a Mythril Ring (addItem, ITEM_OBTAINED), fame +13 in San d'Oria, Bastok and
// Windurst, completed). Then The Lost Cardian's and The Kind Cardian's arms (their own C#), then her 98.
// Teigero-Bangero, after The Wonder Magic Set's arm: this quest not completed: math.randomInt(1, 2): 1 75, else 32.
// The Super Soup Pot is Hoist the Jelly, Roger's reward (Windurst Waters Maysoon, Windurst group 20: it reads this quest
// accepted).
// Kept as the Lua has it: 187 plays whenever the pot is held, in any status; 188's option 1 does nothing (189's sets the
// var); no finish reads the status; Teigero-Bangero's draw happens for any status but completed (available too, once The
// Wonder Magic Set is begun).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class CooksPride : QuestScript
    {
        const string Var = "CooksPrideVar";

        public CooksPride() : base(QuestLog.Jeuno, "COOKS_PRIDE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Naruru.lua onTrigger: The Wonder Magic Set completed, available, the var 0: Start quest "Cook's pride" Long CS
            b.State(QuestStatus.Available, when: q => TheLostCardian.WonderMagicSetCompleted(q) && q.Player.CharVar(Var) == 0)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Naruru).Called().OnTrigger(QuestReply.Event(189));

            // else: Start quest "Cook's pride" Short CS
            b.State(QuestStatus.Available, when: TheLostCardian.WonderMagicSetCompleted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Naruru).Called().OnTrigger(QuestReply.Event(188));

            // accepted, no pot: During quest "Cook's pride"
            b.State(QuestStatus.Accepted, when: q => !q.Player.HasKeyItem(KeyItem.SuperSoupPot))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Naruru).Called().OnTrigger(QuestReply.Event(186));

            // the pot held (any status): Finish quest "Cook's pride"
            b.AnyState(when: q => q.Player.HasKeyItem(KeyItem.SuperSoupPot))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Naruru).Called().OnTrigger(QuestReply.Event(187));

            // Teigero-Bangero.lua onTrigger: not completed: the draw
            b.AnyState(when: q => q.Quest.Status != QuestStatus.Completed)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.TeigeroBangero).Called().OnTrigger(q =>
                {
                    int rand = q.Player.RandomInt(1, 2);

                    if (rand == 1)
                    {
                        return QuestReply.Event(75); // During Panta and Naruru Quests
                    }
                    else
                    {
                        return QuestReply.Event(32); // Same
                    }
                });

            b.AnyState()
                // Naruru.lua onEventFinish 189 / 188 / 187
                .OnEventFinish(Zone.LowerJeuno, 189, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                    else if (q.Option == 1)
                    {
                        q.Player.SetCharVar(Var, 1);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 188, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 187, q =>
                {
                    if (q.Player.FreeSlotCount == 0)
                    {
                        q.Text.Special(Text.LowerJeuno.ITEM_CANNOT_BE_OBTAINED, Item.MythrilRing);
                    }
                    else
                    {
                        q.Player.AddTitle(Title.MercyErrandRunner);
                        q.Player.DelKeyItem(KeyItem.SuperSoupPot);
                        q.Player.SetCharVar(Var, 0);
                        q.Player.GiveCurrency("gil", 3000);
                        q.Player.AddItem(Item.MythrilRing);
                        q.Text.Special(Text.LowerJeuno.ITEM_OBTAINED, Item.MythrilRing);
                        q.Player.AddFame(FameArea.Sandoria, 13);
                        q.Player.AddFame(FameArea.Bastok, 13);
                        q.Player.AddFame(FameArea.Windurst, 13);
                        // the raw player:completeQuest, which keeps the quest's own Quest[3][16] vars
                        q.Player.CompleteQuest(QuestLog.Jeuno, "COOKS_PRIDE");
                    }
                });
        }
    }
}
