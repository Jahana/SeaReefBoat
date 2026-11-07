using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using XI.QuestContent;
using XI.Quests;
using XI.Quests.Authoring;

// quest rework P6 (the platform needs the Jeuno and Other Areas drafts found): a probe quest of the fixture's key answers
// Samia (Bastok Markets) while the player's P6 local var names a probe; each reads the live context, or changes it, as the
// Lua call would, compared with what the Lua call gives field by field. The probe replies nothing, so Samia's own script
// opens its event: each talk is finished before the next
internal static partial class Program
{
    private static void RunPlatformP6()
    {
        Console.WriteLine("quest rework P6: platform needs");
        RunP6ContainerSize();
        RunP6MapSetting();
    }

    // the containers read: inventory, satchel, the recycle bin (10 by the loader), the temp items (50), one past the
    // containers (getContainerSize answers 0)
    private static readonly int[] P6Containers = { 0, 5, 17, 3, 18 };

    // 1. Player.ContainerSize / Player.ChangeContainerSize: getContainerSize's value for each container, and after
    // changeContainerSize the sizes, the char_storage row and the ITEM_MAX (0x01C) the engine sent, the C# call's equal
    // to the Lua call's (each from the same start: inventory 30, satchel 30)
    private static void RunP6ContainerSize()
    {
        P1Probe(new P6Quest(), () =>
        {
            Lua("player:changeContainerSize(5, 30)");
            string lua = string.Join("/", P6Containers.Select(k => Lua($"report(tostring(player:getContainerSize({k})))")));
            Lua("player:setLocalVar('P6', 1)");
            TalkTo("Samia");
            FinishIfEvent();
            Lua("player:setLocalVar('P6', 0)");
            Check("storage: ContainerSize reads each container as Lua's getContainerSize (inventory 30, satchel 30, the bin, the temp items, none)",
                  lua, P6Quest.Sizes);
            Check("  ... the sizes themselves", "30/30/10/50/0", P6Quest.Sizes);

            // the C# change (inventory +5, satchel -10), then put back and the Lua change with the same arguments
            string Sizes() => $"{Lua("report(tostring(player:getContainerSize(0)))")}/{Lua("report(tostring(player:getContainerSize(5)))")}";
            string Row() => DbRow("inventory, satchel", "FROM char_storage WHERE charid = ?");
            string ItemMax() => string.Join(";", Of(0x01C).Select(p => Convert.ToHexString(p.Buffer, 4, Math.Min(32, p.Buffer.Length - 4))));
            Collect();
            Lua("player:setLocalVar('P6', 2)");
            TalkTo("Samia");
            // read before the finish (its 0x05B collects the session's packets)
            var csharp = (Sizes(), Row(), ItemMax());
            FinishIfEvent();
            Lua("player:setLocalVar('P6', 0)");
            Lua("player:changeContainerSize(0, -5) player:changeContainerSize(5, 10)");
            string back = Sizes();
            Collect();
            Lua("player:changeContainerSize(0, 5) player:changeContainerSize(5, -10)");
            var lua2 = (Sizes(), Row(), ItemMax());
            Check("storage: ChangeContainerSize(0, 5), (5, -10): the sizes, the char_storage row and the ITEM_MAX packets are Lua's",
                  $"{lua2.Item1}|{lua2.Item2}|{lua2.Item3}", $"{csharp.Item1}|{csharp.Item2}|{csharp.Item3}");
            Check("  ... the sizes moved (35, 20), the row saved, two ITEM_MAX sent; put back to 30/30 between",
                  "35/20|35 20|2|30/30", $"{csharp.Item1}|{csharp.Item2}|{csharp.Item3.Split(';').Length}|{back}");
        });
    }

    // 2. Settings.Map / Settings.MapFlag: xi.settings.map[name] as Lua has it: a boolean true and false, a decimal, the
    // number 1, an unset name; Map as Main reads main (llround, a boolean 1 / 0), MapFlag as Lua's `== true`
    private static void RunP6MapSetting()
    {
        P1Probe(new P6Quest(), () =>
        {
            string before = Lua("report(tostring(xi.settings.map.FISHING_ENABLE))");
            string[] names = { "FISHING_ENABLE", "P6_OFF", "P6_RATE", "P6_ONE", "P6_NOPE" };
            try
            {
                Lua("xi.settings.map.FISHING_ENABLE = true xi.settings.map.P6_OFF = false xi.settings.map.P6_RATE = 2.5 xi.settings.map.P6_ONE = 1");
                // Lua's own reads: `== true`, and the number (a boolean 1 / 0, unset 0) rounded half away from zero
                string luaFlags = string.Join("/", names.Select(n => Lua($"report(tostring(xi.settings.map.{n} == true))")));
                string luaNumbers = string.Join("/", names.Select(n => Lua(
                    $"local v = xi.settings.map.{n} if type(v) == 'boolean' then v = v and 1 or 0 elseif type(v) ~= 'number' then v = 0 end " +
                    "report(tostring(v >= 0 and math.floor(v + 0.5) or -math.floor(-v + 0.5)))")));
                Lua("player:setLocalVar('P6', 3)");
                TalkTo("Samia");
                FinishIfEvent();
                Lua("player:setLocalVar('P6', 0)");
                Check("setting map: MapFlag is Lua's xi.settings.map[name] == true, name by name", luaFlags, P6Quest.Flags);
                Check("  ... true / false / 2.5 / 1 / unset", "true/false/false/false/false", P6Quest.Flags);
                Check("setting map: Map is the number Lua holds (2.5 rounded half away from zero), a boolean 1 / 0, unset 0", luaNumbers, P6Quest.Numbers);
                Check("  ... the numbers", "1/0/3/1/0", P6Quest.Numbers);
                Check("setting map: the check reads it too (the state matched on MapFlag)", "True", P6Quest.CheckSaw.ToString());
                Check("setting map: main is apart (main's FISHING_ENABLE unset: 0, map's 1)", "0/1", P6Quest.Apart);
            }
            finally
            {
                Lua($"xi.settings.map.FISHING_ENABLE = {before} xi.settings.map.P6_OFF = nil xi.settings.map.P6_RATE = nil xi.settings.map.P6_ONE = nil");
            }
        });
    }

    // while P6 is 1, Samia's handler reads the container sizes; 2, it changes two; 3, it reads the map settings (its state's
    // check reading MapFlag too); each replies nothing
    private sealed class P6Quest : QuestScript
    {
        public static string Sizes, Flags, Numbers, Apart;
        public static bool CheckSaw;
        private static readonly string[] Names = { "FISHING_ENABLE", "P6_OFF", "P6_RATE", "P6_ONE", "P6_NOPE" };

        public P6Quest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q => q.Player.LocalVar("P6") is 1 or 2)
                .Npc(Zone.BastokMarkets, "Samia").OnTrigger(q =>
                {
                    if (q.Player.LocalVar("P6") == 1) Sizes = string.Join("/", P6Containers.Select(k => q.Player.ContainerSize(k)));
                    else
                    {
                        q.Player.ChangeContainerSize(0, 5);
                        q.Player.ChangeContainerSize(5, -10);
                    }
                    return null;
                });
            b.AnyState(when: q => q.Player.LocalVar("P6") == 3 && (CheckSaw = q.Settings.MapFlag("FISHING_ENABLE")))
                .Npc(Zone.BastokMarkets, "Samia").OnTrigger(q =>
                {
                    Flags = string.Join("/", Names.Select(n => q.Settings.MapFlag(n) ? "true" : "false"));
                    Numbers = string.Join("/", Names.Select(n => q.Settings.Map(n).ToString(CultureInfo.InvariantCulture)));
                    Apart = $"{q.Settings.Main("FISHING_ENABLE")}/{q.Settings.Map("FISHING_ENABLE")}";
                    return null;
                });
        }
    }
}
