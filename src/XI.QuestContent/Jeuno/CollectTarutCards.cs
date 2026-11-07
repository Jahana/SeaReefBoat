// Collect Tarut Cards (Log ID 3, Quest ID 10), a line-for-line port of scripts/quests/jeuno/Collect_Tarut_Cards.lua
// (quest rework B3b). Chululu : !pos -13 -6 -42 245. From Jeuno fame level 3 Chululu offers 28: option 0 gives five
// of one random Tarut card (npcUtil.giveItem; a full bag gives nothing and does not begin it), begins it and sets Date
// to the next JST midnight. Accepted: one each of the Fool, Death, the Hermit and the King (tradeMatches) plays 200:
// completed (Card Collector), fame +7 x3, the cards taken, All in the Cards' Date set to the next JST midnight;
// Chululu's trigger plays 10112 once GetSystemTime() is past Date (option 0: a new batch of five, Date moved), else 27.
// Kept: All in the Cards' Date is written by its char var name (player:setVar), not through that quest.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class CollectTarutCards : QuestScript
    {
        public CollectTarutCards() : base(QuestLog.Jeuno, "COLLECT_TARUT_CARDS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Collect_Tarut_Cards";

        static readonly int[] TarutCards =
        {
            Item.TarutCardTheFool,
            Item.TarutCardDeath,
            Item.TarutCardTheHermit,
            Item.TarutCardTheKing,
        };

        // npcUtil.giveItem(player, { { tarutCards[math.randomInt(1, #tarutCards)], 5 } })
        static bool GiveCards(IQuestContext q) =>
            q.Player.GiveItems((TarutCards[q.Player.RandomInt(1, TarutCards.Length) - 1], 5));

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Title = Title.CardCollector });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Jeuno) >= 3)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Chululu).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(28); // Offers the quest
                })
                .OnEventFinish(Zone.LowerJeuno, 28, q =>
                {
                    if (q.Option == 0 && GiveCards(q))
                    {
                        q.Quest.Begin();
                        q.Quest.SetVar("Date", q.Player.JstMidnight);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Chululu).OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.TarutCardTheFool, 1), (Item.TarutCardDeath, 1), (Item.TarutCardTheHermit, 1), (Item.TarutCardTheKing, 1)))
                    {
                        return QuestReply.ProgressEvent(200); // Finishes the quest
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    if (q.Player.SystemTime > q.Quest.Var("Date"))
                    {
                        return QuestReply.ProgressEvent(10112); // A new batch of cards, once per Earth day
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(27); // Reminder
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 10112, q =>
                {
                    if (q.Option == 0 && GiveCards(q))
                    {
                        q.Quest.SetVar("Date", q.Player.JstMidnight);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 200, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 7);
                        q.Player.AddFame(FameArea.Bastok, 7);
                        q.Player.AddFame(FameArea.Windurst, 7);
                        q.Trade.Complete();
                        // player:setVar('Quest[3][166]Date', JstMidnight()): All in the Cards is first offered after JST
                        // midnight (player:setVar is the char var binding; the Lua writes the var by name)
                        q.Player.SetCharVar("Quest[3][166]Date", q.Player.JstMidnight);
                    }
                });
        }
    }
}
