// A Pioneer's Best (Imaginary) Friend (Log ID 9, Quest ID 75), ported from the quest branches of
// scripts/zones/Western_Adoulin/npcs/Merleg.lua and Ruth.lua (quest rework B3b Adoulin group 7; call-only: each script
// calls this quest where its quest branch was; Ruth's inside her script's SoA test, Life on the Frontier or later).
// Merleg !pos 34 0 -131 256 (default 543), Ruth !pos -144 4 -10 256 (default 503; Pioneer Registration's completed
// section answers 590 there).
// Merleg: available: 2520, whose finish adds the quest and, with Ionis already on, starts 2522 itself; accepted: with
// Ionis 2522, else 2521. 2522's finish: the raw player:completeQuest, 500 exp (at EXP_RATE), 200 bayld
// (npcUtil.giveCurrency, which applies BAYLD_RATE), the F.A.I.L. Badge, 30 Adoulin fame. Ruth: accepted without Ionis:
// 2523, whose finish removes the influence effects (silently) and gives Ionis for 9000 s.
// Kept as the scripts have it: the raw completeQuest with its own rewards (the fame's TODO); 2522 started from 2520's
// finish (that event's finish comes back through Merleg's script); the finishes read no status.
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class APioneersBestImaginaryFriend : QuestScript
    {
        public APioneersBestImaginaryFriend() : base(QuestLog.Adoulin, "A_PIONEERS_BEST_IMAGINARY_FRIEND") { }

        // xi.effect.IONIS (scripts/enum/effect.codegen.lua)
        private const int Ionis = 512;
        // xi.effectFlag.INFLUENCE (scripts/enum/effect_flag.codegen.lua)
        private const int InfluenceFlag = 0x01000000;

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Merleg).Called().OnTrigger(q =>
                {
                    QuestStatus apbif = q.Quest.Status;
                    if (apbif == QuestStatus.Accepted)
                    {
                        if (q.Player.HasStatusEffect(Ionis))
                        {
                            // Finishing Quest: 'A Pioneers Best (Imaginary) Friend'
                            return QuestReply.Event(2522);
                        }
                        else
                        {
                            // Dialgoue during Quest: 'A Pioneers Best (Imaginary) Friend'
                            return QuestReply.Event(2521);
                        }
                    }
                    else if (apbif == QuestStatus.Available)
                    {
                        return QuestReply.Event(2520);
                    }

                    return null;
                })
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Ruth).Called().OnTrigger(q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted && !q.Player.HasStatusEffect(Ionis))
                    {
                        // Progresses Quest: 'A Pioneers Best (Imaginary) Friend'
                        return QuestReply.Event(2523);
                    }

                    return null;
                });

            // the finishes, keyed on the csid alone as the scripts' are
            b.AnyState()
                // Merleg's
                .OnEventFinish(Zone.WesternAdoulin, 2520, q =>
                {
                    // Starting Quest: 'A Pioneers Best (Imaginary) Friend'
                    q.Quest.Begin();
                    if (q.Player.HasStatusEffect(Ionis))
                    {
                        // Finishes quest if Ionis is already active
                        q.Player.StartEvent(2522);
                    }
                })
                .OnEventFinish(Zone.WesternAdoulin, 2522, q =>
                {
                    // Finishing Quest: 'A Pioneers Best (Imaginary) Friend'
                    q.Player.CompleteQuest(QuestLog.Adoulin, "A_PIONEERS_BEST_IMAGINARY_FRIEND");
                    q.Player.AddExp(RateAmount(500, q.Settings.MainNumber("EXP_RATE")));
                    q.Player.GiveCurrency("bayld", 200);
                    q.Player.GiveKeyItem(KeyItem.FailBadge);

                    // TODO: Verify fame value added
                    q.Player.AddFame(FameArea.Adoulin, 30);
                })
                // Ruth's
                .OnEventFinish(Zone.WesternAdoulin, 2523, q =>
                {
                    // Progresses Quest: 'A Pioneers Best (Imaginary) Friend'
                    q.Player.DelStatusEffectsByFlag(InfluenceFlag, true);
                    q.Player.AddStatusEffectForDuration(Ionis, 9000);
                });
        }

        // n * rate as the engine's binding reads a Lua number into an integer argument (llround: half away from zero)
        private static int RateAmount(int n, double rate) => (int)Math.Round(n * rate, MidpointRounding.AwayFromZero);
    }
}
