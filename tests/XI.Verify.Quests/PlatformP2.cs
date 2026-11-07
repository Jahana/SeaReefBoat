// XI.Verify.Quests -- quest rework P2: the event option table's named fields (text_table, interrupt_text, flags, canSkip,
// isHidden, cs_option), and the reply arguments the engine would drop, on the builder, the fake and the validator. The
// bridge's side (each pushes the Lua value as Lua would) is in XI.Verify.QuestScripts.Live (PlatformP2.cs).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;

internal static partial class Program
{
    static void RunPlatformP2()
    {
        Console.WriteLine("== quest rework P2: named event option fields, dropped reply args ==");
        RunP2EventFields();
        RunP2DroppedArgs();
        RunP2ItemParams();
        RunP2Describe();
    }

    // 2 (d). the fake's Describe shows the option table the bridge sends (options, strings, named fields), so a fake-based
    // test tells apart replies that differ only there; a reply without one is described as before
    static void RunP2Describe()
    {
        string D(QuestReply r) => QuestContextFake.Describe(r);
        Check("p2 describe:options and strings show as the option table", "Event(113,{[0]=1,strings={[0]='Qgolden',[3]='x'}})",
              D(QuestReply.Event(113).Option(0, 1).OptionString(3, "x").OptionString(0, "Qgolden")));
        Check("p2 describe:replies that differ only in options / strings are told apart", "Event(9998)|Event(9998,{[2]=7})|Event(9998,{strings={[0]='a'}})|Event(9998,{strings={[0]='b'}})",
              string.Join("|", D(QuestReply.Event(9998)), D(QuestReply.Event(9998).Option(2, 7)), D(QuestReply.Event(9998).OptionString(0, "a")), D(QuestReply.Event(9998).OptionString(0, "b"))));
        Check("p2 describe:the positional args go into the table at 0.., an option over one wins", "Event(5,{[0]=1,[1]=2,[3]=4})|Event(5,{[0]=1,[1]=9})",
              $"{D(QuestReply.Event(5, 1, 2).Option(3, 4))}|{D(QuestReply.Event(5, 1, 2).Option(1, 9))}");
        Check("p2 describe:named fields in ParseEvent's order, modifiers after", "ProgressEvent(5046,{[0]=3,text_table=0}):oncePerZone()|ProgressOptionalCutscene(69,{canSkip=true,cs_option=0})|ProgressOptionalCutscene(9,{cs_option={1,2}})",
              string.Join("|", D(QuestReply.ProgressEvent(5046).Option(0, 3).TextTable(0).OncePerZone()), D(QuestReply.ProgressOptionalCutscene(69).CsOption(0).CanSkip()),
                          D(QuestReply.ProgressOptionalCutscene(9).CsOptions(1, 2))));
        var f = new Fake();
        f.RecordReply(QuestReply.Event(24).CanSkip().ReplaceDefault());
        Check("p2 describe:the fake records it", "Reply.Event(24,{canSkip=true}):replaceDefault()", string.Join(",", f.Calls));
        Check("p2 describe:a reply without a table is described as before", "Event(222):oncePerZone()|ProgressEvent(5,1)|MessageName(A,0,0,0,0,true,false)|Event(5,1,true)",
              string.Join("|", D(QuestReply.Event(222).OncePerZone()), D(QuestReply.ProgressEvent(5, 1)), D(QuestReply.MessageName("A", 0, 0, 0, 0, true, false)), D(QuestReply.Event(5, 1, true))));
    }

    // 2 (c). reward.itemParams: npcUtil.completeQuest passes them to npcUtil.giveItem only with an item, so a reward with
    // none is refused; an itemParams with no field set is pushed as nothing (QuestScripts.Live checks the table)
    static void RunP2ItemParams()
    {
        string V(QuestReward r) => string.Join(" | ", QuestScriptValidator.Validate(new FixedRewardQuest(r).Compile()));
        Check("p2 itemParams:a reward with itemParams and no item is refused",
              "reward.itemParams with no item: npcUtil.completeQuest passes them to npcUtil.giveItem only with reward.item / reward.items",
              V(new QuestReward { Gil = 200, ItemParams = new QuestItemParams { FromTrade = true } }));
        Check("p2 itemParams:  an empty items list is no item", "True",
              V(new QuestReward { Items = new int[0], ItemParams = new QuestItemParams { Silent = true } }).StartsWith("reward.itemParams with no item").ToString());
        Check("p2 itemParams:with an item, with items, an empty itemParams with an item, none at all: clean", "///",
              $"{V(new QuestReward { Item = 16667, ItemParams = new QuestItemParams { FromTrade = true } })}/" +
              $"{V(new QuestReward { Items = new[] { 1, 2 }, ItemParams = new QuestItemParams { Multiple = true } })}/" +
              $"{V(new QuestReward { Item = 1, ItemParams = new QuestItemParams() })}/{V(new QuestReward { Gil = 5 })}");
    }

    static string P2Refused(Func<object> f)
    {
        try { f(); return "accepted"; }
        catch (Exception e) { return e.GetType().Name; }
    }

    // 1. named fields: quest:progressEvent(5046, { [0] = zone, text_table = 0 }) (Promotion: Corporal),
    // quest:event(24, { canSkip = true }):replaceDefault() (Crimson Orb), mission:progressOptionalCutscene(69,
    // { cs_option = 0, canSkip = true }) (CoP 5-1), player:startEvent(7, { flags = f, isHidden = true }) (new character)
    static void RunP2EventFields()
    {
        static string Fields(QuestReply r) => string.Join(",", r.EventFields.Select(f => $"{f}:{f.Kind}"));
        Check("p2 fields:TextTable is text_table, a number", "text_table=0:Number", Fields(QuestReply.ProgressEvent(5046).Option(0, 3).TextTable(0)));
        Check("p2 fields:CanSkip is canSkip, a boolean", "canSkip=true:Bool", Fields(QuestReply.Event(24).CanSkip()));
        Check("p2 fields:Hidden is isHidden, a boolean", "isHidden=true:Bool", Fields(QuestReply.ProgressEvent(35).Hidden()));
        Check("p2 fields:CanSkip(false) / Hidden(false) keep the false", "canSkip=false:Bool,isHidden=false:Bool",
              Fields(QuestReply.Event(1).Hidden(false).CanSkip(false)));
        Check("p2 fields:Flags is flags, a uint32 (bit 31 stays positive)", "flags=2147483648:Number", Fields(QuestReply.Event(7).Flags(0x80000000u)));
        Check("p2 fields:InterruptText is interrupt_text", "interrupt_text=-2:Number", Fields(QuestReply.Event(7).InterruptText(-2)));
        Check("p2 fields:CsOption is cs_option, a number", "canSkip=true:Bool,cs_option=0:Number",
              Fields(QuestReply.ProgressOptionalCutscene(69).CsOption(0).CanSkip()));
        Check("p2 fields:CsOptions is cs_option's table form", "cs_option={1,2}:List", Fields(QuestReply.ProgressOptionalCutscene(69).CsOptions(1, 2)));
        Check("p2 fields:kept in ParseEvent's order whatever the call order", "text_table=5,interrupt_text=3,flags=4,canSkip=true,isHidden=true,cs_option=1",
              string.Join(",", QuestReply.ProgressOptionalCutscene(9).CsOption(1).Hidden().CanSkip().Flags(4).InterruptText(3).TextTable(5).EventFields));

        var r = QuestReply.Event(24).CanSkip();
        var copy = r.Option(1, 2).OptionString(0, "x").ReplaceDefault().OncePerZone();
        Check("p2 fields:Option, OptionString and the modifiers keep the fields", "canSkip=true/1=2/0=x/replaceDefault(),oncePerZone()",
              $"{string.Join(",", copy.EventFields)}/{string.Join(",", copy.Options.Select(kv => $"{kv.Key}={kv.Value}"))}/" +
              $"{string.Join(",", copy.StringOptions.Select(kv => $"{kv.Key}={kv.Value}"))}/{string.Join(",", copy.Modifiers)}");
        Check("p2 fields:a copy each time: the original keeps none", "0/1", $"{QuestReply.Event(24).EventFields.Count}/{r.EventFields.Count}");
        Check("p2 fields:a field after Option keeps the options and args", "1,2/3=4/text_table=0",
              $"{string.Join(",", QuestReply.Event(5, 1, 2).Option(3, 4).TextTable(0).Args)}/" +
              $"{string.Join(",", QuestReply.Event(5, 1, 2).Option(3, 4).TextTable(0).Options.Select(kv => $"{kv.Key}={kv.Value}"))}/" +
              $"{string.Join(",", QuestReply.Event(5, 1, 2).Option(3, 4).TextTable(0).EventFields)}");
        Check("p2 fields:every event kind takes them", "7/7",
              $"{new[] { QuestReply.Event(1), QuestReply.ProgressEvent(1), QuestReply.PriorityEvent(1), QuestReply.Cutscene(1), QuestReply.ProgressCutscene(1), QuestReply.ProgressOptionalCutscene(1), QuestReply.ReplaceEvent(1) }.Count(x => x.TextTable(0).CanSkip().EventFields.Count == 2)}/" +
              $"{new[] { QuestReply.Event(1), QuestReply.ProgressEvent(1), QuestReply.PriorityEvent(1), QuestReply.Cutscene(1), QuestReply.ProgressCutscene(1), QuestReply.ProgressOptionalCutscene(1), QuestReply.ReplaceEvent(1) }.Count(x => x.Hidden().Flags(1).InterruptText(1).EventFields.Count == 3)}");

        // refusals: not an event, twice, cs_option off an optional cutscene, out of ParseEvent's range
        Check("p2 fields:a message, a key item, a sequence, no action take none",
              "InvalidOperationException/InvalidOperationException/InvalidOperationException/InvalidOperationException/InvalidOperationException/InvalidOperationException/InvalidOperationException",
              $"{P2Refused(() => QuestReply.MessageText("A").TextTable(0))}/{P2Refused(() => QuestReply.MessageSpecial("A").CanSkip())}/" +
              $"{P2Refused(() => QuestReply.MessageName("A").Hidden())}/{P2Refused(() => QuestReply.Message(5, 2).Flags(1))}/" +
              $"{P2Refused(() => QuestReply.KeyItem(5).InterruptText(1))}/{P2Refused(() => QuestReply.Sequence(QuestReply.MessageText("A")).TextTable(0))}/" +
              $"{P2Refused(() => QuestReply.NoAction().CanSkip())}");
        Check("p2 fields:each field once", "InvalidOperationException/InvalidOperationException/InvalidOperationException/InvalidOperationException",
              $"{P2Refused(() => QuestReply.Event(1).TextTable(0).TextTable(0))}/{P2Refused(() => QuestReply.Event(1).CanSkip().CanSkip(false))}/" +
              $"{P2Refused(() => QuestReply.ProgressOptionalCutscene(1).CsOption(0).CsOptions(1))}/{P2Refused(() => QuestReply.Event(1).Hidden().Option(0, 1).Hidden())}");
        Check("p2 fields:cs_option is an optional cutscene's only (ParseEvent keeps it for OPTIONAL_CUTSCENE)",
              "InvalidOperationException/InvalidOperationException/InvalidOperationException",
              $"{P2Refused(() => QuestReply.ProgressEvent(1).CsOption(0))}/{P2Refused(() => QuestReply.Cutscene(1).CsOptions(1))}/{P2Refused(() => QuestReply.ProgressCutscene(1).CsOption(0))}");
        Check("p2 fields:text_table / interrupt_text are int16; an empty cs_option table is refused",
              "ArgumentOutOfRangeException/ArgumentOutOfRangeException/accepted/ArgumentException/ArgumentException",
              $"{P2Refused(() => QuestReply.Event(1).TextTable(32768))}/{P2Refused(() => QuestReply.Event(1).InterruptText(-32769))}/" +
              $"{P2Refused(() => QuestReply.Event(1).TextTable(-32768))}/{P2Refused(() => QuestReply.ProgressOptionalCutscene(1).CsOptions())}/" +
              $"{P2Refused(() => QuestReply.ProgressOptionalCutscene(1).CsOptions(null))}");
        var list = new[] { 1, 2 };
        var listed = QuestReply.ProgressOptionalCutscene(1).CsOptions(list);
        list[0] = 9;
        Check("p2 fields:CsOptions keeps a copy of its array", "{1,2}", listed.EventFields[0].LuaValue);
        Check("p2 fields:a fixed reply with fields validates clean", "",
              string.Join(" | ", QuestScriptValidator.Validate(new FixedReplyQuest(QuestReply.ProgressEvent(5046).Option(0, 3).TextTable(0).CanSkip()).Compile())));
        Check("p2 fields:  in an NPC call too", "",
              string.Join(" | ", QuestScriptValidator.Validate(new CalledFixedReplyQuest(QuestReply.Event(24).CanSkip().Hidden()).Compile())));
        // P8: a zone-in takes text_table and flags (the table { id, textTable, flags }); canSkip is refused by name
        Check("p2 fields:a zone-in cutscene id refuses them", "a zone-in takes text_table and flags only (the engine's OnZoneIn reads { id, textTable, flags }), not canSkip",
              QuestScriptValidator.ZoneInReplyProblem(QuestReply.Cutscene(5).TextTable(0).CanSkip()));
    }

    // 2 (a, b). a boolean or a string where the engine reads only a number (or reads nothing) is refused: on every event
    // kind (ParseEvent reads parameters as numbers only, positionally or from the option table), and per message kind
    // where its binding drops it. ProgressEvent(497, x, isLast) is the trap: Lua writes isLast and 1 or 0
    static void RunP2DroppedArgs()
    {
        static string P(QuestReply r, bool called = false) => QuestScriptValidator.ArgsProblem(r, called) ?? "null";
        Check("p2 args:a boolean on an event is refused (the isLast trap)",
              "ProgressEvent argument 2 (true) would be dropped: ParseEvent reads event parameters as numbers only; write the number (isLast and 1 or 0), a string with OptionString",
              P(QuestReply.ProgressEvent(497, 3, true)));
        Check("p2 args:a string on an event too", "True", P(QuestReply.Event(5, "abc", 5)).StartsWith("Event argument 1 ('abc') would be dropped: ParseEvent").ToString());
        var kinds = new Func<QuestArg[], QuestReply>[]
        {
            a => QuestReply.Event(1, a), a => QuestReply.ProgressEvent(1, a), a => QuestReply.PriorityEvent(1, a), a => QuestReply.Cutscene(1, a),
            a => QuestReply.ProgressCutscene(1, a), a => QuestReply.ProgressOptionalCutscene(1, a), a => QuestReply.ReplaceEvent(1, a),
        };
        Check("p2 args:every event kind refuses both, container and NPC call; numbers are fine", "7/7/7/7/0/0",
              $"{kinds.Count(k => P(k(new QuestArg[] { true })) != "null")}/{kinds.Count(k => P(k(new QuestArg[] { 1, "x" })) != "null")}/" +
              $"{kinds.Count(k => P(k(new QuestArg[] { false }), true) != "null")}/{kinds.Count(k => P(k(new QuestArg[] { "x" }), true) != "null")}/" +
              $"{kinds.Count(k => P(k(new QuestArg[] { 1, 2, 3 })) != "null")}/{kinds.Count(k => P(k(new QuestArg[] { 1, 2, 3 }), true) != "null")}");
        // (b) positional args mixed into an option table become its entries, read with get_or<uint32>
        Check("p2 args:a boolean mixed into an option table is refused",
              "Event argument 2 (true) would be dropped: the option table's [1] is read as a number only (get_or<uint32>); write the number (isLast and 1 or 0), a string with OptionString",
              P(QuestReply.Event(9, 0, true).Option(2, 1)));
        Check("p2 args:  with event strings or a named field too", "True/True",
              $"{P(QuestReply.Event(9, "x").OptionString(0, "y")).Contains("option table's [0]")}/{P(QuestReply.Event(9, 1, false).TextTable(0)).Contains("option table's [1]")}");

        // messageSpecial(id, p0..p3, showName): numbers, then a boolean, then nothing
        Check("p2 args:messageSpecial: four numbers and showName are fine", "null/null",
              $"{P(QuestReply.MessageSpecial("A", 1, 2, 3, 4, true))}/{P(QuestReply.Message(5, 2, 1, 2, 3, 4, false))}");
        Check("p2 args:  a boolean or string in p0..p3, a string as showName, a sixth argument are refused",
              "MessageSpecial argument 1 (true) would be dropped: messageSpecial reads its first four arguments as numbers only|" +
              "MessageSpecial argument 4 ('x') would be dropped: messageSpecial reads its first four arguments as numbers only|" +
              "MessageSpecial argument 5 ('x') would be dropped: messageSpecial's showName (argument 5) is a boolean only|" +
              "Message argument 6 (true) would be dropped: messageSpecial reads five arguments",
              string.Join("|", P(QuestReply.MessageSpecial("A", true)), P(QuestReply.MessageSpecial("A", 1, 2, 3, "x")),
                          P(QuestReply.MessageSpecial("A", 1, 2, 3, 4, "x")), P(QuestReply.Message(5, 2, 1, 2, 3, 4, true, true))));
        Check("p2 args:  the same in an NPC call (player:messageSpecial)", "null/True",
              $"{P(QuestReply.MessageSpecial("A", 1, 2, 3, 4, true), true)}/{P(QuestReply.MessageSpecial("A", "x"), true) != "null"}");
        // showText(npc, id, p0..p3, showName, turn, messageType): the container's messageName / Message of the name type
        Check("p2 args:messageName (showText): showName / turn take a boolean (or anything as<bool> reads)", "null/null/null",
              $"{P(QuestReply.MessageName("A", 0, 0, 0, 0, true, false))}/{P(QuestReply.Message(5, 3, 0, 0, 0, 0, false, true))}/{P(QuestReply.MessageName("A", 0, 0, 0, 0, "s", 1))}");
        Check("p2 args:  a boolean in p0..p3, as messageType, an eighth argument are refused",
              "MessageName argument 1 (true) would be dropped: showText reads its first four arguments as numbers only|" +
              "MessageName argument 7 (true) would be dropped: showText's messageType (argument 7) is a number only|" +
              "MessageName argument 8 ('x') would be dropped: showText reads seven arguments",
              string.Join("|", P(QuestReply.MessageName("A", true)), P(QuestReply.MessageName("A", 0, 0, 0, 0, true, false, true)),
                          P(QuestReply.MessageName("A", 0, 0, 0, 0, true, false, 1, "x"))));
        // Message:perform shows a text message as messageText(npc, id, { face }): no argument reaches the engine
        Check("p2 args:a text message's boolean / string arguments are refused (its action passes none)", "True/True/True/True",
              $"{P(QuestReply.MessageText("A", true)).EndsWith("passes no arguments")}/{P(QuestReply.Message(5, null, "x")).EndsWith("passes no arguments")}/" +
              $"{P(QuestReply.Message(5, 1, false)).EndsWith("passes no arguments")}/{P(QuestReply.ReplaceMessage("A", true)).EndsWith("passes no arguments")}");
        Check("p2 args:  its number arguments are left as they were", "null/null", $"{P(QuestReply.MessageText("A", 1))}/{P(QuestReply.Message(5, null, 1, 2))}");
        // the NPC call's player:messageName(id, player, p0..p3, chat, sender, showSender)
        Check("p2 args:an NPC call's messageName: the container's (..., true, false) is refused (the chat type)",
              "MessageName argument 5 (true) would be dropped: messageName's chat type (argument 5) is a number only",
              P(QuestReply.MessageName("A", 0, 0, 0, 0, true, false), true));
        Check("p2 args:  numbers and chat are fine; a sender (argument 6) is refused, a string or a boolean", "null/True/True",
              $"{P(QuestReply.MessageName("A", 1, 2, 3, 4, 5), true)}/{P(QuestReply.MessageName("A", 1, 2, 3, 4, 5, "x"), true).Contains("sender entity")}/" +
              $"{P(QuestReply.MessageName("A", 1, 2, 3, 4, 5, false), true).Contains("sender entity")}");
        Check("p2 args:  a number as the sender is refused too (C# drops it, C++ reads null userdata)",
              "MessageName argument 6 (0) would be dropped: messageName's argument 6 is the sender entity, which no reply argument can be",
              P(QuestReply.MessageName("A", 1, 2, 3, 4, 5, 0, true), true));
        Check("p2 args:  the container's messageName (showText) keeps a number at argument 6 (turn)", "null",
              P(QuestReply.MessageName("A", 1, 2, 3, 4, 5, 0)));
        Check("p2 args:  the validator refuses a fixed called MessageName with a number sender", "True",
              QuestScriptValidator.Validate(new CalledFixedReplyQuest(QuestReply.MessageName("A", 0, 0, 0, 0, 4, 7)).Compile())
                  .Any(p => p.Contains("MessageName argument 6 (7) would be dropped: messageName's argument 6 is the sender entity")).ToString());
        // the NPC call's player:messageText(npc, id, arg2, arg3): showName (a boolean) or the mode, then the mode
        Check("p2 args:an NPC call's messageText: a boolean or a number first, a number second", "null/null/null",
              $"{P(QuestReply.MessageText("A", true), true)}/{P(QuestReply.MessageText("A", 1, 2), true)}/{P(QuestReply.MessageText("A", false, 2), true)}");
        Check("p2 args:  a string first, a boolean second, a third argument are refused", "True/True/True",
              $"{P(QuestReply.MessageText("A", "x"), true).Contains("showName (a boolean) or the mode")}/{P(QuestReply.MessageText("A", 1, true), true).Contains("mode (argument 2)")}/" +
              $"{P(QuestReply.MessageText("A", 1, 2, true), true).Contains("reads two arguments")}");
        Check("p2 args:a key item, a sequence, no action have no arguments", "null/null/null",
              $"{P(QuestReply.KeyItem(1))}/{P(QuestReply.Sequence(QuestReply.MessageText("A")))}/{P(QuestReply.NoAction())}");

        // the validator, on a fixed reply: the container's and the NPC call's rules
        Check("p2 args:the validator refuses a fixed event reply with a boolean",
              "X OnTrigger: ProgressEvent argument 2 (true) would be dropped: ParseEvent reads event parameters as numbers only; write the number (isLast and 1 or 0), a string with OptionString",
              string.Join(" | ", QuestScriptValidator.Validate(new FixedReplyQuest(QuestReply.ProgressEvent(497, 3, true)).Compile())));
        Check("p2 args:  an NPC call's messageName(..., true, false)", "X OnTrigger: MessageName argument 5 (true) would be dropped: messageName's chat type (argument 5) is a number only",
              string.Join(" | ", QuestScriptValidator.Validate(new CalledFixedReplyQuest(QuestReply.MessageName("A", 0, 0, 0, 0, true, false)).Compile())));
        Check("p2 args:  and keeps the container's messageName(..., true, false) clean", "",
              string.Join(" | ", QuestScriptValidator.Validate(new FixedReplyQuest(QuestReply.MessageName("A", 0, 0, 0, 0, true, false)).Compile())));
    }
}
