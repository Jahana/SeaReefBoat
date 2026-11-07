// Everyone's Grudging (Log ID 5, Quest ID 4), a line-for-line port of scripts/quests/outlands/Everyones_Grudging.lua
// (quest rework B3b Outlands, group 2). Kazham: Jakoh Wahcondalo !pos 101 -15 -115 (his script: Tuning Out only; default
// 113), Etteh Sulaej !pos 94 -14.5 -111 (no script; default 42), Romaa Mihgo !pos 29 -12 -176.5 (her script: Tuning Out
// only; default 263). Den of Rancor: the NM Rancor Torch (popped by the Altar of Rancor _4ge on the fourth Rancor Flame:
// its NPC script; his mob script sets rancorCurse 0 on his death).
// Offered at Windurst fame level 7 with A Question of Taste completed, A Pose By Any Other Name's mustZone clear and the
// char var rancorCurse 1 (an Altar of Rancor's flame: _4gb / _4gc / _4gd's NPC scripts): Jakoh 107 (a progress cutscene: 0,
// Rancor Flame) begins it and adds the title Excommunicate of Kazham. Accepted: Etteh 109, Romaa 276, Jakoh 108 at Prog 0
// else 110 (all Events: 0, Rancor Flame); Rancor Torch's death at Prog 0 sets Prog 1; 110's finish completes it (Windurst
// fame 60, 11000 gil). Completed: no section.
// Kept as the Lua has it: 110 is an Event (not a progress event) and its finish completes; quest:complete's result is not
// read (a gil reward: always true); the Torch's death sets Prog 1 whoever claimed it.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class EveryonesGrudging : QuestScript
    {
        public EveryonesGrudging() : base(QuestLog.Outlands, "EVERYONES_GRUDGING") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/Everyones_Grudging";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 60, FameArea = FameArea.Windurst, Gil = 11000 });
            b.Steps(1);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.Windurst) >= 7 &&
                    q.Player.HasCompletedQuest(QuestLog.Outlands, "A_QUESTION_OF_TASTE") &&
                    !q.Player.QuestMustZone(QuestLog.Windurst, "A_POSE_BY_ANY_OTHER_NAME") &&
                    q.Player.CharVar("rancorCurse") == 1)
                .Npc(Zone.Kazham, Npc.Kazham.JakohWahcondalo).OnTrigger(QuestReply.ProgressCutscene(107, 0, Item.RancorFlame))
                .OnEventFinish(Zone.Kazham, 107, q =>
                {
                    q.Quest.Begin();
                    q.Player.AddTitle(Title.ExcommunicateOfKazham);
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Kazham, Npc.Kazham.EttehSulaej).OnTrigger(QuestReply.Event(109, 0, Item.RancorFlame))
                .Npc(Zone.Kazham, Npc.Kazham.JakohWahcondalo).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.Event(108, 0, Item.RancorFlame);
                    }
                    else
                    {
                        return QuestReply.Event(110, 0, Item.RancorFlame);
                    }
                })
                .Npc(Zone.Kazham, Npc.Kazham.RomaaMihgo).OnTrigger(QuestReply.Event(276, 0, Item.RancorFlame))
                .OnEventFinish(Zone.Kazham, 110, q =>
                {
                    q.Quest.Complete();
                })
                .Npc(Zone.DenOfRancor, Npc.DenOfRancor.RancorTorch).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        q.Quest.SetStep(1);
                    }
                });
        }
    }
}
