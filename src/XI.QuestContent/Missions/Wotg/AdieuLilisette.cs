// Adieu, Lilisette (WotG 38, Log ID 5, Mission ID 37), a line-for-line port of
// scripts/missions/wotg/38_Adieu_Lilisette.lua (quest rework C3 T7). Lion Springs Door !pos 96 0 106 80. One section
// (the mission current and xi.wotg.helpers.meetsMission38Reqs, which stays Lua), in Southern San d'Oria [S]: Lion
// Springs plays the progress event 166 (the campaign allegiance), whose finish completes the mission (By the Fading
// Light current); Rholont at allegiance 1 plays the plain event 674 (80, 23). Kept as the Lua has it: the whole section
// is gated by meetsMission38Reqs (a char with none of Bonds of Mythril, Face of the Future and At Journey's End gets
// none of it); Rholont's event 674 only at allegiance 1 (the Lua's TODO).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class AdieuLilisette : MissionScript
    {
        public AdieuLilisette() : base(XI.Quests.MissionLog.WoTG, "ADIEU_LILISETTE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/38_Adieu_Lilisette";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.ByTheFadingLight,
            });

            b.State(current: true, when: q => q.Player.WotgMeetsMissionReqs(38))
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.LionSprings)
                .OnTrigger(q => QuestReply.ProgressEvent(166, q.Player.CampaignAllegiance))
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Rholont).OnTrigger(q =>
                {
                    // the Lua's TODO: check whether the message changes for another allegiance, with or without Face
                    // of the Future
                    if (q.Player.CampaignAllegiance == 1)
                    {
                        return QuestReply.Event(674, 80, 23);
                    }

                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 166, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
