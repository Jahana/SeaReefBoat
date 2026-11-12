// Back to the Beginning (WotG 2, Log ID 5, Mission ID 1), a line-for-line port of
// scripts/missions/wotg/02_Back_to_the_Beginning.lua (quest rework C3 T5). One section: the mission current and
// xi.wotg.helpers.meetsMission3Reqs (scripts/missions/wotg/helpers.lua, which stays Lua; a char with Claws of the
// Griffon, The Tigress Strikes or Fires of Discontent done, or the decoupling module's always true). The six maws
// (Batallia Downs, Rolanberry Fields and Sauromugue Champaign, each present and [S]) play 501 (the present zones) or
// 701 (the [S] zones) with the zone's index and 1, and the finish completes the mission (the Lightsworm, Cait Sith
// current) and then adds the maw and sends the char to its destination. As the Lua has it: the guard calls
// meetsMission3Reqs, which the Lua file lists as this mission's three quests (the helper's name is one ahead of the
// mission); xi.maws.addMaw is the helper's.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class BackToTheBeginning : MissionScript
    {
        public BackToTheBeginning() : base(XI.Quests.MissionLog.WoTG, "BACK_TO_THE_BEGINNING") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/02_Back_to_the_Beginning";

        // completeMission
        static void CompleteMission(IQuestContext q)
        {
            if (q.Mission.Complete())
            {
                q.Player.AddMaw();
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.Lightsworm,
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.CaitSith,
            });

            b.State(current: true, when: q => q.Player.WotgMeetsMissionReqs(3))
                .Npc(Zone.BatalliaDowns, Npc.BatalliaDowns.CavernousMaw).OnTrigger(QuestReply.ProgressEvent(501, 0, 1))
                .OnEventFinish(Zone.BatalliaDowns, 501, CompleteMission)
                .Npc(Zone.RolanberryFields, Npc.RolanberryFields.CavernousMaw)
                .OnTrigger(QuestReply.ProgressEvent(501, 1, 1))
                .OnEventFinish(Zone.RolanberryFields, 501, CompleteMission)
                .Npc(Zone.SauromugueChampaign, Npc.SauromugueChampaign.CavernousMaw)
                .OnTrigger(QuestReply.ProgressEvent(501, 2, 1))
                .OnEventFinish(Zone.SauromugueChampaign, 501, CompleteMission)
                .Npc(Zone.BatalliaDownsS, Npc.BatalliaDownsS.CavernousMaw)
                .OnTrigger(QuestReply.ProgressEvent(701, 0, 1))
                .OnEventFinish(Zone.BatalliaDownsS, 701, CompleteMission)
                .Npc(Zone.RolanberryFieldsS, Npc.RolanberryFieldsS.CavernousMaw)
                .OnTrigger(QuestReply.ProgressEvent(701, 1, 1))
                .OnEventFinish(Zone.RolanberryFieldsS, 701, CompleteMission)
                .Npc(Zone.SauromugueChampaignS, Npc.SauromugueChampaignS.CavernousMaw)
                .OnTrigger(QuestReply.ProgressEvent(701, 2, 1))
                .OnEventFinish(Zone.SauromugueChampaignS, 701, CompleteMission);
        }
    }
}
