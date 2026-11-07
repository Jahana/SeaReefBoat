using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;

namespace XI.Zone
{
    // The menus the client fills from the server: key items marked as read (c2s/0x064), the two Currencies pages
    // (0x10F / 0x115), the map's markers (0x114), a guild NPC's recipe suggestions (0x058), the equipset editor's check
    // (0x052), and the dungeon tablet parameters (0x0D8, not implemented in the C++ either).
    public sealed partial class ZoneHost
    {
        // ---- GP_CLI_COMMAND_SCENARIOITEM ----------------------------------------------------------------------------

        private const int ScenarioFlagsAt = 8, ScenarioTableAt = 74;

        // markSeenKeyItem for every bit the client sent, in the table it names, then SaveKeyItems
        private void OnScenarioItem(MapSession s, CharData c, BasicPacket p)
        {
            int table = BitConverter.ToUInt16(p.Buffer, ScenarioTableAt);
            for (int i = 0; i < 16; i++)
            {
                uint flags = BitConverter.ToUInt32(p.Buffer, ScenarioFlagsAt + i * 4);
                for (int bit = 0; bit < 32; bit++)
                {
                    if ((flags >> bit & 1) == 0) continue;
                    int index = i * 32 + bit;
                    c.KeyItems[table * 128 + 64 + index / 8] |= (byte)(1 << index % 8);
                }
            }
            Exec("UPDATE chars SET keyitems = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.KeyItems), BoundValue.Of(c.Id));
        }

        // ---- GP_SERV_COMMAND_CURRENCIES_1 / _2 ----------------------------------------------------------------------
        // char_points read whole each time the page opens: the column, where it goes in the body and its width

        private static readonly (string Column, int Offset, int Size)[] Currencies1Columns =
        {
            ("sandoria_cp", S2cCurrencies1.OffConquestSandoria, 4), ("bastok_cp", S2cCurrencies1.OffConquestBastok, 4),
            ("windurst_cp", S2cCurrencies1.OffConquestWindurst, 4),
            ("beastman_seal", S2cCurrencies1.OffBeastmansSeals, 2), ("kindred_seal", S2cCurrencies1.OffKindredsSeals, 2),
            ("kindred_crest", S2cCurrencies1.OffKindredsCrests, 2), ("high_kindred_crest", S2cCurrencies1.OffHighKindredsCrests, 2),
            ("sacred_kindred_crest", S2cCurrencies1.OffSacredKindredsCrests, 2), ("ancient_beastcoin", S2cCurrencies1.OffAncientBeastcoins, 2),
            ("valor_point", S2cCurrencies1.OffValorPoints, 2), ("scyld", S2cCurrencies1.OffScylds, 2),
            ("guild_fishing", S2cCurrencies1.OffGuildFishing, 4), ("guild_woodworking", S2cCurrencies1.OffGuildWoodworking, 4),
            ("guild_smithing", S2cCurrencies1.OffGuildSmithing, 4), ("guild_goldsmithing", S2cCurrencies1.OffGuildGoldsmithing, 4),
            ("guild_weaving", S2cCurrencies1.OffGuildWeaving, 4), ("guild_leathercraft", S2cCurrencies1.OffGuildLeathercraft, 4),
            ("guild_bonecraft", S2cCurrencies1.OffGuildBonecraft, 4), ("guild_alchemy", S2cCurrencies1.OffGuildAlchemy, 4),
            ("guild_cooking", S2cCurrencies1.OffGuildCooking, 4), ("cinder", S2cCurrencies1.OffCinders, 4),
            ("fire_fewell", S2cCurrencies1.OffSynergyFire, 1), ("ice_fewell", S2cCurrencies1.OffSynergyIce, 1),
            ("wind_fewell", S2cCurrencies1.OffSynergyWind, 1), ("earth_fewell", S2cCurrencies1.OffSynergyEarth, 1),
            ("lightning_fewell", S2cCurrencies1.OffSynergyLightning, 1), ("water_fewell", S2cCurrencies1.OffSynergyWater, 1),
            ("light_fewell", S2cCurrencies1.OffSynergyLight, 1), ("dark_fewell", S2cCurrencies1.OffSynergyDark, 1),
            ("ballista_point", S2cCurrencies1.OffBallistaPoints, 4), ("fellow_point", S2cCurrencies1.OffFellowPoints, 4),
            ("chocobuck_sandoria", S2cCurrencies1.OffChocobucksSandoria, 2), ("chocobuck_bastok", S2cCurrencies1.OffChocobucksBastok, 2),
            ("chocobuck_windurst", S2cCurrencies1.OffChocobucksWindurst, 2),
            ("research_mark", S2cCurrencies1.OffResearchMarks, 4), ("tunnel_worm", S2cCurrencies1.OffWizenedTunnel, 1),
            ("morion_worm", S2cCurrencies1.OffWizenedMorion, 1), ("phantom_worm", S2cCurrencies1.OffWizenedPhantom, 1),
            ("moblin_marble", S2cCurrencies1.OffMoblinMarbles, 4), ("infamy", S2cCurrencies1.OffInfamy, 2),
            ("prestige", S2cCurrencies1.OffPrestige, 2), ("legion_point", S2cCurrencies1.OffLegionPoints, 4),
            ("spark_of_eminence", S2cCurrencies1.OffSparksOfEminence, 4), ("shining_star", S2cCurrencies1.OffShiningStars, 4),
            ("imperial_standing", S2cCurrencies1.OffImperialStanding, 4), ("leujaoam_assault_point", S2cCurrencies1.OffAssaultLSanctum, 4),
            ("mamool_assault_point", S2cCurrencies1.OffAssaultMjtg, 4), ("lebros_assault_point", S2cCurrencies1.OffAssaultLCavern, 4),
            ("periqia_assault_point", S2cCurrencies1.OffAssaultPeriqia, 4), ("ilrusi_assault_point", S2cCurrencies1.OffAssaultIlrusi, 4),
            ("nyzul_isle_assault_point", S2cCurrencies1.OffTokens, 4), ("zeni_point", S2cCurrencies1.OffZeni, 4),
            ("jetton", S2cCurrencies1.OffJettons, 4), ("therion_ichor", S2cCurrencies1.OffTherionIchor, 4),
            ("allied_notes", S2cCurrencies1.OffAlliedNotes, 4), ("aman_vouchers", S2cCurrencies1.OffCopperAmanVouchers, 2),
            ("login_points", S2cCurrencies1.OffLoginPoints, 2), ("cruor", S2cCurrencies1.OffCruor, 4),
            ("resistance_credit", S2cCurrencies1.OffResistanceCredits, 4), ("dominion_note", S2cCurrencies1.OffDominionNotes, 4),
            ("fifth_echelon_trophy", S2cCurrencies1.OffEchelon5th, 1), ("fourth_echelon_trophy", S2cCurrencies1.OffEchelon4th, 1),
            ("third_echelon_trophy", S2cCurrencies1.OffEchelon3rd, 1), ("second_echelon_trophy", S2cCurrencies1.OffEchelon2nd, 1),
            ("first_echelon_trophy", S2cCurrencies1.OffEchelon1st, 1), ("cave_points", S2cCurrencies1.OffCaveConservation, 1),
            ("id_tags", S2cCurrencies1.OffImperialArmyIdTags, 1), ("op_credits", S2cCurrencies1.OffOpCredits, 1),
            ("voidstones", S2cCurrencies1.OffVoidstones, 4), ("kupofried_corundums", S2cCurrencies1.OffKupofriedsCorundums, 4),
            ("pheromone_sacks", S2cCurrencies1.OffMoblinPheromoneSacks, 1),
            ("rems_ch1", S2cCurrencies1.OffRemsTale1, 1), ("rems_ch2", S2cCurrencies1.OffRemsTale2, 1),
            ("rems_ch3", S2cCurrencies1.OffRemsTale3, 1), ("rems_ch4", S2cCurrencies1.OffRemsTale4, 1),
            ("rems_ch5", S2cCurrencies1.OffRemsTale5, 1), ("rems_ch6", S2cCurrencies1.OffRemsTale6, 1),
            ("rems_ch7", S2cCurrencies1.OffRemsTale7, 1), ("rems_ch8", S2cCurrencies1.OffRemsTale8, 1),
            ("rems_ch9", S2cCurrencies1.OffRemsTale9, 1), ("rems_ch10", S2cCurrencies1.OffRemsTale10, 1),
            ("reclamation_marks", S2cCurrencies1.OffReclamationMarks, 2), ("unity_accolades", S2cCurrencies1.OffUnityAccolades, 4),
            ("fire_crystals", S2cCurrencies1.OffFireCrystals, 2), ("ice_crystals", S2cCurrencies1.OffIceCrystals, 2),
            ("wind_crystals", S2cCurrencies1.OffWindCrystals, 2), ("earth_crystals", S2cCurrencies1.OffEarthCrystals, 2),
            ("lightning_crystals", S2cCurrencies1.OffLightningCrystals, 2), ("water_crystals", S2cCurrencies1.OffWaterCrystals, 2),
            ("light_crystals", S2cCurrencies1.OffLightCrystals, 2), ("dark_crystals", S2cCurrencies1.OffDarkCrystals, 2),
            ("deeds", S2cCurrencies1.OffDeeds, 2),
        };

        // the five 9-bit plan counts packed into one uint64
        private static readonly string[] Currencies1Plans = { "bloodshed_plans", "umbrage_plans", "ritualistic_plans", "tutelary_plans", "primacy_plans" };

        private static readonly (string Column, int Offset, int Size)[] Currencies2Columns = BuildCurrencies2Columns();

        private static (string, int, int)[] BuildCurrencies2Columns()
        {
            var cols = new List<(string, int, int)>
            {
                ("bayld", S2cCurrencies2.OffBayld, 4), ("kinetic_unit", S2cCurrencies2.OffKineticUnits, 2),
                ("imprimaturs", S2cCurrencies2.OffCoalitionImprimaturs, 1), ("mystical_canteen", S2cCurrencies2.OffMysticalCanteens, 1),
                ("obsidian_fragment", S2cCurrencies2.OffObsidianFragments, 4), ("lebondopt_wing", S2cCurrencies2.OffLebondoptWings, 2),
                ("pulchridopt_wing", S2cCurrencies2.OffPulchridoptWings, 2), ("mweya_plasm", S2cCurrencies2.OffMweyaPlasmCorpuscles, 4),
            };
            // ghastly .. duskorb: three bytes each (the stone, +1, +2) from ghastly_stones at 20
            string[] stones = { "ghastly", "verdigris", "wailing", "snowslit", "snowtip", "snowdim", "snoworb", "leafslit", "leaftip",
                                "leafdim", "leaforb", "duskslit", "dusktip", "duskdim", "duskorb" };
            for (int i = 0; i < stones.Length; i++)
            {
                cols.Add((stones[i] + "_stone", S2cCurrencies2.OffGhastlyStones0 + i * 3, 1));
                cols.Add((stones[i] + "_stone_1", S2cCurrencies2.OffGhastlyStones0 + i * 3 + 1, 1));
                cols.Add((stones[i] + "_stone_2", S2cCurrencies2.OffGhastlyStones0 + i * 3 + 2, 1));
            }
            cols.AddRange(new (string, int, int)[]
            {
                ("pellucid_stone", S2cCurrencies2.OffPellucidStones, 1), ("fern_stone", S2cCurrencies2.OffFernStones, 1),
                ("taupe_stone", S2cCurrencies2.OffTaupeStones, 1),
                ("escha_beads", S2cCurrencies2.OffEschaBeads, 2), ("escha_silt", S2cCurrencies2.OffEschaSilt, 4),
                ("potpourri", S2cCurrencies2.OffPotpourri, 4), ("current_hallmarks", S2cCurrencies2.OffHallmarks, 4),
                ("total_hallmarks", S2cCurrencies2.OffTotalHallmarks, 4), ("gallantry", S2cCurrencies2.OffBadgesOfGallantry, 4),
                ("crafter_points", S2cCurrencies2.OffCrafterPoints, 4),
            });
            // fire_crystal_set .. darkness_spheres_set: one byte each from 96
            string[] sets = { "fire_crystal_set", "ice_crystal_set", "wind_crystal_set", "earth_crystal_set", "lightning_crystal_set",
                              "water_crystal_set", "light_crystal_set", "dark_crystal_set", "mc_s_sr01_set", "mc_s_sr02_set", "mc_s_sr03_set",
                              "liquefaction_spheres_set", "induration_spheres_set", "detonation_spheres_set", "scission_spheres_set",
                              "impaction_spheres_set", "reverberation_spheres_set", "transfixion_spheres_set", "compression_spheres_set",
                              "fusion_spheres_set", "distortion_spheres_set", "fragmentation_spheres_set", "gravitation_spheres_set",
                              "light_spheres_set", "darkness_spheres_set" };
            for (int i = 0; i < sets.Length; i++) cols.Add((sets[i], S2cCurrencies2.OffFireCrystalsSet + i, 1));
            cols.AddRange(new (string, int, int)[]
            {
                ("silver_aman_voucher", S2cCurrencies2.OffSilverAmanVouchers, 4), ("domain_points", S2cCurrencies2.OffDomainPoints, 4),
                ("domain_points_daily", S2cCurrencies2.OffDomainPointsEarnedToday, 4), ("mog_segments", S2cCurrencies2.OffMogSegments, 4),
                ("gallimaufry", S2cCurrencies2.OffGallimaufry, 4), ("is_accolades", S2cCurrencies2.OffIsAccolades, 2),
                ("temenos_units", S2cCurrencies2.OffTemenosUnits, 4), ("apollyon_units", S2cCurrencies2.OffApollyonUnits, 4),
            });
            return cols.ToArray();
        }

        private static void WriteColumns(PacketWriter w, ResultSet r, (string Column, int Offset, int Size)[] columns)
        {
            foreach (var (column, offset, size) in columns)
            {
                long v = r.Get<long>(column);
                switch (size)
                {
                    case 1: w.U8(4 + offset, (byte)v); break;
                    case 2: w.U16(4 + offset, (ushort)v); break;
                    default: w.U32(4 + offset, (uint)v); break;
                }
            }
        }

        private BasicPacket Currencies1(CharData c)
        {
            var w = new PacketWriter(0x113, 4 + S2cCurrencies1.SizeOf);
            var columns = new List<string>();
            foreach (var col in Currencies1Columns) columns.Add(col.Column);
            columns.Add("daily_tally");
            columns.AddRange(Currencies1Plans);
            var r = Exec($"SELECT {string.Join(", ", columns)} FROM char_points WHERE charid = ?", BoundValue.Of(c.Id));
            if (r != null && r.Next())
            {
                WriteColumns(w, r, Currencies1Columns);
                long dailyTally = r.Get<long>("daily_tally");
                w.U16(4 + S2cCurrencies1.OffDailyTally, dailyTally == -1 ? 0 : (ushort)dailyTally);
                ulong plans = 0;
                for (int i = 0; i < Currencies1Plans.Length; i++)
                    plans |= (r.Get<ulong>(Currencies1Plans[i]) & 0x1FF) << i * 9;
                w.U32(4 + S2cCurrencies1.OffPlans, (uint)plans);
                w.U32(4 + S2cCurrencies1.OffPlans + 4, (uint)(plans >> 32));
            }
            w.U32(4 + S2cCurrencies1.OffTraverserStones, AvailableTraverserStones(c));
            return w.Packet;
        }

        private BasicPacket Currencies2(CharData c)
        {
            var w = new PacketWriter(0x118, 4 + S2cCurrencies2.SizeOf);
            var columns = new List<string>();
            foreach (var col in Currencies2Columns) columns.Add(col.Column);
            var r = Exec($"SELECT {string.Join(", ", columns)} FROM char_points WHERE charid = ?", BoundValue.Of(c.Id));
            if (r != null && r.Next()) WriteColumns(w, r, Currencies2Columns);
            return w.Packet;
        }

        // the Abyssite of Celerity key items: each takes 4 hours off the 20 a stone takes
        private static readonly int[] TraverserStoneReductionKeyItems = { 1385, 1386, 1387 };

        // charutils::getAvailableTraverserStones: none until a quest sets the epoch
        private uint AvailableTraverserStones(CharData c)
        {
            long start = 0;
            uint claimed = 0;
            var r = Exec("SELECT UNIX_TIMESTAMP(traverser_start) AS start, traverser_claimed FROM char_unlocks WHERE charid = ? LIMIT 1",
                         BoundValue.Of(c.Id));
            if (r != null && r.Next())
            {
                start = r.IsNull("start") ? 0 : r.Get<long>("start");
                claimed = r.Get<uint>("traverser_claimed");
            }
            if (start == 0) return 0;

            long waitHours = 20;
            foreach (int ki in TraverserStoneReductionKeyItems)
                if (c.HasKeyItem(ki)) waitHours -= 4;
            long elapsedHours = Math.Max(0, UnixNow() - start) / 3600;
            return (uint)(elapsedHours / waitHours - claimed);
        }

        // ---- GP_CLI_COMMAND_RECIPE ----------------------------------------------------------------------------------
        // A guild NPC's recipe suggestions (GP_SERV_COMMAND_RECIPE): a random recipe near the level the client sent, a
        // page of 16 recipes in a rank (a 17th only says another page exists), or one recipe at an offset of that list.

        private const int RecipeRequestRandom = 1, RecipeRequestList = 2, RecipeRequestDetail = 3;
        private const ushort RecipeDetail1 = 1, RecipeList = 2, RecipeDetail2 = 3;

        private const string RecipeFrom =
            "FROM (SELECT *, CASE ? WHEN 1 THEN Wood WHEN 2 THEN Smith WHEN 3 THEN Gold WHEN 4 THEN Cloth " +
            "WHEN 5 THEN Leather WHEN 6 THEN Bone WHEN 7 THEN Alchemy WHEN 8 THEN Cook END AS craftLevel " +
            "FROM synth_recipes WHERE Desynth = 0) AS recipes " +
            "INNER JOIN item_basic ON Result = item_basic.itemid " +
            "WHERE craftLevel >= GREATEST(Wood, Smith, Gold, Cloth, Leather, Bone, Alchemy, Cook) AND craftLevel BETWEEN ? AND ? ";
        private const string RecipeDetailColumns =
            "SELECT KeyItem, Wood, Smith, Gold, Cloth, Leather, Bone, Alchemy, Cook, Crystal, Result, " +
            "Ingredient1, Ingredient2, Ingredient3, Ingredient4, Ingredient5, Ingredient6, Ingredient7, Ingredient8 ";

        private static PacketValidator ValidateRecipe(CharData c, BasicPacket p)
        {
            int mode = U16(p, 10);
            return Pv(c)
                .Range("skill", U16(p, 4), 1, 8)
                .Range("level", U16(p, 6), 0, 110)
                .Range("Mode", mode, RecipeRequestRandom, RecipeRequestDetail)
                .Custom(v =>
                {
                    if (mode == RecipeRequestList) v.Range("Param4", U16(p, 18), 0, 11).MultipleOf("Param1", U16(p, 12), 16);
                    else if (mode == RecipeRequestDetail) v.Range("Param4", U16(p, 18), 0, 11);
                });
        }

        private void OnRecipe(MapSession s, CharData c, BasicPacket p)
        {
            ushort skill = U16(p, 4), level = U16(p, 6);
            switch (U16(p, 10))
            {
                case RecipeRequestRandom:
                    Push(s, Recipe(RecipeDetail1, skill, level, 0, 0));
                    break;
                case RecipeRequestList:
                    Push(s, Recipe(RecipeList, skill, level, (byte)U16(p, 18), U16(p, 12)));
                    break;
                case RecipeRequestDetail:
                    Push(s, Recipe(RecipeDetail2, skill, level, (byte)U16(p, 18), U16(p, 16)));
                    break;
            }
        }

        private BasicPacket Recipe(ushort type, ushort skill, ushort level, byte rank, ushort offset)
        {
            var w = new PacketWriter(0x031, 52);
            if (type == RecipeDetail1)
            {
                // no rank from the client: the ten levels from the one it sent
                byte min = (byte)(level / 10 * 10), max = (byte)((level / 10 + 1) * 10);
                var r = Exec(RecipeDetailColumns + RecipeFrom + "ORDER BY RAND() LIMIT 1", BoundValue.Of(skill), BoundValue.Of(min), BoundValue.Of(max));
                if (r != null && r.Next()) RecipeDetails(w, r, skill);
            }
            else
            {
                byte min = (byte)(rank * 10), max = (byte)((rank + 1) * 10);
                if (level < max) max = (byte)level;
                if (type == RecipeDetail2)
                {
                    var r = Exec(RecipeDetailColumns + RecipeFrom + "ORDER BY craftLevel, item_basic.name LIMIT ?, 1",
                                 BoundValue.Of(skill), BoundValue.Of(min), BoundValue.Of(max), BoundValue.Of(offset));
                    if (r != null && r.Next()) RecipeDetails(w, r, skill);
                }
                else
                {
                    var r = Exec("SELECT Result " + RecipeFrom + "ORDER BY craftLevel, item_basic.name LIMIT ?, 17",
                                 BoundValue.Of(skill), BoundValue.Of(min), BoundValue.Of(max), BoundValue.Of(offset));
                    int idx = 0;
                    while (r != null && r.Next())
                    {
                        // the 17th is not listed: it tells the client there is another page
                        if (idx == 16)
                        {
                            w.U16(50, r.Get<ushort>("Result"));
                            break;
                        }
                        w.U16(16 + idx * 2, r.Get<ushort>("Result"));
                        idx++;
                    }
                }
            }
            w.U16(48, type);
            return w.Packet;
        }

        // processRecipeDetails: up to three other crafts the recipe needs, and the ingredients as item + count in
        // item id order
        private static void RecipeDetails(PacketWriter w, ResultSet r, ushort skill)
        {
            int sub = 0;
            for (uint i = 1; i < 9; i++)
            {
                if (i == skill || sub >= 3 || r.Get<ushort>(i) == 0) continue;
                w.U16(6 + sub * 2, (int)i);
                sub++;
            }
            w.U16(4, r.Get<ushort>("Result"));
            w.U16(12, r.Get<ushort>("Crystal"));
            w.U16(14, r.Get<ushort>("KeyItem"));

            var ingredients = new SortedDictionary<ushort, ushort>();
            for (uint i = 0; i < 8; i++)
            {
                ushort item = r.Get<ushort>(11 + i);
                if (item == 0) continue;
                ingredients[item] = (ushort)(ingredients.GetValueOrDefault(item) + 1);
            }
            int idx = 0;
            foreach (var (item, count) in ingredients)
            {
                w.U16(16 + idx * 2, item);
                w.U16(32 + idx * 2, count);
                idx++;
            }
        }

        // ---- GP_CLI_COMMAND_EQUIPSET_CHECK --------------------------------------------------------------------------
        // The equipset editor asks what the set looks like with one item changed: the item goes in (taking out what it
        // displaces) or the slot is disabled, and GP_SERV_COMMAND_EQUIPSET_VALID sends the 16 slots back. The set's
        // other items are not checked against the char's containers (the equip itself does that).

        private const int EquipsetChangeAt = 8, EquipsetItemsAt = 12, EquipsetSlots = 16;

        private static PacketValidator ValidateEquipsetCheck(CharData c, BasicPacket p) => Pv(c)
            .BlockedBy(BlockedState.InEvent)
            .Range("EquipKind", p.Buffer[4], 0, 15)
            .IsValidContainer("ItemChange.Category", (uint)(p.Buffer[EquipsetChangeAt] >> 2));

        private void OnEquipsetCheck(MapSession s, CharData c, BasicPacket p)
        {
            var items = loader_.Stats?.Items;
            if (items == null)
            {
                log_($"zone: equipset check from {c.Name} needs the item tables");
                return;
            }
            Push(s, EquipsetValid(c, p, items));
        }

        private static BasicPacket EquipsetValid(CharData c, BasicPacket p, ItemData items)
        {
            var w = new PacketWriter(0x116, 72);
            var set = new EquipmentInfo[EquipsetSlots];
            var bag = new byte[EquipsetSlots];
            var bagIndex = new byte[EquipsetSlots];
            var disabled = new bool[EquipsetSlots];

            int slot = p.Buffer[4];
            byte flags = p.Buffer[EquipsetChangeAt];
            bool newDisabled = (flags & 2) != 0;
            byte newBag = (byte)(flags >> 2), newBagIndex = p.Buffer[EquipsetChangeAt + 1];
            ushort newItemId = U16(p, EquipsetChangeAt + 2);

            for (int i = 0; i < EquipsetSlots; i++)
            {
                int at = EquipsetItemsAt + i * 4;
                set[i] = items.Equipment(U16(p, at + 2));
                bag[i] = (byte)(p.Buffer[at] >> 2);
                bagIndex[i] = p.Buffer[at + 1];
                disabled[i] = (p.Buffer[at] & 2) != 0;
            }

            var held = c.FindItem((Container)newBag, newBagIndex);
            var newItem = held == null ? null : items.Equipment(held.ItemId);
            // a bad packet: the reply goes out empty
            if (newItem != null && ((newItem.EquipSlots & 1 << slot) == 0 || newItemId != held.ItemId)) return w.Packet;

            if (newItem != null && !newDisabled)
            {
                for (int i = 0; i < EquipsetSlots; i++)
                {
                    if ((newItem.RemoveSlots & 1 << i) != 0) set[i] = null;
                    if (set[i] != null && (set[i].RemoveSlots & newItem.EquipSlots) != 0) set[i] = null;
                }

                var main = set[CharStats.SlotMain]?.Weapon;
                var sub = set[CharStats.SlotSub]?.Weapon;
                var ranged = set[CharStats.SlotRanged]?.Weapon;
                var ammo = set[CharStats.SlotAmmo]?.Weapon;
                if (newItem.Weapon is WeaponInfo weapon)
                {
                    if (weapon.HandToHand) set[CharStats.SlotSub] = null;
                    if (weapon.TwoHanded && (sub != null && sub.Skill != Skill.None || set[CharStats.SlotSub]?.IsShield == true))
                        set[CharStats.SlotSub] = null;
                    if (weapon.Ranged)
                    {
                        if (ammo != null && ammo.Skill != weapon.Skill) set[CharStats.SlotAmmo] = null;
                        if (ranged != null && ranged.Skill != weapon.Skill) set[CharStats.SlotRanged] = null;
                    }
                    // a grip takes out a one-handed main weapon
                    if (weapon.Skill == Skill.None && (newItem.EquipSlots & 0x02) != 0 && main != null && !main.TwoHanded)
                        set[CharStats.SlotMain] = null;
                    // a one-handed main weapon takes out a grip
                    if (slot == CharStats.SlotMain && !weapon.HandToHand && !weapon.TwoHanded && sub != null && sub.Skill == Skill.None)
                        set[CharStats.SlotSub] = null;
                }
                if (newItem.IsShield && main != null && main.TwoHanded) set[CharStats.SlotMain] = null;

                set[slot] = newItem;
                bag[slot] = newBag;
                bagIndex[slot] = newBagIndex;
                disabled[slot] = newDisabled;
            }
            else
            {
                set[slot] = null;
                disabled[slot] = true;
            }

            for (int i = 0; i < EquipsetSlots; i++)
            {
                int at = 4 + i * 4;
                if (set[i] != null && (set[i].EquipSlots & 1 << i) != 0)
                {
                    w.U8(at, 1 | bag[i] << 2);
                    w.U8(at + 1, bagIndex[i]);
                    w.U16(at + 2, set[i].ItemId);
                }
                else if (disabled[i])
                {
                    w.U8(at, 2);
                }
            }
            return w.Packet;
        }

        // ---- GP_CLI_COMMAND_DUNGEON_PARAM ---------------------------------------------------------------------------

        private void OnDungeonParam(MapSession s, CharData c, BasicPacket p) =>
            log_($"zone: GP_CLI_COMMAND_DUNGEON_PARAM: Not implemented. ActIndex: {U16(p, 4)}, Param1: {U16(p, 6)}, " +
                 $"Param2: {p.Buffer[8]}, UniqueNo: {U32(p, 12)}");
    }
}
