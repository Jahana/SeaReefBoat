// Making Headlines (Log ID 2, Quest ID 37), ported from the quest branches of
// scripts/zones/Windurst_Waters/npcs/Naiko-Paneiko.lua, Kyume-Romeh.lua, scripts/zones/Port_Windurst/npcs/Yujuju.lua,
// scripts/zones/Windurst_Walls/npcs/Hiwon-Biwon.lua, scripts/zones/Windurst_Woods/npcs/Umumu.lua and
// scripts/zones/Inner_Horutoto_Ruins/npcs/_5ca.lua (quest rework B3b, Windurst group 26; call-only). Naiko-Paneiko :
// !pos -246 -5 -308 238; Kyume-Romeh : !pos -58 -4 23 238; Yujuju : !pos 201.523 -4.785 138.978 240; Umumu : !pos
// 32.575 -5.250 141.372 241; Mahogany Door (_5ca) : !pos -11 0 20 192. QuestMakingHeadlines_var is a bitmask: bit 0
// Kyume-Romeh, 1 Yujuju, 2 Hiwon-Biwon, 3 Umumu (the four scoops), 4 the Mahogany Door. Naiko-Paneiko: available 665
// (its finish, any option: begun); accepted: the four scoop bits not all set: 666 or 671 (math.randomInt(1, 100) <=
// 50); else bit 4 not set: 673; else 674 or 670 (the roll). 670 / 674's finish: npcUtil.completeQuest's steps
// (560 x GIL_RATE gil with GIL_OBTAINED, Editor's Hatchet Man, the var cleared, the raw player:completeQuest, so the
// quest's Quest[2][37] vars stay), then the four scoops taken. Completed: his 663 (kept in his script). Kyume-Romeh
// (after his Hat in Hand show-off arm, which stays in his script), accepted: bit 0 not set 668 (its finish: the
// Windurst Waters Scoop, bit 0), else 669. Yujuju, accepted: bit 1 not set 314 (its finish: the Port Windurst Scoop,
// bit 1), else 315. Hiwon-Biwon, accepted: bit 2 not set: Curses, Foiled Again! accepted: 283 or 284 (the roll), else
// 281 (their finish: the Windurst Walls Scoop, bit 2); bit 2 set: 282. Umumu, accepted: bit 4 set 383; bit 3 not set
// 381 (its finish: the Windurst Woods Scoop, bit 3); else 382; completed: 385, 386 or 414 (math.randomInt(1, 3)). The
// Mahogany Door, accepted, the four scoop bits set and bit 4 not: CAT_BURGLARS_HIDEOUT (1, the Windurst Woods Scoop)
// and bit 4, and no event (NoAction; its script ignores the reply). The door has a default action (DefaultActions 44):
// the lookup's alternation runs the script every other talk, and plays the 44 after a script turn that starts no event.
// Kept as the Lua has it: the door's message names the Windurst Woods Scoop whatever was found; 665 begins on any
// option; the scoops are taken after the completion whatever it returned; the finishes read no status.
using System;
using XI.Quests;
using XI.Quests.Authoring;
using BastokWildcat = XI.QuestContent.Bastok.LureOfTheWildcat;

namespace XI.QuestContent.Windurst
{
    public sealed class MakingHeadlines : QuestScript
    {
        const string Var = "QuestMakingHeadlines_var";

        public MakingHeadlines() : base(QuestLog.Windurst, "MAKING_HEADLINES") { }

        // utils.mask.getBit(mask, pos)
        static bool GetBit(int mask, int pos) => (mask & (1 << pos)) != 0;

        // utils.mask.setBit(player:getCharVar(Var), pos, true), written back
        static void SetBit(IQuestContext q, int pos) =>
            q.Player.SetCharVar(Var, q.Player.CharVar(Var) | (1 << pos));

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                // Naiko-Paneiko.lua onTrigger (his 663 stays in his script)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.NaikoPaneiko).Called().OnTrigger(q =>
                {
                    QuestStatus makingHeadlines = q.Quest.Status;

                    if (makingHeadlines == QuestStatus.Available)
                    {
                        return QuestReply.Event(665);
                    }
                    else if (makingHeadlines == QuestStatus.Accepted)
                    {
                        int prog = q.Player.CharVar(Var);

                        if (!BastokWildcat.IsFull(prog, 4))
                        {
                            if (q.Player.RandomInt(1, 100) <= 50)
                            {
                                return QuestReply.Event(666); // quest reminder 1
                            }
                            else
                            {
                                return QuestReply.Event(671); // quest reminder 2
                            }
                        }
                        else if (!GetBit(prog, 4))
                        {
                            return QuestReply.Event(673); // advises to validate the story
                        }
                        else
                        {
                            if (q.Player.RandomInt(1, 100) <= 50)
                            {
                                return QuestReply.Event(674); // quest finish 1
                            }
                            else
                            {
                                return QuestReply.Event(670); // quest finish 2
                            }
                        }
                    }

                    return null; // his standard 663
                })
                // Kyume-Romeh.lua onTrigger (after his Hat in Hand arm; his random 604 / 393 stays in his script)
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.KyumeRomeh).Called().OnTrigger(q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted)
                    {
                        if (!GetBit(q.Player.CharVar(Var), 0))
                        {
                            return QuestReply.Event(668); // quest progress
                        }
                        else
                        {
                            return QuestReply.Event(669); // quest not furthered
                        }
                    }

                    return null;
                })
                // Yujuju.lua onTrigger (his 340 stays in his script)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Yujuju).Called().OnTrigger(q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted)
                    {
                        if (!GetBit(q.Player.CharVar(Var), 1))
                        {
                            return QuestReply.Event(314); // get the scoop
                        }
                        else
                        {
                            return QuestReply.Event(315); // after the scoop
                        }
                    }

                    return null;
                })
                // Hiwon-Biwon.lua onTrigger (his random default dialog stays in his script)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.HiwonBiwon).Called().OnTrigger(q =>
                {
                    if (q.Quest.Status == QuestStatus.Accepted)
                    {
                        if (!GetBit(q.Player.CharVar(Var), 2))
                        {
                            if (q.Player.QuestStatus(QuestLog.Windurst, "CURSES_FOILED_AGAIN_1") ==
                                QuestStatus.Accepted)
                            {
                                if (q.Player.RandomInt(1, 100) <= 50)
                                {
                                    return QuestReply.Event(283); // the scoop, while sick
                                }
                                else
                                {
                                    return QuestReply.Event(284); // the scoop, while sick
                                }
                            }
                            else
                            {
                                return QuestReply.Event(281); // the scoop
                            }
                        }
                        else
                        {
                            return QuestReply.Event(282);
                        }
                    }

                    return null;
                })
                // Umumu.lua onTrigger (his 414 in the else stays in his script)
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Umumu).Called().OnTrigger(q =>
                {
                    QuestStatus makingHeadlines = q.Quest.Status;

                    if (makingHeadlines == QuestStatus.Accepted)
                    {
                        int prog = q.Player.CharVar(Var);

                        if (GetBit(prog, 4))
                        {
                            return QuestReply.Event(383); // advised to go to Naiko
                        }
                        else if (!GetBit(prog, 3))
                        {
                            return QuestReply.Event(381); // the scoop, asked to validate it
                        }
                        else
                        {
                            return QuestReply.Event(382); // reminded to validate
                        }
                    }
                    else if (makingHeadlines == QuestStatus.Completed)
                    {
                        int rand = q.Player.RandomInt(1, 3);

                        if (rand == 1)
                        {
                            return QuestReply.Event(385);
                        }
                        else if (rand == 2)
                        {
                            return QuestReply.Event(386);
                        }
                        else if (rand == 3)
                        {
                            return QuestReply.Event(414);
                        }
                    }

                    return null; // his standard 414
                })
                // _5ca.lua onTrigger (the Mahogany Door)
                .Npc(Zone.InnerHorutotoRuins, Npc.InnerHorutotoRuins._5ca).Called().OnTrigger(q =>
                {
                    int prog = q.Player.CharVar(Var);

                    if (q.Quest.Status == QuestStatus.Accepted &&
                        BastokWildcat.IsFull(prog, 4) &&
                        !GetBit(prog, 4))
                    {
                        // confirm the story
                        q.Text.Special(Text.InnerHorutotoRuins.CAT_BURGLARS_HIDEOUT, 1, KeyItem.WindurstWoodsScoop);
                        q.Player.SetCharVar(Var, prog | (1 << 4));
                        return QuestReply.NoAction();
                    }

                    return null;
                });

            b.AnyState()
                // Naiko-Paneiko.lua onEventFinish
                .OnEventFinish(Zone.WindurstWaters, 665, q =>
                {
                    q.Quest.Begin();
                })
                .OnEventFinish(Zone.WindurstWaters, 670, Finish)
                .OnEventFinish(Zone.WindurstWaters, 674, Finish)
                // Kyume-Romeh.lua onEventFinish (668)
                .OnEventFinish(Zone.WindurstWaters, 668, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.WindurstWatersScoop);
                    SetBit(q, 0);
                })
                // Yujuju.lua onEventFinish
                .OnEventFinish(Zone.PortWindurst, 314, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.PortWindurstScoop);
                    SetBit(q, 1);
                })
                // Hiwon-Biwon.lua onEventFinish
                .OnEventFinish(Zone.WindurstWalls, 281, WallsScoop)
                .OnEventFinish(Zone.WindurstWalls, 283, WallsScoop)
                .OnEventFinish(Zone.WindurstWalls, 284, WallsScoop)
                // Umumu.lua onEventFinish
                .OnEventFinish(Zone.WindurstWoods, 381, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.WindurstWoodsScoop);
                    SetBit(q, 3);
                });
        }

        // Naiko-Paneiko's 670 / 674
        static void Finish(IQuestContext q)
        {
            // npcUtil.completeQuest with the title EDITORS_HATCHET_MAN, gil 560 and the var QuestMakingHeadlines_var,
            // step for step: not quest:complete, whose cleanup would clear the quest's own Quest[2][37] vars (no item:
            // it always completes; the script does not read its result)
            int gil = RateAmount(560, q.Settings.MainNumber("GIL_RATE"));
            q.Player.AddGil(gil);
            q.Text.Special(Text.WindurstWaters.GIL_OBTAINED, gil);
            q.Player.AddTitle(Title.EditorsHatchetMan);
            q.Player.SetCharVar(Var, 0);
            q.Player.CompleteQuest(QuestLog.Windurst, "MAKING_HEADLINES");
            q.Player.DelKeyItem(KeyItem.WindurstWoodsScoop);
            q.Player.DelKeyItem(KeyItem.WindurstWallsScoop);
            q.Player.DelKeyItem(KeyItem.WindurstWatersScoop);
            q.Player.DelKeyItem(KeyItem.PortWindurstScoop);
        }

        // Hiwon-Biwon's 281 / 283 / 284
        static void WallsScoop(IQuestContext q)
        {
            q.Player.GiveKeyItem(KeyItem.WindurstWallsScoop);
            SetBit(q, 2);
        }

        // n * rate as the engine's binding reads a Lua number into an integer argument (llround: half away from zero)
        private static int RateAmount(int n, double rate) => (int)Math.Round(n * rate, MidpointRounding.AwayFromZero);
    }
}
