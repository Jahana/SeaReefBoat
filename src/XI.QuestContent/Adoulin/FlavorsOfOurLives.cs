// Flavors of Our Lives (Log ID 9, Quest ID 46), a line-for-line port of scripts/quests/adoulin/Flavors_of_Our_Lives.lua
// (quest rework B3b Adoulin group 2). Western Adoulin: Berghent !pos 95.721 -0.148 -28.680 256 (no script),
// Masad !pos -28.182 -0.650 -91.991 256 (no script; SoA missions hold him too: 2-1-0 Budding Prospects, from Flavors'
// Prog 2; 3-4-0 The Man in Black and 3-4-1 To the Victor, event 134, or progressEvent 153 with the Card Jailer Teodor),
// Dewalt !pos -23.099 -0.648 28.208 256 (script: Don't Ever Leaf Me); Rala Waterways:
// Chalvava !pos -318.000 -1.000 -318.000 258 (no script; SoA missions hold her: 2-6-1 Calamity in the Kitchen,
// progressEvent 344 / 345; 2-6-2 Arciela's Promise, event 360 once per zone; 4-4-1 Seeds of Doubt, progressEvent 367;
// 4-4-2 The Tomatoes of Wrath, progressEvent 370 while its Option var is 0); Yahse Hunting Grounds: the
// Harvesting Points (no script).
// Available: Berghent: 80, or 81 once refused (the local var Flavors_of_Our_Lives_Refused); 80's finish begins it on
// option 1, else sets the local var; 81's begins it on option 1. Prog 0: Berghent 82 (an Event); Masad: when SoA is at
// Budding Prospects or before, Budding Prospects' Timer (tomorrow) and its mustZone; this quest's Timer (tomorrow); 83,
// whose finish sets Prog 1. Prog 1: Berghent 82; Dewalt 85, whose finish sets Prog 2; Masad 104 (a progressEvent while
// the Timer is to come, else an Event once per zone). Prog 2: Berghent 82, Dewalt 105 (Events); Chalvava 8, whose
// finish sets Prog 3. Prog 3 without the Blightberry: Berghent 82, Dewalt 105, Chalvava 9 (Events); a trade to a
// Harvesting Point in the Yahse Hunting Grounds: xi.helm.onTrade (harvesting, no csid), then the Blightberry (a key
// item action). Prog 3 with it: Berghent 87, whose finish completes it (300 bayld, 500 exp, the title
// Potation Pathfinder; no fame area). Completed: Berghent 88.
// A deliberate change from the Lua (user ruling 2026-11-05, kept bugs): zone 260 is not in
// xi.helm.dataTable[HARVESTING].zone, and upstream's xi.helm.onTrade raised there (pickItem indexed the nil entry), so
// the trade answered nothing; the shared helm logic now finds nothing and breaks no tool in a zone with no data, so the
// Sickle trade goes on to the Blightberry. With ENABLE_HELM_WAIT on (off by default) it returns before pickItem while
// GetSystemTime() < [HELM]LastAttempt + 3 (the Blightberry all the same).
// Kept as the Lua has it: the Blightberry is kept at the completion (the Lua's TODO); 81's finish does nothing on a
// refusal.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Adoulin
{
    public sealed class FlavorsOfOurLives : QuestScript
    {
        public FlavorsOfOurLives() : base(QuestLog.Adoulin, "FLAVORS_OF_OUR_LIVES") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/adoulin/Flavors_of_Our_Lives";

        // xi.helmType.HARVESTING
        private const int HelmTypeHarvesting = 1;

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Bayld = 300,
                Exp = 500,
                Title = Title.PotationPathfinder,
            });

            // the Prog values the finishes below set (83, 85, 8)
            b.Steps(1, 2, 3);

            // Section: Talk to Berghent near the Big Bridge in Western Adoulin (J-9) to start the quest.
            b.State(QuestStatus.Available)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Berghent).OnTrigger(q =>
                {
                    if (q.Player.LocalVar("Flavors_of_Our_Lives_Refused") == 1)
                    {
                        return QuestReply.ProgressEvent(81);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(80);
                    }
                })
                .OnEventFinish(Zone.WesternAdoulin, 80, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                    else // Refused
                    {
                        q.Player.SetLocalVar("Flavors_of_Our_Lives_Refused", 1);
                    }
                })
                .OnEventFinish(Zone.WesternAdoulin, 81, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Talk to Masad inside the Mummers' Coalition.
            b.State(QuestStatus.Accepted, step: 0)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Berghent).OnTrigger(QuestReply.Event(82)) // Reminder
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Masad).OnTrigger(q =>
                {
                    if (q.Player.CurrentMission(MissionLog.Soa) <= Mission.Soa.BuddingProspects)
                    {
                        // xi.mission.setVar(player, SOA, BUDDING_PROSPECTS, 'Timer', VanadielUniqueDay() + 1)
                        q.Player.SetMissionVar(MissionLog.Soa, Mission.Soa.BuddingProspects, "Timer",
                            q.Player.VanadielUniqueDay + 1);
                        // xi.mission.setMustZone(player, SOA, BUDDING_PROSPECTS)
                        q.Player.SetMissionMustZone(MissionLog.Soa, Mission.Soa.BuddingProspects);
                    }

                    q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay + 1);

                    return QuestReply.ProgressEvent(83);
                })
                .OnEventFinish(Zone.WesternAdoulin, 83, q =>
                {
                    q.Quest.SetStep(1);
                });

            // Section: Talk to Dewalt inside the Couriers' Coalition.
            b.State(QuestStatus.Accepted, step: 1)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Berghent).OnTrigger(QuestReply.Event(82)) // Reminder
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Dewalt).OnTrigger(QuestReply.ProgressEvent(85))
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Masad).OnTrigger(q =>
                {
                    int waitTimer = q.Quest.Var("Timer");

                    // Priority changes from blocking to non-blocking after the timer has expired
                    if (waitTimer > q.Player.VanadielUniqueDay)
                    {
                        return QuestReply.ProgressEvent(104);
                    }
                    else
                    {
                        return QuestReply.Event(104).OncePerZone();
                    }
                })
                .OnEventFinish(Zone.WesternAdoulin, 85, q =>
                {
                    q.Quest.SetStep(2);
                });

            // Section: Head to Rala Waterways and talk to Chalvava at (F-11).
            b.State(QuestStatus.Accepted, step: 2)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Berghent).OnTrigger(QuestReply.Event(82)) // Reminder
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Dewalt).OnTrigger(QuestReply.Event(105)) // Reminder
                .Npc(Zone.RalaWaterways, Npc.RalaWaterways.Chalvava).OnTrigger(QuestReply.ProgressEvent(8))
                .OnEventFinish(Zone.RalaWaterways, 8, q =>
                {
                    q.Quest.SetStep(3);
                });

            // Section: Go to Yahse Hunting Grounds and harvest a Key Item Blightberry using a Sickle
            b.State(QuestStatus.Accepted, step: 3, when: q => !q.Player.HasKeyItem(KeyItem.Blightberry))
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Berghent).OnTrigger(QuestReply.Event(82)) // Reminder
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Dewalt).OnTrigger(QuestReply.Event(105)) // Reminder
                .Npc(Zone.RalaWaterways, Npc.RalaWaterways.Chalvava).OnTrigger(QuestReply.Event(9)) // Reminder
                .Npc(Zone.YahseHuntingGrounds, Npc.YahseHuntingGrounds.HarvestingPoint).OnTrade(q =>
                {
                    // No CS for HELM in Adoulin
                    // xi.helm.onTrade(player, npc, trade, xi.helmType.HARVESTING, nil): nothing found here (see above)
                    q.Trade.HelmOnTrade(HelmTypeHarvesting);
                    return QuestReply.KeyItem(KeyItem.Blightberry);
                });

            // Section: Return to Berghent for your reward
            b.State(QuestStatus.Accepted, step: 3, when: q => q.Player.HasKeyItem(KeyItem.Blightberry))
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Berghent).OnTrigger(QuestReply.ProgressEvent(87))
                .OnEventFinish(Zone.WesternAdoulin, 87, q =>
                {
                    q.Quest.Complete();
                    // TODO: remove blightberry
                });

            // New default text
            b.State(QuestStatus.Completed)
                .Npc(Zone.WesternAdoulin, Npc.WesternAdoulin.Berghent).OnTrigger(QuestReply.Event(88));
        }
    }
}
