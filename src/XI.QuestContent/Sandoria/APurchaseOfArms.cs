// A Purchase of Arms (Log ID 0, Quest ID 27), a line-for-line port of scripts/quests/sandoria/A_Purchase_of_Arms.lua (quest
// rework B3b, San d'Oria group 3). Helbort : !pos 71 -1 65 230; Alexius : !pos 105 1 382 104. Offered once Father and Son
// is completed, at San d'Oria fame level 2: Helbort's 594 (option 0: the Weapons Order and begun). With the order:
// Helbort's HELBORT_ORDERS; Alexius (Jugner Forest) 5: the Weapons Receipt for the order. Without the order: Alexius's
// ALEXIUS_ORDERS (the receipt); Helbort's 607: completed (Elm Staff, fame +20, Arms Trader), the receipt deleted.
// Completed: Helbort's text HELBORT_ORDERS + 3. Alexius's ALEXIUS_ORDERS passes showText's showName and turn as the Lua
// booleans true, false (QuestArg): the NPC does not turn. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class APurchaseOfArms : QuestScript
    {
        public APurchaseOfArms() : base(QuestLog.Sandoria, "A_PURCHASE_OF_ARMS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/A_Purchase_of_Arms";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Sandoria, Item = Item.ElmStaff, Title = Title.ArmsTrader });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Sandoria, "FATHER_AND_SON") == QuestStatus.Completed &&
                    q.Player.FameLevel(FameArea.Sandoria) >= 2)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Helbort).OnTrigger(QuestReply.ProgressEvent(594))
                .OnEventFinish(Zone.SouthernSanDoria, 594, q =>
                {
                    if (q.Option == 0 && q.Player.GiveKeyItem(KeyItem.WeaponsOrder))
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted, when: q => q.Player.HasKeyItem(KeyItem.WeaponsOrder))
                // quest:message(id): a Text-type message, the same action as messageText
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Helbort).OnTrigger(QuestReply.MessageText(Text.SouthernSanDoria.HELBORT_ORDERS))
                .Npc(Zone.JugnerForest, Npc.JugnerForest.Alexius).OnTrigger(QuestReply.ProgressEvent(5))
                .OnEventFinish(Zone.JugnerForest, 5, q =>
                {
                    if (q.Player.GiveKeyItem(KeyItem.WeaponsReceipt))
                    {
                        q.Player.DelKeyItem(KeyItem.WeaponsOrder);
                    }
                });

            b.State(QuestStatus.Accepted, when: q => !q.Player.HasKeyItem(KeyItem.WeaponsOrder))
                // showText(npc, id, WEAPONS_RECEIPT, 0, 0, 0, showName true, turn false): Lua booleans, never ints (0 is truthy)
                .Npc(Zone.JugnerForest, Npc.JugnerForest.Alexius)
                    .OnTrigger(QuestReply.MessageName(Text.JugnerForest.ALEXIUS_ORDERS, KeyItem.WeaponsReceipt, 0, 0, 0, true, false))
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Helbort).OnTrigger(QuestReply.ProgressEvent(607))
                .OnEventFinish(Zone.SouthernSanDoria, 607, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.WeaponsReceipt);
                    }
                });

            b.State(QuestStatus.Completed)
                // quest:message(southID.text.HELBORT_ORDERS + 3): a text id plus an offset
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Helbort).OnTrigger(q =>
                    QuestReply.Message(q.Text.Id(Text.SouthernSanDoria.HELBORT_ORDERS) + 3));
        }
    }
}
