// Unlocking a Myth: Ranger (Log ID 3, Quest ID 112, UNLOCKING_A_MYTH_RANGER), a port of
// scripts/quests/jeuno/Unlocking_A_Myth_RNG.lua (quest rework B3b): xi.jeuno.helpers.UnlockingAMyth:new(xi.job.RNG),
// the template's sections for its job (UnlockingAMyth.cs).
namespace XI.QuestContent.Jeuno
{
    public sealed class UnlockingAMythRanger : UnlockingAMyth
    {
        public UnlockingAMythRanger() : base(Job.Rng) { }
    }
}
