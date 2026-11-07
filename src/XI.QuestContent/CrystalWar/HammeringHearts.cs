// Hammering Hearts (Log ID 7, Quest ID 14), a line-for-line port of scripts/quests/crystalWar/Hammering_Hearts.lua
// (quest rework B3b Crystal War group 11). Bastok Markets [S]: Scarred Shark !pos -233.989 -2 67.638 (no script, no
// default).
// Available: Scarred Shark 40, whose finish on option 0 begins it. Accepted, Prog 0: Scarred Shark 43 (a progress
// event: the reminder); a Heavy Quadav Chestplate and a Heavy Quadav Backplate alone: 41, whose finish confirms the
// trade, sets Prog 1 and the char's local var Quest[7][14]NeedToZone 1. Prog 1: the local var 1: 44; else (zoned: the
// local var gone): 42, whose finish completes it (the Trainee Hammer). Completed: 45 (an Event, replaceDefault).
// Kept as the Lua has it: 44 has no finish; 42's completion can fail on a full inventory (the quest stays at Prog 1).
// Rival containers: none. Scarred Shark has no script and no other quest line: the reply kinds are pinned in
// tests/XI.Verify.Quests/CrystalWarGroup11.cs.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class HammeringHearts : QuestScript
    {
        public HammeringHearts() : base(QuestLog.CrystalWar, "HAMMERING_HEARTS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/Hammering_Hearts";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1);

            b.Reward(new QuestReward
            {
                Item = Item.TraineeHammer,
            });

            // Section: Talk to Scarred Shark in Bastok Markets (S) at (G-5) for a cutscene.
            b.State(QuestStatus.Available)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.ScarredShark).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(40);
                })
                .OnEventFinish(Zone.BastokMarketsS, 40, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            // 0: Trade a Heavy Quadav Backplate and a Heavy Quadav Chestplate to Scarred Shark.
            b.State(QuestStatus.Accepted, step: 0)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.ScarredShark).OnTrigger(q =>
                {
                    // Reminder
                    return QuestReply.ProgressEvent(43);
                })
                .OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.HeavyQuadavChestplate, Item.HeavyQuadavBackplate))
                    {
                        return QuestReply.ProgressEvent(41);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BastokMarketsS, 41, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetStep(1);
                    q.Player.SetLocalVar("Quest[7][14]NeedToZone", 1);
                });

            // 1: Zone, then talk to Scarred Shark again for a final cutscene and your reward.
            b.State(QuestStatus.Accepted, step: 1)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.ScarredShark).OnTrigger(q =>
                {
                    if (q.Player.LocalVar("Quest[7][14]NeedToZone") == 1)
                    {
                        return QuestReply.ProgressEvent(44);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(42);
                    }
                })
                .OnEventFinish(Zone.BastokMarketsS, 42, q =>
                {
                    q.Quest.Complete();
                });

            // Section: Quest completed. New default text
            b.State(QuestStatus.Completed)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.ScarredShark).OnTrigger(q =>
                {
                    return QuestReply.Event(45).ReplaceDefault();
                });
        }
    }
}
