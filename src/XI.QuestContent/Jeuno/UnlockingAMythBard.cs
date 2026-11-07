// Unlocking a Myth: Bard (Log ID 3, Quest ID 111, UNLOCKING_A_MYTH_BARD), a port of
// scripts/quests/jeuno/Unlocking_A_Myth_BRD.lua (quest rework B3b): xi.jeuno.helpers.UnlockingAMyth:new(xi.job.BRD),
// the template's sections for its job (UnlockingAMyth.cs).
namespace XI.QuestContent.Jeuno
{
    public sealed class UnlockingAMythBard : UnlockingAMyth
    {
        public UnlockingAMythBard() : base(Job.Brd) { }
    }
}
