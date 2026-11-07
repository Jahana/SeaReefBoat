// Scaredy-Cats (Log ID 9, Quest ID 72), ported from the quest branches of scripts/zones/Western_Adoulin/npcs/Eamonn.lua
// and Bilp.lua (quest rework B3b Adoulin group 7; call-only: each script calls this quest where its quest branch was).
// Eamonn !pos -91 3 2 256, Bilp !pos -91 3 0 256 (no defaults). Its state is the char var Scaredycats_Status.
// Both (each its own reminder and after-dialogue): status below 1 and available: 5031; status 1: accepted 5025, else
// 5024 (whose finish on option 1 sets status 2 and adds the quest); status 2: Eamonn 5027, Bilp 5026; else (status 3
// or more, or below 1 and not available): Eamonn 5030, Bilp 5029.
// Kept as the scripts have it: nothing sets status 1 (the walk-in event with Bilp, Eamonn and Lhe is not implemented),
// so 5024 and the quest are a GM's; nothing sets status 3 or completes the quest; the after-dialogue plays at status 0
// for an accepted or completed quest too; the finish reads no status.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class Scaredycats : QuestScript
    {
        public Scaredycats() : base(QuestLog.Adoulin, "SCAREDYCATS") { }

        // the two NPCs' talk: their reminder at status 2 and their after-dialogue differ
        private static QuestReply Talk(IQuestContext q, int statusTwoCsid, int afterCsid)
        {
            QuestStatus scaredyCats = q.Quest.Status;
            int scaredyCatsStatus = q.Player.CharVar("Scaredycats_Status");

            if (scaredyCatsStatus < 1 && scaredyCats == QuestStatus.Available)
            {
                // Dialogue before seeing the initial walk-in CS with Bilp, Eamonn, and Lhe.
                return QuestReply.Event(5031);
            }
            else if (scaredyCatsStatus == 1)
            {
                if (scaredyCats == QuestStatus.Accepted)
                {
                    // Reminder for Quest: 'Scaredy-Cats', go to Ceizak Battlegrounds
                    return QuestReply.Event(5025);
                }
                else
                {
                    // Starts Quest: 'Scaredy-Cats', after first refusal.
                    return QuestReply.Event(5024);
                }
            }
            else if (scaredyCatsStatus == 2)
            {
                // Reminder for Quest: 'Scaredy-Cats', go to Sih Gates.
                return QuestReply.Event(statusTwoCsid);
            }
            else
            {
                // Dialogue after completeing Quest: 'Scaredy-Cats'
                return QuestReply.Event(afterCsid);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Eamonn).Called().OnTrigger(q => Talk(q, 5027, 5030))
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Bilp).Called().OnTrigger(q => Talk(q, 5026, 5029));

            // both scripts' finish (the same arm), keyed on the csid alone as theirs is
            b.AnyState()
                .OnEventFinish(Zone.WesternAdoulin, 5024, q =>
                {
                    if (q.Option == 1)
                    {
                        // Starts Quest: 'Scaredy-Cats', after first refusal.
                        q.Player.SetCharVar("Scaredycats_Status", 2);
                        q.Quest.Begin();
                    }
                });
        }
    }
}
