// Moghouse Exposition (hidden quest moghouseExpo), ported from scripts/quests/hiddenQuests/Moghouse_Exposition.lua
// (quest rework B3b hidden quests). Every Mog House zone (xi.moghouse.moghouseZones) shares two section tables.
// Char creation writes HQuest[moghouseExpo]notSeen 1 (scripts/globals/player.lua, charCreate).
// Section 1 (in the Mog House of the home nation, notSeen 1): the zone-in plays 30000; its finish: quest:complete (no
// reward: the vars cleared), then oNation 1.
// Section 2 (in a Mog House, not the home nation's, oNation 1): the Moogle's 30001 (progressEvent); its finish:
// quest:complete (the vars cleared).
// Kept as the Lua has it: 30000's finish writes oNation after the completion's cleanup, so the second section's var
// outlives the completion; a zone-in into a Mog House zone outside the Mog House reads the check too (false: not in
// the Mog House).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.HiddenQuests
{
    public sealed class MoghouseExposition : HiddenQuestScript
    {
        public MoghouseExposition() : base("moghouseExpo") { }

        public override string LegacyPath => "scripts/quests/hiddenQuests/Moghouse_Exposition";

        // xi.moghouse.moghouseZones, in its order (scripts/globals/moghouse.lua)
        private static readonly ushort[] MoghouseZones =
        {
            Zone.AlZahbi, Zone.AhtUrhganWhitegate, Zone.SouthernSanDoriaS, Zone.BastokMarketsS, Zone.WindurstWatersS,
            Zone.SouthernSanDoria, Zone.NorthernSanDoria, Zone.PortSanDoria, Zone.BastokMines, Zone.BastokMarkets,
            Zone.PortBastok, Zone.WindurstWaters, Zone.WindurstWalls, Zone.PortWindurst, Zone.WindurstWoods,
            Zone.RuludeGardens, Zone.UpperJeuno, Zone.LowerJeuno, Zone.PortJeuno, Zone.WesternAdoulin,
            Zone.EasternAdoulin,
        };

        // every Mog House zone's Moogle is named Moogle
        private const string Moogle = "Moogle";

        protected override void Build(QuestScriptBuilder b)
        {
            var section1 = b.AnyState(when: q =>
                q.Player.InMogHouseInHomeNation &&
                q.Quest.Var("notSeen") == 1);

            var section2 = b.AnyState(when: q =>
                !q.Player.InMogHouseInHomeNation &&
                q.Player.InMogHouse &&
                q.Quest.Var("oNation") == 1);

            foreach (ushort zoneId in MoghouseZones)
            {
                section1
                    .OnZoneIn(zoneId, q => QuestReply.Event(30000))
                    .OnEventFinish(zoneId, 30000, q =>
                    {
                        q.Quest.Complete();
                        q.Quest.SetVar("oNation", 1);
                    });
            }

            foreach (ushort zoneId in MoghouseZones)
            {
                section2
                    .Npc(zoneId, Moogle).OnTrigger(QuestReply.ProgressEvent(30001))
                    .OnEventFinish(zoneId, 30001, q =>
                    {
                        q.Quest.Complete();
                    });
            }
        }
    }
}
