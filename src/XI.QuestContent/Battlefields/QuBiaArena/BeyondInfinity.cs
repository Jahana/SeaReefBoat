// Beyond Infinity (Qu'Bia Arena), a line-for-line port of scripts/battlefields/QuBia_Arena/beyond_infinity.lua (quest
// rework C3, Q3a). Index 21; the shared shape is BeyondInfinityBattlefield's. Its constant is BEYOND_INFINITY.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.QuBiaArena
{
    public sealed class BeyondInfinity : BeyondInfinityBattlefield
    {
        public BeyondInfinity()
            : base("BEYOND_INFINITY", Zone.QubiaArena, Battlefield.BeyondInfinity, 21,
                   Npc.QubiaArena.BCEntrance, Npc.QubiaArena.BurningCircle, Text.QubiaArena.SOUL_GEM_REACTS,
                   Text.QubiaArena.LOST_KEYITEM)
        {
        }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/QuBia_Arena/beyond_infinity";
    }
}
