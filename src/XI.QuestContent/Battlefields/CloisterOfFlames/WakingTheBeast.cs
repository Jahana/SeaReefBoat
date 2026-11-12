// Waking the Beast (Cloister of Flames BCNM), a port of scripts/battlefields/Cloister_of_Flames/waking_the_beast.lua
// (quest rework C3, Q2). A BattlefieldQuest of the Other Areas quest Waking the Beast on WakingTheBeastBattlefield:
// index 2, entered at FPEntrance and left at FireProtocrystal; its avatar is Ifrit_Prime_WTB with Fire_Elemental.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.CloisterOfFlames
{
    public sealed class WakingTheBeast : WakingTheBeastBattlefield
    {
        public WakingTheBeast() : base("WAKING_THE_BEAST_CLOISTER_OF_FLAMES", Zone.CloisterOfFlames,
                                       Battlefield.WakingTheBeastCloisterOfFlames,
                                       2, Npc.CloisterOfFlames.FPEntrance, Npc.CloisterOfFlames.FireProtocrystal,
                                       "Ifrit_Prime_WTB", "Fire_Elemental") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Cloister_of_Flames/waking_the_beast";
    }
}
