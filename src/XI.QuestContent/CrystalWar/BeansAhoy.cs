// Beans Ahoy (Log ID 7, Quest ID 29), ported from the quest branches of scripts/zones/Southern_San_dOria_[S]/npcs/
// Thierride.lua (quest rework B3b Crystal War, draft; call-only). Thierride !pos -124.538 -2 14.598 80. The char vars
// BeansAhoy and BeansAhoy_ConquestWeek (no quest prefix).
// Trade: accepted, a Chunk of Lufet Salt alone (one item): BeansAhoy 0: 337 (finish: tradeComplete, BeansAhoy 1,
// needToZone); not needToZone: 340; else nothing; any other trade: his script's 339. Talk: available: 334 (finish: the
// quest added); accepted: 335; completed, ConquestWeek 0: 342; completed: 341. 340 / 342's finish: the Angler's
// Cassoulet held (anywhere) or no free slot: ITEM_CANNOT_BE_OBTAINED; else the cassoulet (addItem,
// ITEM_OBTAINED), ConquestWeek 1 until the next conquest tally, and for 340: the quest completed
// (player:completeQuest: the bit alone), BeansAhoy 0, tradeComplete.
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): the held check is
// player:hasItem(cassoulet), every container; the Lua's hasItem(cassoulet, 1) read location 1 (the Mog Safe) only, so
// with one in the bag 340 / 342 went on: the rare cassoulet's addItem failed while ITEM_OBTAINED was sent and the
// ConquestWeek var set (and 340 completed the quest and took the salt).
// Kept as the Lua has it: the salt check is hasItemQty(salt, 1) and getItemCount() == 1; the second salt at needToZone
// gets no reply at all (not 339); 337 / 340 take the salt at their finish (tradeComplete); 340 with no room keeps the
// salt and the quest; 342 (the weekly cassoulet) has no trade to complete; the ConquestWeek var expires at the tally.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class BeansAhoy : QuestScript
    {
        public BeansAhoy() : base(QuestLog.CrystalWar, "BEANS_AHOY") { }

        // 340 / 342's finish (the script's shared arm)
        static void GiveCassoulet(IQuestContext q)
        {
            // held anywhere (the Lua's hasItem(item, 1) read the Mog Safe, location 1, only: fixed,
            // user ruling 2026-11-04)
            if (q.Player.HasItem(Item.AnglersCassoulet) ||
                q.Player.FreeSlotCount < 1)
            {
                q.Text.Special(Text.SouthernSanDoriaS.ITEM_CANNOT_BE_OBTAINED, Item.AnglersCassoulet);
            }
            else
            {
                q.Player.AddItem(Item.AnglersCassoulet, 1);
                q.Text.Special(Text.SouthernSanDoriaS.ITEM_OBTAINED, Item.AnglersCassoulet);
                q.Player.SetCharVar("BeansAhoy_ConquestWeek", 1, (uint)q.Player.NextConquestTally);
                if (q.Csid == 340)
                {
                    // NEEDS FACET: Player.CompleteQuest(QuestLog log, string constName) =
                    // player:completeQuest(xi.questLog.CRYSTAL_WAR, xi.quest.id.crystalWar.BEANS_AHOY) (group 10's
                    // member: the bit alone, not npcUtil.completeQuest)
                    q.Player.CompleteQuest(QuestLog.CrystalWar, "BEANS_AHOY");
                    q.Player.SetCharVar("BeansAhoy", 0);
                    q.Trade.Complete();
                }
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            // Thierride.lua onTrade: the quest's arms; the script's 339 is the fallback
            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Thierride).Called().OnTrade(q =>
                {
                    bool lufetSalt = q.Trade.HasItemQty(Item.ChunkOfLufetSalt, 1);
                    int cnt = q.Trade.ItemCount;

                    if (lufetSalt && cnt == 1)
                    {
                        if (q.Player.CharVar("BeansAhoy") == 0)
                        {
                            return QuestReply.Event(337); // Traded the Correct Item Dialogue
                        }
                        else if (!q.Player.NeedToZone)
                        {
                            return QuestReply.Event(340); // Quest Complete Dialogue
                        }
                        return QuestReply.NoAction(); // the salt at needToZone: no reply (not the 339 fallback)
                    }
                    return null;
                });

            // Thierride.lua onTrigger, in its order
            b.AnyState()
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Thierride).Called().OnTrigger(q =>
                {
                    QuestStatus beansAhoy = q.Quest.Status;

                    if (beansAhoy == QuestStatus.Available)
                    {
                        return QuestReply.Event(334); // Quest Start
                    }
                    else if (beansAhoy == QuestStatus.Accepted)
                    {
                        return QuestReply.Event(335); // Quest Active
                    }
                    else if (beansAhoy == QuestStatus.Completed &&
                        q.Player.CharVar("BeansAhoy_ConquestWeek") == 0)
                    {
                        return QuestReply.Event(342);
                    }
                    else if (beansAhoy == QuestStatus.Completed)
                    {
                        return QuestReply.Event(341);
                    }
                    return null;
                });

            // Thierride.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.SouthernSanDoriaS, 334, q =>
                {
                    q.Quest.Begin();
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 337, q =>
                {
                    q.Trade.Complete();
                    q.Player.SetCharVar("BeansAhoy", 1);
                    q.Player.SetNeedToZone(true);
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 340, GiveCassoulet)
                .OnEventFinish(Zone.SouthernSanDoriaS, 342, GiveCassoulet);
        }
    }
}
