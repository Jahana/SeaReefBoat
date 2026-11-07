// Lovers in the Dusk (Log ID 1, Quest ID 63), a line-for-line port of scripts/quests/bastok/Lovers_in_the_Dusk.lua
// (quest rework B3b). Carmelo : !pos -146.476 -7.48 -10.889 236; qm4 in The Sanctuary of Zi'Tah, which plays 204 only
// at dusk (VanadielTOTD() == xi.time.DUSK). The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class LoversInTheDusk : QuestScript
    {
        public LoversInTheDusk() : base(QuestLog.Bastok, "LOVERS_IN_THE_DUSK") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Lovers_in_the_Dusk";

        // xi.time.DUSK
        private const int Dusk = 5;

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 70, FameArea = FameArea.Bastok, Item = Item.SirenFlute });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Bastok, "A_TEST_OF_TRUE_LOVE") &&
                    q.Player.FameLevel(FameArea.Bastok) >= 6 &&
                    !q.Quest.MustZone)
                .Npc(Zone.PortBastok, Npc.PortBastok.Carmelo).OnTrigger(QuestReply.ProgressEvent(275))
                .OnEventFinish(Zone.PortBastok, 275, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.ChansonDeLiberte);
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortBastok, Npc.PortBastok.Carmelo).OnTrigger(QuestReply.Event(276))
                .Npc(Zone.TheSanctuaryOfZitah, Npc.TheSanctuaryOfZitah.qm4).OnTrigger(q =>
                    q.Player.VanadielTotd == Dusk ? QuestReply.ProgressEvent(204) : null)
                .OnEventFinish(Zone.TheSanctuaryOfZitah, 204, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.ChansonDeLiberte);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.PortBastok, Npc.PortBastok.Carmelo).OnTrigger(QuestReply.Event(277).ReplaceDefault());
        }
    }
}
