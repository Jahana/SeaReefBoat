// Ark Angels 2 (Tarutaru), a line-for-line port of scripts/battlefields/LaLoff_Amphitheater/ark_angels_2.lua (quest
// rework C3, C6). Index 1, entered at qm1_2, refused to a char holding Shard of Cowardice; its Ark Angel is
// ARK_ANGEL_TT. The shared shape is ArkAngelsBattlefield's.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.LaloffAmphitheater
{
    public sealed class ArkAngels2 : ArkAngelsBattlefield
    {
        public ArkAngels2()
            : base("ARK_ANGELS_2", Battlefield.ArkAngels2, 1, Npc.LaloffAmphitheater.qm12, KeyItem.ShardOfCowardice,
                   "ARK_ANGEL_TT", System.Array.Empty<BattlefieldGroup>())
        {
        }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/LaLoff_Amphitheater/ark_angels_2";
    }
}
