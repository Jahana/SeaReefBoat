using System;
using System.Collections.Generic;
using XI.Ipc;
using XI.Script;
using XI.World;

namespace XI.Zone
{
    // A kill's rewards (CMobEntity::DistributeRewards) and its experience points: charutils::LoadExpTable's script
    // tables, CheckMob, GetBaseExp and DistributeExperiencePoints, which grades each member's share with
    // xi.experiencePoints.calculate
    public sealed partial class ZoneHost
    {
        // EMobDifficulty (xi.mobDifficulty)
        public const byte MobTooWeak = 0, MobIncrediblyEasyPrey = 1, MobEasyPrey = 2, MobDecentChallenge = 3, MobEvenMatch = 4,
                          MobTough = 5, MobVeryTough = 6, MobIncrediblyTough = 7;
        private const int ExpTableRows = 60, ExpTableColumns = 20; // level difference -44..15, (level - 1) / 5
        private const float ExpRange = 100f;
        private const ushort MsgBasicTooFarForExp = 37, MsgBasicExpChain = 253, MsgBasicLimitChain = 372;
        private const ushort EffectLevelRestriction = 143, EffectSignet = 253, EffectBattlefield = 254, EffectSanction = 256;
        private const byte RegionRonfaure = 0, RegionJeuno = 22, RegionWestAhtUrhgan = 28, RegionAlzadaal = 32;
        private const int RoeEventMobKill = 1;
        private const byte AttackTypePhysical = 1; // xi::AttackType

        private readonly ushort[,] expTable_ = new ushort[ExpTableRows, ExpTableColumns];
        private (ushort Exp, byte Difficulty)[] expCurve_ = Array.Empty<(ushort, byte)>(); // highest first
        private (byte Level, ushort Exp) incrediblyEasyPrey_;

        // charutils::LoadExpTable's script part (GameData reads exp_base): the base experience by level difference
        // from xi.data.experiencePoints.baseTable, then the /check curve xi.expDifficultyCurve hands to
        // LoadExpDifficultyCurves
        private void LoadExpTables()
        {
            if (lua_ == null) return;
            lua_.SetGlobal("LoadExpDifficultyCurves", LoadExpDifficultyCurves);
            int top = lua_.Top;
            try
            {
                lua_.GetPath("xi.experiencePoints.calculate");
                bool calculate = lua_.IsFunction(-1);
                lua_.SetTop(top);
                if (!calculate)
                    log_("xi.experiencePoints.calculate function is not valid; experience points will not be calculated!");
                else
                {
                    lua_.GetPath("xi.data.experiencePoints.baseTable");
                    if (!lua_.IsTable(-1))
                        log_("xi.data.experiencePoints.baseTable is not valid; experience points will not be calculated!");
                    else
                    {
                        var table = new ushort[ExpTableRows, ExpTableColumns];
                        bool valid = true;
                        for (int dLevel = -44; dLevel <= 15 && valid; dLevel++)
                        {
                            lua_.RawGetI(-1, dLevel);
                            if (!lua_.IsTable(-1) || lua_.ObjLen(-1) != ExpTableColumns)
                            {
                                log_($"xi.data.experiencePoints.baseTable is missing a valid row for level difference {dLevel}.");
                                valid = false;
                            }
                            else
                            {
                                for (int y = 0; y < ExpTableColumns; y++)
                                {
                                    lua_.RawGetI(-1, y + 1);
                                    table[dLevel + 44, y] = (ushort)lua_.ToIntegral(-1);
                                    lua_.Pop(1);
                                }
                            }
                            lua_.Pop(1);
                        }
                        if (valid) Array.Copy(table, expTable_, table.Length);
                    }
                    lua_.SetTop(top);
                }

                lua_.GetPath("xi.expDifficultyCurve.loadExpDifficultyCurve");
                if (!lua_.IsFunction(-1)) log_("xi.expDifficultyCurve.loadExpDifficultyCurve function is not valid.");
                else lua_.Call(0, 0);
            }
            catch (LuaException e)
            {
                log_("xi.expDifficultyCurve.loadExpDifficultyCurve function failed to execute.");
                ScriptError("LoadExpTables", e);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::LoadExpDifficultyCurves(expToDifficultyTable, incrediblyEasyPreyLevel, incrediblyEasyPreyMinExp)
        private int LoadExpDifficultyCurves(LuaState l)
        {
            var curve = new List<(ushort Exp, byte Difficulty)>();
            if (l.IsTable(1))
            {
                l.PushNil();
                while (l.Next(1))
                {
                    curve.Add(((ushort)l.ToIntegral(-2), (byte)l.ToIntegral(-1)));
                    l.Pop(1);
                }
            }
            curve.Sort((a, b) => b.Exp.CompareTo(a.Exp));
            expCurve_ = curve.ToArray();
            incrediblyEasyPrey_ = ((byte)l.ToIntArg(2), (ushort)l.ToIntArg(3));
            return 0;
        }

        // charutils::GetBaseExp: pets past 99 count as 99
        private uint GetBaseExp(int charLvl, int mobLvl)
        {
            charLvl = Math.Min(charLvl, 99);
            if (charLvl <= 0) return 0;
            return expTable_[Math.Clamp(mobLvl - charLvl + 44, 0, ExpTableRows - 1), (charLvl - 1) / 5];
        }

        // charutils::CheckMob: the /check difficulty of the mob for a level
        private byte CheckMob(int charLvl, BattleEntity m)
        {
            int mobLvl = m.MLvl + m.GetMod(Mod.EXP_LVL_MOD);
            uint baseExp = GetBaseExp(charLvl, mobLvl);
            if (baseExp == 0) return MobTooWeak;
            foreach (var (exp, difficulty) in expCurve_)
                if (baseExp >= exp) return difficulty;
            if (baseExp >= incrediblyEasyPrey_.Exp && mobLvl >= incrediblyEasyPrey_.Level) return MobIncrediblyEasyPrey;
            return MobTooWeak;
        }

        // charutils::GetExpLevel: a level restriction with subPower 1 grades on the true level
        private static byte GetExpLevel(CharData c)
        {
            var restriction = c.StatusEffects.GetStatusEffect(EffectLevelRestriction);
            return restriction != null && restriction.SubPower == 1 ? c.JobLevels[c.MJob] : c.MLvl;
        }

        // ---- the kill ---------------------------------------------------------------------------------------------

        // CMobEntity::DistributeRewards: the owner's alliance gets the kill, then the owner's gil and drops (JP wiki: a
        // Monipulator never earns gil, and finds items only under Belligerency). Unless help was called, the BLUs may
        // learn its skills (ZoneHost.BlueLearning.cs).
        private void DistributeRewards(MobEntity m, long now)
        {
            var owner = m.OwnerId != 0 ? CharById(m.OwnerId).Item1 : null;
            if (owner == null)
            {
                OnMobDeath(m, null);
                return;
            }
            m.StatusEffects.KillAllStatusEffect();
            owner.History[HistoryEnemiesDefeated]++;

            // a kill under Signet counts for the beastmen's influence
            byte region = ZoneRegions.Of(m.Zone);
            if (owner.StatusEffects.HasStatusEffect(EffectSignet) && region <= RegionTavnazia)
                SendIpc(new ConquestEvent
                {
                    TypeField = (byte)ConquestMessage.M2W_AddMobKills,
                    Payload = new ConquestAddCounter { Count = 1, Region = region }.ToBytes(),
                });

            // for the owner's whole alliance
            OnMobDeath(m, owner);

            if (m.CallForHelp) return;
            TryLearningSpells(owner, m);
            m.UsedSkillIds.Clear();
            foreach (var member in ForAlliance(owner))
                // CBaseEntity::getZone: the zone id alone, so a member in another instance of the zone is
                // credited, as the C++
                if (ZoneOf(member) == ZoneOf(owner))
                    RoeEvent(RoeEventMobKill, member, new (string, object)[] { ("mob", m), ("atkType", m.LastHitAtkType) });

            if (m.GiveExp && !owner.StatusEffects.HasStatusEffect(EffectBattlefield))
            {
                DistributeExperiencePoints(owner, m, now);
                DistributeCapacityPoints(owner, m, now);
            }
            if (owner.Monstrosity != null)
            {
                if (owner.Monstrosity.Belligerency) DropItems(owner, m, now);
                return;
            }
            DistributeLoot(owner, m, now);
        }

        // charutils::DistributeExperiencePoints: graded against the highest level of the members within 100 yalms (and
        // the highest that ever fought it), shared by as many as were there. No level sync or pets are ported.
        private void DistributeExperiencePoints(CharData pc, MobEntity m, long now)
        {
            // a synced party gains nothing while the designee is far from the mob or down
            if (pc.Party?.SyncTarget is { } syncTarget && (Distance(m, syncTarget) >= ExpRange || syncTarget.Hp == 0))
            {
                foreach (var member in pc.Party.Members.ToArray())
                    if (SameArea(member, m) && Distance(member, m) < ExpRange)
                        PushTo(member, SelfPackets.BattleMessage(member, 0, 0, MsgBasicLevelSyncNoExp));
                return;
            }

            int pcInZone = 0;
            byte maxLevel = GetExpLevel(pc);
            byte region = ZoneRegions.Of(pc.Zone);
            var members = new List<CharData>(ForAlliance(pc));
            foreach (var member in members)
            {
                if (!SameArea(member, m) || Distance(member, m) >= ExpRange) continue;
                maxLevel = Math.Max(maxLevel, GetExpLevel(member));
                pcInZone++;
            }
            pcInZone = Math.Max(pcInZone, m.HiPartySize);
            maxLevel = (byte)Math.Min((int)Math.Max(maxLevel, m.HiPcLvl), 99);
            m.HiPartySize = (byte)pcInZone;
            m.HiPcLvl = maxLevel;

            var growth = loader_.Stats?.Growth;
            foreach (var member in members)
            {
                if (!member.IsAlive) continue;
                int mobLevel = m.MLvl + m.GetMod(Mod.EXP_LVL_MOD);
                byte memberLevel = GetExpLevel(member);
                byte mobCheck = CheckMob(maxLevel, m);
                if (mobCheck == MobTooWeak || !SameArea(member, m)) continue;
                if (Distance(member, m) > ExpRange)
                {
                    PushTo(member, SelfPackets.BattleMessage(member, 0, 0, MsgBasicTooFarForExp));
                    continue;
                }

                bool chainActive = member.ExpChainTimeMs > now;
                var result = CalculateExperiencePoints(member, m, new (string, object)[]
                {
                    ("baseExp", GetBaseExp(maxLevel, mobLevel)), ("mobDifficulty", mobCheck), ("memberLevel", memberLevel),
                    ("highestMemberLevel", maxLevel), ("partySize", pcInZone),
                    ("memberTNL", loader_.Stats?.ExpNextLevelFor(member, memberLevel) ?? 0),
                    ("highestMemberTNL", loader_.Stats?.ExpNextLevelFor(member, maxLevel) ?? 0),
                    ("regionId", region), ("chainNumber", member.ExpChainNumber), ("chainActive", chainActive),
                });
                if (result is not var (exp, wasChained, chainWindow)) continue;

                if (chainWindow > 0)
                {
                    member.ExpChainTimeMs = now + chainWindow * 1000L;
                    if (!wasChained) member.ExpChainNumber = 1;
                }
                AddExperiencePoints(false, member, m, exp, mobCheck, wasChained, true);
            }
        }

        // luautils::CalculateExperiencePoints: xi.experiencePoints.calculate(member, mob, data)
        private (uint Exp, bool WasChained, ushort ChainWindow)? CalculateExperiencePoints(CharData member, MobEntity m,
            IReadOnlyList<(string Key, object Value)> data)
        {
            if (lua_ == null) return null;
            const string path = "xi.experiencePoints.calculate";
            int top = lua_.Top;
            try
            {
                lua_.GetPath(path);
                if (lua_.IsNil(-1))
                {
                    log_($"luautils::callGlobalFunction: {path}: Function not found");
                    return null;
                }
                lua_.PushObject(member);
                lua_.PushObject(m);
                lua_.NewTable();
                foreach (var (key, value) in data)
                {
                    lua_.Push(value);
                    lua_.SetField(-2, key);
                }
                lua_.Call(3, 1);
                if (!lua_.IsTable(-1)) return null;
                double Field(string key)
                {
                    lua_.GetField(-1, key);
                    double v = lua_.IsNumber(-1) ? lua_.ToNumber(-1) : lua_.Type(-1) == 1 && lua_.ToBoolean(-1) ? 1 : 0;
                    lua_.Pop(1);
                    return v;
                }
                return ((uint)LuaState.LlRound(Field("exp")), Field("chainActive") != 0, (ushort)LuaState.LlRound(Field("chainWindow"))); // get_or(0)
            }
            catch (LuaException e)
            {
                ScriptError(path, e);
                return null;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // ---- conquest -------------------------------------------------------------------------------------------

        // conquest::AddConquestPoints: 15% of the EXP; a rank 2 or 3 nation's char earns 10% in a region its nation holds,
        // or under an alliance 20% in a rank 1 nation's region and 10% in the others; CONQUEST_BONUS adds to it
        private void AddConquestPoints(CharData c, uint exp)
        {
            byte region = ZoneRegions.Of(c.Zone);
            if (region == ZoneRegions.Unknown) return;
            var state = GetConquestData();
            byte owner = state.RegionOwner(region);
            byte nationRank = NationRank(state, c.Nation);
            double percentage = 0.15;
            if (owner <= Nation.Windurst && nationRank > 1)
            {
                if (IsConquestAlliance(state)) percentage = NationRank(state, owner) == 1 ? 0.2 : 0.1;
                else if (owner == c.Nation) percentage = 0.1;
            }
            percentage += c.GetMod(Mod.CONQUEST_BONUS) / 100.0;
            string column = ConquestPointsColumn(c);
            if (column != null) AddPoints(c, column, (int)(uint)(exp * percentage));
        }

        // conquest::GainInfluencePoints: the world server adds them to the char's nation in the region
        private void GainInfluencePoints(CharData c, uint points)
        {
            double percentage = 1.0 + c.GetMod(Mod.CONQUEST_REGION_BONUS) / 100.0;
            points = (uint)(points * percentage);
            SendIpc(new ConquestEvent
            {
                TypeField = (byte)ConquestMessage.M2W_AddInfluencePoints,
                Payload = new ConquestAddInfluencePoints { Points = (int)points, Nation = c.Nation, Region = ZoneRegions.Of(c.Zone) }.ToBytes(),
            });
        }

        // luautils::GetNationRank: the nation's place by regions held; two tied first both read second, three read third
        private static byte NationRank(ConquestState state, byte nation)
        {
            byte balance = ConquestPackets.Balance(state.RegionControlCount(Nation.Sandoria), state.RegionControlCount(Nation.Bastok),
                                                   state.RegionControlCount(Nation.Windurst));
            int sandoria = balance & 3, bastok = balance >> 2 & 3, windurst = balance >> 4 & 3;
            int rank = nation switch { Nation.Sandoria => sandoria, Nation.Bastok => bastok, Nation.Windurst => windurst, _ => -1 };
            if (rank < 0) return 0;
            if (rank == 1) rank = (sandoria == 1 ? 1 : 0) + (bastok == 1 ? 1 : 0) + (windurst == 1 ? 1 : 0);
            return (byte)rank;
        }

        // conquest::IsAlliance
        private static bool IsConquestAlliance(ConquestState state) =>
            ConquestPackets.Alliance(state.RegionControlCount(Nation.Sandoria), state.RegionControlCount(Nation.Bastok),
                                     state.RegionControlCount(Nation.Windurst)) > 0;

        // ---- mob bindings ---------------------------------------------------------------------------------------

        private const ushort MsgBasicLevelDown = 11;

        // charutils::DelExperiencePoints: a death's EXP loss (or a script's forced one), kept in expLost for a raise to
        // give part back; short of it the job falls a level. A Monipulator loses none.
        private void DelExperiencePoints(CharData c, float retainPercent, ushort forcedXpLoss)
        {
            var stats = loader_.Stats;
            if (stats == null) return;
            var set = stats.Settings;
            if (retainPercent > 1.0f || retainPercent < 0.0f)
            {
                log_($"Invalid retainPercent value ({retainPercent:F6}) received.");
                return;
            }
            if (set.ExpLossLevel > 99 || set.ExpLossLevel < 1)
            {
                log_($"Invalid EXP_LOSS_LEVEL setting value was obtained ({set.ExpLossLevel}).");
                return;
            }
            if (c.MLvl < set.ExpLossLevel && forcedXpLoss == 0) return;
            // MONs don't lose exp on death
            if (c.Monstrosity != null) return;

            uint ExpNext(int level) => stats.Growth.GetExpNextLevel(level);
            int job = c.MJob;
            int mLevel = c.LevelRestriction != 0 && c.LevelRestriction < c.MLvl ? (int)c.LevelRestriction : c.MLvl;
            ushort exploss = (ushort)(mLevel <= 67 ? ExpNext(mLevel) * 8 / 100 : 2400);
            if (mLevel <= 24 && set.UsePreAbysseaExpLossTiers) exploss = (ushort)(ExpNext(mLevel) * 10 / 100);
            if (forcedXpLoss > 0)
            {
                exploss = forcedXpLoss;
            }
            else
            {
                exploss = (ushort)(exploss * (1 - retainPercent));
                exploss = (ushort)(exploss * set.ExpLossRate);
            }
            SetCharVar(c, "expLost", exploss, 0);

            if (c.JobExp[job] < exploss)
            {
                if (c.JobLevels[job] > 1)
                {
                    int lowerLevelMaxExp = (int)ExpNext(c.JobLevels[job] - 1);
                    exploss = (ushort)(exploss - c.JobExp[job]);
                    c.JobExp[job] = (ushort)Math.Max(0, lowerLevelMaxExp - exploss);
                    c.JobLevels[job]--;
                    LevelDown(c, stats);
                }
                else
                {
                    c.JobExp[job] = 0;
                }
            }
            else
            {
                c.JobExp[job] = (ushort)(c.JobExp[job] - exploss);
            }
            SaveCharExp(c, job);
            PushTo(c, SelfPackets.CliStatus(c, stats, UnityStatus(c)));
        }

        // DelExperiencePoints' de-level, as LevelUp without restoring HP / MP: the gift mods refreshed, the traits rebuilt
        // (BuildingCharTraitsTable), the extended job packets sent; a sync designee's party is refreshed.
        private void LevelDown(CharData c, CharStats stats)
        {
            int job = c.MJob;
            if (c.LevelRestriction == 0 || c.JobLevels[job] < c.LevelRestriction)
            {
                SetMLevel(c, c.JobLevels[job]);
                SetSLevel(c, c.JobLevels[c.SJob], stats.Settings.SubjobRatio);
            }
            RefreshGiftMods(c);
            stats.BuildSkillsTable(c);
            loader_.CalculateStats(c);
            stats.CheckValidEquipment(c, true);
            loader_.BuildAbilityTable(c);
            BuildCharTraits(c);
            stats.BuildWeaponSkills(c);

            long now = UnixNow();
            PushTo(c, GameOkPackets.JobInfo(c, now));
            PushTo(c, CharStatus(c));
            PushTo(c, SelfPackets.CliStatus2(c));
            PushTo(c, AbilRecast(c));
            PushTo(c, LoginPackets.CommandData(c));
            PushTo(c, SelfPackets.Merits(c, stats));
            PushTo(c, MonPage1(c));
            PushTo(c, GameOkPackets.Monstrosity2(c));
            foreach (var p in ExtendedJobPackets(c)) PushTo(c, p);
            PushTo(c, CharSyncOf(c));
            CharStats.UpdateHealth(c);

            SaveCharStats(c);
            SaveCharJob(c, job);
            if (c.Party?.SyncTarget == c) c.Party.RefreshSync();
            c.Party?.ReloadParty();

            PushCharInRange(c, SelfPackets.BattleMessage2(c, c, c.JobLevels[job], 0, MsgBasicLevelDown), true);
            CallPlayerGlobal("xi.player.onPlayerLevelDown", c);
            c.UpdateMask |= CharStats.UpdateHp;
        }

        private void RegisterMobBindings(Dictionary<string, XI.Script.LuaFunction> m)
        {
            m["getFamily"] = l =>
            {
                if (Entity(l) is MobEntity mob) return Ret(l, mob.Family);
                log_("CLuaBaseEntity::getFamily() -  m_pBaseEntity is not a Mob.");
                return Ret(l, 0);
            };
            m["getSpecies"] = l =>
            {
                if (Entity(l) is MobEntity mob) return Ret(l, mob.Species);
                log_("CLuaBaseEntity::getSpecies() -  m_pBaseEntity is not a Mob.");
                return Ret(l, 0);
            };
            m["getEcosystem"] = l => Ret(l, Battle(l) is MobEntity mob ? mob.Ecosystem : 0);
            m["getPool"] = l => Ret(l, Entity(l) is MobEntity mob ? mob.Pool : 0);
            // CLuaBaseEntity::checkKillCredit(mob, minRange): the mob gives EXP and the char was within range (100)
            m["checkKillCredit"] = l =>
            {
                if (Entity(l) is not CharData c || l.ToObject(2) is not MobEntity mob)
                {
                    log_("CLuaBaseEntity::checkKillCredit() - Non-PC type calling function, or PLuaBaseEntity is not a MOB.");
                    return Ret(l, false);
                }
                return Ret(l, CheckKillCredit(c, mob, l.IsNumber(3) ? (float)l.ToNumber(3) : 100f));
            };
            // CLuaBaseEntity::checkDifficulty(mob)
            m["checkDifficulty"] = l =>
            {
                if (Pc(l, "checkDifficulty") is not CharData c || l.ToObject(2) is not MobEntity mob) return Ret(l, 0);
                return Ret(l, CheckMob(c.MLvl, mob));
            };
        }

        // CLuaBaseEntity::checkKillCredit: the mob gives EXP and the char was within range
        private bool CheckKillCredit(CharData c, MobEntity mob, float range = 100f)
        {
            bool credit = CheckMob(mob.HiPcLvl, mob) > MobTooWeak && Distance(mob, c) < range && !mob.CallForHelp;
            // a synced party's designee must be in range and up as well
            if (credit && c.Party?.SyncTarget is { } syncTarget)
                credit = Distance(mob, syncTarget) < range && syncTarget.Hp > 0;
            return credit;
        }
    }
}
