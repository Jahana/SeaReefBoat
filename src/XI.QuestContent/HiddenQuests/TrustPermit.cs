// xi.trust.hasPermit(player) (scripts/globals/trust.lua), shared by the trust hidden quests: any of the three nations'
// trust permits.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    internal static class TrustPermit
    {
        public static bool Has(IQuestPlayerView player) =>
            player.HasKeyItem(KeyItem.WindurstTrustPermit) ||
            player.HasKeyItem(KeyItem.BastokTrustPermit) ||
            player.HasKeyItem(KeyItem.SanDoriaTrustPermit);
    }
}
