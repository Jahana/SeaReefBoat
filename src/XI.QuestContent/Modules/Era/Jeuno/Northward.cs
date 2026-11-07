// Northward's era module, a line-for-line port of modules/era/lua/quests/jeuno/northward.lua (quest rework B3b Jeuno,
// group 13): it removes the exp and the gil from the quest's reward (both were added to it in 2013).
// Module:new('era_quest_northward', xi.pre(xi.expansion.SOA)); its onServerStart override replaces quest.reward with the
// Map of Castle Zvahl and Envoy to the North alone.
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Jeuno
{
    public sealed class Northward : QuestModule
    {
        public Northward() : base("era_quest_northward", "modules/era/lua/quests/jeuno/northward.lua") { }

        // xi.pre(xi.expansion.SOA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Soa);

        protected override void Build(QuestModuleBuilder m)
        {
            // quest.reward = { keyItem = xi.keyItem.MAP_OF_CASTLE_ZVAHL, title = xi.title.ENVOY_TO_THE_NORTH }
            m.Quest(QuestKey.Jeuno.Northward)
                .Reward(new QuestReward
                {
                    KeyItem = KeyItem.MapOfCastleZvahl,
                    Title = Title.EnvoyToTheNorth,
                });
        }
    }
}
