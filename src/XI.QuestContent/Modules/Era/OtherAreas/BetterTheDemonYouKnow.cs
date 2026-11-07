// Better the Demon You Know's era module, a line-for-line port of
// modules/era/lua/quests/otherareas/better_the_demon_you_know.lua (quest rework B3b, Other Areas group 8). It restores
// the JST midnight wait before Koblakiq finishes reading the demon pen (the July 8, 2014 version update shortened the
// wait to one minute; https://forum.square-enix.com/ffxi/threads/43135-Jul-8-2014-(JST)-Version-Update).
// Module:new('era_quest_better_the_demon_you_know', xi.pre(xi.expansion.SOA)); its onServerStart override wraps the
// second section's Oldton Movalpolos onEventFinish 22 (the base, then Wait = JstMidnight()). The quest's trigger is
// left as it is: it reads GetSystemTime() >= Wait, so the due time is the next JST midnight itself.
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.OtherAreas
{
    public sealed class BetterTheDemonYouKnow : QuestModule
    {
        public BetterTheDemonYouKnow()
            : base(
                "era_quest_better_the_demon_you_know",
                "modules/era/lua/quests/otherareas/better_the_demon_you_know.lua") { }

        // xi.pre(xi.expansion.SOA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Soa);

        protected override void Build(QuestModuleBuilder m)
        {
            m.Quest(QuestKey.OtherAreas.BetterTheDemonYouKnow)
                // quest.sections[2][xi.zone.OLDTON_MOVALPOLOS].onEventFinish[22]: the quest's state 2 (accepted)
                // Koblakiq works on the pen overnight. He sends the player on at JST midnight.
                .State(2).WrapOnEventFinish(Zone.OldtonMovalpolos, 22, (q, basePen) =>
                {
                    basePen(q);
                    q.Quest.SetVar("Wait", q.Player.JstMidnight); // Module change
                });
        }
    }
}
