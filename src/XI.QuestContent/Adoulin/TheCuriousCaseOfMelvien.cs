// The Curious Case of Melvien (Log ID 9, Quest ID 138), ported from the quest branch of
// scripts/zones/Western_Adoulin/npcs/Mastan.lua (quest rework B3b Adoulin group 6; call-only: the script calls this
// quest after Order Up's call). Mastan : !pos -9 0 67 256.
// Accepted, with Melvien's Turn and without Melvien's Death: 184, whose finish on option 1 gives Melvien's Death.
// Nothing else: no script begins or completes the quest, and nothing gives Melvien's Turn (the rest of the quest is
// not implemented).
// Kept as the script has it: the finish reads no status.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class TheCuriousCaseOfMelvien : QuestScript
    {
        public TheCuriousCaseOfMelvien() : base(QuestLog.Adoulin, "THE_CURIOUS_CASE_OF_MELVIEN") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Mastan).Called().OnTrigger(q =>
                {
                    bool tccomNeedKI =
                        q.Player.HasKeyItem(KeyItem.MelviensTurn) && !q.Player.HasKeyItem(KeyItem.MelviensDeath);

                    if (q.Quest.Status == QuestStatus.Accepted && tccomNeedKI)
                    {
                        // Progresses Quest: 'The Curious Case of Melvien'
                        return QuestReply.Event(184);
                    }

                    return null;
                });

            // the finish, keyed on the csid and option alone as the script's is
            b.AnyState()
                .OnEventFinish(Zone.WesternAdoulin, 184, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.GiveKeyItem(KeyItem.MelviensDeath);
                    }
                });
        }
    }
}
