// Storms of Fate (Log ID 3, Quest ID 86), a line-for-line port of scripts/quests/jeuno/Storms_of_Fate.lua (quest rework
// B3b). _0p2 : !pos -259 -30 276 25; Unstable_Displacement : !pos -612.800 1.750 693.190 29. Available while
// CoP Dawn is current with its Status var 8 and this quest's mustZone clear: Ru'Lude Gardens' trigger area 1 plays 142
// (option 1 begins, any other sets the mustZone). Accepted: Misareaux Coast's _0p2 at Prog 0: 559 (Prog 1); Riverne
// Site #B01's Unstable_Displacement at Prog 1: 1 (Prog 2); the Storms of Fate battlefield's 32001 at Prog 2 with
// battlefieldWin 896: the Whisper of the Wyrmking, Prog 3; Ru'Lude Gardens' area 1 at Prog 3: 143 (completed;
// Shadows of the Departed's Timer = VanadielUniqueDay() + 1).
// Kept Lua quirks: 142's decline sets the mustZone (a placeholder, the Lua says); the area-1 handler of the accepted
// state returns nil before Prog 3; 143's finish sets the next quest's Timer only when quest:complete succeeds (an
// empty reward: always).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class StormsOfFate : QuestScript
    {
        public StormsOfFate() : base(QuestLog.Jeuno, "STORMS_OF_FATE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Storms_of_Fate";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward());

            b.State(QuestStatus.Available, when: q =>
                    q.Player.CurrentMission(MissionLog.Cop) == Mission.Cop.Dawn &&
                    q.Player.MissionVar(MissionLog.Cop, Mission.Cop.Dawn, "Status") == 8 &&
                    !q.Quest.MustZone)
                .OnTriggerAreaEnter(Zone.RuludeGardens, 1, q =>
                {
                    return QuestReply.ProgressEvent(142);
                })
                .OnEventFinish(Zone.RuludeGardens, 142, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                    else
                    {
                        // TODO: There may be an alternate event or parameters passed if this quest was
                        // initially declined.  This mustZone is a placeholder only, and needs verification.

                        q.Quest.SetMustZone();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.MisareauxCoast, Npc.MisareauxCoast._0p2).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(559);
                    }
                    return null;
                })
                .OnEventFinish(Zone.MisareauxCoast, 559, q =>
                {
                    q.Quest.SetStep(1);
                })
                .Npc(Zone.RiverneSiteB01, Npc.RiverneSiteB01.UnstableDisplacement).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(1);
                    }
                    return null;
                })
                .OnEventFinish(Zone.RiverneSiteB01, 1, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.RiverneSiteB01, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.StormsOfFate &&
                        q.Quest.Step == 2)
                    {
                        q.Player.GiveKeyItem(KeyItem.WhisperOfTheWyrmking);
                        q.Quest.SetStep(3);
                    }
                })
                .OnTriggerAreaEnter(Zone.RuludeGardens, 1, q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(143);
                    }
                    return null;
                })
                .OnEventFinish(Zone.RuludeGardens, 143, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetQuestVar(QuestLog.Jeuno, "SHADOWS_OF_THE_DEPARTED", "Timer", q.Player.VanadielUniqueDay + 1);
                    }
                });
        }
    }
}
