// The Antique Collector (Log ID 3, Quest ID 25), a line-for-line port of scripts/quests/jeuno/The_Antique_Collector.lua
// (quest rework B3b). Imasuke : !pos -165 11 94 246. Available at Jeuno fame level 2: Imasuke's 13, option 1
// begins it. Accepted: 14; a Kaiser Sword traded alone: 15, whose finish completes it (2000 exp, 2000 gil, the Map of
// Delkfutt's Tower, Trader of Antiquities), fame +10 in the three nations and takes the sword.
// The Lua file has no known bug to keep (its TODO on the reward is kept as the reward).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class TheAntiqueCollector : QuestScript
    {
        public TheAntiqueCollector() : base(QuestLog.Jeuno, "THE_ANTIQUE_COLLECTOR") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/The_Antique_Collector";

        protected override void Build(QuestScriptBuilder b)
        {
            // TODO: Quest reward has conflicting information from various resources.  Need to confirm
            // that XP and Gil rewards are also given when the player does not have the KI reward.
            b.Reward(new QuestReward
            {
                Exp = 2000,
                Gil = 2000,
                KeyItem = KeyItem.MapOfDelkfuttsTower,
                Title = Title.TraderOfAntiquities,
            });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Jeuno) >= 2)
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Imasuke).OnTrigger(QuestReply.ProgressEvent(13))
                .OnEventFinish(Zone.PortJeuno, 13, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Imasuke)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.KaiserSword))
                        {
                            return QuestReply.ProgressEvent(15);
                        }
                        return null;
                    })
                    .OnTrigger(QuestReply.Event(14))
                .OnEventFinish(Zone.PortJeuno, 15, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 10);
                        q.Player.AddFame(FameArea.Bastok, 10);
                        q.Player.AddFame(FameArea.Windurst, 10);
                        q.Trade.Confirm();
                    }
                });
        }
    }
}
