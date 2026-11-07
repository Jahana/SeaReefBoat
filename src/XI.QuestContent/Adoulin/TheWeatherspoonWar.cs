// The Weatherspoon War (Log ID 9, Quest ID 141), ported from the quest branch of
// scripts/zones/Western_Adoulin/npcs/Flapano.lua (quest rework B3b Adoulin group 8; call-only: the script calls this
// quest first in his talk, before Exotic Delicacies and the shop). Flapano : !pos 70 0 -13 256 (no default).
// Talk, accepted with Weatherspoon_War_Status 6: 191. Nothing else: 191 has no finish, and no script begins or
// completes the quest or sets the var.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class TheWeatherspoonWar : QuestScript
    {
        public TheWeatherspoonWar() : base(QuestLog.Adoulin, "THE_WEATHERSPOON_WAR") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Flapano).Called().OnTrigger(q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted && q.Player.CharVar("Weatherspoon_War_Status") == 6)
                    {
                        return QuestReply.Event(191);
                    }

                    return null;
                });
        }
    }
}
