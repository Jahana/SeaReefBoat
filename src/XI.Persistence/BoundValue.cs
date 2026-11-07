using System;

namespace XI.Persistence
{
    // A single type-erased prepared-statement parameter, mirroring db::BoundValue.
    // Note the C++ variant has NO int64/uint64 case: binding a 64-bit int via
    // db::preparedStmt is a compile error (see lowerBoundValue). The result-set READ
    // path does support them; the BIND path does not. That asymmetry is preserved here.
    public readonly struct BoundValue
    {
        public enum Kind : byte
        {
            Int8,
            UInt8,
            Int16,
            UInt16,
            Int32,
            UInt32,
            Bool,
            Float,
            Double,
            String,
            Blob,
        }

        public readonly Kind Type;
        private readonly object _value;

        private BoundValue(Kind kind, object value)
        {
            Type = kind;
            _value = value;
        }

        public static BoundValue Of(sbyte v) => new(Kind.Int8, v);
        public static BoundValue Of(byte v) => new(Kind.UInt8, v);
        public static BoundValue Of(short v) => new(Kind.Int16, v);
        public static BoundValue Of(ushort v) => new(Kind.UInt16, v);
        public static BoundValue Of(int v) => new(Kind.Int32, v);
        public static BoundValue Of(uint v) => new(Kind.UInt32, v);
        public static BoundValue Of(bool v) => new(Kind.Bool, v);
        public static BoundValue Of(float v) => new(Kind.Float, v);
        public static BoundValue Of(double v) => new(Kind.Double, v);
        public static BoundValue Of(string v) => new(Kind.String, v ?? string.Empty);
        public static BoundValue Of(byte[] v) => new(Kind.Blob, v ?? Array.Empty<byte>());

        public object Value => _value;

        public sbyte AsInt8 => (sbyte)_value;
        public byte AsUInt8 => (byte)_value;
        public short AsInt16 => (short)_value;
        public ushort AsUInt16 => (ushort)_value;
        public int AsInt32 => (int)_value;
        public uint AsUInt32 => (uint)_value;
        public bool AsBool => (bool)_value;
        public float AsFloat => (float)_value;
        public double AsDouble => (double)_value;
        public string AsString => (string)_value;
        public byte[] AsBlob => (byte[])_value;
    }

    // Mirrors db::detail::lowerBoundValue typing for the scalar args we bind in Phase 1.
    // Enums decay to their underlying type before selection (enum_decay_t); size_t binds
    // as uint32 (legacy behaviour). 64-bit ints are intentionally unsupported here.
    public static class Binder
    {
        public static BoundValue Lower(int v) => BoundValue.Of(v);
        public static BoundValue Lower(uint v) => BoundValue.Of(v);
        public static BoundValue Lower(short v) => BoundValue.Of(v);
        public static BoundValue Lower(ushort v) => BoundValue.Of(v);
        public static BoundValue Lower(sbyte v) => BoundValue.Of(v);
        public static BoundValue Lower(byte v) => BoundValue.Of(v);
        public static BoundValue Lower(bool v) => BoundValue.Of(v);
        public static BoundValue Lower(float v) => BoundValue.Of(v);
        public static BoundValue Lower(double v) => BoundValue.Of(v);
        public static BoundValue Lower(string v) => BoundValue.Of(v);
        public static BoundValue Lower(byte[] v) => BoundValue.Of(v);

        public static BoundValue LowerEnum<TEnum>(TEnum value) where TEnum : struct, Enum
        {
            // Mirror enum_decay_t: convert to the underlying type then lower.
            var underlying = Convert.GetTypeCode(value);
            return underlying switch
            {
                TypeCode.Int32 => BoundValue.Of(Convert.ToInt32(value)),
                TypeCode.UInt32 => BoundValue.Of(Convert.ToUInt32(value)),
                TypeCode.Int16 => BoundValue.Of(Convert.ToInt16(value)),
                TypeCode.UInt16 => BoundValue.Of(Convert.ToUInt16(value)),
                TypeCode.Byte => BoundValue.Of(Convert.ToByte(value)),
                TypeCode.SByte => BoundValue.Of(Convert.ToSByte(value)),
                _ => throw new NotSupportedException($"enum underlying type {underlying} not bindable (matches C++ variant)"),
            };
        }
    }
}
