// Borghertz's Hands, 15 jobs: the template xi.jeuno.helpers.BorghertzQuests
// (scripts/quests/jeuno/helpers.lua:354-666 at 77e0890a; carry-quests-1 dropped it from the file, and a record
// --lua-at takes the file back), a line-for-line port (quest rework B3b). Each of the fifteen Lua files
// (scripts/quests/jeuno/Borghertzs_<Name>_Hands.lua)
// is BorghertzQuests:new(params); here each quest is a sealed subclass passing its params (Quests below; the quest
// classes are found by reflection: the abstract base is skipped).
// Guslam (Upper Jeuno; default 154): available, the prerequisite begun or done, the job, level 50, no Borghertz quest
// accepted: Option = main job, 155; its finish begins the quest whose Option is the main job. Accepted without the Old
// Gauntlets: Guslam 43; the old-gauntlet zone's Treasure Coffer opens to the Old Gauntlets (xi.treasure's key-item
// bypass). With them: Guslam 26 (Prog 0: Prog 1 on a first Borghertz quest, else Prog 3); Deadly Minnow 24 at Prog 1
// (Prog 2); Yin Pocanakhu 220 at Prog 2 (its update: 1000 gil: Prog 3, else YIN_POCANAKHU_GET_LOST), 221 at Prog 3 or
// 4; Port Jeuno's qm1: 20 at Prog 3 (option 1: Prog 4), 49 at Prog >= 4 without the Shadow Flames, 48 at Prog 5 with
// them (completed: the hands, fame +7 x3, both key items taken); Castle Zvahl Baileys' Torch at Prog 4: Dark Spark
// popped (SENSE_OF_FOREBODING), its death: Prog 5; the Torch at Prog 5: the Shadow Flames. Started or done, on the job,
// the [AF]ZilartCoffer bit unset: each optional zone's Treasure Coffer opens to its artifact (the item bypass) and sets
// the bit (job * 2 - 1, job * 2).
// Kept as the Lua has it: the Lua header calls the template the Gobbiebag's base class (a copied comment); 155's finish
// checks only the Option var; the Torch's pop failing falls through (nil); 49 also answers at Prog 5 without the
// flames; quest:complete false (a full bag: the hands) leaves both key items; the coffer sections hold while the quest
// is completed. Prog is the quest's step (Quest.Step), Option a quest var.
using System;
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public abstract class BorghertzQuests : QuestScript
    {
        // a Lua params table; each zone's Treasure Coffer is named by its Npc constant ("Treasure_Coffer" in every zone)
        public sealed class Params
        {
            public readonly string ConstName;
            public readonly int HandAfId;
            public readonly QuestLog RequiredLogId;
            public readonly string RequiredQuestId;
            public readonly int RequiredJobId;
            public readonly ushort OldGauntletZoneId;
            public readonly string OldGauntletCoffer;
            public readonly ushort OptionalZoneId1;
            public readonly string OptionalCoffer1;
            public readonly ushort OptionalZoneId2;
            public readonly string OptionalCoffer2;
            public readonly int OptionalArtifact1;
            public readonly int OptionalArtifact2;

            public Params(string constName, int handAfId, QuestLog requiredLogId, string requiredQuestId, int requiredJobId,
                ushort oldGauntletZoneId, string oldGauntletCoffer, ushort optionalZoneId1, string optionalCoffer1,
                ushort optionalZoneId2, string optionalCoffer2, int optionalArtifact1, int optionalArtifact2)
            {
                ConstName = constName;
                HandAfId = handAfId;
                RequiredLogId = requiredLogId;
                RequiredQuestId = requiredQuestId;
                RequiredJobId = requiredJobId;
                OldGauntletZoneId = oldGauntletZoneId;
                OldGauntletCoffer = oldGauntletCoffer;
                OptionalZoneId1 = optionalZoneId1;
                OptionalCoffer1 = optionalCoffer1;
                OptionalZoneId2 = optionalZoneId2;
                OptionalCoffer2 = optionalCoffer2;
                OptionalArtifact1 = optionalArtifact1;
                OptionalArtifact2 = optionalArtifact2;
            }
        }

        // the fifteen Lua files' params tables, in quest id order (44 ... 58)
        public static readonly IReadOnlyList<Params> Quests = new Params[]
        {
            new Params("BORGHERTZS_WARRING_HANDS", Item.FightersMufflers, QuestLog.Bastok, "THE_TALEKEEPERS_TRUTH", Job.War,
                Zone.TheEldiemeNecropolis, Npc.TheEldiemeNecropolis.TreasureCoffer,
                Zone.CastleZvahlBaileys, Npc.CastleZvahlBaileys.TreasureCoffer,
                Zone.CrawlersNest, Npc.CrawlersNest.TreasureCoffer,
                Item.FightersCuisses, Item.FightersMask), // 44 Borghertzs_Warring_Hands
            new Params("BORGHERTZS_STRIKING_HANDS", Item.TempleGloves, QuestLog.Bastok, "THE_FIRST_MEETING", Job.Mnk,
                Zone.CrawlersNest, Npc.CrawlersNest.TreasureCoffer,
                Zone.Beadeaux, Npc.Beadeaux.TreasureCoffer,
                Zone.GarlaigeCitadel, Npc.GarlaigeCitadel.TreasureCoffer,
                Item.TempleCyclas, Item.TempleCrown), // 45 Borghertzs_Striking_Hands
            new Params("BORGHERTZS_HEALING_HANDS", Item.HealersMitts, QuestLog.Sandoria, "PRELUDE_OF_BLACK_AND_WHITE", Job.Whm,
                Zone.Beadeaux, Npc.Beadeaux.TreasureCoffer,
                Zone.CrawlersNest, Npc.CrawlersNest.TreasureCoffer,
                Zone.GarlaigeCitadel, Npc.GarlaigeCitadel.TreasureCoffer,
                Item.HealersPantaloons, Item.HealersCap), // 46 Borghertzs_Healing_Hands
            new Params("BORGHERTZS_SORCEROUS_HANDS", Item.WizardsGloves, QuestLog.Windurst, "RECOLLECTIONS", Job.Blm,
                Zone.GarlaigeCitadel, Npc.GarlaigeCitadel.TreasureCoffer,
                Zone.MonasticCavern, Npc.MonasticCavern.TreasureCoffer,
                Zone.TheEldiemeNecropolis, Npc.TheEldiemeNecropolis.TreasureCoffer,
                Item.WizardsCoat, Item.WizardsTonban), // 47 Borghertzs_Sorcerous_Hands
            new Params("BORGHERTZS_VERMILLION_HANDS", Item.WarlocksGloves, QuestLog.Sandoria, "ENVELOPED_IN_DARKNESS", Job.Rdm,
                Zone.TheEldiemeNecropolis, Npc.TheEldiemeNecropolis.TreasureCoffer,
                Zone.CastleOztroja, Npc.CastleOztroja.TreasureCoffer,
                Zone.GarlaigeCitadel, Npc.GarlaigeCitadel.TreasureCoffer,
                Item.WarlocksTabard, Item.WarlocksTights), // 48 Borghertzs_Vermillion_Hands
            new Params("BORGHERTZS_SNEAKY_HANDS", Item.RoguesArmlets, QuestLog.Windurst, "AS_THICK_AS_THIEVES", Job.Thf,
                Zone.MonasticCavern, Npc.MonasticCavern.TreasureCoffer,
                Zone.CastleOztroja, Npc.CastleOztroja.TreasureCoffer,
                Zone.CastleZvahlBaileys, Npc.CastleZvahlBaileys.TreasureCoffer,
                Item.RoguesCulottes, Item.RoguesVest), // 49 Borghertzs_Sneaky_Hands
            new Params("BORGHERTZS_STALWART_HANDS", Item.GallantGauntlets, QuestLog.Sandoria, "A_BOYS_DREAM", Job.Pld,
                Zone.TheEldiemeNecropolis, Npc.TheEldiemeNecropolis.TreasureCoffer,
                Zone.Beadeaux, Npc.Beadeaux.TreasureCoffer,
                Zone.GarlaigeCitadel, Npc.GarlaigeCitadel.TreasureCoffer,
                Item.GallantBreeches, Item.GallantCoronet), // 50 Borghertzs_Stalwart_Hands
            new Params("BORGHERTZS_SHADOWY_HANDS", Item.ChaosGauntlets, QuestLog.Bastok, "DARK_PUPPET", Job.Drk,
                Zone.TheEldiemeNecropolis, Npc.TheEldiemeNecropolis.TreasureCoffer,
                Zone.CastleOztroja, Npc.CastleOztroja.TreasureCoffer,
                Zone.MonasticCavern, Npc.MonasticCavern.TreasureCoffer,
                Item.ChaosCuirass, Item.ChaosFlanchard), // 51 Borghertzs_Shadowy_Hands
            new Params("BORGHERTZS_WILD_HANDS", Item.BeastGloves, QuestLog.Jeuno, "SCATTERED_INTO_SHADOW", Job.Bst,
                Zone.CrawlersNest, Npc.CrawlersNest.TreasureCoffer,
                Zone.Beadeaux, Npc.Beadeaux.TreasureCoffer,
                Zone.GarlaigeCitadel, Npc.GarlaigeCitadel.TreasureCoffer,
                Item.BeastJackcoat, Item.BeastHelm), // 52 Borghertzs_Wild_Hands
            new Params("BORGHERTZS_HARMONIOUS_HANDS", Item.ChoralCuffs, QuestLog.Jeuno, "THE_REQUIEM", Job.Brd,
                Zone.CastleZvahlBaileys, Npc.CastleZvahlBaileys.TreasureCoffer,
                Zone.CastleOztroja, Npc.CastleOztroja.TreasureCoffer,
                Zone.CrawlersNest, Npc.CrawlersNest.TreasureCoffer,
                Item.ChoralCannions, Item.ChoralRoundlet), // 53 Borghertzs_Harmonious_Hands
            new Params("BORGHERTZS_CHASING_HANDS", Item.HuntersBracers, QuestLog.Windurst, "FIRE_AND_BRIMSTONE", Job.Rng,
                Zone.GarlaigeCitadel, Npc.GarlaigeCitadel.TreasureCoffer,
                Zone.CrawlersNest, Npc.CrawlersNest.TreasureCoffer,
                Zone.MonasticCavern, Npc.MonasticCavern.TreasureCoffer,
                Item.HuntersBraccae, Item.HuntersJerkin), // 54 Borghertzs_Chasing_Hands
            new Params("BORGHERTZS_LOYAL_HANDS", Item.MyochinKote, QuestLog.Outlands, "YOMI_OKURI", Job.Sam,
                Zone.KuftalTunnel, Npc.KuftalTunnel.TreasureCoffer,
                Zone.QuicksandCaves, Npc.QuicksandCaves.TreasureCoffer,
                Zone.TempleOfUggalepih, Npc.TempleOfUggalepih.TreasureCoffer,
                Item.MyochinHaidate, Item.MyochinDomaru), // 55 Borghertzs_Loyal_Hands
            new Params("BORGHERTZS_LURKING_HANDS", Item.NinjaTekko, QuestLog.Outlands, "I_LL_TAKE_THE_BIG_BOX", Job.Nin,
                Zone.IfritsCauldron, Npc.IfritsCauldron.TreasureCoffer,
                Zone.SeaSerpentGrotto, Npc.SeaSerpentGrotto.TreasureCoffer,
                Zone.TheBoyahdaTree, Npc.TheBoyahdaTree.TreasureCoffer,
                Item.NinjaKyahan, Item.NinjaHatsuburi), // 56 Borghertzs_Lurking_Hands
            new Params("BORGHERTZS_DRAGON_HANDS", Item.DrachenFingerGauntlets, QuestLog.Sandoria, "CHASING_QUOTAS", Job.Drg,
                Zone.TheBoyahdaTree, Npc.TheBoyahdaTree.TreasureCoffer,
                Zone.IfritsCauldron, Npc.IfritsCauldron.TreasureCoffer,
                Zone.QuicksandCaves, Npc.QuicksandCaves.TreasureCoffer,
                Item.DrachenMail, Item.DrachenGreaves), // 57 Borghertzs_Dragon_Hands
            new Params("BORGHERTZS_CALLING_HANDS", Item.EvokersBracers, QuestLog.Windurst, "CLASS_REUNION", Job.Smn,
                Zone.SeaSerpentGrotto, Npc.SeaSerpentGrotto.TreasureCoffer,
                Zone.TempleOfUggalepih, Npc.TempleOfUggalepih.TreasureCoffer,
                Zone.ToraimaraiCanal, Npc.ToraimaraiCanal.TreasureCoffer,
                Item.EvokersDoublet, Item.EvokersPigaches), // 58 Borghertzs_Calling_Hands
        };

        // borghertzQuests
        static readonly string[] BorghertzQuestIds =
        {
            "BORGHERTZS_WARRING_HANDS",
            "BORGHERTZS_STRIKING_HANDS",
            "BORGHERTZS_HEALING_HANDS",
            "BORGHERTZS_SORCEROUS_HANDS",
            "BORGHERTZS_VERMILLION_HANDS",
            "BORGHERTZS_SNEAKY_HANDS",
            "BORGHERTZS_STALWART_HANDS",
            "BORGHERTZS_SHADOWY_HANDS",
            "BORGHERTZS_WILD_HANDS",
            "BORGHERTZS_HARMONIOUS_HANDS",
            "BORGHERTZS_CHASING_HANDS",
            "BORGHERTZS_LOYAL_HANDS",
            "BORGHERTZS_LURKING_HANDS",
            "BORGHERTZS_DRAGON_HANDS",
            "BORGHERTZS_CALLING_HANDS",
        };

        readonly Params _params;
        readonly string _legacyFile;

        protected BorghertzQuests(int index, string legacyFile) : base(QuestLog.Jeuno, Quests[index].ConstName)
        {
            _params = Quests[index];
            _legacyFile = legacyFile;
        }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/" + _legacyFile;

        static bool CanStartQuest(IQuestContext q, Params p)
        {
            // Previous quest check.
            if (q.Player.QuestStatus(p.RequiredLogId, p.RequiredQuestId) == QuestStatus.Available)
            {
                return false;
            }

            // Job check.
            if (q.Player.MainJob != p.RequiredJobId)
            {
                return false;
            }

            // Level check.
            if (q.Player.MainLevel < 50)
            {
                return false;
            }

            // Any other Borghertz quest check.
            for (int i = 0; i < BorghertzQuestIds.Length; i++)
            {
                if (q.Player.QuestStatus(QuestLog.Jeuno, BorghertzQuestIds[i]) == QuestStatus.Accepted)
                {
                    return false;
                }
            }

            return true;
        }

        static bool IsFirstBorghertzQuest(IQuestContext q)
        {
            for (int i = 0; i < BorghertzQuestIds.Length; i++)
            {
                if (q.Player.QuestStatus(QuestLog.Jeuno, BorghertzQuestIds[i]) == QuestStatus.Completed)
                {
                    return false;
                }
            }

            return true;
        }

        // utils.mask.getBit(mask, pos)
        static bool GetBit(int mask, int pos) => (mask & (1 << pos)) != 0;

        protected override void Build(QuestScriptBuilder b)
        {
            Params p = _params;

            b.Reward(new QuestReward { Item = p.HandAfId });

            b.State(QuestStatus.Available)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Guslam).OnTrigger(q =>
                {
                    if (CanStartQuest(q, p))
                    {
                        q.Quest.SetVar("Option", q.Player.MainJob);
                        return QuestReply.ProgressEvent(155);
                    }
                    return null;
                })
                .OnEventFinish(Zone.UpperJeuno, 155, q =>
                {
                    if (q.Quest.Var("Option") == q.Player.MainJob)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted, when: q => !q.Player.HasKeyItem(KeyItem.OldGauntlets))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Guslam).OnTrigger(q => QuestReply.Event(43))
                // Old gauntlets coffer logic.
                .Npc(p.OldGauntletZoneId, p.OldGauntletCoffer).OnTrade(q =>
                {
                    q.Trade.TreasureTrade(2, KeyItem.OldGauntlets);

                    return QuestReply.NoAction();
                });

            b.State(QuestStatus.Accepted, when: q => q.Player.HasKeyItem(KeyItem.OldGauntlets))
                .Npc(Zone.CastleZvahlBaileys, Npc.CastleZvahlBaileys.DarkSpark).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 4)
                    {
                        q.Quest.SetStep(5);
                    }
                })
                .Npc(Zone.CastleZvahlBaileys, Npc.CastleZvahlBaileys.Torch).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 4 &&
                        q.Player.PopFromQm(Mob.CastleZvahlBaileys.DARK_SPARK, claim: true, hide: 0))
                    {
                        return QuestReply.MessageSpecial(Text.CastleZvahlBaileys.SENSE_OF_FOREBODING);
                    }
                    else if (questProgress == 5 &&
                        !q.Player.HasKeyItem(KeyItem.ShadowFlames))
                    {
                        q.Player.GiveKeyItem(KeyItem.ShadowFlames);
                        return QuestReply.NoAction();
                    }
                    return null;
                })
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.YinPocanakhu).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;
                    if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(220);
                    }
                    else if (questProgress == 3 || questProgress == 4)
                    {
                        return QuestReply.Event(221);
                    }
                    return null;
                })
                .OnEventUpdate(Zone.LowerJeuno, 220, q =>
                {
                    if (q.Player.Gil >= 1000)
                    {
                        q.Player.DelGil(1000);
                        q.Quest.SetStep(3); // Spoken with Yin Pocanakhu.
                        q.Player.UpdateEvent(1, 1, 0, 0);
                    }
                    else
                    {
                        q.Text.MessageText(Text.LowerJeuno.YIN_POCANAKHU_GET_LOST, true, 0);
                        q.Player.UpdateEvent(0, 1, 0, 0);
                    }
                })
                .Npc(Zone.PortJeuno, Npc.PortJeuno.qm1).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 3)
                    {
                        return QuestReply.ProgressEvent(20);
                    }
                    else if (questProgress >= 4 && !q.Player.HasKeyItem(KeyItem.ShadowFlames))
                    {
                        return QuestReply.Event(49);
                    }
                    else if (questProgress == 5 && q.Player.HasKeyItem(KeyItem.ShadowFlames))
                    {
                        return QuestReply.ProgressEvent(48);
                    }
                    return null;
                })
                .OnEventFinish(Zone.PortJeuno, 20, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.SetStep(4); // Spoken with Borghertz. Requested "Shadow Flames".
                    }
                })
                .OnEventFinish(Zone.PortJeuno, 48, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 7);
                        q.Player.AddFame(FameArea.Bastok, 7);
                        q.Player.AddFame(FameArea.Windurst, 7);
                        q.Player.DelKeyItem(KeyItem.OldGauntlets);
                        q.Player.DelKeyItem(KeyItem.ShadowFlames);
                    }
                })
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.DeadlyMinnow).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(24);
                    }
                    return null;
                })
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Guslam).OnTrigger(q => QuestReply.ProgressEvent(26))
                .OnEventFinish(Zone.UpperJeuno, 24, q =>
                {
                    q.Quest.SetStep(2); // Spoken with deadly Minnow.
                })
                .OnEventFinish(Zone.UpperJeuno, 26, q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        if (IsFirstBorghertzQuest(q))
                        {
                            q.Quest.SetStep(1); // Need to speak with Deadly Minnow and Yin Pocanakhu.
                        }
                        else
                        {
                            q.Quest.SetStep(3); // Skip intermediaries.
                        }
                    }
                });

            // status ~= QUEST_AVAILABLE: Quest must be already started or completed. Player must be on the appropiate job.
            Func<IQuestCheck, bool> coffer1 = q => q.Player.MainJob == p.RequiredJobId &&
                !GetBit(q.Player.CharVar("[AF]ZilartCoffer"), p.RequiredJobId * 2 - 1);
            b.State(QuestStatus.Accepted, when: coffer1).Or(QuestStatus.Completed, when: coffer1)
                .Npc(p.OptionalZoneId1, p.OptionalCoffer1).OnTrade(q =>
                {
                    int result = q.Trade.TreasureTrade(1, p.OptionalArtifact1);
                    if (result == p.OptionalArtifact1)
                    {
                        q.Player.IncrementCharVar("[AF]ZilartCoffer", 1 << (p.RequiredJobId * 2 - 1));
                    }

                    return QuestReply.NoAction();
                });

            Func<IQuestCheck, bool> coffer2 = q => q.Player.MainJob == p.RequiredJobId &&
                !GetBit(q.Player.CharVar("[AF]ZilartCoffer"), p.RequiredJobId * 2);
            b.State(QuestStatus.Accepted, when: coffer2).Or(QuestStatus.Completed, when: coffer2)
                .Npc(p.OptionalZoneId2, p.OptionalCoffer2).OnTrade(q =>
                {
                    int result = q.Trade.TreasureTrade(1, p.OptionalArtifact2);
                    if (result == p.OptionalArtifact2)
                    {
                        q.Player.IncrementCharVar("[AF]ZilartCoffer", 1 << (p.RequiredJobId * 2));
                    }

                    return QuestReply.NoAction();
                });
        }
    }
}
