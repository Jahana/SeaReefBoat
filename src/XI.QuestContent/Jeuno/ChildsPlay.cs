// Child's Play (Log ID 3, Quest ID 23), a line-for-line port of scripts/quests/jeuno/Childs_Play.lua (quest rework
// B3b). Karl : !pos -60 0.1 -8 246. Available while The Wonder Magic Set is accepted: Karl's 0 begins it. Accepted:
// Karl's 61; a White Rock traded alone: 1, whose finish completes it (the Wonder Magic Set key item, Trader of
// Mysteries), fame +7 in the three nations and takes the rock. Completed: no section (Karl's default 58).
// The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class ChildsPlay : QuestScript
    {
        public ChildsPlay() : base(QuestLog.Jeuno, "CHILDS_PLAY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Childs_Play";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { KeyItem = KeyItem.WonderMagicSet, Title = Title.TraderOfMysteries });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Jeuno, "THE_WONDER_MAGIC_SET") == QuestStatus.Accepted)
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Karl).OnTrigger(QuestReply.ProgressEvent(0))
                .OnEventFinish(Zone.PortJeuno, 0, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Karl)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.WhiteRock))
                        {
                            return QuestReply.ProgressEvent(1);
                        }
                        return null;
                    })
                    .OnTrigger(QuestReply.Event(61))
                .OnEventFinish(Zone.PortJeuno, 1, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 7);
                        q.Player.AddFame(FameArea.Bastok, 7);
                        q.Player.AddFame(FameArea.Windurst, 7);
                        q.Trade.Confirm();
                    }
                });
        }
    }
}
