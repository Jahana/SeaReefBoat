// Requiem of Sin (Log ID 4, Quest ID 83), a line-for-line port of scripts/quests/otherAreas/Requiem_of_Sin.lua (quest rework
// B3b). Tavnazian Safehold: Despachiaire !pos 111.209 -40.015 -85.481 (no script, default 106); Boneyard Gully: the
// battlefield Requiem of Sin (scripts/battlefields/Boneyard_Gully/requiem_of_sin.lua: id 678, index 6, entry _081; its own
// checkRequirements: the Letter from Shikaree Y or the Letter from the Mithran Trackers, no quest status).
// Available once Tango with a Tracker is completed: Despachiaire, when ShikareeBattleWait < NextConquestTally() and Prog 0:
// 578, whose finish gives the Letter from Shikaree Y, sets the wait and Prog 1. Boneyard Gully's 32000 finish: no
// battlefield: nothing; battlefield 678: begun. Accepted: Despachiaire, the wait over and the letter not held: 579 (the
// letter again, the wait set); 32001 with battlefieldWin 678: completed (Disciple of Justice). Completed, the wait over and
// the Letter from the Mithran Trackers not held (the check): Despachiaire 579, whose finish gives that letter and sets the
// wait.
// Kept as the Lua has it: 578 sets Prog 1 while the quest stays available; the completed section's conditions sit in its
// check, so its 579 finish runs only while they still hold.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class RequiemOfSin : QuestScript
    {
        public RequiemOfSin() : base(QuestLog.OtherAreas, "REQUIEM_OF_SIN") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Requiem_of_Sin";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Title = Title.DiscipleOfJustice,
            });
            b.Steps(1);

            // Section 1: Player has completed Tango with a Tracker and can receive the initial letter from Despachiaire.
            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.OtherAreas, "TANGO_WITH_A_TRACKER"))
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Despachiaire).OnTrigger(q =>
                {
                    if (q.Player.CharVar("ShikareeBattleWait") < q.Player.NextConquestTally &&
                        q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(578);
                    }
                    return null;
                })
                .OnEventFinish(Zone.TavnazianSafehold, 578, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.LetterFromShikareeY);
                    q.Player.SetCharVar("ShikareeBattleWait", q.Player.NextConquestTally);
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.BoneyardGully, 32000, q =>
                {
                    // local battlefield = player:getBattlefield() (BattlefieldId: its getID(), null when there is none)
                    int? battlefield = q.Player.BattlefieldId;
                    if (battlefield == null)
                    {
                        return;
                    }

                    if (battlefield == Battlefield.RequiemOfSin)
                    {
                        q.Quest.Begin();
                    }
                });

            // Section 2: Player has the letter and can obtain replacement letters or enter the battlefield.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Despachiaire).OnTrigger(q =>
                {
                    if (q.Player.CharVar("ShikareeBattleWait") < q.Player.NextConquestTally &&
                        !q.Player.HasKeyItem(KeyItem.LetterFromShikareeY))
                    {
                        return QuestReply.ProgressEvent(579);
                    }
                    return null;
                })
                .OnEventFinish(Zone.TavnazianSafehold, 579, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.LetterFromShikareeY);
                    q.Player.SetCharVar("ShikareeBattleWait", q.Player.NextConquestTally);
                })
                .OnEventFinish(Zone.BoneyardGully, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.RequiemOfSin)
                    {
                        q.Quest.Complete();
                    }
                });

            // Section 3: Quest is completed. Player can obtain a new Letter From the Mithran Trackers once per conquest tally
            b.State(QuestStatus.Completed, when: q => q.Player.CharVar("ShikareeBattleWait") < q.Player.NextConquestTally &&
                                                      !q.Player.HasKeyItem(KeyItem.LetterFromTheMithranTrackers))
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Despachiaire).OnTrigger(QuestReply.ProgressEvent(579))
                .OnEventFinish(Zone.TavnazianSafehold, 579, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.LetterFromTheMithranTrackers);
                    q.Player.SetCharVar("ShikareeBattleWait", q.Player.NextConquestTally);
                });
        }
    }
}
