// Out of One's Shell (Log ID 1, Quest ID 5), a line-for-line port of scripts/quests/bastok/Out_of_Ones_Shell.lua (quest
// rework B3b). Ronan : !pos 84.712 -8.772 20.301 236. Ronan's 82 (The Quadav's Curse completed, Bastok fame level 2)
// begins it; three Shell Bugs traded at Prog 0 play 84 (the bugs taken, Prog 1, the mustZone set); at Prog 1 Ronan plays
// 85 until the char has zoned, then 86, which completes it (Monk's Headgear, Shell Outer, fame +20). Mine Konte and
// Panana play 83 at Prog 0; Ronan's other lines are RONAN_DIALOG_1. Once it is completed Corann plays 88 and Ronan 89
// (replaceDefault); The Quadav's Curse (C#) plays Corann's 87 until this quest is completed.
// The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class OutOfOnesShell : QuestScript
    {
        public OutOfOnesShell() : base(QuestLog.Bastok, "OUT_OF_ONES_SHELL") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Out_of_Ones_Shell";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Bastok,
                Item = Item.MonksHeadgear,
                Title = Title.ShellOuter,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Bastok, "THE_QUADAVS_CURSE") &&
                    q.Player.FameLevel(FameArea.Bastok) >= 2)
                .Npc(Zone.PortBastok, Npc.PortBastok.Ronan).OnTrigger(QuestReply.ProgressEvent(82))
                .OnEventFinish(Zone.PortBastok, 82, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortBastok, Npc.PortBastok.Ronan)
                    .OnTrade(q =>
                        q.Quest.Step == 0 && q.Trade.Matches((Item.ShellBug, 3)) ? QuestReply.ProgressEvent(84) : null)
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Step == 1)
                        {
                            if (!q.Quest.MustZone)
                            {
                                return QuestReply.ProgressEvent(86);
                            }
                            else
                            {
                                return QuestReply.Event(85);
                            }
                        }
                        else
                        {
                            return QuestReply.MessageName(Text.PortBastok.RONAN_DIALOG_1);
                        }
                    })
                .Npc(Zone.PortBastok, Npc.PortBastok.MineKonte).OnTrigger(q =>
                    q.Quest.Step == 0 ? QuestReply.Event(83) : null)
                .Npc(Zone.PortBastok, Npc.PortBastok.Panana).OnTrigger(q =>
                    q.Quest.Step == 0 ? QuestReply.Event(83) : null)
                .OnEventFinish(Zone.PortBastok, 84, q =>
                {
                    q.Trade.Complete();

                    q.Quest.SetStep(1);
                    q.Quest.SetMustZone();
                })
                .OnEventFinish(Zone.PortBastok, 86, q => q.Quest.Complete());

            b.State(QuestStatus.Completed)
                .Npc(Zone.PortBastok, Npc.PortBastok.Corann).OnTrigger(QuestReply.Event(88).ReplaceDefault())
                .Npc(Zone.PortBastok, Npc.PortBastok.Ronan).OnTrigger(QuestReply.Event(89).ReplaceDefault());
        }
    }
}
