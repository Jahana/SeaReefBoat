// Never to Return (Log ID 3, Quest ID 14), a line-for-line port of scripts/quests/jeuno/Never_to_Return.lua (quest rework
// B3b). Kurou-Morou : !pos -4 -6 -28 245. With Your Crystal Ball completed Kurou-Morou reads fortunes (204: the update
// answers (VanadielUniqueDay() + the char's id) % 100; option 0 on a new Vana'diel day adds 1 to Prog). From Prog 3, on
// a day without a reading and at Jeuno fame level 5, he offers 202 (option 0: begun). Accepted: 203; a Horn Hairpin
// (tradeMatches) plays 201: completed (1200 gil, Garnet Ring), the hairpin taken, fame +17 x3. Completed: the
// readings go on (not counted). Kept: 201 completes the quest before tradeComplete and the fame.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class NeverToReturn : QuestScript
    {
        public NeverToReturn() : base(QuestLog.Jeuno, "NEVER_TO_RETURN") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Never_to_Return";

        // player:updateEvent((VanadielUniqueDay() + player:getID()) % 100)
        static void FortuneUpdate(IQuestContext q) =>
            q.Player.UpdateEvent((q.Player.VanadielUniqueDay + q.Player.Id) % 100);

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Gil = 1200, Item = Item.GarnetRing });
            b.Steps(1, 2, 3);

            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.Jeuno, "YOUR_CRYSTAL_BALL"))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.KurouMorou).OnTrigger(q =>
                {
                    if (q.Quest.Step >= 3 &&
                        q.Quest.Var("Day") != q.Player.VanadielUniqueDay &&
                        q.Player.FameLevel(FameArea.Jeuno) >= 5)
                    {
                        return QuestReply.ProgressEvent(202); // Quest offered
                    }

                    return QuestReply.Event(204); // Fortune reading
                })
                .OnEventUpdate(Zone.LowerJeuno, 204, q => FortuneUpdate(q))
                .OnEventFinish(Zone.LowerJeuno, 202, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 204, q =>
                {
                    if (q.Option == 0 && q.Quest.Var("Day") != q.Player.VanadielUniqueDay)
                    {
                        q.Quest.SetStep(q.Quest.Step + 1);
                        q.Quest.SetVar("Day", q.Player.VanadielUniqueDay);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.KurouMorou).OnTrigger(q =>
                {
                    return QuestReply.Event(203); // Reminder
                })
                .OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.HornHairpin, 1)))
                    {
                        return QuestReply.ProgressEvent(201); // Quest complete
                    }
                    return null;
                })
                .OnEventFinish(Zone.LowerJeuno, 201, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        q.Player.AddFame(FameArea.Sandoria, 17);
                        q.Player.AddFame(FameArea.Bastok, 17);
                        q.Player.AddFame(FameArea.Windurst, 17);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.KurouMorou).OnTrigger(q =>
                {
                    return QuestReply.Event(204); // Fortune reading
                })
                .OnEventUpdate(Zone.LowerJeuno, 204, q => FortuneUpdate(q));
        }
    }
}
