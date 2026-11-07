using System;
using System.Collections.Generic;
using System.Text;

namespace XI.Persistence
{
    // Deterministic in-memory IRawResultSet for the offline verifier. Models the engine's
    // 1-indexed label contract exactly: RawColumnLabel(i) is only valid for i >= 1.
    public sealed class InMemoryResultSet : IRawResultSet
    {
        private readonly List<string> _labels; // 0-based store, exposed 1-indexed
        private readonly List<Dictionary<string, object>> _rows;
        private int _cursor = -1;

        public InMemoryResultSet(IReadOnlyList<string> columnNames, IReadOnlyList<Dictionary<string, object>> rows)
        {
            _labels = new List<string>(columnNames);
            _rows = new List<Dictionary<string, object>>(rows);
        }

        // the same rows from the first, each blob a copy of its own (what a fresh read of them would hand out)
        public InMemoryResultSet Rewound()
        {
            var rows = new List<Dictionary<string, object>>(_rows.Count);
            foreach (var row in _rows)
            {
                var copy = new Dictionary<string, object>(row);
                foreach (var kv in row)
                    if (kv.Value is byte[] b) copy[kv.Key] = (byte[])b.Clone();
                rows.Add(copy);
            }
            return new InMemoryResultSet(_labels, rows);
        }

        private Dictionary<string, object> Current => _rows[_cursor];

        private object Cell(string key) => Current.TryGetValue(key, out var v) ? v : null;

        public bool RawNext()
        {
            _cursor++;
            return _cursor < _rows.Count;
        }

        public int RawRowsCount() => _rows.Count;

        public int RawColumnCount() => _labels.Count;

        public string RawColumnLabel(int oneBasedIndex)
        {
            if (oneBasedIndex < 1 || oneBasedIndex > _labels.Count)
            {
                throw new IndexOutOfRangeException($"raw column label {oneBasedIndex} is out of range (1..{_labels.Count})");
            }

            return _labels[oneBasedIndex - 1];
        }

        public bool RawIsNull(string key) => Cell(key) == null;

        private long AsLong(string key)
        {
            var v = Cell(key);
            return v switch
            {
                null => 0,
                byte[] b => b.Length >= 8 ? BitConverter.ToInt64(b, 0) : ExpandInt(b),
                _ => Convert.ToInt64(v, System.Globalization.CultureInfo.InvariantCulture),
            };
        }

        private static long ExpandInt(byte[] b)
        {
            long acc = 0;
            for (var i = 0; i < b.Length; i++)
            {
                acc |= (long)b[i] << (8 * i);
            }

            return acc;
        }

        public long RawGetInt64(string key) => AsLong(key);
        public ulong RawGetUInt64(string key) => Cell(key) is ulong u ? u : (ulong)AsLong(key);
        public int RawGetInt32(string key) => (int)AsLong(key);
        public uint RawGetUInt32(string key) => (uint)AsLong(key);
        public short RawGetInt16(string key) => (short)AsLong(key);
        public ushort RawGetUInt16(string key) => (ushort)AsLong(key);
        public sbyte RawGetInt8(string key) => (sbyte)AsLong(key);
        public byte RawGetUInt8(string key) => (byte)AsLong(key);
        public bool RawGetBool(string key) => AsLong(key) != 0;
        public double RawGetDouble(string key) => Convert.ToDouble(Cell(key), System.Globalization.CultureInfo.InvariantCulture);
        public float RawGetFloat(string key) => (float)RawGetDouble(key);

        public string RawGetString(string key)
        {
            var v = Cell(key);
            if (v == null) return string.Empty;
            if (v is byte[] b) return Encoding.UTF8.GetString(b);
            return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        }

        public byte[] RawGetBlobBytes(string key)
        {
            var v = Cell(key);
            return v switch
            {
                null => Array.Empty<byte>(),
                byte[] b => b,
                _ => Encoding.UTF8.GetBytes(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty),
            };
        }
    }
}
