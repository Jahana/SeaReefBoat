// Everyone's Grudge (Log ID 5), ported from the quest branches of scripts/zones/Norg/npcs/Magephaud.lua (quest rework B3b,
// Outlands group 6; call-only: the script now calls this quest). Magephaud : !pos 13.041 -0.076 4.903 252.
// Magephaud, in the script's order: available, a tonberry kill counted (the char var EVERYONES_GRUDGE_KILLS, which the
// tonberry mixin counts) and Norg fame level 2: 116 (Gold Beastcoin), whose finish begins the quest and sets
// EveryonesGrudgeStarted 1; the char var EveryonesGrudgeStarted 1 (any status): 117 (Gold Beastcoin); completed: 119.
// His trade, accepted, three Gold Beastcoins and three items in all: 118 (Gold Beastcoin); its finish: the quest
// completed, the trade taken, Norg fame +20, the Tonberry Priest Key, EveryonesGrudgeStarted 0, the Honorary Doctorate
// title.
// Kept as the Lua has it: the trigger mixes a status test and a plain char var (117 answers whatever the status while
// the var is 1); the completion comes before the trade is taken; nothing answers otherwise (no default in the script:
// the zone's default 115 (DefaultActions) plays after it, and on every other talk, since the lookup alternates the
// default with his script).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class EveryonesGrudge : QuestScript
    {
        public EveryonesGrudge() : base(QuestLog.Outlands, "EVERYONES_GRUDGE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Magephaud.lua onTrigger, in its order (a status test, then a char var any status, then a status)
            b.AnyState()
                .Npc(Zone.Norg, Npc.Norg.Magephaud).Called().OnTrigger(q =>
                {
                    int nFame = q.Player.FameLevel(FameArea.Norg);

                    if (q.Quest.Status == QuestStatus.Available &&
                        q.Player.CharVar("EVERYONES_GRUDGE_KILLS") >= 1 &&
                        nFame >= 2)
                    {
                        return QuestReply.Event(116, Item.GoldBeastcoin); // Quest start - you have tonberry kills?! I got yo back ^.-
                    }
                    else if (q.Player.CharVar("EveryonesGrudgeStarted") == 1)
                    {
                        return QuestReply.Event(117, Item.GoldBeastcoin);
                    }
                    else if (q.Quest.Status == QuestStatus.Completed)
                    {
                        return QuestReply.Event(119); // After completion cs
                    }

                    return null;
                });

            // Magephaud.lua onTrade
            b.State(QuestStatus.Accepted)
                .Npc(Zone.Norg, Npc.Norg.Magephaud).Called().OnTrade(q =>
                {
                    if (q.Trade.HasItemQty(Item.GoldBeastcoin, 3) &&
                        q.Trade.ItemCount == 3)
                    {
                        return QuestReply.Event(118, Item.GoldBeastcoin);
                    }

                    return null;
                });

            // Magephaud.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.Norg, 116, q =>
                {
                    q.Quest.Begin();
                    q.Player.SetCharVar("EveryonesGrudgeStarted", 1);
                })
                .OnEventFinish(Zone.Norg, 118, q =>
                {
                    // the raw player:completeQuest, which keeps the quest's own Quest[5][134] vars
                    q.Player.CompleteQuest(QuestLog.Outlands, "EVERYONES_GRUDGE");
                    q.Trade.Complete();
                    q.Player.AddFame(FameArea.Norg, 20);
                    q.Player.GiveKeyItem(KeyItem.TonberryPriestKey);
                    q.Player.SetCharVar("EveryonesGrudgeStarted", 0);
                    q.Player.AddTitle(Title.HonoraryDoctorateMajoringInTonberries);
                });
        }
    }
}
