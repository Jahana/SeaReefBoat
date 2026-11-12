// Ro'Maeve (Zilart 9, Log ID 3, Mission ID 18), a line-for-line port of scripts/missions/rotz/09_RoMaeve.lua (quest
// rework C3, C5). _700 (Oaken Door) !pos 97 -7 -12 252; Aldo !pos 20 3 -58 245. Section 1 (current): Lower Jeuno's
// Aldo plays 84 while the Option var is 0, whose finish sets it to 1, else 24; Norg's _700 plays 3, whose finish on
// option 0 completes the mission (The Temple of Desolation next; no key item, no title). Kept as the Lua has it: the
// Option var gates only Aldo's two lines; the 3's finish does not move the char (the Lua's own note says it should).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Zilart
{
    public sealed class Romaeve : MissionScript
    {
        public Romaeve() : base(XI.Quests.MissionLog.Zilart, "ROMAEVE") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/rotz/09_RoMaeve";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.Zilart,
                NextMission = Mission.Zilart.TheTempleOfDesolation,
            });

            b.State(current: true)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Aldo).OnTrigger(q =>
                {
                    if (q.Mission.Var("Option") == 0)
                    {
                        return QuestReply.ProgressEvent(84);
                    }
                    else
                    {
                        return QuestReply.Event(24);
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 84, q => q.Mission.SetVar("Option", 1))
                .Npc(Zone.Norg, Npc.Norg._700).OnTrigger(QuestReply.ProgressEvent(3))
                .OnEventFinish(Zone.Norg, 3, q =>
                {
                    if (q.Option == 0)
                    {
                        // NOTE: Event should move player to 99.789, -7.086, -11.999, 126 (Norg)
                        q.Mission.Complete();
                    }
                });
        }
    }
}
