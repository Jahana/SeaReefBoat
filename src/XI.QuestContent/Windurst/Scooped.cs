// Scooped! (Log ID 2, Quest ID 38), a line-for-line port of scripts/quests/windurst/Scooped.lua (quest rework B3b).
// Hariga-Origa : !pos -70.244, -3.800, -4.439 (as the Lua header has it; the quest names Naiko-Paneiko and Chyuk-Kochak).
// After Making Headlines (an NPC-script quest): Naiko-Paneiko's 676 begins it; a Bronze Box traded to him completes it
// (680: fame +20, 1500 gil).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class Scooped : QuestScript
    {
        public Scooped() : base(QuestLog.Windurst, "SCOOPED") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Scooped";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Windurst, Gil = 1500 });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Windurst, "MAKING_HEADLINES"))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.NaikoPaneiko).OnTrigger(QuestReply.ProgressEvent(676))
                .OnEventFinish(Zone.WindurstWaters, 676, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.NaikoPaneiko)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.BronzeBox))
                        {
                            return QuestReply.ProgressEvent(680);
                        }

                        return null;
                    })
                    .OnTrigger(QuestReply.Event(677))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.ChyukKochak).OnTrigger(QuestReply.Event(679))
                .OnEventFinish(Zone.WindurstWaters, 680, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.Complete();
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.ChyukKochak).OnTrigger(QuestReply.Event(682).ReplaceDefault())
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.NaikoPaneiko).OnTrigger(QuestReply.Event(663).ReplaceDefault());
        }
    }
}
