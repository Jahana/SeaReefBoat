// The Empress Crowned (ToAU 47, Log ID 4, Mission ID 46), a line-for-line port of
// scripts/missions/toau/47_The_Empress_Crowned.lua (quest rework C3 T4). One section (the mission current), all in Aht
// Urhgan Whitegate: trigger area 3 plays the progress event 3144 (the mercenary rank, and 1); its update answers 0, 1,
// 0, 0, 1, 1, 0, 0 whatever the option; its finish completes the mission (Eternal Mercenary current, the Glory Crown,
// the Eternal Mercenary title). The Lua passes a ninth updateEvent operand, always 0; the port drops it (the engine
// reads eight, so nothing changes).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class TheEmpressCrowned : MissionScript
    {
        public TheEmpressCrowned() : base(XI.Quests.MissionLog.ToAU, "THE_EMPRESS_CROWNED") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/47_The_Empress_Crowned";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Item = Item.GloryCrown,
                Title = Title.EternalMercenary,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.EternalMercenary,
            });

            b.State(current: true)
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 3, q =>
                {
                    return QuestReply.ProgressEvent(3144, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 3144, q =>
                {
                    q.Player.UpdateEvent(0, 1, 0, 0, 1, 1, 0, 0);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3144, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
