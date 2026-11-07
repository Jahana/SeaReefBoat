// The Gift (Log ID 4, Quest ID 21), a line-for-line port of scripts/quests/otherAreas/The_Gift.lua (quest rework
// B3b). Selbina: Oswald !pos 47.119 -15.273 7.989 248. Available once Under the Sea is completed and The Sand Charm is
// accepted or completed (status >= QUEST_ACCEPTED), with xi.settings.map.FISHING_ENABLE true: Oswald's fixed 70 (the
// Danceshroom), whose finish with option 50 begins the quest. Accepted: Oswald 71; a Danceshroom alone: 72 (0, the
// Danceshroom), whose finish completes the quest (the Sleep Dagger, Savior of Love), adds fame 10 in San d'Oria and Bastok
// and confirms the trade. Completed, while The Real Gift is available: Oswald's progressEvent 78 with replaceDefault (its
// priority is ReplaceDefault: the modifier replaces Progress).
// Kept as the Lua has it: the fame and the trade come after quest:complete (a full inventory keeps the Danceshroom and
// gives no fame); while The Real Gift is available too (The Sand Charm completed), Oswald's 70 and The Real Gift's 73 are
// both Progress and take turns.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class TheGift : QuestScript
    {
        public TheGift() : base(QuestLog.OtherAreas, "THE_GIFT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/The_Gift";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.SleepDagger,
                Title = Title.SaviorOfLove,
            });

            // Settings.MapFlag(name) (P6): xi.settings.map[name] == true, as the Lua reads it (never Settings.Map(..) == 1: a number 1 is not true in Lua)
            b.State(QuestStatus.Available, when: q => q.Player.QuestStatus(QuestLog.OtherAreas, "UNDER_THE_SEA") == QuestStatus.Completed &&
                                                       q.Player.QuestStatus(QuestLog.OtherAreas, "THE_SAND_CHARM") >= QuestStatus.Accepted &&
                                                       q.Settings.MapFlag("FISHING_ENABLE"))
                .Npc(Zone.Selbina, Npc.Selbina.Oswald).OnTrigger(QuestReply.ProgressEvent(70, Item.Danceshroom)) // Girlfriend needs a shroom
                .OnEventFinish(Zone.Selbina, 70, q =>
                {
                    if (q.Option == 50)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Selbina, Npc.Selbina.Oswald)
                    .OnTrigger(q =>
                    {
                        return QuestReply.Event(71); // They are really hard to come by
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.Danceshroom))
                        {
                            return QuestReply.ProgressEvent(72, 0, Item.Danceshroom); // You found it! Please take this reward
                        }
                        return null;
                    })
                .OnEventFinish(Zone.Selbina, 72, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 10);
                        q.Player.AddFame(FameArea.Bastok, 10);
                        q.Trade.Confirm();
                    }
                });

            b.State(QuestStatus.Completed, when: q => q.Player.QuestStatus(QuestLog.OtherAreas, "THE_REAL_GIFT") == QuestStatus.Available)
                .Npc(Zone.Selbina, Npc.Selbina.Oswald).OnTrigger(QuestReply.ProgressEvent(78).ReplaceDefault());
                // I've been all over Vana'diel, but the inn is my favorite.
        }
    }
}
