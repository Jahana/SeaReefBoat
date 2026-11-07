// The Darksmith (Log ID 1, Quest ID 40), a line-for-line port of scripts/quests/bastok/The_Darksmith.lua (quest rework
// B3b). Mighty Fist : !pos -47 2 -30 237. Mighty Fist's own script (561, then 560 once its local var is set) stays; the
// lookup alternates this quest's Event-priority 561 / 560 with it. Kept as the Lua file has it: the second section
// matches accepted and completed, so every later trade of two Chunks of Darksteel Ore completes the quest again, with
// its 8000 gil; each completion also clears the quest's mustZone (quest:complete's cleanup clears its vars).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TheDarksmith : QuestScript
    {
        public TheDarksmith() : base(QuestLog.Bastok, "THE_DARKSMITH") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/The_Darksmith";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 16, FameArea = FameArea.Bastok, Gil = 8000 });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Bastok) >= 3)
                .Npc(Zone.Metalworks, Npc.Metalworks.MightyFist).OnTrigger(QuestReply.ProgressEvent(565))
                .OnEventFinish(Zone.Metalworks, 565, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Or(QuestStatus.Completed)
                .Npc(Zone.Metalworks, Npc.Metalworks.MightyFist)
                    .OnTrade(q => q.Trade.Matches((Item.ChunkOfDarksteelOre, 2)) ? QuestReply.ProgressEvent(566) : null)
                    // 561 the first time (reset on zoning), 560 after
                    .OnTrigger(q => !q.Quest.MustZone ? QuestReply.Event(561) : QuestReply.Event(560))
                .OnEventFinish(Zone.Metalworks, 561, q => q.Quest.SetMustZone())
                .OnEventFinish(Zone.Metalworks, 566, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                });
        }
    }
}
