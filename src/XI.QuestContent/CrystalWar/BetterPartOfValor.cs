// Better Part of Valor (Log ID 7, Quest ID 12), a line-for-line port of
// scripts/quests/crystalWar/WOTG_BAS_1_Better_Part_of_Valor.lua (quest rework B3b Crystal War group 3). Bastok Markets
// [S]: Engelhart !pos -79 -4 -123 (default 104). North Gustaberg [S]: qm1 !pos -576.6 39.9 64.2, Solitary Ant !pos 375
// 0.9 151.6 (default 112). Vunkerl Inlet [S]: Leadavox !pos 207 -32 315.5 (default 100).
// Available, one of the nation openers completed (xi.wotg.helpers.hasCompletedFirstQuest): the zone-in to North
// Gustaberg [S] from Bastok Markets [S]: 1, whose finish begins it and gives the Clump of Animal Hair. Accepted:
// Engelhart: Prog 0: 116 (-> Prog 1, the hair taken); Prog 4: 118, whose finish completes it (10000 gil, the Warning
// Letter) and then takes the Xhifhut and sets needToZone; else 117 (an Event). qm1: Prog 1: 3 (-> Prog 2). Solitary
// Ant: 2 (an Event, replaceDefault). Leadavox: Prog 2: 101 (-> Prog 3); Prog 3: 102 (an Event); a Gnole Claw alone at
// Prog 3: 103, whose finish confirms the trade, gives the Xhifhut, Prog 4.
// Kept as the Lua has it: 1 begins on any option; Leadavox answers nil at Prog 0 / 1 / 4 (his default 100), qm1 at any
// Prog but 1; Solitary Ant's 2 replaces his default at every Prog; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class BetterPartOfValor : QuestScript
    {
        public BetterPartOfValor() : base(QuestLog.CrystalWar, "BETTER_PART_OF_VALOR") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_BAS_1_Better_Part_of_Valor";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4);

            b.Reward(new QuestReward
            {
                Gil = 10000,
                KeyItem = KeyItem.WarningLetter,
            });

            // xi.wotg.helpers.hasCompletedFirstQuest: group 2's copy (TheTigressStirs.HasCompletedFirstQuest)
            b.State(QuestStatus.Available, when: q => TheTigressStirs.HasCompletedFirstQuest(q.Player))
                .OnZoneIn(Zone.NorthGustabergS, q =>
                {
                    if (q.Player.PreviousZone == Zone.BastokMarketsS)
                    {
                        return QuestReply.Event(1);
                    }
                    return null;
                })
                .OnEventFinish(Zone.NorthGustabergS, 1, q =>
                {
                    q.Quest.Begin();
                    q.Player.GiveKeyItem(KeyItem.ClumpOfAnimalHair);
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Engelhart).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(116);
                    }
                    else if (questProgress == 4)
                    {
                        return QuestReply.ProgressEvent(118);
                    }
                    else
                    {
                        return QuestReply.Event(117);
                    }
                })
                .OnEventFinish(Zone.BastokMarketsS, 116, q =>
                {
                    q.Quest.SetStep(1);
                    q.Player.DelKeyItem(KeyItem.ClumpOfAnimalHair);
                })
                .OnEventFinish(Zone.BastokMarketsS, 118, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.Xhifhut);
                        q.Player.SetNeedToZone(true);
                    }
                })
                .Npc(Zone.NorthGustabergS, Npc.NorthGustabergS.qm1).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(3);
                    }
                    return null;
                })
                .OnEventFinish(Zone.NorthGustabergS, 3, q =>
                {
                    q.Quest.SetStep(2);
                })
                .Npc(Zone.NorthGustabergS, Npc.NorthGustabergS.SolitaryAnt).OnTrigger(QuestReply.Event(2).ReplaceDefault())
                .Npc(Zone.VunkerlInletS, Npc.VunkerlInletS.Leadavox).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(101);
                    }
                    else if (questProgress == 3)
                    {
                        return QuestReply.Event(102);
                    }
                    return null;
                })
                .Npc(Zone.VunkerlInletS, Npc.VunkerlInletS.Leadavox).OnTrade(q =>
                {
                    if (q.Quest.Step == 3 &&
                        q.Trade.HasExactly((Item.GnoleClaw, 1)))
                    {
                        return QuestReply.ProgressEvent(103);
                    }
                    return null;
                })
                .OnEventFinish(Zone.VunkerlInletS, 101, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.VunkerlInletS, 103, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.Xhifhut);
                    q.Quest.SetStep(4);
                });
        }
    }
}
