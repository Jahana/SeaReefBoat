// Over the Hills and Far Away (Log ID 0, Quest ID 112), ported from the quest branches of
// scripts/zones/Port_San_dOria/npcs/Antreneau.lua and scripts/zones/Uleguerand_Range/npcs/qm_moblin_hotrok.lua
// (quest rework B3b, San d'Oria group 16; call-only). Antreneau : !pos -71 -5 -39 232 (his default
// ANTRENEAU_READY_TO_EAT); qm_moblin_hotrok (???) : !pos -299 -62 -18 5.
// Antreneau: A Taste for Meat's var Option (Quest[0][100]Option) 0, A Taste for Meat completed, San d'Oria
// fame level 8, The Medicine Woman completed and the char var DiaryPage 4 or more: available: 725 (its finish begins
// the quest, on any option); accepted: 726 (the map); completed: 727 (his uncle found). Else nothing (his default
// plays).
// The ??? in Uleguerand Range: a trade while accepted holding the Moblin Hotrok (npcUtil.tradeHas): 10 (0, the
// hotrok 1729, the Map of the Uleguerand Range, then four flags): with the CoP Louverance status nibble 14
// (getMissionStatus(COP, LOUVERANCE)), the title Companion of Louverance or True Companion of Louverance: (0, 0, 1, 0),
// else (1, 0, 1, 0); without that status: a title: (0, 1, 0, 0), else (1, 0, 0, 0). Its trigger:
// completed NOTHING_OUT_OF_ORDINARY; accepted SOMETHING_GLITTERING then WHAT_LIES_BENEATH (0, the hotrok); else
// SOMETHING_GLITTERING_BUT. 10's finish: npcUtil.completeQuest step for step (the map by npcUtil.giveKeyItem,
// fame 80 San d'Oria, 2000 gil at GIL_RATE with GIL_OBTAINED, 2000 exp at EXP_RATE, the raw player:completeQuest: not
// quest:complete, whose cleanup would clear the quest's own Quest[0][112] vars), then the hotrok taken (confirmTrade).
// The rates' products reach the engine as doubles, which it rounds half away from zero (llround).
// Read only: A Taste for Meat's var (main's ATasteForMeat.cs owns it; Antreneau's own A Taste for Meat events are that
// quest's), DiaryPage (To Cure a Cough's diary pages).
// Kept as the Lua has it: 725 begins on any option and reads no status in its finish; 10's finish reads no status (a
// trade's 10 is offered only while accepted); the ??? reads the Louverance nibble only for the event's flags.
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class OverTheHillsAndFarAway : QuestScript
    {
        // xi.mission.status.COP.LOUVERANCE (scripts/globals/missions.lua): the nibble of the CoP status word
        const int CopStatusLouverance = 2;

        public OverTheHillsAndFarAway() : base(QuestLog.Sandoria, "OVER_THE_HILLS_AND_FAR_AWAY") { }

        private static int RateAmount(int n, double rate) => (int)Math.Round(n * rate, MidpointRounding.AwayFromZero);

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                // Antreneau.lua onTrigger (his default plays when nothing starts)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Antreneau).Called().OnTrigger(q =>
                {
                    if (q.Player.CharVar("Quest[0][100]Option") == 0 &&
                        q.Player.QuestStatus(QuestLog.Sandoria, "A_TASTE_FOR_MEAT") == QuestStatus.Completed &&
                        q.Player.FameLevel(FameArea.Sandoria) >= 8 &&
                        q.Player.QuestStatus(QuestLog.Sandoria, "THE_MEDICINE_WOMAN") == QuestStatus.Completed &&
                        q.Player.CharVar("DiaryPage") >= 4)
                    {
                        QuestStatus status = q.Quest.Status;

                        if (status == QuestStatus.Available)
                        {
                            return QuestReply.Event(725); // start
                        }
                        else if (status == QuestStatus.Accepted)
                        {
                            return QuestReply.Event(726); // talks about the map
                        }
                        else if (status == QuestStatus.Completed)
                        {
                            return QuestReply.Event(727); // found his uncle Louverance
                        }
                    }

                    return null;
                })
                // qm_moblin_hotrok.lua onTrade, onTrigger
                .Npc(Zone.UleguerandRange, Npc.UleguerandRange.qmmoblinhotrok).Called()
                    .OnTrade(q =>
                    {
                        if (q.Quest.Status == QuestStatus.Accepted &&
                            q.Trade.Has(Item.MoblinHotrok))
                        {
                            bool companion = q.Player.HasTitle(Title.CompanionOfLouverance) ||
                                q.Player.HasTitle(Title.TrueCompanionOfLouverance);

                            if (q.Player.MissionStatus(MissionLog.Cop, CopStatusLouverance) == 14)
                            {
                                if (companion)
                                {
                                    // remembers you and the girl from Tavnazia, asks to trade the dagger for the map
                                    return QuestReply.Event(10, 0, Item.MoblinHotrok, KeyItem.MapOfTheUleguerandRange,
                                        0, 0, 1, 0);
                                }
                                else
                                {
                                    // remembers you and the girl from Tavnazia, asks for the dagger
                                    return QuestReply.Event(10, 0, Item.MoblinHotrok, KeyItem.MapOfTheUleguerandRange,
                                        1, 0, 1, 0);
                                }
                            }
                            else
                            {
                                if (companion)
                                {
                                    // doesn't recognize you, asks to trade the dagger for the map
                                    return QuestReply.Event(10, 0, Item.MoblinHotrok, KeyItem.MapOfTheUleguerandRange,
                                        0, 1, 0, 0);
                                }
                                else
                                {
                                    // doesn't recognize you, asks for the dagger
                                    return QuestReply.Event(10, 0, Item.MoblinHotrok, KeyItem.MapOfTheUleguerandRange,
                                        1, 0, 0, 0);
                                }
                            }
                        }

                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        QuestStatus status = q.Quest.Status;

                        if (status == QuestStatus.Completed)
                        {
                            q.Text.Special(Text.UleguerandRange.NOTHING_OUT_OF_ORDINARY);
                        }
                        else if (status == QuestStatus.Accepted)
                        {
                            q.Text.Special(Text.UleguerandRange.SOMETHING_GLITTERING);
                            q.Text.Special(Text.UleguerandRange.WHAT_LIES_BENEATH, 0, Item.MoblinHotrok);
                        }
                        else
                        {
                            q.Text.Special(Text.UleguerandRange.SOMETHING_GLITTERING_BUT);
                        }

                        return QuestReply.NoAction();
                    });

            b.AnyState()
                // Antreneau.lua onEventFinish 725
                .OnEventFinish(Zone.PortSanDoria, 725, q =>
                {
                    q.Quest.Begin();
                })
                // qm_moblin_hotrok.lua onEventFinish 10
                .OnEventFinish(Zone.UleguerandRange, 10, q =>
                {
                    // npcUtil.completeQuest(player, SANDORIA, OVER_THE_HILLS_AND_FAR_AWAY, { fame = 80, fameArea =
                    // SANDORIA, keyItem = MAP_OF_THE_ULEGUERAND_RANGE, gil = 2000, exp = 2000 }), step for step: not
                    // quest:complete, whose cleanup would clear the quest's own Quest[0][112] vars. The rates' products
                    // reach the engine as doubles, which it rounds half away from zero (llround)
                    int gil = RateAmount(2000, q.Settings.MainNumber("GIL_RATE"));
                    q.Player.GiveKeyItem(KeyItem.MapOfTheUleguerandRange);
                    q.Player.AddFame(FameArea.Sandoria, 80);
                    q.Player.AddGil(gil);
                    q.Text.Special(Text.UleguerandRange.GIL_OBTAINED, gil);
                    q.Player.AddExp(RateAmount(2000, q.Settings.MainNumber("EXP_RATE")));
                    q.Player.CompleteQuest(QuestLog.Sandoria, "OVER_THE_HILLS_AND_FAR_AWAY");
                    q.Trade.Confirm();
                });
        }
    }
}
