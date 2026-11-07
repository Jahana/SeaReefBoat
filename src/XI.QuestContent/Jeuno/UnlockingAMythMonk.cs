// Unlocking a Myth: Monk (Log ID 3, Quest ID 103, UNLOCKING_A_MYTH_MONK), a port of
// scripts/quests/jeuno/Unlocking_A_Myth_MNK.lua (quest rework B3b): xi.jeuno.helpers.UnlockingAMyth:new(xi.job.MNK),
// the template's sections for its job (UnlockingAMyth.cs).
namespace XI.QuestContent.Jeuno
{
    public sealed class UnlockingAMythMonk : UnlockingAMyth
    {
        public UnlockingAMythMonk() : base(Job.Mnk) { }
    }
}
