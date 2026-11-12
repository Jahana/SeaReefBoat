// Cait Sith (WotG 3, Log ID 5, Mission ID 2), a line-for-line port of scripts/missions/wotg/03_Cait_Sith.lua (quest
// rework C3 T5). One section (the mission current, any status): the zone-in to Southern San d'Oria [S] from East
// Ronfaure [S] plays 67; its finish gives the Lightsworm when the char's Rhapsodies of Vanadiel mission is Cauterize,
// then completes the mission (Cait Sith's Assistant, The Queen of the Dance current). Kept as the Lua has it: no status
// gate (any status plays the zone-in), and the Lightsworm given here is the one Back to the Beginning's reward also
// gives (npcUtil.giveKeyItem skips a key item the char holds, silently: a char who has it gets nothing).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class CaitSith : MissionScript
    {
        public CaitSith() : base(XI.Quests.MissionLog.WoTG, "CAIT_SITH") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/03_Cait_Sith";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Title = Title.CaitSithsAssistant,
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.TheQueenOfTheDance,
            });

            b.State(current: true)
                .OnZoneIn(Zone.SouthernSanDoriaS, q =>
                    q.Player.PreviousZone == Zone.EastRonfaureS ? QuestReply.Event(67) : null)
                .OnEventFinish(Zone.SouthernSanDoriaS, 67, q =>
                {
                    if (q.Player.CurrentMission(MissionLog.Rov) == Mission.Rov.Cauterize)
                    {
                        q.Player.GiveKeyItem(KeyItem.Lightsworm);
                    }

                    q.Mission.Complete();
                });
        }
    }
}
