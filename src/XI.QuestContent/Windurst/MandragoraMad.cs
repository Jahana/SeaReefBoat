// Mandragora Mad (Log ID 2, Quest ID 34), ported from scripts/zones/Windurst_Walls/npcs/Yoran-Oran.lua (quest rework
// B3b, Windurst group 17; call-only: every branch of his script is this quest's, so his script only calls it).
// Yoran-Oran : !pos -109.987 -14 203.338 239. He has a default action (event 245, Windurst_Walls/DefaultActions.lua):
// after a zone-in the first talk plays it and the next his script. Blast from the Past and Toraimarai Turmoil (C#) answer
// him from their own containers, and the CoP and Windurst missions (Lua) from their own sections, before his script.
// Trigger: available: 249 (its finish, any option: begun); accepted: 256; completed: nothing. Trade, not available,
// exactly one of (npcUtil.tradeHas(trade, item, true)): a Cornette: 251 (200 x GIL_RATE); a Pinch of Yuhtunga Sulfur:
// 252 (250); a Three-Leaf Mandragora Bud: 253 (1200); a Four-Leaf Mandragora Bud: 254 (120); a Snobby Letter: 255 (5500);
// anything else: 250. 251-255's finish: npcUtil.completeQuest with Windurst fame 10 / 10 / 22 / 10 / 36 by the event,
// then that gil x GIL_RATE (addGil, no message) and the trade confirmed.
// The fame differs by event, which one QuestReward (one per quest) cannot say: each finish makes the calls
// npcUtil.completeQuest makes for a table of fame alone (addFame, then the raw player:completeQuest, so the quest's
// Quest[2][34] vars stay), so no QuestReward is declared.
// Kept as the Lua has it: 249 begins on any option; the turn-ins read no status (a completed char repeats them, each
// completing the quest again with its fame); the completed talk plays nothing; any trade that is not exactly one of the
// five items, while not available, plays 250 (two Cornettes too); the event arguments and the gil are the amount x
// GIL_RATE (the engine's integral getter, llround).
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class MandragoraMad : QuestScript
    {
        public MandragoraMad() : base(QuestLog.Windurst, "MANDRAGORA_MAD") { }

        // xi.settings.main.GIL_RATE * gil: the event argument and the addGil, read by the engine's integral getter (llround)
        static int Pay(IQuestContext q, int gil) =>
            (int)Math.Round(q.Settings.MainNumber("GIL_RATE") * gil, MidpointRounding.AwayFromZero);

        // each turn-in in the Lua's elseif order: its event, the item, the gil and the fame
        static readonly (int Csid, int Item, int Gil, int Fame)[] TurnIns =
        {
            (251, Item.Cornette, 200, 10),
            (252, Item.PinchOfYuhtungaSulfur, 250, 10),
            (253, Item.ThreeLeafMandragoraBud, 1200, 22),
            (254, Item.FourLeafMandragoraBud, 120, 10),
            (255, Item.SnobbyLetter, 5500, 36),
        };

        protected override void Build(QuestScriptBuilder b)
        {
            // Yoran-Oran.lua onTrigger: available: 249
            b.State(QuestStatus.Available)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.YoranOran).Called().OnTrigger(QuestReply.Event(249));

            // accepted: 256 (completed: nothing)
            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.YoranOran).Called().OnTrigger(QuestReply.Event(256));

            // Yoran-Oran.lua onTrade: not available: exactly one turn-in item: its event; else 250
            b.AnyState(when: q => q.Quest.Status != QuestStatus.Available)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.YoranOran).Called()
                    .OnTrade(q =>
                    {
                        foreach (var t in TurnIns)
                        {
                            if (q.Trade.HasExactly(t.Item))
                            {
                                return QuestReply.Event(t.Csid, Pay(q, t.Gil));
                            }
                        }

                        return QuestReply.Event(250);
                    });

            // Yoran-Oran.lua onEventFinish
            b.AnyState().OnEventFinish(Zone.WindurstWalls, 249, q => q.Quest.Begin());

            foreach (var t in TurnIns)
            {
                var turnIn = t;
                b.AnyState().OnEventFinish(Zone.WindurstWalls, turnIn.Csid, q =>
                {
                    // npcUtil.completeQuest with the event's fame and the fameArea WINDURST, step for step: not
                    // quest:complete, whose cleanup would clear the quest's own Quest[2][34] vars
                    q.Player.AddFame(FameArea.Windurst, turnIn.Fame);
                    q.Player.CompleteQuest(QuestLog.Windurst, "MANDRAGORA_MAD");
                    q.Player.AddGil(Pay(q, turnIn.Gil));
                    q.Trade.Confirm();
                });
            }
        }
    }
}
