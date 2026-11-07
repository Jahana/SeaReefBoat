// Tenshodo Membership (Log ID 3, Quest ID 17), a line-for-line port of scripts/quests/jeuno/Tenshodo_Membership.lua
// (quest rework B3b). NPC: Ghebi Damomohe !pos 16 -.1 -5.2 245; NPC: Jabbar !pos -101 -2.3 23 236. Only while
// available (the quest never shows in the log until completed): Ghebi Damomohe's trade of one Tenshodo Invite plays 108;
// her talk at Jeuno fame level 3 plays 107 with the Tenshodo Application Form, else 106 (option 2: Prog 1); Port Bastok's
// Jabbar / Silver Owl play 152 (Silver Owl: 152, 1) with the form, else 151 (151, 1) at Prog 1, whose finish gives the
// form. 107 / 108 complete it (a Tenshodo Invite and the Tenshodo Members Card) and take the form (108: the traded
// invite too).
// Kept Lua quirks: the invite trade completes the quest straight from available (a char who skips the form); the reward
// gives a new invite for the one traded; Jabbar's and Silver Owl's 152 have no finish; the quest's 106 finish also
// runs on Ghebi's own script 106, so option 2 sets Prog 1 even below fame level 3.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class TenshodoMembership : QuestScript
    {
        public TenshodoMembership() : base(QuestLog.Jeuno, "TENSHODO_MEMBERSHIP") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Tenshodo_Membership";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.TenshodoInvite, KeyItem = KeyItem.TenshodoMembersCard });

            b.State(QuestStatus.Available)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.GhebiDamomohe)
                    .OnTrade(q =>
                    {
                        if (q.Trade.Matches((Item.TenshodoInvite, 1)))
                        {
                            return QuestReply.ProgressEvent(108);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Player.FameLevel(FameArea.Jeuno) >= 3)
                        {
                            if (q.Player.HasKeyItem(KeyItem.TenshodoApplicationForm))
                            {
                                return QuestReply.ProgressEvent(107);
                            }
                            else
                            {
                                return QuestReply.Event(106);
                            }
                        }
                        return null;
                    })
                .OnEventFinish(Zone.LowerJeuno, 106, q =>
                {
                    if (q.Option == 2)
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 107, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.TenshodoApplicationForm);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 108, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        q.Player.DelKeyItem(KeyItem.TenshodoApplicationForm);
                    }
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Jabbar).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.TenshodoApplicationForm))
                    {
                        return QuestReply.ProgressEvent(152);
                    }
                    else if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(151);
                    }
                    return null;
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.SilverOwl).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.TenshodoApplicationForm))
                    {
                        return QuestReply.ProgressEvent(152, 1);
                    }
                    else if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(151, 1);
                    }
                    return null;
                })
                .OnEventFinish(Zone.PortBastok, 151, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.TenshodoApplicationForm);
                });
        }
    }
}
