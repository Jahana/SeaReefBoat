// Shady Business (Log ID 1, Quest ID 8), a line-for-line port of scripts/quests/bastok/Shady_Business.lua (quest rework
// B3b). Talib : !pos -101.133 4.649 28.803 236. Kept as the Lua file has it: its one section matches any status once Beauty
// and the Galka is completed, so the zinc trade completes the quest again after each completion (350 gil and 30 Norg
// fame each time: the repeat); event 90 begins it only while it is available.
// A deliberate change from the Lua (user ruling 2026-11-05, kept bugs): the Lua's trade also answered while the quest
// was available, so the zinc completed it without its being accepted; the trade now answers only once it is accepted
// (or completed, the repeat).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class ShadyBusiness : QuestScript
    {
        public ShadyBusiness() : base(QuestLog.Bastok, "SHADY_BUSINESS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Shady_Business";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Gil = 350 });

            b.AnyState(when: q => q.Player.HasCompletedQuest(QuestLog.Bastok, "BEAUTY_AND_THE_GALKA"))
                .Npc(Zone.PortBastok, Npc.PortBastok.Talib)
                    .OnTrigger(QuestReply.Event(90))
                    // not while available: accepted first (user ruling 2026-11-05)
                    .OnTrade(q => q.Quest.Status != QuestStatus.Available && q.Trade.Matches((Item.ChunkOfZincOre, 4))
                        ? QuestReply.ProgressEvent(91)
                        : null)
                // completed, it is not accepted again
                .OnEventFinish(Zone.PortBastok, 90, q =>
                {
                    if (q.Quest.Status == QuestStatus.Available)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.PortBastok, 91, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Norg, 30);
                        q.Trade.Complete();
                    }
                });
        }
    }
}
