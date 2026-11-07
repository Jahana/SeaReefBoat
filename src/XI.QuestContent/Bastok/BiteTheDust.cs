// Bite the Dust (Log ID 1, Quest ID 46), a line-for-line port of scripts/quests/bastok/Bite_the_Dust.lua (quest rework
// B3b). Yazan : !pos -20.06 -3.3 24.471 236. Kept as the Lua file has it: the second section matches accepted and
// completed, so every later Sand Bat Fang trade completes the quest again, with its gil, fame and title.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class BiteTheDust : QuestScript
    {
        public BiteTheDust() : base(QuestLog.Bastok, "BITE_THE_DUST") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Bite_the_Dust";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 10, FameArea = FameArea.Bastok, Gil = 350, Title = Title.SandBlaster });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Bastok) >= 2)
                .Npc(Zone.PortBastok, Npc.PortBastok.Yazan).OnTrigger(QuestReply.ProgressEvent(191))
                .OnEventFinish(Zone.PortBastok, 191, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Or(QuestStatus.Completed)
                .Npc(Zone.PortBastok, Npc.PortBastok.Yazan)
                    .OnTrade(q => q.Trade.Matches((Item.SandBatFang, 1)) ? QuestReply.ProgressEvent(193) : null)
                    // 192 while accepted, 194 once per zone-in after
                    .OnTrigger(q => q.Quest.Status == QuestStatus.Accepted
                        ? QuestReply.Event(192)
                        : QuestReply.Event(194).OncePerZone())
                .OnEventFinish(Zone.PortBastok, 193, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                });
        }
    }
}
