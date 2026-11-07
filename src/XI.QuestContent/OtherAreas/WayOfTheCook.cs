// Way of the Cook (Log ID 4, Quest ID 1), a line-for-line port of scripts/quests/otherAreas/RQ2_Way_of_the_Cook.lua (quest
// rework B3b). Mhaura: Rycharde !pos 17.451 -16.000 88.815 249. Available once Rycharde the Chef is completed:
// Rycharde, when Rycharde the Chef's DayCompleted + 8 <= the Vana'diel day and Windurst fame level >= 3: 76 (the Beehive
// Chip and the meat), whose finish with option 74 clears Quest[4][0]DayCompleted, sets EndTime to VanadielTime() + 3
// Vana'diel days (3 x 3456 earth seconds) and begins the quest; else 75. Take: 68. Accepted: Rycharde, before EndTime:
// 78 with the hours left (math.ceil of the seconds left over 144), from EndTime on: 79; a Slice of Dhalmel Meat and a
// Beehive Chip alone: 80 before EndTime, 81 from it, whose finishes complete the quest (fame 20 Windurst, One Star
// Purveyor), take the trade and pay 1500 / 1000 gil; the meat alone or the chip alone: 73. Take: 65.
// Kept as the Lua has it: the gil comes after quest:complete, through npcUtil.giveCurrency, not in the reward table; 80 or
// 81 is chosen at the trade, so a finish after EndTime still pays 1500 when the trade came before it.
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class WayOfTheCook : QuestScript
    {
        public WayOfTheCook() : base(QuestLog.OtherAreas, "WAY_OF_THE_COOK") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/RQ2_Way_of_the_Cook";

        // xi.vanaTime.DAY and HOUR, in earth seconds
        private const int VanaDay = 3456;
        private const int VanaHour = 144;

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Windurst,
                Title = Title.OneStarPurveyor,
            });

            // Section: Check if quest is available.
            b.State(QuestStatus.Available, when: q => q.Player.QuestStatus(QuestLog.OtherAreas, "RYCHARDE_THE_CHEF") == QuestStatus.Completed)
                .Npc(Zone.Mhaura, Npc.Mhaura.Rycharde).OnTrigger(q =>
                {
                    if (q.Player.CharVar("Quest[4][0]DayCompleted") + 8 <= q.Player.VanadielUniqueDay &&
                        q.Player.FameLevel(FameArea.Windurst) >= 3)
                    {
                        return QuestReply.ProgressEvent(76, Item.BeehiveChip, Item.SliceOfDhalmelMeat); // Way of the Cook starting event.
                    }
                    else
                    {
                        return QuestReply.Event(75); // Default dialog after completing previous quest.
                    }
                })
                .Npc(Zone.Mhaura, Npc.Mhaura.Take).OnTrigger(QuestReply.Event(68))
                .OnEventFinish(Zone.Mhaura, 76, q =>
                {
                    if (q.Option == 74) // Accept quest option.
                    {
                        q.Player.SetCharVar("Quest[4][0]DayCompleted", 0); // Delete previous quest (Rycharde the Chef) variables
                        q.Quest.SetVar("EndTime", q.Player.VanadielTime + 3 * VanaDay); // Set current quest started variables
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted. Handle trade and time limit
            b.State(QuestStatus.Accepted)
                .Npc(Zone.Mhaura, Npc.Mhaura.Rycharde)
                    .OnTrigger(q =>
                    {
                        int currentTime = q.Player.VanadielTime;
                        int endTime = q.Quest.Var("EndTime");

                        if (currentTime < endTime)
                        {
                            int hoursLeft = (int)Math.Ceiling((endTime - currentTime) / (double)VanaHour);
                            return QuestReply.Event(78, hoursLeft); // You have x hours left.
                        }
                        else
                        {
                            return QuestReply.Event(79); // Not done yet.
                        }
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.SliceOfDhalmelMeat, Item.BeehiveChip))
                        {
                            int currentTime = q.Player.VanadielTime;
                            int endTime = q.Quest.Var("EndTime");

                            if (currentTime < endTime)
                            {
                                return QuestReply.ProgressEvent(80); // Quest completed in time.
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(81); // Quest completed late.
                            }
                        }
                        else if (q.Trade.HasExactly(Item.SliceOfDhalmelMeat) ||
                                 q.Trade.HasExactly(Item.BeehiveChip))
                        {
                            return QuestReply.Event(73); // Incomplete trade.
                        }
                        return null;
                    })
                .Npc(Zone.Mhaura, Npc.Mhaura.Take).OnTrigger(QuestReply.Event(65))
                .OnEventFinish(Zone.Mhaura, 80, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        q.Player.GiveCurrency("gil", 1500);
                        q.Quest.SetVar("DayCompleted", q.Player.VanadielUniqueDay); // Set completition day of WAY_OF_THE_COOK quest.
                    }
                })
                .OnEventFinish(Zone.Mhaura, 81, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        q.Player.GiveCurrency("gil", 1000);
                        q.Quest.SetVar("DayCompleted", q.Player.VanadielUniqueDay); // Set completition day of WAY_OF_THE_COOK quest.
                    }
                });
        }
    }
}
