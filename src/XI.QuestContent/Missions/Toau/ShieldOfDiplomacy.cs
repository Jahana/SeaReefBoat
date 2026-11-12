// Shield of Diplomacy (ToAU 22, Log ID 4, Mission ID 21), a line-for-line port of
// scripts/missions/toau/22_Shield_of_Diplomacy.lua (quest rework C3 T2). One section (the mission current). Aht Urhgan
// Whitegate: Naja Salaheem plays the plain event 3094 (the mercenary rank, and 1). Navukgo Execution Chamber: the
// zone-in at status 0 plays 1, whose finish sets status 1; the entry door (_1s0) at status 1 plays the progress event
// 2, whose finish sets status 2 (the battlefield, Battlefields/NavukgoExecutionChamber/ShieldOfDiplomacy.cs, takes
// over from there); the battlefield's win finish 32001 completes the mission (Social Graces current, the Karababa's
// Bodyguard title) only when the char's battlefieldWin local var is this battlefield's id.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class ShieldOfDiplomacy : MissionScript
    {
        public ShieldOfDiplomacy() : base(XI.Quests.MissionLog.ToAU, "SHIELD_OF_DIPLOMACY") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/22_Shield_of_Diplomacy";

        const int AreaId = MissionLog.Toau;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Title = Title.KarababasBodyguard,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.SocialGraces,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    return QuestReply.Event(3094, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .Npc(Zone.NavukgoExecutionChamber, Npc.NavukgoExecutionChamber._1s0).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        return QuestReply.ProgressEvent(2);
                    }

                    return null;
                })
                .OnZoneIn(Zone.NavukgoExecutionChamber, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.Event(1);
                    }

                    return null;
                })
                .OnEventFinish(Zone.NavukgoExecutionChamber, 1, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                })
                .OnEventFinish(Zone.NavukgoExecutionChamber, 2, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 2);
                })
                .OnEventFinish(Zone.NavukgoExecutionChamber, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.ShieldOfDiplomacy)
                    {
                        q.Mission.Complete();
                    }
                });
        }
    }
}
