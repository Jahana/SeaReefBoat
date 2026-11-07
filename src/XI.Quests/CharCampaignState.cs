// XI.Quests -- the normalized char_campaign_state row, the same shape as char_assault_state: one row per character
// holding the current id and the 512 completion flags as a 64-byte mask. No compiled oracle: nothing in the C++ changes
// the blob, so tests/XI.Verify.Quests/C1Progress.cs proves Project(ToCell(Import(blob))) == blob over synthetic blobs.
// As with assault, Extract reads a raw complete[] byte as a bit and Project writes 0/1.
using System;

namespace XI.Quests
{
    public sealed class CampaignCell
    {
        public const int CompletedBits = 512;
        public const int MaskBytes = CompletedBits / 8; // 64

        public ushort Current;
        private readonly byte[] _mask = new byte[MaskBytes];

        public bool Completed(int id) => id >= 0 && id < CompletedBits && ((_mask[id / 8] >> (id % 8)) & 1) != 0;

        public void SetCompleted(int id, bool v)
        {
            if (id < 0 || id >= CompletedBits) return;
            if (v) _mask[id / 8] |= (byte)(1 << (id % 8));
            else _mask[id / 8] &= (byte)~(1 << (id % 8));
        }
    }

    public static class CampaignWireCodec
    {
        public static CampaignCell Extract(CampaignBitBlob blob)
        {
            var c = new CampaignCell { Current = blob.RawGetCurrent() };
            for (int id = 0; id < CampaignCell.CompletedBits; id++)
                c.SetCompleted(id, blob.RawGetCompleteFlag(id));
            return c;
        }

        public static CampaignBitBlob Project(CampaignCell cell)
        {
            var blob = new CampaignBitBlob();
            blob.RawSetCurrent(cell.Current);
            for (int id = 0; id < CampaignCell.CompletedBits; id++)
                blob.RawSetComplete(id, cell.Completed(id));
            return blob;
        }
    }

    public sealed class CharCampaignStateRow
    {
        public uint CharId;
        public ushort Current;
        public byte[] CompletedMask = new byte[CampaignCell.MaskBytes]; // 64 bytes = 512 bits

        public CampaignCell ToCell()
        {
            var c = new CampaignCell { Current = Current };
            for (int id = 0; id < CampaignCell.CompletedBits; id++)
                c.SetCompleted(id, id < CompletedMask.Length * 8 && ((CompletedMask[id / 8] >> (id % 8)) & 1) != 0);
            return c;
        }
    }

    public static class CampaignStateSchema
    {
        public const string TableName = "char_campaign_state";

        public const string CreateTableSql =
            "CREATE TABLE IF NOT EXISTS `char_campaign_state` (\n" +
            "  `charid`         int(10) unsigned NOT NULL,\n" +
            "  `current`        smallint(5) unsigned NOT NULL DEFAULT 0,\n" +
            "  `completed_mask` varbinary(64) NOT NULL DEFAULT '',\n" +
            "  `updated_at`     timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,\n" +
            "  PRIMARY KEY (`charid`)\n" +
            ") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;";

        public static readonly string[] Columns = { "charid", "current", "completed_mask", "updated_at" };
    }

    public static class CampaignStateImporter
    {
        // a legacy 514-byte chars.campaign blob as one row (a null or short blob zero-fills)
        public static CharCampaignStateRow Import(uint charId, byte[] campaignBlob)
        {
            var blob = new CampaignBitBlob();
            blob.LoadFromBlob(campaignBlob);
            var cell = CampaignWireCodec.Extract(blob);

            var mask = new byte[CampaignCell.MaskBytes];
            for (int id = 0; id < CampaignCell.CompletedBits; id++)
                if (cell.Completed(id)) mask[id / 8] |= (byte)(1 << (id % 8));

            return new CharCampaignStateRow { CharId = charId, Current = cell.Current, CompletedMask = mask };
        }

        public static CampaignCell ToCell(CharCampaignStateRow row) => row.ToCell();
    }
}
