// Knocking on Forbidden Doors (Log ID 4, Quest ID 78), a line-for-line port of
// scripts/quests/otherAreas/Knocking_on_Forbidden_Doors.lua (quest rework B3b, Other Areas group 4), with xi.mannequin
// (scripts/globals/mannequins.lua: cost, getMannequins, getMannequinPose, setMannequinPose) ported as Mannequins below.
// Tavnazian Safehold: Enaremand (two of that name, the second cutscene_only; no script, default 537), Chemioue (two of that
// name, 16883724 and 16883760, the second cutscene_only; no script, default 280); Phomiuna Aqueducts: the Wooden Ladders (a
// script: the ladder cutscenes by the char's position), LADDER_KNOCKING the 4th; Misareaux Coast: qm_mire_incense, qm_alsha
// (no script, no default), the NM Alsha; Mhaura: Fyi Chalmwoh !pos -39.273 -16.000 70.126 249 (no script, no default).
// Available once Behind the Smile is completed: Enaremand 535, whose finish begins the quest. Accepted: Chemioue at Prog 0:
// 536 (Prog 1); the ladder LADDER_KNOCKING at Prog 1: 38 (27) (Prog 2); qm_mire_incense at Prog 2 without the Mire Incense:
// the key item (quest:keyItem); qm_alsha: with the incense at Prog 2: 556 ([1] = the incense; Prog 3); at Prog 3 with Alsha
// not spawned: 557 ([1] = the incense), whose finish at Prog 3 pops Alsha (claimed, the ??? not hidden) and shows
// FOUL_STENCH_OF_DEATH; at Prog 4: 558 (Prog 5, the incense taken, Better Humes and Mannequins given). Alsha's death, for
// the killer only (optParams.isKiller): Prog 4 for each alliance member whose Quest[4][78]Prog is 3. Fyi at Prog 5: 321
// ([0] = 704, the mannequin mask, the three costs, the gil, [6] = the key item), whose finish completes the quest (no
// reward table). Completed with Better Humes and Mannequins: Fyi's mannequin shop: 321 (the mask, the costs, the gil); a
// trade of exactly one mannequin: 319; 321's update: option 0, whether the gil reaches 100000; 11 to 18, the pose of that
// race's mannequin; 319's finish: option 1 to 8 with 2000 gil taken: the trade confirmed, that race's mannequin given, its
// pose reset; 321's finish: no free slot: ITEM_CANNOT_BE_OBTAINED; option 1 to 8 with 100000 gil taken: ITEM_OBTAINED, the
// mannequin added, its pose reset; option 10 or more with 200 gil taken: the pose set (race ((option - 11) % 8) + 1, pose
// floor(option / 32)).
// Kept as the Lua has it: 38, 556, 558 and the accepted 321 finishes are keyed on the csid alone; Alsha's death sets the
// killer's Prog 4 when any alliance member (the killer too) is at Prog 3, whatever the killer's own Prog; the accepted 321's
// finish completes the quest and then the completed section's 321 finish runs too (its check holds once the quest is
// completed: the lookup runs every section whose check passes), with the same option; Lua's % is floored, so option 10
// poses race 8 ((10 - 11) % 8 = 7); the free-slot message names HUME_M_MANNEQUIN + option - 1 whatever the option.
using System.Collections.Generic;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    // xi.mannequin (scripts/globals/mannequins.lua), for Knocking on Forbidden Doors
    public static class Mannequins
    {
        // xi.mannequin.cost
        public const int CostPurchase = 100000;
        public const int CostTrade = 2000;
        public const int CostPose = 200;

        // xi.mannequin.getMannequins(player): bit (itemId - HUME_M_MANNEQUIN) set for each mannequin held (player:hasItem)
        public static int GetMannequins(IQuestContext q)
        {
            int mannequinMask = 0;
            for (int itemId = Item.HumeMMannequin; itemId <= Item.GalkaMannequin; itemId++)
            {
                if (q.Player.HasItem(itemId))
                {
                    mannequinMask |= 1 << (itemId - Item.HumeMMannequin); // utils.mask.setBit(mannequinMask, bit, true)
                }
            }

            return mannequinMask;
        }

        // xi.mannequin.setMannequinPose(player, race, pose): Race = 1 to 8
        public static void SetMannequinPose(IQuestContext q, int race, int pose)
        {
            q.Player.SetMannequinPose(Item.HumeMMannequin + race - 1, race, pose);
        }

        // xi.mannequin.getMannequinPose(player, race)
        public static int GetMannequinPose(IQuestContext q, int race)
        {
            return q.Player.MannequinPose(Item.HumeMMannequin + race - 1);
        }
    }

    public sealed class KnockingOnForbiddenDoors : QuestScript
    {
        public KnockingOnForbiddenDoors() : base(QuestLog.OtherAreas, "KNOCKING_ON_FORBIDDEN_DOORS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Knocking_on_Forbidden_Doors";

        // Lua's a % b (floored: the result has b's sign)
        static int LuaMod(int a, int b) => ((a % b) + b) % b;

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4, 5);

            // START: Talk to Enaremand (J-7) on the upper level in Tavnazian Safehold
            // QUEST AVAILABLE
            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.OtherAreas, "BEHIND_THE_SMILE"))
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Enaremand).OnTrigger(QuestReply.ProgressEvent(535))
                .OnEventFinish(Zone.TavnazianSafehold, 535, q =>
                {
                    q.Quest.Begin();
                });

            // QUEST ACCEPTED
            b.State(QuestStatus.Accepted)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Chemioue).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(536);
                    }
                    return null;
                })
                .OnEventFinish(Zone.TavnazianSafehold, 536, q =>
                {
                    q.Quest.SetStep(1);
                })
                .Npc(Zone.PhomiunaAqueducts, Npc.PhomiunaAqueducts.WoodenLadder).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1 &&
                        q.Npc.Id == q.Player.NpcIdOf("LADDER_KNOCKING"))
                    {
                        // Find a better way to identify exactly which ladder it is in case IDs shift
                        return QuestReply.ProgressEvent(38, 27); // Essentially chains the ladder cs with the actual quest cs
                    }
                    return null;
                })
                .OnEventFinish(Zone.PhomiunaAqueducts, 38, q =>
                {
                    q.Quest.SetStep(2);
                })
                .Npc(Zone.MisareauxCoast, Npc.MisareauxCoast.qmmireincense).OnTrigger(q =>
                {
                    // ??? by end of river
                    if (q.Quest.Step == 2 &&
                        !q.Player.HasKeyItem(KeyItem.MireIncense))
                    {
                        return QuestReply.KeyItem(KeyItem.MireIncense);
                    }
                    return null;
                })
                .Npc(Zone.MisareauxCoast, Npc.MisareauxCoast.qmalsha).OnTrigger(q =>
                {
                    int progressVar = q.Quest.Step;
                    // First time clicking on ???
                    if (q.Player.HasKeyItem(KeyItem.MireIncense) &&
                        progressVar == 2)
                    {
                        return QuestReply.ProgressEvent(556).Option(1, KeyItem.MireIncense);
                    }
                    // Second time clicking on ???
                    else if (progressVar == 3 &&
                             !q.Player.MobIsSpawned(Mob.MisareauxCoast.ALSHA))
                    {
                        return QuestReply.ProgressEvent(557).Option(1, KeyItem.MireIncense);
                    }
                    // Clicking on the ??? after killing NM
                    else if (progressVar == 4)
                    {
                        return QuestReply.ProgressEvent(558);
                    }
                    return null;
                })
                .Npc(Zone.MisareauxCoast, Npc.MisareauxCoast.Alsha).OnMobDeath(q =>
                {
                    // Needs backend cpp changes to allow Alsha defeating herself to not allow credit.
                    // optParams.isKiller: q.MobDeath (quest rework P7), onMobDeath's third argument
                    if (q.MobDeath.IsKiller)
                    {
                        // for _, member in ipairs(player:getAlliance()) do if member:getCharVar('Quest[4][78]Prog') == 3 then ...
                        foreach (int memberProg in q.Player.AllianceCharVars("Quest[4][78]Prog"))
                        {
                            if (memberProg == 3)
                            {
                                q.Quest.SetStep(4);
                            }
                        }
                    }
                })
                .OnEventFinish(Zone.MisareauxCoast, 556, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.MisareauxCoast, 557, q =>
                {
                    if (q.Quest.Step == 3 &&
                        q.Player.PopFromQm(Mob.MisareauxCoast.ALSHA, hide: 0, claim: true))
                    {
                        q.Text.Special(Text.MisareauxCoast.FOUL_STENCH_OF_DEATH);
                    }
                })
                .OnEventFinish(Zone.MisareauxCoast, 558, q =>
                {
                    q.Quest.SetStep(5);
                    q.Player.DelKeyItem(KeyItem.MireIncense);
                    q.Player.GiveKeyItem(KeyItem.BetterHumesAndMannequins);
                })
                .Npc(Zone.Mhaura, Npc.Mhaura.FyiChalmwoh).OnTrigger(q =>
                {
                    if (q.Quest.Step == 5)
                    {
                        return QuestReply.ProgressEvent(321)
                            .Option(0, 704)
                            .Option(1, Mannequins.GetMannequins(q))
                            .Option(2, Mannequins.CostPurchase)
                            .Option(3, Mannequins.CostTrade)
                            .Option(4, Mannequins.CostPose)
                            .Option(5, q.Player.Gil)
                            .Option(6, KeyItem.BetterHumesAndMannequins);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Mhaura, 321, q =>
                {
                    q.Quest.Complete();
                });

            // Quest complete
            b.State(QuestStatus.Completed, when: q => q.Player.HasKeyItem(KeyItem.BetterHumesAndMannequins))
                .Npc(Zone.Mhaura, Npc.Mhaura.FyiChalmwoh).OnTrigger(q =>
                {
                    return QuestReply.ProgressEvent(321)
                        .Option(1, Mannequins.GetMannequins(q))
                        .Option(2, Mannequins.CostPurchase)
                        .Option(3, Mannequins.CostTrade)
                        .Option(4, Mannequins.CostPose)
                        .Option(5, q.Player.Gil);
                })
                .OnTrade(q =>
                {
                    // Trade exactly one mannequin without gil.  Gil taken separately.
                    int tradedMannequin = 0;

                    for (int itemId = Item.HumeMMannequin; itemId <= Item.GalkaMannequin; itemId++)
                    {
                        if (q.Trade.HasExactly(itemId))
                        {
                            tradedMannequin = itemId;
                            break;
                        }
                    }

                    if (tradedMannequin != 0)
                    {
                        return QuestReply.ProgressEvent(319)
                            .Option(0, 2)
                            .Option(1, Mannequins.GetMannequins(q)) // Player Mannequin List
                            .Option(2, Mannequins.CostPurchase)
                            .Option(3, Mannequins.CostTrade)
                            .Option(4, 1); // Leaving this out gives a "It's still in pretty good condition" message
                    }
                    return null;
                })
                .OnEventUpdate(Zone.Mhaura, 321, q =>
                {
                    int option = q.Option;
                    if (option == 0)
                    {
                        // Purchase a mannequin
                        int richEnough = 0;
                        if (q.Player.Gil >= Mannequins.CostPurchase)
                        {
                            richEnough = 1;
                        }

                        // player:updateEvent({ [0] = richEnough, [1] = getMannequins, [2] = option }): indexes 0 to 2
                        q.Player.UpdateEvent(richEnough, // Not sure if this is the legitimate use, but it works.
                            Mannequins.GetMannequins(q),
                            option);
                    }
                    else if (option >= 11 &&
                             option <= 18)
                    {
                        // Pose a mannequin
                        int race = option - 10; // From 1 to 8, for consistency in lua
                        q.Player.UpdateEvent(1,
                            Mannequins.GetMannequins(q),
                            option,
                            Mannequins.GetMannequinPose(q, race));
                    }
                })
                .OnEventFinish(Zone.Mhaura, 319, q =>
                {
                    int option = q.Option;
                    // Trade mannequin.  Option = race (1-8)
                    if (option >= 1 &&
                        option <= 8 &&
                        q.Player.DelGilChecked(Mannequins.CostTrade))
                    {
                        q.Trade.Confirm();
                        q.Player.GiveItem(Item.HumeMMannequin + option - 1);
                        int race = LuaMod(option - 1, 8) + 1;
                        Mannequins.SetMannequinPose(q, race, 0);
                    }
                })
                .OnEventFinish(Zone.Mhaura, 321, q =>
                {
                    int option = q.Option;
                    // If the transaction failed, the option is nil.
                    if (q.Player.FreeSlotCount == 0)
                    {
                        q.Text.Special(Text.Mhaura.ITEM_CANNOT_BE_OBTAINED, Item.HumeMMannequin + option - 1);
                    }
                    else if (option >= 1 && // Purchase the mannequin.  Option = race (1-8)
                             option <= 8 &&
                             q.Player.DelGilChecked(Mannequins.CostPurchase))
                    {
                        q.Text.Special(Text.Mhaura.ITEM_OBTAINED, Item.HumeMMannequin + option - 1);
                        q.Player.AddItem(Item.HumeMMannequin + option - 1);
                        int race = LuaMod(option - 1, 8) + 1;
                        Mannequins.SetMannequinPose(q, race, 0);
                    }
                    else if (option >= 10 &&
                             q.Player.DelGilChecked(Mannequins.CostPose))
                    {
                        // Posing a mannequin
                        int race = LuaMod(option - 11, 8) + 1; // 1 to 8 for lua consistency
                        int pose = option / 32; // math.floor(option / 32), option >= 10
                        Mannequins.SetMannequinPose(q, race, pose);
                    }
                });
        }
    }
}
