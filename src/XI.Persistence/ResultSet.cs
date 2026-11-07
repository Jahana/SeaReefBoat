using System;
using System.Collections.Generic;

namespace XI.Persistence
{
    // The 1-indexed raw backend surface, mirroring ResultSet's protected raw* virtuals in
    // src/common/database/result_set.h. Everything on THIS interface is 1-indexed for labels,
    // exactly as the C++ note demands. Backends (MariaDB reader, in-memory fake) implement it.
    public interface IRawResultSet
    {
        bool RawNext();
        int RawRowsCount();
        int RawColumnCount();
        string RawColumnLabel(int oneBasedIndex);
        bool RawIsNull(string key);
        long RawGetInt64(string key);
        ulong RawGetUInt64(string key);
        int RawGetInt32(string key);
        uint RawGetUInt32(string key);
        short RawGetInt16(string key);
        ushort RawGetUInt16(string key);
        sbyte RawGetInt8(string key);
        byte RawGetUInt8(string key);
        bool RawGetBool(string key);
        float RawGetFloat(string key);
        double RawGetDouble(string key);
        string RawGetString(string key);
        byte[] RawGetBlobBytes(string key);
    }

    // The public ResultSet façade. Its integer accessors are 0-indexed and translate to the
    // 1-indexed raw backend, matching C++:
    //   get<T>(index)        -> get<T>(rawColumnLabel(index + 1))
    //   columnName(index)    -> rawColumnLabel(index + 1)
    // get<T>(null) returns the default for non-blob types, mirroring the DebugSQL note that NULL
    // is legitimate LEFT JOIN data (callers wanting a fallback use GetOrDefault).
    public sealed class ResultSet
    {
        private readonly IRawResultSet _raw;

        public ResultSetType Type { get; }
        public string Query { get; }
        private readonly uint _rowsAffected;

        public ResultSet(IRawResultSet raw, string query, ResultSetType type, uint rowsAffected = 0)
        {
            _raw = raw;
            Query = query;
            Type = type;
            _rowsAffected = rowsAffected;
        }

        // a SELECT's rows again from the first (null for a result that cannot be read twice)
        public ResultSet Replay() =>
            Type == ResultSetType.Select && _raw is InMemoryResultSet rows ? new ResultSet(rows.Rewound(), Query, Type, _rowsAffected) : null;

        public bool Next()
        {
            if (Type != ResultSetType.Select)
            {
                return false;
            }

            return _raw.RawNext();
        }

        public uint RowsCount() => Type == ResultSetType.Select ? (uint)_raw.RawRowsCount() : 0u;

        public uint RowsAffected() => Type == ResultSetType.Update ? _rowsAffected : 0u;

        public uint ColumnCount() => Type == ResultSetType.Select ? (uint)_raw.RawColumnCount() : 0u;

        // 0-indexed public accessor over the 1-indexed backend.
        public string ColumnName(uint zeroBasedIndex)
        {
            if (Type != ResultSetType.Select)
            {
                return string.Empty;
            }

            return _raw.RawColumnLabel((int)zeroBasedIndex + 1);
        }

        public bool IsNull(string key)
        {
            if (Type != ResultSetType.Select)
            {
                return false;
            }

            return _raw.RawIsNull(key);
        }

        public byte[] GetBlobBytes(string key)
        {
            if (Type != ResultSetType.Select)
            {
                return Array.Empty<byte>();
            }

            return _raw.RawGetBlobBytes(key);
        }

        // get<T>(key)
        public T Get<T>(string key)
        {
            if (Type != ResultSetType.Select)
            {
                return default;
            }

            var target = typeof(T);
            var underlying = target.IsEnum ? Enum.GetUnderlyingType(target) : target;

            if (!_raw.RawIsNull(key))
            {
                var value = ReadScalar(underlying, key);
                if (target.IsEnum)
                {
                    return (T)Enum.ToObject(target, value);
                }

                return (T)value;
            }

            // NULL: blob types are the only ones that fall through in C++ (they read garbage);
            // for scalars C++ returns its value-initialized `UnderlyingT value{}`. For std::string
            // that is the EMPTY STRING, not null; for numbers it is zero; for enums the default cast.
            if (underlying == typeof(string))
            {
                return (T)(object)string.Empty;
            }

            return default;
        }

        // get<T>(index): 0-indexed -> rawColumnLabel(index + 1)
        public T Get<T>(uint zeroBasedIndex)
        {
            if (Type != ResultSetType.Select)
            {
                return default;
            }

            return Get<T>(_raw.RawColumnLabel((int)zeroBasedIndex + 1));
        }

        public T GetOrDefault<T>(string key, T defaultValue)
        {
            if (Type != ResultSetType.Select)
            {
                return defaultValue;
            }

            if (_raw.RawIsNull(key))
            {
                return defaultValue;
            }

            return Get<T>(key);
        }

        public T GetOrDefault<T>(uint zeroBasedIndex, T defaultValue)
        {
            if (Type != ResultSetType.Select)
            {
                return defaultValue;
            }

            return GetOrDefault(_raw.RawColumnLabel((int)zeroBasedIndex + 1), defaultValue);
        }

        private object ReadScalar(Type underlying, string key)
        {
            if (underlying == typeof(long)) return _raw.RawGetInt64(key);
            if (underlying == typeof(ulong)) return _raw.RawGetUInt64(key);
            if (underlying == typeof(int)) return _raw.RawGetInt32(key);
            if (underlying == typeof(uint)) return _raw.RawGetUInt32(key);
            if (underlying == typeof(short)) return _raw.RawGetInt16(key);
            if (underlying == typeof(ushort)) return _raw.RawGetUInt16(key);
            if (underlying == typeof(sbyte)) return _raw.RawGetInt8(key);
            if (underlying == typeof(byte)) return _raw.RawGetUInt8(key);
            if (underlying == typeof(bool)) return _raw.RawGetBool(key);
            if (underlying == typeof(float)) return _raw.RawGetFloat(key);
            if (underlying == typeof(double)) return _raw.RawGetDouble(key);
            if (underlying == typeof(string)) return _raw.RawGetString(key);

            throw new NotSupportedException($"ResultSet::get: unsupported type {underlying.FullName}");
        }
    }
}
