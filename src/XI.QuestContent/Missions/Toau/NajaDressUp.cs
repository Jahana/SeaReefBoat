// Naja Salaheem's dress-up update shared by Ghosts of the Past (ToAU 16, event 3074) and Guests of the Empire (ToAU 17,
// event 3076): the same table in both Lua files (option 17 differs only by its tenth operand) (quest rework C3 T2). The
// option picks an outfit and sets its bit (utils.mask.setBit(paramOne, pos, true): bit.bor(mask, bit.lshift(1, pos)))
// in the mission's najaDressUp local var, and answers with the outfit's parameters; option 1 is the first update, and
// any other option answers nothing. The var is written back whatever the option. The Lua passes nine operands to
// updateEvent (ten in 17's option 17); the engine takes eight.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    internal static class NajaDressUp
    {
        const string DressVar = "najaDressUp";

        public static void Update(IQuestContext q)
        {
            int paramOne = q.Mission.LocalVar(DressVar);
            int option = q.Option;

            if (option == 1) // Initial, before playing dress-up with Naja.
            {
                q.Player.UpdateEvent(50, 1, 0, 0, 0, 1, 0, 0);
            }
            // First argument disables options. Others do seem to affect whatever Naja is wearing.
            else if (option == 10) // Blue with Gold trimming.
            {
                paramOne = SetBit(paramOne, 1);
                q.Player.UpdateEvent(paramOne, 1, 255, 0, 0, 0, 4095, 0);
            }
            else if (option == 11) // Red with gold embroidery.
            {
                paramOne = SetBit(paramOne, 2);
                q.Player.UpdateEvent(paramOne, 1, 1757, 0, 0, 0, 4095, 1);
            }
            else if (option == 12) // Pale colors with lace.
            {
                paramOne = SetBit(paramOne, 3);
                q.Player.UpdateEvent(paramOne, 1, 10, 0, 1, 1, 0, 0);
            }
            else if (option == 13) // Deep purple armor.
            {
                paramOne = SetBit(paramOne, 4);
                q.Player.UpdateEvent(paramOne, 1, 0, 0, 0, 0, 0, 0);
            }
            else if (option == 14) // Rich crimson!
            {
                paramOne = SetBit(paramOne, 5);
                q.Player.UpdateEvent(paramOne, 1, 0, 0, 0, 1, 32, 1);
            }
            else if (option == 15) // Cloth with a pattern.
            {
                paramOne = SetBit(paramOne, 6);
                q.Player.UpdateEvent(paramOne, 1, 2964, 0, 0, 2185, 3, 0);
            }
            else if (option == 16) // Indigo with gold embroidery.
            {
                paramOne = SetBit(paramOne, 7);
                q.Player.UpdateEvent(paramOne, 1, 1, 22185, 0, 1, 0, 0);
            }
            else if (option == 17) // Georgeous black.
            {
                paramOne = SetBit(paramOne, 8);
                q.Player.UpdateEvent(paramOne, 1, 1320, 0, 0, 1, 1, 1);
            }

            q.Mission.SetLocalVar(DressVar, paramOne);
        }

        // utils.mask.setBit(mask, pos, true): the count taken mod 32 in LuaJIT and in C# alike
        static int SetBit(int mask, int pos) => mask | (1 << pos);
    }
}
