// Forget Me Not (WotG 42, Log ID 5, Mission ID 41), a line-for-line port of
// scripts/missions/wotg/42_Forget_Me_Not.lua (quest rework C3 T8). Cavernous Maws: Batallia Downs !pos -48 0.1 435
// 105; Rolanberry Fields !pos -198 8 361 110; Sauromugue Champaign !pos 369 8 -227 120; Batallia Downs [S] !pos -48 0
// 435 84; Rolanberry Fields [S] !pos -198 8 360 91; Sauromugue Champaign [S] !pos 369 8 -227 98. Section 1 (the
// mission current, Status 0): each of the six zones' Cavernous Maw plays its progress event (Batallia Downs 4,
// Rolanberry Fields and Sauromugue Champaign 503, Batallia Downs [S] 8, Rolanberry Fields [S] and Sauromugue Champaign
// [S] 703, each with its own operands), whose finish sets Status 1 and Option to the char's zone and sets the char down
// at 800 71.839 770 rot 63 in Grauberg [S]. Section 2 (Status 1): Grauberg [S]'s zone-in plays the plain event 33;
// its update with option 1 sends the update (12, 23, 1756, 0, 0, 0, 0, 0); its finish sets Status 2 and sends the char
// through xi.maws.goToMaw to xi.maws.pastMaws[Option] (the maw's destination, the other era's zone). Section 3 (Status
// 2): the zone-in of any of the six zones completes the mission (Pillar of Hope current) and, when it completes, takes
// the four Large Memory Fragments (xi.wotg.helpers.removeMemoryFragments). Kept as the Lua has it: an Option with no
// past maw (0, Status 1 set by other means) hands goToMaw a nil, which raises "attempt to index local 'maw' (a nil
// value)" after Status 2 is set.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class ForgetMeNot : MissionScript
    {
        public ForgetMeNot() : base(XI.Quests.MissionLog.WoTG, "FORGET_ME_NOT") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/42_Forget_Me_Not";

        // completeMissionOnZoneIn: the completion, then the fragments taken
        static QuestReply CompleteMissionOnZoneIn(IQuestContext q)
        {
            if (q.Mission.Complete())
            {
                q.Player.RemoveMemoryFragments();
            }

            return null;
        }

        // mawOnEventFinish: Status 1, Option the char's zone (player:getZoneID(), the handler's zone), set down in
        // Grauberg [S]
        static void MawOnEventFinish(IQuestContext q)
        {
            q.Mission.SetVar("Status", 1);
            q.Mission.SetVar("Option", q.Zone);

            q.Player.SetPos(800, 71.839, 770, 63, Zone.GraubergS);
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.PillarOfHope,
            });

            b.State(current: true, when: q => q.Mission.Var("Status") == 0)
                .Npc(Zone.BatalliaDowns, Npc.BatalliaDowns.CavernousMaw)
                .OnTrigger(QuestReply.ProgressEvent(4, 0, 1, 2964, 1700, 43, 0, 0, 0))
                .OnEventFinish(Zone.BatalliaDowns, 4, MawOnEventFinish)
                .Npc(Zone.RolanberryFields, Npc.RolanberryFields.CavernousMaw)
                .OnTrigger(QuestReply.ProgressEvent(503, 1, 26, 0, 6912, 235686, 1205, 183377, 1))
                .OnEventFinish(Zone.RolanberryFields, 503, MawOnEventFinish)
                .Npc(Zone.SauromugueChampaign, Npc.SauromugueChampaign.CavernousMaw)
                .OnTrigger(QuestReply.ProgressEvent(503, 2, 0, 0, 0, 0, 651, 636808, 1))
                .OnEventFinish(Zone.SauromugueChampaign, 503, MawOnEventFinish)
                .Npc(Zone.BatalliaDownsS, Npc.BatalliaDownsS.CavernousMaw)
                .OnTrigger(QuestReply.ProgressEvent(8, 0, 0, 0, 0, 0, 6553607, 0, 1))
                .OnEventFinish(Zone.BatalliaDownsS, 8, MawOnEventFinish)
                .Npc(Zone.RolanberryFieldsS, Npc.RolanberryFieldsS.CavernousMaw)
                .OnTrigger(QuestReply.ProgressEvent(703, 1, 0, 0, 0, 0, 0, 3, 1))
                .OnEventFinish(Zone.RolanberryFieldsS, 703, MawOnEventFinish)
                .Npc(Zone.SauromugueChampaignS, Npc.SauromugueChampaignS.CavernousMaw)
                .OnTrigger(QuestReply.ProgressEvent(703, 2, 300, 200, 100, 0, 5439495, 0, 1))
                .OnEventFinish(Zone.SauromugueChampaignS, 703, MawOnEventFinish);

            b.State(current: true, when: q => q.Mission.Var("Status") == 1)
                .OnZoneIn(Zone.GraubergS, q => QuestReply.Event(33))
                .OnEventUpdate(Zone.GraubergS, 33, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.UpdateEvent(12, 23, 1756, 0, 0, 0, 0, 0);
                    }
                })
                .OnEventFinish(Zone.GraubergS, 33, q =>
                {
                    q.Mission.SetVar("Status", 2);

                    q.Player.GoToPastMaw(q.Mission.Var("Option"));
                });

            b.State(current: true, when: q => q.Mission.Var("Status") == 2)
                .OnZoneIn(Zone.BatalliaDowns, CompleteMissionOnZoneIn)
                .OnZoneIn(Zone.RolanberryFields, CompleteMissionOnZoneIn)
                .OnZoneIn(Zone.SauromugueChampaign, CompleteMissionOnZoneIn)
                .OnZoneIn(Zone.BatalliaDownsS, CompleteMissionOnZoneIn)
                .OnZoneIn(Zone.RolanberryFieldsS, CompleteMissionOnZoneIn)
                .OnZoneIn(Zone.SauromugueChampaignS, CompleteMissionOnZoneIn);
        }
    }
}
