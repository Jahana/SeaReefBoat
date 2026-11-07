// Elder Memories (Log ID 4, Quest ID 24), a line-for-line port of scripts/quests/otherAreas/Elder_Memories.lua (quest
// rework B3b): the subjob quest of Selbina. Isacio !pos -54 -1 -44 248 (no script, no default); Mhaura's Vera.
// Mutually exclusive with The Old Lady (each available check needs the other available). Available: Isacio at main level
// >= xi.settings.main.SUBJOB_QUEST_LEVEL (18; read as Lua has it, a fraction kept): 111 (the Magicked Skull), whose
// finish with option 40 begins the quest; below it: 119. Accepted: Vera (Mhaura) 130 replaceDefault. Isacio, with
// Gilgamesh's Introductory Letter: 117; else at Prog 0 / 1 / 2: 114 (the item asked for: the Magicked Skull, the
// Damselfly Worm, the Crab Apron); other Prog: nil. A trade of exactly the Prog's item (npcUtil.tradeMatches, one of it):
// Prog 0: 115 (the next item), Prog 1: 116 (the next item), Prog 2: 117 (no argument). 115 / 116 finished: the trade
// taken, Prog 1 / 2. 117 finished: completed (no reward table), the trade taken, the subjob unlocked (unlockJob(0)) with
// SUBJOB_UNLOCKED. Completed: Vera 130 replaceDefault, Isacio 118 replaceDefault.
// Kept as the Lua has it: with the letter, 117 plays at any Prog, and its finish calls tradeComplete with no trade open
// (which takes what the last trade left in the trade container); the letter is kept; the finishes are keyed on the csid
// alone.
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class ElderMemories : QuestScript
    {
        public ElderMemories() : base(QuestLog.OtherAreas, "ELDER_MEMORIES") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Elder_Memories";

        // subJobItems[Prog]: the item asked for and the event its trade plays (a Prog outside 0..2 has no entry)
        private static readonly Dictionary<int, (int Item, int TradeEvent)> SubJobItems = new Dictionary<int, (int, int)>
        {
            [0] = (Item.MagickedSkull, 115),
            [1] = (Item.DamselflyWorm, 116),
            [2] = (Item.CrabApron, 117),
        };

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2);

            // Section: Quest is available.
            b.State(QuestStatus.Available, when: q => q.Player.QuestStatus(QuestLog.OtherAreas, "THE_OLD_LADY") == QuestStatus.Available)
                .Npc(Zone.Selbina, Npc.Selbina.Isacio).OnTrigger(q =>
                {
                    if (q.Player.MainLevel >= q.Settings.MainNumber("SUBJOB_QUEST_LEVEL"))
                    {
                        return QuestReply.ProgressEvent(111, Item.MagickedSkull); // Player is eligible for the quest.
                    }
                    else
                    {
                        return QuestReply.Event(119); // Player level is too low to be offered the quest.
                    }
                })
                .OnEventFinish(Zone.Selbina, 111, q =>
                {
                    if (q.Option == 40) // Accepted quest.
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.Mhaura, Npc.Mhaura.Vera).OnTrigger(QuestReply.Event(130).ReplaceDefault())
                .Npc(Zone.Selbina, Npc.Selbina.Isacio)
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
                            return QuestReply.ProgressEvent(117);
                        }

                        if (SubJobItems.TryGetValue(q.Quest.Step, out var step))
                        {
                            return QuestReply.Event(114, step.Item);
                        }
                        return null;
                    })
                .OnEventFinish(Zone.Selbina, 115, q =>
                {
                    q.Trade.Complete();
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.Selbina, 116, q =>
                {
                    q.Trade.Complete();
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.Selbina, 117, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        q.Player.UnlockJob(0);
                        q.Text.Special(Text.Selbina.SUBJOB_UNLOCKED);
                    }
                });

            // Section: Quest completed.
            b.State(QuestStatus.Completed)
                .Npc(Zone.Mhaura, Npc.Mhaura.Vera).OnTrigger(QuestReply.Event(130).ReplaceDefault())
                .Npc(Zone.Selbina, Npc.Selbina.Isacio).OnTrigger(QuestReply.Event(118).ReplaceDefault());
        }
    }
}
