// Ark Angels 4 (Elvaan), a line-for-line port of scripts/battlefields/LaLoff_Amphitheater/ark_angels_4.lua (quest
// rework C3, C6). Index 3, entered at qm1_4, refused to a char holding Shard of Arrogance; its Ark Angel is
// ARK_ANGEL_EV. The shared shape is ArkAngelsBattlefield's.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.LaloffAmphitheater
{
    public sealed class ArkAngels4 : ArkAngelsBattlefield
    {
        public ArkAngels4()
            : base("ARK_ANGELS_4", Battlefield.ArkAngels4, 3, Npc.LaloffAmphitheater.qm14, KeyItem.ShardOfArrogance,
                   "ARK_ANGEL_EV", System.Array.Empty<BattlefieldGroup>())
        {
        }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/LaLoff_Amphitheater/ark_angels_4";
    }
}
