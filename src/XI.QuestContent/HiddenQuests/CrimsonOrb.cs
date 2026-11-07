// Crimson Orb (hidden quest CrimsonOrb), ported from scripts/quests/hiddenQuests/Crimson_Orb.lua (quest rework B3b
// hidden quests). Davoi: Wall of Banishing (_45d) !pos 181 0.1 -218; Sedal-Godjal !pos 185 -3 -116; the ponds _45g
// (Groaning) !pos 101 0.1 60, _45h (Howling) !pos 21 0.1 -258, _45i (Wailing) !pos 380 0.1 -181, _45j (Screaming) !pos
// -219 0.1 -101. Reward: the Crimson Orb key item. One section (the Crimson Orb not held). The wall: Prog 0-1: Prog 1,
// nil (its script: the cave is sealed off); Prog 2 or more: MAY_BE_SOME_WAY_TO_BREAK. Sedal-Godjal: Prog 0: 24
// (canSkip, replaceDefault); 1: 22 (canSkip), finished on 1: Prog 2, the White Orb; 2: 21 (canSkip); 3: the cutscene 25
// (0, 0, 0, 136), finished: the Cursed Orb taken, quest:complete (the Crimson Orb, the vars cleared). A pond at Prog 2
// whose Option bit is unset: the local npcOffset, ORB_QUEST_OFFSET, the cutscene 50 + offset (0, the ponds done, race);
// else nil (its default: the pool is the color of blood). The pond cutscenes' finish: the bit set, ORB_QUEST_OFFSET +
// done + 1 (0, 0, 0, the next orb), the orb swapped for the next (White, Pink, Red, Blood, Cursed); on the fourth pond
// Curse I (power 50, 600 s), ORB_QUEST_OFFSET + 5, Prog 3. Kept as the Lua has it: the wall's Prog 0-1 arm writes Prog
// 1 and falls through to the wall's script; a pond's finish reads the offset from the local var its trigger wrote (a
// zone change clears it: offset 0); the finish counts the ponds before setting the bit.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class CrimsonOrb : HiddenQuestScript
    {
        public CrimsonOrb() : base("CrimsonOrb") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Crimson_Orb";

        // xi.effect.CURSE_I
        private const int EffectCurseI = 9;

        private static readonly string[] PondNpcs =
        {
            Npc.Davoi._45g,
            Npc.Davoi._45h,
            Npc.Davoi._45i,
            Npc.Davoi._45j,
        };

        // utils.mask.getBit(mask, bit)
        private static bool GetBit(int mask, int bit) => (mask & (1 << bit)) != 0;

        // utils.mask.countBits(mask, length)
        private static int CountBits(int mask, int length)
        {
            int count = 0;
            for (int i = 0; i < length; i++)
            {
                if (GetBit(mask, i))
                {
                    count++;
                }
            }

            return count;
        }

        // The below functions handle the logic for all ponds. NPC Offset is 0-indexed (50..53), and derived from the
        // table above. While we set all 4 bits, the ponds themselves only care about themselves, and if it is the last
        // pond in sequence (numPonds == 3) to change the event animation.
        private static QuestReply PondOnTrigger(IQuestContext q, int npcOffset)
        {
            int questOption = q.Quest.Var("Option");
            int numPonds = CountBits(questOption, 4);

            if (q.Quest.Step == 2 &&
                !GetBit(questOption, npcOffset))
            {
                q.Quest.SetLocalVar("npcOffset", npcOffset);
                q.Text.Special(Text.Davoi.ORB_QUEST_OFFSET);
                return QuestReply.ProgressCutscene(50 + npcOffset, 0, numPonds, q.Player.Race);
            }

            // Intentional fallthrough to default action (Pool is the color of blood)
            return null;
        }

        private static void PondEventFinish(IQuestContext q)
        {
            int questOption = q.Quest.Var("Option");
            int npcOffset = q.Quest.LocalVar("npcOffset");
            int numPonds = CountBits(questOption, 4);

            q.Quest.SetVarBit("Option", npcOffset);
            q.Text.Special(
                q.Text.Id(Text.Davoi.ORB_QUEST_OFFSET) + numPonds + 1, 0, 0, 0, KeyItem.WhiteOrb + numPonds + 1);
            q.Player.DelKeyItem(KeyItem.WhiteOrb + numPonds);
            q.Player.AddKeyItem(KeyItem.WhiteOrb + numPonds + 1);

            if (numPonds == 3)
            {
                // player:addStatusEffect(xi.effect.CURSE_I, { power = 50, duration = 600, origin = player })
                q.Player.AddStatusEffectForDuration(EffectCurseI, 50, 600);
                q.Text.Special(q.Text.Id(Text.Davoi.ORB_QUEST_OFFSET) + 5);
                q.Quest.SetStep(3);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.CrimsonOrb,
            });
            b.Steps(1, 2, 3);

            var section = b.AnyState(when: q => !q.Player.HasKeyItem(KeyItem.CrimsonOrb));

            section
                .Npc(Zone.Davoi, Npc.Davoi._45d).OnTrigger(q => // Wall of Banishing
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress <= 1)
                    {
                        q.Quest.SetStep(1);
                        // Intentional fallthrough to default action (Cave is sealed off)
                    }
                    else if (questProgress >= 2)
                    {
                        return QuestReply.MessageSpecial(Text.Davoi.MAY_BE_SOME_WAY_TO_BREAK);
                    }

                    return null;
                });

            for (int offset = 0; offset < PondNpcs.Length; offset++)
            {
                int npcOffset = offset;
                section.Npc(Zone.Davoi, PondNpcs[offset]).OnTrigger(q => PondOnTrigger(q, npcOffset));
            }

            section
                .Npc(Zone.Davoi, Npc.Davoi.SedalGodjal).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.Event(24).CanSkip().ReplaceDefault();
                    }
                    else if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(22).CanSkip();
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.Event(21).CanSkip();
                    }
                    else if (questProgress == 3)
                    {
                        return QuestReply.ProgressCutscene(25, 0, 0, 0, 136);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Davoi, 22, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.SetStep(2);
                        q.Player.GiveKeyItem(KeyItem.WhiteOrb);
                    }
                })
                .OnEventFinish(Zone.Davoi, 25, q =>
                {
                    q.Player.DelKeyItem(KeyItem.CursedOrb);
                    q.Quest.Complete();
                })
                .OnEventFinish(Zone.Davoi, 50, PondEventFinish)
                .OnEventFinish(Zone.Davoi, 51, PondEventFinish)
                .OnEventFinish(Zone.Davoi, 52, PondEventFinish)
                .OnEventFinish(Zone.Davoi, 53, PondEventFinish);
        }
    }
}
