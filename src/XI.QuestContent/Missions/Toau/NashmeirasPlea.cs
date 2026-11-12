// Nashmeira's Plea (ToAU 44, Log ID 4, Mission ID 43), a line-for-line port of
// scripts/missions/toau/44_Nashmeiras_Plea.lua (quest rework C3 T4). One section (the mission current). Aht Urhgan
// Whitegate: Naja Salaheem plays the progress event 3156 without the Mythril Mirror key item (its finish gives it),
// else the plain event 3149 (both the mercenary rank, and 1). Alzadaal Undersea Ruins: blank_lamp at status 0 plays the
// progress event 8 (0, 4, 0, 0, 0, 1, 0, 0), whose finish with option 0 sets status 1; the zone-in at status 2 plays
// 10, whose finish completes the mission (Ragnarok current, the Preventer of Ragnarok title). The finish of 116 (the
// Runic Seal's event, a workaround in the Lua) with option 1 and the INSTANCE_ID local var 7701 runs
// xi.instance.onEventFinish, which stays Lua; the bridge passes nil for the npc, as nothing in it reads the npc.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class NashmeirasPlea : MissionScript
    {
        public NashmeirasPlea() : base(XI.Quests.MissionLog.ToAU, "NASHMEIRAS_PLEA") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/44_Nashmeiras_Plea";

        const int AreaId = MissionLog.Toau;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Title = Title.PreventerOfRagnarok,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.Ragnarok,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.MythrilMirror))
                    {
                        return QuestReply.ProgressEvent(3156, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                    }

                    return QuestReply.Event(3149, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3156, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.MythrilMirror);
                })
                .Npc(Zone.AlzadaalUnderseaRuins, Npc.AlzadaalUnderseaRuins.blanklamp).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.ProgressEvent(8, 0, 4, 0, 0, 0, 1, 0, 0);
                    }

                    return null;
                })
                .OnZoneIn(Zone.AlzadaalUnderseaRuins, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 2)
                    {
                        return QuestReply.Event(10);
                    }

                    return null;
                })
                .OnEventFinish(Zone.AlzadaalUnderseaRuins, 8, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.SetMissionStatus(AreaId, 1);
                    }
                })
                .OnEventFinish(Zone.AlzadaalUnderseaRuins, 10, q =>
                {
                    q.Mission.Complete();
                })
                .OnEventFinish(Zone.AlzadaalUnderseaRuins, 116, q =>
                {
                    if (q.Option == 1 && q.Player.LocalVar("INSTANCE_ID") == 7701)
                    {
                        q.Player.InstanceOnEventFinish(116, q.Option);
                    }
                });
        }
    }
}
