// Beneath the Mask (Log ID 7, Quest ID 49), a line-for-line port of
// scripts/quests/crystalWar/WOTG_BAS_9_Beneath_the_Mask.lua (quest rework B3b Crystal War group 5). Bastok Markets [S]:
// Gentle Tiger !pos -203.9 -10 2.2 (default 109). The Eldieme Necropolis [S]: Red Axe !pos 304.6 -27.5 14 (no default).
// Vunkerl Inlet [S]: Leadavox !pos 207.1 -32 315.5 (default 100). Beaucedine Glacier [S]: Hoarfang !pos -263.6 -40.8
// -329.3 (no default). Available, Honor Under Fire completed and the current WotG mission Fate in Haze or later: Gentle
// Tiger 345, whose finish begins it. Accepted, by Prog: 0: Gentle Tiger 351 (an Event); any zone-in to Beadeaux [S]: 1
// (-> 1). 1: Gentle Tiger 346 (-> 2). 2: Gentle Tiger 352 (an Event); Red Axe 42 (-> 3). 3: Gentle Tiger 352; Red Axe
// 44 (an Event); a Lump of Beeswax and a Jar of Red Textile Dye alone to Leadavox: 2, whose finish confirms the trade,
// gives the Wax Seal and (giveKeyItem answers true) sets Prog 4. 4 with the Wax Seal: Gentle Tiger 352; Red Axe 43,
// whose finish sets Prog 5 and takes the seal. 5: Gentle Tiger 347 (-> 6); Red Axe 45 (an Event). 6: Gentle Tiger 353
// (an Event); Hoarfang 7, whose finish completes it (a Super Reraiser).
// Kept as the Lua has it: 345 begins on any option; the Beadeaux [S] zone-in plays 1 from any zone; Prog 4 without the
// seal has no section; 7 completes with no check of its result; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class BeneathTheMask : QuestScript
    {
        public BeneathTheMask() : base(QuestLog.CrystalWar, "BENEATH_THE_MASK") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_BAS_9_Beneath_the_Mask";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4, 5, 6);

            b.Reward(new QuestReward
            {
                Item = Item.SuperReraiser,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.CrystalWar, "HONOR_UNDER_FIRE") &&
                    q.Player.CurrentMission(MissionLog.Wotg) >= Mission.Wotg.FateInHaze)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.ProgressEvent(345))
                .OnEventFinish(Zone.BastokMarketsS, 345, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, step: 0)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(351))
                .OnZoneIn(Zone.BeadeauxS, q => QuestReply.Event(1))
                .OnEventFinish(Zone.BeadeauxS, 1, q =>
                {
                    q.Quest.SetStep(1);
                });

            b.State(QuestStatus.Accepted, step: 1)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.ProgressEvent(346))
                .OnEventFinish(Zone.BastokMarketsS, 346, q =>
                {
                    q.Quest.SetStep(2);
                });

            b.State(QuestStatus.Accepted, step: 2)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(352))
                .Npc(Zone.TheEldiemeNecropolisS, Npc.TheEldiemeNecropolisS.RedAxe)
                    .OnTrigger(QuestReply.ProgressEvent(42))
                .OnEventFinish(Zone.TheEldiemeNecropolisS, 42, q =>
                {
                    q.Quest.SetStep(3);
                });

            b.State(QuestStatus.Accepted, step: 3)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(352))
                .Npc(Zone.TheEldiemeNecropolisS, Npc.TheEldiemeNecropolisS.RedAxe).OnTrigger(QuestReply.Event(44))
                .Npc(Zone.VunkerlInletS, Npc.VunkerlInletS.Leadavox).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.LumpOfBeeswax, Item.JarOfRedTextileDye))
                    {
                        return QuestReply.ProgressEvent(2);
                    }
                    return null;
                })
                .OnEventFinish(Zone.VunkerlInletS, 2, q =>
                {
                    q.Trade.Confirm();

                    if (q.Player.GiveKeyItem(KeyItem.WaxSeal))
                    {
                        q.Quest.SetStep(4);
                    }
                });

            b.State(QuestStatus.Accepted, step: 4, when: q => q.Player.HasKeyItem(KeyItem.WaxSeal))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(352))
                .Npc(Zone.TheEldiemeNecropolisS, Npc.TheEldiemeNecropolisS.RedAxe)
                    .OnTrigger(QuestReply.ProgressEvent(43))
                .OnEventFinish(Zone.TheEldiemeNecropolisS, 43, q =>
                {
                    q.Quest.SetStep(5);
                    q.Player.DelKeyItem(KeyItem.WaxSeal);
                });

            b.State(QuestStatus.Accepted, step: 5)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.ProgressEvent(347))
                .OnEventFinish(Zone.BastokMarketsS, 347, q =>
                {
                    q.Quest.SetStep(6);
                })
                .Npc(Zone.TheEldiemeNecropolisS, Npc.TheEldiemeNecropolisS.RedAxe).OnTrigger(QuestReply.Event(45));

            b.State(QuestStatus.Accepted, step: 6)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(353))
                .Npc(Zone.BeaucedineGlacierS, Npc.BeaucedineGlacierS.Hoarfang).OnTrigger(QuestReply.ProgressEvent(7))
                .OnEventFinish(Zone.BeaucedineGlacierS, 7, q =>
                {
                    q.Quest.Complete();
                });
        }
    }
}
