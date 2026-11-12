// Ark Angels 1 (Hume), a line-for-line port of scripts/battlefields/LaLoff_Amphitheater/ark_angels_1.lua (quest
// rework C3, C6). Index 0, entered at qm1_1, refused to a char holding Shard of Apathy; its Ark Angel is
// ARK_ANGEL_HM. The shared shape is ArkAngelsBattlefield's.
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.LaloffAmphitheater
{
    public sealed class ArkAngels1 : ArkAngelsBattlefield
    {
        public ArkAngels1()
            : base("ARK_ANGELS_1", Battlefield.ArkAngels1, 0, Npc.LaloffAmphitheater.qm11, KeyItem.ShardOfApathy,
                   "ARK_ANGEL_HM", System.Array.Empty<BattlefieldGroup>())
        {
        }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/LaLoff_Amphitheater/ark_angels_1";
    }
}
