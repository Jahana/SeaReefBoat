// XI.Quests -- the campaign-log wire blob (chars.campaign). mmo.h campaignlog_t { uint16 current; bool complete[512]; } =
// 514 bytes, value-initialized all-zero, loaded (charutils.cpp:399) and saved (charutils.cpp:5457) whole. Nothing in the
// C++ changes it, so there are no accessors with semantics here: only the raw view the normalized codec needs.
//
// Raw layout (little-endian): current = bytes +0..1; complete[id] byte = +2 + id (id < 512).
using System;
using System.Text;

namespace XI.Quests
{
    public sealed class CampaignBitBlob
    {
        public const int CurrentBytes = 2;    // uint16
        public const int CompleteCount = 512; // bool complete[512]
        public const int Size = CurrentBytes + CompleteCount; // 514
        const int CompleteBase = 2;

        private readonly byte[] _data = new byte[Size];

        public byte[] Bytes => (byte[])_data.Clone();

        public ushort RawGetCurrent() => (ushort)(_data[0] | (_data[1] << 8));
        public void RawSetCurrent(ushort v) { _data[0] = (byte)(v & 0xFF); _data[1] = (byte)(v >> 8); }
        public bool RawGetCompleteFlag(int id) => id >= 0 && id < CompleteCount && _data[CompleteBase + id] != 0;
        public void RawSetComplete(int id, bool v) { if (id >= 0 && id < CompleteCount) _data[CompleteBase + id] = (byte)(v ? 1 : 0); }

        // db::extractFromBlob: zero-init, then copy min(sizeof(dest), blobLen)
        public void LoadFromBlob(byte[] blob)
        {
            Array.Clear(_data, 0, _data.Length);
            if (blob == null) return;
            Buffer.BlockCopy(blob, 0, _data, 0, Math.Min(Size, blob.Length));
        }

        public string ToHex()
        {
            var sb = new StringBuilder(Size * 2);
            for (int i = 0; i < Size; i++) sb.Append(_data[i].ToString("X2"));
            return sb.ToString();
        }
    }
}
