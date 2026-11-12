// Magicite (Windurst 4-1, Log ID 2, Mission ID 13), a line-for-line port of
// scripts/missions/windurst/4_1_Magicite.lua (quest rework C3 N4). Pakh Jatalfih !pos 34 8 -35 243; _6r8 (Embassy)
// !pos 31 9 -22 243; _6r9 (Audience Chamber) !pos 0 -5 66 243; Aldo !pos 20 3 -58 245; Paya-Sabya !pos 9 1 70 244;
// Geebeh !pos 11 1 68 244; Muckvix !pos -26.824 4.601 -137.082 245; Magicite (Orastone) !pos -344 25 43 152; Magicite
// (Optistone) !pos -160 -8 8 150; Magicite (Aurastone) !pos 11 25 -81 148. Reward: rank 5, 10000 gil and the Message
// to Jeuno (Windurst). Section 1 (no Windurst mission current, a Windurstian of rank 4): with
// xi.mission.getMissionRankPoints met, the embassy door _6r8 plays 131 (progress; 1 with the Archducal Audience Permit,
// else 0) and Pakh Jatalfih 50 (progress); without, Pakh Jatalfih plays 54 (event; it wins over the 54 that A New
// Journey's section 3 serves at priority 20). 131's finish (any option but a cancelled one) begins the mission, sets
// status 1, says YOU_ACCEPT_THE_MISSION (messageText, mode 6) and gives the permit. Section 2 (status 1): _6r9 128
// (progress), Pakh Jatalfih 134; 128's finish sets status 2 and gives the Letter to Aldo. Section 3 (status 2): Aldo
// 152 (progress: 1, 1 with the Silver Bell, else 0, 1), whose finish sets status 3, takes the letter and gives the bell
// unless held; Pakh Jatalfih 137. Section 4 (status 3): the Altar Room Magicite without the Orastone: 44 (progress: the
// zone, 3 with the other two stones, else the zone), whose finish gives the Orastone; the zone-in at Option 2 plays
// 10000, whose finish (unless cancelled) sets Option 0. Aldo: no stone 161, else 183. Muckvix: without the Yagudo Torch
// 184 (progress) at Option 1, else 80; with it 81 at Option 2, else 79; 184's finish gives the torch and sets Option 2.
// The Monastic Cavern Magicite without the Optistone: 0 (progress: the zone, 0, 1 with the other two, else the zone),
// finish: the Optistone. The Qulun Dome Magicite without the Aurastone: 0 (progress: 1, the Coruscant Rosary, the
// Black Matinee Necklace with the other two, else 0, the same two), finish: the Aurastone. _6r9 with all three stones
// plays 60 (progress: 1, 0 with the Airship Pass, else 0, 0); Pakh Jatalfih 137; 60's finish takes the stones, gives
// 20000 gil with the pass or else the pass, adds Have Wings, Will Fly and sets status 4. Geebeh without the torch at
// Option 1: WITHER_AND_DIE. Paya-Sabya without the torch: 23 at Option 1, else 80 (progress); 80's finish sets Option
// 1. Section 5 (status 4): Aldo 183; Pakh Jatalfih 37 (progress), whose finish completes the mission. Kept as the Lua
// has it: section 1 compares the nation with mission.areaId (the log id, 2, which is xi.nation.WINDURST); unlike the
// other two nations' Paya-Sabya, this one plays 80 at Option 2 too.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class Magicite : MissionScript
    {
        public Magicite() : base(XI.Quests.MissionLog.Windurst, "MAGICITE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/4_1_Magicite";

        // mission.areaId
        const int AreaId = MissionLog.Windurst;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { Rank = 5, Gil = 10000, KeyItem = KeyItem.MessageToJeunoWindurst });

            // Player has no active missions
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Windurst) == Mission.Windurst.None &&
                    q.Player.Nation == AreaId &&
                    q.Player.Rank(AreaId) == 4)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens._6r8).OnTrigger(q =>
                {
                    if (!q.Player.MissionRankPoints(Mission.Windurst.Magicite))
                    {
                        return null;
                    }

                    if (q.Player.HasKeyItem(KeyItem.ArchducalAudiencePermit))
                    {
                        return QuestReply.ProgressEvent(131, 1);
                    }

                    return QuestReply.ProgressEvent(131, 0);
                })
                // Event 50 points at the embassy door.
                // Progress priority keeps it ahead of the event 54 that 3-3 serves on this NPC.
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.PakhJatalfih).OnTrigger(q =>
                {
                    if (q.Player.MissionRankPoints(Mission.Windurst.Magicite))
                    {
                        return QuestReply.ProgressEvent(50);
                    }

                    return QuestReply.Event(54);
                })
                // Both menu answers carry on into the rest of the scene.
                // Only a cancelled event leaves the mission unstarted.
                .OnEventFinish(Zone.RuludeGardens, 131, q =>
                {
                    if (q.Option == Utils.EventCancelledOption)
                    {
                        return;
                    }

                    q.Mission.Begin();
                    q.Player.SetMissionStatus(AreaId, 1);
                    q.Text.MessageText(Text.RuludeGardens.YOU_ACCEPT_THE_MISSION, false, 6);
                    q.Player.GiveKeyItem(KeyItem.ArchducalAudiencePermit);
                });

            // Permit obtained. The archduke has not been seen.
            b.State(current: true, when: q => q.Player.MissionStatus(AreaId) == 1)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens._6r9).OnTrigger(QuestReply.ProgressEvent(128))
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.PakhJatalfih).OnTrigger(QuestReply.Event(134))
                .OnEventFinish(Zone.RuludeGardens, 128, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 2);
                    q.Player.GiveKeyItem(KeyItem.LetterToAldo);
                });

            // Carrying the letter to Aldo.
            b.State(current: true, when: q => q.Player.MissionStatus(AreaId) == 2)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Aldo).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.SilverBell))
                    {
                        return QuestReply.ProgressEvent(152, 1, 1);
                    }

                    return QuestReply.ProgressEvent(152, 0, 1);
                })
                .OnEventFinish(Zone.LowerJeuno, 152, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 3);
                    q.Player.DelKeyItem(KeyItem.LetterToAldo);

                    if (!q.Player.HasKeyItem(KeyItem.SilverBell))
                    {
                        q.Player.GiveKeyItem(KeyItem.SilverBell);
                    }
                })
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.PakhJatalfih).OnTrigger(QuestReply.Event(137));

            // Key item hunt and magicite gathering.
            b.State(current: true, when: q => q.Player.MissionStatus(AreaId) == 3)
                .Npc(Zone.AltarRoom, Npc.AltarRoom.Magicite).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MagiciteOrastone))
                    {
                        return null;
                    }

                    // Param 1 appends the Lion and Shadow of Darkness scene when this completes the set.
                    if (q.Player.HasKeyItem(KeyItem.MagiciteOptistone) &&
                        q.Player.HasKeyItem(KeyItem.MagiciteAurastone))
                    {
                        return QuestReply.ProgressEvent(44, Zone.AltarRoom, 3);
                    }

                    return QuestReply.ProgressEvent(44, Zone.AltarRoom);
                })
                .OnZoneIn(Zone.AltarRoom, q =>
                    q.Mission.Var("Option") == 2 ? QuestReply.Event(10000) : null) // Fickblix CS
                .OnEventFinish(Zone.AltarRoom, 44, q => q.Player.GiveKeyItem(KeyItem.MagiciteOrastone))
                // A cancelled event does not retire the Fickblix scene.
                .OnEventFinish(Zone.AltarRoom, 10000, q =>
                {
                    if (q.Option == Utils.EventCancelledOption)
                    {
                        return;
                    }

                    q.Mission.SetVar("Option", 0);
                })
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Aldo).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.MagiciteOptistone) &&
                        !q.Player.HasKeyItem(KeyItem.MagiciteAurastone) &&
                        !q.Player.HasKeyItem(KeyItem.MagiciteOrastone))
                    {
                        return QuestReply.Event(161);
                    }

                    return QuestReply.Event(183);
                })
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Muckvix).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.YagudoTorch))
                    {
                        if (q.Mission.Var("Option") == 1)
                        {
                            return QuestReply.ProgressEvent(184);
                        }
                        else
                        {
                            return QuestReply.Event(80);
                        }
                    }
                    else
                    {
                        if (q.Mission.Var("Option") == 2)
                        {
                            return QuestReply.Event(81);
                        }
                        else
                        {
                            return QuestReply.Event(79);
                        }
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 184, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.YagudoTorch);
                    q.Mission.SetVar("Option", 2); // Fickblix CS
                })
                .Npc(Zone.MonasticCavern, Npc.MonasticCavern.Magicite).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MagiciteOptistone))
                    {
                        return null;
                    }

                    // Param 2 appends the Lion and Shadow of Darkness scene when this completes the set.
                    if (q.Player.HasKeyItem(KeyItem.MagiciteAurastone) &&
                        q.Player.HasKeyItem(KeyItem.MagiciteOrastone))
                    {
                        return QuestReply.ProgressEvent(0, Zone.MonasticCavern, 0, 1);
                    }

                    return QuestReply.ProgressEvent(0, Zone.MonasticCavern);
                })
                .OnEventFinish(Zone.MonasticCavern, 0, q => q.Player.GiveKeyItem(KeyItem.MagiciteOptistone))
                .Npc(Zone.QulunDome, Npc.QulunDome.Magicite).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MagiciteAurastone))
                    {
                        return null;
                    }

                    // Param 0 appends the Lion and Shadow of Darkness scene when this completes the set.
                    if (q.Player.HasKeyItem(KeyItem.MagiciteOptistone) &&
                        q.Player.HasKeyItem(KeyItem.MagiciteOrastone))
                    {
                        return QuestReply.ProgressEvent(0, 1, KeyItem.CoruscantRosary, KeyItem.BlackMatineeNecklace);
                    }

                    return QuestReply.ProgressEvent(0, 0, KeyItem.CoruscantRosary, KeyItem.BlackMatineeNecklace);
                })
                .OnEventFinish(Zone.QulunDome, 0, q => q.Player.GiveKeyItem(KeyItem.MagiciteAurastone))
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens._6r9).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.MagiciteOptistone) ||
                        !q.Player.HasKeyItem(KeyItem.MagiciteAurastone) ||
                        !q.Player.HasKeyItem(KeyItem.MagiciteOrastone))
                    {
                        return null;
                    }

                    // Param 0 swaps the airship pass for gil, param 1 skips the Eald'narche and Wolfgang scene when
                    // set.
                    if (q.Player.HasKeyItem(KeyItem.AirshipPass))
                    {
                        return QuestReply.ProgressEvent(60, 1, 0);
                    }

                    return QuestReply.ProgressEvent(60, 0, 0);
                })
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.PakhJatalfih).OnTrigger(QuestReply.Event(137))
                .OnEventFinish(Zone.RuludeGardens, 60, q =>
                {
                    q.Player.DelKeyItem(KeyItem.MagiciteOptistone);
                    q.Player.DelKeyItem(KeyItem.MagiciteAurastone);
                    q.Player.DelKeyItem(KeyItem.MagiciteOrastone);

                    if (q.Player.HasKeyItem(KeyItem.AirshipPass))
                    {
                        q.Player.GiveCurrency("gil", 20000);
                    }
                    else
                    {
                        q.Player.GiveKeyItem(KeyItem.AirshipPass);
                    }

                    q.Player.AddTitle(Title.HaveWingsWillFly);
                    q.Player.SetMissionStatus(AreaId, 4);
                })
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Geebeh).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.YagudoTorch) &&
                        q.Mission.Var("Option") == 1)
                    {
                        return QuestReply.MessageText(Text.UpperJeuno.WITHER_AND_DIE);
                    }

                    return null;
                })
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.PayaSabya).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.YagudoTorch))
                    {
                        return null;
                    }

                    // Event 23 is the follow up once the garden scene has played.
                    if (q.Mission.Var("Option") == 1)
                    {
                        return QuestReply.Event(23);
                    }

                    return QuestReply.ProgressEvent(80);
                })
                .OnEventFinish(Zone.UpperJeuno, 80, q => q.Mission.SetVar("Option", 1)); // "Yagudo Torch" CS

            // The stones are handed over. The embassy closes the mission.
            b.State(current: true, when: q => q.Player.MissionStatus(AreaId) == 4)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Aldo).OnTrigger(QuestReply.Event(183))
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.PakhJatalfih).OnTrigger(QuestReply.ProgressEvent(37))
                .OnEventFinish(Zone.RuludeGardens, 37, q => q.Mission.Complete());
        }
    }
}
