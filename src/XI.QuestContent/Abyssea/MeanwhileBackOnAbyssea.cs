// Meanwhile, Back on Abyssea (Log ID 8, Quest ID 185), a line-for-line port of
// scripts/quests/abyssea/Meanwhile_Back_on_Abyssea.lua (quest rework B3b Abyssea). Port Jeuno: Joachim
// !pos -52.844 0 -9.978 246 (his script: the traverser stone menu). Hall of the Gods: its zone-in.
// Abyssea - Empyreal Paradox: Prishe !pos 536.353 -499.999 -591.338 255 (no script; default 205). Added by The Wyrm
// God's 203 (no available section). Accepted: Joachim at Prog 0: 345 (-> Prog 1). Prog 1: the Hall of the Gods zone-in
// from 18:00 to 4:59: 9 (-> Prog 2). Prog 2: Prishe: 204, whose finish completes it (the Abyssite of the Cosmos) and
// adds A Moonlight Requite (accepted; it has no file: it marks the story's end). Completed: Prishe: 207
// (replaceDefault). Kept as the Lua has it: 9 is returned as a number (an Event); once The Wyrm God is completed too,
// Prishe's 206 (its completed section) and 207 tie (both replaceDefault Events) and rotate, 207 first (this quest's
// container comes before The Wyrm God's).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class MeanwhileBackOnAbyssea : QuestScript
    {
        public MeanwhileBackOnAbyssea() : base(QuestLog.Abyssea, "MEANWHILE_BACK_ON_ABYSSEA") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/Meanwhile_Back_on_Abyssea";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2);

            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.AbyssiteOfTheCosmos,
            });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Joachim).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(345);
                    }
                    return null;
                })
                .OnEventFinish(Zone.PortJeuno, 345, q =>
                {
                    q.Quest.SetStep(1);
                });

            b.State(QuestStatus.Accepted, step: 1)
                .OnZoneIn(Zone.HallOfTheGods, q =>
                {
                    int hour = q.Player.VanadielHour;
                    if (hour >= 18 || hour < 5)
                    {
                        return QuestReply.Event(9);
                    }
                    return null;
                })
                .OnEventFinish(Zone.HallOfTheGods, 9, q =>
                {
                    q.Quest.SetStep(2);
                });

            b.State(QuestStatus.Accepted, step: 2)
                .Npc(Zone.AbysseaEmpyrealParadox, Npc.AbysseaEmpyrealParadox.Prishe)
                    .OnTrigger(QuestReply.ProgressEvent(204))
                .OnEventFinish(Zone.AbysseaEmpyrealParadox, 204, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddQuest(QuestLog.Abyssea, "A_MOONLIGHT_REQUITE");
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.AbysseaEmpyrealParadox, Npc.AbysseaEmpyrealParadox.Prishe)
                    .OnTrigger(QuestReply.Event(207).ReplaceDefault());
        }
    }
}
