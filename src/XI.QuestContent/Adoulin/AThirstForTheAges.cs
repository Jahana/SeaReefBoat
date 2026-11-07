// A Thirst for the Ages (Log ID 9, Quest ID 114), ported from the quest branch of
// scripts/zones/Western_Adoulin/npcs/Nikkhail.lua (quest rework B3b Adoulin group 7; call-only: the script calls this
// quest inside its SoA test, Life on the Frontier or later). Nikkhail !pos -101 3 9 256 (default 510).
// Accepted, ATFTA_Status below 2 and without the Copy of the Alliance Agreement: 5053, whose finish gives the copy
// (npcUtil.giveKeyItem). Nothing else: no script begins or completes the quest or sets ATFTA_Status.
// Kept as the script has it: the finish reads no status.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class AThirstForTheAges : QuestScript
    {
        public AThirstForTheAges() : base(QuestLog.Adoulin, "A_THIRST_FOR_THE_AGES") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Nikkhail).Called().OnTrigger(q =>
                {
                    QuestStatus atfta = q.Quest.Status;
                    bool atftaNeedKI = q.Player.CharVar("ATFTA_Status") < 2 &&
                        !q.Player.HasKeyItem(KeyItem.CopyOfTheAllianceAgreement);

                    if (atfta == QuestStatus.Accepted && atftaNeedKI)
                    {
                        // Progresses Quest: 'A Thirst for the Ages'
                        return QuestReply.Event(5053);
                    }

                    return null;
                });

            // the finish, keyed on the csid alone as the script's is
            b.AnyState()
                .OnEventFinish(Zone.WesternAdoulin, 5053, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.CopyOfTheAllianceAgreement);
                });
        }
    }
}
