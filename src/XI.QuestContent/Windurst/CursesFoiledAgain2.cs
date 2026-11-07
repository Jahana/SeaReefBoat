// Curses, Foiled Again!? (2) (Log ID 2, Quest ID 33), a line-for-line port of
// scripts/quests/windurst/Curses_Foiled_Again_2.lua (quest rework B3b). Shantotto : !pos 122 -2 112 239.
// After Curses, Foiled Again! (1) at fame level 2, the next Vana'diel day and a zone change (that quest's mustZone):
// Hiwon-Biwon's 179 (or 284 while Making Headlines is accepted) sets Prog 1; Shantotto's 177 sets Prog 2 and the
// mustZone; after a zone change her 180 (option 3) begins the quest. Two Bomb Arms, a Revival Tree Root and a Lock of
// Hiwon's Hair (from Hiwon-Biwon's 182) complete it (183: fame +20, a Misery Staff, Hexer Vexer) and set Curses, Foiled
// A-Golem!?'s mustZone. Kept as the Lua file has it: Shantotto at Prog 2 with the mustZone returns nil; 182 always
// tries to give the hair (a rare item: a second one fails with the usual message).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class CursesFoiledAgain2 : QuestScript
    {
        public CursesFoiledAgain2() : base(QuestLog.Windurst, "CURSES_FOILED_AGAIN_2") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Curses_Foiled_Again_2";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Windurst,
                Item = Item.MiseryStaff,
                Title = Title.HexerVexer,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Windurst, "CURSES_FOILED_AGAIN_1") &&
                    q.Player.FameLevel(FameArea.Windurst) >= 2)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.Shantotto).OnTrigger(q =>
                {
                    if (q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay &&
                        !q.Player.QuestMustZone(QuestLog.Windurst, "CURSES_FOILED_AGAIN_1"))
                    {
                        int questProgress = q.Quest.Step;

                        if (questProgress == 0)
                        {
                            return QuestReply.Event(178);
                        }
                        else if (questProgress == 1)
                        {
                            return QuestReply.Event(177);
                        }
                        else if (questProgress == 2 && !q.Quest.MustZone)
                        {
                            return QuestReply.ProgressEvent(180, 0, 0, 0, 0, Item.PinchOfBombAsh, Item.BoneChip, Item.BombArm, Item.RevivalTreeRoot);
                        }
                    }
                    else
                    {
                        return QuestReply.Event(174);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.HiwonBiwon).OnTrigger(q =>
                {
                    if (q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay &&
                        !q.Player.QuestMustZone(QuestLog.Windurst, "CURSES_FOILED_AGAIN_1"))
                    {
                        // NOTE: There is interplay between Making Headlines and this quest for dialogue.  We should be safe with
                        // fallthroughs in interaction, but also there may be a better method to implement both.

                        if (q.Player.QuestStatus(QuestLog.Windurst, "MAKING_HEADLINES") == QuestStatus.Accepted)
                        {
                            return QuestReply.ProgressEvent(284);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(179);
                        }
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWalls, 177, q =>
                {
                    q.Quest.SetStep(2);
                    q.Quest.SetMustZone();
                })
                .OnEventFinish(Zone.WindurstWalls, 179, q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.WindurstWalls, 180, q =>
                {
                    if (q.Option == 3)
                    {
                        q.Quest.Begin();
                        q.Player.SetTitle(Title.TarutaruMurderSuspect);
                    }
                })
                .OnEventFinish(Zone.WindurstWalls, 284, q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        q.Quest.SetStep(1);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.Shantotto)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly((Item.BombArm, 2), (Item.RevivalTreeRoot, 1), (Item.LockOfHiwonsHair, 1)))
                        {
                            return QuestReply.ProgressEvent(183, 0, 0, 0, 0, 0, 0, Item.BombArm, Item.RevivalTreeRoot);
                        }

                        return null;
                    })
                    .OnTrigger(QuestReply.Event(181, 0, 0, 0, 0, 0, 0, Item.BombArm, Item.RevivalTreeRoot))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.HiwonBiwon).OnTrigger(QuestReply.ProgressEvent(182))
                .OnEventFinish(Zone.WindurstWalls, 182, q =>
                {
                    // NOTE: This event is blocking, and always attempts to give to player, even
                    // if one is currently in inventory.

                    q.Player.GiveItem(Item.LockOfHiwonsHair);
                })
                .OnEventFinish(Zone.WindurstWalls, 183, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();

                        q.Player.SetQuestMustZone(QuestLog.Windurst, "CURSES_FOILED_A_GOLEM");
                    }
                });

            b.State(QuestStatus.Completed, when: q =>
                    !q.Player.HasCompletedQuest(QuestLog.Windurst, "CURSES_FOILED_A_GOLEM"))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.HiwonBiwon).OnTrigger(QuestReply.Event(185).ReplaceDefault())
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.Shantotto).OnTrigger(QuestReply.Event(184).ReplaceDefault());
        }
    }
}
