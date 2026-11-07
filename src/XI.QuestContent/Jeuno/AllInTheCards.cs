// All in the Cards (Log ID 3, Quest ID 166), a line-for-line port of scripts/quests/jeuno/All_in_the_Cards.lua (quest
// rework B3b). Chululu : !pos -13 -6 -42 245. A repeatable: with Collect Tarut Cards completed and GetSystemTime() past
// Date, Chululu offers 10110 (available) or 10113 (completed); option 0 gives five of one random Tarut card (a full
// bag: nothing, not begun), moves Date to the next JST midnight and begins it. Accepted: 10112 once past Date (a new
// batch whatever the option), else 10111; one of each of the four cards (tradeMatches) plays 10114: completed (600
// gil, Card Collector), fame +16 x3, the cards taken, Date at the next JST midnight. At Chululu its Event 10111 / 10112
// cycle with Rubbish Day's Event 199 (still on Lua), and its Progress offers cycle with that quest's Progress 199.
// Kept: the accepted 10112 gives a batch whatever the option (Collect Tarut Cards' needs option 0).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class AllInTheCards : QuestScript
    {
        public AllInTheCards() : base(QuestLog.Jeuno, "ALL_IN_THE_CARDS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/All_in_the_Cards";

        static readonly int[] TarutCards =
        {
            Item.TarutCardTheFool,
            Item.TarutCardDeath,
            Item.TarutCardTheHermit,
            Item.TarutCardTheKing,
        };

        static bool GiveCards(IQuestContext q)
        {
            if (q.Player.GiveItems((TarutCards[q.Player.RandomInt(1, TarutCards.Length) - 1], 5)))
            {
                q.Quest.SetVar("Date", q.Player.JstMidnight);
                return true;
            }

            return false;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Gil = 600, Title = Title.CardCollector });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Jeuno, "COLLECT_TARUT_CARDS") == QuestStatus.Completed &&
                    q.Player.SystemTime > q.Quest.Var("Date"))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Chululu).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(10110); // Offers the quest
                })
                .OnEventFinish(Zone.LowerJeuno, 10110, q =>
                {
                    if (q.Option == 0 && GiveCards(q))
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Completed, when: q =>
                    q.Player.QuestStatus(QuestLog.Jeuno, "COLLECT_TARUT_CARDS") == QuestStatus.Completed &&
                    q.Player.SystemTime > q.Quest.Var("Date"))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Chululu).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(10113); // Offers the quest again
                })
                .OnEventFinish(Zone.LowerJeuno, 10113, q =>
                {
                    if (q.Option == 0 && GiveCards(q))
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Chululu).OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.TarutCardTheFool, 1), (Item.TarutCardDeath, 1), (Item.TarutCardTheHermit, 1), (Item.TarutCardTheKing, 1)))
                    {
                        return QuestReply.ProgressEvent(10114); // Finishes the quest
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    if (q.Player.SystemTime > q.Quest.Var("Date"))
                    {
                        return QuestReply.Event(10112); // A new batch of cards, once per Earth day
                    }
                    else
                    {
                        return QuestReply.Event(10111); // Reminder
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 10112, q =>
                {
                    GiveCards(q);
                })
                .OnEventFinish(Zone.LowerJeuno, 10114, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 16);
                        q.Player.AddFame(FameArea.Bastok, 16);
                        q.Player.AddFame(FameArea.Windurst, 16);
                        q.Trade.Complete();
                        q.Quest.SetVar("Date", q.Player.JstMidnight); // Next offer waits for JST midnight
                    }
                });
        }
    }
}
