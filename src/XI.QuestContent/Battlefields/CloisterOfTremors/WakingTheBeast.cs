// Waking the Beast (Cloister of Tremors BCNM), a port of scripts/battlefields/Cloister_of_Tremors/waking_the_beast.lua
// (quest rework C3, Q2). A BattlefieldQuest of the Other Areas quest Waking the Beast on WakingTheBeastBattlefield:
// index 3, entered at EPEntrance and left at EarthProtocrystal; its avatar is Titan_Prime_WTB with Earth_Elemental.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.CloisterOfTremors
{
    public sealed class WakingTheBeast : WakingTheBeastBattlefield
    {
        public WakingTheBeast() : base("WAKING_THE_BEAST_CLOISTER_OF_TREMORS", Zone.CloisterOfTremors,
                                       Battlefield.WakingTheBeastCloisterOfTremors,
                                       3, Npc.CloisterOfTremors.EPEntrance, Npc.CloisterOfTremors.EarthProtocrystal,
                                       "Titan_Prime_WTB", "Earth_Elemental") { }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Cloister_of_Tremors/waking_the_beast";
    }
}
