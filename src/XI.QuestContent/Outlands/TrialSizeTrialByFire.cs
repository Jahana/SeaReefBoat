// Trial-Size Trial by Fire (Log ID 5, Quest ID 15), ported from the quest branches of scripts/zones/Kazham/npcs/Dodmos.lua
// and the win finish of scripts/battlefields/Cloister_of_Flames/trial-size_trial_by_fire.lua (quest rework B3b, Outlands
// group 9; call-only: both scripts now call this quest). Dodmos : !pos 102.647 -14.999 -97.664 250 (his default 275).
// A SMN of main level 20 or more at Windurst fame level 2 gets 286 (0, Mini Tuning Fork of Fire, 0, 20: the fork, the zone,
// the level); option 1 with no free slot: ITEM_CANNOT_BE_OBTAINED; else begun, the fork (addItem, ITEM_OBTAINED).
// Accepted: 272 while a fork is held, else 290 (option 1: a new fork, or ITEM_CANNOT_BE_OBTAINED with no free slot); a
// SMN's trade of the fork plays 287, whose option 1 teleports the char to the Cloister of Flames (the fork kept).
// Completed: 289. The battlefield (a BattlefieldQuest naming this quest: SMN 20, the fork, status accepted or above, a
// status read that stays in Lua) wins with 32001, whose finish (content:onEventFinishWin, which calls xi.cs.questEvent
// with no npc) teaches Ifrit (IFRIT_UNLOCKED 0, 0, 0) when not known, gives the Scroll of Instant Warp
// (npcUtil.giveItem) when none is held, adds 20 Windurst fame and completes the quest (player:completeQuest).
// The Lua's state is the status alone.
// Kept as the Lua has it: 286, 290 and 287 do not look at the status (290 gives a fork in any status); 32001's finish
// completes the quest in any status and adds the fame again on every win (a completed char may re-enter); the scroll's
// npcUtil.giveItem failure (no free slot) does not stop the completion.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class TrialSizeTrialByFire : QuestScript
    {
        // xi.teleport.id.CLOISTER_OF_FLAMES (scripts/globals/teleports.lua)
        const int TeleportCloisterOfFlames = 42;

        public TrialSizeTrialByFire() : base(QuestLog.Outlands, "TRIAL_SIZE_TRIAL_BY_FIRE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Dodmos.lua onTrigger: requires a Summoner of level 20 at least
            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainLevel >= 20 &&
                    q.Player.MainJob == Job.Smn &&
                    q.Player.FameLevel(FameArea.Windurst) >= 2)
                .Npc(Zone.Kazham, Npc.Kazham.Dodmos).Called()
                    .OnTrigger(QuestReply.Event(286, 0, Item.MiniTuningForkOfFire, 0, 20)); // the fork, the zone, the level

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Kazham, Npc.Kazham.Dodmos).Called()
                    // Dodmos.lua onTrade (the status test is the state)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasItemQty(Item.MiniTuningForkOfFire, 1) &&
                            q.Player.MainJob == Job.Smn)
                        {
                            return QuestReply.Event(287, 0, Item.MiniTuningForkOfFire, 0, 20);
                        }

                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasItem(Item.MiniTuningForkOfFire))
                        {
                            return QuestReply.Event(272); // be prepared
                        }
                        else
                        {
                            return QuestReply.Event(290, 0, Item.MiniTuningForkOfFire, 0, 20); // another fork
                        }
                    });

            b.State(QuestStatus.Completed)
                .Npc(Zone.Kazham, Npc.Kazham.Dodmos).Called()
                    .OnTrigger(QuestReply.Event(289)); // defeated the avatar

            // Dodmos.lua onEventFinish (286, 290, 287); trial-size_trial_by_fire.lua content:onEventFinishWin (32001)
            b.AnyState()
                .OnEventFinish(Zone.Kazham, 286, q =>
                {
                    if (q.Option == 1)
                    {
                        if (q.Player.FreeSlotCount == 0)
                        {
                            q.Text.Special(Text.Kazham.ITEM_CANNOT_BE_OBTAINED, Item.MiniTuningForkOfFire); // the Lua's literal 1544
                        }
                        else
                        {
                            q.Quest.Begin();
                            q.Player.AddItem(Item.MiniTuningForkOfFire);
                            q.Text.Special(Text.Kazham.ITEM_OBTAINED, Item.MiniTuningForkOfFire);
                        }
                    }
                })
                .OnEventFinish(Zone.Kazham, 290, q =>
                {
                    if (q.Option == 1)
                    {
                        if (q.Player.FreeSlotCount == 0)
                        {
                            q.Text.Special(Text.Kazham.ITEM_CANNOT_BE_OBTAINED, Item.MiniTuningForkOfFire);
                        }
                        else
                        {
                            q.Player.AddItem(Item.MiniTuningForkOfFire);
                            q.Text.Special(Text.Kazham.ITEM_OBTAINED, Item.MiniTuningForkOfFire);
                        }
                    }
                })
                .OnEventFinish(Zone.Kazham, 287, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.TeleportTo(TeleportCloisterOfFlames);
                    }
                })
                .OnEventFinish(Zone.CloisterOfFlames, 32001, q =>
                {
                    if (!q.Player.HasSpell(Spell.Ifrit))
                    {
                        q.Player.AddSpell(Spell.Ifrit);
                        q.Text.Special(Text.CloisterOfFlames.IFRIT_UNLOCKED, 0, 0, 0);
                    }

                    if (!q.Player.HasItem(Item.ScrollOfInstantWarp))
                    {
                        q.Player.GiveItem(Item.ScrollOfInstantWarp);
                    }

                    q.Player.AddFame(FameArea.Windurst, 20);
                    // the raw player:completeQuest, which keeps the quest's own Quest[5][15] vars
                    q.Player.CompleteQuest(QuestLog.Outlands, "TRIAL_SIZE_TRIAL_BY_FIRE");
                });
        }
    }
}
