// Waking the Beast (Cloister of Gales BCNM), a port of scripts/battlefields/Cloister_of_Gales/waking_the_beast.lua
// (quest rework C3, Q2). A BattlefieldQuest of the Other Areas quest Waking the Beast on WakingTheBeastBattlefield:
// index 3, entered at WPEntrance and left at WindProtocrystal; its avatar is Garuda_Prime_WTB with Air_Elemental.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.CloisterOfGales
{
    public sealed class WakingTheBeast : WakingTheBeastBattlefield
    {
        public WakingTheBeast() : base("WAKING_THE_BEAST_CLOISTER_OF_GALES", Zone.CloisterOfGales,
                                       Battlefield.WakingTheBeastCloisterOfGales,
                                       3, Npc.CloisterOfGales.WPEntrance, Npc.CloisterOfGales.WindProtocrystal,
                                       "Garuda_Prime_WTB", "Air_Elemental") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Cloister_of_Gales/waking_the_beast";
    }
}
