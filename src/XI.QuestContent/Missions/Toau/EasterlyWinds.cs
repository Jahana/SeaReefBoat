// Easterly Winds (ToAU 6, Log ID 4, Mission ID 5), a line-for-line port of
// scripts/missions/toau/06_Easterly_Winds.lua (quest rework C3 T1). Halver !pos 2 0.1 0.1 233. One section (the mission
// current). Aht Urhgan Whitegate: Cacaroon plays 3023 and Nadeey 3025 (text table 0, plain events), Naja 3021 with the
// mercenary rank. Chateau d'Oraguille: Halver plays 565 (plain). Ru'Lude Gardens: trigger area 1 plays 10094, whose
// finish with option 1 gives 10 imperial bronze pieces and completes the mission only when they fit (Westerly Winds
// current); any other option completes it without them. The SOA era module (Modules/Era/Missions/MissionAdjustments.cs)
// replaces the Chateau's Halver finish of 564 of the previous mission and this mission's Ru'Lude trigger area.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class EasterlyWinds : MissionScript
    {
        public EasterlyWinds() : base(XI.Quests.MissionLog.ToAU, "EASTERLY_WINDS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/06_Easterly_Winds";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.WesterlyWinds,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Cacaroon)
                .OnTrigger(QuestReply.Event(3023).TextTable(0))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Nadeey)
                .OnTrigger(QuestReply.Event(3025).TextTable(0))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                    QuestReply.Event(3021, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0))
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Halver).OnTrigger(q =>
                {
                    return QuestReply.Event(565);
                })
                .OnTriggerAreaEnter(Zone.RuludeGardens, 1, q =>
                {
                    return QuestReply.ProgressEvent(10094);
                })
                .OnEventFinish(Zone.RuludeGardens, 10094, q =>
                {
                    // NOTE (Lua): We don't want to fall through and complete if the player's inventory is full.
                    // This is the reasoning for the two different mission:complete() calls.
                    if (q.Option == 1)
                    {
                        if (q.Player.GiveItems((Item.ImperialBronzePiece, 10)))
                        {
                            q.Mission.Complete();
                        }
                    }
                    else
                    {
                        q.Mission.Complete();
                    }
                });
        }
    }
}
