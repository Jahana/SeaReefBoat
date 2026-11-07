// Divine Might's era module, a line-for-line port of modules/era/lua/quests/outlands/divine_might.lua (quest rework B3b
// Outlands, group 4; P4). It restores the clear full moon night, 00:00 to 03:00, at the Qu'Hau Spring for Divine Might and
// Divine Might (Repeat) (the June 7, 2016 version update widened the window to 18:00 through 06:00 and dropped the
// weather; https://forum.square-enix.com/ffxi/threads/50760-Jun.-7-2016-(JST)-Version-Update).
// Module:new('era_quest_divine_might', xi.pre(xi.expansion.ROV)); its onServerStart override replaces section 2's Ro'Maeve
// QuHau_Spring onTrade in both quests with a copy that reads the hour 0 to 2 and the zone's weather NONE (0) or SUNSHINE
// (1). The weather check reads the zone (player:getZone():getWeather()): a scholar's storm does not close the spring.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Outlands
{
    public sealed class DivineMight : QuestModule
    {
        // xi.moonCycle.FULL_MOON (scripts/enum/moon_cycle.lua)
        const int FullMoon = 6;

        public DivineMight() : base("era_quest_divine_might", "modules/era/lua/quests/outlands/divine_might.lua") { }

        // xi.pre(xi.expansion.ROV)
        public override bool Condition(IQuestSettings settings) => Pre(settings, QuestExpansion.Rov);

        protected override void Build(QuestModuleBuilder m)
        {
            m.Quest(QuestKey.Outlands.DivineMight)
                // Copy of the base handler. The spring accepts the trade on a clear full moon night.
                .State(2).Npc(Zone.Romaeve, Npc.Romaeve.QuHauSpring).ReplaceOnTrade(q =>
                {
                    int vanaHour = q.Player.VanadielHour;
                    // Player.ZoneWeather (int) = player:getZone():getWeather() (the Lua module calls getZone()
                    // without true; the C# engine's getZone ignores the argument)
                    int weather = q.Player.ZoneWeather; // Module change

                    if (
                        // Player.VanadielMoonCycle (int) = getVanadielMoonCycle()
                        (q.Player.VanadielMoonCycle == FullMoon) &&
                        (vanaHour >= 0 && vanaHour < 3) && // Module change
                        (weather == Weather.None || weather == Weather.Sunshine) && // Module change
                        q.Trade.HasExactly(Item.BottleOfIlluminink, Item.SheetOfParchment))
                    {
                        return QuestReply.ProgressEvent(7, Item.SheetOfParchment, Item.BottleOfIlluminink);
                    }
                    return null;
                });

            m.Quest(QuestKey.Outlands.DivineMightRepeat)
                // Copy of the base handler. The window also gates the chunk of light ore trade.
                .State(2).Npc(Zone.Romaeve, Npc.Romaeve.QuHauSpring).ReplaceOnTrade(q =>
                {
                    int vanaHour = q.Player.VanadielHour;
                    // Player.ZoneWeather (int) = player:getZone():getWeather()
                    int weather = q.Player.ZoneWeather; // Module change

                    if (
                        // Player.VanadielMoonCycle (int) = getVanadielMoonCycle()
                        (q.Player.VanadielMoonCycle == FullMoon) &&
                        (vanaHour >= 0 && vanaHour < 3) && // Module change
                        (weather == Weather.None || weather == Weather.Sunshine)) // Module change
                    {
                        if (q.Trade.HasExactly(Item.BottleOfIlluminink, Item.SheetOfParchment))
                        {
                            return QuestReply.ProgressEvent(7, Item.SheetOfParchment, Item.BottleOfIlluminink);
                        }
                        else if (
                            !q.Player.HasKeyItem(KeyItem.MoonlightOre) &&
                            q.Trade.HasExactly(Item.ChunkOfLightOre))
                        {
                            return QuestReply.ProgressEvent(8);
                        }
                    }
                    return null;
                });
        }
    }
}
