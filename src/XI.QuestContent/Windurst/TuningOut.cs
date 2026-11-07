// Tuning Out (Log ID 2, Quest ID 91), ported from the Tuning Out branches of scripts/zones/Windurst_Waters/npcs/Leepe-Hoppe.lua
// (quest rework B3b, Windurst group 18; call-only: his script calls Tuning In, then Tuning Out). Leepe-Hoppe : !pos 13
// -9 -197 238.
// Tuning In completed and this quest available: 888; its finish (any option): TuningOut_Progress 1, begun. Accepted
// with TuningOut_Progress 8: 897; its finish: npcUtil.completeQuest's steps (the Cache-Nez, stopping when it cannot be
// given; Windurst fame 40; Friend of the Helmed; the raw player:completeQuest, so the quest's Quest[2][91] vars stay),
// and when it completes, TuningOut_Progress 0. Accepted otherwise: 889 (the reminder, after Tuning In's reminder in
// his chain: Tuning In's handler answers first while it is accepted).
// The progress steps 1 -> 8 stay Lua (char var only, they never name the quest): Kazham Jakoh_Wahcondalo and
// Romaa_Mihgo, Norg Comitiolus, Beaucedine Glacier Torino-Samarino, Yuhtunga Jungle qm2.
// Kept as the Lua has it: 888 sets the var before the quest is added and begins on any option; 897's finish reads no
// status. 888's Tuning In completed operand is shadowed in game (with Tuning In accepted, its 885 answers first), so
// only the XI.Verify.Quests check `windurst g18:Tuning Out's offer condition` (mutant O084) pins it.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class TuningOut : QuestScript
    {
        const string Progress = "TuningOut_Progress";

        public TuningOut() : base(QuestLog.Windurst, "TUNING_OUT") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Leepe-Hoppe.lua onTrigger: Tuning In completed, this quest available: starting dialogue
            b.State(QuestStatus.Available, when: q => q.Player.QuestStatus(QuestLog.Windurst, "TUNING_IN") == QuestStatus.Completed)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.LeepeHoppe).Called().OnTrigger(QuestReply.Event(888));

            // accepted, TuningOut_Progress 8: finishing dialogue
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar(Progress) == 8)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.LeepeHoppe).Called().OnTrigger(QuestReply.Event(897));

            // accepted: reminder to go help Ildy in Kazham
            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.LeepeHoppe).Called().OnTrigger(QuestReply.Event(889));

            // Leepe-Hoppe.lua onEventFinish (888, 897)
            b.AnyState()
                .OnEventFinish(Zone.WindurstWaters, 888, q =>
                {
                    q.Player.SetCharVar(Progress, 1);
                    q.Quest.Begin();
                })
                .OnEventFinish(Zone.WindurstWaters, 897, q =>
                {
                    // npcUtil.completeQuest with the Cache-Nez, the title FRIEND_OF_THE_HELMED, fame 40 and the
                    // fameArea WINDURST, step for step: not quest:complete, whose cleanup would clear the quest's own
                    // Quest[2][91] vars. It returns false, with nothing more done, when the Cache-Nez cannot be given
                    if (!q.Player.GiveItem(Item.CacheNez))
                    {
                        return;
                    }
                    q.Player.AddFame(FameArea.Windurst, 40);
                    q.Player.AddTitle(Title.FriendOfTheHelmed);
                    q.Player.CompleteQuest(QuestLog.Windurst, "TUNING_OUT");
                    q.Player.SetCharVar(Progress, 0); // zero when the quest is done
                });
        }
    }
}
