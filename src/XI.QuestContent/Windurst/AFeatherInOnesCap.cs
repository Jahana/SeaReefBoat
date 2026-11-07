// A Feather in One's Cap (Log ID 2, Quest ID 1), ported from the A Feather in One's Cap branches of
// scripts/zones/Windurst_Waters/npcs/Baren-Moren.lua (quest rework B3b, Windurst group 13; call-only: his script calls
// Hat in Hand, then this quest, then plays his random standard dialog). Baren-Moren : !pos -66 -3 -148 238.
// Hat in Hand completed, this quest available, Windurst fame level 3 or more and no needToZone: 75 (0, 842); option 1
// begins it (available) or sets the repeat var QuestFeatherInOnesCap_var 1 (completed). Accepted or the repeat var 1:
// 78 (0, 842). Completed and no needToZone: 75 again (the repeat). The trade of 3 Giant Bird Feathers (npcUtil.tradeHas)
// while accepted or with the repeat var 1: 79 (1500); its finish: accepted: npcUtil.completeQuest's steps
// (Windurst fame 16, the raw player:completeQuest, so the quest's Quest[2][1] vars stay); else Windurst fame 16 and
// the repeat var 0; then 1500 x GIL_RATE gil (no message), the trade confirmed, needToZone set.
// Kept as the Lua has it: 75's and 79's finishes read the status when the event ends; the 79 event argument is the
// literal 1500 (the gil paid is 1500 x GIL_RATE); 75's option 1 while accepted does nothing; the repeat runs through
// the char var, not the quest status.
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class AFeatherInOnesCap : QuestScript
    {
        const string RepeatVar = "QuestFeatherInOnesCap_var";

        public AFeatherInOnesCap() : base(QuestLog.Windurst, "A_FEATHER_IN_ONES_CAP") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Baren-Moren.lua onTrigger, after Hat in Hand's arms: Hat in Hand completed, available, Windurst fame level 3
            // or more, no needToZone: Quest 'Feather In One's Cap' offered
            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Windurst, "HAT_IN_HAND") == QuestStatus.Completed &&
                    q.Player.FameLevel(FameArea.Windurst) >= 3 &&
                    !q.Player.NeedToZone)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.BarenMoren).Called()
                    .OnTrigger(QuestReply.Event(75, 0, Item.GiantBirdFeather));

            // accepted or the repeat var 1: Quest Objective Reminder; Baren-Moren.lua onTrade (the same check): 3 Giant
            // Bird Feathers (npcUtil.tradeHas): Quest Turn In
            b.AnyState(when: q => q.Quest.Status == QuestStatus.Accepted || q.Player.CharVar(RepeatVar) == 1)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.BarenMoren).Called()
                    .OnTrigger(QuestReply.Event(78, 0, Item.GiantBirdFeather))
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasQty((Item.GiantBirdFeather, 3)))
                        {
                            return QuestReply.Event(79, 1500);
                        }

                        return null;
                    });

            // completed, no needToZone: Repeatable Quest 'A Feather In One's Cap' offered
            b.State(QuestStatus.Completed, when: q => !q.Player.NeedToZone)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.BarenMoren).Called()
                    .OnTrigger(QuestReply.Event(75, 0, Item.GiantBirdFeather));

            // Baren-Moren.lua onEventFinish (75, 79)
            b.AnyState()
                .OnEventFinish(Zone.WindurstWaters, 75, q =>
                {
                    if (q.Option == 1)
                    {
                        if (q.Quest.Status == QuestStatus.Available)
                        {
                            q.Quest.Begin();
                        }
                        else if (q.Quest.Status == QuestStatus.Completed)
                        {
                            q.Player.SetCharVar(RepeatVar, 1);
                        }
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 79, q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted)
                    {
                        // npcUtil.completeQuest with fame 16 and the fameArea WINDURST, step for step: not
                        // quest:complete, whose cleanup would clear the quest's own Quest[2][1] vars
                        q.Player.AddFame(FameArea.Windurst, 16);
                        q.Player.CompleteQuest(QuestLog.Windurst, "A_FEATHER_IN_ONES_CAP");
                    }
                    else
                    {
                        q.Player.AddFame(FameArea.Windurst, 16);
                        q.Player.SetCharVar(RepeatVar, 0);
                    }

                    // xi.settings.main.GIL_RATE * 1500, read by the engine's integral getter (llround)
                    q.Player.AddGil((int)Math.Round(q.Settings.MainNumber("GIL_RATE") * 1500, MidpointRounding.AwayFromZero));
                    q.Trade.Confirm();
                    q.Player.SetNeedToZone(true);
                });
        }
    }
}
