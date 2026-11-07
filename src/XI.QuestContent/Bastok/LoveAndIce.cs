// Love and Ice (Log ID 1, Quest ID 43), a line-for-line port of scripts/quests/bastok/Love_and_Ice.lua (quest rework
// B3b). Carmelo : !pos -146.476 -7.48 -10.889 236; Mirror Pond (J-8) : !pos -96.165 1.518 -392.700 111. Offered once the
// Carrier Pigeon Letter has been seen; Carmelo's Song Sheet goes to the Mirror Pond (shown by Zone.lua outside snow and
// blizzards), and the completion sets A Test of True Love's mustZone. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class LoveAndIce : QuestScript
    {
        public LoveAndIce() : base(QuestLog.Bastok, "LOVE_AND_ICE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Love_and_Ice";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 50,
                FameArea = FameArea.Bastok,
                Item = Item.LamiaHarp,
                Title = Title.SorrowDrowner,
            });

            b.State(QuestStatus.Available, when: q => q.Player.SeenKeyItem(KeyItem.CarrierPigeonLetter))
                .Npc(Zone.PortBastok, Npc.PortBastok.Carmelo).OnTrigger(q =>
                    q.Player.HasCompletedQuest(QuestLog.Bastok, "THE_SIRENS_TEAR") &&
                    q.Player.FameLevel(FameArea.Bastok) >= 5
                        ? QuestReply.ProgressEvent(185)
                        : QuestReply.Event(187))
                .OnEventFinish(Zone.PortBastok, 185, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.CarmelosSongSheet);
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortBastok, Npc.PortBastok.Carmelo).OnTrigger(q =>
                    !q.Player.HasKeyItem(KeyItem.CarmelosSongSheet) ? QuestReply.ProgressEvent(186) : null)
                .OnEventFinish(Zone.PortBastok, 186, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetQuestMustZone(QuestLog.Bastok, "A_TEST_OF_TRUE_LOVE");
                    }
                })
                .Npc(Zone.BeaucedineGlacier, Npc.BeaucedineGlacier.MirrorPond1).OnTrigger(q =>
                    q.Player.HasKeyItem(KeyItem.CarmelosSongSheet) ? QuestReply.ProgressEvent(100) : null)
                .OnEventFinish(Zone.BeaucedineGlacier, 100, q => q.Player.DelKeyItem(KeyItem.CarmelosSongSheet));
        }
    }
}
