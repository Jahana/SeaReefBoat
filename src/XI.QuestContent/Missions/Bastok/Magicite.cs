// Magicite (Bastok 4-1, Log ID 1, Mission ID 13), a line-for-line port of scripts/missions/bastok/4_1_Magicite.lua
// (quest rework C3 N4). Goggehn !pos 3 9 -78 243; _6r2 (Embassy) !pos 18 8 -74 243; _6r9 (Audience Chamber) !pos 0 -5
// 66 243; Aldo !pos 20 3 -58 245; Paya-Sabya !pos 9 1 70 244; Geebeh !pos 11 1 68 244; Muckvix !pos -26.824 4.601
// -137.082 245; Magicite (Orastone) !pos -344 25 43 152; Magicite (Optistone) !pos -160 -8 8 150; Magicite (Aurastone)
// !pos 11 25 -81 148. Reward: rank 5, 10000 gil and the Message to Jeuno (Bastok). Section 1 (no Bastok mission
// current, a Bastokan of rank 4): with xi.mission.getMissionRankPoints met, the embassy door _6r2 plays 129 (progress;
// 1 with the Archducal Audience Permit) and Goggehn 0, else Goggehn 4 (events). 129's finish (any option but a
// cancelled one) begins the mission, says YOU_ACCEPT_THE_MISSION (messageText, mode 6) and gives the permit. Section 2
// (current, status 1 or less): the Audience Chamber _6r9 at status 0 plays 128 (progress), else 138 (1); Goggehn 132 at
// 0, else 135. 128's finish sets status 1 and gives the Letter to Aldo. Aldo at status 1 plays 152 (progress: 1 with
// the Silver Bell, else 0, 1), whose finish sets status 2, takes the letter and gives the bell unless held. Section 3
// (status 2): _6r9 with a stone missing plays 138 (1), else 60 (progress: 1, 0 with the Airship Pass, else 0, 0);
// Goggehn 135. 60's finish takes the three stones, gives 20000 gil with the pass or else the pass, adds Have Wings,
// Will Fly and sets status 3. Aldo: no stone 161, else 183. Muckvix: without the Yagudo Torch 184 (progress) at Option
// 1, else 80; with it 81 at Option 2, else 79. 184's finish gives the torch and sets Option 2. Geebeh without the torch
// at Option 1: WITHER_AND_DIE. Paya-Sabya without the torch: 23 at Option 1, 80 (progress) at Option 0. 80's finish
// sets Option 1. The Altar Room Magicite without the Orastone: 44 (progress: the zone, 3 with the other two stones,
// else the zone), whose finish gives the Orastone; the zone-in at Option 2 plays 10000, whose finish (unless cancelled)
// sets Option 0. The Monastic Cavern Magicite without the Optistone: 0 (progress: 1, 1, 1, 1, 1, 1, 1, 1 with the other
// two, else the zone), finish: the Optistone. The Qulun Dome Magicite without the Aurastone: 0 (progress: 1, 46, 47
// with the other two, else 0, 46, 47), finish: the Aurastone. Section 4 (status 3): _6r2 THE_CONSULATE_IS_AWAY; Goggehn
// 35 (progress), whose finish completes the mission; Aldo 183. Kept as the Lua has it: section 1 compares the nation
// with mission.areaId (the log id, 1, which is xi.nation.BASTOK); Paya-Sabya says nothing at Option 2 without the
// torch.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class Magicite : MissionScript
    {
        public Magicite() : base(XI.Quests.MissionLog.Bastok, "MAGICITE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/4_1_Magicite";

        // mission.areaId
        const int AreaId = MissionLog.Bastok;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward { Rank = 5, Gil = 10000, KeyItem = KeyItem.MessageToJeunoBastok });

            // Player has no active missions
            b.State(current: null, when: q =>
                    q.Player.CurrentMission(MissionLog.Bastok) == Mission.Bastok.None &&
                    q.Player.Nation == AreaId &&
                    q.Player.Rank(AreaId) == 4)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens._6r2).OnTrigger(q =>
                {
                    if (q.Player.MissionRankPoints(Mission.Bastok.Magicite))
                    {
                        int hasKIParam = q.Player.HasKeyItem(KeyItem.ArchducalAudiencePermit) ? 1 : 0;
                        return QuestReply.ProgressEvent(129, hasKIParam); // This event starts the mission.
                    }

                    return null;
                })
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Goggehn).OnTrigger(q =>
                {
                    if (q.Player.MissionRankPoints(Mission.Bastok.Magicite))
                    {
                        return QuestReply.Event(0);
                    }
                    else
                    {
                        return QuestReply.Event(4);
                    }
                })
                // Both menu answers carry on into the rest of the scene.
                // Only a cancelled event leaves the mission unstarted.
                .OnEventFinish(Zone.RuludeGardens, 129, q =>
                {
                    if (q.Option == Utils.EventCancelledOption)
                    {
                        return;
                    }

                    q.Mission.Begin();
                    q.Text.MessageText(Text.RuludeGardens.YOU_ACCEPT_THE_MISSION, false, 6);
                    q.Player.GiveKeyItem(KeyItem.ArchducalAudiencePermit);
                });

            // Section 2: Mandatory pre-requisites. Necessary steps required even when having completed this mission
            // for another nation.
            b.State(current: true, when: q => q.Player.MissionStatus(AreaId) <= 1)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens._6r9).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.ProgressEvent(128);
                    }

                    return QuestReply.Event(138, 1);
                })
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Goggehn).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.Event(132);
                    }
                    else
                    {
                        return QuestReply.Event(135);
                    }
                })
                .OnEventFinish(Zone.RuludeGardens, 128, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                    q.Player.GiveKeyItem(KeyItem.LetterToAldo);
                })
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Aldo).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) != 1)
                    {
                        return null;
                    }

                    if (q.Player.HasKeyItem(KeyItem.SilverBell))
                    {
                        return QuestReply.ProgressEvent(152, 1); // CS without Verena nor Fickblix
                    }

                    return QuestReply.ProgressEvent(152, 0, 1); // Regular CS with Verena and Fickblix
                })
                .OnEventFinish(Zone.LowerJeuno, 152, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 2);
                    q.Player.DelKeyItem(KeyItem.LetterToAldo);

                    if (!q.Player.HasKeyItem(KeyItem.SilverBell))
                    {
                        q.Player.GiveKeyItem(KeyItem.SilverBell);
                    }
                });

            // Section 3: Key Item hunt and Magicite obtention. Several steps aren't required or may have been already
            // completed in another nation.
            b.State(current: true, when: q => q.Player.MissionStatus(AreaId) == 2)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens._6r9).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.MagiciteOptistone) ||
                        !q.Player.HasKeyItem(KeyItem.MagiciteAurastone) ||
                        !q.Player.HasKeyItem(KeyItem.MagiciteOrastone))
                    {
                        return QuestReply.Event(138, 1);
                    }

                    // Param 0 swaps the airship pass for gil, param 1 skips the Eald'narche and Wolfgang scene when
                    // set.
                    if (q.Player.HasKeyItem(KeyItem.AirshipPass))
                    {
                        return QuestReply.ProgressEvent(60, 1, 0);
                    }

                    return QuestReply.ProgressEvent(60, 0, 0);
                })
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Goggehn).OnTrigger(q => QuestReply.Event(135))
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
                    q.Player.SetMissionStatus(AreaId, 3);
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
                    q.Mission.SetVar("Option", 2); // Fickbix CS
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

                    int option = q.Mission.Var("Option");

                    // Event 23 is the follow up once the garden scene has played.
                    if (option == 1)
                    {
                        return QuestReply.Event(23);
                    }
                    else if (option == 0)
                    {
                        return QuestReply.ProgressEvent(80);
                    }

                    return null;
                })
                .OnEventFinish(Zone.UpperJeuno, 80, q => q.Mission.SetVar("Option", 1)) // "Yagudo Torch" CS
                .Npc(Zone.AltarRoom, Npc.AltarRoom.Magicite).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MagiciteOrastone))
                    {
                        return null;
                    }

                    if (q.Player.HasKeyItem(KeyItem.MagiciteOptistone) &&
                        q.Player.HasKeyItem(KeyItem.MagiciteAurastone))
                    {
                        // Play Lion part of the CS (Last Magicite Received)
                        return QuestReply.ProgressEvent(44, Zone.AltarRoom, 3);
                    }

                    return QuestReply.ProgressEvent(44, Zone.AltarRoom);
                })
                .OnZoneIn(Zone.AltarRoom, q =>
                    q.Mission.Var("Option") == 2 ? QuestReply.Event(10000) : null) // Fickbix CS
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
                .Npc(Zone.MonasticCavern, Npc.MonasticCavern.Magicite).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.MagiciteOptistone))
                    {
                        return null;
                    }

                    if (q.Player.HasKeyItem(KeyItem.MagiciteAurastone) &&
                        q.Player.HasKeyItem(KeyItem.MagiciteOrastone))
                    {
                        // Play Lion part of the CS (Last Magicite Received)
                        return QuestReply.ProgressEvent(0, 1, 1, 1, 1, 1, 1, 1, 1);
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

                    if (q.Player.HasKeyItem(KeyItem.MagiciteOptistone) &&
                        q.Player.HasKeyItem(KeyItem.MagiciteOrastone))
                    {
                        // Play Lion part of the CS (Last Magicite Received)
                        return QuestReply.ProgressEvent(0, 1, 46, 47);
                    }

                    return QuestReply.ProgressEvent(0, 0, 46, 47);
                })
                .OnEventFinish(Zone.QulunDome, 0, q => q.Player.GiveKeyItem(KeyItem.MagiciteAurastone));

            // Section 4: Magicite given to Jeuno totally-not-evil leader. Finish quest.
            b.State(current: true, when: q => q.Player.MissionStatus(AreaId) == 3)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens._6r2).OnTrigger(q =>
                    QuestReply.MessageSpecial(Text.RuludeGardens.THE_CONSULATE_IS_AWAY))
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Goggehn).OnTrigger(q => QuestReply.ProgressEvent(35))
                .OnEventFinish(Zone.RuludeGardens, 35, q => q.Mission.Complete())
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Aldo).OnTrigger(q => QuestReply.Event(183));
        }
    }
}
