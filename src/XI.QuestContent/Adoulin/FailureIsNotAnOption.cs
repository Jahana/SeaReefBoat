// F.A.I.L.ure Is Not an Option (Log ID 9, Quest ID 29), ported from the quest branches of
// scripts/zones/Western_Adoulin/npcs/Clautaire.lua (quest rework B3b Adoulin group 7; call-only: the script calls this
// quest where its quest branch was). Clautaire !pos 44 4 -159 256 (default 545).
// Accepted: with the Hunk of Bedrock 76, else 77. Available, Adoulin fame level 4 or more, with the F.A.I.L. Badge
// (A Pioneer's Best (Imaginary) Friend's reward): 78, whose finish adds the quest. 76's finish: the Hunk taken, the raw
// player:completeQuest, 1000 exp and 500 bayld (at EXP_RATE and BAYLD_RATE), BAYLD_OBTAINED with that amount,
// 30 Adoulin fame.
// Kept as the script has it: nothing gives the Hunk of Bedrock (the quest's mining is not implemented), so 76 is a
// GM's; the raw completeQuest with its own rewards (the fame's TODO); the badge is kept; the finishes read no status.
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class FailureIsNotAnOption : QuestScript
    {
        public FailureIsNotAnOption() : base(QuestLog.Adoulin, "FAILURE_IS_NOT_AN_OPTION") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Clautaire).Called().OnTrigger(q =>
                {
                    QuestStatus finao = q.Quest.Status;
                    if (finao == QuestStatus.Accepted)
                    {
                        if (q.Player.HasKeyItem(KeyItem.HunkOfBedrock))
                        {
                            // Finishing Quest: 'F.A.I.L.ure Is Not an Option'
                            return QuestReply.Event(76);
                        }
                        else
                        {
                            // Dialgoue during Quest: 'F.A.I.L.ure Is Not an Option'
                            return QuestReply.Event(77);
                        }
                    }
                    else if (
                        finao == QuestStatus.Available &&
                        q.Player.FameLevel(FameArea.Adoulin) >= 4 &&
                        q.Player.HasKeyItem(KeyItem.FailBadge))
                    {
                        // Starting Quest: 'F.A.I.L.ure Is Not an Option'
                        return QuestReply.Event(78);
                    }

                    return null;
                });

            // the finishes, keyed on the csid alone as the script's are
            b.AnyState()
                .OnEventFinish(Zone.WesternAdoulin, 78, q =>
                {
                    // Starting Quest: 'F.A.I.L.ure Is Not an Option'
                    q.Quest.Begin();
                })
                .OnEventFinish(Zone.WesternAdoulin, 76, q =>
                {
                    // Finishing Quest: 'F.A.I.L.ure Is Not an Option'
                    q.Player.DelKeyItem(KeyItem.HunkOfBedrock);
                    q.Player.CompleteQuest(QuestLog.Adoulin, "FAILURE_IS_NOT_AN_OPTION");
                    q.Player.AddExp(RateAmount(1000, q.Settings.MainNumber("EXP_RATE")));
                    int bayld = RateAmount(500, q.Settings.MainNumber("BAYLD_RATE"));
                    q.Player.AddCurrency("bayld", bayld);
                    q.Text.Special(Text.WesternAdoulin.BAYLD_OBTAINED, bayld);

                    // TODO: Verify fame value added
                    q.Player.AddFame(FameArea.Adoulin, 30);
                });
        }

        // n * rate as the engine's binding reads a Lua number into an integer argument (llround: half away from zero)
        private static int RateAmount(int n, double rate) => (int)Math.Round(n * rate, MidpointRounding.AwayFromZero);
    }
}
