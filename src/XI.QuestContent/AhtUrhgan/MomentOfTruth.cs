// Moment of Truth (Log ID 6, Quest ID 30), a line-for-line port of scripts/quests/ahtUrhgan/Moment_of_Truth.lua (quest
// rework B3b Aht Urhgan, draft). Aht Urhgan Whitegate: Mishhar !pos -12 -6 29 (no script; default event 590), trigger
// area 5 (64 -7 -137 .. 95 -5 -123, the Shaharat Teahouse; the zone's own switch has no case for it). Mamook: qm7 !pos
// 216.9 -26.1 -94.6 (no script; default NOTHING_OUT_OF_ORDINARY). Jade Sepulcher: the Ornamental Door _1v0 (no script;
// default messageSpecial -1), the battlefield Moment of Truth (Battlefields/JadeSepulcher/MomentOfTruth.cs:
// index 3, entry _1v0 by talk, no item, up to 6 chars, requiredVar Quest[6][30]Prog 4; area 1's six mobs are
// SHADOWHAND_KAJEEL_JA + 0 .. + 5, all to die). Available with Give Peace a Chance completed: Mishhar 800, whose finish
// begins it. Accepted: Mishhar by Prog: 0 801 (an Event), 1 803 (Prog 2), 2 588 (an Event), 3 and 4 804 (an Event), 5
// 805; area 5 at Prog 0: 802 (Prog 1); Mamook's qm7 at Prog 2: the cutscene 221 (Prog 3); Jade Sepulcher's _1v0 at Prog
// 3: 3 (Prog 4); the battlefield's 32001 with battlefieldWin 1155: Prog 5. 805's finish gives the main job's potion
// (jobRewardItems) and completes it when that fits. Completed: Mishhar 806, replaceDefault. Kept as the Lua has it: a
// main job the table lacks (GEO, RUN, MON) reads nil, and npcUtil.giveItem(player, nil) gives nothing and returns true:
// completed with no item; complete's result unread; no reward table (the quest gives nothing else); Give Peace a
// Chance's write to this quest's Stage is never read.
// Rival containers (golden flows #rivals): Whitegate's area 5 (Against All Odds 797; Westerly Winds 3027 ties with the
// mission's container first, true by the priority rules and not shown by a flow) and the Ornamental Door _1v0
// (Puppet in Peril 4) answer the same entries and talks; the reply kinds above are pinned against them there.
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class MomentOfTruth : QuestScript
    {
        public MomentOfTruth() : base(QuestLog.AhtUrghan, "MOMENT_OF_TRUTH") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Moment_of_Truth";

        // 805's jobRewardItems, by main job
        static readonly Dictionary<int, int> JobRewardItems = new Dictionary<int, int>
        {
            [Job.War] = Item.StrengthPotion,
            [Job.Mnk] = Item.StrengthPotion,
            [Job.Drk] = Item.StrengthPotion,
            [Job.Drg] = Item.StrengthPotion,

            [Job.Whm] = Item.MindPotion,

            [Job.Blm] = Item.IntelligencePotion,
            [Job.Sch] = Item.IntelligencePotion,

            [Job.Rdm] = Item.VileElixirP1,
            [Job.Blu] = Item.VileElixirP1,

            [Job.Thf] = Item.HermesQuencher,
            [Job.Pld] = Item.VitalityPotion,

            [Job.Bst] = Item.CharismaPotion,
            [Job.Brd] = Item.CharismaPotion,
            [Job.Dnc] = Item.CharismaPotion,

            [Job.Rng] = Item.AgilityPotion,
            [Job.Nin] = Item.AgilityPotion,
            [Job.Cor] = Item.AgilityPotion,

            [Job.Sam] = Item.IcarusWing,
            [Job.Smn] = Item.ProEtherP1,
            [Job.Pup] = Item.DexterityPotion,
        };

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4, 5);

            // Section: Quest available
            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.AhtUrghan, "GIVE_PEACE_A_CHANCE") == QuestStatus.Completed)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Mishhar).OnTrigger(QuestReply.ProgressEvent(800))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 800, q =>
                {
                    q.Quest.Begin();
                });

            // Section: Quest accepted
            b.State(QuestStatus.Accepted)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Mishhar).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;
                    if (questProgress == 0)
                    {
                        return QuestReply.Event(801);
                    }
                    else if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(803);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.Event(588);
                    }
                    else if (questProgress == 3 ||
                             questProgress == 4)
                    {
                        return QuestReply.Event(804);
                    }
                    else if (questProgress == 5)
                    {
                        return QuestReply.ProgressEvent(805);
                    }
                    return null;
                })
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 5, q =>
                    q.Quest.Step == 0 ? QuestReply.ProgressEvent(802) : null)
                .OnEventFinish(Zone.AhtUrhganWhitegate, 802, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 803, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 805, q =>
                {
                    int playerjob = q.Player.MainJob;

                    // jobRewardItems[playerjob]: nil for a job the table lacks, and npcUtil.giveItem(player, nil)
                    // returns true
                    bool given = JobRewardItems.TryGetValue(playerjob, out int rewardItem)
                        ? q.Player.GiveItem(rewardItem)
                        : true;
                    if (given)
                    {
                        q.Quest.Complete();
                    }
                })
                .Npc(Zone.Mamook, Npc.Mamook.qm7).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressCutscene(221);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Mamook, 221, q =>
                {
                    q.Quest.SetStep(3);
                })
                .Npc(Zone.JadeSepulcher, Npc.JadeSepulcher._1v0).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(3);
                    }
                    return null;
                })
                .OnEventFinish(Zone.JadeSepulcher, 3, q =>
                {
                    q.Quest.SetStep(4);
                })
                .OnEventFinish(Zone.JadeSepulcher, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.MomentOfTruth)
                    {
                        q.Quest.SetStep(5);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Mishhar)
                .OnTrigger(QuestReply.Event(806).ReplaceDefault());
        }
    }
}
