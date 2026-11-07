// Beyond Infinity (Log ID 3, Quest ID 137), a line-for-line port of scripts/quests/jeuno/LB10_Beyond_Infinity.lua (quest
// rework B3b). Nomad Moogle : !pos 10.012 1.453 121.883 243. Available (Prelude to Puissance completed, level cap
// 95): the moogle's 10045 (level, limit breaker 1 / 2, 5 and the local var rejectedStartLB10 when MAX_LEVEL > 95, the
// Limit Breaker held and level > 90, else 0 and 0); options 13, 14, 19, 20, 21 begin, and 14 / 19 / 20 / 21 set the char
// down at Horlais Peak / Waughroon Shrine / Balga's Dais / Qu'Bia Arena. Accepted, Prog 0, the Soul Gem Clasp held: the
// moogle's 10045 (level, limit breaker, 5, 1), its options 16 / 22 / 23 / 24 the four arenas; Maat's 10193. Accepted, Prog
// 0: the arenas' 32001 when battlefieldWin is the arena's Beyond Infinity fight (a Scroll of Instant Warp, Prog 1).
// Accepted, Prog 0, no clasp (the fight lost: the clasp is worn on entry): five High Kindred's Crests traded alone play
// 10195 (1), whose finish takes them, gives the clasp and sets down at 16 / 22 / 23 / 24; the moogle's 10045 (level,
// limit breaker, 5, 3, 0, 0, 1 with a merit point else 0), its update option 9 takes a merit point, its finish gives the
// clasp on any option but 0 and sets down at 17 / 25 / 26 / 27. Accepted, Prog 1: 10139 (75, 1): completed (Bushin
// Aspirant), level cap 99, YOUR_LEVEL_LIMIT_IS_NOW_99. Completed: level 99 without the Job Breaker: 10240 (level, limit
// breaker), option 28 gives the Job Breaker; else event 10045 (level, limit breaker, 0, 0).
// Kept Lua quirks: the available 10045's finish runs before the accepted states' finishes in the same lookup (each
// check reads the status afresh), so a begin with no clasp held also runs state 4's finish (the clasp given, as option
// 13 / 14 / 19 / 20 / 21 is not 0) and a begin with the clasp held runs state 2's (no arm: 13-21 are none of 16 / 22 / 23
// / 24); the 10195 finish confirms the trade and gives the clasp whatever the option; the 10139 finish's quest:complete
// never fails (a title only).
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class BeyondInfinity : QuestScript
    {
        public BeyondInfinity() : base(QuestLog.Jeuno, "BEYOND_INFINITY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/LB10_Beyond_Infinity";

        // atoriBattlefieldIds: xi.battlefield.id.BEYOND_INFINITY_BALGAS_DAIS (116), BEYOND_INFINITY_HORLAIS_PEAK (20),
        // BEYOND_INFINITY (533, Qu'Bia Arena), BEYOND_INFINITY_WAUGHROON_SHRINE (85) (scripts/globals/battlefield.lua)
        static readonly Dictionary<ushort, int> AtoriBattlefieldIds = new Dictionary<ushort, int>
        {
            [Zone.BalgasDais] = 116,
            [Zone.HorlaisPeak] = 20,
            [Zone.QubiaArena] = 533,
            [Zone.WaughroonShrine] = 85,
        };

        // atoriBattlefieldZone.onEventFinish[32001]
        static void OnBattlefieldWin(IQuestContext q)
        {
            int battlefieldWin = q.Player.LocalVar("battlefieldWin");

            // atoriBattlefieldIds[player:getZoneID()]: the handler's zone (the 32001 finish runs in the arena)
            if (battlefieldWin == AtoriBattlefieldIds[q.Zone])
            {
                q.Player.GiveItem(Item.ScrollOfInstantWarp);
                q.Quest.SetStep(1);
            }
        }

        // the four player:setPos(...) lines each finish repeats
        static void SetDown(IQuestContext q, ushort arena)
        {
            if (arena == Zone.HorlaisPeak)
            {
                q.Player.SetPos(-511.459, 159.004, -210.543, 10, Zone.HorlaisPeak);
            }
            else if (arena == Zone.WaughroonShrine)
            {
                q.Player.SetPos(-349.899, 104.213, -260.150, 0, Zone.WaughroonShrine);
            }
            else if (arena == Zone.BalgasDais)
            {
                q.Player.SetPos(299.316, -123.591, 353.760, 66, Zone.BalgasDais);
            }
            else if (arena == Zone.QubiaArena)
            {
                q.Player.SetPos(-225.146, -24.250, 20.057, 255, Zone.QubiaArena);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Title = Title.BushinAspirant });

            // Section: Quest available.
            // Note: This step only happens with one, very specific option in the previous quest.
            // In most cases, the quest will already be accepted.
            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Jeuno, "PRELUDE_TO_PUISSANCE") &&
                    q.Player.LevelCap == 95)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.NomadMoogle).OnTrigger(q =>
                {
                    int playerLevel = q.Player.MainLevel;
                    int limitBreaker = q.Player.HasKeyItem(KeyItem.LimitBreaker) ? 1 : 2;
                    int lastQuestNumber = 0;
                    int lastQuestStage = 0;
                    int rejectedOffer = 0;
                    if (q.Settings.MainNumber("MAX_LEVEL") > 95 &&
                        limitBreaker == 1 &&
                        playerLevel > 90)
                    {
                        lastQuestNumber = 5;
                        rejectedOffer = q.Player.LocalVar("rejectedStartLB10");
                    }

                    return QuestReply.ProgressEvent(10045, playerLevel, limitBreaker, lastQuestNumber, lastQuestStage, rejectedOffer);
                })
                .OnEventFinish(Zone.RuludeGardens, 10045, q =>
                {
                    // This options start quest.
                    if (q.Option == 13 ||
                        q.Option == 14 ||
                        q.Option == 19 ||
                        q.Option == 20 ||
                        q.Option == 21)
                    {
                        q.Quest.Begin();
                    }

                    // This options also warp you to a BCNM. Note that the quest "Beyond Infinity" is already activated.
                    if (q.Option == 14)
                    {
                        SetDown(q, Zone.HorlaisPeak);
                    }
                    else if (q.Option == 19)
                    {
                        SetDown(q, Zone.WaughroonShrine);
                    }
                    else if (q.Option == 20)
                    {
                        SetDown(q, Zone.BalgasDais);
                    }
                    else if (q.Option == 21)
                    {
                        SetDown(q, Zone.QubiaArena);
                    }
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted, when: q =>
                    q.Quest.Step == 0 &&
                    q.Player.HasKeyItem(KeyItem.SoulGemClasp))
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.NomadMoogle).OnTrigger(q =>
                {
                    int playerLevel = q.Player.MainLevel;
                    int limitBreaker = q.Player.HasKeyItem(KeyItem.LimitBreaker) ? 1 : 2;
                    int lastQuestNumber = 5;
                    int lastQuestStage = 1;

                    return QuestReply.Event(10045, playerLevel, limitBreaker, lastQuestNumber, lastQuestStage);
                })
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Maat).OnTrigger(q =>
                {
                    return QuestReply.Event(10193);
                })
                .OnEventFinish(Zone.RuludeGardens, 10045, q =>
                {
                    if (q.Option == 16)
                    {
                        SetDown(q, Zone.HorlaisPeak);
                    }
                    else if (q.Option == 22)
                    {
                        SetDown(q, Zone.WaughroonShrine);
                    }
                    else if (q.Option == 23)
                    {
                        SetDown(q, Zone.BalgasDais);
                    }
                    else if (q.Option == 24)
                    {
                        SetDown(q, Zone.QubiaArena);
                    }
                });

            // BCNM Win Events.  Soul Gem Clasp is required for entry, and removed
            // after.  Separate section to not confuse with the failed event.
            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 0)
                .OnEventFinish(Zone.BalgasDais, 32001, OnBattlefieldWin)
                .OnEventFinish(Zone.HorlaisPeak, 32001, OnBattlefieldWin)
                .OnEventFinish(Zone.QubiaArena, 32001, OnBattlefieldWin)
                .OnEventFinish(Zone.WaughroonShrine, 32001, OnBattlefieldWin);

            // Section: Quest accepted. We failed BCNM.
            b.State(QuestStatus.Accepted, when: q =>
                    q.Quest.Step == 0 &&
                    !q.Player.HasKeyItem(KeyItem.SoulGemClasp))
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.NomadMoogle)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly((Item.HighKindredsCrest, 5)))
                        {
                            return QuestReply.ProgressEvent(10195, 1);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int playerLevel = q.Player.MainLevel;
                        int limitBreaker = q.Player.HasKeyItem(KeyItem.LimitBreaker) ? 1 : 2;
                        int lastQuestNumber = 5;
                        int lastQuestStage = 3; // I guess failing is considered a new stage.
                        int meritCost = q.Player.MeritCount >= 1 ? 1 : 0;

                        return QuestReply.ProgressEvent(10045, playerLevel, limitBreaker, lastQuestNumber, lastQuestStage, 0, 0, meritCost);
                    })
                .OnEventUpdate(Zone.RuludeGardens, 10045, q =>
                {
                    if (q.Option == 9)
                    {
                        int numMerits = q.Player.MeritCount;

                        if (numMerits >= 1)
                        {
                            q.Player.SetMerits(numMerits - 1);
                        }
                    }
                })
                .OnEventFinish(Zone.RuludeGardens, 10045, q =>
                {
                    if (q.Option != 0)
                    {
                        q.Player.GiveKeyItem(KeyItem.SoulGemClasp);
                    }

                    if (q.Option == 17)
                    {
                        SetDown(q, Zone.HorlaisPeak);
                    }
                    else if (q.Option == 25)
                    {
                        SetDown(q, Zone.WaughroonShrine);
                    }
                    else if (q.Option == 26)
                    {
                        SetDown(q, Zone.BalgasDais);
                    }
                    else if (q.Option == 27)
                    {
                        SetDown(q, Zone.QubiaArena);
                    }
                })
                .OnEventFinish(Zone.RuludeGardens, 10195, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.SoulGemClasp);

                    if (q.Option == 16)
                    {
                        SetDown(q, Zone.HorlaisPeak);
                    }
                    else if (q.Option == 22)
                    {
                        SetDown(q, Zone.WaughroonShrine);
                    }
                    else if (q.Option == 23)
                    {
                        SetDown(q, Zone.BalgasDais);
                    }
                    else if (q.Option == 24)
                    {
                        SetDown(q, Zone.QubiaArena);
                    }
                });

            // Section: Quest accepted. We beated the BCNM.
            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 1)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.NomadMoogle).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(10139, 75, 1);
                })
                .OnEventFinish(Zone.RuludeGardens, 10139, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetLevelCap(99);
                        q.Text.Special(Text.RuludeGardens.YOUR_LEVEL_LIMIT_IS_NOW_99);
                    }
                });

            // Section: Quest completed.
            // TODO: Move all of this to "martial mastery" quest
            b.State(QuestStatus.Completed)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.NomadMoogle).OnTrigger(q =>
                {
                    int playerLevel = q.Player.MainLevel;
                    int limitBreaker = q.Player.HasKeyItem(KeyItem.LimitBreaker) ? 1 : 2;

                    if (q.Player.MainLevel >= 99 &&
                        !q.Player.HasKeyItem(KeyItem.JobBreaker))
                    {
                        return QuestReply.ProgressEvent(10240, playerLevel, limitBreaker);
                    }
                    else
                    {
                        return QuestReply.Event(10045, playerLevel, limitBreaker, 0, 0);
                    }
                })
                .OnEventFinish(Zone.RuludeGardens, 10240, q =>
                {
                    if (q.Option == 28)
                    {
                        q.Player.GiveKeyItem(KeyItem.JobBreaker);
                    }
                });
        }
    }
}
