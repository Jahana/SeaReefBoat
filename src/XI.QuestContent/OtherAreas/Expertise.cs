// Expertise (Log ID 4, Quest ID 4), a line-for-line port of scripts/quests/otherAreas/RQ5_Expertise.lua (quest rework B3b).
// Mhaura: Take !pos 20.616 -8.000 69.757 249, Rycharde; Selbina: Valgeir !pos 57.496 -15.273 20.229 248.
// Available once His Name is Valgeir is completed: Rycharde 89 (fixed); Take, at Windurst fame level >= 3 and His Name is
// Valgeir's DayCompleted + 8 < the Vana'diel day: 61, whose finish (any option) clears Quest[4][3]DayCompleted and begins
// the quest; else 65. Accepted: Valgeir at Prog 0: 102 (the Scream Fungus and the crab meat), finish Prog 1; Prog 1: 104;
// Prog 2: readyTime <= GetSystemTime(): 105, whose finish sets Prog 3 and gives the Land Crab Bisque, else 141; Prog 3:
// 142; any other Prog: nil (his default 140). A Scream Fungus and a Slice of Land Crab Meat alone (at any Prog): 103,
// whose finish sets Prog 2, readyTime GetSystemTime() + 3456 (24 x 144) and confirms the trade. Take at Prog 3: 62 (the
// Tableware Set), whose finish completes the quest (the Tableware Set, Three Star Purveyor), takes the bisque with
// KEYITEM_OBTAINED + 1 (the "returned" line) and stores DayCompleted; else 63. Rycharde while accepted: no handler (69).
// Kept as the Lua has it: the trade answers at any Prog (traded at Prog 0, 1 or 3 it sets Prog 2 again and restarts the
// wait); 61 begins the quest whatever the option; the bisque goes only when quest:complete gives the set (a full
// inventory keeps the bisque and Prog 3).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class Expertise : QuestScript
    {
        public Expertise() : base(QuestLog.OtherAreas, "EXPERTISE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/RQ5_Expertise";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.TablewareSet,
                Title = Title.ThreeStarPurveyor,
            });
            b.Steps(1, 2, 3);

            // Section: Quest is available.
            b.State(QuestStatus.Available, when: q => q.Player.QuestStatus(QuestLog.OtherAreas, "HIS_NAME_IS_VALGEIR") == QuestStatus.Completed)
                .Npc(Zone.Mhaura, Npc.Mhaura.Rycharde).OnTrigger(QuestReply.Event(89))
                .Npc(Zone.Mhaura, Npc.Mhaura.Take).OnTrigger(q =>
                {
                    if (q.Player.FameLevel(FameArea.Windurst) >= 3 &&
                        q.Player.CharVar("Quest[4][3]DayCompleted") + 8 < q.Player.VanadielUniqueDay)
                    {
                        return QuestReply.ProgressEvent(61); // Quest starting event.
                    }
                    else
                    {
                        return QuestReply.Event(65);
                    }
                })
                .OnEventFinish(Zone.Mhaura, 61, q =>
                {
                    q.Player.SetCharVar("Quest[4][3]DayCompleted", 0); // Delete Variable
                    q.Quest.Begin();
                });

            // Section: Quest accepeted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.Mhaura, Npc.Mhaura.Take).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(62, Item.TablewareSet); // Give dish from Valgeir.
                    }
                    else
                    {
                        return QuestReply.Event(63); // Not goten the dish from Valgeir.
                    }
                })
                .OnEventFinish(Zone.Mhaura, 62, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.LandCrabBisque); // Give Land Crab Bisque from Valgeir.
                        q.Text.Special(q.Text.Id(Text.Mhaura.KEYITEM_OBTAINED) + 1, KeyItem.LandCrabBisque);
                        q.Quest.SetVar("DayCompleted", q.Player.VanadielUniqueDay); // Completition day of Expertise quest.
                    }
                })
                .Npc(Zone.Selbina, Npc.Selbina.Valgeir)
                    .OnTrigger(q =>
                    {
                        int questProgress = q.Quest.Step;

                        if (questProgress == 0)
                        {
                            return QuestReply.ProgressEvent(102, Item.ScreamFungus, Item.SliceOfLandCrabMeat); // Ask for ingredients to cook.
                        }
                        else if (questProgress == 1)
                        {
                            return QuestReply.Event(104); // Reminder.
                        }
                        else if (questProgress == 2)
                        {
                            int readyTime = q.Quest.Var("readyTime");

                            if (readyTime <= q.Player.SystemTime) // Done waiting
                            {
                                return QuestReply.ProgressEvent(105); // Get food.
                            }
                            else
                            {
                                return QuestReply.Event(141);
                            }
                        }
                        else if (questProgress == 3)
                        {
                            return QuestReply.Event(142);
                        }
                        return null;
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.ScreamFungus, Item.SliceOfLandCrabMeat))
                        {
                            return QuestReply.ProgressEvent(103); // Give ingredients.
                        }
                        return null;
                    })
                .OnEventFinish(Zone.Selbina, 102, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.Selbina, 103, q =>
                {
                    q.Quest.SetStep(2);
                    q.Quest.SetVar("readyTime", q.Player.SystemTime + 24 * 144); // Current time + 24 hours * 144 real seconds (Thats the ammount of seconds a Vana'Diel hour takes)
                    q.Trade.Confirm();
                })
                .OnEventFinish(Zone.Selbina, 105, q =>
                {
                    q.Quest.SetStep(3);
                    q.Player.GiveKeyItem(KeyItem.LandCrabBisque);
                });
        }
    }
}
