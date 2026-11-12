// The Temple of Uggalepih (Zilart 4, Log ID 3, Mission ID 8), a line-for-line port of
// scripts/missions/rotz/04_The_Temple_of_Uggalepih.lua (quest rework C3, C5). Gilgamesh !pos 122.452 -9.009 -12.052
// 252; Jakoh Wahcondalo !pos 101 -16 -115 250; Mahogany Door (BCNM) !pos 299 0.1 349 163. Section 1 (current): Norg's
// Gilgamesh plays 8 and Kazham's Jakoh Wahcondalo 115. The Sacrificial Chamber zone-in plays 7 at status 1 and 8 at
// status 2; both updates on option 0 answer (0, 23, 1756, 0, 163, 0, 0, 0). The 32001 finish of a Temple of Uggalepih
// win (Battlefields/SacrificialChamber/TempleOfUggalepih.cs sets battlefieldWin) sets status 1 and moves the char to
// (-329.762, -0.015, -300.172, 127) in the chamber, whose zone-in plays 7; 7's finish sets status 2 and moves it there
// again, whose zone-in plays 8; 8's finish completes the mission (the Dark Fragment, the Bearer of the Wisewoman's Hope
// title, Headstone Pilgrimage next), takes the Sacrificial Chamber Key and, on the RoV log's The Cursed Temple,
// completes that and adds Wisdom of Our Forefathers. Section 2 (another Zilart mission current, battlefieldWin the
// Temple of Uggalepih): the 32001 finish gives the title. Kept as the Lua has it: battlefieldWin is set only while the
// mission is current (BattlefieldMission:onBattlefieldWin), so section 2 never answers a real win; the 8's finish
// tests mission:complete's result, always true (the reward has no item).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Zilart
{
    public sealed class TheTempleOfUggalepih : MissionScript
    {
        public TheTempleOfUggalepih() : base(XI.Quests.MissionLog.Zilart, "THE_TEMPLE_OF_UGGALEPIH") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/rotz/04_The_Temple_of_Uggalepih";

        // mission.areaId
        const int AreaId = MissionLog.Zilart;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.DarkFragment,
                Title = Title.BearerOfTheWisewomansHope,
                NextLog = XI.Quests.MissionLog.Zilart,
                NextMission = Mission.Zilart.HeadstonePilgrimage,
            });

            b.State(current: true)
                .Npc(Zone.Norg, Npc.Norg.Gilgamesh).OnTrigger(QuestReply.Event(8))
                // NOTE: This CS is blocked by Evisceration quest if it is active, and will not be displayed.
                .Npc(Zone.Kazham, Npc.Kazham.JakohWahcondalo).OnTrigger(QuestReply.Event(115))
                .OnZoneIn(Zone.SacrificialChamber, q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 1)
                    {
                        return QuestReply.Event(7);
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.Event(8);
                    }

                    return null;
                })
                .OnEventUpdate(Zone.SacrificialChamber, 7, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.UpdateEvent(0, 23, 1756, 0, 163, 0, 0, 0);
                    }
                })
                .OnEventUpdate(Zone.SacrificialChamber, 8, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.UpdateEvent(0, 23, 1756, 0, 163, 0, 0, 0);
                    }
                })
                .OnEventFinish(Zone.SacrificialChamber, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.TempleOfUggalepih)
                    {
                        q.Player.SetMissionStatus(AreaId, 1);
                        q.Player.SetPos(-329.762, -0.015, -300.172, 127, Zone.SacrificialChamber);
                    }
                })
                .OnEventFinish(Zone.SacrificialChamber, 7, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 2);
                    q.Player.SetPos(-329.762, -0.015, -300.172, 127, Zone.SacrificialChamber);
                })
                .OnEventFinish(Zone.SacrificialChamber, 8, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.SacrificialChamberKey);

                        // NOTE: RoV mission transition occurs if the player is on Cursed Temple following the
                        // completion of this event.
                        if (q.Player.CurrentMission(MissionLog.Rov) == Mission.Rov.TheCursedTemple)
                        {
                            q.Player.CompleteMissionRaw(MissionLog.Rov, Mission.Rov.TheCursedTemple);
                            q.Player.AddMission(MissionLog.Rov, Mission.Rov.WisdomOfOurForefathers);
                        }
                    }
                });

            // Players not on mission should still receive BCNM title
            b.State(current: false,
                    when: q => q.Player.LocalVar("battlefieldWin") == Battlefield.TempleOfUggalepih)
                .OnEventFinish(Zone.SacrificialChamber, 32001, q =>
                    q.Player.AddTitle(Title.BearerOfTheWisewomansHope));
        }
    }
}
