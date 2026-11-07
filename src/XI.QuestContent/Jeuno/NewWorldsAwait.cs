// New Worlds Await (Log ID 3, Quest ID 133), a line-for-line port of scripts/quests/jeuno/LB06_New_Worlds_Await.lua
// (quest rework B3b). Nomad Moogle : !pos 10.012 1.453 121.883 243. Event 10045 is the global event of limit breaks 6-10:
// level, Limit Breaker held (1) or not (2), the quest number, its stage. At level cap 75 the moogle plays 10045 (quest 1
// at level 75 with the Limit Breaker when MAX_LEVEL is over 75; else 0); its update with option 4 answers a fixed reply;
// option 4 gives the Limit Breaker, option 5 begins it. Accepted: 10045 (1, 1) as a reminder; exactly five Kindred's
// Seals with more than 2 merit points play 10135: completed, the seals taken, 3 merit points spent, level cap 80,
// YOUR_LEVEL_LIMIT_IS_NOW_80. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class NewWorldsAwait : QuestScript
    {
        public NewWorldsAwait() : base(QuestLog.Jeuno, "NEW_WORLDS_AWAIT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/LB06_New_Worlds_Await";

        // Event 10045 is the global event used in all Limit break quest from 6 to 10.
        // Parameter 1 -> player level.
        // Parameter 2 -> player has the Limit Breaker KI (1) or not (2)
        // Parameter 3 -> "ID/number" of current LB quest associated with this npc. LB-6 = 1, LB-7 = 2, LB-8 = 3...
        // Parameter 4 -> progress in current quest.

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            // Section: Quest accepted. There is no quest pre-requisite.
            b.State(QuestStatus.Available, when: q =>
                    q.Player.LevelCap == 75)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.NomadMoogle).OnTrigger(q =>
                {
                    int playerLevel = q.Player.MainLevel;
                    int limitBreaker = q.Player.HasKeyItem(KeyItem.LimitBreaker) ? 1 : 2;
                    int lastQuestNumber = 0;
                    int lastQuestStage = 0;
                    if (q.Settings.MainNumber("MAX_LEVEL") > 75 && // Can we level over 75?
                        playerLevel == 75 && // Are we at limit level?
                        limitBreaker == 1) // Do we have limit breaker KI? (Pre-requisite)
                    {
                        lastQuestNumber = 1;
                    }

                    return QuestReply.ProgressEvent(10045, playerLevel, limitBreaker, lastQuestNumber, lastQuestStage);
                })
                .OnEventUpdate(Zone.RuludeGardens, 10045, q =>
                {
                    if (q.Option == 4)
                    {
                        q.Player.UpdateEvent(243, 2, 3, 1, 17154, 0, 235339776, 4); // I've taken 3 different captures and they always seem different.
                    }
                })
                .OnEventFinish(Zone.RuludeGardens, 10045, q =>
                {
                    // Obtain Limit Breaker KI option.
                    if (q.Option == 4)
                    {
                        q.Player.GiveKeyItem(KeyItem.LimitBreaker);
                    }

                    // Accept LB6 quest option.
                    else if (q.Option == 5)
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.NomadMoogle).OnTrade(q =>
                {
                    if (q.Trade.HasExactly((Item.KindredsSeal, 5)) &&
                        q.Player.MeritCount > 2)
                    {
                        return QuestReply.ProgressEvent(10135);
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    int playerLevel = q.Player.MainLevel;
                    int limitBreaker = q.Player.HasKeyItem(KeyItem.LimitBreaker) ? 1 : 2;
                    int lastQuestNumber = 1;
                    int lastQuestStage = 1;

                    return QuestReply.Event(10045, playerLevel, limitBreaker, lastQuestNumber, lastQuestStage); // Reminder.
                })
                .OnEventFinish(Zone.RuludeGardens, 10135, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        q.Player.SetMerits(q.Player.MeritCount - 3);
                        q.Player.SetLevelCap(80);
                        q.Text.Special(Text.RuludeGardens.YOUR_LEVEL_LIMIT_IS_NOW_80);
                    }
                });
        }
    }
}
