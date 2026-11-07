using System;
using System.Collections.Generic;
using System.Linq;
using XI.Map;
using XI.QuestContent;
using XI.Zone;

// lane engine-infra-1: the zone tick's entity order against CZoneEntities::ZoneServer (zone_entities.cpp:1942) and
// CAIContainer::Tick (ai_container.cpp:480). Timers (entity:timer) due in the same tick run mob by mob, then NPC by
// NPC, each list in targid order, whatever order they were queued in; the chars' timers and TICK listeners run char
// by char in targid order; a char that zoned out (SendToZone) ticks no more
internal static partial class Program
{
    private static void RunTickOrder()
    {
        RunTickOrderNonChars();
        RunTickOrderChars();
        RunTickOrderZonedOut();
    }

    // a timer due now on each of the entities (Lua expressions), queued in the given order; the order they ran in
    private static string TimersRun(params (string Expr, string Name)[] entities)
    {
        var code = string.Concat(entities.Select(e =>
            $"{e.Expr}:timer(0, function() __ei1order[#__ei1order + 1] = '{e.Name}' end) "));
        Lua("__ei1order = {} " + code);
        Tick(400);
        return Lua("report(table.concat(__ei1order, ',')) __ei1order = nil");
    }

    // 1. two Marsh Funguars and an NPC of Carpenters' Landing, the NPC's timer queued first and the higher-targid mob's
    // before the lower one's: the mobs run first, lower targid first, then the NPC
    private static void RunTickOrderNonChars()
    {
        uint id = CharIds[2];
        Cleanup(db, id);
        Fixture(id, Zone.CarpentersLanding, -414.6f, 0f, -363.5f, 0);
        Login();
        try
        {
            var mobs = Here.Mobs.Where(m => m.Name == "Marsh_Funguar").OrderBy(m => m.TargId).Take(2).ToList();
            var npc = Here.Npcs.First(n => n.Status == NpcEntity.StatusNormal);
            Check("tick order: mobs, then NPCs, each by targid (queued NPC, high mob, low mob)", "low,high,npc",
                  TimersRun(($"GetNPCByID({npc.Id})", "npc"), ($"GetMobByID({mobs[1].Id})", "high"),
                            ($"GetMobByID({mobs[0].Id})", "low")));
        }
        finally
        {
            Logout();
            Cleanup(db, id);
        }
    }

    // a second char logged in beside the driven one (its own session, the client's 0x00A and 0x00C)
    private static MapSession LoginOther(uint otherId)
    {
        var s2 = new MapSession { CharId = otherId, AccountId = otherId };
        zone.OnLogin(s2);
        void SendOther(ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s2, opcode, new BasicPacket(buf));
            zone.AfterParse(s2);
        }
        SendOther(0x00A, 92, buf => BitConverter.GetBytes(otherId).CopyTo(buf, 12));
        SendOther(0x00C, 12);
        var c2 = (CharData)s2.Char;
        for (int i = 0; i < 4 && c2.IsInEvent; i++)
        {
            var ev = (ushort)c2.CurrentEvent.EventId;
            SendOther(0x05B, 20, b => BitConverter.GetBytes(ev).CopyTo(b, 0x12));
        }
        return s2;
    }

    // 2. two chars in Bastok Markets, their timers and TICK listeners: char by char in targid order, whichever logged
    // in first (the act indexes swapped between the two runs)
    private static void RunTickOrderChars()
    {
        uint id = CharIds[3], other = CharIds[4];
        Cleanup(db, id);
        Cleanup(db, other);
        Fixture(other, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        var s2 = LoginOther(other);
        var c2 = (CharData)s2.Char;
        try
        {
            Lua($@"
                __ei1ticks = {{}}
                for _, who in ipairs({{ {{ {id}, 'a' }}, {{ {other}, 'b' }} }}) do
                    GetPlayerByID(who[1]):addListener('TICK', 'EI1_ORDER', function()
                        __ei1ticks[#__ei1ticks + 1] = who[2]
                    end)
                end");
            string Ticks() => Lua("report(table.concat(__ei1ticks, ',')) __ei1ticks = {}");
            string Run()
            {
                Ticks();
                string timers = TimersRun(($"GetPlayerByID({id})", "a"), ($"GetPlayerByID({other})", "b"));
                return $"{timers}|{Ticks()}";
            }
            string Expect(CharData first) => first == c ? "a,b|a,b" : "b,a|b,a";

            var lower = c.TargId < c2.TargId ? c : c2;
            Check("tick order: two chars, timers and TICK listeners in targid order", Expect(lower), Run());
            (c.TargId, c2.TargId) = (c2.TargId, c.TargId);
            lower = c.TargId < c2.TargId ? c : c2;
            Check("  the act indexes swapped: still in targid order", Expect(lower), Run());
            (c.TargId, c2.TargId) = (c2.TargId, c.TargId);
        }
        finally
        {
            Lua($"for _, who in ipairs({{ {id}, {other} }}) do local p = GetPlayerByID(who) " +
                "if p then p:removeListener('EI1_ORDER') end end __ei1ticks = nil");
            zone.OnCharTimeout(s2, false);
            Logout();
            Cleanup(db, id);
            Cleanup(db, other);
        }
    }

    // 3. a char that zoned out (a GM setPos to Port Bastok: SendToZone in the tick) and is waiting for its client: its
    // TICK listener and its timer run no more
    private static void RunTickOrderZonedOut()
    {
        uint id = CharIds[5];
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        try
        {
            Lua("__ei1zoned = {} player:addListener('TICK', 'EI1_ZONED', " +
                "function() __ei1zoned[#__ei1zoned + 1] = 't' end)");
            Tick(400);
            Check("tick order: a char's TICK listener heard while in the zone", "t",
                  Lua("report(table.concat(__ei1zoned, ','))"));
            Lua("__ei1zoned = {} player:timer(1000, function() __ei1zoned[#__ei1zoned + 1] = 'timer' end) " +
                "player:setPos(-174, -8, 10, 0, xi.zone.PORT_BASTOK)");
            Tick(400);
            Check("  zoned out in the tick", "True", c.ZonedOut.ToString());
            Lua("__ei1zoned = {}");
            Tick(1000);
            Tick(1000);
            Check("  zoned out: no TICK listener, no timer", "", Lua("report(table.concat(__ei1zoned, ','))"));
        }
        finally
        {
            Lua("__ei1zoned = nil");
            Logout();
            Cleanup(db, id);
        }
    }
}
