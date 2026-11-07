// Gifts of the Griffon (Log ID 7, Quest ID 15), a line-for-line port of
// scripts/quests/crystalWar/WOTG_SAN_1_Gifts_of_the_Griffon.lua (quest rework B3b Crystal War group 6). Southern San
// d'Oria [S]: Louxiard !pos -93 -4 49 (default 37), Rholont !pos -168 -2 56 (default 32), Elnonde !pos 86 2 0 (615),
// Illeuse !pos -44.2 2 -36.2 (617), Loillie !pos 78 -8 -23 (613), Machionage !pos -255 -3 109 (619), Rongelouts N
// Distaud !pos 0 2 -22 (604), Sabiliont !pos 9 2 -87 (608). Available, one of the nation openers completed
// (xi.wotg.helpers.hasCompletedFirstQuest): Louxiard: Prog 0: 21, whose finish sets the quest's mustZone and Prog 1;
// Prog 1 with the mustZone clear: 22 (-> Prog 2). Rholont: Prog 2: 23, whose finish begins it and gives 7 Plumes d'Or
// (one stack). Accepted: Rholont: no plume held and fewer than 7 traded (the bits of Option): the Timer 0: 34, whose
// finish sets the Timer to NextConquestTally(); before the Timer: 46 (an Event); at or past it: 35, whose finish
// clears the Timer and gives the plumes not yet traded; all 7 traded: 24, whose finish completes it (a Deathstone)
// and sets Claws of the Griffon's Timer to VanadielUniqueDay() + 1; else (a plume held, fewer than 7 traded) 33 (an
// Event). The seven townsfolk: a Plume d'Or alone, their bit unset: their event (25 Rongelouts N Distaud, 26
// Louxiard, 27 Sabiliont, 28 Machionage, 29 Loillie, 30 Elnonde, 31 Illeuse), whose finish confirms the trade and
// sets the bit csid - 25. The Lua's plumeTradeNpc is one handler table shared by the seven NPCs, finding the event by
// npc:getName(); here each NPC has its own handler with its event (the same: each name has one event, and each
// handler gets its own NPC), as Bastok's Lure of the Wildcat does.
// Kept as the Lua has it: Prog and the mustZone are written while the quest is available; 23 begins on any option;
// Louxiard answers nil at Prog 1 with the mustZone set and at Prog 2+, Rholont while available at any Prog but 2; 24
// completes with no check but the reward's fit; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class GiftsOfTheGriffon : QuestScript
    {
        public GiftsOfTheGriffon() : base(QuestLog.CrystalWar, "GIFTS_OF_THE_GRIFFON") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_SAN_1_Gifts_of_the_Griffon";

        // the Lua's npcTradeEvents: each townsperson's trade event (its bit of Option is the event - 25)
        static readonly (string Npc, int Event)[] NpcTradeEvents =
        {
            (Npc.SouthernSanDoriaS.Elnonde, 30),
            (Npc.SouthernSanDoriaS.Illeuse, 31),
            (Npc.SouthernSanDoriaS.Loillie, 29),
            (Npc.SouthernSanDoriaS.Louxiard, 26),
            (Npc.SouthernSanDoriaS.Machionage, 28),
            (Npc.SouthernSanDoriaS.RongeloutsNDistaud, 25),
            (Npc.SouthernSanDoriaS.Sabiliont, 27),
        };

        // utils.mask.countBits(mask, len)
        static int CountBits(int mask, int len)
        {
            int count = 0;

            for (int i = 0; i < len; i++)
            {
                count += (mask >> i) & 1;
            }

            return count;
        }

        // the Lua's plumeTradeNpc.onTrade, for the NPC whose event is tradeEventId
        static QuestReply PlumeTrade(IQuestContext q, int tradeEventId)
        {
            if (q.Trade.HasExactly(Item.PlumeDor) &&
                !q.Quest.IsVarBitsSet("Option", tradeEventId - 25))
            {
                return QuestReply.ProgressEvent(tradeEventId);
            }
            return null;
        }

        // the Lua's plumeOnEventFinish
        static void PlumeOnEventFinish(IQuestContext q, int csid)
        {
            q.Trade.Confirm();
            q.Quest.SetVarBit("Option", csid - 25);
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2);

            b.Reward(new QuestReward
            {
                Item = Item.Deathstone,
            });

            b.State(QuestStatus.Available, when: q => TheTigressStirs.HasCompletedFirstQuest(q.Player))
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Louxiard).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(21);
                    }
                    else if (questProgress == 1 &&
                        !q.Quest.MustZone)
                    {
                        return QuestReply.ProgressEvent(22);
                    }
                    return null;
                })
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Rholont).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(23);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 21, q =>
                {
                    q.Quest.SetMustZone();
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 22, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 23, q =>
                {
                    q.Quest.Begin();
                    q.Player.GiveItems((Item.PlumeDor, 7));
                });

            var accepted = b.State(QuestStatus.Accepted);

            accepted
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Rholont).OnTrigger(q =>
                {
                    int plumesTraded = CountBits(q.Quest.Var("Option"), 7);

                    if (!q.Player.HasItem(Item.PlumeDor) &&
                        plumesTraded < 7)
                    {
                        int waitTime = q.Quest.Var("Timer");

                        if (waitTime == 0)
                        {
                            return QuestReply.ProgressEvent(34);
                        }
                        else if (q.Player.SystemTime < waitTime)
                        {
                            return QuestReply.Event(46);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(35);
                        }
                    }
                    else if (plumesTraded == 7)
                    {
                        return QuestReply.ProgressEvent(24);
                    }
                    else
                    {
                        return QuestReply.Event(33);
                    }
                });

            foreach (var (npc, tradeEventId) in NpcTradeEvents)
            {
                accepted.Npc(Zone.SouthernSanDoriaS, npc).OnTrade(q => PlumeTrade(q, tradeEventId));
            }

            accepted
                .OnEventFinish(Zone.SouthernSanDoriaS, 24, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetQuestVar(
                            QuestLog.CrystalWar, "CLAWS_OF_THE_GRIFFON", "Timer", q.Player.VanadielUniqueDay + 1);
                    }
                });

            for (int csid = 25; csid <= 31; csid++)
            {
                int eventId = csid;
                accepted.OnEventFinish(Zone.SouthernSanDoriaS, eventId, q => PlumeOnEventFinish(q, eventId));
            }

            accepted
                .OnEventFinish(Zone.SouthernSanDoriaS, 34, q =>
                {
                    q.Quest.SetVar("Timer", q.Player.NextConquestTally);
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 35, q =>
                {
                    int numPlumes = 7 - CountBits(q.Quest.Var("Option"), 7);

                    q.Quest.SetVar("Timer", 0);
                    q.Player.GiveItems((Item.PlumeDor, numPlumes));
                });
        }
    }
}
