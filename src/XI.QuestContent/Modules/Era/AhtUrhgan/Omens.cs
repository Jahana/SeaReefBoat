// Omens' era module, a line-for-line port of modules/era/lua/quests/ahturhgan/omens.lua (quest rework B3b Aht Urhgan,
// P4, group 1). It restores the JST midnight wait before Waoud offers Transformations (the June 7, 2016 version update
// shortened the wait to one minute; https://forum.square-enix.com/ffxi/threads/50760-Jun.-7-2016-(JST)-Version-Update).
// Module:new('era_quest_omens', xi.pre(xi.expansion.ROV)); its onServerStart override replaces section 2's Whitegate
// onEventFinish 716 with a copy that sets Transformations' Timer = JstMidnight().
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.AhtUrhgan
{
    public sealed class Omens : QuestModule
    {
        public Omens() : base("era_quest_omens", "modules/era/lua/quests/ahturhgan/omens.lua") { }

        // xi.pre(xi.expansion.ROV)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Rov);

        protected override void Build(QuestModuleBuilder m)
        {
            m.Quest(QuestKey.AhtUrhgan.Omens)
                // Copy of the base handler. Transformations is offered at JST midnight.
                .State(2).ReplaceOnEventFinish(Zone.AhtUrhganWhitegate, 716, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.SealedImmortalEnvelope);

                        q.Player.SetQuestMustZone(QuestLog.AhtUrghan, "TRANSFORMATIONS");
                        // Module change
                        q.Player.SetQuestVar(QuestLog.AhtUrghan, "TRANSFORMATIONS", "Timer", q.Player.JstMidnight);
                    }
                });
        }
    }
}
