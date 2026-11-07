// The Clockmaster (Log ID 3, Quest ID 21), a line-for-line port of scripts/quests/jeuno/The_Clockmaster.lua (quest
// rework B3b). _6s2 : !pos -80 0 104 244; Collet : !pos -44 0 107 244. Save the Clock Tower completed and Jeuno fame
// level 5: the door's 152 completes it at once (Time Hammer, 1200 gil, Timekeeper, fame +17 in San d'Oria, Bastok and
// Windurst; not completed when the hammer does not fit). Completed: the door's 110 and Collet's 163, both
// replaceDefault. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class TheClockmaster : QuestScript
    {
        public TheClockmaster() : base(QuestLog.Jeuno, "THE_CLOCKMASTER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/The_Clockmaster";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.TimeHammer, Gil = 1200, Title = Title.Timekeeper });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Jeuno, "SAVE_THE_CLOCK_TOWER") &&
                    q.Player.FameLevel(FameArea.Jeuno) >= 5)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno._6s2).OnTrigger(q => QuestReply.ProgressEvent(152))
                .OnEventFinish(Zone.UpperJeuno, 152, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 17);
                        q.Player.AddFame(FameArea.Bastok, 17);
                        q.Player.AddFame(FameArea.Windurst, 17);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno._6s2).OnTrigger(QuestReply.Event(110).ReplaceDefault())
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Collet).OnTrigger(QuestReply.Event(163).ReplaceDefault());
        }
    }
}
