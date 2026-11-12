// Tango with a Tracker (Log ID 4, Quest ID 82), a line-for-line port of scripts/quests/otherAreas/Tango_with_a_Tracker.lua
// (quest rework B3b). Tavnazian Safehold: Despachiaire !pos 111.209 -40.015 -85.481 (no script, default 106); Boneyard
// Gully: the battlefield Tango with a Tracker (Battlefields/BoneyardGully/TangoWithATracker.cs, a
// BattlefieldQuest: id 677, index 5, entry _081; its own checkRequirements asks the registrant for the Letter from Shikaree
// X through Battlefield's, not BattlefieldQuest's, so no quest status: the available quest enters and begins inside).
// Available once CoP's A Fate Decided is completed: Despachiaire at Prog 0: 576, whose finish gives the Letter from
// Shikaree X, sets ShikareeBattleWait = NextConquestTally() and Prog 1. Boneyard Gully's 32000 finish (any battlefield's
// entry menu): with no battlefield, nothing; in battlefield 677: begun. Accepted: Despachiaire, when ShikareeBattleWait <
// NextConquestTally() and the letter is not held: 577 (a new letter, the wait set again). Boneyard Gully's 32001 with
// battlefieldWin 677: completed (10000 gil, Sin Hunter Hunter).
// Kept as the Lua has it: 576 sets Prog 1 while the quest stays available; the wait is a plain char var (Requiem of Sin
// shares it); a battlefield's 32001 sets battlefieldWin only while the quest is accepted (BattlefieldQuest:onBattlefieldWin).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class TangoWithATracker : QuestScript
    {
        public TangoWithATracker() : base(QuestLog.OtherAreas, "TANGO_WITH_A_TRACKER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Tango_with_a_Tracker";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Gil = 10000,
                Title = Title.SinHunterHunter,
            });
            b.Steps(1);

            // Section 1: Player receives key item to begin quest at battlefield
            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.AFateDecided))
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Despachiaire).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(576);
                    }
                    return null;
                })
                .OnEventFinish(Zone.TavnazianSafehold, 576, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.LetterFromShikareeX);
                    q.Player.SetCharVar("ShikareeBattleWait", q.Player.NextConquestTally); // Player can only get a new letter once per conquest reset.
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

                    if (battlefield == Battlefield.TangoWithATracker)
                    {
                        q.Quest.Begin();
                    }
                });

            // Section 2: Can speak with Despachiaire to receive letter again if failed after next conquest tally
            // Complete quest on battlefield win
            b.State(QuestStatus.Accepted)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Despachiaire).OnTrigger(q =>
                {
                    if (q.Player.CharVar("ShikareeBattleWait") < q.Player.NextConquestTally &&
                        !q.Player.HasKeyItem(KeyItem.LetterFromShikareeX))
                    {
                        return QuestReply.ProgressEvent(577); // Recieve a subsequent letter
                    }
                    return null;
                })
                .OnEventFinish(Zone.TavnazianSafehold, 577, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.LetterFromShikareeX);
                    q.Player.SetCharVar("ShikareeBattleWait", q.Player.NextConquestTally); // Player can only get a new letter once per conquest reset.
                })
                .OnEventFinish(Zone.BoneyardGully, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.TangoWithATracker)
                    {
                        q.Quest.Complete();
                    }
                });
        }
    }
}
