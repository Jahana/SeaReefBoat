// Lest We Forget (WotG 54, Log ID 5, Mission ID 53), a line-for-line port of
// scripts/missions/wotg/54_Lest_We_Forget.lua (quest rework C3 T8). Veridical Conflux !pos -142.279 -6.749 585.239 89.
// The last mission: an empty reward table, and nothing completes it. One section (the mission current), in Grauberg
// [S]. The Veridical Conflux at Status 1 (A Token of Troth's finish sets it) plays the progress event 39 (89, 23, 1756)
// as an important event; its update with an option above 0 reads the two augment selections from the option's bits
// 1-4 and 17-20 and sends the update ((first's value << 27) + (first's augment << 16) + 770, (second's value << 11) +
// second's augment); its finish with any option but the cancel (1 << 30), with no free slot, says
// ITEM_CANNOT_BE_OBTAINED, else adds the Moonshade Earring with the two augments, says ITEM_OBTAINED and sets Status 0.
// afterZoneIn: without the earring (anywhere the char holds items) at Status 0, the local var Option is set to 1. The
// qm_reset with that local var at 1 and no mustZone plays the progress event 46 (89, 23, 1756, 0, 9467, 16940788, 0,
// 0); its finish with option 1 deletes Maiden of the Dusk, Where It All Began, A Token of Troth and Lest We Forget,
// adds Maiden of the Dusk with Status 2, gives the Primal Glow and says YOU_HAVE_RETRACED_RIVER; any other option sets
// the mustZone. Kept as the Lua has it: the augment tables are 1-based Lua tables though their note calls them
// 0-indexed, so a selection of 0 or past 8 indexes a nil entry and raises (the update and the finish alike); and
// player:addItem(id, 1, augment, value, augment, value), the positional form, takes no augments in C++
// (lua_base_entity.cpp) or C#, so the earring comes with an empty extra, as the recorded traces show it.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Wotg
{
    public sealed class LestWeForget : MissionScript
    {
        public LestWeForget() : base(XI.Quests.MissionLog.WoTG, "LEST_WE_FORGET") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/wotg/54_Lest_We_Forget";

        const int AreaId = MissionLog.Wotg;

        // utils.EVENT_CANCELLED_OPTION: bit.lshift(1, 30)
        const int EventCancelledOption = 1 << 30;

        // firstAugInfo, the Lua's [1] to [8]: the augment (augments.sql) and its value
        public static readonly (int Augment, int Value)[] FirstAugInfo =
        {
            (23, 3),   // ACC+4
            (25, 3),   // ATK+4
            (27, 3),   // RACC+4
            (29, 3),   // RATK+4
            (35, 3),   // MACC+4
            (133, 3),  // MAB+4
            (1, 24),   // HP+25
            (9, 24),   // MP+25
        };

        // secondAugInfo, the Lua's [1] to [8]
        public static readonly (int Augment, int Value)[] SecondAugInfo =
        {
            (59, 0),   // Regain +10
            (60, 0),   // Refresh +1
            (352, 4),  // Occ. grants dmg. bonus based on TP+5%
            (353, 4),  // TP Bonus +250
            (350, 2),  // Occ. maximizes magic accuracy+3%
            (351, 2),  // Occ. quickens spellcasting+3%
            (145, 2),  // Counter+3
            (61, 4),   // Occ. inc. resist. to all stat. ailments +5
        };

        // table[sel], the Lua's 1-based index: an entry outside 1..8 is nil, and indexing it raises
        public static (int Augment, int Value) AugAt((int Augment, int Value)[] table, int sel)
        {
            if (sel < 1 || sel > table.Length)
            {
                throw new System.InvalidOperationException("attempt to index a nil value");
            }

            return table[sel - 1];
        }

        // bit.band(bit.rshift(option, 1), 0xF) and bit.band(bit.rshift(option, 17), 0xF)
        static int FirstSel(int option) => (option >> 1) & 0xF;

        static int SecondSel(int option) => (option >> 17) & 0xF;

        protected override void Build(MissionScriptBuilder b)
        {
            b.State(current: true)
                .Npc(Zone.GraubergS, Npc.GraubergS.VeridicalConflux).OnTrigger(q =>
                {
                    if (q.Mission.Var("Status") == 1)
                    {
                        return QuestReply.ProgressEvent(39, 89, 23, 1756).ImportantEvent();
                    }

                    return null;
                })
                .Npc(Zone.GraubergS, Npc.GraubergS.qmreset).OnTrigger(q =>
                {
                    if (q.Mission.LocalVar("Option") == 1 &&
                        !q.Mission.MustZone)
                    {
                        return QuestReply.ProgressEvent(46, 89, 23, 1756, 0, 9467, 16940788, 0, 0);
                    }

                    return null;
                })
                .OnAfterZoneIn(Zone.GraubergS, q =>
                {
                    if (!q.Player.HasItem(Item.MoonshadeEarring) &&
                        q.Mission.Var("Status") == 0)
                    {
                        q.Mission.SetLocalVar("Option", 1);
                    }

                    return null;
                })
                .OnEventUpdate(Zone.GraubergS, 39, q =>
                {
                    if (q.Option > 0)
                    {
                        var first = AugAt(FirstAugInfo, FirstSel(q.Option));
                        var second = AugAt(SecondAugInfo, SecondSel(q.Option));

                        int firstAugmentParam = (first.Value << 27) + (first.Augment << 16) + 770;
                        int secondAugmentParam = (second.Value << 11) + second.Augment;

                        q.Player.UpdateEvent(firstAugmentParam, secondAugmentParam);
                    }
                })
                .OnEventFinish(Zone.GraubergS, 39, q =>
                {
                    if (q.Option != EventCancelledOption)
                    {
                        int firstAugSel = FirstSel(q.Option);
                        int secondAugSel = SecondSel(q.Option);

                        if (q.Player.FreeSlotCount < 1)
                        {
                            q.Text.Special(Text.GraubergS.ITEM_CANNOT_BE_OBTAINED);
                        }
                        else
                        {
                            var first = AugAt(FirstAugInfo, firstAugSel);
                            var second = AugAt(SecondAugInfo, secondAugSel);
                            q.Player.AddItemAugmented(Item.MoonshadeEarring, 1, first.Augment, first.Value,
                                second.Augment, second.Value);

                            q.Text.Special(Text.GraubergS.ITEM_OBTAINED, Item.MoonshadeEarring);

                            q.Mission.SetVar("Status", 0);
                        }
                    }
                })
                .OnEventFinish(Zone.GraubergS, 46, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.DelMission(AreaId, Mission.Wotg.MaidenOfTheDusk);
                        q.Player.DelMission(AreaId, Mission.Wotg.WhereItAllBegan);
                        q.Player.DelMission(AreaId, Mission.Wotg.ATokenOfTroth);
                        q.Player.DelMission(AreaId, Mission.Wotg.LestWeForget);

                        q.Player.AddMission(AreaId, Mission.Wotg.MaidenOfTheDusk);
                        q.Player.SetMissionVar(AreaId, Mission.Wotg.MaidenOfTheDusk, "Status", 2);

                        q.Player.GiveKeyItem(KeyItem.PrimalGlow);
                        q.Text.Special(Text.GraubergS.YOU_HAVE_RETRACED_RIVER);
                    }
                    else
                    {
                        q.Mission.SetMustZone();
                    }
                });
        }
    }
}
