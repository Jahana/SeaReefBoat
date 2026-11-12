// Beyond Infinity (Waughroon Shrine), a line-for-line port of scripts/battlefields/Waughroon_Shrine/beyond_infinity.lua
// (quest rework C3, Q3a). Index 21; the shared shape is BeyondInfinityBattlefield's.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.WaughroonShrine
{
    public sealed class BeyondInfinityWaughroonShrine : BeyondInfinityBattlefield
    {
        public BeyondInfinityWaughroonShrine()
            : base("BEYOND_INFINITY_WAUGHROON_SHRINE", Zone.WaughroonShrine, Battlefield.BeyondInfinityWaughroonShrine,
                   21, Npc.WaughroonShrine.BCEntrance, Npc.WaughroonShrine.BurningCircle,
                   Text.WaughroonShrine.SOUL_GEM_REACTS, Text.WaughroonShrine.LOST_KEYITEM)
        {
        }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Waughroon_Shrine/beyond_infinity";
    }
}
