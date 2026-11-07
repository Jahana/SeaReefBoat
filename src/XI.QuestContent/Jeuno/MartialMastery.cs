// Martial Mastery (Log ID 3, Quest ID 167), a line-for-line port of scripts/quests/jeuno/Martial_Mastery.lua (quest
// rework B3b). Nomad Moogle : !pos 10.012 1.453 121.883 243. Available to a char of level 96 or more with any combat skill
// (hand-to-hand to staff, archery, marksmanship) at 357 or more: the moogle plays 10196, whose finish begins it.
// Accepted, with the same level and skill and 15 or more merit points: 10198, whose finish completes it (Heart of the
// Bushin, Bushin-Ryu Inheritor) and spends 15 merit points (LEARNED_SECRET_TECHNIQUE). The Lua file has no known bug to
// keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class MartialMastery : QuestScript
    {
        public MartialMastery() : base(QuestLog.Jeuno, "MARTIAL_MASTERY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Martial_Mastery";

        private static readonly int[] ValidCombatSkills =
        {
            Skill.HandToHand,
            Skill.Dagger,
            Skill.Sword,
            Skill.GreatSword,
            Skill.Axe,
            Skill.GreatAxe,
            Skill.Scythe,
            Skill.Polearm,
            Skill.Katana,
            Skill.GreatKatana,
            Skill.Club,
            Skill.Staff,
            Skill.Archery,
            Skill.Marksmanship,
        };

        private static bool HasRequiredCombatSkill(IQuestPlayerView player)
        {
            foreach (int v in ValidCombatSkills)
            {
                if (player.SkillLevel(v) >= 357)
                {
                    return true;
                }
            }

            return false;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { KeyItem = KeyItem.HeartOfTheBushin, Title = Title.BushinRyuInheritor });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainLevel >= 96 &&
                    HasRequiredCombatSkill(q.Player))
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.NomadMoogle).OnTrigger(QuestReply.ProgressEvent(10196))
                .OnEventFinish(Zone.RuludeGardens, 10196, q =>
                {
                    q.Quest.Begin();
                });

            // TODO: Confirm that the player must be on a valid job to complete
            b.State(QuestStatus.Accepted, when: q =>
                    q.Player.MainLevel >= 96 &&
                    HasRequiredCombatSkill(q.Player) &&
                    q.Player.MeritCount >= 15)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.NomadMoogle).OnTrigger(QuestReply.ProgressEvent(10198))
                .OnEventFinish(Zone.RuludeGardens, 10198, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetMerits(q.Player.MeritCount - 15);
                        q.Text.Special(Text.RuludeGardens.LEARNED_SECRET_TECHNIQUE);
                    }
                });
        }
    }
}
