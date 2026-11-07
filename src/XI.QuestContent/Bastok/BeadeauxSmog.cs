// Beadeaux Smog (Log ID 1, Quest ID 33), a line-for-line port of scripts/quests/bastok/Beadeaux_Smog.lua (quest rework
// B3b). High Bear : !pos 25.231 -14.999 4.552 237; qm1 (for the key item) : !pos -58.873 1.026 -116.665 147, shown only in
// rain or a squall (Beadeaux's Zone.lua). Kept as the Lua file has it: the Corrupted Dirt is not taken back on completion.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class BeadeauxSmog : QuestScript
    {
        public BeadeauxSmog() : base(QuestLog.Bastok, "BEADEAUX_SMOG") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Beadeaux_Smog";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 40, FameArea = FameArea.Bastok, Title = Title.BeadeauxSurveyor });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Bastok) >= 4)
                .Npc(Zone.Metalworks, Npc.Metalworks.HighBear).OnTrigger(QuestReply.ProgressEvent(731))
                .OnEventFinish(Zone.Metalworks, 731, q => q.Quest.Begin());

            // while the quest is accepted, High Bear uses his default text
            b.State(QuestStatus.Accepted, when: q => !q.Player.HasKeyItem(KeyItem.CorruptedDirt))
                .Npc(Zone.Beadeaux, Npc.Beadeaux.qm1).OnTrigger(q => QuestReply.KeyItem(KeyItem.CorruptedDirt));

            // the hand-in
            b.State(QuestStatus.Accepted, when: q => q.Player.HasKeyItem(KeyItem.CorruptedDirt))
                .Npc(Zone.Metalworks, Npc.Metalworks.HighBear).OnTrigger(QuestReply.ProgressEvent(732))
                .OnEventFinish(Zone.Metalworks, 732, q =>
                {
                    if (q.Player.GiveItem(Item.Chakram))
                    {
                        q.Quest.Complete();
                    }
                });
        }
    }
}
