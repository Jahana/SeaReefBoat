// The Wonder Magic Set (Log ID 3, Quest ID 7), ported from the The Wonder Magic Set branches of
// scripts/zones/Lower_Jeuno/npcs/Panta-Putta.lua and Teigero-Bangero.lua (quest rework B3b, Jeuno group 17; call-only:
// each script now calls this quest at the branches' spot). Panta-Putta : !pos -61 0 -140 245; Teigero-Bangero : !pos -58
// 0 -143 245.
// Panta-Putta, her first arms: Jeuno fame level >= 4 and available: 77 (its finish, option 1: begun); accepted without
// the Wonder Magic Set: 55; the Wonder Magic Set held (any status): 33 (its finish: no free slot: ITEM_CANNOT_BE_OBTAINED;
// else Fool's Errand Runner, the key item taken, a Mythril Earring (addItem, ITEM_OBTAINED), fame +13 in San d'Oria,
// Bastok and Windurst, needToZone, completed); completed and Cook's Pride not completed: 40. Then The Lost Cardian's and
// The Kind Cardian's arms (their own C#), then her 78.
// Teigero-Bangero, his first arm: available: 34. Then Cook's Pride's, The Lost Cardian's and The Kind Cardian's arms.
// The key item comes from Child's Play (Port Jeuno Karl, Jeuno group 12): its own quest.
// Kept as the Lua has it: 33 plays whenever the key item is held, in any status; 77's finish reads no status and begins
// on option 1 only; 33's finish reads no status (it completes a quest that is not accepted when a GM gave the key item).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class TheWonderMagicSet : QuestScript
    {
        public TheWonderMagicSet() : base(QuestLog.Jeuno, "THE_WONDER_MAGIC_SET") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Panta-Putta.lua onTrigger: Jeuno fame level 4 or more and available: Start quest "The wonder magic set"
            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Jeuno) >= 4)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.PantaPutta).Called().OnTrigger(QuestReply.Event(77));

            // accepted, no key item: During quest "The wonder magic set"
            b.State(QuestStatus.Accepted, when: q => !q.Player.HasKeyItem(KeyItem.WonderMagicSet))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.PantaPutta).Called().OnTrigger(QuestReply.Event(55));

            // the key item held (any status): Finish quest "The wonder magic set"
            b.AnyState(when: q => q.Player.HasKeyItem(KeyItem.WonderMagicSet))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.PantaPutta).Called().OnTrigger(QuestReply.Event(33));

            // completed, Cook's Pride not completed: Standard dialog
            b.State(QuestStatus.Completed, when: q => !TheLostCardian.CooksPrideCompleted(q))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.PantaPutta).Called().OnTrigger(QuestReply.Event(40));

            // Teigero-Bangero.lua onTrigger: available: Base Standard CS & dialog
            b.State(QuestStatus.Available)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.TeigeroBangero).Called().OnTrigger(QuestReply.Event(34));

            b.AnyState()
                // Panta-Putta.lua onEventFinish 77 / 33
                .OnEventFinish(Zone.LowerJeuno, 77, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 33, q =>
                {
                    if (q.Player.FreeSlotCount == 0)
                    {
                        q.Text.Special(Text.LowerJeuno.ITEM_CANNOT_BE_OBTAINED, Item.MythrilEarring);
                    }
                    else
                    {
                        q.Player.AddTitle(Title.FoolsErrandRunner);
                        q.Player.DelKeyItem(KeyItem.WonderMagicSet);
                        q.Player.AddItem(Item.MythrilEarring);
                        q.Text.Special(Text.LowerJeuno.ITEM_OBTAINED, Item.MythrilEarring);
                        q.Player.AddFame(FameArea.Sandoria, 13);
                        q.Player.AddFame(FameArea.Bastok, 13);
                        q.Player.AddFame(FameArea.Windurst, 13);
                        q.Player.SetNeedToZone(true);
                        // the raw player:completeQuest, which keeps the quest's own Quest[3][7] vars
                        q.Player.CompleteQuest(QuestLog.Jeuno, "THE_WONDER_MAGIC_SET");
                    }
                });
        }
    }
}
