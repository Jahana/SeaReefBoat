// Class Reunion (Log ID 2, Quest ID 82; SMN AF2), ported from the Class Reunion branches of
// scripts/zones/Windurst_Walls/npcs/_6n2.lua, Koru-Moru.lua and Shantotto.lua,
// scripts/zones/Windurst_Waters/npcs/Fuepepe.lua and Furakku-Norakku.lua, and
// scripts/zones/Northern_San_dOria/npcs/Gulmama.lua (quest rework B3b, Windurst group 31; call-only).
// _6n2 : !pos -26 -13 260 239; Koru-Moru : !pos -120 -6 124 239; Shantotto : !pos 122 -2 112 239;
// Fuepepe : !pos 161 -2 161 238; Furakku-Norakku : !pos -19 -5 101 238; Gulmama : !pos -186 0 107 231.
// The door _6n2 (the House of the Hero): The Puppet Master completed, this quest available, main level
// >= AF2_QUEST_LEVEL, SMN, no needToZone: 413; its finish, any option: begun, Carbuncle's Tear
// (npcUtil.giveKeyItem), ClassReunionProgress 1. Carbuncle Debacle's offer comes after (its own C#, which the door's
// script calls next).
// Koru-Moru, after Carbuncle Debacle's arms (its own C#, which his script calls first), accepted: the var 1:
// 412 (0, 450, Astragalos) (its finish: the tear taken, the var 2); 2: 414 (0, 0, Astragalos); 3 or more with a
// student not talked to: 408; 6 with both talked to: 410 (its finish: a free slot: Evoker's Spats
// (addItem, ITEM_OBTAINED), completed, the three vars 0, needToZone, Windurst fame 20; else ITEM_CANNOT_BE_OBTAINED).
// The Puppet Master and this quest completed: 411. Else The Root of the Problem's arm (its own C#, called last).
// His trade: his script checks four Astragaloi, nothing else and no gil, then calls this quest: accepted and the
// var 2: 407 (its finish: the trade taken, the var 3).
// His default 193 (DefaultActions) is a Default-priority action, so the lookup takes turns with his script (the
// default's turn, the script's turn, ...); so do the door's DOORS_SEALED_SHUT, Fuepepe's 423 and Furakku-Norakku's
// 371, and Shantotto's: Curses, Foiled Again! (1)'s offer 171 (an Event) takes her turns while that quest is open,
// her default 164 otherwise. The containers at these NPCs (The Puppet Master at the door and at Koru-Moru, Star
// Struck, Blast from the Past, Making the Grade at Koru-Moru and Fuepepe, ...) answer first with their Progress arms;
// their Event arms take turns with the scripts.
// Shantotto: accepted, the var 3: 409 (its finish: the var 4). Fuepepe: accepted, the var 3 or more, not talked to:
// 817 (its finish: ClassReunion_TalkedToFupepe 1). Furakku-Norakku: the same with his own var, 816 (its finish:
// ClassReunion_TalkedToFurakku 1). Gulmama, before Trial by Ice's call: accepted, the var 4: 713 (0, Ice Pendulum);
// the var 5 without the pendulum: 712 (0, Ice Pendulum); their finish: a free slot: the pendulum
// (addItem, ITEM_OBTAINED), the var 5; else ITEM_CANNOT_BE_OBTAINED.
// The Cloister of Frost battlefield's win (class_reunion.lua onEventFinishWin: the var 5 -> 6) never names the quest:
// it stays in Lua (its BattlefieldQuest entry reads this quest's status there).
// Kept as the Lua has it: 413 begins on any option; no finish reads the status; Furakku-Norakku's and Gulmama's
// scripts compare the status with the literal 1 (QUEST_ACCEPTED); a talk var other than 1 counts as not talked to;
// the var 3, 4, 5, or 7 and up with both students talked to plays nothing of this quest at Koru-Moru (his other arms
// then); 412 takes the tear whether held or not; 713 plays at the var 4 whether the pendulum is held or not; the
// pendulum and the spats are rare: a held one fails the addItem, ITEM_OBTAINED is still sent and the var still set
// (410: the quest still completes).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class ClassReunion : QuestScript
    {
        const string Var = "ClassReunionProgress";
        const string TalkedToFupepe = "ClassReunion_TalkedToFupepe";
        const string TalkedToFurakku = "ClassReunion_TalkedToFurakku";

        public ClassReunion() : base(QuestLog.Windurst, "CLASS_REUNION") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls._6n2).Called()
                    // _6n2.lua onTrigger, the CLASS REUNION arm
                    .OnTrigger(q =>
                    {
                        if (q.Player.QuestStatus(QuestLog.Windurst, "THE_PUPPET_MASTER") == QuestStatus.Completed &&
                            q.Quest.Status == QuestStatus.Available &&
                            q.Player.MainLevel >= q.Settings.MainNumber("AF2_QUEST_LEVEL") &&
                            q.Player.MainJob == Job.Smn &&
                            !q.Player.NeedToZone)
                        {
                            return QuestReply.Event(413);
                        }

                        return null; // Carbuncle Debacle's offer next
                    })
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KoruMoru).Called()
                    // Koru-Moru.lua onTrade, the Astragalos arm (his script checks the trade, then calls)
                    .OnTrade(q =>
                    {
                        if (q.Quest.Status == QuestStatus.Accepted && q.Player.CharVar(Var) == 2)
                        {
                            // now Koru remembers something that you need to inquire his former students.
                            return QuestReply.Event(407);
                        }

                        return null;
                    })
                    // Koru-Moru.lua onTrigger, the Class Reunion arms (after Carbuncle Debacle's)
                    .OnTrigger(q =>
                    {
                        QuestStatus thePuppetMaster = q.Player.QuestStatus(QuestLog.Windurst, "THE_PUPPET_MASTER");
                        QuestStatus classReunion = q.Quest.Status;
                        int classReunionProgress = q.Player.CharVar(Var);
                        int talk1 = q.Player.CharVar(TalkedToFupepe);
                        int talk2 = q.Player.CharVar(TalkedToFurakku);

                        if (classReunion == QuestStatus.Accepted && classReunionProgress == 1)
                        {
                            // bring Koru 4 astragaloi
                            return QuestReply.Event(412, 0, 450, Item.Astragalos, 0, 0, 0, 0, 0);
                        }
                        else if (classReunion == QuestStatus.Accepted && classReunionProgress == 2)
                        {
                            // reminder to bring 4 astragaloi
                            return QuestReply.Event(414, 0, 0, Item.Astragalos, 0, 0, 0, 0, 0);
                        }
                        else if (classReunion == QuestStatus.Accepted &&
                            classReunionProgress >= 3 &&
                            (talk1 != 1 || talk2 != 1))
                        {
                            return QuestReply.Event(408); // reminder to visit the students
                        }
                        else if (classReunion == QuestStatus.Accepted &&
                            classReunionProgress == 6 &&
                            talk1 == 1 &&
                            talk2 == 1)
                        {
                            return QuestReply.Event(410); // ending cs
                        }
                        else if (thePuppetMaster == QuestStatus.Completed && classReunion == QuestStatus.Completed)
                        {
                            return QuestReply.Event(411); // new cs after completed AF2
                        }

                        return null; // The Root of the Problem's arm next
                    })
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.Shantotto).Called()
                    // Shantotto.lua onTrigger
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Status == QuestStatus.Accepted && q.Player.CharVar(Var) == 3)
                        {
                            return QuestReply.Event(409); // she mentions that Sunny-Pabonny left for San d'Oria
                        }

                        return null;
                    })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Fuepepe).Called()
                    // Fuepepe.lua onTrigger, the CLASS REUNION arm
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Status == QuestStatus.Accepted &&
                            q.Player.CharVar(Var) >= 3 &&
                            q.Player.CharVar(TalkedToFupepe) != 1)
                        {
                            return QuestReply.Event(817); // he tells you about Uran-Mafran
                        }

                        return null;
                    })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.FurakkuNorakku).Called()
                    // Furakku-Norakku.lua onTrigger, the Class Reunion arm (`classReunion == 1`: QUEST_ACCEPTED)
                    .OnTrigger(q =>
                    {
                        QuestStatus classReunion = q.Quest.Status;
                        int classReunionProgress = q.Player.CharVar(Var);
                        int talk2 = q.Player.CharVar(TalkedToFurakku);

                        if (classReunion == QuestStatus.Accepted && classReunionProgress >= 3 && talk2 != 1)
                        {
                            return QuestReply.Event(816); // he tells you about Uran-Mafran
                        }

                        return null;
                    })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Gulmama).Called()
                    // Gulmama.lua onTrigger, the Class Reunion arms (`classReunion == 1`; before Trial by Ice's call)
                    .OnTrigger(q =>
                    {
                        QuestStatus classReunion = q.Quest.Status;
                        int classReunionProgress = q.Player.CharVar(Var);

                        if (classReunion == QuestStatus.Accepted && classReunionProgress == 4)
                        {
                            // he gives you an ice pendulum and wants you to go to Cloister of Frost
                            return QuestReply.Event(713, 0, Item.IcePendulum, 0, 0, 0, 0, 0, 0);
                        }
                        else if (classReunion == QuestStatus.Accepted &&
                            classReunionProgress == 5 &&
                            !q.Player.HasItem(Item.IcePendulum))
                        {
                            // lost the ice pendulum need another one
                            return QuestReply.Event(712, 0, Item.IcePendulum, 0, 0, 0, 0, 0, 0);
                        }

                        return null; // Trial by Ice's call next
                    });

            b.AnyState()
                // _6n2.lua onEventFinish 413
                .OnEventFinish(Zone.WindurstWalls, 413, q =>
                {
                    q.Quest.Begin();
                    q.Player.GiveKeyItem(KeyItem.CarbunclesTear);
                    q.Player.SetCharVar(Var, 1);
                })
                // Koru-Moru.lua onEventFinish 412 / 407 / 410
                .OnEventFinish(Zone.WindurstWalls, 412, q =>
                {
                    q.Player.DelKeyItem(KeyItem.CarbunclesTear);
                    q.Player.SetCharVar(Var, 2);
                })
                .OnEventFinish(Zone.WindurstWalls, 407, q =>
                {
                    q.Trade.Complete();
                    q.Player.SetCharVar(Var, 3);
                })
                .OnEventFinish(Zone.WindurstWalls, 410, q =>
                {
                    if (q.Player.FreeSlotCount != 0)
                    {
                        q.Player.AddItem(Item.EvokersSpats);
                        q.Text.Special(Text.WindurstWalls.ITEM_OBTAINED, Item.EvokersSpats);
                        // the raw player:completeQuest: not quest:complete, whose cleanup would clear the quest's own
                        // Quest[2][82] vars
                        q.Player.CompleteQuest(QuestLog.Windurst, "CLASS_REUNION");
                        q.Player.SetCharVar(Var, 0);
                        q.Player.SetCharVar(TalkedToFurakku, 0);
                        q.Player.SetCharVar(TalkedToFupepe, 0);
                        q.Player.SetNeedToZone(true);
                        q.Player.AddFame(FameArea.Windurst, 20);
                    }
                    else
                    {
                        q.Text.Special(Text.WindurstWalls.ITEM_CANNOT_BE_OBTAINED, Item.EvokersSpats);
                    }
                })
                // Shantotto.lua onEventFinish 409
                .OnEventFinish(Zone.WindurstWalls, 409, q =>
                {
                    q.Player.SetCharVar(Var, 4);
                })
                // Fuepepe.lua onEventFinish 817, Furakku-Norakku.lua onEventFinish 816
                .OnEventFinish(Zone.WindurstWaters, 817, q =>
                {
                    q.Player.SetCharVar(TalkedToFupepe, 1);
                })
                .OnEventFinish(Zone.WindurstWaters, 816, q =>
                {
                    q.Player.SetCharVar(TalkedToFurakku, 1);
                })
                // Gulmama.lua onEventFinish 713 / 712
                .OnEventFinish(Zone.NorthernSanDoria, 713, q => GivePendulum(q))
                .OnEventFinish(Zone.NorthernSanDoria, 712, q => GivePendulum(q));
        }

        // Gulmama.lua onEventFinish, `csid == 713 or csid == 712`
        static void GivePendulum(IQuestContext q)
        {
            if (q.Player.FreeSlotCount != 0)
            {
                q.Player.AddItem(Item.IcePendulum);
                q.Text.Special(Text.NorthernSanDoria.ITEM_OBTAINED, Item.IcePendulum);
                q.Player.SetCharVar(Var, 5);
            }
            else
            {
                q.Text.Special(Text.NorthernSanDoria.ITEM_CANNOT_BE_OBTAINED, Item.IcePendulum);
            }
        }
    }
}
