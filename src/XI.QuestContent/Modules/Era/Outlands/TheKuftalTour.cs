// The Kuftal Tour's module, a line-for-line port of modules/era/lua/quests/outlands/the_kuftal_tour.lua (quest rework B3b
// Outlands, group 5; P4). It restores the six-member party (the June 2015 update dropped it to two;
// https://forum.square-enix.com/ffxi/threads/47481-Jun-25-2015-%28JST%29-Version-Update).
// Module:new('the_kuftal_tour', xi.pre(xi.expansion.ROV)) (the module's name has no era_quest_ prefix: kept); its
// onServerStart override replaces section 2's Kuftal Tunnel qm5 onTrigger with a copy that asks for a party of exactly 6.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Outlands
{
    public sealed class TheKuftalTour : QuestModule
    {
        public TheKuftalTour() : base("the_kuftal_tour", "modules/era/lua/quests/outlands/the_kuftal_tour.lua") { }

        // xi.pre(xi.expansion.ROV)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Rov);

        protected override void Build(QuestModuleBuilder m)
        {
            m.Quest(QuestKey.Outlands.TheKuftalTour)
                .State(2).Npc(Zone.KuftalTunnel, Npc.KuftalTunnel.qm5).ReplaceOnTrigger(q =>
                {
                    bool flag = true;

                    foreach (var member in q.Player.Alliance)
                    {
                        if (member.MainLevel > 40 ||
                            member.Distance > 15)
                        {
                            flag = false;
                        }
                    }

                    if (flag && q.Player.PartySize == 6)
                    {
                        return QuestReply.ProgressEvent(14);
                    }
                    return null;
                });
        }
    }
}
