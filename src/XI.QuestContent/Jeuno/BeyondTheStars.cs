// Beyond the Stars (Log ID 3, Quest ID 135), a line-for-line port of scripts/quests/jeuno/LB08_Beyond_the_Stars.lua (quest
// rework B3b). Nomad Moogle : !pos 10.012 1.453 121.883 243. With Expanding Horizons completed and level cap 85 the
// moogle plays 10045 (with the Limit Breaker and MAX_LEVEL over 85: quest 3 above level 80, quest 2 stage 2 from 75;
// else 0, 0); option 9 begins it. Accepted: exactly ten Kindred's Crests with more than 4 merit points play 10137 (the
// trade confirmed, 5 merit points spent, Prog 1); the moogle plays 10161 at Prog 1 (the rock-paper-scissors match: each
// update answers Maat's move from the option against Degenhard's random one; option 254 at the finish completes it:
// level cap 90, YOUR_LEVEL_LIMIT_IS_NOW_90), else 10045 (3, 1). Kept as the Lua has it: 10161's finish ignores
// quest:complete's result (the reward is empty: it cannot fail).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class BeyondTheStars : QuestScript
    {
        public BeyondTheStars() : base(QuestLog.Jeuno, "BEYOND_THE_STARS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/LB08_Beyond_the_Stars";

        // TODO: Properly code the rock, paper, scissors minigame. Awaiting for a capture.
        // Probably a matter of chaining onEventUpdates and tracking Maat's and Degengard's HP. Maybe not.
        // It seems that the event tracks HP on it's own.
        // Degengard's moves are selected at random.

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());
            b.Steps(1);

            // Section: Quest available.
            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Jeuno, "EXPANDING_HORIZONS") &&
                    q.Player.LevelCap == 85)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.NomadMoogle).OnTrigger(q =>
                {
                    int playerLevel = q.Player.MainLevel;
                    int limitBreaker = q.Player.HasKeyItem(KeyItem.LimitBreaker) ? 1 : 2;
                    int lastQuestNumber = 0;
                    int lastQuestStage = 0;
                    if (q.Settings.MainNumber("MAX_LEVEL") > 85 &&
                        limitBreaker == 1)
                    {
                        if (playerLevel > 80)
                        {
                            lastQuestNumber = 3;
                        }
                        else if (playerLevel >= 75)
                        {
                            lastQuestNumber = 2;
                            lastQuestStage = 2;
                        }
                    }

                    return QuestReply.ProgressEvent(10045, playerLevel, limitBreaker, lastQuestNumber, lastQuestStage);
                })
                .OnEventFinish(Zone.RuludeGardens, 10045, q =>
                {
                    if (q.Option == 9) // Accept quest option.
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.NomadMoogle).OnTrade(q =>
                {
                    if (q.Trade.HasExactly((Item.KindredsCrest, 10)) &&
                        q.Player.MeritCount > 4)
                    {
                        return QuestReply.ProgressEvent(10137);
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    int playerLevel = q.Player.MainLevel;
                    int limitBreaker = q.Player.HasKeyItem(KeyItem.LimitBreaker) ? 1 : 2;
                    int lastQuestNumber = 3;
                    int lastQuestStage = 1;

                    // Rock, Paper, Scissor Minigame.
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(10161, playerLevel, 0);
                    }

                    // Reminder
                    else
                    {
                        return QuestReply.Event(10045, playerLevel, limitBreaker, lastQuestNumber, lastQuestStage);
                    }
                })
                .OnEventUpdate(Zone.RuludeGardens, 10161, q =>
                {
                    // Maat move choice.
                    int maatMove = 0;
                    if (q.Option == 21)
                    {
                        maatMove = 0; // Red.
                    }
                    else if (q.Option == 22)
                    {
                        maatMove = 1; // Blue.
                    }
                    else if (q.Option == 23)
                    {
                        maatMove = 2; // Green.
                    }

                    // Degenhard move choice.
                    int degenhardMove = q.Player.RandomInt(0, 2);

                    // Rock-Paper-Scissors: Red beats Blue; Blue beats Green; Green beats Red.
                    int clashOutcome = 1; // Assume Degenhard wins
                    if (maatMove == degenhardMove)
                    {
                        clashOutcome = 2; // Draw
                    }
                    else if ((maatMove == 0 && degenhardMove == 1) ||
                             (maatMove == 1 && degenhardMove == 2) ||
                             (maatMove == 2 && degenhardMove == 0))
                    {
                        clashOutcome = 0;
                    }

                    int eventBitmask = maatMove + degenhardMove * 4 + clashOutcome * 16; // Controls moves chosen by Maat and Degenhard, and who wins the round.
                    q.Player.UpdateEvent(eventBitmask);
                })
                .OnEventFinish(Zone.RuludeGardens, 10137, q =>
                {
                    q.Trade.Confirm();
                    q.Player.SetMerits(q.Player.MeritCount - 5);
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.RuludeGardens, 10161, q =>
                {
                    if (q.Option == 254)
                    {
                        q.Quest.Complete();
                        q.Player.SetLevelCap(90);
                        q.Text.Special(Text.RuludeGardens.YOUR_LEVEL_LIMIT_IS_NOW_90);
                    }
                });
        }
    }
}
