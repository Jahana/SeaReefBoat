// Death and Rebirth (Log ID 8, Quest ID 181), a line-for-line port of scripts/quests/abyssea/Death_and_Rebirth.lua
// (quest rework B3b Abyssea). Port Jeuno: Joachim !pos -52.844 0 -9.978 246 (his script: the traverser stone menu, 328
// once Dawn of Death is accepted). Hall of the Gods: its zone-in. Added by A Sea Dog's Summons' 6 (no available
// section; that 6 plays from 18:00 to 4:59 since user ruling 2026-11-04). Accepted, with all nine maw quests
// completed: Joachim: 343 (-> Prog 1). Accepted at Prog 1: the Hall of the Gods zone-in from 18:00 to 4:59
// (hour >= 18 or hour < 5): 7, whose finish completes it and adds Emissaries of God (accepted). Kept as the Lua has
// it: 343 answers at any Prog while the nine are completed (section 1 has no Prog check); 7 is returned as a number
// (an Event); no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class DeathAndRebirth : QuestScript
    {
        public DeathAndRebirth() : base(QuestLog.Abyssea, "DEATH_AND_REBIRTH") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/Death_and_Rebirth";

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

        // the Lua's allZoneQuestsComplete
        static bool AllZoneQuestsComplete(IQuestPlayerView player)
        {
            foreach (var questId in ZoneQuests)
            {
                if (!player.HasCompletedQuest(QuestLog.Abyssea, questId))
                {
                    return false;
                }
            }

            return true;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1);

            b.Reward(new QuestReward());

            b.State(QuestStatus.Accepted, when: q => AllZoneQuestsComplete(q.Player))
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Joachim).OnTrigger(QuestReply.ProgressEvent(343))
                .OnEventFinish(Zone.PortJeuno, 343, q =>
                {
                    q.Quest.SetStep(1);
                });

            b.State(QuestStatus.Accepted, step: 1)
                .OnZoneIn(Zone.HallOfTheGods, q =>
                {
                    int hour = q.Player.VanadielHour;
                    if (hour >= 18 || hour < 5)
                    {
                        return QuestReply.Event(7);
                    }
                    return null;
                })
                .OnEventFinish(Zone.HallOfTheGods, 7, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddQuest(QuestLog.Abyssea, "EMISSARIES_OF_GOD");
                    }
                });
        }
    }
}
