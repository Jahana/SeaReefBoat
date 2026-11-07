// Moogles in the Wild's era module, a line-for-line port of modules/era/lua/quests/otherareas/moogles_in_the_wild.lua
// (quest rework B3b, Other Areas group 9). It restores the conquest tally wait before the Moogle offers the quest and
// the JST midnight wait before the Moogle hands over the reward (the July 8, 2014 version update shortened both waits
// to one minute; https://forum.square-enix.com/ffxi/threads/43135-Jul-8-2014-(JST)-Version-Update).
// Module:new('era_quest_moogles_in_the_wild', xi.pre(xi.expansion.SOA)); its onServerStart override replaces section
// 1's check with a copy whose last test is bedPlacedTime < NextConquestTally() - utils.days(7) (the bed placed before
// the last tally), and, in every Mog House zone's section, the 30015 finish with a copy that sets Timer =
// JstMidnight().
using XI.QuestContent.OtherAreas;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.OtherAreas
{
    public sealed class MooglesInTheWild : QuestModule
    {
        public MooglesInTheWild()
            : base(
                "era_quest_moogles_in_the_wild",
                "modules/era/lua/quests/otherareas/moogles_in_the_wild.lua") { }

        // xi.pre(xi.expansion.SOA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Soa);

        protected override void Build(QuestModuleBuilder m)
        {
            var quest = m.Quest(QuestKey.OtherAreas.MooglesInTheWild);

            // Copy of the base check. The quest is offered at the next conquest tally.
            quest.State(1).ReplaceCheck(q =>
            {
                int bedPlacedTime = q.Quest.Var("bedPlacedTime");

                return q.Quest.Status == QuestStatus.Available &&
                    q.Player.HasCompletedQuest(QuestLog.OtherAreas, "THE_MOOGLE_PICNIC") &&
                    q.Player.InMogHouseInHomeNation &&
                    q.Player.FameLevel(q.Player.Nation) >= 7 &&
                    q.Quest.LocalVar("mustZone") == 0 &&
                    q.Quest.LocalVar("questSeen") == 0 &&
                    bedPlacedTime != 0 &&
                    bedPlacedTime < q.Player.NextConquestTally - 7 * 86400; // Module change (utils.days(7))
            });

            // Every Mog House zone shares one section table. A wrapper would run the base handler once per zone.
            foreach (var (zoneId, _) in MogHouse.Moogles)
            {
                // Copy of the base handler. The reward is handed over at JST midnight.
                quest.State(2).ReplaceOnEventFinish(zoneId, 30015, q =>
                {
                    q.Trade.Complete();
                    q.Quest.SetStep(1);
                    q.Quest.SetVar("Timer", q.Player.JstMidnight); // Module change
                });
            }
        }
    }
}
