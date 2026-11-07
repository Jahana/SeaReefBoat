// Do Not Go into the Light (Log ID 9, Quest ID 122), ported from the quest branches of
// scripts/zones/Western_Adoulin/npcs/Chanteillie.lua (quest rework B3b Adoulin group 8; call-only: the script calls
// this quest first in her trade and her talk, before Vegetable Vegetable Crisis). Chanteillie : !pos 89 0 -75 256
// (default 531).
// Trade, accepted with DNGITL_Status 3: a Piece of Urunday Lumber, a Damascus Ingot and a Fire Crystal: 5076, whose
// finish confirms the trade, gives the Inventor's Coalition Pickaxe and sets DNGITL_Status 0. Talk, accepted with the
// pickaxe: 5077 (no finish).
// Kept as the script has it: nothing sets DNGITL_Status 3 or begins or completes the quest (GMs); the finish reads no
// status; the pickaxe stays (the talk plays 5077 each time).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class DoNotGoIntoTheLight : QuestScript
    {
        public DoNotGoIntoTheLight() : base(QuestLog.Adoulin, "DO_NOT_GO_INTO_THE_LIGHT") { }

        // an item with no generated constant (item_basic.sql; no xi.item name)
        private const int PieceOfUrundayLumber = 3927;

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Chanteillie).Called().OnTrade(q =>
                {
                    // DO NOT GO INTO THE LIGHT (Urunday Lumber, Damascus Ingot, Fire Crystal)
                    if (q.Quest.Status == QuestStatus.Accepted &&
                        q.Player.CharVar("DNGITL_Status") == 3 &&
                        q.Trade.Has(PieceOfUrundayLumber, Item.DamascusIngot, Item.FireCrystal))
                    {
                        return QuestReply.Event(5076);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted &&
                        q.Player.HasKeyItem(KeyItem.InventorsCoalitionPickaxe))
                    {
                        return QuestReply.Event(5077);
                    }

                    return null;
                });

            // the finish, keyed on the csid alone as the script's is
            b.AnyState()
                .OnEventFinish(Zone.WesternAdoulin, 5076, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.InventorsCoalitionPickaxe);
                    q.Player.SetCharVar("DNGITL_Status", 0);
                });
        }
    }
}
