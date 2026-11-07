// Trial Size Trial by Ice (Log ID 0, Quest ID 107), ported from the quest branches of
// scripts/zones/Northern_San_dOria/npcs/Castilchat.lua and the win finish of
// scripts/battlefields/Cloister_of_Frost/trial-size_trial_by_ice.lua (quest rework B3b, San d'Oria group 16; call-only:
// both scripts now call this quest). Castilchat : !pos -188.658 0 109.275 231 (no default action: 711 is his script's).
// A SMN of main level 20 or more at San d'Oria fame level 2 gets 733 (0, Mini Tuning Fork of Ice, 4, 20: the fork, the
// zone, the level); option 1 with no free slot: ITEM_CANNOT_BE_OBTAINED; else begun, the fork (addItem, ITEM_OBTAINED).
// Accepted: 708 while a fork is held, else 737 (any option: a new fork, or ITEM_CANNOT_BE_OBTAINED with no free slot);
// a SMN's trade of the fork alone (one item) plays 734, whose option 1 teleports the char to the Cloister of Frost (the
// fork kept) and whose option 0 gives another fork. Completed: 736. Else his script's 711. The battlefield (a
// BattlefieldQuest naming this quest: SMN 20, the fork, status accepted or above, a status read that stays in Lua) wins
// with 32001, whose finish (content:onEventFinishWin, which calls xi.cs.questEvent with no npc) teaches Shiva
// (SHIVA_UNLOCKED 0, 0, 4) when not known, gives the Scroll of Instant Warp (npcUtil.giveItem) when none is held,
// adds 20 San d'Oria fame and completes the quest (the raw player:completeQuest, which keeps the quest's own
// Quest[0][107] vars). The Lua's state is the status alone.
// Kept as the Lua has it: 733, 737 and 734 do not look at the status (737 gives a fork in any status, on any
// option); 734's option 0 gives a fork while the traded one is still held (the fork is rare: addItem fails,
// ITEM_OBTAINED is sent all the same); 32001's finish completes the quest in any status and adds the fame again on
// every win (a completed char may re-enter); the scroll's npcUtil.giveItem failure (no free slot) does not stop the
// completion.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class TrialSizeTrialByIce : QuestScript
    {
        // xi.teleport.id.CLOISTER_OF_FROST (scripts/globals/teleports.lua)
        const int TeleportCloisterOfFrost = 43;

        public TrialSizeTrialByIce() : base(QuestLog.Sandoria, "TRIAL_SIZE_TRIAL_BY_ICE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Castilchat.lua onTrigger: requires a Summoner of level 20 at least
            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainLevel >= 20 &&
                    q.Player.MainJob == Job.Smn &&
                    q.Player.FameLevel(FameArea.Sandoria) >= 2)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Castilchat).Called()
                    // the fork, the zone, the level
                    .OnTrigger(QuestReply.Event(733, 0, Item.MiniTuningForkOfIce, 4, 20));

            b.State(QuestStatus.Accepted)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Castilchat).Called()
                    // Castilchat.lua onTrade (the status test is the state)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasItemQty(Item.MiniTuningForkOfIce, 1) &&
                            q.Player.MainJob == Job.Smn &&
                            q.Trade.ItemCount == 1)
                        {
                            return QuestReply.Event(734, 0, Item.MiniTuningForkOfIce, 4, 20);
                        }

                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasItem(Item.MiniTuningForkOfIce))
                        {
                            return QuestReply.Event(708); // be prepared
                        }
                        else
                        {
                            return QuestReply.Event(737, 0, Item.MiniTuningForkOfIce, 4, 20); // another fork
                        }
                    });

            b.State(QuestStatus.Completed)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Castilchat).Called()
                    .OnTrigger(QuestReply.Event(736)); // defeated the avatar

            // Castilchat.lua onEventFinish (733, 734, 737);
            // trial-size_trial_by_ice.lua content:onEventFinishWin (32001)
            b.AnyState()
                .OnEventFinish(Zone.NorthernSanDoria, 733, q =>
                {
                    if (q.Option == 1)
                    {
                        if (q.Player.FreeSlotCount == 0)
                        {
                            q.Text.Special(Text.NorthernSanDoria.ITEM_CANNOT_BE_OBTAINED, Item.MiniTuningForkOfIce);
                        }
                        else
                        {
                            q.Quest.Begin();
                            q.Player.AddItem(Item.MiniTuningForkOfIce);
                            q.Text.Special(Text.NorthernSanDoria.ITEM_OBTAINED, Item.MiniTuningForkOfIce);
                        }
                    }
                })
                // `csid == 734 and option == 0 or csid == 737`: 737 on any option
                .OnEventFinish(Zone.NorthernSanDoria, 737, q => GiveFork(q))
                .OnEventFinish(Zone.NorthernSanDoria, 734, q =>
                {
                    if (q.Option == 0)
                    {
                        GiveFork(q);
                    }
                    else if (q.Option == 1)
                    {
                        q.Player.TeleportTo(TeleportCloisterOfFrost);
                    }
                })
                .OnEventFinish(Zone.CloisterOfFrost, 32001, q =>
                {
                    if (!q.Player.HasSpell(Spell.Shiva))
                    {
                        q.Player.AddSpell(Spell.Shiva);
                        q.Text.Special(Text.CloisterOfFrost.SHIVA_UNLOCKED, 0, 0, 4);
                    }

                    if (!q.Player.HasItem(Item.ScrollOfInstantWarp))
                    {
                        q.Player.GiveItem(Item.ScrollOfInstantWarp);
                    }

                    q.Player.AddFame(FameArea.Sandoria, 20);
                    // the raw player:completeQuest
                    q.Player.CompleteQuest(QuestLog.Sandoria, "TRIAL_SIZE_TRIAL_BY_ICE");
                });
        }

        static void GiveFork(IQuestContext q)
        {
            if (q.Player.FreeSlotCount == 0)
            {
                q.Text.Special(Text.NorthernSanDoria.ITEM_CANNOT_BE_OBTAINED, Item.MiniTuningForkOfIce);
            }
            else
            {
                q.Player.AddItem(Item.MiniTuningForkOfIce);
                q.Text.Special(Text.NorthernSanDoria.ITEM_OBTAINED, Item.MiniTuningForkOfIce);
            }
        }
    }
}
