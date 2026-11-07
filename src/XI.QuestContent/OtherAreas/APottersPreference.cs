// A Potter's Preference (Log ID 4, Quest ID 9), ported from scripts/zones/Mhaura/npcs/Nereus.lua (quest rework B3b,
// Other Areas group 10; call-only: every branch of his script is this quest's, so his script only calls it; he has no
// DefaultActions entry). Nereus : !pos 34.555 -16.071 83.334 249.
// Trigger: available and Windurst fame level >= 6: 111 (Dish of Gusgen Clay); accepted: 114 (the dish); completed:
// the day QuestAPotterPrefeCompDay_var + 8 reached (VanadielUniqueDay): 112 (the repeat), else 115; else (available,
// low fame) 110. 111 option 1: begun. 112's finish: QuestAPotterPrefeRepeat_var 1.
// Trade, accepted or the repeat var 1, a Dish of Gusgen Clay in it (npcUtil.tradeHas): 113. 113's finish: the dish
// confirmed and taken, Windurst fame +60, 2160 gil (npcUtil.giveCurrency), the repeat var 0, the day var today,
// completed.
// Kept as the Lua has it: completed with the repeat var 1 still plays 112 / 115 (no reminder), and 112 sets the var
// again; 113's finish reads no status; a trade with more than the dish takes only the dish.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class APottersPreference : QuestScript
    {
        const string RepeatVar = "QuestAPotterPrefeRepeat_var";
        const string CompDayVar = "QuestAPotterPrefeCompDay_var";

        public APottersPreference() : base(QuestLog.OtherAreas, "A_POTTERS_PREFERENCE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.Mhaura, Npc.Mhaura.Nereus).Called()
                    // Nereus.lua onTrade
                    .OnTrade(q =>
                    {
                        if (q.Quest.Status == QuestStatus.Accepted ||
                            q.Player.CharVar(RepeatVar) == 1)
                        {
                            if (q.Trade.Has(Item.DishOfGusgenClay))
                            {
                                return QuestReply.Event(113); // quest done!
                            }
                        }

                        return null;
                    })
                    // Nereus.lua onTrigger
                    .OnTrigger(q =>
                    {
                        QuestStatus status = q.Quest.Status;

                        if (status == QuestStatus.Available &&
                            q.Player.FameLevel(FameArea.Windurst) >= 6)
                        {
                            return QuestReply.Event(111, Item.DishOfGusgenClay); // start quest A Potter's Preference
                        }
                        else if (status == QuestStatus.Accepted)
                        {
                            // get me dish_of_gusgen_clay as soon as you can
                            return QuestReply.Event(114, Item.DishOfGusgenClay);
                        }
                        else if (status == QuestStatus.Completed)
                        {
                            if (q.Player.CharVar(CompDayVar) + 8 <= q.Player.VanadielUniqueDay)
                            {
                                // eight days after completion, allow to do the quest again
                                return QuestReply.Event(112); // repeat quest
                            }
                            else
                            {
                                return QuestReply.Event(115); // i have enough for now, come later
                            }
                        }
                        else
                        {
                            return QuestReply.Event(110); // standard dialog
                        }
                    });

            b.AnyState()
                // Nereus.lua onEventFinish 111 / 113 / 112
                .OnEventFinish(Zone.Mhaura, 111, q =>
                {
                    if (q.Option == 1) // accept quest
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.Mhaura, 113, q =>
                {
                    // quest completed
                    q.Trade.Confirm();
                    q.Player.AddFame(FameArea.Windurst, 60);
                    q.Player.GiveCurrency("gil", 2160);
                    q.Player.SetCharVar(RepeatVar, 0);
                    q.Player.SetCharVar(CompDayVar, q.Player.VanadielUniqueDay);
                    // the raw player:completeQuest, which keeps the quest's own Quest[4][9] vars
                    q.Player.CompleteQuest(QuestLog.OtherAreas, "A_POTTERS_PREFERENCE");
                })
                .OnEventFinish(Zone.Mhaura, 112, q =>
                {
                    // repeat quest
                    q.Player.SetCharVar(RepeatVar, 1);
                });
        }
    }
}
