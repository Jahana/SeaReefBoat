// Such Sweet Sorrow (Log ID 6, Quest ID 13), a line-for-line port of scripts/quests/ahtUrhgan/Such_Sweet_Sorrow.lua
// (quest rework B3b Aht Urhgan, group 10). Aht Urhgan Whitegate: Dabhuh !pos 97.9 0 -91.5 (no script, no default).
// Available: Dabhuh 582 ({ text_table = 0 }), whose finish sets Option 1 and sets the char down in Caedarva Mire
// (43.493 5.325 -699.828, rot 90); Caedarva Mire's zone-in at Option 1: 29, whose finish sets Option 2 and sets the
// char down in Whitegate (100.023 0 -91.762, rot 125); Whitegate's zone-in at Option 2: { 956, 0 } (cutscene 956, text
// table 0), whose finish begins it and sets Option 0. Accepted: Dabhuh 584 ({ text_table = 0 }, an Event); a trade of
// exactly a Merrow Scale: 583 ({ text_table = 0 }), whose finish completes it (a Merrow No.17 Locket) and confirms the
// trade. Completed: Dabhuh 584 ({ text_table = 0 }). Whitegate's zone-in returns the Lua table { 956, 0 }
// (luautils::OnZoneIn: id, textTable, flags): the C# reply is QuestReply.Event(956).TextTable(0), which the bridge
// pushes as { 956, 0, 0 } (quest rework P8): OnZoneIn reads the Lua's missing flags as 0, the same cutscene. Kept as
// the Lua has it: 582 and 29 move the char on any option; the zone-ins answer at their Option whatever the previous
// zone; no trade handler while available.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class SuchSweetSorrow : QuestScript
    {
        public SuchSweetSorrow() : base(QuestLog.AhtUrghan, "SUCH_SWEET_SORROW") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Such_Sweet_Sorrow";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.MerrowNo17Locket,
            });

            b.State(QuestStatus.Available)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Dabhuh)
                    .OnTrigger(QuestReply.ProgressEvent(582).TextTable(0))
                .OnZoneIn(Zone.AhtUrhganWhitegate, q =>
                {
                    if (q.Quest.Var("Option") == 2)
                    {
                        return QuestReply.Event(956).TextTable(0);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 582, q =>
                {
                    q.Quest.SetVar("Option", 1);
                    q.Player.SetPos(43.493, 5.325, -699.828, 90, Zone.CaedarvaMire);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 956, q =>
                {
                    q.Quest.Begin();
                    q.Quest.SetVar("Option", 0);
                })
                .OnZoneIn(Zone.CaedarvaMire, q =>
                {
                    if (q.Quest.Var("Option") == 1)
                    {
                        return QuestReply.Event(29);
                    }
                    return null;
                })
                .OnEventFinish(Zone.CaedarvaMire, 29, q =>
                {
                    q.Quest.SetVar("Option", 2);
                    q.Player.SetPos(100.023, 0, -91.762, 125, Zone.AhtUrhganWhitegate);
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Dabhuh)
                    .OnTrigger(q => QuestReply.Event(584).TextTable(0))
                .OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.MerrowScale))
                    {
                        return QuestReply.ProgressEvent(583).TextTable(0);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 583, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Dabhuh)
                    .OnTrigger(QuestReply.Event(584).TextTable(0));
        }
    }
}
