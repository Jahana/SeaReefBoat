// Cavernous Maws (WotG 1, Log ID 5, Mission ID 0), a line-for-line port of scripts/missions/wotg/01_Cavernous_Maws.lua
// (quest rework C3 T5). Batallia Downs !pos -48 0.1 435 105; Rolanberry Fields !pos -198 8 361 110; Sauromugue
// Champaign !pos 369 8 -227 120. One section (the mission current while xi.settings.main.ENABLE_WOTG is 1): each
// zone's Cavernous Maw plays 500 with its own index (0, 1, 2) as the one operand, and the finish completes the mission
// (the Pure White Feather, Back to the Beginning current) and then sends the char to the destination of a random past
// maw (the other era's zone). As the Lua has it: ENABLE_WOTG is compared as Lua compares it (Settings.MainEquals); the
// random maw is xi.maws.gotoRandomMaw's (Batallia Downs, Rolanberry Fields or Sauromugue Champaign).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class CavernousMaws : MissionScript
    {
        public CavernousMaws() : base(XI.Quests.MissionLog.WoTG, "CAVERNOUS_MAWS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/01_Cavernous_Maws";

        // each zone's onEventFinish[500]
        static void CompleteMission(IQuestContext q)
        {
            if (q.Mission.Complete())
            {
                q.Player.GotoRandomMaw();
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.PureWhiteFeather,
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.BackToTheBeginning,
            });

            b.State(current: true, when: q => q.Settings.MainEquals("ENABLE_WOTG", 1))
                .Npc(Zone.BatalliaDowns, Npc.BatalliaDowns.CavernousMaw).OnTrigger(QuestReply.ProgressEvent(500, 0))
                .OnEventFinish(Zone.BatalliaDowns, 500, CompleteMission)
                .Npc(Zone.RolanberryFields, Npc.RolanberryFields.CavernousMaw)
                .OnTrigger(QuestReply.ProgressEvent(500, 1))
                .OnEventFinish(Zone.RolanberryFields, 500, CompleteMission)
                .Npc(Zone.SauromugueChampaign, Npc.SauromugueChampaign.CavernousMaw)
                .OnTrigger(QuestReply.ProgressEvent(500, 2))
                .OnEventFinish(Zone.SauromugueChampaign, 500, CompleteMission);
        }
    }
}
