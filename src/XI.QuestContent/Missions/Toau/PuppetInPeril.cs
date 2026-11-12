// Puppet in Peril (ToAU 29, Log ID 4, Mission ID 28), a line-for-line port of
// scripts/missions/toau/29_Puppet_in_Peril.lua (quest rework C3 T3). One section (the mission current). Aht Urhgan
// Whitegate: Naja Salaheem plays the plain event 3117 (the mercenary rank and 1). Jade Sepulcher: the entry door
// (_1v0) at status 0 plays the progress event 4 (a 4 at the second operand), whose finish sets status 1; at any other
// status the mission plays nothing (the battlefield, Battlefields/JadeSepulcher/PuppetInPeril.cs, takes over from
// there); the battlefield's win finish 32001 completes the mission (Prevalence of Pirates current) only when the
// char's battlefieldWin local var is this battlefield's id.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class PuppetInPeril : MissionScript
    {
        public PuppetInPeril() : base(XI.Quests.MissionLog.ToAU, "PUPPET_IN_PERIL") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/29_Puppet_in_Peril";

        const int AreaId = MissionLog.Toau;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.PrevalenceOfPirates,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    return QuestReply.Event(3117, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .Npc(Zone.JadeSepulcher, Npc.JadeSepulcher._1v0).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.ProgressEvent(4, 0, 4, 0, 0, 0, 0, 0, 0);
                    }

                    return null;
                })
                .OnEventFinish(Zone.JadeSepulcher, 4, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                })
                .OnEventFinish(Zone.JadeSepulcher, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.PuppetInPeril)
                    {
                        q.Mission.Complete();
                    }
                });
        }
    }
}
