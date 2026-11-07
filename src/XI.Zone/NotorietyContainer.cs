using System.Collections;
using System.Collections.Generic;

namespace XI.Zone
{
    // CNotorietyContainer: if enmity is the one-to-many list a mob holds, notoriety is the many-to-one opposite held by
    // its targets (chars, pets, trusts, charmed mobs): the mobs with the owner on their enmity list. CEnmityContainer
    // keeps it in step (a new entry adds, Clear removes); GetHighestEnmity dropping a gone owner does not, so
    // hasEnmity weeds out what is stale before it answers, as the C++ does.
    public sealed class NotorietyContainer : IEnumerable<MobEntity>
    {
        private readonly BattleEntity owner_;
        private readonly HashSet<MobEntity> lookup_ = new HashSet<MobEntity>();

        public NotorietyContainer(BattleEntity owner) => owner_ = owner;

        public int Count => lookup_.Count;
        public bool Contains(MobEntity entity) => lookup_.Contains(entity);

        // add: only an entity of another allegiance
        public void Add(MobEntity entity)
        {
            if (owner_ != null && entity != null && EnmityContainer.AllegianceOf(entity) != EnmityContainer.AllegianceOf(owner_))
                lookup_.Add(entity);
        }

        // remove
        public void Remove(MobEntity entity)
        {
            if (owner_ != null && entity != null) lookup_.Remove(entity);
        }

        // hasEnmity: a living mob without the owner on its list, or a dead one, comes off first
        public bool HasEnmity()
        {
            if (owner_ != null && lookup_.Count > 0)
            {
                var toRemove = new List<MobEntity>();
                foreach (var mob in lookup_)
                    if (!mob.IsAlive || mob.IsDead || !mob.Enmity.HasId(owner_.Id)) toRemove.Add(mob);
                foreach (var mob in toRemove) Remove(mob);
            }
            return lookup_.Count > 0;
        }

        // begin / end: over a copy, since what the loop does may change the enmity lists (and so this set)
        public IEnumerator<MobEntity> GetEnumerator() => ((IEnumerable<MobEntity>)new List<MobEntity>(lookup_)).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
