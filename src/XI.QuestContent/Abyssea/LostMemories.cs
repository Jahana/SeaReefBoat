// Lost Memories (Log ID 8, Quest ID 9), a line-for-line port of scripts/quests/abyssea/Lost_Memories.lua (quest rework
// B3b Abyssea). Abyssea - La Theine: Halver !pos 600 40 -515 132 (no script, no default). Available, with
// Abyssea - La Theine fame level below 5: Halver: 162 (an Event). Fame level 5 or more: 165 (an Event), whose finish
// begins it on any option but 0. Accepted: Halver: 163 (an Event); a trade of two Lambent Scales (at least, other items
// allowed): 164 (an Event), whose finish confirms the trade, gives 480 cruor (CRUOR_TOTAL with the new total) and
// completes it (the Vial of Lambent Potion). Kept as the Lua has it: the cruor is given before the completion, which
// cannot fail (a key item); the Lua's TODO (a repeat); no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class LostMemories : QuestScript
    {
        public LostMemories() : base(QuestLog.Abyssea, "LOST_MEMORIES") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/Lost_Memories";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.VialOfLambentPotion,
            });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.AbysseaLatheine) < 5)
                .Npc(Zone.AbysseaLaTheine, Npc.AbysseaLaTheine.Halver).OnTrigger(q =>
                {
                    return QuestReply.Event(162);
                });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.AbysseaLatheine) >= 5)
                .Npc(Zone.AbysseaLaTheine, Npc.AbysseaLaTheine.Halver).OnTrigger(q =>
                {
                    return QuestReply.Event(165);
                })
                .OnEventFinish(Zone.AbysseaLaTheine, 165, q =>
                {
                    if (q.Option != 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.AbysseaLaTheine, Npc.AbysseaLaTheine.Halver).OnTrigger(q =>
                {
                    return QuestReply.Event(163);
                })
                .Npc(Zone.AbysseaLaTheine, Npc.AbysseaLaTheine.Halver).OnTrade(q =>
                {
                    if (q.Trade.HasQty((Item.LambentScale, 2)))
                    {
                        return QuestReply.Event(164);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AbysseaLaTheine, 164, q =>
                {
                    q.Trade.Confirm();
                    //TODO add repeat quest functionality?
                    q.Player.AddCurrency("cruor", 480);
                    q.Text.Special(Text.AbysseaLaTheine.CRUOR_TOTAL, 480, q.Player.Currency("cruor"));
                    q.Quest.Complete();
                });
        }
    }
}
