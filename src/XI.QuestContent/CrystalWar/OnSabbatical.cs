// On Sabbatical (Log ID 7, Quest ID 32), a line-for-line port of scripts/quests/crystalWar/SCH_AF1_On_Sabbatical.lua
// (quest rework B3b Crystal War, draft). The Eldieme Necropolis [S]: Erlene !pos 376.936 -39.999 17.914 (default 15).
// Bastok Markets [S]: Gentle Tiger !pos -203.932 -9.998 2.237 (default 109). Pashhow Marshlands [S]: Indescript
// Markings !pos -456.707 24.4385 -363.364 (three of the name; their script's default NOTHING_OUT_OF_ORDINARY).
// Available, a Scholar at or above AF1_QUEST_LEVEL: Erlene: 18, whose finish gives Ulbrecht's Sealed Letter and begins
// it. Accepted: Gentle Tiger: Prog 0: 46 (-> Prog 1); else 47. Indescript Markings: the first of the three
// (INDESCRIPT_MARKINGS_OFFSET) at Prog 1: 2, whose finish gives Schultz's Sealed Letter and sets Prog 2. Erlene: Prog
// 2: 20, whose finish completes it (a Klimaform Schema), takes both letters and sets this quest's Timer to tomorrow and
// its mustZone (Downward Helix reads the Timer); else 19.
// Kept as the Lua has it: 18 begins on any option; 47 and 19 have no finish; the Timer and the mustZone are written
// after the completion's cleanup, by xi.quest.setVar / setMustZone on the quest's own id; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class OnSabbatical : QuestScript
    {
        public OnSabbatical() : base(QuestLog.CrystalWar, "ON_SABBATICAL") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/SCH_AF1_On_Sabbatical";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2);

            b.Reward(new QuestReward
            {
                Item = Item.KlimaformSchema,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainJob == Job.Sch &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF1_QUEST_LEVEL"))
                .Npc(Zone.TheEldiemeNecropolisS, Npc.TheEldiemeNecropolisS.Erlene).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(18);
                })
                .OnEventFinish(Zone.TheEldiemeNecropolisS, 18, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.UlbrechtsSealedLetter);
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(46);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(47);
                    }
                })
                .OnEventFinish(Zone.BastokMarketsS, 46, q =>
                {
                    q.Quest.SetStep(1);
                })
                .Npc(Zone.PashhowMarshlandsS, Npc.PashhowMarshlandsS.IndescriptMarkings).OnTrigger(q =>
                {
                    int offset = q.Npc.Id - q.Player.NpcIdOf("INDESCRIPT_MARKINGS_OFFSET");
                    if (offset == 0 && q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(2);
                    }
                    return null;
                })
                .OnEventFinish(Zone.PashhowMarshlandsS, 2, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.SchultzsSealedLetter);
                    q.Quest.SetStep(2);
                })
                .Npc(Zone.TheEldiemeNecropolisS, Npc.TheEldiemeNecropolisS.Erlene).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(20);
                    }

                    return QuestReply.ProgressEvent(19);
                })
                .OnEventFinish(Zone.TheEldiemeNecropolisS, 20, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.UlbrechtsSealedLetter);
                        q.Player.DelKeyItem(KeyItem.SchultzsSealedLetter);
                        q.Player.SetQuestVar(
                            QuestLog.CrystalWar, "ON_SABBATICAL", "Timer", q.Player.VanadielUniqueDay + 1);
                        q.Player.SetQuestMustZone(QuestLog.CrystalWar, "ON_SABBATICAL");
                    }
                });
        }
    }
}
