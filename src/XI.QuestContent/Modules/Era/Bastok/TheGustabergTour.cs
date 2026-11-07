// The Gustaberg Tour's era module, a line-for-line port of modules/era/lua/quests/bastok/the_gustaberg_tour.lua (quest
// rework P4): it reverts the completion requirement to six party members of level five or below (the June 25, 2015 version
// update reduced it to two members of level fifteen or below). Module:new('era_quest_the_gustaberg_tour',
// xi.pre(xi.expansion.ROV)); its onServerStart override replaces section 2's North Gustaberg Hunting_Bear onTrigger with a
// copy of the quest's handler, the level lowered to 5 and the party size raised to 6 (== 6, where the quest has > 1).
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Bastok
{
    public sealed class TheGustabergTour : QuestModule
    {
        public TheGustabergTour() : base("era_quest_the_gustaberg_tour", "modules/era/lua/quests/bastok/the_gustaberg_tour.lua") { }

        // xi.pre(xi.expansion.ROV)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Rov);

        protected override void Build(QuestModuleBuilder m)
        {
            // quest.sections[2][xi.zone.NORTH_GUSTABERG]['Hunting_Bear'].onTrigger: the quest's state 2 (accepted)
            m.Quest(QuestKey.Bastok.TheGustabergTour)
                .State(2).Npc(Zone.NorthGustaberg, Npc.NorthGustaberg.HuntingBear).ReplaceOnTrigger(q =>
                {
                    bool flag = true;

                    foreach (var member in q.Player.Alliance)
                    {
                        if (member.MainLevel > 5 || member.Distance > 15)
                        {
                            flag = false;
                        }
                    }

                    return flag && q.Player.PartySize == 6 ? QuestReply.ProgressEvent(22) : QuestReply.ProgressEvent(21);
                });
        }
    }
}
