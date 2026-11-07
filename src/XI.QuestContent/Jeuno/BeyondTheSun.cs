// Beyond the Sun (Log ID 3, Quest ID 76), a line-for-line port of scripts/quests/jeuno/LB05_2_Beyond_the_Sun.lua (quest
// rework B3b). Maat : !pos 8 3 118 243. With Shattering Stars completed, Maat answers a char of one of the fifteen
// original jobs with 74 once maatsCap holds all fifteen jobs (completed: Maat's Cap, Ultimate Champion of the World),
// else the rematch 78 with the job; any other job gets MAAT_CAP_PLACEHOLDER. Completed: 78 with the job from level 66.
// Kept Lua quirks: 74's finish ignores quest:complete's result (a full bag: not completed, Maat's Cap not given, and 74
// plays again); the available section reads no level.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class BeyondTheSun : QuestScript
    {
        public BeyondTheSun() : base(QuestLog.Jeuno, "BEYOND_THE_SUN") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/LB05_2_Beyond_the_Sun";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.MaatsCap, Title = Title.UltimateChampionOfTheWorld });

            // Section: Quest available.
            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Jeuno, "SHATTERING_STARS") == QuestStatus.Completed)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Maat).OnTrigger(q =>
                {
                    if (q.Player.MainJob <= 15)
                    {
                        // utils.mask.isFull(mask, 15): the low 15 bits all set
                        const int fullMask = (1 << 15) - 1;
                        if ((q.Player.CharVar("maatsCap") & fullMask) == fullMask) // Defeated maat on 15 jobs
                        {
                            return QuestReply.ProgressEvent(74);
                        }
                        else
                        {
                            return QuestReply.Event(78, q.Player.MainJob); // Rematch dialog. Job dependant.
                        }
                    }
                    else
                    {
                        return QuestReply.MessageText(Text.RuludeGardens.MAAT_CAP_PLACEHOLDER);
                    }
                })
                .OnEventFinish(Zone.RuludeGardens, 74, q =>
                {
                    q.Quest.Complete();
                });

            // Section: Quest completed.
            b.State(QuestStatus.Completed)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Maat).OnTrigger(q =>
                {
                    if (q.Player.MainJob <= 15 &&
                        q.Player.MainLevel >= 66)
                    {
                        return QuestReply.Event(78, q.Player.MainJob);
                    }
                    return null;
                });
        }
    }
}
