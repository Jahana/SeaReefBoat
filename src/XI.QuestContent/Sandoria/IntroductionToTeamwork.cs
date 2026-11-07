// Introduction to Teamwork (Log ID 0, Quest ID 63), a line-for-line port of
// scripts/quests/sandoria/Introduction_to_Teamwork.lua (quest rework B3b, San d'Oria group 5). Vilatroire : !pos -260
// -70 423 100. Available to every char: Vilatroire's 135 at San d'Oria fame level 2 and level 10 (option 1: begun),
// else 134, which replaces his default 136. Accepted: in a party of two or more, his 129 with option [1] = 1; its
// update counts the party members (the char among them) of the char's nation: any member out of the zone or farther
// than 15 yalms updates with 1; two or more of one nation set the quest's local var Prog to 1 and update with 15, 1;
// else 3, 1. 129's finish with option 0 and the local Prog 1 completes it (Shell Ring, fame +20, Third-Rate Organizer)
// and clears the local var. Alone, Vilatroire's trigger returns nothing (his default 136). Intermediate Teamwork needs
// it completed.
// Kept as the Lua has it: the members counted include the char itself; the update's failure arms (a member out of the
// zone or range, too few members, too few alike) leave the local Prog as it was, so after one passing update a later
// 129 finished with option 0 completes it whatever its own update said. The era module
// modules/era/lua/quests/sandoria/introduction_to_teamwork.lua (before Rhapsodies of Vana'diel) replaces the 129 update
// with a copy needing six members: Modules/Era/Sandoria/IntroductionToTeamwork.cs.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class IntroductionToTeamwork : QuestScript
    {
        public IntroductionToTeamwork() : base(QuestLog.Sandoria, "INTRODUCTION_TO_TEAMWORK") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Introduction_to_Teamwork";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.ShellRing,
                Fame = 20,
                FameArea = FameArea.Sandoria,
                Title = Title.ThirdRateOrganizer,
            });

            b.State(QuestStatus.Available)
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.Vilatroire).OnTrigger(q =>
                {
                    if (q.Player.FameLevel(FameArea.Sandoria) >= 2 &&
                        q.Player.MainLevel >= 10)
                    {
                        return QuestReply.ProgressEvent(135);
                    }
                    else
                    {
                        return QuestReply.Event(134).ReplaceDefault();
                    }
                })
                .OnEventFinish(Zone.WestRonfaure, 135, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.Vilatroire).OnTrigger(q =>
                {
                    if (q.Player.PartySize > 1)
                    {
                        return QuestReply.ProgressEvent(129).Option(1, 1);
                    }
                    return null;
                })
                .OnEventUpdate(Zone.WestRonfaure, 129, q =>
                {
                    int partySizeRequirement = 2;
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
                })
                .OnEventFinish(Zone.WestRonfaure, 129, q =>
                {
                    if (q.Option == 0 && q.Quest.LocalVar("Prog") == 1)
                    {
                        if (q.Quest.Complete())
                        {
                            q.Quest.SetLocalVar("Prog", 0);
                        }
                    }
                });
        }
    }
}
