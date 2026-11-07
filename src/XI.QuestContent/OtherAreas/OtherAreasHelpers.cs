// Helpers for otherAreas quests, a line-for-line port of scripts/quests/otherAreas/helpers.lua (quest rework B3b, Other
// Areas group 3): xi.otherAreas.helpers.TestMyMettle.moveJar, for Test My Mettle's Jar trigger. The Lua file stays:
// Davoi's Zone.lua requires it and calls moveJar from onGameHour once Davoi_Jar_Move_Time has passed (no quest status
// read there).
// Kept as the Lua has it: npcUtil.pickNewPosition draws math.randomInt(1, tableSize) once before it counts the table
// (tableSize 0: math.randomInt(1, 0), which the engine answers 1 without drawing), finds the jar's current spot by the
// floored coordinates of GetNPCByID(davoiID.npc.JAR) (the same NPC the trigger got), then draws randomInt(1, 10) until the
// spot differs (GetNPCByID(JAR) is the trigger's npc: Davoi has one Jar, 17388022); npcUtil.queueMove moves it after
// 3000 ms with doMove's setPos(x, y, z, 0) (no rotation given: 0, as the traces' 0x00E show).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public static class OtherAreasHelpers
    {
        public static class TestMyMettle
        {
            static readonly double[][] Positions =
            {
                new[] {  185.230, -0.865, -189.750 }, // [0] J-9 Jar near campfire
                new[] {   94.290, -8.450, -163.800 }, // [1] I-8 Jar near lake
                new[] {  275.770,  3.160, -204.120 }, // [2] K-9 Jar near stump
                new[] {  171.750, -3.300, -111.800 }, // [3] J-8 Jar on platform
                new[] {   58.300, -9.780, -132.430 }, // [4] H-8 Jar near oak door
                new[] {   25.080,  0.440, -107.300 }, // [5] H-8 Jar near campfire
                new[] { -106.770,  2.580,   26.060 }, // [6] F-6 Jar north of Hut Flap
                new[] { -128.720,  3.250, -237.600 }, // [7] F-9 Jar near stump
                new[] {  -36.720,  3.300, -218.270 }, // [8] G-9 Jar near well
                new[] {   54.800,  0.720, -202.950 }, // [9] H-9 Jar near campfire
            };

            public static void MoveJar(IQuestContext q)
            {
                // 5 min (offset by onGameHour frequency of ~2.5 min) to 3 hours
                // full equation = ((2.5 * 10) * 6) to ((3 * 60) * 60)
                int randomHourSpan = q.Player.RandomInt(125, 10800);

                q.Player.SetServerVar("Davoi_Jar_Move_Time", q.Player.SystemTime + randomHourSpan);

                // npcUtil.pickNewPosition(davoiID.npc.JAR, positions, false): GetNPCByID(JAR) is the trigger's npc
                var point = NpcUtil.PickNewPosition(q, q.Npc, Positions, false);

                q.Npc.QueueMove(point[0], point[1], point[2]);
            }
        }
    }
}
