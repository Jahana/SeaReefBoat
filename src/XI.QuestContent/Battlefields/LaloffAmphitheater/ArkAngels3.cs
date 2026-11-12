// Ark Angels 3 (Mithra), a line-for-line port of scripts/battlefields/LaLoff_Amphitheater/ark_angels_3.lua (quest
// rework C3, C6). Index 2, entered at qm1_3, refused to a char holding Shard of Envy; its Ark Angel is
// ARK_ANGEL_MR. Two pets' groups follow, not spawned at start: the tigers (ARK_ANGEL_MR + 3 to 5) and the mandragoras
// (+ 6 to 8), which Ark_Angel_MR.lua reads from the content to call one. The shared shape is ArkAngelsBattlefield's.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.LaloffAmphitheater
{
    public sealed class ArkAngels3 : ArkAngelsBattlefield
    {
        public ArkAngels3()
            : base("ARK_ANGELS_3", Battlefield.ArkAngels3, 2, Npc.LaloffAmphitheater.qm13, KeyItem.ShardOfEnvy,
                   "ARK_ANGEL_MR", new[]
                   {
                       // Tiger Pets
                       PetGroup("ARK_ANGEL_MR", 3),
                       // Mandragora Pets
                       PetGroup("ARK_ANGEL_MR", 6),
                   })
        {
        }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/LaLoff_Amphitheater/ark_angels_3";
    }
}
