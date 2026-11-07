// The Old Man and the Harpoon (Log ID 9, Quest ID 77), ported from the quest branches of
// scripts/zones/Western_Adoulin/npcs/Jorin.lua and Shipilolo.lua (quest rework B3b Adoulin group 6; call-only: the
// scripts call this quest). Jorin : !pos 92 32 152 256; Shipilolo : !pos 84 0 -60 256.
// Jorin: available: 2540, whose finish adds the quest and gives the Broken Harpoon; accepted: with the
// Extravagant Harpoon 2542, else 2541. 2542's finish: the raw player:completeQuest, 500 exp and 300 bayld (at the
// rates), BAYLD_OBTAINED, the Extravagant Harpoon taken, 30 Adoulin fame. Shipilolo (inside his script's SoA test:
// Life on the Frontier or later): accepted with the Broken Harpoon: 2543, whose finish takes it and gives the
// Extravagant Harpoon.
// Kept as the scripts have it: the raw completeQuest with its own rewards; the SoA test stays in Shipilolo's script;
// the finishes read no status.
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class TheOldManAndTheHarpoon : QuestScript
    {
        public TheOldManAndTheHarpoon() : base(QuestLog.Adoulin, "THE_OLD_MAN_AND_THE_HARPOON") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Jorin).Called().OnTrigger(q =>
                {
                    QuestStatus tomath = q.Quest.Status;

                    if (tomath == QuestStatus.Accepted)
                    {
                        if (q.Player.HasKeyItem(KeyItem.ExtravagantHarpoon))
                        {
                            // Finishing Quest: 'The Old Man and the Harpoon'
                            return QuestReply.Event(2542);
                        }
                        else
                        {
                            // Dialgoue during Quest: 'The Old Man and the Harpoon'
                            return QuestReply.Event(2541);
                        }
                    }
                    else if (tomath == QuestStatus.Available)
                    {
                        // Starts Quest: 'The Old Man and the Harpoon'
                        return QuestReply.Event(2540);
                    }

                    return null;
                })
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Shipilolo).Called().OnTrigger(q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted && q.Player.HasKeyItem(KeyItem.BrokenHarpoon))
                    {
                        // Progresses Quest: 'The Old Man and the Harpoon'
                        return QuestReply.Event(2543);
                    }

                    return null;
                });

            // the finishes, keyed on the csid alone as the scripts' are
            b.AnyState()
                // Jorin's
                .OnEventFinish(Zone.WesternAdoulin, 2540, q =>
                {
                    // Starting Quest: 'The Old Man and the Harpoon'
                    q.Quest.Begin();
                    q.Player.GiveKeyItem(KeyItem.BrokenHarpoon);
                })
                .OnEventFinish(Zone.WesternAdoulin, 2542, q =>
                {
                    // Finishing Quest: 'The Old Man and the Harpoon'
                    q.Player.CompleteQuest(QuestLog.Adoulin, "THE_OLD_MAN_AND_THE_HARPOON");
                    q.Player.AddExp(RateAmount(500, q.Settings.MainNumber("EXP_RATE")));
                    int bayld = RateAmount(300, q.Settings.MainNumber("BAYLD_RATE"));
                    q.Player.AddCurrency("bayld", bayld);
                    q.Text.Special(Text.WesternAdoulin.BAYLD_OBTAINED, bayld);
                    q.Player.DelKeyItem(KeyItem.ExtravagantHarpoon);

                    // TODO: Verify fame value added
                    q.Player.AddFame(FameArea.Adoulin, 30);
                })
                // Shipilolo's
                .OnEventFinish(Zone.WesternAdoulin, 2543, q =>
                {
                    // Progresses Quest: 'The Old Man and the Harpoon'
                    q.Player.DelKeyItem(KeyItem.BrokenHarpoon);
                    q.Player.GiveKeyItem(KeyItem.ExtravagantHarpoon);
                });
        }

        // n * rate as the engine's bindings read a Lua number into an integer argument (llround: half away from zero)
        private static int RateAmount(int n, double rate) => (int)Math.Round(n * rate, MidpointRounding.AwayFromZero);
    }
}
