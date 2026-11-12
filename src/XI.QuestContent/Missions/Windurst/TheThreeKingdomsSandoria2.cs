// The Three Kingdoms San d'Oria, part 2 (Windurst 2-3, Log ID 2, Mission ID 8), a line-for-line port of
// scripts/missions/windurst/2_3_3_The_Three_Kingdoms_Sandoria2.lua (quest rework C3 N2). Kasaroro !pos -72 -3 34 231;
// Halver !pos 2 0.1 0.1 233. One section (this mission current), no reward table. Chateau d'Oraguille: Halver at
// status 8, 504 (progress) with parameter 7 = 1 when ENABLE_TRUST_QUESTS is 1 and the char has neither his spell nor
// his cipher (else 0), otherwise HALVER_OFFSET + 279, a message; the finish of 504 moves to status 9 and gives the
// cipher under that same test. Horlais Peak: the 32001 finish, at status 9 after the Rank 2 Mission 1 battlefield (id
// 0; the local var battlefieldWin reads 0 for a char who never won one), gives the Kindred Crest, takes the Dark Key
// and moves to status 10. Northern San d'Oria: Kasaroro at status 8 says KASARORO_DIALOG, at 10 plays 551 (progress;
// its finish completes the mission, begins The Three Kingdoms, takes the crest, gives the Kindred Report and sets
// status 11). Status reads are the mission's log.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class TheThreeKingdomsSandoria2 : MissionScript
    {
        public TheThreeKingdomsSandoria2() : base(XI.Quests.MissionLog.Windurst, "THE_THREE_KINGDOMS_SANDORIA2") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/2_3_3_The_Three_Kingdoms_Sandoria2";

        // ENABLE_TRUST_QUESTS is 1 and the char has neither Halver's spell nor his cipher
        static bool NeedsHalverTrust(IQuestContext q) =>
            q.Settings.MainEquals("ENABLE_TRUST_QUESTS", 1) &&
            !q.Player.HasSpell(Spell.Halver) &&
            !q.Player.FindItem(Item.CipherOfHalversAlterEgo);

        protected override void Build(MissionScriptBuilder b)
        {
            b.State(current: true)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Halver).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Windurst);

                    if (missionStatus == 8)
                    {
                        int needsHalverTrust = NeedsHalverTrust(q) ? 1 : 0;

                        return QuestReply.ProgressEvent(504).Option(7, needsHalverTrust);
                    }
                    else
                    {
                        return QuestReply.MessageText(q.Text.Id(Text.ChateauDoraguille.HALVER_OFFSET) + 279);
                    }
                })
                .OnEventFinish(Zone.ChateauDoraguille, 504, q =>
                {
                    q.Player.SetMissionStatus(MissionLog.Windurst, 9);

                    if (NeedsHalverTrust(q))
                    {
                        q.Player.GiveItem(Item.CipherOfHalversAlterEgo);
                    }
                })
                .OnEventFinish(Zone.HorlaisPeak, 32001, q =>
                {
                    if (q.Player.MissionStatus(MissionLog.Windurst) == 9 &&
                        q.Player.LocalVar("battlefieldWin") == Battlefield.Rank2Mission1)
                    {
                        q.Player.GiveKeyItem(KeyItem.KindredCrest);
                        q.Player.DelKeyItem(KeyItem.DarkKey);
                        q.Player.SetMissionStatus(MissionLog.Windurst, 10);
                    }
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Kasaroro).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Windurst);

                    if (missionStatus == 8)
                    {
                        return QuestReply.MessageText(Text.NorthernSanDoria.KASARORO_DIALOG);
                    }
                    else if (missionStatus == 10)
                    {
                        return QuestReply.ProgressEvent(551);
                    }

                    return null;
                })
                .OnEventFinish(Zone.NorthernSanDoria, 551, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.AddMission(MissionLog.Windurst, Mission.Windurst.TheThreeKingdoms);
                        q.Player.DelKeyItem(KeyItem.KindredCrest);
                        q.Player.GiveKeyItem(KeyItem.KindredReport);
                        q.Player.SetMissionStatus(MissionLog.Windurst, 11);
                    }
                });
        }
    }
}
