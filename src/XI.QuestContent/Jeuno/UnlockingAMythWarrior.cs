// Unlocking a Myth: Warrior (Log ID 3, Quest ID 102, UNLOCKING_A_MYTH_WARRIOR), a port of
// scripts/quests/jeuno/Unlocking_A_Myth_WAR.lua (quest rework B3b): xi.jeuno.helpers.UnlockingAMyth:new(xi.job.WAR),
// the template's sections for its job (UnlockingAMyth.cs).
namespace XI.QuestContent.Jeuno
{
    public sealed class UnlockingAMythWarrior : UnlockingAMyth
    {
        public UnlockingAMythWarrior() : base(Job.War) { }
    }
}
