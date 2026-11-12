// Edge of Existence (WotG 40, Log ID 5, Mission ID 39), a line-for-line port of
// scripts/missions/wotg/40_Edge_of_Existence.lua (quest rework C3 T7). Cavernous Maws: Batallia Downs !pos -48 0.1 435
// 105; Rolanberry Fields !pos -198 8 361 110; Sauromugue Champaign !pos 369 8 -227 120; Batallia Downs [S] !pos -48 0
// 435 84; Rolanberry Fields [S] !pos -198 8 360 91; Sauromugue Champaign [S] !pos 369 8 -227 98. One section (the
// mission current): each of the six zones' Cavernous Maw plays its progress event (Batallia Downs 3, Rolanberry
// Fields and Sauromugue Champaign 502, Batallia Downs [S] 7, Rolanberry Fields [S] and Sauromugue Champaign [S] 702,
// each with its own operands), whose finish completes the mission (Her Memories current) and then calls
// xi.maws.goToMaw with the zone's id. Kept as the Lua has it: goToMaw wants a maw table (maw.dest), so each finish
// raises "attempt to index local 'maw' (a number value)" after the completion and the char stays where it is.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class EdgeOfExistence : MissionScript
    {
        public EdgeOfExistence() : base(XI.Quests.MissionLog.WoTG, "EDGE_OF_EXISTENCE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/40_Edge_of_Existence";

        // utils.MAX_UINT32 - n
        static int MaxUint32Minus(uint n) => unchecked((int)(uint.MaxValue - n));

        // each zone's onEventFinish: the completion, then xi.maws.goToMaw(player, <the zone's id>)
        static System.Action<IQuestContext> CompleteAndGo(int zone) => q =>
        {
            if (q.Mission.Complete())
            {
                q.Player.GoToMaw(zone);
            }
        };

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.HerMemories,
            });

            b.State(current: true)
                .Npc(Zone.BatalliaDowns, Npc.BatalliaDowns.CavernousMaw)
                .OnTrigger(QuestReply.ProgressEvent(3, 0, 0, 0, 0, 0, 0, 0, 1))
                .OnEventFinish(Zone.BatalliaDowns, 3, CompleteAndGo(Zone.BatalliaDowns))
                .Npc(Zone.RolanberryFields, Npc.RolanberryFields.CavernousMaw)
                .OnTrigger(QuestReply.ProgressEvent(502, 1, 26, 83764, 22093, 466180, 1822, 648024, 0))
                .OnEventFinish(Zone.RolanberryFields, 502, CompleteAndGo(Zone.RolanberryFields))
                .Npc(Zone.SauromugueChampaign, Npc.SauromugueChampaign.CavernousMaw)
                .OnTrigger(QuestReply.ProgressEvent(502, 2, 0, 0, 0, 0, 0, 0, 1))
                .OnEventFinish(Zone.SauromugueChampaign, 502, CompleteAndGo(Zone.SauromugueChampaign))
                .Npc(Zone.BatalliaDownsS, Npc.BatalliaDownsS.CavernousMaw)
                .OnTrigger(QuestReply.ProgressEvent(7, 0, 0, 0, 0, 0, 0, 3, 1))
                .OnEventFinish(Zone.BatalliaDownsS, 7, CompleteAndGo(Zone.BatalliaDownsS))
                .Npc(Zone.RolanberryFieldsS, Npc.RolanberryFieldsS.CavernousMaw)
                .OnTrigger(QuestReply.ProgressEvent(702, 1, 0, 0, 0, 0, 0, 3, 1))
                .OnEventFinish(Zone.RolanberryFieldsS, 702, CompleteAndGo(Zone.RolanberryFieldsS))
                .Npc(Zone.SauromugueChampaignS, Npc.SauromugueChampaignS.CavernousMaw)
                .OnTrigger(QuestReply.ProgressEvent(702, 2, MaxUint32Minus(7), MaxUint32Minus(1), MaxUint32Minus(1),
                    0, 0, 0, 1))
                .OnEventFinish(Zone.SauromugueChampaignS, 702, CompleteAndGo(Zone.SauromugueChampaignS));
        }
    }
}
