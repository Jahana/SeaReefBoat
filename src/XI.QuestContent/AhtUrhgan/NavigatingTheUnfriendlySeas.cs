// Navigating the Unfriendly Seas (COR AF2; Log ID 6, Quest ID 25), a line-for-line port of
// scripts/quests/ahtUrhgan/COR_AF2_Navigating_the_Unfriendly_Seas.lua (quest rework B3b Aht Urhgan, group 2). Arrapago
// Reef: qm6 !pos 469 -12 112 (no script; default NOTHING_OUT_OF_ORDINARY). Nashmau: Leleroon !pos -15 0 25 (no script;
// default 264; the hidden quest Crafted Corsair Artifact once Against All Odds is accepted). Wajaom Woodlands: the
// Leypoint !pos -200 -8.5 80 (no script; default LEYPOINT).
// Available with Equipped for All Occasions completed, to a COR at AF2_QUEST_LEVEL (50): qm6 232, whose finish begins it.
// Accepted: a trade holding a Hydrogauge to Leleroon at Prog 0: 283, whose finish sets Prog 1 at Prog 0 (the gauge
// kept); Leleroon at Prog 1 or 2: 292 (an Event); the Hydrogauge alone traded to the Leypoint at Prog 1: confirmed (the
// gauge taken), Prog 2, Wait = now + 60, PLACE_HYDROGAUGE (the reply); the Leypoint at Prog 2: ENIGMATIC_LIGHT while Wait
// > now, else 508 (Prog 3, Wait 0); qm6 Prog 3: 233, whose finish completes it (Corsair's Culottes).
// Kept as the Lua has it: 232 begins it on any option; 233 ignores complete's result; 283's finish tests Prog again.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class NavigatingTheUnfriendlySeas : QuestScript
    {
        public NavigatingTheUnfriendlySeas() : base(QuestLog.AhtUrghan, "NAVIGATING_THE_UNFRIENDLY_SEAS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/COR_AF2_Navigating_the_Unfriendly_Seas";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.CorsairsCulottes });
            b.Steps(1, 2, 3);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.AhtUrghan, "EQUIPPED_FOR_ALL_OCCASIONS") == QuestStatus.Completed &&
                    q.Player.MainJob == Job.Cor &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF2_QUEST_LEVEL"))
                .Npc(Zone.ArrapagoReef, Npc.ArrapagoReef.qm6).OnTrigger(q => QuestReply.ProgressEvent(232))
                .OnEventFinish(Zone.ArrapagoReef, 232, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Nashmau, Npc.Nashmau.Leleroon)
                    .OnTrade(q =>
                    {
                        if (q.Quest.Step == 0 &&
                            q.Trade.ItemQty(Item.Hydrogauge) > 0)
                        {
                            return QuestReply.ProgressEvent(283);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Step >= 1 &&
                            q.Quest.Step < 3)
                        {
                            return QuestReply.Event(292);
                        }
                        return null;
                    })
                .OnEventFinish(Zone.Nashmau, 283, q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .Npc(Zone.WajaomWoodlands, Npc.WajaomWoodlands.Leypoint)
                    .OnTrade(q =>
                    {
                        if (q.Quest.Step == 1 &&
                            q.Trade.HasExactly(Item.Hydrogauge))
                        {
                            q.Trade.Confirm();
                            q.Quest.SetStep(2);
                            q.Quest.SetVar("Wait", q.Player.SystemTime + 60); // 1 minute wait time
                            return QuestReply.MessageSpecial(Text.WajaomWoodlands.PLACE_HYDROGAUGE, Item.Hydrogauge);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Step == 2)
                        {
                            if (q.Quest.Var("Wait") > q.Player.SystemTime)
                            {
                                return QuestReply.MessageSpecial(Text.WajaomWoodlands.ENIGMATIC_LIGHT, Item.Hydrogauge);
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(508);
                            }
                        }
                        return null;
                    })
                .OnEventFinish(Zone.WajaomWoodlands, 508, q =>
                {
                    q.Quest.SetStep(3);
                    q.Quest.SetVar("Wait", 0);
                })
                .Npc(Zone.ArrapagoReef, Npc.ArrapagoReef.qm6).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(233);
                    }
                    return null;
                })
                .OnEventFinish(Zone.ArrapagoReef, 233, q =>
                {
                    q.Quest.Complete();
                });
        }
    }
}
