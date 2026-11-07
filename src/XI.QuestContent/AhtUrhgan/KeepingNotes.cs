// Keeping Notes (Log ID 6, Quest ID 0), a line-for-line port of scripts/quests/ahtUrhgan/Keeping_Notes.lua (quest
// rework B3b Aht Urhgan, draft). Aht Urhgan Whitegate: Ahkk Jharcham !pos 0.1 0 -76.3 (no script, no default).
// Available (no other condition): Ahkk Jharcham 9, whose finish begins it. Accepted: Ahkk 10; a trade of exactly a
// Sheet of Parchment and a Jar of Black Ink: 11, any other trade: 14 (an Event); 11's finish completes it (no reward)
// and, when it does, takes the trade and adds 0x10 to the Mog House flag. Completed: Ahkk 12 (an Event); a trade of
// exactly one Sheet of Parchment: 13, any other: 14 (Events). Kept as the Lua has it: the flag is added (mhflag +
// 0x0010), not or-ed, so a flag already holding 0x10 grows past it; 14 answers any other trade.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class KeepingNotes : QuestScript
    {
        public KeepingNotes() : base(QuestLog.AhtUrghan, "KEEPING_NOTES") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Keeping_Notes";

        protected override void Build(QuestScriptBuilder b)
        {
            // Section: Quest available
            b.State(QuestStatus.Available)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.AhkkJharcham)
                .OnTrigger(q => QuestReply.ProgressEvent(9))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 9, q =>
                {
                    q.Quest.Begin();
                });

            // Section: Quest accepted
            b.State(QuestStatus.Accepted)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.AhkkJharcham)
                .OnTrigger(q => QuestReply.ProgressEvent(10))
                .OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.SheetOfParchment, Item.JarOfBlackInk))
                    {
                        return QuestReply.ProgressEvent(11);
                    }
                    else
                    {
                        return QuestReply.Event(14);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 11, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        int mhflag = q.Player.MoghouseFlag;
                        q.Player.SetMoghouseFlag(mhflag + 0x0010);
                    }
                });

            // Section: Quest completed
            b.State(QuestStatus.Completed)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.AhkkJharcham).OnTrigger(q => QuestReply.Event(12))
                .OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.SheetOfParchment))
                    {
                        return QuestReply.Event(13);
                    }
                    else
                    {
                        return QuestReply.Event(14);
                    }
                });
        }
    }
}
