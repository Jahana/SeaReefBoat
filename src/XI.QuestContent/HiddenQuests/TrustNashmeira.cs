// Trust: Nashmeira (hidden quest TrustNashmeira), ported from scripts/quests/hiddenQuests/Trust_Nashmeira.lua (quest
// rework B3b hidden quests). Imperial Whitegate, Aht Urhgan Whitegate (default messageSpecial GATE_IS_FIRMLY_CLOSED).
// One section: ENABLE_TRUST_QUESTS 1, a trust permit, Nashmeira not learned, ToAU The Empress Crowned completed. The
// gate: 5092 (a progress event, text_table 0); its finish on option 2, when quest:complete gives the (empty) reward:
// Nashmeira learned (silently) and YOU_LEARNED_TRUST.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class TrustNashmeira : HiddenQuestScript
    {
        public TrustNashmeira() : base("TrustNashmeira") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Trust_Nashmeira";

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q =>
                    q.Settings.MainEquals("ENABLE_TRUST_QUESTS", 1) &&
                    TrustPermit.Has(q.Player) &&
                    !q.Player.HasSpell(Spell.Nashmeira) &&
                    q.Player.HasCompletedMission(MissionLog.Toau, Mission.Toau.TheEmpressCrowned))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.ImperialWhitegate).OnTrigger(q =>
                    QuestReply.ProgressEvent(5092).TextTable(0))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 5092, q =>
                {
                    if (q.Option == 2 && q.Quest.Complete())
                    {
                        q.Player.AddSpell(Spell.Nashmeira, silentLog: true);
                        q.Text.Special(Text.AhtUrhganWhitegate.YOU_LEARNED_TRUST, 0, Spell.Nashmeira);
                    }
                });
        }
    }
}
