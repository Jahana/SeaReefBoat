// Arts and Crafts (Log ID 6, Quest ID 1), a line-for-line port of scripts/quests/ahtUrhgan/Arts_and_Crafts.lua (quest
// rework B3b Aht Urhgan, draft). Aht Urhgan Whitegate (no scripts): Hadahda (default 518), Mhasbaf (542), Mathlouq
// (543), Zabahf (530), Ekhu Pesshyadha (532), Qutiba (none), Balakaf (515), Matifa (541). Available (no other
// condition): Hadahda 508, option 1: begun. Accepted: Hadahda 517 once Prog's bits 1-7 are all set, else 509 (an
// Event); a trade to Hadahda of exactly one Bowl of Sutlac at Stage 1: 573. Each craftsman while his bit is clear:
// Mhasbaf 510 (bit 1), Mathlouq 511 (2), Zabahf 512 (3), Ekhu Pesshyadha 513 (4), Qutiba 514 (5), Balakaf 515 (6),
// Matifa 516 (7); each finish sets its bit. 517's finish: a Bowl of Sutlac and, when it fits, Prog 0 and Stage 1.
// 573's finish completes it (an Imperial Bronze Piece) and, when it does, takes the bowl. Kept as the Lua has it:
// 517 clears Prog (the bits) and sets Stage 1 only when the bowl fits, so Hadahda answers 509 after it. A craftsman
// whose bit is set returns nil, and what plays is the next quest's or the NPC's default: Zabahf plays Got It All's
// 533, Qutiba Delivering the Goods' 51 (and the other Qutiba quests'), the rest their default event. Balakaf's quest
// event 515 is also his default's id.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class ArtsAndCrafts : QuestScript
    {
        public ArtsAndCrafts() : base(QuestLog.AhtUrghan, "ARTS_AND_CRAFTS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Arts_and_Crafts";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.ImperialBronzePiece,
            });

            // Section: Begin quest
            b.State(QuestStatus.Available)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Hadahda).OnTrigger(QuestReply.ProgressEvent(508))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 508, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Questing
            b.State(QuestStatus.Accepted)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Hadahda).OnTrigger(q =>
                {
                    if (q.Quest.IsVarBitsSet("Prog", 1, 2, 3, 4, 5, 6, 7))
                    {
                        return QuestReply.ProgressEvent(517);
                    }
                    else
                    {
                        return QuestReply.Event(509);
                    }
                })
                .OnTrade(q =>
                {
                    if (q.Quest.Var("Stage") == 1 &&
                        q.Trade.HasExactly(Item.BowlOfSutlac))
                    {
                        return QuestReply.ProgressEvent(573);
                    }
                    return null;
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Mhasbaf).OnTrigger(q =>
                    !q.Quest.IsVarBitsSet("Prog", 1) ? QuestReply.ProgressEvent(510) : null)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Mathlouq).OnTrigger(q =>
                    !q.Quest.IsVarBitsSet("Prog", 2) ? QuestReply.ProgressEvent(511) : null)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Zabahf).OnTrigger(q =>
                    !q.Quest.IsVarBitsSet("Prog", 3) ? QuestReply.ProgressEvent(512) : null)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.EkhuPesshyadha).OnTrigger(q =>
                    !q.Quest.IsVarBitsSet("Prog", 4) ? QuestReply.ProgressEvent(513) : null)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Qutiba).OnTrigger(q =>
                    !q.Quest.IsVarBitsSet("Prog", 5) ? QuestReply.ProgressEvent(514) : null)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Balakaf).OnTrigger(q =>
                    !q.Quest.IsVarBitsSet("Prog", 6) ? QuestReply.ProgressEvent(515) : null)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Matifa).OnTrigger(q =>
                    !q.Quest.IsVarBitsSet("Prog", 7) ? QuestReply.ProgressEvent(516) : null)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 510, q => q.Quest.SetVarBit("Prog", 1))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 511, q => q.Quest.SetVarBit("Prog", 2))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 512, q => q.Quest.SetVarBit("Prog", 3))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 513, q => q.Quest.SetVarBit("Prog", 4))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 514, q => q.Quest.SetVarBit("Prog", 5))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 515, q => q.Quest.SetVarBit("Prog", 6))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 516, q => q.Quest.SetVarBit("Prog", 7))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 517, q =>
                {
                    if (q.Player.GiveItem(Item.BowlOfSutlac))
                    {
                        q.Quest.SetVar("Prog", 0);
                        q.Quest.SetVar("Stage", 1);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 573, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                });
        }
    }
}
