// The Usual (Log ID 1, Quest ID 27), a line-for-line port of scripts/quests/bastok/The_Usual.lua (quest rework B3b).
// Hilda : !pos -163 -8 13 236; Raibaht : !pos -27 -10 -1 237. The King Truffle buys the Steaming Sheep Invitation,
// Raibaht's 510 (option 0) sets Prog 1, and Hilda's 136 completes it. The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TheUsual : QuestScript
    {
        public TheUsual() : base(QuestLog.Bastok, "THE_USUAL") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/The_Usual";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 50,
                FameArea = FameArea.Bastok,
                Item = Item.SpeedBow,
                Title = Title.SteamingSheepRegular,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Bastok, "CIDS_SECRET") &&
                    q.Player.FameLevel(FameArea.Bastok) >= 5)
                .Npc(Zone.PortBastok, Npc.PortBastok.Hilda).OnTrigger(QuestReply.ProgressEvent(134))
                .OnEventFinish(Zone.PortBastok, 134, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortBastok, Npc.PortBastok.Hilda)
                    .OnTrade(q =>
                        !q.Player.HasKeyItem(KeyItem.SteamingSheepInvitation) && q.Trade.Matches((Item.KingTruffle, 1))
                            ? QuestReply.ProgressEvent(135)
                            : null)
                    .OnTrigger(q => q.Quest.Step == 1 ? QuestReply.ProgressEvent(136) : null)
                .OnEventFinish(Zone.PortBastok, 135, q =>
                {
                    q.Trade.Complete();

                    q.Player.GiveKeyItem(KeyItem.SteamingSheepInvitation);
                })
                .OnEventFinish(Zone.PortBastok, 136, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.SteamingSheepInvitation);
                    }
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Raibaht).OnTrigger(q =>
                    q.Player.HasKeyItem(KeyItem.SteamingSheepInvitation) && q.Quest.Step == 0 ? QuestReply.ProgressEvent(510) : null)
                .OnEventFinish(Zone.Metalworks, 510, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.SetStep(1);
                    }
                });
        }
    }
}
