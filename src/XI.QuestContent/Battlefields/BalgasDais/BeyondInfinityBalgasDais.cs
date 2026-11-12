// Beyond Infinity (Balga's Dais), a line-for-line port of scripts/battlefields/Balgas_Dais/beyond_infinity.lua (quest
// rework C3, Q3a). Index 20; the shared shape is BeyondInfinityBattlefield's.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.BalgasDais
{
    public sealed class BeyondInfinityBalgasDais : BeyondInfinityBattlefield
    {
        public BeyondInfinityBalgasDais()
            : base("BEYOND_INFINITY_BALGAS_DAIS", Zone.BalgasDais, Battlefield.BeyondInfinityBalgasDais, 20,
                   Npc.BalgasDais.BCEntrance, Npc.BalgasDais.BurningCircle, Text.BalgasDais.SOUL_GEM_REACTS,
                   Text.BalgasDais.LOST_KEYITEM)
        {
        }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Balgas_Dais/beyond_infinity";
    }
}
