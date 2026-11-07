// Riding on the Clouds (Log ID 3, Quest ID 131), a line-for-line port of scripts/quests/jeuno/LB04_Riding_on_the_clouds.lua
// (quest rework B3b). Maat : !pos 8 3 118 243. At level cap 65 (MAX_LEVEL 70 or more) Maat offers 88 from level
// 61: the talk draws the four nations' NPC indexes (npcSandoria, npcBastok, npcWindurst, npcOtherlands: math.randomInt(0,
// 7) each, in that order) and sends them with 180; option 1 begins, any other option sets the four back to 0. Accepted:
// a Kindred's Seal traded alone to the NPC its nation's var names (8 per nation: the var's index in the Lua file's order)
// sets that var to 8, takes the seal and gives the nation's stone (San d'Oria Scowling, Bastok Smiling, Windurst
// Spirited, Selbina / Mhaura Somber). Maat: 90 with the four stones (completed, Cloud Breaker, the stones taken, level cap
// 70, YOUR_LEVEL_LIMIT_IS_NOW_70), else the reminder 89 with the four vars and 180.
// Kept Lua quirks: every NPC trade handler returns nil, also when it gives the stone (the NPC's own onTrade then runs
// on the same trade, the lookup's fallback); a var already 8 (stone given) answers no NPC of that nation; the stone is
// given again when a GM sets the var back (npcUtil.giveKeyItem on a held key item).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class RidingOnTheClouds : QuestScript
    {
        public RidingOnTheClouds() : base(QuestLog.Jeuno, "RIDING_ON_THE_CLOUDS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/LB04_Riding_on_the_clouds";

        // handleSandoriaTrade / handleBastokTrade / handleWindurstTrade / handleOtherlandsTrade
        static void HandleTrade(IQuestContext q, string varName, int keyItem)
        {
            if (q.Trade.HasExactly(Item.KindredsSeal))
            {
                q.Quest.SetVar(varName, 8);
                q.Trade.Confirm();
                q.Player.GiveKeyItem(keyItem);
            }
        }

        static void HandleSandoriaTrade(IQuestContext q) => HandleTrade(q, "npcSandoria", KeyItem.ScowlingStone);
        static void HandleBastokTrade(IQuestContext q) => HandleTrade(q, "npcBastok", KeyItem.SmilingStone);
        static void HandleWindurstTrade(IQuestContext q) => HandleTrade(q, "npcWindurst", KeyItem.SpiritedStone);
        static void HandleOtherlandsTrade(IQuestContext q) => HandleTrade(q, "npcOtherlands", KeyItem.SomberStone);

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Title = Title.CloudBreaker });

            // Section: Quest available.
            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainLevel >= 61 &&
                    q.Player.LevelCap == 65 &&
                    q.Settings.MainNumber("MAX_LEVEL") >= 70)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Maat).OnTrigger(q =>
                {
                    q.Quest.SetVar("npcSandoria", q.Player.RandomInt(0, 7));
                    q.Quest.SetVar("npcBastok", q.Player.RandomInt(0, 7));
                    q.Quest.SetVar("npcWindurst", q.Player.RandomInt(0, 7));
                    q.Quest.SetVar("npcOtherlands", q.Player.RandomInt(0, 7));
                    return QuestReply.ProgressEvent(88,
                        q.Quest.Var("npcSandoria"),
                        q.Quest.Var("npcBastok"),
                        q.Quest.Var("npcWindurst"),
                        q.Quest.Var("npcOtherlands"),
                        180);
                })
                .OnEventFinish(Zone.RuludeGardens, 88, q =>
                {
                    if (q.Option == 1) // Accept quest option.
                    {
                        q.Quest.Begin();
                    }
                    else
                    {
                        q.Quest.SetVar("npcSandoria", 0);
                        q.Quest.SetVar("npcBastok", 0);
                        q.Quest.SetVar("npcWindurst", 0);
                        q.Quest.SetVar("npcOtherlands", 0);
                    }
                });

            // Section: Quest accepted.
            b.State(QuestStatus.Accepted)
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Maat).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.SmilingStone) &&
                        q.Player.HasKeyItem(KeyItem.ScowlingStone) &&
                        q.Player.HasKeyItem(KeyItem.SomberStone) &&
                        q.Player.HasKeyItem(KeyItem.SpiritedStone))
                    {
                        return QuestReply.ProgressEvent(90); // Finish Quest "Riding on the Clouds"
                    }
                    else
                    {
                        return QuestReply.Event(89,
                            q.Quest.Var("npcSandoria"),
                            q.Quest.Var("npcBastok"),
                            q.Quest.Var("npcWindurst"),
                            q.Quest.Var("npcOtherlands"),
                            180); // NPC reminder.
                    }
                })
                .OnEventFinish(Zone.RuludeGardens, 90, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.ScowlingStone);
                        q.Player.DelKeyItem(KeyItem.SmilingStone);
                        q.Player.DelKeyItem(KeyItem.SomberStone);
                        q.Player.DelKeyItem(KeyItem.SpiritedStone);
                        q.Player.SetLevelCap(70);
                        q.Text.Special(Text.RuludeGardens.YOUR_LEVEL_LIMIT_IS_NOW_70);
                    }
                })

                // Sandoria zones and NPCs
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Raminel).OnTrade(q =>
                {
                    if (q.Quest.Var("npcSandoria") == 0)
                    {
                        HandleSandoriaTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Sobane).OnTrade(q =>
                {
                    if (q.Quest.Var("npcSandoria") == 1)
                    {
                        HandleSandoriaTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Taurette).OnTrade(q =>
                {
                    if (q.Quest.Var("npcSandoria") == 2)
                    {
                        HandleSandoriaTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Maloquedil).OnTrade(q =>
                {
                    if (q.Quest.Var("npcSandoria") == 3)
                    {
                        HandleSandoriaTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Sheridan).OnTrade(q =>
                {
                    if (q.Quest.Var("npcSandoria") == 4)
                    {
                        HandleSandoriaTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Fontoumant).OnTrade(q =>
                {
                    if (q.Quest.Var("npcSandoria") == 5)
                    {
                        HandleSandoriaTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Brifalien).OnTrade(q =>
                {
                    if (q.Quest.Var("npcSandoria") == 6)
                    {
                        HandleSandoriaTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Rugiette).OnTrade(q =>
                {
                    if (q.Quest.Var("npcSandoria") == 7)
                    {
                        HandleSandoriaTrade(q);
                    }
                    return null;
                })

                // Bastok zones and NPCs
                .Npc(Zone.BastokMines, Npc.BastokMines.Babenn).OnTrade(q =>
                {
                    if (q.Quest.Var("npcBastok") == 0)
                    {
                        HandleBastokTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Gwill).OnTrade(q =>
                {
                    if (q.Quest.Var("npcBastok") == 1)
                    {
                        HandleBastokTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.BastokMarkets, Npc.BastokMarkets.Brygid).OnTrade(q =>
                {
                    if (q.Quest.Var("npcBastok") == 2)
                    {
                        HandleBastokTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Kaede).OnTrade(q =>
                {
                    if (q.Quest.Var("npcBastok") == 3)
                    {
                        HandleBastokTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.PortBastok, Npc.PortBastok.Hilda).OnTrade(q =>
                {
                    if (q.Quest.Var("npcBastok") == 4)
                    {
                        HandleBastokTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Naji).OnTrade(q =>
                {
                    if (q.Quest.Var("npcBastok") == 5)
                    {
                        HandleBastokTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Raibaht).OnTrade(q =>
                {
                    if (q.Quest.Var("npcBastok") == 6)
                    {
                        HandleBastokTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Lucius).OnTrade(q =>
                {
                    if (q.Quest.Var("npcBastok") == 7)
                    {
                        HandleBastokTrade(q);
                    }
                    return null;
                })

                // Windurst zones and NPCs
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.KokoLihzeh).OnTrade(q =>
                {
                    if (q.Quest.Var("npcWindurst") == 0)
                    {
                        HandleWindurstTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.NaikoPaneiko).OnTrade(q =>
                {
                    if (q.Quest.Var("npcWindurst") == 1)
                    {
                        HandleWindurstTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Kerutoto).OnTrade(q =>
                {
                    if (q.Quest.Var("npcWindurst") == 2)
                    {
                        HandleWindurstTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KoruMoru).OnTrade(q =>
                {
                    if (q.Quest.Var("npcWindurst") == 3)
                    {
                        HandleWindurstTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Shanruru).OnTrade(q =>
                {
                    if (q.Quest.Var("npcWindurst") == 4)
                    {
                        HandleWindurstTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.BoizoNaizo).OnTrade(q =>
                {
                    if (q.Quest.Var("npcWindurst") == 5)
                    {
                        HandleWindurstTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.SolaJaab).OnTrade(q =>
                {
                    if (q.Quest.Var("npcWindurst") == 6)
                    {
                        HandleWindurstTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Kupipi).OnTrade(q =>
                {
                    if (q.Quest.Var("npcWindurst") == 7)
                    {
                        HandleWindurstTrade(q);
                    }
                    return null;
                })

                // Otherlands (Selbina and Mhaura) zones and NPCs
                .Npc(Zone.Selbina, Npc.Selbina.Mathilde).OnTrade(q =>
                {
                    if (q.Quest.Var("npcOtherlands") == 0)
                    {
                        HandleOtherlandsTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.Selbina, Npc.Selbina.Vobo).OnTrade(q =>
                {
                    if (q.Quest.Var("npcOtherlands") == 1)
                    {
                        HandleOtherlandsTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.Selbina, Npc.Selbina.Melyon).OnTrade(q =>
                {
                    if (q.Quest.Var("npcOtherlands") == 2)
                    {
                        HandleOtherlandsTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.Selbina, Npc.Selbina.Gabwaleid).OnTrade(q =>
                {
                    if (q.Quest.Var("npcOtherlands") == 3)
                    {
                        HandleOtherlandsTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.Mhaura, Npc.Mhaura.Celestina).OnTrade(q =>
                {
                    if (q.Quest.Var("npcOtherlands") == 4)
                    {
                        HandleOtherlandsTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.Mhaura, Npc.Mhaura.Ekokoko).OnTrade(q =>
                {
                    if (q.Quest.Var("npcOtherlands") == 5)
                    {
                        HandleOtherlandsTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.Mhaura, Npc.Mhaura.BihoroGuhoro).OnTrade(q =>
                {
                    if (q.Quest.Var("npcOtherlands") == 6)
                    {
                        HandleOtherlandsTrade(q);
                    }
                    return null;
                })
                .Npc(Zone.Mhaura, Npc.Mhaura.JikkaAbukka).OnTrade(q =>
                {
                    if (q.Quest.Var("npcOtherlands") == 7)
                    {
                        HandleOtherlandsTrade(q);
                    }
                    return null;
                });
        }
    }
}
