// Chocobo on the Loose! (Log ID 3, Quest ID 92), a line-for-line port of scripts/quests/jeuno/Chocobo_on_the_Loose.lua
// (quest rework B3b). Brutus : !pos -55 8 95 244; Chocobo Tracks : !pos -556 0 523 102; Hantileon : !pos -2.675
// -1.1 -105.287 230. Available from level 20 with ENABLE_TOAU 1: Brutus's 10093 begins it. Accepted: La Theine Plateau's
// Chocobo Tracks at Prog 0: 209 (Prog 1); Brutus: 10094 once per zone at Prog 0, 10095 at Prog 1 (Prog 2), 10099 at
// Prog 2, 10100 at Prog 3 (Prog 4, Timer = the next Vana'diel day, the mustZone), 10109 at Prog 4 with the mustZone
// clear and the Timer reached (completed: the Chocobo Egg (faintly warm), fame +7 in the three nations); Southern San
// d'Oria's Hantileon: 821 at Prog 2 (Prog 3), 822 at Prog 3. Completed: no section.
// The Lua file has no known bug to keep; the reward's egg (TODO in the Lua: "Verify this is the correct egg") is kept.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class ChocoboOnTheLoose : QuestScript
    {
        public ChocoboOnTheLoose() : base(QuestLog.Jeuno, "CHOCOBO_ON_THE_LOOSE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Chocobo_on_the_Loose";

        protected override void Build(QuestScriptBuilder b)
        {
            // TODO: Verify this is the correct egg for Quest Reward
            b.Reward(new QuestReward { Item = Item.ChocoboEggFaintlyWarm });

            // ENABLE_TOAU == 1 compared as Lua compares (Settings.MainEquals, P9): 1.25 is not 1, nor is true
            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainLevel >= 20 && q.Settings.MainEquals("ENABLE_TOAU", 1))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Brutus).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(10093);
                })
                .OnEventFinish(Zone.UpperJeuno, 10093, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.ChocoboTracks).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(209);
                    }
                    return null;
                })
                .OnEventFinish(Zone.LaTheinePlateau, 209, q =>
                {
                    q.Quest.SetStep(1);
                })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Hantileon).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(821);
                    }
                    else if (questProgress == 3)
                    {
                        return QuestReply.ProgressEvent(822);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoria, 821, q =>
                {
                    q.Quest.SetStep(3);
                })
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Brutus).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.Event(10094).OncePerZone();
                    }
                    else if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(10095);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.Event(10099);
                    }
                    else if (questProgress == 3)
                    {
                        return QuestReply.ProgressEvent(10100);
                    }
                    else if (
                        questProgress == 4 &&
                        !q.Quest.MustZone &&
                        q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay)
                    {
                        return QuestReply.ProgressEvent(10109);
                    }
                    return null;
                })
                .OnEventFinish(Zone.UpperJeuno, 10095, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.UpperJeuno, 10100, q =>
                {
                    q.Quest.SetStep(4);
                    q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay + 1);
                    q.Quest.SetMustZone();
                })
                .OnEventFinish(Zone.UpperJeuno, 10109, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 7);
                        q.Player.AddFame(FameArea.Bastok, 7);
                        q.Player.AddFame(FameArea.Windurst, 7);
                    }
                });
        }
    }
}
