using System.Collections.Generic;

namespace XI.Zone
{
    // CAIContainer::Tick's TICK listener event: each entity's listeners hear the milliseconds since the zone's last tick
    // (0 on an entity's first). The other listener events fire where their C++ does (combat, loot, items, EXP,
    // triggers).
    //
    // Where in the zone tick (B3b Deferred 1, P10 final review M8): CZoneEntities::ZoneServer (zone_entities.cpp:1942)
    // ticks the mobs, then the NPCs, the pets, the trusts and last the chars, each through CAIContainer::Tick
    // (ai_container.cpp:479): its TICK listeners, its own tick, then its ActionQueue.checkAction. So an NPC's timer due
    // in a tick runs before a char's TICK listener of the same tick (Scattered into Shadow: Tebhi's 4000 ms timer sets
    // him CUTSCENE_ONLY, then another char's TEBHI_RESPAWN listener sees it). Here a mob's, NPC's, pet's or trust's
    // listeners run at the start of the zone tick (TickListeners), before those entities' ticks; a char's run in
    // TickEntityActionQueues, after every other entity's action queue and right before the char's own queue
    // (TickCharListeners), char by char in targid order. The queues run in the C++ entity order
    // (TickEntityActionQueues).
    // Still not as in the C++: the non-chars' listeners all run first, not each with its own entity's tick, and a
    // char's own state systems (weapon skills, magic, abilities, items ...) tick at the start of the zone tick, before
    // its listeners and its queue (TODO.md's tick order row).
    public sealed partial class ZoneHost
    {
        private void TickListeners()
        {
            if (lua_ == null) return;
            long now = NowMs();
            // every entity's clock moves; the ones listening are called once the walk is done
            var ticking = new List<(object Entity, long Elapsed)>();
            void Visit(object e)
            {
                var events = EventsOf(e);
                long elapsed = events.LastTickMs == 0 ? 0 : now - events.LastTickMs;
                events.LastTickMs = now;
                if (events.HasListener("TICK")) ticking.Add((e, elapsed));
            }
            foreach (var (info, _, _) in AllAreas())
            {
                foreach (var m in info.Mobs) Visit(m);
                foreach (var n in info.Npcs) Visit(n);
                foreach (var p in info.Pets) Visit(p);
                foreach (var t in info.Trusts) Visit(t);
            }
            foreach (var (e, elapsed) in ticking) TriggerListener(e, "TICK", e, elapsed);
        }

        // a char's TICK listeners (CAIContainer::Tick's first step), with the milliseconds since its previous tick
        private void TickCharListeners(CharData c, long now)
        {
            var events = EventsOf(c);
            long elapsed = events.LastTickMs == 0 ? 0 : now - events.LastTickMs;
            events.LastTickMs = now;
            if (events.HasListener("TICK")) TriggerListener(c, "TICK", c, elapsed);
        }
    }
}
