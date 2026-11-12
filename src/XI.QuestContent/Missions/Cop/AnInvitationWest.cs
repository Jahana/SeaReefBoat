// An Invitation West (Promathia 2-1, Log ID 6, Mission ID 138), a line-for-line port of
// scripts/missions/cop/2_1_An_Invitation_West.lua (quest rework C3). One section (the mission current): the Lufaise
// Meadows zone-in at Status 0 plays 110, whose finish sends KI_STOLEN (0, the Mysterious Amulet), takes the amulet and
// sets Status 1; the Tavnazian Safehold zone-in at Status 1 plays 101, whose finish completes the mission. The title
// is Dead Body; The Lost City is next. Kept as the Lua has it: the amulet is taken even when the char lacks it.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class AnInvitationWest : MissionScript
    {
        public AnInvitationWest() : base(XI.Quests.MissionLog.CoP, "AN_INVITATION_WEST") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/2_1_An_Invitation_West";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Title = Title.DeadBody,
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.TheLostCity,
            });

            b.State(current: true)
                .OnZoneIn(Zone.LufaiseMeadows, q => q.Mission.Var("Status") == 0 ? QuestReply.Event(110) : null)
                .OnEventFinish(Zone.LufaiseMeadows, 110, q =>
                {
                    q.Text.Special(Text.LufaiseMeadows.KI_STOLEN, 0, KeyItem.MysteriousAmulet);
                    q.Player.DelKeyItem(KeyItem.MysteriousAmulet);
                    q.Mission.SetVar("Status", 1);
                })
                .OnZoneIn(Zone.TavnazianSafehold, q => q.Mission.Var("Status") == 1 ? QuestReply.Event(101) : null)
                .OnEventFinish(Zone.TavnazianSafehold, 101, q => q.Mission.Complete());
        }
    }
}
