// glaze writes a std::string as a CBOR text string of its raw bytes, and the game's strings are Shift-JIS with
// auto-translate phrases, not UTF-8. These carry such a string as Latin-1 (one char per byte) so it round-trips
// byte for byte; the readers and writers are Lax so the bytes are not checked as UTF-8.
using System;
using System.Formats.Cbor;
using System.Text;

namespace XI.Ipc
{
    public static class CborRaw
    {
        public static void WriteText(CborWriter w, string s)
        {
            byte[] bytes = Encoding.Latin1.GetBytes(s ?? "");
            byte[] head = Head(3, (ulong)bytes.Length);
            byte[] item = new byte[head.Length + bytes.Length];
            Buffer.BlockCopy(head, 0, item, 0, head.Length);
            Buffer.BlockCopy(bytes, 0, item, head.Length, bytes.Length);
            w.WriteEncodedValue(item);
        }

        public static string ReadText(CborReader r) =>
            Encoding.Latin1.GetString(r.ReadDefiniteLengthTextStringBytes().Span);

        // the raw bytes of a Latin-1 carried string, and back
        public static byte[] Bytes(string s) => Encoding.Latin1.GetBytes(s ?? "");
        public static string FromBytes(ReadOnlySpan<byte> b) => Encoding.Latin1.GetString(b);

        private static byte[] Head(int major, ulong length)
        {
            byte mt = (byte)(major << 5);
            if (length < 24) return new[] { (byte)(mt | (byte)length) };
            if (length <= byte.MaxValue) return new[] { (byte)(mt | 24), (byte)length };
            if (length <= ushort.MaxValue) return new[] { (byte)(mt | 25), (byte)(length >> 8), (byte)length };
            if (length <= uint.MaxValue)
                return new[] { (byte)(mt | 26), (byte)(length >> 24), (byte)(length >> 16), (byte)(length >> 8), (byte)length };
            var b = new byte[9];
            b[0] = (byte)(mt | 27);
            for (int i = 0; i < 8; i++) b[8 - i] = (byte)(length >> (i * 8));
            return b;
        }
    }
}
