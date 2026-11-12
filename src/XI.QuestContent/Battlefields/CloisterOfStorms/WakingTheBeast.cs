// Waking the Beast (Cloister of Storms BCNM), a port of scripts/battlefields/Cloister_of_Storms/waking_the_beast.lua
// (quest rework C3, Q2). A BattlefieldQuest of the Other Areas quest Waking the Beast on WakingTheBeastBattlefield:
// index 3, entered at LPEntrance and left at LightningProtocrystal;
// its avatar is Ramuh_Prime_WTB with Thunder_Elemental.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.CloisterOfStorms
{
    public sealed class WakingTheBeast : WakingTheBeastBattlefield
    {
        public WakingTheBeast() : base("WAKING_THE_BEAST_CLOISTER_OF_STORMS", Zone.CloisterOfStorms,
                                       Battlefield.WakingTheBeastCloisterOfStorms,
                                       3, Npc.CloisterOfStorms.LPEntrance, Npc.CloisterOfStorms.LightningProtocrystal,
                                       "Ramuh_Prime_WTB", "Thunder_Elemental") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Cloister_of_Storms/waking_the_beast";
    }
}
