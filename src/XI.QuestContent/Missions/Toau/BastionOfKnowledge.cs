// Bastion of Knowledge (ToAU 28, Log ID 4, Mission ID 27), a line-for-line port of
// scripts/missions/toau/28_Bastion_of_Knowledge.lua (quest rework C3 T3). One section (the mission current), all in Aht
// Urhgan Whitegate. Naja Salaheem plays the plain event 3116 (the mercenary rank and 1; the same event as Misplaced
// Nobility's). Trigger area 4 plays the progress event 3112 (a 1 at the first and second operands), whose finish
// completes the mission (Puppet in Peril current, the Aphmau's Mercenary title).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class BastionOfKnowledge : MissionScript
    {
        public BastionOfKnowledge() : base(XI.Quests.MissionLog.ToAU, "BASTION_OF_KNOWLEDGE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/28_Bastion_of_Knowledge";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Title = Title.AphmausMercenary,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.PuppetInPeril,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    return QuestReply.Event(3116, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 4, q =>
                {
                    return QuestReply.ProgressEvent(3112, 1, 1, 0, 0, 0, 0, 0, 0);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3112, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
