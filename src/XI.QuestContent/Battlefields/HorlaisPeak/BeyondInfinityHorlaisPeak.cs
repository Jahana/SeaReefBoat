// Beyond Infinity (Horlais Peak), a line-for-line port of scripts/battlefields/Horlais_Peak/beyond_infinity.lua (quest
// rework C3, Q3a). Index 20; the shared shape is BeyondInfinityBattlefield's.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.HorlaisPeak
{
    public sealed class BeyondInfinityHorlaisPeak : BeyondInfinityBattlefield
    {
        public BeyondInfinityHorlaisPeak()
            : base("BEYOND_INFINITY_HORLAIS_PEAK", Zone.HorlaisPeak, Battlefield.BeyondInfinityHorlaisPeak, 20,
                   Npc.HorlaisPeak.BCEntrance, Npc.HorlaisPeak.BurningCircle, Text.HorlaisPeak.SOUL_GEM_REACTS,
                   Text.HorlaisPeak.LOST_KEYITEM)
        {
        }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Horlais_Peak/beyond_infinity";
    }
}
