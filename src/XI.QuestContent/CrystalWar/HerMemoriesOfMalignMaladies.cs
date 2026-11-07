// Her Memories: Of Malign Maladies (Log ID 7, Quest ID 72), a line-for-line port of
// scripts/quests/crystalWar/Her_Memories_Of_Malign_Maladies.lua (quest rework B3b Crystal War, draft). Southern San
// d'Oria: Amaura !pos -84.367 -6.949 91.148 (script: To Cure a Cough's xi.cs call; default 642). Upper Jeuno: Monberaux
// !pos -42 0 -2 (script: 109 once, then 28). Windurst Waters: Library_book3 !pos -16.17 -6.25 112.289 (default 369).
// Grauberg [S]: Fey Blossoms !pos -141.312 -6.75 564.417 (no default). Southern San d'Oria [S]: Raustigne !pos 3.979
// -1.999 44.456 (default 606).
// Available, the WotG mission Her Memories current: Amaura: 955 (8 args), whose finish begins it. Accepted: Amaura: 956
// (an Event, oncePerZone). Monberaux: Prog 0: 10190 (-> Prog 1); else 10191 (an Event). Library_book3: Prog 1: 1017 (->
// 2); Prog 2: 1018 (an Event). Fey Blossoms: a Philosopher's Stone alone at Prog 2: 30, whose finish confirms the
// trade, sets Prog 3 and the Timer to tomorrow; a talk: Prog 2: SUITABLE_PLACE_TO_SOAK (the stone; a messageSpecial
// reply); Prog 3 without the Fey Stone: the Timer at or before today: 32, whose finish gives the Fey Stone; else 31 (an
// Event). Raustigne: the Fey Stone: 169, whose finish completes it (the Large Memory Fragment), runs
// xi.wotg.helpers.checkMemoryFragments and takes the Fey Stone.
// Rival containers: Homecoming Queen at Amaura (958 / 959) and Boy and the Beast at Raustigne (55): golden flow
// #rivals. Library_book3 and the Fey Blossoms have no script and no other quest: their kinds are pinned in
// tests/XI.Verify.Quests/CrystalWarGroup10.cs.
// Kept as the Lua has it: 955 begins on any option; the stone is confirmed at the finish, not the trade; 32 answers
// again once the Fey Stone is gone (Prog 3 stays); the Fey Stone is taken after the fragment check; no completed
// section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class HerMemoriesOfMalignMaladies : QuestScript
    {
        public HerMemoriesOfMalignMaladies() : base(QuestLog.CrystalWar, "HER_MEMORIES_OF_MALIGN_MALADIES") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/Her_Memories_Of_Malign_Maladies";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3);

            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.LargeMemoryFragment2,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.CurrentMission(MissionLog.Wotg) == Mission.Wotg.HerMemories)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Amaura).OnTrigger(
                    QuestReply.ProgressEvent(955, 649992904, 0, 0, 0, 156286100, 19935, 0, 4))
                .OnEventFinish(Zone.SouthernSanDoria, 955, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Amaura).OnTrigger(
                    QuestReply.Event(956, 649993042, 0, 27830, 94060, 0, 0, 0, 0).OncePerZone())
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Monberaux).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(10190, 244, 1, 40, 0, 62914449, 41076209, 4095, 131105);
                    }
                    else
                    {
                        return QuestReply.Event(10191, 244, 1, 255, 0, 65339391, 27831, 4095, 131105);
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 10190, q =>
                {
                    q.Quest.SetStep(1);
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Librarybook3).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(1017, 650060514, 0, 0, 0, 110, 6312479, 4095, 128);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.Event(1018, 650060691, 0, 0, 0, 65863679, 4104166, 4095, 4);
                    }
                    return null;
                })
                .OnEventFinish(Zone.WindurstWaters, 1017, q =>
                {
                    q.Quest.SetStep(2);
                })
                .Npc(Zone.GraubergS, Npc.GraubergS.FeyBlossoms).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.PhilosophersStone) &&
                        q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(30, 89, 23, 2964, 56, 0, 6029313, 0, 0);
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 2)
                    {
                        return QuestReply.MessageSpecial(Text.GraubergS.SUITABLE_PLACE_TO_SOAK, Item.PhilosophersStone);
                    }
                    else if (questProgress == 3 &&
                        !q.Player.HasKeyItem(KeyItem.FeyStone))
                    {
                        if (q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay)
                        {
                            return QuestReply.ProgressEvent(32, 89, 6, 0, 56, 0, 6029328, 0, 0);
                        }
                        else
                        {
                            return QuestReply.Event(31, 89, 23, 2964, 56, 0, 6029313, 0, 0);
                        }
                    }
                    return null;
                })
                .OnEventFinish(Zone.GraubergS, 30, q =>
                {
                    q.Trade.Confirm();

                    q.Quest.SetStep(3);
                    q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay + 1);
                })
                .OnEventFinish(Zone.GraubergS, 32, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.FeyStone);
                })
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Raustigne).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.FeyStone))
                    {
                        return QuestReply.ProgressEvent(169, 80, 23, 1756, 0, 67108863, 85453257, 3903, 131140);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 169, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.CheckMemoryFragments();
                        q.Player.DelKeyItem(KeyItem.FeyStone);
                    }
                });
        }
    }
}
