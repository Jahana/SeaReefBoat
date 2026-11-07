// The Eleventh's Hour (Log ID 1, Quest ID 7), a line-for-line port of scripts/quests/bastok/The_Elevenths_Hour.lua (quest
// rework B3b). Babenn : !pos 73 -1 34 234; Elki : !pos -17.087 -0.05 52.745 234; Old Toolbox : !pos 113.649 -32.8 79.617
// 143. Elki's 43 (Hearts of Mythril completed, Bastok fame level 3, the quest's mustZone clear: Hearts of Mythril sets it
// on completion) begins it on option 1; the Old Toolbox's 23 gives the KI Old Toolbox on option 0; Elki's 44 sets Prog 1;
// Babenn's 45 completes it (Small Sword, Pursuer of the Truth, fame +30) and deletes the toolbox. Pavvke's 48 plays
// while it is accepted.
// Kept as the Lua file has it: Parraggoh's 46 is in the accepted section but asks whether the quest is completed, so it
// never plays (getQuestStatus answers completed whenever the complete bit is set); Elki's 44 has no Prog guard.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TheEleventhsHour : QuestScript
    {
        public TheEleventhsHour() : base(QuestLog.Bastok, "THE_ELEVENTHS_HOUR") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/The_Elevenths_Hour";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 30,
                FameArea = FameArea.Bastok,
                Item = Item.SmallSword,
                Title = Title.PursuerOfTheTruth,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Bastok, "HEARTS_OF_MYTHRIL") &&
                    q.Player.FameLevel(FameArea.Bastok) >= 3 &&
                    !q.Quest.MustZone)
                .Npc(Zone.BastokMines, Npc.BastokMines.Elki).OnTrigger(QuestReply.ProgressEvent(43))
                .OnEventFinish(Zone.BastokMines, 43, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMines, Npc.BastokMines.Babenn).OnTrigger(q =>
                    q.Quest.Step == 1 ? QuestReply.ProgressEvent(45) : null)
                .Npc(Zone.BastokMines, Npc.BastokMines.Elki).OnTrigger(q =>
                    q.Player.HasKeyItem(KeyItem.OldToolbox) ? QuestReply.ProgressEvent(44) : null)
                .Npc(Zone.BastokMines, Npc.BastokMines.Parraggoh).OnTrigger(q =>
                    q.Player.HasCompletedQuest(QuestLog.Bastok, "THE_ELEVENTHS_HOUR") ? QuestReply.Event(46) : null)
                .Npc(Zone.BastokMines, Npc.BastokMines.Pavvke).OnTrigger(QuestReply.Event(48))
                .OnEventFinish(Zone.BastokMines, 44, q => q.Quest.SetStep(1))
                .OnEventFinish(Zone.BastokMines, 45, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.OldToolbox);
                    }
                })
                .Npc(Zone.PalboroughMines, Npc.PalboroughMines.OldToolbox).OnTrigger(q =>
                    !q.Player.HasKeyItem(KeyItem.OldToolbox) ? QuestReply.ProgressEvent(23) : null)
                .OnEventFinish(Zone.PalboroughMines, 23, q =>
                {
                    if (q.Option == 0)
                    {
                        // player:addKeyItem (not npcUtil.giveKeyItem: no message)
                        q.Player.AddKeyItem(KeyItem.OldToolbox);
                    }
                });
        }
    }
}
