// The Sacred Katana's era module, a line-for-line port of modules/era/lua/quests/outlands/the_sacred_katana.lua (quest
// rework B3b Outlands, group 1; P4). It restores the JST midnight wait before Jaucribaix offers Yomi Okuri (the July 8,
// 2014 version update shortened the wait to one minute;
// https://forum.square-enix.com/ffxi/threads/43135-Jul-8-2014-(JST)-Version-Update).
// Module:new('era_quest_the_sacred_katana', xi.pre(xi.expansion.SOA)); its onServerStart override replaces section 2's Norg
// onEventFinish 141 with a copy that sets Yomi Okuri's Timer = JstMidnight(). Yomi Okuri's check is left as it is: it reads
// GetSystemTime() < Timer, so the offer comes at the next JST midnight itself.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Outlands
{
    public sealed class TheSacredKatana : QuestModule
    {
        public TheSacredKatana() : base("era_quest_the_sacred_katana", "modules/era/lua/quests/outlands/the_sacred_katana.lua") { }

        // xi.pre(xi.expansion.SOA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Soa);

        protected override void Build(QuestModuleBuilder m)
        {
            m.Quest(QuestKey.Outlands.TheSacredKatana)
                // Copy of the base handler. Yomi Okuri is offered at JST midnight.
                .State(2).ReplaceOnEventFinish(Zone.Norg, 141, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        q.Player.DelKeyItem(KeyItem.HandfulOfCrystalScales);

                        // Player must zone before being able to flag the next quest
                        q.Player.SetQuestMustZone(QuestLog.Outlands, "YOMI_OKURI");
                        q.Player.SetQuestVar(QuestLog.Outlands, "YOMI_OKURI", "Timer", q.Player.JstMidnight); // Module change
                    }
                });
        }
    }
}
