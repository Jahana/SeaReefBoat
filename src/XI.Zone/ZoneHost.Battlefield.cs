using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // Battlefields (BCNMs):
    // - CBattlefieldHandler: a zone named by a bcnm_records row holds as many battlefields as the global
    //   onBattlefieldHandlerInitialize allows. HandleBattlefields runs on the zone tick: it ticks, cleans up and
    //   destroys battlefields, and kicks orphaned players out. LoadBattlefield / RegisterBattlefield,
    //   RemoveFromBattlefield and RestoreClearance live here too.
    // - CBattlefield: InsertEntity / RemoveEntity, ApplyLevelRestrictions, onTick, Cleanup (with the bcnm_records
    //   fastest time), CheckInProgress and handleDeath (each group's death / allDeath / randomDeath callbacks).
    // - The timer packet (GP_SERV_COMMAND_BATTLEFIELD 0x075) and the luautils hooks, through
    //   xi.battlefield.contents[id].
    // - CLuaBattlefield (CBattlefield in Lua), including addGroups. Entity bindings: getBattlefield,
    //   getRegisteredBattlefield, getBattlefieldID, registerBattlefield, battlefieldAtCapacity, enterBattlefield,
    //   leaveBattlefield, setEnteredBattlefield and hasEnteredBattlefield. Zone binding: getBattlefieldByInitiator.
    // - The zone hooks: CZone::CharZoneIn's clearance, CZone::DecreaseZoneCounter's WARPDC, CMobEntity::Die's
    //   handleDeath, the mob tick skipped for a battlefield being cleaned up, and the master's PBattlefield copied to
    //   its pet and trusts.
    // Allies come from mobutils::InstantiateAlly (ZoneHost.DynamicEntities.cs).
    // RemoveEntity clears the entity's (and a char's pet's) timer queue (PAI->ClearTimerQueue).
    // Not ported: a zone's own level restriction (no zone.yaml has one, so updateCharLevelRestriction only lifts a
    // battlefield's LevelRestriction).
    public sealed partial class ZoneHost
    {
        private const string BattlefieldLuaType = "CBattlefield";
        private const ushort EffectSjRestriction = 157;

        // ---- CBattlefieldHandler -----------------------------------------------------------------------------------

        // CZone::LoadZoneSettings: a zone some battlefield record names gets a handler
        private void BattlefieldLoadHandler(ZoneInfo info)
        {
            var r = Exec("SELECT name FROM bcnm_records WHERE zoneId = ? LIMIT 1", BoundValue.Of(info.Id));
            if (r == null || !r.Next() || string.IsNullOrEmpty(r.Get<string>("name"))) return;
            info.BattlefieldHandler = new BattlefieldHandler { MaxBattlefields = OnBattlefieldHandlerInitialize(info) };
        }

        // luautils::OnBattlefieldHandlerInitialize: the global's number, 3 without one
        private int OnBattlefieldHandlerInitialize(ZoneInfo info)
        {
            const int maxAreas = 3;
            if (lua_ == null) return maxAreas;
            int top = lua_.Top;
            try
            {
                lua_.GetGlobal("onBattlefieldHandlerInitialize");
                if (!lua_.IsFunction(-1)) return maxAreas;
                lua_.PushObject(info, WeatherZoneLuaType);
                lua_.Call(1, 1);
                return lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : maxAreas;
            }
            catch (LuaException e)
            {
                ScriptError("luautils::onBattlefieldHandlerInitialize", e);
                return maxAreas;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // CZone::ZoneServer: each zone's HandleBattlefields
        private void TickBattlefields()
        {
            long now = NowMs();
            foreach (var (info, _, _) in AllAreas())
                if (info.BattlefieldHandler != null) HandleBattlefields(info, info.BattlefieldHandler, now);
        }

        // CBattlefieldHandler::HandleBattlefields
        private void HandleBattlefields(ZoneInfo info, BattlefieldHandler h, long now)
        {
            foreach (var bf in new List<Battlefield>(h.Battlefields.Values))
                if (!bf.CanCleanup()) BattlefieldOnTick(bf, now);

            foreach (var (area, bf) in new List<KeyValuePair<byte, Battlefield>>(h.Battlefields))
            {
                if (!bf.CanCleanup() || !BattlefieldCleanup(bf, now, false)) continue;
                log_($"[CBattlefieldHandler]HandleBattlefields cleaned up Battlefield {bf.Name}");
                if (h.Battlefields.TryGetValue(area, out var current) && current == bf) h.Battlefields.Remove(area);
                BattlefieldDestroy(bf);
            }

            for (int i = 0; i < h.OrphanedPlayers.Count;)
            {
                var (charId, atMs) = h.OrphanedPlayers[i];
                if (now < atMs)
                {
                    i++;
                    continue;
                }
                if (BattlefieldCharIn(info, charId) is CharData c)
                {
                    OnBattlefieldKick(c);
                    c.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Confrontation, EffectNotice.Silent);
                    UpdateCharLevelRestriction(c);
                }
                h.OrphanedPlayers.RemoveAt(i);
            }
        }

        // ~CBattlefield: the groups go, then luautils::OnBattlefieldDestroy
        private void BattlefieldDestroy(Battlefield bf)
        {
            foreach (var g in bf.Groups) BattlefieldUnrefGroup(g);
            bf.Groups.Clear();
            OnBattlefieldDestroy(bf);
            lua_?.Forget(bf);
        }

        // CBattlefieldHandler::LoadBattlefield
        private byte LoadBattlefield(ZoneInfo info, BattlefieldHandler h, CharData c, BattlefieldRegistration reg)
        {
            if (c.PBattlefield != null || h.Battlefields.Count >= h.MaxBattlefields) return Battlefield.ReturnWait;
            if (h.Battlefields.ContainsKey(reg.Area)) return Battlefield.ReturnIncrementRequest;
            // made it this far so looks like there's a free battlefield
            if (reg.Id == 0xFFFF) return Battlefield.ReturnCutscene;

            var r = Exec("SELECT name, fastestName, fastestTime, fastestPartySize FROM bcnm_records WHERE bcnmId = ?", BoundValue.Of(reg.Id));
            if (r == null || !r.Next())
            {
                log_($"Cannot load battlefield : {reg.Id} ");
                return Battlefield.ReturnReqsNotMet;
            }
            var bf = new Battlefield(reg.Id, info, reg.Area, c, NowMs())
            {
                Name = r.Get<string>("name") ?? "",
                LevelCap = (byte)reg.LevelCap,
                MaxParticipants = (int)reg.MaxPlayers,
                Rules = (ushort)reg.Rules,
                IsMission = reg.IsMission,
                ShowTimer = reg.ShowTimer,
            };
            bf.SetRecord(r.Get<string>("fastestName"), r.Get<uint>("fastestTime") * 1000L, (int)r.Get<uint>("fastestPartySize"));
            BattlefieldSetTimeLimit(bf, reg.TimeLimitSec * 1000L);

            h.Battlefields[bf.Area] = bf;

            if (c.StatusEffects.GetStatusEffect(EffectBattlefield) == null)
                c.StatusEffects.AddStatusEffect(new StatusEffect(EffectBattlefield, EffectBattlefield, bf.Id, 0, 0, c.Id, bf.Area), EffectNotice.Silent);

            OnBattlefieldRegister(c, bf);
            InvokeBattlefieldEvent(bf.Id, "onBattlefieldInitialize", () => lua_.PushObject(bf, BattlefieldLuaType), 1);
            BattlefieldInsertEntity(bf, c, true);
            return Battlefield.ReturnCutscene;
        }

        // CBattlefieldHandler::GetBattlefield: by registration and clearance, or by the entity inside one
        private static Battlefield GetBattlefield(BattlefieldHandler h, object e, bool checkRegistered = false)
        {
            if (checkRegistered && e is CharData c)
            {
                foreach (var bf in h.Battlefields.Values)
                    if (bf.IsRegistered(c) && bf.HasClearance(c)) return bf;
                return null;
            }
            foreach (var bf in h.Battlefields.Values)
                if (BattlefieldGetEntity(bf, e) != null) return bf;
            return null;
        }

        private static Battlefield GetBattlefieldByInitiator(BattlefieldHandler h, uint charId)
        {
            foreach (var bf in h.Battlefields.Values)
                if (bf.InitiatorId == charId) return bf;
            return null;
        }

        private static Battlefield GetRegisteredBattlefield(BattlefieldHandler h, CharData c)
        {
            foreach (var bf in h.Battlefields.Values)
                if (bf.IsRegistered(c)) return bf;
            return null;
        }

        // A registration the clearance effect does not name is left over from an earlier party
        private static void RemoveOtherRegistrations(BattlefieldHandler h, CharData c, Battlefield keep)
        {
            foreach (var bf in h.Battlefields.Values)
                if (bf != keep) bf.RegisteredPlayers.Remove(c.Id);
        }

        // CBattlefieldHandler::RegisterBattlefield
        private byte RegisterBattlefield(ZoneInfo info, BattlefieldHandler h, CharData c, BattlefieldRegistration reg)
        {
            if (c.PBattlefield != null)
            {
                log_($"{c.Name} tried to enter another battlefield");
                return Battlefield.ReturnWait;
            }
            // attempt to add to an existing battlefield
            var found = GetBattlefield(h, c, true);

            // Could not find this character registered, try find by id and initiator. This is how party
            // members get registered, after the initiator copied their clearance effect onto them.
            if (found == null)
            {
                foreach (var bf in h.Battlefields.Values)
                {
                    if (bf.InitiatorId == reg.Initiator && bf.Id == reg.Id && bf.HasClearance(c))
                    {
                        found = bf;
                        break;
                    }
                }
                if (found == null)
                {
                    // ...but they do have the BCNM Status Effect somehow (This should not happen, but keeping to be safe)
                    if (c.StatusEffects.HasStatusEffect(EffectBattlefield)) return Battlefield.ReturnReqsNotMet;
                    // ...but they opened the menu with clearance that the fight then took away. Retail answers
                    // WAIT for the other arenas and LOCKED for the party's own.
                    if (c.LocalVars.GetValueOrDefault("[BCNM]EnterExisting") == 1)
                    {
                        var locked = GetRegisteredBattlefield(h, c);
                        if (locked != null && reg.Area != locked.Area) return Battlefield.ReturnWait;
                        c.LocalVars["[BCNM]EnterExisting"] = 0;
                        return locked != null ? Battlefield.ReturnLocked : Battlefield.ReturnReqsNotMet;
                    }
                }
            }
            // Registered battlefields are only found through the Battlefield Status Effect that names them
            if (found != null)
            {
                RemoveOtherRegistrations(h, c, found);
                // Reset their progress var to 0 and proceed to attempt to enter them into the BCNM
                c.LocalVars["[BCNM]EnterExisting"] = 0;
                // can't enter, mobs been slapped
                if (BattlefieldCheckInProgress(found)) return Battlefield.ReturnLocked;
                // players haven't started fighting yet, try entering
                if (reg.Area != found.Area) return Battlefield.ReturnIncrementRequest;
                return BattlefieldInsertEntity(found, c, false) ? Battlefield.ReturnCutscene : Battlefield.ReturnBattlefieldFull;
            }

            RemoveOtherRegistrations(h, c, null);
            return LoadBattlefield(info, h, c, reg);
        }

        // CBattlefieldHandler::RemoveFromBattlefield
        private bool RemoveFromBattlefield(BattlefieldHandler h, object e, Battlefield bf, byte leavecode)
        {
            bf ??= GetBattlefield(h, e);
            return bf != null && BattlefieldRemoveEntity(bf, e, leavecode);
        }

        // CBattlefieldHandler::RestoreClearance: zoning out drops the clearance effect, a player still registered in
        // an open battlefield gets it back
        private void RestoreClearance(BattlefieldHandler h, CharData c)
        {
            if (c.StatusEffects.HasStatusEffect(EffectBattlefield)) return;
            foreach (var bf in h.Battlefields.Values)
            {
                if (bf.IsRegistered(c) && !BattlefieldCheckInProgress(bf))
                {
                    // CBattlefield::GrantClearance
                    c.StatusEffects.AddStatusEffect(new StatusEffect(EffectBattlefield, EffectBattlefield, bf.Id, 0, 0, bf.InitiatorId, bf.Area),
                                                    EffectNotice.Silent);
                    return;
                }
            }
        }

        // ---- zone hooks ----------------------------------------------------------------------------------------------

        // CZone::CharZoneIn: the clearance comes back, a char of a battlefield here goes back in, and any other
        // confrontation effect is lifted (an orphan inside the arena is kicked out 5 s later)
        private void BattlefieldCharZoneIn(CharData c)
        {
            var h = AreaOf(c).BattlefieldHandler;
            bool confrontation = c.StatusEffects.HasStatusEffectByFlag(EffectFlag.Confrontation);
            if (h != null)
            {
                RestoreClearance(h, c);
                var bf = GetBattlefield(h, c, true);
                if (bf != null && confrontation)
                {
                    BattlefieldInsertEntity(bf, c, Battlefield.HasPlayerEntered(c));
                }
                else if (confrontation)
                {
                    // Player is in a zone with a battlefield but they are not part of one.
                    if (Battlefield.HasPlayerEntered(c))
                    {
                        // If inside of the battlefield arena then kick them out
                        h.OrphanedPlayers.Add((c.Id, NowMs() + 5000));
                    }
                    else
                    {
                        c.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Confrontation, EffectNotice.Silent);
                        UpdateCharLevelRestriction(c);
                        c.PPet?.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Confrontation, EffectNotice.Silent);
                    }
                }
            }
            else if (confrontation)
            {
                // Player is zoning into a zone that does not have a battlefield but the player has a confrontation effect
                c.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Confrontation, EffectNotice.Silent);
                c.PPet?.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Confrontation, EffectNotice.Silent);
            }
            else if (c.StatusEffects.HasStatusEffect(EffectId.LevelSync) && c.Party == null)
            {
                // logging in with no party and a level sync status
                c.StatusEffects.DelStatusEffectSilent(EffectId.LevelSync);
                c.StatusEffects.DelStatusEffectSilent(EffectLevelRestriction);
            }
        }

        // CZoneEntities::DecreaseZoneCounter: a char leaving the zone leaves its battlefield (WARPDC)
        private void BattlefieldCharZoneOut(CharData c)
        {
            if (AreaOf(c).BattlefieldHandler is { } h) RemoveFromBattlefield(h, c, c.PBattlefield, Battlefield.LeaveWarpDc);
        }

        // CZone::updateCharLevelRestriction: no zone has a level restriction of its own, so a battlefield's comes off
        private static void UpdateCharLevelRestriction(CharData c)
        {
            var e = c.StatusEffects.GetStatusEffect(EffectLevelRestriction);
            if (e == null || e.Power == 0) return;
            c.StatusEffects.DelStatusEffect(EffectLevelRestriction);
        }

        // ---- CBattlefield ------------------------------------------------------------------------------------------

        // CZone::GetCharByID
        private CharData BattlefieldCharIn(ZoneInfo info, uint id) => CharById(id).Item1 is CharData c && InArea(c, info) ? c : null;

        private static Battlefield PBattlefieldOf(object e) => e switch
        {
            BattleEntity b => b.PBattlefield,
            NpcEntity n => n.PBattlefield,
            _ => null,
        };

        private static void SetPBattlefield(object e, Battlefield bf)
        {
            if (e is BattleEntity b) b.PBattlefield = bf;
            else if (e is NpcEntity n) n.PBattlefield = bf;
        }

        // CBattlefield::SetTimeLimit: the entered players' timers follow
        private void BattlefieldSetTimeLimit(Battlefield bf, long ms)
        {
            bf.TimeLimitMs = ms;
            bf.LastTimeUpdateSec = (uint)(ms / 1000);
            if (!bf.ShowTimer) return;
            foreach (var id in bf.EnteredPlayers)
                if (BattlefieldCharIn(bf.Zone, id) is CharData c) SendTimerPacket(c, (uint)(bf.RemainingTimeMs / 1000));
        }

        // CBattlefield::SetStatus
        private void BattlefieldSetStatus(Battlefield bf, byte status)
        {
            bf.Status = status;
            InvokeBattlefieldEvent(bf.Id, "onBattlefieldStatusChange", () =>
            {
                lua_.PushObject(bf, BattlefieldLuaType);
                lua_.Push((double)bf.Status);
            }, 2);
        }

        // CBattlefield::ApplyLevelRestrictions: the cap (and the buffs it strips), and the subjob unless allowed
        private void BattlefieldApplyLevelRestrictions(Battlefield bf, CharData c)
        {
            int cap = bf.LevelCap;
            if (cap > 0)
            {
                // We wait till here to do this because we don't want to modify uncapped battles.
                cap += ChatCfg.BattleCapTweak;
                // a mission without LV_CAP_MISSION_BCNM caps at the server's max level, which strips the buffs
                if (!ChatCfg.LvCapMissionBcnm && bf.IsMission) cap = ChatCfg.MaxLevel;

                c.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Dispelable, EffectNotice.Silent);
                c.StatusEffects.DelStatusEffectSilent(EffectReraise);
                c.Tp = 0;
                c.UpdateMask |= CharStats.UpdateHp;
                c.StatusEffects.AddStatusEffect(new StatusEffect(EffectLevelRestriction, EffectLevelRestriction, (ushort)cap, 0, 0));
            }
            else
            {
                c.StatusEffects.DelStatusEffect(EffectLevelRestriction);
            }

            // Check if we should remove SJ, whether or not there is a lv cap.
            if ((bf.Rules & Battlefield.RulesAllowSubjobs) == 0)
                c.StatusEffects.AddStatusEffect(new StatusEffect(EffectSjRestriction, EffectSjRestriction, 0, 0, 0));
        }

        // CBattlefield::InsertEntity
        private bool BattlefieldInsertEntity(Battlefield bf, object e, bool enter, byte conditions = Battlefield.ConditionNone, bool ally = false)
        {
            if (e == null)
            {
                log_("CBattlefield::InsertEntity() - PEntity is null.");
                return false;
            }
            if (PBattlefieldOf(e) != null) return false;

            switch (e)
            {
                case TrustEntity:
                    break;
                case CharData c:
                    if (bf.PlayerCount >= bf.MaxParticipants) return false;
                    if (enter)
                    {
                        BattlefieldApplyLevelRestrictions(bf, c);
                        bf.EnteredPlayers.Add(c.Id);
                        ClearTrusts(c);
                        InvokeBattlefieldEvent(bf.Id, "onBattlefieldEnter", () =>
                        {
                            lua_.PushObject(c);
                            lua_.PushObject(bf, BattlefieldLuaType);
                        }, 2);
                        if (bf.ShowTimer) SendTimerPacket(c, (uint)(bf.RemainingTimeMs / 1000));
                        // Try to add the player's pet in case they have one that can
                        if (c.PPet != null) BattlefieldInsertEntity(bf, c.PPet, true);
                    }
                    else if (!bf.IsRegistered(c))
                    {
                        bf.RegisteredPlayers.Add(c.Id);
                        return true;
                    }
                    break;
                case NpcEntity npc:
                    npc.Status = (conditions & Battlefield.ConditionDisappearAtStart) != 0 ? NpcEntity.StatusDisappear : NpcEntity.StatusNormal;
                    MarkUpdate(npc, EntityUpdatePacket.UpdateAllMob);
                    bf.NpcList.Add(npc);
                    break;
                case MobEntity m when ally:
                    bf.AllyList.Add(m);
                    break;
                case PetEntity pet when pet.PMaster is CharData:
                    // Properly set the existing pet to exist within this battlefield
                    pet.BcnmId = bf.Id;
                    pet.BattlefieldArea = bf.Area;
                    break;
                case MobEntity m:
                {
                    // only apply conditions to mobs spawning by default
                    var mob = new Battlefield.Mob { PMob = m, Condition = conditions };
                    m.BcnmId = bf.Id;
                    m.BattlefieldArea = bf.Area;
                    if ((conditions & Battlefield.ConditionWinRequirement) != 0) bf.RequiredEnemyList.Add(mob);
                    else bf.AdditionalEnemyList.Add(mob);
                    long now = NowMs();
                    // CBattleEntity::isAlive: not gone and in neither the death nor the despawn state, so a mob still
                    // fading out from the last cleanup is not killed again
                    if (!StatusEffectContainer.IsDead(m)) MobDie(m, now);
                    if ((conditions & Battlefield.ConditionSpawnedAtStart) != 0) SpawnMob(bf.Zone, m);
                    break;
                }
            }

            // set their battlefield to this as they're now physically inside that battlefield
            if (enter) SetPBattlefield(e, bf);

            // mob, initiator or ally: the Battlefield effect names the battlefield (Tango with a Tracker / Requiem of
            // Sin share NPC ids between battlefields, so an existing one takes the new id)
            if (e is BattleEntity b)
            {
                if (b.StatusEffects.GetStatusEffect(EffectBattlefield) is { } effect) effect.Power = bf.Id;
                else
                    b.StatusEffects.AddStatusEffect(new StatusEffect(EffectBattlefield, EffectBattlefield, bf.Id, 0, 0, bf.InitiatorId, bf.Area),
                                                    EffectNotice.Silent);
            }
            return true;
        }

        // CBattlefield::GetEntity
        private static object BattlefieldGetEntity(Battlefield bf, object e)
        {
            switch (e)
            {
                case CharData c:
                    return bf.EnteredPlayers.Contains(c.Id) ? c : null;
                case TrustEntity or PetEntity:
                {
                    var owner = ((MobEntity)e).PMaster as CharData;
                    return owner != null && bf.EnteredPlayers.Contains(owner.Id) ? owner : null;
                }
                case MobEntity m when m.Allegiance == AllegianceMob:
                    foreach (var mob in bf.AdditionalEnemyList)
                        if (mob.PMob.Id == m.Id) return mob.PMob;
                    foreach (var mob in bf.RequiredEnemyList)
                        if (mob.PMob.Id == m.Id) return mob.PMob;
                    return null;
                case MobEntity m when m.Allegiance == CharData.AllegiancePlayer:
                    foreach (var ally in bf.AllyList)
                        if (ally.Id == m.Id) return ally;
                    return null;
                case NpcEntity npc:
                    foreach (var n in bf.NpcList)
                        if (n.Id == npc.Id) return n;
                    return null;
            }
            return null;
        }

        // CAIContainer::ClearTimerQueue: the entity's queued timers (queueTimer / timer) dropped, its actions kept
        private void ClearTimerQueue(object e)
        {
            if (actionQueues_.TryGetValue(e, out var queue)) queue.ClearTimers(lua_);
        }

        // CBattlefield::RemoveEntity
        private bool BattlefieldRemoveEntity(Battlefield bf, object e, byte leavecode = 0)
        {
            // player's already zoned, we don't need to do anything
            if (e == null) return false;

            // Clear timer queue for entity before removal (PAI->ClearTimerQueue), and the char's pet's
            ClearTimerQueue(e);
            if (e is CharData { PPet: { } timerPet }) ClearTimerQueue(timerPet);

            bool found = false;
            if (e is CharData c)
            {
                if ((bf.Rules & Battlefield.RulesAllowSubjobs) == 0) c.StatusEffects.DelStatusEffect(EffectSjRestriction);

                // Release charmed pet when master leaves battlefield
                if (c.PPet is { IsCharmed: true }) DetachPet(c);

                // Remove Battlefield effect from Trusts
                foreach (var trust in c.PTrusts)
                    trust.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Confrontation, EffectNotice.Silent);

                UpdateCharLevelRestriction(c);

                if (leavecode == Battlefield.LeaveExit && c.StatusEffects.HasStatusEffectByFlag(EffectFlag.Confrontation))
                {
                    if (bf.Status == Battlefield.StatusLocked)
                        c.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Confrontation, EffectNotice.Silent);
                    else
                        Battlefield.SetPlayerEntered(c, false);
                }

                // CDeathState::allowSendRaise
                if (c.DeathAtMs != 0)
                {
                    c.RaiseMenuAtMs = NowMs() + RaisePromptAgainMs;
                    c.RaiseSent = false;
                }

                bf.EnteredPlayers.Remove(c.Id);
                SendClearTimerPacket(c);

                // Remove enmity from character and their pet with all mobs
                foreach (var mob in bf.Enemies())
                {
                    if (c.PPet != null) mob.Enmity.Clear(c.PPet.Id);
                    mob.Enmity.Clear(c.Id);
                }
            }
            else
            {
                if (e is NpcEntity npc)
                {
                    npc.Status = NpcEntity.StatusDisappear;
                    MarkUpdate(npc, EntityUpdatePacket.UpdateAllMob);
                    found = bf.NpcList.Remove(npc);
                }
                else if (e is MobEntity m)
                {
                    // allies targid >= 0x700
                    if (m.TargId >= 0x700)
                    {
                        // Disappear pets that do not belong to players
                        if (m is PetEntity pet && pet.PMaster is not CharData) m.Status = NpcEntity.StatusDisappear;
                        if (bf.AllyList.Contains(m))
                        {
                            // no isAlive check: an ally can be dead at cleanup but not despawned (Prishe in Dawn)
                            if (m.IsSpawned)
                            {
                                m.Status = NpcEntity.StatusDisappear;
                                m.UpdateMask |= EntityUpdatePacket.UpdateHp;
                            }
                            found = bf.AllyList.Remove(m);
                        }
                    }
                    else
                    {
                        found |= bf.RequiredEnemyList.RemoveAll(x => x.PMob == m) > 0;
                        found |= bf.AdditionalEnemyList.RemoveAll(x => x.PMob == m) > 0;
                    }
                    // Clear the mob's enmity
                    m.Enmity.Clear();
                }
                PushCharsNear(e, Schedulor(e, e, "kesu"));
            }

            SetPBattlefield(e, null);
            return found;
        }

        // CBattlefield::onTick: the in-progress check, then once a second the tick, the fight's clock and
        // OnBattlefieldTick
        private void BattlefieldOnTick(Battlefield bf, long now)
        {
            if (!bf.Attacked) BattlefieldCheckInProgress(bf);
            if (now > bf.TickMs + 1000)
            {
                bf.TickMs = now;
                bf.FightTickMs = bf.Status == Battlefield.StatusLocked ? bf.FightTickMs : now;
                bf.FinishTimeMs = bf.Status >= Battlefield.StatusWon ? bf.FightTickMs - bf.StartMs : bf.FinishTimeMs;
                // luautils::OnBattlefieldTick
                long seconds = bf.TimeInsideMs / 1000;
                if (InvokeBattlefieldEvent(bf.Id, "onBattlefieldTick", () =>
                    {
                        lua_.PushObject(bf, BattlefieldLuaType);
                        lua_.Push((double)seconds);
                    }, 2) != 0)
                    log_($"luautils::onBattlefieldTick: Unable to find onBattlefieldTick function for {bf.Name}");
            }
        }

        // CBattlefield::Cleanup: the players hear they are leaving and get 10 s, then the enemies despawn, everything
        // is removed, the registered players lose their clearance and a faster win is recorded
        private bool BattlefieldCleanup(Battlefield bf, long now, bool force)
        {
            // Wait until
            if (!force && bf.EnteredPlayers.Count > 0 && bf.CleanupTimeMs > now) return false;

            byte leavecode = bf.Status == Battlefield.StatusWon ? Battlefield.LeaveWin : Battlefield.LeaveLose;
            // First cleanup the players if they haven't been cleaned up yet
            if (!bf.CleanedPlayers)
            {
                foreach (var id in new List<uint>(bf.EnteredPlayers))
                {
                    var pc = BattlefieldCharIn(bf.Zone, id);
                    InvokeBattlefieldEvent(bf.Id, "onBattlefieldLeave", () =>
                    {
                        lua_.PushObject(pc);
                        lua_.PushObject(bf, BattlefieldLuaType);
                        lua_.Push((double)leavecode);
                    }, 3);
                }
                bf.CleanedPlayers = true;
                if (!force)
                {
                    bf.CleanupTimeMs = now + 10000;
                    return false;
                }
            }

            foreach (var mob in bf.Enemies())
            {
                // Negate the no despawn bit to allow mobs that may use no despawn mechanics to despawn properly
                const ushort behaviorNoDespawn = 0x001;
                mob.Behavior &= unchecked((ushort)~behaviorNoDespawn);
                if (mob.IsAlive && !mob.IsDead && mob.IsSpawned && mob.FadeAtMs == 0) DespawnMob(bf.Zone, mob, now);
            }

            if (bf.Status == Battlefield.StatusWon && bf.RecordTimeMs > bf.FinishTimeMs)
                bf.SetRecord(bf.InitiatorName, bf.FinishTimeMs, bf.EnteredPlayers.Count);

            var tempAllies = new List<MobEntity>(bf.AllyList);
            foreach (var mob in new List<MobEntity>(bf.Enemies())) BattlefieldRemoveEntity(bf, mob);
            foreach (var npc in new List<NpcEntity>(bf.NpcList)) BattlefieldRemoveEntity(bf, npc);
            foreach (var ally in tempAllies) BattlefieldRemoveEntity(bf, ally);
            leavecode = bf.Status == Battlefield.StatusWon ? Battlefield.LeaveWin : Battlefield.LeaveLose;
            foreach (var id in new List<uint>(bf.EnteredPlayers))
                if (BattlefieldCharIn(bf.Zone, id) is CharData pc) BattlefieldRemoveEntity(bf, pc, leavecode);

            // Remove all registered players as long as they're in the zone and still hold clearance for this battlefield
            foreach (var id in bf.RegisteredPlayers)
            {
                if (BattlefieldCharIn(bf.Zone, id) is not CharData pc || !bf.HasClearance(pc)) continue;
                pc.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Confrontation, EffectNotice.Silent);
                UpdateCharLevelRestriction(pc);
                // Remove allies from player's spawn list
                foreach (var ally in tempAllies) pc.SpawnMobList.Remove(ally.Id);
                pc.PPet?.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Confrontation, EffectNotice.Silent);
            }

            if (bf.Attacked && bf.Status == Battlefield.StatusWon)
            {
                bool updateRecord = true;
                var r = Exec("SELECT fastestTime FROM bcnm_records WHERE bcnmId = ? AND zoneId = ?", BoundValue.Of(bf.Id), BoundValue.Of(bf.Zone.Id));
                if (r != null && r.Next()) updateRecord = r.Get<uint>("fastestTime") > bf.RecordTimeMs / 1000;
                if (updateRecord)
                    Exec("UPDATE bcnm_records SET fastestName = ?, fastestTime = ?, fastestPartySize = ? WHERE bcnmId = ? AND zoneid = ?",
                         BoundValue.Of(bf.RecordName), BoundValue.Of((uint)(bf.RecordTimeMs / 1000)), BoundValue.Of((uint)bf.RecordPartySize),
                         BoundValue.Of(bf.Id), BoundValue.Of(bf.Zone.Id));
            }
            return true;
        }

        // CBattlefield::CheckInProgress: an enemy with enmity or a target locks an open battlefield
        private bool BattlefieldCheckInProgress(Battlefield bf)
        {
            foreach (var mob in bf.Enemies())
            {
                if (mob.Enmity.Count == 0 && mob.BattleTarget == null) continue;
                if (bf.Status == Battlefield.StatusOpen) BattlefieldSetStatus(bf, Battlefield.StatusLocked);
                bf.Attacked = true;
            }
            // mobs might have 0 enmity but we wont allow anymore players to enter
            return bf.Status != Battlefield.StatusOpen;
        }

        // CBattlefield::handleDeath: the dying mob's groups hear of it, with how many of each group are dead
        private void BattlefieldHandleDeath(Battlefield bf, MobEntity dying)
        {
            if (bf.Groups.Count == 0) return;
            // a copy, since the callbacks may change the groups
            foreach (var group in new List<Battlefield.Group>(bf.Groups))
            {
                int deathCount = 0;
                foreach (uint id in group.MobIds)
                    if (!bf.Zone.MobById.TryGetValue(id, out var m) || !m.IsAlive) deathCount++;
                if (!group.MobIds.Contains(dying.Id)) continue;

                if (group.DeathCallback != null)
                    BattlefieldCallGroup(bf, group.DeathCallback, "group.death", () =>
                    {
                        lua_.PushObject(dying);
                        lua_.Push((double)deathCount);
                    }, 2);
                if (group.AllDeathCallback != null && deathCount >= group.MobIds.Count)
                    BattlefieldCallGroup(bf, group.AllDeathCallback, "group.allDeath", () => lua_.PushObject(dying), 1);
                if (group.RandomDeathCallback != null && dying.Id == group.RandomMobId)
                    BattlefieldCallGroup(bf, group.RandomDeathCallback, "group.randomDeath", () => lua_.PushObject(dying), 1);
            }
        }

        // a group callback, (battlefield, ...)
        private void BattlefieldCallGroup(Battlefield bf, LuaRef fn, string what, Action pushArgs, int nargs)
        {
            int top = lua_.Top;
            try
            {
                lua_.PushRef(fn);
                lua_.PushObject(bf, BattlefieldLuaType);
                pushArgs?.Invoke();
                lua_.Call(nargs + 1, 0);
            }
            catch (LuaException e)
            {
                log_($"Error in battlefield {bf.Name} {what}: {e.Message}");
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        private void BattlefieldUnrefGroup(Battlefield.Group g)
        {
            if (lua_ == null) return;
            foreach (var r in new[] { g.DeathCallback, g.AllDeathCallback, g.RandomDeathCallback, g.SetupCallback })
                if (r != null) lua_.Unref(r);
            g.DeathCallback = g.AllDeathCallback = g.RandomDeathCallback = g.SetupCallback = null;
        }

        // ---- the timer packet and the luautils hooks -----------------------------------------------------------------

        // charutils::SendTimerPacket: GP_SERV_COMMAND_BATTLEFIELD's countdown
        private void SendTimerPacket(CharData c, uint seconds)
        {
            var p = new BattlefieldPacket();
            p.AddCountdown(seconds, 0, VanadielTimestampNow());
            PushTo(c, p.Packet);
        }

        // charutils::SendClearTimerPacket: an empty GP_SERV_COMMAND_BATTLEFIELD
        private void SendClearTimerPacket(CharData c) => PushTo(c, new PacketWriter(0x075, 4 + S2cBattlefield.SizeOf).Packet);

        // luautils::invokeBattlefieldEvent: xi.battlefield.contents[id][eventName](content, ...); -1 without one. A C#
        // battlefield's content is its proxy table (ZoneHost.BattlefieldScripts), whose function fields call its hooks
        private int InvokeBattlefieldEvent(ushort battlefieldId, string eventName, Action pushArgs, int nargs)
        {
            if (lua_ == null) return -1;
            int top = lua_.Top;
            try
            {
                lua_.GetPath("xi.battlefield.contents");
                if (!lua_.IsTable(-1)) return -1;
                lua_.Push((double)battlefieldId);
                lua_.GetTable(-2);
                if (!lua_.IsTable(-1)) return -1;
                lua_.GetField(-1, eventName);
                if (lua_.IsNil(-1)) return -1;
                lua_.PushValue(-2);
                pushArgs?.Invoke();
                lua_.Call(nargs + 1, 0);
                return 0;
            }
            catch (LuaException e)
            {
                log_($"luautils::{eventName}: {e.Message}");
                return -1;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnBattlefieldRegister
        private void OnBattlefieldRegister(CharData c, Battlefield bf) =>
            InvokeBattlefieldEvent(bf.Id, "onBattlefieldRegister", () =>
            {
                lua_.PushObject(c);
                lua_.PushObject(bf, BattlefieldLuaType);
            }, 2);

        // luautils::OnBattlefieldKick: the battlefield the clearance effect names
        private void OnBattlefieldKick(CharData c)
        {
            if (c.StatusEffects.GetStatusEffect(EffectBattlefield) is { } e)
                InvokeBattlefieldEvent(e.Power, "onBattlefieldKick", () => lua_.PushObject(c), 1);
        }

        // luautils::OnBattlefieldDestroy
        private void OnBattlefieldDestroy(Battlefield bf) =>
            InvokeBattlefieldEvent(bf.Id, "onBattlefieldDestroy", () => lua_.PushObject(bf, BattlefieldLuaType), 1);

        // ---- CLuaBattlefield -----------------------------------------------------------------------------------------

        private static Battlefield LuaBattlefield(LuaState l) =>
            l.ToObject(1) as Battlefield ?? throw new InvalidOperationException("bad self: expected " + BattlefieldLuaType);

        private void RegisterBattlefieldBindings(Dictionary<string, LuaFunction> m)
        {
            lua_.RegisterType(BattlefieldLuaType, new Dictionary<string, LuaFunction>
            {
                ["getID"] = l => Ret(l, LuaBattlefield(l).Id),
                ["getZoneID"] = l => Ret(l, LuaBattlefield(l).Zone.Id),
                ["getArea"] = l => Ret(l, LuaBattlefield(l).Area),
                ["getTimeLimit"] = l => Ret(l, LuaBattlefield(l).TimeLimitMs / 1000),
                ["getTimeInside"] = l => Ret(l, LuaBattlefield(l).TimeInsideMs / 1000),
                ["getRemainingTime"] = l => Ret(l, LuaBattlefield(l).RemainingTimeMs / 1000),
                ["getWipeTime"] = l => Ret(l, LuaBattlefield(l).WipeTimeSec),
                ["getMaxParticipants"] = l => Ret(l, LuaBattlefield(l).MaxParticipants),
                ["getPlayerCount"] = l => Ret(l, LuaBattlefield(l).PlayerCount),
                ["getPlayers"] = l =>
                {
                    var bf = LuaBattlefield(l);
                    var list = new List<object>();
                    foreach (var id in bf.EnteredPlayers)
                        if (BattlefieldCharIn(bf.Zone, id) is CharData c) list.Add(c);
                    return ZoneLuaPushArray(l, list);
                },
                ["getPlayersAndTrusts"] = l =>
                {
                    var bf = LuaBattlefield(l);
                    var list = new List<object>();
                    foreach (var id in bf.EnteredPlayers)
                    {
                        if (BattlefieldCharIn(bf.Zone, id) is not CharData c) continue;
                        list.Add(c);
                        list.AddRange(c.PTrusts);
                    }
                    return ZoneLuaPushArray(l, list);
                },
                ["getMobs"] = l =>
                {
                    var bf = LuaBattlefield(l);
                    var list = new List<object>();
                    if (l.ToBoolean(2))
                        foreach (var mob in bf.RequiredEnemyList) list.Add(mob.PMob);
                    if (l.ToBoolean(3))
                        foreach (var mob in bf.AdditionalEnemyList) list.Add(mob.PMob);
                    return ZoneLuaPushArray(l, list);
                },
                ["getNPCs"] = l => ZoneLuaPushArray(l, LuaBattlefield(l).NpcList),
                ["getAllies"] = l => ZoneLuaPushArray(l, LuaBattlefield(l).AllyList),
                ["getRecord"] = l =>
                {
                    var bf = LuaBattlefield(l);
                    l.PushString(bf.RecordName);
                    l.Push((double)(uint)(bf.RecordTimeMs / 1000));
                    l.Push((double)bf.RecordPartySize);
                    return 3;
                },
                ["getStatus"] = l => Ret(l, LuaBattlefield(l).Status),
                ["getLocalVar"] = l => Ret(l, LuaBattlefield(l).GetLocalVar(l.ToStringArg(2))),
                ["getLastTimeUpdate"] = l => Ret(l, LuaBattlefield(l).LastTimeUpdateSec),
                ["getInitiator"] = l =>
                {
                    var bf = LuaBattlefield(l);
                    l.Push((double)bf.InitiatorId);
                    l.PushString(bf.InitiatorName);
                    return 2;
                },
                ["getArmouryCrate"] = l => Ret(l, LuaBattlefield(l).ArmouryCrate),
                ["setLocalVar"] = l =>
                {
                    LuaBattlefield(l).LocalVars[l.ToStringArg(2)] = (ulong)l.ToIntegralArg(3); // uint64_t
                    return 0;
                },
                ["setLastTimeUpdate"] = l =>
                {
                    LuaBattlefield(l).LastTimeUpdateSec = l.ToUIntArg(2);
                    return 0;
                },
                ["setTimeLimit"] = l =>
                {
                    BattlefieldSetTimeLimit(LuaBattlefield(l), l.ToUIntArg(2) * 1000L);
                    return 0;
                },
                ["setWipeTime"] = l =>
                {
                    LuaBattlefield(l).WipeTimeSec = l.ToUIntArg(2);
                    return 0;
                },
                ["setRecord"] = l =>
                {
                    var bf = LuaBattlefield(l);
                    bf.SetRecord(l.ToStringArg(2), l.ToUIntArg(3) * 1000L, bf.PlayerCount);
                    return 0;
                },
                ["setStatus"] = l =>
                {
                    BattlefieldSetStatus(LuaBattlefield(l), (byte)l.ToIntArg(2));
                    return 0;
                },
                // insertEntity(targid, ally, inBattlefield): an ally is a new mob from the zone's mob group of that
                // id (mobutils::InstantiateAlly); anything else is the zone's entity by targid
                ["insertEntity"] = l =>
                {
                    var bf = LuaBattlefield(l);
                    ushort targid = (ushort)l.ToIntArg(2);
                    bool ally = l.ToBoolean(3), inBattlefield = l.ToBoolean(4);
                    object e = null;
                    e = ally ? InstantiateAlly(targid, bf.Zone.Id) : BattlefieldZoneEntity(bf.Zone, targid);
                    if (e == null)
                    {
                        log_($"CLuaBattlefield::insertEntity - targid ID {targid} not found!");
                        l.PushNil();
                        return 1;
                    }
                    BattlefieldInsertEntity(bf, e, inBattlefield, Battlefield.ConditionNone, ally);
                    l.PushObject(e);
                    return 1;
                },
                ["cleanup"] = l => Ret(l, LuaBattlefield(l).CanCleanup(l.ToBoolean(2))),
                ["win"] = l =>
                {
                    var bf = LuaBattlefield(l);
                    BattlefieldSetStatus(bf, Battlefield.StatusWon);
                    bf.CanCleanup(true);
                    return 0;
                },
                ["lose"] = l =>
                {
                    var bf = LuaBattlefield(l);
                    BattlefieldSetStatus(bf, Battlefield.StatusLost);
                    bf.CanCleanup(true);
                    return 0;
                },
                ["addGroups"] = l =>
                {
                    BattlefieldAddGroups(l, LuaBattlefield(l), l.ToBoolean(3));
                    return 0;
                },
            });

            // ---- CLuaBaseEntity --------------------------------------------------------------------------------------

            m["getBattlefield"] = l =>
            {
                l.PushObject(PBattlefieldOf(Entity(l)), BattlefieldLuaType);
                return 1;
            };
            m["getRegisteredBattlefield"] = l =>
            {
                var bf = Entity(l) is CharData c && AreaOf(c).BattlefieldHandler is { } h ? GetRegisteredBattlefield(h, c) : null;
                l.PushObject(bf, BattlefieldLuaType);
                return 1;
            };
            m["getBattlefieldID"] = l => Ret(l, PBattlefieldOf(Entity(l))?.Id ?? -1);
            m["registerBattlefield"] = l =>
            {
                var e = Entity(l);
                var info = AreaOf(e);
                if (info.BattlefieldHandler == null)
                {
                    log_($"m_BattlefieldHandler was null for {NameOf(e)}.");
                    return Ret(l, Battlefield.ReturnBattlefieldFull);
                }
                if (e is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(e)}).");
                    return Ret(l, Battlefield.ReturnBattlefieldFull);
                }
                if (l.IsNil(2))
                {
                    log_("CLuaBaseEntity::registerBattlefield() - Battlefield State was < 0.  Investigate!");
                    return Ret(l, Battlefield.ReturnBattlefieldFull);
                }
                var reg = new BattlefieldRegistration
                {
                    Id = (ushort)l.ToIntArg(2),
                    Area = l.IsNil(3) ? (byte)1 : (byte)l.ToIntArg(3),
                    Initiator = l.IsNil(4) ? 0 : l.ToUIntArg(4),
                };
                if (l.IsTable(5))
                {
                    reg.MaxPlayers = (uint)FieldInt(l, 5, "maxPlayers", 0);
                    reg.LevelCap = (uint)FieldInt(l, 5, "levelCap", 0);
                    reg.TimeLimitSec = (uint)FieldInt(l, 5, "timeLimit", 0);
                    reg.IsMission = FieldBool(l, 5, "isMission");
                    if (reg.IsMission && !ChatCfg.LvCapMissionBcnm) reg.LevelCap = (uint)ChatCfg.MaxLevel;
                    l.GetField(5, "showTimer");
                    reg.ShowTimer = l.IsNil(-1) || l.ToBoolean(-1);
                    l.Pop(1);
                    if (FieldBool(l, 5, "allowSubjob")) reg.Rules |= Battlefield.RulesAllowSubjobs;
                    if (FieldBool(l, 5, "canLoseExp")) reg.Rules |= Battlefield.RulesLoseExp;
                }
                return Ret(l, RegisterBattlefield(info, info.BattlefieldHandler, c, reg));
            };
            // battlefieldAtCapacity: every area taken
            m["battlefieldAtCapacity"] = l =>
            {
                var e = Entity(l);
                if (AreaOf(e).BattlefieldHandler is not { } h)
                {
                    log_($"m_BattlefieldHandler was null for {NameOf(e)}.");
                    return Ret(l, true);
                }
                if (e is not CharData)
                {
                    log_($"Invalid entity type calling function ({NameOf(e)}).");
                    return Ret(l, true);
                }
                return Ret(l, h.Battlefields.Count >= h.MaxBattlefields);
            };
            // enterBattlefield(area): the one the char is registered for, or the area's
            m["enterBattlefield"] = l =>
            {
                var e = Entity(l);
                if (e is not CharData c || AreaOf(c).BattlefieldHandler is not { } h)
                {
                    log_("CLuaBaseEntity::enterBattlefield() - Non-PC calling function, or Battlefield Handler is null.");
                    return Ret(l, false);
                }
                var bf = l.IsNil(2) ? GetBattlefield(h, c, true) : h.Battlefields.GetValueOrDefault((byte)l.ToIntArg(2));
                return Ret(l, bf != null && BattlefieldInsertEntity(bf, c, true));
            };
            m["leaveBattlefield"] = l =>
            {
                var e = Entity(l);
                if (e is NpcEntity || AreaOf(e).BattlefieldHandler is not { } h)
                {
                    log_("CLuaBaseEntity::leaveBattlefield() - NPC calling function, or Battlefield Handler is null.");
                    return Ret(l, false);
                }
                return Ret(l, RemoveFromBattlefield(h, e, PBattlefieldOf(e), (byte)l.ToIntArg(2)));
            };
            m["setEnteredBattlefield"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return 0;
                }
                if (c.StatusEffects.HasStatusEffect(EffectBattlefield)) Battlefield.SetPlayerEntered(c, l.ToBoolean(2));
                return 0;
            };
            m["hasEnteredBattlefield"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return Ret(l, false);
                }
                return Ret(l, Battlefield.HasPlayerEntered(c));
            };
        }

        // CLuaZone::getBattlefieldByInitiator
        private void BattlefieldZoneMethods(Dictionary<string, LuaFunction> zoneMethods)
        {
            zoneMethods["getBattlefieldByInitiator"] = l =>
            {
                var h = WeatherLuaZone(l).BattlefieldHandler;
                l.PushObject(h != null ? GetBattlefieldByInitiator(h, l.ToUIntArg(2)) : null, BattlefieldLuaType);
                return 1;
            };
        }

        // CZone::GetEntity(targid, 0x1F): a char, an NPC, a mob or a pet of the zone
        private object BattlefieldZoneEntity(ZoneInfo info, ushort targid)
        {
            foreach (var npc in info.Npcs)
                if (npc.TargId == targid) return npc;
            foreach (var mob in info.Mobs)
                if (mob.TargId == targid) return mob;
            foreach (var pet in info.Pets)
                if (pet.TargId == targid) return pet;
            foreach (var trust in info.Trusts)
                if (trust.TargId == targid) return trust;
            foreach (var (c, s) in charSessions_)
                if (s.InZone && InArea(c, info) && c.TargId == targid) return c;
            return null;
        }

        // CLuaBattlefield::addGroups: each group's mobs (by name, the area's share of them for a battlefield of
        // several arenas, or by id), their mods, links, roaming, mixins and initialize; then the spawns, the setups,
        // the link parties rebuilt, and the armoury crate
        private void BattlefieldAddGroups(LuaState l, Battlefield bf, bool hasMultipleArenas)
        {
            var info = bf.Zone;
            const int groupsIdx = 2;
            if (!l.IsTable(groupsIdx)) return;
            // Ensure that each area has its own super linking
            short superlinkId = (short)(1000 * bf.Area);
            // The lowest and highest entity ids allowed within the battlefield (battlefields of several areas)
            uint lowestId = uint.MaxValue, highestId = 0;

            if (hasMultipleArenas)
            {
                var entityIds = new SortedSet<uint>();
                ForEachTableValue(l, groupsIdx, () =>
                {
                    foreach (var name in TableStrings(l, -1, "mobs"))
                    {
                        foreach (var entity in QueryEntitiesByName(info, name))
                        {
                            uint id = IdOf(entity);
                            entityIds.Add(id);
                            lowestId = Math.Min(lowestId, id);
                            highestId = Math.Max(highestId, id);
                        }
                    }
                });
                if (entityIds.Count > 0)
                {
                    uint stride = (uint)entityIds.Count / (uint)Math.Max(1, info.BattlefieldHandler?.MaxBattlefields ?? 3);
                    // Look to see if there's an Armoury Crate within the group of monsters
                    uint armouryCrateOffset = 0;
                    for (uint potentialCrateId = lowestId + stride; potentialCrateId >= lowestId; potentialCrateId--)
                    {
                        if (info.NpcById.TryGetValue(potentialCrateId, out var crate) && crate.Name == "Armoury_Crate")
                        {
                            armouryCrateOffset = potentialCrateId - lowestId;
                            // If an Armoury Crate is in the battlefield we need to account for it in the stride
                            ++stride;
                            break;
                        }
                        if (potentialCrateId == 0) break;
                    }
                    lowestId += stride * (uint)(bf.Area - 1);
                    highestId = lowestId + stride - 1;
                    if (armouryCrateOffset != 0) bf.ArmouryCrate = lowestId + armouryCrateOffset;
                }
            }
            else
            {
                lowestId = 0;
                highestId = uint.MaxValue;
            }

            // The entities to be added to the battlefield
            var entities = new SortedSet<uint>();
            var spawnedEntities = new SortedSet<uint>();
            var explicitPartyEntities = new HashSet<uint>();
            var battlefieldGroups = new List<Battlefield.Group>();
            bool failed = false;

            ForEachTableValue(l, groupsIdx, () =>
            {
                if (failed || !l.IsTable(-1)) return;
                int g = l.Top;
                var groupEntities = new List<MobEntity>();

                void Add(object entity)
                {
                    if (entity is not MobEntity mob)
                    {
                        log_("PMob is null.");
                        failed = true;
                        return;
                    }
                    groupEntities.Add(mob);
                    if (entities.Add(mob.Id)) BattlefieldInsertEntity(bf, mob, true);
                }

                // Lookup mob ids given the provided names
                foreach (var name in TableStrings(l, g, "mobs"))
                    foreach (var entity in QueryEntitiesByName(info, name))
                    {
                        uint id = IdOf(entity);
                        if (id >= lowestId && id <= highestId) Add(entity);
                    }

                // Look to see if there have been any mob ids specifically set for this battlefield.
                l.GetField(g, "mobIds");
                if (l.IsTable(-1))
                {
                    int ids = l.Top;
                    if (hasMultipleArenas)
                    {
                        // Mob IDs need to be in the format of { { id, ... }, { ... } } with each subtable being an area
                        int area = bf.Area - 1;
                        if (area >= l.ObjLen(ids))
                        {
                            log_("Battlefield Area is greated than groupMobIds size.");
                            failed = true;
                        }
                        else
                        {
                            l.RawGetI(ids, area + 1);
                            foreach (uint id in TableUInts(l, l.Top))
                                if (info.MobById.TryGetValue(id, out var mob)) Add(mob);
                            l.Pop(1);
                        }
                    }
                    else
                    {
                        foreach (uint id in TableUInts(l, ids))
                            if (info.MobById.TryGetValue(id, out var mob)) Add(mob);
                    }
                }
                l.Pop(1);
                if (failed) return;

                var group = new Battlefield.Group();
                foreach (var mob in groupEntities)
                {
                    // Restore modifiers here since we save the modifiers below but don't want any previous modifiers persisting
                    mob.RestoreModifiers();
                    mob.RestoreMobModifiers();
                    group.MobIds.Add(mob.Id);
                }
                group.DeathCallback = FieldFunctionRef(l, g, "death");
                group.AllDeathCallback = FieldFunctionRef(l, g, "allDeath");
                group.RandomDeathCallback = FieldFunctionRef(l, g, "randomDeath");
                group.SetupCallback = FieldFunctionRef(l, g, "setup");

                if (FieldBool(l, g, "isParty"))
                {
                    List<MobEntity> party = null;
                    foreach (var mob in groupEntities)
                    {
                        // Leave existing party first before joining this new one
                        mob.LinkParty?.Remove(mob);
                        if (party == null) party = new List<MobEntity>();
                        party.Add(mob);
                        mob.LinkParty = party;
                        explicitPartyEntities.Add(mob.Id);
                    }
                }

                bool superlink = FieldBool(l, g, "superlink");
                byte superlinkGroup = (byte)FieldInt(l, g, "superlinkGroup", 0);
                if (superlink && superlinkGroup != 0)
                    log_($"Superlink bool and Group defined in the same mob group for Battlefield {bf.Id}");
                if (superlink || superlinkGroup != 0)
                {
                    // the two options split the battlefield's range at 500
                    if (superlinkGroup != 0) superlinkId = (short)(1000 * bf.Area + 500 + superlinkGroup);
                    else ++superlinkId;
                    foreach (var mob in groupEntities)
                    {
                        mob.SetMobMod(MobMod.Superlink, superlinkId);
                        mob.SaveMobModifiers();
                    }
                }

                l.GetField(g, "stationary");
                bool stationary = l.IsNil(-1) || l.ToBoolean(-1);
                l.Pop(1);
                if (stationary)
                {
                    foreach (var mob in groupEntities)
                    {
                        mob.SetMobMod(MobMod.RoamResetFacing, 1);
                        mob.MaxRoamDistance = 0.5f;
                        mob.RoamFlags |= RoamFlag.Scripted;
                        mob.SaveMobModifiers();
                    }
                }

                l.GetField(g, "mods");
                if (l.IsTable(-1))
                {
                    int mods = l.Top;
                    foreach (var mob in groupEntities)
                    {
                        ForEachTablePair(l, mods, () => mob.SetModifier((Mod)l.ToEnum(-2), (short)(ushort)l.ToIntegral(-1))); // as<xi::Mod>, as<uint16>
                        mob.SaveModifiers();
                    }
                }
                l.Pop(1);

                l.GetField(g, "mobMods");
                if (l.IsTable(-1))
                {
                    int mobMods = l.Top;
                    foreach (var mob in groupEntities)
                    {
                        ForEachTablePair(l, mobMods, () => mob.SetMobMod((MobMod)(ushort)l.ToIntegral(-2), (short)(ushort)l.ToIntegral(-1))); // as<uint16> both
                        mob.SaveMobModifiers();
                    }
                }
                l.Pop(1);

                l.GetField(g, "mixins");
                bool hasMixins = !l.IsNil(-1);
                l.Pop(1);
                if (hasMixins)
                {
                    foreach (var mob in groupEntities)
                    {
                        int top = l.Top;
                        try
                        {
                            l.GetGlobal("applyMixins");
                            if (!l.IsFunction(-1)) break;
                            l.PushObject(mob);
                            l.GetField(g, "mixins");
                            l.GetField(g, "mixinOptions");
                            l.Call(3, 0);
                        }
                        catch (LuaException e)
                        {
                            ScriptError("luautils::applyMixins", e);
                        }
                        finally
                        {
                            l.SetTop(top);
                        }
                    }
                }

                l.GetField(g, "initialize");
                if (l.IsFunction(-1))
                {
                    l.PushObject(bf, BattlefieldLuaType);
                    ZoneLuaPushArray(l, groupEntities);
                    try
                    {
                        l.Call(2, 0);
                    }
                    catch (LuaException e)
                    {
                        log_($"Error in battlefield {bf.Name} group.initialize: {e.Message}");
                    }
                    // Save modifiers after initialize all mobs
                    foreach (var mob in groupEntities)
                    {
                        mob.SaveModifiers();
                        mob.SaveMobModifiers();
                    }
                }
                l.SetTop(g);

                l.GetField(g, "spawned");
                bool spawned = l.IsNil(-1) || l.ToBoolean(-1);
                l.Pop(1);
                if (spawned)
                    foreach (var mob in groupEntities) spawnedEntities.Add(mob.Id);

                battlefieldGroups.Add(group);
            });
            if (failed) return;

            // Spawn entities after all groups have been setup.
            // This prevents wiping modifiers set in spawn in subsequent groups that repeat the same mobs.
            foreach (uint id in spawnedEntities)
                if (info.MobById.TryGetValue(id, out var mob)) SpawnMob(info, mob);

            // Finalize setting up groups now that they've been spawned
            foreach (var group in battlefieldGroups)
            {
                if (group.SetupCallback != null)
                {
                    var mobs = new List<object>();
                    foreach (uint id in group.MobIds) mobs.Add(info.MobById.GetValueOrDefault(id));
                    BattlefieldCallGroup(bf, group.SetupCallback, "group.setup", () => ZoneLuaPushArray(lua_, mobs), 1);
                }
                // CBattlefield::addGroup
                if (group.RandomDeathCallback != null && group.MobIds.Count > 0)
                    group.RandomMobId = group.MobIds[MobRng.Next(group.MobIds.Count)];
                bf.Groups.Add(group);
            }

            // Rebuild party links after all the battlefield groups have their modifiers applied
            // This avoids ordering bugs when SUPERLINK/SUBLINK or other link-affecting state
            // are assigned after the zone-load party pass.
            foreach (uint id in entities)
            {
                if (explicitPartyEntities.Contains(id) || !info.MobById.TryGetValue(id, out var mob) || mob.LinkParty == null) continue;
                mob.LinkParty.Remove(mob);
                mob.LinkParty = null;
            }
            foreach (uint id in entities)
                if (!explicitPartyEntities.Contains(id) && info.MobById.TryGetValue(id, out var mob)) MobLinkFindParty(info, mob);

            if (bf.ArmouryCrate != 0 && info.NpcById.TryGetValue(bf.ArmouryCrate, out var armouryCrate))
            {
                BattlefieldInsertEntity(bf, armouryCrate, true, Battlefield.ConditionDisappearAtStart);
                armouryCrate.EntityFlags |= EntityFlagUntargetable;
                armouryCrate.LocalVars.Clear();
                armouryCrate.Events.RemoveListener(lua_, "TRIGGER_CRATE");
            }
        }

        // ---- table helpers -------------------------------------------------------------------------------------------

        // for each value of the table at idx (pairs order), with the value on top of the stack
        private static void ForEachTableValue(LuaState l, int idx, Action body)
        {
            if (idx < 0) idx = l.Top + idx + 1;
            l.PushNil();
            while (l.Next(idx))
            {
                int top = l.Top;
                body();
                l.SetTop(top - 1);
            }
        }

        // for each key / value of the table at idx, the key at -2 and the value at -1
        private static void ForEachTablePair(LuaState l, int idx, Action body)
        {
            l.PushNil();
            while (l.Next(idx))
            {
                int top = l.Top;
                body();
                l.SetTop(top - 1);
            }
        }

        // the table field's array of strings (none when it is missing)
        private static List<string> TableStrings(LuaState l, int idx, string field)
        {
            if (idx < 0) idx = l.Top + idx + 1;
            var list = new List<string>();
            l.GetField(idx, field);
            if (l.IsTable(-1))
                for (int i = 1, n = l.ObjLen(-1); i <= n; i++)
                {
                    l.RawGetI(-1, i);
                    if (l.IsString(-1)) list.Add(l.ToStringValue(-1));
                    l.Pop(1);
                }
            l.Pop(1);
            return list;
        }

        private static List<uint> TableUInts(LuaState l, int idx)
        {
            var list = new List<uint>();
            for (int i = 1, n = l.ObjLen(idx); i <= n; i++)
            {
                l.RawGetI(idx, i);
                if (l.IsNumber(-1)) list.Add((uint)l.ToIntegral(-1)); // std::vector<uint32>
                l.Pop(1);
            }
            return list;
        }

        // a reference to the table's function field, null when it has none
        private static LuaRef FieldFunctionRef(LuaState l, int idx, string field)
        {
            l.GetField(idx, field);
            if (l.IsFunction(-1)) return l.Ref();
            l.Pop(1);
            return null;
        }
    }

    // BattlefieldRegistration
    internal sealed class BattlefieldRegistration
    {
        public ushort Id = 0xFFFF;
        public byte Area = 1;
        public uint Initiator, MaxPlayers, LevelCap, Rules, TimeLimitSec;
        public bool IsMission;
        public bool ShowTimer = true;
    }
}
