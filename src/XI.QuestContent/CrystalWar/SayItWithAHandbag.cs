// Say It with a Handbag (Log ID 7, Quest ID 41), ported from the quest branches of scripts/zones/Windurst_Waters_[S]/
// npcs/Hampu-Kampu.lua and Door_Acolyte_Hostel_up.lua and scripts/zones/Southern_San_dOria/npcs/Kipopo.lua (quest
// rework B3b Crystal War, draft; call-only). Hampu-Kampu !pos -115.597 -1 -158.703 94; Door: Acolyte Hostel (up) !pos
// 146.619 -8.525 242.874 94; Kipopo !pos -191.05 -1.15 12.285 230 (the first Kipopo, 17719381). The char vars
// sayItWithAHandbagCS and sayItWithAHandbagBonusCS (no quest prefix).
// Hampu-Kampu, in his order: completed: 175; the Repaired Handbag and CS 4: 174 (finish, any option:
// npcUtil.completeQuest: the Trainee's Needle, CS deleted; done: the handbag taken, BonusCS 1); the Torn Patches of
// Leather or CS 3: 173; accepted, CS 1: 172 (finish: the patches); accepted: 170; available: 169 (finish: the quest
// added); else nothing. The door: accepted, CS 0: 171 (finish, option 1: CS 1); else nothing. Kipopo (trade): the
// patches, CS 2, a Square of Laminated Ram Leather, a Square of Sheep Leather and a Spool of Silk Thread alone: 910
// (finish: the patches taken, CS 3, needToZone, confirmed); (talk), in his order: completed and BonusCS 1: 914 (finish:
// BonusCS 0); the handbag and CS 4: 913; CS 3: needToZone: 911; else 912 (finish: the handbag, CS 4); CS 2: 909; the
// patches and CS 1: 908 (finish, option 1: CS 2); else his script's image support (xi.crafting).
// Kept as the Lua has it: Kipopo's and Hampu-Kampu's middle arms read no status (a GM's CS opens them); 173 answers the
// patches before 172 (so 172 plays once: its finish gives the patches); 174's finish ignores the option; Kipopo's 908
// finish on another option falls to the image support's finish (the patched script calls the quest for 908 only on
// option 1).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class SayItWithAHandbag : QuestScript
    {
        public SayItWithAHandbag() : base(QuestLog.CrystalWar, "SAY_IT_WITH_A_HANDBAG") { }

        // xi.item has no name for 2012 (square_of_laminated_ram_leather in item_basic)
        const int SquareOfLaminatedRamLeather = 2012;

        protected override void Build(QuestScriptBuilder b)
        {
            // Hampu-Kampu.lua onTrigger, in its order
            b.AnyState()
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.HampuKampu).Called().OnTrigger(q =>
                {
                    QuestStatus sayItWithAHandbag = q.Quest.Status;
                    int sayItWithAHandbagCS = q.Player.CharVar("sayItWithAHandbagCS");

                    if (sayItWithAHandbag == QuestStatus.Completed)
                    {
                        return QuestReply.Event(175);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.RepairedHandbag) &&
                        sayItWithAHandbagCS == 4)
                    {
                        return QuestReply.Event(174);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.TornPatchesOfLeather) ||
                        sayItWithAHandbagCS == 3)
                    {
                        return QuestReply.Event(173);
                    }
                    else if (sayItWithAHandbag == QuestStatus.Accepted &&
                        sayItWithAHandbagCS == 1)
                    {
                        return QuestReply.Event(172);
                    }
                    else if (sayItWithAHandbag == QuestStatus.Accepted)
                    {
                        return QuestReply.Event(170);
                    }
                    else if (sayItWithAHandbag == QuestStatus.Available)
                    {
                        return QuestReply.Event(169);
                    }
                    return null;
                });

            // Door_Acolyte_Hostel_up.lua onTrigger
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar("sayItWithAHandbagCS") == 0)
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.DoorAcolyteHostelup).Called().OnTrigger(QuestReply.Event(171));

            // Kipopo.lua onTrade
            b.AnyState()
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Kipopo).Called().OnTrade(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.TornPatchesOfLeather) &&
                        q.Player.CharVar("sayItWithAHandbagCS") == 2 &&
                        q.Trade.HasExactly(SquareOfLaminatedRamLeather, Item.SquareOfSheepLeather, Item.SpoolOfSilkThread))
                    {
                        return QuestReply.Event(910);
                    }
                    return null;
                })
                // Kipopo.lua onTrigger, in its order (the image support is the script's fallback)
                .OnTrigger(q =>
                {
                    int sayItWithAHandbagCS = q.Player.CharVar("sayItWithAHandbagCS");

                    if (q.Quest.Status == QuestStatus.Completed &&
                        q.Player.CharVar("sayItWithAHandbagBonusCS") == 1)
                    {
                        return QuestReply.Event(914);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.RepairedHandbag) &&
                        sayItWithAHandbagCS == 4)
                    {
                        return QuestReply.Event(913);
                    }
                    else if (sayItWithAHandbagCS == 3)
                    {
                        if (q.Player.NeedToZone)
                        {
                            return QuestReply.Event(911);
                        }
                        else
                        {
                            return QuestReply.Event(912);
                        }
                    }
                    else if (sayItWithAHandbagCS == 2)
                    {
                        return QuestReply.Event(909);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.TornPatchesOfLeather) &&
                        sayItWithAHandbagCS == 1)
                    {
                        return QuestReply.Event(908);
                    }
                    return null; // the script's xi.crafting.oldImageSupportOnTrigger
                });

            // the three scripts' onEventFinish (Kipopo's script calls the quest for 914 / 912 / 910 / 908 option 1
            // only)
            b.AnyState()
                .OnEventFinish(Zone.WindurstWatersS, 174, q =>
                {
                    // Option doesn't matter as NPC will take key item if yes or no
                    // npcUtil.completeQuest step for step, in its order, ending in the raw player:completeQuest: not
                    // quest:complete, whose cleanup would clear the quest's own Quest[7][41] vars
                    if (q.Player.GiveItem(Item.TraineesNeedle)) // 19110
                    {
                        q.Player.SetCharVar("sayItWithAHandbagCS", 0);
                        q.Player.CompleteQuest(QuestLog.CrystalWar, "SAY_IT_WITH_A_HANDBAG");
                        q.Player.DelKeyItem(KeyItem.RepairedHandbag);
                        q.Player.SetCharVar("sayItWithAHandbagBonusCS", 1);
                    }
                })
                .OnEventFinish(Zone.WindurstWatersS, 172, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.TornPatchesOfLeather);
                })
                .OnEventFinish(Zone.WindurstWatersS, 169, q =>
                {
                    q.Quest.Begin();
                })
                .OnEventFinish(Zone.WindurstWatersS, 171, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.SetCharVar("sayItWithAHandbagCS", 1);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 914, q =>
                {
                    q.Player.SetCharVar("sayItWithAHandbagBonusCS", 0);
                })
                .OnEventFinish(Zone.SouthernSanDoria, 912, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.RepairedHandbag);
                    q.Player.SetCharVar("sayItWithAHandbagCS", 4);
                })
                .OnEventFinish(Zone.SouthernSanDoria, 910, q =>
                {
                    q.Player.DelKeyItem(KeyItem.TornPatchesOfLeather);
                    q.Player.SetCharVar("sayItWithAHandbagCS", 3);
                    q.Player.SetNeedToZone(true);
                    q.Trade.Confirm();
                })
                .OnEventFinish(Zone.SouthernSanDoria, 908, q =>
                {
                    // the script calls this only on option 1 (its `csid == 908 and option == 1` arm)
                    q.Player.SetCharVar("sayItWithAHandbagCS", 2);
                });
        }
    }
}
