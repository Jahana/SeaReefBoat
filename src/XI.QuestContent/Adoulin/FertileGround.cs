// Fertile Ground (Log ID 9, Quest ID 85), ported from the quest branch of
// scripts/zones/Western_Adoulin/npcs/Shipilolo.lua (quest rework B3b Adoulin group 6; call-only: the script calls
// this quest inside its SoA test, after The Old Man and the Harpoon's call). Shipilolo : !pos 84 0 -60 256.
// Accepted without the Bottle of Fertilizer X: 2850, whose finish adds the bottle (player:addKeyItem, no message: the
// script's TODO). Nothing else: no script begins or completes the quest.
// Kept as the script has it: the key item with no message; the finish reads no status.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class FertileGround : QuestScript
    {
        public FertileGround() : base(QuestLog.Adoulin, "FERTILE_GROUND") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Shipilolo).Called().OnTrigger(q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted && !q.Player.HasKeyItem(KeyItem.BottleOfFertilizerX))
                    {
                        // Progresses Quest: 'Fertile Ground'
                        return QuestReply.Event(2850);
                    }

                    return null;
                });

            // the finish, keyed on the csid alone as the script's is
            b.AnyState()
                .OnEventFinish(Zone.WesternAdoulin, 2850, q =>
                {
                    // Progresses Quest: 'Fertile Ground' TODO: Should this also give the player a message?
                    q.Player.AddKeyItem(KeyItem.BottleOfFertilizerX);
                });
        }
    }
}
