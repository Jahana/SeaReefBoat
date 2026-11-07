// Enveloped in Darkness' era module, a line-for-line port of modules/era/lua/quests/sandoria/enveloped_in_darkness.lua
// (quest rework B3b, San d'Oria Task 9): it restores the JST midnight wait for the boots and the crawler blood buried
// at the digging spot in the Crawlers' Nest (the June 17, 2014 version update shortened it to about 30 seconds).
// Module:new('era_quest_enveloped_in_darkness', xi.pre(xi.expansion.SOA)); its onServerStart override replaces section
// 2's Crawlers' Nest onEventFinish 4 with a copy of the quest's own whose Time is JstMidnight(), not now + 30.
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Sandoria
{
    public sealed class EnvelopedInDarkness : QuestModule
    {
        public EnvelopedInDarkness() : base("era_quest_enveloped_in_darkness", "modules/era/lua/quests/sandoria/enveloped_in_darkness.lua") { }

        // xi.pre(xi.expansion.SOA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Soa);

        protected override void Build(QuestModuleBuilder m)
        {
            // quest.sections[2][xi.zone.CRAWLERS_NEST].onEventFinish[4]: the quest's state 2 (accepted)
            m.Quest(QuestKey.Sandoria.EnvelopedInDarkness)
                .State(2).ReplaceOnEventFinish(Zone.CrawlersNest, 4, q =>
                {
                    // Copy of the base handler with the 30 second wait extended to JST midnight.
                    if (q.Option == 1)
                    {
                        // set the purification time
                        q.Quest.SetVar("Time", q.Player.JstMidnight); // Module change: the boots are purified at JST midnight.

                        // delete the key items
                        q.Player.DelKeyItem(KeyItem.CrawlerBlood);
                        q.Player.DelKeyItem(KeyItem.OldBoots);

                        // the message when accepting to bury the boots and the blood
                        q.Text.Special(Text.CrawlersNest.YOU_BURY_THE, KeyItem.OldBoots, KeyItem.CrawlerBlood);
                    }
                });
        }
    }
}
