// Don't Forget the Antidote (Log ID 5), ported from the quest branches of scripts/zones/Rabao/npcs/Edigey.lua (quest
// rework B3b, Outlands group 7; call-only: the script now calls this quest). Edigey : !pos -36.721 0 -37.882 247. His
// default (DefaultActions 50) alternates with his script in the lookup: every second talk runs the script, so this quest.
// Edigey: available and Selbina-Rabao fame level 4: 2 (0, Vial of Desert Venom), whose finish on 1 begins the quest and
// sets DontForgetAntidoteVar 1; accepted: 3 (0, the vial); completed: 5 (0, the vial). The script keeps its 50 otherwise.
// His trade, accepted or completed, one vial and one item in all: 4 (0, the vial). 4's finish: DontForgetAntidoteVar 1
// (the first time): the var 0, the trade taken, the Desert Hunter title, the Dotanuki (addItem, ITEM_OBTAINED), the
// quest completed, San d'Oria and Bastok fame +5 each; else (a repeat): the trade taken, 1800 gil (npcUtil.giveCurrency),
// the same fame.
// Kept as the Lua has it: the first completion gives the Dotanuki by addItem with no room check (ITEM_OBTAINED is shown
// whatever addItem answered); the finishes read the char var, not the status.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class DontForgetTheAntidote : QuestScript
    {
        public DontForgetTheAntidote() : base(QuestLog.Outlands, "DONT_FORGET_THE_ANTIDOTE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Edigey.lua onTrigger, in its order
            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.SelbinaRabao) >= 4)
                .Npc(Zone.Rabao, Npc.Rabao.Edigey).Called().OnTrigger(QuestReply.Event(2, 0, Item.VialOfDesertVenom));

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Rabao, Npc.Rabao.Edigey).Called().OnTrigger(QuestReply.Event(3, 0, Item.VialOfDesertVenom));

            b.State(QuestStatus.Completed)
                .Npc(Zone.Rabao, Npc.Rabao.Edigey).Called().OnTrigger(QuestReply.Event(5, 0, Item.VialOfDesertVenom));

            // Edigey.lua onTrade
            b.State(QuestStatus.Accepted).Or(QuestStatus.Completed)
                .Npc(Zone.Rabao, Npc.Rabao.Edigey).Called().OnTrade(q =>
                {
                    if (q.Trade.HasItemQty(Item.VialOfDesertVenom, 1) &&
                        q.Trade.ItemCount == 1)
                    {
                        return QuestReply.Event(4, 0, Item.VialOfDesertVenom);
                    }

                    return null;
                });

            // Edigey.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.Rabao, 2, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Player.SetCharVar("DontForgetAntidoteVar", 1);
                    }
                })
                .OnEventFinish(Zone.Rabao, 4, q =>
                {
                    if (q.Player.CharVar("DontForgetAntidoteVar") == 1) // If completing for the first time
                    {
                        q.Player.SetCharVar("DontForgetAntidoteVar", 0);
                        q.Trade.Complete();
                        q.Player.AddTitle(Title.DesertHunter);
                        q.Player.AddItem(Item.Dotanuki); // Dotanuki
                        q.Text.Special(Text.Rabao.ITEM_OBTAINED, Item.Dotanuki);
                        // the raw player:completeQuest, which keeps the quest's own Quest[5][192] vars
                        q.Player.CompleteQuest(QuestLog.Outlands, "DONT_FORGET_THE_ANTIDOTE");
                        q.Player.AddFame(FameArea.Sandoria, 5);
                        q.Player.AddFame(FameArea.Bastok, 5);
                    }
                    else // Subsequent completions
                    {
                        q.Trade.Complete();
                        q.Player.GiveCurrency("gil", 1800);
                        q.Player.AddFame(FameArea.Sandoria, 5);
                        q.Player.AddFame(FameArea.Bastok, 5);
                    }
                });
        }
    }
}
