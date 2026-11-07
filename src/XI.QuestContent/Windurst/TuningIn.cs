// Tuning In (Log ID 2, Quest ID 90), ported from the Tuning In branches of scripts/zones/Windurst_Waters/npcs/Leepe-Hoppe.lua
// (quest rework B3b, Windurst group 18; call-only: his script calls Tuning In, then Tuning Out). Leepe-Hoppe : !pos 13 -9
// -197 238. He has a default action (event 345, Windurst_Waters/DefaultActions.lua): after a zone-in the first talk plays
// it, the next his script. The Moonlit Path, Food for Thought and Toraimarai Turmoil (C#) answer him from their own
// containers, and Windurst missions 1-3 and 7-2 (Lua) from their own sections, before his script.
// His if-chain, in order: [Tuning In's offer] [Tuning Out's offer] [Tuning Out's 897] [Tuning In's reminder] [Tuning
// Out's reminder]. Available, Windurst fame level 4 or more and the CoP mission at Distant Beliefs or later
// (getCurrentMission(COP) >= DISTANT_BELIEFS) or The Last Verse completed: 884 (0, 1696, 1697, 1698); its finish (any
// option): begun. Accepted: 885 (0, 1696, 1697, 1698), the reminder, which comes after Tuning Out's 897 in his chain:
// it yields when 897 holds (Tuning Out accepted with TuningOut_Progress 8). Tuning Out's offer needs this quest
// completed, so it never meets this quest's arms. The trade of exactly the Magicked Steel Ingot, the Spruce Lumber and
// the Extra-fine File (npcUtil.tradeHasExactly), then accepted: 886; its finish: npcUtil.completeQuest's steps
// (4000 gil x GIL_RATE with GIL_OBTAINED, Fine Tuner, the raw player:completeQuest, so the quest's Quest[2][90] vars
// stay), then (always: no item in the table) tradeComplete.
// Kept as the Lua has it: 884 begins on any option; 886's finish reads no status; the trade checks the items before
// the status; the "or The Last Verse completed" operand, which is always false in game (for the CoP log
// hasCompletedMission is mission < current, and current never passes 850: MaxMissionId 851, and Dawn's nextMission is
// The Last Verse itself), so no flow sees it: only the XI.Verify.Quests check `windurst g18:Tuning In's offer
// condition` pins it.
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class TuningIn : QuestScript
    {
        public TuningIn() : base(QuestLog.Windurst, "TUNING_IN") { }

        // Tuning Out's 897 (accepted, TuningOut_Progress 8): it comes before this quest's reminder in his chain
        static bool TuningOutFinishes(IQuestCheck q) =>
            q.Player.QuestStatus(QuestLog.Windurst, "TUNING_OUT") == QuestStatus.Accepted &&
            q.Player.CharVar("TuningOut_Progress") == 8;

        protected override void Build(QuestScriptBuilder b)
        {
            // Leepe-Hoppe.lua onTrigger: available, fame level 4, CoP Distant Beliefs or later, or The Last Verse completed
            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.Windurst) >= 4 &&
                    (q.Player.CurrentMission(MissionLog.Cop) >= Mission.Cop.DistantBeliefs ||
                     q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.TheLastVerse)))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.LeepeHoppe).Called()
                    .OnTrigger(QuestReply.Event(884, 0, Item.MagickedSteelIngot, Item.PieceOfSpruceLumber, Item.ExtraFineFile));

            // accepted: the reminder 885, unless Tuning Out's 897 (before it in his chain) holds
            b.State(QuestStatus.Accepted, when: q => !TuningOutFinishes(q))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.LeepeHoppe).Called()
                    .OnTrigger(QuestReply.Event(885, 0, Item.MagickedSteelIngot, Item.PieceOfSpruceLumber, Item.ExtraFineFile));

            // Leepe-Hoppe.lua onTrade: exactly the three items, then accepted: 886
            b.AnyState()
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.LeepeHoppe).Called()
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.MagickedSteelIngot, Item.PieceOfSpruceLumber, Item.ExtraFineFile) &&
                            q.Quest.Status == QuestStatus.Accepted)
                        {
                            return QuestReply.Event(886);
                        }

                        return null;
                    });

            // Leepe-Hoppe.lua onEventFinish (884, 886)
            b.AnyState()
                .OnEventFinish(Zone.WindurstWaters, 884, q => q.Quest.Begin())
                .OnEventFinish(Zone.WindurstWaters, 886, q =>
                {
                    // npcUtil.completeQuest with gil 4000 and the title FINE_TUNER, step for step: not quest:complete,
                    // whose cleanup would clear the quest's own Quest[2][90] vars. No item, so it always returns true
                    int gil = RateAmount(4000, q.Settings.MainNumber("GIL_RATE"));
                    q.Player.AddGil(gil);
                    q.Text.Special(Text.WindurstWaters.GIL_OBTAINED, gil);
                    q.Player.AddTitle(Title.FineTuner);
                    q.Player.CompleteQuest(QuestLog.Windurst, "TUNING_IN");
                    q.Trade.Complete();
                });
        }

        // n * rate as the engine's binding reads a Lua number into an integer argument (llround: half away from zero)
        private static int RateAmount(int n, double rate) => (int)Math.Round(n * rate, MidpointRounding.AwayFromZero);
    }
}
