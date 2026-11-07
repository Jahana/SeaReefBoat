// XI.Quests -- a named field of an event's option table (quest rework P2).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace XI.Quests.Authoring
{
    public enum QuestEventFieldKind : byte
    {
        Number, Bool, List,
    }

    // one named field of an event's option table, as CLuaBaseEntity::ParseEvent reads it (lua_base_entity.cpp:1337-1358;
    // C#: ZoneHost.Bindings.cs ParseEvent): text_table = 0 (int16, default -1), interrupt_text (int16), flags (uint32),
    // canSkip = true and isHidden = true (Lua booleans: get_or<bool> reads nothing else), cs_option = 0 or { 1, 2 } (an
    // optional cutscene's options: a number replaces the default { 0 }, a table's numbers are added to it)
    public readonly struct QuestEventField : IEquatable<QuestEventField>
    {
        // ParseEvent's order, the order the reply keeps them in
        internal static readonly string[] Order = { "text_table", "interrupt_text", "flags", "canSkip", "isHidden", "cs_option" };

        readonly long number_;
        readonly bool bool_;
        readonly int[] list_;

        internal QuestEventField(string name, QuestEventFieldKind kind, long number, bool b, int[] list)
        {
            Name = name;
            Kind = kind;
            number_ = number;
            bool_ = b;
            list_ = list;
        }

        // the Lua key: text_table, interrupt_text, flags, canSkip, isHidden, cs_option
        public string Name { get; }
        public QuestEventFieldKind Kind { get; }
        // the value of its kind; reading another kind's throws
        public long Number => Kind == QuestEventFieldKind.Number ? number_ : throw new InvalidOperationException($"{Name} is not a number");
        public bool Bool => Kind == QuestEventFieldKind.Bool ? bool_ : throw new InvalidOperationException($"{Name} is not a boolean");
        public IReadOnlyList<int> List => Kind == QuestEventFieldKind.List ? list_ : throw new InvalidOperationException($"{Name} is not a list");

        public bool Equals(QuestEventField other) =>
            Name == other.Name && Kind == other.Kind && number_ == other.number_ && bool_ == other.bool_ &&
            (list_ == null ? other.list_ == null : other.list_ != null && list_.SequenceEqual(other.list_));
        public override bool Equals(object obj) => obj is QuestEventField f && Equals(f);
        public override int GetHashCode() => HashCode.Combine(Name, Kind, number_, bool_, list_?.Length ?? -1);

        // the value as Lua writes it: 0, true, { 1, 2 }
        public string LuaValue => Kind switch
        {
            QuestEventFieldKind.Bool => bool_ ? "true" : "false",
            QuestEventFieldKind.List => "{" + string.Join(",", list_.Select(v => v.ToString(CultureInfo.InvariantCulture))) + "}",
            _ => number_.ToString(CultureInfo.InvariantCulture),
        };

        // as the table entry: text_table=0
        public override string ToString() => $"{Name}={LuaValue}";
    }
}
