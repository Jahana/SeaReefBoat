// Wandering Souls (Log ID 5, Quest ID 161), a line-for-line port of scripts/quests/outlands/Wandering_Souls.lua (quest
// rework B3b Outlands, group 5). Cape Teriggan: Cermet_Headstone !pos -107 -8 450 (no script; default
// CANNOT_REMOVE_FRAG; Rhapsodies of Zilart 5 Headstone Pilgrimage's monument: its 201 finish adds this quest; with the
// Wind Fragment held it answers nil until this quest is completed, then ALREADY_OBTAINED_FRAG; completed, its
// replaceDefault ZILART_MONUMENT ranks below this quest's message: #kinds).
// Accepted only (the mission begins it): the headstone answers MUST_BE_A_WAY_TO_SOOTHE; a Rain Lily alone traded plays
// 202 (the lily), whose finish runs quest:complete (the Flagellant's Rope, Bearer of Bonds Beyond Time) and on true
// confirms the trade.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class WanderingSouls : QuestScript
    {
        public WanderingSouls() : base(QuestLog.Outlands, "WANDERING_SOULS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/Wandering_Souls";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.FlagellantsRope,
                Title = Title.BearerOfBondsBeyondTime,
            });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.CapeTeriggan, Npc.CapeTeriggan.CermetHeadstone)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.RainLily))
                        {
                            return QuestReply.ProgressEvent(202, Item.RainLily);
                        }
                        return null;
                    })
                    .OnTrigger(QuestReply.MessageSpecial(Text.CapeTeriggan.MUST_BE_A_WAY_TO_SOOTHE))
                .OnEventFinish(Zone.CapeTeriggan, 202, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                });
        }
    }
}
