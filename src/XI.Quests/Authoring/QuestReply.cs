// XI.Quests -- the action a quest handler returns (quest rework B2).
using System;
using System.Collections.Generic;
using System.Linq;

namespace XI.Quests.Authoring
{
    public enum QuestReplyKind : byte
    {
        Event, ProgressEvent, PriorityEvent, Cutscene, ProgressCutscene, ProgressOptionalCutscene, ReplaceEvent,
        Message, MessageSpecial, MessageText, MessageName, ReplaceMessage, KeyItem, Sequence, NoAction,
        // sequence steps only: { face = n } / { wait = ms }
        Face, Wait,
    }

    // a modifier the Lua action takes after its helper (scripts/globals/interaction/actions/action.lua), called on the
    // action as a method in the order given: quest:event(222):oncePerZone()
    public enum QuestReplyModifierKind : byte
    {
        ReplaceDefault, OncePerZone, ImportantEvent, ImportantOnce, Progress, SetPriority, OpenDoor,
    }

    public readonly struct QuestReplyModifier
    {
        public QuestReplyModifier(QuestReplyModifierKind kind, int? arg = null)
        {
            Kind = kind;
            Arg = arg;
        }

        public QuestReplyModifierKind Kind { get; }
        // SetPriority's priority; null for the others
        public int? Arg { get; }

        // the Action method it calls
        public string LuaName => Kind switch
        {
            QuestReplyModifierKind.ReplaceDefault => "replaceDefault",
            QuestReplyModifierKind.OncePerZone => "oncePerZone",
            QuestReplyModifierKind.ImportantEvent => "importantEvent",
            QuestReplyModifierKind.ImportantOnce => "importantOnce",
            QuestReplyModifierKind.Progress => "progress",
            QuestReplyModifierKind.SetPriority => "setPriority",
            QuestReplyModifierKind.OpenDoor => "openDoor",
            _ => throw new InvalidOperationException($"unknown reply modifier {(int)Kind}"),
        };

        // the call as Lua writes it: oncePerZone(), setPriority(20)
        public override string ToString() => Arg is int a ? $"{LuaName}({a})" : $"{LuaName}()";
    }

    // the action a handler returns: one of the Lua Container helpers with its arguments. A text argument is a
    // zones[zone].text name: Text (a zone and a name) is resolved in its own zone, TextName in the handler's zone, by the
    // bridge; Args are the helper's remaining arguments (QuestArg: Lua numbers, booleans, strings); Options are an event's option table
    // ({ [index] = value }, the helper's last argument)
    public sealed class QuestReply
    {
        static readonly QuestArg[] NoArgs = new QuestArg[0];
        static readonly QuestReply[] NoSteps = new QuestReply[0];

        static readonly IReadOnlyDictionary<int, int> NoOptions = new Dictionary<int, int>();
        static readonly IReadOnlyDictionary<int, string> NoStringOptions = new Dictionary<int, string>();
        static readonly QuestReplyModifier[] NoModifiers = new QuestReplyModifier[0];
        static readonly QuestEventField[] NoFields = new QuestEventField[0];

        QuestReply(QuestReplyKind kind, int id, string textName, int? messageType, QuestArg[] args, QuestReply[] steps,
                   ZoneText? text = null, IReadOnlyDictionary<int, int> options = null, QuestReplyModifier[] modifiers = null,
                   IReadOnlyDictionary<int, string> stringOptions = null, int? textFace = null, QuestEventField[] eventFields = null)
        {
            EventFields = eventFields ?? NoFields;
            StringOptions = stringOptions ?? NoStringOptions;
            TextFace = textFace;
            Modifiers = modifiers ?? NoModifiers;
            Kind = kind;
            Id = id;
            TextName = textName;
            Text = text;
            Options = options ?? NoOptions;
            MessageType = messageType;
            Args = args ?? NoArgs;
            Steps = steps ?? NoSteps;
        }

        public QuestReplyKind Kind { get; }
        // the event id / message id / key item; unused for a named text, Sequence, NoAction
        public int Id { get; }
        // MessageSpecial / MessageText / MessageName by name; null when Id is the message (MessageSpecial / MessageName by id)
        public string TextName { get; }
        // MessageSpecial / MessageText / MessageName / ReplaceMessage by zone and name; null when TextName or Id is the message
        public ZoneText? Text { get; }
        // Message's optional messageType
        public int? MessageType { get; }
        // the helper's remaining arguments: numbers, booleans and strings, pushed as Lua pushes them (QuestArg)
        public IReadOnlyList<QuestArg> Args { get; }
        // an event's option table, { [index] = value }
        public IReadOnlyDictionary<int, int> Options { get; }
        // the option table's strings sub-table, { ['strings'] = { [index] = text } } (ParseEvent's event strings)
        public IReadOnlyDictionary<int, string> StringOptions { get; }
        // Sequence
        public IReadOnlyList<QuestReply> Steps { get; }
        // a sequence text step's face, { text = id, face = angle }: the NPC turns to the angle as the text is shown
        // (Sequence:new makes it one Message with face: player:messageText(npc, id, { face = angle })); null for none
        public int? TextFace { get; }
        // the Action modifiers called on the helper's action, in order (quest:event(222):oncePerZone())
        public IReadOnlyList<QuestReplyModifier> Modifiers { get; }
        // the option table's named fields (text_table = 0, canSkip = true, ...), in ParseEvent's order; events only
        public IReadOnlyList<QuestEventField> EventFields { get; }

        public static QuestReply Event(int csid, params QuestArg[] args) => Make(QuestReplyKind.Event, csid, args);
        public static QuestReply ProgressEvent(int csid, params QuestArg[] args) => Make(QuestReplyKind.ProgressEvent, csid, args);
        public static QuestReply PriorityEvent(int csid, params QuestArg[] args) => Make(QuestReplyKind.PriorityEvent, csid, args);
        public static QuestReply Cutscene(int csid, params QuestArg[] args) => Make(QuestReplyKind.Cutscene, csid, args);
        public static QuestReply ProgressCutscene(int csid, params QuestArg[] args) => Make(QuestReplyKind.ProgressCutscene, csid, args);
        public static QuestReply ProgressOptionalCutscene(int csid, params QuestArg[] args) => Make(QuestReplyKind.ProgressOptionalCutscene, csid, args);
        public static QuestReply ReplaceEvent(int csid, params QuestArg[] args) => Make(QuestReplyKind.ReplaceEvent, csid, args);

        public static QuestReply Message(int messageId, int? messageType = null, params QuestArg[] args) =>
            new QuestReply(QuestReplyKind.Message, messageId, null, messageType, Copy(args), null);

        public static QuestReply MessageSpecial(string textName, params QuestArg[] args) => Named(QuestReplyKind.MessageSpecial, textName, args);
        public static QuestReply MessageText(string textName, params QuestArg[] args) => Named(QuestReplyKind.MessageText, textName, args);
        public static QuestReply MessageName(string textName, params QuestArg[] args) => Named(QuestReplyKind.MessageName, textName, args);
        public static QuestReply ReplaceMessage(string textName, params QuestArg[] args) => Named(QuestReplyKind.ReplaceMessage, textName, args);

        public static QuestReply MessageSpecial(ZoneText text, params QuestArg[] args) => Named(QuestReplyKind.MessageSpecial, text, args);
        public static QuestReply MessageText(ZoneText text, params QuestArg[] args) => Named(QuestReplyKind.MessageText, text, args);
        public static QuestReply MessageName(ZoneText text, params QuestArg[] args) => Named(QuestReplyKind.MessageName, text, args);
        public static QuestReply ReplaceMessage(ZoneText text, params QuestArg[] args) => Named(QuestReplyKind.ReplaceMessage, text, args);

        // by text id (quest rework P5): quest:messageSpecial(ID.text.X + n, ...) / quest:messageName(ID.text.X + n, ...),
        // a text name plus an offset, which no ZoneText names: the id read with q.Text.Id(text), plus the offset
        // (QuestReply.MessageSpecial(q.Text.Id(Text.SauromugueChampaign.THF_AF_WALL_OFFSET) + 3, 0, Item.Grapnel)). The
        // bridge sends the id as it is, container and NPC call alike
        public static QuestReply MessageSpecial(int textId, params QuestArg[] args) => Make(QuestReplyKind.MessageSpecial, textId, args);
        public static QuestReply MessageName(int textId, params QuestArg[] args) => Make(QuestReplyKind.MessageName, textId, args);
        // mission:messageText(ID.text.X + n) (quest rework C2, San d'Oria 1-3's ORIGINAL_MISSION_OFFSET + 24), by id as
        // MessageSpecial's
        public static QuestReply MessageText(int textId, params QuestArg[] args) =>
            Make(QuestReplyKind.MessageText, textId, args);

        // sequence steps only: the NPC turns to the angle, or the sequence waits
        public static QuestReply Face(int npcFacing) => new QuestReply(QuestReplyKind.Face, npcFacing, null, null, null, null);
        public static QuestReply Wait(int ms) => new QuestReply(QuestReplyKind.Wait, ms, null, null, null, null);

        // a copy with the event's option [index] = value (the Lua quests' { [2] = x } table); events only
        public QuestReply Option(int index, int value)
        {
            if (!IsEvent(Kind)) throw new InvalidOperationException($"{Kind} takes no options (events only)");
            // the indexes ParseEvent reads
            if (index < 0 || index > 7) throw new ArgumentOutOfRangeException(nameof(index), index, "an event option index is 0..7");
            var options = new Dictionary<int, int>(Options) { [index] = value };
            return new QuestReply(Kind, Id, TextName, MessageType, Copy((QuestArg[])Args), null, Text, options, (QuestReplyModifier[])Modifiers, StringOptions, TextFace,
                                  (QuestEventField[])EventFields);
        }

        // a copy with the helper's argument [index] (0-based, after the id) replaced: a module's wrap that changes the action
        // its base returned, as Lua writes action.options[index + 1] = value (The Moonlit Path's era module sets a bit of
        // event 846's fifth parameter, action.options[5], which is WithArg(4, ...)). The argument must be there
        public QuestReply WithArg(int index, QuestArg value)
        {
            if (index < 0 || index >= Args.Count)
                throw new ArgumentOutOfRangeException(nameof(index), index, $"the reply has {Args.Count} arguments");
            var args = Copy((QuestArg[])Args);
            args[index] = value;
            return new QuestReply(Kind, Id, TextName, MessageType, args, (QuestReply[])Steps, Text, Options, (QuestReplyModifier[])Modifiers,
                                  StringOptions, TextFace, (QuestEventField[])EventFields);
        }

        // a copy of a text step that turns the NPC to the angle as it is shown: the sequence entry { text = id, face = angle }
        // (quest:sequence({ text = X, wait = 1000 }, { text = X + 1, face = 82, wait = 2000 }, { face = 115 }) is
        // Sequence(MessageText(X), Wait(1000), MessageText(X + 1).WithFace(82), Wait(2000), Face(115))). A text message only
        // (MessageText, or Message of the text type), and a sequence step only: the validator refuses it elsewhere
        public QuestReply WithFace(int angle)
        {
            if (!(Kind == QuestReplyKind.MessageText || Kind == QuestReplyKind.Message && (MessageType == null || MessageType == 1)))
                throw new InvalidOperationException($"{Kind} takes no face (a sequence's text step only)");
            return new QuestReply(Kind, Id, TextName, MessageType, Copy((QuestArg[])Args), (QuestReply[])Steps, Text, Options, (QuestReplyModifier[])Modifiers,
                                  StringOptions, angle, (QuestEventField[])EventFields);
        }

        // a copy with the event's string [index] = text, in the option table's strings sub-table (the Lua quests'
        // quest:event(113, { [0] = n, ['strings'] = { [0] = name } })); events only
        public QuestReply OptionString(int index, string text)
        {
            if (!IsEvent(Kind)) throw new InvalidOperationException($"{Kind} takes no options (events only)");
            // the strings the event packet (GP_SERV_COMMAND_EVENTSTR) carries
            if (index < 0 || index > 3) throw new ArgumentOutOfRangeException(nameof(index), index, "an event string index is 0..3");
            if (text == null) throw new ArgumentNullException(nameof(text), "an event string cannot be null");
            var strings = new Dictionary<int, string>(StringOptions) { [index] = text };
            return new QuestReply(Kind, Id, TextName, MessageType, Copy((QuestArg[])Args), null, Text, Options, (QuestReplyModifier[])Modifiers, strings, TextFace,
                                  (QuestEventField[])EventFields);
        }

        // ---- the option table's named fields (ParseEvent: lua_base_entity.cpp:1337-1358): each a copy with the field set;
        // events only, each field once. quest:progressEvent(5046, { [0] = zone, text_table = 0 }) is
        // QuestReply.ProgressEvent(5046).Option(0, zone).TextTable(0) ----

        // text_table = n: the event's text table (ParseEvent's default -1); int16
        public QuestReply TextTable(int table) => WithField("text_table", QuestEventFieldKind.Number, Int16(table, nameof(table)));
        // interrupt_text = n: the text shown when the event is interrupted (default 0); int16
        public QuestReply InterruptText(int text) => WithField("interrupt_text", QuestEventFieldKind.Number, Int16(text, nameof(text)));
        // flags = n: the event's cutscene flags (xi.cutsceneFlag, default 0); uint32
        public QuestReply Flags(uint flags) => WithField("flags", QuestEventFieldKind.Number, flags);
        // canSkip = true: the player may skip the cutscene (a Lua boolean; ParseEvent reads nothing else, default false)
        public QuestReply CanSkip(bool canSkip = true) => WithField("canSkip", QuestEventFieldKind.Bool, 0, canSkip);
        // isHidden = true: the event is hidden (a Lua boolean, default false)
        public QuestReply Hidden(bool hidden = true) => WithField("isHidden", QuestEventFieldKind.Bool, 0, hidden);
        // cs_option = n: the option that starts an optional cutscene's warp (in place of the default 0). ParseEvent keeps it
        // for an optional cutscene only, so it is ProgressOptionalCutscene's alone
        public QuestReply CsOption(int option) => WithField("cs_option", QuestEventFieldKind.Number, option);
        // cs_option = { a, b }: options added to the default 0 (ParseEvent's table form); ProgressOptionalCutscene's alone
        public QuestReply CsOptions(params int[] options)
        {
            if (options == null || options.Length == 0) throw new ArgumentException("cs_option's table form needs at least one option", nameof(options));
            return WithField("cs_option", QuestEventFieldKind.List, 0, false, (int[])options.Clone());
        }

        static int Int16(int value, string name) =>
            value is >= short.MinValue and <= short.MaxValue ? value
            : throw new ArgumentOutOfRangeException(name, value, "ParseEvent reads it as an int16");

        QuestReply WithField(string name, QuestEventFieldKind kind, long number, bool b = false, int[] list = null)
        {
            if (!IsEvent(Kind)) throw new InvalidOperationException($"{Kind} takes no {name} (events only)");
            if (name == "cs_option" && Kind != QuestReplyKind.ProgressOptionalCutscene)
                throw new InvalidOperationException($"{Kind} takes no cs_option: ParseEvent keeps it for an optional cutscene only (ProgressOptionalCutscene)");
            if (EventFields.Any(f => f.Name == name)) throw new InvalidOperationException($"{name} is set already");
            var fields = EventFields.Append(new QuestEventField(name, kind, number, b, list))
                                    .OrderBy(f => Array.IndexOf(QuestEventField.Order, f.Name)).ToArray();
            return new QuestReply(Kind, Id, TextName, MessageType, Copy((QuestArg[])Args), null, Text, Options, (QuestReplyModifier[])Modifiers, StringOptions, TextFace,
                                  fields);
        }

        // ---- the Action modifiers (action.lua): each a copy with the modifier added after the ones it has ----

        // Action:replaceDefault(): priority ReplaceDefault (20), preferred over the NPC script's own default
        public QuestReply ReplaceDefault() => With(new QuestReplyModifier(QuestReplyModifierKind.ReplaceDefault));
        // Action:oncePerZone(): after it is performed, priority Ignore until the char zones
        public QuestReply OncePerZone() => With(new QuestReplyModifier(QuestReplyModifierKind.OncePerZone));
        // Action:importantEvent(): priority Progress, then Event once performed
        public QuestReply ImportantEvent() => With(new QuestReplyModifier(QuestReplyModifierKind.ImportantEvent));
        // Action:importantOnce(): priority Event, then Default once performed
        public QuestReply ImportantOnce() => With(new QuestReplyModifier(QuestReplyModifierKind.ImportantOnce));
        // Action:progress(): priority Progress (1000)
        public QuestReply Progress() => With(new QuestReplyModifier(QuestReplyModifierKind.Progress));
        // Action:setPriority(priority): Action.Priority (Ignore 1, Default 10, ReplaceDefault 20, Message 50, Event 100,
        // Progress 1000) or any number
        public QuestReply SetPriority(int priority) => With(new QuestReplyModifier(QuestReplyModifierKind.SetPriority, priority));
        // Action:openDoor() (and its alias Action:open()): the action returns -1, which opens a door
        public QuestReply OpenDoor() => With(new QuestReplyModifier(QuestReplyModifierKind.OpenDoor));

        QuestReply With(QuestReplyModifier m)
        {
            var modifiers = new QuestReplyModifier[Modifiers.Count + 1];
            for (int i = 0; i < Modifiers.Count; i++) modifiers[i] = Modifiers[i];
            modifiers[Modifiers.Count] = m;
            return new QuestReply(Kind, Id, TextName, MessageType, Copy((QuestArg[])Args), (QuestReply[])Steps, Text, Options, modifiers, StringOptions, TextFace,
                                  (QuestEventField[])EventFields);
        }

        // the kinds that start an event (the ones that take arguments and options)
        public static bool IsEvent(QuestReplyKind k) => k is QuestReplyKind.Event or QuestReplyKind.ProgressEvent or QuestReplyKind.PriorityEvent or
            QuestReplyKind.Cutscene or QuestReplyKind.ProgressCutscene or QuestReplyKind.ProgressOptionalCutscene or QuestReplyKind.ReplaceEvent;

        public static QuestReply KeyItem(int keyItem) => Make(QuestReplyKind.KeyItem, keyItem, null);

        public static QuestReply Sequence(params QuestReply[] steps) =>
            new QuestReply(QuestReplyKind.Sequence, 0, null, null, null, steps == null ? null : (QuestReply[])steps.Clone());

        public static QuestReply NoAction() => new QuestReply(QuestReplyKind.NoAction, 0, null, null, null, null);

        static QuestReply Make(QuestReplyKind kind, int id, QuestArg[] args) =>
            new QuestReply(kind, id, null, null, Copy(args), null);

        static QuestReply Named(QuestReplyKind kind, string textName, QuestArg[] args) =>
            new QuestReply(kind, 0, textName, null, Copy(args), null);

        static QuestReply Named(QuestReplyKind kind, ZoneText text, QuestArg[] args) =>
            new QuestReply(kind, 0, null, null, Copy(args), null, text);

        static QuestArg[] Copy(QuestArg[] args) => args == null || args.Length == 0 ? NoArgs : (QuestArg[])args.Clone();
    }
}
