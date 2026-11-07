// Out of the Depths (Log ID 1, Quest ID 75), a line-for-line port of scripts/quests/bastok/Out_of_the_Depths.lua (quest
// rework B3b). Ayame : !pos 132.904 -18.983 34.106 237; Ravorara : !pos -150.614 -6.000 -8.320 236; Brakobrik : !pos
// 165.573 12.000 -89.774 11; Pavvke : !pos 15.820 6.985 -14.324 234. Ayame's 859 (Bastok fame level 3) begins it on
// option 3; Ravorara's 307 sets Prog 1; Brakobrik's cutscene 2 (option 2) sets Prog 2; Pinches of Hoary Bomb Ash traded to
// Brakobrik (1 to 4) give, through cutscene 4, the KI Dusty Tome / Pointed Jug / Cracked Club / Peeling Hairpin; each KI
// shown to Ravorara (309, in that order) is taken for 100 / 200 / 300 / 400 gil and sets its bit of Option; with all four
// bits (Option 15) one ash gives the KI Old Nametag and Prog 3; the nametag at Ravorara (309, option 5) sets Prog 4;
// Pavvke's 238 completes it (1200 gil, Trash Collector, fame +80). A Question of Faith (C#) needs it completed; Dalba
// (Port Bastok) reads its completion (a status read).
// Kept as the Lua file has it: a KI already held makes cutscene 4 answer option 6 (the ash taken, REFUSES_TO_GIVE_ANOTHER);
// cutscene 4 with option 5 when Option is not 15 does nothing; Ravorara's 309 with any option other than 1-5 does nothing.
using System;
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Bastok
{
    public sealed class OutOfTheDepths : QuestScript
    {
        public OutOfTheDepths() : base(QuestLog.Bastok, "OUT_OF_THE_DEPTHS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/bastok/Out_of_the_Depths";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 80, FameArea = FameArea.Bastok, Gil = 1200, Title = Title.TrashCollector });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Bastok) >= 3)
                .Npc(Zone.Metalworks, Npc.Metalworks.Ayame).OnTrigger(QuestReply.ProgressEvent(859))
                .OnEventFinish(Zone.Metalworks, 859, q =>
                {
                    if (q.Option == 3)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Metalworks, Npc.Metalworks.Ayame).OnTrigger(QuestReply.Event(860).SetPriority(101)) // Reminder
                .Npc(Zone.PortBastok, Npc.PortBastok.Ravorara).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (progress == 0)
                    {
                        return QuestReply.ProgressEvent(307);
                    }
                    else if (progress <= 3 && KiCheck(q))
                    {
                        // Always checks the KIs in this order
                        if (q.Player.HasKeyItem(KeyItem.DustyTome))
                        {
                            return QuestReply.ProgressEvent(309, 0, 1, 0, KeyItem.DustyTome);
                        }
                        else if (q.Player.HasKeyItem(KeyItem.PointedJug))
                        {
                            return QuestReply.ProgressEvent(309, 0, 2, 0, KeyItem.PointedJug);
                        }
                        else if (q.Player.HasKeyItem(KeyItem.CrackedClub))
                        {
                            return QuestReply.ProgressEvent(309, 0, 3, 0, KeyItem.CrackedClub);
                        }
                        else if (q.Player.HasKeyItem(KeyItem.PeelingHairpin))
                        {
                            return QuestReply.ProgressEvent(309, 0, 4, 0, KeyItem.PeelingHairpin);
                        }
                        // (unreachable: KiCheck is true, so one of the four is held; Lua returns nil here)
                        return null;
                    }
                    else if (progress == 3 && !KiCheck(q) && q.Player.HasKeyItem(KeyItem.OldNametag))
                    {
                        return QuestReply.ProgressEvent(309, 0, 5, 0, KeyItem.OldNametag);
                    }
                    else if (progress == 4)
                    {
                        return QuestReply.Event(311);
                    }
                    else
                    {
                        return QuestReply.Event(308);
                    }
                })
                .OnEventFinish(Zone.PortBastok, 307, q => q.Quest.SetStep(1))
                .OnEventFinish(Zone.PortBastok, 309, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.DelKeyItem(KeyItem.DustyTome);
                        q.Quest.SetVarBit("Option", 0);
                        q.Player.GiveCurrency("gil", 100);
                    }
                    else if (q.Option == 2)
                    {
                        q.Player.DelKeyItem(KeyItem.PointedJug);
                        q.Quest.SetVarBit("Option", 1);
                        q.Player.GiveCurrency("gil", 200);
                    }
                    else if (q.Option == 3)
                    {
                        q.Player.DelKeyItem(KeyItem.CrackedClub);
                        q.Quest.SetVarBit("Option", 2);
                        q.Player.GiveCurrency("gil", 300);
                    }
                    else if (q.Option == 4)
                    {
                        q.Player.DelKeyItem(KeyItem.PeelingHairpin);
                        q.Quest.SetVarBit("Option", 3);
                        q.Player.GiveCurrency("gil", 400);
                    }
                    else if (q.Option == 5)
                    {
                        q.Player.DelKeyItem(KeyItem.OldNametag);
                        q.Quest.SetStep(4);
                    }
                })
                // Ensures this plays before the start of the quest "Fallen Comrades" as observed in captures.
                .Npc(Zone.BastokMines, Npc.BastokMines.Pavvke).OnTrigger(q =>
                    q.Quest.Step == 4 ? QuestReply.ProgressEvent(238).SetPriority(1005) : null)
                .OnEventFinish(Zone.BastokMines, 238, q => q.Quest.Complete())
                .Npc(Zone.OldtonMovalpolos, Npc.OldtonMovalpolos.Brakobrik)
                    .OnTrigger(q =>
                    {
                        int progress = q.Quest.Step;

                        if (progress == 1)
                        {
                            return QuestReply.ProgressCutscene(2, 0, Item.PinchOfHoaryBombAsh);
                        }
                        else if (progress == 2)
                        {
                            return QuestReply.MessageSpecial(Text.OldtonMovalpolos.KILLING_BOMBS, 0, Item.PinchOfHoaryBombAsh);
                        }
                        return null;
                    })
                    .OnTrade(q =>
                    {
                        int bombAshCount = Math.Clamp(q.Trade.ItemQty(Item.PinchOfHoaryBombAsh), 0, 4); // utils.clamp
                        int option = q.Quest.Var("Option");
                        int dustyTome = q.Player.HasKeyItem(KeyItem.DustyTome) ? 1 : 0;

                        if (q.Quest.Step == 2 && bombAshCount > 0)
                        {
                            if (q.Trade.Matches((Item.PinchOfHoaryBombAsh, bombAshCount)))
                            {
                                // Player has traded 1, 2, 3 or 4 ash and does not have associated KI.
                                if ((bombAshCount == 4 && !q.Player.HasKeyItem(KeyItem.PeelingHairpin)) ||
                                    (bombAshCount == 3 && !q.Player.HasKeyItem(KeyItem.CrackedClub)) ||
                                    (bombAshCount == 2 && !q.Player.HasKeyItem(KeyItem.PointedJug)) ||
                                    (bombAshCount == 1 && !q.Player.HasKeyItem(KeyItem.DustyTome) && option < 15))
                                {
                                    return QuestReply.ProgressCutscene(4, 0, 0, bombAshCount); // Both selections return option = #bombAsh
                                }
                                // Depending on where the player is in the quest this interaction changes
                                else if (bombAshCount == 1 && option == 15)
                                {
                                    return QuestReply.ProgressCutscene(4, 5, 4, 1, 1, dustyTome); // Selection 1 returns 1 selection 2 returns 5
                                }
                                // Player has the key item for the number of ash traded
                                else
                                {
                                    return QuestReply.ProgressCutscene(4, 0, 0, 0, 0, 1); // Both selections return option 6
                                }
                            }
                        }
                        return null;
                    })
                .OnEventFinish(Zone.OldtonMovalpolos, 2, q =>
                {
                    if (q.Option == 2)
                    {
                        q.Quest.SetStep(2);
                    }
                })
                .OnEventFinish(Zone.OldtonMovalpolos, 4, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Trade.Complete();
                        q.Player.GiveKeyItem(KeyItem.DustyTome);
                    }
                    else if (q.Option == 2)
                    {
                        q.Trade.Complete();
                        q.Player.GiveKeyItem(KeyItem.PointedJug);
                    }
                    else if (q.Option == 3)
                    {
                        q.Trade.Complete();
                        q.Player.GiveKeyItem(KeyItem.CrackedClub);
                    }
                    else if (q.Option == 4)
                    {
                        q.Trade.Complete();
                        q.Player.GiveKeyItem(KeyItem.PeelingHairpin);
                    }
                    else if (q.Option == 5 && q.Quest.Var("Option") == 15)
                    {
                        q.Trade.Complete();
                        q.Player.GiveKeyItem(KeyItem.OldNametag);
                        q.Quest.SetStep(3);
                    }
                    else if (q.Option == 6)
                    {
                        q.Trade.Complete();
                        q.Text.Special(Text.OldtonMovalpolos.REFUSES_TO_GIVE_ANOTHER);
                    }
                });
        }

        // kiCheck(player): any of the four key items held
        static bool KiCheck(IQuestContext q) =>
            q.Player.HasKeyItem(KeyItem.DustyTome) ||
            q.Player.HasKeyItem(KeyItem.PointedJug) ||
            q.Player.HasKeyItem(KeyItem.CrackedClub) ||
            q.Player.HasKeyItem(KeyItem.PeelingHairpin);
    }
}
