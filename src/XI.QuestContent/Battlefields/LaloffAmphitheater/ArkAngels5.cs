// Ark Angels 5 (Galka), a line-for-line port of scripts/battlefields/LaLoff_Amphitheater/ark_angels_5.lua (quest
// rework C3, C6). Index 4, entered at qm1_5, refused to a char holding Shard of Rage; its Ark Angel is
// ARK_ANGEL_GK. A pet's group follows, not spawned at start: the wyverns (ARK_ANGEL_GK + 3 to 5), which
// Ark_Angel_GK.lua reads from the content to call one. The shared shape is ArkAngelsBattlefield's.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.LaloffAmphitheater
{
    public sealed class ArkAngels5 : ArkAngelsBattlefield
    {
        public ArkAngels5()
            : base("ARK_ANGELS_5", Battlefield.ArkAngels5, 4, Npc.LaloffAmphitheater.qm15, KeyItem.ShardOfRage,
                   "ARK_ANGEL_GK", new[]
                   {
                       // Wyvern
                       PetGroup("ARK_ANGEL_GK", 3),
                   })
        {
        }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/LaLoff_Amphitheater/ark_angels_5";
    }
}
