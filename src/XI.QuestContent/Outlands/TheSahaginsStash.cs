// The Sahagin's Stash (Log ID 5, Quest ID 136), a line-for-line port of scripts/quests/outlands/The_Sahagins_Stash.lua
// (quest rework B3b Outlands, group 4). Norg: Laisrean !pos -3.5 0 21.7 (no script, no other quest; the default 83). Sea
// Serpent Grotto: qm2 !pos 295.3 27.1 213 (no script, no other quest, no default: nothing).
// Offered at Norg fame level 4 from level 5: Laisrean 33 (option [7] = the Option var: 1 once declined); option 1 begins
// it, any other sets Option 1. Accepted: Laisrean 35 (Sea Serpent Statue) with the statue (quest:complete: the Scroll of
// Utsusemi: Ichi, Norg fame 40, Treasure House Ransacker; on true the statue taken), else 34. Sea Serpent Grotto's qm2
// without the statue: 1, whose finish gives it. Completed: no section.
// Kept as the Lua has it: the Option var survives until complete's cleanup; qm2's 1 plays again whenever the statue is
// gone (no Prog).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class TheSahaginsStash : QuestScript
    {
        public TheSahaginsStash() : base(QuestLog.Outlands, "THE_SAHAGINS_STASH") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/The_Sahagins_Stash";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 40,
                FameArea = FameArea.Norg,
                Item = Item.ScrollOfUtsusemiIchi,
                Title = Title.TreasureHouseRansacker,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.Norg) >= 4 &&
                    q.Player.MainLevel >= 5)
                .Npc(Zone.Norg, Npc.Norg.Laisrean).OnTrigger(q =>
                {
                    int hasDeclined = q.Quest.Var("Option");

                    return QuestReply.ProgressEvent(33).Option(7, hasDeclined);
                })
                .OnEventFinish(Zone.Norg, 33, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                    else
                    {
                        q.Quest.SetVar("Option", 1);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Norg, Npc.Norg.Laisrean).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.SeaSerpentStatue))
                    {
                        return QuestReply.ProgressEvent(35, KeyItem.SeaSerpentStatue);
                    }
                    else
                    {
                        return QuestReply.Event(34);
                    }
                })
                .OnEventFinish(Zone.Norg, 35, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.SeaSerpentStatue);
                    }
                })
                .Npc(Zone.SeaSerpentGrotto, Npc.SeaSerpentGrotto.qm2).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.SeaSerpentStatue))
                    {
                        return QuestReply.ProgressEvent(1);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SeaSerpentGrotto, 1, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.SeaSerpentStatue);
                });
        }
    }
}
