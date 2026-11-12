// Knight of Gold (ToAU 4, Log ID 4, Mission ID 3), a line-for-line port of
// scripts/missions/toau/04_Knight_of_Gold.lua (quest rework C3 T1). Cacaroon !pos -72 0 -86 50; Walahra Temple !pos 80
// -0.3 22 50; Teahouse !pos 80 -6 -117 50. One section (the mission current), all in Aht Urhgan Whitegate. Status 0 or
// 1:
// Cacaroon plays 3035 (the status as its eighth operand), whose finish sets status 1 on option 0, else 2. Status 2:
// trading him 1000 gil or an imperial bronze piece plays 3022, whose finish completes the trade and sets status 3; his
// trigger plays 3036 (not a progress event). Status 3: trigger area 4 plays 3024, whose finish sets status 4; Nadeey
// plays 3025 from status 3 on. Status 4: trigger area 5 plays 3026, whose update keeps the topics read in the mission
// local var Option (bit option-1, clamped to 1..4 first) and answers them with the update 1, and whose finish
// completes the mission (Confessions of Royalty current, the Raillefal's Letter). Naja plays 3021 with the mercenary
// rank (a plain event). Kept as the Lua has it: the completion does not test the three topics (the update only reports
// them), the gil trade is taken by tradeComplete whichever of the two is given.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class KnightOfGold : MissionScript
    {
        public KnightOfGold() : base(XI.Quests.MissionLog.ToAU, "KNIGHT_OF_GOLD") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/04_Knight_of_Gold";

        const int AreaId = MissionLog.Toau;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                KeyItem = KeyItem.RaillefalsLetter,
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.ConfessionsOfRoyalty,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Cacaroon).OnTrade(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 2 &&
                        (q.Trade.Matches((Item.Gil, 1000)) || q.Trade.Matches((Item.ImperialBronzePiece, 1))))
                    {
                        return QuestReply.ProgressEvent(3022).TextTable(0);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus <= 1)
                    {
                        int param6 = 0; // Can trigger a line "weirdo with silly mask". Maybe tied to RoV?

                        return QuestReply.ProgressEvent(3035, 0, 0, 0, 0, 0, param6, 0, missionStatus, 0);
                    }
                    else if (missionStatus == 2)
                    {
                        return QuestReply.Event(3036).TextTable(0);
                    }

                    return QuestReply.Event(3023).TextTable(0);
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Nadeey).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(AreaId) >= 3)
                    {
                        return QuestReply.Event(3025).TextTable(0);
                    }

                    return null;
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                    QuestReply.Event(3021, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0))
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 4, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 3)
                    {
                        return QuestReply.ProgressEvent(3024).TextTable(0);
                    }

                    return null;
                })
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 5, q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 4)
                    {
                        int param6 = 0; // Mention "San d'Oria's legendary sword". Maybe tied to RoV?

                        return QuestReply.ProgressEvent(3026, 0, 0, 0, 0, 0, param6, 0, 0, 0);
                    }

                    return null;
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 3026, q =>
                {
                    // First option is always 10. Then it's 1, 2, or 3 based on selection.
                    // All 3 topics must be selected before you can advance.
                    int bit = System.Math.Clamp(q.Option, 1, 4) - 1;
                    int topics = q.Mission.LocalVar("Option") | (1 << bit);
                    q.Mission.SetLocalVar("Option", topics);

                    q.Player.UpdateEvent(topics, 1);
                })
                // Step 2: Cacaroon trade event.
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3022, q =>
                {
                    q.Trade.Complete();
                    q.Player.SetMissionStatus(AreaId, 3);
                })
                // Step 3: Zone-in event.
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3024, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 4);
                })
                // Step 4: Zone-in event.
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3026, q =>
                {
                    q.Mission.Complete();
                })
                // Step 1: Cacaroon trigger event.
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3035, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.SetMissionStatus(AreaId, 1);
                    }
                    else
                    {
                        q.Player.SetMissionStatus(AreaId, 2);
                    }
                });
        }
    }
}
