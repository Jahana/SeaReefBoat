// The Wyrm God (Log ID 8, Quest ID 184), a line-for-line port of scripts/quests/abyssea/The_Wyrm_God.lua (quest rework
// B3b Abyssea). Abyssea - Empyreal Paradox: TR_Entrance !pos 539.861 -500 -594.576 255 (no script, no default), Prishe
// !pos 536.353 -499.999 -591.338 255 (no script; default 205). Added by Beneath a Blood Red Sky's 202 (no available
// section). Accepted: TR_Entrance at Prog 0: 208 (-> Prog 1); the 32001 finish at Prog 1 with the char's battlefieldWin
// local var at xi.battlefield.id.THE_WYRM_GOD: Prog 2; Prishe at Prog 2: 203, whose finish completes it and adds
// Meanwhile, Back on Abyssea (accepted). Completed: Prishe: 206 (replaceDefault; it ties with Meanwhile, Back on
// Abyssea's 207 once both are completed, 207 first). Kept as the Lua has it: xi.battlefield.id has no THE_WYRM_GOD
// (globals/battlefield.lua:107-358), so the Lua compares the local var with nil, never equal: 32001 never sets Prog 2
// (and Abyssea - Empyreal Paradox has no battlefield, so no 32001 plays there at all); the Lua's TODO.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Abyssea
{
    public sealed class TheWyrmGod : QuestScript
    {
        public TheWyrmGod() : base(QuestLog.Abyssea, "THE_WYRM_GOD") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/abyssea/The_Wyrm_God";

        // xi.battlefield.id.THE_WYRM_GOD: nil (no such entry), which no local var equals
        static readonly int? BattlefieldTheWyrmGod = null;

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2);

            b.Reward(new QuestReward());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.AbysseaEmpyrealParadox, Npc.AbysseaEmpyrealParadox.TREntrance).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(208);
                    }
                    return null;
                })
                .Npc(Zone.AbysseaEmpyrealParadox, Npc.AbysseaEmpyrealParadox.Prishe).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(203);
                    }
                    return null;
                })
                .OnEventFinish(Zone.AbysseaEmpyrealParadox, 208, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.AbysseaEmpyrealParadox, 32001, q =>
                {
                    // TODO: 'battlefieldWin' local var should be set on successful completion of the BCNM
                    // with its BCNM ID. See other examples of this pattern implemented in various missions.
                    if (q.Player.LocalVar("battlefieldWin") == BattlefieldTheWyrmGod &&
                        q.Quest.Step == 1)
                    {
                        q.Quest.SetStep(2);
                    }
                })
                .OnEventFinish(Zone.AbysseaEmpyrealParadox, 203, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddQuest(QuestLog.Abyssea, "MEANWHILE_BACK_ON_ABYSSEA");
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.AbysseaEmpyrealParadox, Npc.AbysseaEmpyrealParadox.Prishe)
                    .OnTrigger(QuestReply.Event(206).ReplaceDefault());
        }
    }
}
