using System;
using System.Collections.Generic;
using System.Linq;
using XI.QuestContent;
using XI.Quests;
using XI.Quests.Authoring;

// quest rework P1 (the platform needs drafting found): each new reply form pushes the Lua value as Lua would. A probe
// quest of the fixture's key answers Samia (Bastok Markets) with the reply the player's P1 local var names; its action
// is compared, field by field, with the action the same Lua helper call builds, and played where the value shows.
internal static partial class Program
{
    private static void RunPlatformP1()
    {
        Console.WriteLine("quest rework P1: platform needs");
        RunP1TypedArgs();
        RunP1StringParams();
        RunP1OptionText();
        RunP1SequenceFace();
        RunP1RewardItemParams();
    }

    // 5. reward itemParams: the C# quest's reward table is the Lua one ({ item = X, itemParams = { fromTrade = true } }), so
    // npcUtil.completeQuest passes fromTrade to npcUtil.giveItem: with no room the message is ITEM_CANNOT_BE_OBTAINED + 4
    // ("Try trading again ..."), and the quest is not completed; with room it is, with the item
    private static void RunP1RewardItemParams()
    {
        P1Probe(new P1RewardQuest(), () =>
        {
            uint samia = NpcId("Samia");
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
                report(ser(cs) .. '|' .. ser({{ item = xi.item.LIGHT_AXE, itemParams = {{ fromTrade = true }} }}))");
            var halves = got.Split('|');
            Check("reward itemParams: the C# quest's reward table is the Lua one", halves[1], halves[0]);
            Check("  the table", "{item=number:16667,itemParams={fromTrade=boolean:true}}", halves[0]);

            int cannot = TextId(Zone.BastokMarkets, "ITEM_CANNOT_BE_OBTAINED");
            FillInventory();
            Lua("player:setLocalVar('P1', 7)");
            TalkTo("Samia");
            Check("  in play, no room: ITEM_CANNOT_BE_OBTAINED + 4 (fromTrade), not completed", $"{cannot + 4}:{Item.LightAxe}/00",
                  $"{Special()}/{QuestBits(FixtureLogId, FixtureQuestId)}");
            FinishIfEvent();
            FreeInventory();
            TalkTo("Samia");
            FinishIfEvent();
            Lua("player:setLocalVar('P1', 0)");
            Check("  with room: completed, the item given", "01/1",
                  $"{QuestBits(FixtureLogId, FixtureQuestId)}/{Qty(Item.LightAxe)}");
        });
    }

    // the fixture's key with a reward of a Light Axe given as from a trade: while P1 is 7, talking to Samia completes it
    private sealed class P1RewardQuest : QuestScript
    {
        public P1RewardQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.LightAxe, ItemParams = new QuestItemParams { FromTrade = true } });
            b.AnyState(when: q => q.Player.LocalVar("P1") == 7)
                .Npc(Zone.BastokMarkets, "Samia").OnTrigger(q =>
                {
                    q.Quest.Complete();
                    return null;
                });
        }
    }

    // 4. a sequence text step with a face: the C# sequence's chain of actions (Sequence:new's linked list from first) is
    // the Lua one's, node by node: the faced text is one Message with face 82, not a Message then a Face. In play Samia
    // turns to 82 with the second text and to 115 at the end
    private static void RunP1SequenceFace()
    {
        P1Probe(new P1ArgsProbeQuest(), () =>
        {
            int x = TextId(Zone.BastokMarkets, "ITEM_OBTAINED"), y = TextId(Zone.BastokMarkets, "GIL_OBTAINED");
            uint samia = NpcId("Samia");
            string got = Lua($@"
                player:setLocalVar('P1', 6)
                local function chain(a)
                    local out = {{}}
                    local node = a.first
                    while node do
                        out[#out + 1] = tostring(node.type) .. ':' .. tostring(node.id) .. ':' ..
                            (type(node.face) == 'number' and tostring(node.face) or '-') .. ':' .. tostring(node.milliseconds)
                        node = node.__nextAction
                    end
                    return table.concat(out, ' ')
                end
                local cs
                for _, e in ipairs(InteractionGlobal.lookup.data[xi.zone.BASTOK_MARKETS]['Samia'].onTrigger) do
                    if e.container and e.container.id == '{FixtureContainer}' then cs = e.handler(player, GetNPCByID({samia})) end
                end
                local q = Quest:new({FixtureLuaIds})
                local lua = q:sequence({{ text = {x}, wait = 1000 }}, {{ text = {y}, face = 82, wait = 2000 }}, {{ face = 115 }})
                report(chain(cs) .. '|' .. chain(lua) .. '|' .. tostring(cs.id) .. '/' .. tostring(lua.id))
                player:setLocalVar('P1', 0)");
            var parts = got.Split('|');
            Check("sequence face: the C# chain of actions is the Lua one's", parts[1], parts[0]);
            Check("  the faced text is one Message with face 82", $"True", parts[0].Contains($":{y}:82:").ToString());
            Check("  the sequence's id, the first text", $"{x}/{x}", parts[2]);

            var npc = Npc(samia);
            Lua("player:setLocalVar('P1', 6)");
            TalkTo("Samia");
            Lua("player:setLocalVar('P1', 0)");
            for (int i = 0; i < 3; i++) Tick(500);
            byte mid = npc.Rotation;
            for (int i = 0; i < 5; i++) Tick(500);
            Check("  in play: Samia faces 82 with the second text, 115 at the end", "82/115", $"{mid}/{npc.Rotation}");
        });
    }

    // the C# action Samia's probe reply builds against the Lua action of luaCall (on Quest:new of the fixture's ids), as
    // "<C# fields>|<Lua fields>": its id, messageType, priority and each option as type:value
    private static string P1Fields(int probe, string luaCall)
    {
        uint samia = NpcId("Samia");
        return Lua($@"
            player:setLocalVar('P1', {probe})
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
                local parts = {{ tostring(a.id), tostring(a.messageType), tostring(a.priority) }}
                for i = 1, #(a.options or {{}}) do parts[#parts + 1] = ser(a.options[i]) end
                return table.concat(parts, '/')
            end
            local cs
            for _, e in ipairs(InteractionGlobal.lookup.data[xi.zone.BASTOK_MARKETS]['Samia'].onTrigger) do
                if e.container and e.container.id == '{FixtureContainer}' then cs = e.handler(player, GetNPCByID({samia})) end
            end
            local q = Quest:new({FixtureLuaIds})
            report(fields(cs) .. '|' .. fields(q:{luaCall}))
            player:setLocalVar('P1', 0)");
    }

    private static void P1Probe(QuestScript quest, Action body)
    {
        uint id = CharIds[0];
        zone.TestQuestScripts.Add(quest);
        Lua("InteractionGlobal.reload(true)");
        Cleanup(db, id);
        Fixture(id, Zone.BastokMarkets, -296.0f, -16.0f, -157.0f, 0);
        Login();
        body();
        Logout();
        Cleanup(db, id);
        zone.TestQuestScripts.Clear();
        Lua("InteractionGlobal.reload(true)");
    }

    // 1. typed reply args: quest:messageName(id, 0, 0, 0, 0, true, false) is showText(npc, id, 0, 0, 0, 0, true, false):
    // showName true, turn false. The C# action's options are the Lua action's, booleans included; in play Samia keeps her
    // rotation, where the int form (1, 0: 0 is truthy in Lua) turns her
    private static void RunP1TypedArgs()
    {
        P1Probe(new P1ArgsProbeQuest(), () =>
        {
            int text = TextId(Zone.BastokMarkets, "ITEM_OBTAINED");
            string got = P1Fields(1, $"messageName({text}, 0, 0, 0, 0, true, false)");
            var halves = got.Split('|');
            Check("typed args: messageName(..., true, false): the C# action's fields are the Lua action's", halves[1], halves[0]);
            Check("  its last two options are Lua booleans", "boolean:true/boolean:false", string.Join("/", halves[0].Split('/').Skip(7)));
            // event(9998, 'abc', 5) is refused since P2 (ParseEvent reads a string as the event's string and drops the 5
            // after it): PlatformP2.cs RunP2DroppedArgs

            // showText's turn: the NPC looks at the speaker's target (LookAt) and faces it
            var samia = Npc(NpcId("Samia"));
            samia.LookAt = 0;
            Lua("player:setLocalVar('P1', 1)");
            TalkTo("Samia");
            Lua("player:setLocalVar('P1', 0)");
            Check("  in play: the message is sent and Samia does not turn to the char (turn false)", "True/0",
                  $"{Special().Split(',').Any(m => m.StartsWith($"{text}:"))}/{samia.LookAt}");
            Lua("player:setLocalVar('P1', 2)");
            TalkTo("Samia");
            Lua("player:setLocalVar('P1', 0)");
            Check("  the int form (1, 0) turns her: 0 is truthy in Lua", $"{c.TargId}", $"{samia.LookAt}");
        });
    }

    // 2. string event params: quest:event(113, { [0] = n, ['strings'] = { [0] = name } }) is
    // QuestReply.Event(113).Option(0, n).OptionString(0, name): one option table with its strings sub-table, which
    // startEvent's ParseEvent reads into the event's strings
    private static void RunP1StringParams()
    {
        P1Probe(new P1ArgsProbeQuest(), () =>
        {
            string got = P1Fields(4, "event(9998, { [0] = 1, [2] = 7, ['strings'] = { [0] = 'Qgolden', [1] = 'two' } })");
            var halves = got.Split('|');
            Check("event strings: the C# action's option table is the Lua one, strings sub-table included", halves[1], halves[0]);
            Check("  the table", "{0=number:1,2=number:7,strings={0=string:Qgolden,1=string:two}}", halves[0].Split('/', 4)[3]);
            Lua("player:setLocalVar('P1', 4)");
            TalkTo("Samia");
            Lua("player:setLocalVar('P1', 0)");
            Check("  in play: the event's strings and parameters", "9998 Qgolden,two 1,7",
                  $"{c.CurrentEvent.EventId} {string.Join(",", c.CurrentEvent.Strings.Values)} {c.CurrentEvent.Params.GetValueOrDefault(0)},{c.CurrentEvent.Params.GetValueOrDefault(2)}");
            Check("  sent as the string event (0x033)", "True", pending_.Concat(s.PacketList).Any(p => Opcode(p) == 0x033).ToString());
            FinishIfEvent();
        });
    }

    // 3. the string event option: GP_CLI_COMMAND_PASSWARDS (0x060) sends a typed text, which onEventUpdate gets as a Lua
    // string; a C# handler reads it as OptionText (Option 0), a number update as Option (OptionText null). The same
    // through an NPC call: xi.cs.questEvent(player, npc, key, csid, option, 'update') with a string option
    private static string p1Option_;

    private static void RunP1OptionText()
    {
        zone.TestQuestScripts.Add(new P1CallOptionQuest());
        P1Probe(new P1OptionTextQuest(), () =>
        {
            Lua("player:setLocalVar('P1', 5)");
            uint samia = TalkTo("Samia");
            Check("option text: Samia's event is the probe's 9998", $"{samia}:9998", EvId());
            p1Option_ = null;
            Collect();
            Send(0x060, 28, b =>
            {
                BitConverter.GetBytes(samia).CopyTo(b, 4);
                BitConverter.GetBytes(Npc(samia).TargId).CopyTo(b, 8);
                System.Text.Encoding.ASCII.GetBytes("kurou-morou").CopyTo(b, 12);
            });
            Check("  a typed text (0x060): OnEventUpdate gets OptionText, Option 0", "0/kurou-morou", p1Option_);
            p1Option_ = null;
            EndEvent(9998, 3, 1);
            Check("  a number update: Option, OptionText null", "3/null", p1Option_);
            Finish();
            Lua("player:setLocalVar('P1', 0)");
            p1Option_ = null;
            Check("  an NPC call with a string option: true", "true",
                  Lua($"report(tostring(xi.cs.questEvent(player, GetNPCByID({NpcId("Umberto")}), '{CallKey}', 9996, 'chululu', 'update')))"));
            Check("    the called OnEventUpdate got OptionText, Option 0", "0/chululu", p1Option_);
            p1Option_ = null;
            Lua($"xi.cs.questEvent(player, GetNPCByID({NpcId("Umberto")}), '{CallKey}', 9996, 4, 'update')");
            Check("    with a number: Option, OptionText null", "4/null", p1Option_);
        });
    }

    // the fixture's key while P1 is 5: Samia's 9998, whose update records what the handler got
    private sealed class P1OptionTextQuest : QuestScript
    {
        public P1OptionTextQuest() : base(FixtureLog, FixtureConst) { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q => q.Player.LocalVar("P1") == 5)
                .Npc(Zone.BastokMarkets, "Samia").OnTrigger(q => QuestReply.Event(9998));
            b.AnyState(when: q => q.Player.LocalVar("P1") == 5)
                .OnEventUpdate(Zone.BastokMarkets, 9998, q => p1Option_ = $"{q.Option}/{q.OptionText ?? "null"}");
        }
    }

    // A Discerning Eye (CallKey) as a call-only quest, as MobZoneCallQuest: a called update of 9996 records what it got
    private sealed class P1CallOptionQuest : QuestScript
    {
        public P1CallOptionQuest() : base(QuestLog.Bastok, "A_DISCERNING_EYE") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Available)
                .Npc(Zone.AltarRoom, XI.QuestContent.Npc.AltarRoom.YagudoAvatar).Called()
                .OnMobDeath(q => { })
                .OnEventUpdate(Zone.BastokMarkets, 9996, q => p1Option_ = $"{q.Option}/{q.OptionText ?? "null"}");
        }
    }

    // the fixture's key, any status while P1 is set: Samia answers with the reply P1 names
    private sealed class P1ArgsProbeQuest : QuestScript
    {
        public P1ArgsProbeQuest() : base(FixtureLog, FixtureConst) { }

        private static QuestReply Reply(int i) => i switch
        {
            1 => QuestReply.MessageName(Text.BastokMarkets.ITEM_OBTAINED, 0, 0, 0, 0, true, false),
            2 => QuestReply.MessageName(Text.BastokMarkets.ITEM_OBTAINED, 0, 0, 0, 0, 1, 0),
            4 => QuestReply.Event(9998).Option(0, 1).Option(2, 7).OptionString(0, "Qgolden").OptionString(1, "two"),
            6 => QuestReply.Sequence(QuestReply.MessageText(Text.BastokMarkets.ITEM_OBTAINED), QuestReply.Wait(1000),
                                     QuestReply.MessageText(Text.BastokMarkets.GIL_OBTAINED).WithFace(82), QuestReply.Wait(2000), QuestReply.Face(115)),
            _ => null,
        };

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState(when: q => q.Player.LocalVar("P1") > 0)
                .Npc(Zone.BastokMarkets, "Samia").OnTrigger(q => Reply(q.Player.LocalVar("P1")));
        }
    }
}
