// Wish Upon a Star (Log ID 1, Quest ID 64), a line-for-line port of scripts/quests/bastok/Wish_Upon_a_Star.lua (quest
// rework B3b). Zacc : !pos -255.709 -13 -91.379 235; Malene : !pos -173 -5 64 235; Enu : !pos -253.673 -13 -92.326 235.
// At Bastok fame level 5: Zacc's 329 (Prog 1), Malene's 330 (Prog 2), Enu's 332 (begun). Accepted: a Hatchet traded to
// a Logging Point of Yuhtunga Jungle (205) or Yhoator Jungle (10) gives a Fallen Star when the char has none; Enu takes
// the star at night or midnight with no weather (334: completed) and plays 337 otherwise. Completed: Enu's 335 and
// Zacc's 336 (replaceDefault).
// The reward's item is a stack ({ { BAG_OF_CACTUS_STEMS, 4 } }), which QuestReward cannot hold (Item is one id, Items a
// list of single ids): 334 gives the stack itself before quest:complete, the same npcUtil.giveItem call
// npcUtil.completeQuest makes first, and the reward keeps the fame. modules/custom/lua/quest_workarounds.lua patches
// Enu's trade (NONE at any time, or sunshine at night); its C# port is Modules/Custom/QuestWorkarounds.cs, enabled when
// modules/init.txt lists the file.
// Kept as the Lua file has it: 337 (the wrong time or weather) has no finish, so the star stays. Bastok Markets'
// weather table never rolls NONE (every day's three weathers are 1 or more, and a city has no fog), so 334 is reached
// only by a weather a GM sets (the flows' `weather 0`).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class WishUponAStar : QuestScript
    {
        public WishUponAStar() : base(QuestLog.Bastok, "WISH_UPON_A_STAR") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Wish_Upon_a_Star";

        // the Logging Points' trade: a Hatchet, no Fallen Star held
        static QuestReply LoggingPointTrade(IQuestContext q, int csid)
        {
            if (!q.Player.HasItem(Item.FallenStar) && q.Trade.Matches((Item.Hatchet, 1)))
            {
                if (q.Player.GiveItem(Item.FallenStar, silent: true, fromTrade: true))
                {
                    q.Trade.Complete();
                    return QuestReply.ProgressEvent(csid, Item.FallenStar);
                }
            }
            return null;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            // the Lua's reward also has item = { { BAG_OF_CACTUS_STEMS, 4 } }: given in 334 (see the header)
            b.Reward(new QuestReward { Fame = 50, FameArea = FameArea.Bastok });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Bastok) >= 5)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Enu).OnTrigger(q =>
                    q.Quest.Step == 2 ? QuestReply.ProgressEvent(332) : null)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Malene).OnTrigger(q =>
                    q.Quest.Step == 1 ? QuestReply.ProgressEvent(330) : null)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Zacc).OnTrigger(q =>
                    q.Quest.Step == 0 ? QuestReply.ProgressEvent(329) : null)
                .OnEventFinish(Zone.BastokMarkets, 329, q => q.Quest.SetStep(1))
                .OnEventFinish(Zone.BastokMarkets, 330, q => q.Quest.SetStep(2))
                .OnEventFinish(Zone.BastokMarkets, 332, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                // TODO in the Lua: this should use the HELM system
                .Npc(Zone.YuhtungaJungle, Npc.YuhtungaJungle.LoggingPoint).OnTrade(q => LoggingPointTrade(q, 205))
                .Npc(Zone.YhoatorJungle, Npc.YhoatorJungle.LoggingPoint).OnTrade(q => LoggingPointTrade(q, 10))
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Enu)
                    .OnTrade(q =>
                    {
                        if (q.Trade.Matches((Item.FallenStar, 1)))
                        {
                            bool isNight = q.Player.VanadielTotd == Time.Night ||
                                           q.Player.VanadielTotd == Time.Midnight;

                            if (q.Player.Weather == Weather.None && isNight)
                            {
                                return QuestReply.ProgressEvent(334);
                            }
                            else
                            {
                                return QuestReply.Event(337);
                            }
                        }
                        return null;
                    })
                    .OnTrigger(QuestReply.Event(333))
                .OnEventFinish(Zone.BastokMarkets, 334, q =>
                {
                    // npcUtil.completeQuest gives reward.item first and stops when it does not fit
                    if (q.Player.GiveItems((Item.BagOfCactusStems, 4)) && q.Quest.Complete())
                    {
                        q.Trade.Complete();
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Enu).OnTrigger(QuestReply.Event(335).ReplaceDefault())
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Zacc).OnTrigger(QuestReply.Event(336).ReplaceDefault());
        }
    }
}
