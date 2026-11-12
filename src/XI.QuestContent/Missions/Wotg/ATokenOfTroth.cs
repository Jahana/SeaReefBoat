// A Token of Troth (WotG 53, Log ID 5, Mission ID 52), a line-for-line port of
// scripts/missions/wotg/53_Token_of_Troth.lua (quest rework C3 T8). Bulwark Gate !pos -447.174 -1.831 342.417 98. One
// section (the mission current), in Sauromugue Champaign [S]: with a main or sub weapon equipped the Bulwark Gate plays
// the plain event 117 (0, 23, 1756, 0, 0, 0, 1, 1); else at Status 0 the progress event 115 (98, 5, 1756, 0, 0, 1, 0),
// whose finish sets Status 1; else the progress event 116 (98, 5, 1756, 0, 0, 0, 1, 0), whose finish completes the
// mission (Lest We Forget current) and then, when it completed, takes the Wedding Invitation and sets Lest We
// Forget's Status to 1 (the Lua's note: reset to 0 when the Moonshade Earring is claimed).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class ATokenOfTroth : MissionScript
    {
        public ATokenOfTroth() : base(XI.Quests.MissionLog.WoTG, "A_TOKEN_OF_TROTH") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/53_Token_of_Troth";

        const int AreaId = MissionLog.Wotg;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.LestWeForget,
            });

            b.State(current: true)
                .Npc(Zone.SauromugueChampaignS, Npc.SauromugueChampaignS.BulwarkGate).OnTrigger(q =>
                {
                    if (q.Player.EquipId(Slot.Main) != 0 ||
                        q.Player.EquipId(Slot.Sub) != 0)
                    {
                        return QuestReply.Event(117, 0, 23, 1756, 0, 0, 0, 1, 1);
                    }
                    else if (q.Mission.Var("Status") == 0)
                    {
                        return QuestReply.ProgressEvent(115, 98, 5, 1756, 0, 0, 1, 0);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(116, 98, 5, 1756, 0, 0, 0, 1, 0);
                    }
                })
                .OnEventFinish(Zone.SauromugueChampaignS, 115, q =>
                {
                    q.Mission.SetVar("Status", 1);
                })
                .OnEventFinish(Zone.SauromugueChampaignS, 116, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.WeddingInvitation);

                        // the Lua's note: reset to 0 when the Moonshade Earring is claimed, so no var stays forever
                        q.Player.SetMissionVar(AreaId, Mission.Wotg.LestWeForget, "Status", 1);
                    }
                });
        }
    }
}
