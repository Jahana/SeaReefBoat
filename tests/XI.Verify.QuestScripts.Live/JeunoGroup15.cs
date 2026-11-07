using System;
using System.IO;
using XI.Quests;
using XI.QuestContent;
using XI.Quests.Authoring;

// quest rework B3b Jeuno group 15 (the twenty Unlocking a Myth quests): Player.VigilWeaponRequiredWsPoints, read on the
// live context against the Lua call xi.equipment.vigilWeaponRequiredWsPoints(player). The era module
// modules/era/lua/globals/equipment/unlocking_a_myth.lua overrides the global, not a quest; the server's Lua module
// loader applies it (platform round P11: QuestScripts.Live p11 and golden jeuno.unlocking_a_myth_warrior#era). Here the
// module file itself is run (modules/module_utils.lua first) and its override of the global applied with applyOverride,
// so the member is shown to call whatever the global is: the base 250, then the era's value by NyzulFloorProgress
// (0: 16000, 45: 3120, 80: 880, 100: 250), then the base again once the global is put back. A probe quest answers
// Samia (Bastok Markets) while the player's local var JG15 is 1 and replies nothing.
internal static partial class Program
{
    private const string JG15Module = "modules/era/lua/globals/equipment/unlocking_a_myth.lua";

    private static void RunJeunoGroup15()
    {
        Console.WriteLine("quest rework B3b Jeuno group 15: Player.VigilWeaponRequiredWsPoints");
        uint id = CharIds[0];
        zone.TestQuestScripts.Add(new JG15Quest());
        Lua("InteractionGlobal.reload(true)");
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        string root = Path.GetDirectoryName(dataDir_).Replace('\\', '/');
        bool applied = false;
        try
        {
            // member/Lua at one NyzulFloorProgress
            string Read(int floor)
            {
                Lua($"player:setCharVar('NyzulFloorProgress', {floor})");
                Lua("player:setLocalVar('JG15', 1)");
                TalkTo("Samia");
                FinishIfEvent();
                Lua("player:setLocalVar('JG15', 0)");
                string luaValue = Lua("report(tostring(xi.equipment.vigilWeaponRequiredWsPoints(player)))");
                return $"{JG15Quest.Points}/{luaValue}";
            }
            string before = Read(0);
            // the module file run, its override of the global applied (xi.pre(ABYSSEA)'s condition is not what is shown here:
            // the override is applied as the C++ server applies it when the module is on)
            string found = Lua($@"
                JG15Saved = xi.equipment.vigilWeaponRequiredWsPoints
                assert(loadfile('{root}/modules/module_utils.lua'))()
                for i = #xi.module.registry, 1, -1 do xi.module.registry[i] = nil end
                assert(loadfile('{root}/{JG15Module}'))()
                local n = 0
                for _, m in ipairs(xi.module.registry) do
                    for _, o in ipairs(m.overrides) do
                        if o.name == 'xi.equipment.vigilWeaponRequiredWsPoints' then
                            applyOverride(xi.equipment, 'vigilWeaponRequiredWsPoints', o.func, o.name, '{root}/{JG15Module}')
                            n = n + 1
                        end
                    end
                end
                for i = #xi.module.registry, 1, -1 do xi.module.registry[i] = nil end
                report(tostring(n))");
            applied = true;
            string era = $"{Read(0)},{Read(45)},{Read(80)},{Read(100)}";
            Lua("xi.equipment.vigilWeaponRequiredWsPoints = JG15Saved JG15Saved = nil");
            applied = false;
            string after = Read(80);
            Check("jg15m:VigilWeaponRequiredWsPoints is the Lua global xi.equipment.vigilWeaponRequiredWsPoints(player): the base 250",
                  "250/250", before);
            Check("jg15m:  ... the era module's file applies one override of the global", "1", found);
            Check("jg15m:  ... under it, the member follows the override (NyzulFloorProgress 0, 45, 80, 100)",
                  "16000/16000,3120/3120,880/880,250/250", era);
            Check("jg15m:  ... the global put back: 250 again (NyzulFloorProgress 80)", "250/250", after);
        }
        finally
        {
            if (applied) Lua("xi.equipment.vigilWeaponRequiredWsPoints = JG15Saved JG15Saved = nil");
            Lua("player:setCharVar('NyzulFloorProgress', 0) player:setLocalVar('JG15', 0)");
            Logout();
            Cleanup(db, id);
            zone.TestQuestScripts.Clear();
            Lua("InteractionGlobal.reload(true)");
        }
    }

    private sealed class JG15Quest : QuestScript
    {
        public static int Points;

        public JG15Quest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q => q.Player.LocalVar("JG15") == 1)
                .Npc(Zone.BastokMarkets, "Samia").OnTrigger(q =>
                {
                    Points = q.Player.VigilWeaponRequiredWsPoints;
                    return null;
                });
        }
    }
}
