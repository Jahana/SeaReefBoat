// Waking the Beast (Cloister of Frost BCNM), a port of scripts/battlefields/Cloister_of_Frost/waking_the_beast.lua
// (quest rework C3, Q2). A BattlefieldQuest of the Other Areas quest Waking the Beast on WakingTheBeastBattlefield:
// index 3, entered at IPEntrance and left at IceProtocrystal; its avatar is Shiva_Prime_WTB with Ice_Elemental.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.CloisterOfFrost
{
    public sealed class WakingTheBeast : WakingTheBeastBattlefield
    {
        public WakingTheBeast() : base("WAKING_THE_BEAST_CLOISTER_OF_FROST", Zone.CloisterOfFrost,
                                       Battlefield.WakingTheBeastCloisterOfFrost,
                                       3, Npc.CloisterOfFrost.IPEntrance, Npc.CloisterOfFrost.IceProtocrystal,
                                       "Shiva_Prime_WTB", "Ice_Elemental") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Cloister_of_Frost/waking_the_beast";
    }
}
