// Fangs of the Lion (ToAU 43, Log ID 4, Mission ID 42), a line-for-line port of
// scripts/missions/toau/43_Fangs_of_the_Lion.lua (quest rework C3 T4). One section (the mission current), all in Aht
// Urhgan Whitegate. Trigger area 3 plays the progress event 3138 (the mercenary rank, and 1); its update answers 0, 1
// whatever the option; its finish completes the mission (Nashmeira's Plea current, the Mythril Mirror key item, the
// Nashmeira's Loyalist title). The Lua passes a ninth updateEvent operand, always 0; the port drops it (the engine
// reads eight, so nothing changes).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class FangsOfTheLion : MissionScript
    {
        public FangsOfTheLion() : base(XI.Quests.MissionLog.ToAU, "FANGS_OF_THE_LION") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/43_Fangs_of_the_Lion";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.MythrilMirror,
                Title = Title.NashmeirasLoyalist,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.NashmeirasPlea,
            });

            b.State(current: true)
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 3, q =>
                {
                    // Your mercenary rank is mentioned in the CS.
                    return QuestReply.ProgressEvent(3138, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 3138, q =>
                {
                    q.Player.UpdateEvent(0, 1, 0, 0, 0, 0, 0, 0);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3138, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
