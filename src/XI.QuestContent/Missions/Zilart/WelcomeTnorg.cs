// Welcome t'Norg (Zilart 2, Log ID 3, Mission ID 4), a line-for-line port of
// scripts/missions/rotz/02_Welcome_to_Norg.lua (quest rework C3, C5). _700 (Oaken Door) !pos 97 -7 -12 252. Section 1
// (current): Norg's _700 plays 2; 2's update on option 0 answers (252, 0, 1, MAX_UINT32 - 72454152, 66977791,
// 222376875, 4031, 4); 2's finish on option 0 completes the mission (Kazham's Chieftainess next; no key item, no
// title). Kept as the Lua has it: another option leaves the mission current.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Zilart
{
    public sealed class WelcomeTnorg : MissionScript
    {
        public WelcomeTnorg() : base(XI.Quests.MissionLog.Zilart, "WELCOME_TNORG") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/rotz/02_Welcome_to_Norg";

        // utils.MAX_UINT32 - n
        static int MaxUint32Minus(uint n) => unchecked((int)(uint.MaxValue - n));

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.Zilart,
                NextMission = Mission.Zilart.KazhamsChieftainess,
            });

            b.State(current: true)
                .Npc(Zone.Norg, Npc.Norg._700).OnTrigger(q => QuestReply.ProgressEvent(2))
                .OnEventUpdate(Zone.Norg, 2, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.UpdateEvent(252, 0, 1, MaxUint32Minus(72454152), 66977791, 222376875, 4031, 4);
                    }
                })
                .OnEventFinish(Zone.Norg, 2, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Mission.Complete();
                    }
                });
        }
    }
}
