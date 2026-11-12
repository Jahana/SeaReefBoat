// Confessions of Royalty (ToAU 5, Log ID 4, Mission ID 4), a line-for-line port of
// scripts/missions/toau/05_Confessions_of_Royalty.lua (quest rework C3 T1). Halver !pos 2 0.1 0.1 233. One section (the
// mission current). Aht Urhgan Whitegate: Cacaroon plays 3023 and Nadeey 3025 (text table 0, plain events), Naja 3021
// with the mercenary rank. Chateau d'Oraguille: Halver plays 564 while the Raillefal's Letter is held, whose finish
// with
// option 1 takes the letter and completes the mission (Easterly Winds current); a zone-in with the mission Option var 1
// plays 563, whose finish takes the letter, completes the mission and moves the char to Northern San d'Oria. Northern
// San d'Oria: Guilerme plays 810 while the letter is held and the char cannot enter the Chateau (canEnterChateau
// below);
// its finish with option 1 sets the mission Option var 1 and moves the char into the Chateau, which plays 563. Kept as
// the Lua has it: Option is never cleared (a later zone-in of the Chateau, the mission still current, would play 563
// again); canEnterChateau tests the current mission of a non San d'Orian's own nation against 5 (a mission id, not a
// rank); the finish 564 gives no reward, as the reward table holds only the next mission.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class ConfessionsOfRoyalty : MissionScript
    {
        public ConfessionsOfRoyalty() : base(XI.Quests.MissionLog.ToAU, "CONFESSIONS_OF_ROYALTY") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/05_Confessions_of_Royalty";

        static bool CanEnterChateau(IQuestContext q)
        {
            int pNation = q.Player.Nation;
            int pRank = q.Player.Rank(pNation);

            // Everyone can enter at rank 3.
            if (pRank >= 3)
            {
                return true;
            }

            // Sandoria citizens gain access at rank 2.
            if (pNation == MissionLog.Sandoria && pRank >= 2)
            {
                return true;
            }

            // Other citizens gain access during "The emisary"/"The three Kingdoms"
            if (pNation != MissionLog.Sandoria && q.Player.CurrentMission(pNation) >= 5)
            {
                return true;
            }

            return false;
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.EasterlyWinds,
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
                    if (q.Player.HasKeyItem(KeyItem.RaillefalsLetter))
                    {
                        return QuestReply.ProgressEvent(564);
                    }

                    return null;
                })
                // Forces the player into the CS if they don't normall have access to the Chateau.
                .OnZoneIn(Zone.ChateauDoraguille, q =>
                {
                    if (q.Mission.Var("Option") == 1)
                    {
                        return QuestReply.Event(563);
                    }

                    return null;
                })
                // Force zones the player out of the Chateau if they don't have access.
                .OnEventFinish(Zone.ChateauDoraguille, 563, q =>
                {
                    q.Player.DelKeyItem(KeyItem.RaillefalsLetter);
                    q.Mission.Complete();
                    q.Player.SetPos(0, 0, 100, 64, Zone.NorthernSanDoria);
                })
                .OnEventFinish(Zone.ChateauDoraguille, 564, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.DelKeyItem(KeyItem.RaillefalsLetter);
                        q.Mission.Complete();
                    }
                })
                // Unique interaction IF the player does not have access to the Chateau to gain access to continue the
                // mission.
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Guilerme).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.RaillefalsLetter) && !CanEnterChateau(q))
                    {
                        return QuestReply.ProgressEvent(810);
                    }

                    return null;
                })
                .OnEventFinish(Zone.NorthernSanDoria, 810, q =>
                {
                    // Force zones the player into the Chateau
                    if (q.Option == 1)
                    {
                        q.Mission.SetVar("Option", 1);
                        q.Player.SetPos(0, 0, -13, 192, Zone.ChateauDoraguille);
                    }
                });
        }
    }
}
