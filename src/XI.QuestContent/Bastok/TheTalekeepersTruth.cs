// The Talekeeper's Truth (Log ID 1, Quest ID 55, WAR AF2), a line-for-line port of
// scripts/quests/bastok/WAR_AF2_The_Talekeepers_Truth.lua (quest rework B3b). Phara : !pos 75 0 -80 234; Deidogg : !pos -13
// 7 29 234; qm_talekeeper : !pos 15 -31 -94 143. While available, Phara's 154 sets Prog 1, Deidogg's 160 Prog 2 and his 161
// begins it at Prog 0. Accepted: the ??? in Palborough Mines pops Ni'Ghu Nestfender at Prog 0 (hide 0); the Mottled Quadav
// Egg to Deidogg (162) sets Prog 1, the Parasite Skin (164) Prog 2 and a Timer for the next Vana'diel day; 165 then
// completes it, setting The Talekeeper's Gift's Timer (the next day) and its mustZone. Kept as the Lua file has it: Phara's
// 154 plays again at Prog 1 and 2 while the quest is available (its finish changes nothing then).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TheTalekeepersTruth : QuestScript
    {
        public TheTalekeepersTruth() : base(QuestLog.Bastok, "THE_TALEKEEPERS_TRUTH") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/WAR_AF2_The_Talekeepers_Truth";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Bastok, Item = Item.FightersCalligae });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Bastok, "THE_DOORMAN") &&
                    q.Player.MainJob == Job.War &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF2_QUEST_LEVEL"))
                .Npc(Zone.BastokMines, Npc.BastokMines.Deidogg).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Var("Prog");

                    if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(160);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(161);
                    }
                    return null;
                })
                .Npc(Zone.BastokMines, Npc.BastokMines.Phara).OnTrigger(QuestReply.ProgressEvent(154))
                .OnEventFinish(Zone.BastokMines, 154, q =>
                {
                    if (q.Quest.Var("Prog") == 0)
                    {
                        q.Quest.SetVar("Prog", 1);
                    }
                })
                .OnEventFinish(Zone.BastokMines, 160, q => q.Quest.SetVar("Prog", 2))
                .OnEventFinish(Zone.BastokMines, 161, q =>
                {
                    q.Quest.SetVar("Prog", 0);
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMines, Npc.BastokMines.Deidogg).OnTrade(q =>
                {
                    int questProgress = q.Quest.Var("Prog");

                    if (questProgress == 0 && q.Trade.Matches((Item.MottledQuadavEgg, 1)))
                    {
                        return QuestReply.ProgressEvent(162);
                    }
                    else if (questProgress == 1 && q.Trade.Matches((Item.ParasiteSkin, 1)))
                    {
                        return QuestReply.ProgressEvent(164);
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    int questProgress = q.Quest.Var("Prog");

                    if (questProgress == 1)
                    {
                        return QuestReply.Event(163);
                    }
                    else if (questProgress == 2)
                    {
                        if (q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay)
                        {
                            return QuestReply.ProgressEvent(165);
                        }
                        else
                        {
                            return QuestReply.Event(166);
                        }
                    }
                    return null;
                })
                .OnEventFinish(Zone.BastokMines, 162, q =>
                {
                    q.Trade.Complete();

                    q.Quest.SetVar("Prog", 1);
                })
                .OnEventFinish(Zone.BastokMines, 164, q =>
                {
                    q.Trade.Complete();

                    q.Quest.SetVar("Prog", 2);
                    q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay + 1);
                })
                .OnEventFinish(Zone.BastokMines, 165, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetQuestVar(QuestLog.Bastok, "THE_TALEKEEPERS_GIFT", "Timer", q.Player.VanadielUniqueDay + 1);
                        q.Player.SetQuestMustZone(QuestLog.Bastok, "THE_TALEKEEPERS_GIFT");
                    }
                })
                .Npc(Zone.PalboroughMines, Npc.PalboroughMines.qmtalekeeper).OnTrigger(q =>
                {
                    if (q.Quest.Var("Prog") == 0 && q.Player.PopFromQm(Mob.PalboroughMines.NI_GHU_NESTFENDER, claim: true, hide: 0))
                    {
                        return QuestReply.MessageSpecial(Text.PalboroughMines.SENSE_OF_FOREBODING);
                    }
                    return null;
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.BastokMines, Npc.BastokMines.Deidogg).OnTrigger(QuestReply.Event(166).ReplaceDefault());
        }
    }
}
