// Even More Gullible's Travels (Log ID 5), ported from the quest branches of scripts/zones/Kazham/npcs/Magriffon.lua and
// scripts/zones/Yuhtunga_Jungle/npcs/Blue_Rafflesia.lua (quest rework B3b, Outlands group 7; call-only: the scripts now
// call this quest; Magriffon's calls Gullible's Travels first). Blue Rafflesia : !pos -468.876 -1 220.247 123 (the first
// three of BLUE_RAFFLESIA_OFFSET).
// Magriffon, after Gullible's Travels' arms: accepted with EVEN_MORE_GULLIBLES_PROGRESS 0: 149 (0, 256, 0, 0, 0, 35000);
// 1: 151; 2: 152 (0, 1144, 256); else Gullible's Travels completed: available, Windurst fame level 7 and no needToZone:
// 148 (0, 256, 0, 0, 35000), whose finish on 1 begins the quest; else 147. The script keeps its 143 otherwise. His trade,
// accepted with progress 0, 35000 gil or more: 150 (0, 256); its finish: the trade confirmed, 35000 gil taken, progress
// 1, the title Even More Gullible's Travels set, the Treasure Map. 152's finish: progress 0, Windurst fame +70, the quest
// completed.
// The Blue Rafflesias (offset 0 / 1 / 2 from BLUE_RAFFLESIA_OFFSET; event 21 / 22 / 23, char var First / Second /
// ThirdBlueRafflesiaCS): accepted, progress 1 and its var 0: the event; completed, RafflesiaScentDay before today,
// BathedInScent 0 and its var 0: the event (the repeat); else FLOWER_BLOOMING. The finish on 1: the other two vars 1: both
// 0, BathedInScent 1, RafflesiaScentDay today, a Jar of Rafflesia Nectar, FEEL_DIZZY, and while accepted progress 2;
// else its own var 1 and a jar.
// Kept as the Lua has it: a Blue Rafflesia past the third answers nothing; a talk that played the event but was not
// finished on 1 changes nothing; the jar is given with npcUtil.giveItem (with no room: its message, the vars still set);
// the trade's gil is confirmed, then delGil takes 35000; the title is set, not added; accepted with progress 3 or more
// falls to the Gullible's Travels completed arm (147).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class EvenMoreGulliblesTravels : QuestScript
    {
        public EvenMoreGulliblesTravels() : base(QuestLog.Outlands, "EVEN_MORE_GULLIBLES_TRAVELS") { }

        private const string Progress = "EVEN_MORE_GULLIBLES_PROGRESS";

        // the three Blue Rafflesias: the event and the char var of each
        private static readonly (int Csid, string Var)[] Rafflesias =
        {
            (21, "FirstBlueRafflesiaCS"),
            (22, "SecondBlueRafflesiaCS"),
            (23, "ThirdBlueRafflesiaCS"),
        };

        // the Blue Rafflesia finish (option 1): the third flower bathes the char, else its own var is set
        private static void RafflesiaFinish(IQuestContext q, int index)
        {
            if (q.Option != 1)
            {
                return;
            }

            // the other two flowers' vars, in the Lua's First / Second / Third order
            string other1 = Rafflesias[index == 0 ? 1 : 0].Var;
            string other2 = Rafflesias[index == 2 ? 1 : 2].Var;

            if (q.Player.CharVar(other1) == 1 &&
                q.Player.CharVar(other2) == 1)
            {
                // This is 3rd Rafflessia clicked, progressing.
                q.Player.SetCharVar(other1, 0);
                q.Player.SetCharVar(other2, 0);
                q.Player.SetCharVar("BathedInScent", 1);
                q.Player.SetCharVar("RafflesiaScentDay", q.Player.VanadielUniqueDay);
                q.Player.GiveItem(Item.JarOfRafflesiaNectar);
                q.Text.Special(Text.YuhtungaJungle.FEEL_DIZZY); // You feel slightly dizzy. You must have breathed in too much of the pollen.
                if (q.Quest.Status == QuestStatus.Accepted)
                {
                    q.Player.SetCharVar(Progress, 2);
                }
            }
            else
            {
                q.Player.SetCharVar(Rafflesias[index].Var, 1);
                q.Player.GiveItem(Item.JarOfRafflesiaNectar);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            // Magriffon.lua onTrigger: the arms after Gullible's Travels', in its order
            b.AnyState()
                .Npc(Zone.Kazham, Npc.Kazham.Magriffon).Called()
                    .OnTrigger(q =>
                    {
                        QuestStatus gulliblesTravelsStatus = q.Player.QuestStatus(QuestLog.Outlands, "GULLIBLES_TRAVELS");
                        QuestStatus evenmoreTravelsStatus = q.Quest.Status;

                        if (evenmoreTravelsStatus == QuestStatus.Accepted &&
                            q.Player.CharVar(Progress) == 0)
                        {
                            return QuestReply.Event(149, 0, 256, 0, 0, 0, 35000);
                        }
                        else if (evenmoreTravelsStatus == QuestStatus.Accepted &&
                            q.Player.CharVar(Progress) == 1)
                        {
                            return QuestReply.Event(151);
                        }
                        else if (evenmoreTravelsStatus == QuestStatus.Accepted &&
                            q.Player.CharVar(Progress) == 2)
                        {
                            return QuestReply.Event(152, 0, 1144, 256);
                        }
                        else if (gulliblesTravelsStatus == QuestStatus.Completed)
                        {
                            if (evenmoreTravelsStatus == QuestStatus.Available &&
                                q.Player.FameLevel(FameArea.Windurst) >= 7 &&
                                !q.Player.NeedToZone)
                            {
                                return QuestReply.Event(148, 0, 256, 0, 0, 35000);
                            }
                            else
                            {
                                return QuestReply.Event(147);
                            }
                        }

                        return null;
                    });

            // Magriffon.lua onTrade: the Even More Gullible's Travels arm
            b.State(QuestStatus.Accepted, when: q => q.Player.CharVar(Progress) == 0)
                .Npc(Zone.Kazham, Npc.Kazham.Magriffon).Called().OnTrade(q =>
                {
                    if (q.Trade.Gil >= 35000)
                    {
                        return QuestReply.Event(150, 0, 256);
                    }

                    return null;
                });

            // Blue_Rafflesia.lua onTrigger
            b.AnyState()
                .Npc(Zone.YuhtungaJungle, Npc.YuhtungaJungle.BlueRafflesia).Called().OnTrigger(q =>
                {
                    QuestStatus evenmoreTravelsStatus = q.Quest.Status;
                    int questprogress = q.Player.CharVar(Progress);
                    int scentDay = q.Player.CharVar("RafflesiaScentDay");
                    int currentDay = q.Player.VanadielUniqueDay;
                    bool scentReady = scentDay < currentDay;
                    // npc:getID() - ID.npc.BLUE_RAFFLESIA_OFFSET (Blue_Rafflesia.lua:18)
                    int offset = q.Npc.Id - q.Player.NpcIdOf("BLUE_RAFFLESIA_OFFSET");

                    if (offset < 0 || offset > 2)
                    {
                        return null;
                    }

                    var (csid, flowerVar) = Rafflesias[offset];

                    if (evenmoreTravelsStatus == QuestStatus.Accepted &&
                        questprogress == 1 &&
                        q.Player.CharVar(flowerVar) == 0)
                    {
                        // Player is on quest, first time.
                        return QuestReply.Event(csid);
                    }
                    else if (evenmoreTravelsStatus == QuestStatus.Completed &&
                        scentReady &&
                        q.Player.CharVar("BathedInScent") == 0 &&
                        q.Player.CharVar(flowerVar) == 0)
                    {
                        // Repeating
                        return QuestReply.Event(csid);
                    }
                    else
                    {
                        return QuestReply.MessageSpecial(Text.YuhtungaJungle.FLOWER_BLOOMING);
                    }
                });

            // Magriffon.lua onEventFinish (148, 150, 152), Blue_Rafflesia.lua onEventFinish (21, 22, 23)
            b.AnyState()
                .OnEventFinish(Zone.Kazham, 148, q => // Even More Guillible's Travels First CS
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.Kazham, 150, q => // Even More Guillible's Travels Second CS
                {
                    q.Trade.Confirm();
                    q.Player.DelGil(35000);
                    q.Player.SetCharVar(Progress, 1);
                    q.Player.SetTitle(Title.EvenMoreGulliblesTravels);
                    q.Player.GiveKeyItem(KeyItem.TreasureMap);
                })
                .OnEventFinish(Zone.Kazham, 152, q =>
                {
                    q.Player.SetCharVar(Progress, 0);
                    q.Player.AddFame(FameArea.Windurst, 70);
                    // the raw player:completeQuest, which keeps the quest's own Quest[5][9] vars
                    q.Player.CompleteQuest(QuestLog.Outlands, "EVEN_MORE_GULLIBLES_TRAVELS");
                })
                .OnEventFinish(Zone.YuhtungaJungle, 21, q => RafflesiaFinish(q, 0))
                .OnEventFinish(Zone.YuhtungaJungle, 22, q => RafflesiaFinish(q, 1))
                .OnEventFinish(Zone.YuhtungaJungle, 23, q => RafflesiaFinish(q, 2));
        }
    }
}
