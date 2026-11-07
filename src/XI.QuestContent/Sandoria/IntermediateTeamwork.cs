// Intermediate Teamwork (Log ID 0, Quest ID 64), a line-for-line port of
// scripts/quests/sandoria/Intermediate_Teamwork.lua (quest rework B3b, San d'Oria group 5). Vilatroire : !pos -260 -70
// 423 100. Available with Introduction to Teamwork completed: Vilatroire's 133 at San d'Oria fame level 3 and level 10
// (option 1: begun), else 132, which replaces his default 136. Accepted: in a party of two or more, his 129 with option
// [1] = 2; its update counts the party members (the char among them) of the char's race (Hume, Elvaan and Tarutaru
// either sex; Galka; Mithra): any member out of the zone or farther than 15 yalms updates with 1; two or more set the
// quest's local var Prog to 1 and update with 15, 2; else 4, 2. 129's finish with option 0 and the local Prog 1
// completes it (Scroll of Mage's Ballad, fame +30, Second-Rate Organizer) and clears the local var. Alone, Vilatroire's
// trigger returns nothing (his default 136). Advanced Teamwork needs it completed.
// Kept as the Lua has it: the members counted include the char itself; the update's failure arms (a member out of the
// zone or range, too few members, too few alike) leave the local Prog as it was, so after one passing update a later
// 129 finished with option 0 completes it whatever its own update said. The era module
// modules/era/lua/quests/sandoria/intermediate_teamwork.lua (before Rhapsodies of Vana'diel) replaces the 129 update
// with a copy needing six members: Modules/Era/Sandoria/IntermediateTeamwork.cs. The xi.race constants are the shared
// Race class (Ids/Race.cs).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class IntermediateTeamwork : QuestScript
    {
        public IntermediateTeamwork() : base(QuestLog.Sandoria, "INTERMEDIATE_TEAMWORK") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Intermediate_Teamwork";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.ScrollOfMagesBallad,
                Fame = 30,
                FameArea = FameArea.Sandoria,
                Title = Title.SecondRateOrganizer,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Sandoria, "INTRODUCTION_TO_TEAMWORK") == QuestStatus.Completed)
                .Npc(Zone.WestRonfaure, Npc.WestRonfaure.Vilatroire).OnTrigger(q =>
                {
                    if (q.Player.FameLevel(FameArea.Sandoria) >= 3 &&
                        q.Player.MainLevel >= 10)
                    {
                        return QuestReply.ProgressEvent(133);
                    }
                    else
                    {
                        return QuestReply.Event(132).ReplaceDefault();
                    }
                })
                .OnEventFinish(Zone.WestRonfaure, 133, q =>
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
                        return QuestReply.ProgressEvent(129).Option(1, 2);
                    }
                    return null;
                })
                .OnEventUpdate(Zone.WestRonfaure, 129, q =>
                {
                    int partySizeRequirement = 2;
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
