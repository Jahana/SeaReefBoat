// Guests of the Empire (ToAU 17, Log ID 4, Mission ID 16), a line-for-line port of
// scripts/missions/toau/17_Guests_of_the_Empire.lua (quest rework C3 T2). Naja Salaheem !pos 22.700 -8.804 -45.591 50,
// Imperial Whitegate !pos 152 -2 0 50. One section (the mission current); properlyDressed is the Royal Palace armor
// check (the facet RoyalPalaceArmorCheck) as 1 or 0. The Imperial Whitegate at status 2 with the main and sub slots
// empty and the armor right plays the progress event 3078, else the plain event 3081. Naja at status 0 plays the
// progress event 3076; at status 1 the same when dressed, else the plain event 3080; at status 2 the progress event
// 3077; at any other status nothing. The dress-up updates of 3076 are NajaDressUp's. 3076's finish with option 0 sets
// status 2, else status 1 and clears the dress-up var; 3078's finish completes the mission (Passing Glory current, an
// Imperial Mythril Piece, the Ovjang's Errand Runner title) and, when it did, sets Passing Glory's mustZone local var
// and the Timer char var to the next Vana'diel day. Kept as the Lua has it: the finish sets the next mission's mustZone
// as a local var, which getMustZone (a char var) never reads, so only the Timer delays the next mission. The ROV era
// module (Modules/Era/Missions/MissionAdjustments.cs) replaces the 3078 finish.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class GuestsOfTheEmpire : MissionScript
    {
        public GuestsOfTheEmpire() : base(XI.Quests.MissionLog.ToAU, "GUESTS_OF_THE_EMPIRE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/17_Guests_of_the_Empire";

        const int AreaId = MissionLog.Toau;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Item = Item.ImperialMythrilPiece,
                Title = Title.OvjangsErrandRunner,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.PassingGlory,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.ImperialWhitegate).OnTrigger(q =>
                {
                    int properlyDressed = q.Player.RoyalPalaceArmorCheck() ? 1 : 0;

                    if (q.Player.MissionStatus(AreaId) == 2 &&
                        q.Player.EquipId(Slot.Main) == 0 &&
                        q.Player.EquipId(Slot.Sub) == 0 &&
                        properlyDressed == 1)
                    {
                        return QuestReply.ProgressEvent(3078, 0, 1, 0, 0, 0, 0, 0, properlyDressed, 0);
                    }

                    return QuestReply.Event(3081, 0, 2, 1, 0, 1, 0, 0, properlyDressed, 0);
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    int properlyDressed = q.Player.RoyalPalaceArmorCheck() ? 1 : 0;
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    // First interaction. If you are properly dressed from the get go, you skip playing dress-up again.
                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressEvent(3076, q.Player.MercenaryRank,
                            0, 0, 0, 0, 0, 0, properlyDressed, 0);
                    }
                    // You played dress-up again. Don't repeat.
                    else if (missionStatus == 1)
                    {
                        if (properlyDressed == 1)
                        {
                            return QuestReply.ProgressEvent(3076, q.Player.MercenaryRank,
                                0, 0, 0, 0, 0, 0, properlyDressed, 0);
                        }

                        return QuestReply.Event(3080, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, properlyDressed, 0);
                    }
                    // Success.
                    else if (missionStatus == 2)
                    {
                        return QuestReply.ProgressEvent(3077, q.Player.MercenaryRank,
                            0, 0, 0, 0, 0, 0, properlyDressed, 0);
                    }

                    return null;
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 3076, NajaDressUp.Update)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3076, q =>
                {
                    // Naja approved your gear.
                    if (q.Option == 0)
                    {
                        q.Player.SetMissionStatus(AreaId, 2);
                    }
                    // Played dress-up. Mark to not repeat it.
                    else
                    {
                        q.Player.SetMissionStatus(AreaId, 1);
                        q.Mission.SetLocalVar("najaDressUp", 0);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3078, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.SetLocalVar("Mission[4][17]mustZone", 1);
                        q.Player.SetCharVar("Mission[4][17]Timer", q.Player.VanadielUniqueDay + 1);
                    }
                });
        }
    }
}
