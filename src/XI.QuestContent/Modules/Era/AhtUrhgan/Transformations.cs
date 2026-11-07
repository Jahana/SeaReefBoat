// Transformations' era module, a line-for-line port of modules/era/lua/quests/ahturhgan/transformations.lua (quest
// rework B3b Aht Urhgan, P4, group 1). It restores the JST midnight wait before Waoud offers The Beast Within (the June
// 7, 2016 version update shortened the wait to one minute;
// https://forum.square-enix.com/ffxi/threads/50760-Jun.-7-2016-(JST)-Version-Update).
// Module:new('era_quest_transformations', xi.pre(xi.expansion.ROV)); its onServerStart override replaces section 2's
// Alzadaal Undersea Ruins onEventFinish 5 with a copy that sets The Beast Within's Timer = JstMidnight().
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.AhtUrhgan
{
    public sealed class Transformations : QuestModule
    {
        public Transformations()
            : base("era_quest_transformations", "modules/era/lua/quests/ahturhgan/transformations.lua") { }

        // xi.pre(xi.expansion.ROV)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Rov);

        protected override void Build(QuestModuleBuilder m)
        {
            m.Quest(QuestKey.AhtUrhgan.Transformations)
                // Copy of the base handler. The Beast Within is offered at JST midnight.
                .State(2).ReplaceOnEventFinish(Zone.AlzadaalUnderseaRuins, 5, q =>
                {
                    if (q.Quest.Complete())
                    {
                        // Module change
                        q.Player.SetQuestVar(QuestLog.AhtUrghan, "THE_BEAST_WITHIN", "Timer", q.Player.JstMidnight);
                    }
                });
        }
    }
}
