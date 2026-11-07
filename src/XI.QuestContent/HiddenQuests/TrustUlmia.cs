// Trust: Ulmia (hidden quest TrustUlmia), ported from scripts/quests/hiddenQuests/Trust_Ulmia.lua (quest rework B3b
// hidden quests). Dilapidated Gate (_0p0) : !pos 260.000 7.319 -440.001 25 (default messageSpecial
// SNOWMINT_POINT_LOCKED). One section: ENABLE_TRUST_QUESTS 1, a trust permit, Ulmia not learned, CoP current past
// Dawn with Dawn's Status 2 or more. The gate: 560 (a progress event, 0, 0, 0, the memories: Apocalypse Nigh (the
// Jeuno quest) completed 2); its finish on option 2, when quest:complete gives the (empty) reward: Ulmia learned
// (silently) and YOU_LEARNED_TRUST. Kept as the Lua has it: the check's comment says "on Dawn" while it tests past
// Dawn; its TODO.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class TrustUlmia : HiddenQuestScript
    {
        public TrustUlmia() : base("TrustUlmia") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Trust_Ulmia";

        private static int TrustMemory(IQuestPlayerView player)
        {
            int memories = 0;

            // When I sang the Lay of the Immortals before the fifth crystal, it stirred up emotions within me.
            if (player.HasCompletedQuest(QuestLog.Jeuno, "APOCALYPSE_NIGH"))
            {
                memories = memories + 2;
            }

            return memories;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q =>
                    q.Settings.MainEquals("ENABLE_TRUST_QUESTS", 1) &&
                    TrustPermit.Has(q.Player) &&
                    !q.Player.HasSpell(Spell.Ulmia) &&
                    // On Dawn, but past "the boss"
                    (q.Player.CurrentMission(MissionLog.Cop) > Mission.Cop.Dawn &&
                    q.Player.MissionVar(MissionLog.Cop, Mission.Cop.Dawn, "Status") >= 2))
                    // TODO: Additional conditions
                // Dilapidated Gate
                .Npc(Zone.MisareauxCoast, Npc.MisareauxCoast._0p0).OnTrigger(q =>
                    QuestReply.ProgressEvent(560, 0, 0, 0, TrustMemory(q.Player)))
                .OnEventFinish(Zone.MisareauxCoast, 560, q =>
                {
                    if (q.Option == 2 && q.Quest.Complete())
                    {
                        q.Player.AddSpell(Spell.Ulmia, silentLog: true);
                        q.Text.Special(Text.MisareauxCoast.YOU_LEARNED_TRUST, 0, Spell.Ulmia);
                    }
                });
        }
    }
}
