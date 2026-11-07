// Creepy Crawlies (Log ID 2, Quest ID 39), ported from scripts/zones/Windurst_Woods/npcs/Illu_Bohjaa.lua (quest rework
// B3b, Windurst group 16; call-only: every answer of his script is this quest's, so his script only calls it). Illu
// Bohjaa : !pos 21 -5 125 241.
// Trigger: available: 333 (0, 816, 938, 1156); else 334 (0, 816, 938, 1156). 333 with option 1: begun. Trade, not
// available: 3 Spools of Silk Thread (npcUtil.tradeHas { { 816, 3 } }), else 3 of item 1156 (the crawler calculus; no
// xi.item constant names it): Windurst fame 10 at the trade, then 335 (600 x GIL_RATE, 816, 938, 1156). 335's finish:
// completed (player:completeQuest, again each time), the title Crawler Culler, 600 x GIL_RATE gil (addGil, no message),
// the trade confirmed. Illu Bohjaa has no DefaultActions entry and no other quest answers him.
// Kept as the Lua has it: the fame is added at the trade, before the event; 335's finish reads no status; the event
// argument and the gil are 600 x GIL_RATE (the engine's integral getter, llround); tradeHas takes at least three and
// confirms only those (a fourth spool, other items, are kept).
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class CreepyCrawlies : QuestScript
    {
        const int CrawlerCalculus = 1156; // the Lua's literal; xi.item has no constant for it

        public CreepyCrawlies() : base(QuestLog.Windurst, "CREEPY_CRAWLIES") { }

        // 600 * xi.settings.main.GIL_RATE: the event argument and the addGil, read by the engine's integral getter (llround)
        static int Pay(IQuestContext q) =>
            (int)Math.Round(600 * q.Settings.MainNumber("GIL_RATE"), MidpointRounding.AwayFromZero);

        protected override void Build(QuestScriptBuilder b)
        {
            // Illu_Bohjaa.lua onTrigger: available: 333
            b.State(QuestStatus.Available)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.IlluBohjaa).Called()
                    .OnTrigger(QuestReply.Event(333, 0, Item.SpoolOfSilkThread, Item.PapakaGrass, CrawlerCalculus));

            // else: 334; Illu_Bohjaa.lua onTrade (not available): three spools, else three calculi: fame 10, then 335
            b.AnyState(when: q => q.Quest.Status != QuestStatus.Available)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.IlluBohjaa).Called()
                    .OnTrigger(QuestReply.Event(334, 0, Item.SpoolOfSilkThread, Item.PapakaGrass, CrawlerCalculus))
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasQty((Item.SpoolOfSilkThread, 3)) || q.Trade.HasQty((CrawlerCalculus, 3)))
                        {
                            q.Player.AddFame(FameArea.Windurst, 10);
                            return QuestReply.Event(335, Pay(q), Item.SpoolOfSilkThread, Item.PapakaGrass, CrawlerCalculus);
                        }

                        return null;
                    });

            // Illu_Bohjaa.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.WindurstWoods, 333, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.WindurstWoods, 335, q =>
                {
                    // the raw player:completeQuest: not quest:complete, whose cleanup would clear the quest's own
                    // Quest[2][39] vars
                    q.Player.CompleteQuest(QuestLog.Windurst, "CREEPY_CRAWLIES");
                    q.Player.AddTitle(Title.CrawlerCuller);
                    q.Player.AddGil(Pay(q));
                    q.Trade.Confirm();
                });
        }
    }
}
