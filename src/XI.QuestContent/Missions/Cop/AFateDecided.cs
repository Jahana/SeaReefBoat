// A Fate Decided (Promathia 8-2, Log ID 6, Mission ID 818), a line-for-line port of
// scripts/missions/cop/8_2_A_Fate_Decided.lua (quest rework C3, C4). The mission current, in the Grand Palace of
// Hu'Xzoi. _iya says DOES_NOT_RESPOND at priority 1000. _iyq at Status 0 with Ixghrah not up spawns him (SpawnMob,
// claimed) and says PRESENCE_HAS_DRAWN; at Status 1 it plays the cutscene 3, whose finish completes the mission.
// Ixghrah's death at Status 0 sets Status 1. When Angels Fall is next; no title.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class AFateDecided : MissionScript
    {
        public AFateDecided() : base(XI.Quests.MissionLog.CoP, "A_FATE_DECIDED") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/8_2_A_Fate_Decided";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.WhenAngelsFall,
            });

            b.State(current: true)
                .Npc(Zone.GrandPalaceOfHuxzoi, Npc.GrandPalaceOfHuxzoi.iya).OnTrigger(q =>
                    QuestReply.MessageSpecial(Text.GrandPalaceOfHuxzoi.DOES_NOT_RESPOND).SetPriority(1000))
                .Npc(Zone.GrandPalaceOfHuxzoi, Npc.GrandPalaceOfHuxzoi.iyq).OnTrigger(q =>
                {
                    int missionStatus = q.Mission.Var("Status");

                    if (missionStatus == 0 && !q.Player.MobIsSpawned(Mob.GrandPalaceOfHuxzoi.IXGHRAH))
                    {
                        q.Player.SpawnMob(Mob.GrandPalaceOfHuxzoi.IXGHRAH, 0, claim: true);
                        return QuestReply.MessageSpecial(Text.GrandPalaceOfHuxzoi.PRESENCE_HAS_DRAWN);
                    }
                    else if (missionStatus == 1)
                    {
                        return QuestReply.ProgressCutscene(3);
                    }

                    return null;
                })
                .Npc(Zone.GrandPalaceOfHuxzoi, Npc.GrandPalaceOfHuxzoi.Ixghrah).OnMobDeath(q =>
                {
                    if (q.Mission.Var("Status") == 0)
                    {
                        q.Mission.SetVar("Status", 1);
                    }
                })
                .OnEventFinish(Zone.GrandPalaceOfHuxzoi, 3, q => q.Mission.Complete());
        }
    }
}
