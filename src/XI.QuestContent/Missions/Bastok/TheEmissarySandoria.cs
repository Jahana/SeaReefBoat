// The Emissary San d'Oria (Bastok 2-3, Log ID 1, Mission ID 6), a line-for-line port of
// scripts/missions/bastok/2_3_1_The_Emissary_Sandoria.lua (quest rework C3 N2). Baraka !pos 36 -2 -2 231; Helaku !pos
// 49 -2 -12 231; Halver !pos 2 0.1 0.1 233. One section (this mission current), no reward table. Northern San d'Oria:
// Baraka at status 2, HERE_WITH_BUSINESS, otherwise 539 (progress); Helaku at 2, 536 (its finish moves to status 3),
// at 3, HELAKU_DIALOG, at 4, 542, at 5, 543 (its finish completes the mission, moves to status 6 and begins The
// Emissary again); Shakir at 2, 556. Chateau d'Oraguille: Halver at 3, 501 with parameter 7 = 1 when
// ENABLE_TRUST_QUESTS is 1 and the char has neither his spell nor his cipher (else 0), above 3 HALVER_OFFSET + 266;
// the finish of 501 moves to status 4 and gives the cipher under that same test. Ghelsba Outpost: the death of
// Warchief Vatgit at status 4 moves to 5.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class TheEmissarySandoria : MissionScript
    {
        public TheEmissarySandoria() : base(XI.Quests.MissionLog.Bastok, "THE_EMISSARY_SANDORIA") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/2_3_1_The_Emissary_Sandoria";

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
                    int missionStatus = q.Player.MissionStatus(MissionLog.Bastok);

                    if (missionStatus == 3)
                    {
                        int needsHalverTrust = NeedsHalverTrust(q) ? 1 : 0;

                        return QuestReply.ProgressEvent(501).Option(7, needsHalverTrust);
                    }
                    else if (missionStatus > 3)
                    {
                        return QuestReply.MessageText(q.Text.Id(Text.ChateauDoraguille.HALVER_OFFSET) + 266);
                    }

                    return null;
                })
                .OnEventFinish(Zone.ChateauDoraguille, 501, q =>
                {
                    q.Player.SetMissionStatus(MissionLog.Bastok, 4);

                    if (NeedsHalverTrust(q))
                    {
                        q.Player.GiveItem(Item.CipherOfHalversAlterEgo);
                    }
                })
                .Npc(Zone.GhelsbaOutpost, Npc.GhelsbaOutpost.WarchiefVatgit).OnMobDeath(q =>
                {
                    if (q.Player.MissionStatus(MissionLog.Bastok) == 4)
                    {
                        q.Player.SetMissionStatus(MissionLog.Bastok, 5);
                    }
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Baraka).OnTrigger(q =>
                {
                    // TODO in the Lua: verify this message, as it was previously hard-coded to ID 11141
                    return q.Player.MissionStatus(MissionLog.Bastok) == 2
                        ? QuestReply.MessageText(Text.NorthernSanDoria.HERE_WITH_BUSINESS)
                        : QuestReply.ProgressEvent(539);
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Helaku).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Bastok);

                    if (missionStatus == 2)
                    {
                        return QuestReply.ProgressEvent(536);
                    }
                    else if (missionStatus == 3)
                    {
                        return QuestReply.MessageText(Text.NorthernSanDoria.HELAKU_DIALOG);
                    }
                    else if (missionStatus == 4)
                    {
                        return QuestReply.ProgressEvent(542);
                    }
                    else if (missionStatus == 5)
                    {
                        return QuestReply.ProgressEvent(543);
                    }

                    return null;
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Shakir).OnTrigger(q =>
                    q.Player.MissionStatus(MissionLog.Bastok) == 2 ? QuestReply.ProgressEvent(556) : null)
                .OnEventFinish(Zone.NorthernSanDoria, 536, q =>
                    q.Player.SetMissionStatus(MissionLog.Bastok, 3))
                .OnEventFinish(Zone.NorthernSanDoria, 543, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.SetMissionStatus(MissionLog.Bastok, 6);
                        q.Player.AddMission(MissionLog.Bastok, Mission.Bastok.TheEmissary);
                    }
                });
        }
    }
}
