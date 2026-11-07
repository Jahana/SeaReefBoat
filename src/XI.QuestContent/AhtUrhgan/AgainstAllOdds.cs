// Against All Odds (COR AF3; Log ID 6, Quest ID 26), a line-for-line port of scripts/quests/ahtUrhgan/COR_AF3_Against_All_Odds.lua
// (quest rework B3b Aht Urhgan, group 2). Aht Urhgan Whitegate: trigger area 5 (64 -7 -137 .. 95 -5 -123, the Shaharat
// Teahouse; the zone's own switch has no case for it), Ratihb !pos 75 -6 -137 (no script; default 603). Arrapago Reef:
// the Cutter !pos -465 -2 -394 (its script: The Ashu Talif's instance entry, xi.instance.onTrigger, else
// CUTTER_NOTHING_HAPPENS), trigger area 1 (-462 -4 -420 .. -455 -1 -392, "approach the Cutter"), the zone-in.
// Available with Navigating the Unfriendly Seas completed, to a COR at AF3_QUEST_LEVEL (50): area 5 plays 797, whose
// finish begins it (the Life Float, Wait = JstMidnight()). Accepted: Arrapago's area 1 at Prog 0: 237, whose finish
// starts 240, whose finish sets Prog 1; Ratihb at Prog 1 without the Life Float once Wait has passed (or is 0): 798 (the
// float again, Wait = JstMidnight()); the Cutter at Prog 2: 238; the Arrapago zone-in from The Ashu Talif at Prog 2:
// 238; 238's finish completes it (Corsair's Tricorne, Paragon of Corsair Excellence). Prog 1 -> 2 is the instance's own
// (zones/The_Ashu_Talif/instances/against_all_odds.lua, onInstanceComplete: Prog 2, Wait 0), not this file's.
// Kept as the Lua has it: area 5's 797 on every entry while available; 238's finish ignores complete's result; the
// zone-in's 238 ignores the char's position.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class AgainstAllOdds : QuestScript
    {
        public AgainstAllOdds() : base(QuestLog.AhtUrghan, "AGAINST_ALL_ODDS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/COR_AF3_Against_All_Odds";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.CorsairsTricorne,
                Title = Title.ParagonOfCorsairExcellence,
            });
            b.Steps(1, 2);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.AhtUrghan, "NAVIGATING_THE_UNFRIENDLY_SEAS") == QuestStatus.Completed &&
                    q.Player.MainJob == Job.Cor &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF3_QUEST_LEVEL"))
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 5, q => QuestReply.ProgressEvent(797))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 797, q =>
                {
                    q.Quest.Begin();
                    q.Player.GiveKeyItem(KeyItem.LifeFloat);
                    q.Quest.SetVar("Wait", q.Player.JstMidnight);
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Ratihb).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1 &&
                        !q.Player.HasKeyItem(KeyItem.LifeFloat) &&
                        (q.Quest.Var("Wait") < q.Player.SystemTime ||
                         q.Quest.Var("Wait") == 0))
                    {
                        return QuestReply.ProgressEvent(798);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 798, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.LifeFloat);
                    q.Quest.SetVar("Wait", q.Player.JstMidnight);
                })
                .Npc(Zone.ArrapagoReef, Npc.ArrapagoReef.Cutter).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(238);
                    }
                    return null;
                })
                .OnTriggerAreaEnter(Zone.ArrapagoReef, 1, q => q.Quest.Step == 0 ? QuestReply.ProgressEvent(237) : null)
                // prevZone is player:getPreviousZone() (the same value: both are CharData.PrevZone)
                .OnZoneIn(Zone.ArrapagoReef, q =>
                    q.Player.PreviousZone == Zone.TheAshuTalif && q.Quest.Step == 2 ? QuestReply.Event(238) : null)
                .OnEventFinish(Zone.ArrapagoReef, 237, q =>
                {
                    q.Player.StartEvent(240);
                })
                .OnEventFinish(Zone.ArrapagoReef, 238, q =>
                {
                    q.Quest.Complete();
                })
                .OnEventFinish(Zone.ArrapagoReef, 240, q =>
                {
                    q.Quest.SetStep(1);
                });
        }
    }
}
