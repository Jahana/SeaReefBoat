using System;
using XI.Persistence;
using XI.Quests;
using XI.QuestContent;
using XI.Quests.Authoring;

// quest rework B3b Windurst group 5 (I Can Hear a Rainbow): the facet members no golden flow can split, read on the live
// context against the Lua calls. A probe quest answers Samia (Bastok Markets) while the player's local var W5 is 1 and
// replies nothing (Samia's own script opens its event: finished before the next talk).
// 1. Player.HasItem(item, location): player:hasItem(item, location): a Carbuncle's Ruby in the mog safe (container 1,
//    put there before the login: no GM tool of the golden flows puts an item outside the inventory) and a Meteorite in
//    the inventory, each read in the inventory (0), the safe (1) and anywhere (HasItem(item)).
// 2. Player.ZoneWeather: player:getZone(true):getWeather(), the zone's weather, while Player.Weather (player:getWeather())
//    is the char's: the zone RAIN, the char under Firestorm (a scholar's storm: HOT_SPELL).
internal static partial class Program
{
    private const int W5Ruby = 1125, W5Meteorite = 582;

    private static void RunWindurstGroup5()
    {
        Console.WriteLine("quest rework B3b Windurst group 5: facets");
        uint id = CharIds[0];
        var quest = new W5Quest();
        zone.TestQuestScripts.Add(quest);
        Lua("InteractionGlobal.reload(true)");
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0, (W5Meteorite, 1u));
        Sql(db, "REPLACE INTO char_inventory(charid,location,slot,itemId,quantity) VALUES (?,1,1,?,1)",
            BoundValue.Of(id), BoundValue.Of((ushort)W5Ruby));
        Login();
        string weatherBefore = Lua("report(tostring(player:getZone(true):getWeather()))");
        try
        {
            // 1. the items
            string lua = Lua($"report(string.format('%s/%s/%s/%s/%s/%s', tostring(player:hasItem({W5Ruby}, 0)), tostring(player:hasItem({W5Ruby}, 1)), " +
                             $"tostring(player:hasItem({W5Ruby})), tostring(player:hasItem({W5Meteorite}, 0)), tostring(player:hasItem({W5Meteorite}, 1)), " +
                             $"tostring(player:hasItem({W5Meteorite}))))");
            // 2. the weathers: the zone RAIN, the char's storm HOT_SPELL
            Lua("player:setWeather(6) player:addStatusEffect(xi.effect.FIRESTORM, { origin = player, power = 1, duration = 600 })");
            string luaWeather = Lua("report(string.format('%d/%d', player:getZone(true):getWeather(), player:getWeather()))");
            Lua("player:setLocalVar('W5', 1)");
            TalkTo("Samia");
            FinishIfEvent();
            Lua("player:setLocalVar('W5', 0)");
            Check("windurst g5 facets: HasItem(item, location) is Lua's player:hasItem(item, location) (ruby in the safe, meteorite in the inventory)",
                  lua, W5Quest.Items);
            Check("  ... the ruby in the safe only, the meteorite in the inventory only; HasItem(item) anywhere", "false/true/true/true/false/true", W5Quest.Items);
            Check("windurst g5 facets: ZoneWeather is player:getZone(true):getWeather() and Weather player:getWeather() (the zone RAIN, the char under Firestorm)",
                  luaWeather, W5Quest.Weathers);
            Check("  ... the zone's RAIN, the char's HOT_SPELL", "6/4", W5Quest.Weathers);
        }
        finally
        {
            Lua($"player:delStatusEffect(xi.effect.FIRESTORM) player:setWeather({weatherBefore})");
            Logout();
            Cleanup(db, id);
            zone.TestQuestScripts.Clear();
            Lua("InteractionGlobal.reload(true)");
        }
    }

    private sealed class W5Quest : QuestScript
    {
        public static string Items, Weathers;

        public W5Quest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q => q.Player.LocalVar("W5") == 1)
                .Npc(Zone.BastokMarkets, "Samia").OnTrigger(q =>
                {
                    string B(bool v) => v ? "true" : "false";
                    Items = $"{B(q.Player.HasItem(W5Ruby, 0))}/{B(q.Player.HasItem(W5Ruby, 1))}/{B(q.Player.HasItem(W5Ruby))}/" +
                            $"{B(q.Player.HasItem(W5Meteorite, 0))}/{B(q.Player.HasItem(W5Meteorite, 1))}/{B(q.Player.HasItem(W5Meteorite))}";
                    Weathers = $"{q.Player.ZoneWeather}/{q.Player.Weather}";
                    return null;
                });
        }
    }
}
