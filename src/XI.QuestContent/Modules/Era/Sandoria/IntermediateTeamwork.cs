// Intermediate Teamwork's era module, a line-for-line port of modules/era/lua/quests/sandoria/intermediate_teamwork.lua
// (quest rework B3b, San d'Oria Task 9): it reverts the required number of same-race party members to six (the May 14,
// 2015 version update reduced it to two). Module:new('era_quest_intermediate_teamwork', xi.pre(xi.expansion.ROV)); its
// onServerStart override replaces section 2's West Ronfaure onEventUpdate 129 with a copy of the quest's own whose
// partySizeRequirement is 6, not 2.
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Sandoria
{
    public sealed class IntermediateTeamwork : QuestModule
    {
        public IntermediateTeamwork() : base("era_quest_intermediate_teamwork", "modules/era/lua/quests/sandoria/intermediate_teamwork.lua") { }

        // xi.pre(xi.expansion.ROV)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Rov);

        protected override void Build(QuestModuleBuilder m)
        {
            // quest.sections[2][xi.zone.WEST_RONFAURE].onEventUpdate[129]: the quest's state 2 (accepted)
            m.Quest(QuestKey.Sandoria.IntermediateTeamwork)
                .State(2).ReplaceOnEventUpdate(Zone.WestRonfaure, 129, q =>
                {
                    // Copy of the base handler with partySizeRequirement raised to 6
                    int partySizeRequirement = 6;
                    // player:getParty(): each member's zone, distance, nation, race and main job, in the table's order
                    var party = q.Player.PartyMembers;
                    int partySameRaceCount = 0;

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
                                int pRace = q.Player.Race;
                                int mRace = member.Race;

                                if ((pRace == Race.HumeM || pRace == Race.HumeF) &&
                                    (mRace == Race.HumeM || mRace == Race.HumeF))
                                {
                                    partySameRaceCount = partySameRaceCount + 1;
                                }
                                else if ((pRace == Race.ElvaanM || pRace == Race.ElvaanF) &&
                                    (mRace == Race.ElvaanM || mRace == Race.ElvaanF))
                                {
                                    partySameRaceCount = partySameRaceCount + 1;
                                }
                                else if ((pRace == Race.TaruM || pRace == Race.TaruF) &&
                                    (mRace == Race.TaruM || mRace == Race.TaruF))
                                {
                                    partySameRaceCount = partySameRaceCount + 1;
                                }
                                else if (pRace == Race.Galka && mRace == Race.Galka)
                                {
                                    partySameRaceCount = partySameRaceCount + 1;
                                }
                                else if (pRace == Race.Mithra && mRace == Race.Mithra)
                                {
                                    partySameRaceCount = partySameRaceCount + 1;
                                }
                            }
                        }
                    }
                    else
                    {
                        q.Player.UpdateEvent(1);
                        return;
                    }

                    if (partySameRaceCount >= partySizeRequirement)
                    {
                        q.Quest.SetLocalVar("Prog", 1);
                        q.Player.UpdateEvent(15, 2);
                        return;
                    }
                    else
                    {
                        q.Player.UpdateEvent(4, 2);
                        return;
                    }
                });
        }
    }
}
