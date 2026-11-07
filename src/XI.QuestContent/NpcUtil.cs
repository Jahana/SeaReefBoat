// Shared ports of scripts/globals/npc_util.lua helpers that quest ports call (quest rework carry-quests-1). Each is a
// line-for-line port, kept as the Lua has it.
using XI.Quests.Authoring;

namespace XI.QuestContent
{
    public static class NpcUtil
    {
        // npcUtil.pickNewPosition(npcID, positionTable, allowCurrentPosition): a row of positionTable ({ x, y, z }
        // each) for the NPC to move to. npc is GetNPCByID(npcID), which every caller has as the handler's own NPC
        // (null: no NPC, so no row matches). Kept as the Lua has it: math.randomInt(1, tableSize) is drawn once before
        // the loop counts the table (tableSize 0: randomInt(1, 0), which the engine answers 1 without drawing); the
        // current row is the last one whose floored x, y and z equal the NPC's (row 1 when none does); without
        // allowCurrentPosition it draws randomInt(1, tableSize) until the row differs; with it, the first draw
        // stands (row 1)
        public static double[] PickNewPosition(IQuestContext q, IQuestNpc npc, double[][] positionTable,
            bool allowCurrentPosition = false)
        {
            int positionIndex = 1; // Default to position one in the table if it can't be found.
            int tableSize = 0;
            int newPosition = q.Player.RandomInt(1, tableSize);

            for (int i = 1; i <= positionTable.Length; i++) // Looking for the current position
            {
                var v = positionTable[i - 1];
                if (!allowCurrentPosition)
                {
                    // Finding by comparing the NPC's coords
                    if (npc != null &&
                        System.Math.Floor(v[0]) == System.Math.Floor(npc.XPos) &&
                        System.Math.Floor(v[1]) == System.Math.Floor(npc.YPos) &&
                        System.Math.Floor(v[2]) == System.Math.Floor(npc.ZPos))
                    {
                        positionIndex = i; // Found where the NPC is!
                    }
                }

                tableSize = tableSize + 1; // Counting the array size
            }

            if (!allowCurrentPosition)
            {
                // Pick a new pos that isn't the current
                do
                {
                    newPosition = q.Player.RandomInt(1, tableSize);
                }
                while (newPosition == positionIndex);
            }

            return positionTable[newPosition - 1];
        }
    }
}
