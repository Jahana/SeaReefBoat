// Beginnings' era module, a line-for-line port of modules/era/lua/quests/ahturhgan/beginnings.lua (quest rework B3b Aht
// Urhgan, P4, group 1). It restores the JST midnight wait before Waoud offers Omens (the June 7, 2016 version update
// shortened the wait to one minute; https://forum.square-enix.com/ffxi/threads/50760-Jun.-7-2016-(JST)-Version-Update).
// Module:new('era_quest_beginnings', xi.pre(xi.expansion.ROV)); its onServerStart override replaces section 2's
// Whitegate onEventFinish 707 with a copy that sets Omens' Timer = JstMidnight(). Omens' check is left as it is: Waoud
// reads GetSystemTime() < Timer, so the offer comes at the next JST midnight itself.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.AhtUrhgan
{
    public sealed class Beginnings : QuestModule
    {
        public Beginnings() : base("era_quest_beginnings", "modules/era/lua/quests/ahturhgan/beginnings.lua") { }

        // xi.pre(xi.expansion.ROV)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Rov);

        protected override void Build(QuestModuleBuilder m)
        {
            m.Quest(QuestKey.AhtUrhgan.Beginnings)
                // Copy of the base handler. Omens is offered at JST midnight.
                .State(2).ReplaceOnEventFinish(Zone.AhtUrhganWhitegate, 707, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetQuestMustZone(QuestLog.AhtUrghan, "OMENS");
                        // Module change
                        q.Player.SetQuestVar(QuestLog.AhtUrghan, "OMENS", "Timer", q.Player.JstMidnight);
                    }
                });
        }
    }
}
