// XI.Verify.Quests -- quest rework P1: the platform needs drafting found (typed reply args, string event params, the
// string event option, a sequence text step with a face, reward itemParams), on the builder, the fake and the validator.
// The bridge's side (each pushes the Lua value as Lua would) is in XI.Verify.QuestScripts.Live (PlatformP1.cs).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;

internal static partial class Program
{
    static void RunPlatformP1()
    {
        Console.WriteLine("== quest rework P1: platform needs ==");
        RunP1TypedArgs();
        RunP1StringParams();
        RunP1OptionText();
        RunP1SequenceFace();
        RunP1RewardItemParams();
    }

    // 5. reward itemParams: npcUtil.completeQuest(..., { item = X, itemParams = { fromTrade = true } })
    static void RunP1RewardItemParams()
    {
        var d = new FixedRewardQuest(new QuestReward { Item = 16667, ItemParams = new QuestItemParams { FromTrade = true } }).Compile();
        Check("p1 itemParams:the reward keeps them, only the fields set", "True/null/null",
              $"{d.Reward.ItemParams.FromTrade}/{d.Reward.ItemParams.Silent?.ToString() ?? "null"}/{d.Reward.ItemParams.Multiple?.ToString() ?? "null"}");
        var m = new MutatingItemParamsQuest().Compile();
        Check("p1 itemParams:a copy: changing the object after Reward() changes nothing", "True/null", $"{m.Reward.ItemParams.FromTrade}/{m.Reward.ItemParams.Silent?.ToString() ?? "null"}");
        Check("p1 itemParams:none by default", "True", (new FixedRewardQuest(new QuestReward { Item = 1 }).Compile().Reward.ItemParams == null).ToString());
    }

    // declares a reward with itemParams, then changes the object it passed
    sealed class MutatingItemParamsQuest : QuestScript
    {
        public MutatingItemParamsQuest() : base(QuestLog.Bastok, "MUTATING") { }
        protected override void Build(QuestScriptBuilder b)
        {
            var r = new QuestReward { Item = 1, ItemParams = new QuestItemParams { FromTrade = true } };
            b.Reward(r);
            r.ItemParams.FromTrade = false;
            r.ItemParams.Silent = true;
        }
    }

    // 4. a sequence text step with a face: quest:sequence({ text = X, wait = 1000 }, { text = X + 1, face = 82, wait = 2000 },
    // { face = 115 }) is Sequence(MessageText(X), Wait(1000), MessageText(X + 1).WithFace(82), Wait(2000), Face(115))
    static void RunP1SequenceFace()
    {
        var seq = QuestReply.Sequence(QuestReply.MessageText("A"), QuestReply.Wait(1000), QuestReply.MessageText("B").WithFace(82),
                                      QuestReply.Wait(2000), QuestReply.Face(115));
        Check("p1 face:the step carries its face, the others none", "-,-,82,-,-",
              string.Join(",", seq.Steps.Select(s => s.TextFace?.ToString() ?? "-")));
        Check("p1 face:the fake describes it", "Sequence(MessageText(A),Wait(1000),MessageText(B,face=82),Wait(2000),Face(115))",
              QuestContextFake.Describe(seq));
        Check("p1 face:a sequence with a faced text step validates clean", "",
              string.Join(" | ", QuestScriptValidator.Validate(new FixedReplyQuest(seq).Compile())));
        Check("p1 face:Message of the text type takes one too", "7", QuestReply.Message(5, 1).WithFace(7).TextFace.ToString());
        string Refused(Func<object> f)
        {
            try { f(); return "accepted"; }
            catch (Exception e) { return e.GetType().Name; }
        }
        Check("p1 face:an event, a special message, a name message take none", "InvalidOperationException/InvalidOperationException/InvalidOperationException",
              $"{Refused(() => QuestReply.Event(1).WithFace(1))}/{Refused(() => QuestReply.MessageSpecial("A").WithFace(1))}/{Refused(() => QuestReply.Message(5, 3).WithFace(1))}");
        Check("p1 face:a faced text outside a sequence is refused", "X OnTrigger: a text's face (WithFace) is a sequence step only",
              string.Join(" | ", QuestScriptValidator.Validate(new FixedReplyQuest(QuestReply.MessageText("A").WithFace(82)).Compile())));
        Check("p1 face:  and in an NPC call", "True",
              QuestScriptValidator.Validate(new CalledFixedReplyQuest(QuestReply.MessageText("A").WithFace(82)).Compile())
                  .Any(p => p.Contains("a text's face (WithFace) is a sequence step only")).ToString());
    }

    // 3. the string event option: onEventUpdate's option is a string after a typed input (type(option) == 'string');
    // OptionText carries it, Option stays an int (0 for a string, as the bridge always read it)
    static void RunP1OptionText()
    {
        var def = new OptionTextQuest().Compile();
        var update = def.States[0].Handlers.Single(h => h.Kind == QuestHandlerKind.OnEventUpdate);
        var f = new Fake { Csid = 199, OptionText = "Chululu" };
        update.Run(f);
        f.OptionText = null;
        f.Option = 3;
        update.Run(f);
        Check("p1 option text:a handler reads OptionText, else Option", "Player.SetCharVar(T,7,0),Player.SetCharVar(N,3,0)", string.Join(",", f.Calls));
        Check("p1 option text:the fake's OptionText is null by default", "True", (new Fake().OptionText == null).ToString());
    }

    sealed class OptionTextQuest : QuestScript
    {
        public OptionTextQuest() : base(QuestLog.Bastok, "X") { }
        protected override void Build(QuestScriptBuilder b) =>
            b.AnyState().OnEventUpdate(235, 199, q =>
            {
                if (q.OptionText != null) q.Player.SetCharVar("T", q.OptionText.Length);
                else q.Player.SetCharVar("N", q.Option);
            });
    }

    // 2. string event params: quest:event(113, { [0] = n, ['strings'] = { [0] = name } })
    static void RunP1StringParams()
    {
        var r = QuestReply.Event(113).Option(0, 1).OptionString(0, "Qgolden");
        Check("p1 strings:OptionString reaches the reply, Option beside it", "0=Qgolden/0=1",
              $"{string.Join(",", r.StringOptions.Select(kv => $"{kv.Key}={kv.Value}"))}/{string.Join(",", r.Options.Select(kv => $"{kv.Key}={kv.Value}"))}");
        var two = r.OptionString(3, "x").Option(2, 5).OncePerZone();
        Check("p1 strings:a copy each time; Option and the modifiers keep the strings", "1/0=Qgolden,3=x/oncePerZone()",
              $"{r.StringOptions.Count}/{string.Join(",", two.StringOptions.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"))}/{string.Join(",", two.Modifiers)}");
        string Refused(Func<object> f)
        {
            try { f(); return "accepted"; }
            catch (Exception e) { return e.GetType().Name; }
        }
        Check("p1 strings:events only, index 0..3, no null", "InvalidOperationException/ArgumentOutOfRangeException/ArgumentOutOfRangeException/ArgumentNullException",
              $"{Refused(() => QuestReply.MessageText("A").OptionString(0, "x"))}/{Refused(() => QuestReply.Event(1).OptionString(4, "x"))}/" +
              $"{Refused(() => QuestReply.Event(1).OptionString(-1, "x"))}/{Refused(() => QuestReply.Event(1).OptionString(0, null))}");
        Check("p1 strings:a reply with event strings validates clean", "", string.Join(" | ", QuestScriptValidator.Validate(new FixedReplyQuest(r).Compile())));
        Check("p1 strings:a zone-in cutscene id refuses them", "a cutscene id takes no arguments here (1 given)",
              QuestScriptValidator.ZoneInReplyProblem(QuestReply.Cutscene(5).OptionString(0, "x")));
    }

    // 1. typed reply args: quest:messageName(id, 0, 0, 0, 0, true, false) (showName, turn: 0 is truthy in Lua)
    static void RunP1TypedArgs()
    {
        var r = QuestReply.MessageName("MERTAIRE_MALLIEBELL_LEFT", 0, 0, 0, 0, true, false);
        Check("p1 args:messageName carries the booleans as booleans", "Number,Number,Number,Number,Bool,Bool",
              string.Join(",", r.Args.Select(a => a.Kind)));
        Check("p1 args:written as Lua writes them", "0,0,0,0,true,false", string.Join(",", r.Args));
        Check("p1 args:the fake describes the reply as the Lua call", "MessageName(MERTAIRE_MALLIEBELL_LEFT,0,0,0,0,true,false)",
              QuestContextFake.Describe(r));
        var ints = QuestReply.Event(5, 1, 2);
        Check("p1 args:int arguments stay numbers, with their values", "Number:1,Number:2",
              string.Join(",", ints.Args.Select(a => $"{a.Kind}:{a.Number}")));
        var str = QuestReply.Event(5, "it's").Args[0];
        Check("p1 args:a string argument, written as a Lua string", "String:it's:'it\\'s'", $"{str.Kind}:{str.String}:{str}");
        Check("p1 args:Message keeps its messageType apart from the args", "1/true", $"{QuestReply.Message(7, 1, true).MessageType}/{QuestReply.Message(7, 1, true).Args[0]}");
        Check("p1 args:the kinds compare by value", "True/False/False",
              $"{((QuestArg)1).Equals((QuestArg)1)}/{((QuestArg)1).Equals((QuestArg)true)}/{((QuestArg)0).Equals((QuestArg)false)}");
        string Throws(System.Func<object> f)
        {
            try { f(); return "no"; }
            catch (System.InvalidOperationException) { return "InvalidOperation"; }
            catch (System.ArgumentNullException) { return "ArgumentNull"; }
        }
        Check("p1 args:reading another kind's value throws", "InvalidOperation/InvalidOperation",
              $"{Throws(() => ((QuestArg)true).Number)}/{Throws(() => ((QuestArg)1).Bool)}");
        Check("p1 args:a null string is refused (nil would end the arguments)", "ArgumentNull", Throws(() => QuestReply.Event(5, (string)null)));
        Check("p1 args:a reply with boolean args validates clean", "",
              string.Join(" | ", QuestScriptValidator.Validate(new FixedReplyQuest(r).Compile())));
        Check("p1 args:Option and the modifiers keep the typed args", "0,true/oncePerZone()",
              $"{string.Join(",", QuestReply.Event(9, 0, true).Option(2, 1).OncePerZone().Args)}/{string.Join(",", QuestReply.Event(9, 0, true).OncePerZone().Modifiers)}");
    }
}
