// Chasing Quotas' era module, a line-for-line port of modules/era/lua/quests/sandoria/chasing_quotas.lua (quest rework
// B3b, San d'Oria group 8): it restores the JST midnight wait before Ceraulian discovers the mugging (the February 19, 2015
// version update shortened it to one minute). Module:new('era_quest_chasing_quotas', xi.pre(xi.expansion.SOA)); its
// onServerStart override wraps section 2's Port San d'Oria onEventFinish 17: the quest's own finish (the hairpin taken,
// Prog 1, Wait = now + 60), then Wait = JstMidnight().
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Sandoria
{
    public sealed class ChasingQuotas : QuestModule
    {
        public ChasingQuotas() : base("era_quest_chasing_quotas", "modules/era/lua/quests/sandoria/chasing_quotas.lua") { }

        // xi.pre(xi.expansion.SOA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Soa);

        protected override void Build(QuestModuleBuilder m)
        {
            // quest.sections[2][xi.zone.PORT_SAN_DORIA].onEventFinish[17]: the quest's state 2 (accepted)
            m.Quest(QuestKey.Sandoria.ChasingQuotas)
                .State(2).WrapOnEventFinish(Zone.PortSanDoria, 17, (q, baseTradeCS) =>
                {
                    baseTradeCS(q);
                    // The mugging is discovered at JST midnight.
                    q.Quest.SetVar("Wait", q.Player.JstMidnight); // Module change
                });
        }
    }
}
