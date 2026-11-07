using System.Collections.Generic;

namespace XI.Zone
{
    // Mob linking: CZoneEntities::FindPartyForMob groups a zone's mobs into link parties at load (by family for mobs
    // that link, by MOBMOD_SUBLINK, by MOBMOD_SUPERLINK, or all force-linking mobs together), and
    // CMobController::TryLink has an engaged mob call the idle members of its party that CMobEntity::CanLink allows
    // onto its target, every other combat tick.
    // A mob's pet joins its fight, a pet links only with its master, who joins in when it can link, and an avatar
    // (not Alexander, Odin or Atomos) defends its master against the mob. Mobs added after load find their party too
    // (ZoneHost.DynamicEntities.cs, ZoneHost.Instances.cs), and a battlefield's groups rebuild theirs
    // (ZoneHost.Battlefield.cs).
    public sealed partial class ZoneHost
    {
        // CMobEntity::ShouldForceLink: Dynamis zones, battlefield mobs and superlinkers link regardless of family
        private bool MobLinkShouldForce(MobEntity m) =>
            ZoneInfoOf(m.Zone).Types.Contains("dynamis") || (m.Type & MobType.Battlefield) != 0 || m.GetMobMod(MobMod.Superlink) != 0;

        // CZoneEntities::FindPartyForMob
        private void MobLinkFindParty(ZoneInfo info, MobEntity m)
        {
            bool forceLink = MobLinkShouldForce(m);
            // check for sublinks even if a family doesn't link with itself
            int sublink = m.GetMobMod(MobMod.Sublink);
            if (!(forceLink || m.Link || sublink != 0) || m.LinkParty != null) return;
            foreach (var current in info.Mobs)
            {
                if (!forceLink && sublink == 0 && !current.Link) continue;
                if (current.LinkParty == null || current.Allegiance != m.Allegiance) continue;
                // SUPERLINK first (forceLink can come with it, as in BCNMs / Dynamis), then forceLink, then family or
                // sublink
                bool match;
                int superlink = m.GetMobMod(MobMod.Superlink);
                if (superlink != 0) match = current.GetMobMod(MobMod.Superlink) == superlink;
                else if (forceLink) match = MobLinkShouldForce(current);
                else match = (current.Link && current.Family == m.Family) || (sublink != 0 && sublink == current.GetMobMod(MobMod.Sublink));
                // a pet only shares a party through a mob master
                if (match && (current.PMaster == null || current.PMaster is MobEntity))
                {
                    current.LinkParty.Add(m);
                    m.LinkParty = current.LinkParty;
                    return;
                }
            }
            m.LinkParty = new List<MobEntity> { m };
        }

        // CMobEntity::CanLink: whether this mob joins a fight at the caller's position
        private bool MobLinkCanLink(MobEntity m, MobEntity caller, int superLink)
        {
            // handle super linking
            if (superLink != 0 && m.GetMobMod(MobMod.Superlink) == superLink) return true;
            // can't link right now
            if (m.Neutral) return false;
            bool nameHidden = (m.NameVis & MobEntity.NameVisHideName) != 0;
            // an underground worm or antlion
            if ((m.RoamFlags & RoamFlag.Worm) != 0 && nameHidden) return false;
            if ((m.RoamFlags & RoamFlag.Ambush) != 0 && nameHidden) return false;
            // a mob that detects by both sight and hearing only needs to meet one check
            int detects = m.GetMobMod(MobMod.Detection);
            if ((detects & DetectsSight) != 0 && (detects & DetectsHearing) == 0 && !Facing(m, caller)) return false;
            if (Distance(m, caller) > m.GetMobMod(MobMod.LinkRadius)) return false;
            if (m.GetMobMod(MobMod.NoLink) > 0) return false;
            return CanSeeTarget(m, caller.X, caller.Y, caller.Z);
        }

        // CMobController::TryLink: the party scan runs every other tick
        private void MobTryLink(MobEntity m, long now)
        {
            if (m.BattleTarget is not BattleEntity target) return;
            int superLink = m.GetMobMod(MobMod.Superlink);
            // Avatar pets defend their master, except Alexander, Odin and Atomos.
            if (target.PPet is PetEntity { PetType: PetType.Avatar } avatar && avatar.BattleTarget == null &&
                avatar.PetId != PetId.Alexander && avatar.PetId != PetId.Odin && avatar.PetId != PetId.Atomos)
            {
                // PCs only get bodyguarded by the avatar they own; non-PC targets always do (a PC's twice, as in the
                // C++: the second finds the avatar engaged already).
                if (target is not CharData pc || IsMobOwner(pc, m))
                {
                    if (target is CharData) PetAttackTarget(target, m);
                    PetAttackTarget(target, m);
                }
            }
            // my pet should help as well
            if (m.PPet is MobEntity pet && pet.Animation == Anim.None && pet.IsSpawned && pet.IsAlive) MobEngage(pet, target, now);
            m.LinkScanThisTick = !m.LinkScanThisTick;
            if (m.LinkScanThisTick && m.LinkParty != null && m.LinkParty.Count > 1 && m.GetMobMod(MobMod.OneWayLinking) == 0)
                MobLinkParty(m, target, superLink, now);
            // ask my master for help
            if (m.PMaster is MobEntity master && master.Animation == Anim.None && master.IsSpawned && master.IsAlive &&
                MobLinkCanLink(master, m, superLink))
                MobEngage(master, target, now);
        }

        private void MobLinkParty(MobEntity m, BattleEntity target, int superLink, long now)
        {
            foreach (var member in m.LinkParty.ToArray())
            {
                // a despawned member counts as dead; pets only link with their masters
                if (member.PMaster != null || member.IsDead || !member.IsAlive || !member.IsSpawned) continue;
                // skip same-family party members unless this mob wants to link with kin
                if (m.Family == member.Family && !MobLinkShouldForce(m) && !m.Link) continue;
                // PAI->IsRoaming: no animation (not engaged, not resting)
                if (member.Animation == Anim.None && MobLinkCanLink(member, m, superLink)) MobEngage(member, target, now);
            }
        }
    }
}
