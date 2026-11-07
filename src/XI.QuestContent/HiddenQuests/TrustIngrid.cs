// Trust: Ingrid (hidden quest TrustIngrid), ported from scripts/quests/hiddenQuests/Trust_Ingrid.lua (quest rework B3b
// hidden quests). Rigobertine, Eastern Adoulin.
// One section: ENABLE_TRUST_QUESTS 1, a trust permit, Ingrid not learned, SoA The Merciless One completed. Rigobertine:
// 5062 (a plain event: 257, 0, 0, 255, 255, 4352, 1, the parameters captured after The Merciless One); its finish on
// option 2: Ingrid learned (silently) and YOU_LEARNED_TRUST.
// Kept as the Lua has it: no quest:complete (nothing to clean up); the event's parameters are fixed (its TODO).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class TrustIngrid : HiddenQuestScript
    {
        public TrustIngrid() : base("TrustIngrid") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Trust_Ingrid";

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q =>
                    q.Settings.MainEquals("ENABLE_TRUST_QUESTS", 1) &&
                    TrustPermit.Has(q.Player) &&
                    !q.Player.HasSpell(Spell.Ingrid) &&
                    q.Player.HasCompletedMission(MissionLog.Soa, Mission.Soa.TheMercilessOne))
                .Npc(Zone.EasternAdoulin, Npc.EasternAdoulin.Rigobertine).OnTrigger(q =>
                {
                    // TODO: These parameters may have no impact on the event, and were captured
                    // immediately after completing "The Merciless One" and receiving eternal flame
                    // keyitem.

                    return QuestReply.Event(5062, 257, 0, 0, 255, 255, 4352, 1);
                })
                .OnEventFinish(Zone.EasternAdoulin, 5062, q =>
                {
                    if (q.Option == 2)
                    {
                        q.Player.AddSpell(Spell.Ingrid, silentLog: true);

                        q.Text.Special(Text.EasternAdoulin.YOU_LEARNED_TRUST, 0, Spell.Ingrid);
                    }
                });
        }
    }
}
