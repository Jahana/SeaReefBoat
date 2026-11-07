// Delivering the Goods (Log ID 6, Quest ID 61), a line-for-line port of
// scripts/quests/ahtUrhgan/Delivering_the_Goods.lua (quest rework B3b Aht Urhgan, draft). Aht Urhgan Whitegate:
// Fochacha !pos 2.9 0 -10.8, Qutiba !pos 92.3 -6.5 -130, Ulamaal !pos 93.5 -6.5 -128.5 (no scripts, no defaults;
// Qutiba and Ulamaal stand in trigger area 5). Available (no other condition): Fochacha 39 (its finish begins it),
// Qutiba 51, Ulamaal 51 (Events). Accepted, Prog 0: Fochacha 46 (an Event), Qutiba / Ulamaal 40 (Prog 1). Prog 1:
// Fochacha 41, Qutiba 52, Ulamaal 53 (Events); 41's finish completes it (three Imperial Bronze Pieces) and, when it
// does, sets Vanishing Act's Stage to now + 60 and the zoning flag. The era module delivering_the_goods replaces 41's
// finish (Stage = JstMidnight()). The reward's item is a stack ({ { IMPERIAL_BRONZE_PIECE, 3 } }), which QuestReward
// cannot hold (Item is one id, Items a list of single ids): 41 gives the stack first, as npcUtil.completeQuest does,
// and completes only when it fit (Wish Upon a Star's way).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class DeliveringTheGoods : QuestScript
    {
        public DeliveringTheGoods() : base(QuestLog.AhtUrghan, "DELIVERING_THE_GOODS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Delivering_the_Goods";

        protected override void Build(QuestScriptBuilder b)
        {
            // the Lua's reward is item = { { IMPERIAL_BRONZE_PIECE, 3 } }: given in 41 (see the header)
            b.Steps(1);

            // Section: Quest available
            b.State(QuestStatus.Available)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Fochacha)
                .OnTrigger(q => QuestReply.ProgressEvent(39))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Qutiba).OnTrigger(q => QuestReply.Event(51))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Ulamaal).OnTrigger(q => QuestReply.Event(51))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 39, q =>
                {
                    q.Quest.Begin();
                });

            // Section: Quest accepted
            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 0)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Fochacha).OnTrigger(q => QuestReply.Event(46))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Qutiba)
                .OnTrigger(q => QuestReply.ProgressEvent(40))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Ulamaal)
                .OnTrigger(q => QuestReply.ProgressEvent(40))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 40, q =>
                {
                    q.Quest.SetStep(1);
                });

            // Section: Complete quest (the era module delivering_the_goods replaces 41 here: state 3)
            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 1)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Fochacha)
                .OnTrigger(q => QuestReply.ProgressEvent(41))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Qutiba).OnTrigger(q => QuestReply.Event(52))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Ulamaal).OnTrigger(q => QuestReply.Event(53))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 41, q =>
                {
                    // npcUtil.completeQuest gives reward.item first and stops when it does not fit
                    if (q.Player.GiveItems((Item.ImperialBronzePiece, 3)) && q.Quest.Complete())
                    {
                        // xi.quest.setVar(player, AHT_URHGAN, VANISHING_ACT, 'Stage',
                        // GetSystemTime() + 60): 1 minute wait time
                        q.Player.SetQuestVar(QuestLog.AhtUrghan, "VANISHING_ACT", "Stage", q.Player.SystemTime + 60);

                        // Player must zone before being able to flag the next quest
                        // Capture: retail still refused two minutes after the turn in. The timer alone does not open it.
                        q.Player.SetNeedToZone(true);
                    }
                });
        }
    }
}
