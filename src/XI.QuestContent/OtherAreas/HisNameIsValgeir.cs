// His Name is Valgeir (Log ID 4, Quest ID 3), a line-for-line port of scripts/quests/otherAreas/RQ4_His_Name_is_Valgeir.lua
// (quest rework B3b). Mhaura: Rycharde !pos 17.451 -16.000 88.815 249, Felisa (the ferry's ticket seller); Selbina:
// Valgeir !pos 57.496 -15.273 20.229 248. Available once Unending Chase is completed: Rycharde, when Unending Chase's
// DayCompleted + 2 < the Vana'diel day and Windurst fame level >= 3: 86, else 75; 86's finish with option 80 or 81 clears
// Quest[4][2]DayCompleted, gives the Aragoneu Pizza (npcUtil.giveKeyItem) and begins the quest. Take: 65. Accepted:
// Felisa, with the pizza, the char's z above 38.5 and Prog 0: 230 (a free ferry ride, once), whose finish sets Prog 1;
// otherwise nil (her script: 221 or 235). Rycharde: without the pizza 88, whose finish completes the quest (fame 40
// Windurst, 2000 gil, the Map of the Toraimarai Canal), adds 2000 exp and stores DayCompleted; with it 87. Take: 65.
// Valgeir: with the pizza 100, whose finish takes it (no message); without 101.
// Kept as the Lua has it: 230's finish sets Prog 1 whatever the option, and any 230 finished in Mhaura while the quest is
// accepted does (Felisa's own 230, if her script ever played it); the exp comes after quest:complete (player:addExp).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class HisNameIsValgeir : QuestScript
    {
        public HisNameIsValgeir() : base(QuestLog.OtherAreas, "HIS_NAME_IS_VALGEIR") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/RQ4_His_Name_is_Valgeir";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 40,
                FameArea = FameArea.Windurst,
                Gil = 2000,
                KeyItem = KeyItem.MapOfTheToraimaraiCanal,
            });
            b.Steps(1);

            // Section: Quest is available.
            b.State(QuestStatus.Available, when: q => q.Player.QuestStatus(QuestLog.OtherAreas, "UNENDING_CHASE") == QuestStatus.Completed)
                .Npc(Zone.Mhaura, Npc.Mhaura.Rycharde).OnTrigger(q =>
                {
                    if (q.Player.CharVar("Quest[4][2]DayCompleted") + 2 < q.Player.VanadielUniqueDay &&
                        q.Player.FameLevel(FameArea.Windurst) >= 3)
                    {
                        return QuestReply.ProgressEvent(86); // His Name is Valgeir starting event.
                    }
                    else
                    {
                        return QuestReply.Event(75);
                    }
                })
                .Npc(Zone.Mhaura, Npc.Mhaura.Take).OnTrigger(QuestReply.Event(65))
                .OnEventFinish(Zone.Mhaura, 86, q =>
                {
                    if (q.Option == 80 || q.Option == 81) // Accept quest option.
                    {
                        q.Player.SetCharVar("Quest[4][2]DayCompleted", 0); // Delete previous quest (Unending Chase) variables
                        q.Player.GiveKeyItem(KeyItem.AragoneuPizza); // Give pizza to player
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepeted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.Mhaura, Npc.Mhaura.Felisa).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.AragoneuPizza) && // No free ride after delivering Pizza.
                        q.Player.ZPos > 38.5 && // Pos check.
                        q.Quest.Step == 0) // Hasn't taken the free ride.
                    {
                        return QuestReply.ProgressEvent(230); // Free ride. 1 time only. Non-Mandatory step.
                    }
                    return null;
                })
                .Npc(Zone.Mhaura, Npc.Mhaura.Rycharde).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.AragoneuPizza))
                    {
                        return QuestReply.ProgressEvent(88); // Finish quest.
                    }
                    else
                    {
                        return QuestReply.Event(87); // Not delivered the pizza yet.
                    }
                })
                .Npc(Zone.Mhaura, Npc.Mhaura.Take).OnTrigger(QuestReply.Event(65))
                .OnEventFinish(Zone.Mhaura, 88, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddExp(2000);
                        q.Quest.SetVar("DayCompleted", q.Player.VanadielUniqueDay); // Set completition day of quest.
                    }
                })
                .OnEventFinish(Zone.Mhaura, 230, q =>
                {
                    q.Quest.SetStep(1);
                })
                .Npc(Zone.Selbina, Npc.Selbina.Valgeir).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.AragoneuPizza))
                    {
                        return QuestReply.ProgressEvent(100); // Deliver Pizza.
                    }
                    else
                    {
                        return QuestReply.Event(101); // Pizza delivered.
                    }
                })
                .OnEventFinish(Zone.Selbina, 100, q =>
                {
                    q.Player.DelKeyItem(KeyItem.AragoneuPizza);
                });
        }
    }
}
