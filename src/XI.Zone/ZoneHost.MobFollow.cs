using System.Collections.Generic;
using XI.Script;

namespace XI.Zone
{
    // CMobController's follow target: SetFollowTarget (onMobFollow; letting a roam leader go makes the mob neutral
    // for 30 s more, onMobUnfollow, and at full HP drops its claim and list), HasFollowTarget, ClearFollowTarget
    // (on engage for a roam follower, on Reset), CanFollowTarget (a RoamFlag.Follow mob follows a char it could aggro
    // instead of aggroing, CZoneEntities' aggro check), DoRoamTick's roam follow (walking after a moving leader past
    // its leash, else idling with the roam script) and DoCombatTick's run-away follow (running to the target, then
    // RUN_AWAY). The bindings: follow, hasFollowTarget, unfollow, with setLink, isInDynamis and getTargID. A char
    // leader has no path to follow (C++'s char has no PathFind and would dereference it), so a roam follower of a
    // char only idles near it.
    public sealed partial class ZoneHost
    {
        // FollowType
        private const byte FollowNone = 0, FollowRoam = 1, FollowRunAway = 2;
        private const float FollowRunAwayDistance = 4.0f;

        // followTarget(): a char that left the zone resolves to nothing
        private BattleEntity FollowTargetOf(MobEntity m) =>
            m.FollowTarget is CharData c && CharDisappeared(c) ? null : m.FollowTarget;

        // CMobController::SetFollowTarget
        private void SetFollowTarget(MobEntity m, BattleEntity target, byte followType, long now)
        {
            var current = FollowTargetOf(m);
            if (current == target && m.FollowType == followType) return;
            if (target != null)
            {
                CallMobHook(m, "onMobFollow", m, target);
            }
            else if (m.FollowType == FollowRoam)
            {
                m.Neutral = true;
                m.NeutralAtMs = now + 30000;
                if (current != null) CallMobHook(m, "onMobUnfollow", m, current);
                if (m.Hp == m.MaxHp)
                {
                    m.OwnerId = 0;
                    m.Enmity.Clear();
                }
            }
            m.FollowTarget = target;
            m.FollowType = followType;
        }

        // CMobController::HasFollowTarget
        private bool HasFollowTarget(MobEntity m) => FollowTargetOf(m) != null && m.FollowType != FollowNone;

        // CMobController::ClearFollowTarget
        private static void ClearFollowTarget(MobEntity m)
        {
            m.FollowTarget = null;
            m.FollowType = FollowNone;
        }

        // CMobController::CanFollowTarget
        private bool CanFollowTarget(MobEntity m, CharData c) =>
            !m.Neutral && (m.RoamFlags & RoamFlag.Follow) != 0 && FollowTargetOf(m) == null && m.FollowType == FollowNone && CanAggroTarget(m, c);

        // DoRoamTick's roam follow: true when the tick ends here (the follower is idle by its leader)
        private bool MobRoamFollow(MobEntity m, long now)
        {
            if (FollowTargetOf(m) is not { } leader || m.FollowType != FollowRoam) return false;
            int leash = m.GetMobMod(MobMod.FollowLeashRange);
            float followRoamDistance = leash > 0 ? leash : 4.0f;
            var path = PathOf(m);
            // Only path to leader if they're moving
            if (Distance(m, leader) > followRoamDistance && leader is MobEntity lm && lm.PathFind is { IsFollowingPath: true })
            {
                int stop = m.GetMobMod(MobMod.FollowStopRange);
                path.PathAround(leader.X, leader.Y, leader.Z, stop > 0 ? stop : 2.0f, PathFlag.Run);
            }
            if (path.IsFollowingPath) return false;
            // Idle followers still shed neutral and still get the roam tick.
            m.Neutral = now <= m.NeutralAtMs + MobNeutralMs;
            MobRoamScriptTick(m, now);
            return true;
        }

        // DoCombatTick's run-away follow: true when the tick ends here
        private bool MobRunAwayFollow(MobEntity m, long now)
        {
            if (FollowTargetOf(m) is not { } to || m.FollowType != FollowRunAway) return false;
            if (Distance(m, to) > FollowRunAwayDistance)
            {
                var path = PathOf(m);
                if (!path.IsFollowingPath) path.PathTo(to.X, to.Y, to.Z);
                path.FollowPath(now);
            }
            else
            {
                TriggerListener(m, "RUN_AWAY", m, to);
                ClearFollowTarget(m);
            }
            return true;
        }

        private void RegisterMobFollowBindings(Dictionary<string, LuaFunction> m)
        {
            bool AsMob(LuaState l, out MobEntity mob)
            {
                mob = Entity(l) as MobEntity;
                if (mob != null) return true;
                log_($"Invalid entity ({NameOf(Entity(l))}) calling function.");
                return false;
            }
            m["follow"] = l =>
            {
                if (AsMob(l, out var mob)) SetFollowTarget(mob, l.ToObject(2) as BattleEntity, (byte)l.ToIntArg(3), NowMs());
                return 0;
            };
            m["hasFollowTarget"] = l => Ret(l, AsMob(l, out var mob) && HasFollowTarget(mob));
            m["unfollow"] = l =>
            {
                if (AsMob(l, out var mob)) ClearFollowTarget(mob);
                return 0;
            };
            m["setLink"] = l =>
            {
                if (Entity(l) is MobEntity mob) mob.Link = l.ToIntArg(2) != 0;
                else log_($"Attempting to set link for invalid entity type ({NameOf(Entity(l))}).");
                return 0;
            };
            m["isInDynamis"] = l =>
            {
                if (Entity(l) is BattleEntity b) return Ret(l, ZoneInfoOf(b.Zone).Types.Contains("dynamis"));
                log_($"Invalid Entity (NPC: {NameOf(Entity(l))}) calling function.");
                return Ret(l, false);
            };
            m["getTargID"] = l => Ret(l, Entity(l) switch
            {
                BattleEntity b => b.TargId,
                NpcEntity n => n.TargId,
                _ => 0,
            });
        }
    }
}
