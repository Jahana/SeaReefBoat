// Order Up (Log ID 9, Quest ID 30), ported from the quest branches of scripts/zones/Western_Adoulin/npcs/Terwok.lua,
// Marjoirelle.lua, Grevan.lua, Mastan.lua and Oka_Qhantari.lua (quest rework B3b Adoulin group 6; call-only: the
// scripts call this quest). Terwok : !pos 127 4 -81 256; Marjoirelle : 21.7 -0.2 -160.2 (npcs.yaml: her script's !pos
// is Terwok's); Grevan : !pos 50 0 6 256; Mastan : !pos -9 0 67 256; Oka Qhantari : -130.6 3.3 -6.1 (npcs.yaml).
// Its state is the bit mask char var Order_Up_NPCs: Terwok bit 7, Marjoirelle 8, Oka Qhantari 9, Grevan 10, Mastan 11.
// While accepted, each of the five without its bit: its event (Terwok 67, Marjoirelle 68, Grevan 69, Mastan 70,
// Oka Qhantari 71), whose finish sets the bit. Nothing else: no script begins or completes the quest, and none reads
// the whole mask (its giver and its end are not implemented).
// Kept as the scripts have it: the finishes set the bit on the csid alone (no status test); when this answers nothing,
// Grevan's script goes on to his own dialogue (Hypocritical Oath / Not-So-Clean Bill status reads: 171 / 188) and
// Mastan's to The Curious Case of Melvien.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class OrderUp : QuestScript
    {
        public OrderUp() : base(QuestLog.Adoulin, "ORDER_UP") { }

        private const string OrderUpNpcs = "Order_Up_NPCs";

        // utils.mask.getBit(player:getCharVar('Order_Up_NPCs'), bit)
        private static bool GetBit(IQuestContext q, int bit) => (q.Player.CharVar(OrderUpNpcs) & (1 << bit)) != 0;

        // player:setCharVar('Order_Up_NPCs', utils.mask.setBit(player:getCharVar('Order_Up_NPCs'), bit, true))
        private static void SetBit(IQuestContext q, int bit) =>
            q.Player.SetCharVar(OrderUpNpcs, q.Player.CharVar(OrderUpNpcs) | (1 << bit));

        // each NPC's talk: accepted and its bit unset: its event
        private static QuestReply Ask(IQuestContext q, int bit, int csid)
        {
            if (q.Quest.Status == QuestStatus.Accepted && !GetBit(q, bit))
            {
                // Progresses Quest: 'Order Up'
                return QuestReply.Event(csid);
            }

            return null;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Terwok).Called().OnTrigger(q => Ask(q, 7, 67))
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Marjoirelle).Called().OnTrigger(q => Ask(q, 8, 68))
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Grevan).Called().OnTrigger(q => Ask(q, 10, 69))
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Mastan).Called().OnTrigger(q => Ask(q, 11, 70))
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.OkaQhantari).Called().OnTrigger(q => Ask(q, 9, 71));

            // the finishes, as each script keys them on its csid alone
            b.AnyState()
                .OnEventFinish(Zone.WesternAdoulin, 67, q => SetBit(q, 7))
                .OnEventFinish(Zone.WesternAdoulin, 68, q => SetBit(q, 8))
                .OnEventFinish(Zone.WesternAdoulin, 69, q => SetBit(q, 10))
                .OnEventFinish(Zone.WesternAdoulin, 70, q => SetBit(q, 11))
                .OnEventFinish(Zone.WesternAdoulin, 71, q => SetBit(q, 9));
        }
    }
}
