// A Taste of Honey (Log ID 6, Quest ID 12), a line-for-line port of scripts/quests/ahtUrhgan/A_Taste_of_Honey.lua
// (quest rework B3b Aht Urhgan, draft). Aht Urhgan Whitegate: Fochacha, Qutiba, Ulamaal (no scripts, no defaults).
// Wajaom Woodlands: the twelve Pephredo Hives (no script; default NOTHING_OUT_OF_ORDINARY). Available with Vanishing
// Act completed: Qutiba / Ulamaal: 55 / 56 (Events) while the char must zone or Stage is later than now (Vanishing
// Act's 45 sets it), else 579 (its finish: Stage 0, begun); Fochacha 50 (an Event). Accepted: a trade to Qutiba
// matching three Pots of White Honey: 580 (its finish completes it: Irmik Helvasi; the trade taken when it does);
// Qutiba 585 (an Event); a Pephredo Hive whose var (the hive's npc id) is 0 and whose Pot of White Honey fits: the
// var 1 until the next JST midnight, no action (the pot's ITEM_OBTAINED only); else nil (the default). Completed: a
// trade to Qutiba matching one pot: 581 (the trade taken); Qutiba: the local var recipe 57 and 57 (its updates walk
// recipe 57 -> 1 -> 2 -> 3 -> 0 with their updateEvent values); the hives as when accepted. Kept as the Lua has it:
// each hive's var is named by its npc id (quest:getVar(player, npc:getID()): 'Quest[6][12]' .. the id); the hive
// gives the pot before it sets the var; 579 begins it on any option.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class ATasteOfHoney : QuestScript
    {
        public ATasteOfHoney() : base(QuestLog.AhtUrghan, "A_TASTE_OF_HONEY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/A_Taste_of_Honey";

        // pephredoOnTrigger: the var is named by the hive's npc id (Lua's varPrefix .. npc:getID())
        static QuestReply PephredoOnTrigger(IQuestContext q)
        {
            string hiveVar = q.Npc.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (q.Quest.Var(hiveVar) == 0 &&
                q.Player.GiveItem(Item.PotOfWhiteHoney))
            {
                q.Quest.SetVar(hiveVar, 1, (uint)q.Player.NextJstDay);
                return QuestReply.NoAction();
            }
            return null;
        }

        static void RecipeUpdate(IQuestContext q, int startValue)
        {
            if (q.Player.LocalVar("recipe") == startValue)
            {
                q.Player.SetLocalVar("recipe", 1);
                q.Player.UpdateEvent(5579, 1, 255, 0, 67108863, 5976652, 4095, 0);
            }
            else if (q.Player.LocalVar("recipe") == 1)
            {
                q.Player.SetLocalVar("recipe", 2);
                q.Player.UpdateEvent(4096, 615, 936, 1523, 67108863, 5976652, 4, 0);
            }
            else if (q.Player.LocalVar("recipe") == 2)
            {
                q.Player.SetLocalVar("recipe", 3);
                q.Player.UpdateEvent(2214, 2237, 4509, 5568);
            }
            else if (q.Player.LocalVar("recipe") == 3)
            {
                q.Player.SetLocalVar("recipe", 0);
                q.Player.UpdateEvent(5575);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.IrmikHelvasi,
            });

            // Section: Quest available
            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.AhtUrghan, "VANISHING_ACT") == QuestStatus.Completed)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Qutiba).OnTrigger(q =>
                {
                    // Variable set in ToAU quest 'Vanishing Act'.
                    if (q.Player.NeedToZone || q.Quest.Var("Stage") > q.Player.SystemTime)
                    {
                        return QuestReply.Event(55);
                    }

                    return QuestReply.ProgressEvent(579);
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Fochacha).OnTrigger(q => QuestReply.Event(50))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Ulamaal).OnTrigger(q =>
                {
                    if (q.Player.NeedToZone || q.Quest.Var("Stage") > q.Player.SystemTime)
                    {
                        return QuestReply.Event(56);
                    }

                    return QuestReply.ProgressEvent(579);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 579, q =>
                {
                    q.Quest.SetVar("Stage", 0);
                    q.Quest.Begin();
                });

            // Section: Quest accepted
            b.State(QuestStatus.Accepted)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Qutiba).OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.PotOfWhiteHoney, 3)))
                    {
                        return QuestReply.ProgressEvent(580);
                    }
                    return null;
                })
                .OnTrigger(q => QuestReply.Event(585))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 580, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                })
                .Npc(Zone.WajaomWoodlands, Npc.WajaomWoodlands.PephredoHive).OnTrigger(PephredoOnTrigger);

            // Section: Quest completed
            b.State(QuestStatus.Completed)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Qutiba).OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.PotOfWhiteHoney, 1)))
                    {
                        return QuestReply.ProgressEvent(581);
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    q.Player.SetLocalVar("recipe", 57);
                    return QuestReply.Event(57);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 581, q =>
                {
                    q.Trade.Complete();
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 57, q => RecipeUpdate(q, 57))
                .Npc(Zone.WajaomWoodlands, Npc.WajaomWoodlands.PephredoHive).OnTrigger(PephredoOnTrigger);
        }
    }
}
