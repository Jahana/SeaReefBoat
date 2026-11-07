using System;
using System.Collections.Generic;
using System.Linq;
using XI.QuestContent;
using XI.Quests;
using XI.Quests.Authoring;

// quest rework P2: the event option table's named fields, and the reply arguments the engine would drop. A probe quest
// of the fixture's key answers Samia (Bastok Markets) with the reply the player's P2 local var names; its action is
// compared, field by field and type by type, with the action the same Lua helper call builds (the tables are real
// examples from scripts/quests and scripts/missions), and played where the value shows (the event ParseEvent built).
internal static partial class Program
{
    private static void RunPlatformP2()
    {
        Console.WriteLine("quest rework P2: named event option fields, dropped reply args");
        RunP2EventFields();
        RunP2DroppedArgs();
        RunP2EmptyItemParams();
    }

    // 2 (c). an itemParams with no field set pushes nothing: the reward table is the Lua { item = X }, not
    // { item = X, itemParams = {} }
    private static void RunP2EmptyItemParams()
    {
        P1Probe(new P2EmptyItemParamsQuest(), () =>
        {
            string got = Lua($@"
                local function ser(v)
                    if type(v) ~= 'table' then return type(v) .. ':' .. tostring(v) end
                    local keys = {{}}
                    for k in pairs(v) do keys[#keys + 1] = k end
                    table.sort(keys, function(x, y) return tostring(x) < tostring(y) end)
                    local out = {{}}
                    for _, k in ipairs(keys) do out[#out + 1] = tostring(k) .. '=' .. ser(v[k]) end
                    return '{{' .. table.concat(out, ',') .. '}}'
                end
                local cs
                for _, e in ipairs(InteractionGlobal.lookup.data[xi.zone.BASTOK_MARKETS]['Samia'].onTrigger) do
                    if e.container and e.container.id == '{FixtureContainer}' then cs = e.container.reward end
                end
                report(ser(cs) .. '|' .. ser({{ item = xi.item.LIGHT_AXE }}))");
            var halves = got.Split('|');
            Check("empty itemParams: the reward table is the Lua { item = X }, no itemParams = {}", halves[1], halves[0]);
            Check("  the table", "{item=number:16667}", halves[0]);
        });
    }

    // the fixture's key with a Light Axe reward and an itemParams with no field set
    private sealed class P2EmptyItemParamsQuest : QuestScript
    {
        public P2EmptyItemParamsQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.LightAxe, ItemParams = new QuestItemParams() });
            b.AnyState(when: q => q.Player.LocalVar("P2") == 99)
                .Npc(Zone.BastokMarkets, "Samia").OnTrigger(q => null);
        }
    }

    // 2 (a, b). a boolean or string the engine would drop is refused when the bridge pushes the reply: nothing is pushed
    // (the handler gives no action, and the line is logged), where Lua would start the event with the argument dropped,
    // which the plain Lua call shows. A boolean the engine reads (messageSpecial's showName) is pushed as Lua pushes it
    private static void RunP2DroppedArgs()
    {
        zone.TestQuestScripts.Add(new P2CallQuest());
        P1Probe(new P2ProbeQuest(), () =>
        {
            int text = TextId(Zone.BastokMarkets, "ITEM_OBTAINED");
            string Refused(int probe, string luaCall)
            {
                int mark = log.Count;
                var halves = P2Fields(probe, luaCall).Split('|');
                string line = log.Skip(mark).FirstOrDefault(l => l.Contains("would be dropped")) ?? "no line";
                // "quests: <key> <handler>: <problem>"
                int at = line.IndexOf(": ", line.IndexOf(": ", StringComparison.Ordinal) + 2, StringComparison.Ordinal);
                return $"{halves[0]}/{(at < 0 ? line : line.Substring(at + 2))}";
            }
            string Started(string call)
            {
                Lua(call);
                if (!c.IsInEvent) return "no event";
                string got = $"{c.CurrentEvent.EventId} [{string.Join(",", c.CurrentEvent.Params.Select(kv => $"{kv.Key}={kv.Value}"))}] [{string.Join(",", c.CurrentEvent.Strings.Values)}]";
                FinishIfEvent(99);
                return got;
            }

            Check("dropped args: ProgressEvent(9998, 1, true) is refused: no action, the line logged",
                  "nil/ProgressEvent argument 2 (true) would be dropped: ParseEvent reads event parameters as numbers only; write the number (isLast and 1 or 0), a string with OptionString",
                  Refused(11, "progressEvent(9998, 1, true)"));
            Check("  as the engine drops it: player:startEvent(9998, 1, true) has only [0] = 1", "9998 [0=1] []", Started("player:startEvent(9998, 1, true)"));
            Check("  in play: Samia starts no 9998", "True", (P2PlayedId(11) != 9998).ToString());
            Check("dropped args: Event(9998, 'abc', 5) is refused (a string through OptionString)", "True",
                  Refused(10, "event(9998, 'abc', 5)").StartsWith("nil/Event argument 1 ('abc') would be dropped: ParseEvent").ToString());
            Check("  as the engine reads it: 'abc' is the event's string and the 5 is dropped", "9998 [] [abc]", Started("player:startEvent(9998, 'abc', 5)"));
            Check("dropped args: Event(9998, 0, true).Option(2, 1) is refused (the option table's [1])", "True",
                  Refused(12, "event(9998, { [0] = 0, [1] = true, [2] = 1 })").StartsWith("nil/Event argument 2 (true) would be dropped: the option table's [1]").ToString());
            Check("  as the engine drops it: { [0] = 0, [1] = true, [2] = 1 } has only [2] = 1", "9998 [2=1] []",
                  Started("player:startEvent(9998, { [0] = 0, [1] = true, [2] = 1 })"));
            Check("dropped args: MessageText(X, true) is refused (the text action passes no arguments)", "True",
                  Refused(13, $"messageText({text}, true)").EndsWith("passes no arguments").ToString());
            Check("dropped args: MessageSpecial(X, true) is refused (p0 is a number only)", "True",
                  Refused(15, $"messageSpecial({text}, true)").Contains("messageSpecial reads its first four arguments as numbers only").ToString());

            // a boolean the engine reads stays legal, pushed as Lua pushes it
            var halves = P2Fields(14, $"messageSpecial({text}, 1, 2, 3, 4, true)").Split('|');
            Check("dropped args: MessageSpecial(X, 1, 2, 3, 4, true) is pushed as the Lua one", halves[1], halves[0]);
            Check("  its showName a Lua boolean", "boolean:true", halves[0].Split('/').Last());
            Collect();
            Lua("player:setLocalVar('P2', 14)");
            TalkTo("Samia");
            Lua("player:setLocalVar('P2', 0)");
            Check("  in play: the message with its four numbers", $"{text}:1", Special().Split(',').FirstOrDefault(m => m.StartsWith($"{text}:")) ?? "none");
            FinishIfEvent(99);

            // an NPC call: player:messageName(id, player, p0..p3, chat, sender, showSender) takes no (..., true, false)
            uint umberto = NpcId("Umberto");
            int mark = log.Count;
            Lua("player:setLocalVar('P2', 16)");
            string called = Lua($"report(tostring(xi.cs.quest(player, GetNPCByID({umberto}), '{CallKey}', 'onTrigger')))");
            Lua("player:setLocalVar('P2', 0)");
            Check("dropped args: an NPC call's MessageName(X, 0, 0, 0, 0, true, false) is refused: false, the line logged",
                  "false/True", $"{called}/{log.Skip(mark).Any(l => l.Contains("MessageName argument 5 (true) would be dropped: messageName's chat type"))}");
            mark = log.Count;
            Lua("player:setLocalVar('P2', 17)");
            string sender = Lua($"report(tostring(xi.cs.quest(player, GetNPCByID({umberto}), '{CallKey}', 'onTrigger')))");
            Lua("player:setLocalVar('P2', 0)");
            Check("dropped args: an NPC call's MessageName(X, 0, 0, 0, 0, 4, 7) is refused (a number as the sender): false, the line logged",
                  "false/True", $"{sender}/{log.Skip(mark).Any(l => l.Contains("MessageName argument 6 (7) would be dropped: messageName's argument 6 is the sender entity"))}");
        });
    }

    // the event id Samia's P2 probe reply starts, 0 for none
    private static int P2PlayedId(int probe)
    {
        Lua($"player:setLocalVar('P2', {probe})");
        TalkTo("Samia");
        Lua("player:setLocalVar('P2', 0)");
        int id = c.IsInEvent ? c.CurrentEvent.EventId : 0;
        FinishIfEvent(99);
        return id;
    }

    // the C# action Samia's P2 probe reply builds against the Lua action of luaCall (on Quest:new of the fixture's ids),
    // as "<C# fields>|<Lua fields>": its id, priority, cutscene kind and each option as type:value, tables serialised
    private static string P2Fields(int probe, string luaCall)
    {
        uint samia = NpcId("Samia");
        return Lua($@"
            player:setLocalVar('P2', {probe})
            local function ser(v)
                if type(v) ~= 'table' then return type(v) .. ':' .. tostring(v) end
                local keys = {{}}
                for k in pairs(v) do keys[#keys + 1] = k end
                table.sort(keys, function(x, y) return tostring(x) < tostring(y) end)
                local out = {{}}
                for _, k in ipairs(keys) do out[#out + 1] = tostring(k) .. '=' .. ser(v[k]) end
                return '{{' .. table.concat(out, ',') .. '}}'
            end
            local function fields(a)
                if a == nil then return 'nil' end
                local parts = {{ tostring(a.id), tostring(a.priority), tostring(a.isCutscene), tostring(a.isOptionalCutscene) }}
                for i = 1, #(a.options or {{}}) do parts[#parts + 1] = ser(a.options[i]) end
                return table.concat(parts, '/')
            end
            local cs
            for _, e in ipairs(InteractionGlobal.lookup.data[xi.zone.BASTOK_MARKETS]['Samia'].onTrigger) do
                if e.container and e.container.id == '{FixtureContainer}' then cs = e.handler(player, GetNPCByID({samia})) end
            end
            local q = Quest:new({FixtureLuaIds})
            report(fields(cs) .. '|' .. fields(q:{luaCall}))
            player:setLocalVar('P2', 0)");
    }

    // the event Samia's P2 probe reply starts, as ParseEvent built it: id, params, text table, interrupt text, flags,
    // canSkip, isHidden, type, cutscene options
    private static string P2Played(int probe)
    {
        Lua($"player:setLocalVar('P2', {probe})");
        TalkTo("Samia");
        Lua("player:setLocalVar('P2', 0)");
        if (!c.IsInEvent) return "no event";
        var e = c.CurrentEvent;
        string got = $"{e.EventId} [{string.Join(",", e.Params.Select(kv => $"{kv.Key}={kv.Value}"))}] tt={e.TextTable} it={e.InterruptText} " +
                     $"flags={e.EventFlags} skip={e.CanSkip} hidden={e.IsHidden} {e.Type} cs=[{string.Join(",", e.CutsceneOptions)}]";
        FinishIfEvent(99);
        return got;
    }

    // 1. named fields: each reply's table is the Lua one, type by type; in play ParseEvent reads each into the event
    private static void RunP2EventFields()
    {
        zone.TestQuestScripts.Add(new P2CallQuest());
        P1Probe(new P2ProbeQuest(), () =>
        {
            void Same(string name, int probe, string luaCall, string table)
            {
                var halves = P2Fields(probe, luaCall).Split('|');
                Check($"named fields: {name}: the C# action is the Lua {luaCall}", halves[1], halves[0]);
                Check("  the table", table, halves[0].Split('/', 5).ElementAtOrDefault(4));
            }
            // Promotion: Corporal (ahtUrhgan), Promotion_Corporal.lua:106
            Same("text_table", 1, "progressEvent(5046, { [0] = 3, text_table = 0 })", "{0=number:3,text_table=number:0}");
            // Crimson Orb (hiddenQuests), Crimson_Orb.lua:123
            Same("canSkip", 2, "event(24, { canSkip = true }):replaceDefault()", "{canSkip=boolean:true}");
            // CoP 5-1 The Enduring Tumult of War, 5_1_The_Enduring_Tumult_of_War.lua:158
            Same("cs_option", 3, "progressOptionalCutscene(69, { cs_option = 0, canSkip = true })", "{canSkip=boolean:true,cs_option=number:0}");
            // ToAU 15 The Black Coffin, 15_The_Black_Coffin.lua:44
            Same("isHidden", 4, "progressEvent(35, { isHidden = true })", "{isHidden=boolean:true}");
            // Promotion: First Lieutenant (ahtUrhgan), Promotion_First_Lieutenant.lua:131
            Same("text_table beside [0]", 5, "event(5088, { [0] = 1, text_table = 0 })", "{0=number:1,text_table=number:0}");
            // every field ParseEvent reads, and cs_option's table form
            Same("all fields", 6, "event(9997, { [1] = 2, text_table = 5, interrupt_text = 3, flags = 65540, isHidden = true, canSkip = true })",
                 "{1=number:2,canSkip=boolean:true,flags=number:65540,interrupt_text=number:3,isHidden=boolean:true,text_table=number:5}");
            Same("cs_option's table", 7, "progressOptionalCutscene(9996, { cs_option = { 1, 2 }, canSkip = true })",
                 "{canSkip=boolean:true,cs_option={1=number:1,2=number:2}}");

            Check("named fields: in play, text_table 0 and [0] = 3", "5046 [0=3] tt=0 it=0 flags=0 skip=False hidden=False Normal cs=[]", P2Played(1));
            Check("  every field", "9997 [1=2] tt=5 it=3 flags=65540 skip=True hidden=True Normal cs=[]", P2Played(6));
            Check("  cs_option's table adds to the default 0", "9996 [] tt=-1 it=0 flags=0 skip=True hidden=False OptionalCutscene cs=[0,1,2]", P2Played(7));
            Check("  cs_option = 0 with canSkip", "69 [] tt=-1 it=0 flags=0 skip=True hidden=False OptionalCutscene cs=[0]", P2Played(3));
            Check("  cs_option = 3 replaces the default", "9995 [] tt=-1 it=0 flags=0 skip=False hidden=False OptionalCutscene cs=[3]", P2Played(8));

            // an NPC call renders the same table into player:startEvent
            uint umberto = NpcId("Umberto");
            Lua("player:setLocalVar('P2', 9)");
            Collect();
            string called = Lua($"report(tostring(xi.cs.quest(player, GetNPCByID({umberto}), '{CallKey}', 'onTrigger')))");
            Lua("player:setLocalVar('P2', 0)");
            var e = c.IsInEvent ? c.CurrentEvent : null;
            Check("named fields: an NPC call's startEvent gets them too", "true/9994 [0=4] tt=0 skip=True hidden=True",
                  $"{called}/{(e == null ? "no event" : $"{e.EventId} [{string.Join(",", e.Params.Select(kv => $"{kv.Key}={kv.Value}"))}] tt={e.TextTable} skip={e.CanSkip} hidden={e.IsHidden}")}");
            FinishIfEvent(99);
        });
    }

    // the fixture's key, any status while P2 is set: Samia answers with the reply P2 names
    private sealed class P2ProbeQuest : QuestScript
    {
        public P2ProbeQuest() : base(FixtureLog, FixtureConst) { }

        private static QuestReply Reply(int i) => i switch
        {
            1 => QuestReply.ProgressEvent(5046).Option(0, 3).TextTable(0),
            2 => QuestReply.Event(24).CanSkip().ReplaceDefault(),
            3 => QuestReply.ProgressOptionalCutscene(69).CsOption(0).CanSkip(),
            4 => QuestReply.ProgressEvent(35).Hidden(),
            5 => QuestReply.Event(5088).Option(0, 1).TextTable(0),
            6 => QuestReply.Event(9997).Option(1, 2).TextTable(5).InterruptText(3).Flags(65540).Hidden().CanSkip(),
            7 => QuestReply.ProgressOptionalCutscene(9996).CsOptions(1, 2).CanSkip(),
            8 => QuestReply.ProgressOptionalCutscene(9995).CsOption(3),
            // 2: dropped args
            10 => QuestReply.Event(9998, "abc", 5),
            11 => QuestReply.ProgressEvent(9998, 1, true),
            12 => QuestReply.Event(9998, 0, true).Option(2, 1),
            13 => QuestReply.MessageText(Text.BastokMarkets.ITEM_OBTAINED, true),
            14 => QuestReply.MessageSpecial(Text.BastokMarkets.ITEM_OBTAINED, 1, 2, 3, 4, true),
            15 => QuestReply.MessageSpecial(Text.BastokMarkets.ITEM_OBTAINED, true),
            _ => null,
        };

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q => q.Player.LocalVar("P2") > 0)
                .Npc(Zone.BastokMarkets, "Samia").OnTrigger(q => Reply(q.Player.LocalVar("P2")));
        }
    }

    // A Discerning Eye (CallKey) as a call-only quest: Umberto's called onTrigger answers with the reply P2 names
    private sealed class P2CallQuest : QuestScript
    {
        public P2CallQuest() : base(QuestLog.Bastok, "A_DISCERNING_EYE") { }

        private static QuestReply Reply(int i) => i switch
        {
            9 => QuestReply.Event(9994).Option(0, 4).TextTable(0).CanSkip().Hidden(),
            16 => QuestReply.MessageName(Text.BastokMarkets.ITEM_OBTAINED, 0, 0, 0, 0, true, false),
            17 => QuestReply.MessageName(Text.BastokMarkets.ITEM_OBTAINED, 0, 0, 0, 0, 4, 7),
            _ => null,
        };

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available)
                .Npc(Zone.BastokMarkets, "Umberto").Called().OnTrigger(q => Reply(q.Player.LocalVar("P2")));
        }
    }
}
