// Hat in Hand (Log ID 2, Quest ID 0), ported from the Hat in Hand branches of
// scripts/zones/Windurst_Waters/npcs/Baren-Moren.lua (quest rework B3b, Windurst group 13; call-only: his script calls
// this quest, then A Feather in One's Cap, then plays his random standard dialog). Baren-Moren : !pos -66 -3 -148 238.
// Available with All At Sea's Prog var (Quest[2][23]Prog) 0: 48; option 1 begins it and gives the New Model Hat
// (npcUtil.giveKeyItem). With the hat held (any status): 52 by the show-off count (QuestHatInHand_count, which eight
// Windurst Waters NPCs' scripts raise by char var only: they stay Lua): 8 or more: 52 (80), tier 5; 6-7: (50), tier 4;
// 4-5: (30), tier 3; 2-3: (20), tier 2; else 52, tier 1 (the tier in the local var hatRewardTier). 52's finish (option 1
// or more) completes the quest through npcUtil.completeQuest with Windurst fame 10, the two show-off vars cleared and by
// tier 500 gil and the Windshear Hat / 400 gil and the hat / 300 / 150 / 300 gil; when it completes, the New Model Hat
// is taken and needToZone set.
// npcUtil.completeQuest's table differs by tier, which a QuestReward (one per quest) cannot say: the finish makes the
// same calls npcUtil.completeQuest makes, in its order (the item by npcUtil.giveItem, false: nothing more; the fame; the
// gil by GIL_RATE with GIL_OBTAINED; the vars; then the raw player:completeQuest, so the quest's Quest[2][0] vars
// stay), so no QuestReward is declared.
// Kept as the Lua has it: 52 plays whenever the hat is held (any status, before A Feather in One's Cap's arms) and its
// finish completes the quest again for a completed char given a hat; tier 1 pays 300 gil, more than tier 2's 150; 48's
// and 52's finishes read no status; a 52 finished with hatRewardTier 0 (no talk set it) completes with fame alone. The
// Lua sets hatRewardTier right after its startEvent; here it is set before the reply is sent (a local var: no packet).
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class HatInHand : QuestScript
    {
        // All At Sea's Prog var (Windurst quest 23, C#): its progress blocks the offer
        const string AllAtSeaProg = "Quest[2][23]Prog";
        // the show-off vars the eight NPCs' scripts set: a bit per NPC shown the hat, and their count
        const string ShownVar = "QuestHatInHand_var";
        const string CountVar = "QuestHatInHand_count";
        const string TierVar = "hatRewardTier";

        public HatInHand() : base(QuestLog.Windurst, "HAT_IN_HAND") { }

        static bool HasHat(IQuestCheck q) => q.Player.HasKeyItem(KeyItem.NewModelHat);

        // player:startEvent(52, ...) and player:setLocalVar('hatRewardTier', tier)
        static QuestReply ShowOff(IQuestContext q, int tier, QuestReply reply)
        {
            q.Player.SetLocalVar(TierVar, tier);
            return reply;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            // Baren-Moren.lua onTrigger: available and All At Sea's Prog 0: Quest Offered
            b.State(QuestStatus.Available, when: q => q.Player.CharVar(AllAtSeaProg) == 0)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.BarenMoren).Called().OnTrigger(QuestReply.Event(48));

            // the New Model Hat held (any status), the show-off count 8 or more: HAT + FULL REWARD
            b.AnyState(when: q => HasHat(q) && q.Player.CharVar(CountVar) >= 8)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.BarenMoren).Called()
                    .OnTrigger(q => ShowOff(q, 5, QuestReply.Event(52, 80)));

            // 6-7: HAT + GOOD REWARD
            b.AnyState(when: q => HasHat(q) && q.Player.CharVar(CountVar) >= 6)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.BarenMoren).Called()
                    .OnTrigger(q => ShowOff(q, 4, QuestReply.Event(52, 50)));

            // 4-5: PARTIAL REWARD
            b.AnyState(when: q => HasHat(q) && q.Player.CharVar(CountVar) >= 4)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.BarenMoren).Called()
                    .OnTrigger(q => ShowOff(q, 3, QuestReply.Event(52, 30)));

            // 2-3: POOR REWARD
            b.AnyState(when: q => HasHat(q) && q.Player.CharVar(CountVar) >= 2)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.BarenMoren).Called()
                    .OnTrigger(q => ShowOff(q, 2, QuestReply.Event(52, 20)));

            // 0-1: NO REWARD
            b.AnyState(when: q => HasHat(q))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.BarenMoren).Called()
                    .OnTrigger(q => ShowOff(q, 1, QuestReply.Event(52)));

            // Baren-Moren.lua onEventFinish (48, 52)
            b.AnyState()
                .OnEventFinish(Zone.WindurstWaters, 48, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.NewModelHat);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 52, q =>
                {
                    if (q.Option >= 1)
                    {
                        int rewardTier = q.Player.LocalVar(TierVar);
                        int gil = 0;
                        int item = 0;

                        if (rewardTier == 5)
                        {
                            gil = 500;
                            item = Item.WindshearHat; // the Lua's 12543
                        }
                        else if (rewardTier == 4)
                        {
                            gil = 400;
                            item = Item.WindshearHat;
                        }
                        else if (rewardTier == 3)
                        {
                            gil = 300;
                        }
                        else if (rewardTier == 2)
                        {
                            gil = 150;
                        }
                        else if (rewardTier == 1)
                        {
                            gil = 300;
                        }

                        if (CompleteWithRewards(q, gil, item))
                        {
                            q.Player.DelKeyItem(KeyItem.NewModelHat);
                            q.Player.SetNeedToZone(true);
                        }
                    }
                });
        }

        // npcUtil.completeQuest with fame 10, the fameArea WINDURST,
        // the vars QuestHatInHand_var and QuestHatInHand_count, and the tier's gil and item when set, step for step
        // (globals/npc_util.lua): not quest:complete, whose cleanup would clear the quest's own Quest[2][0] vars
        static bool CompleteWithRewards(IQuestContext q, int gil, int item)
        {
            if (item != 0)
            {
                if (!q.Player.GiveItem(item))
                {
                    return false;
                }
            }

            q.Player.AddFame(FameArea.Windurst, 10);

            if (gil != 0)
            {
                // params.gil * GIL_RATE, read by the engine's integral getter (llround), as The Sweetest Things reads it
                int amount = (int)Math.Round(gil * q.Settings.MainNumber("GIL_RATE"), MidpointRounding.AwayFromZero);
                q.Player.AddGil(amount);
                q.Text.Special(Text.WindurstWaters.GIL_OBTAINED, amount);
            }

            q.Player.SetCharVar(ShownVar, 0);
            q.Player.SetCharVar(CountVar, 0);
            q.Player.CompleteQuest(QuestLog.Windurst, "HAT_IN_HAND");

            return true;
        }
    }
}
