// The Curse Collector (Log ID 1, Quest ID 34), a line-for-line port of scripts/quests/bastok/The_Curse_Collector.lua
// (quest rework B2). Zon-Fobun : !pos -241.293 -3 63.406 235; The Mute : !pos -166.230 -1 -73.685 147. Prog holds two
// bits: bit 0 from talking to The Mute, bit 1 from walking into an Afflictor's area (1-6) in Beadeaux. Kept as the Lua
// file has it: The Mute's handler returns no action (its TODO: no event or message is known), so The Mute's own default
// answers; Zon-Fobun has no reminder before Prog 3 (its TODO); areas 1 and 2 need no height, 4 needs y above 35 and 3, 5
// and 6 y above 20.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class TheCurseCollector : QuestScript
    {
        public TheCurseCollector() : base(QuestLog.Bastok, "THE_CURSE_COLLECTOR") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/The_Curse_Collector";

        // the Lua file's handleAfflictorTriggerArea
        private static void HandleAfflictorTriggerArea(IQuestContext q)
        {
            int triggerAreaId = q.TriggerAreaId;
            double yPos = q.Player.YPos;
            double? requiredYPos = null;

            if (triggerAreaId == 4)
            {
                requiredYPos = 35;
            }
            else if (triggerAreaId >= 3)
            {
                requiredYPos = 20;
            }

            if ((requiredYPos == null || yPos > requiredYPos) && !q.Quest.IsVarBitsSet("Prog", 1))
            {
                q.Quest.SetVarBit("Prog", 1);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 40, FameArea = FameArea.Bastok, Item = Item.PoisonCesti });

            // offered at Bastok fame level 4, with the Cursepaper
            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Bastok) >= 4)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.ZonFobun).OnTrigger(QuestReply.ProgressEvent(251))
                .OnEventFinish(Zone.BastokMarkets, 251, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.Cursepaper);
                    q.Quest.Begin();
                });

            var accepted = b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.ZonFobun).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(252);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BastokMarkets, 252, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.Cursepaper);
                    }
                })
                .Npc(Zone.Beadeaux, Npc.Beadeaux.TheMute).OnTrigger(q =>
                {
                    if (!q.Quest.IsVarBitsSet("Prog", 0))
                    {
                        q.Quest.SetVarBit("Prog", 0);
                    }

                    // the Lua file's intentional fallthrough
                    return null;
                });
            for (int area = 1; area <= 6; area++)
            {
                accepted.OnTriggerAreaEnter(Zone.Beadeaux, area, HandleAfflictorTriggerArea);
            }
        }
    }
}
