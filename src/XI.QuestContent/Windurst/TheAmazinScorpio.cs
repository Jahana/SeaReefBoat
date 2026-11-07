// The Amazin' Scorpio (Log ID 2, Quest ID 61), ported from the quest branches of
// scripts/zones/Windurst_Woods/npcs/Soni-Muni.lua and Gottah_Maporushanoh.lua (quest rework B3b, Windurst group 28;
// call-only). Soni-Muni : !pos -17.073 1.749 -59.327 241 (she patrols a short path); Gottah Maporushanoh : Windurst
// Woods. Soni-Muni: completed 485; accepted 482 (0, 0, Scorpion Stinger); available with Windurst fame level 2 or more:
// 481 (the same) (its finish, any option: begun); else her 421 (kept in her script). The trade, accepted: a Scorpion
// Stinger (npcUtil.tradeHas): 484; its finish: npcUtil.completeQuest's steps (Windurst fame 20, 1500 x GIL_RATE gil
// with GIL_OBTAINED, Great Grappler Scorpio, the raw player:completeQuest, so the quest's Quest[2][61] vars stay); then
// (always: no item) the trade confirmed. Gottah Maporushanoh: completed 486; accepted 483;
// else his 420. Lure of the Wildcat (Windurst) answers Soni-Muni first while it is accepted (its own container), and
// Lost Chick answers Gottah's events' updates; neither is this quest's.
// Kept as the Lua has it: 481 begins on any option; 484's finish reads no status.
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class TheAmazinScorpio : QuestScript
    {
        public TheAmazinScorpio() : base(QuestLog.Windurst, "THE_AMAZIN_SCORPIO") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.SoniMuni).Called()
                    // Soni-Muni.lua onTrade
                    .OnTrade(q =>
                    {
                        if (q.Quest.Status == QuestStatus.Accepted && q.Trade.Has(Item.ScorpionStinger))
                        {
                            return QuestReply.Event(484);
                        }

                        return null;
                    })
                    // Soni-Muni.lua onTrigger (her 421 stays in her script)
                    .OnTrigger(q =>
                    {
                        QuestStatus amazinScorpio = q.Quest.Status;

                        if (amazinScorpio == QuestStatus.Completed)
                        {
                            return QuestReply.Event(485);
                        }
                        else if (amazinScorpio == QuestStatus.Accepted)
                        {
                            return QuestReply.Event(482, 0, 0, Item.ScorpionStinger);
                        }
                        else if (amazinScorpio == QuestStatus.Available &&
                            q.Player.FameLevel(FameArea.Windurst) >= 2)
                        {
                            return QuestReply.Event(481, 0, 0, Item.ScorpionStinger);
                        }

                        return null;
                    })
                // Gottah_Maporushanoh.lua onTrigger (his 420 stays in his script)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.GottahMaporushanoh).Called().OnTrigger(q =>
                {
                    QuestStatus amazinScorpio = q.Quest.Status;

                    if (amazinScorpio == QuestStatus.Completed)
                    {
                        return QuestReply.Event(486);
                    }
                    else if (amazinScorpio == QuestStatus.Accepted)
                    {
                        return QuestReply.Event(483);
                    }

                    return null;
                });

            // Soni-Muni.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.WindurstWoods, 481, q =>
                {
                    q.Quest.Begin();
                })
                .OnEventFinish(Zone.WindurstWoods, 484, q =>
                {
                    // npcUtil.completeQuest with fame 20, the fameArea WINDURST, the title GREAT_GRAPPLER_SCORPIO and
                    // gil 1500, step for step: not quest:complete, whose cleanup would clear the quest's own
                    // Quest[2][61] vars. No item, so it always returns true
                    q.Player.AddFame(FameArea.Windurst, 20);
                    int gil = RateAmount(1500, q.Settings.MainNumber("GIL_RATE"));
                    q.Player.AddGil(gil);
                    q.Text.Special(Text.WindurstWoods.GIL_OBTAINED, gil);
                    q.Player.AddTitle(Title.GreatGrapplerScorpio);
                    q.Player.CompleteQuest(QuestLog.Windurst, "THE_AMAZIN_SCORPIO");
                    q.Trade.Confirm();
                });
        }

        // n * rate as the engine's binding reads a Lua number into an integer argument (llround: half away from zero)
        private static int RateAmount(int n, double rate) => (int)Math.Round(n * rate, MidpointRounding.AwayFromZero);
    }
}
