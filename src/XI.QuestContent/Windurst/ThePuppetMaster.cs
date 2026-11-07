// The Puppet Master (Log ID 2, Quest ID 81), a line-for-line port of scripts/quests/windurst/SMN_AF1_The_Puppet_Master.lua
// (quest rework B3b). House of the Hero (_6n2) : !pos -26 -13 260 239; Juroro : !pos 32 7 -41 236; Koru-Moru : !pos -120
// -6 124 239. A summoner at the AF1 level with no mustZone: the House of the Hero's 402 begins it (403 while accepted).
// Juroro (Port Bastok): 256 at Prog 0 (a progress: it ties with Trial by Earth's offer) gives the Earth Pendulum (Prog
// 1); at Prog 1 without the pendulum and not on Trial by Earth, 257 gives another; at Prog 1 otherwise his 253 with the
// pendulum's arguments; 258 at Prog 2 (Prog 3). The Cloister of Tremors battlefield (puppet_master.lua, entered with the
// pendulum, kept) wins with 32001, whose finish here sets Prog 2 when the char's local var battlefieldWin is the
// battlefield's id and Prog is 1. Koru-Moru's 404 at Prog 3 completes it (fame 20, Kukulcan's Staff; this quest's and
// Class Reunion's mustZone). Completed while Class Reunion is not: Koru-Moru's 405 while the mustZone holds. Kept as the
// Lua file has it: 257's giveItem is not checked; 256 sets Prog 1 only when the pendulum is given.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class ThePuppetMaster : QuestScript
    {
        public ThePuppetMaster() : base(QuestLog.Windurst, "THE_PUPPET_MASTER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/SMN_AF1_The_Puppet_Master";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Windurst, Item = Item.KukulcansStaff });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainLevel >= q.Settings.MainNumber("AF1_QUEST_LEVEL") &&
                    q.Player.MainJob == Job.Smn &&
                    !q.Quest.MustZone)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls._6n2).OnTrigger(QuestReply.ProgressEvent(402))
                .OnEventFinish(Zone.WindurstWalls, 402, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls._6n2).OnTrigger(QuestReply.Event(403))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KoruMoru).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(404);
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWalls, 404, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Quest.SetMustZone();
                        q.Player.SetQuestMustZone(QuestLog.Windurst, "CLASS_REUNION");
                    }
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Juroro).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        // Takes priority over quest "Trial by Earth"
                        return QuestReply.ProgressEvent(256, 0, KeyItem.TuningForkOfEarth, 0, Item.EarthPendulum).Progress();
                    }
                    else if (
                        questProgress == 1 &&
                        !q.Player.HasItem(Item.EarthPendulum) &&
                        // Progress is locked as long as the player is on Trial by Earth as confirmed in captures
                        q.Player.QuestStatus(QuestLog.Bastok, "TRIAL_BY_EARTH") != QuestStatus.Accepted)
                    {
                        return QuestReply.ProgressEvent(257, 0, Item.EarthPendulum);
                    }
                    else if (questProgress == 1)
                    {
                        return QuestReply.Event(253, 0, 0, 0, 0, 0, Item.EarthPendulum, 1);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(258);
                    }

                    return null;
                })
                .OnEventFinish(Zone.PortBastok, 256, q =>
                {
                    if (q.Player.GiveItem(Item.EarthPendulum))
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.PortBastok, 257, q => q.Player.GiveItem(Item.EarthPendulum))
                .OnEventFinish(Zone.PortBastok, 258, q => q.Quest.SetStep(3))
                // the battlefield's win event (BattlefieldQuest:onBattlefieldWin sets battlefieldWin while accepted)
                .OnEventFinish(Zone.CloisterOfTremors, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.PuppetMaster &&
                        q.Quest.Step == 1)
                    {
                        q.Quest.SetStep(2);
                    }
                });

            b.State(QuestStatus.Completed, when: q => !q.Player.HasCompletedQuest(QuestLog.Windurst, "CLASS_REUNION"))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KoruMoru).OnTrigger(q =>
                {
                    if (q.Quest.MustZone)
                    {
                        return QuestReply.Event(405);
                    }

                    return null;
                });
        }
    }
}
