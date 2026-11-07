// A Journey Begins (Log ID 8, Quest ID 160), a line-for-line port of scripts/quests/abyssea/A_Journey_Begins.lua (quest
// rework B3b Abyssea). Port Jeuno: Joachim !pos -52.844 0 -9.978 246 (his script: the traverser stone menu,
// xi.abyssea.traverserNPCOnTrigger; no default). Added at every login and zone-in by xi.player.onGameIn
// (globals/player.lua:196-201) while ENABLE_ABYSSEA is 1 and the quest is available: no available section. Accepted at
// level 30 or more: the Port Jeuno zone-in at Prog 0 plays 324 (-> Prog 1); Joachim at Prog 1: 325, whose finish
// completes it (Traverser Stone 1), starts the traverser stone epoch and adds The Truth Beckons (accepted). Kept as the
// Lua has it: the zone-in returns 324 as a number (an Event); the login itself never plays 324 on the login that adds
// the quest (onZoneIn runs before onGameIn); no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class AJourneyBegins : QuestScript
    {
        public AJourneyBegins() : base(QuestLog.Abyssea, "A_JOURNEY_BEGINS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/A_Journey_Begins";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1);

            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.TraverserStone1,
            });

            // Retail behavior for this quest is to flag when the expansion has been purchased, and is flagged if
            // Abyssea is enabled in onGameIn.
            b.State(QuestStatus.Accepted, when: q => q.Player.MainLevel >= 30)
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Joachim).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(325);
                    }
                    return null;
                })
                .OnZoneIn(Zone.PortJeuno, q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.Event(324);
                    }
                    return null;
                })
                .OnEventFinish(Zone.PortJeuno, 324, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.PortJeuno, 325, q =>
                {
                    if (q.Quest.Complete())
                    {
                        // Traverser Stone epoch begins when 'The Truth Beckons' is flagged, and is retail
                        // behavior. This can be confirmed by checking the currency tab on retail servers.
                        q.Player.SetTraverserEpoch();
                        q.Player.AddQuest(QuestLog.Abyssea, "THE_TRUTH_BECKONS");
                    }
                });
        }
    }
}
