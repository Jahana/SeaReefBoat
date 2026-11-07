// A Squire's Test II (Log ID 0, Quest ID 19), a line-for-line port of scripts/quests/sandoria/A_Squires_Test_II.lua
// (quest rework B3b, San d'Oria group 2). Balasiel : !pos -136 -11 64 230; qm2 : !pos -94 1 273 193; qm3 : !pos -139 0.1
// 264 193. Offered once A Squire's Test is completed: from level 10 625 (its finish begins it, whatever the option),
// below it 671. Accepted: Balasiel's 630, or 626 with the Stalactite Dew (completed: fame +20, the Squire Certificate,
// Spelunker; the dew deleted); Chanpau's 629 sets the quest var Option 1, after which Morjean (Northern San d'Oria)
// answers 602. In Ordelle's Caves qm2 (no dew) sets the quest var Timer to the system time + 30 s and says
// PLACE_HANDS_IN_POOL; qm3 then gives the dew while the Timer runs, after it DEW_SLIPS_THROUGH_FINGERS, and
// ALREADY_OBTAINED_DEW once held. A Knight's Test reads this quest's status; Diary's script reads it too (stays).
// The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class ASquiresTestIi : QuestScript
    {
        public ASquiresTestIi() : base(QuestLog.Sandoria, "A_SQUIRES_TEST_II") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/A_Squires_Test_II";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Sandoria, KeyItem = KeyItem.SquireCertificate, Title = Title.Spelunker });

            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.Sandoria, "A_SQUIRES_TEST"))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Balasiel).OnTrigger(q =>
                {
                    if (q.Player.MainLevel >= 10)
                    {
                        return QuestReply.ProgressEvent(625);
                    }
                    else
                    {
                        return QuestReply.Event(671);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 625, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Balasiel).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.StalactiteDew))
                    {
                        return QuestReply.ProgressEvent(626);
                    }
                    else
                    {
                        return QuestReply.Event(630);
                    }
                })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Chanpau).OnTrigger(QuestReply.Event(629))
                .OnEventFinish(Zone.SouthernSanDoria, 626, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.StalactiteDew);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 629, q => q.Quest.SetVar("Option", 1))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Morjean).OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") == 1)
                    {
                        return QuestReply.Event(602);
                    }
                    return null;
                })
                .Npc(Zone.OrdellesCaves, Npc.OrdellesCaves.qm2).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.StalactiteDew))
                    {
                        q.Quest.SetVar("Timer", q.Player.SystemTime + 30);
                    }

                    return QuestReply.MessageSpecial(Text.OrdellesCaves.PLACE_HANDS_IN_POOL);
                })
                .Npc(Zone.OrdellesCaves, Npc.OrdellesCaves.qm3).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.StalactiteDew))
                    {
                        if (q.Player.SystemTime <= q.Quest.Var("Timer"))
                        {
                            return QuestReply.KeyItem(KeyItem.StalactiteDew);
                        }
                        else
                        {
                            return QuestReply.MessageSpecial(Text.OrdellesCaves.DEW_SLIPS_THROUGH_FINGERS);
                        }
                    }
                    else
                    {
                        return QuestReply.MessageSpecial(Text.OrdellesCaves.ALREADY_OBTAINED_DEW);
                    }
                });
        }
    }
}
