// Cargo (Log ID 4, Quest ID 20), ported from scripts/zones/Selbina/npcs/Vuntar.lua (quest rework B3b, Other Areas
// group 10; call-only: every branch of his script is this quest's, so his script only calls it; he has no
// DefaultActions entry). Vuntar : !pos 7 -2 -15 248.
// Trigger: main level >= 20 and available: 50 (Rolanberry); below 20: 53; else 51 (Rolanberry). 50's finish: begun.
// Trade, not available: GetSystemTime() past VuntarCanBuyItem_date: a Rolanberry (881 C.E.): 52 (1); (874 C.E.): 52
// (2); (864 C.E.): 52 (3) (npcUtil.tradeHas; the match confirmed); else (the date not past) 1134 (Rolanberry).
// 52's finish: VuntarCanBuyItem_date = JstMidnight(); accepted: completed, San d'Oria and Bastok fame +15; option 1 /
// 2 / 3: 800 / 2000 / 3000 gil (npcUtil.giveCurrency) and the trade taken.
// Kept as the Lua has it: 50 begins with no option read; the date is set whatever the option; another option pays
// nothing and keeps the berry; the trade's 1134 plays whatever the trade holds.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class Cargo : QuestScript
    {
        const string DateVar = "VuntarCanBuyItem_date";

        public Cargo() : base(QuestLog.OtherAreas, "CARGO") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.Selbina, Npc.Selbina.Vuntar).Called()
                    // Vuntar.lua onTrade
                    .OnTrade(q =>
                    {
                        if (q.Quest.Status != QuestStatus.Available)
                        {
                            if (q.Player.SystemTime > q.Player.CharVar(DateVar))
                            {
                                if (q.Trade.Has(Item.Rolanberry881Ce))
                                {
                                    return QuestReply.Event(52, 1); // Can Buy rolanberry (881 ce)
                                }
                                else if (q.Trade.Has(Item.Rolanberry874Ce))
                                {
                                    return QuestReply.Event(52, 2); // Can Buy rolanberry (874 ce)
                                }
                                else if (q.Trade.Has(Item.Rolanberry864Ce))
                                {
                                    return QuestReply.Event(52, 3); // Can Buy rolanberry (864 ce)
                                }
                            }
                            else
                            {
                                return QuestReply.Event(1134, 4365); // Can't buy rolanberrys
                            }
                        }

                        return null;
                    })
                    // Vuntar.lua onTrigger
                    .OnTrigger(q =>
                    {
                        if (q.Player.MainLevel >= 20 &&
                            q.Quest.Status == QuestStatus.Available)
                        {
                            return QuestReply.Event(50, 4365); // Start quest "Cargo"
                        }
                        else if (q.Player.MainLevel < 20)
                        {
                            return QuestReply.Event(53); // Dialog for low level or low fame
                        }
                        else
                        {
                            return QuestReply.Event(51, 4365); // During & after completed quest "Cargo"
                        }
                    });

            b.AnyState()
                // Vuntar.lua onEventFinish 50 / 52
                .OnEventFinish(Zone.Selbina, 50, q =>
                {
                    q.Quest.Begin();
                })
                .OnEventFinish(Zone.Selbina, 52, q =>
                {
                    q.Player.SetCharVar(DateVar, q.Player.JstMidnight);

                    if (q.Quest.Status == QuestStatus.Accepted)
                    {
                        // the raw player:completeQuest, which keeps the quest's own Quest[4][20] vars
                        q.Player.CompleteQuest(QuestLog.OtherAreas, "CARGO");
                        q.Player.AddFame(FameArea.Sandoria, 15);
                        q.Player.AddFame(FameArea.Bastok, 15);
                    }

                    if (q.Option == 1)
                    {
                        q.Player.GiveCurrency("gil", 800);
                        q.Trade.Confirm();
                    }
                    else if (q.Option == 2)
                    {
                        q.Player.GiveCurrency("gil", 2000);
                        q.Trade.Confirm();
                    }
                    else if (q.Option == 3)
                    {
                        q.Player.GiveCurrency("gil", 3000);
                        q.Trade.Confirm();
                    }
                });
        }
    }
}
