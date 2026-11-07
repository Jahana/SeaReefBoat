// The Dawn of Delectability (Log ID 7, Quest ID 5), a line-for-line port of
// scripts/quests/crystalWar/The_Dawn_of_Delectability.lua (quest rework B3b Crystal War group 2). Windurst Waters [S]:
// Ranpi-Monpi !pos -115 -3 43. Windurst Waters: Ranpi-Monpi !pos -116 -3 52.
// Available: Ranpi-Monpi [S] 117, whose finish begins it and sets Prog 1. Prog 1: Ranpi-Monpi [S] 119 (optional
// dialogue, no finish); Ranpi-Monpi (present) 978 (-> Prog 2). Prog 2: Ranpi-Monpi: the Pepperoni, Walnut, Dragon Fruit
// and Bastore Sweeper exactly: 983, whose finish confirms the trade, sets Prog 3 and Timer = VanadielUniqueDay() + 1;
// a talk: 979. Prog 3 with Timer still ahead: 982; Timer reached: 980, whose finish gives Ranpi-Monpi's Specialty, Prog
// 4, Timer 0. Prog 4: Ranpi-Monpi [S] 118, whose finish takes the specialty, gives the Culinary Knife, Prog 5. Prog 5:
// Ranpi-Monpi 981, whose finish completes it (a Trainee Knife) and takes the knife. Completed, or Prog 5 at any status:
// Ranpi-Monpi [S] 120 (an Event, replaceDefault).
// Kept as the Lua has it: 117 begins on any option; 119, 979, 982 are progress events with no finish; the last section
// (`status == COMPLETED or vars.Prog == 5`) also answers Ranpi-Monpi [S] with 120 while accepted at Prog 5; a trade of
// anything else answers nil (his script has no onTrade); 980's finish gives the specialty again when it is held (a GM
// reset of Prog: npcUtil.giveKeyItem gives nothing) and still sets Prog 4.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class TheDawnOfDelectability : QuestScript
    {
        public TheDawnOfDelectability() : base(QuestLog.CrystalWar, "THE_DAWN_OF_DELECTABILITY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/The_Dawn_of_Delectability";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4, 5);

            b.Reward(new QuestReward
            {
                Item = Item.TraineeKnife,
            });

            // Visit Ranpi-Monpi (S) in Windurst Waters (S) (E-9, north side) to start the quest.
            b.State(QuestStatus.Available)
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.RanpiMonpi).OnTrigger(QuestReply.ProgressEvent(117))
                .OnEventFinish(Zone.WindurstWatersS, 117, q =>
                {
                    q.Quest.Begin();
                    q.Quest.SetStep(1);
                });

            // Speak with Ranpi in the present and he'll request ingredients
            b.State(QuestStatus.Accepted, step: 1)
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.RanpiMonpi).OnTrigger(QuestReply.ProgressEvent(119)) // Optional dialogue
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.RanpiMonpi).OnTrigger(QuestReply.ProgressEvent(978))
                .OnEventFinish(Zone.WindurstWaters, 978, q =>
                {
                    q.Quest.SetStep(2);
                });

            // Trade Ranpi the ingredients
            b.State(QuestStatus.Accepted, step: 2)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.RanpiMonpi).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.Pepperoni, Item.Walnut, Item.DragonFruit, Item.BastoreSweeper))
                    {
                        return QuestReply.ProgressEvent(983);
                    }
                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.RanpiMonpi).OnTrigger(QuestReply.ProgressEvent(979)) // reminder dialogue
                .OnEventFinish(Zone.WindurstWaters, 983, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetStep(3);
                    q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay + 1);
                });

            // Speak with Ranpi before day is up
            b.State(QuestStatus.Accepted, step: 3, when: q => q.Quest.Var("Timer") > q.Player.VanadielUniqueDay)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.RanpiMonpi).OnTrigger(QuestReply.ProgressEvent(982));

            // Speak with Ranpi after a game day wait
            b.State(QuestStatus.Accepted, step: 3, when: q => q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.RanpiMonpi).OnTrigger(QuestReply.ProgressEvent(980))
                .OnEventFinish(Zone.WindurstWaters, 980, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.RanpiMonpiSpecialty);
                    q.Quest.SetStep(4);
                    q.Quest.SetVar("Timer", 0);
                });

            // Return to Windurst Waters (S) and feed Ranpi.
            b.State(QuestStatus.Accepted, step: 4)
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.RanpiMonpi).OnTrigger(QuestReply.ProgressEvent(118))
                .OnEventFinish(Zone.WindurstWatersS, 118, q =>
                {
                    q.Player.DelKeyItem(KeyItem.RanpiMonpiSpecialty);
                    q.Player.GiveKeyItem(KeyItem.CulinaryKnife);
                    q.Quest.SetStep(5);
                });

            // Return to Windurst Waters and finish the quest
            b.State(QuestStatus.Accepted, step: 5)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.RanpiMonpi).OnTrigger(QuestReply.ProgressEvent(981))
                .OnEventFinish(Zone.WindurstWaters, 981, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.CulinaryKnife);
                    }
                });

            // New default text for Ranpi
            b.State(QuestStatus.Completed)
                .OrAny(step: 5)
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.RanpiMonpi).OnTrigger(QuestReply.Event(120).ReplaceDefault());
        }
    }
}
