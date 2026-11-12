// Light of Judgment (ToAU 41, Log ID 4, Mission ID 40), a line-for-line port of
// scripts/missions/toau/41_Light_of_Judgement.lua (quest rework C3 T4). One section (the mission current), all in Aht
// Urhgan Whitegate. Naja Salaheem plays the plain event 3148 (the mercenary rank, and 1). Rodin-Comidin plays the
// progress event 3137 (text table 0), whose finish with option 0 completes the mission (Path of Darkness current, the
// Nyzul Isle Route key item); any other option does nothing.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class LightOfJudgement : MissionScript
    {
        public LightOfJudgement() : base(XI.Quests.MissionLog.ToAU, "LIGHT_OF_JUDGMENT") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/41_Light_of_Judgement";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.NyzulIsleRoute,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.PathOfDarkness,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    return QuestReply.Event(3148, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.RodinComidin).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(3137).TextTable(0);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3137, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Mission.Complete();
                    }
                });
        }
    }
}
