// Fire in the Hole (Log ID 7, Quest ID 36), a line-for-line port of
// scripts/quests/crystalWar/WOTG_BAS_6_Fire_in_the_Hole.lua (quest rework B3b Crystal War group 4). North Gustaberg
// [S]: Stonehoused Adit !pos -434.7 36.7 280 (its script: xi.instance.onTrigger for Ruhotz Silvermines), Solitary Ant
// !pos 375 0.9 151.6 (default 112). Bastok Markets [S]: Gentle Tiger !pos -203.9 -10 2.2 (default 109).
// Available, Storm on the Horizon completed and the current WotG mission In the Name of the Father or later: the
// zone-in to Bastok Markets [S] from North Gustaberg [S]: 60, whose finish starts 77 itself (player:startEvent); 77's
// finish begins it and gives the Silvermine Key. Accepted: Stonehoused Adit: Prog 0 with the key: 5 (-> Prog 1).
// Solitary Ant: Prog 0 / 1 with the key: 7 (no finish); Prog 2+ without it: 8, whose finish (its npc Solitary Ant)
// gives the key again and sets Prog 1. The zone-in to North Gustaberg [S] from Ruhotz Silvermines at Prog 3: 6 (-> Prog
// 4). Ruhotz Silvermines' 10000 finished at Prog 2: Prog 3 (the instance's completion cutscene: "Fire in the Hole
// instance is not implemented currently"). The zone-in to Bastok Markets [S] from North Gustaberg [S] at Prog 4: 62 (->
// 5). Gentle Tiger: Prog 5: 63 (-> 6); Prog 6: 65, whose finish completes it (a Republican Silver Medal, Mythril
// Musketeer No. 6).
// Kept as the Lua has it: nothing sets Prog 2 (the instance 9301 has no script), so the quest stays at Prog 1 on Lua;
// 60 and 77 begin on any option; 7 has no finish; 8's finish tests the npc's name; the Stonehoused Adit's own script
// runs when the quest answers nil (and finishes 5 too); Gentle Tiger answers nil at other Progs; 65 completes with no
// check of its result; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class FireInTheHole : QuestScript
    {
        public FireInTheHole() : base(QuestLog.CrystalWar, "FIRE_IN_THE_HOLE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_BAS_6_Fire_in_the_Hole";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 3, 4, 5, 6);

            b.Reward(new QuestReward
            {
                Item = Item.RepublicanSilverMedal,
                Title = Title.MythrilMusketeerNo6,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.CrystalWar, "STORM_ON_THE_HORIZON") &&
                    q.Player.CurrentMission(MissionLog.Wotg) >= Mission.Wotg.InTheNameOfTheFather)
                .OnZoneIn(Zone.BastokMarketsS, q =>
                {
                    if (q.Player.PreviousZone == Zone.NorthGustabergS)
                    {
                        return QuestReply.Event(60);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BastokMarketsS, 60, q =>
                {
                    q.Player.StartEvent(77);
                })
                .OnEventFinish(Zone.BastokMarketsS, 77, q =>
                {
                    q.Quest.Begin();
                    q.Player.GiveKeyItem(KeyItem.SilvermineKey);
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.NorthGustabergS, Npc.NorthGustabergS.StonehousedAdit).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0 &&
                        q.Player.HasKeyItem(KeyItem.SilvermineKey))
                    {
                        return QuestReply.ProgressEvent(5);
                    }
                    return null;
                })
                .Npc(Zone.NorthGustabergS, Npc.NorthGustabergS.SolitaryAnt).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress >= 0 &&
                        questProgress <= 1 &&
                        q.Player.HasKeyItem(KeyItem.SilvermineKey))
                    {
                        return QuestReply.ProgressEvent(7);
                    }
                    else if (questProgress >= 2 &&
                        !q.Player.HasKeyItem(KeyItem.SilvermineKey))
                    {
                        return QuestReply.ProgressEvent(8);
                    }
                    return null;
                })
                .OnZoneIn(Zone.NorthGustabergS, q =>
                {
                    if (q.Quest.Step == 3 &&
                        q.Player.PreviousZone == Zone.RuhotzSilvermines)
                    {
                        return QuestReply.Event(6);
                    }
                    return null;
                })
                .OnEventFinish(Zone.NorthGustabergS, 5, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.NorthGustabergS, 6, q =>
                {
                    q.Quest.SetStep(4);
                })
                .OnEventFinish(Zone.NorthGustabergS, 8, q =>
                {
                    // the Lua's `npc and npc:getName() == 'Solitary_Ant'`
                    if (q.Npc != null && q.Npc.Name == Npc.NorthGustabergS.SolitaryAnt)
                    {
                        q.Player.GiveKeyItem(KeyItem.SilvermineKey);
                        q.Quest.SetStep(1);
                    }
                })
                // Fire in the Hole instance is not implemented currently
                .OnEventFinish(Zone.RuhotzSilvermines, 10000, q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        q.Quest.SetStep(3);
                    }
                })
                .OnZoneIn(Zone.BastokMarketsS, q =>
                {
                    if (q.Quest.Step == 4 &&
                        q.Player.PreviousZone == Zone.NorthGustabergS)
                    {
                        return QuestReply.Event(62);
                    }
                    return null;
                })
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 5)
                    {
                        return QuestReply.ProgressEvent(63);
                    }
                    else if (questProgress == 6)
                    {
                        return QuestReply.ProgressEvent(65);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BastokMarketsS, 62, q =>
                {
                    q.Quest.SetStep(5);
                })
                .OnEventFinish(Zone.BastokMarketsS, 63, q =>
                {
                    q.Quest.SetStep(6);
                })
                .OnEventFinish(Zone.BastokMarketsS, 65, q =>
                {
                    q.Quest.Complete();
                });
        }
    }
}
