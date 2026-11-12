// Sweets for the Soul (ToAU 19, Log ID 4, Mission ID 18), a line-for-line port of
// scripts/missions/toau/19_Sweets_for_the_Soul.lua (quest rework C3 T2). One section (the mission current), all in Aht
// Urhgan Whitegate. Abquhbah, while the SeenAbquhbah var is 0, plays the progress event 3106 (text table 0), whose
// finish sets the var to 1; after that his trigger does nothing. Naja Salaheem plays the plain event 3091 (the
// mercenary rank, and 1). Trigger area 5 plays the progress event 3092 (1, 1), whose finish completes the mission
// (Teahouse Tumult current, the Karababa's Tour Guide title).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class SweetsForTheSoul : MissionScript
    {
        public SweetsForTheSoul() : base(XI.Quests.MissionLog.ToAU, "SWEETS_FOR_THE_SOUL") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/19_Sweets_for_the_Soul";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Title = Title.KarababasTourGuide,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.TeahouseTumult,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Abquhbah).OnTrigger(q =>
                {
                    if (q.Mission.Var("SeenAbquhbah") == 0)
                    {
                        return QuestReply.ProgressEvent(3106).TextTable(0);
                    }

                    return null;
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    return QuestReply.Event(3091, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 5, q =>
                {
                    return QuestReply.ProgressEvent(3092, 1, 1, 0, 0, 0, 0, 0, 0);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3092, q =>
                {
                    q.Mission.Complete();
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3106, q =>
                {
                    q.Mission.SetVar("SeenAbquhbah", 1);
                });
        }
    }
}
