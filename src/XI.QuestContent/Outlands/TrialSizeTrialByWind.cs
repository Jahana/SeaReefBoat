// Trial-Size Trial by Wind (Log ID 5, Quest ID 197), ported from the quest branches of scripts/zones/Rabao/npcs/Rahi_Fohlatti.lua
// and the win finish of scripts/battlefields/Cloister_of_Gales/trial-size_trial_by_wind.lua (quest rework B3b, Outlands
// group 9; call-only: both scripts now call this quest). Rahi Fohlatti : !pos -17 7 -10 247 (her default 71).
// A SMN of main level 20 or more at Selbina-Rabao fame level 2 gets 108 (0, Mini Tuning Fork of Wind, 3, 20: the fork, the zone,
// the level); option 1 with no free slot: ITEM_CANNOT_BE_OBTAINED; else begun, the fork (addItem, ITEM_OBTAINED).
// Accepted: 68 while a fork is held, else 112 (option 1: a new fork, or ITEM_CANNOT_BE_OBTAINED with no free slot); a
// SMN's trade of the fork plays 109, whose option 1 teleports the char to the Cloister of Gales (the fork kept).
// Completed: 111. The battlefield (a BattlefieldQuest naming this quest: SMN 20, the fork, status accepted or above, a
// status read that stays in Lua) wins with 32001, whose finish (content:onEventFinishWin, which calls xi.cs.questEvent
// with no npc) teaches Garuda (GARUDA_UNLOCKED 0, 0, 3) when not known, gives the Scroll of Instant Warp
// (npcUtil.giveItem) when none is held, adds 10 San d'Oria and 10 Bastok fame and completes the quest (player:completeQuest).
// The Lua's state is the status alone.
// Kept as the Lua has it: 108, 112 and 109 do not look at the status (112 gives a fork in any status); 32001's finish
// completes the quest in any status and adds the fame again on every win (a completed char may re-enter); the scroll's
// npcUtil.giveItem failure (no free slot) does not stop the completion.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class TrialSizeTrialByWind : QuestScript
    {
        // xi.teleport.id.CLOISTER_OF_GALES (scripts/globals/teleports.lua)
        const int TeleportCloisterOfGales = 44;

        public TrialSizeTrialByWind() : base(QuestLog.Outlands, "TRIAL_SIZE_TRIAL_BY_WIND") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Rahi_Fohlatti.lua onTrigger: requires a Summoner of level 20 at least
            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainLevel >= 20 &&
                    q.Player.MainJob == Job.Smn &&
                    q.Player.FameLevel(FameArea.SelbinaRabao) >= 2)
                .Npc(Zone.Rabao, Npc.Rabao.RahiFohlatti).Called()
                    .OnTrigger(QuestReply.Event(108, 0, Item.MiniTuningForkOfWind, 3, 20)); // the fork, the zone, the level

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Rabao, Npc.Rabao.RahiFohlatti).Called()
                    // Rahi_Fohlatti.lua onTrade (the status test is the state)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasItemQty(Item.MiniTuningForkOfWind, 1) &&
                            q.Player.MainJob == Job.Smn)
                        {
                            return QuestReply.Event(109, 0, Item.MiniTuningForkOfWind, 3, 20);
                        }

                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasItem(Item.MiniTuningForkOfWind))
                        {
                            return QuestReply.Event(68); // be prepared
                        }
                        else
                        {
                            return QuestReply.Event(112, 0, Item.MiniTuningForkOfWind, 3, 20); // another fork
                        }
                    });

            b.State(QuestStatus.Completed)
                .Npc(Zone.Rabao, Npc.Rabao.RahiFohlatti).Called()
                    .OnTrigger(QuestReply.Event(111)); // defeated the avatar

            // Rahi_Fohlatti.lua onEventFinish (108, 112, 109); trial-size_trial_by_wind.lua content:onEventFinishWin (32001)
            b.AnyState()
                .OnEventFinish(Zone.Rabao, 108, q =>
                {
                    if (q.Option == 1)
                    {
                        if (q.Player.FreeSlotCount == 0)
                        {
                            q.Text.Special(Text.Rabao.ITEM_CANNOT_BE_OBTAINED, Item.MiniTuningForkOfWind);
                        }
                        else
                        {
                            q.Quest.Begin();
                            q.Player.AddItem(Item.MiniTuningForkOfWind);
                            q.Text.Special(Text.Rabao.ITEM_OBTAINED, Item.MiniTuningForkOfWind);
                        }
                    }
                })
                .OnEventFinish(Zone.Rabao, 112, q =>
                {
                    if (q.Option == 1)
                    {
                        if (q.Player.FreeSlotCount == 0)
                        {
                            q.Text.Special(Text.Rabao.ITEM_CANNOT_BE_OBTAINED, Item.MiniTuningForkOfWind);
                        }
                        else
                        {
                            q.Player.AddItem(Item.MiniTuningForkOfWind);
                            q.Text.Special(Text.Rabao.ITEM_OBTAINED, Item.MiniTuningForkOfWind);
                        }
                    }
                })
                .OnEventFinish(Zone.Rabao, 109, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.TeleportTo(TeleportCloisterOfGales);
                    }
                })
                .OnEventFinish(Zone.CloisterOfGales, 32001, q =>
                {
                    if (!q.Player.HasSpell(Spell.Garuda))
                    {
                        q.Player.AddSpell(Spell.Garuda);
                        q.Text.Special(Text.CloisterOfGales.GARUDA_UNLOCKED, 0, 0, 3);
                    }

                    if (!q.Player.HasItem(Item.ScrollOfInstantWarp))
                    {
                        q.Player.GiveItem(Item.ScrollOfInstantWarp);
                    }

                    q.Player.AddFame(FameArea.Sandoria, 10);
                    q.Player.AddFame(FameArea.Bastok, 10);
                    // the raw player:completeQuest, which keeps the quest's own Quest[5][197] vars
                    q.Player.CompleteQuest(QuestLog.Outlands, "TRIAL_SIZE_TRIAL_BY_WIND");
                });
        }
    }
}
