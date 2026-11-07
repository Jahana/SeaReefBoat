// Blue Ribbon Blues (Log ID 2, Quest ID 17), a line-for-line port of scripts/quests/windurst/Blue_Ribbon_Blues.lua
// (quest rework B3b). Kerutoto : !pos 13 -5 -157 238; Roberta : !pos 21 -4 -157 241; Hume Bones : !pos 299 0.1 19 195.
// Offered after Water Way to Go at fame level 5: Kerutoto's 357 (oncePerZone, Prog 1) sends the char to Roberta for a
// Purple Ribbon; the ribbon traded to Kerutoto (358) pays 3600 gil and begins the quest (Prog 0, a 60-second Timer). After
// the timer Kerutoto gives a ribbon back (360: Prog 1); traded to the Hume Bones it pops Lich C. Magnus, whose death sets
// the quest local var Prog 2; the Hume Bones then give the Blue Ribbon (Prog 3), and Kerutoto completes the quest (362).
// Toraimarai Turmoil reads its completion; its era module is Modules/Era/Windurst/BlueRibbonBlues.cs. Kept as the Lua file
// has it: the onMobDeath's title is commented out; a ribbon traded back to Kerutoto at Prog 1 (365) takes it and restarts
// the timer; the local var Prog stays 2 after the Blue Ribbon is given, so the Hume Bones try to give it again (the ribbon
// is rare: ITEM_CANNOT_BE_OBTAINED).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class BlueRibbonBlues : QuestScript
    {
        public BlueRibbonBlues() : base(QuestLog.Windurst, "BLUE_RIBBON_BLUES") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Blue_Ribbon_Blues";

        // Roberta's 376 / 377 (sections 1 and 2 have the same body)
        static void FinishRoberta(IQuestContext q)
        {
            if (q.Option == 1)
            {
                q.Player.GiveItem(Item.PurpleRibbon);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 50, FameArea = FameArea.Windurst, Title = Title.GhostieBuster });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Windurst, "WATER_WAY_TO_GO") &&
                    q.Player.FameLevel(FameArea.Windurst) >= 5 &&
                    !q.Player.QuestMustZone(QuestLog.Windurst, "WATER_WAY_TO_GO"))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Kerutoto)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.PurpleRibbon))
                        {
                            return QuestReply.ProgressEvent(358, 3600);
                        }

                        return null;
                    })
                    .OnTrigger(q => QuestReply.Event(357).OncePerZone())
                .OnEventFinish(Zone.WindurstWaters, 357, q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 358, q =>
                {
                    q.Trade.Confirm();
                    q.Player.AddGil(3600);
                    q.Quest.Begin();
                    q.Quest.SetStep(0);
                    q.Quest.SetVar("Timer", q.Player.SystemTime + 60);
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Roberta).OnTrigger(q =>
                {
                    if (!q.Player.FindItem(Item.PurpleRibbon))
                    {
                        int questProgress = q.Quest.Step;

                        if (questProgress == 1)
                        {
                            return QuestReply.ProgressEvent(376, 0, Item.PurpleRibbon);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(377, 0, Item.PurpleRibbon);
                        }
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(379);
                    }
                })
                .OnEventFinish(Zone.WindurstWoods, 376, FinishRoberta)
                .OnEventFinish(Zone.WindurstWoods, 377, FinishRoberta);

            // Player has accepted the quest following Kerutoto trade event.  Due to the amount of interaction prior
            // to accepting, the Prog variable is reset to 0 when transitioning to this block.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.TheEldiemeNecropolis, Npc.TheEldiemeNecropolis.HumeBones)
                    .OnTrade(q =>
                    {
                        // npcUtil.popFromQM(player, npc, eldiemeID.mob.LICH_C_MAGNUS, { hide = 0 }): claim defaults to true
                        if (q.Trade.HasExactly(Item.PurpleRibbon) &&
                            q.Quest.Step == 1 &&
                            q.Player.PopFromQm(Mob.TheEldiemeNecropolis.LICH_C_MAGNUS, claim: true, hide: 0))
                        {
                            q.Trade.Confirm();
                            return QuestReply.MessageSpecial(Text.TheEldiemeNecropolis.RETURN_RIBBON_TO_HER);
                        }

                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Quest.LocalVar("Prog") == 2)
                        {
                            // For onTrigger, there must be a quest function returned to avoid hitting the
                            // default action.  Handle the item obtained messageSpecials here.

                            if (q.Player.GiveItem(Item.BlueRibbon, silent: true, fromTrade: false))
                            {
                                q.Quest.SetStep(3);
                                return QuestReply.MessageSpecial(Text.TheEldiemeNecropolis.ITEM_OBTAINED, Item.BlueRibbon);
                            }
                            else
                            {
                                return QuestReply.MessageSpecial(Text.TheEldiemeNecropolis.ITEM_CANNOT_BE_OBTAINED, Item.BlueRibbon);
                            }
                        }

                        return null;
                    })
                .Npc(Zone.TheEldiemeNecropolis, Npc.TheEldiemeNecropolis.LichCMagnus).OnMobDeath(q =>
                {
                    // player:addTitle(xi.title.LICH_BANISHER)

                    if (q.Quest.Step == 1)
                    {
                        q.Quest.SetLocalVar("Prog", 2);
                    }
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Kerutoto)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.PurpleRibbon) &&
                            q.Quest.Step == 1)
                        {
                            return QuestReply.ProgressEvent(365);
                        }

                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int questProgress = q.Quest.Step;

                        if (questProgress == 0)
                        {
                            if (q.Player.SystemTime < q.Quest.Var("Timer"))
                            {
                                return QuestReply.ProgressEvent(359);
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(360);
                            }
                        }
                        else if (questProgress == 1)
                        {
                            if (!q.Player.FindItem(Item.PurpleRibbon))
                            {
                                return QuestReply.ProgressEvent(366, 0, Item.PurpleRibbon);
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(361, 0, Item.PurpleRibbon);
                            }
                        }
                        else if (questProgress == 3)
                        {
                            return QuestReply.ProgressEvent(362);
                        }

                        return null;
                    })
                .OnEventFinish(Zone.WindurstWaters, 360, q =>
                {
                    if (q.Player.GiveItem(Item.PurpleRibbon))
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.WindurstWaters, 365, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetStep(0);
                    q.Quest.SetVar("Timer", q.Player.SystemTime + 60);
                })
                .OnEventFinish(Zone.WindurstWaters, 362, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Quest.SetMustZone();
                    }
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Roberta).OnTrigger(q =>
                {
                    if (q.Quest.Step < 3 &&
                        !q.Player.FindItem(Item.PurpleRibbon))
                    {
                        return QuestReply.ProgressEvent(377, 0, Item.PurpleRibbon);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(380);
                    }
                })
                .OnEventFinish(Zone.WindurstWoods, 377, FinishRoberta);

            b.State(QuestStatus.Completed, when: q => q.Quest.MustZone)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Kerutoto).OnTrigger(QuestReply.Event(363).ReplaceDefault());
        }
    }
}
