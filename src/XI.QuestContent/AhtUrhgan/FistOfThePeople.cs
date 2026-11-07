// Fist of the People (Log ID 6, Quest ID 46), a line-for-line port of scripts/quests/ahtUrhgan/Fist_of_the_People.lua
// (quest rework B3b Aht Urhgan group 6). Al Zahbi: Talhaal !pos -36 -6 107 (no script; default 256).
// Aht Urhgan Whitegate: Fari-Wari !pos 80 -6 -137 (no script; default event 831). Wajaom Woodlands: the
// Leypoint !pos -200 -8.5 80 (no script; default LEYPOINT).
// Available while Ode to the Serpents is accepted: Talhaal 282, whose finish begins it. Accepted, Prog 0: Fari-Wari 884
// (Prog 1; a Progress, so it beats Ode's 891). Prog 1: a trade of exactly one Rusty Medal to the Leypoint: 511, whose
// finish completes it (Stoneserpent Shocktrooper) and, when it does, takes the medal, INCREASED_STANDING
// and 500 imperial standing.
// Kept as the Lua has it: 282 begins it on any option; the trade is taken only after the completion (confirmTrade
// in 511's finish; the reward is a title only, so the completion never fails).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class FistOfThePeople : QuestScript
    {
        public FistOfThePeople() : base(QuestLog.AhtUrghan, "FIST_OF_THE_PEOPLE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Fist_of_the_People";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Title = Title.StoneserpentShocktrooper,
            });
            b.Steps(1);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.AhtUrghan, "ODE_TO_THE_SERPENTS") == QuestStatus.Accepted)
                .Npc(Zone.AlZahbi, Npc.AlZahbi.Talhaal).OnTrigger(QuestReply.ProgressEvent(282))
                .OnEventFinish(Zone.AlZahbi, 282, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 0)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.FariWari).OnTrigger(QuestReply.ProgressEvent(884))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 884, q =>
                {
                    q.Quest.SetStep(1);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 1)
                .Npc(Zone.WajaomWoodlands, Npc.WajaomWoodlands.Leypoint).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.RustyMedal))
                    {
                        return QuestReply.ProgressEvent(511);
                    }
                    return null;
                })
                .OnEventFinish(Zone.WajaomWoodlands, 511, q =>
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
