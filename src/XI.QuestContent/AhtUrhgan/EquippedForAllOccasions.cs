// Equipped for All Occasions (COR AF1; Log ID 6, Quest ID 24), a line-for-line port of
// scripts/quests/ahtUrhgan/COR_AF1_Equipped_for_All_Occasions.lua (quest rework B3b Aht Urhgan, group 2). Arrapago Reef:
// qm6 !pos 469 -12 112 (no script; default NOTHING_OUT_OF_ORDINARY). Maze of Shakhrami: _5i0 the Iron Door !pos 248 18
// -142 (its script: the Rusty Key's trade, cutscene 42 from x >= 248, else MIGHT_BE_ABLE_TO_OPEN), the Lost Soul
// (LOST_SOUL). Aht Urhgan Whitegate: Ratihb !pos 75 -6 -137 (no script; default 603).
// Available to a COR at AF1_QUEST_LEVEL (40): qm6 228, whose finish begins it at Prog 1. Accepted: _5i0 at Prog 1 pops
// the Lost Soul (popFromQM, the door not hidden) and answers nothing (the door's script then runs); at Prog 2 66 (the
// Wheel Lock Trigger, Prog 3); the Lost Soul's death at Prog 1: Prog 2; qm6 Prog 3: 231 (the trigger taken, Prog 4);
// Ratihb at Prog 4 with Stage 1 (Luck of the Draw's 552 sets it): 772, whose finish completes it (a Trump Gun).
// Kept as the Lua has it: the pop's nil lets the door's own script answer the same talk; 772 ignores complete's result;
// 66's key item given whatever the char holds.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class EquippedForAllOccasions : QuestScript
    {
        public EquippedForAllOccasions() : base(QuestLog.AhtUrghan, "EQUIPPED_FOR_ALL_OCCASIONS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/COR_AF1_Equipped_for_All_Occasions";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.TrumpGun });
            b.Steps(1, 2, 3, 4);

            // Section: Quest
            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainJob == Job.Cor &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF1_QUEST_LEVEL"))
                .Npc(Zone.ArrapagoReef, Npc.ArrapagoReef.qm6).OnTrigger(q => QuestReply.ProgressEvent(228))
                .OnEventFinish(Zone.ArrapagoReef, 228, q =>
                {
                    q.Quest.Begin();
                    q.Quest.SetStep(1);
                });

            // Section: Quest accepted
            b.State(QuestStatus.Accepted)
                .Npc(Zone.MazeOfShakhrami, Npc.MazeOfShakhrami._5i0).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 1)
                    {
                        q.Player.PopFromQm(Mob.MazeOfShakhrami.LOST_SOUL, 0);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(66);
                    }
                    return null;
                })
                .Npc(Zone.MazeOfShakhrami, Npc.MazeOfShakhrami.LostSoul).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        q.Quest.SetStep(2);
                    }
                })
                .OnEventFinish(Zone.MazeOfShakhrami, 66, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.WheelLockTrigger);
                    q.Quest.SetStep(3);
                })
                .Npc(Zone.ArrapagoReef, Npc.ArrapagoReef.qm6).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(231);
                    }
                    return null;
                })
                .OnEventFinish(Zone.ArrapagoReef, 231, q =>
                {
                    q.Player.DelKeyItem(KeyItem.WheelLockTrigger);
                    q.Quest.SetStep(4);
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Ratihb).OnTrigger(q =>
                {
                    // Variable 'Stage' is set on quest 'Luck of the Draw', on section 'Quest completed'.
                    if (q.Quest.Step == 4 && q.Quest.Var("Stage") == 1)
                    {
                        return QuestReply.ProgressEvent(772);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 772, q =>
                {
                    q.Quest.Complete();
                });
        }
    }
}
