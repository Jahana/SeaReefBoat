// Inside the Belly (Log ID 4, Quest ID 26), a line-for-line port of scripts/quests/otherAreas/Inside_the_Belly.lua (quest
// rework B3b, Other Areas group 3). Selbina: Zaldon !pos -11.810 -7.287 -6.742 248 (a script: A Boy's Dream's trade; default 150). Every
// section reads xi.settings.map.FISHING_ENABLE == true (false by default): with fishing off the quest answers nothing.
// Available, The Real Gift completed and fishing total skill >= 30 (xi.crafting.getTotalSkill: getCharSkillLevel / 10,
// floored, plus the FISH mod): Zaldon's 161, whose finish begins the quest. Accepted (skill >= 30) and completed (any
// skill): Zaldon's trigger plays a tier event by the total skill (< 40: 162, < 50: 163, < 75: 164, else 165, each with its
// fish list); a trade of one fish of fishRewards and nothing else (npcUtil.tradeHasExactly, the slots read in order)
// confirms the trade at once and draws math.randomInt(1, 1000) against the fish's reward chances, summed in order
// (chance * 10 each): 166 (0, the item) with the entry kept in a quest local var, else 167. Either finish gives the item
// (math.randomInt(min, max) of it when the entry has min and max, else one), the fish's gil and its title, if any, and
// clears the local vars; while accepted it then completes the quest (no reward) and adds fame +10 San d'Oria and Bastok.
// Kept as the Lua has it: the trade is confirmed before the event (the fish is gone whatever happens next); a full
// inventory loses the item but the gil, the title and the completion still come; the accepted section needs the skill,
// the completed one does not; the sums are doubles (1.3 * 10 and the like), compared as the Lua compares them.
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): the lookup runs each onEventFinish entry
// whose check holds when its turn comes, so the accepted 166 / 167 finish (state 2) completes the quest and then the
// completed one (state 3) runs too. In the Lua its giveReward reads the fishId the first one cleared (0) and raises on
// the missing reward (`reward.gil` on nil), after the reward, the completion and the fame. Here the completed
// section's 166 / 167 do nothing when no fish is pending, so the second finish is a no-op and nothing raises. The
// accepted section's finish is as the Lua has it: a GM-forced 166 / 167 with no fish pending still fails in
// GiveReward (a NullReferenceException where the Lua raises), before the completion.
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class InsideTheBelly : QuestScript
    {
        public InsideTheBelly() : base(QuestLog.OtherAreas, "INSIDE_THE_BELLY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Inside_the_Belly";

        // xi.mod.FISH (scripts/enum/mod.codegen.lua): xi.crafting.craftMod[xi.skill.FISHING]
        const int ModFish = 127;

        // one entry of a fish's items: chance in percent (the Lua's, a double), the item, and min / max when the Lua has them
        private sealed record FishItem(double Chance, int ItemId, int? Min = null, int? Max = null);

        // a fish's reward: its gil, its title (0: none) and its items, in the Lua's order
        private sealed record FishReward(int Gil, int Title, FishItem[] Items);

        // data from http://wiki.ffxiclopedia.org/wiki/Inside_the_Belly (the Lua's fishRewards)
        private static readonly IReadOnlyDictionary<int, FishReward> FishRewards = new Dictionary<int, FishReward>
        {
            // Lua :16 -- Grimmonite
            [Item.Grimmonite] = new FishReward(350, 0, new[]
            {
                new FishItem(5, Item.GoldRing), // guessing 5%. Wiki unknown
                new FishItem(5, Item.MythrilRing), // guessing 5%. Wiki unknown
                new FishItem(5, Item.SilverRing), // guessing 5%. Wiki unknown
            }),
            // Lua :27
            [Item.RyuguTitan] = new FishReward(800, 0, new[]
            {
                new FishItem(1.3, Item.MercurialSword),
            }),
            // Lua :36
            [Item.GiantDonko1] = new FishReward(96, 0, new[]
            {
                new FishItem(4.7, Item.BrokenHalcyonFishingRod),
            }),
            // Lua :45
            [Item.JungleCatfish] = new FishReward(300, 0, new[]
            {
                new FishItem(3, Item.BrokenHumeFishingRod),
            }),
            // Lua :54
            [Item.GiantChirai] = new FishReward(550, 0, new[]
            {
                new FishItem(1.2, Item.SpoolOfTwinthread),
            }),
            // Lua :63
            [Item.CaveCherax] = new FishReward(800, 0, new[]
            {
                new FishItem(26.2, Item.DwarfPugil),
            }),
            // Lua :72
            [Item.ArmoredPisces] = new FishReward(475, 0, new[]
            {
                new FishItem(0.4, Item.StolidBreastplate),
            }),
            // Lua :81
            [Item.Tricorn] = new FishReward(810, 0, new[]
            {
                new FishItem(4, Item.ChunkOfDarksteelOre), // guessing 4%. Wiki unknown
            }),
            // Lua :90
            [Item.ZafmlugBass] = new FishReward(15, 0, new[]
            {
                new FishItem(1.4, Item.BlueRock),
            }),
            // Lua :99
            [Item.MonkeOnke1] = new FishReward(150, 0, new[]
            {
                new FishItem(10, Item.PinchOfPoisonDust, 1, 6), // guessing 10%. Wiki unknown
            }),
            // Lua :108
            [Item.DarkBass1] = new FishReward(10, 0, new[]
            {
                new FishItem(4.6, Item.GreenRock),
            }),
            // Lua :117
            [Item.SilverShark] = new FishReward(250, Title.AceAngler, new[]
            {
                new FishItem(1.4, Item.Trident),
            }),
            // Lua :127
            [Item.EmperorFish] = new FishReward(300, 0, new[]
            {
                new FishItem(1, Item.CuirHighboots), // guessing 1%. Wiki says 0%
            }),
            // Lua :136
            [Item.Takitaro] = new FishReward(350, 0, new[]
            {
                new FishItem(2.4, Item.PhilosophersStone),
            }),
            // Lua :145
            [Item.Bladefish1] = new FishReward(200, 0, new[]
            {
                new FishItem(11.7, Item.RobberRig),
            }),
            // Lua :154
            [Item.GigantSquid] = new FishReward(300, 0, new[]
            {
                new FishItem(2.5, Item.FlameShield), // guessing 2.5%. Wiki unknown
            }),
            // Lua :163
            [Item.SeaZombie] = new FishReward(350, 0, new[]
            {
                new FishItem(26.1, Item.DrillCalamary),
            }),
            // Lua :172
            [Item.Titanictus] = new FishReward(350, Title.LuShangLikeFisherKing, new[]
            {
                new FishItem(1.3, Item.AncientSword),
                new FishItem(5, Item.Seashell), // guessing 5%. Wiki unknown
            }),
            // Lua :183
            [Item.GavialFish] = new FishReward(250, 0, new[]
            {
                new FishItem(4.9, Item.DroneEarring),
            }),
            // Lua :192
            [Item.ThreeEyedFish1] = new FishReward(250, 0, new[]
            {
                new FishItem(10, Item.PinchOfParalysisDust, 1, 10), // guessing 10%. Wiki unknown
            }),
            // Lua :201
            [Item.BhefhelMarlin1] = new FishReward(150, 0, new[]
            {
                new FishItem(14.3, Item.BrigandsChart),
                new FishItem(4.4, Item.PiratesChart),
            }),
            // Lua :211
            [Item.GugruTuna1] = new FishReward(50, 0, new[]
            {
                new FishItem(2.5, Item.TinyTathlum),
            }),
            // Lua :220
            [Item.OgreEel1] = new FishReward(16, Title.CordonBleuFisher, new[]
            {
                new FishItem(2.5, Item.TurquoiseRing),
            }),
            // Lua :230
            [Item.TitanicSawfish] = new FishReward(810, 0, new[]
            {
                new FishItem(0.7, Item.Aizenkunitoshi),
            }),
            // Lua :239
            [Item.Gugrusaurus] = new FishReward(880, 0, new[]
            {
                new FishItem(0.4, Item.SaberShoot),
            }),
            // Lua :248
            [Item.Lik] = new FishReward(880, 0, new[]
            {
                new FishItem(0.5, Item.SpoolOfOpalSilk),
            }),
            // Lua :257
            [Item.Pterygotus] = new FishReward(390, 0, new[]
            {
                new FishItem(6.7, Item.LapisLazuli),
            }),
            // Lua :266
            [Item.MolaMola] = new FishReward(487, 0, new[]
            {
                new FishItem(1.8, Item.MercurialSpear),
            }),
            // Lua :275
            [Item.Rhinochimera1] = new FishReward(300, 0, new[]
            {
                new FishItem(3.2, Item.SolonTorque),
            }),
            // Lua :284
            [Item.Istavrit1] = new FishReward(50, 0, new[]
            {
                new FishItem(10, Item.PinchOfVenomDust, 1, 6), // guessing 10%. Wiki unknown
            }),
            // Lua :293
            [Item.Turnabaligi] = new FishReward(340, 0, new[]
            {
                new FishItem(1, Item.ChunkOfDarkOre),
                new FishItem(1.4, Item.ChunkOfIceOre),
                new FishItem(1.4, Item.ChunkOfWaterOre),
            }),
            // Lua :304
            [Item.Kalkanbaligi] = new FishReward(390, 0, new[]
            {
                new FishItem(3.3, Item.FlatShield),
            }),
            // Lua :313
            [Item.VeydalWrasse1] = new FishReward(225, 0, new[]
            {
                new FishItem(5, Item.Nebimonite), // guessing 5%. Wiki unknown
                new FishItem(5, Item.Seashell), // guessing 5%. Wiki unknown
            }),
            // Lua :323
            [Item.Lakerda] = new FishReward(51, 0, new[]
            {
                new FishItem(6, Item.Pearl),
                new FishItem(1.9, Item.BlackPearl),
            }),
            // Lua :333
            [Item.Kilicbaligi] = new FishReward(150, 0, new[]
            {
                new FishItem(2.5, Item.RustyGreatsword), // guessing 2.5%. Wiki unknown
            }),
            // Lua :342
            [Item.Ahtapot] = new FishReward(350, 0, new[]
            {
                new FishItem(18.5, Item.MildewyIngot),
                new FishItem(10.2, Item.DecayedIngot),
            }),
            // Lua :352
            [Item.Morinabaligi] = new FishReward(300, 0, new[]
            {
                new FishItem(5, Item.CuirGloves), // guessing 5%. Wiki unknown
            }),
            // Lua :361
            [Item.Yayinbaligi] = new FishReward(50, 0, new[]
            {
                new FishItem(5, Item.TelluricRing), // guessing 5%. Wiki unknown
            }),
            // Lua :370
            [Item.Megalodon] = new FishReward(532, 0, new[]
            {
                new FishItem(3, Item.BrokenMithranFishingRod), // guessing 3%. Wiki unknown
                new FishItem(3, Item.MithranFishingRod), // guessing 3%. Wiki unknown
            }),
            // Lua :380
            [Item.Matsya] = new FishReward(12592, 0, new[]
            {
                new FishItem(1.2, Item.ShapersShawl),
            }),
            // Lua :389
            [Item.Pirarucu] = new FishReward(516, 0, new[]
            {
                new FishItem(5, Item.WyvernSkin), // guessing 5%. Wiki unknown
                new FishItem(2.5, Item.PeisteSkin), // guessing 2.5%. Wiki unknown
            }),
            // Lua :399
            [Item.Gerrothorax] = new FishReward(423, 0, new[]
            {
                new FishItem(1.2, Item.RiskyPatch),
            }),
            // Lua :408
            [Item.GigantOctopus1] = new FishReward(119, 0, new[]
            {
                new FishItem(10, Item.JarOfBlackInk, 1, 6), // guessing 10%. Wiki unknown
            }),
            // Lua :417 -- Abaia
            [Item.Abaia] = new FishReward(690, 0, new[]
            {
                new FishItem(1.5, Item.AuroraBass, 1, 1), // Aurora Bass x3
                new FishItem(7.8, Item.AuroraBass, 2, 2), // Aurora Bass x2
                new FishItem(12.5, Item.AuroraBass, 3, 3), // Aurora Bass x1
                new FishItem(0.7, Item.PlumbBoots),
            }),
            // Lua :429
            [Item.Soryu] = new FishReward(1512, 0, new[]
            {
                new FishItem(46.8, Item.SoryusLiver),
            }),
            // Lua :438
            [Item.Sekiryu] = new FishReward(1512, 0, new[]
            {
                new FishItem(48.1, Item.SekiryusLiver), // guessing 48.1%. Wiki unknown
            }),
            // Lua :447
            [Item.Hakuryu] = new FishReward(1512, 0, new[]
            {
                new FishItem(48.1, Item.HakuryusLiver),
            }),
            // Lua :456
            [Item.GiantCatfish1] = new FishReward(50, Title.CordonBleuFisher, new[]
            {
                new FishItem(6.2, Item.EarthWand),
            }),
            // Lua :466
            [Item.DoradoGar] = new FishReward(568, 0, new[]
            {
                new FishItem(5, Item.GoldIngot, 1, 4), // guessing 5%. Wiki unknown
            }),
            // Lua :475
            [Item.Crocodilos] = new FishReward(1763, 0, new[]
            {
                new FishItem(2.3, Item.PuffinRing),
            }),
            // Lua :484
            [Item.Pelazoea] = new FishReward(360, 0, new[]
            {
                new FishItem(1.8, Item.NoddyRing),
            }),
            // Lua :493
            [Item.FarEastPuffer] = new FishReward(735, 0, new[]
            {
                new FishItem(5, Item.StinkySubligar), // guessing 5%. Wiki unknown
            }),
        };

        // xi.crafting.getTotalSkill(player, xi.skill.FISHING): getRealSkill (getCharSkillLevel / 10, floored) plus the FISH mod
        public static int FishingTotalSkill(IQuestPlayerView player) =>
            player.CharSkillLevel(Skill.Fishing) / 10 + player.Mod(ModFish);

        // csTier: the trigger's event and its fish list, by tier (the era module has its own lists)
        static readonly int[][] CsTier =
        {
            new[] { 162, Item.GiantCatfish1, Item.DarkBass1, Item.OgreEel1, Item.ZafmlugBass },
            new[] { 163, Item.ZafmlugBass, Item.GiantDonko1, Item.BhefhelMarlin1, Item.Bladefish1, Item.SilverShark },
            new[] { 164, Item.JungleCatfish, Item.GavialFish, Item.Pirarucu, Item.EmperorFish, Item.Megalodon, Item.Morinabaligi },
            new[] { 165, Item.Pterygotus, Item.Kalkanbaligi, Item.Takitaro, Item.SeaZombie, Item.Titanictus, Item.Turnabaligi, Item.CaveCherax, Item.Tricorn },
        };

        // the tier of a total skill: < 40: 1, < 50: 2, < 75: 3, else 4 (the era module's trigger has the same edges)
        public static int TierOf(int fishingSkill)
        {
            int tier = 4;

            if (fishingSkill < 40)
            {
                tier = 1;
            }
            else if (fishingSkill < 50)
            {
                tier = 2;
            }
            else if (fishingSkill < 75)
            {
                tier = 3;
            }

            return tier;
        }

        // quest:event(unpack(csTier[tier])): the event, the list its parameters
        public static QuestReply TierEvent(int[] row)
        {
            var args = new QuestArg[row.Length - 1];
            for (int i = 1; i < row.Length; i++) args[i - 1] = row[i];
            return QuestReply.Event(row[0], args);
        }

        static QuestReply TradeFish(IQuestContext q, int fishId)
        {
            q.Quest.SetLocalVar("fishId", fishId);
            q.Quest.SetLocalVar("itemIdx", 0);

            var rewards = FishRewards[fishId].Items;
            int roll = q.Player.RandomInt(1, 1000);
            double sum = 0;

            // NOTE: We confirm the trade now, and not at the end of the cutscene as normal
            //     : because the cutscene gives away whether or not the trade was successful
            //     : or not, and it's possible for players to cheese this trade by force-dc-ing.
            q.Trade.Confirm();

            for (int idx = 1; idx <= rewards.Length; idx++)
            {
                sum = sum + (rewards[idx - 1].Chance * 10);

                if (roll <= sum)
                {
                    q.Quest.SetLocalVar("itemIdx", idx);

                    return QuestReply.Event(166, 0, rewards[idx - 1].ItemId);
                }
            }

            return QuestReply.Event(167);
        }

        // the fishId local var names no fishRewards entry (0 once a finish cleared it)
        static bool NoFishPending(IQuestContext q) => !FishRewards.ContainsKey(q.Quest.LocalVar("fishId"));

        static void GiveReward(IQuestContext q)
        {
            int fishId = q.Quest.LocalVar("fishId");
            int itemIdx = q.Quest.LocalVar("itemIdx");
            // fishRewards[fishId]: nil (null) for a fishId of 0, which fails below where the Lua fails (only a
            // GM-forced finish gets here with no fish; the completed section's finishes return first, see the header)
            FishRewards.TryGetValue(fishId, out var reward);

            if (itemIdx > 0)
            {
                var rewardItem = reward.Items[itemIdx - 1];
                int itemId = rewardItem.ItemId;
                int itemQt = 1;

                if (rewardItem.Min != null && rewardItem.Max != null)
                {
                    itemQt = q.Player.RandomInt(rewardItem.Min.Value, rewardItem.Max.Value);
                }

                q.Player.GiveItems((itemId, itemQt));
            }

            q.Player.GiveCurrency("gil", reward.Gil);
            q.Quest.SetLocalVar("fishId", 0);
            q.Quest.SetLocalVar("itemIdx", 0);

            if (reward.Title != 0)
            {
                q.Player.AddTitle(reward.Title);
            }
        }

        static QuestReply ZaldonOnTrade(IQuestContext q)
        {
            for (int itemSlot = 0; itemSlot <= q.Trade.SlotCount - 1; itemSlot++)
            {
                int itemId = q.Trade.ItemId(itemSlot);

                if (FishRewards.ContainsKey(itemId) &&
                    q.Trade.HasExactly(itemId))
                {
                    return TradeFish(q, itemId);
                }
            }
            return null;
        }

        static QuestReply ZaldonOnTrigger(IQuestContext q)
        {
            int fishingSkill = FishingTotalSkill(q.Player);

            return TierEvent(CsTier[TierOf(fishingSkill) - 1]);
        }

        // Settings.MapFlag(name) (P6): xi.settings.map[name] == true, as the Lua reads it (never Settings.Map(..) == 1: a number 1 is not true in Lua)
        static bool FishingEnabled(IQuestSettings s) => s.MapFlag("FISHING_ENABLE");

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.OtherAreas, "THE_REAL_GIFT") == QuestStatus.Completed &&
                    FishingTotalSkill(q.Player) >= 30 &&
                    FishingEnabled(q.Settings))
                .Npc(Zone.Selbina, Npc.Selbina.Zaldon).OnTrigger(QuestReply.ProgressEvent(161))
                .OnEventFinish(Zone.Selbina, 161, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, when: q =>
                    FishingTotalSkill(q.Player) >= 30 &&
                    FishingEnabled(q.Settings))
                .Npc(Zone.Selbina, Npc.Selbina.Zaldon)
                    .OnTrade(q => ZaldonOnTrade(q))
                    .OnTrigger(q => ZaldonOnTrigger(q))
                .OnEventFinish(Zone.Selbina, 166, q =>
                {
                    GiveReward(q);

                    q.Quest.Complete();
                    q.Player.AddFame(FameArea.Sandoria, 10);
                    q.Player.AddFame(FameArea.Bastok, 10);
                })
                .OnEventFinish(Zone.Selbina, 167, q =>
                {
                    GiveReward(q);

                    q.Quest.Complete();
                    q.Player.AddFame(FameArea.Sandoria, 10);
                    q.Player.AddFame(FameArea.Bastok, 10);
                });

            b.State(QuestStatus.Completed, when: q => FishingEnabled(q.Settings))
                .Npc(Zone.Selbina, Npc.Selbina.Zaldon)
                    .OnTrade(q => ZaldonOnTrade(q))
                    .OnTrigger(q => ZaldonOnTrigger(q))
                .OnEventFinish(Zone.Selbina, 166, q =>
                {
                    // no fish pending (the accepted finish ran first and cleared it): nothing (see the header)
                    if (NoFishPending(q)) return;
                    GiveReward(q);
                })
                .OnEventFinish(Zone.Selbina, 167, q =>
                {
                    if (NoFishPending(q)) return;
                    GiveReward(q);
                });
        }
    }
}
