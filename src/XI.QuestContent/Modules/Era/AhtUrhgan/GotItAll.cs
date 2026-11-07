// Got It All's era module, a line-for-line port of modules/era/lua/quests/ahturhgan/got_it_all.lua (quest rework B3b Aht
// Urhgan, P4; draft: goes to src/XI.QuestContent/Modules/Era/AhtUrhgan/GotItAll.cs). It means to restore the JST midnight
// wait before Tehf Kimasnahya's final event (the July 8, 2014 version update abolished it;
// https://forum.square-enix.com/ffxi/threads/43135-Jul-8-2014-(JST)-Version-Update, https://wiki.ffo.jp/html/5374.html).
// Module:new('era_quest_got_it_all', xi.pre(xi.expansion.SOA)); its onServerStart override replaces section 2's
// Whitegate onEventFinish 527 with a copy of the quest's own that also sets Stage = JstMidnight().
// Kept as the Lua has it: nothing reads Stage (528 wants only the zoning flag), so the wait is not enforced.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.AhtUrhgan
{
    public sealed class GotItAll : QuestModule
    {
        public GotItAll() : base("era_quest_got_it_all", "modules/era/lua/quests/ahturhgan/got_it_all.lua") { }

        // xi.pre(xi.expansion.SOA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Soa);

        protected override void Build(QuestModuleBuilder m)
        {
            // quest.sections[2][xi.zone.AHT_URHGAN_WHITEGATE].onEventFinish[527]: the quest's state 2 (accepted)
            m.Quest(QuestKey.AhtUrhgan.GotItAll)
                .State(2).ReplaceOnEventFinish(Zone.AhtUrhganWhitegate, 527, q =>
                {
                    // Restores the JST Midnight Wait
                    q.Quest.SetStep(7);
                    q.Quest.SetVar("Stage", q.Player.JstMidnight);
                    q.Player.SetNeedToZone(true);
                });
        }
    }
}
