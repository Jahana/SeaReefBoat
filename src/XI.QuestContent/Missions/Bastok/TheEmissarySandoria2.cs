// The Emissary San d'Oria, part 2 (Bastok 2-3, Log ID 1, Mission ID 8), a line-for-line port of
// scripts/missions/bastok/2_3_3_The_Emissary_Sandoria2.lua (quest rework C3 N2). Helaku !pos 49 -2 -12 231; Halver !pos
// 2 0.1 0.1 233. One section (this mission current), no reward table. Chateau d'Oraguille: Halver at status 8, 503 with
// parameter 7 = 1 when ENABLE_TRUST_QUESTS is 1 and the char has neither his spell nor his cipher (else 0), up to
// status 10 otherwise HALVER_OFFSET + 279, a message; the finish of 503 moves to status 9 and gives the cipher under
// that same test. Horlais Peak: the 32001 finish, at status 9 after the Rank 2 Mission battlefield (id 0; the local
// var battlefieldWin reads 0 for a char who never won one), gives the Kindred Crest and moves to status 10. Northern
// San d'Oria: Helaku with the Kindred Crest, 545 (progress; its finish completes the mission, begins The Emissary,
// moves to status 11, takes the crest and gives the Kindred Report), otherwise HELAKU_DIALOG + 16, a message; Shakir
// plays 538 (an event).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class TheEmissarySandoria2 : MissionScript
    {
        public TheEmissarySandoria2() : base(XI.Quests.MissionLog.Bastok, "THE_EMISSARY_SANDORIA2") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/2_3_3_The_Emissary_Sandoria2";

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

                    if (missionStatus == 8)
                    {
                        int needsHalverTrust = NeedsHalverTrust(q) ? 1 : 0;

                        return QuestReply.ProgressEvent(503).Option(7, needsHalverTrust);
                    }
                    else if (missionStatus <= 10)
                    {
                        return QuestReply.MessageText(q.Text.Id(Text.ChateauDoraguille.HALVER_OFFSET) + 279);
                    }

                    return null;
                })
                .OnEventFinish(Zone.ChateauDoraguille, 503, q =>
                {
                    q.Player.SetMissionStatus(MissionLog.Bastok, 9);

                    if (NeedsHalverTrust(q))
                    {
                        q.Player.GiveItem(Item.CipherOfHalversAlterEgo);
                    }
                })
                .OnEventFinish(Zone.HorlaisPeak, 32001, q =>
                {
                    if (q.Player.MissionStatus(MissionLog.Bastok) == 9 &&
                        q.Player.LocalVar("battlefieldWin") == Battlefield.Rank2Mission1)
                    {
                        q.Player.GiveKeyItem(KeyItem.KindredCrest);
                        q.Player.SetMissionStatus(MissionLog.Bastok, 10);
                    }
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Helaku).OnTrigger(q =>
                    q.Player.HasKeyItem(KeyItem.KindredCrest)
                        ? QuestReply.ProgressEvent(545)
                        : QuestReply.MessageText(q.Text.Id(Text.NorthernSanDoria.HELAKU_DIALOG) + 16))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Shakir).OnTrigger(q => QuestReply.Event(538))
                .OnEventFinish(Zone.NorthernSanDoria, 545, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.AddMission(MissionLog.Bastok, Mission.Bastok.TheEmissary);
                        q.Player.SetMissionStatus(MissionLog.Bastok, 11);
                        q.Player.DelKeyItem(KeyItem.KindredCrest);
                        q.Player.GiveKeyItem(KeyItem.KindredReport);
                    }
                });
        }
    }
}
