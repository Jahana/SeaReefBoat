using System;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

// the call-only test mission's states (quest rework C2 Task 4), shared by XI.Verify.Quests and QuestScripts.Live: at
// Gerbaum (Bastok Mines) status 0 plays 1000 through xi.cs.mission and its finish (xi.cs.missionEvent) sets status 1;
// status 1 plays 1001 and its zone-in is 1002. extra: what a suite adds to the status 1 state
internal static class C2CallMissionBody
{
    public static void Build(MissionScriptBuilder b, Action<QuestStateBuilder> extra = null)
    {
        b.State(current: true, status: 0).Npc(QC.Zone.BastokMines, QC.Npc.BastokMines.Gerbaum).Called()
            .OnTrigger(QuestReply.Event(1000))
            .OnEventFinish(QC.Zone.BastokMines, 1000, q => q.Mission.SetStatus(1));
        var last = b.State(current: true, status: 1);
        last.Npc(QC.Zone.BastokMines, QC.Npc.BastokMines.Gerbaum).Called().OnTrigger(QuestReply.Event(1001))
            .OnZoneIn(QC.Zone.BastokMines, q => QuestReply.Event(1002));
        extra?.Invoke(last);
    }
}
