// The custom quest workarounds module, a line-for-line port of modules/custom/lua/quest_workarounds.lua (quest rework B3b):
// overrides that make quests completable while a system they need is missing. Module:new('quest_workarounds') (no
// condition: enabled whenever modules/init.txt lists it). It lives in Modules/Custom, not Modules/Custom/<Area>, as its
// Lua file has no area folder; today it patches one quest:
// - Wish Upon a Star (bastok): Bastok zones don't currently produce NONE weather, so SUNSHINE counts too (see
//   https://github.com/LandSandBoat/server/pull/5831). It replaces section 2's Bastok Markets Enu onTrade: the trade is
//   npcUtil.tradeHasExactly (not the quest's tradeMatches), and 334 plays with the weather NONE at any time of day, or
//   SUNSHINE at night or midnight (Lua's `a or b and c` is `a or (b and c)`); 337 otherwise; nothing for another trade.
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Custom
{
    public sealed class QuestWorkarounds : QuestModule
    {
        public QuestWorkarounds() : base("quest_workarounds", "modules/custom/lua/quest_workarounds.lua") { }

        protected override void Build(QuestModuleBuilder m)
        {
            // quest.sections[2][xi.zone.BASTOK_MARKETS]['Enu'].onTrade: Wish Upon a Star's state 2 (accepted)
            m.Quest(QuestKey.Bastok.WishUponAStar)
                .State(2).Npc(Zone.BastokMarkets, Npc.BastokMarkets.Enu).ReplaceOnTrade(q =>
                {
                    bool isNight = q.Player.VanadielTotd == Time.Night || q.Player.VanadielTotd == Time.Midnight;

                    if (q.Trade.HasExactly(Item.FallenStar))
                    {
                        if (q.Player.Weather == Weather.None ||
                            q.Player.Weather == Weather.Sunshine && isNight)
                        {
                            return QuestReply.ProgressEvent(334);
                        }
                        else
                        {
                            return QuestReply.Event(337);
                        }
                    }
                    return null;
                });
        }
    }
}
