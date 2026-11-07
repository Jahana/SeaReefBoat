// XI.Verify.Quests -- quest rework C1: the assault, campaign and eminence rows, offline. Campaign has no compiled oracle
// (nothing in the C++ changes m_campaignLog), so its gate is a round trip over synthetic blobs.
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;

internal static partial class Program
{
    static void RunC1Campaign()
    {
        Console.WriteLine("\n== C1: the campaign blob and its row ==");
        string RoundTrip(byte[] blob) => CampaignWireCodec.Project(CampaignStateImporter.ToCell(CampaignStateImporter.Import(7, blob))).ToHex();

        Check("c1 campaign: the blob is campaignlog_t", "514", CampaignBitBlob.Size.ToString());
        var empty = new byte[CampaignBitBlob.Size];
        Check("c1 campaign: all zero round trips", Convert.ToHexString(empty), RoundTrip(empty));

        var rich = new CampaignBitBlob();
        rich.RawSetCurrent(0x1234);
        rich.RawSetComplete(0, true);
        rich.RawSetComplete(7, true);
        rich.RawSetComplete(8, true);
        rich.RawSetComplete(511, true);
        Check("c1 campaign: the current id and slots 0, 7, 8, 511 round trip", rich.ToHex(), RoundTrip(rich.Bytes));
        var row = CampaignStateImporter.Import(7, rich.Bytes);
        Check("c1 campaign: the row", "7/4660/64/True/False", $"{row.CharId}/{row.Current}/{row.CompletedMask.Length}/{row.ToCell().Completed(511)}/{row.ToCell().Completed(510)}");

        var raw = rich.Bytes;
        raw[2 + 5] = 7; // a raw byte the engine never writes
        var canonical = new CampaignBitBlob();
        canonical.LoadFromBlob(rich.Bytes);
        canonical.RawSetComplete(5, true);
        Check("c1 campaign: a raw non-0/1 byte takes its canonical form", canonical.ToHex(), RoundTrip(raw));

        Check("c1 campaign: a short blob zero-fills", "0100" + new string('0', 512 * 2), RoundTrip(new byte[] { 1 }));
        Check("c1 campaign: a null blob is empty", Convert.ToHexString(empty), RoundTrip(null));

        var legacy = new LegacyCharRow
        {
            CharId = 7, QuestsBlob = new QuestBitBlob().Bytes, MissionsBlob = new MissionBitBlob().Bytes, AssaultBlob = new AssaultBitBlob().Bytes,
            CampaignBlob = rich.Bytes, EminenceBlob = new EminenceBitBlob().Bytes, KeyitemsBlob = new byte[1024],
        };
        Check("c1 campaign: the migration seam imports and projects it", rich.ToHex(),
              Convert.ToHexString(CharacterStateMigration.Project(CharacterStateMigration.Import(legacy, null)).CampaignBlob));
    }

    static void RunC1Sync()
    {
        Console.WriteLine("\n== C1: CharProgress keeps the assault, campaign and eminence rows ==");
        var quests = new QuestBitBlob().Bytes;
        var missions = new MissionBitBlob().Bytes;
        var a = new AssaultBitBlob();
        var k = new CampaignBitBlob();
        var e = new EminenceBitBlob();
        var p = CharProgress.Import(7, quests, missions, null, null, a.Bytes, k.Bytes, e.Bytes);
        Check("c1 sync: the rows of an empty char", "True/True/0", $"{p.Assault != null}/{p.Campaign != null}/{p.Eminence.Count}");

        void Assault(string name, Action op)
        {
            op();
            var ch = p.SyncAssault(a.Bytes);
            Check("c1 sync: assault " + name, $"True/{a.ToHex()}", $"{ch.AssaultUpsert != null}/{p.ProjectAssault().ToHex()}");
            Check("c1 sync: assault " + name + ", again changes nothing", "True", p.SyncAssault(a.Bytes).IsEmpty.ToString());
        }
        Assault("add", () => a.AddAssault(5));
        Assault("complete", () => a.CompleteAssault(5));
        Assault("add another", () => a.AddAssault(127));
        Assault("delete", () => a.DelAssault(127));

        k.RawSetCurrent(3);
        k.RawSetComplete(511, true);
        Check("c1 sync: campaign", $"True/{k.ToHex()}", $"{p.SyncCampaign(k.Bytes).CampaignUpsert != null}/{p.ProjectCampaign().ToHex()}");
        Check("c1 sync: campaign, again changes nothing", "True", p.SyncCampaign(k.Bytes).IsEmpty.ToString());

        void Eminence(string name, string changes, Action op)
        {
            op();
            var ch = p.SyncEminence(e.Bytes);
            Check("c1 sync: eminence " + name, $"{changes}/{e.ToHex()}", $"{ch.EminenceUpserts.Count} up {ch.EminenceDeletes.Count} del/{p.ProjectEminence().ToHex()}");
            Check("c1 sync: eminence " + name + ", again changes nothing", "True", p.SyncEminence(e.Bytes).IsEmpty.ToString());
        }
        Eminence("three taken", "3 up 0 del", () => { e.Add(1); e.Add(2); e.Add(3); });
        Eminence("progress", "1 up 0 del", () => e.SetProgress(2, 7));
        Eminence("completed", "1 up 0 del", () => e.SetCompletion(2, true));
        Eminence("removed: the rest move up a slot", "2 up 1 del", () => e.Del(1));
        Eminence("removed but completed: its row stays, inactive", "2 up 0 del", () => e.Del(2));
        Eminence("completion cleared: nothing left of it", "0 up 1 del", () => e.SetCompletion(2, false));
        Eminence("three completed", "3 up 0 del", () => { e.SetCompletion(10, true); e.SetCompletion(11, true); e.SetCompletion(12, true); });
        Eminence("a reset clears several in one change", "0 up 3 del", () => { e.SetCompletion(10, false); e.SetCompletion(11, false); e.SetCompletion(12, false); });
        Check("c1 sync: the eminence rows left", "3", string.Join(",", p.Eminence.Keys));

        var both = p.SyncMissionsList(missions, a.Bytes, k.Bytes);
        Check("c1 sync: the missions list, nothing changed", "True", both.IsEmpty.ToString());
        a.AddAssault(9);
        k.RawSetCurrent(4);
        var mb = new MissionBitBlob();
        mb.RawSetArea(0, 11, 0);
        both = p.SyncMissionsList(mb.Bytes, a.Bytes, k.Bytes);
        Check("c1 sync: the missions list carries all three", "1/True/True", $"{both.MissionUpserts.Count}/{both.AssaultUpsert != null}/{both.CampaignUpsert != null}");
        Check("c1 sync: IsEmpty sees each new part", "False/False/False/False",
              $"{new ProgressChanges { AssaultUpsert = p.Assault }.IsEmpty}/{new ProgressChanges { CampaignUpsert = p.Campaign }.IsEmpty}/" +
              $"{Upserted(p).IsEmpty}/{Deleted(p).IsEmpty}");

        // Import == the migration seam
        var state = CharacterStateMigration.Project(CharacterStateMigration.Import(new LegacyCharRow
        {
            CharId = 7, QuestsBlob = quests, MissionsBlob = missions, AssaultBlob = a.Bytes, CampaignBlob = k.Bytes, EminenceBlob = e.Bytes, KeyitemsBlob = new byte[1024],
        }, null));
        var imported = CharProgress.Import(7, quests, missions, null, null, a.Bytes, k.Bytes, e.Bytes);
        Check("c1 sync: Import == the migration seam",
              $"{Convert.ToHexString(state.AssaultBlob)}/{Convert.ToHexString(state.CampaignBlob)}/{Convert.ToHexString(state.EminenceBlob)}",
              $"{imported.ProjectAssault().ToHex()}/{imported.ProjectCampaign().ToHex()}/{imported.ProjectEminence().ToHex()}");

        // FromRows: no assault or campaign row reads as the empty state
        var bare = CharProgress.FromRows(7, new List<CharQuestStateRow>(), new List<CharMissionStateRow>());
        Check("c1 sync: FromRows without the new rows", $"{new AssaultBitBlob().ToHex()}/{new CampaignBitBlob().ToHex()}/0",
              $"{bare.ProjectAssault().ToHex()}/{bare.ProjectCampaign().ToHex()}/{bare.Eminence.Count}");

        // Diff
        string DiffOf(AssaultBitBlob aa, CampaignBitBlob kk, EminenceBitBlob ee, bool noA = false, bool noK = false) =>
            CharProgress.Import(7, quests, missions, null, null, aa.Bytes, kk.Bytes, ee.Bytes)
                .Diff(CharProgress.Import(7, quests, missions, null, null, a.Bytes, k.Bytes, e.Bytes), noA, noK);
        Check("c1 sync: Diff of the same state", "", DiffOf(a, k, e));
        var a2 = new AssaultBitBlob();
        var k2 = new CampaignBitBlob();
        var e2 = new EminenceBitBlob();
        e2.Add(40);
        Check("c1 sync: Diff names each part, in order", "assault,campaign,eminence", DiffOf(a2, k2, e2));
        Check("c1 sync: Diff of assault alone", "assault", DiffOf(a2, k, e));
        Check("c1 sync: Diff of campaign alone", "campaign", DiffOf(a, k2, e));
        Check("c1 sync: Diff of eminence alone", "eminence", DiffOf(a, k, e2));
        Check("c1 sync: a missing row is named even when the blob agrees", "assault,campaign", DiffOf(a, k, e, true, true));
        Check("c1 sync: a missing row and a changed blob: named once", "assault", DiffOf(a2, k, e, true));
        // a hand edit: the same record in two slots, and a raw byte, compare equal to their canonical form
        var dup = new EminenceBitBlob();
        dup.RawSetSlot(0, 40, 2);
        dup.RawSetSlot(1, 40, 2);
        var one = new EminenceBitBlob();
        one.RawSetSlot(1, 40, 2);
        Check("c1 sync: a record in two slots == its canonical form", "",
              CharProgress.Import(7, quests, missions, null, null, null, null, dup.Bytes).Diff(CharProgress.Import(7, quests, missions, null, null, null, null, one.Bytes)));
        var rawByte = new AssaultBitBlob().Bytes;
        rawByte[2 + 5] = 2;
        var flag = new AssaultBitBlob().Bytes;
        flag[2 + 5] = 1;
        Check("c1 sync: a raw completion byte == its canonical form", "",
              CharProgress.Import(7, quests, missions, null, null, rawByte).Diff(CharProgress.Import(7, quests, missions, null, null, flag)));

        // ProjectInto
        var qOut = new byte[QuestBitBlob.Size];
        var mOut = new byte[MissionBitBlob.Size];
        var aOut = new byte[AssaultBitBlob.Size];
        var kOut = new byte[CampaignBitBlob.Size];
        imported.ProjectInto(qOut, mOut, aOut, kOut);
        Check("c1 sync: ProjectInto fills all four", $"{a.ToHex()}/{k.ToHex()}",
              $"{Convert.ToHexString(aOut)}/{Convert.ToHexString(kOut)}");

        // a record in slot 30 (a timed id): one row, byte-equal projection; the same id in slot 0 as well (the engine reaches it,
        // 0x10C's timed guard never fires) changes nothing a row can hold
        var s30 = new EminenceBitBlob();
        s30.RawSetSlot(30, 4019, 3);
        var timed = CharProgress.Import(7, quests, missions, null, null, null, null, null);
        var timedCh = timed.SyncEminence(s30.Bytes);
        Check("c1 sync: a record in slot 30 syncs as one upsert", "1/0", $"{timedCh.EminenceUpserts.Count}/{timedCh.EminenceDeletes.Count}");
        Check("c1 sync: a record in slot 30 projects byte-equal", s30.ToHex(), timed.ProjectEminence().ToHex());
        var both30 = new EminenceBitBlob();
        both30.RawSetSlot(0, 4019, 3);
        both30.RawSetSlot(30, 4019, 3);
        Check("c1 sync: the same id in slots 0 and 30 changes nothing a row holds", "True", timed.SyncEminence(both30.Bytes).IsEmpty.ToString());
    }

    private static ProgressChanges Upserted(CharProgress p)
    {
        var ch = new ProgressChanges();
        ch.EminenceUpserts.Add(new CharEminenceStateRow { CharId = p.CharId, RecordId = 1 });
        return ch;
    }

    private static ProgressChanges Deleted(CharProgress p)
    {
        var ch = new ProgressChanges();
        ch.EminenceDeletes.Add(new CharEminenceStateRow { CharId = p.CharId, RecordId = 1 });
        return ch;
    }

    static void RunC1Store()
    {
        Console.WriteLine("\n== C1: QuestStateStore and the three tables ==");
        var a = new AssaultBitBlob();
        a.AddAssault(4);
        var k = new CampaignBitBlob();
        k.RawSetCurrent(3);
        k.RawSetComplete(511, true);
        var e = new EminenceBitBlob();
        e.Add(2);
        e.SetProgress(2, 9);
        e.SetCompletion(11, true);
        var p = CharProgress.Import(42, new QuestBitBlob().Bytes, new MissionBitBlob().Bytes, null, null, a.Bytes, k.Bytes, e.Bytes);

        var all = new RecordingDb();
        Check("c1 store: ReplaceAll succeeds", "True", QuestStateStore.ReplaceAll(all, p).ToString());
        string[] Args(RecordingDb db, string head) =>
            db.Ran.Where(r => r.Sql.StartsWith(head)).Select(r => ArgsText(r.Args)).ToArray();
        Check("c1 store: ReplaceAll deletes every kind first", "char_quest_state,char_mission_state,char_assault_state,char_campaign_state,char_eminence_state",
              string.Join(",", all.Ran.Take(5).Select(r => r.Sql.Split(' ')[2])));
        Check("c1 store: placeholders == bound values", "True", all.Ran.All(r => Placeholders(r.Sql) == r.Args.Length).ToString());
        Check("c1 store: no ON DUPLICATE in ReplaceAll's inserts", "True", all.Ran.All(r => !r.Sql.Contains("ON DUPLICATE")).ToString());

        var single = new RecordingDb();
        var ch = new ProgressChanges { AssaultUpsert = p.Assault, CampaignUpsert = p.Campaign };
        ch.EminenceUpserts.AddRange(p.Eminence.Values);
        ch.EminenceDeletes.Add(new CharEminenceStateRow { CharId = 42, RecordId = 99 });
        Check("c1 store: Apply succeeds", "True", QuestStateStore.Apply(single, ch).ToString());
        Check("c1 store: placeholders == bound values in Apply", "True", single.Ran.All(r => Placeholders(r.Sql) == r.Args.Length).ToString());
        Check("c1 store: Apply deletes first", "DELETE FROM char_eminence_state", string.Join(" ", single.Ran[0].Sql.Split(' ').Take(3)));
        Check("c1 store: the delete's binds", "UInt32:42,UInt32:99", ArgsText(single.Ran[0].Args));
        Check("c1 store: assault binds what ReplaceAll binds", string.Join("|", Args(all, "INSERT INTO char_assault_state")), string.Join("|", Args(single, "INSERT INTO char_assault_state")));
        Check("c1 store: campaign binds what ReplaceAll binds", string.Join("|", Args(all, "INSERT INTO char_campaign_state")), string.Join("|", Args(single, "INSERT INTO char_campaign_state")));
        var batched = all.Ran.Where(r => r.Sql.StartsWith("INSERT INTO char_eminence_state")).SelectMany(r => r.Args).ToArray();
        var upserts = single.Ran.Where(r => r.Sql.StartsWith("INSERT INTO char_eminence_state")).SelectMany(r => r.Args).ToArray();
        Check("c1 store: eminence binds what ReplaceAll binds", ArgsText(upserts), ArgsText(batched));
        Check("c1 store: an inactive record binds slot 255", "UInt8:0/UInt8:255", $"{ArgsText(new[] { batched[2] })}/{ArgsText(new[] { batched[7] })}");
        Check("c1 store: the load SQL names the columns the store reads", "True",
              (QuestStateStore.SqlLoadAssault.Contains("completed_mask") && QuestStateStore.SqlLoadCampaign.Contains("completed_mask") && QuestStateStore.SqlLoadEminence.Contains("record_id")).ToString());

        var empty = new RecordingDb();
        Check("c1 store: no row read: true, null", "True/True",
              $"{QuestStateStore.TryLoadAssault(empty, 42, out var none)}/{none == null}");
        Check("c1 store: no eminence rows: empty, not null", "0", QuestStateStore.LoadEminence(empty, 42).Count.ToString());
    }
}
