// Introduction to Teamwork's era module, a line-for-line port of modules/era/lua/quests/sandoria/introduction_to_teamwork.lua
// (quest rework B3b, San d'Oria Task 9): it reverts the required number of same-nation party members to six (the May
// 14, 2015 version update reduced it to two). Module:new('era_quest_introduction_to_teamwork', xi.pre(xi.expansion.ROV));
// its onServerStart override replaces section 2's West Ronfaure onEventUpdate 129 with a copy of the quest's own whose
// partySizeRequirement is 6, not 2.
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Sandoria
{
    public sealed class IntroductionToTeamwork : QuestModule
    {
        public IntroductionToTeamwork() : base("era_quest_introduction_to_teamwork", "modules/era/lua/quests/sandoria/introduction_to_teamwork.lua") { }

        // xi.pre(xi.expansion.ROV)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Rov);

        protected override void Build(QuestModuleBuilder m)
        {
            // quest.sections[2][xi.zone.WEST_RONFAURE].onEventUpdate[129]: the quest's state 2 (accepted)
            m.Quest(QuestKey.Sandoria.IntroductionToTeamwork)
                .State(2).ReplaceOnEventUpdate(Zone.WestRonfaure, 129, q =>
                {
                    // Copy of the base handler with partySizeRequirement raised to 6
                    int partySizeRequirement = 6;
                    // player:getParty(): each member's zone, distance, nation, race and main job, in the table's order
                    var party = q.Player.PartyMembers;
                    int partySameNationCount = 0;

                    if (party.Count >= partySizeRequirement)
                    {
                        foreach (var member in party)
                        {
                            // player:getZoneID(): the handler's zone
                            if (member.ZoneId != q.Zone ||
                                member.Distance > 15)
                            {
                                q.Player.UpdateEvent(1);
                                return;
                            }
                            else
                            {
                                if (member.Nation == q.Player.Nation)
                                {
                                    partySameNationCount = partySameNationCount + 1;
                                }
                            }
                        }
                    }
                    else
                    {
                        q.Player.UpdateEvent(1);
                        return;
                    }

                    if (partySameNationCount >= partySizeRequirement)
                    {
                        q.Quest.SetLocalVar("Prog", 1);
                        q.Player.UpdateEvent(15, 1);
                        return;
                    }
                    else
                    {
                        q.Player.UpdateEvent(3, 1);
                    }
                });
        }
    }
}
