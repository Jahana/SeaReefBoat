// Ducal Hospitality (Log ID 3, Quest ID 68), a line-for-line port of scripts/quests/jeuno/Ducal_Hospitality.lua (quest
// rework B3b). Taillegeas - !gotoid 17772720. Available once CoP A Vessel Without a Captain is completed (the CoP
// mission past it) at Jeuno fame level 4: Taillegeas draws one of five item sets (math.randomInt(1, 5), kept in ItemSet)
// and plays 10057 with its items; any option but 10 begins it. Accepted: 10059 with the set's items; a trade of exactly
// the set's items plays 10058, whose finish completes it (4000 gil, Ducal Dupe), fame +5 in the three nations, takes the
// trade and sets the mustZone. Completed (repeatable): ItemSet over 0: 10059 with the set; else with the mustZone set:
// 10060; else a new draw and 10057 (option 10: ItemSet 0); the set traded: 10058, whose finish while the mustZone is
// clear takes the trade, sets the mustZone, ItemSet 0, gives 4000 gil and fame +5 in the three nations. Every event's
// option 0 is 1 once CoP The Warrior's Path is completed.
// Kept Lua quirks: the accepted state reads questItemSets[ItemSet] with no check, so ItemSet 0 while accepted (a GM's
// quest status only) is a Lua error (questItemSets[0] is nil) in the trigger; the trade checks ItemSet > 0 first; the
// first completion's 10058 finish also runs the completed state's 10058 finish in the same lookup, which the mustZone
// just set skips; the completed 10057 finish with any option but 10 keeps the drawn set.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class DucalHospitality : QuestScript
    {
        public DucalHospitality() : base(QuestLog.Jeuno, "DUCAL_HOSPITALITY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Ducal_Hospitality";

        // questItemSets[1..5]: the items and the text option; [0] is nil in the Lua (a Lua error when read)
        static readonly (int[] Items, int TextOption)[] QuestItemSets =
        {
            (null, 0),
            (new[] { Item.HandfulOfCounterfeitGil, Item.LoafOfGoblinBread, Item.GoblinDoll, Item.GoblinMushpot, Item.GoblinPie }, 0),
            (new[] { Item.PileOfChocoboBedding, Item.SetOfKaiserinCosmetics, Item.MyFirstMagicKit }, 1),
            (new[] { Item.PieceOfEasternPaper, Item.Shuriken, Item.SilverObi, Item.TonosamaRiceBall }, 4),
            (new[] { Item.Dart, Item.KongouInaho, Item.NyumomoDoll, Item.ConeOfSnollGelato }, 4),
            (new[] { Item.CopyOfFernansDiaries, Item.NaphillePochette, Item.SpheneEarring, Item.TurquoiseRing }, 4),
        };

        // #questItemSets: the table's [0] is nil, so its length less that slot
        static readonly int ItemSetCount = QuestItemSets.Length - 1;

        static int WarriorsPathComplete(IQuestContext q) =>
            q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.TheWarriorsPath) ? 1 : 0;

        // items[n] or 0 (1-based)
        static int SetItem(int[] items, int n) => n <= items.Length ? items[n - 1] : 0;

        // the { [0] = warriorsPathComplete, [1] .. [5] = the set's items, [6] = textOption } options table; set 0 (nil in
        // the Lua) throws, as indexing nil does
        static QuestReply SetEvent(int csid, int warriorsPathComplete, int questItemSet)
        {
            var set = QuestItemSets[questItemSet];
            if (set.Items == null)
            {
                throw new System.InvalidOperationException("attempt to index a nil value (questItemSets[" + questItemSet + "])");
            }

            return QuestReply.ProgressEvent(csid)
                .Option(0, warriorsPathComplete)
                .Option(1, set.Items[0])
                .Option(2, set.Items[1])
                .Option(3, set.Items[2])
                .Option(4, SetItem(set.Items, 4))
                .Option(5, SetItem(set.Items, 5))
                .Option(6, set.TextOption);
        }

        static QuestReply SetTrade(IQuestContext q)
        {
            int warriorsPathComplete = WarriorsPathComplete(q);
            int questItemSet = q.Quest.Var("ItemSet");
            if (questItemSet > 0 &&
                q.Trade.HasExactly(QuestItemSets[questItemSet].Items))
            {
                return QuestReply.ProgressEvent(10058)
                    .Option(0, warriorsPathComplete)
                    .Option(6, questItemSet - 1);
            }
            return null;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Gil = 4000, Title = Title.DucalDupe });

            // Quest: AVAILABLE
            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.AVesselWithoutACaptain) &&
                    q.Player.FameLevel(FameArea.Jeuno) >= 4)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Taillegeas).OnTrigger(q =>
                {
                    int warriorsPathComplete = WarriorsPathComplete(q);
                    int questItemSet = q.Player.RandomInt(1, ItemSetCount);
                    q.Quest.SetVar("ItemSet", questItemSet);
                    return SetEvent(10057, warriorsPathComplete, questItemSet);
                })
                .OnEventFinish(Zone.RuludeGardens, 10057, q =>
                {
                    if (q.Option == 10)
                    {
                        return;
                    }
                    else
                    {
                        q.Quest.Begin();
                    }
                });

            // Quest: ACCEPTED
            b.State(QuestStatus.Accepted)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Taillegeas)
                    .OnTrigger(q =>
                    {
                        int warriorsPathComplete = WarriorsPathComplete(q);
                        int questItemSet = q.Quest.Var("ItemSet");
                        return SetEvent(10059, warriorsPathComplete, questItemSet);
                    })
                    .OnTrade(SetTrade)
                .OnEventFinish(Zone.RuludeGardens, 10058, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 5);
                        q.Player.AddFame(FameArea.Bastok, 5);
                        q.Player.AddFame(FameArea.Windurst, 5);
                        q.Trade.Confirm();
                        q.Quest.SetMustZone();
                    }
                });

            // Quest: COMPLETED
            // Allows for repeats of the quest
            b.State(QuestStatus.Completed)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Taillegeas)
                    .OnTrigger(q =>
                    {
                        int warriorsPathComplete = WarriorsPathComplete(q);
                        int questItemSet = q.Quest.Var("ItemSet");
                        if (questItemSet > 0)
                        {
                            // Quest has been re-started and an item-set was assigned to the player
                            return SetEvent(10059, warriorsPathComplete, questItemSet);
                        }
                        else if (q.Quest.MustZone)
                        {
                            // Player just completed the quest, needs to zone before re-starting
                            return QuestReply.ProgressEvent(10060).Option(0, warriorsPathComplete);
                        }
                        else
                        {
                            // Player has the quest active and needs the item list again
                            questItemSet = q.Player.RandomInt(1, ItemSetCount);
                            q.Quest.SetVar("ItemSet", questItemSet);
                            return SetEvent(10057, warriorsPathComplete, questItemSet);
                        }
                    })
                    .OnTrade(SetTrade)
                .OnEventFinish(Zone.RuludeGardens, 10057, q =>
                {
                    if (q.Option == 10)
                    {
                        q.Quest.SetVar("ItemSet", 0);
                    }
                })
                .OnEventFinish(Zone.RuludeGardens, 10058, q =>
                {
                    if (!q.Quest.MustZone) // if we don't check this we get double reward immediately after first completion
                    {
                        q.Trade.Confirm();
                        q.Quest.SetMustZone();
                        q.Quest.SetVar("ItemSet", 0);
                        q.Player.GiveCurrency("gil", 4000);
                        q.Player.AddFame(FameArea.Sandoria, 5);
                        q.Player.AddFame(FameArea.Bastok, 5);
                        q.Player.AddFame(FameArea.Windurst, 5);
                    }
                });
        }
    }
}
