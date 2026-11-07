// Port of CCharEntity::SetMoghancement / changeMoghancement (char_entity.cpp) and the MOGHANCEMENT_TYPE ids
// (items/item_furnishing.h): the key item the dominant installed furnishing's element grants, and its mods.

namespace XI.Zone
{
    public static class Moghancement
    {
        // the element ones lower the material loss of a synth with that element's crystal
        public const ushort Fire = 512, Ice = 513, Wind = 514, Earth = 515, Lightning = 516, Water = 517, Light = 518, Dark = 519;
        public const ushort Experience = 520, Gardening = 521, Desynthesis = 522;
        public const ushort Fishing = 523, Woodworking = 524, Smithing = 525, Goldsmithing = 526, Clothcraft = 527,
                            Leathercraft = 528, Bonecraft = 529, Alchemy = 530, Cooking = 531;
        public const ushort Conquest = 532, Region = 533, FishingItem = 534, SandoriaConquest = 535, BastokConquest = 536,
                            WindurstConquest = 537, Money = 538, Campaign = 539, MoneyII = 540, SkillGains = 541, Bounty = 542;
        public const ushort MoglificationFishing = 544, MoglificationWoodworking = 545, MoglificationSmithing = 546,
                            MoglificationGoldsmithing = 547, MoglificationClothcraft = 548, MoglificationLeathercraft = 549,
                            MoglificationBonecraft = 550, MoglificationAlchemy = 551, MoglificationCooking = 552;
        public const ushort MegaFishing = 553, MegaWoodworking = 554, MegaSmithing = 555, MegaGoldsmithing = 556,
                            MegaClothcraft = 557, MegaLeathercraft = 558, MegaBonecraft = 559, MegaAlchemy = 560, MegaCooking = 561;
        public const ushort MoglificationExperienceBoost = 562, MoglificationCapacityBoost = 563;
        public const ushort ResistDeath = 566, ResistSleep = 2848, ResistPoison = 2849, ResistParalysis = 2850,
                            ResistSilence = 2852, ResistPetrification = 2853, ResistVirus = 2854, ResistCurse = 2855;

        // SetMoghancement: the previous one's mods off, the new one's on
        public static void Set(CharData c, ushort id)
        {
            Change(c, c.Moghancement, -1);
            Change(c, id, 1);
            c.Moghancement = id;
        }

        private static void Add(CharData c, Mod mod, int value) => c.ModStat[(int)mod] = (short)(c.ModStat[(int)mod] + value);

        private static void Change(CharData c, ushort id, int m)
        {
            switch (id)
            {
                case Fire: Add(c, Mod.SYNTH_MATERIAL_LOSS_FIRE, 10 * m); break;
                case Ice: Add(c, Mod.SYNTH_MATERIAL_LOSS_ICE, 10 * m); break;
                case Wind: Add(c, Mod.SYNTH_MATERIAL_LOSS_WIND, 10 * m); break;
                case Earth: Add(c, Mod.SYNTH_MATERIAL_LOSS_EARTH, 10 * m); break;
                case Lightning: Add(c, Mod.SYNTH_MATERIAL_LOSS_THUNDER, 10 * m); break;
                case Water: Add(c, Mod.SYNTH_MATERIAL_LOSS_WATER, 10 * m); break;
                case Light: Add(c, Mod.SYNTH_MATERIAL_LOSS_LIGHT, 10 * m); break;
                case Dark: Add(c, Mod.SYNTH_MATERIAL_LOSS_DARK, 10 * m); break;

                case Fishing: Add(c, Mod.FISH, m); break;
                case Woodworking: Add(c, Mod.WOOD, m); break;
                case Smithing: Add(c, Mod.SMITH, m); break;
                case Goldsmithing: Add(c, Mod.GOLDSMITH, m); break;
                case Clothcraft: Add(c, Mod.CLOTH, m); break;
                case Leathercraft: Add(c, Mod.LEATHER, m); break;
                case Bonecraft: Add(c, Mod.BONE, m); break;
                case Alchemy: Add(c, Mod.ALCHEMY, m); break;
                case Cooking: Add(c, Mod.COOK, m); break;

                // TODO in the C++: "makes it slightly easier to reel in your catch"
                case MoglificationFishing: Add(c, Mod.FISH, m); break;
                case MoglificationWoodworking: Add(c, Mod.WOOD, m); Add(c, Mod.SYNTH_MATERIAL_LOSS_WOODWORKING, 5 * m); break;
                case MoglificationSmithing: Add(c, Mod.SMITH, m); Add(c, Mod.SYNTH_MATERIAL_LOSS_SMITHING, 5 * m); break;
                case MoglificationGoldsmithing: Add(c, Mod.GOLDSMITH, m); Add(c, Mod.SYNTH_MATERIAL_LOSS_GOLDSMITHING, 5 * m); break;
                case MoglificationClothcraft: Add(c, Mod.CLOTH, m); Add(c, Mod.SYNTH_MATERIAL_LOSS_CLOTHCRAFT, 5 * m); break;
                case MoglificationLeathercraft: Add(c, Mod.LEATHER, m); Add(c, Mod.SYNTH_MATERIAL_LOSS_LEATHERCRAFT, 5 * m); break;
                case MoglificationBonecraft: Add(c, Mod.BONE, m); Add(c, Mod.SYNTH_MATERIAL_LOSS_BONECRAFT, 5 * m); break;
                case MoglificationAlchemy: Add(c, Mod.ALCHEMY, m); Add(c, Mod.SYNTH_MATERIAL_LOSS_ALCHEMY, 5 * m); break;
                case MoglificationCooking: Add(c, Mod.COOK, m); Add(c, Mod.SYNTH_MATERIAL_LOSS_COOKING, 5 * m); break;

                // the Mega Moglifications state nothing about material loss
                case MegaFishing: Add(c, Mod.FISH, 5 * m); break;
                case MegaWoodworking: Add(c, Mod.WOOD, 5 * m); break;
                case MegaSmithing: Add(c, Mod.SMITH, 5 * m); break;
                case MegaGoldsmithing: Add(c, Mod.GOLDSMITH, 5 * m); break;
                case MegaClothcraft: Add(c, Mod.CLOTH, 5 * m); break;
                case MegaLeathercraft: Add(c, Mod.LEATHER, 5 * m); break;
                case MegaBonecraft: Add(c, Mod.BONE, 5 * m); break;
                case MegaAlchemy: Add(c, Mod.ALCHEMY, 5 * m); break;
                case MegaCooking: Add(c, Mod.COOK, 5 * m); break;

                case Experience: Add(c, Mod.EXPERIENCE_RETAINED, 5 * m); break;
                case Gardening: Add(c, Mod.GARDENING_WILT_BONUS, 36 * m); break;
                case Desynthesis: Add(c, Mod.SYNTH_SUCCESS_RATE_DESYNTHESIS, 4 * m); break;
                case Conquest: Add(c, Mod.CONQUEST_BONUS, 6 * m); break;
                case Region: Add(c, Mod.CONQUEST_REGION_BONUS, 10 * m); break;
                case FishingItem: break; // TODO in the C++: more items found when fishing
                case SandoriaConquest: if (c.Nation == 0) Add(c, Mod.CONQUEST_BONUS, 6 * m); break;
                case BastokConquest: if (c.Nation == 1) Add(c, Mod.CONQUEST_BONUS, 6 * m); break;
                case WindurstConquest: if (c.Nation == 2) Add(c, Mod.CONQUEST_BONUS, 6 * m); break;
                case Money: Add(c, Mod.MOGHANCEMENT_GIL_BONUS_P, 10 * m); break;
                case Campaign: Add(c, Mod.CAMPAIGN_BONUS, 5 * m); break;
                case MoneyII: Add(c, Mod.MOGHANCEMENT_GIL_BONUS_P, 15 * m); break;
                // the C++'s guess: unknown, but strong for a newish item
                case SkillGains: Add(c, Mod.COMBAT_SKILLUP_RATE, 25 * m); Add(c, Mod.MAGIC_SKILLUP_RATE, 25 * m); break;
                case Bounty: Add(c, Mod.EXP_BONUS, 10 * m); Add(c, Mod.CAPACITY_BONUS, 10 * m); break;
                case MoglificationExperienceBoost: Add(c, Mod.EXP_BONUS, 15 * m); break;
                case MoglificationCapacityBoost: Add(c, Mod.CAPACITY_BONUS, 15 * m); break;

                // the resistance values are unknown
                case ResistDeath: Add(c, Mod.DEATHRES, 10 * m); break;
                case ResistSleep: Add(c, Mod.SLEEPRES, 10 * m); break;
                case ResistPoison: Add(c, Mod.POISONRES, 10 * m); break;
                case ResistParalysis: Add(c, Mod.PARALYZERES, 10 * m); break;
                case ResistSilence: Add(c, Mod.SILENCERES, 10 * m); break;
                case ResistPetrification: Add(c, Mod.PETRIFYRES, 10 * m); break;
                case ResistVirus: Add(c, Mod.VIRUSRES, 10 * m); break;
                case ResistCurse: Add(c, Mod.CURSERES, 10 * m); break;
            }
        }
    }
}
