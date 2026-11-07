// Trust: Cherukiki (hidden quest TrustCherukiki), ported from scripts/quests/hiddenQuests/Trust_Cherukiki.lua (quest
// rework B3b hidden quests). Taillegeas : !pos 31.000 1.995 57.971 243 (default 10061).
// One section: ENABLE_TRUST_QUESTS 1, a trust permit, Cherukiki not learned, and CoP current past Chains and Bonds and
// before The Warrior's Path, or at Dawn with its Status 2 or more, or past Dawn. Taillegeas: 10235 (a progress event,
// 0, 0, 0, the memories: always 0). Its finish on option 2, when quest:complete gives the (empty) reward: Cherukiki
// learned (silently) and YOU_LEARNED_TRUST.
// Kept as the Lua has it: trustMemory is always 0 (its TODO).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class TrustCherukiki : HiddenQuestScript
    {
        public TrustCherukiki() : base("TrustCherukiki") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Trust_Cherukiki";

        // trustMemory: TODO: Does she have these?
        private static int TrustMemory(IQuestPlayerView player)
        {
            int memories = 0;

            return memories;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q =>
                    q.Settings.MainEquals("ENABLE_TRUST_QUESTS", 1) &&
                    TrustPermit.Has(q.Player) &&
                    !q.Player.HasSpell(Spell.Cherukiki) &&
                    (
                        // Between these missions
                        (q.Player.CurrentMission(MissionLog.Cop) > Mission.Cop.ChainsAndBonds &&
                        q.Player.CurrentMission(MissionLog.Cop) < Mission.Cop.TheWarriorsPath)
                        ||
                        // On Dawn, but past "the boss"
                        (q.Player.CurrentMission(MissionLog.Cop) == Mission.Cop.Dawn &&
                        q.Player.MissionVar(MissionLog.Cop, Mission.Cop.Dawn, "Status") >= 2)
                        ||
                        // Past Dawn
                        q.Player.CurrentMission(MissionLog.Cop) > Mission.Cop.Dawn
                    ))
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Taillegeas).OnTrigger(q =>
                    QuestReply.ProgressEvent(10235, 0, 0, 0, TrustMemory(q.Player)))
                .OnEventFinish(Zone.RuludeGardens, 10235, q =>
                {
                    if (q.Option == 2 && q.Quest.Complete())
                    {
                        q.Player.AddSpell(Spell.Cherukiki, silentLog: true);
                        q.Text.Special(Text.RuludeGardens.YOU_LEARNED_TRUST, 0, Spell.Cherukiki);
                    }
                });
        }
    }
}
