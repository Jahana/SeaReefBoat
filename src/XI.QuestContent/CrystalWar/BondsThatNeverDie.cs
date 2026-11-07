// Bonds That Never Die (Log ID 7, Quest ID 45), a line-for-line port of
// scripts/quests/crystalWar/WOTG_SAN_8_Bonds_That_Never_Die.lua (quest rework B3b Crystal War, draft). Southern San
// d'Oria [S]: Rholont !pos -168 -2 56 (default 32), Rongelouts N Distaud !pos 0 2 -22 (default 604). Pashhow Marshlands
// [S]: Shimmering Pondweed (no default). Jugner Forest [S]: Overgrown Mushrooms (no default), Mossy Stump (default
// NO_RESPONSE). Everbloom Hollow: the instance (not implemented).
// Available, The Price of Valor completed: Rholont: no Letter to Count Aurchiat: 649, whose finish gives it; else 651
// (an Event). Shimmering Pondweed: the letter: 107, whose finish begins it and takes the letter. Accepted: Overgrown
// Mushrooms: Prog 0: 212, whose finish gives a Hatchet and, given, sets Prog 1; Prog 1 with the Length of Jugner Ivy
// (logging in Jugner Forest [S]: xi.wotg.helpers.helmTrade reads this quest's Prog 1): 213 (-> 2). Mossy Stump: Prog 2:
// 214 (-> 3). Everbloom Hollow's 10000 finish: Prog 4, setPos into Jugner Forest [S]. The Jugner Forest [S] zone-in at
// Prog 4 (any zone): 215 (-> 5). Rongelouts N Distaud: Prog 5: 650, whose finish completes it (a Behemoth Horn) and
// sets Option 1, Songbirds in a Snowstorm's Timer to tomorrow and its mustZone. Rholont: 651 (an Event). Completed:
// Rongelouts N Distaud: Option 1: 654, whose finish sets Option 0.
// Kept as the Lua has it: 107 begins on any option; the ivy is never taken; Prog 3 has no handler here (the instance's
// entry); Rholont's accepted 651 is a plain Event whatever the Prog.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class BondsThatNeverDie : QuestScript
    {
        public BondsThatNeverDie() : base(QuestLog.CrystalWar, "BONDS_THAT_NEVER_DIE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_SAN_8_Bonds_That_Never_Die";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4, 5);

            b.Reward(new QuestReward
            {
                Item = Item.BehemothHorn,
            });

            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.CrystalWar, "THE_PRICE_OF_VALOR"))
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Rholont).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.LetterToCountAurchiat))
                    {
                        return QuestReply.ProgressEvent(649);
                    }
                    else
                    {
                        return QuestReply.Event(651);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 649, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.LetterToCountAurchiat);
                })
                .Npc(Zone.PashhowMarshlandsS, Npc.PashhowMarshlandsS.ShimmeringPondweed).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.LetterToCountAurchiat))
                    {
                        return QuestReply.ProgressEvent(107);
                    }
                    return null;
                })
                .OnEventFinish(Zone.PashhowMarshlandsS, 107, q =>
                {
                    q.Quest.Begin();
                    q.Player.DelKeyItem(KeyItem.LetterToCountAurchiat);
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.JugnerForestS, Npc.JugnerForestS.OvergrownMushrooms).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(212);
                    }
                    else if (questProgress == 1 &&
                        q.Player.HasKeyItem(KeyItem.LengthOfJugnerIvy))
                    {
                        return QuestReply.ProgressEvent(213);
                    }
                    return null;
                })
                .Npc(Zone.JugnerForestS, Npc.JugnerForestS.MossyStump).OnTrigger(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        return QuestReply.ProgressEvent(214);
                    }
                    return null;
                })
                .OnZoneIn(Zone.JugnerForestS, q =>
                {
                    if (q.Quest.Step == 4)
                    {
                        return QuestReply.Event(215);
                    }
                    return null;
                })
                .OnEventFinish(Zone.JugnerForestS, 212, q =>
                {
                    if (q.Player.GiveItem(Item.Hatchet))
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.JugnerForestS, 213, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.JugnerForestS, 214, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.JugnerForestS, 215, q =>
                {
                    q.Quest.SetStep(5);
                })
                .OnEventFinish(Zone.EverbloomHollow, 10000, q =>
                {
                    q.Quest.SetStep(4);
                    q.Player.SetPos(-285.717, 0.5, 88.107, 98, Zone.JugnerForestS);
                })
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.RongeloutsNDistaud).OnTrigger(q =>
                {
                    if (q.Quest.Step == 5)
                    {
                        return QuestReply.ProgressEvent(650);
                    }
                    return null;
                })
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.Rholont).OnTrigger(QuestReply.Event(651))
                .OnEventFinish(Zone.SouthernSanDoriaS, 650, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Quest.SetVar("Option", 1);

                        q.Player.SetQuestVar(QuestLog.CrystalWar, "SONGBIRDS_IN_A_SNOWSTORM", "Timer", q.Player.VanadielUniqueDay + 1);
                        q.Player.SetQuestMustZone(QuestLog.CrystalWar, "SONGBIRDS_IN_A_SNOWSTORM");
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.SouthernSanDoriaS, Npc.SouthernSanDoriaS.RongeloutsNDistaud).OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") == 1)
                    {
                        return QuestReply.ProgressEvent(654);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoriaS, 654, q =>
                {
                    q.Quest.SetVar("Option", 0);
                });
        }
    }
}
