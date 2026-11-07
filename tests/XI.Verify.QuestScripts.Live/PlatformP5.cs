using System;
using System.Globalization;
using System.Linq;
using XI.QuestContent;
using XI.Quests;
using XI.Quests.Authoring;

// quest rework P5 (the platform needs the San d'Oria, Windurst and Jeuno drafts found): a probe quest of the fixture's key
// answers Samia (Bastok Markets) while the player's P5 local var names a probe; each reads the live context or replies as
// the Lua quest would, compared with what the Lua call gives
internal static partial class Program
{
    private static void RunPlatformP5()
    {
        Console.WriteLine("quest rework P5: platform needs");
        RunP5MainNumber();
        RunP5TextIdReplies();
        RunP5AfterZoneIn();
    }

    // 3. OnAfterZoneIn: the C# quest's section holds afterZoneIn = { [1] = handler } in the zone's table, where
    // InteractionLookup:afterZoneIn finds it among the Lua containers' (the same lookup, the same order); luautils::
    // AfterZoneIn runs it 4 s after the zone-in, and its reply is performed with no NPC: the char's own event. A reply the
    // NPC would show is refused and logged; the char var P5 picks the probe (a zone-in clears local vars)
    private static void RunP5AfterZoneIn()
    {
        uint id = CharIds[0];
        zone.TestQuestScripts.Add(new P5AfterZoneInQuest());
        Lua("InteractionGlobal.reload(true)");
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        string entry = Lua($@"
            local ids = {{}}
            for _, e in ipairs(InteractionGlobal.lookup.data[xi.zone.BASTOK_MARKETS].afterZoneIn[1] or {{}}) do
                ids[#ids + 1] = e.container and e.container.id or '-'
            end
            report(table.concat(ids, ','))");
        Check("afterZoneIn: the C# quest's handler sits in the zone's afterZoneIn[1] list of the lookup", "True",
              entry.Split(',').Contains(FixtureContainer).ToString());
        Lua("player:setCharVar('P5', 4)");
        Logout();
        Login();
        Tick(3000);
        Check("  3 s after the zone-in: nothing yet", "none", EvId());
        int mark = log.Count;
        Tick(1500);
        Check("  4.5 s after: the reply's event 9997, the char's own (no NPC)", $"{id}:9997", EvId());
        Check("  the C# handler ran, no quests: line", "", string.Join(" | ", log.Skip(mark).Where(l => l.StartsWith("quests: "))));
        Finish();
        Lua("player:setCharVar('P5', 5)");
        Logout();
        Login();
        Collect();
        pending_.Clear();
        mark = log.Count;
        Tick(4500);
        Check("  a reply with a text the NPC shows is refused: no event, and logged", "none/True",
              $"{EvId()}/{log.Skip(mark).Any(l => l == $"quests: {FixtureKey} afterZoneIn: a MessageText reply needs an NPC to show it, and an afterZoneIn has none")}");
        Lua("player:setCharVar('P5', 0)");
        Logout();
        Cleanup(db, id);
        zone.TestQuestScripts.Clear();
        Lua("InteractionGlobal.reload(true)");
    }

    // the char var P5 4: afterZoneIn in Bastok Markets replies event 9997; 5: a MessageText (refused: no NPC)
    private sealed class P5AfterZoneInQuest : QuestScript
    {
        public P5AfterZoneInQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .OnAfterZoneIn(Zone.BastokMarkets, q => q.Player.CharVar("P5") switch
                {
                    4 => QuestReply.Event(9997),
                    5 => QuestReply.MessageText(Text.BastokMarkets.ITEM_OBTAINED),
                    _ => null,
                });
        }
    }

    // 4. QuestReply.MessageSpecial(int textId, ...) / MessageName(int textId, ...): the C# action of Samia's reply by id is
    // the Lua action of quest:messageSpecial(id + 1, 0, item) / quest:messageName(id + 2), field by field; in play the
    // 0x02A carries id + 1 (and its parameter), messageName's (showText, a 0x02A from Samia) id + 2
    private static void RunP5TextIdReplies()
    {
        P1Probe(new P5TextIdQuest(), () =>
        {
            int text = TextId(Zone.BastokMarkets, "ITEM_OBTAINED");
            var special = P5Fields(2, $"messageSpecial({text + 1}, 0, {Item.LightAxe})").Split('|');
            Check("text id: MessageSpecial(id + 1, 0, item): the C# action's fields are Lua's quest:messageSpecial(id + 1, 0, item)'s", special[1], special[0]);
            Check("  the id, the messageType (Special), the args", $"{text + 1}/2/", string.Join("/", special[0].Split('/').Take(2)) + "/");
            var name = P5Fields(3, $"messageName({text + 2})").Split('|');
            Check("text id: MessageName(id + 2): the C# action's fields are Lua's quest:messageName(id + 2)'s", name[1], name[0]);

            Lua("player:setLocalVar('P5', 2)");
            TalkTo("Samia");
            Lua("player:setLocalVar('P5', 0)");
            Check("  in play: the 0x02A carries id + 1 and the item", $"True", Special().Split(',').Contains($"{text + 1}:0").ToString());
            Lua("player:setLocalVar('P5', 3)");
            TalkTo("Samia");
            Lua("player:setLocalVar('P5', 0)");
            Check("  in play: messageName's message (showText: a 0x02A from Samia) carries id + 2", "True",
                  Special().Split(',').Any(m => m.StartsWith($"{text + 2}:")).ToString());
        });
    }

    // Samia's probe reply (P5 = probe) against the Lua action of luaCall on Quest:new of the fixture's ids, as
    // "<C# fields>|<Lua fields>": its id, messageType, priority and each option as type:value
    private static string P5Fields(int probe, string luaCall)
    {
        uint samia = NpcId("Samia");
        return Lua($@"
            player:setLocalVar('P5', {probe})
            local function fields(a)
                local parts = {{ tostring(a.id), tostring(a.messageType), tostring(a.priority) }}
                for i = 1, #(a.options or {{}}) do parts[#parts + 1] = type(a.options[i]) .. ':' .. tostring(a.options[i]) end
                return table.concat(parts, '/')
            end
            local cs
            for _, e in ipairs(InteractionGlobal.lookup.data[xi.zone.BASTOK_MARKETS]['Samia'].onTrigger) do
                if e.container and e.container.id == '{FixtureContainer}' then cs = e.handler(player, GetNPCByID({samia})) end
            end
            local q = Quest:new({FixtureLuaIds})
            report(fields(cs) .. '|' .. fields(q:{luaCall}))
            player:setLocalVar('P5', 0)");
    }

    // P5 2: MessageSpecial(ITEM_OBTAINED + 1, 0, Light Axe); P5 3: MessageName(ITEM_OBTAINED + 2), by id (one handler: the
    // probe reads the container's Samia handler without its check)
    private sealed class P5TextIdQuest : QuestScript
    {
        public P5TextIdQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q => q.Player.LocalVar("P5") is 2 or 3)
                .Npc(Zone.BastokMarkets, "Samia").OnTrigger(q => q.Player.LocalVar("P5") == 2
                    ? QuestReply.MessageSpecial(q.Text.Id(Text.BastokMarkets.ITEM_OBTAINED) + 1, 0, Item.LightAxe)
                    : QuestReply.MessageName(q.Text.Id(Text.BastokMarkets.ITEM_OBTAINED) + 2));
        }
    }

    // 5. Settings.MainNumber: the live context reads xi.settings.main[name] as the Lua number it is. With GIL_RATE 1.5 a
    // handler's 400 * MainNumber is Lua's 400 * xi.settings.main.GIL_RATE (600); Main rounds the fraction (llround: 2)
    private static void RunP5MainNumber()
    {
        P1Probe(new P5SettingsQuest(), () =>
        {
            string before = Lua("report(tostring(xi.settings.main.GIL_RATE))");
            try
            {
                Lua("xi.settings.main.GIL_RATE = 1.5 xi.settings.main.P5_FLAG = true");
                Lua("player:setLocalVar('P5', 1)");
                TalkTo("Samia");
                Lua("player:setLocalVar('P5', 0)");
                string lua = Lua("report(tostring(400 * xi.settings.main.GIL_RATE))");
                Check("setting: MainNumber reads GIL_RATE 1.5 as Lua has it; 400 * it is Lua's 400 * GIL_RATE; Main rounds it (2)",
                      $"1.5/{lua}/2", $"{P5SettingsQuest.Number.ToString(CultureInfo.InvariantCulture)}/{P5SettingsQuest.Gil}/{P5SettingsQuest.Whole}");
                Check("  a boolean reads 1, an unset name 0, through both", "1/1|0/0", P5SettingsQuest.Others);
            }
            finally
            {
                Lua($"xi.settings.main.GIL_RATE = {before} xi.settings.main.P5_FLAG = nil");
            }
        });
    }

    // while P5 is 1, Samia's handler reads the settings into its fields and replies nothing
    private sealed class P5SettingsQuest : QuestScript
    {
        public static double Number;
        public static int Whole, Gil;
        public static string Others;

        public P5SettingsQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q => q.Player.LocalVar("P5") == 1)
                .Npc(Zone.BastokMarkets, "Samia").OnTrigger(q =>
                {
                    Number = q.Settings.MainNumber("GIL_RATE");
                    Whole = q.Settings.Main("GIL_RATE");
                    Gil = (int)(400 * q.Settings.MainNumber("GIL_RATE"));
                    Others = $"{q.Settings.MainNumber("P5_FLAG")}/{q.Settings.Main("P5_FLAG")}|{q.Settings.MainNumber("P5_NOPE")}/{q.Settings.Main("P5_NOPE")}";
                    return null;
                });
        }
    }
}
