// When the Bow Breaks (Log ID 6, Quest ID 45), a line-for-line port of scripts/quests/ahtUrhgan/When_the_Bow_Breaks.lua
// (quest rework B3b Aht Urhgan group 6). Al Zahbi: Gaweesh !pos -64.5 -7 37.9 (no script; default 258).
// Wajaom Woodlands: the Giwahb Watchtower !pos -339.7 -37.2 654.5 (no script; default GIWAHB_WATCHTOWER_LOCKED).
// Available while Ode to the Serpents is accepted: Gaweesh 280, whose finish begins it. Accepted: a trade of exactly
// one Frayed Arrow to the Watchtower: 512, whose finish completes it (Galeserpent Guardian) and, when it does, takes
// the arrow, INCREASED_STANDING and 500 imperial standing.
// Kept as the Lua has it: 280 begins it on any option; the trade is taken only after the completion (confirmTrade
// in 512's finish; the reward is a title only, so the completion never fails).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class WhenTheBowBreaks : QuestScript
    {
        public WhenTheBowBreaks() : base(QuestLog.AhtUrghan, "WHEN_THE_BOW_BREAKS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/When_the_Bow_Breaks";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Title = Title.GaleserpentGuardian,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.AhtUrghan, "ODE_TO_THE_SERPENTS") == QuestStatus.Accepted)
                .Npc(Zone.AlZahbi, Npc.AlZahbi.Gaweesh).OnTrigger(QuestReply.ProgressEvent(280))
                .OnEventFinish(Zone.AlZahbi, 280, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.WajaomWoodlands, Npc.WajaomWoodlands.GiwahbWatchtower).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.FrayedArrow))
                    {
                        return QuestReply.ProgressEvent(512);
                    }
                    return null;
                })
                .OnEventFinish(Zone.WajaomWoodlands, 512, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        q.Text.Special(Text.WajaomWoodlands.INCREASED_STANDING);
                        q.Player.AddCurrency("imperial_standing", 500);
                    }
                });
        }
    }
}
