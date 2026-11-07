// In a Pickle (Log ID 2, Quest ID 5), ported from scripts/zones/Windurst_Waters/npcs/Chamama.lua (quest rework B3b,
// Windurst group 15; call-only: every answer of her script is this quest's, her standard 651 included, so her script
// only calls it). Chamama : !pos -70 -4 -4 238. Each answer that draws math.randomInt draws it here (q.Player.RandomInt
// is the same call on the same seeded script RNG).
// Trigger: available and no needToZone: randomInt(1, 2): 1: 654 (0, Rarab Tail), else 651 (standard). Accepted or the
// repeat var QuestInAPickle_var 1: 655 (0, Rarab Tail). Completed with needToZone: 660. Completed, no needToZone, var
// not 1: randomInt(1, 2): 1: 661 (the repeat), else 651. Else 651. 654 with option 1: begun; 661 with option 1: var 1.
// Trade, accepted or completed, one Smooth Stone (hasItemQty) and nothing else (getItemCount 1, no gil):
// randomInt(1, 4): 1-2: accepted: 659 / completed: 662 (200); 3: 657 (too light), 4: 658 (too small), each started by
// the handler and the stone taken at once (startEvent, then tradeComplete). 659's finish: the stone taken, completed
// (player:completeQuest), needToZone, the Bone Hairpin (addItem) and ITEM_OBTAINED, 200 gil (npcUtil.giveCurrency),
// Windurst fame 10. 662's: the stone taken, needToZone, GIL_RATE x 200 gil (addGil, no message), fame 10, var 0.
// Inspector's Gadget and The Promise (C#, Windurst group 7) answer her from their own containers first; their Event
// lines alternate with her script. Chamama has no DefaultActions entry.
// Kept as the Lua has it: the trade reads the status only (a completed char without the repeat var 1 still gets 662 and
// its pay); 659 completes before giving the hairpin, and addItem has no free-slot check (unreachable: the stone is rare,
// stack 1, so its tradeComplete frees a slot first); the 662 argument is the literal 200 while the pay is 200 x
// GIL_RATE; the finishes read no status; an available char with the repeat var 1 gets the offer draw (654 / 651), not
// the reminder 655 (the available arm comes first in her chain).
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class InAPickle : QuestScript
    {
        const string RepeatVar = "QuestInAPickle_var";

        public InAPickle() : base(QuestLog.Windurst, "IN_A_PICKLE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Chamama.lua onTrade: accepted or completed, one Smooth Stone, one item, no gil
            b.AnyState(when: q => q.Quest.Status == QuestStatus.Accepted || q.Quest.Status == QuestStatus.Completed)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Chamama).Called()
                    .OnTrade(q =>
                    {
                        if (!(q.Trade.HasItemQty(Item.SmoothStone, 1) && q.Trade.ItemCount == 1 && q.Trade.Gil == 0))
                        {
                            return null;
                        }

                        int rand = q.Player.RandomInt(1, 4);

                        if (rand <= 2)
                        {
                            if (q.Quest.Status == QuestStatus.Accepted)
                            {
                                return QuestReply.Event(659); // Quest Turn In (1st Time)
                            }

                            return QuestReply.Event(662, 200); // Repeatable Quest Turn In
                        }

                        // player:startEvent, then player:tradeComplete: the stone is taken at the trade
                        q.Player.StartEvent(rand == 3 ? 657 : 658); // Too Light / Too Small
                        q.Trade.Complete();

                        return null;
                    });

            // Chamama.lua onTrigger: available, no needToZone: Quest Begin / Standard Conversation
            b.State(QuestStatus.Available, when: q => !q.Player.NeedToZone)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Chamama).Called()
                    .OnTrigger(q => q.Player.RandomInt(1, 2) == 1
                        ? QuestReply.Event(654, 0, Item.RarabTail)
                        : QuestReply.Event(651));

            // accepted or the repeat var 1: Quest Objective Reminder
            b.AnyState(when: q => q.Quest.Status == QuestStatus.Accepted || q.Player.CharVar(RepeatVar) == 1)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Chamama).Called()
                    .OnTrigger(QuestReply.Event(655, 0, Item.RarabTail));

            // completed, needToZone: After Quest
            b.State(QuestStatus.Completed, when: q => q.Player.NeedToZone)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Chamama).Called()
                    .OnTrigger(QuestReply.Event(660));

            // completed, no needToZone, the repeat var not 1: Repeatable Quest Begin / Standard Conversation
            b.State(QuestStatus.Completed, when: q => !q.Player.NeedToZone && q.Player.CharVar(RepeatVar) != 1)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Chamama).Called()
                    .OnTrigger(q => q.Player.RandomInt(1, 2) == 1
                        ? QuestReply.Event(661)
                        : QuestReply.Event(651));

            // else: Standard Conversation
            b.AnyState()
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Chamama).Called()
                    .OnTrigger(QuestReply.Event(651));

            // Chamama.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.WindurstWaters, 654, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 659, q =>
                {
                    q.Trade.Complete();
                    // the raw player:completeQuest: not quest:complete, whose cleanup would clear the quest's own
                    // Quest[2][5] vars
                    q.Player.CompleteQuest(QuestLog.Windurst, "IN_A_PICKLE");
                    q.Player.SetNeedToZone(true);
                    q.Player.AddItem(Item.BoneHairpin);
                    q.Text.Special(Text.WindurstWaters.ITEM_OBTAINED, Item.BoneHairpin);
                    q.Player.GiveCurrency("gil", 200);
                    q.Player.AddFame(FameArea.Windurst, 10);
                })
                .OnEventFinish(Zone.WindurstWaters, 661, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.SetCharVar(RepeatVar, 1);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 662, q =>
                {
                    q.Trade.Complete();
                    q.Player.SetNeedToZone(true);
                    // xi.settings.main.GIL_RATE * 200, read by the engine's integral getter (llround)
                    q.Player.AddGil((int)Math.Round(q.Settings.MainNumber("GIL_RATE") * 200, MidpointRounding.AwayFromZero));
                    q.Player.AddFame(FameArea.Windurst, 10);
                    q.Player.SetCharVar(RepeatVar, 0);
                });
        }
    }
}
