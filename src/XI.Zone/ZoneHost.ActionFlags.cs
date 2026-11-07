using XI.Map;

namespace XI.Zone
{
    // CBattleEntity::processActionEffectFlags, which every finished action runs (a melee round, a ranged shot, a weapon
    // skill, an ability, a spell, an item, a mob / pet / trust skill), and CCharEntity::skipEvent.
    public sealed partial class ZoneHost
    {
        // what an action's emit and receipt drop: a char target in a skippable event skips it; a target on another
        // side is hostile: the main one loses its DETECTABLE effects, each its ON_ATTACK ones, a fishing char stops
        // fishing; anything hostile drops the actor's ON_ATTACK (and ATTACK for a melee round, a weapon skill, a mob or
        // pet skill)
        private void ProcessActionEffectFlags(BattleEntity actor, ActionPacket action)
        {
            bool emittedHostile = false, isMainTarget = true;
            int allegiance = EnmityContainer.AllegianceOf(actor);
            foreach (var t in action.Targets)
            {
                var pt = WsBattleById(t.ActorId) ?? AreaOf(actor).Trusts.Find(tr => tr.Id == t.ActorId);
                if (pt is CharData ec && ec.IsInEvent) SkipEvent(ec);
                if (pt != null && allegiance != EnmityContainer.AllegianceOf(pt))
                {
                    emittedHostile = true;
                    if (isMainTarget) pt.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Detectable);
                    pt.StatusEffects.DelStatusEffectsByFlag(EffectFlag.OnAttack);
                    if (pt is CharData fc && IsFishing(fc) && SessionOf(fc) is { } s) InterruptFishing(s, fc);
                }
                isMainTarget = false;
            }
            if (!emittedHostile) return;
            actor.StatusEffects.DelStatusEffectsByFlag(EffectFlag.OnAttack);
            if (action.ActionType is ActionCategory.BasicAttack or ActionCategory.SkillFinish or ActionCategory.MobSkillFinish or ActionCategory.PetSkillFinish)
                actor.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Attack);
        }

        // CCharEntity::isFishing
        private static bool IsFishing(CharData c) =>
            (c.Animation >= Anim.NewFishingFish && c.Animation <= Anim.NewFishingStop) ||
            c.Animation == Anim.FishingStart || c.Animation == Anim.NewFishingStart;

        // CCharEntity::skipEvent: a skippable event (not a locked char's) is cancelled, with its interrupt text
        private void SkipEvent(CharData c)
        {
            if (!c.IsInEvent || c.Locked || !c.CurrentEvent.CanSkip || SessionOf(c) is not { } s) return;
            s.PacketList.AddLast(SystemMessage(MsgEventSkipped));
            s.PacketList.AddLast(EventPackets.Eventucoff(c, EventucoffMode.CancelEvent));
            if (c.CurrentEvent.InterruptText != 0 && c.CurrentEvent.TargetEntity is { } te)
            {
                var (id, targId, _, isChar) = Speaker(te);
                s.PacketList.AddLast(EventPackets.TalkNum(id, targId, isChar, (ushort)c.CurrentEvent.InterruptText, false, 0));
            }
            EndCurrentEvent(s, c);
        }
    }
}
