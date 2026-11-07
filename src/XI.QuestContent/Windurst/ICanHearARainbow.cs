// I Can Hear a Rainbow (Log ID 2, Quest ID 75), a line-for-line port of scripts/quests/windurst/SMN_I_Can_Hear_a_Rainbow.lua
// (quest rework B3b). House of the Hero (_6n2) : !pos -26 -13 260 239; qm3 (La Theine Plateau). At the advanced-job level
// with a Carbuncle's Ruby in the inventory, the House of the Hero's 384 begins it (385, once per zone, while Prog 0). At
// Prog 0, a zone-in to one of 25 outdoor zones with the ruby in the inventory stores the zone's weather (Sunshine as
// None) in the local var Weather; when that weather is None or elemental (fire, ice, wind, earth, thunder, water) and
// its element's bit (None is bit 0) is not yet set in the Light var, the zone's event plays. Its update counts the
// Light bits (six: Prog 1) and sends (0, 1, weather, count); its finish sets the stored weather's element bit. At Prog 1
// the ruby traded alone to the La Theine qm3 plays 124: completed (fame 20, the summoner's job gesture, Rainbow
// Weaver), the ruby taken, SMN unlocked, the Carbuncle spell, two messages, and the La Theine rainbow's local var
// setRainbow set. Kept as the Lua file has it: the update sets Prog 1 before the seventh light's bit (the finish sets
// it); the finish sets a bit for any event of that csid in that zone at Prog 0 (the stored Weather may be stale, 0
// before any zone-in: bit 0); the zone-in's ruby check is the inventory only, the offer's too.
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class ICanHearARainbow : QuestScript
    {
        // xi.inventoryLocation.INVENTORY (scripts/enum/inventory_location.lua)
        const int InventoryLocationInventory = 0;
        // xi.element (scripts/enum/element.codegen.lua)
        const int ElementNone = 0;
        const int ElementFire = 1;
        const int ElementIce = 2;
        const int ElementWind = 3;
        const int ElementEarth = 4;
        const int ElementThunder = 5;
        const int ElementWater = 6;
        const int ElementLight = 7;
        const int ElementDark = 8;

        // validWeatherTable: NONE, HOT_SPELL, HEAT_WAVE, RAIN, SQUALL, DUST_STORM, SAND_STORM, WIND, GALES, SNOW,
        // BLIZZARDS, THUNDER, THUNDERSTORMS
        static readonly HashSet<int> ValidWeatherTable = new HashSet<int> { 0, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

        // zoneEventTable: the zone-in event of each zone, in the Lua's order
        static readonly (ushort Zone, int Csid)[] ZoneEventTable =
        {
            (Zone.WestRonfaure, 51),
            (Zone.EastRonfaure, 21),
            (Zone.NorthGustaberg, 244),
            (Zone.SouthGustaberg, 901),
            (Zone.WestSarutabaruta, 48),
            (Zone.EastSarutabaruta, 50),
            (Zone.LaTheinePlateau, 123),
            (Zone.KonschtatHighlands, 104),
            (Zone.TahrongiCanyon, 35),
            (Zone.ValkurmDunes, 3),
            (Zone.BuburimuPeninsula, 3),
            (Zone.JugnerForest, 15),
            (Zone.BatalliaDowns, 901),
            (Zone.PashhowMarshlands, 13),
            (Zone.RolanberryFields, 2),
            (Zone.MeriphataudMountains, 31),
            (Zone.SauromugueChampaign, 3),
            (Zone.BeaucedineGlacier, 114),
            (Zone.Xarcabard, 9),
            (Zone.YuhtungaJungle, 11),
            (Zone.YhoatorJungle, 2),
            (Zone.TheSanctuaryOfZitah, 2),
            (Zone.EasternAltepaDesert, 2),
            (Zone.WesternAltepaDesert, 2),
            (Zone.CapeTeriggan, 2),
        };

        // zoneEventTable[zoneId][1]
        static int ZoneEvent(ushort zoneId)
        {
            foreach (var entry in ZoneEventTable)
            {
                if (entry.Zone == zoneId)
                {
                    return entry.Csid;
                }
            }

            throw new KeyNotFoundException($"no zone-in event for zone {zoneId}");
        }

        public ICanHearARainbow() : base(QuestLog.Windurst, "I_CAN_HEAR_A_RAINBOW") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/SMN_I_Can_Hear_a_Rainbow";

        // xi.data.element.getWeatherElement (scripts/data/element_tables.lua): the element whose single or double
        // weather it is, FIRE to DARK in order, else NONE
        static int GetWeatherElement(int weather)
        {
            switch (weather)
            {
                case 4: case 5: return ElementFire;       // HOT_SPELL, HEAT_WAVE
                case 12: case 13: return ElementIce;      // SNOW, BLIZZARDS
                case 10: case 11: return ElementWind;     // WIND, GALES
                case 8: case 9: return ElementEarth;      // DUST_STORM, SAND_STORM
                case 14: case 15: return ElementThunder;  // THUNDER, THUNDERSTORMS
                case 6: case 7: return ElementWater;      // RAIN, SQUALL
                case 16: case 17: return ElementLight;    // AURORAS, STELLAR_GLARE
                case 18: case 19: return ElementDark;     // GLOOM, DARKNESS
                default: return ElementNone;
            }
        }

        // utils.mask.getBit(mask, pos)
        static bool GetBit(int mask, int pos) => (mask & (1 << pos)) != 0;

        static QuestReply HandleZoneIn(IQuestContext q)
        {
            if (q.Player.HasItem(Item.CarbunclesRuby, InventoryLocationInventory))
            {
                // player:getZone(true):getWeather(): the zone's weather (Player.Weather is the char's)
                int weather = q.Player.ZoneWeather;

                // Exception: Handle Sunshine weather as weather ID 0 (NONE) for the time being.
                if (weather == Weather.Sunshine)
                {
                    weather = Weather.None;
                }

                // Store weather in a local var just in case weather changes in this few seconds.
                q.Player.SetLocalVar("Weather", weather);

                // Fetch weather element if it's a valid element.
                if (ValidWeatherTable.Contains(weather))
                {
                    int lightBitMask = q.Quest.Var("Light");
                    int element = GetWeatherElement(weather);
                    if (!GetBit(lightBitMask, element))
                    {
                        // zoneEventTable[zone:getID()][1]: a zone-in returns the csid
                        return QuestReply.Event(ZoneEvent(q.Zone));
                    }
                }
            }

            return null;
        }

        static void HandleEventUpdate(IQuestContext q)
        {
            int weather = q.Player.LocalVar("Weather");
            int lightBitMask = q.Quest.Var("Light");
            int lightCounter = 0;

            for (int bit = 0; bit <= 6; bit++)
            {
                if (GetBit(lightBitMask, bit))
                {
                    lightCounter = lightCounter + 1;
                }
            }

            // Note: You will only count 6 by triggering the 7th (last) light.
            // The client expects a 6 in event update when triggering the last light.
            if (lightCounter == 6)
            {
                q.Quest.SetStep(1);
            }

            q.Player.UpdateEvent(0, 1, weather, lightCounter);
        }

        static void HandleEventFinish(IQuestContext q)
        {
            int weather = q.Player.LocalVar("Weather");
            int element = GetWeatherElement(weather);
            // utils.mask.setBit(quest:getVar(player, 'Light'), element, true)
            int lightBitMask = q.Quest.Var("Light") | (1 << element);

            q.Quest.SetVar("Light", lightBitMask);
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Windurst,
                KeyItem = KeyItem.JobGestureSummoner,
                Title = Title.RainbowWeaver,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainLevel >= q.Settings.MainNumber("ADVANCED_JOB_LEVEL") &&
                    q.Player.HasItem(Item.CarbunclesRuby, InventoryLocationInventory))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls._6n2).OnTrigger(q => QuestReply.ProgressEvent(384, 0, Item.CarbunclesRuby))
                .OnEventFinish(Zone.WindurstWalls, 384, q => q.Quest.Begin());

            // the Lua's 25 zone tables, each { onZoneIn = handleZoneIn, onEventUpdate = { [csid] = handleEventUpdate },
            // onEventFinish = { [csid] = handleEventFinish } }, in the Lua's order
            QuestStateBuilder lights = b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 0);
            lights.Npc(Zone.WindurstWalls, Npc.WindurstWalls._6n2).OnTrigger(QuestReply.Event(385, 0, Item.CarbunclesRuby).OncePerZone());
            foreach (var entry in ZoneEventTable)
            {
                lights.OnZoneIn(entry.Zone, HandleZoneIn)
                    .OnEventUpdate(entry.Zone, entry.Csid, HandleEventUpdate)
                    .OnEventFinish(entry.Zone, entry.Csid, HandleEventFinish);
            }

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 1)
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.qm3).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.CarbunclesRuby))
                    {
                        return QuestReply.ProgressEvent(124);
                    }

                    return null;
                })
                .OnEventFinish(Zone.LaTheinePlateau, 124, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();

                        // Unlock SMN job.
                        q.Player.UnlockJob(Job.Smn);
                        q.Player.AddSpell(Spell.Carbuncle);
                        q.Text.Special(Text.LaTheinePlateau.UNLOCK_SUMMONER);
                        q.Text.Special(Text.LaTheinePlateau.UNLOCK_CARBUNCLE);

                        // Set rainbow to appear: GetNPCByID(laTheineID.npc.RAINBOW):setLocalVar('setRainbow', 1) (the
                        // Lua's `if rainbow then` is true whenever the NPC exists)
                        q.Player.SetNpcLocalVar("RAINBOW", "setRainbow", 1);
                    }
                });
        }
    }
}
