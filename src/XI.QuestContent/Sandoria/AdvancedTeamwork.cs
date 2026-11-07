// Advanced Teamwork (Log ID 0, Quest ID 65), a line-for-line port of scripts/quests/sandoria/Advanced_Teamwork.lua
// (quest rework B3b, San d'Oria group 5). Vilatroire : !pos -260 -70 423 100. Available with Intermediate Teamwork
// completed: Vilatroire's 131 at San d'Oria fame level 4 and level 10 (option 1: begun), else 130, which replaces his
// default 136. Accepted: in a party of two or more, his 129 with option [1] = 3; its update counts the party members
// (the char among them) of the char's main job: any member out of the zone or farther than 15 yalms updates with 1;
// exactly two (== 2, not >= 2) set the quest's local var Prog to 1 and update with 15, 3; else 5, 3. 129's finish with
// option 0 and the local Prog 1 completes it (Horn Ring, fame +40, First-Rate Organizer) and clears the local var.
// Alone, Vilatroire's trigger returns nothing (his default 136). Completed: his 130.
// Kept as the Lua has it: the same-job count must be exactly 2 (== partySizeRequirement; a party of three of one job
// updates with 5, 3); the members counted include the char itself; the update's failure arms (a member out of the zone
// or range, too few members, too few alike) leave the local Prog as it was, so after one passing update a later 129
// finished with option 0 completes it whatever its own update said. The era module
// modules/era/lua/quests/sandoria/advanced_teamwork.lua (before Rhapsodies of Vana'diel) replaces the 129 update with a
// copy needing six members: Modules/Era/Sandoria/AdvancedTeamwork.cs.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class AdvancedTeamwork : QuestScript
    {
        public AdvancedTeamwork() : base(QuestLog.Sandoria, "ADVANCED_TEAMWORK") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Advanced_Teamwork";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.HornRing,
                Fame = 40,
                FameArea = FameArea.Sandoria,
                Title = Title.FirstRateOrganizer,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Sandoria, "INTERMEDIATE_TEAMWORK") == QuestStatus.Completed)
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.Vilatroire).OnTrigger(q =>
                {
                    if (q.Player.FameLevel(FameArea.Sandoria) >= 4 &&
                        q.Player.MainLevel >= 10)
                    {
                        return QuestReply.ProgressEvent(131);
                    }
                    else
                    {
                        return QuestReply.Event(130).ReplaceDefault();
                    }
                })
                .OnEventFinish(Zone.WestRonfaure, 131, q =>
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
                        return QuestReply.ProgressEvent(129).Option(1, 3);
                    }
                    return null;
                })
                .OnEventUpdate(Zone.WestRonfaure, 129, q =>
                {
                    int partySizeRequirement = 2;
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

            b.State(QuestStatus.Completed)
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.Vilatroire).OnTrigger(QuestReply.Event(130));
        }
    }
}
