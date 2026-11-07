// Something Fishy (Log ID 2, Quest ID 52), ported from scripts/zones/Port_Windurst/npcs/Tokaka.lua (quest rework B3b,
// Windurst group 19; call-only: every branch of his script is this quest's, so his script only calls it). Tokaka : !pos
// -190 -2 66 240. He has a default action (event 207, Port_Windurst/DefaultActions.lua): after a zone-in the first talk
// plays it, the next his script. Blast from the Past (C#) answers him from its own container while it is accepted.
// Trigger: accepted or completed (status >= accepted): needToZone: 211; else 209 (0, Bastore Sardine) (its finish:
// TokakaSpokenTo 1). Available: 208 (0, Bastore Sardine) (its finish, any option: begun, TokakaSpokenTo 1). Trade, with
// TokakaSpokenTo 1 and no needToZone (any status): exactly one Bastore Sardine (hasItemQty is exact) and nothing else
// (getItemCount 1; a gil slot counts as one): 210 (70 x GIL_RATE, Bastore Sardine). 210's finish: accepted: completed,
// then Windurst fame 10; else fame 10 alone; then the trade taken, 70 x GIL_RATE gil (addGil, no message),
// TokakaSpokenTo 0, needToZone.
// Kept as the Lua has it: the trade reads the char var and needToZone, not the status (a GM's var 1 on an available
// char gives 210, whose finish then adds fame without completing; a completed char repeats it for fame alone); 208
// begins on any option and the offer does not read needToZone; the event argument and the gil are 70 x GIL_RATE (the
// engine's integral getter, llround).
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class SomethingFishy : QuestScript
    {
        const string SpokenTo = "TokakaSpokenTo";

        public SomethingFishy() : base(QuestLog.Windurst, "SOMETHING_FISHY") { }

        // xi.settings.main.GIL_RATE * 70: the event argument and the addGil, read by the integral getter (llround)
        static int Pay(IQuestContext q) =>
            (int)Math.Round(q.Settings.MainNumber("GIL_RATE") * 70, MidpointRounding.AwayFromZero);

        protected override void Build(QuestScriptBuilder b)
        {
            // Tokaka.lua onTrigger: accepted or completed, needToZone: 211
            b.AnyState(when: q => q.Quest.Status >= QuestStatus.Accepted && q.Player.NeedToZone)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Tokaka).Called().OnTrigger(QuestReply.Event(211));

            // accepted or completed: 209
            b.AnyState(when: q => q.Quest.Status >= QuestStatus.Accepted)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Tokaka).Called()
                    .OnTrigger(QuestReply.Event(209, 0, Item.BastoreSardine1));

            // available: 208
            b.State(QuestStatus.Available)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Tokaka).Called()
                    .OnTrigger(QuestReply.Event(208, 0, Item.BastoreSardine1));

            // Tokaka.lua onTrade: TokakaSpokenTo 1 and no needToZone: one sardine and nothing else: 210
            b.AnyState(when: q => q.Player.CharVar(SpokenTo) == 1 && !q.Player.NeedToZone)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Tokaka).Called()
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasItemQty(Item.BastoreSardine1, 1) && q.Trade.ItemCount == 1)
                        {
                            return QuestReply.Event(210, Pay(q), Item.BastoreSardine1);
                        }

                        return null;
                    });

            // Tokaka.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.PortWindurst, 208, q =>
                {
                    q.Quest.Begin();
                    q.Player.SetCharVar(SpokenTo, 1);
                })
                .OnEventFinish(Zone.PortWindurst, 210, q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted)
                    {
                        // the raw player:completeQuest: not quest:complete, whose cleanup would clear the quest's own
                        // Quest[2][52] vars
                        q.Player.CompleteQuest(QuestLog.Windurst, "SOMETHING_FISHY");
                        q.Player.AddFame(FameArea.Windurst, 10);
                    }
                    else
                    {
                        q.Player.AddFame(FameArea.Windurst, 10);
                    }

                    q.Trade.Complete();
                    q.Player.AddGil(Pay(q));
                    q.Player.SetCharVar(SpokenTo, 0);
                    q.Player.SetNeedToZone(true);
                })
                .OnEventFinish(Zone.PortWindurst, 209, q => q.Player.SetCharVar(SpokenTo, 1));
        }
    }
}
