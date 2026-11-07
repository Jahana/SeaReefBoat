// Painful Memory (Log ID 3, Quest ID 63), a line-for-line port of scripts/quests/jeuno/Painful_Memory.lua (quest rework
// B3b). Mertaire !pos -17 0 -61 245; Waters of Oblivion !pos -289 -45 212 166. BRD AF1: Path of the Bard completed, a
// bard at AF1_QUEST_LEVEL. Mertaire offers 138 (option 0: Option 1; another begins it with Mertaire's Bracelet) then
// 137 (option 1 begins it with the bracelet). Accepted: Mertaire 136; the Waters of Oblivion pop Tros (claimed, hide 0)
// at Prog 0 while the bracelet is held (noAction whatever the pop returns), Tros's death sets Prog 1, then the Waters
// play 8: completed (Paper Knife), fame +7 x3, the bracelet deleted. Completed: Mertaire 135.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class PainfulMemory : QuestScript
    {
        public PainfulMemory() : base(QuestLog.Jeuno, "PAINFUL_MEMORY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Painful_Memory";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.PaperKnife });
            b.Steps(1);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Jeuno, "PATH_OF_THE_BARD") &&
                    q.Player.MainJob == Job.Brd &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF1_QUEST_LEVEL"))
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Mertaire).OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") == 0)
                    {
                        return QuestReply.ProgressEvent(138);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(137); // Player declined
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 137, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.MertairesBracelet);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 138, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetVar("Option", 1); // Player declined
                    }
                    else
                    {
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.MertairesBracelet);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Mertaire).OnTrigger(QuestReply.Event(136))
                .Npc(Zone.RanguemontPass, Npc.RanguemontPass.Tros).OnMobDeath(q => q.Quest.SetStep(1))
                .Npc(Zone.RanguemontPass, Npc.RanguemontPass.WatersofOblivion).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (q.Player.HasKeyItem(KeyItem.MertairesBracelet) && progress == 0)
                    {
                        q.Player.PopFromQm("TROS", claim: true, hide: 0); // TODO: Should not spawn claimed but navmesh+line of sight make it to where the player can't engage with the mob on spawn.
                        return QuestReply.NoAction(); // No message
                    }
                    else if (progress == 1)
                    {
                        return QuestReply.ProgressCutscene(8); // TODO: Confirm agro is dropped when CS is triggered
                    }
                    return null;
                })
                .OnEventFinish(Zone.RanguemontPass, 8, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 7);
                        q.Player.AddFame(FameArea.Bastok, 7);
                        q.Player.AddFame(FameArea.Windurst, 7);
                        q.Player.DelKeyItem(KeyItem.MertairesBracelet);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Mertaire).OnTrigger(QuestReply.Event(135));
        }
    }
}
