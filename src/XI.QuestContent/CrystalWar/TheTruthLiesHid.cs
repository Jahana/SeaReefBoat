// The Truth Lies Hid (Log ID 7, Quest ID 58), a line-for-line port of
// scripts/quests/crystalWar/WOTG_BAS_11_The_Truth_Lies_Hid.lua (quest rework B3b Crystal War group 5). Bastok Markets
// [S]: Gentle Tiger !pos -203.9 -10 2.2 (default 109). Castle Zvahl Keep [S]: Rikke !pos -90.1 0.6 86.3 (default 28),
// Rakke !pos -113.7 0.6 -149.9 (default 29), Rokke !pos -30 0.6 -153.9 (default 30), qm !pos -384.3 -52 -95.8 (default
// NOTHING_OUT_OF_ORDINARY), Displaced Block !pos -264.1 -53 82.3 (no default). Available, What Price Loyalty completed
// and the current WotG mission Adieu, Lilisette or later: Gentle Tiger 211, whose finish begins it. Accepted, by Prog:
// 0: Gentle Tiger 212 (an Event); any zone-in to Castle Zvahl Baileys [S]: 12 (-> 1). 1: the imps' puzzle. The day's
// scenario is impScenarios[(VanadielUniqueDay() % 3) + 1] (honest imp 2, 1, 3; each imp's statement param). An imp's
// talk: traded today: its selfTrade (155, 0, success) when it was the one traded, else its otherTrade (success); else
// talked today: its reminder (155, 0, statement); else the day noted and its initial (155, 0, statement). An Elixir
// alone, no trade today: the trade state noted (day, imp, success); the honest imp: its trade as a progress event (155,
// 0, 1), else as an event (155, 0, 0). Each trade csid's finish (12 / 13 / 14): that imp traded with success: Prog 2.
// 2: each imp: the honest one of today's scenario: its selfTrade (155, 0, 1), else its otherTrade (1); qm 15 (-> 3). 3:
// the Displaced Block 16, whose finish completes it (a Sanctus Rosary).
// Kept as the Lua has it: 211 begins on any option; the Prog 0 zone-in plays 12 from any zone; the Elixir is never
// taken (no confirmTrade); a wrong imp's trade still blocks the day; the honest imp at Prog 2 is today's, not the one
// traded; 16 completes with no check of its result; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class TheTruthLiesHid : QuestScript
    {
        public TheTruthLiesHid() : base(QuestLog.CrystalWar, "THE_TRUTH_LIES_HID") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_BAS_11_The_Truth_Lies_Hid";

        // the Lua's impScenarios: the honest imp and each imp's statement ([1] .. [3] at [0] .. [2])
        static readonly (int HonestImp, int[] Statements)[] ImpScenarios =
        {
            // Scenario A
            (2, new[] { 1, 1, 1 }),
            // Scenario B
            (1, new[] { 0, 0, 0 }),
            // Scenario C
            (3, new[] { 0, 2, 0 }),
        };

        // the Lua's impData, [1] .. [3] at [0] .. [2]
        static readonly (string DayVar, int Initial, int Reminder, int Trade, int SelfTrade, int OtherTrade)[] ImpData =
        {
            ("Imp1Day", 9, 19, 12, 22, 25),
            ("Imp2Day", 10, 20, 13, 23, 26),
            ("Imp3Day", 11, 21, 14, 24, 27),
        };

        static (int HonestImp, int[] Statements) GetCurrentImpScenario(IQuestContext q)
        {
            // The active scenario rotates by Vana'diel day.
            return ImpScenarios[q.Player.VanadielUniqueDay % ImpScenarios.Length];
        }

        static void SetImpTradeState(IQuestContext q, int impNumber, bool isCorrect)
        {
            q.Quest.SetVar("TradeDay", q.Player.VanadielUniqueDay);
            q.Quest.SetVar("TradeImp", impNumber);
            q.Quest.SetVar("TradeSuccess", isCorrect ? 1 : 0);
        }

        static QuestReply HandleImpPuzzleTrigger(IQuestContext q, int impNumber)
        {
            var imp = ImpData[impNumber - 1];

            if (q.Quest.Var("TradeDay") == q.Player.VanadielUniqueDay)
            {
                int tradeSucceeded = q.Quest.Var("TradeSuccess");

                if (q.Quest.Var("TradeImp") == impNumber)
                {
                    return QuestReply.Event(imp.SelfTrade, 155, 0, tradeSucceeded);
                }

                return QuestReply.Event(imp.OtherTrade, tradeSucceeded);
            }

            int param3 = GetCurrentImpScenario(q).Statements[impNumber - 1];

            if (q.Quest.Var(imp.DayVar) == q.Player.VanadielUniqueDay)
            {
                return QuestReply.Event(imp.Reminder, 155, 0, param3);
            }

            q.Quest.SetVar(imp.DayVar, q.Player.VanadielUniqueDay);
            return QuestReply.Event(imp.Initial, 155, 0, param3);
        }

        static QuestReply HandleImpTrade(IQuestContext q, int impNumber)
        {
            var imp = ImpData[impNumber - 1];

            if (q.Trade.HasExactly(Item.Elixir) &&
                q.Quest.Var("TradeDay") != q.Player.VanadielUniqueDay)
            {
                bool isCorrect = GetCurrentImpScenario(q).HonestImp == impNumber;
                SetImpTradeState(q, impNumber, isCorrect);

                if (isCorrect)
                {
                    return QuestReply.ProgressEvent(imp.Trade, 155, 0, 1);
                }

                return QuestReply.Event(imp.Trade, 155, 0, 0);
            }
            return null;
        }

        static QuestReply HandleSolvedImpTrigger(IQuestContext q, int impNumber)
        {
            var imp = ImpData[impNumber - 1];

            if (GetCurrentImpScenario(q).HonestImp == impNumber)
            {
                return QuestReply.Event(imp.SelfTrade, 155, 0, 1);
            }

            return QuestReply.Event(imp.OtherTrade, 1);
        }

        static void FinishImpTrade(IQuestContext q, int impNumber)
        {
            if (q.Quest.Var("TradeImp") == impNumber && q.Quest.Var("TradeSuccess") == 1)
            {
                q.Quest.SetStep(2);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3);

            b.Reward(new QuestReward
            {
                Item = Item.SanctusRosary,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.CrystalWar, "WHAT_PRICE_LOYALTY") &&
                    q.Player.CurrentMission(MissionLog.Wotg) >= Mission.Wotg.AdieuLilisette)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.ProgressEvent(211))
                .OnEventFinish(Zone.BastokMarketsS, 211, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, step: 0)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(212))
                .OnZoneIn(Zone.CastleZvahlBaileysS, q => QuestReply.Event(12))
                .OnEventFinish(Zone.CastleZvahlBaileysS, 12, q =>
                {
                    q.Quest.SetStep(1);
                });

            b.State(QuestStatus.Accepted, step: 1)
                .Npc(Zone.CastleZvahlKeepS, Npc.CastleZvahlKeepS.Rikke).OnTrade(q => HandleImpTrade(q, 1))
                .Npc(Zone.CastleZvahlKeepS, Npc.CastleZvahlKeepS.Rikke).OnTrigger(q => HandleImpPuzzleTrigger(q, 1))
                .Npc(Zone.CastleZvahlKeepS, Npc.CastleZvahlKeepS.Rakke).OnTrade(q => HandleImpTrade(q, 2))
                .Npc(Zone.CastleZvahlKeepS, Npc.CastleZvahlKeepS.Rakke).OnTrigger(q => HandleImpPuzzleTrigger(q, 2))
                .Npc(Zone.CastleZvahlKeepS, Npc.CastleZvahlKeepS.Rokke).OnTrade(q => HandleImpTrade(q, 3))
                .Npc(Zone.CastleZvahlKeepS, Npc.CastleZvahlKeepS.Rokke).OnTrigger(q => HandleImpPuzzleTrigger(q, 3))
                .OnEventFinish(Zone.CastleZvahlKeepS, 12, q => FinishImpTrade(q, 1))
                .OnEventFinish(Zone.CastleZvahlKeepS, 13, q => FinishImpTrade(q, 2))
                .OnEventFinish(Zone.CastleZvahlKeepS, 14, q => FinishImpTrade(q, 3));

            b.State(QuestStatus.Accepted, step: 2)
                .Npc(Zone.CastleZvahlKeepS, Npc.CastleZvahlKeepS.Rikke).OnTrigger(q => HandleSolvedImpTrigger(q, 1))
                .Npc(Zone.CastleZvahlKeepS, Npc.CastleZvahlKeepS.Rakke).OnTrigger(q => HandleSolvedImpTrigger(q, 2))
                .Npc(Zone.CastleZvahlKeepS, Npc.CastleZvahlKeepS.Rokke).OnTrigger(q => HandleSolvedImpTrigger(q, 3))
                .Npc(Zone.CastleZvahlKeepS, Npc.CastleZvahlKeepS.qm).OnTrigger(QuestReply.ProgressEvent(15))
                .OnEventFinish(Zone.CastleZvahlKeepS, 15, q =>
                {
                    q.Quest.SetStep(3);
                });

            b.State(QuestStatus.Accepted, step: 3)
                .Npc(Zone.CastleZvahlKeepS, Npc.CastleZvahlKeepS.DisplacedBlock).OnTrigger(QuestReply.ProgressEvent(16))
                .OnEventFinish(Zone.CastleZvahlKeepS, 16, q =>
                {
                    q.Quest.Complete();
                });
        }
    }
}
