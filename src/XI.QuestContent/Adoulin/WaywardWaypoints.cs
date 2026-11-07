// Wayward Waypoints (Log ID 9, Quest ID 27), ported from the quest branch of
// scripts/zones/Western_Adoulin/npcs/Shipilolo.lua (quest rework B3b Adoulin group 6; call-only: the script calls
// this quest inside its SoA test, after The Old Man and the Harpoon's and Fertile Ground's calls).
// Shipilolo : !pos 84 0 -60 256.
// Accepted, the char var WW_Need_Shipilolo above 0, without the Waypoint Recalibration Kit: 79, whose finish adds the
// kit (player:addKeyItem, no message) and sets WW_Need_Shipilolo 0. Nothing else: no script begins or completes the
// quest or sets WW_Need_Shipilolo.
// Kept as the script has it: the key item with no message; the finish reads no status.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class WaywardWaypoints : QuestScript
    {
        public WaywardWaypoints() : base(QuestLog.Adoulin, "WAYWARD_WAYPOINTS") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Shipilolo).Called().OnTrigger(q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted &&
                        q.Player.CharVar("WW_Need_Shipilolo") > 0 &&
                        !q.Player.HasKeyItem(KeyItem.WaypointRecalibrationKit))
                    {
                        // Progresses Quest: 'Wayward Waypoints'
                        return QuestReply.Event(79);
                    }

                    return null;
                });

            // the finish, keyed on the csid alone as the script's is
            b.AnyState()
                .OnEventFinish(Zone.WesternAdoulin, 79, q =>
                {
                    q.Player.AddKeyItem(KeyItem.WaypointRecalibrationKit);
                    q.Player.SetCharVar("WW_Need_Shipilolo", 0);
                });
        }
    }
}
