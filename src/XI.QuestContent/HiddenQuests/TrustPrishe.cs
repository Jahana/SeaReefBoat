// Trust: Prishe (hidden quest TrustPrishe), ported from scripts/quests/hiddenQuests/Trust_Prishe.lua (quest rework B3b
// hidden quests). Walnut Door (_0qa) : !pos 117.029 -42.799 41.997 26 (default messageSpecial DOOR_IS_LOCKED_TIGHT).
// One section: ENABLE_TRUST_QUESTS 1, a trust permit, Prishe not learned, CoP current past Dawn with Dawn's Status 5 or
// more. The door: 633 (a progress event, 0, 0, 0, the memories); its finish on option 2, when quest:complete gives the
// (empty) reward: Prishe learned (silently) and YOU_LEARNED_TRUST.
// The memories: In the Mood for Love completed 2, A Chocobo's Tale completed 4, and two tests the Lua writes as
// player:hasCompletedMission(xi.questLog.JEUNO, xi.quest.id.jeuno.HOOK_LINE_AND_SINKER) and (..., APOCALYPSE_NIGH): a
// quest log and quest ids given to hasCompletedMission, so they read mission log 3 (Zilart) missions 71 and 89; both
// ids are 64 or more, so the engine answers "id < the current Zilart mission" (MissionBitBlob.HasCompletedMission):
// true while Zilart's current is 0xFFFF (not begun, or finished), false while a Zilart mission is current. 8 and 16
// are added on that. Kept as the Lua has it: those two tests (ported as the same reads, mission log 3, ids 71 and
// 89); the check's TODO.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class TrustPrishe : HiddenQuestScript
    {
        // the Lua's xi.questLog.JEUNO (3) and xi.quest.id.jeuno.HOOK_LINE_AND_SINKER (71) / APOCALYPSE_NIGH
        // (89), passed to hasCompletedMission: mission log 3 is MissionLog.Zilart
        private const int LuaJeunoQuestLog = 3;
        private const int LuaHookLineAndSinkerId = 71;
        private const int LuaApocalypseNighId = 89;

        public TrustPrishe() : base("TrustPrishe") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Trust_Prishe";

        private static int TrustMemory(IQuestPlayerView player)
        {
            int memories = 0;

            // Now that I think about it, I remember a time when some old jeweler over in Jeuno was going
            // on and on about what "love" really is.
            if (player.HasCompletedQuest(QuestLog.Jeuno, "IN_THE_MOOD_FOR_LOVE"))
            {
                memories = memories + 2;
            }

            // But I recall sharing something with a certain young girl...
            if (player.HasCompletedQuest(QuestLog.Jeuno, "A_CHOCOBOS_TALE"))
            {
                memories = memories + 4;
            }

            // [Well, then/Hmm... Come to think of it], the two of us make one hell of a team, don't you agree?
            if (player.HasCompletedMission(LuaJeunoQuestLog, LuaHookLineAndSinkerId))
            {
                memories = memories + 8;
            }

            // The Shadow Lord, Kam'lanaut, Eald'narche, Nag'Molada...doesn't matter one bit to me.
            if (player.HasCompletedMission(LuaJeunoQuestLog, LuaApocalypseNighId))
            {
                memories = memories + 16;
            }

            return memories;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q =>
                    q.Settings.MainEquals("ENABLE_TRUST_QUESTS", 1) &&
                    TrustPermit.Has(q.Player) &&
                    !q.Player.HasSpell(Spell.Prishe) &&
                    (q.Player.CurrentMission(MissionLog.Cop) > Mission.Cop.Dawn &&
                    q.Player.MissionVar(MissionLog.Cop, Mission.Cop.Dawn, "Status") >= 5))
                    // TODO: Additional conditions
                // Walnut Door
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold._0qa).OnTrigger(q =>
                    QuestReply.ProgressEvent(633, 0, 0, 0, TrustMemory(q.Player)))
                .OnEventFinish(Zone.TavnazianSafehold, 633, q =>
                {
                    if (q.Option == 2 && q.Quest.Complete())
                    {
                        q.Player.AddSpell(Spell.Prishe, silentLog: true);
                        q.Text.Special(Text.TavnazianSafehold.YOU_LEARNED_TRUST, 0, Spell.Prishe);
                    }
                });
        }
    }
}
