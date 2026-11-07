// A Job for the Consortium (Log ID 0, Quest ID 67), a line-for-line port of
// scripts/quests/sandoria/A_Job_for_the_Consortium.lua (quest rework B3b, San d'Oria group 8). Portaure : !pos -22 -4 -106
// 232; _6u2 : !pos -61.0 6.451 57.998 246; Haubijoux : !pos -61 8 58 246; Yin Pocanakhu : !pos 35 4 -46 245. Available,
// Stage 0, Option 0: Portaure's 571 once (importantOnce; Option 1). Stage 0 with the Tenshodo Members Card, San d'Oria fame
// level 5 and Norg fame level 1 (any status: repeatable): Portaure's LAY_LOW while the airship ban runs, else 651 (option
// [0]: 0 the first time, 1 after); option 1 gives Brugaire Goods (begun, Prog DAMAGED 2, Stage 1). Stage 1 with the goods:
// Portaure's 652; the Port Jeuno customs (_6u2, Haubijoux): 54, by day a 25% chance of being caught (Prog CONFISCATED 1,
// param 1); 54's option 1: caught, the goods taken and the airship ban to the next JST midnight (CONFISCATED), else
// CLEARED_CUSTOMS (Prog NORMAL 0); Yin Pocanakhu: 218 (damaged) or 219, each taking the goods. Stage 1 without the goods:
// Portaure's 653 + Prog: 653 completed with 1000 gil and Norg fame +20, 654 / 655 completed bare. While the ban runs (any
// status) the Port Jeuno airship gates and clerks answer 40-43 at priority 200. No outside reference.
// Kept as the Lua has it: the quest is repeatable through section 2 (complete clears Stage); 651's option 1 begins it
// again when completed; the customs roll is drawn on each talk, so a caught talk declined (option 0) leaves Prog 1 with
// the goods held (Yin Pocanakhu's 219, the undamaged delivery, and Portaure's 654).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class AJobForTheConsortium : QuestScript
    {
        public AJobForTheConsortium() : base(QuestLog.Sandoria, "A_JOB_FOR_THE_CONSORTIUM") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/A_Job_for_the_Consortium";

        // packageCondition
        private const int Normal = 0;
        private const int Confiscated = 1;
        private const int Damaged = 2;

        // checkSmuggle: by day (6-17), a 25% chance; the RNG is drawn only by day
        private static bool CheckSmuggle(IQuestContext q)
        {
            bool caughtSmuggling = false;
            int hour = q.Player.VanadielHour;

            if (hour > 5 &&
                hour < 18 &&
                q.Player.RandomInt(1, 100) <= 25)
            {
                caughtSmuggling = true;
            }

            return caughtSmuggling;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available, when: q => q.Quest.Var("Stage") == 0 && q.Quest.Var("Option") == 0)
                // This is a single time dialogue. It only ever plays once and is never seen again.
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Portaure).OnTrigger(QuestReply.Event(571).ImportantOnce())
                .OnEventFinish(Zone.PortSanDoria, 571, q => q.Quest.SetVar("Option", 1));

            b.AnyState(when: q =>
                    q.Quest.Var("Stage") == 0 &&
                    q.Player.HasKeyItem(KeyItem.TenshodoMembersCard) &&
                    q.Player.FameLevel(FameArea.Sandoria) >= 5 &&
                    q.Player.FameLevel(FameArea.Norg) >= 1)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Portaure).OnTrigger(q =>
                {
                    int firstTime = q.Quest.Status == QuestStatus.Available ? 0 : 1;

                    if (q.Player.CharVar("[AIRSHIP]suspended") >= q.Player.SystemTime)
                    {
                        return QuestReply.MessageText(Text.PortSanDoria.LAY_LOW);
                    }
                    else
                    {
                        return QuestReply.Event(651).Option(0, firstTime);
                    }
                })
                .OnEventFinish(Zone.PortSanDoria, 651, q =>
                {
                    if (q.Option == 1 && q.Player.GiveKeyItem(KeyItem.BrugaireGoods))
                    {
                        q.Quest.Begin();
                        // Set as damaged, going through customs reverses it.
                        q.Quest.SetStep(Damaged);
                        q.Quest.SetVar("Stage", 1);
                    }
                });

            b.AnyState(when: q => q.Quest.Var("Stage") == 1 && q.Player.HasKeyItem(KeyItem.BrugaireGoods))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Portaure).OnTrigger(QuestReply.Event(652))
                .Npc(Zone.PortJeuno, Npc.PortJeuno._6u2).OnTrigger(q =>
                {
                    bool caught = CheckSmuggle(q);
                    int param = 0;

                    if (caught)
                    {
                        q.Quest.SetStep(Confiscated);
                        param = 1;
                    }

                    return QuestReply.ProgressEvent(54, param);
                })
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Haubijoux).OnTrigger(q =>
                {
                    bool caught = CheckSmuggle(q);
                    int param = 0;

                    if (caught)
                    {
                        q.Quest.SetStep(Confiscated);
                        param = 1;
                    }

                    return QuestReply.ProgressEvent(54, param);
                })
                .OnEventFinish(Zone.PortJeuno, 54, q =>
                {
                    if (q.Option == 1)
                    {
                        if (q.Quest.Step == Confiscated)
                        {
                            q.Player.DelKeyItem(KeyItem.BrugaireGoods);
                            q.Player.SetCharVar("[AIRSHIP]suspended", q.Player.JstMidnight);
                            q.Text.Special(Text.PortJeuno.CONFISCATED, KeyItem.BrugaireGoods);
                        }
                        else
                        {
                            q.Text.Special(Text.PortJeuno.CLEARED_CUSTOMS);
                            q.Quest.SetStep(Normal);
                        }
                    }
                })
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.YinPocanakhu).OnTrigger(q =>
                {
                    bool damaged = q.Quest.Step == Damaged;

                    if (damaged)
                    {
                        return QuestReply.ProgressEvent(218);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(219);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 218, q =>
                {
                    q.Text.Special(Text.LowerJeuno.DAMANGED_PACKAGE_DELIVERED, KeyItem.BrugaireGoods);
                    q.Player.DelKeyItem(KeyItem.BrugaireGoods);
                })
                .OnEventFinish(Zone.LowerJeuno, 219, q =>
                {
                    q.Text.Special(Text.LowerJeuno.PACKAGE_DELIVERED, KeyItem.BrugaireGoods);
                    q.Player.DelKeyItem(KeyItem.BrugaireGoods);
                });

            b.AnyState(when: q => q.Quest.Var("Stage") == 1 && !q.Player.HasKeyItem(KeyItem.BrugaireGoods))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Portaure).OnTrigger(q =>
                {
                    int result = q.Quest.Step;
                    // 0 delivered, 1 = confiscated, 2 = damaged
                    return QuestReply.ProgressEvent(653 + result);
                })
                .OnEventFinish(Zone.PortSanDoria, 653, q =>
                {
                    q.Quest.Complete();
                    q.Player.GiveCurrency("gil", 1000);
                    q.Player.AddFame(FameArea.Norg, 20);
                })
                .OnEventFinish(Zone.PortSanDoria, 654, q => q.Quest.Complete())
                .OnEventFinish(Zone.PortSanDoria, 655, q => q.Quest.Complete());

            // Actions for Airship travels in Jeuno when suspended.
            b.AnyState(when: q => q.Player.CharVar("[AIRSHIP]suspended") >= q.Player.SystemTime)
                .Npc(Zone.PortJeuno, Npc.PortJeuno._6u4).OnTrigger(QuestReply.Event(42).SetPriority(200)) // Sandy
                .Npc(Zone.PortJeuno, Npc.PortJeuno._6u8).OnTrigger(QuestReply.Event(41).SetPriority(200)) // Kazahm
                .Npc(Zone.PortJeuno, Npc.PortJeuno._6ua).OnTrigger(QuestReply.Event(40).SetPriority(200)) // Bastok
                .Npc(Zone.PortJeuno, Npc.PortJeuno._6ue).OnTrigger(QuestReply.Event(43).SetPriority(200)) // Windurst
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Illauvolahaut).OnTrigger(QuestReply.Event(41).SetPriority(200)) // Kazahm
                .Npc(Zone.PortJeuno, Npc.PortJeuno.OmiroZamiro).OnTrigger(QuestReply.Event(43).SetPriority(200)) // Windurst
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Purequane).OnTrigger(QuestReply.Event(42).SetPriority(200)) // Sandy
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Zedduva).OnTrigger(QuestReply.Event(40).SetPriority(200)); // Bastok
        }
    }
}
