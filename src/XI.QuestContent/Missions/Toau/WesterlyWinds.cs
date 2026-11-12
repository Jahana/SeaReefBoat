// Westerly Winds (ToAU 7, Log ID 4, Mission ID 6), a line-for-line port of
// scripts/missions/toau/07_Westerly_Winds.lua (quest rework C3 T1). Naja Salaheem !pos 22.700 -8.804 -45.591 50. One
// section (the mission current), all in Aht Urhgan Whitegate. Status 0: Cacaroon plays 3023, Nadeey 3025 (text table 0,
// plain events), Naja 3021 (the mercenary rank), and trigger area 5 plays 3027, whose finish, with a free slot, gives
// the Raillefal's Note, an imperial silver piece and the Agent of the Allied Forces title and sets status 1. Other
// status: Naja plays 3028 (mercenary rank), whose update answers 0 after option 2 (hiding "It's a prince"), else 2,
// with 1 as the second operand; its finish completes the mission (A Mercenary Life current), then takes the note and
// sets the A Mercenary Life mustZone. Kept as the Lua has it: the completion reward also gives an imperial silver piece
// (the second one); the 3027 finish needs only one free slot for the note and the piece (the piece may fail on a full
// bag with the slot taken by the note); the note deletion and mustZone follow only a true complete.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class WesterlyWinds : MissionScript
    {
        public WesterlyWinds() : base(XI.Quests.MissionLog.ToAU, "WESTERLY_WINDS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/07_Westerly_Winds";

        const int AreaId = MissionLog.Toau;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Item = Item.ImperialSilverPiece,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.AMercenaryLife,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Cacaroon).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.Event(3023).TextTable(0);
                    }

                    return null;
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Nadeey).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.Event(3025).TextTable(0);
                    }

                    return null;
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.Event(3021, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                    }

                    return QuestReply.ProgressEvent(3028, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 5, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 0)
                    {
                        return QuestReply.ProgressEvent(3027).TextTable(0);
                    }

                    return null;
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 3028, q =>
                {
                    int param1 = q.Option == 2 ? 0 : 2; // Hides "It's a prince" option after selecting it.
                    q.Player.UpdateEvent(param1, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3027, q =>
                {
                    // Don't change order. In retail, Keyitem is gotten before item.
                    if (q.Player.FreeSlotCount >= 1)
                    {
                        q.Player.GiveKeyItem(KeyItem.RaillefalsNote);
                        q.Player.GiveItem(Item.ImperialSilverPiece);
                        q.Player.SetTitle(Title.AgentOfTheAlliedForces);
                        q.Player.SetMissionStatus(AreaId, 1);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3028, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.RaillefalsNote);
                        q.Player.SetMissionMustZone(AreaId, Mission.Toau.AMercenaryLife);
                    }
                });
        }
    }
}
