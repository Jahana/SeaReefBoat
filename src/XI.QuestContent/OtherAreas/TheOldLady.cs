// The Old Lady (Log ID 4, Quest ID 10), a line-for-line port of scripts/quests/otherAreas/The_Old_Lady.lua (quest rework
// B3b): the subjob quest of Mhaura. Vera !pos -49 -5 20 249 (no script, no default); Selbina's Isacio. Mutually
// exclusive with Elder Memories (each available check needs the other available). Available: Vera at main level >=
// xi.settings.main.SUBJOB_QUEST_LEVEL (18; read as Lua has it, a fraction kept): 131 (the Wild Rabbit Tail), whose finish
// with option 40 begins the quest; below it: 133. Accepted: Isacio (Selbina) 99 replaceDefault. Vera, with Gilgamesh's
// Introductory Letter: 137; else at Prog 0 / 1 / 2: 132 (the item asked for: the Wild Rabbit Tail, the Cup of Dhalmel
// Saliva, the Bloody Robe); other Prog: nil. A trade of exactly the Prog's item (npcUtil.tradeMatches, one of it): Prog
// 0: 135 (the next item), Prog 1: 136 (the next item), Prog 2: 137 (no argument). 135 / 136 finished: the trade taken,
// Prog 1 / 2. 137 finished: completed (no reward table), the trade taken, the subjob unlocked (unlockJob(0)) with
// SUBJOB_UNLOCKED. Completed: Vera 138 replaceDefault, Isacio 99 replaceDefault.
// Kept as the Lua has it: with the letter, 137 plays at any Prog, and its finish calls tradeComplete with no trade open
// (which takes what the last trade left in the trade container); the letter is kept; the finishes are keyed on the csid
// alone.
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class TheOldLady : QuestScript
    {
        public TheOldLady() : base(QuestLog.OtherAreas, "THE_OLD_LADY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/The_Old_Lady";

        // subJobItems[Prog]: the item asked for and the event its trade plays (a Prog outside 0..2 has no entry)
        private static readonly Dictionary<int, (int Item, int TradeEvent)> SubJobItems = new Dictionary<int, (int, int)>
        {
            [0] = (Item.WildRabbitTail, 135),
            [1] = (Item.CupOfDhalmelSaliva, 136),
            [2] = (Item.BloodyRobe, 137),
        };

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2);

            // Section: Quest is available
            b.State(QuestStatus.Available, when: q => q.Player.QuestStatus(QuestLog.OtherAreas, "ELDER_MEMORIES") == QuestStatus.Available)
                .Npc(Zone.Mhaura, Npc.Mhaura.Vera).OnTrigger(q =>
                {
                    if (q.Player.MainLevel >= q.Settings.MainNumber("SUBJOB_QUEST_LEVEL"))
                    {
                        return QuestReply.ProgressEvent(131, Item.WildRabbitTail); // Player is eligible for the quest.
                    }
                    else
                    {
                        return QuestReply.Event(133); // Player level is too low to be offered the quest.
                    }
                })
                .OnEventFinish(Zone.Mhaura, 131, q =>
                {
                    if (q.Option == 40) // Accepted quest.
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted
            b.State(QuestStatus.Accepted)
                .Npc(Zone.Mhaura, Npc.Mhaura.Vera)
                    .OnTrade(q =>
                    {
                        int questProgress = q.Quest.Step;

                        if (SubJobItems.TryGetValue(questProgress, out var step) && q.Trade.Matches((step.Item, 1)))
                        {
                            if (SubJobItems.TryGetValue(questProgress + 1, out var nextStep))
                            {
                                return QuestReply.ProgressEvent(step.TradeEvent, nextStep.Item);
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(step.TradeEvent);
                            }
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        // Obtained from Rhapsodies of Vanadiel 1-4, allows player to skip the item collection.
                        if (q.Player.HasKeyItem(KeyItem.GilgameshsIntroductoryLetter))
                        {
                            return QuestReply.ProgressEvent(137);
                        }

                        if (SubJobItems.TryGetValue(q.Quest.Step, out var step))
                        {
                            return QuestReply.Event(132, step.Item);
                        }
                        return null;
                    })
                .OnEventFinish(Zone.Mhaura, 135, q =>
                {
                    q.Trade.Complete();
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.Mhaura, 136, q =>
                {
                    q.Trade.Complete();
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.Mhaura, 137, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        q.Player.UnlockJob(0);
                        q.Text.Special(Text.Mhaura.SUBJOB_UNLOCKED);
                    }
                })
                .Npc(Zone.Selbina, Npc.Selbina.Isacio).OnTrigger(QuestReply.Event(99).ReplaceDefault());

            // Section: Quest completed
            b.State(QuestStatus.Completed)
                .Npc(Zone.Mhaura, Npc.Mhaura.Vera).OnTrigger(QuestReply.Event(138).ReplaceDefault())
                .Npc(Zone.Selbina, Npc.Selbina.Isacio).OnTrigger(QuestReply.Event(99).ReplaceDefault());
        }
    }
}
