// Imperial Coronation (ToAU 46, Log ID 4, Mission ID 45), a line-for-line port of
// scripts/missions/toau/46_Imperial_Coronation.lua (quest rework C3 T4). Two sections, all in Aht Urhgan Whitegate;
// properlyDressed is the Royal Palace armor check (the facet RoyalPalaceArmorCheck) as 1 or 0. Section 1 (the mission
// current): the Imperial Whitegate with the main and sub slots empty and the armor right plays the progress event 3140
// (0, the char's current title, 0, 0, 0, 0, 0, properlyDressed, 0), else the plain event 3081 (0, 1, 0, 0, 0, 0, 0, 0,
// properlyDressed, 0); Naja Salaheem plays the plain event 3150. The update of 3140 by option: 0 answers 0, 1; 1, 2 and
// 3 give Balrahn's, Ulthalam's or Jalzahn's Ring and, when given, answer 1, 1 and set the Mission[4][45]Ring char var
// to the next conquest tally (the tally as its expiry too); 4 gives the Imperial Standard and answers 1, 1, else 0, 0;
// 99 answers the three ring ids. Kept as the Lua has it: option 1 answers 1, 1 even when the ring was not given
// (without setting the var), while options 2 and 3 answer nothing when it was not. 3140's finish completes the mission
// (The Empress Crowned current). Section 2 (the mission completed): the Imperial Whitegate dressed and empty-handed
// plays the plain event 3155 (0 if the char holds the Imperial Standard, else 1; 0 if the Ring var is set, else 1; 0,
// 0, 0, 0, 0, properlyDressed, 0), otherwise nothing; the update of 3155 is the same table, but options 1, 2 and 3 set
// the Ring var to the ring's item id and option 6 (not 4) gives the Imperial Standard. Kept as the Lua has it, too:
// 3140's finish completes the mission, which clears the Mission[4][45] vars, so the Ring var section 1 sets never
// reaches section 2 (3155 reports no ring); 3081 passes properlyDressed ninth, where startEvent reads the text table
// (3140 and 3155 pass it eighth). The Lua passes a ninth updateEvent operand, always 0; the port drops it (the engine
// reads eight, so nothing changes).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Toau
{
    public sealed class ImperialCoronation : MissionScript
    {
        public ImperialCoronation() : base(XI.Quests.MissionLog.ToAU, "IMPERIAL_CORONATION") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/toau/46_Imperial_Coronation";

        const int AreaId = MissionLog.Toau;

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.ToAU,
                NextMission = Mission.Toau.TheEmpressCrowned,
            });

            b.State(current: true)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.ImperialWhitegate).OnTrigger(q =>
                {
                    int properlyDressed = q.Player.RoyalPalaceArmorCheck() ? 1 : 0;

                    if (q.Player.EquipId(Slot.Main) == 0 &&
                        q.Player.EquipId(Slot.Sub) == 0 &&
                        properlyDressed == 1)
                    {
                        return QuestReply.ProgressEvent(3140, 0, q.Player.CurrentTitle, 0, 0, 0, 0, 0,
                            properlyDressed, 0);
                    }

                    return QuestReply.Event(3081, 0, 1, 0, 0, 0, 0, 0, 0, properlyDressed, 0);
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.NajaSalaheem).OnTrigger(q =>
                {
                    return QuestReply.Event(3150, q.Player.MercenaryRank, 1, 0, 0, 0, 0, 0, 0, 0);
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 3140, q =>
                {
                    if (q.Option == 0) // Reject all 3 rings.
                    {
                        q.Player.UpdateEvent(0, 1, 0, 0, 0, 0, 0, 0);
                    }
                    else if (q.Option == 1)
                    {
                        if (q.Player.GiveItem(Item.BalrahnsRing))
                        {
                            q.Player.UpdateEvent(1, 1, 0, 0, 0, 0, 0, 0);
                            q.Player.SetCharVar("Mission[4][45]Ring", q.Player.NextConquestTally,
                                (uint)q.Player.NextConquestTally);
                        }
                        else
                        {
                            q.Player.UpdateEvent(1, 1, 0, 0, 0, 0, 0, 0);
                        }
                    }
                    else if (q.Option == 2)
                    {
                        if (q.Player.GiveItem(Item.UlthalamsRing))
                        {
                            q.Player.UpdateEvent(1, 1, 0, 0, 0, 0, 0, 0);
                            q.Player.SetCharVar("Mission[4][45]Ring", q.Player.NextConquestTally,
                                (uint)q.Player.NextConquestTally);
                        }
                    }
                    else if (q.Option == 3)
                    {
                        if (q.Player.GiveItem(Item.JalzahnsRing))
                        {
                            q.Player.UpdateEvent(1, 1, 0, 0, 0, 0, 0, 0);
                            q.Player.SetCharVar("Mission[4][45]Ring", q.Player.NextConquestTally,
                                (uint)q.Player.NextConquestTally);
                        }
                    }
                    else if (q.Option == 4) // Get Imperial Standard.
                    {
                        if (q.Player.GiveItem(Item.ImperialStandard))
                        {
                            q.Player.UpdateEvent(1, 1, 0, 0, 0, 0, 0, 0);
                        }
                        else
                        {
                            q.Player.UpdateEvent(0, 0, 0, 0, 0, 0, 0, 0);
                        }
                    }
                    else if (q.Option == 99) // Offered rings.
                    {
                        q.Player.UpdateEvent(Item.BalrahnsRing, Item.UlthalamsRing, Item.JalzahnsRing, 0, 0, 0, 0, 0);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 3140, q =>
                {
                    q.Mission.Complete();
                });

            b.State(current: null, when: q => q.Player.HasCompletedMission(AreaId, Mission.Toau.ImperialCoronation))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.ImperialWhitegate).OnTrigger(q =>
                {
                    int properlyDressed = q.Player.RoyalPalaceArmorCheck() ? 1 : 0;

                    if (q.Player.EquipId(Slot.Main) == 0 &&
                        q.Player.EquipId(Slot.Sub) == 0 &&
                        properlyDressed == 1)
                    {
                        int hasImperialStandard = q.Player.HasItem(Item.ImperialStandard) ? 0 : 1;
                        int hasScenarioRing = q.Player.CharVar("Mission[4][45]Ring") != 0 ? 0 : 1;

                        return QuestReply.Event(3155, hasImperialStandard, hasScenarioRing, 0, 0, 0, 0, 0,
                            properlyDressed, 0);
                    }

                    return null;
                })
                .OnEventUpdate(Zone.AhtUrhganWhitegate, 3155, q =>
                {
                    if (q.Option == 0) // Reject all 3 rings.
                    {
                        q.Player.UpdateEvent(0, 1, 0, 0, 0, 0, 0, 0);
                    }
                    else if (q.Option == 1)
                    {
                        if (q.Player.GiveItem(Item.BalrahnsRing))
                        {
                            q.Player.UpdateEvent(1, 1, 0, 0, 0, 0, 0, 0);
                            q.Player.SetCharVar("Mission[4][45]Ring", Item.BalrahnsRing);
                        }
                        else
                        {
                            q.Player.UpdateEvent(1, 1, 0, 0, 0, 0, 0, 0);
                        }
                    }
                    else if (q.Option == 2)
                    {
                        if (q.Player.GiveItem(Item.UlthalamsRing))
                        {
                            q.Player.UpdateEvent(1, 1, 0, 0, 0, 0, 0, 0);
                            q.Player.SetCharVar("Mission[4][45]Ring", Item.UlthalamsRing);
                        }
                    }
                    else if (q.Option == 3)
                    {
                        if (q.Player.GiveItem(Item.JalzahnsRing))
                        {
                            q.Player.UpdateEvent(1, 1, 0, 0, 0, 0, 0, 0);
                            q.Player.SetCharVar("Mission[4][45]Ring", Item.JalzahnsRing);
                        }
                    }
                    else if (q.Option == 6) // Get Imperial Standard.
                    {
                        if (q.Player.GiveItem(Item.ImperialStandard))
                        {
                            q.Player.UpdateEvent(1, 1, 0, 0, 0, 0, 0, 0);
                        }
                        else
                        {
                            q.Player.UpdateEvent(0, 0, 0, 0, 0, 0, 0, 0);
                        }
                    }
                    else if (q.Option == 99) // Offered rings.
                    {
                        q.Player.UpdateEvent(Item.BalrahnsRing, Item.UlthalamsRing, Item.JalzahnsRing, 0, 0, 0, 0, 0);
                    }
                });
        }
    }
}
