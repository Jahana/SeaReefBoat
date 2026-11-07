using System.Collections.Generic;
using XI.Script;

namespace XI.Zone
{
    // The enmity sources outside the melee path: battleutils::GenerateCureEnmity, handleKillshotEnmity for a mob's
    // auto attack, TransferEnmity, CMobEntity::OnEngage's MOBMOD_ALLI_HATE, and the enmity bindings (getCE, getVE,
    // setCE, setVE, addBaseEnmity, lowerEnmity, transferEnmity, updateEnmityFromCure, setEnmityActive, hasClaim,
    // hasEnmity, getNotorietyList, clearEnmityForEntity, getEnmityList), with the pets and trusts these take: a trust
    // or a pet (not an automaton) curing for its master, a pet's master's mobs in transferEnmity, ALLI_HATE through a
    // pet target. The mobs fighting an entity are its notoriety container (NotorietyContainer.cs). A char's zone-out
    // (CZoneEntities::DecreaseZoneCounter) leaves its entries without their owner (LogoutReset) and drops the mobs'
    // claim and target on it.
    public sealed partial class ZoneHost
    {
        // battleutils::GenerateCureEnmity: every mob with the cured target on its list (and a PC level, so it has
        // been fought) hates the curer for it
        private void GenerateCureEnmity(BattleEntity source, BattleEntity target, int amount, int fixedCe, int fixedVe)
        {
            if (source == null || target == null)
            {
                log_("battleutils::GenerateCureEnmity - PSource or PTarget was null.");
                return;
            }
            foreach (var mob in target.Notoriety)
                if (mob.HiPcLvl > 0 && mob.Enmity.HasId(target.Id))
                    mob.Enmity.UpdateEnmityFromCure(source, target.MLvl, amount, fixedCe, fixedVe);
        }

        // battleutils::handleKillshotEnmity: a mob that has killed the top of its list forgets it
        private void EnmityHandleKillshot(MobEntity m, BattleEntity target)
        {
            if (target != null && !target.IsAlive && m.Enmity.GetHighestEnmity(_ => true, EnmityCharById) is { } highest && highest.Id == target.Id)
                m.Enmity.Clear(target.Id);
        }

        // CZoneEntities::DecreaseZoneCounter's mobs: each keeps the char's entry without its owner (LogoutReset), lets go
        // of the char's claim and stops targeting it
        private void EnmityCharZoneOut(CharData c)
        {
            foreach (var m in AreaOf(c).Mobs)
            {
                m.Enmity.LogoutReset(c.Id);
                if (m.OwnerId == c.Id)
                {
                    m.OwnerId = 0;
                    m.UpdateMask |= EntityUpdatePacket.UpdateStatus;
                }
                if (m.BattleTarget == c) m.BattleTarget = null;
            }
        }

        // CMobEntity::OnEngage's MOBMOD_ALLI_HATE: the alliance members within that range of the target (a pet's
        // master for a pet) get base enmity
        private void EnmityAlliHate(MobEntity m, BattleEntity target)
        {
            int range = m.GetMobMod(MobMod.AlliHate);
            if (range == 0) return;
            if (target is PetEntity) target = target.PMaster;
            if (target is not CharData c) return;
            foreach (var member in ForAlliance(c))
                if (Distance(member, c) < range) m.Enmity.AddBaseEnmity(member);
        }

        private void RegisterEnmityBindings(Dictionary<string, LuaFunction> m)
        {
            m["getCE"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"Attempting to get CE for invalid entity type ({NameOf(Entity(l))}).");
                    return Ret(l, 0);
                }
                return Ret(l, l.ToObject(2) is BattleEntity t ? mob.Enmity.CE(t) : 0);
            };
            m["getVE"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"Attempting to get VE for invalid entity type ({NameOf(Entity(l))}).");
                    return Ret(l, 0);
                }
                return Ret(l, l.ToObject(2) is BattleEntity t ? mob.Enmity.VE(t) : 0);
            };
            m["setCE"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"Attempting to set CE for invalid entity type ({NameOf(Entity(l))}).");
                    return 0;
                }
                if (l.ToObject(2) is BattleEntity t) mob.Enmity.SetCE(t, (ushort)l.ToIntArg(3));
                return 0;
            };
            m["setVE"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"Attempting to set VE for invalid entity type ({NameOf(Entity(l))}).");
                    return 0;
                }
                if (l.ToObject(2) is BattleEntity t) mob.Enmity.SetVE(t, (ushort)l.ToIntArg(3));
                return 0;
            };
            m["addBaseEnmity"] = l =>
            {
                if (Entity(l) is MobEntity mob)
                {
                    if (l.ToObject(2) is BattleEntity t) mob.Enmity.AddBaseEnmity(t);
                }
                else log_($"Attempting to add base enmity to invalid entity type ({NameOf(Entity(l))}).");
                return 0;
            };
            m["lowerEnmity"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"Attempting to lower enmity for invalid entity type ({NameOf(Entity(l))}).");
                    return 0;
                }
                if (l.ToObject(2) is BattleEntity t) mob.Enmity.LowerEnmityByPercent(t, (byte)l.ToIntArg(3), null);
                return 0;
            };
            m["setEnmityActive"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"Attempting to reset enmity for invalid entity type ({NameOf(Entity(l))}).");
                    return 0;
                }
                if (l.ToObject(2) is BattleEntity t) mob.Enmity.SetActive(t.Id, l.ToBoolean(3));
                return 0;
            };
            m["clearEnmityForEntity"] = l =>
            {
                if (Entity(l) is MobEntity mob)
                {
                    mob.Enmity.Clear(IdOf(l.ToObject(2)));
                    return 0;
                }
                log_("lua::clearEnmityForEntity called on invalid entity");
                return 0;
            };
            // transferEnmity(entity, percent, range): each mob the char (self, or the entity; a pet's master) has spawned
            // within range of the entity hands it percent of self's enmity (battleutils::TransferEnmity)
            m["transferEnmity"] = l =>
            {
                object self = Entity(l);
                var entity = l.ToObject(2) as BattleEntity;
                byte percent = (byte)l.ToIntArg(3);
                float range = (float)l.ToNumber(4);
                CharData iter = self switch
                {
                    CharData sc => sc,
                    PetEntity sp => sp.PMaster as CharData,
                    _ => entity switch
                    {
                        CharData ec => ec,
                        PetEntity ep => ep.PMaster as CharData,
                        _ => null,
                    },
                };
                if (iter == null || entity == null || self is not BattleEntity giver) return 0;
                foreach (var id in new List<uint>(iter.SpawnMobList))
                    if (MobById(id) is { } mob && Distance(mob, entity) <= range)
                        mob.Enmity.LowerEnmityByPercent(giver, percent, entity);
                return 0;
            };
            // updateEnmityFromCure(target, amount, [fixedCE], [fixedVE]): a char or trust curer; a pet (not an automaton)
            // cures for its char master
            m["updateEnmityFromCure"] = l =>
            {
                int amount = l.ToIntArg(3);
                int fixedCe = l.IsNumber(4) ? l.ToIntArg(4) : 0;
                int fixedVe = l.IsNumber(5) ? l.ToIntArg(5) : 0;
                if (amount < 0)
                {
                    log_("Received negative cure amount.");
                    return 0;
                }
                BattleEntity curer = Entity(l) switch
                {
                    CharData c => c,
                    TrustEntity t => t,
                    PetEntity p when p is not AutomatonEntity => p.PMaster as CharData,
                    _ => null,
                };
                if (curer != null && l.ToObject(2) is BattleEntity target) GenerateCureEnmity(curer, target, amount, fixedCe, fixedVe);
                return 0;
            };
            m["hasClaim"] = l =>
            {
                if (Entity(l) is not BattleEntity self)
                {
                    log_($"Attempting to check claim for invalid entity type ({NameOf(Entity(l))}).");
                    return Ret(l, false);
                }
                if (l.ToObject(2) is not BattleEntity target)
                {
                    log_($"Attempting to check claim against invalid target entity type ({NameOf(l.ToObject(2))}).");
                    return Ret(l, false);
                }
                // battleutils::HasClaim: only a mob carries an owner; a mob (no master) is its own alliance
                if (target is not MobEntity claimed) return Ret(l, false);
                return Ret(l, self is CharData c ? HasClaim(c, claimed) : claimed.OwnerId == self.Id);
            };
            // hasEnmityEXPENSIVE: on a living mob's list, or the battle target of one of another allegiance
            m["hasEnmity"] = l =>
            {
                if (Entity(l) is not BattleEntity b)
                {
                    log_($"Invalid Entity (NPC: {NameOf(Entity(l))}) calling function.");
                    return Ret(l, false);
                }
                return Ret(l, HasEnmityExpensive(b));
            };
            m["getNotorietyList"] = l =>
            {
                if (Entity(l) is not BattleEntity b)
                {
                    log_($"Invalid Entity (NPC: {NameOf(Entity(l))}) calling function.");
                    l.PushNil();
                    return 1;
                }
                l.NewTable(b.Notoriety.Count);
                int i = 0;
                foreach (var mob in b.Notoriety)
                {
                    l.PushObject(mob);
                    l.RawSetI(-2, ++i);
                }
                return 1;
            };
            // getEnmityList: { entity, ce, ve, active, tameable } per entry with an owner
            m["getEnmityList"] = l =>
            {
                if (Entity(l) is not MobEntity mob)
                {
                    log_($"Attempting to get enmity list for invalid entity type ({NameOf(Entity(l))}).");
                    l.PushNil();
                    return 1;
                }
                l.NewTable();
                int i = 0;
                foreach (var e in mob.Enmity.Entries)
                {
                    if (e.Owner == null) continue;
                    l.NewTable();
                    l.PushObject(e.Owner);
                    l.SetField(-2, "entity");
                    l.Push((double)e.CE);
                    l.SetField(-2, "ce");
                    l.Push((double)e.VE);
                    l.SetField(-2, "ve");
                    l.Push(e.Active);
                    l.SetField(-2, "active");
                    l.Push(mob.Enmity.IsTameable);
                    l.SetField(-2, "tameable");
                    l.RawSetI(-2, ++i);
                }
                return 1;
            };
        }
    }
}
