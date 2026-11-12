// Waking the Beast (Cloister of Tides BCNM), a port of scripts/battlefields/Cloister_of_Tides/waking_the_beast.lua
// (quest rework C3, Q2). A BattlefieldQuest of the Other Areas quest Waking the Beast on WakingTheBeastBattlefield:
// index 2, entered at WPEntrance and left at WaterProtocrystal; its avatar is Leviathan_Prime_WTB with Water_Elemental.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.CloisterOfTides
{
    public sealed class WakingTheBeast : WakingTheBeastBattlefield
    {
        public WakingTheBeast() : base("WAKING_THE_BEAST_CLOISTER_OF_TIDES", Zone.CloisterOfTides,
                                       Battlefield.WakingTheBeastCloisterOfTides,
                                       2, Npc.CloisterOfTides.WPEntrance, Npc.CloisterOfTides.WaterProtocrystal,
                                       "Leviathan_Prime_WTB", "Water_Elemental") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Cloister_of_Tides/waking_the_beast";
    }
}
