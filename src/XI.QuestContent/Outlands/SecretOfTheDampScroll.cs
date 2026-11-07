// Secret of the Damp Scroll (Log ID 5, Quest ID 135), a line-for-line port of
// scripts/quests/outlands/Secret_of_the_Damp_Scroll.lua (quest rework B3b Outlands, group 4). Norg: Shivivi !pos 70 -6.3
// -7.3 (her script paths her about and has no onTrigger, so no script turn; no other quest; the default 85). Horlais Peak:
// Hot_Springs !pos 445 -37.8 -18.6 (no script; San d'Oria's The General's Secret answers its trigger, this quest its trade;
// the default NOTHING_OUT_OF_ORDINARY).
// Offered at Norg fame level 3 from level 10 to a char holding the Damp Scroll (hasItem: any container): Shivivi 31 (Damp
// Scroll) begins it. Accepted: Shivivi 32; the Damp Scroll alone traded to the Hot Springs: 2 (Damp Scroll), whose finish
// runs quest:complete (the Scroll of Jubaku: Ichi, Norg fame 30, Cracker of the Secret Code) and on true confirms the trade.
// Completed: no section.
// Kept as the Lua has it: the offer reads hasItem (the scroll in the Mog House counts); the trade is confirmed only when
// the reward is given.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class SecretOfTheDampScroll : QuestScript
    {
        public SecretOfTheDampScroll() : base(QuestLog.Outlands, "SECRET_OF_THE_DAMP_SCROLL") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/Secret_of_the_Damp_Scroll";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 30,
                FameArea = FameArea.Norg,
                Item = Item.ScrollOfJubakuIchi,
                Title = Title.CrackerOfTheSecretCode,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.Norg) >= 3 &&
                    q.Player.MainLevel >= 10 &&
                    q.Player.HasItem(Item.DampScroll))
                .Npc(Zone.Norg, Npc.Norg.Shivivi).OnTrigger(QuestReply.ProgressEvent(31, Item.DampScroll))
                .OnEventFinish(Zone.Norg, 31, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Norg, Npc.Norg.Shivivi).OnTrigger(QuestReply.Event(32))
                .Npc(Zone.HorlaisPeak, Npc.HorlaisPeak.HotSprings).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.DampScroll))
                    {
                        return QuestReply.ProgressEvent(2, Item.DampScroll);
                    }
                    return null;
                })
                .OnEventFinish(Zone.HorlaisPeak, 2, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                });
        }
    }
}
