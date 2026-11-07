// Her Memories: Homecoming Queen (Log ID 7, Quest ID 64), a line-for-line port of
// scripts/quests/crystalWar/Her_Memories_Homecoming_Queen.lua (quest rework B3b Crystal War, draft), with its three
// sub-quests (no files of their own): Her Memories: Old Bean (65), The Faux Pas (66), The Grave Resolve (67). Port San
// d'Oria: Thierride !pos -67 -5 -28 (script: onTrade only; default 521). Southern San d'Oria: Amaura !pos -84.367
// -6.949 91.148 (script: To Cure a Cough's xi.cs call; default 642). Northern San d'Oria: Abioleget !pos 128.771 0
// 118.538 (default ABIOLEGET_DIALOG), Bertenont !pos -165 0.1 226 (script: BERTENONT_DIALOG). Chateau d'Oraguille:
// Halver !pos 2 0.1 0.1 (script: 577 / 31 for a San d'Orian). Batallia Downs: Weathered Gravestone !pos 149.728 -5.109
// -395.121 (default SEE_WEATHERED_GRAVESTONE).
// Available, the WotG mission Her Memories current: the Southern San d'Oria zone-in from East Ronfaure: 957, whose
// finish begins it. Accepted, one state per sub-quest not completed:
// - Old Bean: Thierride: Prog1 0: no Thierride's Bean Creation: 792, whose finish adds Old Bean and gives the bean; the
//   bean: 794 (an Event, oncePerZone); Prog1 1: 793 (below). Amaura: the bean: 958 (8 args), whose finish takes it and
//   sets Prog1 1; else Prog1 1: 959 (an Event, oncePerZone).
// - The Faux Pas: Abioleget: Prog2 0: 869, whose finish adds The Faux Pas and sets Prog2 1; else 871 (an Event,
//   oncePerZone). Bertenont: Prog2 1: 870 (below).
// - The Grave Resolve: Halver: Prog3 0: 571 (8 args), whose finish adds The Grave Resolve and sets Prog3 1; else 572
//   (an Event, oncePerZone). Weathered Gravestone: Prog3 1+: 5 (8 args), whose finish sets Prog3 2; a Lilac alone at
//   Prog3 2: the trade confirmed, 6 (8 args, below).
// 793 / 870 / 6 (the Lua's handleQuestCompletion): the tiny fragments held are counted; the sub-quest is completed
// (player:completeQuest: no reward); under 2 held: FRAGMENT_FAR_TOO_SMALL (messageName, no entity) and its tiny
// fragment; else FRAGMENTS_MELD, the three tiny fragments taken, this quest completed (the Large Memory Fragment) and
// xi.wotg.helpers.checkMemoryFragments (four large fragments: REPORT_TO_CAIT_SITH, Her Memories completed, Forget Me
// Not added).
// The text ids come from the char's zone (`zones[player:getZoneID()]`): the C# names them in q.Zone, as the Lua does;
// Port San d'Oria, Northern San d'Oria and Batallia Downs all have FRAGMENT_FAR_TOO_SMALL and FRAGMENTS_MELD.
// `pairs(subQuestData)` (keys 793, 870, 6) visits 870, 793, 6 under the engine's LuaJIT (libluajit_64.dll, run in the
// scratch project): the C# keeps that order for the count and the three delKeyItem calls.
// Kept as the Lua has it: 957 begins on any option; the sub-quests are added and completed with no status check of
// their own; the Gravestone's 5 answers again at Prog3 2 (its finish sets 2 again); the Lilac is confirmed in the
// trade, before the event; the third tiny fragment is taken whether held or not; no completed section.
// Rival containers (golden flows #rivals, #rivals2, #rivals3): Waking the Colossus at Halver (567), A Taste for Meat at
// Thierride (526) and the WotG mission A Hawk in Repose at the Weathered Gravestone (502 / 503) answer the same talks
// and trades at Progress; Of Malign Maladies' #rivals plays 958 / 959 against its own Amaura replies;
// sandoria.the_vicasques_sermon#her_memories plays 869 / 871 against The Vicasque's Sermon at Abioleget; #rivals4
// plays Lure of the Wildcat's 809 (Bertenont) and 558 (Halver), Progress while their bit is not set.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class HerMemoriesHomecomingQueen : QuestScript
    {
        public HerMemoriesHomecomingQueen() : base(QuestLog.CrystalWar, "HER_MEMORIES_HOMECOMING_QUEEN") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/Her_Memories_Homecoming_Queen";

        // the Lua's subQuestData: [csid] = { the sub-quest, its tiny fragment }
        static (string Quest, int KeyItem) SubQuestData(int csid) => csid switch
        {
            793 => ("HER_MEMORIES_OLD_BEAN", KeyItem.TinyMemoryFragment1),
            870 => ("HER_MEMORIES_THE_FAUX_PAS", KeyItem.TinyMemoryFragment2),
            6 => ("HER_MEMORIES_THE_GRAVE_RESOLVE", KeyItem.TinyMemoryFragment3),
            _ => throw new System.ArgumentOutOfRangeException(nameof(csid)),
        };

        // pairs(subQuestData)'s order under the engine's LuaJIT (see the header)
        static readonly int[] SubQuestPairsOrder = { 870, 793, 6 };

        // the Lua's handleQuestCompletion (793, 870, 6)
        static void HandleQuestCompletion(IQuestContext q)
        {
            int numKeyItems = 0;

            foreach (int key in SubQuestPairsOrder)
            {
                if (q.Player.HasKeyItem(SubQuestData(key).KeyItem))
                {
                    numKeyItems = numKeyItems + 1;
                }
            }

            q.Player.CompleteQuest(QuestLog.CrystalWar, SubQuestData(q.Csid).Quest);

            if (numKeyItems < 2)
            {
                q.Text.MessageName(new ZoneText(q.Zone, "FRAGMENT_FAR_TOO_SMALL"), SubQuestData(q.Csid).KeyItem);
                q.Player.GiveKeyItem(SubQuestData(q.Csid).KeyItem);
            }
            else
            {
                q.Text.Special(new ZoneText(q.Zone, "FRAGMENTS_MELD"), KeyItem.LargeMemoryFragment1);

                foreach (int key in SubQuestPairsOrder)
                {
                    q.Player.DelKeyItem(SubQuestData(key).KeyItem);
                }

                if (q.Quest.Complete())
                {
                    q.Player.CheckMemoryFragments();
                }
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                KeyItem = KeyItem.LargeMemoryFragment1,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.CurrentMission(MissionLog.Wotg) == Mission.Wotg.HerMemories)
                .OnZoneIn(Zone.SouthernSanDoria, q =>
                {
                    if (q.Player.PreviousZone == Zone.EastRonfaure)
                    {
                        return QuestReply.Event(957);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoria, 957, q =>
                {
                    q.Quest.Begin();
                });

            // Her Memories: Old Bean
            b.State(QuestStatus.Accepted, when: q =>
                    q.Player.QuestStatus(QuestLog.CrystalWar, "HER_MEMORIES_OLD_BEAN") != QuestStatus.Completed)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Thierride).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Var("Prog1");

                    if (questProgress == 0)
                    {
                        if (!q.Player.HasKeyItem(KeyItem.ThierridesBeanCreation))
                        {
                            return QuestReply.ProgressEvent(792);
                        }
                        else
                        {
                            return QuestReply.Event(794).OncePerZone();
                        }
                    }
                    else if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(793);
                    }
                    return null;
                })
                .OnEventFinish(Zone.PortSanDoria, 792, q =>
                {
                    q.Player.AddQuest(QuestLog.CrystalWar, "HER_MEMORIES_OLD_BEAN");

                    q.Player.GiveKeyItem(KeyItem.ThierridesBeanCreation);
                })
                .OnEventFinish(Zone.PortSanDoria, 793, HandleQuestCompletion)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Amaura).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.ThierridesBeanCreation))
                    {
                        return QuestReply.ProgressEvent(958, 650067949, 0, 5, 0, 156286100, 100000, 4095, 131073);
                    }
                    else if (q.Quest.Var("Prog1") == 1)
                    {
                        return QuestReply.Event(959).OncePerZone();
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoria, 958, q =>
                {
                    q.Player.DelKeyItem(KeyItem.ThierridesBeanCreation);

                    q.Quest.SetVar("Prog1", 1);
                });

            // Her Memories: The Faux Pas
            b.State(QuestStatus.Accepted, when: q =>
                    q.Player.QuestStatus(QuestLog.CrystalWar, "HER_MEMORIES_THE_FAUX_PAS") != QuestStatus.Completed)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Abioleget).OnTrigger(q =>
                {
                    if (q.Quest.Var("Prog2") == 0)
                    {
                        return QuestReply.ProgressEvent(869);
                    }
                    else
                    {
                        return QuestReply.Event(871).OncePerZone();
                    }
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Bertenont).OnTrigger(q =>
                {
                    if (q.Quest.Var("Prog2") == 1)
                    {
                        return QuestReply.ProgressEvent(870);
                    }
                    return null;
                })
                .OnEventFinish(Zone.NorthernSanDoria, 869, q =>
                {
                    q.Player.AddQuest(QuestLog.CrystalWar, "HER_MEMORIES_THE_FAUX_PAS");

                    q.Quest.SetVar("Prog2", 1);
                })
                .OnEventFinish(Zone.NorthernSanDoria, 870, HandleQuestCompletion);

            // Her Memories: The Grave Resolve
            b.State(QuestStatus.Accepted, when: q =>
                    q.Player.QuestStatus(QuestLog.CrystalWar, "HER_MEMORIES_THE_GRAVE_RESOLVE")
                        != QuestStatus.Completed)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Halver).OnTrigger(q =>
                {
                    if (q.Quest.Var("Prog3") == 0)
                    {
                        return QuestReply.ProgressEvent(571, 65, 23, 2964, 100000, 233714167, 0, 1, 0);
                    }
                    else
                    {
                        return QuestReply.Event(572, 69, 23, 2964, 100000, 233714167, 0, 1, 0).OncePerZone();
                    }
                })
                .OnEventFinish(Zone.ChateauDoraguille, 571, q =>
                {
                    q.Player.AddQuest(QuestLog.CrystalWar, "HER_MEMORIES_THE_GRAVE_RESOLVE");

                    q.Quest.SetVar("Prog3", 1);
                })
                .Npc(Zone.BatalliaDowns, Npc.BatalliaDowns.WeatheredGravestone).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.Lilac) &&
                        q.Quest.Var("Prog3") == 2)
                    {
                        q.Trade.Confirm();

                        return QuestReply.ProgressEvent(6, 119, 300, 200, 100, 0, 0, 558716, 0);
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    if (q.Quest.Var("Prog3") >= 1)
                    {
                        return QuestReply.ProgressEvent(5, 105, 0, 0, 0, 43, 0, 558716, 0);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BatalliaDowns, 5, q =>
                {
                    q.Quest.SetVar("Prog3", 2);
                })
                .OnEventFinish(Zone.BatalliaDowns, 6, HandleQuestCompletion);
        }
    }
}
