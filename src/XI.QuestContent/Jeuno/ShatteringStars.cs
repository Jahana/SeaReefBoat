// Shattering Stars (Log ID 3, Quest ID 132), a line-for-line port of scripts/quests/jeuno/LB05_1_Shattering_Stars.lua
// (quest rework B3b). Maat : !pos 8 3 118 243. A char of one of the fifteen original jobs at level 66 or more with level
// cap 70 (MAX_LEVEL 75 or more): Maat offers 92 with the job (begun). Accepted (same job and level): the job's
// testimony while Prog is 0 plays 64 with the job (tradedTestimony 1; option 1 sets the char down at the job's Maat
// battlefield); Maat 93 once Prog is set (completed, Star Breaker, level cap 75, YOUR_LEVEL_LIMIT_IS_NOW_75), else 91
// (testimony never traded) or 63, with the job. Accepted or completed: the battlefield win's 32001 in the five Maat
// arenas, when battlefieldWin is one of that arena's Maat fights: while accepted, a Scroll of Instant Warp and Prog = the
// job; the job's maatsCap bit; Maat Masher.
// Kept Lua quirks: 92's finish begins the quest whatever the option; the testimony trade is read with getItemQty (other
// items beside it still play 64) and never taken by the quest (the battlefield wears it); 64's option 1 sets the char
// down at the arena, any other option leaves it in place.
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class ShatteringStars : QuestScript
    {
        public ShatteringStars() : base(QuestLog.Jeuno, "SHATTERING_STARS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/LB05_1_Shattering_Stars";

        static readonly Dictionary<ushort, int[]> MaatBattlefieldIds = new Dictionary<ushort, int[]>
        {
            [Zone.BalgasDais] = new[] { 101, 102, 103 },
            [Zone.ChamberOfOracles] = new[] { 194, 195, 196 },
            [Zone.HorlaisPeak] = new[] { 5, 6, 7 },
            [Zone.QubiaArena] = new[] { 517, 518, 519 },
            [Zone.WaughroonShrine] = new[] { 70, 71, 72 },
        };

        // maatBattlefieldZone.onEventFinish[32001]
        static void OnBattlefieldWin(IQuestContext q)
        {
            int battlefieldWin = q.Player.LocalVar("battlefieldWin");

            foreach (int battlefieldId in MaatBattlefieldIds[q.Zone])
            {
                if (battlefieldWin == battlefieldId)
                {
                    int jobId = q.Player.MainJob;
                    int maatsCapMask = q.Player.CharVar("maatsCap");

                    if (q.Player.QuestStatus(QuestLog.Jeuno, "SHATTERING_STARS") == QuestStatus.Accepted)
                    {
                        q.Player.GiveItem(Item.ScrollOfInstantWarp);
                        q.Quest.SetStep(jobId);
                    }

                    // utils.mask.getBit / setBit
                    if ((maatsCapMask & (1 << (jobId - 1))) == 0)
                    {
                        q.Player.SetCharVar("maatsCap", maatsCapMask | (1 << (jobId - 1)));
                    }

                    q.Player.AddTitle(Title.MaatMasher);
                }
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Title = Title.StarBreaker });
            b.Steps(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15);

            // Section: Quest available.
            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainJob <= 15 && // Only the "old" jobs may start this quest.
                    q.Player.MainLevel >= 66 &&
                    q.Player.LevelCap == 70 &&
                    q.Settings.MainNumber("MAX_LEVEL") >= 75)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Maat).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(92, q.Player.MainJob);
                })
                .OnEventFinish(Zone.RuludeGardens, 92, q =>
                {
                    q.Quest.Begin();
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted, when: q =>
                    q.Player.MainJob <= 15 &&
                    q.Player.MainLevel >= 66)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Maat).OnTrade(q =>
                {
                    int playerJob = q.Player.MainJob;
                    int properTestimony = Item.WarriorsTestimony + playerJob - 1;

                    if (q.Trade.ItemQty(properTestimony) > 0 &&
                        q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(64, playerJob);
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    int playerJob = q.Player.MainJob;

                    if (q.Quest.Step >= 1)
                    {
                        return QuestReply.ProgressEvent(93); // Complete quest.
                    }
                    else if (q.Quest.Var("tradedTestimony") == 0)
                    {
                        return QuestReply.Event(91, playerJob); // Testimony never traded.
                    }
                    else
                    {
                        return QuestReply.Event(63, playerJob); // Testimony traded at least once.
                    }
                })
                .OnEventFinish(Zone.RuludeGardens, 64, q =>
                {
                    q.Quest.SetVar("tradedTestimony", 1);

                    // Accept teleport.
                    if (q.Option == 1)
                    {
                        int mJob = q.Player.MainJob;
                        if (mJob == Job.Mnk || mJob == Job.Whm || mJob == Job.Smn)
                        {
                            q.Player.SetPos(299.316, -123.591, 353.760, 66, 146);
                        }
                        else if (mJob == Job.War || mJob == Job.Blm || mJob == Job.Rng)
                        {
                            q.Player.SetPos(-511.459, 159.004, -210.543, 10, 139);
                        }
                        else if (mJob == Job.Pld || mJob == Job.Drk || mJob == Job.Brd)
                        {
                            q.Player.SetPos(-225.146, -24.250, 20.057, 255, 206);
                        }
                        else if (mJob == Job.Rdm || mJob == Job.Thf || mJob == Job.Bst)
                        {
                            q.Player.SetPos(-349.899, 104.213, -260.150, 0, 144);
                        }
                        else if (mJob == Job.Sam || mJob == Job.Nin || mJob == Job.Drg)
                        {
                            q.Player.SetPos(-220.084, -0.645, 4.442, 191, 168);
                        }
                    }
                })
                .OnEventFinish(Zone.RuludeGardens, 93, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetLevelCap(75);
                        q.Text.Special(Text.RuludeGardens.YOUR_LEVEL_LIMIT_IS_NOW_75);
                    }
                });

            // status >= QUEST_ACCEPTED: accepted or completed
            b.State(QuestStatus.Accepted, when: q =>
                    q.Player.MainJob <= 15 &&
                    q.Player.MainLevel >= 66)
                .Or(QuestStatus.Completed, when: q =>
                    q.Player.MainJob <= 15 &&
                    q.Player.MainLevel >= 66)
                .OnEventFinish(Zone.BalgasDais, 32001, OnBattlefieldWin)
                .OnEventFinish(Zone.ChamberOfOracles, 32001, OnBattlefieldWin)
                .OnEventFinish(Zone.HorlaisPeak, 32001, OnBattlefieldWin)
                .OnEventFinish(Zone.QubiaArena, 32001, OnBattlefieldWin)
                .OnEventFinish(Zone.WaughroonShrine, 32001, OnBattlefieldWin);
        }
    }
}
