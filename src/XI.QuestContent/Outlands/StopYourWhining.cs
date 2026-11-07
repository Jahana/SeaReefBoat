// Stop Your Whining (Log ID 5, Quest ID 132), a line-for-line port of scripts/quests/outlands/Stop_Your_Whining.lua (quest
// rework B3b Outlands, group 3). Norg: Washu !pos 51 -6.3 15.5 (no script; default 80). Yhoator Jungle: qm2 !pos -302 1.7
// 18.2 (no script; default NOTHING_OUT_OF_ORDINARY).
// Offered at Norg fame level 4 from level 10: Washu 21, option 1: begun, the Empty Barrel. Accepted: Washu 22 with the
// barrel, 23 with the Barrel of Opo-opo Brew (quest:complete: the Scroll of Hojo: Ichi, Norg fame 40, Apprentice
// Sommelier; on true the brew taken), else nil. Yhoator's qm2: the empty barrel: TREE_CHECK, the barrel taken, the brew (a
// keyItem reply); the brew held: TREE_FULL; else nil. Completed: Washu 24 (replaceDefault).
// Kept as the Lua has it: 22 has no finish; TREE_CHECK before the key item's own message.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class StopYourWhining : QuestScript
    {
        public StopYourWhining() : base(QuestLog.Outlands, "STOP_YOUR_WHINING") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/Stop_Your_Whining";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.ScrollOfHojoIchi,
                FameArea = FameArea.Norg,
                Fame = 40,
                Title = Title.ApprenticeSommelier,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.Norg) >= 4 &&
                    q.Player.MainLevel >= 10)
                .Npc(Zone.Norg, Npc.Norg.Washu).OnTrigger(QuestReply.ProgressEvent(21))
                .OnEventFinish(Zone.Norg, 21, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.EmptyBarrel);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Norg, Npc.Norg.Washu).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.EmptyBarrel))
                    {
                        return QuestReply.ProgressEvent(22);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.BarrelOfOpoOpoBrew))
                    {
                        return QuestReply.ProgressEvent(23);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Norg, 23, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.BarrelOfOpoOpoBrew);
                    }
                })
                .Npc(Zone.YhoatorJungle, Npc.YhoatorJungle.qm2).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.EmptyBarrel))
                    {
                        q.Text.Special(Text.YhoatorJungle.TREE_CHECK);
                        q.Player.DelKeyItem(KeyItem.EmptyBarrel);
                        return QuestReply.KeyItem(KeyItem.BarrelOfOpoOpoBrew);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.BarrelOfOpoOpoBrew))
                    {
                        return QuestReply.MessageSpecial(Text.YhoatorJungle.TREE_FULL);
                    }
                    return null;
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.Norg, Npc.Norg.Washu).OnTrigger(QuestReply.Event(24).ReplaceDefault());
        }
    }
}
