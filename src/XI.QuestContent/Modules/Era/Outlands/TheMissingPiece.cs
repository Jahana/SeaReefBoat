// The Missing Piece's era module, a line-for-line port of modules/era/lua/quests/outlands/the_missing_piece.lua (quest
// rework B3b Outlands, group 5; P4). It restores the JST midnight wait before Charlaimagnat hands over the reward (the
// July 8, 2014 version update shortened it to one minute;
// https://forum.square-enix.com/ffxi/threads/43135-Jul-8-2014-(JST)-Version-Update).
// Module:new('era_quest_the_missing_piece', xi.pre(xi.expansion.SOA)); its onServerStart override wraps section 2's
// Northern San d'Oria onEventFinish 703: the base handler runs, then Wait = JstMidnight().
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Outlands
{
    public sealed class TheMissingPiece : QuestModule
    {
        public TheMissingPiece() : base("era_quest_the_missing_piece", "modules/era/lua/quests/outlands/the_missing_piece.lua") { }

        // xi.pre(xi.expansion.SOA)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Soa);

        protected override void Build(QuestModuleBuilder m)
        {
            m.Quest(QuestKey.Outlands.TheMissingPiece)
                // Charlaimagnat studies the tablet overnight. The reward is handed over at JST midnight.
                .State(2).WrapOnEventFinish(Zone.NorthernSanDoria, 703, (q, baseTablet) =>
                {
                    baseTablet(q);
                    q.Quest.SetVar("Wait", q.Player.JstMidnight); // Module change
                });
        }
    }
}
