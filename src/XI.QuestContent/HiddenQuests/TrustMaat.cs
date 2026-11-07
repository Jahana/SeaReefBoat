// Trust: Maat (hidden quest TrustMaat), ported from scripts/quests/hiddenQuests/Trust_Maat.lua (quest rework B3b hidden
// quests). Maat, Rulude Gardens (his script: npcs/Maat.lua; default text MAAT_DIALOG).
// One section: ENABLE_TRUST_QUESTS 1, a trust permit, Maat not learned. Maat: with 6 or more of the 16 low bits of
// maatsCap set: 10241, else 10242 (plain events). 10241's finish on option 2, when quest:complete gives the (empty)
// reward: Maat learned (silently) and YOU_LEARNED_TRUST.
// Kept as the Lua has it: 10242 (no finish) answers every permit holder below six caps, a plain event at Maat, whose
// own script and the genkai quests share him.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class TrustMaat : HiddenQuestScript
    {
        public TrustMaat() : base("TrustMaat") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Trust_Maat";

        // utils.mask.countBits(mask, 16): the set bits among the 16 low ones
        private static int CountBits(int mask, int length)
        {
            int count = 0;
            for (int i = 0; i < length; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    count++;
                }
            }

            return count;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q =>
                    q.Settings.MainEquals("ENABLE_TRUST_QUESTS", 1) &&
                    TrustPermit.Has(q.Player) &&
                    !q.Player.HasSpell(Spell.Maat))
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Maat).OnTrigger(q =>
                {
                    if (CountBits(q.Player.CharVar("maatsCap"), 16) >= 6)
                    {
                        return QuestReply.Event(10241);
                    }
                    else
                    {
                        return QuestReply.Event(10242);
                    }
                })
                .OnEventFinish(Zone.RuludeGardens, 10241, q =>
                {
                    if (q.Option == 2 && q.Quest.Complete())
                    {
                        q.Player.AddSpell(Spell.Maat, silentLog: true);
                        q.Text.Special(Text.RuludeGardens.YOU_LEARNED_TRUST, 0, Spell.Maat);
                    }
                });
        }
    }
}
