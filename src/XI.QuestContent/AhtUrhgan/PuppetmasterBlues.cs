// Puppetmaster Blues (PUP AF3; Log ID 6, Quest ID 29), a line-for-line port of
// scripts/quests/ahtUrhgan/PUP_AF3_Puppetmaster_Blues.lua (quest rework B3b Aht Urhgan, draft). Aht Urhgan
// Whitegate: Iruki-Waraki (script: The Wayward Automaton, Operation Teatime; default 259). Bastok Markets:
// Shamarhaan (no script; default 433). Mount Zhayolm: qm8 !pos 760.8 -15 1.7 (no script, no default). Nashmau:
// Sajhra !pos 14.8 0 -61 (script). Talacca Cove: the battlefield Puppetmaster Blues
// (Battlefields/TalaccaCove/PuppetmasterBlues.cs: index 2, entry _1l0, the Toggle Switch and Valkeng's
// Memory Chip required and kept, the quest accepted or completed). Available with Operation Teatime completed, main
// job PUP at AF3_QUEST_LEVEL (50): Iruki-Waraki 782, whose finish begins it. Accepted: Iruki-Waraki 783 (an Event)
// at Prog 0-2, 784 at Prog 3 (Prog 4), 785 (an Event) at Prog 4, else 786 (completed: the Puppetry Taj, Paragon of
// Puppetmaster Excellence); Shamarhaan 437 at Prog 0 (Valkeng's Memory Chip, Prog 1), 438 (an Event) at Prog 1, 439
// at Prog 2 (Prog 3), else 440 (an Event); qm8 at Prog 1 without the Toggle Switch: the switch (KEYITEM_OBTAINED),
// no action, else nil; Sajhra 291 at Prog 4 (Prog 5); the battlefield's 32001 with battlefieldWin 1090: the chip
// and the switch taken, Prog 2. Completed: Iruki-Waraki: player:startEvent(787) inside the handler, which returns
// nil (the lookup then tries his script). Kept as the Lua has it: 782's finish returns quest:begin's value; 32001
// sets Prog 2 at any Prog; the completed section starts its event itself and returns nil.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class PuppetmasterBlues : QuestScript
    {
        public PuppetmasterBlues() : base(QuestLog.AhtUrghan, "PUPPETMASTER_BLUES") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/PUP_AF3_Puppetmaster_Blues";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.PuppetryTaj,
                Title = Title.ParagonOfPuppetmasterExcellence,
            });
            b.Steps(1, 2, 3, 4, 5);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.AhtUrghan, "OPERATION_TEATIME") &&
                    q.Player.MainJob == Job.Pup &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF3_QUEST_LEVEL"))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.IrukiWaraki).OnTrigger(q => QuestReply.ProgressEvent(782))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 782, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.IrukiWaraki).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress <= 2)
                    {
                        return QuestReply.Event(783);
                    }
                    else if (questProgress == 3)
                    {
                        return QuestReply.ProgressEvent(784);
                    }
                    else if (questProgress == 4)
                    {
                        return QuestReply.Event(785);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(786);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 784, q =>
                {
                    q.Quest.SetStep(4);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 786, q =>
                {
                    q.Quest.Complete();
                })
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Shamarhaan).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(437);
                    }
                    else if (questProgress == 1)
                    {
                        return QuestReply.Event(438);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(439);
                    }
                    else
                    {
                        return QuestReply.Event(440);
                    }
                })
                .OnEventFinish(Zone.BastokMarkets, 437, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.ValkengsMemoryChip);
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.BastokMarkets, 439, q =>
                {
                    q.Quest.SetStep(3);
                })
                .Npc(Zone.MountZhayolm, Npc.MountZhayolm.qm8).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1 &&
                        !q.Player.HasKeyItem(KeyItem.ToggleSwitch))
                    {
                        q.Player.GiveKeyItem(KeyItem.ToggleSwitch);
                        return QuestReply.NoAction();
                    }
                    return null;
                })
                .Npc(Zone.Nashmau, Npc.Nashmau.Sajhra).OnTrigger(q =>
                {
                    if (q.Quest.Step == 4)
                    {
                        return QuestReply.ProgressEvent(291);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Nashmau, 291, q =>
                {
                    q.Quest.SetStep(5);
                })
                .OnEventFinish(Zone.TalaccaCove, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.PuppetmasterBlues)
                    {
                        q.Player.DelKeyItem(KeyItem.ValkengsMemoryChip);
                        q.Player.DelKeyItem(KeyItem.ToggleSwitch);
                        q.Quest.SetStep(2);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.IrukiWaraki).OnTrigger(q =>
                {
                    q.Player.StartEvent(787);
                    return null;
                });
        }
    }
}
