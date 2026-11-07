// Prelude to Puissance (Log ID 3, Quest ID 170), a line-for-line port of
// scripts/quests/jeuno/LB09_2_Prelude_to_Puissance.lua (quest rework B3b). Nomad Moogle : !pos 10.012 1.453 121.883 243.
// With Dormant Powers Dislodged completed and level cap 95 the moogle plays 10045 (0, 0), or with the Limit Breaker and
// MAX_LEVEL over 95: 10194 (6, 2) above level 90 (its finish begins it), 10045 (4, 2) from 75. Accepted: a Seasoning
// Stone while tradeCompleted is 0 sets it, confirms the trade and plays 10045 (0, 1, 5, 0); the moogle plays 10045 (6, 2,
// or 5, 0 once traded, then 0). The 10045 finish with option 0, 13, 14, 15, 19, 20 or 21 completes it (the Soul Gem
// Clasp; option 15 sets the local var rejectedStartLB10 first); on completion every option but 0 and 15 adds Beyond
// Infinity, and 14, 19, 20, 21 set the char down at Horlais Peak, Waughroon Shrine, Balga's Dais, Qu'Bia Arena. Kept Lua
// quirk: the completion needs no trade (option 0 at the reminder completes it without the stone).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class PreludeToPuissance : QuestScript
    {
        public PreludeToPuissance() : base(QuestLog.Jeuno, "PRELUDE_TO_PUISSANCE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/LB09_2_Prelude_to_Puissance";

        // TODO: Properly code timing minigame. Awaiting for a capture.
        // Amount of visual qeues selected at random. Min: Probably 3. Max: 7. Camera angle keeps changing qithout hints.
        // Always the same camera angle at victory.

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { KeyItem = KeyItem.SoulGemClasp });

            // Section: Quest available.
            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Jeuno, "DORMANT_POWERS_DISLODGED") &&
                    q.Player.LevelCap == 95)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.NomadMoogle).OnTrigger(q =>
                {
                    int eventId = 10045;
                    int playerLevel = q.Player.MainLevel;
                    int limitBreaker = q.Player.HasKeyItem(KeyItem.LimitBreaker) ? 1 : 2;
                    int lastQuestNumber = 0;
                    int lastQuestStage = 0;
                    if (q.Settings.MainNumber("MAX_LEVEL") > 95 &&
                        limitBreaker == 1)
                    {
                        if (playerLevel > 90)
                        {
                            eventId = 10194;
                            lastQuestNumber = 6;
                            lastQuestStage = 2;
                        }
                        else if (playerLevel >= 75)
                        {
                            lastQuestNumber = 4;
                            lastQuestStage = 2;
                        }
                    }

                    return QuestReply.ProgressEvent(eventId, playerLevel, limitBreaker, lastQuestNumber, lastQuestStage);
                })
                .OnEventFinish(Zone.RuludeGardens, 10194, q =>
                {
                    q.Quest.Begin();
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.NomadMoogle).OnTrade(q =>
                {
                    if (q.Quest.Var("tradeCompleted") == 0 &&
                        q.Trade.HasExactly(Item.SeasoningStone))
                    {
                        q.Quest.SetVar("tradeCompleted", 1);
                        q.Trade.Confirm();

                        return QuestReply.ProgressEvent(10045, 0, 1, 5, 0);
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    int playerLevel = q.Player.MainLevel;
                    int limitBreaker = q.Player.HasKeyItem(KeyItem.LimitBreaker) ? 1 : 2;
                    int lastQuestNumber = 6;
                    int lastQuestStage = 2;
                    if (q.Quest.Var("tradeCompleted") > 0)
                    {
                        lastQuestNumber = 5;
                        lastQuestStage = 0;
                    }

                    return QuestReply.ProgressEvent(10045, playerLevel, limitBreaker, lastQuestNumber, lastQuestStage, 0);
                })
                .OnEventFinish(Zone.RuludeGardens, 10045, q =>
                {
                    // All this options complete the current quest.
                    if (q.Option == 0 ||
                        q.Option == 13 ||
                        q.Option == 14 ||
                        q.Option == 15 ||
                        q.Option == 19 ||
                        q.Option == 20 ||
                        q.Option == 21)
                    {
                        // Rejected offer. Still completes quest.
                        if (q.Option == 15)
                        {
                            q.Player.SetLocalVar("rejectedStartLB10", 1);
                        }

                        // Complete quest.
                        if (q.Quest.Complete())
                        {
                            // This options immediately start next quest. (All except 0 and 15).
                            if (q.Option != 0 &&
                                q.Option != 15)
                            {
                                q.Player.AddQuest(QuestLog.Jeuno, "BEYOND_INFINITY");
                            }

                            // This options also warp you to a BCNM. Note that the quest "Beyond Infinity" is already activated in this cases.
                            if (q.Option == 14)
                            {
                                q.Player.SetPos(-511.459, 159.004, -210.543, 10, 139); // Horlais Peek
                            }
                            else if (q.Option == 19)
                            {
                                q.Player.SetPos(-349.899, 104.213, -260.150, 0, 144); // Waughrum Shrine
                            }
                            else if (q.Option == 20)
                            {
                                q.Player.SetPos(299.316, -123.591, 353.760, 66, 146); // Balga's Dais
                            }
                            else if (q.Option == 21)
                            {
                                q.Player.SetPos(-225.146, -24.250, 20.057, 255, 206); // Qu'bia Arena
                            }
                        }
                    }
                });
        }
    }
}
