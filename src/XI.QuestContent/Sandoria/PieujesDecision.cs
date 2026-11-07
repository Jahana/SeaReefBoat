// Pieuje's Decision (Log ID 0, Quest ID 89, WHM AF3), ported from the quest branches of
// scripts/zones/Chateau_dOraguille/npcs/_6h1.lua, scripts/zones/FeiYin/Zone.lua and npcs/qm1.lua and
// scripts/zones/Northern_San_dOria/npcs/Narcheral.lua (quest rework B3b, San d'Oria group 13; call-only: each script calls
// the quest at the spot its branch was). _6h1 : !pos -37 -3 31 233; Narcheral : !pos 129 -11 126 231; qm1 : !pos -55 -16 69
// 204. The quest's state is the status and the char var pieujesDecisionCS (not a quest var).
// The door _6h1 calls the quest inside its WHM block (main job WHM, main level AF2_QUEST_LEVEL: the script's guard, not
// this quest's): with Prelude of Black and White completed, 552 (finish: begun, on any option). Accepted: entering
// Fei'Yin from Beaucedine Glacier (prevZone 111) while pieujesDecisionCS is 0 plays 19 (finish: pieujesDecisionCS 1);
// Fei'Yin's qm1 shows SOUL_OF_TAVNAZIA and, traded the Tavnazia Bell (tradeHas: other items ride along) with no Tavnazian
// Mask held and Altedour I Tavnazia not spawned (a dead Altedour is still spawned until his body goes), takes the bell
// (confirmTrade), shows EVIL_PRESENCE and spawns him claimed; Narcheral, traded exactly the Tavnazian Mask: 692 (finish:
// with a free slot Paragon of White Mage Excellence, pieujesDecisionCS 0, Healer's Bliaut, ITEM_OBTAINED, the mask
// taken, fame +20, completed by the raw player:completeQuest, which keeps the quest's own Quest[0][89] vars;
// with none ITEM_CANNOT_BE_OBTAINED).
// The scripts keep their own answers when the quest does not act: the door's rank-10 73 and 523 (and nothing for a WHM
// of that level otherwise; the door's DefaultActions text shows then), the qm1's YOU_FIND_NOTHING, Fei'Yin's ACP zone-in
// (29, before this quest's 19).
// Kept as the Lua has it: 552's finish adds the quest on any option; the 692 finish checks the free slots itself and
// gives the bliaut with addItem before player:completeQuest; 19 needs prevZone Beaucedine Glacier.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class PieujesDecision : QuestScript
    {
        const string Cs = "pieujesDecisionCS";

        public PieujesDecision() : base(QuestLog.Sandoria, "PIEUJES_DECISION") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // _6h1.lua onTrigger, inside the script's WHM block: start
            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Sandoria, "PRELUDE_OF_BLACK_AND_WHITE") == QuestStatus.Completed)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille._6h1).Called().OnTrigger(QuestReply.Event(552));

            b.State(QuestStatus.Accepted)
                // FeiYin qm1.lua (else the script's YOU_FIND_NOTHING)
                .Npc(Zone.Feiyin, Npc.Feiyin.qm1).Called()
                    .OnTrigger(QuestReply.MessageSpecial(Text.Feiyin.SOUL_OF_TAVNAZIA))
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHas(trade, xi.item.TAVNAZIA_BELL)
                        if (q.Trade.Has(Item.TavnaziaBell) &&
                            !q.Player.HasItem(Item.TavnazianMask) &&
                            !q.Player.MobIsSpawned(Mob.Feiyin.ALTEDOUR_I_TAVNAZIA))
                        {
                            q.Trade.Confirm();
                            q.Text.Special(Text.Feiyin.EVIL_PRESENCE);
                            // SpawnMob(ID.mob.ALTEDOUR_I_TAVNAZIA):updateClaim(player)
                            q.Player.SpawnMob(Mob.Feiyin.ALTEDOUR_I_TAVNAZIA, 0, claim: true);
                            return QuestReply.NoAction();
                        }
                        return null;
                    })
                // Narcheral.lua onTrade: npcUtil.tradeHasExactly(trade, xi.item.TAVNAZIAN_MASK)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Narcheral).Called().OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.TavnazianMask))
                    {
                        return QuestReply.Event(692);
                    }
                    return null;
                });

            // FeiYin Zone.lua onZoneIn, in the else of its ACP mission's 29 (which stays)
            b.State(QuestStatus.Accepted, when: q =>
                    q.Player.PreviousZone == Zone.BeaucedineGlacier &&
                    q.Player.CharVar(Cs) == 0)
                .OnZoneIn(Zone.Feiyin, q => QuestReply.Event(19)); // WHM AF

            b.AnyState()
                // _6h1.lua onEventFinish
                .OnEventFinish(Zone.ChateauDoraguille, 552, q =>
                {
                    q.Quest.Begin();
                })
                // FeiYin Zone.lua onEventFinish
                .OnEventFinish(Zone.Feiyin, 19, q =>
                {
                    q.Player.SetCharVar(Cs, 1);
                })
                // Narcheral.lua onEventFinish
                .OnEventFinish(Zone.NorthernSanDoria, 692, q =>
                {
                    if (q.Player.FreeSlotCount == 0)
                    {
                        q.Text.Special(Text.NorthernSanDoria.ITEM_CANNOT_BE_OBTAINED, Item.HealersBliaut);
                    }
                    else
                    {
                        q.Player.AddTitle(Title.ParagonOfWhiteMageExcellence);
                        q.Player.SetCharVar(Cs, 0);
                        q.Player.AddItem(Item.HealersBliaut);
                        q.Text.Special(Text.NorthernSanDoria.ITEM_OBTAINED, Item.HealersBliaut);
                        // player:tradeComplete()
                        q.Trade.Complete();
                        q.Player.AddFame(FameArea.Sandoria, 20);
                        // the raw player:completeQuest, which keeps the quest's own Quest[0][89] vars
                        q.Player.CompleteQuest(QuestLog.Sandoria, "PIEUJES_DECISION");
                    }
                });
        }
    }
}
