// Dances with Luopans (Log ID 9, Quest ID 118), a line-for-line port of scripts/quests/adoulin/Dances_with_Luopans.lua
// (quest rework B3b Adoulin group 4; the Geomancer unlock). Western Adoulin: Sylvie !pos 78.094 32.000 135.725 256 (no
// script; default 38). La Theine Plateau, Konschtat Highlands, Tahrongi Canyon: the Ergon Locus (no script, no
// default). Ceizak Battlegrounds trigger areas 1 and 2, Yahse Hunting Grounds trigger area 1 (the Ergon Locus areas
// the zones register).
// The healing effect (scripts/effects/healing.lua) requires the quest's Lua file and calls its globals
// xi.dancesWithLuopans.onHealing (onEffectGain) and onEffectLose (onEffectLose): two exports (quest rework P10), which
// set those globals to the callbacks below and claim the file's require name, so healing.lua still loads.
// Available, main level at ADVANCED_JOB_LEVEL or more: Sylvie: 31, or 32 once declined (the quest var Declined); 31's
// finish begins it on option 1, else sets Declined 1; 32's, on option 1, clears Declined and begins it.
// Accepted: Sylvie: Prog 1 36, else the Luopan held 35, else 33 (all progressEvents); a trade holding a Petrified Log
// (tradeHas) with the Fistful of Homeland Soil held: 34, whose finish confirms the trade, takes the soil and gives the
// Luopan. An Ergon Locus of the char's own nation's zone, no soil and no Luopan: the Fistful of Homeland Soil
// (npcUtil.giveKeyItem inside the trigger, which then answers nothing). Entering a locus area with the Luopan at
// Prog 0: UNCANNY_SENSATION, the local var LocusArea 1; leaving it: LocusArea and RestUntil 0. Healing (any status)
// with LocusArea 1 at Prog 0: ENERGIES_COURSE, RestUntil = now + a random wait (3 healing ticks .. 8 minutes), and a
// timer of that wait that, when it fires with RestUntil set and reached, shows MYSTICAL_WARMTH, clears RestUntil and
// sets Prog 1; the healing's end clears RestUntil. 36's finish: the Plate of Indi-Poison and the Matre Bell
// (npcUtil.giveItem), and when given: Geomancer unlocked, YOU_CAN_NOW_BECOME, the Geomancer gesture, completed (no
// reward table).
// Completed: Sylvie: a Geomancer without a Matre Bell 37 (the local var MatreBellPayment cleared), else 39 (Events
// replacing the default); 37's update (options 1 / 2: 300000 gil / 150000 bayld) answers 2 with no free slot, 1 when
// the char can pay (MatreBellPayment the option), else 0; 37's finish, with a payment chosen and option 1, gives the
// Matre Bell and takes the gil or the bayld.
// Kept as the Lua has it: the Ergon Locus trigger gives the soil and answers nothing (the lookup goes on to the NPC's
// own, empty, handlers); the Petrified Log trade is tradeHas (more items allowed, only the log taken); 34 checks no
// Prog; no reward table; the healing hook runs for any healing entity and at any quest status (it reads LocusArea and
// Prog only), its texts those of the zone the char rests in (zones[player:getZoneID()]).
using System;
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class DancesWithLuopans : QuestScript
    {
        public DancesWithLuopans() : base(QuestLog.Adoulin, "DANCES_WITH_LUOPANS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list, and the module the
        // exports claim (scripts/effects/healing.lua requires it)
        public override string LegacyPath => "scripts/quests/adoulin/Dances_with_Luopans";

        private static void OnLocusAreaEnter(IQuestContext q)
        {
            if (q.Player.HasKeyItem(KeyItem.Luopan) &&
                q.Quest.Step == 0)
            {
                // local ID = zones[player:getZoneID()]
                q.Text.Special(new ZoneText(q.Zone, "UNCANNY_SENSATION"));
                q.Quest.SetLocalVar("LocusArea", 1);
            }
        }

        private static void OnLocusAreaLeave(IQuestContext q)
        {
            q.Quest.SetLocalVar("LocusArea", 0);
            q.Quest.SetLocalVar("RestUntil", 0);
        }

        private static readonly Dictionary<int, int> HomelandSoilNation = new Dictionary<int, int>
        {
            [Zone.LaTheinePlateau] = Nation.Sandoria,
            [Zone.KonschtatHighlands] = Nation.Bastok,
            [Zone.TahrongiCanyon] = Nation.Windurst,
        };

        private static QuestReply OnHomelandSoilTrigger(IQuestContext q)
        {
            if (q.Player.Nation == HomelandSoilNation[q.Zone] &&
                !q.Player.HasKeyItem(KeyItem.FistfulOfHomelandSoil) &&
                !q.Player.HasKeyItem(KeyItem.Luopan))
            {
                q.Player.GiveKeyItem(KeyItem.FistfulOfHomelandSoil);
            }

            return null;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            // the function player:timer runs once the wait is over (the Lua's closure: ID is the zone onHealing ran in,
            // and the char's timers end with a zone change, so it fires in that zone)
            var restTimer = b.Callback("restTimer", q =>
            {
                int restUntil = q.Quest.LocalVar("RestUntil");

                if (restUntil > 0 &&
                    q.Player.SystemTime >= restUntil)
                {
                    q.Text.Special(new ZoneText(q.Zone, "MYSTICAL_WARMTH"));
                    q.Quest.SetLocalVar("RestUntil", 0);
                    q.Quest.SetStep(1);
                }
            });

            // xi.dancesWithLuopans.onHealing(player)
            var onHealing = b.Callback("onHealing", q =>
            {
                if (q.Quest.LocalVar("LocusArea") == 1 &&
                    q.Quest.Step == 0)
                {
                    int maxRestSeconds = 8 * 60;
                    int secondsPerTick = q.Settings.Map("HEALING_TICK_DELAY");
                    int minWaitTime = Math.Min(3 * secondsPerTick, maxRestSeconds);
                    int waitTimeInSeconds = q.Player.RandomInt(minWaitTime, maxRestSeconds);

                    q.Text.Special(new ZoneText(q.Zone, "ENERGIES_COURSE"));
                    q.Quest.SetLocalVar("RestUntil", q.Player.SystemTime + waitTimeInSeconds);

                    q.Player.Timer(waitTimeInSeconds * 1000, restTimer);
                }
            });

            // xi.dancesWithLuopans.onEffectLose(player)
            var onEffectLose = b.Callback("onEffectLose", q =>
            {
                q.Quest.SetLocalVar("RestUntil", 0);
            });

            b.Export("xi.dancesWithLuopans.onHealing", onHealing);
            b.Export("xi.dancesWithLuopans.onEffectLose", onEffectLose);

            // the Prog the rest timer sets
            b.Steps(1);

            b.State(QuestStatus.Available, when: q => q.Player.MainLevel >= q.Settings.MainNumber("ADVANCED_JOB_LEVEL"))
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Sylvie).OnTrigger(q =>
                {
                    if (q.Quest.Var("Declined") == 1)
                    {
                        return QuestReply.ProgressEvent(32);
                    }

                    return QuestReply.ProgressEvent(31);
                })
                .OnEventFinish(Zone.WesternAdoulin, 31, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                    else
                    {
                        q.Quest.SetVar("Declined", 1);
                    }
                })
                .OnEventFinish(Zone.WesternAdoulin, 32, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.SetVar("Declined", 0);
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .OnTriggerAreaEnter(Zone.CeizakBattlegrounds, 1, OnLocusAreaEnter)
                .OnTriggerAreaEnter(Zone.CeizakBattlegrounds, 2, OnLocusAreaEnter)
                .OnTriggerAreaLeave(Zone.CeizakBattlegrounds, 1, OnLocusAreaLeave)
                .OnTriggerAreaLeave(Zone.CeizakBattlegrounds, 2, OnLocusAreaLeave)
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.ErgonLocus).OnTrigger(OnHomelandSoilTrigger)
                .Npc(Zone.KonschtatHighlands, Npc.KonschtatHighlands.ErgonLocus).OnTrigger(OnHomelandSoilTrigger)
                .Npc(Zone.TahrongiCanyon, Npc.TahrongiCanyon.ErgonLocus).OnTrigger(OnHomelandSoilTrigger)
                .OnTriggerAreaEnter(Zone.YahseHuntingGrounds, 1, OnLocusAreaEnter)
                .OnTriggerAreaLeave(Zone.YahseHuntingGrounds, 1, OnLocusAreaLeave)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Sylvie).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(36);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.Luopan))
                    {
                        return QuestReply.ProgressEvent(35);
                    }

                    return QuestReply.ProgressEvent(33);
                })
                .OnTrade(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.FistfulOfHomelandSoil) &&
                        q.Trade.Has(Item.PetrifiedLog))
                    {
                        return QuestReply.ProgressEvent(34);
                    }

                    return null;
                })
                .OnEventFinish(Zone.WesternAdoulin, 34, q =>
                {
                    q.Trade.Confirm();
                    q.Player.DelKeyItem(KeyItem.FistfulOfHomelandSoil);
                    q.Player.GiveKeyItem(KeyItem.Luopan);
                })
                .OnEventFinish(Zone.WesternAdoulin, 36, q =>
                {
                    if (q.Player.GiveItems((Item.PlateOfIndiPoison, 1), (Item.MatreBell, 1)))
                    {
                        q.Player.UnlockJob(Job.Geo);
                        q.Text.Special(Text.WesternAdoulin.YOU_CAN_NOW_BECOME, 0);
                        q.Player.GiveKeyItem(KeyItem.JobGestureGeomancer);
                        q.Quest.Complete();
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Sylvie).OnTrigger(q =>
                {
                    if (q.Player.MainJob == Job.Geo &&
                        !q.Player.HasItem(Item.MatreBell))
                    {
                        q.Quest.SetLocalVar("MatreBellPayment", 0);
                        return QuestReply.Event(37).ReplaceDefault();
                    }

                    return QuestReply.Event(39).ReplaceDefault();
                })
                .OnEventUpdate(Zone.WesternAdoulin, 37, q =>
                {
                    if (q.Option != 1 && q.Option != 2)
                    {
                        return;
                    }

                    q.Quest.SetLocalVar("MatreBellPayment", 0);

                    int eventUpdateParam = 0;
                    if (q.Player.FreeSlotCount < 1)
                    {
                        eventUpdateParam = 2;
                    }
                    else if (
                        (q.Option == 1 && q.Player.Gil >= 300000) ||
                        (q.Option == 2 && q.Player.Currency("bayld") >= 150000))
                    {
                        q.Quest.SetLocalVar("MatreBellPayment", q.Option);
                        eventUpdateParam = 1;
                    }

                    q.Player.UpdateEvent(0, 0, 0, 0, 0, 0, 0, eventUpdateParam);
                })
                .OnEventFinish(Zone.WesternAdoulin, 37, q =>
                {
                    int paymentOption = q.Quest.LocalVar("MatreBellPayment");
                    if (paymentOption == 0)
                    {
                        return;
                    }

                    q.Quest.SetLocalVar("MatreBellPayment", 0);

                    if (q.Option != 1)
                    {
                        return;
                    }

                    if (q.Player.GiveItems((Item.MatreBell, 1)))
                    {
                        if (paymentOption == 1)
                        {
                            q.Player.DelGil(300000);
                        }
                        else if (paymentOption == 2)
                        {
                            q.Player.DelCurrency("bayld", 150000);
                        }
                    }
                });
        }
    }
}
