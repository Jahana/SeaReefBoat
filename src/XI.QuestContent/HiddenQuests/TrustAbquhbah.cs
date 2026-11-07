// Trust: Abquhbah (hidden quest TrustAbquhbah), ported from scripts/quests/hiddenQuests/Trust_Abquhbah.lua (quest
// rework B3b hidden quests). Abquhbah, Aht Urhgan Whitegate (his script: npcs/Abquhbah.lua). One section:
// ENABLE_TRUST_QUESTS 1, a trust permit, Abquhbah not learned, no cipher of his held, ToAU Immortal Sentries
// completed and RoV at Ever Forward or later. Abquhbah: 170 (a progress event: [1] the permit 1, [2] the nation,
// text_table 0); its finish gives the cipher (npcUtil.giveItem) while the permit is held. Kept as the Lua has it: the
// finish reads only the permit (the cipher again on a second 170 before it is used is stopped by the check's
// findItem); no quest:complete (nothing to clean up).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    // HQuest[TrustAbquhbah]; see hiddenQuests/GROUPS.md
    public sealed class TrustAbquhbah : HiddenQuestScript
    {
        public TrustAbquhbah() : base("TrustAbquhbah") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Trust_Abquhbah";

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q =>
                    q.Settings.MainEquals("ENABLE_TRUST_QUESTS", 1) &&
                    TrustPermit.Has(q.Player) &&
                    !q.Player.HasSpell(Spell.Abquhbah) &&
                    !q.Player.FindItem(Item.CipherOfAbquhbahsAlterEgo) &&
                    q.Player.HasCompletedMission(MissionLog.Toau, Mission.Toau.ImmortalSentries) &&
                    q.Player.CurrentMission(MissionLog.Rov) >= Mission.Rov.EverForward)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Abquhbah).OnTrigger(q =>
                {
                    int hasTrustPermit = TrustPermit.Has(q.Player) ? 1 : 0;

                    return QuestReply.ProgressEvent(170)
                        .Option(1, hasTrustPermit)
                        .Option(2, q.Player.Nation)
                        .TextTable(0);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 170, q =>
                {
                    if (TrustPermit.Has(q.Player))
                    {
                        q.Player.GiveItem(Item.CipherOfAbquhbahsAlterEgo);
                    }
                });
        }
    }
}
