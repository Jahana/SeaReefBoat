// Saga of the Skyserpent (Log ID 6, Quest ID 43), a line-for-line port of
// scripts/quests/ahtUrhgan/Saga_of_the_Skyserpent.lua (quest rework B3b Aht Urhgan group 6). Aht Urhgan Whitegate:
// Fari-Wari !pos 80 -6 -137 (no script; default event 831). Al Zahbi: Biyaada !pos -65.8 -6 69.3 (no script;
// default 250). Halvung: qm7 !pos -11.6 7.9 -185.5 (no script, no default). Wajaom Woodlands: the zone-in.
// Available (no other condition): Fari-Wari 823, whose finish begins it. Accepted, Prog 0: Biyaada 278 (an Event),
// Fari-Wari 829 (an Event), Halvung's qm7: Prog 1 set inside the lookup, then the key item action (the Lilac Ribbon,
// KEYITEM_OBTAINED). Prog 1: Biyaada 278; Fari-Wari 953, whose finish sets the char at 0 0 0 in Wajaom (the zone's own
// zone-in places it); the Wajaom zone-in at Prog 1: 12, whose finish starts 13; 13's finish: Prog 2, Stage = today
// (VanadielUniqueDay), the ribbon taken, the char set down in Whitegate (80 -6 -123). Prog 2: Biyaada 279;
// Fari-Wari: Stage before today: 825, whose finish completes it (an Imperial Gold Piece, Skyserpent Aggrandizer) and,
// when it does, 1000 imperial standing and BESIEGED_OFFSET; else (13's day) 833 (an Event).
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): section 4's Whitegate table holds
// `['Fari-Wari'] = quest:progressEvent(825, ...)` followed by a bare table `{ onTrigger = ... }` (the Stage-gated
// 825 / 833 choice); that table is the zone table's array entry [1], which the lookup files under the entity key 1
// (a number, never an NPC's name), so the Lua never reached it: Fari-Wari played 825 at Prog 2 at once and 833
// never. The C# gives Fari-Wari the bare table's onTrigger, as the Stage write in 13 means.
// Kept as the Lua has it: 823 begins it on any option; qm7 sets Prog 1 inside the lookup, before its key item action
// runs (the ribbon held already: no message, Prog 1 all the same); the zone-in tests Prog 1 again inside its Prog-1
// section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class SagaOfTheSkyserpent : QuestScript
    {
        public SagaOfTheSkyserpent() : base(QuestLog.AhtUrghan, "SAGA_OF_THE_SKYSERPENT") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Saga_of_the_Skyserpent";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.ImperialGoldPiece,
                Title = Title.SkyserpentAggrandizer,
            });
            b.Steps(1, 2);

            b.State(QuestStatus.Available)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.FariWari)
                    .OnTrigger(QuestReply.ProgressEvent(823).TextTable(0))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 823, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 0)
                .Npc(Zone.AlZahbi, Npc.AlZahbi.Biyaada).OnTrigger(QuestReply.Event(278))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.FariWari).OnTrigger(QuestReply.Event(829))
                .Npc(Zone.Halvung, Npc.Halvung.qm7).OnTrigger(q =>
                {
                    q.Quest.SetStep(1);
                    return QuestReply.KeyItem(KeyItem.LilacRibbon);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 1)
                .Npc(Zone.AlZahbi, Npc.AlZahbi.Biyaada).OnTrigger(QuestReply.Event(278))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.FariWari)
                    .OnTrigger(QuestReply.ProgressEvent(953).TextTable(0))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 953, q =>
                {
                    q.Player.SetPos(0, 0, 0, 0, Zone.WajaomWoodlands);
                })
                .OnZoneIn(Zone.WajaomWoodlands, q => q.Quest.Step == 1 ? QuestReply.Event(12) : null)
                .OnEventFinish(Zone.WajaomWoodlands, 12, q =>
                {
                    q.Player.StartEvent(13);
                })
                .OnEventFinish(Zone.WajaomWoodlands, 13, q =>
                {
                    q.Quest.SetStep(2);
                    q.Quest.SetVar("Stage", q.Player.VanadielUniqueDay);
                    q.Player.DelKeyItem(KeyItem.LilacRibbon);
                    q.Player.SetPos(80, -6, -123, 65, Zone.AhtUrhganWhitegate);
                });

            // Fari-Wari: the Lua's bare [1] = { onTrigger = ... } entry of this zone table, which the lookup never
            // reached (its plain 825 played instead): fixed, user ruling 2026-11-04 (see the header)
            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 2)
                .Npc(Zone.AlZahbi, Npc.AlZahbi.Biyaada).OnTrigger(QuestReply.Event(279))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.FariWari)
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Var("Stage") < q.Player.VanadielUniqueDay)
                        {
                            return QuestReply.ProgressEvent(825).TextTable(0);
                        }
                        else
                        {
                            return QuestReply.Event(833);
                        }
                    })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 825, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddCurrency("imperial_standing", 1000);
                        // zones[player:getZoneID()].text.BESIEGED_OFFSET: the handler's zone, Whitegate
                        q.Text.Special(Text.AhtUrhganWhitegate.BESIEGED_OFFSET);
                    }
                });
        }
    }
}
