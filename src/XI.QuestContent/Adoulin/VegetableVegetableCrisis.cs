// Vegetable Vegetable Crisis (Log ID 9, Quest ID 110), ported from the quest branches of
// scripts/zones/Western_Adoulin/npcs/Chanteillie.lua (quest rework B3b Adoulin group 8; call-only: the script calls
// this quest after Do Not Go into the Light's call, in her trade and her talk). Chanteillie : !pos 89 0 -75 256
// (default 531).
// Trade, accepted with VVC_Status 1: a Piece of Urunday Lumber, a Midrium Ingot and a Square of Raaz Leather: 5089,
// whose finish confirms the trade, sets VVC_Status 2 and VVC_Gameday_Wait to VanadielUniqueDay(). Talk, accepted
// with VVC_Status 1: 5088 (no finish).
// Kept as the script has it: nothing sets VVC_Status 1 or begins or completes the quest (GMs); nothing reads
// VVC_Gameday_Wait; the finish reads no status.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class VegetableVegetableCrisis : QuestScript
    {
        public VegetableVegetableCrisis() : base(QuestLog.Adoulin, "VEGETABLE_VEGETABLE_CRISIS") { }

        // items with no generated constant (item_basic.sql; no xi.item name)
        private const int PieceOfUrundayLumber = 3927;
        private const int MidriumIngot = 3919;
        private const int SquareOfRaazLeather = 8708;

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Chanteillie).Called().OnTrade(q =>
                {
                    // VEGETABLE VEGETABLE CRISIS (Urunday Lumber, Midrium Ingot, Raaz Leather)
                    if (q.Quest.Status == QuestStatus.Accepted &&
                        q.Player.CharVar("VVC_Status") == 1 &&
                        q.Trade.Has(PieceOfUrundayLumber, MidriumIngot, SquareOfRaazLeather))
                    {
                        return QuestReply.Event(5089);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted && q.Player.CharVar("VVC_Status") == 1)
                    {
                        return QuestReply.Event(5088);
                    }

                    return null;
                });

            // the finish, keyed on the csid alone as the script's is
            b.AnyState()
                .OnEventFinish(Zone.WesternAdoulin, 5089, q =>
                {
                    q.Trade.Confirm();
                    q.Player.SetCharVar("VVC_Status", 2);
                    q.Player.SetCharVar("VVC_Gameday_Wait", q.Player.VanadielUniqueDay);
                });
        }
    }
}
