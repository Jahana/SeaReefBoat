using System;
using System.Collections.Generic;
using XI.Persistence;
using XI.Quests;
using XI.Zone;

// quest rework C1: the assault, campaign and eminence rows on the real database, through the zone host
internal static partial class Program
{
    private const uint C1Char = 996144, C1Upgrade = 996145, C1Off = 996146, C1Deleted = 996147, C1Timed = 996148;

    private static byte[] Padded(byte[] b, int n)
    {
        var p = new byte[n];
        Array.Copy(b, p, Math.Min(b.Length, n));
        return p;
    }

    // the stored rows' projection == the chars columns, byte for byte (130 / 514 / 700; NULL reads as zero)
    private static string C1RowsMatchLegacy()
    {
        QuestStateStore.TryLoadAssault(db, CharId, out var assault);
        QuestStateStore.TryLoadCampaign(db, CharId, out var campaign);
        var p = CharProgress.FromRows(CharId, new List<CharQuestStateRow>(), new List<CharMissionStateRow>(), null, assault, campaign, QuestStateStore.LoadEminence(db, CharId));
        bool a = p.ProjectAssault().ToHex() == Convert.ToHexString(Padded(DbBlob("assault"), AssaultBitBlob.Size));
        bool k = p.ProjectCampaign().ToHex() == Convert.ToHexString(Padded(DbBlob("campaign"), CampaignBitBlob.Size));
        bool e = p.ProjectEminence().ToHex() == Convert.ToHexString(Padded(DbBlob("eminence"), EminenceBitBlob.Size));
        return $"{a}/{k}/{e}";
    }

    private static string C1AssaultRow(params int[] ids)
    {
        QuestStateStore.TryLoadAssault(db, CharId, out var row);
        if (row == null) return "-";
        var cell = row.ToCell();
        return cell.Current + "|" + string.Join(",", Array.ConvertAll(ids, id => cell.Completed(id) ? "1" : "0"));
    }

    private static string C1EminenceRow(uint record)
    {
        var r = Sql(db, "SELECT slot, progress, completed FROM char_eminence_state WHERE charid = ? AND record_id = ?", BoundValue.Of(CharId), BoundValue.Of(record));
        return r != null && r.Next() ? $"{r.Get<byte>("slot")}|{r.Get<uint>("progress")}|{(r.Get<bool>("completed") ? 1 : 0)}" : "-";
    }

    // current 7, assault 3 completed; campaign 12, slot 511 completed; record 2 active with progress 7, record 11 completed
    private static void C1Seed(uint charId)
    {
        var a = new AssaultBitBlob();
        a.CompleteAssault(3);
        a.AddAssault(7);
        var k = new CampaignBitBlob();
        k.RawSetCurrent(12);
        k.RawSetComplete(511, true);
        var e = new EminenceBitBlob();
        e.Add(2);
        e.SetProgress(2, 7);
        e.SetCompletion(11, true);
        Sql(db, "UPDATE chars SET assault = ?, campaign = ?, eminence = ? WHERE charid = ?",
            BoundValue.Of(a.Bytes), BoundValue.Of(k.Bytes), BoundValue.Of(e.Bytes), BoundValue.Of(charId));
    }

    // a fixture char logs in to Port Bastok inside the Rhapsodies of Vana'diel opening cutscene (event 30035), which the RoE packets refuse
    // (BlockedState.InEvent); the char leaves it first, as a player finishing it would
    private static void Roe(ushort opcode, ushort record)
    {
        c.CurrentEvent = new EventInfo();
        Send(opcode, 8, buf => BitConverter.GetBytes(record).CopyTo(buf, 4));
    }

    private static void RunC1()
    {
        Console.WriteLine("assault, campaign and eminence rows");
        RunC1Load();
        RunC1Writes();
        RunC1Drift();
        RunC1Upgrade();
        RunC1Failures();
        RunC1Off();
        RunC1Delete();
        RunC1TimedDuplicate();
    }

    private static void RunC1Load()
    {
        Fixture(C1Char, null, null);
        C1Seed(C1Char);
        Mark();
        Login();
        Check("a first login imports the three kinds", "progress: char 996144 imported/1/1/2",
              $"{ProgressLines()}/{Count("char_assault_state")}/{Count("char_campaign_state")}/{Count("char_eminence_state")}");
        Check("  the rows == the blobs", "True/True/True", C1RowsMatchLegacy());
        Check("  assault", "7|1", C1AssaultRow(3));
        Check("  record 2 active, record 11 completed only", "0|7|0/255|0|1", $"{C1EminenceRow(2)}/{C1EminenceRow(11)}");
        Check("  the arrays hold the rows' projection", $"{Convert.ToHexString(DbBlob("assault"))}/{Convert.ToHexString(DbBlob("campaign"))}/{Convert.ToHexString(DbBlob("eminence"))}",
              $"{Convert.ToHexString(c.AssaultLog)}/{Convert.ToHexString(c.CampaignLog)}/{Convert.ToHexString(c.EminenceLog)}");
        Logout();
        Mark();
        Login();
        Check("the next login finds them in agreement", "", ProgressLines());
    }

    private static void RunC1Writes()
    {
        CharId = C1Char;
        Login();

        Lua("player:addAssault(9)");
        Check("addAssault: row and blob", "9|0/True/True/True", $"{C1AssaultRow(9)}/{C1RowsMatchLegacy()}");
        Lua("player:completeAssault(9)");
        Check("completeAssault: row and blob", "0|1/True/True/True", $"{C1AssaultRow(9)}/{C1RowsMatchLegacy()}");
        Lua("player:addAssault(4)");
        Lua("player:delAssault(4)");
        Check("delAssault: row and blob", "0|0/True/True/True", $"{C1AssaultRow(4)}/{C1RowsMatchLegacy()}");

        Roe(0x10C, 3);
        Check("a record taken: row and blob", "1|0|0/True/True/True", $"{C1EminenceRow(3)}/{C1RowsMatchLegacy()}");

        // within RoeCacheTimeMs of that write: neither side is written, the array is
        Lua("player:setEminenceProgress(3, 4)");
        Check("progress inside the 15 s limit: in the array, in neither store", "4/1|0|0/True/True/True",
              $"{Lua("report(tostring(player:getEminenceProgress(3)))")}/{C1EminenceRow(3)}/{C1RowsMatchLegacy()}");

        // record 11 has its completion cleared: nothing is left of it, and the write carries record 3's progress
        Lua("player:setEminenceCompleted(11, true, false)");
        Check("a completion cleared: its row goes, the held progress lands in both", "-/1|4|0/True/True/True",
              $"{C1EminenceRow(11)}/{C1EminenceRow(3)}/{C1RowsMatchLegacy()}");

        Lua("player:setEminenceCompleted(3, true, true)");
        Check("a record completed, kept active: row and blob", "1|0|1/True/True/True", $"{C1EminenceRow(3)}/{C1RowsMatchLegacy()}");

        Roe(0x10D, 2);
        Check("a record removed: its row goes, record 3 moves up a slot", "-/0|0|1/True/True/True", $"{C1EminenceRow(2)}/{C1EminenceRow(3)}/{C1RowsMatchLegacy()}");

        Roe(0x10D, 3);
        Check("a completed record removed: its row stays, inactive", "255|0|1/True/True/True", $"{C1EminenceRow(3)}/{C1RowsMatchLegacy()}");

        Lua("player:setEminenceCompleted(3, true, false)");
        Check("its completion cleared: nothing left", "-/0/True/True/True", $"{C1EminenceRow(3)}/{Count("char_eminence_state")}/{C1RowsMatchLegacy()}");

        Logout();
        Mark();
        Login();
        Check("a login after the writes finds them in agreement", "", ProgressLines());
        Logout();
    }

    private static void RunC1Drift()
    {
        CharId = C1Char;
        var a = new AssaultBitBlob();
        a.AddAssault(22);
        Sql(db, "UPDATE chars SET assault = ? WHERE charid = ?", BoundValue.Of(a.Bytes), BoundValue.Of(CharId));
        Mark();
        Login();
        Check("assault edited in SQL: re-imported, the blob wins", "progress: char 996144 re-imported (legacy assault changed)/22|0/True/True/True",
              $"{ProgressLines()}/{C1AssaultRow(3)}/{C1RowsMatchLegacy()}");
        Logout();

        var k = new CampaignBitBlob();
        k.RawSetCurrent(33);
        Sql(db, "UPDATE chars SET campaign = ? WHERE charid = ?", BoundValue.Of(k.Bytes), BoundValue.Of(CharId));
        Mark();
        Login();
        Check("campaign edited in SQL: re-imported", "progress: char 996144 re-imported (legacy campaign changed)/True/True/True", $"{ProgressLines()}/{C1RowsMatchLegacy()}");
        Logout();

        var e = new EminenceBitBlob();
        e.Add(4);
        Sql(db, "UPDATE chars SET eminence = ? WHERE charid = ?", BoundValue.Of(e.Bytes), BoundValue.Of(CharId));
        Mark();
        Login();
        Check("eminence edited in SQL: re-imported", "progress: char 996144 re-imported (legacy eminence changed)/0|0|0/True/True/True",
              $"{ProgressLines()}/{C1EminenceRow(4)}/{C1RowsMatchLegacy()}");
        Logout();

        // the canonical form: a raw completion byte reads as its flag and compares equal to it from then on
        var raw = a.Bytes;
        raw[2 + 5] = 2;
        Sql(db, "UPDATE chars SET assault = ? WHERE charid = ?", BoundValue.Of(raw), BoundValue.Of(CharId));
        Mark();
        Login();
        Check("a raw completion byte: re-imported, canonical in the array", "progress: char 996144 re-imported (legacy assault changed)/1",
              $"{ProgressLines()}/{c.AssaultLog[2 + 5]}");
        Logout();
        Mark();
        Login();
        Check("  and in agreement with the canonical row at the next login", "", ProgressLines());
        Logout();
    }

    private static void RunC1Upgrade()
    {
        Fixture(C1Upgrade, null, null);
        C1Seed(C1Upgrade);
        Login();
        Logout();
        foreach (var t in new[] { "char_assault_state", "char_campaign_state", "char_eminence_state" })
            Sql(db, $"DELETE FROM {t} WHERE charid = ?", BoundValue.Of(CharId));
        Mark();
        Login();
        Check("a char with quest and mission rows only gains the new ones", "progress: char 996145 re-imported (legacy assault,campaign,eminence changed)/1/1/2/True/True/True",
              $"{ProgressLines()}/{Count("char_assault_state")}/{Count("char_campaign_state")}/{Count("char_eminence_state")}/{C1RowsMatchLegacy()}");
        Logout();
        foreach (var t in new[] { "char_assault_state", "char_campaign_state", "char_eminence_state" })
            Sql(db, $"DELETE FROM {t} WHERE charid = ?", BoundValue.Of(CharId));
        Sql(db, "UPDATE chars SET assault = ?, campaign = ?, eminence = ? WHERE charid = ?",
            BoundValue.Of(new byte[AssaultBitBlob.Size]), BoundValue.Of(new byte[CampaignBitBlob.Size]), BoundValue.Of(new byte[EminenceBitBlob.Size]), BoundValue.Of(CharId));
        Mark();
        Login();
        Check("  with empty blobs: assault and campaign named, no eminence row", "progress: char 996145 re-imported (legacy assault,campaign changed)/1/1/0",
              $"{ProgressLines()}/{Count("char_assault_state")}/{Count("char_campaign_state")}/{Count("char_eminence_state")}");
        Logout();
    }

    private static void RunC1Failures()
    {
        CharId = C1Upgrade;
        Login();
        var flaky = new FlakyDatabase(db);
        var rows = new ProgressRows(flaky) { Log = log.Add, Registry = loader.Progress.Registry };

        // a read that fails: the legacy state is used
        flaky.FailWhen = q => q.Contains("FROM char_eminence_state");
        Mark();
        rows.Load(c);
        Check("an eminence read fails: the legacy state, logged", "True/progress: char 996145: its rows could not be read; the legacy state is used",
              $"{c.Progress == null}/{ProgressLines()}");
        flaky.FailWhen = _ => false;
        Logout();
        Login();

        // the rows fail after a good legacy write: rolled back, the legacy write again alone, rows off
        c.EminenceLog[188 + 12 / 8] |= 1 << 12 % 8;
        flaky.FailWhen = q => q.StartsWith("INSERT INTO char_eminence_state");
        Mark();
        rows.Persist(c, () => flaky.Execute("UPDATE chars SET eminence = ? WHERE charid = ? LIMIT 1", new[] { BoundValue.Of(c.EminenceLog), BoundValue.Of(CharId) }) != null,
                     p => p.SyncEminence(c.EminenceLog));
        Check("a failed row write: legacy written, no row, rows off, logged",
              "True/-/True/progress: char 996145: writing its rows failed; its rows are off until its next load",
              $"{(DbBlob("eminence")[188 + 12 / 8] & 1 << 12 % 8) != 0}/{C1EminenceRow(12)}/{c.Progress == null}/{ProgressLines()}");
        flaky.FailWhen = _ => false;
        Logout();
        Mark();
        Login();
        Check("  the next login rebuilds them", "progress: char 996145 re-imported (legacy eminence changed)/255|0|1/True/True/True",
              $"{ProgressLines()}/{C1EminenceRow(12)}/{C1RowsMatchLegacy()}");
        Logout();
    }

    private static void RunC1Off()
    {
        Fixture(C1Off, null, null);
        loader.Progress.Enabled = false;
        try
        {
            Login();
            Lua("player:addAssault(5)");
            Roe(0x10C, 2);
            Logout();
            Check("the switch off: no row read or written, the blobs written", "0/0/0/True",
                  $"{Count("char_assault_state")}/{Count("char_campaign_state")}/{Count("char_eminence_state")}/{DbBlob("assault")[0] == 5 && DbBlob("eminence")[0] == 2}");
        }
        finally
        {
            loader.Progress.Enabled = true;
        }
    }

    // one record id in two active slots, reached through the engine: a timed id in slot 30 (RoeOnCharLoad leaves it alone), then 0x10C takes it into
    // slot 0 as well (the timed guard never fires; AddEminenceRecord scans slots 0..29). A row holds the last slot, so the zone must not project
    // eminence onto its array: the blob keeps both slots through every save and load.
    private static void RunC1TimedDuplicate()
    {
        const ushort record = 4019;
        var e = new EminenceBitBlob();
        e.RawSetSlot(30, record, 2);
        Fixture(C1Timed, null, null);
        Sql(db, "UPDATE chars SET eminence = ? WHERE charid = ?", BoundValue.Of(e.Bytes), BoundValue.Of(C1Timed));
        Login();
        Roe(0x10C, record);
        var viaEngine = new EminenceBitBlob();
        viaEngine.LoadFromBlob(c.EminenceLog);
        Check("a timed id in slot 30: 0x10C takes it into slot 0 as well", $"{record}/{record}", $"{viaEngine.RawGetActive(0)}/{viaEngine.RawGetActive(30)}");
        Lua($"player:setEminenceProgress({record}, 5)");
        Logout();
        var stored = new EminenceBitBlob();
        stored.LoadFromBlob(DbBlob("eminence"));
        Check("  the stored blob keeps both slots, with their progress", $"{record}/{record}/5/2",
              $"{stored.RawGetActive(0)}/{stored.RawGetActive(30)}/{stored.RawGetProgress(0)}/{stored.RawGetProgress(30)}");
        var before = Convert.ToHexString(DbBlob("eminence"));
        Mark();
        Login();
        Check("  the next login logs no progress line", "", ProgressLines());
        Check("  the array is byte-identical to the stored blob", before, Convert.ToHexString(c.EminenceLog));
        Check("  the row holds the last slot", "30|2|0", C1EminenceRow(record));
        Logout();
        Check("  and the stored blob is still byte-identical after the save", before, Convert.ToHexString(DbBlob("eminence")));
        // clearing slot 0 leaves slot 30, and the row stays
        Login();
        Roe(0x10D, record);
        Logout();
        stored.LoadFromBlob(DbBlob("eminence"));
        Check("  removing it from slot 0 leaves slot 30 with its progress", "0/4019/2", $"{stored.RawGetActive(0)}/{stored.RawGetActive(30)}/{stored.RawGetProgress(30)}");
        Check("  and the row is still the last slot", "30|2|0", C1EminenceRow(record));
        Mark();
        Login();
        Check("  the next login logs no progress line", "", ProgressLines());
        Logout();
    }

    private static void RunC1Delete()
    {
        Fixture(C1Deleted, null, null);
        C1Seed(C1Deleted);
        Login();
        Logout();
        Check("before the delete", "1/1/2", $"{Count("char_assault_state")}/{Count("char_campaign_state")}/{Count("char_eminence_state")}");
        Sql(db, "DELETE FROM chars WHERE charid = ?", BoundValue.Of(CharId));
        Check("char_delete removes the three kinds", "0/0/0", $"{Count("char_assault_state")}/{Count("char_campaign_state")}/{Count("char_eminence_state")}");
    }
}
