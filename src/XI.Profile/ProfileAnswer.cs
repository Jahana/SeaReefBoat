// src/profile/profile/answer.h: a profile answer whose size and inline checksums are filled in as objects are added,
// and the request parsing helpers.

using System;
using System.Collections.Generic;

namespace XI.Profile
{
    public sealed class ProfileAnswer
    {
        public const byte OpenAnswerCode = 0x81;
        public const byte AnswerCode = 0x83;

        private readonly List<byte> _body = new();
        private uint _sum;

        public ProfileAnswer(ProfError error = ProfError.None) => Error = error;

        public ProfError Error { get; }

        // add: a sealed object (one whose last u32 is its checksum) gets the running sum of everything before it
        public ProfileAnswer Add(byte[] obj, bool isSealed = false)
        {
            if (isSealed)
            {
                Le.U32(obj, obj.Length - 4, _sum + ProfileCodec.Checksum(obj.AsSpan(0, obj.Length - 4)));
            }
            return AddBytes(obj);
        }

        public ProfileAnswer AddAll(IEnumerable<byte[]> objects)
        {
            foreach (var obj in objects) Add(obj);
            return this;
        }

        public ProfileAnswer AddBytes(ReadOnlySpan<byte> bytes)
        {
            foreach (var b in bytes) _body.Add(b);
            _sum += ProfileCodec.Checksum(bytes);
            return this;
        }

        public ProfileAnswer AddChecksum()
        {
            var sum = new byte[4];
            Le.U32(sum, 0, _sum);
            return AddBytes(sum);
        }

        public byte[] Head() => ProfRecvAns.Make(AnswerCode, Error, (uint)_body.Count);

        public byte[] Body() => _body.ToArray();

        // hasTrailingChecksum: a request's last u32 is the checksum of the bytes before it
        public static bool HasTrailingChecksum(ReadOnlySpan<byte> bytes) =>
            bytes.Length >= 4 && Le.U32(bytes, bytes.Length - 4) == ProfileCodec.Checksum(bytes.Slice(0, bytes.Length - 4));

        // parse: exact size, checksum verified if it has one
        public static bool Parse(ReadOnlySpan<byte> body, int size, bool isSealed)
        {
            if (isSealed && !HasTrailingChecksum(body)) return false;
            return body.Length == size;
        }
    }
}
