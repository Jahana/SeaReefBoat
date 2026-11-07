// Chocobo's Wounds' era module, a line-for-line port of modules/era/lua/quests/jeuno/chocobos_wounds.lua (quest rework
// B3b Jeuno, P4): it reverts the feeding timer to one Vana'diel day (the February 19, 2015 version update made it 45
// seconds). Module:new('era_quest_chocobos_wounds', xi.pre(xi.expansion.SOA)); its onServerStart override replaces
// section 2's Upper Jeuno Chocobo onTrade (73 while Timer >= VanadielUniqueDay(), where the quest has Timer >
// GetSystemTime()) and the feeding finishes 57, 58, 59, 60 and 99 (Timer = VanadielUniqueDay(), where the quest has
// GetSystemTime() + 45; Prog and the confirmed trade as the quest's). 64 stays the quest's.
using System.Collections.Generic;
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Jeuno
{
    public sealed class ChocobosWounds : QuestModule
    {
        public ChocobosWounds() : base("era_quest_chocobos_wounds", "modules/era/lua/quests/jeuno/chocobos_wounds.lua") { }

        // xi.pre(xi.expansion.SOA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Soa);

        // the module's eventTable, by Prog
        static readonly Dictionary<int, int> EventTable = new Dictionary<int, int>
        {
            [1] = 57,
            [2] = 58,
            [3] = 99,
            [4] = 59,
            [5] = 60,
            [6] = 64,
        };

        protected override void Build(QuestModuleBuilder m)
        {
            // quest.sections[2][xi.zone.UPPER_JEUNO]: the quest's state 2 (accepted)
            m.Quest(QuestKey.Jeuno.ChocobosWounds)
                .State(2)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Chocobo).ReplaceOnTrade(q =>
                {
                    // Override trade function timer with VanadielUniqueDay instead of GetSystemTime +45
                    if (q.Trade.ItemQty(Item.BunchOfGysahlGreens) > 0)
                    {
                        return QuestReply.Event(76);
                    }

                    if (q.Trade.ItemQty(Item.ClumpOfGausebitWildgrass) == 0)
                    {
                        return QuestReply.NoAction();
                    }

                    if (q.Quest.Var("Timer") >= q.Player.VanadielUniqueDay)
                    {
                        return QuestReply.Event(73);
                    }

                    int questProgress = q.Quest.Step;
                    if (questProgress <= 2)
                    {
                        return QuestReply.ProgressEvent(EventTable[questProgress]);
                    }

                    if (q.Trade.HasExactly(Item.ClumpOfGausebitWildgrass))
                    {
                        return QuestReply.ProgressEvent(EventTable[questProgress]);
                    }
                    return null;
                })
                .ReplaceOnEventFinish(Zone.UpperJeuno, 57, q =>
                {
                    q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay);
                    q.Quest.SetStep(2);
                })
                .ReplaceOnEventFinish(Zone.UpperJeuno, 58, q =>
                {
                    q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay);
                    q.Quest.SetStep(3);
                })
                .ReplaceOnEventFinish(Zone.UpperJeuno, 59, q =>
                {
                    q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay);
                    q.Quest.SetStep(5);
                    q.Trade.Confirm();
                })
                .ReplaceOnEventFinish(Zone.UpperJeuno, 60, q =>
                {
                    q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay);
                    q.Quest.SetStep(6);
                    q.Trade.Confirm();
                })
                .ReplaceOnEventFinish(Zone.UpperJeuno, 99, q =>
                {
                    q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay);
                    q.Quest.SetStep(4);
                    q.Trade.Confirm();
                });
        }
    }
}
