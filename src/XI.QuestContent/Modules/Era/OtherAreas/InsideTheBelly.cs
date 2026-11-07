// Inside the Belly's era module, a line-for-line port of modules/era/lua/quests/otherareas/inside_the_belly.lua (quest
// rework B3b, Other Areas group 3; P4). It restores Zaldon's trade list to the era of the newest enabled expansion: the
// quest launched with 18 fish, and version updates from 2009-11-10 onward grew the list; each batch is gated on when the trade option was added, not on when its fish was
// implemented (https://www.playonline.com/pcd/verup/ff11/detail/5053/detail.html, https://wiki.ffo.jp/html/1338.html).
// Module:new('era_quest_inside_the_belly', xi.pre(WOTG) or xi.pre(ABYSSEA) or xi.pre(SOA) or xi.pre(ROV)).
// Sections 2 and 3 (accepted, completed): Zaldon's onTrade wrapped: a trade holding a blocked fish in any slot returns
// nothing (the fish is kept; the Lua comment says Zaldon's default plays, but his default is a trigger's: no reply), else the quest's own trade. While xi.pre(WOTG) Zaldon's onTrigger
// is replaced by the same tiers with the stock hint lists (only original and 2009-batch fish; events 163, 164 and 165
// render a fixed count of names, 5, 2+4, 2+6, and an empty slot prints a blank name).
// The Lua computes each batch's `blocked` (xi.pre) when the module file loads and the trigger's xi.pre(WOTG) at server
// start; both read xi.settings.main, which the C# reads at Build (QuestModuleBuilder.Settings) when the quests register.
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.OtherAreas
{
    public sealed class InsideTheBelly : QuestModule
    {
        public InsideTheBelly() : base("era_quest_inside_the_belly", "modules/era/lua/quests/otherareas/inside_the_belly.lua") { }

        // xi.pre(xi.expansion.WOTG) or xi.pre(xi.expansion.ABYSSEA) or xi.pre(xi.expansion.SOA) or xi.pre(xi.expansion.ROV)
        public override bool Condition(IQuestSettings settings) =>
            Pre(settings, QuestExpansion.Wotg) ||
            Pre(settings, QuestExpansion.Abyssea) ||
            Pre(settings, QuestExpansion.Soa) ||
            Pre(settings, QuestExpansion.Rov);

        // tradeBatches: the expansion whose xi.pre blocks the batch, and its fish
        static readonly (QuestExpansion BlockedBefore, int[] Items)[] TradeBatches =
        {
            // Added 2009-11-10 and 2010-03-23.
            (QuestExpansion.Wotg, new[]
            {
                Item.Bladefish1, Item.GavialFish, Item.VeydalWrasse1, Item.Morinabaligi, Item.Turnabaligi, Item.Kalkanbaligi,
                Item.Pterygotus, Item.Gerrothorax, Item.Pirarucu, Item.Megalodon, Item.Yayinbaligi, Item.Lakerda,
                Item.Kilicbaligi, Item.MonkeOnke1, Item.Ahtapot, Item.ArmoredPisces, Item.MolaMola,
            }),
            // Added 2010-09-09 and 2010-12-07.
            (QuestExpansion.Abyssea, new[]
            {
                Item.GugruTuna1, Item.Istavrit1, Item.GigantOctopus1, Item.ThreeEyedFish1, Item.GigantSquid,
                Item.Rhinochimera1, Item.Grimmonite, Item.TitanicSawfish, Item.Pelazoea, Item.DoradoGar, Item.Crocodilos,
            }),
            // Added 2012-07-24, between Abyssea and SoA.
            (QuestExpansion.Soa, new[]
            {
                Item.Abaia, Item.Matsya,
            }),
            // Added 2015-06-25 and 2015-11-10.
            (QuestExpansion.Rov, new[]
            {
                Item.Soryu, Item.Sekiryu, Item.Hakuryu, Item.FarEastPuffer,
            }),
        };

        // the pre-WOTG csTier
        static readonly int[][] CsTier =
        {
            new[] { 162, Item.GiantCatfish1, Item.DarkBass1, Item.OgreEel1, Item.ZafmlugBass },
            new[] { 163, Item.ZafmlugBass, Item.GiantDonko1, Item.BhefhelMarlin1, Item.JungleCatfish, Item.SilverShark },
            new[] { 164, Item.JungleCatfish, Item.EmperorFish, Item.SilverShark, Item.Takitaro, Item.SeaZombie, Item.GiantChirai },
            new[] { 165, Item.Takitaro, Item.SeaZombie, Item.Titanictus, Item.CaveCherax, Item.Tricorn, Item.RyuguTitan, Item.Lik, Item.Gugrusaurus },
        };

        protected override void Build(QuestModuleBuilder m)
        {
            var blockedFish = new HashSet<int>();
            foreach (var batch in TradeBatches)
            {
                if (m.Pre(batch.BlockedBefore))
                {
                    foreach (int itemId in batch.Items)
                    {
                        blockedFish.Add(itemId);
                    }
                }
            }

            // Sections 2 and 3 take the fish trades (quest accepted and quest completed).
            foreach (int sectionIdx in new[] { 2, 3 })
            {
                var zaldon = m.Quest(QuestKey.OtherAreas.InsideTheBelly)
                    .State(sectionIdx).Npc(Zone.Selbina, Npc.Selbina.Zaldon)
                    // A blocked fish returns nothing. Zaldon plays his default dialogue and the fish is kept.
                    .WrapOnTrade((q, baseOnTrade) =>
                    {
                        for (int itemSlot = 0; itemSlot <= q.Trade.SlotCount - 1; itemSlot++)
                        {
                            if (blockedFish.Contains(q.Trade.ItemId(itemSlot)))
                            {
                                return null;
                            }
                        }

                        return baseOnTrade(q);
                    });

                // The stock hint lists name only original and 2009-batch fish.
                // Events 163, 164, and 165 render a fixed count of names (5, 2+4, 2+6). An empty slot prints a blank name.
                if (m.Pre(QuestExpansion.Wotg))
                {
                    zaldon.ReplaceOnTrigger(q =>
                    {
                        int fishingSkill = global::XI.QuestContent.OtherAreas.InsideTheBelly.FishingTotalSkill(q.Player);

                        int tier = global::XI.QuestContent.OtherAreas.InsideTheBelly.TierOf(fishingSkill);

                        return global::XI.QuestContent.OtherAreas.InsideTheBelly.TierEvent(CsTier[tier - 1]);
                    });
                }
            }
        }
    }
}
