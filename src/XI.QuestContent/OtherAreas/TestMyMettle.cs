// Test My Mettle (Log ID 4, Quest ID 25), a line-for-line port of scripts/quests/otherAreas/Test_My_Mettle.lua (quest
// rework B3b, Other Areas group 3). Selbina: Devean !pos 39.86 -14.56 40 248 (no script; default 124). Davoi: Jar (no script; default
// NOTHING_OUT_OF_ORDINARY), moved by Davoi's Zone.lua onGameHour and by the trigger here (OtherAreasHelpers.TestMyMettle).
// Repeatable. Not accepted, main level >= 10, rank >= 2 in the char's nation, Selbina / Rabao fame level >= 2 and Repeat <=
// GetSystemTime(): Devean's 120 (0, the Power Sandals, 0, 0, 0, the Flint Stone). Its finish: option 0 or the cancel bit
// (bit.rshift(option, 30) == 1): nothing; else the option is the bet plus the Vana'diel hours chosen (the first bet of
// 1000, 500, 250, 100 the option exceeds): with that much gil: the gil taken, Timer = now + hours * 144, Reward = bet *
// the multiplier (hours > 72: 1; <= 24: 3.0, <= 36: 2.5, <= 48: 2.0, <= 60: 1.5, <= 72: 1.2), begun; without:
// DONT_HAVE_ENOUGH_GIL. Accepted: the Jar, without the sandals: npcUtil.giveItem the sandals, the jar moved when given, and
// noAction either way; with them: nil (the Jar's default). Devean's trigger while the timer runs: 121 (the sandals, the
// hours left, floored); after: the sandals held and Option 0: 125 (Option 1), else 123. A trade of the sandals alone: 122
// while the timer runs, else 123. 122's finish: completed (no reward), the trade confirmed, the Reward gil, fame +5 San
// d'Oria and Bastok, Repeat = JstMidnight(). 123's finish: the trade confirmed, player:delCurrentQuest (the quest off the
// current list, completed if it ever was), Repeat = JstMidnight().
// Kept as the Lua has it: an option of 1 to 100 matches no bet: getSelectedOption returns nil and the Lua errors on the gil
// compare (nothing happens); 123 keeps Timer, Reward and Option (delCurrentQuest clears no var, quest:complete would), so
// a later run's Option is already 1; the trigger's 123 confirms no trade.
// Lua broken on Lua (kept): "repeatable" never repeats. Once completed, the quest's completed bit stays set, and
// getQuestStatus answers completed while it is (the engine's order, as LSB's C++), so 120's quest:begin (addQuest) sets
// the current bit but the status stays completed: state 1 holds again (Devean offers 120 and takes the bet each time)
// and state 2 never does (no 121 / 122 / 123 / 125, the Jar gives nothing). The traces show it (main flow, `#timer`).
// An option of 2^31 or more (a client that sends no menu value) is not modelled: the C# int reads it negative (no bet).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class TestMyMettle : QuestScript
    {
        public TestMyMettle() : base(QuestLog.OtherAreas, "TEST_MY_METTLE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Test_My_Mettle";

        static readonly int[] BetAmounts =
        {
            1000,
            500,
            250,
            100,
        };

        static readonly (int Hours, double Multiplier)[] RewardMultiplier =
        {
            (24, 3.0),
            (36, 2.5),
            (48, 2.0),
            (60, 1.5),
            (72, 1.2),
        };

        // Event option return is the sum of bet amount and timeframe chosen. null: the Lua's nil (no bet below the option)
        static (int BetAmount, int VanaHoursRemaining)? GetSelectedOption(int option)
        {
            for (int selectedBet = 1; selectedBet <= 4; selectedBet++)
            {
                if (option > BetAmounts[selectedBet - 1])
                {
                    return (BetAmounts[selectedBet - 1], option - BetAmounts[selectedBet - 1]);
                }
            }
            return null;
        }

        static double GetRewardMultiplier(int vanaHoursRemaining)
        {
            if (vanaHoursRemaining > 72)
            {
                return 1;
            }

            for (int i = 1; i <= RewardMultiplier.Length; i++)
            {
                if (vanaHoursRemaining <= RewardMultiplier[i - 1].Hours)
                {
                    return RewardMultiplier[i - 1].Multiplier;
                }
            }
            return 0; // not reached: hours <= 72 matches the last row (the Lua returns nil)
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q => q.Quest.Status != QuestStatus.Accepted &&
                                  q.Player.MainLevel >= 10 &&
                                  q.Player.Rank(q.Player.Nation) >= 2 &&
                                  q.Player.FameLevel(FameArea.SelbinaRabao) >= 2 &&
                                  q.Quest.Var("Repeat") <= q.Player.SystemTime)
                .Npc(Zone.Selbina, Npc.Selbina.Devean).OnTrigger(QuestReply.ProgressEvent(120, 0, Item.PowerSandals, 0, 0, 0, Item.FlintStone))
                .OnEventFinish(Zone.Selbina, 120, q =>
                {
                    if (q.Option == 0)
                    {
                        return;
                    }

                    bool eventCancelled = ((uint)q.Option >> 30) == 1;
                    if (eventCancelled)
                    {
                        return;
                    }

                    var selected = GetSelectedOption(q.Option);
                    if (selected == null)
                    {
                        return; // the Lua errors here: player:getGil() >= nil
                    }
                    var (betAmount, vanaHoursRemaining) = selected.Value;

                    if (q.Player.Gil >= betAmount)
                    {
                        q.Player.DelGil(betAmount);

                        // One Vana'diel Hour is equal to 2m24s (144s)
                        q.Quest.SetVar("Timer", q.Player.SystemTime + vanaHoursRemaining * 144);
                        q.Quest.SetVar("Reward", (int)(betAmount * GetRewardMultiplier(vanaHoursRemaining)));
                        q.Quest.Begin();
                    }
                    else
                    {
                        q.Text.Special(Text.Selbina.DONT_HAVE_ENOUGH_GIL);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Selbina, Npc.Selbina.Devean)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.PowerSandals))
                        {
                            int timeRemaining = q.Quest.Var("Timer") - q.Player.SystemTime;

                            if (timeRemaining > 0)
                            {
                                return QuestReply.ProgressEvent(122);
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(123);
                            }
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int timeRemaining = q.Quest.Var("Timer") - q.Player.SystemTime;

                        if (timeRemaining > 0)
                        {
                            int hoursRemaining = timeRemaining / 144;

                            return QuestReply.Event(121, Item.PowerSandals, hoursRemaining);
                        }
                        else
                        {
                            if (q.Player.HasItem(Item.PowerSandals) &&
                                q.Quest.Var("Option") == 0)
                            {
                                return QuestReply.ProgressEvent(125);
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(123);
                            }
                        }
                    })
                .OnEventFinish(Zone.Selbina, 122, q =>
                {
                    int rewardedGil = q.Quest.Var("Reward");

                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        q.Player.GiveCurrency("gil", rewardedGil);
                        q.Player.AddFame(FameArea.Sandoria, 5);
                        q.Player.AddFame(FameArea.Bastok, 5);
                        q.Quest.SetVar("Repeat", q.Player.JstMidnight);
                    }
                })
                .OnEventFinish(Zone.Selbina, 123, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.DelCurrent();
                    q.Quest.SetVar("Repeat", q.Player.JstMidnight);
                })
                .OnEventFinish(Zone.Selbina, 125, q =>
                {
                    q.Quest.SetVar("Option", 1);
                })
                .Npc(Zone.Davoi, Npc.Davoi.Jar).OnTrigger(q =>
                {
                    // TODO: Find the Jar's default action

                    if (!q.Player.HasItem(Item.PowerSandals))
                    {
                        if (q.Player.GiveItem(Item.PowerSandals))
                        {
                            OtherAreasHelpers.TestMyMettle.MoveJar(q);
                        }

                        return QuestReply.NoAction();
                    }
                    return null;
                });
        }
    }
}
