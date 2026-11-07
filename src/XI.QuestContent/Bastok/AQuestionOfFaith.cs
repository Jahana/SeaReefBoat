// A Question of Faith (Log ID 1, Quest ID 77), a line-for-line port of scripts/quests/bastok/A_Question_of_Faith.lua
// (quest rework B2). Ayame : !pos 132 -18 33 237; Virnage : !pos 0.7 -0.0 49 234; Rakorok : !pos 158 13 -42 11. Kept as
// the Lua file has it: Rakorok's pop is npcUtil.popFromQM with hide = 0, so Rakorok (an NPC, not a ???) stays shown; a
// talk while Bugallug is still up pops nothing and returns no action (Rakorok's own default answers).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class AQuestionOfFaith : QuestScript
    {
        public AQuestionOfFaith() : base(QuestLog.Bastok, "A_QUESTION_OF_FAITH") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/A_Question_of_Faith";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 40, FameArea = FameArea.Bastok, Gil = 3000 });

            // offered at Bastok fame level 4 once Out of the Depths is completed
            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.Bastok) >= 4 &&
                    q.Player.HasCompletedQuest(QuestLog.Bastok, "OUT_OF_THE_DEPTHS"))
                .Npc(Zone.Metalworks, Npc.Metalworks.Ayame).OnTrigger(QuestReply.ProgressEvent(875))
                .OnEventFinish(Zone.Metalworks, 875, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMines, Npc.BastokMines.Virnage).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.DawnTalisman) && q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(239, 0, KeyItem.DawnTalisman);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.DawnTalisman))
                    {
                        return QuestReply.Event(242, 0, KeyItem.DawnTalisman);
                    }
                    else if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(241);
                    }
                    return null;
                })
                // option 1 takes the Dawn Talisman
                .OnEventFinish(Zone.BastokMines, 239, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.GiveKeyItem(KeyItem.DawnTalisman);
                    }
                })
                .OnEventFinish(Zone.BastokMines, 241, q => q.Quest.Complete())
                // Rakorok pops Bugallug for the talisman holder; after the kill, event 6 takes the talisman
                .Npc(Zone.OldtonMovalpolos, Npc.OldtonMovalpolos.Rakorok).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.DawnTalisman) && q.Quest.Step == 0)
                    {
                        if (q.Player.PopFromQm(Mob.OldtonMovalpolos.BUGALLUG, claim: true, hide: 0))
                        {
                            q.Text.Special(Text.OldtonMovalpolos.ALTANA_DIE);
                            return QuestReply.MessageSpecial(Text.OldtonMovalpolos.MONSTER_APPEARED);
                        }
                    }
                    else if (q.Quest.Step == 1 && q.Player.HasKeyItem(KeyItem.DawnTalisman))
                    {
                        return QuestReply.ProgressEvent(6, 11, KeyItem.DawnTalisman);
                    }
                    return null;
                })
                .Npc(Zone.OldtonMovalpolos, Npc.OldtonMovalpolos.Bugallug).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.OldtonMovalpolos, 6, q =>
                {
                    q.Player.DelKeyItem(KeyItem.DawnTalisman);
                    q.Text.Special(Text.OldtonMovalpolos.WAS_TAKEN_FROM_YOU, 0, KeyItem.DawnTalisman);
                });
        }
    }
}
