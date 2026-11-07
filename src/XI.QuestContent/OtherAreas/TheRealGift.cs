// The Real Gift (Log ID 4, Quest ID 22), a line-for-line port of scripts/quests/otherAreas/The_Real_Gift.lua (quest
// rework B3b). Selbina: Oswald !pos 47.119 -15.273 7.989 248. Available once Under the Sea and The Sand Charm are
// completed (The Sand Charm's status >= QUEST_COMPLETED), with xi.settings.map.FISHING_ENABLE true: Oswald's fixed 73 (the
// Shall Shell), whose finish with option 50 begins the quest. Accepted: Oswald 74 (the Shall Shell); a Shall Shell alone:
// 75, whose finish completes the quest (the Glass Fiber Fishing Rod, The Love Doctor), adds fame 10 in San d'Oria and
// Bastok and confirms the trade. Completed: Oswald's progressEvent 76 with replaceDefault.
// Kept as the Lua has it: The Gift need not be done first (only Under the Sea and The Sand Charm are checked), so while
// both are available Oswald's 70 and 73 (both Progress) take turns, and while The Gift is accepted its 71 (Event) loses to
// 73 every time.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class TheRealGift : QuestScript
    {
        public TheRealGift() : base(QuestLog.OtherAreas, "THE_REAL_GIFT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/The_Real_Gift";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.GlassFiberFishingRod,
                Title = Title.TheLoveDoctor,
            });

            // Settings.MapFlag(name) (P6): xi.settings.map[name] == true, as the Lua reads it (never Settings.Map(..) == 1: a number 1 is not true in Lua)
            b.State(QuestStatus.Available, when: q => q.Player.QuestStatus(QuestLog.OtherAreas, "UNDER_THE_SEA") == QuestStatus.Completed &&
                                                       q.Player.QuestStatus(QuestLog.OtherAreas, "THE_SAND_CHARM") >= QuestStatus.Completed &&
                                                       q.Settings.MapFlag("FISHING_ENABLE"))
                .Npc(Zone.Selbina, Npc.Selbina.Oswald).OnTrigger(QuestReply.ProgressEvent(73, Item.ShallShell)) // Bring me a shall shell
                .OnEventFinish(Zone.Selbina, 73, q =>
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
                        return QuestReply.Event(74, Item.ShallShell); // Shall shells yield pearls
                    })
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.ShallShell))
                        {
                            return QuestReply.ProgressEvent(75); // You're so fantastic! Thank you!
                        }
                        return null;
                    })
                .OnEventFinish(Zone.Selbina, 75, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 10);
                        q.Player.AddFame(FameArea.Bastok, 10);
                        q.Trade.Confirm();
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.Selbina, Npc.Selbina.Oswald).OnTrigger(QuestReply.ProgressEvent(76).ReplaceDefault());
                // Thanks for all you've done.
        }
    }
}
