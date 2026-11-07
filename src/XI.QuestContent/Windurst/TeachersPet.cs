// Teacher's Pet (Log ID 2, Quest ID 28), a line-for-line port of scripts/quests/windurst/Teachers_Pet.lua (quest rework
// B3b). Moreno-Toeno : !pos 169 -1.25 159 238. Available: Moreno-Toeno's 437 (Prog 1), then 438 (0, Bird Feather,
// Two-Leaf Mandragora Bud): option 0 begins it, any other resets Prog to 0. Accepted or completed (repeatable): his 439
// (0, the two items); the Bird Feather and the Two-Leaf Mandragora Bud traded exactly: 440 (250, the two items), which
// takes them, gives 250 gil and completes the quest (fame 10), then sets Making the Grade's mustZone. Kept as the Lua
// file has it: 440 takes the items and pays the gil before the completion (which cannot fail: the reward is fame
// only); each repeat sets Making the Grade's mustZone again (its completed Fuepepe line 459 waits for a zone change).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class TeachersPet : QuestScript
    {
        public TeachersPet() : base(QuestLog.Windurst, "TEACHERS_PET") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Teachers_Pet";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 10, FameArea = FameArea.Windurst });

            b.State(QuestStatus.Available)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.MorenoToeno).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(437);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(438, 0, Item.BirdFeather, Item.TwoLeafMandragoraBud);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 437, q => q.Quest.SetStep(1))
                .OnEventFinish(Zone.WindurstWaters, 438, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                    else
                    {
                        q.Quest.SetStep(0);
                    }
                });

            // Lua: status ~= xi.questStatus.QUEST_AVAILABLE
            b.State(QuestStatus.Accepted)
                .Or(QuestStatus.Completed)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.MorenoToeno)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.BirdFeather, Item.TwoLeafMandragoraBud))
                        {
                            return QuestReply.ProgressEvent(440, 250, Item.BirdFeather, Item.TwoLeafMandragoraBud);
                        }

                        return null;
                    })
                    .OnTrigger(QuestReply.Event(439, 0, Item.BirdFeather, Item.TwoLeafMandragoraBud))
                .OnEventFinish(Zone.WindurstWaters, 440, q =>
                {
                    q.Trade.Confirm();
                    q.Player.AddGil(250); // "Obtained X gil." Text is baked into the CS, so gil needs to be given outside of the quest rewards.

                    if (q.Quest.Complete())
                    {
                        q.Player.SetQuestMustZone(QuestLog.Windurst, "MAKING_THE_GRADE");
                    }
                });
        }
    }
}
