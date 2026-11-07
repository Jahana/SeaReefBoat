// Heart of Madness (Log ID 8, Quest ID 169), a line-for-line port of scripts/quests/abyssea/Heart_of_Madness.lua (quest
// rework B3b Abyssea). Port Jeuno: Joachim !pos -52.844 0 -9.978 246 (his script: the traverser stone menu, 328 once
// Dawn of Death is accepted). Added by An Officer and a Pirate's 334 (no available section). Accepted, with 5 or more
// of the nine maw quests completed (A Goldstruck Gigas, To Paste a Peiste, Megadrile Menace, The Beast of Bastore, A
// Delectable Demon, A Fluttery Fiend, A Beaked Blusterer, A Man-eating Mite, An Ulcerous Uragnite): Joachim: 335, whose
// finish completes it and adds Tenuous Existence (accepted). Fewer: no section (Joachim's own script answers). Kept as
// the Lua has it: no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class HeartOfMadness : QuestScript
    {
        public HeartOfMadness() : base(QuestLog.Abyssea, "HEART_OF_MADNESS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/Heart_of_Madness";

        // the Lua's zoneQuests, in its order
        static readonly string[] ZoneQuests =
        {
            "A_GOLDSTRUCK_GIGAS",
            "TO_PASTE_A_PEISTE",
            "MEGADRILE_MENACE",
            "THE_BEAST_OF_BASTORE",
            "A_DELECTABLE_DEMON",
            "A_FLUTTERY_FIEND",
            "A_BEAKED_BLUSTERER",
            "A_MAN_EATING_MITE",
            "AN_ULCEROUS_URAGNITE",
        };

        // the Lua's countCompletedZoneQuests
        static int CountCompletedZoneQuests(IQuestPlayerView player)
        {
            int count = 0;
            foreach (var questId in ZoneQuests)
            {
                if (player.HasCompletedQuest(QuestLog.Abyssea, questId))
                {
                    count = count + 1;
                }
            }

            return count;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            b.State(QuestStatus.Accepted, when: q => CountCompletedZoneQuests(q.Player) >= 5)
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Joachim).OnTrigger(QuestReply.ProgressEvent(335))
                .OnEventFinish(Zone.PortJeuno, 335, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddQuest(QuestLog.Abyssea, "TENUOUS_EXISTENCE");
                    }
                });
        }
    }
}
