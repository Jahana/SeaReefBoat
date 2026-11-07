using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace XI.Persistence
{
    // An IDatabase that can answer a char's reads from rows fetched ahead of time on another thread. The map's login
    // (charutils::LoadChar and the zone-in after it) reads some forty rows one query at a time; a worker reads them into
    // a ReadSet while the main thread goes on, and the main thread's login then runs as before, inside Serve(set),
    // its SELECTs answered from the set wherever the SQL text and the bound values match. Anything else goes to the
    // database as usual, so a read the set lacks is only slower, never different.
    //
    // A read the set holds must still be what the database would return when the main thread asks. Every statement
    // that writes, from any thread of this process, goes through Execute here: a write that may touch the set's char
    // marks the tables it names stale in that set (from when the set is opened until it is disposed), and a stale
    // table's rows are read from the database again. A write may touch the char unless it names charid, binds none of
    // the char's id, and has no subquery or join (a write to another char's rows).
    public sealed class PrefetchDatabase : IDatabase
    {
        private readonly IDatabase inner_;
        private readonly object lock_ = new object();
        private readonly List<ReadSet> live_ = new List<ReadSet>();

        [ThreadStatic] private static ReadSet serving_;

        public PrefetchDatabase(IDatabase inner) => inner_ = inner;

        public IDatabase Inner => inner_;
        public string Schema => inner_.Schema;
        public string Version => inner_.Version;
        public string DriverVersion => inner_.DriverVersion;

        public sealed class ReadSet : IDisposable
        {
            private readonly PrefetchDatabase owner_;
            private readonly Dictionary<string, ResultSet> rows_ = new Dictionary<string, ResultSet>();
            private readonly HashSet<string> stale_ = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private bool allStale_;

            public uint CharId { get; }
            // served reads answered from the set / sent to the database (a stale table's included)
            public int Hits { get; private set; }
            public int Misses { get; private set; }
            // the served SELECTs that bound the char's id and were not in the set (the reads a read-ahead list lacks)
            public List<string> MissedCharReads { get; } = new List<string>();

            internal ReadSet(PrefetchDatabase owner, uint charId)
            {
                owner_ = owner;
                CharId = charId;
            }

            public int Count
            {
                get { lock (this) return rows_.Count; }
            }

            internal void Put(string key, ResultSet r)
            {
                lock (this) rows_[key] = r;
            }

            internal void MarkStale(IReadOnlyList<string> tables)
            {
                lock (this)
                {
                    if (tables == null) allStale_ = true;
                    else foreach (var t in tables) stale_.Add(t);
                }
            }

            // a read the set answers from the others it holds when it does not hold the read itself (null: it cannot);
            // asked only while the read's tables are not stale
            public Func<string, IReadOnlyList<BoundValue>, ResultSet> Derive { get; set; }

            // a held read, from its first row (null: not held, or a table it names is stale)
            public ResultSet Held(string query, IReadOnlyList<BoundValue> args)
            {
                lock (this)
                    return rows_.TryGetValue(Key(query, args), out var r) && !allStale_ && !NamesStale(query) ? r.Replay() : null;
            }

            internal ResultSet Take(string key, string query, IReadOnlyList<BoundValue> args)
            {
                lock (this)
                {
                    bool held = rows_.TryGetValue(key, out var r);
                    bool fresh = !allStale_ && !NamesStale(query);
                    if (held && fresh)
                    {
                        Hits++;
                        return r.Replay();
                    }
                    if (!held && fresh && Derive?.Invoke(query, args) is { } derived)
                    {
                        Hits++;
                        return derived;
                    }
                    Misses++;
                    if (!held && BindsId(args, CharId)) MissedCharReads.Add(query + Strings(args));
                    return null;
                }
            }

            private static string Strings(IReadOnlyList<BoundValue> args)
            {
                var sb = new StringBuilder();
                if (args != null)
                    foreach (var a in args)
                        if (a.Type == BoundValue.Kind.String) sb.Append(" \"").Append(a.AsString).Append('"');
                return sb.ToString();
            }

            private bool NamesStale(string query)
            {
                if (stale_.Count == 0) return false;
                var tables = TablesOf(query);
                if (tables == null) return true;
                foreach (var t in tables)
                    if (stale_.Contains(t)) return true;
                return false;
            }

            public void Dispose() => owner_.Close(this);
        }

        // a set for the char, registered for the writes from now on; fill it with Read (any thread), serve it with Serve
        public ReadSet Open(uint charId)
        {
            var set = new ReadSet(this, charId);
            lock (lock_) live_.Add(set);
            return set;
        }

        // a set let go (the verify suites look at what it answered)
        public event Action<ReadSet> Closed;

        private void Close(ReadSet set)
        {
            bool removed;
            lock (lock_) removed = live_.Remove(set);
            if (removed) Closed?.Invoke(set);
        }

        // a SELECT read into the set (on the calling thread)
        public void Read(ReadSet set, string query, IReadOnlyList<BoundValue> args)
        {
            if (!IsRead(query)) return;
            var r = inner_.Execute(query, args);
            if (r != null && r.Replay() != null) set.Put(Key(query, args), r);
        }

        // the calling thread's SELECTs are answered from the set until the scope is disposed
        public IDisposable Serve(ReadSet set)
        {
            var previous = serving_;
            serving_ = set;
            return new Scope(() => serving_ = previous);
        }

        private sealed class Scope : IDisposable
        {
            private Action undo_;
            public Scope(Action undo) => undo_ = undo;
            public void Dispose()
            {
                undo_?.Invoke();
                undo_ = null;
            }
        }

        public ResultSet Execute(string query, IReadOnlyList<BoundValue> parameters)
        {
            bool select = IsRead(query);
            var set = serving_;
            if (select && set != null)
            {
                var hit = set.Take(Key(query, parameters), query, parameters);
                if (hit != null) return hit;
            }
            if (select) return inner_.Execute(query, parameters);
            try { return inner_.Execute(query, parameters); }
            finally { Wrote(query, parameters); }
        }

        // a SELECT or SHOW, as QueryValidation classifies it
        private static bool IsRead(string query) => QueryValidation.ValidateLeadingKeyword(query) == ResultSetType.Select;

        public ResultSet ExecuteBulk(string query, IReadOnlyList<BoundValue> parameters)
        {
            try { return inner_.ExecuteBulk(query, parameters); }
            finally { Wrote(query, parameters); }
        }

        public bool Transaction(Fn transactionFn) => inner_.Transaction(transactionFn);

        // a write, done (or failed): the sets whose char it may touch drop what they hold of its tables
        private void Wrote(string query, IReadOnlyList<BoundValue> args)
        {
            ReadSet[] sets;
            lock (lock_)
            {
                if (live_.Count == 0) return;
                sets = live_.ToArray();
            }
            var tables = TablesOf(query);
            foreach (var set in sets)
                if (MayTouch(query, args, set.CharId)) set.MarkStale(tables);
        }

        private static readonly Regex Subquery = new Regex(@"\b(select|join)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex CharIdColumn = new Regex(@"\bcharid\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static bool MayTouch(string query, IReadOnlyList<BoundValue> args, uint charId)
        {
            if (!CharIdColumn.IsMatch(query) || Subquery.IsMatch(query)) return true;
            if (BindsId(args, charId)) return true;
            return Regex.IsMatch(query, @"(?<!\d)" + charId + @"(?!\d)");
        }

        private static bool BindsId(IReadOnlyList<BoundValue> args, uint id)
        {
            if (args == null) return false;
            foreach (var a in args)
            {
                long v;
                switch (a.Type)
                {
                    case BoundValue.Kind.Int8: v = a.AsInt8; break;
                    case BoundValue.Kind.UInt8: v = a.AsUInt8; break;
                    case BoundValue.Kind.Int16: v = a.AsInt16; break;
                    case BoundValue.Kind.UInt16: v = a.AsUInt16; break;
                    case BoundValue.Kind.Int32: v = a.AsInt32; break;
                    case BoundValue.Kind.UInt32: v = a.AsUInt32; break;
                    case BoundValue.Kind.String: if (a.AsString == id.ToString()) return true; continue;
                    default: continue;
                }
                if (v == id) return true;
            }
            return false;
        }

        // the tables a statement names (after FROM, JOIN, INTO, UPDATE, TABLE); null when it names none it can find
        private static readonly Regex TableName = new Regex(@"\b(?:from|join|into|update|table)\s+`?([A-Za-z0-9_]+)`?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static IReadOnlyList<string> TablesOf(string query)
        {
            List<string> tables = null;
            foreach (Match m in TableName.Matches(query))
                (tables ??= new List<string>()).Add(m.Groups[1].Value);
            return tables;
        }

        // the SQL text and every bound value, typed
        private static string Key(string query, IReadOnlyList<BoundValue> args)
        {
            var sb = new StringBuilder(query);
            if (args != null)
                foreach (var a in args)
                {
                    sb.Append('\u0001').Append((int)a.Type).Append(':');
                    if (a.Type == BoundValue.Kind.Blob) sb.Append(Convert.ToBase64String(a.AsBlob));
                    else sb.Append(Convert.ToString(a.Value, System.Globalization.CultureInfo.InvariantCulture));
                }
            return sb.ToString();
        }
    }
}
