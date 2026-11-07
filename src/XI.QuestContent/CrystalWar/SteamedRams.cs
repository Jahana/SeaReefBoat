// Steamed Rams (Log ID 7, Quest ID 9), a line-for-line port of scripts/quests/crystalWar/WOTG_SAN_0_Steamed_Rams.lua
// (quest rework B3b Crystal War, group 1). Garlaige Citadel [S]: Randecque !pos 61.3 -6.1 137.4 (script: 3 with a campaign
// allegiance, else nothing). Southern San d'Oria [S]: Mainchelite !pos -15.3 1.4 -30.4 (no script; default 15). East
// Ronfaure [S]: qm3, qm4, qm5 (no scripts; defaults 4, 5, 6).
// Available: Randecque: with a campaign allegiance, 12 (an Event); without the Red Recommendation Letter, 1 ([7] =
// Option, 1 once declined), whose finish on option 0 gives the letter and sets Option 0; with it, 2. Mainchelite: with
// The Fighting Fourth or Snake on the Plains accepted, 9 (an Event); with the letter or an allegiance, 7 + 3 (pledged) +
// Option: 7 / 8 (no allegiance), 10 / 11 (pledged); 7 and 10 on option 0 begin it (7 takes the letter), else set Option
// 1; 8 and 11 on option 0 begin it (8 takes the letter); otherwise 15 (an Event). Accepted: Mainchelite: with the
// Charred Propeller, Oxidized Plate and Piece of Shattered Lumber: 12 (no allegiance) / 624 (pledged); without them, 13
// / 14, whose finish on option 1 deletes the quest (13 gives the letter back). 12's finish: with no allegiance the
// Sprinter's Shoes first (nothing more when they do not fit); completed (Knight of the Iron Ram), then the Bronze Ribbon
// of Service (no allegiance), allegiance 1, the three key items taken, NOW_ALLIED_WITH (1). 624's: completed, allegiance
// 1, the key items taken, NOW_ALLIED_WITH. East Ronfaure [S]: qm3 / qm4 / qm5 without their key item: 1 / 2 / 3, whose
// finishes give the Charred Propeller / Piece of Shattered Lumber / Oxidized Plate. Completed: Mainchelite 5 (an Event,
// replaceDefault).
// Kept as the Lua has it: no allegiance check on Randecque's 1 finish beyond option 0; 2 has no finish; Option stays 1
// after a later begin by 8 / 11; 13 / 14 delete the quest with no cleanup (Option survives); the qms answer nil once
// their key item is held (their defaults); 12 tests the allegiance again at its finish.
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): 10 and 11 on option 0 also take the
// Red Recommendation Letter when it is held, as 7 and 8 do; the Lua's began the quest and left the letter.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class SteamedRams : QuestScript
    {
        public SteamedRams() : base(QuestLog.CrystalWar, "STEAMED_RAMS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_SAN_0_Steamed_Rams";

        static void TakeEvidence(IQuestContext q)
        {
            q.Player.DelKeyItem(KeyItem.CharredPropeller);
            q.Player.DelKeyItem(KeyItem.OxidizedPlate);
            q.Player.DelKeyItem(KeyItem.PieceOfShatteredLumber);
        }

        // 10 / 11 (pledged) take a held Red Recommendation Letter, as 7 / 8 take it (fixed, user ruling 2026-11-04)
        static void TakeHeldLetter(IQuestContext q)
        {
            if (q.Player.HasKeyItem(KeyItem.RedRecommendationLetter))
            {
                q.Player.DelKeyItem(KeyItem.RedRecommendationLetter);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Title = Title.KnightOfTheIronRam,
            });

            b.State(QuestStatus.Available)
                .Npc(Zone.GarlaigeCitadelS, Npc.GarlaigeCitadelS.Randecque).OnTrigger(q =>
                {
                    if (q.Player.CampaignAllegiance > 0)
                    {
                        return QuestReply.Event(12);
                    }
                    else if (!q.Player.HasKeyItem(KeyItem.RedRecommendationLetter))
                    {
                        int hasDeclined = q.Quest.Var("Option");

                        return QuestReply.ProgressEvent(1).Option(7, hasDeclined);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(2);
                    }
                })
                .OnEventFinish(Zone.GarlaigeCitadelS, 1, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.GiveKeyItem(KeyItem.RedRecommendationLetter);
                        q.Quest.SetVar("Option", 0);
                    }
                })
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Mainchelite).OnTrigger(q =>
                {
                    if (q.Player.QuestStatus(QuestLog.CrystalWar, "THE_FIGHTING_FOURTH") == QuestStatus.Accepted ||
                        q.Player.QuestStatus(QuestLog.CrystalWar, "SNAKE_ON_THE_PLAINS") == QuestStatus.Accepted)
                    {
                        return QuestReply.Event(9);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.RedRecommendationLetter) || q.Player.CampaignAllegiance > 0)
                    {
                        int hasDeclined = q.Quest.Var("Option");
                        int isPledged = q.Player.CampaignAllegiance > 0 ? 3 : 0;

                        return QuestReply.ProgressEvent(7 + isPledged + hasDeclined);
                    }
                    else
                    {
                        return QuestReply.Event(15);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 7, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                        q.Player.DelKeyItem(KeyItem.RedRecommendationLetter);
                    }
                    else
                    {
                        q.Quest.SetVar("Option", 1);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 8, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                        q.Player.DelKeyItem(KeyItem.RedRecommendationLetter);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 10, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                        TakeHeldLetter(q);
                    }
                    else
                    {
                        q.Quest.SetVar("Option", 1);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 11, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                        TakeHeldLetter(q);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Mainchelite).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.CharredPropeller) &&
                        q.Player.HasKeyItem(KeyItem.OxidizedPlate) &&
                        q.Player.HasKeyItem(KeyItem.PieceOfShatteredLumber))
                    {
                        if (q.Player.CampaignAllegiance == 0)
                        {
                            return QuestReply.ProgressEvent(12);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(624);
                        }
                    }
                    else
                    {
                        int isPledged = q.Player.CampaignAllegiance > 0 ? 1 : 0;

                        return QuestReply.ProgressEvent(13 + isPledged);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 12, q =>
                {
                    bool hasNoAllegiance = q.Player.CampaignAllegiance == 0;

                    if (hasNoAllegiance && !q.Player.GiveItem(Item.SprintersShoes))
                    {
                        return;
                    }

                    if (q.Quest.Complete())
                    {
                        if (hasNoAllegiance)
                        {
                            q.Player.GiveKeyItem(KeyItem.BronzeRibbonOfService);
                        }

                        q.Player.SetCampaignAllegiance(1);

                        TakeEvidence(q);

                        q.Text.Special(Text.SouthernSanDoriaS.NOW_ALLIED_WITH, 1);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 13, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.GiveKeyItem(KeyItem.RedRecommendationLetter);
                        q.Player.DelQuest(QuestLog.CrystalWar, "STEAMED_RAMS");
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 14, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.DelQuest(QuestLog.CrystalWar, "STEAMED_RAMS");
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 624, q =>
                {
                    if (q.Quest.Complete())
                    {
                        // TODO: Adjust medal count for allegiance change.

                        q.Player.SetCampaignAllegiance(1);

                        TakeEvidence(q);

                        q.Text.Special(Text.SouthernSanDoriaS.NOW_ALLIED_WITH, 1);
                    }
                })
                .Npc(Zone.EastRonfaureS, Npc.EastRonfaureS.qm3).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.CharredPropeller))
                    {
                        return QuestReply.ProgressEvent(1);
                    }

                    return null;
                })
                .Npc(Zone.EastRonfaureS, Npc.EastRonfaureS.qm4).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.PieceOfShatteredLumber))
                    {
                        return QuestReply.ProgressEvent(2);
                    }

                    return null;
                })
                .Npc(Zone.EastRonfaureS, Npc.EastRonfaureS.qm5).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.OxidizedPlate))
                    {
                        return QuestReply.ProgressEvent(3);
                    }

                    return null;
                })
                .OnEventFinish(Zone.EastRonfaureS, 1, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.CharredPropeller);
                })
                .OnEventFinish(Zone.EastRonfaureS, 2, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.PieceOfShatteredLumber);
                })
                .OnEventFinish(Zone.EastRonfaureS, 3, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.OxidizedPlate);
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Mainchelite).OnTrigger(QuestReply.Event(5).ReplaceDefault());
        }
    }
}
