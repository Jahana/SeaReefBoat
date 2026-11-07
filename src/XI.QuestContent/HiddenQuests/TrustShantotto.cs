// Trust: Shantotto (hidden quest TrustShantotto), ported from scripts/quests/hiddenQuests/Trust_Shantotto.lua (quest
// rework B3b hidden quests). Shantotto : !pos 122 -2 112 239 (her script: npcs/Shantotto.lua;
// default 171, her failing talks play 171).
// One section: ENABLE_TRUST_QUESTS 1, Shantotto not learned, the Windurst Trust Permit (only that one). Shantotto: with
// Curses, Foiled... Again!? (CURSES_FOILED_A_GOLEM) completed and the nine nation trusts learned (Kupipi, Nanaa Mihgo,
// Ajido-Marujido, Excenmille, Curilla, Trion, Ayame, Naji, Volker): 529 as a progress event ([3] the memories: always
// 0, [7] 1); else 529 as a plain event with importantEvent. Its finish on option 2: Shantotto learned (silently) and
// YOU_LEARNED_TRUST. Kept as the Lua has it: trustMemory is always 0 (its body is commented out); the finish reads
// only the option, so the "else" 529 (importantEvent) finished on 2 teaches her too; no quest:complete.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class TrustShantotto : HiddenQuestScript
    {
        private static readonly int[] RequiredTrusts =
        {
            Spell.Kupipi,
            Spell.NanaaMihgo,
            Spell.AjidoMarujido,
            Spell.Excenmille,
            Spell.Curilla,
            Spell.Trion,
            Spell.Ayame,
            Spell.Naji,
            Spell.Volker,
        };

        public TrustShantotto() : base("TrustShantotto") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Trust_Shantotto";

        private static bool HasRequiredTrusts(IQuestPlayerView player)
        {
            foreach (int trustSpellId in RequiredTrusts)
            {
                if (!player.HasSpell(trustSpellId))
                {
                    return false;
                }
            }

            return true;
        }

        // trustMemory: every memory is a TODO (commented out in the Lua)
        private static int TrustMemory(IQuestPlayerView player)
        {
            int memories = 0;
            return memories;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q =>
                    q.Settings.MainEquals("ENABLE_TRUST_QUESTS", 1) &&
                    !q.Player.HasSpell(Spell.Shantotto) &&
                    q.Player.HasKeyItem(KeyItem.WindurstTrustPermit))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.Shantotto).OnTrigger(q =>
                {
                    if (q.Player.HasCompletedQuest(QuestLog.Windurst, "CURSES_FOILED_A_GOLEM") &&
                        HasRequiredTrusts(q.Player))
                    {
                        return QuestReply.ProgressEvent(529).Option(3, TrustMemory(q.Player)).Option(7, 1);
                    }
                    else
                    {
                        return QuestReply.Event(529).ImportantEvent();
                    }
                })
                .OnEventFinish(Zone.WindurstWalls, 529, q =>
                {
                    if (q.Option == 2)
                    {
                        q.Player.AddSpell(Spell.Shantotto, silentLog: true);
                        q.Text.Special(Text.WindurstWalls.YOU_LEARNED_TRUST, 0, Spell.Shantotto);
                    }
                });
        }
    }
}
