using System.Collections.Generic;
using XI.Map;
using XI.Script;

namespace XI.Zone
{
    // CInactiveState (PAI->Inactive / PAI->Untargetable): the entity stops acting for a time, or (a duration of 0)
    // while a prevent-action effect (sleep, petrification, stun, terror, the penalty, charm) holds it. It suspends the
    // attack state's swing countdown until it ends; while it is on top no path is followed, the mob controller's
    // combat tick stops after onMobFight, and getCurrentAction is 27. The kind that may not change state (stun, the
    // prevent-action one) first interrupts what can be interrupted (InterruptStates: a mob skill, a spell, a job
    // ability, a mob's ranged attack, an item not used yet) and keeps CanChangeState false; the kind that may (wait) lets a spell or skill
    // run over it. CAIContainer's tick puts a living entity with a prevent-action effect (charm aside) into it unless
    // it is casting or using a mob skill, which resolve their own interrupt. The untargetable kind hides the entity
    // from targeting (PAI->IsUntargetable). Bindings: stun, untargetableAndUnactionable, wait, with setMobFlags,
    // getMobFlags and setTrueDetection. An NPC's wait is ZoneHost.NpcAi.cs's NpcWait. An item already used
    // is left to finish (C++ keeps it beneath).
    public sealed class InactiveState
    {
        public long EntryMs, DurationMs;
        public bool CanChangeState, Untargetable;
        public long SwingRemainingMs;   // the attack state's countdown, held while this state runs
        public InactiveState Below;     // an inactive state this one was stacked on
    }

    public sealed partial class ZoneHost
    {
        private const byte ActionInactive = 27;

        // PAI->IsUntargetable: the untargetable inactive state, or the entity's own flag
        private static bool IsUntargetable(BattleEntity e) =>
            e.InactiveState is { Untargetable: true } || e is MobEntity { Untargetable: true };

        // ForceChangeState<CInactiveState>, init's InterruptStates included
        private void EnterInactive(BattleEntity e, long durationMs, bool canChangeState, bool untargetable, long now)
        {
            if (!canChangeState)
            {
                if (e is MobEntity m && m.SkillState != null) EndMobSkill(m, now);
                if (e is MobEntity rm) EndMobRanged(rm, now);
                if (e is CharData c)
                {
                    if (SessionOf(c) is { } s && c.ItemUse is { Completed: false }) CancelItemUse(s, c);
                    CancelAbility(c);
                }
                AbortMagic(e, now);
            }
            var below = e.InactiveState;
            e.InactiveState = new InactiveState
            {
                EntryMs = now,
                DurationMs = durationMs,
                CanChangeState = canChangeState,
                Untargetable = untargetable,
                SwingRemainingMs = below?.SwingRemainingMs ?? System.Math.Max(0, SwingAtOf(e) - now),
                Below = below,
            };
        }

        // CInactiveState::Update: true once it is done
        private static bool InactiveDone(BattleEntity e, InactiveState st, long now)
        {
            if (st.DurationMs == 0)
            {
                if (!e.IsAlive) return true;
                var fx = e.StatusEffects;
                if (!fx.HasPreventActionEffect() || (fx.HasStatusEffect(EffectId.CharmI, EffectId.CharmII) && !fx.HasPreventActionEffect(true)))
                    return true;
            }
            return st.DurationMs > 0 && now > st.EntryMs + st.DurationMs;
        }

        // the state on top when inactive: true while it still holds the entity. When the last one ends the attack
        // state picks its countdown up again.
        private bool TickInactive(BattleEntity e, long now)
        {
            var st = e.InactiveState;
            if (st == null) return false;
            if (!InactiveDone(e, st, now)) return true;
            e.InactiveState = st.Below;
            if (e.InactiveState != null) return true;
            if (e is MobEntity m) m.SwingAtMs = now + st.SwingRemainingMs;
            else if (e is CharData c && c.AttackTarget != null) c.SwingAtMs = now + st.SwingRemainingMs;
            return false;
        }

        // CAIContainer::Tick's shouldGoInactive, then Inactive(0ms, false)
        private void CheckGoInactive(BattleEntity e, long now)
        {
            if (e.IsAlive && e.InactiveState == null && e.MagicState == null && e is not MobEntity { SkillState: not null } &&
                e.StatusEffects.HasPreventActionEffect(true))
                EnterInactive(e, 0, false, false, now);
        }

        // the chars' half of the AI tick: the inactive state on top, then the prevent-action check
        private void TickCharInactive()
        {
            long now = NowMs();
            foreach (var (c, _) in new List<KeyValuePair<CharData, MapSession>>(charSessions_))
            {
                if (c.Hp <= 0) c.InactiveState = null;
                else if (!TickInactive(c, now)) CheckGoInactive(c, now);
            }
        }

        private void RegisterInactiveBindings(Dictionary<string, LuaFunction> m)
        {
            m["stun"] = l =>
            {
                if (Entity(l) is BattleEntity b) EnterInactive(b, l.ToIntArg(2), false, false, NowMs());
                return 0;
            };
            m["untargetableAndUnactionable"] = l =>
            {
                if (Entity(l) is BattleEntity b) EnterInactive(b, l.ToIntArg(2), false, true, NowMs());
                return 0;
            };
            m["wait"] = l =>
            {
                switch (Entity(l))
                {
                    case CharData c: log_($"Invalid entity (Player: {c.Name}) calling function."); break;
                    case MobEntity mob: EnterInactive(mob, l.IsNil(2) ? 4000 : OptU32(l, 2), true, false, NowMs()); break;
                    case NpcEntity npc: NpcWait(npc, l.IsNil(2) ? 4000 : OptU32(l, 2)); break;
                }
                return 0;
            };
            m["setMobFlags"] = l =>
            {
                var self = Entity(l);
                if (self is not (MobEntity or CharData))
                {
                    log_($"Attempt to set Mob Flags for invalid entity ({NameOf(self)})");
                    return 0;
                }
                MobEntity mob;
                if (!l.IsNil(3))
                {
                    mob = MobById(OptU32(l, 3));
                    if (mob == null)
                    {
                        log_($"Could not find a monster with id {OptU32(l, 3)} to use for setMobFlags.");
                        return 0;
                    }
                }
                else if (self is MobEntity sm) mob = sm;
                else if ((mob = MobByTargId((CharData)self, ((CharData)self).FaceTarget)) == null)
                {
                    log_("Must target a monster to use for setMobFlags ");
                    return 0;
                }
                mob.EntityFlags = (uint)l.ToEnum(2); // xi::EntityFlags
                mob.UpdateMask |= EntityUpdatePacket.UpdateHp;
                return 0;
            };
            m["getMobFlags"] = l =>
            {
                if (Entity(l) is MobEntity mob) return Ret(l, mob.EntityFlags);
                log_($"Attempting to get mob flags for invalid entity type ({NameOf(Entity(l))}).");
                return Ret(l, 0);
            };
            m["setTrueDetection"] = l =>
            {
                if (Entity(l) is MobEntity mob) mob.TrueDetection = l.ToBoolean(2);
                else log_($"Attempting to set true detection for invalid entity type ({NameOf(Entity(l))}).");
                return 0;
            };
        }
    }
}
