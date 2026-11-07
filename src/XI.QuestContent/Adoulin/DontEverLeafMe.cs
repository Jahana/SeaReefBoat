// Don't Ever Leaf Me (Log ID 9, Quest ID 70), ported from the quest branches of
// scripts/zones/Western_Adoulin/npcs/Dewalt.lua and Barenngo.lua (quest rework B3b Adoulin group 7; call-only: each
// script calls this quest where its quest branch was). Dewalt !pos -23 0 28 256 (default 5017; Flavors of Our Lives'
// containers hold him too), Barenngo !pos -101 3 14 256 (default 5018).
// Accepted: Dewalt with DELM_Dewalt_Branch below 1: 5013 (its finish: the var 1), below 2: 5014; Barenngo with
// DELM_Barenngo_Branch below 1: 5015 (its finish: the var 1), below 2: 5016. Nothing else: no script begins or
// completes the quest or sets either var to 2.
// Kept as the scripts have it: the finishes read no status; at 2 or more (a GM's) nothing.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class DontEverLeafMe : QuestScript
    {
        public DontEverLeafMe() : base(QuestLog.Adoulin, "DONT_EVER_LEAF_ME") { }

        // each NPC's talk: accepted with its branch var below 1: its progress event; below 2: its hint
        private static QuestReply Talk(IQuestContext q, string branchVar, int progressCsid, int hintCsid)
        {
            QuestStatus delm = q.Quest.Status;

            if (delm == QuestStatus.Accepted && q.Player.CharVar(branchVar) < 1)
            {
                // Progresses Quest: 'Dont Ever Leaf Me'
                return QuestReply.Event(progressCsid);
            }
            else if (delm == QuestStatus.Accepted && q.Player.CharVar(branchVar) < 2)
            {
                // Reminds player of hint for Quest: 'Dont Ever Leaf Me'
                return QuestReply.Event(hintCsid);
            }

            return null;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Dewalt).Called()
                    .OnTrigger(q => Talk(q, "DELM_Dewalt_Branch", 5013, 5014))
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Barenngo).Called()
                    .OnTrigger(q => Talk(q, "DELM_Barenngo_Branch", 5015, 5016));

            // the finishes, keyed on the csid alone as the scripts' are
            b.AnyState()
                .OnEventFinish(Zone.WesternAdoulin, 5013, q =>
                {
                    // Progresses Quest: 'Dont Ever Leaf Me'
                    q.Player.SetCharVar("DELM_Dewalt_Branch", 1);
                })
                .OnEventFinish(Zone.WesternAdoulin, 5015, q =>
                {
                    // Progresses Quest: 'Dont Ever Leaf Me'
                    q.Player.SetCharVar("DELM_Barenngo_Branch", 1);
                });
        }
    }
}
