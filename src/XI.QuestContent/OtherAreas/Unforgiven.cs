// Unforgiven (Log ID 4, Quest ID 72), a line-for-line port of scripts/quests/otherAreas/Unforgiven.lua (quest rework
// B3b, Other Areas group 7). Tavnazian Safehold, no scripts: Elysia !pos -50.410 -22.204 -41.640 (default 190),
// Pradiulot !pos -20.814 -22 8.399 (two; the second, +1, is cutscene_only; default 192), qm_unforgiven !pos 110.291
// -40.85 -53.039 (default NOTHING_OUT_OF_ORDINARY).
// Available once the char's current CoP mission is An Invitation West or later: Elysia 200, whose finish begins the
// quest. Accepted: Elysia with the Alabaster Hairpin: 202 (the hairpin taken, Prog 1); at Prog 1: 203 (event); else 201
// (event). The ??? without the hairpin: the hairpin (quest:keyItem). Pradiulot at Prog 1: 204, whose finish, when
// quest:complete gives 2000 exp and the Map of Tavnazia, sets Post 1. Completed: Elysia at Post 1: 205 (Post 2);
// Pradiulot at Post 2: 206, whose finish runs quest:cleanup (the vars cleared).
// Kept as the Lua has it: the ??? gives the hairpin again after 202 (Prog 1: the hairpin is gone), so Elysia's 202 can
// run again at Prog 1; 204's quest:complete cannot fail (no item).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class Unforgiven : QuestScript
    {
        public Unforgiven() : base(QuestLog.OtherAreas, "UNFORGIVEN") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Unforgiven";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Exp = 2000,
                KeyItem = KeyItem.MapOfTavnazia,
            });
            b.Steps(1);

            b.State(QuestStatus.Available, when: q => q.Player.CurrentMission(MissionLog.Cop) >= Mission.Cop.AnInvitationWest)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Elysia).OnTrigger(QuestReply.ProgressEvent(200))
                .OnEventFinish(Zone.TavnazianSafehold, 200, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Elysia).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.AlabasterHairpin))
                    {
                        return QuestReply.ProgressEvent(202);
                    }
                    else if (q.Quest.Step == 1)
                    {
                        return QuestReply.Event(203); // Reminder to talk to Pradiulot
                    }
                    else
                    {
                        return QuestReply.Event(201); // Reminder to find the Alabaster Hairpin
                    }
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.qmunforgiven).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.AlabasterHairpin))
                    {
                        return QuestReply.KeyItem(KeyItem.AlabasterHairpin);
                    }
                    return null;
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Pradiulot).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(204);
                    }
                    return null;
                })
                .OnEventFinish(Zone.TavnazianSafehold, 202, q =>
                {
                    q.Player.DelKeyItem(KeyItem.AlabasterHairpin);
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.TavnazianSafehold, 204, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Quest.SetVar("Post", 1);
                    }
                });

            // Post Quest Cutscene
            b.State(QuestStatus.Completed)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Elysia).OnTrigger(q =>
                {
                    if (q.Quest.Var("Post") == 1)
                    {
                        return QuestReply.ProgressEvent(205);
                    }
                    return null;
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Pradiulot).OnTrigger(q =>
                {
                    if (q.Quest.Var("Post") == 2)
                    {
                        return QuestReply.ProgressEvent(206);
                    }
                    return null;
                })
                .OnEventFinish(Zone.TavnazianSafehold, 205, q =>
                {
                    q.Quest.SetVar("Post", 2);
                })
                .OnEventFinish(Zone.TavnazianSafehold, 206, q =>
                {
                    // quest:cleanup(player)
                    q.Quest.Cleanup();
                });
        }
    }
}
