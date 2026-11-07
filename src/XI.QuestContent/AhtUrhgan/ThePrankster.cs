// The Prankster (Log ID 6, Quest ID 60), a line-for-line port of scripts/quests/ahtUrhgan/The_Prankster.lua (quest
// rework B3b Aht Urhgan, group 10). Aht Urhgan Whitegate: Ahaadah !pos -70.2 -6 105.2 (no script; default 870); trigger
// areas 11 (22 1 -100 .. 24.5 3 -98) and 12 (25 -7 -127 .. 30 -5 -123). Bhaflau Thickets: qm3 !pos 460.2 -14.9 256.2
// (no script; default NOTHING_HAPPENS), the Plague Chigoe NM (mob script: idle despawn 180, no onMobDeath). Available
// (no other condition): Ahaadah 1, whose finish begins it. Accepted, by Prog (one section each): Prog 0: Ahaadah 15 (an
// Event); area 11: 2 (Prog 1). Prog 1: Ahaadah 16; area 12: 3 (Prog 2). Prog 2: Ahaadah 17; qm3: with the Plague Chigoe
// not spawned, the cutscene 1, whose finish pops it (claimed, the qm not hidden); its death: Prog 3. Prog 3: Ahaadah
// 17; qm3: the cutscene 2, whose finish completes it (the Map of Caedarva Mire). Completed: Ahaadah 18 (an Event,
// replaceDefault). Kept as the Lua has it: 1 begins it on any option; the trigger areas answer at every entry while
// their Prog holds; the chigoe's death sets Prog 3 with no Prog check (the section is Prog 2's); 3's finish returns
// setVar's result.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class ThePrankster : QuestScript
    {
        public ThePrankster() : base(QuestLog.AhtUrghan, "THE_PRANKSTER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/The_Prankster";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.MapOfCaedarvaMire,
            });
            b.Steps(1, 2, 3);

            b.State(QuestStatus.Available)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Ahaadah).OnTrigger(QuestReply.ProgressEvent(1))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 1, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 0)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Ahaadah).OnTrigger(QuestReply.Event(15))
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 11, q => QuestReply.ProgressEvent(2))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 2, q =>
                {
                    q.Quest.SetStep(1);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 1)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Ahaadah).OnTrigger(QuestReply.Event(16))
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 12, q => QuestReply.ProgressEvent(3))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3, q =>
                {
                    q.Quest.SetStep(2);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 2)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Ahaadah).OnTrigger(QuestReply.Event(17))
                .Npc(Zone.BhaflauThickets, Npc.BhaflauThickets.qm3).OnTrigger(q =>
                {
                    if (!q.Player.MobIsSpawned(Mob.BhaflauThickets.PLAGUE_CHIGOE))
                    {
                        return QuestReply.ProgressCutscene(1);
                    }
                    return null;
                })
                .Npc(Zone.BhaflauThickets, Npc.BhaflauThickets.PlagueChigoe).OnMobDeath(q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.BhaflauThickets, 1, q =>
                {
                    // npcUtil.popFromQM(player, npc, ID.mob.PLAGUE_CHIGOE, { hide = 0 }): claim popFromQM's own (true)
                    q.Player.PopFromQm(Mob.BhaflauThickets.PLAGUE_CHIGOE, hide: 0);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 3)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Ahaadah).OnTrigger(QuestReply.Event(17))
                .Npc(Zone.BhaflauThickets, Npc.BhaflauThickets.qm3).OnTrigger(QuestReply.ProgressCutscene(2))
                .OnEventFinish(Zone.BhaflauThickets, 2, q =>
                {
                    q.Quest.Complete();
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Ahaadah)
                    .OnTrigger(QuestReply.Event(18).ReplaceDefault());
        }
    }
}
