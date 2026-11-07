// Trial-Size Trial by Water (Log ID 5, Quest ID 148), ported from the quest branches of scripts/zones/Norg/npcs/Verctissa.lua
// and the win finish of scripts/battlefields/Cloister_of_Tides/trial-size_trial_by_water.lua (quest rework B3b, Outlands
// group 9; call-only: both scripts now call this quest). Verctissa : !pos -13 1 -20 252 (her default 114).
// A SMN of main level 20 or more at Norg fame level 2 gets 199 (0, Mini Tuning Fork of Water, 2, 20: the fork, the zone,
// the level); option 1 with no free slot: ITEM_CANNOT_BE_OBTAINED; else begun, the fork (addItem, ITEM_OBTAINED).
// Accepted: 111 while a fork is held, else 203 (option 1: a new fork, or ITEM_CANNOT_BE_OBTAINED with no free slot); a
// SMN's trade of the fork plays 200, whose option 1 teleports the char to the Cloister of Tides (the fork kept).
// Completed: 202. The battlefield (a BattlefieldQuest naming this quest: SMN 20, the fork, status accepted or above, a
// status read that stays in Lua) wins with 32001, whose finish (content:onEventFinishWin, which calls xi.cs.questEvent
// with no npc) teaches Leviathan (LEVIATHAN_UNLOCKED 0, 0, 2) when not known, gives the Scroll of Instant Warp
// (npcUtil.giveItem) when none is held, adds 20 Norg fame and completes the quest (player:completeQuest).
// The Lua's state is the status alone.
// Kept as the Lua has it: 199, 203 and 200 do not look at the status (203 gives a fork in any status); 32001's finish
// completes the quest in any status and adds the fame again on every win (a completed char may re-enter); the scroll's
// npcUtil.giveItem failure (no free slot) does not stop the completion.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class TrialSizeTrialByWater : QuestScript
    {
        // xi.teleport.id.CLOISTER_OF_TIDES (scripts/globals/teleports.lua)
        const int TeleportCloisterOfTides = 46;

        public TrialSizeTrialByWater() : base(QuestLog.Outlands, "TRIAL_SIZE_TRIAL_BY_WATER") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Verctissa.lua onTrigger: requires a Summoner of level 20 at least
            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainLevel >= 20 &&
                    q.Player.MainJob == Job.Smn &&
                    q.Player.FameLevel(FameArea.Norg) >= 2)
                .Npc(Zone.Norg, Npc.Norg.Verctissa).Called()
                    .OnTrigger(QuestReply.Event(199, 0, Item.MiniTuningForkOfWater, 2, 20)); // the fork, the zone, the level

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Norg, Npc.Norg.Verctissa).Called()
                    // Verctissa.lua onTrade (the status test is the state)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasItemQty(Item.MiniTuningForkOfWater, 1) &&
                            q.Player.MainJob == Job.Smn)
                        {
                            return QuestReply.Event(200, 0, Item.MiniTuningForkOfWater, 2, 20);
                        }

                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasItem(Item.MiniTuningForkOfWater))
                        {
                            return QuestReply.Event(111); // be prepared
                        }
                        else
                        {
                            return QuestReply.Event(203, 0, Item.MiniTuningForkOfWater, 2, 20); // another fork
                        }
                    });

            b.State(QuestStatus.Completed)
                .Npc(Zone.Norg, Npc.Norg.Verctissa).Called()
                    .OnTrigger(QuestReply.Event(202)); // defeated the avatar

            // Verctissa.lua onEventFinish (199, 203, 200); trial-size_trial_by_water.lua content:onEventFinishWin (32001)
            b.AnyState()
                .OnEventFinish(Zone.Norg, 199, q =>
                {
                    if (q.Option == 1)
                    {
                        if (q.Player.FreeSlotCount == 0)
                        {
                            q.Text.Special(Text.Norg.ITEM_CANNOT_BE_OBTAINED, Item.MiniTuningForkOfWater);
                        }
                        else
                        {
                            q.Quest.Begin();
                            q.Player.AddItem(Item.MiniTuningForkOfWater);
                            q.Text.Special(Text.Norg.ITEM_OBTAINED, Item.MiniTuningForkOfWater);
                        }
                    }
                })
                .OnEventFinish(Zone.Norg, 203, q =>
                {
                    if (q.Option == 1)
                    {
                        if (q.Player.FreeSlotCount == 0)
                        {
                            q.Text.Special(Text.Norg.ITEM_CANNOT_BE_OBTAINED, Item.MiniTuningForkOfWater);
                        }
                        else
                        {
                            q.Player.AddItem(Item.MiniTuningForkOfWater);
                            q.Text.Special(Text.Norg.ITEM_OBTAINED, Item.MiniTuningForkOfWater);
                        }
                    }
                })
                .OnEventFinish(Zone.Norg, 200, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.TeleportTo(TeleportCloisterOfTides);
                    }
                })
                .OnEventFinish(Zone.CloisterOfTides, 32001, q =>
                {
                    if (!q.Player.HasSpell(Spell.Leviathan))
                    {
                        q.Player.AddSpell(Spell.Leviathan);
                        q.Text.Special(Text.CloisterOfTides.LEVIATHAN_UNLOCKED, 0, 0, 2);
                    }

                    if (!q.Player.HasItem(Item.ScrollOfInstantWarp))
                    {
                        q.Player.GiveItem(Item.ScrollOfInstantWarp);
                    }

                    q.Player.AddFame(FameArea.Norg, 20);
                    // the raw player:completeQuest, which keeps the quest's own Quest[5][148] vars
                    q.Player.CompleteQuest(QuestLog.Outlands, "TRIAL_SIZE_TRIAL_BY_WATER");
                });
        }
    }
}
