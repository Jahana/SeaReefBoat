// XI.Quests -- one argument of a quest reply, as the Lua helper receives it (quest rework P1).
using System;
using System.Globalization;

namespace XI.Quests.Authoring
{
    public enum QuestArgKind : byte
    {
        Number, Bool, String,
    }

    // a reply argument: a Lua number (an int, as every reply argument was before), a Lua boolean or a Lua string. The
    // helpers take `params QuestArg[]`, and an int, a bool or a string converts to one, so QuestReply.Event(5, 1, 2) is
    // unchanged and quest:messageName(id, 0, 0, 0, 0, true, false) is QuestReply.MessageName(id, 0, 0, 0, 0, true, false):
    // 0 is truthy in Lua, so showText's showName / turn need the booleans themselves
    public readonly struct QuestArg : IEquatable<QuestArg>
    {
        readonly int number_;
        readonly bool bool_;
        readonly string string_;

        QuestArg(QuestArgKind kind, int number, bool b, string s)
        {
            Kind = kind;
            number_ = number;
            bool_ = b;
            string_ = s;
        }

        public QuestArgKind Kind { get; }
        // the value of its kind; reading another kind's throws
        public int Number => Kind == QuestArgKind.Number ? number_ : throw new InvalidOperationException($"a {Kind} argument is not a number");
        public bool Bool => Kind == QuestArgKind.Bool ? bool_ : throw new InvalidOperationException($"a {Kind} argument is not a boolean");
        public string String => Kind == QuestArgKind.String ? string_ : throw new InvalidOperationException($"a {Kind} argument is not a string");

        public static implicit operator QuestArg(int value) => new QuestArg(QuestArgKind.Number, value, false, null);
        public static implicit operator QuestArg(bool value) => new QuestArg(QuestArgKind.Bool, 0, value, null);
        // a Lua string; null has no Lua string (nil would end the helper's arguments early), so it is refused
        public static implicit operator QuestArg(string value) =>
            new QuestArg(QuestArgKind.String, 0, false, value ?? throw new ArgumentNullException(nameof(value), "a reply argument string cannot be null"));

        public bool Equals(QuestArg other) => Kind == other.Kind && number_ == other.number_ && bool_ == other.bool_ && string_ == other.string_;
        public override bool Equals(object obj) => obj is QuestArg a && Equals(a);
        public override int GetHashCode() => HashCode.Combine(Kind, number_, bool_, string_);

        // as Lua writes it: 5, true, 'text'
        public override string ToString() => Kind switch
        {
            QuestArgKind.Bool => bool_ ? "true" : "false",
            QuestArgKind.String => "'" + string_.Replace("\\", "\\\\").Replace("'", "\\'") + "'",
            _ => number_.ToString(CultureInfo.InvariantCulture),
        };
    }
}
