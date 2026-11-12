// The Three Kingdoms San d'Oria (Windurst 2-3, Log ID 2, Mission ID 6), a line-for-line port of
// scripts/missions/windurst/2_3_1_The_Three_Kingdoms_Sandoria.lua (quest rework C3 N3). Halver !pos 2 0.1 0.1 233;
// Warchief Vatgit !pos -74.960 -34.692 256.968 140; Kasaroro !pos -72 -3 34 231. One section (this mission current), an
// empty reward table (no Reward call, as the other 2-3 legs). Chateau d'Oraguille: Halver at status 3 plays 502
// (progress) with slot 7 = 1 when ENABLE_TRUST_QUESTS is 1 and the char has neither his spell nor his cipher (else 0),
// at any other status says HALVER_OFFSET + 279; 502's finish sets status 4 and gives the cipher under that same test.
// Ghelsba Outpost: Warchief Vatgit's death at status 4 sets status 5. Northern San d'Oria: Kasaroro at status 3 says
// KASARORO_DIALOG, at 4 plays 549, at 5 plays 550 (progress); 550's finish completes the mission and, when that
// succeeds, begins The Three Kingdoms at status 6.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Windurst
{
    public sealed class TheThreeKingdomsSandoria : MissionScript
    {
        public TheThreeKingdomsSandoria() : base(XI.Quests.MissionLog.Windurst, "THE_THREE_KINGDOMS_SANDORIA") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/windurst/2_3_1_The_Three_Kingdoms_Sandoria";

        // mission.areaId
        const int AreaId = MissionLog.Windurst;

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
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 3)
                    {
                        int needsHalverTrust = NeedsHalverTrust(q) ? 1 : 0;

                        return QuestReply.ProgressEvent(502).Option(7, needsHalverTrust);
                    }
                    else
                    {
                        return QuestReply.MessageText(q.Text.Id(Text.ChateauDoraguille.HALVER_OFFSET) + 279);
                    }
                })
                .OnEventFinish(Zone.ChateauDoraguille, 502, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 4);

                    if (NeedsHalverTrust(q))
                    {
                        q.Player.GiveItem(Item.CipherOfHalversAlterEgo);
                    }
                })
                .Npc(Zone.GhelsbaOutpost, Npc.GhelsbaOutpost.WarchiefVatgit).OnMobDeath(q =>
                {
                    if (q.Player.MissionStatus(AreaId) == 4)
                    {
                        q.Player.SetMissionStatus(AreaId, 5);
                    }
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Kasaroro).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(AreaId);

                    if (missionStatus == 3)
                    {
                        return QuestReply.MessageText(Text.NorthernSanDoria.KASARORO_DIALOG);
                    }
                    else if (missionStatus == 4)
                    {
                        return QuestReply.ProgressEvent(549);
                    }
                    else if (missionStatus == 5)
                    {
                        return QuestReply.ProgressEvent(550);
                    }

                    return null;
                })
                .OnEventFinish(Zone.NorthernSanDoria, 550, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.AddMission(MissionLog.Windurst, Mission.Windurst.TheThreeKingdoms);
                        q.Player.SetMissionStatus(AreaId, 6);
                    }
                });
        }
    }
}
