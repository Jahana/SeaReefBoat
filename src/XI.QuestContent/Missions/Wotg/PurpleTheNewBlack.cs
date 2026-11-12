// Purple, The New Black (WotG 7, Log ID 5, Mission ID 6), a line-for-line port of
// scripts/missions/wotg/07_Purple_The_New_Black.lua (quest rework C3 T5). Reinforced Gateway (_2d1)
// !pos -114.386 -3.599 -179.804 85. Section 0 (status 0): La Vaule [S]'s _2d1 plays the progress event 2 (a 85, 3456,
// MAX_UINT32 - 207616, 1525, 0, 19550816 operand list), whose finish sets status 1. Section 1 (status 1 or more): the
// zone-in plays 6 at status 2; the battlefield's win finish 32001 (Battlefields/LaVauleS/PurpleTheNewBlack.cs fights
// it) sets status 2 and sets the char down at -260.44 0.134 -156.652 rot 192 in the same zone only when the char's
// battlefieldWin local var is this battlefield's id; 6's finish completes the mission (In the Name of the Father
// current).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class PurpleTheNewBlack : MissionScript
    {
        public PurpleTheNewBlack() : base(XI.Quests.MissionLog.WoTG, "PURPLE_THE_NEW_BLACK") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/07_Purple_The_New_Black";

        const int AreaId = MissionLog.Wotg;

        // utils.MAX_UINT32 - n
        static int MaxUint32Minus(uint n) => unchecked((int)(uint.MaxValue - n));

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.WoTG,
                NextMission = Mission.Wotg.InTheNameOfTheFather,
            });

            // 0: Pre-BCNM CS
            b.State(current: true, status: 0)
                .Npc(Zone.LaVauleS, Npc.LaVauleS._2d1).OnTrigger(
                    QuestReply.ProgressEvent(2, 85, 3456, MaxUint32Minus(207616), 1525, 0, 19550816, 0, 0))
                .OnEventFinish(Zone.LaVauleS, 2, q =>
                {
                    q.Player.SetMissionStatus(AreaId, 1);
                });

            // 1: Enter the BCNM vs Galarhigg
            b.State(current: true, when: q => q.Player.MissionStatus(AreaId) >= 1)
                .OnZoneIn(Zone.LaVauleS, q => q.Player.MissionStatus(AreaId) == 2 ? QuestReply.Event(6) : null)
                .OnEventFinish(Zone.LaVauleS, 32001, q =>
                {
                    // Completed BCNM
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.PurpleTheNewBlack)
                    {
                        q.Player.SetMissionStatus(AreaId, 2);
                        q.Player.SetPos(-260.44, 0.134, -156.652, 192, Zone.LaVauleS);
                    }
                })
                .OnEventFinish(Zone.LaVauleS, 6, q =>
                {
                    q.Mission.Complete();
                });
        }
    }
}
