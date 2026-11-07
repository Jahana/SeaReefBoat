// Seeing Spots (Log ID 7, Quest ID 10), a line-for-line port of scripts/quests/crystalWar/Seeing_Spots.lua (quest
// rework B3b Crystal War group 11). Southern San d'Oria [S]: Wyatt !pos 124.017 0 84.169 (no script; default
// WYATT_DIALOG).
// Available: Wyatt 2, whose finish begins it. Accepted or completed (one state, as the Lua's one section: two states
// would both run 4's finish, the second after the completion): Wyatt 3 (a progress event: the reminder); four Ladybug
// Wings alone: 4, whose finish completes it (3000 gil, the title Lady Killer) and then confirms the trade.
// Kept as the Lua has it: 2 begins on any option; the completed char trades again and quest:complete runs again (the
// gil and the title again, the completed bit again, the vars cleaned): repeatable; the reminder 3 is a progress event
// after the completion too; no completed default.
// Rival containers: Wrath of the Griffon's completed Wyatt 66 (setPriority 1005, with the Military Scrip) ranks over 2
// / 3 whatever their kind (crystalWar.wrath_of_the_griffon plays 66 over 2). Wyatt has no script; the kinds are pinned
// in tests/XI.Verify.Quests/CrystalWarGroup11.cs. San d'Oria's Atellounes Lament reads the completed status (a status
// read).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class SeeingSpots : QuestScript
    {
        public SeeingSpots() : base(QuestLog.CrystalWar, "SEEING_SPOTS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/Seeing_Spots";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Gil = 3000,
                Title = Title.LadyKiller,
            });

            // After speaking with Wyatt, collect four Ladybug Wings.
            b.State(QuestStatus.Available)
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Wyatt).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(2);
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 2, q =>
                {
                    q.Quest.Begin();
                });

            // Trade the wings to Wyatt to receive your reward (the Lua's one section: accepted or completed; one
            // state, so 4's finish runs once although the completion changes the status)
            b.AnyState(when: q => q.Quest.Status == QuestStatus.Accepted || q.Quest.Status == QuestStatus.Completed)
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Wyatt).OnTrigger(q =>
                {
                    // Reminder
                    return QuestReply.ProgressEvent(3);
                })
                .OnTrade(q =>
                {
                    if (q.Trade.HasExactly((Item.LadybugWing, 4)))
                    {
                        return QuestReply.ProgressEvent(4);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 4, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                });
        }
    }
}
