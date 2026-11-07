// The Lost Cardian (Log ID 3, Quest ID 18), ported from the The Lost Cardian branches of
// scripts/zones/Lower_Jeuno/npcs/Panta-Putta.lua, Naruru.lua, Teigero-Bangero.lua and scripts/zones/Upper_Jeuno/npcs/
// Monberaux.lua (quest rework B3b, Jeuno group 17; call-only: each script now calls this quest at the branches' spot).
// Panta-Putta : !pos -61 0 -140 245; Naruru : !pos -56 0.1 -138 245; Teigero-Bangero : !pos -58 0 -143 245; Monberaux :
// !pos -43 0 -1 244.
// The quest is never begun: it goes from available to completed (Monberaux's finish). Its var theLostCardianVar:
// Teigero-Bangero, after The Wonder Magic Set's and Cook's Pride's arms: this quest available: the var 0: 29 (its finish:
// the var 1), else 66. Panta-Putta, after The Wonder Magic Set's arms: The Wonder Magic Set completed and this quest
// available: the var >= 1: 30 (its finish: the var 2), else 40. Naruru, after Cook's Pride's arms: Cook's Pride completed
// and The Kind Cardian available: the var 0: 31, else 71. Monberaux (his 109 introduction first, his trust arms and 28
// after: his script queues them): Cook's Pride completed, this quest available: the var 2: 33 (long), 3: 34 (short); their
// finish, option 0: Two's Company, the var 0, 2100 gil (npcUtil.giveCurrency), Two of Swords (npcUtil.giveKeyItem), fame
// +7 in San d'Oria, Bastok and Windurst, completed, The Kind Cardian begun (player:addQuest); 33's option 1: the var 3.
// A 33 queued behind his 109 (the first talk) finishes without reaching his script (the engine's queued event): nothing.
// Kept as the Lua has it: the quest is completed without ever being accepted; Naruru's 31 / 71 read The Kind Cardian's
// status and this quest's var, not this quest's status; 34's option 1 does nothing; no finish reads the status.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class TheLostCardian : QuestScript
    {
        const string Var = "theLostCardianVar";

        public TheLostCardian() : base(QuestLog.Jeuno, "THE_LOST_CARDIAN") { }

        // the group's status reads, shared with The Wonder Magic Set, Cook's Pride and The Kind Cardian
        internal static bool WonderMagicSetCompleted(IQuestCheck q) =>
            q.Player.QuestStatus(QuestLog.Jeuno, "THE_WONDER_MAGIC_SET") == QuestStatus.Completed;

        internal static bool CooksPrideCompleted(IQuestCheck q) =>
            q.Player.QuestStatus(QuestLog.Jeuno, "COOKS_PRIDE") == QuestStatus.Completed;

        // this quest's own read (Naruru's 31 / 71)
        static bool KindCardianAvailable(IQuestCheck q) =>
            q.Player.QuestStatus(QuestLog.Jeuno, "THE_KIND_CARDIAN") == QuestStatus.Available;

        protected override void Build(QuestScriptBuilder b)
        {
            // Panta-Putta.lua onTrigger: The Wonder Magic Set completed, available, the var >= 1: Second dialog for "The lost
            // cardien" quest
            b.State(QuestStatus.Available, when: q => WonderMagicSetCompleted(q) && q.Player.CharVar(Var) >= 1)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.PantaPutta).Called().OnTrigger(QuestReply.Event(30));

            // else: Standard dialog
            b.State(QuestStatus.Available, when: WonderMagicSetCompleted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.PantaPutta).Called().OnTrigger(QuestReply.Event(40));

            // Naruru.lua onTrigger: Cook's Pride completed, The Kind Cardian available, the var 0: During quests "The lost
            // cardian"
            b.AnyState(when: q => CooksPrideCompleted(q) && KindCardianAvailable(q) && q.Player.CharVar(Var) == 0)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Naruru).Called().OnTrigger(QuestReply.Event(31));

            // else: During quests "The lost cardian"
            b.AnyState(when: q => CooksPrideCompleted(q) && KindCardianAvailable(q))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Naruru).Called().OnTrigger(QuestReply.Event(71));

            // Teigero-Bangero.lua onTrigger: available, the var 0: First dialog for "The lost cardian" quest
            b.State(QuestStatus.Available, when: q => q.Player.CharVar(Var) == 0)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.TeigeroBangero).Called().OnTrigger(QuestReply.Event(29));

            // else: 66
            b.State(QuestStatus.Available)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.TeigeroBangero).Called().OnTrigger(QuestReply.Event(66));

            // Monberaux.lua onTrigger: Cook's Pride completed, available, the var 2: Long CS & Finish Quest "The Lost Cardian"
            b.State(QuestStatus.Available, when: q => CooksPrideCompleted(q) && q.Player.CharVar(Var) == 2)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Monberaux).Called().OnTrigger(QuestReply.Event(33));

            // the var 3: Shot CS & Finish Quest "The Lost Cardian"
            b.State(QuestStatus.Available, when: q => CooksPrideCompleted(q) && q.Player.CharVar(Var) == 3)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Monberaux).Called().OnTrigger(QuestReply.Event(34));

            b.AnyState()
                // Teigero-Bangero.lua onEventFinish 29
                .OnEventFinish(Zone.LowerJeuno, 29, q =>
                {
                    q.Player.SetCharVar(Var, 1);
                })
                // Panta-Putta.lua onEventFinish 30
                .OnEventFinish(Zone.LowerJeuno, 30, q =>
                {
                    q.Player.SetCharVar(Var, 2);
                })
                // Monberaux.lua onEventFinish 33 / 34
                .OnEventFinish(Zone.UpperJeuno, 33, q =>
                {
                    if (q.Option == 0)
                    {
                        Complete(q);
                    }
                    else if (q.Option == 1)
                    {
                        q.Player.SetCharVar(Var, 3);
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 34, q =>
                {
                    if (q.Option == 0)
                    {
                        Complete(q);
                    }
                });
        }

        // Monberaux.lua onEventFinish, (33 or 34) and option 0
        static void Complete(IQuestContext q)
        {
            q.Player.AddTitle(Title.TwosCompany);
            q.Player.SetCharVar(Var, 0);
            q.Player.GiveCurrency("gil", 2100);
            q.Player.GiveKeyItem(KeyItem.TwoOfSwords);
            q.Player.AddFame(FameArea.Sandoria, 7);
            q.Player.AddFame(FameArea.Bastok, 7);
            q.Player.AddFame(FameArea.Windurst, 7);
            // the raw player:completeQuest, which keeps the quest's own Quest[3][18] vars
            q.Player.CompleteQuest(QuestLog.Jeuno, "THE_LOST_CARDIAN");
            q.Player.AddQuest(QuestLog.Jeuno, "THE_KIND_CARDIAN"); // Start next quest "THE_KING_CARDIAN"
        }
    }
}
