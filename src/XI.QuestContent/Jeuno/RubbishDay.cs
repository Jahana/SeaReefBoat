// Rubbish Day (Log ID 3, Quest ID 13), a line-for-line port of scripts/quests/jeuno/Rubbish_Day.lua (quest rework B3b).
// Chululu : !pos -13 -6 -42 245; Mashira : !pos 141 -6 138 200. With Collect Tarut Cards completed Chululu gives
// compatibility readings (199): the event's updates resolve a typed partner name (updateEvent(the char's id, the
// partner's id or 0, the reply)) or a partner's id, and the first reading with a partner in the zone on each Vana'diel
// day adds 1 to Prog (an eventBits row, drawn by math.randomInt). From Prog 3, on a day without a reading, she offers 198
// (option 0: begun, the Magic Trash, Prog 0). Accepted: Mashira in Garlaige Citadel plays 11 with 1 at Prog 0 (option 1:
// the trash deleted, Prog 1); Chululu 197 at Prog 1 (completed: 6000 gil, Chain Choker, fame +13 x3), else 49.
// Completed: Chululu's 199 readings go on (not counted). Kept: a partner id that is the char's own counts (only the typed
// name checks the partner is another char); the 198 finish begins the quest and then sets Prog 0.
using System.Text;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class RubbishDay : QuestScript
    {
        public RubbishDay() : base(QuestLog.Jeuno, "RUBBISH_DAY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Rubbish_Day";

        // compatibilityReplies
        const int ReplyDefault = 0;    // Used for a valid partner; 'Your partner doesn't seem to be nearby.' when no partner is matched.
        const int ReplyChululu = 1;    // 'What? With me!? Spare me.'
        const int ReplyKurouMorou = 2; // 'What? With that creep? Over my dead body!'
        const int ReplyEmpty = 3;      // 'Please fill in your partner's name.'

        // TODO: Returned bits may be based off of name or other factors. Requires significant captures. For now, this is one of each reply, chosen randomly.
        static readonly int[][] EventBits =
        {
            new[] { 4, 3, 5, 8, 2, 2, 0, 0 },
            new[] { 4, 3, 5, 8, 1, 2, 0, 0 },
            new[] { 2, 1, 0, 0, 0, 1, 0, 0 },
            new[] { 3, 1, 0, 0, 0, 1, 0, 0 },
            new[] { 8, 3, 1, 1, 1, 2, 1, 0 },
            new[] { 8, 3, 5, 8, 2, 1, 1, 0 },
        };

        // Resolves a typed partner name to (matchedId, reply) for the compatibility reading.
        static (int MatchedId, int Reply) CheckCompatibilityString(IQuestContext q, string name)
        {
            if (name == "")
            {
                return (0, ReplyEmpty);
            }

            // string.lower (the C locale): A-Z only, every other character as it is
            var lowered = new StringBuilder(name.Length);
            foreach (char ch in name)
            {
                lowered.Append(ch is >= 'A' and <= 'Z' ? (char)(ch + ('a' - 'A')) : ch);
            }
            string input = lowered.ToString();

            if (input == "chululu")
            {
                return (0, ReplyChululu);
            }
            else if (input == "kurou-morou")
            {
                return (0, ReplyKurouMorou);
            }

            var partner = q.Player.PlayerByName(name);

            // partner:getZoneID() == player:getZoneID(): the char's zone is the handler's (an event update in its zone)
            if (partner is { } p &&
                p.Id != q.Player.Id &&
                p.ZoneId == q.Zone)
            {
                return (p.Id, ReplyDefault);
            }

            return (0, ReplyDefault);
        }

        // Compatibility reading (event 199). Players may continue to get readings even after the quest is offered / completed.
        static void OnCompatibilityUpdate(IQuestContext q, bool countReading)
        {
            if (q.OptionText is { } text)
            {
                var (matchedId, reply) = CheckCompatibilityString(q, text);
                q.Player.UpdateEvent(q.Player.Id, matchedId, reply);
            }
            else if (q.Option != 0)
            {
                var partner = q.Player.PlayerById(q.Option);

                if (partner is { } p &&
                    p.ZoneId == q.Zone)
                {
                    if (countReading &&
                        q.Quest.Var("Day") != q.Player.VanadielUniqueDay)
                    {
                        q.Quest.SetStep(q.Quest.Step + 1);
                        q.Quest.SetVar("Day", q.Player.VanadielUniqueDay);
                    }

                    q.Player.UpdateEvent(EventBits[q.Player.RandomInt(1, EventBits.Length) - 1]);
                }
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Gil = 6000, Item = Item.ChainChoker });
            b.Steps(1, 2, 3);

            b.State(QuestStatus.Available, when: q => q.Player.QuestStatus(QuestLog.Jeuno, "COLLECT_TARUT_CARDS") == QuestStatus.Completed)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Chululu).OnTrigger(q =>
                {
                    if (q.Quest.Step >= 3 && q.Quest.Var("Day") != q.Player.VanadielUniqueDay)
                    {
                        return QuestReply.ProgressEvent(198); // Offers the quest
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(199); // Compatibility test; first completed reading each day counts, but can get unlimited readings until you are offered the quest.
                    }
                })
                .OnEventUpdate(Zone.LowerJeuno, 199, q => OnCompatibilityUpdate(q, true))
                .OnEventFinish(Zone.LowerJeuno, 198, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.MagicTrash);
                        q.Quest.SetStep(0);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Chululu).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(197); // Finishes the quest
                    }
                    else
                    {
                        return QuestReply.Event(49); // Reminder
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 197, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 13);
                        q.Player.AddFame(FameArea.Bastok, 13);
                        q.Player.AddFame(FameArea.Windurst, 13);
                    }
                })
                .Npc(Zone.GarlaigeCitadel, Npc.GarlaigeCitadel.Mashira).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(11, 1);
                    }
                    return null;
                })
                .OnEventFinish(Zone.GarlaigeCitadel, 11, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.DelKeyItem(KeyItem.MagicTrash);
                        q.Quest.SetStep(1);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Chululu).OnTrigger(QuestReply.Event(199)) // Compatibility can be tested even after completing the quest. (Cycles with her other dialog)
                .OnEventUpdate(Zone.LowerJeuno, 199, q => OnCompatibilityUpdate(q, false));
        }
    }
}
