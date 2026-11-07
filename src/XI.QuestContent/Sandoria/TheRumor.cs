// The Rumor (Log ID 0, Quest ID 61), a line-for-line port of scripts/quests/sandoria/The_Rumor.lua (quest rework B3b,
// San d'Oria group 5). Novalmauge : !pos 70 -24 21 167. Offered at San d'Oria fame level 3 and level 5: Novalmauge's 13
// (option 1: begun). Accepted: his 11; a trade of exactly a Vial of Beastman Blood plays 12 (completed: Scroll of Drain,
// fame +30, the vial taken). Completed: his 14, which replaces his default 10. goblinfootprint.lua reads the status
// (stays); Trouble at the Sluice needs it completed. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class TheRumor : QuestScript
    {
        public TheRumor() : base(QuestLog.Sandoria, "THE_RUMOR") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/The_Rumor";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 30, FameArea = FameArea.Sandoria, Item = Item.ScrollOfDrain });

            // the level requirement was added in the March 8, 2007 update
            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.Sandoria) >= 3 &&
                    q.Player.MainLevel >= 5)
                .Npc(Zone.BostaunieuxOubliette, Npc.BostaunieuxOubliette.Novalmauge).OnTrigger(QuestReply.ProgressEvent(13))
                .OnEventFinish(Zone.BostaunieuxOubliette, 13, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BostaunieuxOubliette, Npc.BostaunieuxOubliette.Novalmauge)
                    .OnTrigger(QuestReply.Event(11))
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHasExactly(trade, xi.item.VIAL_OF_BEASTMAN_BLOOD)
                        if (q.Trade.HasExactly(Item.VialOfBeastmanBlood))
                        {
                            return QuestReply.ProgressEvent(12);
                        }
                        return null;
                    })
                .OnEventFinish(Zone.BostaunieuxOubliette, 12, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.BostaunieuxOubliette, Npc.BostaunieuxOubliette.Novalmauge).OnTrigger(QuestReply.Event(14).ReplaceDefault());
        }
    }
}
