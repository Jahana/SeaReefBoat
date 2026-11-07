using System;
using System.Collections.Generic;
using XI.Map;
using XI.Script;

namespace XI.Zone
{
    // GP_CLI_COMMAND_ACTION Weaponskill (0x01A, ActionID 7): CPlayerController::WeaponSkill and CWeaponSkillState for
    // a char. The checks (the skill known and its skill level, Amnesia/Impairment, 1000 TP, a ranged weapon and ammo
    // for archery/marksmanship, the target and facing it) refuse with their battle messages; init pushes the "readies"
    // BATTLE2, and the next tick spends the TP (Meikyo Shisui, Sekkanoki, WS_NO_DEPLETE, CONSERVE_TP) and runs
    // CCharEntity::OnWeaponSkillFinished: the TP bonus (CalculateWeaponSkillTP), the range check (the out-of-range
    // interrupt), the target find (single, or the AoE around the target), the weapon skill script's onUseWeaponSkill
    // per target (onCriticalHit / onWeaponskillHit on the mob), the self-target MP recovery skills, the weapon skill
    // points of an unlockable weapon (AddWeaponSkillPoints), the skillchain (ZoneHost.Skillchains.cs) with its
    // map.WS_POINTS_SKILLCHAIN points, the claim and processActionEffectFlags; then Restraint,
    // the WEAPONSKILL_USE / WEAPONSKILL_TAKE listeners and the RoE event, and the state ends after the animation time.
    // The bindings the weapon skill scripts call come along: takeWeaponskillDamage (battleutils::TakeWeaponskillDamage),
    // physicalDmgTaken / rangedDmgTaken, getWeaponDamageType, getWeaponHitCount, removeAmmo, getWSSkillchainProp, and
    // the CLuaWeaponSkill type.
    // The latent effects are checked where C++ does (CheckLatentsTP, CheckLatentsWS, CheckLatentsWeaponBreak).
    // The script gets the Trick Attack char (getAvailableTrickAttackChar); a damaging hit may break bind
    // (BindBreakCheck); the start checks CanSeeTarget.
    // A char the char may harm is a target (PvP 2). Not ported: pets and trusts as targets, Cover (COVER_TO_MP) and
    // skipEvent.
    public sealed partial class ZoneHost
    {
        private const string WeaponSkillType = "CWeaponSkill";

        // msg_basic.h
        private const ushort MsgWsReadies = 43, MsgWsCannotPerformAction = 71, MsgWsUnableToUse = 89, MsgWsCannotUse = 190,
            MsgWsCannotUseAny = 191, MsgWsNotEnoughTp = 192, MsgWsNoRangedWeapon = 216, MsgWsCannotSee = 217,
            MsgWsUsesSkillRecoversMp = 224, MsgWsTargetRecoversMp = 276;

        // xi::StatusEffect ids EffectId lacks
        private const ushort WsEffectAmnesia = 16, WsEffectMeikyoShisui = 54, WsEffectDefenseBoost = 93, WsEffectImpairment = 261,
            WsEffectSekkanoki = 408, WsEffectFanDance = 411, WsEffectRestraint = 435, WsEffectHagakure = 483;

        // xi::AttackType
        private const byte WsAttackPhysical = 1, WsAttackMagical = 2, WsAttackRanged = 3, WsAttackBreath = 4, WsAttackSpecial = 5;

        private const int MeritOverwhelm = 2758; // xi::Merit::Overwhelm
        private const ushort AnimWsInterrupt = 508; // ActionAnimation::SkillInterrupt

        // CWeaponSkillState
        internal sealed class WeaponSkillState
        {
            public WeaponSkill Skill;
            public BattleEntity Target;
            public int Spent;
            public long EntryMs, FinishMs;
            public bool Completed;
        }

        private readonly HashSet<string> weaponSkillScripts_ = new HashSet<string>(StringComparer.Ordinal);

        // battleutils::isValidSelfTargetWeaponskill: Starlight, Moonlight, Dagan and Myrkr
        private static bool IsSelfTargetWeaponSkill(int wsid) => wsid is 163 or 164 or 173 or 190;

        // the ranged weapon skills (CalculateWeaponSkillTP's and OnWeaponSkillFinished's damage slot)
        private static bool IsRangedWeaponSkill(int wsid) => wsid >= 192 && wsid <= 221;

        // the GP_CLI_COMMAND_ACTION Weaponskill case: map.PREVENT_UNENGAGED_WS refuses one while not engaged
        private void OnWeaponSkillAction(MapSession s, CharData c, ushort actIndex, ushort wsid)
        {
            if (c.AttackTarget == null && ChatCfg.PreventUnengagedWs)
            {
                Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgWsUnableToUse));
                return;
            }
            WeaponSkillUse(s, c, actIndex, wsid);
        }

        // CPlayerController::WeaponSkill
        private void WeaponSkillUse(MapSession s, CharData c, ushort targid, ushort wsid)
        {
            // canAct and CanChangeState
            if (!CanAct(c) || !CanChangeState(c) || c.ItemUse != null)
            {
                Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgWsUnableToUse));
                return;
            }
            var stats = loader_.Stats as CharStats;
            var ws = stats?.WeaponSkills.Get(wsid);
            if (ws == null || !CharStats.HasWeaponSkill(c, ws.Id) || !stats.CanUseWeaponSkillLevel(c, wsid))
            {
                Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgWsCannotUse));
                return;
            }
            var impairment = c.StatusEffects.GetStatusEffect(WsEffectImpairment);
            if (c.StatusEffects.HasStatusEffect(WsEffectAmnesia) || impairment != null && (impairment.Power == 0x02 || impairment.Power == 0x03))
            {
                Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgWsCannotUseAny));
                return;
            }
            if (c.Tp < 1000)
            {
                Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgWsNotEnoughTp));
                return;
            }
            if (ws.Type == Skill.Archery || ws.Type == Skill.Marksmanship)
            {
                // before allowing ranged weapon skill...
                if (c.Equipped[CharStats.SlotAmmo] == null || c.RangedWeapon is not { Ranged: true } || c.AmmoWeapon is not { Ranged: true })
                {
                    Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgWsNoRangedWeapon));
                    return;
                }
            }

            var target = WsValidTarget(c, targid, IsSelfTargetWeaponSkill(wsid), out var errMsg);
            if (target != null)
            {
                if (IsUntargetable(target)) return;
                if (!Facing(c, target) && target != c)
                {
                    Push(s, SelfPackets.BattleMessage(c.Id, c.TargId, target.Id, target.TargId, 0, 0, MsgWsCannotSee));
                    return;
                }
                c.LastWeaponSkill = ws;
                StartWeaponSkill(s, c, target, ws);
            }
            else if (errMsg != null)
            {
                Push(s, errMsg);
            }
        }

        // CCharEntity::IsValidTarget(targid, TARGET_SELF or TARGET_ENEMY) -> applyTargetRestrictions
        // IsValidTarget(target, TARGET_SELF or TARGET_ENEMY) for a weapon skill or a ranged attack: a mob, or a char the
        // char may harm (PvP), as any other action's target
        private BattleEntity WsValidTarget(CharData c, ushort targid, bool self, out BasicPacket errMsg) =>
            AbilityValidTarget(c, targid, self ? TargetSelf : TargetEnemy, out errMsg);

        // CWeaponSkillState::init: the checks again, then the "readies" BATTLE2 to the char and those in range
        private void StartWeaponSkill(MapSession s, CharData c, BattleEntity target, WeaponSkill ws)
        {
            var again = WsValidTarget(c, target == c ? c.TargId : target.TargId, IsSelfTargetWeaponSkill(ws.Id), out var errMsg);
            if (again == null || errMsg != null)
            {
                if (errMsg != null) Push(s, errMsg);
                return;
            }
            if (!CanSeeTarget(c, target))
            {
                Push(s, SelfPackets.BattleMessage(c.Id, c.TargId, target.Id, target.TargId, 0, 0, MsgWsCannotPerformAction));
                return;
            }
            c.WsState = new WeaponSkillState { Skill = ws, Target = target, EntryMs = NowMs() };

            var action = new ActionPacket { ActorId = c.Id, ActionType = ActionCategory.SkillStart, ActionId = (uint)FourCC.SkillUse };
            var r = action.AddTarget(target.Id).AddResult();
            r.Param = ws.Id;
            r.MessageId = MsgWsReadies;
            PushCharInRange(c, Battle2Packets.Battle2(action), true);
        }

        private void TickWeaponSkills()
        {
            long now = NowMs();
            foreach (var (c, s) in new List<KeyValuePair<CharData, MapSession>>(charSessions_))
                if (c.WsState != null && UpdateWeaponSkill(s, c, c.WsState, now))
                    EndWeaponSkill(c, c.WsState, now);
        }

        // a Monipulator's monster skill state (CMobSkillState on a char); one that died drops it
        private void TickCharMobSkills()
        {
            long now = NowMs();
            foreach (var c in new List<CharData>(charSessions_.Keys))
            {
                if (c.SkillState == null) continue;
                if (!c.IsAlive) EndMobSkill(c, now);
                else TickMobSkill(c, now);
            }
        }

        // CWeaponSkillState::Update: true once the state is done
        private bool UpdateWeaponSkill(MapSession s, CharData c, WeaponSkillState st, long now)
        {
            if (c.IsAlive && !st.Completed)
            {
                var target = st.Target;
                var action = new ActionPacket();
                if (target != null && EntityExists(target) && target.IsAlive)
                {
                    SpendWeaponSkillCost(c, st);
                    OnCharWeaponSkillFinished(c, st, action);
                    if (action.Targets.Count > 0) PushCharInRange(c, Battle2Packets.Battle2(action), true);

                    if (c.StatusEffects.GetStatusEffect(WsEffectRestraint) is { } restraint)
                    {
                        ushort wsBonus = restraint.Power;
                        restraint.Power = 0;
                        restraint.SubPower = 0;
                        c.DelModifier(Mod.ALL_WSDMG_FIRST_HIT, (short)wsBonus);
                    }

                    // the category changes upon being out of range
                    if (action.ActionType == ActionCategory.SkillFinish)
                    {
                        uint weaponskillVar = LocalVars(target).TryGetValue("weaponskillHit", out var v) ? v : 0;
                        TriggerListener(c, "WEAPONSKILL_USE", c, target, st.Skill, st.Spent, action, (int)(weaponskillVar & 0xFFFFFF));
                        foreach (var t in action.Targets)
                            if (WsBattleById(t.ActorId) is { } pt)
                                TriggerListener(pt, "WEAPONSKILL_TAKE", c, pt, st.Skill, st.Spent, action);
                        RoeEvent(RoeEventWskillUse, c, new[] { ("skillType", (object)(int)st.Skill.Type) });
                    }
                }
                st.FinishMs = now + st.Skill.AnimationTimeMs;
                st.Completed = true;
                SendUpdateMask(s, c);
            }
            else if (now > st.FinishMs)
            {
                c.History[HistoryWsUsed]++;
                return true;
            }
            return false;
        }

        private const int RoeEventWskillUse = 2; // ROE_EVENT::ROE_WSKILL_USE

        // CWeaponSkillState::Cleanup; the suspended attack state resumes where its swing timer stood
        private void EndWeaponSkill(CharData c, WeaponSkillState st, long now)
        {
            c.WsState = null;
            if (c.AttackTarget != null) c.SwingAtMs += now - st.EntryMs;
            if (c.IsAlive) TriggerListener(c, "WEAPONSKILL_STATE_EXIT", c, (int)st.Skill.Id, st.Completed);
        }

        // CWeaponSkillState::SpendCost (a char's, or a trust's)
        private void SpendWeaponSkillCost(BattleEntity c, WeaponSkillState st)
        {
            var rng = MobRng;
            void Add(short v)
            {
                if (c is CharData pc) AddTp(pc, v);
                else if (c is MobEntity m) AddMobTp(m, v);
            }
            int tp;
            if (c.StatusEffects.HasStatusEffect(WsEffectMeikyoShisui))
            {
                tp = c.Tp;
                Add(-1000);
            }
            else if (c.StatusEffects.HasStatusEffect(WsEffectSekkanoki))
            {
                tp = 1000;
                Add(-1000);
                c.StatusEffects.DelStatusEffect(WsEffectSekkanoki);
            }
            else
            {
                tp = c.Tp;
                if (c.GetMod(Mod.WS_NO_DEPLETE) <= rng.Next(100)) Add((short)-tp);
            }
            if (rng.Next(100) < c.GetMod(Mod.CONSERVE_TP)) Add((short)rng.Next(10, 200));
            st.Spent = tp;
        }

        // battleutils::CalculateWeaponSkillTP
        private short CalculateWeaponSkillTP(CharData c, WeaponSkill ws, short spentTp)
        {
            short tp = (short)(spentTp + c.GetMod(Mod.TP_BONUS));
            bool ranged = IsRangedWeaponSkill(ws.Id);

            // remove TP Bonus from offhand weapon
            if (c.SubWeapon?.Equip is { } sub) tp -= (short)CharStats.ScaledItemModifier(c, sub, Mod.TP_BONUS);
            if (ranged)
            {
                // if ranged WS, remove TP bonus from mainhand weapon
                if (c.MainWeapon?.Equip is { } main) tp -= (short)CharStats.ScaledItemModifier(c, main, Mod.TP_BONUS);
            }
            else
            {
                // if melee WS, remove TP bonus from ranged weapon
                if (c.RangedWeapon?.Equip is { } rng) tp -= (short)CharStats.ScaledItemModifier(c, rng, Mod.TP_BONUS);

                // Add Fencer TP Bonus
                var pMain = c.MainWeapon;
                var pSub = c.Equipped[CharStats.SlotSub];
                if (pMain != null && !pMain.TwoHanded && !pMain.HandToHand &&
                    (pSub == null || c.SubWeapon is { Skill: Skill.None } || pSub.IsShield))
                    tp += c.GetMod(Mod.FENCER_TP_BONUS);
            }
            return Math.Min(tp, (short)3000);
        }

        // CCharEntity::OnWeaponSkillFinished (with CBattleEntity's)
        private void OnCharWeaponSkillFinished(CharData c, WeaponSkillState st, ActionPacket action)
        {
            var ws = st.Skill;
            action.ActorId = c.Id;
            action.ActionType = ActionCategory.SkillFinish;
            action.ActionId = ws.Id;

            var battleTarget = st.Target;
            short tp = CalculateWeaponSkillTP(c, ws, (short)st.Spent);
            c.Latents.CheckLatentsTP();
            c.Latents.CheckLatentsWS(true);

            int damslot = CharStats.SlotMain;
            if (Distance(c, battleTarget) <= ws.Range + HitboxSize(c) + HitboxSize(battleTarget))
            {
                if (IsRangedWeaponSkill(ws.Id)) damslot = CharStats.SlotRanged;

                var targets = WsTargetFind(c, battleTarget, ws);
                if (targets.Count == 0) return;

                foreach (var t in targets)
                {
                    bool primary = t == battleTarget;
                    var r = action.AddTarget(t.Id).AddResult();
                    r.Resolution = 0; // ActionResolution::Hit
                    r.Animation = ws.AnimationId;
                    int damage = OnUseWeaponSkill(c, t, ws, tp, primary, action);

                    if (!IsSelfTargetWeaponSkill(ws.Id))
                    {
                        if (primary && battleTarget is MobEntity bm) CallMobHook(bm, "onWeaponskillHit", bm, c, (int)ws.Id);
                    }
                    else
                    {
                        r.MessageId = primary ? MsgWsUsesSkillRecoversMp : MsgWsTargetRecoversMp;
                        r.Resolution = 0;
                        damage = Math.Max(damage, 0);
                        r.Param = BattleAddMp(t, damage);
                    }

                    if (primary)
                    {
                        bool isNegated = r.Resolution == 1 || r.Resolution == 3; // Miss, Parry
                        if (!isNegated)
                        {
                            int wspoints = ChatCfg.WsPointsBase;

                            // NOTE: GetSkillChainEffect is INSIDE SkillchainOnHit's checks because it
                            //  ALTERS the state of the resonance, which misses and non-elemental skills should NOT do.
                            byte effect = SkillchainOnHit(c, battleTarget, r, damage, ws.PrimarySc, ws.SecondarySc, ws.TertiarySc);
                            if (effect != ProcScNone)
                            {
                                // Despite appearances, ws_points_skillchain is not a multiplier it is just an amount "per skillchain level"
                                int wsPointsSkillchain = ChatCfg.WsPointsSkillchain;
                                if (effect >= ProcScCompression && effect < ProcScRadiance) wspoints += 1 * wsPointsSkillchain; // Level 1
                                else if (effect >= ProcScGravitation) wspoints += 2 * wsPointsSkillchain; // Level 2
                                else wspoints += 3 * wsPointsSkillchain; // Level 3
                            }
                            if (t is MobEntity tm && CheckMob(c.MLvl, tm) > MobTooWeak) AddWeaponSkillPoints(c, damslot, wspoints);
                        }
                    }
                }
                if (battleTarget is MobEntity claimed) ClaimMob(claimed, c, NowMs());
            }
            else
            {
                // ActionInterrupts::WeaponSkillOutOfRange
                var oor = new ActionPacket { ActorId = c.Id, ActionType = ActionCategory.MagicFinish };
                var r = oor.AddTarget(battleTarget.Id).AddResult();
                r.Animation = AnimWsInterrupt;
                r.MessageId = MsgTooFarAway;
                PushCharInRange(c, Battle2Packets.Battle2(oor), true);
            }

            c.Latents.CheckLatentsWS(false);
            ProcessActionEffectFlags(c, action);
        }

        // PAI->TargetFind: findSingleTarget, or findWithinArea(AOE_RADIUS::TARGET, radius) with FINDFLAGS_NONE and
        // TARGET_NONE: the target first, then the target's party with its trusts (a char's or a trust's own side), or
        // the mobs of the char's (the trust's master's) spawn list it may hit
        private List<BattleEntity> WsTargetFind(BattleEntity c, BattleEntity target, WeaponSkill ws)
        {
            var viewer = c as CharData ?? c.PMaster as CharData;
            var targets = new List<BattleEntity>();
            BattleEntity primary = null;
            (float X, float Y, float Z) around = (target.X, target.Y, target.Z);

            bool Valid(BattleEntity t)
            {
                if (t == null || targets.Contains(t)) return false;
                if (t is MobEntity tm && (tm.IsDead || !tm.IsSpawned)) return false;
                if (t is CharData tc && tc.Hp <= 0) return false;
                if (c.StatusEffects.GetConfrontationEffect() != t.StatusEffects.GetConfrontationEffect() || c.PBattlefield != t.PBattlefield ||
                    c.BattleId != t.BattleId)
                    return false;
                if (primary == t || !SameArea(t, c) || IsUntargetable(t)) return false;
                if (MathF.Abs(t.Y - around.Y) >= 8.0f) return false;
                // this is first target, always add him first
                if (primary == null) return true;
                if (EnmityContainer.AllegianceOf(primary) != EnmityContainer.AllegianceOf(t)) return false;
                float dx = t.X - around.X, dy = t.Y - around.Y, dz = t.Z - around.Z;
                return MathF.Sqrt(dx * dx + dy * dy + dz * dz) <= ws.Radius;
            }
            void Add(BattleEntity t)
            {
                if (Valid(t)) targets.Add(t);
            }

            if (ws.IsAoE)
            {
                Add(target);
                primary = target;
                if ((target.PMaster ?? target) is CharData tc)
                {
                    // PLAYER_PLAYER: the target's (master's) party with its trusts (addAllInParty), or it alone
                    if (tc.Party != null)
                        foreach (var member in PartyWithTrusts(tc))
                            if (member is not CharData { MoghouseId: not 0 }) Add(member);
                    else Add(tc);
                }
                else if (viewer != null)
                {
                    // PLAYER_MONSTER: addAllInMobList
                    foreach (var id in viewer.SpawnMobList)
                        if (MobById(id) is { } m && IsMobOwner(viewer, m)) Add(m);
                }
            }
            else
            {
                Add(target);
            }
            return targets;
        }

        // charutils::AddWeaponSkillPoints: an unlockable weapon not yet broken gains the points; at the cap the weapon
        // skills are rebuilt and the status and command lists sent again
        private bool AddWeaponSkillPoints(CharData c, int slot, int wspoints)
        {
            var weapon = CharStats.WeaponIn(c, slot);
            var item = CharStats.EquippedItem(c, slot);
            if (weapon == null || item == null || !weapon.IsUnlockable(item) || weapon.IsUnlocked(item)) return false;

            // CItemWeapon::addWsPoints, up to the item's total
            int total = weapon.UnlockPointsOf(item);
            int points = WeaponInfo.CurrentUnlockPoints(item) + wspoints;
            bool broken = points >= total;
            if (broken) points = total;
            item.Extra[0] = (byte)points;
            item.Extra[1] = (byte)(points >> 8);
            if (broken)
            {
                var stats = (CharStats)loader_.Stats;
                stats.BuildWeaponSkills(c);
                c.Latents.CheckLatentsWeaponBreak((byte)slot);
                PushTo(c, SelfPackets.CliStatus(c, stats, UnityStatus(c)));
                PushTo(c, LoginPackets.CommandData(c));
            }
            return true;
        }

        // luautils::OnUseWeaponSkill: xi.actions.weaponskills.<name>.onUseWeaponSkill(player, target, wsID, tp,
        // primary, action, taChar) returns tpHits, extraHits, criticalHit, damage; a critical runs the mob's
        // onCriticalHit. The damage is what comes back.
        private int OnUseWeaponSkill(BattleEntity c, BattleEntity t, WeaponSkill ws, short tp, bool primary, ActionPacket action)
        {
            if (lua_ == null) return 0;
            string file = $"./scripts/actions/weaponskills/{ws.Name}.lua";
            if (scripts_ != null && weaponSkillScripts_.Add(file)) scripts_.LoadLuaObjectFromFile(file);
            int top = lua_.Top;
            bool critical = false;
            int damage = 0;
            try
            {
                scripts_.PushFileFunction(file, "onUseWeaponSkill");
                if (lua_.IsNil(-1)) return 0;
                lua_.Push(c);
                lua_.Push(t);
                lua_.Push((double)ws.Id);
                lua_.Push((double)tp);
                lua_.Push(primary);
                lua_.Push(action);
                // taChar: battleutils::getAvailableTrickAttackChar(this, PTarget)
                if (TrickAttackChar(c, t) is { } taChar) lua_.Push(taChar);
                else lua_.PushNil();
                lua_.Call(7, 4);
                critical = lua_.Type(-2) == LuaTypeBoolean && lua_.ToBoolean(-2);
                damage = lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : 0;
            }
            catch (LuaException e)
            {
                log_($"luautils::onUseWeaponSkill: {e.Message}");
                return 0;
            }
            finally
            {
                lua_.SetTop(top);
            }
            if (critical && t is MobEntity m) CallMobHook(m, "onCriticalHit", m, c);
            return damage;
        }

        private const int LuaTypeBoolean = 1;

        // zoneutils::GetEntity for a battle entity: a mob, a char or a char's pet
        private BattleEntity WsBattleById(uint id) => (BattleEntity)MobById(id) ?? (BattleEntity)CharById(id).Item1 ?? PetById(id);

        // a char's pet by id, in its zone's pet list
        private PetEntity PetById(uint id) => HeldZone((ushort)(id >> 12 & 0xFFF))?.Pets.Find(p => p.Id == id);

        private void WsAddTp(BattleEntity b, short tp)
        {
            if (b is CharData c) AddTp(c, tp);
            else if (b is MobEntity m) AddMobTp(m, tp);
        }

        // ---- battleutils damage ------------------------------------------------------------------------------------

        // battleutils::TakeWeaponskillDamage
        private int TakeWeaponskillDamage(BattleEntity attacker, BattleEntity defender, int damage, byte attackType, byte damageType,
            int slot, bool primary, float tpMultiplier, ushort bonusTp, float targetTpMultiplier)
        {
            var rng = MobRng;
            bool isRanged = slot == CharStats.SlotAmmo || slot == CharStats.SlotRanged;

            if (attackType == WsAttackPhysical && defender.StatusEffects.GetStatusEffect(WsEffectDefenseBoost) is { SubPower: not 0 } boost &&
                Math.Abs(FacingAngle(PosOf(defender), PosOf(attacker))) < (byte)boost.SubPower / 2)
                damage = 0;

            // Handle damage nullification.
            if (attackType == WsAttackRanged && rng.Next(100) < defender.GetMod(Mod.NULL_RANGED_DAMAGE)) damage = 0;
            else if (attackType == WsAttackPhysical && rng.Next(100) < defender.GetMod(Mod.NULL_PHYSICAL_DAMAGE)) damage = 0;

            if (damage > 0)
            {
                damage = Math.Max(damage - defender.GetMod(Mod.PHALANX), 0);
                damage = WsHandleStoneskin(defender, damage, attackType);
            }

            if (!isRanged) damage = OverwhelmDamageBonus(attacker, defender, damage);

            // HandleAfflatusMiseryDamage
            if (damage > 0 && defender.StatusEffects.HasStatusEffect(EffectAfflatusMisery)) defender.SetModifier(Mod.AFFLATUS_MISERY, (short)damage);
            damage = Math.Clamp(damage, -99999, 99999);
            damage = MeleeFor().DamageCap(defender, damage);

            int corrected = BattleTakeDamage(defender, damage, attacker, attackType, damageType);
            if (damage < 0) damage = -corrected;

            if (attacker is CharData ac && defender is MobEntity claimed) ClaimMob(claimed, ac, NowMs());

            short standbyTp = 0;
            if (damage > 0)
            {
                defender.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Damage);
                // Check for bind breaking
                BindBreakCheck(defender);
                if (defender is MobEntity dm && dm.HiPcLvl < attacker.MLvl) dm.HiPcLvl = attacker.MLvl;
                TryHitInterrupt(defender, attacker);

                // Calculate TP Return from WS
                if (primary)
                {
                    short baseTp = (short)CalculateTpFromDamageDealt(attacker, slot);
                    standbyTp = (short)(bonusTp + (short)(tpMultiplier * baseTp));
                }

                // Add TP to defender
                int delay = isRanged ? BaseRangedDelay(attacker) : Melee.BaseDelay(attacker);
                short gained = (short)CalculateTpFromDamageTaken(attacker, defender, damage, delay);
                WsAddTp(defender, (short)(tpMultiplier * targetTpMultiplier * gained));
            }
            else if (defender is MobEntity dm)
            {
                dm.Enmity.UpdateEnmityFromDamage(attacker, 0);
            }

            WsAddTp(attacker, Math.Max((short)attacker.GetMod(Mod.SAVETP), standbyTp));

            // Remove Hagakure Effect if present
            if (attacker.StatusEffects.HasStatusEffect(WsEffectHagakure)) attacker.StatusEffects.DelStatusEffect(WsEffectHagakure);
            return damage;
        }

        // battleutils::HandleStoneskin with the attack type: a subtype 2 (magical only) stoneskin lets physical and
        // ranged damage through, a subtype 1 (physical only) one magical, breath and special damage
        private static int WsHandleStoneskin(BattleEntity defender, int damage, byte attackType)
        {
            if (damage <= 0 || defender.StatusEffects.GetStatusEffect(EffectId.Stoneskin) is not { } stoneskin) return damage;
            if (attackType != 0)
            {
                if (stoneskin.SubId == 2 && attackType is WsAttackPhysical or WsAttackRanged) return damage;
                if (stoneskin.SubId == 1 && attackType is WsAttackMagical or WsAttackBreath or WsAttackSpecial) return damage;
            }
            return defender.StatusEffects.HandleStoneskin(damage);
        }

        // battleutils::getOverWhelmDamageBonus: a char in front of the defender gains the Overwhelm merits' bonus
        private static int OverwhelmDamageBonus(BattleEntity attacker, BattleEntity defender, int damage)
        {
            if (attacker is not CharData c || Math.Abs(FacingAngle(PosOf(defender), PosOf(c))) >= 32) return damage;
            int meritCount = c.Merits?.Value(c, MeritOverwhelm) ?? 0;
            float tmpDamage = damage;
            tmpDamage += meritCount switch
            {
                1 => tmpDamage * 0.05f,
                2 => tmpDamage * 0.10f,
                3 => tmpDamage * 0.15f,
                4 => tmpDamage * 0.17f,
                5 => tmpDamage * 0.19f,
                _ => 0,
            };
            return (int)MathF.Floor(tmpDamage);
        }

        // battleutils::CalculateTPFromDamageDealt: xi.combat.tp.getSingleRangedHitTPReturn or
        // getSingleMeleeHitTPReturn (no Zanshin)
        private int CalculateTpFromDamageDealt(BattleEntity attacker, int slot) =>
            slot == CharStats.SlotRanged || slot == CharStats.SlotAmmo
                ? WsCallGlobalInt("xi.combat.tp.getSingleRangedHitTPReturn", attacker)
                : WsCallGlobalInt("xi.combat.tp.getSingleMeleeHitTPReturn", attacker, false);

        // battleutils::CalculateTPFromDamageTaken
        private int CalculateTpFromDamageTaken(BattleEntity attacker, BattleEntity defender, int damage, int delay) =>
            WsCallGlobalInt("xi.combat.tp.calculateTPGainOnPhysicalDamage", attacker, defender, (double)damage, (double)(ushort)delay);

        // luautils::callGlobal<int32>
        private int WsCallGlobalInt(string path, params object[] args)
        {
            if (lua_ == null) return 0;
            int top = lua_.Top;
            try
            {
                lua_.GetPath(path);
                if (lua_.IsNil(-1))
                {
                    log_($"luautils::callGlobalFunction: {path}: Function not found");
                    return 0;
                }
                foreach (var a in args) lua_.Push(a);
                lua_.Call(args.Length, 1);
                return lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : 0;
            }
            catch (LuaException e)
            {
                log_($"luautils::callGlobalFunction: {path}: {e.Message}");
                return 0;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // battleutils::PhysicalDmgTaken / RangedDmgTaken (not covered: COVER_TO_MP)
        private int WsDmgTaken(BattleEntity defender, int damage, bool ranged)
        {
            var rng = MobRng;
            float resist = Math.Max(1.0f + defender.GetMod(ranged ? Mod.UDMGRANGE : Mod.UDMGPHYS) / 10000.0f, 0.0f);
            damage = (int)(damage * resist);

            resist = 1.0f + defender.GetMod(ranged ? Mod.DMGRANGE : Mod.DMGPHYS) / 10000.0f + defender.GetMod(Mod.DMG) / 10000.0f;
            resist = Math.Max(resist, 0.5f); // PDT caps at -50%
            if (!ranged) resist += defender.GetMod(Mod.DMGPHYS_II) / 10000.0f; // Burtgang reduction after the 50% cap
            damage = (int)(damage * resist);

            if (damage > 0 && defender.GetMod(Mod.AUTO_EQUALIZER) > 0)
            {
                float reductionRate = MathF.Floor(damage / (float)defender.MaxHp * defender.GetMod(Mod.AUTO_EQUALIZER)) / 100.0f;
                damage = (int)MathF.Floor(damage * (1.0f - Math.Min(reductionRate, 0.90f)));
            }

            // Handle damage absorption.
            if (rng.Next(100) < defender.GetMod(Mod.ABSORB_DMG_CHANCE) || rng.Next(100) < defender.GetMod(Mod.PHYS_ABSORB))
            {
                damage = -damage;
            }
            // Handle damage nullification.
            else if (rng.Next(100) < defender.GetMod(Mod.NULL_DAMAGE) ||
                     rng.Next(100) < defender.GetMod(ranged ? Mod.NULL_RANGED_DAMAGE : Mod.NULL_PHYSICAL_DAMAGE))
            {
                damage = 0;
            }
            else
            {
                damage = HandleSevereDamage(defender, damage);

                // ConvertDmgToMP
                short absorbedMp = (short)(damage * (defender.GetMod(Mod.ABSORB_PHYSDMG_TO_MP) / 100.0));
                if (absorbedMp > 0) BattleAddMp(defender, absorbedMp);

                // HandleFanDance
                if (defender.StatusEffects.GetStatusEffect(WsEffectFanDance) is { } fanDance)
                {
                    int power = fanDance.Power;
                    damage = (int)(damage * (1.0f - power / 10000.0f));
                    // reduce fan dance effectiveness by 10% each hit, to a min of 20%
                    if (power > 2000) fanDance.Power = (ushort)(power - 1000);
                }
            }
            return MeleeFor().DamageCap(defender, damage);
        }

        // battleutils::RemoveAmmo: true when the last of the stack went (and the ammo slot was unequipped)
        private bool RemoveAmmo(CharData c, int quantity)
        {
            var item = CharStats.EquippedItem(c, CharStats.SlotAmmo);
            if (c.Equipped[CharStats.SlotAmmo] == null || item == null) return false;
            Container loc = item.Location;
            byte slot = item.Slot;
            bool last = (int)item.Quantity - quantity < 1;
            if (last && loader_.Stats is CharStats stats) stats.UnequipItem(c, CharStats.SlotAmmo);
            using (var t = new ItemTransaction(this, c))
            {
                if (!t.Take(loc, slot, (uint)quantity) || !t.Commit())
                    log_($"battleutils: {c.Name} did not spend {quantity} ammo in slot {slot}");
                else
                    PushTo(c, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
            }
            return last;
        }

        // ---- bindings -----------------------------------------------------------------------------------------------

        private void RegisterWeaponSkillBindings(Dictionary<string, LuaFunction> m)
        {
            bool NotNpc(LuaState l, out BattleEntity b)
            {
                b = Entity(l) as BattleEntity;
                if (b != null) return true;
                log_($"Invalid Entity (NPC: {NameOf(Entity(l))}) calling function.");
                return false;
            }

            m["takeWeaponskillDamage"] = l =>
            {
                if (Entity(l) is not BattleEntity defender)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return Ret(l, 0);
                }
                if (l.ToObject(2) is not BattleEntity attacker)
                {
                    log_($"Invalid entity type passed as Attacker ({NameOf(Entity(l))}).");
                    return Ret(l, 0);
                }
                return Ret(l, TakeWeaponskillDamage(attacker, defender, l.ToIntArg(3), (byte)l.ToIntArg(4), (byte)l.ToEnumArg(5),
                    (byte)l.ToIntArg(6), l.ToBoolean(7), (float)l.ToNumberArg(8), (ushort)l.ToIntArg(9), (float)l.ToNumberArg(10)));
            };
            m["physicalDmgTaken"] = l => Ret(l, NotNpc(l, out var b) ? WsDmgTaken(b, (int)l.ToNumberArg(2), false) : 0);
            m["rangedDmgTaken"] = l => Ret(l, NotNpc(l, out var b) ? WsDmgTaken(b, (int)l.ToNumberArg(2), true) : 0);
            m["getWeaponDamageType"] = l =>
            {
                if (!NotNpc(l, out var b)) return Ret(l, 0);
                int slot = l.ToIntArg(2);
                if (slot >= 0 && slot <= 3)
                    return Ret(l, b is CharData c ? (int)(CharStats.WeaponIn(c, slot)?.DmgType ?? 0) : 0);
                log_("lua::getWeaponDamageType :: Invalid slot specified!");
                return Ret(l, 0);
            };
            m["getWeaponHitCount"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return Ret(l, 0);
                }
                bool offhand = l.ToBoolean(2);
                if (offhand && c.Equipped[CharStats.SlotSub]?.Weapon is { } sub) return Ret(l, (int)sub.Hit);
                return Ret(l, c.Equipped[CharStats.SlotMain]?.Weapon is { } main ? main.Hit : 0);
            };
            m["removeAmmo"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return 0;
                }
                RemoveAmmo(c, l.IsNumber(2) ? (byte)l.ToIntArg(2) : 1);
                return 0;
            };
            m["getWSSkillchainProp"] = l =>
            {
                var ws = Entity(l) is CharData c ? c.WsState?.Skill : null;
                if (Entity(l) is not CharData) log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                l.Push((double)(ws?.PrimarySc ?? 0));
                l.Push((double)(ws?.SecondarySc ?? 0));
                l.Push((double)(ws?.TertiarySc ?? 0));
                return 3;
            };

            static WeaponSkill Skill(LuaState l) =>
                l.ToObject(1) as WeaponSkill ?? throw new InvalidOperationException("bad self: expected " + WeaponSkillType);
            lua_.RegisterType(WeaponSkillType, new Dictionary<string, LuaFunction>
            {
                ["isAoE"] = l => Ret(l, Skill(l).IsAoE),
                ["isSingle"] = l => Ret(l, !Skill(l).IsAoE),
                ["getID"] = l => Ret(l, (int)Skill(l).Id),
            });
        }
    }
}
