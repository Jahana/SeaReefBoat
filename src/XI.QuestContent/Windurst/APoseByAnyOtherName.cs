// A Pose By Any Other Name (Log ID 2, Quest ID 7), a line-for-line port of
// scripts/quests/windurst/A_Pose_by_Any_Other_Name.lua (quest rework B3b). Angelica : !pos -64 -9.25 -9 238. Angelica
// asks for the body piece of the main job's table (poseItems). Prog holds the requested item id and Wait the system
// time an hour after the request: wearing it within the hour completes the quest (Copy of Ancient Blood, Super Model,
// Angelica's Autograph, mustZone); after the hour she fails it (the quest deleted, its vars cleared, Lower than the
// Lowest Tunnel Worm, needToZone). Everyone's Grudging and A Question of Taste (outlands) read its status and mustZone.
// Kept as the Lua file has it: a start by 91 (option 1) leaves Option 1 (the completion's cleanup or the failure clears
// it); the failure keeps any title and a later offer starts again at 90; a Prog that is no pose body (a GM's) indexes
// poseGear to nil, which 94 sends as 0 (PoseGearOf).
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class APoseByAnyOtherName : QuestScript
    {
        static readonly Dictionary<int, int> PoseItems = new Dictionary<int, int>
        {
            [Job.War] = Item.BronzeHarness,
            [Job.Mnk] = Item.Robe,
            [Job.Whm] = Item.Tunic,
            [Job.Blm] = Item.Tunic,
            [Job.Rdm] = Item.Tunic,
            [Job.Thf] = Item.LeatherVest,
            [Job.Pld] = Item.BronzeHarness,
            [Job.Drk] = Item.BronzeHarness,
            [Job.Bst] = Item.LeatherVest,
            [Job.Brd] = Item.Robe,
            [Job.Rng] = Item.LeatherVest,
            [Job.Sam] = Item.Kenpogi,
            [Job.Nin] = Item.Kenpogi,
            [Job.Drg] = Item.BronzeHarness,
            [Job.Smn] = Item.Tunic,
            [Job.Blu] = Item.Robe,
            [Job.Cor] = Item.BronzeHarness,
            [Job.Pup] = Item.Tunic,
            [Job.Dnc] = Item.LeatherVest,
            [Job.Sch] = Item.Tunic,
            [Job.Geo] = Item.Tunic,
            [Job.Run] = Item.BronzeHarness,
        };

        static readonly Dictionary<int, int> PoseGear = new Dictionary<int, int>
        {
            [Item.BronzeHarness] = 1,
            [Item.Robe] = 2,
            [Item.Tunic] = 3,
            [Item.LeatherVest] = 4,
            [Item.Kenpogi] = 6,
        };

        public APoseByAnyOtherName() : base(QuestLog.Windurst, "A_POSE_BY_ANY_OTHER_NAME") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/A_Pose_by_Any_Other_Name";

        // poseGear[quest:getVar(player, 'Prog')]: nil for a Prog that is no pose body (only a GM's), which the event's
        // binding sends as 0 (windurst.a_pose_by_any_other_name#gm_prog)
        static int PoseGearOf(int prog) => PoseGear.TryGetValue(prog, out int gear) ? gear : 0;

        // poseItems[player:getMainJob()]: every main job is in the table; a missing one (nil in Lua) never equals an equip id
        static int? DesiredBody(IQuestPlayerView player) =>
            PoseItems.TryGetValue(player.MainJob, out int item) ? item : (int?)null;

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Windurst,
                Item = Item.CopyOfAncientBlood,
                Title = Title.SuperModel,
                KeyItem = KeyItem.AngelicasAutograph,
            });

            // Section: Quest available
            b.State(QuestStatus.Available, when: q => q.Player.NeedToZone == false)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Angelica).OnTrigger(q =>
                {
                    int? desiredBody = DesiredBody(q.Player);
                    int currentBody = q.Player.EquipId(Slot.Body);

                    if (currentBody != desiredBody)
                    {
                        if (q.Quest.Var("Option") == 0)
                        {
                            return QuestReply.ProgressEvent(90);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(91);
                        }
                    }
                    else
                    {
                        // If the player is wearing the requested body for their job Angelica will only return event 86
                        // intead of cycling between 86 and 87.
                        return QuestReply.Event(86).SetPriority(101);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 90, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Quest.SetVar("Option", 0);
                    }
                    else if (q.Option == 2)
                    {
                        q.Quest.SetVar("Option", 1);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 91, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted (vars.Prog == 0)
            b.State(QuestStatus.Accepted, step: 0)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Angelica).OnTrigger(q =>
                {
                    // the Lua indexes poseItems the same way; every main job has an entry
                    int requestedBody = PoseItems[q.Player.MainJob];

                    q.Quest.SetVar("Wait", q.Player.SystemTime + 3600); // 1 Hour
                    q.Quest.SetStep(requestedBody);
                    // Job check seems to be baked into the DAT.
                    return QuestReply.ProgressEvent(92, 0, Item.BronzeHarness, Item.Robe, Item.Tunic, Item.LeatherVest, Item.Robe, Item.Kenpogi);
                });

            // Section: Finish quest (vars.Prog ~= 0)
            b.State(QuestStatus.Accepted, when: q => q.Quest.Step != 0)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Angelica).OnTrigger(q =>
                {
                    int requestedBody = q.Quest.Step;

                    if (q.Quest.Var("Wait") >= q.Player.SystemTime) // Under time. Quest completed.
                    {
                        if (q.Player.EquipId(Slot.Body) == requestedBody)
                        {
                            return QuestReply.ProgressEvent(96);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(94, PoseGearOf(q.Quest.Step), Item.BronzeHarness, Item.Robe, Item.Tunic, Item.LeatherVest, Item.Robe, Item.Kenpogi);
                        }
                    }
                    else // Over time. Quest failed.
                    {
                        return QuestReply.ProgressEvent(102);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 96, q => // Quest completed
                {
                    if (q.Quest.Complete())
                    {
                        q.Quest.SetMustZone();
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 102, q => // Quest failed.
                {
                    q.Player.DelQuest(QuestLog.Windurst, "A_POSE_BY_ANY_OTHER_NAME");
                    q.Quest.SetStep(0);
                    q.Quest.SetVar("Wait", 0);
                    q.Quest.SetVar("Option", 0);
                    q.Player.AddTitle(Title.LowerThanTheLowestTunnelWorm);
                    q.Player.SetNeedToZone(true);
                });

            // Section: Quest Completed
            b.State(QuestStatus.Completed)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Angelica).OnTrigger(q =>
                    q.Quest.MustZone ? QuestReply.Event(101).SetPriority(101) : null);
        }
    }
}
