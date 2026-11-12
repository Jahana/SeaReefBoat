// Testing the Waters (ToAU 34, Log ID 4, Mission ID 33), a line-for-line port of
// scripts/missions/toau/34_Testing_the_Waters.lua (quest rework C3 T3). One section (the mission current). Aht Urhgan
// Whitegate: Naja Salaheem plays the plain event 3132 (the mercenary rank and 1). Arrapago Reef: trigger area 1, with
// the mustZone clear and the Ephramadian Gold Coin held (the two parts of its condition), plays the progress event 15;
// its finish with option 1 sets status 1 and puts the char at -88.879 -7.318 -109.233 (rotation 173) in Talacca Cove;
// any other option is the wrong answer and sets the mustZone. Talacca Cove: the zone-in at status 1 plays the plain
// event 106, whose finish completes the mission (Legacy of the Lost current, the Percipient Eye key item, the Treasure
// Trove Tender title) and, when it did, takes the coin.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class TestingTheWaters : MissionScript
    {
        public TestingTheWaters() : base(XI.Quests.MissionLog.ToAU, "TESTING_THE_WATERS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/34_Testing_the_Waters";

        const int AreaId = MissionLog.Toau;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.PercipientEye,
                Title = Title.TreasureTroveTender,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.LegacyOfTheLost,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    return QuestReply.Event(3132, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .OnTriggerAreaEnter(Zone.ArrapagoReef, 1, q =>
                {
                    if (!q.Mission.MustZone && q.Player.HasKeyItem(KeyItem.EphramadianGoldCoin))
                    {
                        return QuestReply.ProgressEvent(15, 0, 0, 0, 0, 0, 0, 0, 0);
                    }

                    return null;
                })
                .OnEventFinish(Zone.ArrapagoReef, 15, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.SetMissionStatus(AreaId, 1);
                        q.Player.SetPos(-88.879, -7.318, -109.233, 173, Zone.TalaccaCove);
                    }
                    else
                    {
                        // WRONG answer, you must zone and try again.
                        q.Mission.SetMustZone();
                    }
                })
                .OnZoneIn(Zone.TalaccaCove, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 1)
                    {
                        return QuestReply.Event(106);
                    }

                    return null;
                })
                .OnEventFinish(Zone.TalaccaCove, 106, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.EphramadianGoldCoin);
                    }
                });
        }
    }
}
