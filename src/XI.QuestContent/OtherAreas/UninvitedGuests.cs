// Uninvited Guests (Log ID 4, Quest ID 81), a line-for-line port of scripts/quests/otherAreas/Uninvited_Guests.lua
// (quest rework B3b, Other Areas group 7). Tavnazian Safehold: Justinius !pos 76 -34 68 (no script; default 265);
// Monarch Linn: the battlefield Uninvited Guests (Battlefields/MonarchLinn/UninvitedGuests.cs: index 6,
// entry SD_Entrance, the Monarch Linn Patrol Permit spent on entry; requiredVar Quest[4][81]Prog = 1 of the
// registrant).
// Available once CoP's The Savage is completed: Justinius 570, whose finish on option 1 starts the quest (the permit,
// Prog 1, the char var UninvitedGuestsReward 0, begun). Accepted or completed: Monarch Linn's 32001 finish (any
// battlefield there): Prog 2. Justinius: at Prog 1 without the permit, Prog 3 first (written in the trigger); then Prog
// 1: 571 (event); Prog 2 or a reward already rolled: 572; Prog 0 and the wait over (Wait < NextConquestTally()): 573;
// Prog 4 and the wait over: 574; Prog 3 or 4: 575. 572's finish: the reward (the char var's, else rolled:
// math.randomInt(1, 1000) over the weights, the char var set); when npcUtil.giveItem hands it over: 10000 gil on the
// first clear (npcUtil.giveCurrency), quest:complete (Monarch Linn Patrol Guard), then Wait = NextConquestTally() and
// the char var 0. 573 on option 1: started again; 574: started again; 575 at Prog 3: Wait = NextConquestTally(), Prog
// 4.
// Kept as the Lua has it: the 32001 handler reads no battlefield id (any Monarch Linn win sets Prog 2); the rolled
// reward is kept in a char var, so a full inventory or a rare item held keeps it (572 again, the same item);
// quest:complete's cleanup clears Prog, so a completed quest sits at Prog 0 (573 once the wait is over); startQuest's
// quest:begin on a completed quest keeps the completed bit; the reward table is walked in its written order (pairs over
// an array).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class UninvitedGuests : QuestScript
    {
        public UninvitedGuests() : base(QuestLog.OtherAreas, "UNINVITED_GUESTS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Uninvited_Guests";

        // phase
        const int WaitingInVictory = 0;
        const int GoToMonarchLinn = 1;
        const int ReturningInVictory = 2;
        const int ReturningInDefeat = 3;
        const int WaitingInDefeat = 4;

        // var
        const string QuestRewardVar = "UninvitedGuestsReward";
        const string ConquestWait = "Wait";

        // The reward is one item, rolled when the player reports in. Weights total 1000.
        // The item list comes from the JP wiki. Retail rates are not published.
        // The memoirs is the common result. The breastplate is the rarest.
        //
        // Source: https://wiki.ffo.jp/html/3837.html
        static readonly (int DropWeight, int RewardId)[] Rewards =
        {
            (541, Item.MiratetesMemoirs),

            // Common.
            (25, Item.PlateOfBreamRisotto),
            (25, Item.SquareOfRaxa),
            (25, Item.ChunkOfAluminumOre),
            (25, Item.ServingOfCrimsonJelly),
            (25, Item.TavnazianSalad),
            (25, Item.ServingOfBisonSteak),
            (25, Item.BowlOfMushroomStew),
            (25, Item.TigerEye),
            (25, Item.UnicornHorn),
            (25, Item.Armoire),
            (25, Item.MannequinBody),
            (25, Item.PlateOfMushroomRisotto),

            // Less common.
            (14, Item.MannequinHands),
            (14, Item.OversizedFang),
            (14, Item.DragonBone),
            (14, Item.ElmLog),
            (14, Item.MannequinLegs),
            (14, Item.MannequinFeet),
            (14, Item.ChunkOfAdamanOre),

            // Uncommon.
            (7, Item.BehemothHide),
            (7, Item.PieceOfHabuSkin),
            (7, Item.CloudEvoker),
            (7, Item.MannequinHead),
            (7, Item.ChunkOfOrichalcumOre),
            (7, Item.ServingOfVermillionJelly),

            // Rare.
            (2, Item.LeremieuSalad),
            (2, Item.AdamantoiseShell),
            (2, Item.PieceOfAngelSkin),
            (2, Item.DragonHeart),
            (2, Item.ServingOfMarbledSteak),
            (2, Item.PlateOfSeaSprayRisotto),
            (2, Item.LockOfSirensHair),
            (2, Item.PlateOfWitchRisotto),
            (2, Item.BowlOfWitchStew),

            // Rarest.
            (1, Item.AssaultBreastplate),
        };

        // Set quest in starting state.
        static void StartQuest(IQuestContext q)
        {
            q.Player.GiveKeyItem(KeyItem.MonarchLinnPatrolPermit);
            q.Quest.SetStep(GoToMonarchLinn);
            q.Player.SetCharVar(QuestRewardVar, 0);
            q.Quest.Begin();
        }

        // A player variable is used to prevent the holding of Rare/Ex item to force a recalculation.
        static int GenerateReward(IQuestContext q)
        {
            int totalChance = 0;
            int rewardId = 0;

            foreach (var item in Rewards)
            {
                totalChance = totalChance + item.DropWeight;
            }

            int roll = q.Player.RandomInt(1, totalChance);

            foreach (var item in Rewards)
            {
                totalChance = totalChance - item.DropWeight;

                if (roll > totalChance)
                {
                    rewardId = item.RewardId;
                    break;
                }
            }

            q.Player.SetCharVar(QuestRewardVar, rewardId);

            return rewardId;
        }

        // Give item (or gil) reward to player.
        static void GiveReward(IQuestContext q)
        {
            int rewardId = q.Player.CharVar(QuestRewardVar);

            if (rewardId == 0)
            {
                rewardId = GenerateReward(q);
            }

            // Retail does not complete the quest when the reward cannot be handed over.
            // The player keeps the rolled reward and reports again after making room.
            if (!q.Player.GiveItem(rewardId))
            {
                return;
            }

            // The first clear pays 10,000 gil on top of the item per JP wiki.
            if (!q.Player.HasCompletedQuest(QuestLog.OtherAreas, "UNINVITED_GUESTS"))
            {
                q.Player.GiveCurrency("gil", 10000);
            }

            if (q.Quest.Complete())
            {
                q.Quest.SetVar(ConquestWait, q.Player.NextConquestTally);
                q.Player.SetCharVar(QuestRewardVar, 0);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Title = Title.MonarchLinnPatrolGuard,
            });
            b.Steps(GoToMonarchLinn, ReturningInVictory, ReturningInDefeat, WaitingInDefeat);

            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.TheSavage))
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Justinius).OnTrigger(QuestReply.ProgressEvent(570))
                .OnEventFinish(Zone.TavnazianSafehold, 570, q =>
                {
                    if (q.Option == 1)
                    {
                        StartQuest(q);
                    }
                });

            b.AnyState(when: q => q.Quest.Status == QuestStatus.Accepted || q.Quest.Status == QuestStatus.Completed)
                // Battlefield Win
                .OnEventFinish(Zone.MonarchLinn, 32001, q =>
                {
                    q.Quest.SetStep(ReturningInVictory);
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Justinius).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;
                    bool conquestWaitFinished = q.Quest.Var(ConquestWait) < q.Player.NextConquestTally;
                    int rewardId = q.Player.CharVar(QuestRewardVar);

                    // Player tried the battle and either lost or warped out.
                    if (questProgress == 1 &&
                        !q.Player.HasKeyItem(KeyItem.MonarchLinnPatrolPermit))
                    {
                        questProgress = ReturningInDefeat;
                        q.Quest.SetStep(questProgress);
                    }

                    // Go to Monarch Linn.
                    if (questProgress == GoToMonarchLinn)
                    {
                        return QuestReply.Event(571);
                    }
                    // Victory. Give reward.
                    else if (questProgress == ReturningInVictory ||
                             rewardId > 0)
                    {
                        return QuestReply.ProgressEvent(572);
                    }
                    // Reissue key item after victory. Player has waited for conquest tally.
                    else if (questProgress == WaitingInVictory &&
                             conquestWaitFinished)
                    {
                        return QuestReply.ProgressEvent(573);
                    }
                    // Reissue key item after failure. Player has waited for conquest tally.
                    else if (questProgress == WaitingInDefeat &&
                             conquestWaitFinished)
                    {
                        return QuestReply.ProgressEvent(574);
                    }
                    // Failure. Player must wait until next conquest tally.
                    else if (questProgress == ReturningInDefeat ||
                             questProgress == WaitingInDefeat)
                    {
                        return QuestReply.ProgressEvent(575);
                    }
                    return null;
                })
                // Victory; Wait gets set with Reward.
                .OnEventFinish(Zone.TavnazianSafehold, 572, q =>
                {
                    GiveReward(q);
                })
                // Repeat quest
                .OnEventFinish(Zone.TavnazianSafehold, 573, q =>
                {
                    if (q.Option == 1)
                    {
                        StartQuest(q);
                    }
                })
                // Repeat quest after failure (and waiting until next conquest).
                .OnEventFinish(Zone.TavnazianSafehold, 574, q =>
                {
                    StartQuest(q);
                })
                // Player has failed and must wait until conquest to retry.
                .OnEventFinish(Zone.TavnazianSafehold, 575, q =>
                {
                    if (q.Quest.Step == ReturningInDefeat)
                    {
                        q.Quest.SetVar(ConquestWait, q.Player.NextConquestTally);
                        q.Quest.SetStep(WaitingInDefeat);
                    }
                });
        }
    }
}
