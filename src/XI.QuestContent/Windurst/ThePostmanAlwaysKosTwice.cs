// The Postman Always KO's Twice (Log ID 2, Quest ID 19), ported from the quest branches of
// scripts/zones/Windurst_Walls/npcs/Ambrosius.lua, Orudoba-Sondeba.lua and Tsuaora-Tsuora.lua (quest rework B3b,
// Windurst group 23; call-only). Ambrosius : Windurst Walls; Orudoba-Sondeba : !pos 70.086 -3.503 -69.939 239;
// Tsuaora-Tsuora : !pos 71.489 -3.418 -67.809 239.
// Ambrosius: available 48 (its finish, option 0: begun); accepted 49; completed 56. The trade, not available: one
// point for each of the Torn Epistle, Muddy Bar Tab, Odd Postcard and Damp Envelope held in it (hasItemQty 1),
// answered only when getItemCount equals the points: 1, 2, 3, 4 letters: accepted 52 / 53 / 54 / 55, completed 57 /
// 58 / 59 / 60, with (50 / 150 / 250 / 500 x GIL_RATE[, n]). Their finishes: the trade taken, that gil
// (player:addGil, no message), Windurst fame 10; 52-55 also complete the quest. Orudoba-Sondeba and Tsuaora-Tsuora,
// accepted: 51 / 50; else their 43 / 42 (kept in their scripts).
// Kept as the Lua has it: the finishes read no status; a letter traded twice scores no point (hasItemQty is exact),
// and then fails the count; 48 begins on option 0 only.
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class ThePostmanAlwaysKosTwice : QuestScript
    {
        public ThePostmanAlwaysKosTwice() : base(QuestLog.Windurst, "THE_POSTMAN_ALWAYS_KOS_TWICE") { }

        // xi.settings.main.GIL_RATE * amount, read by the engine's integral getter (llround)
        static int Pay(IQuestContext q, int amount) =>
            (int)Math.Round(q.Settings.MainNumber("GIL_RATE") * amount, MidpointRounding.AwayFromZero);

        static void Reward(IQuestContext q, int amount, bool complete)
        {
            q.Trade.Complete();
            q.Player.AddGil(Pay(q, amount));
            q.Player.AddFame(FameArea.Windurst, 10);

            if (complete)
            {
                // the raw player:completeQuest: not quest:complete, whose cleanup would clear the quest's own
                // Quest[2][19] vars
                q.Player.CompleteQuest(QuestLog.Windurst, "THE_POSTMAN_ALWAYS_KOS_TWICE");
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.Ambrosius).Called()
                    // Ambrosius.lua onTrigger
                    .OnTrigger(q =>
                    {
                        QuestStatus postman = q.Quest.Status;

                        if (postman == QuestStatus.Available)
                        {
                            return QuestReply.Event(48);
                        }
                        else if (postman == QuestStatus.Accepted)
                        {
                            return QuestReply.Event(49);
                        }
                        else if (postman == QuestStatus.Completed)
                        {
                            return QuestReply.Event(56);
                        }

                        return null;
                    })
                    // Ambrosius.lua onTrade
                    .OnTrade(q =>
                    {
                        QuestStatus postman = q.Quest.Status;

                        if (postman != QuestStatus.Available)
                        {
                            int reward = 0;

                            if (q.Trade.HasItemQty(Item.TornEpistle, 1))
                            {
                                reward = reward + 1;
                            }

                            if (q.Trade.HasItemQty(Item.MuddyBarTab, 1))
                            {
                                reward = reward + 1;
                            }

                            if (q.Trade.HasItemQty(Item.OddPostcard, 1))
                            {
                                reward = reward + 1;
                            }

                            if (q.Trade.HasItemQty(Item.DampEnvelope, 1))
                            {
                                reward = reward + 1;
                            }

                            if (q.Trade.ItemCount == reward)
                            {
                                bool accepted = postman == QuestStatus.Accepted;

                                if (reward == 1)
                                {
                                    return QuestReply.Event(accepted ? 52 : 57, Pay(q, 50));
                                }
                                else if (reward == 2)
                                {
                                    return QuestReply.Event(accepted ? 53 : 58, Pay(q, 150), 2);
                                }
                                else if (reward == 3)
                                {
                                    return QuestReply.Event(accepted ? 54 : 59, Pay(q, 250), 3);
                                }
                                else if (reward == 4)
                                {
                                    return QuestReply.Event(accepted ? 55 : 60, Pay(q, 500), 4);
                                }
                            }
                        }

                        return null;
                    })
                // Orudoba-Sondeba.lua onTrigger (his 43 stays in his script)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.OrudobaSondeba).Called().OnTrigger(q =>
                    q.Quest.Status == QuestStatus.Accepted ? QuestReply.Event(51) : null)
                // Tsuaora-Tsuora.lua onTrigger (her 42 stays in her script)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.TsuaoraTsuora).Called().OnTrigger(q =>
                    q.Quest.Status == QuestStatus.Accepted ? QuestReply.Event(50) : null);

            // Ambrosius.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.WindurstWalls, 48, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.WindurstWalls, 52, q => Reward(q, 50, true))
                .OnEventFinish(Zone.WindurstWalls, 53, q => Reward(q, 150, true))
                .OnEventFinish(Zone.WindurstWalls, 54, q => Reward(q, 250, true))
                .OnEventFinish(Zone.WindurstWalls, 55, q => Reward(q, 500, true))
                .OnEventFinish(Zone.WindurstWalls, 57, q => Reward(q, 50, false))
                .OnEventFinish(Zone.WindurstWalls, 58, q => Reward(q, 150, false))
                .OnEventFinish(Zone.WindurstWalls, 59, q => Reward(q, 250, false))
                .OnEventFinish(Zone.WindurstWalls, 60, q => Reward(q, 500, false));
        }
    }
}
