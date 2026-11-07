// Wrath of the Opo-Opos (Log ID 5, Quest ID 160), a line-for-line port of
// scripts/quests/outlands/Wrath_of_the_Opo_Opos.lua (quest rework B3b Outlands, group 5). Yuhtunga Jungle:
// Cermet_Headstone !pos 491 20 301 (no script; default CANNOT_REMOVE_FRAG; Rhapsodies of Zilart 5 Headstone
// Pilgrimage's monument: its 201 finish adds this quest; with the Fire Fragment held it answers nil until this quest is
// completed, then ALREADY_OBTAINED_FRAG; completed, its replaceDefault ZILART_MONUMENT ranks below this quest's
// message: #kinds).
// Accepted only (the mission begins it): the headstone answers TOUCHING_RED_JEWEL; a Garnet alone traded plays 202 (the
// garnet), whose finish runs quest:complete (the Opo-opo Necklace, Friend of the Opo-opos) and on true confirms the
// trade.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class WrathOfTheOpoOpos : QuestScript
    {
        public WrathOfTheOpoOpos() : base(QuestLog.Outlands, "WRATH_OF_THE_OPO_OPOS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/Wrath_of_the_Opo_Opos";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.OpoOpoNecklace,
                Title = Title.FriendOfTheOpoOpos,
            });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.YuhtungaJungle, Npc.YuhtungaJungle.CermetHeadstone)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.Garnet))
                        {
                            return QuestReply.ProgressEvent(202, Item.Garnet);
                        }
                        return null;
                    })
                    .OnTrigger(QuestReply.MessageSpecial(Text.YuhtungaJungle.TOUCHING_RED_JEWEL))
                .OnEventFinish(Zone.YuhtungaJungle, 202, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                });
        }
    }
}
