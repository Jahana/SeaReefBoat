// Mission Adjustments' era module, a line-for-line port of modules/era/lua/missions/mission_adjustments.lua (quest
// rework C3). Module:new('era_mission_adjustments'), its one onServerStart override declared by era (addOverrideByEra):
// each era's case applies when xi.pre(that expansion) is true, so each is an `if (m.Pre(...))` of Build. T1 ported the
// SOA case's ToAU missions 3, 5, 6, 7 and 8; T2 adds the ROV case's ToAU 17, 18, 24 and 25; T3 adds its 32, 33, 37 and
// 38; N5 adds the SOA case's San d'Oria 6_2 and N6 its 8_1; each branch adds only its keys.
//
// Seekers of Adoulin era (June 17, 2014
// https://forum.square-enix.com/ffxi/threads/42614-Jun-17-2014-(JST)-Version-Update,
// Sep 9, 2014 https://forum.square-enix.com/ffxi/threads/44090-Sep-9-2014-(JST)-Version-Update): forced JST midnight
// waits before ToAU missions 4, 6 and 8 (President Salaheem, Confessions of Royalty / Easterly Winds, Westerly Winds /
// A Mercenary Life). San d'Oria 6-2 (June 17, 2014
// https://forum.square-enix.com/ffxi/threads/42614-Jun-17-2014-%28JST%29-Version-Update): the JST midnight wait for
// the gate guards (Ambrotien, Endracion, Grilau) to decipher the ancient book in Ranperre's Final Rest. San d'Oria 8-1
// (July 8, 2014 https://forum.square-enix.com/ffxi/threads/43135-Jul-8-2014-%28JST%29-Version-Update): the JST midnight
// wait before Northern San d'Oria's final cutscene.
//
// Rhapsodies of Vana'diel era (Aug 3, 2016
// https://forum.square-enix.com/ffxi/threads/51154-Aug.-3-2016-%28JST%29-Version-Update): forced JST midnight waits
// before ToAU missions 18, 25, 33 and 38 (Guests of the Empire / Passing Glory, Foiled Ambition / Playing the Part,
// In the Blood / Sentinels' Honor, Path of Blood / Stirrings of War).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Modules.Era.Missions
{
    public sealed class MissionAdjustments : QuestModule
    {
        public MissionAdjustments() : base("era_mission_adjustments",
            "modules/era/lua/missions/mission_adjustments.lua") { }

        const int AreaId = MissionLog.Toau;
        // San d'Oria 6-2's mission.areaId
        const int SandoriaAreaId = MissionLog.Sandoria;

        protected override void Build(QuestModuleBuilder m)
        {
            if (m.Pre(QuestExpansion.Soa))
            {
                // ToAU Mission 4 "Knight of Gold": President Salaheem's completion waits for the JST midnight
                m.Mission(MissionKey.Toau.PresidentSalaheem)
                    .State(1).Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).ReplaceOnTrigger(q =>
                    {
                        if (q.Player.MissionStatus(AreaId) == 1)
                        {
                            if (!q.Mission.MustZone &&
                                q.Mission.Var("Timer") == 0) // Module change: Check JST midnight timer
                            {
                                return QuestReply.ProgressEvent(3020).TextTable(0); // Enter Not-Trion.
                            }

                            // Default Dialog.
                            return QuestReply.Event(3003).Option(0, q.Player.MercenaryRank).TextTable(0);
                        }

                        return QuestReply.ProgressEvent(73).TextTable(0); // Mog Locker scam.
                    })
                    .ReplaceOnEventFinish(Zone.AhtUrhganWhitegate, 73, q =>
                    {
                        q.Player.SetMissionStatus(AreaId, 1);
                        q.Mission.SetMustZone();
                        // Module change: Start JST midnight timer
                        q.Mission.SetVar("Timer", 1, (uint)q.Player.JstMidnight);
                    });

                // ToAU Mission 6 "Easterly Winds": Confessions of Royalty's finish starts the JST midnight timer
                m.Mission(MissionKey.Toau.ConfessionsOfRoyalty)
                    .State(1).ReplaceOnEventFinish(Zone.ChateauDoraguille, 564, q =>
                    {
                        if (q.Option == 1)
                        {
                            q.Player.DelKeyItem(KeyItem.RaillefalsLetter);
                            if (q.Mission.Complete())
                            {
                                // Module change: Start JST midnight timer
                                q.Player.SetCharVar("Mission[4][5]Timer", 1, (uint)q.Player.JstMidnight);
                            }
                        }
                    });

                m.Mission(MissionKey.Toau.EasterlyWinds)
                    .State(1).ReplaceOnTriggerAreaEnter(Zone.RuludeGardens, 1, q =>
                    {
                        // Module change: Check JST midnight timer before allowing player to progress the mission
                        if (q.Mission.Var("Timer") == 0)
                        {
                            return QuestReply.ProgressEvent(10094);
                        }

                        return null;
                    });

                // ToAU Mission 8 "A Mercenary Life": Westerly Winds' finish starts the JST midnight timer. Kept as the
                // Lua has it: the finish sets the mission's mustZone as a local var, which getMustZone (a char var)
                // never reads, so only the Timer delays it.
                m.Mission(MissionKey.Toau.WesterlyWinds)
                    .State(1).ReplaceOnEventFinish(Zone.AhtUrhganWhitegate, 3028, q =>
                    {
                        if (q.Mission.Complete())
                        {
                            q.Player.DelKeyItem(KeyItem.RaillefalsNote);
                            q.Player.SetLocalVar("Mission[4][7]mustZone", 1);
                            // Module change: Start JST midnight timer
                            q.Player.SetCharVar("Mission[4][7]Timer", 1, (uint)q.Player.JstMidnight);
                        }
                    });

                m.Mission(MissionKey.Toau.AMercenaryLife)
                    .State(2).ReplaceOnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 3, q =>
                    {
                        // Module change: Check JST midnight timer before allowing player to progress the mission
                        if (q.Mission.Var("Timer") == 0)
                        {
                            return QuestReply.ProgressEvent(3050, 3, 3, 3, 3, 3, 3, 3, 3, 0);
                        }

                        return null;
                    });

                // San d'Oria Mission 6-2 "Ranperre's Final Rest": Restores the JST midnight wait for the gate guards
                // to decipher the ancient book. The June 17, 2014 version update decreased the required wait.
                // Start the timer when the book is handed to a gate guard.
                m.Mission(MissionKey.Sandoria.RanperresFinalRest)
                    .State(2).WrapOnEventFinish(Zone.SouthernSanDoria, 1035, (q, baseHandler) =>
                    {
                        baseHandler(q);
                        q.Mission.SetVar("Timer", 1, (uint)q.Player.JstMidnight);
                    })
                    .WrapOnEventFinish(Zone.SouthernSanDoria, 1036, (q, baseHandler) =>
                    {
                        baseHandler(q);
                        q.Mission.SetVar("Timer", 1, (uint)q.Player.JstMidnight);
                    })
                    .WrapOnEventFinish(Zone.NorthernSanDoria, 1035, (q, baseHandler) =>
                    {
                        baseHandler(q);
                        q.Mission.SetVar("Timer", 1, (uint)q.Player.JstMidnight);
                    })
                    // Hold players at the "still deciphering" dialogue until the timer expires.
                    .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Ambrotien).WrapOnTrigger((q, baseHandler) =>
                    {
                        if (q.Player.MissionStatus(SandoriaAreaId) == 4 &&
                            q.Mission.Var("Timer") != 0)
                        {
                            return QuestReply.ProgressEvent(1038);
                        }

                        return baseHandler(q);
                    })
                    .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Endracion).WrapOnTrigger((q, baseHandler) =>
                    {
                        if (q.Player.MissionStatus(SandoriaAreaId) == 4 &&
                            q.Mission.Var("Timer") != 0)
                        {
                            return QuestReply.ProgressEvent(1037);
                        }

                        return baseHandler(q);
                    })
                    .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Grilau).WrapOnTrigger((q, baseHandler) =>
                    {
                        if (q.Player.MissionStatus(SandoriaAreaId) == 4 &&
                            q.Mission.Var("Timer") != 0)
                        {
                            return QuestReply.ProgressEvent(1037);
                        }

                        return baseHandler(q);
                    });

                // San d'Oria Mission 8-1 "Coming of Age": Restores the JST midnight wait before the final cutscene in
                // Northern San d'Oria. The July 8, 2014 version update reduced the wait to one Earth minute.
                // Source: https://forum.square-enix.com/ffxi/threads/43135-Jul-8-2014-%28JST%29-Version-Update
                m.Mission(MissionKey.Sandoria.ComingOfAge)
                    .State(2).ReplaceOnEventFinish(Zone.ChateauDoraguille, 102, q =>
                    {
                        // Copy of the base handler with the one minute wait extended to JST midnight
                        if (q.Mission.Complete())
                        {
                            q.Mission.SetVar("Progress", q.Player.JstMidnight);
                            q.Player.DelKeyItem(KeyItem.DropsOfAmnio);
                        }
                    });
            }

            if (m.Pre(QuestExpansion.Rov))
            {
                // ToAU Mission 18 "Passing Glory": Guests of the Empire's finish starts the JST midnight timer.
                // Kept as the Lua has it: the finish sets the mission's mustZone as a local var, which
                // getMustZone (a char var) never reads, so only the Timer delays it.
                m.Mission(MissionKey.Toau.GuestsOfTheEmpire)
                    .State(1).ReplaceOnEventFinish(Zone.AhtUrhganWhitegate, 3078, q =>
                    {
                        if (q.Mission.Complete())
                        {
                            q.Player.SetLocalVar("Mission[4][17]mustZone", 1);
                            // Module change: Start JST midnight timer
                            q.Player.SetCharVar("Mission[4][17]Timer", 1, (uint)q.Player.JstMidnight);
                        }
                    });

                m.Mission(MissionKey.Toau.PassingGlory)
                    .State(1).ReplaceCheck(q => q.Mission.IsCurrent &&
                        !q.Mission.MustZone &&
                        q.Mission.Var("Timer") == 0);

                // ToAU Mission 25 "Playing the Part": Foiled Ambition's finish starts the JST midnight timer.
                // Kept as the Lua has it: the finish sets the mission's mustZone as a local var, which
                // getMustZone (a char var) never reads, so only the Timer delays it.
                m.Mission(MissionKey.Toau.FoiledAmbition)
                    .State(1).ReplaceOnEventFinish(Zone.AhtUrhganWhitegate, 3097, q =>
                    {
                        if (q.Mission.Complete())
                        {
                            q.Player.SetLocalVar("Mission[4][24]mustZone", 1);
                            // Module change: Start JST midnight timer
                            q.Player.SetCharVar("Mission[4][24]Timer", 1, (uint)q.Player.JstMidnight);
                        }
                    });

                m.Mission(MissionKey.Toau.PlayingThePart)
                    .State(1).Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).ReplaceOnTrigger(q =>
                    {
                        if (!q.Mission.MustZone &&
                            q.Mission.Var("Timer") == 0) // Module change: Check JST midnight timer
                        {
                            return QuestReply.ProgressEvent(3110);
                        }

                        int dialog = q.Mission.Var("Option") + 1; // Captured values 1 and 2
                        if (dialog == 1)
                        {
                            q.Mission.SetVar("Option", 1);
                        }
                        else
                        {
                            q.Mission.SetVar("Option", 0);
                        }

                        return QuestReply.Event(3098, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, dialog, 0);
                    });

                // ToAU Mission 33 "Sentinels' Honor": In the Blood's finish starts the JST midnight timer. Kept as the
                // Lua has it: the finish sets the mission's mustZone as a local var, which getMustZone (a char var)
                // never reads, so only the Timer delays it.
                m.Mission(MissionKey.Toau.InTheBlood)
                    .State(1).ReplaceOnEventFinish(Zone.AhtUrhganWhitegate, 3113, q =>
                    {
                        if (q.Mission.Complete())
                        {
                            q.Player.SetLocalVar("Mission[4][32]mustZone", 1);
                            // Module change: Start JST midnight timer
                            q.Player.SetCharVar("Mission[4][32]Timer", 1, (uint)q.Player.JstMidnight);
                        }
                    });

                m.Mission(MissionKey.Toau.SentinelsHonor)
                    .State(1).Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).ReplaceOnTrigger(q =>
                    {
                        bool hasZoned = !q.Mission.MustZone;
                        bool hasTimePassed = q.Mission.Var("Timer") == 0; // Module change: Check JST midnight timer

                        // Calculate event.
                        int eventId = (hasZoned && hasTimePassed) ? 3130 : 3120;

                        // Calculate dialog.
                        int dialog = 2;
                        if (!hasTimePassed)
                        {
                            // Dialog parameter cycles between 0 and 2 until time lockout is over. At that point,
                            // option 0 stops happening.
                            dialog = q.Mission.Var("Option");
                            if (dialog == 0)
                            {
                                q.Mission.SetVar("Option", 2);
                            }
                            else
                            {
                                q.Mission.SetVar("Option", 0);
                            }
                        }

                        return QuestReply.Event(eventId, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, dialog, 0);
                    });

                // ToAU Mission 38 "Stirrings of War": Path of Blood's finish starts the JST midnight timer. Kept as
                // the Lua has it: the finish sets the mission's mustZone as a local var, which getMustZone (a char
                // var) never reads, so only the Timer delays it. Divergence with equal behaviour: the Lua's
                // modifyInteractionEntry leaves a function-valued onZoneIn behind (its remove only finds table
                // handlers), so the patched mission is in the zone-in list twice; the C# registers it once, and both
                // copies play the same 3220.
                m.Mission(MissionKey.Toau.PathOfBlood)
                    .State(1).ReplaceOnEventFinish(Zone.AhtUrhganWhitegate, 3220, q =>
                    {
                        if (q.Mission.Complete())
                        {
                            q.Player.SetLocalVar("Mission[4][37]mustZone", 1);
                            // Module change: Start JST midnight timer
                            q.Player.SetCharVar("Mission[4][37]Timer", 1, (uint)q.Player.JstMidnight);
                        }
                    });

                m.Mission(MissionKey.Toau.StirringsOfWar)
                    .State(1).ReplaceCheck(q => q.Mission.IsCurrent &&
                        !q.Mission.MustZone &&
                        q.Mission.Var("Timer") == 0); // Module change: Check JST midnight timer
            }
        }
    }
}
