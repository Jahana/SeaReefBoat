// Carbuncle Debacle (Log ID 2, Quest ID 83; SMN AF3), ported from the Carbuncle Debacle branches of
// scripts/zones/Windurst_Walls/npcs/_6n2.lua and Koru-Moru.lua, scripts/zones/Mhaura/npcs/Ripapa.lua and
// scripts/zones/Rabao/npcs/Agado-Pugado.lua (quest rework B3b, Windurst group 31; call-only).
// _6n2 : !pos -26 -13 260 239; Koru-Moru : !pos -120 -6 124 239; Ripapa : !pos 29 -15 55 249;
// Agado-Pugado : !pos -17 7 -10 247.
// The door _6n2, after Class Reunion's offer (its own C#, which the door's script calls first): The Puppet Master and
// Class Reunion completed, this quest available, main level >= AF3_QUEST_LEVEL, SMN, no needToZone: 415; its finish,
// any option: begun, CarbuncleDebacleProgress 1.
// Koru-Moru, first of his arms (his script calls this quest first): accepted with the var 1 or 2: 416 (its finish:
// the var 2); accepted, the var 4: 417 (its finish: the var 5, the Daze Breaker Charm by npcUtil.giveKeyItem);
// 5: 418; 7: 419 (its finish: a free slot: Evoker's Horn (addItem, ITEM_OBTAINED), Paragon of Summoner Excellence,
// completed, Windurst fame 20, the var 0, needToZone; else ITEM_CANNOT_BE_OBTAINED). The Puppet Master, Class Reunion
// and this quest completed: 420. Else Class Reunion's arms (its own C#, called next), then The Root of the Problem's.
// His default 193 and the door's DOORS_SEALED_SHUT are Default-priority actions, so the lookup takes turns with their
// scripts (the default's turn, the script's turn, ...); the containers there (The Puppet Master, Star Struck,
// Blast from the Past, ...) answer first with their Progress arms, and their Event arms take turns with the scripts.
// Ripapa (Mhaura), before Trial by Lightning's call: accepted, the var 2: 10022; 3 without the Lightning Pendulum:
// 10023 (0, Lightning Pendulum); their finish: a free slot: the pendulum (addItem, ITEM_OBTAINED), the var 3; else
// ITEM_CANNOT_BE_OBTAINED.
// Agado-Pugado (Rabao), before Trial by Wind's call: accepted, the var 5 with the Daze Breaker Charm: 86; 6: without
// the Wind Pendulum 87 (0, Wind Pendulum), with it 88 (no finish); 86's and 87's finish: a free slot: the pendulum
// (addItem, ITEM_OBTAINED), the var 6; else ITEM_CANNOT_BE_OBTAINED.
// The Cloister of Storms (the var 3 -> 4) and Cloister of Gales (6 -> 7, the charm taken) battlefield wins never name
// the quest: they are the battlefields' (Battlefields/CloisterOfStorms/CarbuncleDebacle.cs and CloisterOfGales/
// CarbuncleDebacle.cs, onEventFinishWin), and their BattlefieldQuest entries read this quest's status.
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): 416 wants the quest accepted at the var 1
// or 2; the Lua's `accepted and progress == 1 or progress == 2` (no parentheses) played it at the var 2 in any status,
// also ahead of 420.
// Kept as the Lua has it: 415 begins on any option; no finish reads the status; 416's finish sets the var 2 again
// when it was 2; 417 gives the charm whether held or not (npcUtil.giveKeyItem then gives nothing); the pendulums and
// the horn are rare: a held one fails the addItem, ITEM_OBTAINED is still sent and the var still set (419: the quest
// still completes).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class CarbuncleDebacle : QuestScript
    {
        const string Var = "CarbuncleDebacleProgress";

        public CarbuncleDebacle() : base(QuestLog.Windurst, "CARBUNCLE_DEBACLE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls._6n2).Called()
                    // _6n2.lua onTrigger, the CARBUNCLE DEBACLE arm (after Class Reunion's)
                    .OnTrigger(q =>
                    {
                        if (q.Player.QuestStatus(QuestLog.Windurst, "THE_PUPPET_MASTER") == QuestStatus.Completed &&
                            q.Player.QuestStatus(QuestLog.Windurst, "CLASS_REUNION") == QuestStatus.Completed &&
                            q.Quest.Status == QuestStatus.Available &&
                            q.Player.MainLevel >= q.Settings.MainNumber("AF3_QUEST_LEVEL") &&
                            q.Player.MainJob == Job.Smn &&
                            !q.Player.NeedToZone)
                        {
                            return QuestReply.Event(415);
                        }

                        return null;
                    })
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KoruMoru).Called()
                    // Koru-Moru.lua onTrigger, the Carbuncle Debacle arms (his first)
                    .OnTrigger(q =>
                    {
                        QuestStatus thePuppetMaster = q.Player.QuestStatus(QuestLog.Windurst, "THE_PUPPET_MASTER");
                        QuestStatus classReunion = q.Player.QuestStatus(QuestLog.Windurst, "CLASS_REUNION");
                        QuestStatus carbuncleDebacle = q.Quest.Status;
                        int carbuncleDebacleProgress = q.Player.CharVar(Var);

                        // the Lua's `accepted and progress == 1 or progress == 2` read the var 2 in any status
                        // (fixed, user ruling 2026-11-04)
                        if (carbuncleDebacle == QuestStatus.Accepted &&
                            (carbuncleDebacleProgress == 1 || carbuncleDebacleProgress == 2))
                        {
                            return QuestReply.Event(416); // go and see Ripapa
                        }
                        else if (carbuncleDebacle == QuestStatus.Accepted && carbuncleDebacleProgress == 4)
                        {
                            return QuestReply.Event(417); // now go and see Agado-Pugado
                        }
                        else if (carbuncleDebacle == QuestStatus.Accepted && carbuncleDebacleProgress == 5)
                        {
                            return QuestReply.Event(418); // Uran-Mafran must be stopped
                        }
                        else if (carbuncleDebacle == QuestStatus.Accepted && carbuncleDebacleProgress == 7)
                        {
                            return QuestReply.Event(419); // ending cs
                        }
                        else if (thePuppetMaster == QuestStatus.Completed &&
                            classReunion == QuestStatus.Completed &&
                            carbuncleDebacle == QuestStatus.Completed)
                        {
                            return QuestReply.Event(420); // new cs after all 3 SMN AFs done
                        }

                        return null; // Class Reunion's arms next
                    })
                .Npc(Zone.Mhaura, Npc.Mhaura.Ripapa).Called()
                    // Ripapa.lua onTrigger, the Carbuncle Debacle arms (before Trial by Lightning's call)
                    .OnTrigger(q =>
                    {
                        QuestStatus carbuncleDebacle = q.Quest.Status;
                        int carbuncleDebacleProgress = q.Player.CharVar(Var);

                        if (carbuncleDebacle == QuestStatus.Accepted && carbuncleDebacleProgress == 2)
                        {
                            return QuestReply.Event(10022); // get the lighning pendulum lets go to Cloister of Storms
                        }
                        else if (carbuncleDebacle == QuestStatus.Accepted &&
                            carbuncleDebacleProgress == 3 &&
                            !q.Player.HasItem(Item.LightningPendulum))
                        {
                            // "lost the pendulum?"
                            return QuestReply.Event(10023, 0, Item.LightningPendulum, 0, 0, 0, 0, 0, 0);
                        }

                        return null; // Trial by Lightning's call next
                    })
                .Npc(Zone.Rabao, Npc.Rabao.AgadoPugado).Called()
                    // Agado-Pugado.lua onTrigger, the Carbuncle Debacle arms (before Trial by Wind's call)
                    .OnTrigger(q =>
                    {
                        QuestStatus carbuncleDebacle = q.Quest.Status;
                        int carbuncleDebacleProgress = q.Player.CharVar(Var);

                        if (carbuncleDebacle == QuestStatus.Accepted &&
                            carbuncleDebacleProgress == 5 &&
                            q.Player.HasKeyItem(KeyItem.DazeBreakerCharm))
                        {
                            return QuestReply.Event(86); // get the wind pendulum, lets go to Cloister of Gales
                        }
                        else if (carbuncleDebacle == QuestStatus.Accepted && carbuncleDebacleProgress == 6)
                        {
                            if (!q.Player.HasItem(Item.WindPendulum))
                            {
                                // "lost the pendulum?" This one too~???
                                return QuestReply.Event(87, 0, Item.WindPendulum, 0, 0, 0, 0, 0, 0);
                            }
                            else
                            {
                                return QuestReply.Event(88); // reminder to go to Cloister of Gales
                            }
                        }

                        return null; // Trial by Wind's call next
                    });

            b.AnyState()
                // _6n2.lua onEventFinish 415
                .OnEventFinish(Zone.WindurstWalls, 415, q =>
                {
                    q.Quest.Begin();
                    q.Player.SetCharVar(Var, 1);
                })
                // Koru-Moru.lua onEventFinish 416 / 417 / 419
                .OnEventFinish(Zone.WindurstWalls, 416, q =>
                {
                    q.Player.SetCharVar(Var, 2);
                })
                .OnEventFinish(Zone.WindurstWalls, 417, q =>
                {
                    q.Player.SetCharVar(Var, 5);
                    q.Player.GiveKeyItem(KeyItem.DazeBreakerCharm);
                })
                .OnEventFinish(Zone.WindurstWalls, 419, q =>
                {
                    if (q.Player.FreeSlotCount != 0)
                    {
                        q.Player.AddItem(Item.EvokersHorn); // Evoker's Horn
                        q.Text.Special(Text.WindurstWalls.ITEM_OBTAINED, Item.EvokersHorn);
                        q.Player.AddTitle(Title.ParagonOfSummonerExcellence);
                        // the raw player:completeQuest: not quest:complete, whose cleanup would clear the quest's own
                        // Quest[2][83] vars
                        q.Player.CompleteQuest(QuestLog.Windurst, "CARBUNCLE_DEBACLE");
                        q.Player.AddFame(FameArea.Windurst, 20);
                        q.Player.SetCharVar(Var, 0);
                        q.Player.SetNeedToZone(true);
                    }
                    else
                    {
                        q.Text.Special(Text.WindurstWalls.ITEM_CANNOT_BE_OBTAINED, Item.EvokersHorn);
                    }
                })
                // Ripapa.lua onEventFinish 10022 / 10023
                .OnEventFinish(Zone.Mhaura, 10022, q => GiveLightningPendulum(q))
                .OnEventFinish(Zone.Mhaura, 10023, q => GiveLightningPendulum(q))
                // Agado-Pugado.lua onEventFinish 86 / 87
                .OnEventFinish(Zone.Rabao, 86, q => GiveWindPendulum(q))
                .OnEventFinish(Zone.Rabao, 87, q => GiveWindPendulum(q));
        }

        // Ripapa.lua onEventFinish, `csid == 10022 or csid == 10023`
        static void GiveLightningPendulum(IQuestContext q)
        {
            if (q.Player.FreeSlotCount != 0)
            {
                q.Player.AddItem(Item.LightningPendulum);
                q.Text.Special(Text.Mhaura.ITEM_OBTAINED, Item.LightningPendulum);
                q.Player.SetCharVar(Var, 3);
            }
            else
            {
                q.Text.Special(Text.Mhaura.ITEM_CANNOT_BE_OBTAINED, Item.LightningPendulum);
            }
        }

        // Agado-Pugado.lua onEventFinish, `csid == 86 or csid == 87`
        static void GiveWindPendulum(IQuestContext q)
        {
            if (q.Player.FreeSlotCount != 0)
            {
                q.Player.AddItem(Item.WindPendulum);
                q.Text.Special(Text.Rabao.ITEM_OBTAINED, Item.WindPendulum);
                q.Player.SetCharVar(Var, 6);
            }
            else
            {
                q.Text.Special(Text.Rabao.ITEM_CANNOT_BE_OBTAINED, Item.WindPendulum);
            }
        }
    }
}
