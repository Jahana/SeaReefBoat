// Monstrosity: Suibhne Bee Guessing Game (hidden quest monstrosityBee), ported from
// scripts/quests/hiddenQuests/Monstrosity_Bee.lua (quest rework B3b hidden quests).
// Suibhne : !pos -366 -3.612 -466 285.
// One section (ENABLE_MONSTROSITY 1 and the Bee species not unlocked). Suibhne: Option 0: a quiz drawn
// (math.randomInt(1, 6)) and kept in Option; xi.monstrosity.suibhneGreet (his showText); 11 (1 when Timer <= today,
// else 0; the quiz - 1). 11's finish: Timer past today: nothing; the option 1 + the quiz's answers (each answer three
// bits from bit 16): quest:complete (no reward: Option and Timer cleared), then the Bee unlocked and
// MAY_POSSESS_BEES; option 2 (a wrong answer): Timer = tomorrow (the quiz locked for the day).
// Kept as the Lua has it: a locked quiz still plays 11 (0, quiz - 1), as retail sends it; quest:complete with no
// reward is always true (the else of `if quest:complete(player)` cannot run); another option (neither right nor 2)
// does nothing.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class MonstrosityBee : HiddenQuestScript
    {
        public MonstrosityBee() : base("monstrosityBee") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Monstrosity_Bee";

        // xi.monstrositySpecies.BEE (scripts/enum/monstrosity_species.codegen.lua:29)
        private const int SpeciesBee = 27;

        // choices
        private const int Aengus = 1;
        private const int Maccus = 2;
        private const int Suibhne = 3;
        private const int Teyrnon = 4;

        // answerKey[quiz] = { [0] = , [1] = , [2] = }
        private static readonly int[][] AnswerKey =
        {
            null,
            new[] { Teyrnon, Aengus,  Suibhne },
            new[] { Teyrnon, Suibhne, Aengus  },
            new[] { Aengus,  Teyrnon, Suibhne },
            new[] { Aengus,  Suibhne, Teyrnon },
            new[] { Suibhne, Aengus,  Teyrnon },
            new[] { Suibhne, Teyrnon, Aengus  },
        };

        // getCheckValue(choiceNum)
        private static int GetCheckValue(int choiceNum)
        {
            int checkValue = 0;
            int[] answers = AnswerKey[choiceNum];

            for (int fieldPos = 0; fieldPos < answers.Length; fieldPos++)
            {
                checkValue = checkValue + (answers[fieldPos] << (16 + 3 * fieldPos));
            }

            return checkValue;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q =>
                    q.Settings.MainEquals("ENABLE_MONSTROSITY", 1) &&
                    !q.Player.MonstrosityHasUnlockedSpecies(SpeciesBee))
                .Npc(Zone.Feretory, Npc.Feretory.Suibhne).OnTrigger(q =>
                {
                    int quizInfo = q.Quest.Var("Option");

                    if (quizInfo == 0)
                    {
                        quizInfo = q.Player.RandomInt(1, 6);
                        q.Quest.SetVar("Option", quizInfo);
                    }

                    q.Player.MonstrositySuibhneGreet();

                    // A wrong answer locks the quiz until the next Vana'diel day. Retail still sends the quiz.
                    int available = q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay ? 1 : 0;

                    return QuestReply.ProgressEvent(11, available, quizInfo - 1);
                })
                .OnEventFinish(Zone.Feretory, 11, q =>
                {
                    if (q.Quest.Var("Timer") > q.Player.VanadielUniqueDay)
                    {
                        return;
                    }

                    if (q.Option == 1 + GetCheckValue(q.Quest.Var("Option")))
                    {
                        if (q.Quest.Complete())
                        {
                            q.Player.MonstrosityUnlockSpecies(SpeciesBee);
                            q.Text.Special(Text.Feretory.MAY_POSSESS_BEES);
                        }
                    }
                    else if (q.Option == 2)
                    {
                        q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay + 1);
                    }
                });
        }
    }
}
