// Shadows of the Departed's era module, a line-for-line port of modules/era/lua/quests/jeuno/shadows_of_the_departed.lua
// (quest rework B3b Jeuno, P4). It restores
// the JST midnight waits before this quest and before Apocalypse Nigh: Module:new('era_quest_shadows_of_the_departed',
// xi.pre(xi.expansion.SOA)); its onServerStart override replaces section 1's check (the Timer compared with
// GetSystemTime()) and section 2's Ru'Lude Gardens 162 finish (Apocalypse Nigh's Timer = JstMidnight()).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Jeuno
{
    public sealed class ShadowsOfTheDeparted : QuestModule
    {
        public ShadowsOfTheDeparted() : base("era_quest_shadows_of_the_departed", "modules/era/lua/quests/jeuno/shadows_of_the_departed.lua") { }

        // xi.pre(xi.expansion.SOA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Soa);

        protected override void Build(QuestModuleBuilder m)
        {
            var quest = m.Quest(QuestKey.Jeuno.ShadowsOfTheDeparted);

            // Copy of the base check with the timer compared as a timestamp.
            quest.State(1).ReplaceCheck(q =>
                q.Quest.Status == QuestStatus.Available &&
                q.Player.HasCompletedQuest(QuestLog.Jeuno, "STORMS_OF_FATE") &&
                q.Player.CurrentMission(MissionLog.Zilart) == Mission.Zilart.Awakening &&
                q.Player.MissionStatus(MissionLog.Zilart) == 3 &&
                q.Player.SystemTime >= q.Quest.Var("Timer")); // Module change: this quest opens at JST midnight.

            // Copy of the base handler with the one Vana'diel day wait extended to JST midnight.
            quest.State(2).ReplaceOnEventFinish(Zone.RuludeGardens, 162, q =>
            {
                if (q.Quest.Complete())
                {
                    for (int keyItemId = KeyItem.PromyvionHollaSliver; keyItemId <= KeyItem.PromyvionHollaSliver + 2; keyItemId++)
                    {
                        q.Player.DelKeyItem(keyItemId);
                    }

                    q.Text.Special(Text.RuludeGardens.YOU_HAND_THE_THREE_SLIVERS);

                    q.Player.SetQuestVar(QuestLog.Jeuno, "APOCALYPSE_NIGH", "Timer", q.Player.JstMidnight); // Module change: Apocalypse Nigh opens at JST midnight.
                    q.Player.SetQuestMustZone(QuestLog.Jeuno, "APOCALYPSE_NIGH");
                }
            });
        }
    }
}
