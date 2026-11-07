// Trial-Size Trial by Lightning (Log ID 4, Quest ID 28), ported from scripts/zones/Mhaura/npcs/Lacia.lua and the win
// finish of scripts/battlefields/Cloister_of_Storms/trial-size_trial_by_lightning.lua (quest rework B3b, Other Areas
// group 11; call-only: both scripts now call this quest; every branch of Lacia's script is this quest's). Lacia : !pos
// 31.712 -15.554 54.981 249 (her default 10021).
// A SMN of main level 20 or more at Windurst fame level 2 gets 10025 (0, Mini Tuning Fork of Lightning, 5, 20: the
// fork, the zone, the level); option 1 with no free slot: ITEM_CANNOT_BE_OBTAINED; else begun, the fork (addItem,
// ITEM_OBTAINED). Accepted: 10018 while a fork is held, else 10029 (option 1: a new fork, or ITEM_CANNOT_BE_OBTAINED
// with no free slot); a SMN's trade of the fork plays 10026, whose option 1 teleports the char to the Cloister of
// Storms (the fork kept). Completed: 10028. Else nothing (her default). The battlefield (a BattlefieldQuest naming this
// quest: SMN 20, the fork, status accepted or above, a status read that stays in Lua) wins with 32001, whose finish
// (content:onEventFinishWin, which calls xi.cs.questEvent with no npc) teaches Ramuh (RAMUH_UNLOCKED 0, 0, 5) when not
// known, gives the Scroll of Instant Warp (npcUtil.giveItem) when none is held, adds 20 Windurst fame and completes the
// quest (player:completeQuest). The Lua's state is the status alone.
// Kept as the Lua has it: 10025, 10029 and 10026 do not look at the status (10029 gives a fork in any status); 32001's
// finish completes the quest in any status and adds the fame again on every win (a completed char may re-enter); the
// scroll's npcUtil.giveItem failure (no free slot) does not stop the completion.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class TrialSizeTrialByLightning : QuestScript
    {
        // xi.teleport.id.CLOISTER_OF_STORMS (scripts/globals/teleports.lua)
        const int TeleportCloisterOfStorms = 45;

        public TrialSizeTrialByLightning() : base(QuestLog.OtherAreas, "TRIAL_SIZE_TRIAL_BY_LIGHTNING") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Lacia.lua onTrigger: requires a Summoner of level 20 at least
            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainLevel >= 20 &&
                    q.Player.MainJob == Job.Smn &&
                    q.Player.FameLevel(FameArea.Windurst) >= 2)
                .Npc(Zone.Mhaura, Npc.Mhaura.Lacia).Called()
                    // the fork, the zone, the level
                    .OnTrigger(QuestReply.Event(10025, 0, Item.MiniTuningForkOfLightning, 5, 20));

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Mhaura, Npc.Mhaura.Lacia).Called()
                    // Lacia.lua onTrade (the status test is the state)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasItemQty(Item.MiniTuningForkOfLightning, 1) &&
                            q.Player.MainJob == Job.Smn)
                        {
                            return QuestReply.Event(10026, 0, Item.MiniTuningForkOfLightning, 5, 20);
                        }

                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasItem(Item.MiniTuningForkOfLightning))
                        {
                            return QuestReply.Event(10018); // remind player to be prepared
                        }
                        else
                        {
                            // Need another mini tuning fork
                            return QuestReply.Event(10029, 0, Item.MiniTuningForkOfLightning, 5, 20);
                        }
                    });

            b.State(QuestStatus.Completed)
                .Npc(Zone.Mhaura, Npc.Mhaura.Lacia).Called()
                    .OnTrigger(QuestReply.Event(10028)); // Defeated Ramuh

            // Lacia.lua onEventFinish (10025, 10029, 10026); trial-size_trial_by_lightning.lua
            // content:onEventFinishWin (32001)
            b.AnyState()
                .OnEventFinish(Zone.Mhaura, 10025, q =>
                {
                    if (q.Option == 1)
                    {
                        if (q.Player.FreeSlotCount == 0)
                        {
                            q.Text.Special(Text.Mhaura.ITEM_CANNOT_BE_OBTAINED, Item.MiniTuningForkOfLightning);
                        }
                        else
                        {
                            q.Quest.Begin();
                            q.Player.AddItem(Item.MiniTuningForkOfLightning);
                            q.Text.Special(Text.Mhaura.ITEM_OBTAINED, Item.MiniTuningForkOfLightning);
                        }
                    }
                })
                .OnEventFinish(Zone.Mhaura, 10029, q =>
                {
                    if (q.Option == 1)
                    {
                        if (q.Player.FreeSlotCount == 0)
                        {
                            q.Text.Special(Text.Mhaura.ITEM_CANNOT_BE_OBTAINED, Item.MiniTuningForkOfLightning);
                        }
                        else
                        {
                            q.Player.AddItem(Item.MiniTuningForkOfLightning);
                            q.Text.Special(Text.Mhaura.ITEM_OBTAINED, Item.MiniTuningForkOfLightning);
                        }
                    }
                })
                .OnEventFinish(Zone.Mhaura, 10026, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.TeleportTo(TeleportCloisterOfStorms);
                    }
                })
                .OnEventFinish(Zone.CloisterOfStorms, 32001, q =>
                {
                    if (!q.Player.HasSpell(Spell.Ramuh))
                    {
                        q.Player.AddSpell(Spell.Ramuh);
                        q.Text.Special(Text.CloisterOfStorms.RAMUH_UNLOCKED, 0, 0, 5);
                    }

                    if (!q.Player.HasItem(Item.ScrollOfInstantWarp))
                    {
                        q.Player.GiveItem(Item.ScrollOfInstantWarp);
                    }

                    q.Player.AddFame(FameArea.Windurst, 20);
                    // the raw player:completeQuest, which keeps the quest's own Quest[4][28] vars
                    q.Player.CompleteQuest(QuestLog.OtherAreas, "TRIAL_SIZE_TRIAL_BY_LIGHTNING");
                });
        }
    }
}
