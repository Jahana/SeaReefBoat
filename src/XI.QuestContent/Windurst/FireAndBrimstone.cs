// Fire and Brimstone (Log ID 2, Quest ID 73; RNG AF2), ported from the Fire and Brimstone branches of
// scripts/zones/Windurst_Woods/npcs/Perih_Vashai.lua (quest rework B3b, Windurst group 29; call-only). Perih Vashai :
// !pos 117 -3 92 241.
// Perih Vashai: Sin Hunting completed, main job RNG, main level >= AF2_QUEST_LEVEL and this quest available: 531 (its
// finish, any option: begun, fireAndBrimstone 1); Sin Hunting completed and this quest available: 528;
// fireAndBrimstone 1-3: 532; 4: 535 (0, 13360, Old Earring) (its finish: the var 5); 5: 536 (the same).
// The trade, the var 5: an Old Earring (npcUtil.tradeHas, not exact): 537 (0, 13360); its finish:
// npcUtil.completeQuest's steps (Hunter's Beret, stopping when it cannot be given; Windurst fame 20; the var cleared;
// the raw player:completeQuest, so the quest's Quest[2][73] vars stay); given: the trade confirmed (the earring
// only). Unbridled Passion's arms come after these (its own C#, which the script calls when this one gives no reply).
// 13360 is the Lua's literal (a Twinstone Earring; 537's comment says "old earring").
// The steps between (Koh Lenbalalako's 1 -> 2, The Eldieme Necropolis zone-in 2 -> 3 and its Gravestone,
// Castle Oztroja's Scavenge (job_utils/ranger.lua) for the Old Earring) read and write only the var, never the quest:
// they stay in Lua. Sin Hunting, The Fanged One, From Saplings Grow and Trust: Shikaree Z (their containers) and
// CoP 5-3 (Louverance's path) answer him first with their Progress arms; their Event arms take turns with this script.
// Kept as the Lua has it: the var arms and the trade read no status (a GM's var on any char plays them, and 537
// completes an available quest); 531 begins on any option; 535's and 537's finishes read no status; 537 refused (no
// room, or the rare beret held on a completed char's replay) keeps the earring and the var 5.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class FireAndBrimstone : QuestScript
    {
        const string Cs = "fireAndBrimstone";

        public FireAndBrimstone() : base(QuestLog.Windurst, "FIRE_AND_BRIMSTONE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.PerihVashai).Called()
                    // Perih_Vashai.lua onTrade
                    .OnTrade(q =>
                    {
                        if (q.Player.CharVar(Cs) == 5 && q.Trade.Has(Item.OldEarring))
                        {
                            return QuestReply.Event(537, 0, Item.TwinstoneEarring); // old earring
                        }

                        return null;
                    })
                    // Perih_Vashai.lua onTrigger, the FIRE AND BRIMSTONE arms
                    .OnTrigger(q =>
                    {
                        QuestStatus sinHunting = q.Player.QuestStatus(QuestLog.Windurst, "SIN_HUNTING"); // RNG AF1
                        QuestStatus fireAndBrimstone = q.Quest.Status; // RNG AF2
                        int fireAndBrimstoneCS = q.Player.CharVar(Cs);
                        int lvl = q.Player.MainLevel;
                        int job = q.Player.MainJob;

                        if (sinHunting == QuestStatus.Completed &&
                            job == Job.Rng &&
                            lvl >= q.Settings.MainNumber("AF2_QUEST_LEVEL") &&
                            fireAndBrimstone == QuestStatus.Available)
                        {
                            return QuestReply.Event(531); // start RNG AF2
                        }
                        else if (sinHunting == QuestStatus.Completed &&
                            fireAndBrimstone == QuestStatus.Available)
                        {
                            return QuestReply.Event(528); // after RNG AF1, before starting RNG AF2
                        }
                        else if (fireAndBrimstoneCS > 0 && fireAndBrimstoneCS < 4)
                        {
                            return QuestReply.Event(532); // during RNG AF2
                        }
                        else if (fireAndBrimstoneCS == 4)
                        {
                            // second part RNG AF2
                            return QuestReply.Event(535, 0, Item.TwinstoneEarring, Item.OldEarring);
                        }
                        else if (fireAndBrimstoneCS == 5)
                        {
                            // during second part RNG AF2
                            return QuestReply.Event(536, 0, Item.TwinstoneEarring, Item.OldEarring);
                        }

                        return null; // Unbridled Passion's arms next
                    });

            // Perih_Vashai.lua onEventFinish, the FIRE AND BRIMSTONE arms
            b.AnyState()
                .OnEventFinish(Zone.WindurstWoods, 531, q =>
                {
                    q.Quest.Begin(); // start RNG AF2
                    q.Player.SetCharVar(Cs, 1);
                })
                .OnEventFinish(Zone.WindurstWoods, 535, q =>
                {
                    q.Player.SetCharVar(Cs, 5); // start second part RNG AF2
                })
                .OnEventFinish(Zone.WindurstWoods, 537, q =>
                {
                    // npcUtil.completeQuest with Hunter's Beret (12518), fame 20, the fameArea WINDURST and
                    // the var fireAndBrimstone, step for step: not quest:complete, whose cleanup would clear the
                    // quest's own Quest[2][73] vars. It returns false, with nothing more done, when the beret cannot be
                    // given
                    if (!q.Player.GiveItem(Item.HuntersBeret))
                    {
                        return;
                    }
                    q.Player.AddFame(FameArea.Windurst, 20);
                    q.Player.SetCharVar(Cs, 0);
                    q.Player.CompleteQuest(QuestLog.Windurst, "FIRE_AND_BRIMSTONE");
                    q.Trade.Confirm(); // complete quest RNG AF2
                });
        }
    }
}
