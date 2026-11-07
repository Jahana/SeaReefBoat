// Advanced Teamwork's era module, a line-for-line port of modules/era/lua/quests/sandoria/advanced_teamwork.lua (quest
// rework B3b, San d'Oria Task 9): it reverts the required number of same-job party members to six (the May 14, 2015
// version update reduced it to two). Module:new('era_quest_advanced_teamwork', xi.pre(xi.expansion.ROV)); its
// onServerStart override replaces section 2's West Ronfaure onEventUpdate 129 with a copy of the quest's own whose
// partySizeRequirement is 6, not 2 (the count still compared with ==, as the quest's own).
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Sandoria
{
    public sealed class AdvancedTeamwork : QuestModule
    {
        public AdvancedTeamwork() : base("era_quest_advanced_teamwork", "modules/era/lua/quests/sandoria/advanced_teamwork.lua") { }

        // xi.pre(xi.expansion.ROV)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Rov);

        protected override void Build(QuestModuleBuilder m)
        {
            // quest.sections[2][xi.zone.WEST_RONFAURE].onEventUpdate[129]: the quest's state 2 (accepted)
            m.Quest(QuestKey.Sandoria.AdvancedTeamwork)
                .State(2).ReplaceOnEventUpdate(Zone.WestRonfaure, 129, q =>
                {
                    // Copy of the base handler with partySizeRequirement raised to 6
                    int partySizeRequirement = 6;
                    // player:getParty(): each member's zone, distance, nation, race and main job, in the table's order
                    var party = q.Player.PartyMembers;
                    int partySameJobCount = 0;

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
                                if (q.Player.MainJob == member.MainJob)
                                {
                                    partySameJobCount = partySameJobCount + 1;
                                }
                            }
                        }
                    }
                    else
                    {
                        q.Player.UpdateEvent(1);
                        return;
                    }

                    if (partySameJobCount == partySizeRequirement)
                    {
                        q.Quest.SetLocalVar("Prog", 1);
                        q.Player.UpdateEvent(15, 3);
                        return;
                    }
                    else
                    {
                        q.Player.UpdateEvent(5, 3);
                        return;
                    }
                });
        }
    }
}
