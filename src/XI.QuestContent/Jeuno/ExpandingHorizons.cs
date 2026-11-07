// Expanding Horizons (Log ID 3, Quest ID 134), a line-for-line port of scripts/quests/jeuno/LB07_Expanding_Horizons.lua
// (quest rework B3b). Nomad Moogle : !pos 10.012 1.453 121.883 243. With New Worlds Await completed and level cap 80 the
// moogle plays 10045 (with the Limit Breaker and MAX_LEVEL over 80: quest 2 above level 75, quest 1 stage 2 at 75; else
// 0, 0); option 7 begins it. Accepted: 10045 (2, 1) as a reminder; exactly five Kindred's Crests with more than 3 merit
// points play 10136: completed, the trade confirmed, 4 merit points spent, level cap 85, YOUR_LEVEL_LIMIT_IS_NOW_85. The
// Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class ExpandingHorizons : QuestScript
    {
        public ExpandingHorizons() : base(QuestLog.Jeuno, "EXPANDING_HORIZONS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/LB07_Expanding_Horizons";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            // Section: Quest available.
            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Jeuno, "NEW_WORLDS_AWAIT") &&
                    q.Player.LevelCap == 80)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.NomadMoogle).OnTrigger(q =>
                {
                    int playerLevel = q.Player.MainLevel;
                    int limitBreaker = q.Player.HasKeyItem(KeyItem.LimitBreaker) ? 1 : 2;
                    int lastQuestNumber = 0;
                    int lastQuestStage = 0;
                    if (q.Settings.MainNumber("MAX_LEVEL") > 80 &&
                        limitBreaker == 1)
                    {
                        if (playerLevel > 75)
                        {
                            lastQuestNumber = 2;
                        }
                        else if (playerLevel == 75)
                        {
                            lastQuestNumber = 1;
                            lastQuestStage = 2;
                        }
                    }

                    return QuestReply.ProgressEvent(10045, playerLevel, limitBreaker, lastQuestNumber, lastQuestStage);
                })
                .OnEventFinish(Zone.RuludeGardens, 10045, q =>
                {
                    if (q.Option == 7) // Accept quest option.
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.NomadMoogle).OnTrade(q =>
                {
                    if (q.Trade.HasExactly((Item.KindredsCrest, 5)) &&
                        q.Player.MeritCount > 3)
                    {
                        return QuestReply.ProgressEvent(10136);
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    int playerLevel = q.Player.MainLevel;
                    int limitBreaker = q.Player.HasKeyItem(KeyItem.LimitBreaker) ? 1 : 2;
                    int lastQuestNumber = 2;
                    int lastQuestStage = 1;

                    return QuestReply.Event(10045, playerLevel, limitBreaker, lastQuestNumber, lastQuestStage);
                })
                .OnEventFinish(Zone.RuludeGardens, 10136, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        q.Player.SetMerits(q.Player.MeritCount - 4);
                        q.Player.SetLevelCap(85);
                        q.Text.Special(Text.RuludeGardens.YOUR_LEVEL_LIMIT_IS_NOW_85);
                    }
                });
        }
    }
}
