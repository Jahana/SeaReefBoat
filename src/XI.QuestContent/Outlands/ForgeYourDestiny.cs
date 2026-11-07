// Forge Your Destiny (Log ID 5, Quest ID 129), a line-for-line port of scripts/quests/outlands/Forge_Your_Destiny.lua
// (quest rework B3b Outlands, group 1). Norg: Jaucribaix !pos 91 -7 -8 (no script; default 71), Aeka !pos 4 0 -4 (default
// 75), Ranemaud !pos 15 0 23 (default 68). Konschtat Highlands: qm3 !pos -709 2 102 (no script; default
// BLACKENED_SPOT_ON_GROUND), the NM Forger. The Sanctuary of Zi'Tah: qm2 !pos 642 -5 -150 (no script; default
// BEAUTIFUL_STURDY_BRANCH), the NM Guardian Treant.
// Available from ADVANCED_JOB_LEVEL: Jaucribaix 25 (Sacred Branch, Lump of Bomb Steel), begun on option 1. Accepted: Aeka
// gives the Lump of Oriental Steel (44, Option bit 0; again for a Chunk of Darksteel Ore: 47 once bit 0 is set), Ranemaud the
// Sacred Sprig (40, bit 1; again for two Chunks of Gold Ore and a Chunk of Platinum Ore: 43 once bit 1 is set). The oriental
// steel traded at Konschtat's qm3 (within 1.6 yalms, the Forger down and its two-minute cooldown over) spawns the Forger
// (claimed); its despawn starts the cooldown (the qm's local var forgerNextPopAllowedTime = now + 120). A Hatchet traded at
// Zi'Tah's qm2 with the sprig held spawns the Guardian Treant (claimed; a ten-minute cooldown from its despawn); its death
// sets Prog 1, and the sprig traded there at Prog 1 gives the Sacred Branch (Prog 2). The bomb steel and the branch traded to
// Jaucribaix: 27, whose finish takes them and sets waitTime now + 10368 (three Vana'diel days); once it has passed, 29 runs
// quest:complete (Mumeito, the Samurai gesture, Bushido Blade, Norg fame 20) and on true unlocks Samurai.
// Kept as the Lua has it: Aeka's and Ranemaud's offers and the oriental steel / sprig checks use findItem (any container)
// while qm2's trade reads hasItem; Ranemaud says nothing (nil) once waitTime is set, Aeka plays 50; Jaucribaix's 28 passes
// timeRemaining / 144, a Lua float the event's parameter rounds half away from zero (ParseEvent's llround: 72 s is 1,
// 71 s is 0), so the C# rounds the same way;
// the Guardian Treant's death sets Prog 1 whoever claimed it and whatever the Prog (a second Treant at Prog 2 sets it back);
// qm2's sprig trade calls giveItem inside its condition (a full inventory: the branch not given, the trade answers nil); the
// Forger / Treant SpawnMob nil arm (an id with no mob) cannot be reached with the zone's IDs.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class ForgeYourDestiny : QuestScript
    {
        public ForgeYourDestiny() : base(QuestLog.Outlands, "FORGE_YOUR_DESTINY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/Forge_Your_Destiny";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Norg,
                Item = Item.Mumeito,
                KeyItem = KeyItem.JobGestureSamurai,
                Title = Title.BushidoBlade,
            });
            b.Steps(1, 2);

            b.State(QuestStatus.Available, when: q => q.Player.MainLevel >= q.Settings.MainNumber("ADVANCED_JOB_LEVEL"))
                .Npc(Zone.Norg, Npc.Norg.Jaucribaix).OnTrigger(QuestReply.ProgressEvent(25, Item.SacredBranch, Item.LumpOfBombSteel))
                .OnEventFinish(Zone.Norg, 25, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.KonschtatHighlands, Npc.KonschtatHighlands.qm3)
                    .OnTrade(q =>
                    {
                        if (q.Npc.PlayerDistance > 1.6)
                        {
                            return QuestReply.MessageSpecial(Text.KonschtatHighlands.BLACKENED_MUST_BE_CLOSER);
                        }

                        if (!q.Trade.Matches((Item.LumpOfOrientalSteel, 1)))
                        {
                            return QuestReply.NoAction();
                        }

                        if (q.Player.MobIsSpawned(Mob.KonschtatHighlands.FORGER) ||
                            q.Npc.LocalVar("forgerNextPopAllowedTime") > q.Player.SystemTime)
                        {
                            return QuestReply.MessageSpecial(Text.KonschtatHighlands.BLACKENED_NOTHING_HAPPENS, Item.LumpOfOrientalSteel);
                        }

                        // SpawnMob, the claim, the QMID and the DESPAWN listener (forgerNextPopAllowedTime = now + 120); the
                        // Lua sets the QMID and the listener after tradeComplete (no packet between)
                        if (!q.Player.SpawnMobWithNpcCooldown(Mob.KonschtatHighlands.FORGER, q.Npc.Id, "forgerNextPopAllowedTime", 120))
                        {
                            return QuestReply.NoAction();
                        }

                        q.Trade.Complete();

                        // QM is visible, but cannot be used to spawn Forger again until two minutes have elapsed since the NM despawns.
                        return QuestReply.MessageSpecial(Text.KonschtatHighlands.PLACE_BLACKENED_SPOT, Item.LumpOfOrientalSteel);
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Player.MobIsSpawned(Mob.KonschtatHighlands.FORGER))
                        {
                            return QuestReply.MessageSpecial(Text.KonschtatHighlands.NOT_THE_TIME_FOR_THAT);
                        }
                        // This message persists even after kill, while the QM is active and quest is accepted.
                        else if (q.Npc.LocalVar("forgerNextPopAllowedTime") <= q.Player.SystemTime)
                        {
                            return QuestReply.MessageSpecial(Text.KonschtatHighlands.BLACKENED_SHOULD_PLACE, Item.LumpOfOrientalSteel);
                        }
                        // Forger was killed and is still on its respawn cooldown.
                        else
                        {
                            return QuestReply.MessageSpecial(Text.KonschtatHighlands.BLACKENED_SPOT_ON_GROUND);
                        }
                    })
                .Npc(Zone.Norg, Npc.Norg.Aeka)
                    .OnTrade(q =>
                    {
                        if (q.Quest.IsVarBitsSet("Option", 0) &&
                            q.Trade.Matches((Item.ChunkOfDarksteelOre, 1)))
                        {
                            return QuestReply.ProgressEvent(47, 0, Item.LumpOfOrientalSteel, Item.ChunkOfDarksteelOre);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Var("waitTime") != 0)
                        {
                            return QuestReply.ProgressEvent(50);
                        }

                        if (q.Player.FindItem(Item.LumpOfBombSteel))
                        {
                            return QuestReply.ProgressEvent(49, Item.LumpOfBombSteel, Item.LumpOfOrientalSteel);
                        }
                        else if (!q.Player.FindItem(Item.LumpOfOrientalSteel))
                        {
                            if (!q.Quest.IsVarBitsSet("Option", 0))
                            {
                                return QuestReply.ProgressEvent(44, Item.LumpOfBombSteel, Item.LumpOfOrientalSteel);
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(46, 0, Item.LumpOfOrientalSteel, Item.ChunkOfDarksteelOre);
                            }
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(45, Item.LumpOfBombSteel, Item.LumpOfOrientalSteel);
                        }
                    })
                .Npc(Zone.Norg, Npc.Norg.Jaucribaix)
                    .OnTrade(q =>
                    {
                        if (q.Trade.Matches((Item.LumpOfBombSteel, 1), (Item.SacredBranch, 1)))
                        {
                            return QuestReply.ProgressEvent(27);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int waitTime = q.Quest.Var("waitTime");
                        int timeRemaining = waitTime - q.Player.SystemTime;

                        if (waitTime == 0)
                        {
                            return QuestReply.ProgressEvent(26);
                        }
                        else if (timeRemaining > 0)
                        {
                            // Parameter is remaining time in Vana'diel hours.
                            // Lua: timeRemaining / 144, a float the event parameter rounds half away from zero (llround)
                            return QuestReply.ProgressEvent(28, (int)System.Math.Round(timeRemaining / 144.0, System.MidpointRounding.AwayFromZero));
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(29, Item.Mumeito);
                        }
                    })
                .Npc(Zone.Norg, Npc.Norg.Ranemaud)
                    .OnTrade(q =>
                    {
                        if (q.Quest.IsVarBitsSet("Option", 1) &&
                            q.Trade.ItemQty(Item.ChunkOfGoldOre) == 2 &&
                            q.Trade.ItemQty(Item.ChunkOfPlatinumOre) == 1 &&
                            q.Trade.ItemCount == 3)
                        {
                            return QuestReply.ProgressEvent(43, 0, 0, Item.ChunkOfPlatinumOre, Item.ChunkOfGoldOre);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Var("waitTime") == 0)
                        {
                            if (q.Player.FindItem(Item.SacredBranch))
                            {
                                return QuestReply.ProgressEvent(48, Item.SacredBranch, Item.SacredSprig);
                            }
                            else if (!q.Player.FindItem(Item.SacredSprig))
                            {
                                if (!q.Quest.IsVarBitsSet("Option", 1))
                                {
                                    return QuestReply.ProgressEvent(40, Item.SacredBranch, Item.SacredSprig);
                                }
                                else
                                {
                                    return QuestReply.ProgressEvent(42, 0, Item.SacredSprig, Item.ChunkOfPlatinumOre, Item.ChunkOfGoldOre);
                                }
                            }
                            else
                            {
                                return QuestReply.ProgressEvent(41);
                            }
                        }
                        return null;
                    })
                .OnEventFinish(Zone.Norg, 27, q =>
                {
                    // TODO: Add constant for Vana'diel day in seconds, this is a three game day wait.
                    q.Trade.Complete();
                    q.Quest.SetVar("waitTime", q.Player.SystemTime + 10368);
                })
                .OnEventFinish(Zone.Norg, 29, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Text.Special(Text.Norg.YOU_CAN_NOW_BECOME_A_SAMURAI, Item.Mumeito);
                        q.Player.UnlockJob(Job.Sam);
                    }
                })
                .OnEventFinish(Zone.Norg, 40, q =>
                {
                    if (q.Player.GiveItem(Item.SacredSprig))
                    {
                        q.Quest.SetVarBit("Option", 1);
                    }
                })
                .OnEventFinish(Zone.Norg, 43, q =>
                {
                    if (q.Player.GiveItem(Item.SacredSprig))
                    {
                        q.Trade.Complete();
                    }
                })
                .OnEventFinish(Zone.Norg, 44, q =>
                {
                    if (q.Player.GiveItem(Item.LumpOfOrientalSteel))
                    {
                        q.Quest.SetVarBit("Option", 0);
                    }
                })
                .OnEventFinish(Zone.Norg, 47, q =>
                {
                    if (q.Player.GiveItem(Item.LumpOfOrientalSteel))
                    {
                        q.Trade.Complete();
                    }
                })
                .Npc(Zone.TheSanctuaryOfZitah, Npc.TheSanctuaryOfZitah.GuardianTreant).OnMobDeath(q =>
                {
                    q.Quest.SetStep(1);
                })
                .Npc(Zone.TheSanctuaryOfZitah, Npc.TheSanctuaryOfZitah.qm2)
                    .OnTrade(q =>
                    {
                        if (q.Player.HasItem(Item.SacredSprig) &&
                            q.Trade.ItemQty(Item.Hatchet) == 1 &&
                            q.Trade.ItemCount == 1)
                        {
                            if (q.Player.MobIsSpawned(Mob.TheSanctuaryOfZitah.GUARDIAN_TREANT) ||
                                q.Npc.LocalVar("treantNextPopAllowedTime") > q.Player.SystemTime)
                            {
                                return QuestReply.MessageSpecial(Text.TheSanctuaryOfZitah.STRANGE_FORCE_PREVENTS);
                            }
                            else
                            {
                                // as Konschtat's qm3: the listener sets treantNextPopAllowedTime = GetSystemTime() + 60 * 10
                                if (!q.Player.SpawnMobWithNpcCooldown(Mob.TheSanctuaryOfZitah.GUARDIAN_TREANT, q.Npc.Id, "treantNextPopAllowedTime", 60 * 10))
                                {
                                    return QuestReply.NoAction();
                                }

                                q.Trade.Complete();

                                // QM is visible, but cannot be used to spawn Guardian Treant again until ten minutes have elapsed since the NM despawns.
                                return QuestReply.MessageSpecial(Text.TheSanctuaryOfZitah.SENSE_STRONG_EVIL_PRESENCE);
                            }
                        }
                        else if (
                            q.Trade.ItemQty(Item.SacredSprig) == 1 &&
                            q.Trade.ItemCount == 1 &&
                            q.Quest.Step == 1 &&
                            q.Player.GiveItem(Item.SacredBranch))
                        {
                            q.Quest.SetStep(2);
                            q.Trade.Complete();
                            return QuestReply.MessageSpecial(Text.TheSanctuaryOfZitah.STRANGE_FORCE_VANISHED, Item.SacredBranch);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int questProgress = q.Quest.Step;

                        if (q.Player.MobIsSpawned(Mob.TheSanctuaryOfZitah.GUARDIAN_TREANT))
                        {
                            return QuestReply.MessageSpecial(Text.TheSanctuaryOfZitah.NOT_THE_TIME_FOR_THAT);
                        }
                        else if (questProgress == 1)
                        {
                            return QuestReply.MessageSpecial(Text.TheSanctuaryOfZitah.NO_LONGER_SENSE_EVIL);
                        }
                        else if (questProgress == 2)
                        {
                            return QuestReply.MessageSpecial(Text.TheSanctuaryOfZitah.NEWLY_SPROUTED_GLOWING, Item.SacredSprig);
                        }
                        else if (q.Npc.LocalVar("treantNextPopAllowedTime") <= q.Player.SystemTime)
                        {
                            return QuestReply.MessageSpecial(Text.TheSanctuaryOfZitah.LOOKS_LIKE_STURDY_BRANCH, Item.Hatchet);
                        }
                        return null;
                    });
        }
    }
}
