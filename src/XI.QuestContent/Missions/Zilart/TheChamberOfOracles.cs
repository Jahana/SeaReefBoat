// The Chamber of Oracles (Zilart 7, Log ID 3, Mission ID 14), a line-for-line port of
// scripts/missions/rotz/07_The_Chamber_of_Oracles.lua (quest rework C3, C5). !pos 200.3419 -2.25 37.12 168. Section 1
// (not current, not completed): the eight Chamber of Oracles pedestals say PLACED_INTO_THE_PEDESTAL. Section 2
// (current): each pedestal owns one bit of the Zilart status (Fire 1, Darkness 2, Earth 4, Ice 8, Light 16,
// Lightning 32, Water 64, Wind 128). At status 255 it plays 1; with its bit clear it adds the bit, says YOU_PLACE_THE
// (its fragment) and plays 1 once the status reaches 255; with its bit set it says IS_SET_IN_THE_PEDESTAL (its
// fragment). 1's finish takes the eight fragments (Fire to Dark, by id) and completes the mission (the Prismatic
// Fragment, the Lightweaver title, Return to Delkfutt's Tower next). Section 3 (completed): each pedestal says
// HAS_LOST_ITS_POWER (its fragment). Kept as the Lua has it: the pedestals do not test the fragment is held; the bit
// is added to the status as a sum (missionStatus + statusIncrease), safe only because the bit was clear.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Zilart
{
    public sealed class TheChamberOfOracles : MissionScript
    {
        public TheChamberOfOracles() : base(XI.Quests.MissionLog.Zilart, "THE_CHAMBER_OF_ORACLES") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/rotz/07_The_Chamber_of_Oracles";

        static QuestReply HandleActiveOnTrigger(IQuestContext q, int keyItemId, int statusIncrease)
        {
            int missionStatus = q.Player.MissionStatus(MissionLog.Zilart);

            if (missionStatus == 255) // Execute cutscene if the player is interrupted.
            {
                return QuestReply.Event(1);
            }
            else if ((missionStatus & statusIncrease) == 0)
            {
                // Fragments are not removed until the final cutscene ends.
                q.Player.SetMissionStatus(MissionLog.Zilart, missionStatus + statusIncrease);
                q.Text.Special(Text.ChamberOfOracles.YOU_PLACE_THE, keyItemId);

                if (q.Player.MissionStatus(MissionLog.Zilart) == 255)
                {
                    return QuestReply.Event(1);
                }
            }
            else
            {
                return QuestReply.MessageSpecial(Text.ChamberOfOracles.IS_SET_IN_THE_PEDESTAL, keyItemId);
            }

            return null;
        }

        static QuestReply Placed() => QuestReply.MessageSpecial(Text.ChamberOfOracles.PLACED_INTO_THE_PEDESTAL);

        static QuestReply LostPower(int keyItemId) =>
            QuestReply.MessageSpecial(Text.ChamberOfOracles.HAS_LOST_ITS_POWER, keyItemId);

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.PrismaticFragment,
                Title = Title.Lightweaver,
                NextLog = XI.Quests.MissionLog.Zilart,
                NextMission = Mission.Zilart.ReturnToDelkfuttsTower,
            });

            // Section: Mission not Active or Completed
            b.State(current: false,
                    when: q => !q.Player.HasCompletedMission(MissionLog.Zilart, Mission.Zilart.TheChamberOfOracles))
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofDarkness).OnTrigger(Placed())
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofEarth).OnTrigger(Placed())
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofFire).OnTrigger(Placed())
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofIce).OnTrigger(Placed())
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofLight).OnTrigger(Placed())
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofLightning).OnTrigger(Placed())
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofWater).OnTrigger(Placed())
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofWind).OnTrigger(Placed());

            // Section: Mission is Active
            b.State(current: true)
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofDarkness)
                    .OnTrigger(q => HandleActiveOnTrigger(q, KeyItem.DarkFragment, 2))
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofEarth)
                    .OnTrigger(q => HandleActiveOnTrigger(q, KeyItem.EarthFragment, 4))
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofFire)
                    .OnTrigger(q => HandleActiveOnTrigger(q, KeyItem.FireFragment, 1))
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofIce)
                    .OnTrigger(q => HandleActiveOnTrigger(q, KeyItem.IceFragment, 8))
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofLight)
                    .OnTrigger(q => HandleActiveOnTrigger(q, KeyItem.LightFragment, 16))
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofLightning)
                    .OnTrigger(q => HandleActiveOnTrigger(q, KeyItem.LightningFragment, 32))
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofWater)
                    .OnTrigger(q => HandleActiveOnTrigger(q, KeyItem.WaterFragment, 64))
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofWind)
                    .OnTrigger(q => HandleActiveOnTrigger(q, KeyItem.WindFragment, 128))
                .OnEventFinish(Zone.ChamberOfOracles, 1, q =>
                {
                    for (int fragment = KeyItem.FireFragment; fragment <= KeyItem.DarkFragment; fragment++)
                    {
                        q.Player.DelKeyItem(fragment);
                    }

                    q.Mission.Complete();
                });

            // Section: Mission has been Completed
            b.State(current: null,
                    when: q => q.Player.HasCompletedMission(MissionLog.Zilart, Mission.Zilart.TheChamberOfOracles))
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofDarkness)
                    .OnTrigger(LostPower(KeyItem.DarkFragment))
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofEarth)
                    .OnTrigger(LostPower(KeyItem.EarthFragment))
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofFire)
                    .OnTrigger(LostPower(KeyItem.FireFragment))
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofIce)
                    .OnTrigger(LostPower(KeyItem.IceFragment))
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofLight)
                    .OnTrigger(LostPower(KeyItem.LightFragment))
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofLightning)
                    .OnTrigger(LostPower(KeyItem.LightningFragment))
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofWater)
                    .OnTrigger(LostPower(KeyItem.WaterFragment))
                .Npc(Zone.ChamberOfOracles, Npc.ChamberOfOracles.PedestalofWind)
                    .OnTrigger(LostPower(KeyItem.WindFragment));
        }
    }
}
