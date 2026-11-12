// XI.Verify.Quests — Phase-6 wire-projection gate.
//
// Replays every scenario from csharp/oracle/quest_golden.txt (produced by compiling
// the ENGINE'S REAL questlog_t bit-addressing verbatim: addQuest/completeQuest/delQuest/
// delCurrentQuest/getQuestStatus) through the C# QuestBitBlob + QuestWireCodec and diffs:
//   1) META size            -> QuestBitBlob.Size
//   2) resulting 704B blob  -> blob.ToHex() (bit addressing parity, the 0x05F/0x056 contract)
//   3) each STAT cell       -> GetStatus(area,quest) (completed-wins precedence)
//   4) Extract/Project      -> status-preserving + idempotent; the (1,1) pair is asserted
//                              as a documented LOSSY edge (only the current bit is dropped,
//                              status is preserved), never silently hidden.
//
// Arguments: [<golden file>] [--only <prefix>[,<prefix>...]]... (quest rework P8): with --only (repeatable, or one
// comma-separated list), only the checks whose name starts with one of the prefixes count and print ("p1 args",
// "windurst g1"; a prefix ending in a digit does not count a name whose number goes on: "outlands g1" runs no
// "outlands g10:" check, PrefixCounts); every other line is left out too, the RESULT line says how many ran and that
// the run was filtered, and a filter that no check matches fails the run (a typo cannot pass). The generator gate
// (python, about 40 s) runs only when a prefix could match its checks, all named gen:... (OnlySkips; an unfiltered run
// checks they are). Without --only nothing changes.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using XI.Quests;
using XI.Quests.Authoring;
using XI.Common;
using XI.Persistence;

internal static partial class Program
{
    static int _pass, _fail;

    enum OpKind { Add, Complete, DelCur, Del, Raw }

    sealed class Op { public int Area; public int Quest; public OpKind Kind; public bool Cur; public bool Comp; }
    sealed class Stat { public int Area; public int Quest; public int Status; }
    sealed class Scen
    {
        public string Name;
        public List<Op> Ops = new();
        public string Blob;
        public List<Stat> Stats = new();
    }

    static int Main(string[] argv)
    {
        var rest = new List<string>();
        for (int i = 0; i < argv.Length; i++)
        {
            if (argv[i] != "--only") { rest.Add(argv[i]); continue; }
            if (i + 1 >= argv.Length || argv[i + 1].Length == 0)
            {
                Console.WriteLine("--only takes a check-name prefix (or a comma-separated list): --only \"p1 args\"");
                return 1;
            }
            only_ ??= new List<string>();
            foreach (var prefix in argv[++i].Split(','))
            {
                if (prefix.Length == 0)
                {
                    Console.WriteLine($"--only: an empty prefix in {argv[i]}");
                    return 1;
                }
                only_.Add(prefix);
            }
        }
        // the filtered run's other lines (section headers, details) go nowhere; the counted checks to the console
        if (only_ != null)
        {
            out_ = Console.Out;
            Console.SetOut(TextWriter.Null);
        }
        try
        {
            return Run(rest.ToArray());
        }
        finally
        {
            if (only_ != null) Console.SetOut(out_);
        }
    }

    // the RESULT line; filtered (--only): how many checks ran, and none is a failure
    static int Result(string tail)
    {
        var (line, code) = ResultLine(_pass, _fail, tail, only_);
        (out_ ?? Console.Out).WriteLine("\n" + line);
        return code;
    }

    static (string Line, int Code) ResultLine(int pass, int fail, string tail, List<string> only)
    {
        string filter = only == null ? "" : $"  [filtered by --only {string.Join(", ", only.Select(p => $"\"{p}\""))}: {pass + fail} checks ran" +
                                            (pass + fail == 0 ? "; no check matched" : "") + "]";
        return ($"RESULT: {pass} passed, {fail} failed{tail}{filter}", fail == 0 && (only == null || pass > 0) ? 0 : 1);
    }

    // --only's logic, on its pure parts: which names count, which sections it skips, the RESULT line and exit code (a filter
    // that no check matches fails)
    static void RunOnlyFilterGate()
    {
        Console.WriteLine("\n== --only: the check-name filter (quest rework P8) ==");
        var saved = only_;
        only_ = new List<string> { "p1 args", "windurst g1" };
        string counted = $"{Counted("p1 args:x")}/{Counted("windurst g1 facets:y")}/{Counted("p1 arg")}/{Counted("p2 args:x")}/{Counted("P1 args:x")}";
        string skipsGen = $"{OnlySkips(GeneratorPrefix)}";
        only_ = new List<string> { "gen" };
        skipsGen += $"/{OnlySkips(GeneratorPrefix)}";
        only_ = new List<string> { "gen:yaml" };
        skipsGen += $"/{OnlySkips(GeneratorPrefix)}";
        only_ = null;
        skipsGen += $"/{OnlySkips(GeneratorPrefix)}";
        only_ = saved;
        Check("only:a name counts when it starts with a prefix (ordinal, case kept)", "True/True/False/False/False", counted);
        only_ = new List<string> { "outlands g1" };
        string whole = $"{Counted("outlands g1:x")}/{Counted("outlands g1 facets:y")}/{Counted("outlands g10:x")}/" +
                       $"{Counted("outlands g1")}";
        only_ = new List<string> { "outlands g" };
        whole += $"/{Counted("outlands g10:x")}";
        only_ = saved;
        Check("only:a prefix ending in a digit does not count a longer number (g1 is not g10)",
              "True/True/False/True/True", whole);
        Check("only:the generator gate is skipped unless a prefix could name a gen: check (none, gen, gen:yaml, no filter)", "True/False/False/False", skipsGen);
        var none = ResultLine(0, 0, "  (scenarios: 9)", new List<string> { "p1 argz" });
        Check("only:a filter no check matches fails, and says so",
              "1|RESULT: 0 passed, 0 failed  (scenarios: 9)  [filtered by --only \"p1 argz\": 0 checks ran; no check matched]", $"{none.Code}|{none.Line}");
        var some = ResultLine(8, 0, "", new List<string> { "p1 args", "p2" });
        Check("only:a filtered RESULT line says how many ran", "0|RESULT: 8 passed, 0 failed  [filtered by --only \"p1 args\", \"p2\": 8 checks ran]", $"{some.Code}|{some.Line}");
        var plain = ResultLine(8, 1, "", null);
        Check("only:no filter: the RESULT line as before", "1|RESULT: 8 passed, 1 failed", $"{plain.Code}|{plain.Line}");
    }

    static int Run(string[] argv)
    {
        string goldenPath = argv.Length > 0
            ? argv[0]
            : @"H:\023-FFXI-server\csharp\oracle\quest_golden.txt";

        Console.WriteLine("== XI.Quests: wire projection codec vs engine-verbatim oracle ==");
        if (!File.Exists(goldenPath))
        {
            Bad("golden not found: " + goldenPath + " (run oracle/build_quest.bat)");
            Result("");
            return 1;
        }

        // ---- parse golden ----
        var scenarios = new List<Scen>();
        var byName = new Dictionary<string, Scen>();
        Scen Get(string name)
        {
            if (!byName.TryGetValue(name, out var s)) { s = new Scen { Name = name }; byName[name] = s; scenarios.Add(s); }
            return s;
        }

        long expectedSize = 704;
        int metaChecks = 0;
        foreach (string raw in File.ReadAllLines(goldenPath))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("//")) continue;

            var meta = Regex.Match(line, @"^META\s+sizeof_ql\s+(\d+)$");
            if (meta.Success) { expectedSize = long.Parse(meta.Groups[1].Value); metaChecks++; continue; }

            var blob = Regex.Match(line, @"^SCEN\s+(\S+)\s+BLOB\s+([0-9A-Fa-f]+)$");
            if (blob.Success) { Get(blob.Groups[1].Value).Blob = blob.Groups[2].Value.ToUpperInvariant(); continue; }

            var op = Regex.Match(line, @"^SCEN\s+(\S+)\s+OP\s+(\d+)\s+(\d+)\s+(ADD|COMPLETE|DEL_CUR|DEL)$");
            if (op.Success)
            {
                OpKind k = op.Groups[4].Value switch
                {
                    "ADD" => OpKind.Add,
                    "COMPLETE" => OpKind.Complete,
                    "DEL_CUR" => OpKind.DelCur,
                    _ => OpKind.Del,
                };
                Get(op.Groups[1].Value).Ops.Add(new Op { Area = int.Parse(op.Groups[2].Value), Quest = int.Parse(op.Groups[3].Value), Kind = k });
                continue;
            }

            var rw = Regex.Match(line, @"^SCEN\s+(\S+)\s+RAW\s+(\d+)\s+(\d+)\s+([01])\s+([01])$");
            if (rw.Success)
            {
                Get(rw.Groups[1].Value).Ops.Add(new Op
                {
                    Area = int.Parse(rw.Groups[2].Value),
                    Quest = int.Parse(rw.Groups[3].Value),
                    Kind = OpKind.Raw,
                    Cur = rw.Groups[4].Value == "1",
                    Comp = rw.Groups[5].Value == "1",
                });
                continue;
            }

            var st = Regex.Match(line, @"^STAT\s+(\S+)\s+(\d+)\s+(\d+)\s+(\d+)$");
            if (st.Success)
            {
                Get(st.Groups[1].Value).Stats.Add(new Stat
                {
                    Area = int.Parse(st.Groups[2].Value),
                    Quest = int.Parse(st.Groups[3].Value),
                    Status = int.Parse(st.Groups[4].Value),
                });
                continue;
            }

            Console.WriteLine("  [warn] unparsed golden line: " + line);
        }

        // ---- 1) size ----
        Check("META sizeof_ql", expectedSize.ToString(), QuestBitBlob.Size.ToString());

        // ---- 2/3/4) per-scenario ----
        foreach (var sc in scenarios)
        {
            var blob = new QuestBitBlob();
            foreach (var o in sc.Ops)
            {
                switch (o.Kind)
                {
                    case OpKind.Add: blob.AddQuest(o.Area, (ushort)o.Quest); break;
                    case OpKind.Complete: blob.CompleteQuest(o.Area, (ushort)o.Quest); break;
                    case OpKind.DelCur: blob.DelCurrentQuest(o.Area, (ushort)o.Quest); break;
                    case OpKind.Del: blob.DelQuest(o.Area, (ushort)o.Quest); break;
                    case OpKind.Raw: blob.RawSet(o.Area, o.Quest, o.Cur, o.Comp); break;
                }
            }

            // (2) full blob byte-for-byte
            string got = blob.ToHex();
            bool blobEq = got == sc.Blob;
            Check($"blob {sc.Name}", sc.Blob?.Length > 0 ? "match" : "no-blob", blobEq ? "match" : "DIFF");
            if (!blobEq && sc.Blob != null)
                Console.WriteLine($"      want {sc.Blob}\n      got  {got}");

            // (3) each STAT cell
            foreach (var s in sc.Stats)
            {
                var got2 = blob.GetStatus(s.Area, s.Quest);
                Check($"stat {sc.Name} {s.Area}/{s.Quest}", StatusName((QuestStatus)s.Status), StatusName(got2));
            }

            // (4) Extract -> Project: status-preserving + idempotent; detect (1,1) lossy edge.
            var cells = QuestWireCodec.Extract(blob);
            var projected = QuestWireCodec.Project(cells);
            bool canonical = !HasOneOnePair(sc);

            if (canonical)
            {
                Check($"project {sc.Name}", sc.Blob, projected.ToHex());
            }
            else
            {
                // (1,1) present: projection is intentionally lossy in the current bit only.
                Check($"project-lossy {sc.Name}", "differs", got == projected.ToHex() ? "identical" : "differs");
                // status preserved through the lossy projection:
                var cells2 = QuestWireCodec.Extract(projected);
                Check($"project-preserves-status {sc.Name}", cells.Count.ToString(), cells2.Count.ToString());
                foreach (var c in cells)
                {
                    bool found = false;
                    foreach (var d in cells2)
                        if (d.Area == c.Area && d.Id == c.Id && d.Status == c.Status) { found = true; break; }
                    if (!found) Bad($"project dropped status {sc.Name} {c.Area}/{c.Id}");
                }
                Ok($"project status-set preserved {sc.Name}");
            }

            // idempotency: Project(Extract(Project(Extract(x)))) == Project(Extract(x))
            var cells3 = QuestWireCodec.Extract(projected);
            var projected2 = QuestWireCodec.Project(cells3);
            Check($"idempotent {sc.Name}", projected.ToHex(), projected2.ToHex());
        }

        RunAuthoringGate();
        RunImporterGate(scenarios);
        RunExpiryGate();
        RunCharProgressGate();
        RunMalformedRowGate();
        RunQuestKeysGate();
        RunStoreBatchGate();
        RunQuestScriptGate();
        RunFacets();
        RunAnyStatusAndLocals();
        RunPlatformP1();
        RunPlatformP2();
        RunWindurstGroup1();
        RunWindurstGroup2();
        RunWindurstGroup3();
        RunWindurstGroup4();
        RunWindurstGroup5();
        RunWindurstGroup6();
        RunWindurstGroup7();
        RunWindurstGroup8();
        RunWindurstGroup10();
        RunWindurstGroup11();
        RunWindurstGroup12();
        RunWindurstGroup13();
        RunWindurstGroup14();
        RunWindurstGroup15();
        RunWindurstGroup16();
        RunWindurstGroup17();
        RunWindurstGroup18();
        RunWindurstGroup19();
        RunWindurstGroup20();
        RunWindurstGroup21();
        RunWindurstGroup22();
        RunWindurstGroup23();
        RunWindurstGroup24();
        RunWindurstGroup26();
        RunWindurstGroup27();
        RunWindurstGroup29();
        RunWindurstGroup30();
        RunWindurstGroup31();
        RunWindurstGroup32();
        RunOtherAreasGroup1();
        RunOtherAreasGroup2();
        RunOtherAreasGroup3();
        RunOtherAreasGroup4();
        RunOtherAreasGroup5();
        RunOtherAreasGroup6();
        RunOtherAreasGroup7();
        RunOtherAreasGroup8();
        RunOtherAreasGroup9();
        RunOtherAreasGroup10();
        RunOtherAreasGroup11();
        RunOtherAreasGroup12();
        RunSandoriaGroup12();
        RunOutlandsGroup1();
        RunOutlandsGroup2();
        RunOutlandsGroup3();
        RunOutlandsGroup4();
        RunOutlandsGroup5();
        RunOutlandsGroup6();
        RunOutlandsGroup7();
        RunOutlandsGroup8();
        RunOutlandsGroup9();
        RunOutlandsGroup10();
        RunPlatformP4();
        RunPlatformP5();
        RunPlatformP6();
        RunPlatformP7();
        RunPlatformP8();
        RunPlatformP9();
        RunPlatformP10();
        RunDeferredGroup1();
        RunCrystalWarGroup1();
        RunCrystalWarGroup12();
        RunCrystalWarGroup2();
        RunCrystalWarGroup3();
        RunCrystalWarGroup4();
        RunCrystalWarGroup5();
        RunCrystalWarGroup6();
        RunCrystalWarGroup7();
        RunCrystalWarGroup8();
        RunCrystalWarGroup9();
        RunCrystalWarGroup10();
        RunCrystalWarGroup11();
        RunJenoLevelGate();
        RunJeunoGroup15();
        RunJeunoGroup16();
        RunJeunoGroup17();
        RunJeunoGroup18();
        RunJeunoGroup19();
        RunJeunoGroup20();
        RunAhtUrhganGroup1();
        RunAhtUrhganGroup2();
        RunAhtUrhganGroup3();
        RunAhtUrhganGroup4();
        RunAhtUrhganGroup5();
        RunAhtUrhganGroup6();
        RunAhtUrhganGroup7();
        RunAhtUrhganGroup8();
        RunAhtUrhganGroup9();
        RunAhtUrhganGroup10();
        RunAhtUrhganGroup11();
        RunHiddenGroup1();
        RunHiddenGroup2();
        RunHiddenGroup3();
        RunHiddenGroup4();
        RunHiddenGroup5();
        RunAbysseaGroup1();
        RunAbysseaGroup2();
        RunAbysseaGroup3();
        RunAbysseaGroup4();
        RunAbysseaGroup5();
        RunAdoulinGroup1();
        RunAdoulinGroup2();
        RunAdoulinGroup7();

        // quest-b3b-hidden-3 (Adoulin groups 3, 4)
        RunAdoulinGroup3();
        RunAdoulinGroup4();

        // quest rework B3b hidden-2 lane (Adoulin groups 6, 8 and 5)
        RunAdoulinGroup6();
        RunAdoulinGroup8();
        RunAdoulinGroup5();

        // quest rework B3b San d'Oria-3 lane (San d'Oria group 14)
        RunSandoriaGroup14();

        // quest rework B3b San d'Oria-4 lane (San d'Oria groups 15 and 16)
        RunSandoriaGroup15();
        RunSandoriaGroup16();

        // the no-log quests lane (The Sahagin's Key, All the Way to the Bank)
        RunNoLogQuests();

        // carry-quests-1: the shared npcUtil ports; every C# quest's LegacyPath (unique, its log's folder, a Lua file
        // naming the quest)
        RunNpcUtilChecks();
        RunLegacyPathGate();
        // quest rework C2: the shared container core (ContainerScript under QuestScript)
        RunC2Core();
        // quest rework C2 Task 3: MissionScript and q.Mission
        RunC2Mission();
        // quest rework C2 Task 7: Bastok 1-1, CoP 1-1 and RoV 1-1 on C#
        RunC2MissionPorts();
        RunC2Battlefields();
        // quest rework C4: the records against the Lua dump
        RunC4Records();
        RunC4Checks();
        // quest rework C3 Prep: the platform carries
        RunC3Prep();
        RunC3Cop();
        RunC3CopC3a();
        RunC3CopC3b();
        RunC3CopC3c();
        RunC3CopC4();
        RunC3Bfquest();
        RunC3Toau();
        RunC3ToauT2();
        RunC3ToauT3();
        RunC3ToauT4();
        RunC3ToauT5();
        RunC3ToauT6();
        RunC3ToauT7();
        RunC3ToauT8();
        RunC3RotzC5();
        RunC3RotzC6();
        // quest rework C3 Batch F: the facet members several lanes need
        RunC3Facets();
        RunC3Nations();
        RunCarryQuests1();

        RunOnlyFilterGate();
        RunContentIdsGate();
        RunSchemaFileGate();
        RunC1Campaign();
        RunC1Sync();
        RunC1Store();
        RunDailyGate();
        var missionScens = RunMissionGate();
        RunMissionModelGate(missionScens);
        RunMissionAuthoringGate();
        var assaultScens = RunAssaultGate();
        RunAssaultModelGate(assaultScens);
        var eminenceScens = RunEminenceGate();
        RunEminenceModelGate(eminenceScens);
        var keyitemsScens = RunKeyItemsGate();
        RunKeyItemModelGate(keyitemsScens);
        RunMigrationGate(
            PickBlob(scenarios, "mixed", s => s.Name, s => s.Blob),
            PickBlob(missionScens, "mixed", s => s.Name, s => s.Blob),
            PickBlob(assaultScens, "multi_complete", s => s.Name, s => s.Blob),
            PickBlob(eminenceScens, "mixed", s => s.Name, s => s.Blob),
            PickBlob(keyitemsScens, "mixed", s => s.Name, s => s.Blob));
        RunBindGate(
            PickBlob(scenarios, "mixed", s => s.Name, s => s.Blob),
            PickBlob(missionScens, "mixed", s => s.Name, s => s.Blob),
            PickBlob(assaultScens, "multi_complete", s => s.Name, s => s.Blob),
            PickBlob(eminenceScens, "mixed", s => s.Name, s => s.Blob),
            PickBlob(keyitemsScens, "mixed", s => s.Name, s => s.Blob));

        return Result($"  (scenarios: {scenarios.Count})");
    }

    // ---- char_quest_state importer: offline round-trip parity vs the same golden blobs ----
    // Proves Import(legacy704B + char_vars) -> normalized rows -> ToCells -> Project lands
    // back on the byte-verified canonical wire projection, and that soft state (step,
    // progress JSON) folds in WITHOUT leaking a wire bit (a Prog-only quest with no bit must
    // project to Available). This is the strangler-migration keystone.
    static void RunImporterGate(List<Scen> scenarios)
    {
        Console.WriteLine("\n== XI.Quests: char_quest_state importer (offline round-trip parity) ==");

        // -- DDL / INSERT shape --
        Check("ddl:columns", "9", QuestStateSchema.Columns.Length.ToString());
        int ph = 0;
        foreach (var ch in QuestStateImporter.InsertSql) if (ch == '?') ph++;
        Check("ddl:insert-placeholders", "9", ph.ToString());
        Check("ddl:create-table-has-quest_key", "True", QuestStateSchema.CreateTableSql.Contains("quest_key") ? "True" : "False");
        Check("ddl:create-table-pk", "True",
            QuestStateSchema.CreateTableSql.Contains("PRIMARY KEY (`charid`, `quest_key`)") ? "True" : "False");
        Check("ddl:create-table-unique-wire", "True",
            QuestStateSchema.CreateTableSql.Contains("UNIQUE KEY `uq_char_quest_wire`") ? "True" : "False");

        // -- per scenario: Import(raw) must equal Extract(blob) and re-project byte-for-byte --
        uint charId = 0x2222;
        foreach (var sc in scenarios)
        {
            if (string.IsNullOrEmpty(sc.Blob)) { Bad($"import {sc.Name}: golden has no BLOB line"); continue; }
            byte[] raw = HexToBytes(sc.Blob);
            var blob = new QuestBitBlob();
            blob.LoadFromBlob(raw);

            // the reference canonical projection the codec gate already proved for this blob
            var refCells = QuestWireCodec.Extract(blob);
            string refHex = QuestWireCodec.Project(refCells).ToHex();

            var rows = QuestStateImporter.Import(charId, raw, null, null);

            // pure-blob import (no char_vars): rows == the non-Available cells
            Check($"import:rowcount {sc.Name}", refCells.Count.ToString(), rows.Count.ToString());
            foreach (var c in refCells)
            {
                bool found = false;
                foreach (var r in rows)
                    if (r.Area == c.Area && r.QuestId == c.Id && r.Status == c.Status) { found = true; break; }
                if (!found) Bad($"import missing cell {sc.Name} {(int)c.Area}/{c.Id}");
            }
            Ok($"import cells match {sc.Name}");

            // no registry -> every key is the deterministic synthetic legacy:<area>/<id>
            bool keysOk = true;
            foreach (var r in rows) if (r.QuestKey != QuestKeyRegistry.Synthetic(r.Wire)) { keysOk = false; break; }
            Check($"import:synthetic-keys {sc.Name}", "True", keysOk ? "True" : "False");

            // no char_vars -> every row's progress JSON is null (indistinguishable from pure-legacy)
            bool jsonNull = true;
            foreach (var r in rows) if (r.ProgressJson != null) { jsonNull = false; break; }
            Check($"import:no-progress-json {sc.Name}", "True", jsonNull ? "True" : "False");

            // round-trip parity: Project(ToCells(Import(raw))) == canonical projection
            var reimported = QuestWireCodec.Project(QuestStateImporter.ToCells(rows));
            Check($"import:project-parity {sc.Name}", refHex, reimported.ToHex());

            // idempotent: re-importing the canonical projection recovers the same row count
            var rows2 = QuestStateImporter.Import(charId, reimported.Bytes, null, null);
            Check($"import:idempotent {sc.Name}", rows.Count.ToString(), rows2.Count.ToString());
        }

        // -- char_vars merge: Prog -> step, unknown suffix -> SoftVars/JSON, junk ignored --
        {
            var blob = new QuestBitBlob();
            blob.AddQuest(1, 18); // Bastok/18 accepted
            byte[] raw = blob.Bytes;

            var vars = new[]
            {
                new QuestStateImporter.CharVar("Quest[1][18]Prog", 2),
                new QuestStateImporter.CharVar("Quest[1][18]mustZone", 12345),
                new QuestStateImporter.CharVar("NotAQuest", 7),        // no match -> ignored
                new QuestStateImporter.CharVar("Quest[11][3]Prog", 4), // area 11 out of range -> ignored
                new QuestStateImporter.CharVar("Quest[1][256]Prog", 4),// id 256 out of range -> ignored
            };

            var reg = new QuestKeyRegistry();
            reg.Register("bastok.elvaan_goldsmith", new QuestWireRef(QuestLog.Bastok, 18));

            var rows = QuestStateImporter.Import(charId, raw, vars, reg);
            Check("import:soft:rowcount", "1", rows.Count.ToString());
            var r = rows[0];
            Check("import:soft:key-from-registry", "bastok.elvaan_goldsmith", r.QuestKey);
            Check("import:soft:step", "2", r.Step.ToString());
            Check("import:soft:status", "Accepted", r.Status.ToString());
            Check("import:soft:softcount", "1", r.SoftVars.Count.ToString());
            Check("import:soft:progress-json", "{\"mustZone\":12345}", r.ProgressJson);
            // soft state must NOT perturb the wire projection
            string back = QuestWireCodec.Project(QuestStateImporter.ToCells(rows)).ToHex();
            Check("import:soft:project-unchanged", blob.ToHex(), back);
        }

        // -- soft-only quest: Prog var with no wire bit -> row, but projects to NO bit --
        {
            byte[] raw = new byte[QuestBitBlob.Size]; // empty blob
            var vars = new[]
            {
                new QuestStateImporter.CharVar("Quest[3][77]Prog", 5),
                new QuestStateImporter.CharVar("Quest[3][77]mustZone", 999),
            };
            var rows = QuestStateImporter.Import(charId, raw, vars, null);
            Check("import:softonly:rowcount", "1", rows.Count.ToString());
            var r = rows[0];
            Check("import:softonly:status", "Available", r.Status.ToString());
            Check("import:softonly:step", "5", r.Step.ToString());
            Check("import:softonly:key", "legacy:3/77", r.QuestKey);
            var leakCells = QuestWireCodec.Extract(QuestWireCodec.Project(QuestStateImporter.ToCells(rows)));
            Check("import:softonly:no-wire-leak", "0", leakCells.Count.ToString());
        }

        // -- parser negatives --
        Check("import:parse:notquest", "False",
            QuestStateImporter.TryParseQuestVar("Global[1]", out _, out _) ? "True" : "False");
        Check("import:parse:prog", "2:8:Prog",
            QuestStateImporter.TryParseQuestVar("Quest[2][8]Prog", out var w1, out var s1)
                ? $"{(int)w1.Area}:{w1.QuestId}:{s1}" : "bad");
        Check("import:parse:bare-empty-suffix", "3:0:",
            QuestStateImporter.TryParseQuestVar("Quest[3][0]", out var w2, out var s2)
                ? $"{(int)w2.Area}:{w2.QuestId}:{s2}" : "bad");
        Check("import:parse:range-area", "False",
            QuestStateImporter.TryParseQuestVar("Quest[99][1]Prog", out _, out _) ? "True" : "False");
        Check("import:parse:range-id", "False",
            QuestStateImporter.TryParseQuestVar("Quest[1][999]Prog", out _, out _) ? "True" : "False");
        Check("import:parse:null", "False",
            QuestStateImporter.TryParseQuestVar(null, out _, out _) ? "True" : "False");
    }

    static byte[] HexToBytes(string hex)
    {
        int n = hex.Length / 2;
        var b = new byte[n];
        for (int i = 0; i < n; i++)
            b[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return b;
    }

    // ---- repeatable/daily quest: the reopen window is stamped + driven by the Vana'diel
    //      clock (VanaTime), proving complete -> locked-until-next-midnight -> re-open. ----
    static void RunDailyGate()
    {
        Console.WriteLine("\n== XI.Quests: repeatable daily quest (Vana'diel clock-driven reopen) ==");

        var daily = new WindurstCourier();
        var m = daily.Compile();
        ExpectValid("windurst_courier(daily)", m);
        Check("daily:machine-repeat", "Daily", m.Repeat.ToString());

        int area = (int)m.Wire.Area;
        ushort id = m.Wire.QuestId;

        // Complete strictly inside day 800; the daily tick re-arms at day 801 midnight.
        long t0 = 800L * VanaTime.DAY + VanaTime.HOUR;
        long midnight801 = 801L * VanaTime.DAY;
        // cross-check the boundary is what the oracle-verified clock computes
        Check("daily:boundary-is-clock-value", midnight801.ToString(), VanaTime.NextMidnightTicks(t0).ToString());

        var prog = new QuestProgress(m.Id, m.Initial.Step);

        // Accept.
        var cAcc = new QuestContext(0x1111, prog, QuestTrigger.Interact, nowVanaTime: t0);
        Check("daily:accept-fired", "True", QuestRuntime.Advance(m, prog, cAcc).ToString());
        var blobAcc = new QuestBitBlob();
        QuestWireProjector.Project(m.Wire, prog, blobAcc);
        Check("daily:wire:accepted", "Accepted", blobAcc.GetStatus(area, id).ToString());

        // Complete by trading the parcel -> stamps the reopen window from the injected clock.
        var cComp = new QuestContext(0x1111, prog, QuestTrigger.Trade,
            tradedItem: WindurstCourier.ParcelItem, nowVanaTime: t0);
        Check("daily:complete-fired", "True", QuestRuntime.Advance(m, prog, cComp).ToString());
        Check("daily:step-terminal", "2", prog.Step.ToString());
        Check("daily:lifecycle-completed", "Completed", prog.Lifecycle.ToString());
        Check("daily:repeat-stamped", midnight801.ToString(), prog.RepeatByVanaTime.ToString());
        var blobDone = new QuestBitBlob();
        QuestWireProjector.Project(m.Wire, prog, blobDone);
        Check("daily:wire:completed", "Completed", blobDone.GetStatus(area, id).ToString());

        // Locked inside the window: not reopenable, and an Interact does not re-accept.
        Check("daily:cooldown-at-completion", "True", QuestRuntime.OnCooldown(prog, t0).ToString());
        Check("daily:reopen-too-early", "False", QuestRuntime.TryReopen(m, prog, t0).ToString());
        var cEarly = new QuestContext(0x1111, prog, QuestTrigger.Interact, nowVanaTime: midnight801 - 1);
        Check("daily:interact-before-midnight-noop", "False", QuestRuntime.Advance(m, prog, cEarly).ToString());
        Check("daily:reopen-1tick-before", "False", QuestRuntime.TryReopen(m, prog, midnight801 - 1).ToString());

        // Re-arm exactly at the daily tick: step resets to initial, window clears, and the
        // projected wire bit is gone (a fresh quest, neither current nor complete).
        Check("daily:reopen-at-midnight", "True", QuestRuntime.TryReopen(m, prog, midnight801).ToString());
        Check("daily:after-reopen-lifecycle", "Available", prog.Lifecycle.ToString());
        Check("daily:after-reopen-step", "0", prog.Step.ToString());
        Check("daily:after-reopen-window-cleared", "0", prog.RepeatByVanaTime.ToString());
        Check("daily:after-reopen-not-cooldown", "False", QuestRuntime.OnCooldown(prog, midnight801).ToString());
        var blobReopen = new QuestBitBlob();
        QuestWireProjector.Project(m.Wire, prog, blobReopen);
        Check("daily:wire:available-after-reopen", "Available", blobReopen.GetStatus(area, id).ToString());

        // Second cycle: re-accept after re-arm.
        var cRe = new QuestContext(0x1111, prog, QuestTrigger.Interact, nowVanaTime: midnight801);
        Check("daily:second-cycle-accept", "True", QuestRuntime.Advance(m, prog, cRe).ToString());
        Check("daily:second-cycle-step", "1", prog.Step.ToString());
        var blobRe = new QuestBitBlob();
        QuestWireProjector.Project(m.Wire, prog, blobRe);
        Check("daily:wire:accepted-again", "Accepted", blobRe.GetStatus(area, id).ToString());

        // Non-repeat guard: a QuestRepeat.None machine never stamps a window or re-arms.
        var once = new ElvaanGoldsmith().Compile();
        Check("once:repeat-none", "None", once.Repeat.ToString());
        var oprog = new QuestProgress(once.Id, once.Initial.Step);
        var ocAcc = new QuestContext(0x2222, oprog, QuestTrigger.Interact, nowVanaTime: t0);
        QuestRuntime.Advance(once, oprog, ocAcc);
        var ocComp = new QuestContext(0x2222, oprog, QuestTrigger.Trade,
            tradedItem: ElvaanGoldsmith.IngotItem, nowVanaTime: t0);
        QuestRuntime.Advance(once, oprog, ocComp);
        Check("once:completed", "Completed", oprog.Lifecycle.ToString());
        Check("once:no-window-stamped", "0", oprog.RepeatByVanaTime.ToString());
        Check("once:reopen-never", "False", QuestRuntime.TryReopen(once, oprog, midnight801).ToString());
    }

    // ---- MISSION blob: replay each scenario from oracle/mission_golden.txt against the
    //      byte-for-byte MissionBitBlob port (the chars.missions 1050-byte raw struct). The
    //      engine's own accessors were compiled into the harness, so this diffs the C# port
    //      against real engine semantics: sentinel init, CoP/highid current-relative
    //      completion, the area>2 reset split, and the nibble/whole mission status. ----
    enum MKind { Add, Complete, Del, StNib, StFull }
    enum MStatKind { Cur, Has, St, Nib }
    sealed class MOp { public int Area; public MKind Kind; public int Id; public int Value; public int Pos; }
    sealed class MStat { public int Area; public MStatKind Kind; public int Id; public int Pos; public string Expected; }
    sealed class MScen
    {
        public string Name;
        public List<MOp> Ops = new();
        public string Blob;
        public List<MStat> Stats = new();
    }

    static List<MScen> RunMissionGate()
    {
        Console.WriteLine("\n== XI.Quests: mission blob (chars.missions 1050B) vs engine-verbatim oracle ==");

        string goldenPath = @"H:\023-FFXI-server\csharp\oracle\mission_golden.txt";
        if (!File.Exists(goldenPath))
        {
            Bad("mission golden not found: " + goldenPath + " (run oracle/build_mission.bat)");
            return new List<MScen>();
        }

        var scen = new Dictionary<string, MScen>();
        var order = new List<MScen>();
        MScen Get(string n)
        {
            if (!scen.TryGetValue(n, out var s)) { s = new MScen { Name = n }; scen[n] = s; order.Add(s); }
            return s;
        }

        long expectedSize = 0;
        foreach (string raw in File.ReadAllLines(goldenPath))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;

            var meta = Regex.Match(line, @"^META\s+sizeof_ml\s+(\d+)$");
            if (meta.Success) { expectedSize = long.Parse(meta.Groups[1].Value); continue; }

            var mop = Regex.Match(line, @"^SCEN\s+(\S+)\s+MOP\s+(\d+)\s+(ADD|COMPLETE|DEL)\s+(\d+)$");
            if (mop.Success)
            {
                MKind k = mop.Groups[3].Value switch
                {
                    "ADD" => MKind.Add,
                    "COMPLETE" => MKind.Complete,
                    _ => MKind.Del,
                };
                Get(mop.Groups[1].Value).Ops.Add(new MOp { Area = int.Parse(mop.Groups[2].Value), Kind = k, Id = int.Parse(mop.Groups[4].Value) });
                continue;
            }
            var stnib = Regex.Match(line, @"^SCEN\s+(\S+)\s+MOP\s+(\d+)\s+STNIB\s+(\d+)\s+(\d+)$");
            if (stnib.Success)
            {
                Get(stnib.Groups[1].Value).Ops.Add(new MOp
                {
                    Area = int.Parse(stnib.Groups[2].Value),
                    Kind = MKind.StNib,
                    Value = int.Parse(stnib.Groups[3].Value),
                    Pos = int.Parse(stnib.Groups[4].Value),
                });
                continue;
            }
            var stfull = Regex.Match(line, @"^SCEN\s+(\S+)\s+MOP\s+(\d+)\s+STFULL\s+(\d+)$");
            if (stfull.Success)
            {
                Get(stfull.Groups[1].Value).Ops.Add(new MOp
                {
                    Area = int.Parse(stfull.Groups[2].Value),
                    Kind = MKind.StFull,
                    Value = (int)uint.Parse(stfull.Groups[3].Value),
                });
                continue;
            }
            var mblob = Regex.Match(line, @"^SCEN\s+(\S+)\s+BLOB\s+([0-9A-Fa-f]+)$");
            if (mblob.Success) { Get(mblob.Groups[1].Value).Blob = mblob.Groups[2].Value.ToUpperInvariant(); continue; }

            var mcur = Regex.Match(line, @"^MSTAT\s+(\S+)\s+CUR\s+(\d+)\s+(\d+)$");
            if (mcur.Success) { Get(mcur.Groups[1].Value).Stats.Add(new MStat { Area = int.Parse(mcur.Groups[2].Value), Kind = MStatKind.Cur, Expected = mcur.Groups[3].Value }); continue; }
            var mhas = Regex.Match(line, @"^MSTAT\s+(\S+)\s+HAS\s+(\d+)\s+(\d+)\s+([01])$");
            if (mhas.Success) { Get(mhas.Groups[1].Value).Stats.Add(new MStat { Area = int.Parse(mhas.Groups[2].Value), Kind = MStatKind.Has, Id = int.Parse(mhas.Groups[3].Value), Expected = mhas.Groups[4].Value }); continue; }
            var mst = Regex.Match(line, @"^MSTAT\s+(\S+)\s+ST\s+(\d+)\s+(\d+)$");
            if (mst.Success) { Get(mst.Groups[1].Value).Stats.Add(new MStat { Area = int.Parse(mst.Groups[2].Value), Kind = MStatKind.St, Expected = mst.Groups[3].Value }); continue; }
            var mnib = Regex.Match(line, @"^MSTAT\s+(\S+)\s+NIB\s+(\d+)\s+(\d+)\s+(\d+)$");
            if (mnib.Success) { Get(mnib.Groups[1].Value).Stats.Add(new MStat { Area = int.Parse(mnib.Groups[2].Value), Kind = MStatKind.Nib, Pos = int.Parse(mnib.Groups[3].Value), Expected = mnib.Groups[4].Value }); continue; }

            Console.WriteLine("  [warn] unparsed mission golden line: " + line);
        }

        // -- 1) struct size: the fresh init is NOT all-zero, so a mismatch here is fatal --
        Check("mission:sizeof", expectedSize.ToString(), MissionBitBlob.Size.ToString());

        int scenCount = 0;
        foreach (var sc in order)
        {
            scenCount++;
            // A fresh MissionBitBlob reproduces char_entity.cpp's sentinel init exactly.
            var blob = new MissionBitBlob();
            foreach (var o in sc.Ops)
            {
                switch (o.Kind)
                {
                    case MKind.Add: blob.AddMission(o.Area, (ushort)o.Id); break;
                    case MKind.Complete: blob.CompleteMission(o.Area, (ushort)o.Id); break;
                    case MKind.Del: blob.DelMission(o.Area, (ushort)o.Id); break;
                    case MKind.StNib: blob.SetMissionStatusNibble(o.Area, (byte)o.Value, (byte)o.Pos); break;
                    case MKind.StFull: blob.SetMissionStatusFull(o.Area, (uint)o.Value); break;
                }
            }

            // -- 2) the raw 1050-byte blob, byte-for-byte --
            string got = blob.ToHex();
            Check($"mission:blob {sc.Name}", sc.Blob, got);
            if (got != sc.Blob)
                Console.WriteLine($"      first diff at byte {FirstDiffByte(sc.Blob, got)}");

            // -- 3) every accessor read-back --
            foreach (var s in sc.Stats)
            {
                string actual = s.Kind switch
                {
                    MStatKind.Cur => ((uint)blob.GetCurrentMission(s.Area)).ToString(),
                    MStatKind.Has => (blob.HasCompletedMission(s.Area, (ushort)s.Id) ? 1 : 0).ToString(),
                    MStatKind.St => blob.GetMissionStatus(s.Area).ToString(),
                    MStatKind.Nib => blob.GetMissionStatusNibble(s.Area, (byte)s.Pos).ToString(),
                    _ => "?",
                };
                Check($"mission:{s.Kind} {sc.Name} {s.Area}/{s.Id}/{s.Pos}", s.Expected, actual);
            }
        }
        Console.WriteLine($"  (mission scenarios replayed: {scenCount})");
        return order;
    }

    // ---- MISSION normalized model: per-area cells (current + status + completion mask),
    //      a LOSSLESS raw round-trip (no canonicalization -- missions carry the full struct),
    //      the derived CoP/highid IsCompleted view cross-checked against the oracle's own HAS
    //      read-backs, and the char_mission_state importer folding Mission[area][id]Prog
    //      soft state without ever leaking a wire byte. ----
    static void RunMissionModelGate(List<MScen> scens)
    {
        Console.WriteLine("\n== XI.Quests: mission normalized model (char_mission_state) ==");

        // -- DDL / INSERT shape --
        Check("mission-ddl:columns", "7", MissionStateSchema.Columns.Length.ToString());
        int ph = 0;
        foreach (var ch in MissionStateImporter.InsertSql) if (ch == '?') ph++;
        Check("mission-ddl:insert-placeholders", "7", ph.ToString());
        Check("mission-ddl:create-table-pk", "True",
            MissionStateSchema.CreateTableSql.Contains("PRIMARY KEY (`charid`, `area`)") ? "True" : "False");

        // -- parser negatives (out-of-range / non-mission / null) --
        Check("mission:parse:notmission", "False", MissionStateImporter.TryParseMissionVar("Quest[1][2]", out _, out _, out _) ? "True" : "False");
        Check("mission:parse:null", "False", MissionStateImporter.TryParseMissionVar(null, out _, out _, out _) ? "True" : "False");
        Check("mission:parse:range-area", "False", MissionStateImporter.TryParseMissionVar("Mission[99][1]Prog", out _, out _, out _) ? "True" : "False");
        Check("mission:parse:range-id", "False", MissionStateImporter.TryParseMissionVar("Mission[1][851]Prog", out _, out _, out _) ? "True" : "False");
        Check("mission:parse:prog", "4:10:Prog",
            MissionStateImporter.TryParseMissionVar("Mission[4][10]Prog", out int pa, out int pi, out string ps) ? $"{pa}:{pi}:{ps}" : "bad");
        Check("mission:parse:bare-empty-suffix", "3:0:",
            MissionStateImporter.TryParseMissionVar("Mission[3][0]", out int ba, out int bi, out string bs) ? $"{ba}:{bi}:{bs}" : "bad");

        uint charId = 0x99aa;
        foreach (var sc in scens)
        {
            if (string.IsNullOrEmpty(sc.Blob)) { Bad($"mission-model {sc.Name}: golden has no BLOB line"); continue; }
            byte[] raw = HexToBytes(sc.Blob);

            // load the authoritative golden bytes (ground truth, not a re-replay of ops)
            var blob = new MissionBitBlob();
            blob.LoadFromBlob(raw);

            // Extract all 15 per-area cells; Project reproduces the RAW golden bytes (lossless,
            // NOT a canonical projection -- missions carry the full struct).
            var cells = MissionWireCodec.Extract(blob);
            Check($"mission:extract-areas {sc.Name}", "15", cells.Count.ToString());
            var back = MissionWireCodec.Project(cells);
            Check($"mission:codec-lossless {sc.Name}", sc.Blob, back.ToHex());

            // idempotent round-trip
            var back2 = MissionWireCodec.Project(MissionWireCodec.Extract(back));
            Check($"mission:codec-idempotent {sc.Name}", back.ToHex(), back2.ToHex());

            // the derived CoP / missionID>=64 completion view must match the oracle's HAS lines
            foreach (var s in sc.Stats)
                if (s.Kind == MStatKind.Has)
                {
                    bool got = cells[s.Area].IsCompleted(s.Id);
                    Check($"mission:iscompleted {sc.Name} {s.Area}/{s.Id}", s.Expected, got ? "1" : "0");
                }

            // importer: rows -> cells -> Project reproduces the raw golden blob byte-for-byte
            var rows = MissionStateImporter.Import(charId, raw, null);
            Check($"mission:import-rows {sc.Name}", "15", rows.Count.ToString());
            Check($"mission:import-project {sc.Name}", sc.Blob, MissionWireCodec.Project(MissionStateImporter.ToCells(rows)).ToHex());

            // soft Mission[area][id]Prog folds into the owning area row but NEVER the wire blob
            var softVars = new[] { new MissionStateImporter.CharVar("Mission[4][10]Prog", 7) };
            var rows2 = MissionStateImporter.Import(charId, raw, softVars);
            bool folded = rows2[4].SoftVars.TryGetValue("10:Prog", out long v) && v == 7;
            Check($"mission:soft-fold {sc.Name}", "True", folded ? "True" : "False");
            Check($"mission:soft-json {sc.Name}", "{\"10:Prog\":7}", rows2[4].ProgressJson);
            Check($"mission:soft-no-leak {sc.Name}", sc.Blob, MissionWireCodec.Project(MissionStateImporter.ToCells(rows2)).ToHex());
        }

        // -- IsCompleted semantics for the CoP current-relative + high-id cases on a crafted cell --
        {
            var cop = new MissionCell { Area = MissionLog.CoP, Current = 300 };
            Check("mission:iscompleted-cop-below", "True", cop.IsCompleted(150).ToString());
            Check("mission:iscompleted-cop-at", "False", cop.IsCompleted(300).ToString());
            var toau = new MissionCell { Area = MissionLog.ToAU, Current = 80 };
            toau.SetCompleted(10, true);
            Check("mission:iscompleted-stored", "True", toau.IsCompleted(10).ToString());
            Check("mission:iscompleted-highid-relative", "True", toau.IsCompleted(70).ToString()); // 70>=64 -> 70<80
            Check("mission:iscompleted-highid-notstored", "False", toau.IsCompleted(90).ToString()); // 90>=80
        }
    }

    static void ExpectMissionValid(string name, MissionMachine m)
    {
        var v = MissionValidator.Validate(m);
        Check($"mvalidate:ok {name}", "valid(0e/0w)",
            v.IsValid && v.ErrorCount == 0 && v.WarningCount == 0 ? "valid(0e/0w)" : $"bad(e={v.ErrorCount},w={v.WarningCount})");
        foreach (var d in v.Diagnostics) Console.WriteLine("      " + d);
    }

    static void ExpectMissionCode(string name, MissionMachine m, string code)
    {
        var v = MissionValidator.Validate(m);
        Check($"mvalidate:{code} {name}", "true", v.Has(code) ? "true" : "false");
        if (!v.Has(code)) foreach (var d in v.Diagnostics) Console.WriteLine("      (got) " + d);
    }

    // ---- MISSION authoring core: a state machine over a per-area MissionProgress, verified
    //      by driving the byte-for-byte-verified MissionBitBlob at each step + validator
    //      negatives. This is the unified-container-taxonomy proof: the same authoring shape
    //      as quests covers a container with a structurally unrelated wire model. ----
    static void RunMissionAuthoringGate()
    {
        Console.WriteLine("\n== XI.Quests: mission authoring core (state machine + validator) ==");

        var def = new SanDoriaPrologue();
        var m = def.Compile();
        ExpectMissionValid("sandoria_prologue", m);

        int area = (int)m.Area; // Sandoria (area 0) -> completion flag in complete[0]
        int id0 = 0, id1 = 1;

        var prog = new MissionProgress(m.Id, m.Initial.Step, m.Initial.MissionId);

        // Fresh mission log: sentinel init, no active mission, status 0.
        var blobStart = new MissionBitBlob();
        MissionWireProjector.Project(m.Area, prog, blobStart);
        Check("mwalk:start-current-0xFFFF", "65535", blobStart.GetCurrentMission(area).ToString());
        Check("mwalk:start-status", "0", blobStart.GetMissionStatus(area).ToString());

        // Interact -> activate mission 0.
        var cAcc = new MissionContext(0x1234, prog, QuestTrigger.Interact);
        Check("mwalk:activate-fired", "True", MissionRuntime.Advance(m, prog, cAcc).ToString());
        Check("mwalk:activate-step", "1", prog.Step.ToString());
        Check("mwalk:activate-current", "0", blob0Current(m, prog).ToString());
        Check("mwalk:activate-effect", "SetActive", cAcc.Fired.Count > 0 ? cAcc.Fired[0].Kind.ToString() : "none");

        // Trade WRONG item -> guard rejects, no advance.
        var cBad = new MissionContext(0x1234, prog, QuestTrigger.Trade, tradedItem: 9999);
        Check("mwalk:guard-rejects-wrong-trade", "False", MissionRuntime.Advance(m, prog, cBad).ToString());
        Check("mwalk:guard-step-unchanged", "1", prog.Step.ToString());

        // Trade RIGHT item -> complete 0, activate 1 (declared order).
        var cB2 = new MissionContext(0x1234, prog, QuestTrigger.Trade, tradedItem: SanDoriaPrologue.TokenItem);
        Check("mwalk:b2-fired", "True", MissionRuntime.Advance(m, prog, cB2).ToString());
        Check("mwalk:b2-step", "2", prog.Step.ToString());
        Check("mwalk:b2-effect-count", "2", cB2.Fired.Count.ToString());
        Check("mwalk:b2-effect-order", "CompleteActive,SetActive", string.Join(",", cB2.Fired.ConvertAll(x => x.Kind.ToString())));
        var blobB2 = new MissionBitBlob();
        MissionWireProjector.Project(m.Area, prog, blobB2);
        Check("mwalk:b2-current-is-1", "1", blobB2.GetCurrentMission(area).ToString());
        Check("mwalk:b2-completed-id0", "True", blobB2.HasCompletedMission(area, (ushort)id0).ToString());
        Check("mwalk:b2-completed-id1", "False", blobB2.HasCompletedMission(area, (ushort)id1).ToString());

        // Talk -> status nibble(0,5) + complete 1 -> terminal.
        var cFin = new MissionContext(0x1234, prog, QuestTrigger.Talk);
        Check("mwalk:final-fired", "True", MissionRuntime.Advance(m, prog, cFin).ToString());
        Check("mwalk:final-step", "3", prog.Step.ToString());
        var blobFin = new MissionBitBlob();
        MissionWireProjector.Project(m.Area, prog, blobFin);
        Check("mwalk:final-status", "5", blobFin.GetMissionStatus(area).ToString());
        Check("mwalk:final-nibble0", "5", blobFin.GetMissionStatusNibble(area, 0).ToString());
        Check("mwalk:final-completed-id0", "True", blobFin.HasCompletedMission(area, (ushort)id0).ToString());
        Check("mwalk:final-completed-id1", "True", blobFin.HasCompletedMission(area, (ushort)id1).ToString());

        // The projected blob must survive the byte-proven normalized codec round-trip.
        var cells = MissionWireCodec.Extract(blobFin);
        Check("mwalk:codec-lossless", blobFin.ToHex(), MissionWireCodec.Project(cells).ToHex());

        // Terminal is a sink.
        var cSink = new MissionContext(0x1234, prog, QuestTrigger.Talk);
        Check("mwalk:terminal-sink", "False", MissionRuntime.Advance(m, prog, cSink).ToString());

        // ---- mission validator negatives ----
        ExpectMissionCode("m-dangling-to", MM("d1", b => { b.Initial(0); b.State(0); b.On(0, QuestTrigger.Interact, to: 99, MissionAction.Activate(0)); }), "DANGLING_TO");
        ExpectMissionCode("m-dangling-from", MM("d2", b => { b.Initial(0); b.State(0); b.State(1, -1, "t", true); b.On(0, QuestTrigger.Interact, to: 1, MissionAction.Complete(0)); b.On(7, QuestTrigger.Interact, to: 1, MissionAction.Complete(0)); }), "DANGLING_FROM");
        ExpectMissionCode("m-dead-end", MM("d3", b => { b.Initial(0); b.State(0); b.State(1); b.On(0, QuestTrigger.Interact, to: 1, MissionAction.Activate(0)); }), "DEAD_END");
        ExpectMissionCode("m-no-terminal", MM("d4", b => { b.Initial(0); b.State(0); b.State(1); b.On(0, QuestTrigger.Interact, to: 1, MissionAction.Activate(0)); b.On(1, QuestTrigger.Interact, to: 0, MissionAction.Activate(0)); }), "NO_TERMINAL");
        ExpectMissionCode("m-orphan", MM("d5", b => { b.Initial(0); b.State(0); b.State(2, -1, "t", true); b.State(9, -1, "orphan", true); b.On(0, QuestTrigger.Interact, to: 2, MissionAction.Complete(0)); }), "ORPHAN_STATE");
        ExpectMissionCode("m-terminal-unreachable", MM("d6", b => { b.Initial(0); b.State(0); b.State(1, -1, "t", true); b.On(0, QuestTrigger.Interact, to: 0, MissionAction.Activate(0)); }), "TERMINAL_UNREACHABLE");
        ExpectMissionCode("m-complete-missing", MM("d7", b => { b.Initial(0); b.State(0); b.State(1, -1, "t", true); b.On(0, QuestTrigger.Interact, to: 1, MissionAction.Activate(0)); }), "COMPLETE_MISSING");
        ExpectMissionCode("m-activate-and-complete", MM("d8", b => { b.Initial(0); b.State(0); b.State(1, -1, "t", true); b.On(0, QuestTrigger.Interact, to: 1, MissionAction.Activate(0), MissionAction.Complete(0)); }), "ACTIVATE_AND_COMPLETE");
        ExpectMissionCode("m-duplicate-step", MM("d9", b => { b.Initial(0); b.State(0); b.State(0, 5, "dup"); b.State(1, -1, "t", true); b.On(0, QuestTrigger.Interact, to: 1, MissionAction.Complete(0)); }), "DUPLICATE_STEP");
        ExpectMissionCode("m-terminal-has-outgoing", MM("d10", b => { b.Initial(0); b.State(0); b.State(1, -1, "t", true); b.On(0, QuestTrigger.Interact, to: 1, MissionAction.Complete(0)); b.On(1, QuestTrigger.Interact, to: 0, MissionAction.Activate(0)); }), "TERMINAL_HAS_OUTGOING");

        // ---- builder throws (structural, pre-validation) ----
        try { MM("throw1", b => { b.State(0); b.On(0, QuestTrigger.Interact, to: 0, MissionAction.Activate(0)); }); Bad("mbuilder:no-initial-should-throw"); }
        catch (InvalidOperationException) { Ok("mbuilder:no-initial-throws"); }

        try { MM("throw2", b => { b.State(0); b.Initial(5); b.On(0, QuestTrigger.Interact, to: 0, MissionAction.Activate(0)); }); Bad("mbuilder:initial-not-declared-should-throw"); }
        catch (InvalidOperationException) { Ok("mbuilder:initial-step-not-declared-throws"); }
    }

    static string blob0Current(MissionMachine m, MissionProgress p)
    {
        var b = new MissionBitBlob();
        MissionWireProjector.Project(m.Area, p, b);
        return b.GetCurrentMission((int)m.Area).ToString();
    }

    // ---- ASSAULT blob: replay each scenario from oracle/assault_golden.txt against the
    //      byte-for-byte AssaultBitBlob port (chars.assault, the 130-byte single-container
    //      struct). The engine accessors were compiled into the harness, so this diffs the C#
    //      port against real semantics -- notably the UNCONDITIONAL completeAssault and del
    //      clearing only the uint8-truncated current (never the completion flag). ----
    enum AKind { Add, Complete, Del }
    enum AStatKind { Cur, Has }
    sealed class AOp { public AKind Kind; public int Id; }
    sealed class AStat { public AStatKind Kind; public int Id; public string Expected; }
    sealed class AScen { public string Name; public List<AOp> Ops = new(); public string Blob; public List<AStat> Stats = new(); }

    // ---- KEYITEMS blob: replay each scenario from oracle/keyitems_golden.txt against the
    //      byte-for-byte KeyItemsBitBlob port (chars.keyitems, the 1024-byte 8-table struct of two
    //      orthogonal 512-bit bitsets each: keyList "possessed" + seenList "discovered"). The engine
    //      accessors were compiled into the harness, so this diffs the C# port against real semantics
    //      -- the /512 %512 cross-table addressing, the `table >= 8` capacity guard (writes no-op,
    //      reads false), and the keyList/seenList independence. ----
    enum KKind { Add, Del, Seen, Unseen }
    enum KStatKind { Has, Seen }
    sealed class KOp { public KKind Kind; public int Id; }
    sealed class KStat { public KStatKind Kind; public int Id; public string Expected; }
    sealed class KScen { public string Name; public List<KOp> Ops = new(); public string Blob; public List<KStat> Stats = new(); }

    // ---- EMINENCE (Record of Eminence) golden replay ----
    enum EKind { Add, Del, SetC, SetP }
    enum EStatKind { Comp, Prog, Has }
    sealed class EOp { public EKind Kind; public int Id; public long V; }
    sealed class EStat { public EStatKind Kind; public int Id; public string Expected; }
    sealed class EScen { public string Name; public List<EOp> Ops = new(); public string Blob; public List<EStat> Stats = new(); }

    // chars.eminence (eminencelog_t, 700-byte raw struct WITH 2 padding bytes) vs the engine-
    // verbatim oracle. This is the port where the compiled-oracle method earns its keep: the
    // uint32 progress[] aligns to a 4-byte boundary after uint16 active[31] (62 bytes), so the
    // raw on-disk blob carries 2 dead padding bytes and progress starts at offset 64 -- a layout
    // unsafe to hand-transcribe. We also assert that padding explicitly by name below.
    static List<EScen> RunEminenceGate()
    {
        Console.WriteLine("\n== XI.Quests: eminence blob (chars.eminence 700B, padded) vs engine-verbatim oracle ==");

        string goldenPath = @"H:\023-FFXI-server\csharp\oracle\eminence_golden.txt";
        if (!File.Exists(goldenPath))
        {
            Bad("eminence golden not found: " + goldenPath + " (run oracle/build_eminence.bat)");
            return new List<EScen>();
        }

        var scen = new Dictionary<string, EScen>();
        var order = new List<EScen>();
        EScen Get(string n)
        {
            if (!scen.TryGetValue(n, out var s)) { s = new EScen { Name = n }; scen[n] = s; order.Add(s); }
            return s;
        }

        long expectedSize = 0;
        foreach (string raw in File.ReadAllLines(goldenPath))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;

            var meta = Regex.Match(line, @"^META\s+sizeof_em\s+(\d+)$");
            if (meta.Success) { expectedSize = long.Parse(meta.Groups[1].Value); continue; }

            var ad = Regex.Match(line, @"^SCEN\s+(\S+)\s+EMOP\s+(ADD|DEL)\s+(\d+)$");
            if (ad.Success)
            {
                Get(ad.Groups[1].Value).Ops.Add(new EOp { Kind = ad.Groups[2].Value == "ADD" ? EKind.Add : EKind.Del, Id = int.Parse(ad.Groups[3].Value) });
                continue;
            }
            var sc = Regex.Match(line, @"^SCEN\s+(\S+)\s+EMOP\s+SETC\s+(\d+)\s+(\d+)$");
            if (sc.Success) { Get(sc.Groups[1].Value).Ops.Add(new EOp { Kind = EKind.SetC, Id = int.Parse(sc.Groups[2].Value), V = long.Parse(sc.Groups[3].Value) }); continue; }
            var sp = Regex.Match(line, @"^SCEN\s+(\S+)\s+EMOP\s+SETP\s+(\d+)\s+(\d+)$");
            if (sp.Success) { Get(sp.Groups[1].Value).Ops.Add(new EOp { Kind = EKind.SetP, Id = int.Parse(sp.Groups[2].Value), V = long.Parse(sp.Groups[3].Value) }); continue; }

            var blob = Regex.Match(line, @"^SCEN\s+(\S+)\s+BLOB\s+([0-9A-Fa-f]+)$");
            if (blob.Success) { Get(blob.Groups[1].Value).Blob = blob.Groups[2].Value.ToUpperInvariant(); continue; }

            var comp = Regex.Match(line, @"^EMSTAT\s+(\S+)\s+COMP\s+(\d+)\s+(\d+)$");
            if (comp.Success) { Get(comp.Groups[1].Value).Stats.Add(new EStat { Kind = EStatKind.Comp, Id = int.Parse(comp.Groups[2].Value), Expected = comp.Groups[3].Value }); continue; }
            var prog = Regex.Match(line, @"^EMSTAT\s+(\S+)\s+PROG\s+(\d+)\s+(\d+)$");
            if (prog.Success) { Get(prog.Groups[1].Value).Stats.Add(new EStat { Kind = EStatKind.Prog, Id = int.Parse(prog.Groups[2].Value), Expected = prog.Groups[3].Value }); continue; }
            var has = Regex.Match(line, @"^EMSTAT\s+(\S+)\s+HAS\s+(\d+)\s+(\d+)$");
            if (has.Success) { Get(has.Groups[1].Value).Stats.Add(new EStat { Kind = EStatKind.Has, Id = int.Parse(has.Groups[2].Value), Expected = has.Groups[3].Value }); continue; }

            Console.WriteLine("  [warn] unparsed eminence golden line: " + line);
        }

        // -- struct size: 700 = 62 (active) + 2 (padding) + 124 (progress) + 512 (complete) --
        Check("eminence:sizeof", expectedSize.ToString(), EminenceBitBlob.Size.ToString());

        foreach (var sc in order)
        {
            if (string.IsNullOrEmpty(sc.Blob)) { Bad($"eminence {sc.Name}: golden has no BLOB line"); continue; }

            var blob = new EminenceBitBlob();
            foreach (var o in sc.Ops)
            {
                switch (o.Kind)
                {
                    case EKind.Add: blob.Add((uint)o.Id); break;
                    case EKind.Del: blob.Del((uint)o.Id); break;
                    case EKind.SetC: blob.SetCompletion((uint)o.Id, o.V != 0); break;
                    case EKind.SetP: blob.SetProgress((uint)o.Id, (uint)o.V); break;
                }
            }

            string got = blob.ToHex();
            Check($"eminence:blob {sc.Name}", sc.Blob, got);
            if (got != sc.Blob) Console.WriteLine($"      first diff at byte {FirstDiffByte(sc.Blob, got)}");

            // LoadFromBlob reproduces the golden bytes verbatim (extractFromBlob path)
            var loaded = new EminenceBitBlob();
            loaded.LoadFromBlob(HexToBytes(sc.Blob));
            Check($"eminence:load-roundtrip {sc.Name}", sc.Blob, loaded.ToHex());

            foreach (var s in sc.Stats)
            {
                string actual = s.Kind switch
                {
                    EStatKind.Comp => (blob.GetCompletion((uint)s.Id) ? 1 : 0).ToString(),
                    EStatKind.Prog => blob.GetProgress((uint)s.Id).ToString(),
                    EStatKind.Has => (blob.Has((uint)s.Id) ? 1 : 0).ToString(),
                    _ => "?",
                };
                Check($"eminence:{s.Kind} {sc.Name} {s.Id}", s.Expected, actual);
            }
        }

        // -- the padding invariant, asserted by name on the progress_set golden:
        //    active[31] = 62 bytes (offset 0..62); 2 PADDING bytes at 62..64 are zero;
        //    progress[0] = uint32 4321 (0x000010E1) little-endian -> "E1100000" at offset 64. --
        EScen ps = null;
        foreach (var s in order) if (s.Name == "progress_set") { ps = s; break; }
        if (ps != null && !string.IsNullOrEmpty(ps.Blob) && ps.Blob.Length >= 700 * 2)
        {
            string padBytes = ps.Blob.Substring(62 * 2, 2 * 2);            // offsets 62,63
            string prog0Bytes = ps.Blob.Substring(64 * 2, 4 * 2);          // offsets 64..67
            Check("eminence:padding-bytes-zero", "0000", padBytes);
            Check("eminence:progress-at-offset-64", "E1100000", prog0Bytes);
        }
        else
        {
            Bad("eminence:padding-assert (progress_set golden not found/short)");
        }
        Console.WriteLine($"  (eminence scenarios replayed: {order.Count})");
        return order;
    }

    // ---- EMINENCE normalized model + offline importer: the per-record char_eminence_state
    //      half symmetric to RunMissionModelGate. Reuses the SAME eminence_golden.txt raw bytes
    //      that anchor EminenceBitBlob (itself byte-for-byte vs a compiled oracle). Proves
    //      Project(ToCells(Import(blob))) == blob byte-for-byte, slot-ordering fidelity, and the
    //      engine-reachable lossless boundary (distinct active slots; no hand-crafted dup-id
    //      contradiction). No SoftVars layer: getEminenceProgress is in-struct, not a char_var.
    static void RunEminenceModelGate(List<EScen> scens)
    {
        Console.WriteLine("\n== XI.Quests: eminence normalized model (char_eminence_state) ==");

        // -- DDL / INSERT shape --
        Check("eminence-ddl:columns", "6", EminenceStateSchema.Columns.Length.ToString());
        int ph = 0;
        foreach (var ch in EminenceStateImporter.InsertSql) if (ch == '?') ph++;
        Check("eminence-ddl:insert-placeholders", "6", ph.ToString());
        Check("eminence-ddl:create-table-pk", "True",
            EminenceStateSchema.CreateTableSql.Contains("PRIMARY KEY (`charid`, `record_id`)") ? "True" : "False");

        uint charId = 0x99aa;
        foreach (var sc in scens)
        {
            if (string.IsNullOrEmpty(sc.Blob)) { Bad($"eminence-model {sc.Name}: golden has no BLOB line"); continue; }
            byte[] raw = HexToBytes(sc.Blob);

            // load the authoritative golden bytes (ground truth, not a re-replay of ops)
            var blob = new EminenceBitBlob();
            blob.LoadFromBlob(raw);

            // Extract all distinct-record cells; Project reproduces the RAW golden bytes.
            var cells = EminenceWireCodec.Extract(blob);

            // no two cells may claim the same physical slot (engine invariant: active[] holds a
            // record id at most once -- addEminenceRecord rejects a dup active id).
            var slots = new HashSet<int>();
            bool slotsDistinct = true;
            foreach (var c in cells)
                if (c.Slot >= 0 && !slots.Add(c.Slot)) slotsDistinct = false;
            Check($"eminence:slots-distinct {sc.Name}", "True", slotsDistinct ? "True" : "False");

            var back = EminenceWireCodec.Project(cells);
            Check($"eminence:codec-lossless {sc.Name}", sc.Blob, back.ToHex());
            if (back.ToHex() != sc.Blob) Console.WriteLine($"      first diff at byte {FirstDiffByte(sc.Blob, back.ToHex())}");

            // idempotent round-trip
            var back2 = EminenceWireCodec.Project(EminenceWireCodec.Extract(back));
            Check($"eminence:codec-idempotent {sc.Name}", back.ToHex(), back2.ToHex());

            // importer: rows -> cells -> Project reproduces the raw golden blob byte-for-byte
            var rows = EminenceStateImporter.Import(charId, raw);
            Check($"eminence:import-project {sc.Name}", sc.Blob, EminenceWireCodec.Project(EminenceStateImporter.ToCells(rows)).ToHex());
            Check($"eminence:import-rowcount {sc.Name}", cells.Count.ToString(), rows.Count.ToString());
            Check($"eminence:import-charid {sc.Name}", charId.ToString(), (rows.Count > 0 ? rows[0].CharId : charId).ToString());

            // the COMP/PROG/HAS golden read-backs must survive extraction onto the normalized model
            foreach (var s in sc.Stats)
            {
                EminenceCell cell = null;
                foreach (var c in cells) if (c.RecordId == (uint)s.Id) { cell = c; break; }
                string actual = s.Kind switch
                {
                    EStatKind.Comp => ((cell != null && cell.Completed) ? 1 : 0).ToString(),
                    EStatKind.Prog => ((cell != null && cell.Slot >= 0) ? cell.Progress : 0u).ToString(),
                    EStatKind.Has  => ((cell != null && cell.Slot >= 0) ? 1 : 0).ToString(),
                    _ => "?",
                };
                Check($"eminence:cell {s.Kind} {sc.Name} {s.Id}", s.Expected, actual);
            }
        }

        // -- active AND completed on the same id is ONE row (roe.cpp Del never clears complete[]):
        //    complete_and_active = ADD 42, SETC 42 1, SETC 43 1. Record 42 is active+completed
        //    (slot>=0 && Completed); record 43 is completed-only (Slot -1). --
        {
            var em = new EminenceBitBlob();
            em.Add(42); em.SetCompletion(42, true); em.SetCompletion(43, true);
            var cs = EminenceWireCodec.Extract(em);
            EminenceCell c42 = null, c43 = null;
            foreach (var c in cs) { if (c.RecordId == 42) c42 = c; if (c.RecordId == 43) c43 = c; }
            Check("eminence:cell-42-active-completed", "True",
                (c42 != null && c42.IsActive && c42.Completed) ? "True" : "False");
            Check("eminence:cell-43-completed-only", "True",
                (c43 != null && !c43.IsActive && c43.Completed) ? "True" : "False");
            Check("eminence:cell-count", "2", cs.Count.ToString());
        }
    }

    static string ToHex(byte[] b)
    {
        var sb = new System.Text.StringBuilder(b.Length * 2);
        for (int i = 0; i < b.Length; i++) sb.Append(b[i].ToString("X2"));
        return sb.ToString();
    }

    // ---- bind-through helpers: count a statement's `?` placeholders and render a BoundValue[]
    //      (or an expected Kind[]) as a comparable comma-joined Kind sequence (no LINQ import).
    static int Placeholders(string sql)
    {
        int n = 0; foreach (var c in sql) if (c == '?') n++; return n;
    }
    static string KindsOf(BoundValue[] vs)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < vs.Length; i++) { if (i > 0) sb.Append(','); sb.Append(vs[i].Type); }
        return sb.ToString();
    }
    static string KindsOfVector(BoundValue.Kind[] ks)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < ks.Length; i++) { if (i > 0) sb.Append(','); sb.Append(ks[i]); }
        return sb.ToString();
    }

    // Pick a scenario's golden BLOB hex by name for the migration seam; if that exact scenario
    // has no BLOB line, fall back to the longest available blob (richest state) so the composed
    // four-way parity always runs on real, non-trivial bytes.
    static string PickBlob<T>(List<T> list, string want, Func<T, string> getName, Func<T, string> getBlob)
    {
        foreach (var x in list)
            if (getName(x) == want && !string.IsNullOrEmpty(getBlob(x))) return getBlob(x);
        string best = null;
        foreach (var x in list)
        {
            string b = getBlob(x);
            if (!string.IsNullOrEmpty(b) && (best == null || b.Length > best.Length)) best = b;
        }
        return best;
    }

    // Stable per-row fingerprint for the routing assertions (captures the blob-derived status +
    // the folded soft state, so a perturbation from a foreign-prefix char_var is visible).
    static string QuestFingerprint(List<CharQuestStateRow> rows)
    {
        var parts = new List<string>();
        foreach (var r in rows)
            parts.Add($"{r.QuestKey}|{(int)r.Status}|{r.Step}|{r.ProgressJson ?? "-"}");
        return string.Join(";", parts);
    }
    static string MissionFingerprint(List<CharMissionStateRow> rows)
    {
        var parts = new List<string>();
        foreach (var r in rows)
            parts.Add($"{(int)r.Area}|{r.Current}|{r.Status}|{r.CompletedMask}|{r.ProgressJson ?? "-"}");
        return string.Join(";", parts);
    }

    // ---- UNIFIED PER-CHARACTER MIGRATION SEAM: the five per-container importers + three
    //      byte-verified codecs composed into ONE legacy `chars` row -> five normalized tables
    //      -> five legacy blobs, driving CharacterStateMigration. Proves (a) five-way SIMULTANEOUS
    //      parity (quests == canonical; missions/assault/eminence == raw bytes) with a SHARED
    //      char_vars set, and (b) the routing property per-blob gates cannot see: char_vars is one
    //      flat table, so the quest and mission prefix-regex importers must select ONLY their own
    //      prefix from a mixed set (feeding the full set perturbs neither), the flat assault
    //      globals invent NO quest/mission row, and the assault/eminence importers ignore
    //      char_vars entirely (blob-only). Offline, golden-anchored; DDL shape-asserted not run.
    static void RunMigrationGate(string questBlobHex, string missionBlobHex, string assaultBlobHex, string eminenceBlobHex, string keyitemsBlobHex)
    {
        Console.WriteLine("\n== XI.Quests: unified per-character migration seam (6 blobs + shared char_vars) ==");

        byte[] qB = HexToBytes(questBlobHex), mB = HexToBytes(missionBlobHex),
               aB = HexToBytes(assaultBlobHex), eB = HexToBytes(eminenceBlobHex),
               kB = HexToBytes(keyitemsBlobHex);

        Check("migration:all-tables", "6", CharacterStateMigration.AllCreateTables.Length.ToString());

        // a mixed char_vars set: quest prefix + mission prefix + FLAT assault globals
        var allVars = new[]
        {
            new CharVar("Quest[0][1]Prog", 5),
            new CharVar("Quest[0][1]mustZone", 77),
            new CharVar("Mission[4][10]Prog", 9),
            new CharVar("assaultEntered", 12),
            new CharVar("AssaultComplete", 1),
            new CharVar("AssaultFailed", 0),
        };
        uint charId = 0x5eed;
        LegacyCharRow Make(IEnumerable<CharVar> vars) => new LegacyCharRow
        {
            CharId = charId, QuestsBlob = qB, MissionsBlob = mB, AssaultBlob = aB, EminenceBlob = eB,
            KeyitemsBlob = kB, CharVars = vars,
        };

        var st = CharacterStateMigration.Import(Make(allVars), null);
        var back = CharacterStateMigration.Project(st);

        // (a) five-way simultaneous parity
        var qblob = new QuestBitBlob(); qblob.LoadFromBlob(qB);
        string qCanonical = QuestWireCodec.Project(QuestWireCodec.Extract(qblob)).ToHex();
        Check("migration:parity-quests-canonical", qCanonical, ToHex(back.QuestsBlob));
        Check("migration:parity-missions-raw", missionBlobHex, ToHex(back.MissionsBlob));
        Check("migration:parity-assault-raw", assaultBlobHex, ToHex(back.AssaultBlob));
        Check("migration:parity-eminence-raw", eminenceBlobHex, ToHex(back.EminenceBlob));
        Check("migration:parity-keyitems-raw", keyitemsBlobHex, ToHex(back.KeyitemsBlob));
        Check("migration:charid-propagates", charId.ToString(), back.CharId.ToString());

        // positive routing: ImportAll fanned BOTH prefix importers (not one empty list)
        CharQuestStateRow q1 = st.Quests.Find(r => r.QuestId == 1 && (int)r.Area == 0);
        Check("migration:folded-quest-soft", "5|{\"mustZone\":77}", q1 != null ? $"{q1.Step}|{q1.ProgressJson}" : "<missing>");
        CharMissionStateRow m4 = st.Missions.Find(r => (int)r.Area == 4);
        bool mf = m4 != null && m4.SoftVars.TryGetValue("10:Prog", out long mv) && mv == 9;
        Check("migration:folded-mission-soft", "True", mf ? "True" : "False");

        // (b) routing: full mixed set perturbs neither importer vs its own-prefix-only subset
        var stQ = CharacterStateMigration.Import(Make(new[] { allVars[0], allVars[1] }), null); // Quest[*] only
        var stM = CharacterStateMigration.Import(Make(new[] { allVars[2] }), null);              // Mission[*] only
        Check("migration:routing-quest-unperturbed", QuestFingerprint(stQ.Quests), QuestFingerprint(st.Quests));
        Check("migration:routing-mission-unperturbed", MissionFingerprint(stM.Missions), MissionFingerprint(st.Missions));

        // flat assault globals must not fabricate a quest/mission row (counts equal the subset runs)
        Check("migration:no-fabricate-quest", stQ.Quests.Count.ToString(), st.Quests.Count.ToString());
        Check("migration:no-fabricate-mission", stM.Missions.Count.ToString(), st.Missions.Count.ToString());

        // assault is a single container row; assault/eminence/keyitems importers ignore char_vars
        // entirely (they take only their blob -- no per-id soft-state prefix to fold)
        Check("migration:assault-row-single", "True", st.Assault != null ? "True" : "False");
        var stNoVars = CharacterStateMigration.Import(Make(null), null);
        Check("migration:assault-blob-only", ToHex(AssaultWireCodec.Project(AssaultStateImporter.ToCell(stNoVars.Assault)).Bytes), assaultBlobHex);
        Check("migration:eminence-blob-only", EminenceWireCodec.Project(EminenceStateImporter.ToCells(stNoVars.Eminence)).ToHex(), eminenceBlobHex);
        Check("migration:keyitems-blob-only", KeyItemWireCodec.Project(KeyItemStateImporter.ToCells(stNoVars.Keyitems)).ToHex(), keyitemsBlobHex);

        // the five importers are mutually non-interfering: the full mixed char_vars set (which
        // includes the flat assault globals) leaves the keyitems table byte-identical to the
        // no-char-vars run -- keyitems has no prefix regex, so no var can fabricate/disturb a row.
        Check("migration:keyitems-unperturbed",
            KeyItemWireCodec.Project(KeyItemStateImporter.ToCells(stNoVars.Keyitems)).ToHex(),
            KeyItemWireCodec.Project(KeyItemStateImporter.ToCells(st.Keyitems)).ToHex());
    }

    // ---- BIND THROUGH: prove CharacterStateMigration's Bind* methods emit a well-typed
    //      BoundValue[] per row against the REAL XI.Persistence binder, honoring the faithful
    //      no-int64 rule: every 64-bit column (quest repeat_by tick + updated_at, mission
    //      completed_mask + updated_at, assault/eminence updated_at) binds as String kind; the
    //      assault completed_mask (varbinary) binds as Blob. Real imported rows are diffed against
    //      the schema Kind vector; then targeted 64-bit round-trips prove the String encoding is
    //      LOSSLESS where an int64 bind is impossible (a daily tick past uint32, a completed_mask
    //      with bit 63 set that a (long) cast would sign-corrupt, the inactive-slot sentinel).
    static void RunBindGate(string questBlobHex, string missionBlobHex, string assaultBlobHex, string eminenceBlobHex, string keyitemsBlobHex)
    {
        Console.WriteLine("\n== XI.Quests: migration seam binds rows through XI.Persistence BoundValue ==");

        byte[] qB = HexToBytes(questBlobHex), mB = HexToBytes(missionBlobHex),
               aB = HexToBytes(assaultBlobHex), eB = HexToBytes(eminenceBlobHex),
               kB = HexToBytes(keyitemsBlobHex);
        uint charId = 0x5eed;
        // seed a quest soft-var so the quest table ALWAYS has a real imported row to bind:
        // QuestWireCodec.Extract emits only non-Available cells, and the chosen quest golden
        // "mixed" blob may carry none -- this guarantees the real-data quest fidelity below runs
        // on an importer-produced row (Status=Available, Step=3, progress={"mustZone":42}) that
        // binds through all 9 QuestBindKinds, rather than vacuously passing on an empty set.
        var st = CharacterStateMigration.Import(
            new LegacyCharRow
            {
                CharId = charId, QuestsBlob = qB, MissionsBlob = mB, AssaultBlob = aB, EminenceBlob = eB,
                KeyitemsBlob = kB,
                CharVars = new[] { new CharVar("Quest[0][1]Prog", 3), new CharVar("Quest[0][1]mustZone", 42) },
            },
            null);

        // -- real-data Kind-vector fidelity: every row of each table binds to the schema vector,
        //    and the bound arity == that importer's InsertSql `?` placeholder count. --
        bool qOk = st.Quests.Count > 0;
        foreach (var r in st.Quests)
            if (KindsOf(CharacterStateMigration.BindQuest(r)) != KindsOfVector(CharacterStateMigration.QuestBindKinds)) qOk = false;
        Check("bind:quest-kind-vector", "True", qOk ? "True" : "False");
        Check("bind:quest-arity", Placeholders(QuestStateImporter.InsertSql).ToString(),
            (st.Quests.Count > 0 ? CharacterStateMigration.BindQuest(st.Quests[0]).Length : 0).ToString());

        bool miOk = true;
        foreach (var r in st.Missions)
            if (KindsOf(CharacterStateMigration.BindMission(r)) != KindsOfVector(CharacterStateMigration.MissionBindKinds)) miOk = false;
        Check("bind:mission-kind-vector", "True", miOk ? "True" : "False");
        Check("bind:mission-arity", Placeholders(MissionStateImporter.InsertSql).ToString(),
            CharacterStateMigration.BindMission(st.Missions[0]).Length.ToString());

        Check("bind:assault-kind-vector", KindsOfVector(CharacterStateMigration.AssaultBindKinds),
            KindsOf(CharacterStateMigration.BindAssault(st.Assault)));
        Check("bind:assault-arity", Placeholders(AssaultStateImporter.InsertSql).ToString(),
            CharacterStateMigration.BindAssault(st.Assault).Length.ToString());

        bool eOk = true;
        foreach (var r in st.Eminence)
            if (KindsOf(CharacterStateMigration.BindEminence(r)) != KindsOfVector(CharacterStateMigration.EminenceBindKinds)) eOk = false;
        Check("bind:eminence-kind-vector", "True", eOk ? "True" : "False");
        Check("bind:eminence-arity", Placeholders(EminenceStateImporter.InsertSql).ToString(),
            (st.Eminence.Count > 0 ? CharacterStateMigration.BindEminence(st.Eminence[0]).Length : 0).ToString());

        // -- keyitems fidelity: assert NON-EMPTINESS first (the "mixed" golden carries real bits),
        //    then diff every importer-produced row against KeyItemBindKinds and the arity. This
        //    closes the vacuous-fidelity gotcha -- a foreach over an empty set would pass silently. --
        Check("bind:keyitem-nonempty", "True", st.Keyitems.Count > 0 ? "True" : "False");
        bool kOk = st.Keyitems.Count > 0;
        foreach (var r in st.Keyitems)
            if (KindsOf(CharacterStateMigration.BindKeyItem(r)) != KindsOfVector(CharacterStateMigration.KeyItemBindKinds)) kOk = false;
        Check("bind:keyitem-kind-vector", "True", kOk ? "True" : "False");
        Check("bind:keyitem-arity", Placeholders(KeyItemStateImporter.InsertSql).ToString(),
            (st.Keyitems.Count > 0 ? CharacterStateMigration.BindKeyItem(st.Keyitems[0]).Length : 0).ToString());
        // targeted widths on a real row: charid UInt32, key_item_id UInt16 (smallint), the two bits
        // Bool, updated_at String (64-bit epoch, no int64 bind).
        if (st.Keyitems.Count > 0)
        {
            var kr = CharacterStateMigration.BindKeyItem(st.Keyitems[0]);
            Check("bind:keyitem-id-is-uint16", "UInt16", kr[1].Type.ToString());
            Check("bind:keyitem-possessed-is-bool", "Bool", kr[2].Type.ToString());
            Check("bind:keyitem-seen-is-bool", "Bool", kr[3].Type.ToString());
            Check("bind:keyitem-updatedat-is-string", "String", kr[4].Type.ToString());
        }
        else Bad("bind:keyitem-targeted: no row to bind (golden empty?)");

        // -- 64-bit -> String is structurally FORCED: the ported BoundValue Kind has no int64/uint64
        //    case (a raw bind is a C++ compile error), so the only lossless route is a decimal string. --
        bool any64 = false;
        foreach (BoundValue.Kind k in System.Enum.GetValues(typeof(BoundValue.Kind)))
        {
            string n = k.ToString();
            if (n == "Int64" || n == "UInt64") any64 = true;
        }
        Check("bind:no-int64-kind", "True", any64 ? "False" : "True");

        // (1) daily reopen tick 69,206,400,000 > uint32 max (4,294,967,295): a UInt32 bind is impossible
        var daily = new CharQuestStateRow
        {
            CharId = charId, QuestKey = "windurst.merfrost_courier", Area = (QuestLog)2, QuestId = 42,
            Status = (QuestStatus)2, Step = 0, RepeatByVanaTime = 69206400000L, UpdatedAt = 1_700_000_000L,
        };
        var dbind = CharacterStateMigration.BindQuest(daily);
        Check("bind:repeatby-is-string", "String", dbind[7].Type.ToString());
        Check("bind:repeatby-lossless", "69206400000", dbind[7].AsString);
        Check("bind:repeatby-exceeds-uint32", "True", daily.RepeatByVanaTime > uint.MaxValue ? "True" : "False");

        // (2) mission completed_mask with bit 63 set: UStr64 must bind the UNSIGNED decimal
        //     (9223372036854775808), which a (long) cast would render negative.
        var maskRow = new CharMissionStateRow
        {
            CharId = charId, Area = (MissionLog)0, Current = 0, Status = 0,
            CompletedMask = (1UL << 63) | 7UL, UpdatedAt = 0,
        };
        var mb = CharacterStateMigration.BindMission(maskRow);
        Check("bind:mask-is-string", "String", mb[4].Type.ToString());
        Check("bind:mask-unsigned-decimal", (9223372036854775808UL + 7UL).ToString(), mb[4].AsString);
        Check("bind:mask-roundtrips", "True",
            ulong.Parse(mb[4].AsString, System.Globalization.CultureInfo.InvariantCulture) == maskRow.CompletedMask ? "True" : "False");
        Check("bind:mask-would-overflow-long", "True", maskRow.CompletedMask > long.MaxValue ? "True" : "False");

        // (3) assault completed_mask binds as Blob, byte-for-byte
        var abind = CharacterStateMigration.BindAssault(st.Assault);
        Check("bind:assault-mask-is-blob", "Blob", abind[2].Type.ToString());
        byte[] ab = abind[2].AsBlob;
        bool abMatch = ab.Length == st.Assault.CompletedMask.Length;
        for (int i = 0; abMatch && i < ab.Length; i++) if (ab[i] != st.Assault.CompletedMask[i]) abMatch = false;
        Check("bind:assault-mask-bytes-preserved", "True", abMatch ? "True" : "False");

        // (4) eminence inactive-slot sentinel: Slot -1 -> bound byte 255 (tinyint unsigned has no -1)
        CharEminenceStateRow inactive = null;
        foreach (var r in st.Eminence) if (r.Slot < 0) { inactive = r; break; }
        if (inactive != null)
            Check("bind:eminence-inactive-sentinel", "255", CharacterStateMigration.BindEminence(inactive)[2].AsUInt8.ToString());
        else
            Ok("bind:eminence-inactive-sentinel (no inactive cell in this golden, skipped)");
    }

    static List<AScen> RunAssaultGate()
    {
        Console.WriteLine("\n== XI.Quests: assault blob (chars.assault 130B) vs engine-verbatim oracle ==");

        string goldenPath = @"H:\023-FFXI-server\csharp\oracle\assault_golden.txt";
        if (!File.Exists(goldenPath))
        {
            Bad("assault golden not found: " + goldenPath + " (run oracle/build_assault.bat)");
            return new List<AScen>();
        }

        var scen = new Dictionary<string, AScen>();
        var order = new List<AScen>();
        AScen Get(string n)
        {
            if (!scen.TryGetValue(n, out var s)) { s = new AScen { Name = n }; scen[n] = s; order.Add(s); }
            return s;
        }

        long expectedSize = 0;
        foreach (string raw in File.ReadAllLines(goldenPath))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;

            var meta = Regex.Match(line, @"^META\s+sizeof_al\s+(\d+)$");
            if (meta.Success) { expectedSize = long.Parse(meta.Groups[1].Value); continue; }

            var aop = Regex.Match(line, @"^SCEN\s+(\S+)\s+AOP\s+(ADD|COMPLETE|DEL)\s+(\d+)$");
            if (aop.Success)
            {
                AKind k = aop.Groups[2].Value switch
                {
                    "ADD" => AKind.Add,
                    "COMPLETE" => AKind.Complete,
                    _ => AKind.Del,
                };
                Get(aop.Groups[1].Value).Ops.Add(new AOp { Kind = k, Id = int.Parse(aop.Groups[3].Value) });
                continue;
            }
            var ablob = Regex.Match(line, @"^SCEN\s+(\S+)\s+BLOB\s+([0-9A-Fa-f]+)$");
            if (ablob.Success) { Get(ablob.Groups[1].Value).Blob = ablob.Groups[2].Value.ToUpperInvariant(); continue; }

            var acur = Regex.Match(line, @"^ASTAT\s+(\S+)\s+CUR\s+(\d+)$");
            if (acur.Success) { Get(acur.Groups[1].Value).Stats.Add(new AStat { Kind = AStatKind.Cur, Expected = acur.Groups[2].Value }); continue; }
            var ahas = Regex.Match(line, @"^ASTAT\s+(\S+)\s+HAS\s+(\d+)\s+([01])$");
            if (ahas.Success) { Get(ahas.Groups[1].Value).Stats.Add(new AStat { Kind = AStatKind.Has, Id = int.Parse(ahas.Groups[2].Value), Expected = ahas.Groups[3].Value }); continue; }

            Console.WriteLine("  [warn] unparsed assault golden line: " + line);
        }

        // -- struct size (130 = 2 + 128; the fresh init is all-zero, so this anchors everything) --
        Check("assault:sizeof", expectedSize.ToString(), AssaultBitBlob.Size.ToString());

        foreach (var sc in order)
        {
            if (string.IsNullOrEmpty(sc.Blob)) { Bad($"assault {sc.Name}: golden has no BLOB line"); continue; }

            var blob = new AssaultBitBlob(); // all-zero init mirrors m_assaultLog{} + harness reset()
            foreach (var o in sc.Ops)
            {
                switch (o.Kind)
                {
                    case AKind.Add: blob.AddAssault((byte)o.Id); break;
                    case AKind.Complete: blob.CompleteAssault((byte)o.Id); break;
                    case AKind.Del: blob.DelAssault((byte)o.Id); break;
                }
            }

            // the raw 130-byte blob, byte-for-byte
            string got = blob.ToHex();
            Check($"assault:blob {sc.Name}", sc.Blob, got);
            if (got != sc.Blob) Console.WriteLine($"      first diff at byte {FirstDiffByte(sc.Blob, got)}");

            // LoadFromBlob reproduces the golden bytes verbatim (extractFromBlob path)
            var loaded = new AssaultBitBlob();
            loaded.LoadFromBlob(HexToBytes(sc.Blob));
            Check($"assault:load-roundtrip {sc.Name}", sc.Blob, loaded.ToHex());

            // every accessor read-back
            foreach (var s in sc.Stats)
            {
                string actual = s.Kind switch
                {
                    AStatKind.Cur => ((uint)blob.GetCurrentAssault()).ToString(),
                    AStatKind.Has => (blob.HasCompletedAssault((byte)s.Id) ? 1 : 0).ToString(),
                    _ => "?",
                };
                Check($"assault:{s.Kind} {sc.Name} {s.Id}", s.Expected, actual);
            }
        }
        Console.WriteLine($"  (assault scenarios replayed: {order.Count})");
        return order;
    }

    // ---- KEYITEMS blob: replay each scenario from oracle/keyitems_golden.txt against the
    //      byte-for-byte KeyItemsBitBlob port (chars.keyitems, 1024B = 8 tables x {keyList 64B,
    //      seenList 64B}). Proves the /512 %512 cross-table addressing, the `table >= 8` capacity
    //      guard (writes no-op, reads false), and the keyList/seenList independence -- all against
    //      the engine accessors compiled into the harness. Returns the scenarios for later model gates.
    static List<KScen> RunKeyItemsGate()
    {
        Console.WriteLine("\n== XI.Quests: keyitems blob (chars.keyitems 1024B) vs engine-verbatim oracle ==");

        string goldenPath = @"H:\023-FFXI-server\csharp\oracle\keyitems_golden.txt";
        if (!File.Exists(goldenPath))
        {
            Bad("keyitems golden not found: " + goldenPath + " (run oracle/build_keyitems.bat)");
            return new List<KScen>();
        }

        var scen = new Dictionary<string, KScen>();
        var order = new List<KScen>();
        KScen Get(string n)
        {
            if (!scen.TryGetValue(n, out var s)) { s = new KScen { Name = n }; scen[n] = s; order.Add(s); }
            return s;
        }

        long expectedSize = 0;
        foreach (string raw in File.ReadAllLines(goldenPath))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;

            var meta = Regex.Match(line, @"^META\s+sizeof_keys\s+(\d+)$");
            if (meta.Success) { expectedSize = long.Parse(meta.Groups[1].Value); continue; }

            var kop = Regex.Match(line, @"^SCEN\s+(\S+)\s+KOP\s+(ADD|DEL|SEEN|UNSEEN)\s+(\d+)$");
            if (kop.Success)
            {
                KKind k = kop.Groups[2].Value switch
                {
                    "ADD" => KKind.Add,
                    "DEL" => KKind.Del,
                    "SEEN" => KKind.Seen,
                    _ => KKind.Unseen,
                };
                Get(kop.Groups[1].Value).Ops.Add(new KOp { Kind = k, Id = int.Parse(kop.Groups[3].Value) });
                continue;
            }
            var kblob = Regex.Match(line, @"^SCEN\s+(\S+)\s+BLOB\s+([0-9A-Fa-f]+)$");
            if (kblob.Success) { Get(kblob.Groups[1].Value).Blob = kblob.Groups[2].Value.ToUpperInvariant(); continue; }

            var khas = Regex.Match(line, @"^KSTAT\s+(\S+)\s+HAS\s+(\d+)\s+([01])$");
            if (khas.Success) { Get(khas.Groups[1].Value).Stats.Add(new KStat { Kind = KStatKind.Has, Id = int.Parse(khas.Groups[2].Value), Expected = khas.Groups[3].Value }); continue; }
            var kseen = Regex.Match(line, @"^KSTAT\s+(\S+)\s+SEEN\s+(\d+)\s+([01])$");
            if (kseen.Success) { Get(kseen.Groups[1].Value).Stats.Add(new KStat { Kind = KStatKind.Seen, Id = int.Parse(kseen.Groups[2].Value), Expected = kseen.Groups[3].Value }); continue; }

            Console.WriteLine("  [warn] unparsed keyitems golden line: " + line);
        }

        // -- struct size (1024 = 8 tables * (64 keyList + 64 seenList); fresh init all-zero) --
        Check("keyitems:sizeof", expectedSize.ToString(), KeyItemsBitBlob.Size.ToString());

        foreach (var sc in order)
        {
            if (string.IsNullOrEmpty(sc.Blob)) { Bad($"keyitems {sc.Name}: golden has no BLOB line"); continue; }

            var blob = new KeyItemsBitBlob(); // all-zero init mirrors value-init bitsets + harness reset()
            foreach (var o in sc.Ops)
            {
                switch (o.Kind)
                {
                    case KKind.Add: blob.AddKeyItem((ushort)o.Id); break;
                    case KKind.Del: blob.DelKeyItem((ushort)o.Id); break;
                    case KKind.Seen: blob.MarkSeenKeyItem((ushort)o.Id); break;
                    case KKind.Unseen: blob.UnseenKeyItem((ushort)o.Id); break;
                }
            }

            // the raw 1024-byte blob, byte-for-byte
            string got = blob.ToHex();
            Check($"keyitems:blob {sc.Name}", sc.Blob, got);
            if (got != sc.Blob) Console.WriteLine($"      first diff at byte {FirstDiffByte(sc.Blob, got)}");

            // LoadFromBlob reproduces the golden bytes verbatim (extractFromBlob path)
            var loaded = new KeyItemsBitBlob();
            loaded.LoadFromBlob(HexToBytes(sc.Blob));
            Check($"keyitems:load-roundtrip {sc.Name}", sc.Blob, loaded.ToHex());

            // every accessor read-back (hasKeyItem / seenKeyItem, both capacity-guarded)
            foreach (var s in sc.Stats)
            {
                string actual = s.Kind switch
                {
                    KStatKind.Has => (blob.HasKeyItem((ushort)s.Id) ? 1 : 0).ToString(),
                    KStatKind.Seen => (blob.SeenKeyItem((ushort)s.Id) ? 1 : 0).ToString(),
                    _ => "?",
                };
                Check($"keyitems:{s.Kind} {sc.Name} {s.Id}", s.Expected, actual);
            }
        }
        Console.WriteLine($"  (keyitems scenarios replayed: {order.Count})");
        return order;
    }

    // ---- KEYITEMS normalized model + offline importer: the per-key-item char_keyitem_state
    //      half symmetric to RunEminenceModelGate. Each of the 4096 id-space entries carries two
    //      ORTHOGONAL bits (keyList "possessed" + seenList "seen"), so a cell is one row per distinct
    //      id with either bit set. No SoftVars layer (grep of scripts/ shows no per-id key-item char_var,
    //      only the quest container's vars.Prog). Reuses the SAME keyitems_golden.txt raw bytes that
    //      anchor KeyItemsBitBlob.
    static void RunKeyItemModelGate(List<KScen> scens)
    {
        Console.WriteLine("\n== XI.Quests: keyitems normalized model (char_keyitem_state) ==");

        // -- DDL / INSERT shape --
        Check("keyitem-ddl:columns", "5", KeyItemStateSchema.Columns.Length.ToString());
        int ph = 0;
        foreach (var ch in KeyItemStateImporter.InsertSql) if (ch == '?') ph++;
        Check("keyitem-ddl:insert-placeholders", "5", ph.ToString());
        Check("keyitem-ddl:create-table-pk", "True",
            KeyItemStateSchema.CreateTableSql.Contains("PRIMARY KEY (`charid`, `key_item_id`)") ? "True" : "False");

        uint charId = 0x99aa;
        foreach (var sc in scens)
        {
            if (string.IsNullOrEmpty(sc.Blob)) { Bad($"keyitem-model {sc.Name}: golden has no BLOB line"); continue; }
            byte[] raw = HexToBytes(sc.Blob);

            // load the authoritative golden bytes (ground truth, not a re-replay of ops)
            var blob = new KeyItemsBitBlob();
            blob.LoadFromBlob(raw);

            // Extract all distinct cells; Project reproduces the RAW golden bytes (lossless -- both
            // orthogonal bitsets are captured per id, no bit-pair collapse as in quests).
            var cells = KeyItemWireCodec.Extract(blob);

            // a cell may not repeat an id (each id maps to exactly one bit in each bitset).
            var ids = new HashSet<int>();
            bool idsDistinct = true;
            foreach (var c in cells) if (!ids.Add(c.KeyItemId)) idsDistinct = false;
            Check($"keyitem:ids-distinct {sc.Name}", "True", idsDistinct ? "True" : "False");

            var back = KeyItemWireCodec.Project(cells);
            Check($"keyitem:codec-lossless {sc.Name}", sc.Blob, back.ToHex());
            if (back.ToHex() != sc.Blob) Console.WriteLine($"      first diff at byte {FirstDiffByte(sc.Blob, back.ToHex())}");

            // idempotent round-trip
            var back2 = KeyItemWireCodec.Project(KeyItemWireCodec.Extract(back));
            Check($"keyitem:codec-idempotent {sc.Name}", back.ToHex(), back2.ToHex());

            // importer: rows -> cells -> Project reproduces the raw golden blob byte-for-byte
            var rows = KeyItemStateImporter.Import(charId, raw);
            Check($"keyitem:import-project {sc.Name}", sc.Blob, KeyItemWireCodec.Project(KeyItemStateImporter.ToCells(rows)).ToHex());
            Check($"keyitem:import-rowcount {sc.Name}", cells.Count.ToString(), rows.Count.ToString());
            Check($"keyitem:import-charid {sc.Name}", charId.ToString(), (rows.Count > 0 ? rows[0].CharId : charId).ToString());

            // the HAS / SEEN golden read-backs must survive extraction onto the normalized model.
            // A cell absent from the list means neither bit set -> both reads false, matching the
            // capacity-guarded / never-added expectations in the golden (e.g. HAS 4096, HAS 6000).
            foreach (var s in sc.Stats)
            {
                KeyItemCell cell = null;
                foreach (var c in cells) if (c.KeyItemId == (ushort)s.Id) { cell = c; break; }
                string actual = s.Kind switch
                {
                    KStatKind.Has  => ((cell != null && cell.Possessed) ? 1 : 0).ToString(),
                    KStatKind.Seen => ((cell != null && cell.Seen) ? 1 : 0).ToString(),
                    _ => "?",
                };
                Check($"keyitem:cell {s.Kind} {sc.Name} {s.Id}", s.Expected, actual);
            }
        }

        // -- keyList and seenList are INDEPENDENT: seen_without_possession carries the seen bit
        //    only (Possessed=false, Seen=true); add-then-del drops keyList but the seen bit SURVIVES. --
        {
            var kb = new KeyItemsBitBlob();
            kb.MarkSeenKeyItem(500);
            var cs = KeyItemWireCodec.Extract(kb);
            Check("keyitem:seen-only-one-row", "1", cs.Count.ToString());
            Check("keyitem:seen-only-state", "True",
                (cs.Count == 1 && !cs[0].Possessed && cs[0].Seen) ? "True" : "False");

            var kb2 = new KeyItemsBitBlob();
            kb2.AddKeyItem(500); kb2.MarkSeenKeyItem(500); kb2.DelKeyItem(500);
            var cs2 = KeyItemWireCodec.Extract(kb2);
            Check("keyitem:drop-keeps-seen", "True",
                (cs2.Count == 1 && !cs2[0].Possessed && cs2[0].Seen) ? "True" : "False");
            // a cell with neither bit set is never emitted (ids carry no placeholder rows).
            var kb3 = new KeyItemsBitBlob();
            kb3.AddKeyItem(7); kb3.DelKeyItem(7);
            Check("keyitem:both-clear-no-row", "0", KeyItemWireCodec.Extract(kb3).Count.ToString());
        }
    }

    // ---- ASSAULT normalized model + offline importer: the single-row-per-char
    //      char_assault_state half symmetric to RunMissionModelGate / RunEminenceModelGate.
    //      Assault is a SINGLE container (130B = uint16 current + bool complete[128]), so the
    //      normalized shape is exactly ONE row per char (current + a 16-byte 128-bit mask), NOT
    //      per-area (missions) or per-record (eminence). No SoftVars layer: container.lua shows
    //      assault char_vars are flat globals (assaultEntered/AssaultComplete/...), never
    //      per-id `Assault[i]Prog`, so there is nothing per-container to fold. Reuses the SAME
    //      assault_golden.txt raw bytes that anchor AssaultBitBlob.
    static void RunAssaultModelGate(List<AScen> scens)
    {
        Console.WriteLine("\n== XI.Quests: assault normalized model (char_assault_state) ==");

        // -- DDL / INSERT shape --
        Check("assault-ddl:columns", "4", AssaultStateSchema.Columns.Length.ToString());
        int ph = 0;
        foreach (var ch in AssaultStateImporter.InsertSql) if (ch == '?') ph++;
        Check("assault-ddl:insert-placeholders", "4", ph.ToString());
        Check("assault-ddl:create-table-pk", "True",
            AssaultStateSchema.CreateTableSql.Contains("PRIMARY KEY (`charid`)") ? "True" : "False");

        uint charId = 0x99aa;
        foreach (var sc in scens)
        {
            if (string.IsNullOrEmpty(sc.Blob)) { Bad($"assault-model {sc.Name}: golden has no BLOB line"); continue; }
            byte[] raw = HexToBytes(sc.Blob);

            // load the authoritative golden bytes (ground truth, not a re-replay of ops)
            var blob = new AssaultBitBlob();
            blob.LoadFromBlob(raw);

            // raw complete[] bytes must all be 0/1 for the bit-mask collapse to be byte-exact
            bool maskFaithful = true;
            for (int id = 0; id < AssaultCell.CompletedBits; id++)
                if (raw[2 + id] > 1) maskFaithful = false;
            Check($"assault:complete-bytes-01 {sc.Name}", "True", maskFaithful ? "True" : "False");

            // Extract one cell; Project reproduces the RAW golden bytes (lossless, 1 row/char).
            var cell = AssaultWireCodec.Extract(blob);
            var back = AssaultWireCodec.Project(cell);
            Check($"assault:codec-lossless {sc.Name}", sc.Blob, back.ToHex());
            if (back.ToHex() != sc.Blob) Console.WriteLine($"      first diff at byte {FirstDiffByte(sc.Blob, back.ToHex())}");

            // idempotent round-trip
            var back2 = AssaultWireCodec.Project(AssaultWireCodec.Extract(back));
            Check($"assault:codec-idempotent {sc.Name}", back.ToHex(), back2.ToHex());

            // importer: the single row -> cell -> Project reproduces the raw golden blob byte-for-byte
            var row = AssaultStateImporter.Import(charId, raw);
            Check($"assault:import-charid {sc.Name}", charId.ToString(), row.CharId.ToString());
            Check($"assault:import-project {sc.Name}", sc.Blob, AssaultWireCodec.Project(AssaultStateImporter.ToCell(row)).ToHex());

            // the CUR/HAS golden read-backs must survive extraction onto the normalized cell
            foreach (var s in sc.Stats)
            {
                string actual = s.Kind switch
                {
                    AStatKind.Cur => cell.Current.ToString(),
                    AStatKind.Has => (cell.Completed(s.Id) ? 1 : 0).ToString(),
                    _ => "?",
                };
                Check($"assault:cell {s.Kind} {sc.Name} {s.Id}", s.Expected, actual);
            }
        }
    }

    // Index of the first differing byte between two equal-length hex strings (for diagnostics).
    static int FirstDiffByte(string a, string b)
    {
        int n = Math.Min(a.Length, b.Length) / 2;
        for (int i = 0; i < n; i++)
            if (!string.Equals(a.Substring(i * 2, 2), b.Substring(i * 2, 2), StringComparison.OrdinalIgnoreCase))
                return i;
        return a.Length != b.Length ? n : -1;
    }

    // ---- authoring core: state machine + validator + projection-while-walking ----
    // Reference quest (bastok.elvaan_goldsmith) exercising the full redesign surface:
    // stable string id, typed progress steps, explicit Accept/Complete verbs, declarative
    // guards + effects, and the normalized->legacy wire projection at each step.
    sealed class ElvaanGoldsmith : QuestDefinition
    {
        public const ushort IngotItem = 512;   // Bastok Mine Ingot (trade item)
        public const ushort OreKeyItem = 1000; // Aluminum Ore reward
        public override string Id => "bastok.elvaan_goldsmith";
        public override QuestWireRef Wire => new QuestWireRef(QuestLog.Bastok, 18);
        public override void Build(QuestBuilder b)
        {
            b.State(0, "NotStarted");
            b.State(1, "IngotRequested");
            b.State(2, "IngotDelivered", terminal: true);
            b.Initial(0);
            b.On(0, QuestTrigger.Interact, to: 1, QuestAction.Accept());
            b.On(1, QuestTrigger.Trade, to: 2,
                ctx => ctx.TradedItem == IngotItem,
                QuestAction.Message(12345),
                QuestAction.KeyItem(OreKeyItem),
                QuestAction.Reputation(101, 5),
                QuestAction.RoE(200),
                QuestAction.Complete());
        }
    }

    // A REPEATABLE (daily) reference quest: the reopen window is stamped from the injected
    // Vana'diel clock on Complete, via the byte-proven VanaTime.NextMidnightTicks.
    sealed class WindurstCourier : QuestDefinition
    {
        public const ushort ParcelItem = 4480;
        public override string Id => "windurst.merfrost_courier";
        public override QuestWireRef Wire => new QuestWireRef(QuestLog.Windurst, 42);
        public override void Build(QuestBuilder b)
        {
            b.Repeat(QuestRepeat.Daily);
            b.State(0, "NotStarted");
            b.State(1, "ParcelRequested");
            b.State(2, "ParcelDelivered", terminal: true);
            b.Initial(0);
            b.On(0, QuestTrigger.Interact, to: 1, QuestAction.Accept());
            b.On(1, QuestTrigger.Trade, to: 2,
                ctx => ctx.TradedItem == ParcelItem,
                QuestAction.Message(9999),
                QuestAction.Complete());
        }
    }

    // A multi-beat MISSION definition (the unified-taxonomy authoring proof): one area's
    // storyline = activate id -> trade -> complete id -> activate next -> ... -> terminal.
    // Its normalized MissionProgress projects onto the byte-for-byte-verified MissionBitBlob.
    sealed class SanDoriaPrologue : MissionDefinition
    {
        public const ushort TokenItem = 4500; // the trade item gating beat 1 -> 2
        public override string Id => "sandoria.prologue";
        public override MissionLog Area => MissionLog.Sandoria; // area 0: sentinel current = 0xFFFF
        public override void Build(MissionBuilder b)
        {
            b.State(0, -1, "NotStarted");
            b.State(1, 0, "BeatOne");
            b.State(2, 1, "BeatTwo");
            b.State(3, -1, "Done", terminal: true);
            b.Initial(0);
            b.On(0, QuestTrigger.Interact, to: 1, MissionAction.Activate(0));
            b.On(1, QuestTrigger.Trade, to: 2,
                ctx => ctx.TradedItem == TokenItem,
                MissionAction.Complete(0),
                MissionAction.Activate(1));
            b.On(2, QuestTrigger.Talk, to: 3,
                MissionAction.StatusNibble(0, 5),
                MissionAction.Complete(1));
        }
    }

    static MissionMachine MM(string id, Action<MissionBuilder> def)
    {
        var b = new MissionBuilder();
        def(b);
        return b.Build(id, MissionLog.Bastok);
    }

    static QuestMachine M(string id, Action<QuestBuilder> def)
    {
        var b = new QuestBuilder();
        def(b);
        return b.Build(id, new QuestWireRef(QuestLog.Jeuno, 0));
    }

    static void ExpectValid(string name, QuestMachine m)
    {
        var v = QuestValidator.Validate(m);
        Check($"validate:ok {name}", "valid(0e/0w)", v.IsValid && v.ErrorCount == 0 && v.WarningCount == 0 ? "valid(0e/0w)" : $"bad(e={v.ErrorCount},w={v.WarningCount})");
        foreach (var d in v.Diagnostics) Console.WriteLine("      " + d);
    }

    static void ExpectCode(string name, QuestMachine m, string code)
    {
        var v = QuestValidator.Validate(m);
        Check($"validate:{code} {name}", "true", v.Has(code) ? "true" : "false");
        if (!v.Has(code)) foreach (var d in v.Diagnostics) Console.WriteLine("      (got) " + d);
    }

    static void RunAuthoringGate()
    {
        Console.WriteLine("\n== XI.Quests: authoring core (state machine + validator) ==");

        // ---- reference quest validates clean ----
        var q = new ElvaanGoldsmith();
        var machine = q.Compile();
        ExpectValid("elvaan_goldsmith", machine);
        int area = (int)machine.Wire.Area;
        ushort id = machine.Wire.QuestId;

        // ---- headless walk: NotStarted -> Accepted -> (guard rejects) -> Completed ----
        var prog = new QuestProgress(machine.Id, machine.Initial.Step);

        var blob0 = new QuestBitBlob();
        QuestWireProjector.Project(machine.Wire, prog, blob0);
        Check("walk:start", "Available", blob0.GetStatus(area, id).ToString());
        Check("walk:lifecycle:start", "Available", prog.Lifecycle.ToString());

        // Wrong trigger (Trade before accepting) must not advance.
        var ctxBad = new QuestContext(0x1111, prog, QuestTrigger.Trade, tradedItem: ElvaanGoldsmith.IngotItem);
        Check("walk:wrong-trigger-noop", "False", QuestRuntime.Advance(machine, prog, ctxBad).ToString());

        // Accept via Interact.
        var ctx1 = new QuestContext(0x1111, prog, QuestTrigger.Interact);
        bool a1 = QuestRuntime.Advance(machine, prog, ctx1);
        Check("walk:accept-fired", "True", a1.ToString());
        Check("walk:step-after-accept", "1", prog.Step.ToString());
        Check("walk:lifecycle:active", "Active", prog.Lifecycle.ToString());
        Check("walk:effect-accept", "Accept", ctx1.Fired.Count > 0 ? ctx1.Fired[0].Kind.ToString() : "none");
        var blob1 = new QuestBitBlob();
        QuestWireProjector.Project(machine.Wire, prog, blob1);
        Check("walk:wire:accepted", "Accepted", blob1.GetStatus(area, id).ToString());

        // Trade the WRONG item -> guard rejects, no advance.
        var ctx2 = new QuestContext(0x1111, prog, QuestTrigger.Trade, tradedItem: 9999);
        Check("walk:guard-rejects-wrong-item", "False", QuestRuntime.Advance(machine, prog, ctx2).ToString());
        Check("walk:step-unchanged-after-reject", "1", prog.Step.ToString());

        // Trade the RIGHT item -> complete, effects fire in declared order.
        var ctx3 = new QuestContext(0x1111, prog, QuestTrigger.Trade, tradedItem: ElvaanGoldsmith.IngotItem);
        bool a3 = QuestRuntime.Advance(machine, prog, ctx3);
        Check("walk:complete-fired", "True", a3.ToString());
        Check("walk:step-terminal", "2", prog.Step.ToString());
        Check("walk:lifecycle:completed", "Completed", prog.Lifecycle.ToString());
        Check("walk:effect-count", "5", ctx3.Fired.Count.ToString());
        string gotOrder = string.Join(",", ctx3.Fired.ConvertAll(x => x.Kind.ToString()));
        Check("walk:effect-order", "Message,GrantKeyItem,Reputation,RoEObjective,Complete", gotOrder);
        var blob3 = new QuestBitBlob();
        QuestWireProjector.Project(machine.Wire, prog, blob3);
        Check("walk:wire:completed", "Completed", blob3.GetStatus(area, id).ToString());

        // Projection lands in the Extract codec's normalized view (ties to the 51/51 gate).
        var cells = QuestWireCodec.Extract(blob3);
        bool sawCompletedCell = false;
        foreach (var c in cells)
            if (c.Area == machine.Wire.Area && c.Id == id && c.Status == QuestStatus.Completed) sawCompletedCell = true;
        Check("walk:projector-visible-to-codec", "True", sawCompletedCell ? "True" : "False");

        // Terminal is a sink: no further advance.
        var ctx4 = new QuestContext(0x1111, prog, QuestTrigger.Interact);
        Check("walk:terminal-sink", "False", QuestRuntime.Advance(machine, prog, ctx4).ToString());

        // ---- validator negative-cases: each rule fires on a defect ----
        ExpectCode("dangling-to", M("d1", b => { b.Initial(0); b.State(0); b.On(0, QuestTrigger.Interact, to: 99, QuestAction.Accept()); }), "DANGLING_TO");
        ExpectCode("dangling-from", M("d2", b => { b.Initial(0); b.State(0); b.State(1, "t", true); b.On(0, QuestTrigger.Interact, to: 1, QuestAction.Complete()); b.On(7, QuestTrigger.Interact, to: 1, QuestAction.Complete()); }), "DANGLING_FROM");
        ExpectCode("dead-end", M("d3", b => { b.Initial(0); b.State(0); b.State(1); b.On(0, QuestTrigger.Interact, to: 1, QuestAction.Accept()); }), "DEAD_END");
        ExpectCode("no-terminal", M("d4", b => { b.Initial(0); b.State(0); b.State(1); b.On(0, QuestTrigger.Interact, to: 1, QuestAction.Accept()); b.On(1, QuestTrigger.Interact, to: 0, QuestAction.Accept()); }), "NO_TERMINAL");
        ExpectCode("orphan-state", M("d5", b => { b.Initial(0); b.State(0); b.State(2, "t", true); b.State(9, "orphan", true); b.On(0, QuestTrigger.Interact, to: 2, QuestAction.Complete()); }), "ORPHAN_STATE");
        ExpectCode("terminal-unreachable", M("d6", b => { b.Initial(0); b.State(0); b.State(1, "t", true); b.On(0, QuestTrigger.Interact, to: 0, QuestAction.Accept()); }), "TERMINAL_UNREACHABLE");
        ExpectCode("complete-missing", M("d7", b => { b.Initial(0); b.State(0); b.State(1, "t", true); b.On(0, QuestTrigger.Interact, to: 1, QuestAction.Accept()); }), "COMPLETE_MISSING");
        ExpectCode("accept-and-complete", M("d8", b => { b.Initial(0); b.State(0); b.State(1, "t", true); b.On(0, QuestTrigger.Interact, to: 1, QuestAction.Accept(), QuestAction.Complete()); }), "ACCEPT_AND_COMPLETE");
        ExpectCode("duplicate-step", M("d9", b => { b.Initial(0); b.State(0); b.State(0, "dup"); b.State(1, "t", true); b.On(0, QuestTrigger.Interact, to: 1, QuestAction.Complete()); }), "DUPLICATE_STEP");
        ExpectCode("terminal-has-outgoing", M("d10", b => { b.Initial(0); b.State(0); b.State(1, "t", true); b.On(0, QuestTrigger.Interact, to: 1, QuestAction.Complete()); b.On(1, QuestTrigger.Interact, to: 0, QuestAction.Accept()); }), "TERMINAL_HAS_OUTGOING");

        // ---- builder throws (structural, pre-validation) ----
        try { M("throw1", b => { b.State(0); b.On(0, QuestTrigger.Interact, to: 0, QuestAction.Accept()); }); Bad("builder:no-initial-should-throw"); }
        catch (InvalidOperationException) { Ok("builder:no-initial-throws"); }

        try { M("throw2", b => { b.State(0); b.Initial(5); b.On(0, QuestTrigger.Interact, to: 0, QuestAction.Accept()); }); Bad("builder:initial-not-declared-should-throw"); }
        catch (InvalidOperationException) { Ok("builder:initial-step-not-declared-throws"); }
    }

    static bool HasOneOnePair(Scen sc)
    {
        // A (current=1, complete=1) pair only ever comes from a RAW op (the ops never make one).
        foreach (var o in sc.Ops)
            if (o.Kind == OpKind.Raw && o.Cur && o.Comp) return true;
        return false;
    }

    static string StatusName(QuestStatus s) => s switch
    {
        QuestStatus.Available => "Available",
        QuestStatus.Accepted => "Accepted",
        QuestStatus.Completed => "Completed",
        _ => "?" + (int)s,
    };

    // ---- CharProgress: the live server's per-char rows (quest rework piece A) ----
    static string QuestRowsText(IEnumerable<CharQuestStateRow> rows)
    {
        var parts = new List<string>();
        foreach (var r in rows) parts.Add($"{(int)r.Area}/{r.QuestId}|{r.QuestKey}|{(int)r.Status}|{r.Step}|{r.ProgressJson ?? "-"}");
        parts.Sort(StringComparer.Ordinal);
        return string.Join(";", parts);
    }

    static string MissionRowsText(IEnumerable<CharMissionStateRow> rows)
    {
        var parts = new List<string>();
        foreach (var r in rows) parts.Add($"{(int)r.Area}|{r.Current}|{r.Status}|{r.CompletedMask}|{r.ProgressJson ?? "-"}");
        return string.Join(";", parts);
    }

    static string ChangesText(ProgressChanges ch) => $"{ch.QuestUpserts.Count}u{ch.QuestDeletes.Count}d{ch.MissionUpserts.Count}m";

    static string VarsText(CharProgress p)
    {
        var parts = new List<string>();
        foreach (var kv in p.Vars()) parts.Add($"{kv.Key}={kv.Value.Value}@{kv.Value.Expiry}");
        return string.Join(",", parts);
    }

    // ---- the sql/ files create exactly the tables the schema classes describe ----
    static void RunSchemaFileGate()
    {
        Console.WriteLine("\n== sql/char_*_state.sql == the schema DDL ==");
        string sqlDir = null;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null && sqlDir == null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "sql", "char_quest_state.sql"))) sqlDir = Path.Combine(d.FullName, "sql");
        if (sqlDir == null) { Bad("schema-file: sql/char_quest_state.sql not found above " + AppContext.BaseDirectory); return; }
        string Norm(string s) => s.Replace("\r\n", "\n").Trim();
        Check("schema-file:char_quest_state", "True",
              Norm(File.ReadAllText(Path.Combine(sqlDir, "char_quest_state.sql"))).EndsWith(Norm(QuestStateSchema.CreateTableSql)).ToString());
        Check("schema-file:char_mission_state", "True",
              Norm(File.ReadAllText(Path.Combine(sqlDir, "char_mission_state.sql"))).EndsWith(Norm(MissionStateSchema.CreateTableSql)).ToString());
        Check("schema-file:char_assault_state", "True",
              Norm(File.ReadAllText(Path.Combine(sqlDir, "char_assault_state.sql"))).EndsWith(Norm(AssaultStateSchema.CreateTableSql)).ToString());
        Check("schema-file:char_campaign_state", "True",
              Norm(File.ReadAllText(Path.Combine(sqlDir, "char_campaign_state.sql"))).EndsWith(Norm(CampaignStateSchema.CreateTableSql)).ToString());
        Check("schema-file:char_eminence_state", "True",
              Norm(File.ReadAllText(Path.Combine(sqlDir, "char_eminence_state.sql"))).EndsWith(Norm(EminenceStateSchema.CreateTableSql)).ToString());
        string triggers = File.ReadAllText(Path.Combine(sqlDir, "triggers.sql"));
        Check("schema-file:char_delete removes the three kinds", "True",
              new[] { "char_assault_state", "char_campaign_state", "char_eminence_state" }
                  .All(t => triggers.Contains($"DELETE FROM `{t}`")).ToString());
    }

    // records each statement with its bound values; every statement succeeds (an empty result)
    sealed class RecordingDb : IDatabase
    {
        public readonly List<(string Sql, BoundValue[] Args)> Ran = new();
        public ResultSet Execute(string query, IReadOnlyList<BoundValue> parameters)
        {
            Ran.Add((query, parameters.ToArray()));
            return new ResultSet(new InMemoryResultSet(new List<string>(), new List<Dictionary<string, object>>()), query, ResultSetType.Update, 1);
        }
        public ResultSet ExecuteBulk(string query, IReadOnlyList<BoundValue> parameters) => Execute(query, parameters);
        public string Schema => "recording";
        public string Version => "";
        public string DriverVersion => "";
        public bool Transaction(Fn transactionFn) { transactionFn(); return true; }
    }

    static string ArgsText(BoundValue[] a) => string.Join(",", a.Select(v => v.Type + ":" + v.Value));

    sealed class FakeQuestContext : QuestContextFake { }

    // the shape of scripts/quests/bastok/The_Elvaan_Goldsmith.lua (Michea in Bastok Markets, zone 235; Copper Ingot 640; fame area 1)
    sealed class SampleGoldsmith : QuestScript
    {
        public SampleGoldsmith() : base(QuestLog.Bastok, "THE_ELVAAN_GOLDSMITH") { }
        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 10, FameArea = 1, Gil = 180 });
            b.State(QuestStatus.Available)
                .Npc(235, "Michea").OnTrigger(QuestReply.ProgressEvent(215))
                .OnEventFinish(235, 215, q => q.Quest.Begin())
            .State(QuestStatus.Accepted)
                .Or(QuestStatus.Completed, when: q => q.Player.FameLevel(1) == 1 && !q.Quest.MustZone)
                .Npc(235, "Michea")
                    .OnTrade(q => q.Trade.Matches((640, 1)) ? QuestReply.ProgressEvent(216) : null)
                    .OnTrigger(q => QuestReply.Event(215))
                .OnEventFinish(235, 216, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        q.Quest.SetMustZone();
                    }
                });
        }
    }

    // a quest with author slips: a step matched but never set, and two OnTrigger for one NPC in one state
    sealed class SloppyQuest : QuestScript
    {
        public SloppyQuest() : base(QuestLog.Bastok, "SLOPPY") { }
        protected override void Build(QuestScriptBuilder b)
        {
            b.State(QuestStatus.Accepted, step: 3)
                .Npc(235, "Michea").OnTrigger(QuestReply.Event(1)).OnTrigger(QuestReply.Event(2));
        }
    }


    sealed class Fake : QuestContextFake { }

    // a fake whose quest:complete clears the quest's vars on success, as the live Container:cleanup does
    sealed class CompleteClearsVarsFake : QuestContextFake
    {
        public override bool Complete()
        {
            bool done = base.Complete();
            if (done) Vars.Clear();
            return done;
        }
    }

    sealed class FixedReplyQuest : QuestScript
    {
        readonly QuestReply _r;
        public FixedReplyQuest(QuestReply r) : base(QuestLog.Bastok, "X") { _r = r; }
        protected override void Build(QuestScriptBuilder b) => b.State(QuestStatus.Available).Npc(235, "X").OnTrigger(_r);
    }

    // the same reply on an NPC-call handler
    sealed class CalledFixedReplyQuest : QuestScript
    {
        readonly QuestReply _r;
        public CalledFixedReplyQuest(QuestReply r) : base(QuestLog.Bastok, "X") { _r = r; }
        protected override void Build(QuestScriptBuilder b) => b.State(QuestStatus.Available).Npc(235, "X").Called().OnTrigger(_r);
    }

    sealed class FixedRewardQuest : QuestScript
    {
        readonly QuestReward _r;
        public FixedRewardQuest(QuestReward r) : base(QuestLog.Bastok, "X") { _r = r; }
        protected override void Build(QuestScriptBuilder b) => b.Reward(_r);
    }

    // declares a reward, then changes the object it passed
    sealed class MutatingRewardQuest : QuestScript
    {
        public MutatingRewardQuest() : base(QuestLog.Bastok, "MUTATING") { }
        protected override void Build(QuestScriptBuilder b)
        {
            var r = new QuestReward { Gil = 5, Items = new[] { 1, 2 } };
            b.Reward(r);
            r.Gil = 99;
            r.Items[0] = 77;
        }
    }

    // quest rework B3a: the context split into facets, checked with the fake over the real TheElvaanGoldsmith
    static void RunFacets()
    {
        Console.WriteLine("\n== QuestScript: the context facets ==");
        var d = new XI.QuestContent.Bastok.TheElvaanGoldsmith().Compile();
        var accepted = d.States[1];
        var trade = accepted.Handlers.First(h => h.Kind == QuestHandlerKind.OnTrade);
        var f = new Fake { TradeResult = true };
        var reply = trade.Reply(f);
        Check("facets:the ingot trade replies ProgressEvent 216", "ProgressEvent/216", $"{reply.Kind}/{reply.Id}");
        var finish = accepted.Handlers.First(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Id == 216);
        f.Calls.Clear();
        f.CompleteResult = true;
        finish.Run(f);
        Check("facets:finishing 216 completes, takes the trade, sets mustZone", "Quest.Complete(),Trade.Complete(),Quest.SetMustZone()", string.Join(",", f.Calls));

        var g = new Fake();
        g.Fame[XI.QuestContent.FameArea.Bastok] = 1;
        g.MustZone = false;
        Check("facets:Completed at fame 1, no mustZone, matches", "True", accepted.IsMatch(QuestStatus.Completed, 0, g).ToString());
        g.MustZone = true;
        Check("facets:  with mustZone it does not", "False", accepted.IsMatch(QuestStatus.Completed, 0, g).ToString());

        Check("facets:Event(10).Option(2, 7).Options[2]", "7", QuestReply.Event(10).Option(2, 7).Options[2].ToString());
        string threw = "no";
        try { QuestReply.Message(1).Option(2, 7); } catch (InvalidOperationException) { threw = "InvalidOperationException"; }
        Check("facets:Message(1).Option(...) throws", "InvalidOperationException", threw);
        Check("facets:the original reply keeps no options", "0", QuestReply.Event(10).Options.Count.ToString());

        var rd = new MutatingRewardQuest().Compile();
        Check("facets:Reward copies the reward and its items", "5/1", $"{rd.Reward.Gil}/{rd.Reward.Items[0]}");

        void Throws(string what, Action act)
        {
            string got = "no";
            try { act(); } catch (ArgumentNullException) { got = "ArgumentNullException"; }
            Check("facets:null " + what + " throws", "ArgumentNullException", got);
        }
        // Face and Wait are sequence steps; the rule is QuestScriptValidator's
        QuestScriptDefinition WithFixed(QuestReply r) => new FixedReplyQuest(r).Compile();
        Check("facets:Face and Wait inside a sequence with text validate clean", "",
              string.Join(" | ", QuestScriptValidator.Validate(WithFixed(QuestReply.Sequence(QuestReply.MessageText("A"), QuestReply.Face(64), QuestReply.Wait(100), QuestReply.MessageText("B"))))));
        Check("facets:a top-level Face is refused", "X OnTrigger: a Face reply is a sequence step only",
              string.Join(" | ", QuestScriptValidator.Validate(WithFixed(QuestReply.Face(64)))));
        Check("facets:a top-level Wait is refused", "X OnTrigger: a Wait reply is a sequence step only",
              string.Join(" | ", QuestScriptValidator.Validate(WithFixed(QuestReply.Wait(5)))));
        Check("facets:a sequence of Face and Wait only is refused", "X OnTrigger: a sequence with no message step",
              string.Join(" | ", QuestScriptValidator.Validate(WithFixed(QuestReply.Sequence(QuestReply.Face(1), QuestReply.Wait(5))))));
        Check("facets:an event inside a sequence is still refused", "True",
              (QuestScriptValidator.SequenceProblem(QuestReply.Sequence(QuestReply.MessageText("A"), QuestReply.Event(1))) != null).ToString());

        // the reply modifiers (action.lua's Action methods), quest rework B3b Bastok task 1b
        string Mods(QuestReply r) => string.Join(",", r.Modifiers);
        Check("modifiers:ReplaceDefault reaches the reply", "replaceDefault()", Mods(QuestReply.Event(1).ReplaceDefault()));
        Check("modifiers:OncePerZone reaches the reply", "oncePerZone()", Mods(QuestReply.Event(1).OncePerZone()));
        Check("modifiers:ImportantEvent reaches the reply", "importantEvent()", Mods(QuestReply.Event(1).ImportantEvent()));
        Check("modifiers:ImportantOnce reaches the reply", "importantOnce()", Mods(QuestReply.Event(1).ImportantOnce()));
        Check("modifiers:Progress reaches the reply", "progress()", Mods(QuestReply.Event(1).Progress()));
        Check("modifiers:SetPriority reaches the reply with its priority", "setPriority(50)", Mods(QuestReply.Event(1).SetPriority(50)));
        Check("modifiers:OpenDoor reaches the reply", "openDoor()", Mods(QuestReply.NoAction().OpenDoor()));
        Check("modifiers:chained, in the order written",
              "replaceDefault(),oncePerZone(),importantEvent(),importantOnce(),progress(),setPriority(20),openDoor()",
              Mods(QuestReply.Event(1).ReplaceDefault().OncePerZone().ImportantEvent().ImportantOnce().Progress().SetPriority(20).OpenDoor()));
        var plain = QuestReply.Event(222);
        var once = plain.OncePerZone();
        Check("modifiers:a copy: the original keeps none, the rest of the reply is the same", "0/Event/222",
              $"{plain.Modifiers.Count}/{once.Kind}/{once.Id}");
        Check("modifiers:Option keeps them", "oncePerZone()/7", $"{Mods(once.Option(2, 7))}/{once.Option(2, 7).Options[2]}");
        Check("modifiers:a sequence reply takes them", "replaceDefault()",
              Mods(QuestReply.Sequence(QuestReply.MessageText("A"), QuestReply.Face(1)).ReplaceDefault()));
        Check("modifiers:the fake describes a reply as the Lua call", "Event(222):oncePerZone()", QuestContextFake.Describe(once));
        var rf = new Fake();
        rf.RecordReply(QuestReply.ProgressEvent(5, 1).SetPriority(20).OpenDoor());
        Check("modifiers:the fake records a reply with its modifiers", "Reply.ProgressEvent(5,1):setPriority(20):openDoor()", string.Join(",", rf.Calls));
        Check("modifiers:a reply with modifiers validates clean", "",
              string.Join(" | ", QuestScriptValidator.Validate(WithFixed(QuestReply.Event(222).OncePerZone()))));
        Check("modifiers:a sequence step with modifiers is refused", "X OnTrigger: sequence step 1 takes no modifiers (oncePerZone())",
              string.Join(" | ", QuestScriptValidator.Validate(WithFixed(QuestReply.Sequence(QuestReply.MessageText("A").OncePerZone())))));
        Check("modifiers:an NPC call refuses a reply with modifiers", "True",
              QuestScriptValidator.Validate(new CalledFixedReplyQuest(QuestReply.Event(222).OncePerZone()).Compile())
                  .Any(p => p.Contains("modifiers are not allowed in an NPC call")).ToString());
        // Lua's onZoneIn returns a cutscene id, not an action: the zone-in rule the bridge applies to each reply
        Check("modifiers:a zone-in cutscene with a modifier is refused",
              "a cutscene id takes no reply modifiers (oncePerZone()): onZoneIn builds no action",
              QuestScriptValidator.ZoneInReplyProblem(QuestReply.Cutscene(30).OncePerZone()) ?? "null");
        Check("modifiers:a plain zone-in cutscene is fine", "null", QuestScriptValidator.ZoneInReplyProblem(QuestReply.Cutscene(30)) ?? "null");
        Check("modifiers:a zone-in cutscene with arguments is refused", "a cutscene id takes no arguments here (1 given)",
              QuestScriptValidator.ZoneInReplyProblem(QuestReply.Cutscene(30, 1)) ?? "null");
        Check("modifiers:a zone-in message is refused", "a MessageSpecial reply is not a cutscene id",
              QuestScriptValidator.ZoneInReplyProblem(QuestReply.MessageSpecial("X")) ?? "null");
        // a trigger area whose Lua returns an action (DRK AF3 Blade of Evil: return quest:progressEvent(14)): the reply
        // overload, on the state and through an NPC builder; the lookup performs it with no NPC
        var areaDef = new BuiltQuest(b =>
        {
            b.Steps(1);
            b.State(QuestStatus.Accepted, step: 1).OnTriggerAreaEnter(197, 8, q => q.TriggerAreaId == 8 ? QuestReply.ProgressEvent(14) : null);
            b.State(QuestStatus.Accepted).Npc(197, "qm1").OnTrigger(QuestReply.Event(1)).OnTriggerAreaEnter(197, 9, q => QuestReply.Event(15));
            b.State(QuestStatus.Completed).OnTriggerAreaEnter(197, 8, q => q.Player.SetCharVar("x", 1));
        }).Compile();
        var areaHandlers = areaDef.States.SelectMany(st => st.Handlers).Where(h => h.Kind == QuestHandlerKind.OnTriggerAreaEnter).ToList();
        Check("trigger area reply:the state's and the NPC builder's overload hold a reply, the action one a run", "8:reply,9:reply,8:run",
              string.Join(",", areaHandlers.Select(h => $"{h.Id}:{(h.Reply != null && h.Run == null ? "reply" : h.Run != null && h.Reply == null ? "run" : "?")}")));
        var areaFake = new Fake { TriggerAreaId = 8 };
        Check("trigger area reply:the reply is the handler's", "ProgressEvent(14)", QuestContextFake.Describe(areaHandlers[0].Reply(areaFake)));
        areaFake.TriggerAreaId = 7;
        Check("trigger area reply:  and null when it gives none", "null", areaHandlers[0].Reply(areaFake) == null ? "null" : "a reply");
        Check("trigger area reply:a quest with one validates clean", "", string.Join(" | ", QuestScriptValidator.Validate(areaDef)));
        Check("trigger area reply:a null reply is refused at the build", "reply",
              BuildThrows(b => b.State(QuestStatus.Accepted).OnTriggerAreaEnter(197, 8, (Func<IQuestContext, QuestReply>)null)));
        Check("trigger area reply:events, a special message, a key item and no action are fine", "null,null,null,null,null,null",
              string.Join(",", new[] { QuestReply.ProgressEvent(14), QuestReply.Cutscene(7, 1).OncePerZone(), QuestReply.MessageSpecial("X"),
                                       QuestReply.Message(5, 2), QuestReply.KeyItem(3), QuestReply.NoAction() }
                  .Select(r => QuestScriptValidator.TriggerAreaReplyProblem(r) ?? "null")));
        Check("trigger area reply:a text the NPC shows is refused (no NPC)",
              "a MessageText reply needs an NPC to show it, and a trigger area has none|a MessageName reply needs an NPC to show it, and a trigger area has none|" +
              "a Message reply needs an NPC to show it, and a trigger area has none|a Message reply needs an NPC to show it, and a trigger area has none|" +
              "a ReplaceMessage reply needs an NPC to show it, and a trigger area has none|a Sequence reply needs an NPC to show it, and a trigger area has none",
              string.Join("|", new[] { QuestReply.MessageText("X"), QuestReply.MessageName("X"), QuestReply.Message(5), QuestReply.Message(5, 3),
                                       QuestReply.ReplaceMessage("X"), QuestReply.Sequence(QuestReply.MessageText("A")) }
                  .Select(r => QuestScriptValidator.TriggerAreaReplyProblem(r) ?? "null")));
        Check("trigger area reply:a face alone is refused as anywhere", "a Face reply is a sequence step only",
              QuestScriptValidator.TriggerAreaReplyProblem(QuestReply.Face(1)) ?? "null");
        static string BuildThrows(Action<QuestScriptBuilder> build)
        {
            try
            {
                new BuiltQuest(build).Compile();
                return "no throw";
            }
            catch (ArgumentNullException e)
            {
                return e.ParamName;
            }
        }
        Check("modifiers:each modifier kind has its Lua name; an unknown kind throws",
              "replaceDefault,oncePerZone,importantEvent,importantOnce,progress,setPriority,openDoor/InvalidOperationException",
              string.Join(",", System.Enum.GetValues<QuestReplyModifierKind>().Select(k => new QuestReplyModifier(k).LuaName)) + "/" +
              LuaNameOf((QuestReplyModifierKind)99));
        static string LuaNameOf(QuestReplyModifierKind k)
        {
            try { return new QuestReplyModifier(k).LuaName; }
            catch (System.Exception e) { return e.GetType().Name; }
        }

        // the fake: Text.Special, CharVar/SetCharVar, Step is the Prog var
        var tf = new Fake();
        tf.Text.Special(new ZoneText(235, "ITEM_OBTAINED"), 4, 5);
        Check("facets:the fake records Text.Special(<zone>:<NAME>,args)", "Text.Special(235:ITEM_OBTAINED,4,5)", string.Join(",", tf.Calls));
        tf.Calls.Clear();
        tf.CharVars["Seen"] = 3;
        Check("facets:CharVar reads CharVars", "3", tf.Player.CharVar("Seen").ToString());
        tf.Player.SetCharVar("Seen", 8);
        Check("facets:SetCharVar records and writes", "Player.SetCharVar(Seen,8,0)/8", $"{string.Join(",", tf.Calls)}/{tf.Player.CharVar("Seen")}");
        tf.Calls.Clear();
        tf.Equippable.Add(12345);
        bool eqNoLevel = tf.Player.CanEquipItem(12345, false), eqLevel = tf.Player.CanEquipItem(12345, true);
        Check("facets:the fake records CanEquipItem with its checkLevel", "Player.CanEquipItem(12345,False),Player.CanEquipItem(12345,True)/True/True",
              $"{string.Join(",", tf.Calls)}/{eqNoLevel}/{eqLevel}");
        tf.Calls.Clear();
        // the fake's other quests' mustZone: keyed "<log>/<const>", as Player.QuestMustZone is called
        tf.QuestMustZones.Add($"{QuestLog.Bastok}/MOM_THE_ADVENTURER");
        Check("facets:the fake answers QuestMustZone true for a key it was given, false otherwise", "True/False/False",
              $"{tf.Player.QuestMustZone(QuestLog.Bastok, "MOM_THE_ADVENTURER")}/" +
              $"{tf.Player.QuestMustZone(QuestLog.Bastok, "HYPER_ACTIVE")}/" +
              $"{tf.Player.QuestMustZone(QuestLog.Sandoria, "MOM_THE_ADVENTURER")}");
        // the fake's completed missions: keyed "<missionLog>/<mission>" with the generated MissionLog / Mission constants
        tf.CompletedMissions.Add($"{XI.QuestContent.MissionLog.Cop}/{XI.QuestContent.Mission.Cop.TheCallOfTheWyrmking}");
        Check("facets:the fake answers HasCompletedMission true for a key it was given, false otherwise", "True/False/False",
              $"{tf.Player.HasCompletedMission(XI.QuestContent.MissionLog.Cop, XI.QuestContent.Mission.Cop.TheCallOfTheWyrmking)}/" +
              $"{tf.Player.HasCompletedMission(XI.QuestContent.MissionLog.Cop, XI.QuestContent.Mission.Cop.AVesselWithoutACaptain)}/" +
              $"{tf.Player.HasCompletedMission(XI.QuestContent.MissionLog.Bastok, XI.QuestContent.Mission.Cop.TheCallOfTheWyrmking)}");
        // the fake's time of day, party and alliance (group 4), and its treasure trade
        tf.Calls.Clear();
        tf.Totd = 5;
        tf.Party = 2;
        tf.MainLevel = 10;
        Check("facets:the fake's alliance is the char alone at first (its level, distance 0), as a solo char's live one is", "10:0",
              string.Join(",", tf.Player.Alliance.Select(m => $"{m.MainLevel}:{m.Distance.ToString(System.Globalization.CultureInfo.InvariantCulture)}")));
        tf.AllianceMembers.Add((16, 3.5));
        tf.TreasureResult = XI.QuestContent.KeyItem.UnMoment;
        int treasure = tf.Trade.TreasureTrade(2, XI.QuestContent.KeyItem.UnMoment);
        Check("facets:the fake answers VanadielTotd, PartySize, Alliance and records TreasureTrade",
              $"5/2/10:0,16:3.5/Trade.TreasureTrade(2,{XI.QuestContent.KeyItem.UnMoment})/{XI.QuestContent.KeyItem.UnMoment}",
              $"{tf.Player.VanadielTotd}/{tf.Player.PartySize}/" +
              string.Join(",", tf.Player.Alliance.Select(m => $"{m.MainLevel}:{m.Distance.ToString(System.Globalization.CultureInfo.InvariantCulture)}")) +
              $"/{string.Join(",", tf.Calls)}/{treasure}");
        // group 5: the day count, the previous zone, another quest's var, the mobs, the trade's item count, the npc's distance
        tf.Calls.Clear();
        tf.UniqueDay = 8001;
        tf.PrevZone = XI.QuestContent.Zone.BehemothsDominion;
        tf.Player.SetQuestVar(QuestLog.Bastok, "THE_TALEKEEPERS_GIFT", "Timer", 8002);
        bool before = tf.Player.MobIsSpawned("TALEKEEPERS_GIFT_OFFSET", 1);
        tf.Player.SpawnMob("TALEKEEPERS_GIFT_OFFSET", 1, claim: false);
        tf.TradeItems[XI.QuestContent.Item.Pickaxe] = 2;
        tf.Npc = new QuestNpcFake(17388024) { PlayerDistance = 0.5 };
        Check("facets:the fake answers VanadielUniqueDay, PreviousZone, MobIsSpawned / MobIsAlive (SpawnMob adds), ItemQty, PlayerDistance and records SetQuestVar / SpawnMob",
              $"8001/{XI.QuestContent.Zone.BehemothsDominion}/False/True/True/False/2/0/0.5/" +
              "Player.SetQuestVar(Bastok,THE_TALEKEEPERS_GIFT,Timer,8002),Player.SpawnMob(TALEKEEPERS_GIFT_OFFSET,1,False)",
              $"{tf.Player.VanadielUniqueDay}/{tf.Player.PreviousZone}/{before}/{tf.Player.MobIsSpawned("TALEKEEPERS_GIFT_OFFSET", 1)}/" +
              $"{tf.Player.MobIsAlive("TALEKEEPERS_GIFT_OFFSET", 1)}/{tf.Player.MobIsAlive("TALEKEEPERS_GIFT_OFFSET")}/" +
              $"{tf.Trade.ItemQty(XI.QuestContent.Item.Pickaxe)}/{tf.Trade.ItemQty(XI.QuestContent.Item.BoneChip)}/" +
              $"{((IQuestContext)tf).Npc.PlayerDistance.ToString(System.Globalization.CultureInfo.InvariantCulture)}/{string.Join(",", tf.Calls)}");
        // San d'Oria group 1: tradeHasExactly / tradeHas with quantities, giveItem with params
        tf.Npc = null;
        tf.TradeItems.Clear();
        // Jeuno group 1: npcUtil.tradeHasExactly, the items and nothing else (its own block: other lanes add checks here)
        {
            tf.TradeItems[XI.QuestContent.Item.GoldOrcmask] = 1;
            string jExact1 = $"{tf.Trade.HasExactly(XI.QuestContent.Item.GoldOrcmask)}";
            tf.TradeItems[XI.QuestContent.Item.GoldOrcmask] = 2;
            string jExact2 = $"{tf.Trade.HasExactly(XI.QuestContent.Item.GoldOrcmask)}/{tf.Trade.HasExactly(XI.QuestContent.Item.GoldOrcmask, XI.QuestContent.Item.GoldOrcmask)}";
            tf.TradeItems[XI.QuestContent.Item.GoldOrcmask] = 1;
            tf.TradeItems[XI.QuestContent.Item.BoneChip] = 1;
            string jExact3 = $"{tf.Trade.HasExactly(XI.QuestContent.Item.GoldOrcmask)}/{tf.Trade.Has(XI.QuestContent.Item.GoldOrcmask)}";
            Check("facets:the fake's HasExactly: one mask yes; two masks no (unless listed twice); a mask and a chip no (Has yes)",
                  "True/False/True/False/True", $"{jExact1}/{jExact2}/{jExact3}");
        }
        tf.TradeItems.Clear();
        // Jeuno group 2: utils.vanadielClockTime, zones[zone].npc[key] (an unknown key throws), quest:incrementVar (its
        // own block, as group 1's)
        {
            tf.ClockTime = 2200;
            tf.NpcIds["BRAZIER"] = 17576344;
            tf.Calls.Clear();
            tf.Quest.IncrementVar("Stage", 1);
            tf.Quest.IncrementVar("Stage", 1);
            string jUnknownNpc;
            try { tf.Player.NpcIdOf("NO_SUCH_NPC"); jUnknownNpc = "no throw"; }
            catch (InvalidOperationException) { jUnknownNpc = "throws"; }
            Check("facets:the fake's VanadielClockTime, NpcIdOf (an unknown key throws) and IncrementVar (from 0, twice)",
                  "2200/17576344/throws/2/Quest.IncrementVar(Stage,1),Quest.IncrementVar(Stage,1)",
                  $"{tf.Player.VanadielClockTime}/{tf.Player.NpcIdOf("BRAZIER")}/{jUnknownNpc}/{tf.Quest.Var("Stage")}/{string.Join(",", tf.Calls)}");
            tf.Vars.Remove("Stage");
        }
        // Jeuno group 3: npcUtil.popFromQM over a mob and the ids after it (its own block)
        {
            tf.Calls.Clear();
            tf.PopResult = false;
            string jSeries = $"{tf.Player.PopFromQmSeries("YUM_KIMIL", 3, 0)}";
            tf.PopResult = true;
            Check("facets:the fake's PopFromQmSeries records the mob key, count and hide and answers PopResult",
                  "False/Player.PopFromQmSeries(YUM_KIMIL,3,0)", $"{jSeries}/{string.Join(",", tf.Calls)}");
        }
        // Jeuno group 4: JstMidnight(), player:getID(), npcUtil.giveItem with a quantity, player:messageText (its own block)
        {
            tf.Calls.Clear();
            tf.NextJstMidnight = 1800025200;
            tf.CharId = 996177;
            tf.GiveResult = false;
            string jGive = $"{tf.Player.GiveItems((XI.QuestContent.Item.TarutCardDeath, 5))}";
            tf.GiveResult = true;
            tf.Text.MessageText(XI.QuestContent.Text.LowerJeuno.FORTUNE_TOLD_BY_KUROU_MOROU, false, 6);
            Check("facets:the fake's JstMidnight and Id, GiveItems (answers GiveResult) and MessageText",
                  "1800025200/996177/False/Player.GiveItems(559x5),Text.MessageText(245:FORTUNE_TOLD_BY_KUROU_MOROU,False,6)",
                  $"{tf.Player.JstMidnight}/{tf.Player.Id}/{jGive}/{string.Join(",", tf.Calls)}");
        }
        // Jeuno group 5: VanadielHour(), getVanadielMoonCycle(), player:getName(), the zone's local vars, GetPlayerByName /
        // GetPlayerByID, npc:getAnimation / setAnimation, messageSpecial by a text id plus an offset (its own block)
        {
            tf.Calls.Clear();
            tf.Hour = 19;
            tf.MoonCycle = 0;
            tf.CharName = "Qgolden996177";
            tf.ZoneLocalVars["commServiceStart"] = 1;
            tf.Player.SetZoneLocalVar("allLightsLit", 1);
            tf.OnlinePlayers["Bob"] = (996178, 245);
            tf.TextIds["245:LAMP_MSG_OFFSET"] = 7276;
            var lamp = new QuestNpcFake(17780881, tf.Calls) { Animation = 9 };
            lamp.SetAnimation(8);
            tf.Text.Special(tf.Text.Id(XI.QuestContent.Text.LowerJeuno.LAMP_MSG_OFFSET) + 1, 11);
            string jUnknownText;
            try { tf.Text.Id(XI.QuestContent.Text.LowerJeuno.ZAUKO_IS_RECRUITING); jUnknownText = "no throw"; }
            catch (InvalidOperationException) { jUnknownText = "throws"; }
            Check("facets:the fake's VanadielHour, VanadielMoonCycle, Name, zone local vars, PlayerByName / PlayerById, Npc animation, Text.Id and Special(int)",
                  "19/0/Qgolden996177/1/1/0/996178:245/996178:245/null/null/8/throws/" +
                  "Player.SetZoneLocalVar(allLightsLit,1),Npc.SetAnimation(8),Text.Special(#7277,11)",
                  $"{tf.Player.VanadielHour}/{tf.Player.VanadielMoonCycle}/{tf.Player.Name}/{tf.Player.ZoneLocalVar("commServiceStart")}/" +
                  $"{tf.Player.ZoneLocalVar("allLightsLit")}/{tf.Player.ZoneLocalVar("commServiceComp")}/" +
                  $"{tf.Player.PlayerByName("Bob")?.Id}:{tf.Player.PlayerByName("Bob")?.ZoneId}/{tf.Player.PlayerById(996178)?.Id}:{tf.Player.PlayerById(996178)?.ZoneId}/" +
                  $"{(tf.Player.PlayerByName("Nobody") == null ? "null" : "found")}/{(tf.Player.PlayerById(1) == null ? "null" : "found")}/" +
                  $"{lamp.Animation}/{jUnknownText}/{string.Join(",", tf.Calls)}");
            tf.ZoneLocalVars.Clear();
            tf.OnlinePlayers.Clear();
        }
        // Jeuno group 6: player:needToZone() / needToZone(value), getGender(), getPos() / setPos, startEvent (its own block)
        {
            tf.Calls.Clear();
            string g6Before = $"{tf.Player.NeedToZone}/{tf.Player.Gender}";
            tf.Player.SetNeedToZone(true);
            tf.GenderId = 0;
            tf.Position = (-52.5, 0.25, 101, 165);
            var g6Pos = tf.Player.Pos;
            tf.Player.SetPos(g6Pos.X, g6Pos.Y, g6Pos.Z, g6Pos.Rot, XI.QuestContent.Zone.UpperJeuno);
            tf.Player.StartEvent(10139);
            tf.Player.StartEvent(10151, 0, -3);
            Check("facets:the fake's NeedToZone (false, then true), Gender (1 by default, then 0), Pos / SetPos and StartEvent",
                  "False/1/True/0/-52.5:0.25:101:165/" +
                  "Player.SetNeedToZone(True),Player.SetPos(-52.5,0.25,101,165,244),Player.StartEvent(10139),Player.StartEvent(10151,0,-3)",
                  FormattableString.Invariant($"{g6Before}/{tf.Player.NeedToZone}/{tf.Player.Gender}/") +
                  FormattableString.Invariant($"{tf.Player.Pos.X}:{tf.Player.Pos.Y}:{tf.Player.Pos.Z}:{tf.Player.Pos.Rot}/{string.Join(",", tf.Calls)}"));
            tf.GenderId = 1;
            tf.ZoningFlag = false;
            tf.Position = default;
        }
        // group 6: findItem, a pop of an offset key's mob, another NPC's local var
        tf.Calls.Clear();
        tf.Items.Add(XI.QuestContent.Item.BrassCanteen);
        bool popped = tf.Player.PopFromQm("DARK_PUPPET_OFFSET", hide: 0, offset: 2);
        tf.Player.SetNpcLocalVar("AQUILLINA", "tradeCooldown", 900);
        Check("facets:the fake answers FindItem and records PopFromQm (an offset) / SetNpcLocalVar",
              "True/False/True/900/Player.PopFromQm(DARK_PUPPET_OFFSET+2,True,0),Player.SetNpcLocalVar(AQUILLINA,tradeCooldown,900)",
              $"{tf.Player.FindItem(XI.QuestContent.Item.BrassCanteen)}/{tf.Player.FindItem(XI.QuestContent.Item.CanteenOfDrachenfallWater)}/" +
              $"{popped}/{tf.NpcLocalVars["AQUILLINA/tradeCooldown"]}/{string.Join(",", tf.Calls)}");
        tf.Items.Clear();
        // group 7: a skill's stored value, a spell, NextJstDay, a weapon's points in the trade; a weapon skill, a spell and a
        // teleport recorded
        tf.Calls.Clear();
        tf.Skills[XI.QuestContent.Skill.GreatAxe] = 2400;
        tf.JstDay = 1800025200;
        tf.TradeWeaponskillPoints[0] = 300;
        tf.Player.AddLearnedWeaponskill(6);
        tf.Player.AddSpell(299);
        tf.Player.TeleportTo(47);
        Check("facets:the fake answers CharSkillLevel / HasSpell / NextJstDay / ItemWeaponskillPoints and records the rest",
              "2400/0/True/False/1800025200/300/0/Player.AddLearnedWeaponskill(6),Player.AddSpell(299),Player.TeleportTo(47)",
              $"{tf.Player.CharSkillLevel(XI.QuestContent.Skill.GreatAxe)}/{tf.Player.CharSkillLevel(XI.QuestContent.Skill.Marksmanship)}/" +
              $"{tf.Player.HasSpell(299)}/{tf.Player.HasSpell(300)}/{tf.Player.NextJstDay}/{tf.Trade.ItemWeaponskillPoints(0)}/" +
              $"{tf.Trade.ItemWeaponskillPoints(1)}/{string.Join(",", tf.Calls)}");
        tf.Skills.Clear();
        tf.Spells.Clear();
        tf.TradeWeaponskillPoints.Clear();
        // group 8: the Vana'diel hour, a log's current mission and a mission's var; a key item with no message (then held)
        // and an event the handler starts itself, recorded
        tf.Calls.Clear();
        tf.Hour = 18;
        tf.CurrentMissions[XI.QuestContent.MissionLog.Cop] = XI.QuestContent.Mission.Cop.OneToBeFeared;
        tf.MissionVars[$"{XI.QuestContent.MissionLog.Cop}/{XI.QuestContent.Mission.Cop.OneToBeFeared}/Status"] = 1;
        bool toolboxBefore = tf.Player.HasKeyItem(XI.QuestContent.KeyItem.OldToolbox);
        tf.Player.AddKeyItem(XI.QuestContent.KeyItem.OldToolbox);
        tf.Player.StartEvent(143);
        tf.Player.StartEvent(113, 1, 2);
        Check("facets:the fake answers VanadielHour / CurrentMission / MissionVar and records AddKeyItem (then held) / StartEvent",
              $"18/{XI.QuestContent.Mission.Cop.OneToBeFeared}/0/1/0/False/True/" +
              $"Player.AddKeyItem({XI.QuestContent.KeyItem.OldToolbox}),Player.StartEvent(143),Player.StartEvent(113,1,2)",
              $"{tf.Player.VanadielHour}/{tf.Player.CurrentMission(XI.QuestContent.MissionLog.Cop)}/" +
              $"{tf.Player.CurrentMission(XI.QuestContent.MissionLog.Bastok)}/" +
              $"{tf.Player.MissionVar(XI.QuestContent.MissionLog.Cop, XI.QuestContent.Mission.Cop.OneToBeFeared, "Status")}/" +
              $"{tf.Player.MissionVar(XI.QuestContent.MissionLog.Cop, XI.QuestContent.Mission.Cop.OneToBeFeared, "Other")}/" +
              $"{toolboxBefore}/{tf.Player.HasKeyItem(XI.QuestContent.KeyItem.OldToolbox)}/{string.Join(",", tf.Calls)}");
        tf.Hour = 0;
        tf.CurrentMissions.Clear();
        tf.MissionVars.Clear();
        tf.KeyItems.Remove(XI.QuestContent.KeyItem.OldToolbox);
        // group 9: the Mog House flag (then set), the weather, a mob never spawned counts as dead (then spawned: alive);
        // an item with its params, a stack of items and an NPC hidden, recorded
        tf.Calls.Clear();
        tf.MhFlag = 1;
        tf.CurrentWeather = 2;
        bool deadBefore = tf.Player.MobIsDead("KORROLOKA_LEECH", 1);
        tf.Player.SpawnMob("KORROLOKA_LEECH", 1, true);
        tf.Player.SetMoghouseFlag(tf.Player.MoghouseFlag + 2);
        tf.Player.GiveItem(1192, true, true);
        tf.Player.GiveItems((1612, 4), (4096, 1));
        tf.Npc = new QuestNpcFake(17486242, tf.Calls);
        tf.Npc.Hide(300);
        Check("facets:the fake answers MoghouseFlag / Weather / MobIsDead and records SetMoghouseFlag / GiveItem / GiveItems / Hide",
              "3/2/True/False/Player.SpawnMob(KORROLOKA_LEECH,1,True),Player.SetMoghouseFlag(3),Player.GiveItem(1192,True,True)," +
              "Player.GiveItems(1612x4;4096x1),Npc.Hide(300)",
              $"{tf.Player.MoghouseFlag}/{tf.Player.Weather}/{deadBefore}/{tf.Player.MobIsDead("KORROLOKA_LEECH", 1)}/" +
              string.Join(",", tf.Calls));
        tf.Npc = null;
        tf.MhFlag = 0;
        tf.CurrentWeather = 0;
        tf.SpawnedMobs.Clear();
        tf.AliveMobs.Clear();
        // group 10: a status effect added (then held) and deleted, a mob key's id and the dying mob, the automaton's model
        // id, the level cap (then set), a position set, a series popped; the trade's gil (item 65535) and an exact trade
        tf.Calls.Clear();
        tf.MobIds["PUDDING_OFFSET"] = 17580338;
        tf.Mob = new QuestNpcFake(17580339);
        tf.AutomatonModelId = 1977;
        tf.LevelCap = 70;
        tf.Player.AddStatusEffect(143, 25, 1, 0x100);
        bool restricted = tf.Player.HasStatusEffect(143);
        tf.Player.DelStatusEffect(143);
        tf.Player.SetLevelCap(75);
        tf.Player.SetPos(-93.657, -120, -583.561, 232, 13);
        bool series = tf.Player.PopFromQmSeries("PUDDING_OFFSET", 2, 0, true, true);
        tf.TradeItems[65535] = 10000;
        bool gilOnly = tf.Trade.HasExactly(Array.Empty<int>());
        int gil = tf.Trade.Gil;
        tf.TradeItems.Clear();
        tf.TradeItems[1192] = 1;
        bool exact = tf.Trade.HasExactly(1192);
        tf.TradeItems[880] = 1;
        bool extra = tf.Trade.HasExactly(1192);
        Check("facets:the fake answers HasStatusEffect / MobIdOf / Mob / AutomatonModelId / LevelCap / Trade.Gil / HasExactly " +
              "and records AddStatusEffect / DelStatusEffect / SetLevelCap / SetPos / PopFromQmSeries",
              "True/False/17580339/17580339/1977/75/True/10000/False/True/False/" +
              "Player.AddStatusEffect(143,25,1,256),Player.DelStatusEffect(143),Player.SetLevelCap(75)," +
              "Player.SetPos(-93.657,-120,-583.561,232,13),Player.PopFromQmSeries(PUDDING_OFFSET,2,0,True,True)",
              $"{restricted}/{tf.Player.HasStatusEffect(143)}/{tf.Player.MobIdOf("PUDDING_OFFSET") + 1}/{((IQuestContext)tf).Mob.Id}/" +
              $"{tf.Player.AutomatonModelId}/{tf.Player.LevelCap}/{series}/{gil}/{gilOnly}/{exact}/{extra}/" +
              string.Join(",", tf.Calls));
        tf.Mob = null;
        tf.MobIds.Clear();
        tf.AutomatonModelId = 0;
        tf.LevelCap = 0;
        tf.TradeItems.Clear();
        // group 11: the char's nation and its rank there (1 for a nation not set), a spell added with its options table
        // (then known)
        tf.Calls.Clear();
        tf.NationId = 2;
        tf.Ranks[2] = 6;
        bool najiBefore = tf.Player.HasSpell(897);
        tf.Player.AddSpell(897, true);
        Check("facets:the fake answers Nation / Rank and records AddSpell(spell, silentLog) (then known)",
              "2/6/1/False/True/Player.AddSpell(897,True)",
              $"{tf.Player.Nation}/{tf.Player.Rank(tf.Player.Nation)}/{tf.Player.Rank(1)}/{najiBefore}/{tf.Player.HasSpell(897)}/" +
              string.Join(",", tf.Calls));
        tf.NationId = 0;
        tf.Ranks.Clear();
        tf.Spells.Clear();
        tf.Calls.Clear();
        tf.TradeItems[XI.QuestContent.Item.Sheepskin] = 3;
        bool exact3 = tf.Trade.HasExactly((XI.QuestContent.Item.Sheepskin, 3));
        bool exact2 = tf.Trade.HasExactly((XI.QuestContent.Item.Sheepskin, 2));
        bool qty2 = tf.Trade.HasQty((XI.QuestContent.Item.Sheepskin, 2));
        bool qty4 = tf.Trade.HasQty((XI.QuestContent.Item.Sheepskin, 4));
        bool qtySummed = tf.Trade.HasQty((XI.QuestContent.Item.Sheepskin, 2), (XI.QuestContent.Item.Sheepskin, 1));
        tf.TradeItems[XI.QuestContent.Item.BoneChip] = 1;
        bool exactExtra = tf.Trade.HasExactly((XI.QuestContent.Item.Sheepskin, 3));
        bool qtyExtra = tf.Trade.HasQty((XI.QuestContent.Item.Sheepskin, 3));
        bool given = tf.Player.GiveItem(XI.QuestContent.Item.LeatherGloves, silent: false, fromTrade: true);
        Check("facets:the fake answers HasExactly (nothing else traded) and HasQty (at least; repeated ids add up), records GiveItem with its params",
              $"True/False/True/False/True/False/True/True/Player.GiveItem({XI.QuestContent.Item.LeatherGloves},False,True)",
              $"{exact3}/{exact2}/{qty2}/{qty4}/{qtySummed}/{exactExtra}/{qtyExtra}/{given}/{string.Join(",", tf.Calls)}");
        // npcUtil.tradeHas(trade, { a, a }) needs two of a: the fake's Has sums a repeated id
        tf.TradeItems.Clear();
        tf.TradeItems[XI.QuestContent.Item.BoneChip] = 1;
        bool hasOne = tf.Trade.Has(XI.QuestContent.Item.BoneChip);
        bool hasTwiceOfOne = tf.Trade.Has(XI.QuestContent.Item.BoneChip, XI.QuestContent.Item.BoneChip);
        tf.TradeItems[XI.QuestContent.Item.BoneChip] = 2;
        bool hasTwiceOfTwo = tf.Trade.Has(XI.QuestContent.Item.BoneChip, XI.QuestContent.Item.BoneChip);
        bool hasMissing = tf.Trade.Has(XI.QuestContent.Item.BoneChip, XI.QuestContent.Item.Sheepskin);
        Check("facets:the fake's Has counts a repeated id as tradeHas does (one chip for { chip, chip } is not enough)",
              "True/False/True/False",
              $"{hasOne}/{hasTwiceOfOne}/{hasTwiceOfTwo}/{hasMissing}");
        // San d'Oria group 2: getCharSkillLevel, a traded item's weapon skill points, addLearnedWeaponskill
        tf.Calls.Clear();
        tf.Skills[XI.QuestContent.Skill.Polearm] = 2400;
        tf.TradeWeaponskillPoints[0] = 300;
        int polearm = tf.Player.CharSkillLevel(XI.QuestContent.Skill.Polearm);
        int katana = tf.Player.CharSkillLevel(XI.QuestContent.Skill.Katana);
        int wsp0 = tf.Trade.ItemWeaponskillPoints(0);
        int wsp1 = tf.Trade.ItemWeaponskillPoints(1);
        tf.Player.AddLearnedWeaponskill(8);
        Check("facets:the fake answers CharSkillLevel and ItemWeaponskillPoints (0 when unset), records AddLearnedWeaponskill",
              "2400/0/300/0/Player.AddLearnedWeaponskill(8)",
              $"{polearm}/{katana}/{wsp0}/{wsp1}/{string.Join(",", tf.Calls)}");
        tf.Skills.Clear();
        tf.TradeWeaponskillPoints.Clear();
        // Adoulin group 5: a traded item's AH category; an empty slot raises, as trade:getItem(slot):getAHCat() on nil
        tf.TradeSlots.Add(XI.QuestContent.Item.BottleOfAppleJuice);
        tf.AhCategories[XI.QuestContent.Item.BottleOfAppleJuice] = 58;
        int ahCat0 = tf.Trade.ItemAhCategory(0);
        string ahCat1;
        try { ahCat1 = tf.Trade.ItemAhCategory(1).ToString(); }
        catch (InvalidOperationException e) { ahCat1 = "raises: " + e.Message; }
        Check("facets:the fake answers ItemAhCategory by the slot's item; an empty slot raises",
              "58/raises: trade:getItem(1) is nil", $"{ahCat0}/{ahCat1}");
        tf.TradeSlots.Clear();
        tf.AhCategories.Clear();
        // San d'Oria group 3: hasTitle, needToZone (read and set), popFromQM with a radius, npc:showText, a text's id and
        // messageSpecial by id
        tf.Calls.Clear();
        tf.Titles.Add(XI.QuestContent.Title.FamilyCounselor);
        bool counselor = tf.Player.HasTitle(XI.QuestContent.Title.FamilyCounselor);
        bool officer = tf.Player.HasTitle(XI.QuestContent.Title.LostChildOfficer);
        bool zoneBefore = tf.Player.NeedToZone;
        tf.Player.SetNeedToZone(true);
        bool zoneAfter = tf.Player.NeedToZone;
        bool poppedRadius = tf.Player.PopFromQm(XI.QuestContent.Mob.Davoi.HEMATIC_CYST, claim: true, hide: 0, radius: 1);
        var dryWind = XI.QuestContent.Text.OrdellesCaves.DRY_WIND;
        tf.TextIds[dryWind.ToString()] = 7395;
        int dryWindId = tf.Text.Id(dryWind);
        tf.Text.Special(dryWindId + 1, XI.QuestContent.KeyItem.OrdelleWhetstone);
        string unknownText = "no";
        try { tf.Text.Id(XI.QuestContent.Text.OrdellesCaves.NOTHING_OUT_OF_ORDINARY); } catch (InvalidOperationException) { unknownText = "throws"; }
        tf.Npc = new QuestNpcFake(17723401, tf.Calls);
        tf.Npc.ShowText(XI.QuestContent.Text.NorthernSanDoria.OH_I_WANT_MY_ITEM, XI.QuestContent.Item.WillowFishingRod);
        Check("facets:the fake answers HasTitle, NeedToZone and Text.Id (unknown: throws); records SetNeedToZone, PopFromQm with radius, Text.Special by id, Npc.ShowText",
              $"True/False/False/True/True/7395/throws/Player.SetNeedToZone(True),Player.PopFromQm(HEMATIC_CYST,True,0,radius=1),Text.Special(#7396,{XI.QuestContent.KeyItem.OrdelleWhetstone}),Npc.ShowText({XI.QuestContent.Text.NorthernSanDoria.OH_I_WANT_MY_ITEM},{XI.QuestContent.Item.WillowFishingRod})",
              $"{counselor}/{officer}/{zoneBefore}/{zoneAfter}/{poppedRadius}/{dryWindId}/{unknownText}/{string.Join(",", tf.Calls)}");
        // San d'Oria group 11: player:showText(npc, ...), the text sent to the char alone
        tf.Calls.Clear();
        tf.Player.ShowText(XI.QuestContent.Text.LaTheinePlateau.FAURBELLANT_2, 0, XI.QuestContent.KeyItem.ScriptureOfWind);
        tf.Player.ShowText(XI.QuestContent.Text.LaTheinePlateau.FAURBELLANT_4);
        Check("facets:the fake records Player.ShowText (player:showText) apart from Npc.ShowText",
              $"Player.ShowText({XI.QuestContent.Text.LaTheinePlateau.FAURBELLANT_2},0,{XI.QuestContent.KeyItem.ScriptureOfWind}),Player.ShowText({XI.QuestContent.Text.LaTheinePlateau.FAURBELLANT_4})",
              string.Join(",", tf.Calls));
        tf.Titles.Clear();
        tf.ZoningFlag = false;
        tf.TextIds.Clear();
        tf.Npc = null;
        tf.TradeItems.Clear();
        // San d'Oria group 4: getPlaytime, a bare SpawnMob (no claim, no enmity)
        tf.Calls.Clear();
        tf.PlaytimeSeconds = 86400;
        int playtime = tf.Player.Playtime;
        bool brukdokBefore = tf.Player.MobIsSpawned(XI.QuestContent.Mob.Davoi.PURPLEFLASH_BRUKDOK);
        tf.Player.SpawnMob(XI.QuestContent.Mob.Davoi.PURPLEFLASH_BRUKDOK);
        bool brukdokAfter = tf.Player.MobIsSpawned(XI.QuestContent.Mob.Davoi.PURPLEFLASH_BRUKDOK);
        Check("facets:the fake answers Playtime, records a bare SpawnMob (spawned at offset 0)",
              "86400/False/True/Player.SpawnMob(PURPLEFLASH_BRUKDOK)",
              $"{playtime}/{brukdokBefore}/{brukdokAfter}/{string.Join(",", tf.Calls)}");
        tf.PlaytimeSeconds = 0;
        tf.SpawnedMobs.Clear();
        tf.AliveMobs.Clear();
        // Enveloped in Darkness' qm8: Prog 1 returns nil (the zone's default then plays SOMEONE_HAS_BEEN_DIGGING_HERE)
        // and Prog 3 with only the boots returns that same message itself, so the two arms send identical packets and
        // no flow tells them apart: the fake does
        var envDef = new XI.QuestContent.Sandoria.EnvelopedInDarkness().Compile();
        var qm8 = envDef.States[1].Handlers.Single(h => h.Kind == QuestHandlerKind.OnTrigger && h.Entity == XI.QuestContent.Npc.CrawlersNest.qm8);
        var ef = new Fake();
        ef.Step = 1;
        string qm8Prog1 = qm8.Reply(ef) is { } r1 ? QuestContextFake.Describe(r1) : "null";
        ef.Step = 3;
        ef.KeyItems[XI.QuestContent.KeyItem.OldBoots] = true;
        string qm8BootsOnly = qm8.Reply(ef) is { } r3 ? QuestContextFake.Describe(r3) : "null";
        var digging = XI.QuestContent.Text.CrawlersNest.SOMEONE_HAS_BEEN_DIGGING_HERE;
        Check("facets:Enveloped in Darkness' qm8 gives nil at Prog 1, SOMEONE_HAS_BEEN_DIGGING_HERE at Prog 3 with only the boots",
              $"null/MessageSpecial({digging.Zone}:{digging.Name})",
              $"{qm8Prog1}/{qm8BootsOnly}");
        // San d'Oria group 6: player:startEvent (A Taste for Meat's 538), quest:incrementVar and an exact gil trade (The
        // Brugaire Consortium's npcUtil.tradeHasExactly(trade, { { 'gil', 100 } }))
        tf.Calls.Clear();
        tf.Player.StartEvent(538);
        tf.Quest.IncrementVar("Option", 1);
        tf.Quest.IncrementVar("Option", 1);
        int incremented = tf.Quest.Var("Option");
        tf.TradeItems.Clear();
        tf.TradeItems[65535] = 100;
        bool gilExact = tf.Trade.HasExactlyWithGil(100);
        tf.TradeItems[65535] = 99;
        bool gilFewer = tf.Trade.HasExactlyWithGil(100);
        tf.TradeItems[65535] = 101;
        bool gilMore = tf.Trade.HasExactlyWithGil(100);
        bool gilMoreLoose = tf.Trade.HasWithGil(100);
        tf.TradeItems[65535] = 100;
        tf.TradeItems[XI.QuestContent.Item.BoneChip] = 1;
        bool gilAndChip = tf.Trade.HasExactlyWithGil(100);
        Check("facets:the fake records StartEvent and IncrementVar (the var moves); HasExactlyWithGil takes exactly the gil",
              "Player.StartEvent(538),Quest.IncrementVar(Option,1),Quest.IncrementVar(Option,1)/2/True/False/False/True/False",
              $"{string.Join(",", tf.Calls)}/{incremented}/{gilExact}/{gilFewer}/{gilMore}/{gilMoreLoose}/{gilAndChip}");
        tf.TradeItems.Clear();
        tf.Vars.Remove("Option");
        // The Rivalry and The Competition: while both are available each answers Ufanne with its own fixed 310. The two
        // actions do alternate in replay (fixed action ids, so the lookup cycle sees two actions), but a reply's kind
        // (Event against ProgressEvent) changes only its priority, not its packets, so no flow catches a wrong kind:
        // this check does
        var ufanne = XI.QuestContent.Npc.PortSanDoria.Ufanne;
        var uf = new Fake();
        var rivUfanne = new XI.QuestContent.Sandoria.TheRivalry().Compile().States[0].Handlers
            .Single(h => h.Kind == QuestHandlerKind.OnTrigger && h.Entity == ufanne);
        var compUfanne = new XI.QuestContent.Sandoria.TheCompetition().Compile().States[0].Handlers
            .Single(h => h.Kind == QuestHandlerKind.OnTrigger && h.Entity == ufanne);
        string rivReply = (rivUfanne.Fixed ?? rivUfanne.Reply?.Invoke(uf)) is { } rr ? QuestContextFake.Describe(rr) : "null";
        string compReply = (compUfanne.Fixed ?? compUfanne.Reply?.Invoke(uf)) is { } cr ? QuestContextFake.Describe(cr) : "null";
        Check("facets:The Rivalry and The Competition each answer Ufanne with 310 while both are available",
              $"{QuestContextFake.Describe(QuestReply.Event(310))}/{QuestContextFake.Describe(QuestReply.Event(310))}",
              $"{rivReply}/{compReply}");
        // the fake's party is the char (its zone, distance 0, nation, race, main job), then the members a test adds
        var pf = new Fake { Zone = 100, NationId = 1, RaceId = 7, MainJob = 3 };
        pf.PartyMemberRows.Add((101, 16.5, 0, 8, 1));
        Check("facets:the fake's PartyMembers is the char, then PartyMemberRows; Nation and Race",
              "2/(100, 0, 1, 7, 3)/(101, 16.5, 0, 8, 1)/1/7",
              $"{pf.Player.PartyMembers.Count}/{pf.Player.PartyMembers[0]}/{pf.Player.PartyMembers[1]}/{pf.Player.Nation}/{pf.Player.Race}");
        // the three teamwork quests: 129's finish with option 0 and the local Prog 1 completes the quest and clears the
        // local Prog (no trace sees the clearing: the quest is completed); when complete fails, the local Prog stays 1
        foreach (QuestScript tw in new QuestScript[]
                 {
                     new XI.QuestContent.Sandoria.IntroductionToTeamwork(), new XI.QuestContent.Sandoria.IntermediateTeamwork(),
                     new XI.QuestContent.Sandoria.AdvancedTeamwork(),
                 })
        {
            var twFinish = tw.Compile().States[1].Handlers.Single(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Id == 129);
            var done = new Fake { Option = 0 };
            done.QuestLocalVars["Prog"] = 1;
            twFinish.Run(done);
            var failed = new Fake { Option = 0, CompleteResult = false };
            failed.QuestLocalVars["Prog"] = 1;
            twFinish.Run(failed);
            Check($"facets:{tw.GetType().Name} 129 finished with option 0 and the local Prog 1: completed, the local Prog cleared; not when complete fails",
                  "Quest.Complete(),Quest.SetLocalVar(Prog,0)/0/Quest.Complete()/1",
                  $"{string.Join(",", done.Calls)}/{done.QuestLocalVars["Prog"]}/{string.Join(",", failed.Calls)}/{failed.QuestLocalVars["Prog"]}");
        }
        // A Timely Visit: each NM's death handler compares the dying mob's id with its own (jugnerID.mob.GIOLLEMITTE, + 1
        // for the Skeleton Esquire). Each handler is keyed by its own mob's name, and Jugner Forest has one mob of each
        // name, so no trace can show the comparison failing: this check runs each handler on the other mob's id
        var atv = new XI.QuestContent.Sandoria.ATimelyVisit().Compile().States[2].Handlers;
        string NmDeath(string entity, int mobId)
        {
            var m = new Fake { Mob = new QuestNpcFake(mobId) };
            m.MobIds["GIOLLEMITTE"] = 17203666;
            m.Step = 5;
            atv.Single(h => h.Kind == QuestHandlerKind.OnMobDeath && h.Entity == entity).Run(m);
            return string.Join(",", m.Calls);
        }
        var giollemitte = XI.QuestContent.Npc.JugnerForest.GiollemitteBFeroun;
        var esquire = XI.QuestContent.Npc.JugnerForest.SkeletonEsquireNM;
        Check("facets:A Timely Visit's NM deaths set NMKilled for their own mob's id only",
              "Player.SetLocalVar(NMKilled,1)//Player.SetLocalVar(NMKilled,1)/",
              $"{NmDeath(giollemitte, 17203666)}/{NmDeath(giollemitte, 17203667)}/{NmDeath(esquire, 17203667)}/{NmDeath(esquire, 17203666)}");
        // Blackmail's 648 (section 4) and The Setting Sun's 658 act only when complete succeeds; with no item reward it
        // cannot fail in play, so no trace shows the other side
        var bm648 = new XI.QuestContent.Sandoria.Blackmail().Compile().States[3].Handlers
            .Single(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Id == 648);
        var ss658 = new XI.QuestContent.Sandoria.TheSettingSun().Compile().States[1].Handlers
            .Single(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Id == 658);
        var bmFail = new Fake { CompleteResult = false };
        bm648.Run(bmFail);
        var ssFail = new Fake { CompleteResult = false };
        ss658.Run(ssFail);
        var ssDone = new Fake();
        ss658.Run(ssDone);
        Check("facets:Blackmail's 648 and The Setting Sun's 658 take the trade (and set needToZone) only when complete succeeds",
              "Quest.Complete()/Quest.Complete()/Quest.Complete(),Trade.Confirm(),Player.SetNeedToZone(True)",
              $"{string.Join(",", bmFail.Calls)}/{string.Join(",", ssFail.Calls)}/{string.Join(",", ssDone.Calls)}");
        // A Job for the Consortium: by day the customs catch the char on a roll of 25 or under (math.randomInt(1, 100) <=
        // 25). The roll is the script RNG's on the pinned seed, so no flow can choose a roll of exactly 25 or 26: this
        // check runs _6u2's and Haubijoux's handlers (section 3, Prog 2) at 12:00 on each
        var ajcCustoms = new XI.QuestContent.Sandoria.AJobForTheConsortium().Compile().States[2].Handlers;
        string Customs(string entity, int roll)
        {
            var c = new Fake { Hour = 12 };
            c.Rolls.Enqueue(roll);
            c.Step = 2;
            var reply = ajcCustoms.Single(h => h.Kind == QuestHandlerKind.OnTrigger && h.Entity == entity).Reply(c);
            return $"{QuestContextFake.Describe(reply)}:{string.Join(",", c.Calls)}";
        }
        string u2 = XI.QuestContent.Npc.PortJeuno._6u2, haubijoux = XI.QuestContent.Npc.PortJeuno.Haubijoux;
        Check("facets:A Job for the Consortium's customs catch the char on a daytime roll of 25, not of 26",
              "ProgressEvent(54,1):Player.RandomInt(1,100),Quest.SetStep(1)/ProgressEvent(54,0):Player.RandomInt(1,100)/" +
              "ProgressEvent(54,1):Player.RandomInt(1,100),Quest.SetStep(1)/ProgressEvent(54,0):Player.RandomInt(1,100)",
              $"{Customs(u2, 25)}/{Customs(u2, 26)}/{Customs(haubijoux, 25)}/{Customs(haubijoux, 26)}");
        // Rosel the Armorer: Rosel's first talk draws the prince, Stage = math.randomInt(1, 2), and plays 523 with Stage - 1.
        // The draw is the script RNG's on the pinned seed (every trace draws 1), so this check runs the available
        // section's Rosel at Stage 0 on a draw of 1 and of 2
        var roselTalk = new XI.QuestContent.Sandoria.RoselTheArmorer().Compile().States[0].Handlers
            .Single(h => h.Kind == QuestHandlerKind.OnTrigger && h.Entity == XI.QuestContent.Npc.SouthernSanDoria.Rosel);
        string Draw(int roll)
        {
            var c = new Fake();
            c.Rolls.Enqueue(roll);
            var reply = roselTalk.Reply(c);
            return $"{QuestContextFake.Describe(reply)}:{string.Join(",", c.Calls)}";
        }
        Check("facets:Rosel the Armorer draws Stage with math.randomInt(1, 2) and plays 523 with Stage - 1",
              "ProgressEvent(523,0,0,0,0,0):Player.RandomInt(1,2),Quest.SetVar(Stage,1,0)/" +
              "ProgressEvent(523,0,0,0,0,1):Player.RandomInt(1,2),Quest.SetVar(Stage,2,0)",
              $"{Draw(1)}/{Draw(2)}");
        tf.Calls.Clear();
        tf.Quest.SetStep(3);
        Check("facets:SetStep writes the Prog var", "3/3", $"{tf.Quest.Step}/{tf.Quest.Var("Prog")}");
        tf.Vars["Prog"] = 4;
        Check("facets:Step reads the Prog var", "4", tf.Quest.Step.ToString());
        tf.Quest.SetVarBit("Prog", 0);
        Check("facets:SetVarBit on Prog moves Step and IsVarBitsSet agrees", "5/True", $"{tf.Quest.Step}/{tf.Quest.IsVarBitsSet("Prog", 0, 2)}");
        tf.Quest.UnsetVarBit("Prog", 0);
        Check("facets:UnsetVarBit clears only that bit", "4/False",
              $"{tf.Quest.Step}/{tf.Quest.IsVarBitsSet("Prog", 0)}");

        string range = "no";
        try { QuestReply.Event(1).Option(8, 1); } catch (ArgumentOutOfRangeException) { range = "ArgumentOutOfRangeException"; }
        Check("facets:Option(8, ...) is out of range", "ArgumentOutOfRangeException", range);

        // every public field of QuestReward, set to a non-default value, comes back from Reward(): a field added later
        // that Reward() does not copy fails here
        var full = new QuestReward();
        int n = 1;
        foreach (var fld in typeof(QuestReward).GetFields())
        {
            if (fld.FieldType == typeof(int?)) fld.SetValue(full, n++);
            else if (fld.FieldType == typeof(int[])) fld.SetValue(full, new[] { n++, n++ });
            else if (fld.FieldType == typeof(string)) fld.SetValue(full, "Var" + n++);
            else if (fld.FieldType == typeof(QuestItemParams)) fld.SetValue(full, new QuestItemParams { Silent = true, FromTrade = false, Multiple = true });
            else throw new InvalidOperationException($"QuestReward.{fld.Name}: teach this test its type");
        }
        var copied = new FixedRewardQuest(full).Compile().Reward;
        foreach (var fld in typeof(QuestReward).GetFields())
        {
            var a = fld.GetValue(full);
            var b = fld.GetValue(copied);
            static string Text(object v) => v is int[] i ? string.Join(",", i)
                : v is QuestItemParams p ? $"silent={p.Silent},fromTrade={p.FromTrade},multiple={p.Multiple}" : v?.ToString();
            string was = Text(a), got = Text(b);
            Check($"facets:Reward() copies QuestReward.{fld.Name}", was, got);
        }

        var nb = new QuestScriptBuilder();
        Throws("reward", () => nb.Reward(null));
        Throws("name", () => nb.State(QuestStatus.Available).Npc(235, null));
        Throws("run", () => nb.State(QuestStatus.Available).OnEventFinish(235, 1, null));
        Throws("reply", () => nb.State(QuestStatus.Available).Npc(235, "X").OnTrade(null));
        Throws("fixedReply", () => nb.State(QuestStatus.Available).Npc(235, "X").OnTrigger((QuestReply)null));
    }

    // three any-status states: AnyState, a step, a guard; State(...).OrAny(step); chained from a state and an NPC builder
    sealed class AnyStatusQuest : QuestScript
    {
        public AnyStatusQuest() : base(QuestLog.Bastok, "ANY") { }
        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(2, 3);
            b.AnyState()
                .Npc(235, "A").OnTrigger(QuestReply.Event(1))
            .AnyState(step: 2)
                .OnEventFinish(235, 2, q => { })
            .AnyState(when: q => q.Player.LocalVar("Go") == 1)
                .Npc(235, "B").OnTrigger(QuestReply.Event(3))
                .AnyState(step: 3, when: q => q.Quest.LocalVar("Go") == 1)
                .Npc(235, "C").OnTrigger(QuestReply.Event(4));
            b.State(QuestStatus.Available, when: q => false).OrAny(step: 3)
                .Npc(235, "D").OnTrigger(QuestReply.Event(5));
            b.AnyState(step: 9).Npc(235, "E").OnTrigger(QuestReply.Event(6));
        }
    }

    // quest rework B3b prep: any-status states, local vars (player, quest, npc), the npc and settings facets, char-var expiry
    static void RunAnyStatusAndLocals()
    {
        Console.WriteLine("\n== QuestScript: any-status states, local vars, npc, settings ==");
        var d = new AnyStatusQuest().Compile();
        Check("any:six states in declaration order", "6", d.States.Count.ToString());
        Check("any:AnyState's match is AnyStatus, State's is not", "True/False/True",
              $"{d.States[0].Matches[0].AnyStatus}/{d.States[4].Matches[0].AnyStatus}/{d.States[4].Matches[1].AnyStatus}");
        var f = new Fake();
        string All(QuestStateDecl s, int step) => string.Join(",", new[] { QuestStatus.Available, QuestStatus.Accepted, QuestStatus.Completed }
            .Select(st => s.IsMatch(st, step, f).ToString()));
        Check("any:AnyState() matches Available, Accepted and Completed", "True,True,True", All(d.States[0], 0));
        Check("any:AnyState(step: 2) at step 2: all three", "True,True,True", All(d.States[1], 2));
        Check("any:  at step 1: none", "False,False,False", All(d.States[1], 1));
        Check("any:AnyState(when) with the guard false: none", "False,False,False", All(d.States[2], 0));
        f.PlayerLocalVars["Go"] = 1;
        Check("any:  with the guard true (Player.LocalVar): all three", "True,True,True", All(d.States[2], 0));
        Check("any:the NPC builder's AnyState, Quest.LocalVar guard unset: none", "False,False,False", All(d.States[3], 3));
        f.QuestLocalVars["Go"] = 1;
        Check("any:  set, at step 3: all three", "True,True,True", All(d.States[3], 3));
        Check("any:State(Available, false).OrAny(step: 3) at step 3: all three", "True,True,True", All(d.States[4], 3));
        Check("any:  at step 1: none", "False,False,False", All(d.States[4], 1));
        Check("any:a step matched by AnyState but never set is reported", "step 9 is matched but never set",
              string.Join(" | ", QuestScriptValidator.Validate(d)));

        // local vars: the player's, the quest's and the npc's are separate stores; sets are recorded
        var t = new Fake();
        IQuestContext ctx = t;
        IQuestCheck chk = t;
        Check("locals:no npc: the context's Npc is null", "True", (ctx.Npc == null).ToString());
        t.Npc = new QuestNpcFake(42, t.Calls);
        ctx.Player.SetLocalVar("X", 4);
        ctx.Quest.SetLocalVar("X", 6);
        ctx.Npc.SetLocalVar("X", 8);
        Check("locals:sets are recorded", "Player.SetLocalVar(X,4),Quest.SetLocalVar(X,6),Npc.SetLocalVar(X,8)", string.Join(",", t.Calls));
        Check("locals:each store reads its own", "4/6/8/42", $"{ctx.Player.LocalVar("X")}/{ctx.Quest.LocalVar("X")}/{ctx.Npc.LocalVar("X")}/{ctx.Npc.Id}");
        Check("locals:the check's player and quest views read the same", "4/6", $"{chk.Player.LocalVar("X")}/{chk.Quest.LocalVar("X")}");
        Check("locals:an unset local var is 0", "0/0/0", $"{ctx.Player.LocalVar("Y")}/{ctx.Quest.LocalVar("Y")}/{ctx.Npc.LocalVar("Y")}");

        // settings
        t.MainSettings["ENABLE_WOTG"] = 1;
        Check("settings:Main reads MainSettings, an unknown name is 0", "1/0/1", $"{ctx.Settings.Main("ENABLE_WOTG")}/{ctx.Settings.Main("NOPE")}/{chk.Settings.Main("ENABLE_WOTG")}");

        // SetCharVar keeps the expiry
        t.Calls.Clear();
        ctx.Player.SetCharVar("T", 1, 1234);
        ctx.Player.SetCharVar("U", 2);
        Check("charvar:SetCharVar records the expiry and keeps it", "Player.SetCharVar(T,1,1234),Player.SetCharVar(U,2,0)/1/1234/0",
              $"{string.Join(",", t.Calls)}/{t.CharVars["T"]}/{t.CharVarExpiry["T"]}/{(t.CharVarExpiry.TryGetValue("U", out var ue) ? ue : 0)}");

        // Too Many Chefs' offer reads Settings.Main("ENABLE_WOTG")
        var chefs = new XI.QuestContent.Bastok.TooManyChefs().Compile();
        var g = new Fake();
        g.Fame[XI.QuestContent.FameArea.Bastok] = 5;
        g.MainSettings["ENABLE_WOTG"] = 1;
        string on = chefs.States[0].IsMatch(QuestStatus.Available, 0, g).ToString();
        g.MainSettings["ENABLE_WOTG"] = 0;
        Check("settings:Too Many Chefs' offer needs ENABLE_WOTG 1", "True/False", $"{on}/{chefs.States[0].IsMatch(QuestStatus.Available, 0, g)}");

        // A Moral Manifest: call-only, validates clean; the cloth trade (gil), the cutting (giveItem), the Avatar's pop (a list)
        var moral = new XI.QuestContent.OtherAreas.AMoralManifest().Compile();
        Check("moral:call-only and clean", "True/", $"{moral.CallOnly}/{string.Join(" | ", QuestScriptValidator.Validate(moral))}");
        // the finishes are keyed on the csid alone, as the scripts' onEventFinish: the last state, any status, no check
        var finishes = moral.States[11];
        Check("moral:every event finish in one any-status state with no check", "True/True/46,47,51,700,705,48,49,50/0",
              $"{finishes.Matches.All(x => x.AnyStatus && x.When == null)}/{finishes.IsMatch(QuestStatus.Completed, 0, new Fake())}/" +
              string.Join(",", finishes.Handlers.Select(h => h.Id)) + "/" +
              moral.States.Take(11).Sum(st => st.Handlers.Count(h => h.Kind == QuestHandlerKind.OnEventFinish)));
        var m = new Fake { Midnight = 1234 };
        m.TradeItems[XI.QuestContent.Item.SquareOfVelvetCloth] = 1;
        m.TradeItems[XI.QuestContent.Item.SquareOfRainbowCloth] = 1;
        m.TradeItems[65535] = 9999;
        var clothTrade = moral.States[4].Handlers.Single(h => h.Kind == QuestHandlerKind.OnTrade);
        string short1 = clothTrade.Reply(m)?.Id.ToString() ?? "null";
        m.TradeItems[65535] = 10000;
        string paid = clothTrade.Reply(m)?.Id.ToString() ?? "null";
        Check("moral:the cloth trade needs 10000 gil; moral 3, moralZone, moralWait the midnight", "null/703/3/1/1234",
              $"{short1}/{paid}/{m.CharVars["moral"]}/{m.PlayerLocalVars["moralZone"]}/{m.CharVars["moralWait"]}");
        m.Calls.Clear();
        m.GiveResult = false;
        finishes.Handlers.Single(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Id == 705).Run(m);
        Check("moral:705 with the cutting not given leaves moral", "Player.GiveItem(1867)/3", $"{string.Join(",", m.Calls)}/{m.CharVars["moral"]}");
        m.Calls.Clear();
        finishes.Handlers.Single(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Id == 48).Run(m);
        Check("moral:48 pops the Avatar and its NMs in the script's order, hidden 1 s",
              "Player.PopFromQm(YAGUDO_AVATAR;DUU_MASA_THE_ONECUT;FEE_JUGE_THE_RAMFIST;GOO_PAKE_THE_BLOODHOUND;KEE_TAW_THE_NIGHTINGALE;LAA_YAKU_THE_AUSTERE;POO_YOZO_THE_BABBLER,1)",
              string.Join(",", m.Calls));

        // Children of the Rune: call-only, validates clean; the offer behind ADVANCED_JOB_LEVEL, the update halving HP / MP,
        // the finishes in one any-status state, each with the reward attempt
        var cotr = new XI.QuestContent.Adoulin.ChildrenOfTheRune().Compile();
        Check("cotr:call-only and clean", "True/", $"{cotr.CallOnly}/{string.Join(" | ", QuestScriptValidator.Validate(cotr))}");
        var gate = new Fake { MainLevel = 30 };
        gate.MainSettings["ADVANCED_JOB_LEVEL"] = 30;
        string atGate = cotr.States[6].IsMatch(QuestStatus.Available, 0, gate).ToString();
        gate.MainSettings["ADVANCED_JOB_LEVEL"] = 31;
        Check("cotr:the offer needs ADVANCED_JOB_LEVEL", "True/False", $"{atGate}/{cotr.States[6].IsMatch(QuestStatus.Available, 0, gate)}");
        var cotrEvents = cotr.States[8];
        Check("cotr:the update and every finish in one any-status state with no check", "True/26,23,24,26,25,27,28,29/0",
              $"{cotrEvents.Matches.All(x => x.AnyStatus && x.When == null)}/" + string.Join(",", cotrEvents.Handlers.Select(h => h.Id)) + "/" +
              cotr.States.Take(8).Sum(st => st.Handlers.Count(h => h.Kind is QuestHandlerKind.OnEventFinish or QuestHandlerKind.OnEventUpdate)));
        var update = cotrEvents.Handlers.Single(h => h.Kind == QuestHandlerKind.OnEventUpdate);
        var r = new Fake { HP = 11, MP = 5, Option = 0 };
        update.Run(r);
        string none = string.Join(",", r.Calls);
        r.Option = 1;
        update.Run(r);
        Check("cotr:update option 1 halves HP rounded up, MP 5 or less kept", "/Player.SetHP(6)/6/5", $"{none}/{string.Join(",", r.Calls)}/{r.HP}/{r.MP}");
        var full = new Fake { Option = 1, GiveResult = false };
        cotrEvents.Handlers.Single(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Id == 26).Run(full);
        Check("cotr:26 finished with the claymore not given: pending", "Player.SetCharVar(RUN_COTR,3,0),Player.GiveItem(20781)", string.Join(",", full.Calls));
        full.Calls.Clear();
        full.GiveResult = true;
        cotrEvents.Handlers.Single(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Id == 29).Run(full);
        Check("cotr:29 with room: the reward in the script's order",
              "Player.GiveItem(20781),Player.UnlockJob(22),Text.Special(257:YOU_CAN_NOW_BECOME,1)," +
              "Player.GiveKeyItem(2964),Player.SetCharVar(RUN_COTR,0,0),Player.DelKeyItem(2279)," +
              "Player.CompleteQuest(Adoulin,CHILDREN_OF_THE_RUNE)",
              string.Join(",", full.Calls));
        full.Calls.Clear();
        cotrEvents.Handlers.Single(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Id == 27).Run(full);
        Check("cotr:27 with nothing pending: nothing", "", string.Join(",", full.Calls));

        // Never to Return: the reading's update answers (VanadielUniqueDay() + the char's id) % 100, available and completed
        // (the golden trace masks that byte: the char's id is the trace's slot in the char-id pool)
        var ntr = new XI.QuestContent.Jeuno.NeverToReturn().Compile();
        Check("ntr:clean", "", string.Join(" | ", QuestScriptValidator.Validate(ntr)));
        string Reading(int state, int charId)
        {
            var nf = new Fake { UniqueDay = 8003, CharId = charId };
            ntr.States[state].Handlers.Single(h => h.Kind == QuestHandlerKind.OnEventUpdate && h.Id == 204).Run(nf);
            return string.Join(",", nf.Calls);
        }
        Check("ntr:204's update, available and completed, chars 996177 and 996162",
              "Player.UpdateEvent(80)/Player.UpdateEvent(65)/Player.UpdateEvent(80)/Player.UpdateEvent(65)",
              $"{Reading(0, 996177)}/{Reading(0, 996162)}/{Reading(2, 996177)}/{Reading(2, 996162)}");

        // Community Service: the 116 update stores the starter's name in the zone's commServicePlayer1-4 (four bytes each,
        // the low byte first); Zauko's 113 decodes it for another char once the night's quest is turned in, and plays 113
        // alone for the finisher
        var csq = new XI.QuestContent.Jeuno.CommunityService().Compile();
        Check("cs:clean", "", string.Join(" | ", QuestScriptValidator.Validate(csq)));
        var starter = new Fake { Hour = 18, UniqueDay = 8000, CharName = "Qgolden996177", CharId = 996177 };
        csq.States[0].Handlers.Single(h => h.Kind == QuestHandlerKind.OnEventUpdate && h.Id == 116).Run(starter);
        string stored = string.Join(",", Enumerable.Range(1, 4).Select(i => starter.ZoneLocalVars["commServicePlayer" + i]));
        string Zauko113(int charId)
        {
            var other = new Fake { Hour = 19, UniqueDay = 8000, CharId = charId };
            foreach (var kv in starter.ZoneLocalVars) other.ZoneLocalVars[kv.Key] = kv.Value;
            other.ZoneLocalVars["commServiceComp"] = 1;
            other.ZoneLocalVars["commServicePlayer"] = 996177;
            var zauko = csq.States[0].Handlers.Single(h => h.Kind == QuestHandlerKind.OnTrigger && h.Entity == XI.QuestContent.Npc.LowerJeuno.Zauko);
            return QuestContextFake.Describe(zauko.Reply(other));
        }
        Check("cs:the name in four zone vars; 113 with the finisher's name for another char, alone for the finisher",
              "1819240273,963536228,925972025,55/Event(113,{[0]=1,strings={[0]='Qgolden996177'}})/Event(113)",
              $"{stored}/{Zauko113(996178)}/{Zauko113(996177)}");

        // Rubbish Day: the typed partner name (updateEvent(the char's id, the partner's id or 0, the reply)). The setup is
        // what separates the char's own name from an unknown one: the char is online in its own zone here, so only the
        // self check keeps its name from (id, its own id, 0); the golden trace already catches a port without that check
        // (jeuno.rubbish_day step 11). string.lower is the C locale's: A-Z only, so the Kelvin sign (U+212A), which
        // ToLowerInvariant would make a 'k', leaves 'KUROU-MOROU' unmatched
        var rdq = new XI.QuestContent.Jeuno.RubbishDay().Compile();
        Check("rd:clean", "", string.Join(" | ", QuestScriptValidator.Validate(rdq)));
        string Typed(string text, int bobZone)
        {
            var rf = new Fake { CharId = 996177, Zone = 245, OptionText = text, CharName = "Qgolden996177" };
            rf.OnlinePlayers["Qgolden996177"] = (996177, 245);
            rf.OnlinePlayers["Qgolden996178"] = (996178, bobZone);
            rdq.States[0].Handlers.Single(h => h.Kind == QuestHandlerKind.OnEventUpdate && h.Id == 199).Run(rf);
            return string.Join(",", rf.Calls);
        }
        Check("rd:typed '', 'KUROU-MOROU', bob in the zone, bob elsewhere, the char's own name (online, in its zone), nobody, a Kelvin sign",
              "Player.UpdateEvent(996177,0,3)/Player.UpdateEvent(996177,0,2)/Player.UpdateEvent(996177,996178,0)/" +
              "Player.UpdateEvent(996177,0,0)/Player.UpdateEvent(996177,0,0)/Player.UpdateEvent(996177,0,0)/Player.UpdateEvent(996177,0,0)",
              $"{Typed("", 245)}/{Typed("KUROU-MOROU", 245)}/{Typed("Qgolden996178", 245)}/{Typed("Qgolden996178", 244)}/" +
              $"{Typed("Qgolden996177", 245)}/{Typed("Nobody", 245)}/{Typed("\u212AUROU-MOROU", 245)}");

        // Jeuno group 6 (the Dancer quests). Each finish below is run on the fake to show what the golden traces cannot: the
        // local vars Quest[3][97|98|99]mustZone the finishes write (getMustZone reads the char var, so no trace sees them),
        // and Rhea Myuliah's Prog 4 arm against a GM's Prog 5, which give the same steps in the trace (10121 is her
        // default's csid too)
        string Finish(XI.Quests.Authoring.QuestScriptDefinition def, int state, int csid, Fake f)
        {
            def.States[state].Handlers.Single(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Id == csid).Run(f);
            return string.Join(",", f.Calls);
        }
        var lmq = new XI.QuestContent.Jeuno.LakesideMinuet().Compile();
        Check("lm:clean", "", string.Join(" | ", QuestScriptValidator.Validate(lmq)));
        string Rhea(int prog)
        {
            var rf = new Fake { Status = QuestStatus.Accepted, Step = prog };
            var rhea = lmq.States[1].Handlers.Single(h => h.Kind == QuestHandlerKind.OnTrigger && h.Entity == XI.QuestContent.Npc.UpperJeuno.RheaMyuliah);
            var reply = rhea.Reply(rf);
            return reply == null ? "null" : QuestContextFake.Describe(reply);
        }
        Check("lm:Rhea Myuliah at Prog 4 (the arm) and at a GM's Prog 5 (no reply: her default)", "ProgressEvent(10121)/null", $"{Rhea(4)}/{Rhea(5)}");
        Check("lm:10118 in the script's order, needToZone last",
              "Quest.Complete(),Player.AddFame(0,7),Player.AddFame(1,7),Player.AddFame(2,7),Player.UnlockJob(19)," +
              $"Text.Special(244:UNLOCK_DANCER),Player.DelKeyItem({XI.QuestContent.KeyItem.StardustPebble}),Player.GiveKeyItem({XI.QuestContent.KeyItem.JobGestureDancer})," +
              "Player.SetNeedToZone(True)",
              Finish(lmq, 1, 10118, new Fake()));

        var uwq = new XI.QuestContent.Jeuno.TheUnfinishedWaltz().Compile();
        Check("uw:clean", "", string.Join(" | ", QuestScriptValidator.Validate(uwq)));
        Check("uw:10133 completed: The Road to Divadom's Timer (char var) and mustZone (a local var); not completed: nothing",
              "Quest.Complete(),Player.SetCharVar(Quest[3][97]Timer,8001,0),Player.SetLocalVar(Quest[3][97]mustZone,1)/Quest.Complete()",
              $"{Finish(uwq, 1, 10133, new Fake { UniqueDay = 8000 })}/{Finish(uwq, 1, 10133, new Fake { UniqueDay = 8000, CompleteResult = false })}");

        var rdq6 = new XI.QuestContent.Jeuno.TheRoadToDivadom().Compile();
        Check("rdv:clean", "", string.Join(" | ", QuestScriptValidator.Validate(rdq6)));
        var lailaF = new Fake { Status = QuestStatus.Accepted, Step = 3, MainJob = 19 };
        var laila3 = rdq6.States[1].Handlers.Single(h => h.Kind == QuestHandlerKind.OnTrigger && h.Entity == XI.QuestContent.Npc.UpperJeuno.Laila);
        string laila3Reply = QuestContextFake.Describe(laila3.Reply(lailaF));
        Check("rdv:Laila at Prog 3 starts 10139, 10214 and 10215, then replies 10170",
              "Player.StartEvent(10139),Player.StartEvent(10214),Player.StartEvent(10215)/ProgressEvent(10170)",
              $"{string.Join(",", lailaF.Calls)}/{laila3Reply}");
        Check("rdv:10170 for a female char: the tights (F), completed, Comeback Queen's Timer, its mustZone (a local var), HQuest Prog 1; " +
              "no room for a male char: the tights (M), Prog 4",
              "Player.GiveItem(15660),Quest.Complete(),Player.SetCharVar(Quest[3][98]Timer,8001,0),Player.SetLocalVar(Quest[3][98]mustZone,1)," +
              "Player.SetCharVar(HQuest[DncArtifact]Prog,1,0)/Player.GiveItem(15659),Quest.SetStep(4)",
              $"{Finish(rdq6, 1, 10170, new Fake { UniqueDay = 8000, GenderId = 0 })}/{Finish(rdq6, 1, 10170, new Fake { GiveResult = false })}");

        var cqq = new XI.QuestContent.Jeuno.ComebackQueen().Compile();
        Check("cq:clean", "", string.Join(" | ", QuestScriptValidator.Validate(cqq)));
        var cqZone = new Fake { Step = 4, Position = (-52.5, 0.25, 101, 165) };
        Check("cq:10208 (progressZone): Prog + 1, then the char set down where it stands, in Upper Jeuno",
              "Quest.IncrementVar(Prog,1),Player.SetPos(-52.5,0.25,101,165,244)/5",
              FormattableString.Invariant($"{Finish(cqq, 1, 10208, cqZone)}/{cqZone.Step}"));
        Check("cq:10154 and 10211 for a female char: the casaque (F), completed, A Furious Finale's Timer and mustZone (a local var); " +
              "10211 with no room: Prog 8; 10154 with no room: nothing more",
              "Player.GiveItem(14579),Quest.Complete(),Player.SetCharVar(Quest[3][99]Timer,8003,0),Player.SetLocalVar(Quest[3][99]mustZone,1)/" +
              "Player.GiveItem(14579),Quest.Complete(),Player.SetCharVar(Quest[3][99]Timer,8003,0),Player.SetLocalVar(Quest[3][99]mustZone,1)/" +
              "Player.GiveItem(14578),Quest.SetStep(8)/Player.GiveItem(14578)",
              $"{Finish(cqq, 1, 10154, new Fake { UniqueDay = 8002, GenderId = 0 })}/{Finish(cqq, 1, 10211, new Fake { UniqueDay = 8002, GenderId = 0 })}/" +
              $"{Finish(cqq, 1, 10211, new Fake { GiveResult = false })}/{Finish(cqq, 1, 10154, new Fake { GiveResult = false })}");

        // Jeuno group 7 (Maat's limit breaks). The fake answers the new members (LevelCap, MobIsDead) and records
        // SetLevelCap; Shattering Stars' battlefield win (32001 in the five Maat arenas) is run on the fake because its
        // golden trace is pending (a battlefield trace is stable only with Bastok branch 2's bcnm_records pin)
        var lbFake = new Fake { LevelCap = 55 };
        lbFake.AliveMobs.Add("BOREAL_TIGER+0");
        lbFake.Player.SetLevelCap(60);
        Check("lb:the fake answers LevelCap / MobIsDead and records SetLevelCap", "Player.SetLevelCap(60)/60/False/True",
              $"{string.Join(",", lbFake.Calls)}/{lbFake.Player.LevelCap}/{lbFake.Player.MobIsDead("BOREAL_TIGER")}/{lbFake.Player.MobIsDead("BOREAL_HOUND")}");
        var idcq = new XI.QuestContent.Jeuno.InDefiantChallenge().Compile();
        var athmq = new XI.QuestContent.Jeuno.AtopTheHighestMountains().Compile();
        var wbtwq = new XI.QuestContent.Jeuno.WhenceBlowsTheWind().Compile();
        var ssq = new XI.QuestContent.Jeuno.ShatteringStars().Compile();
        var btsq = new XI.QuestContent.Jeuno.BeyondTheSun().Compile();
        Check("lb:the five clean", "////",
              string.Join("/", new[] { idcq, athmq, wbtwq, ssq, btsq }.Select(def => string.Join(" | ", QuestScriptValidator.Validate(def)))));
        string Win(ushort zone, int win, int job, QuestStatus status, int mask)
        {
            var wf = new Fake { Zone = zone, MainJob = job };
            wf.PlayerLocalVars["battlefieldWin"] = win;
            wf.QuestStatuses["Jeuno/SHATTERING_STARS"] = status;
            wf.CharVars["maatsCap"] = mask;
            ssq.States[2].Handlers.Single(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Id == 32001 && h.Zone == zone).Run(wf);
            return string.Join(",", wf.Calls);
        }
        Check("ss:32001 in Balga's Dais, win 101, a Monk accepted: the scroll, Prog 2, maatsCap bit 1, Maat Masher; completed with " +
              "the bit already set: the title alone; win 5 (Horlais Peak's) in Balga's Dais: nothing; Horlais Peak's 7 for a " +
              "completed Summoner: bit 14 added to 1, the title",
              $"Player.GiveItem({XI.QuestContent.Item.ScrollOfInstantWarp}),Quest.SetStep(2),Player.SetCharVar(maatsCap,2,0),Player.AddTitle({XI.QuestContent.Title.MaatMasher})/" +
              $"Player.AddTitle({XI.QuestContent.Title.MaatMasher})//Player.SetCharVar(maatsCap,16385,0),Player.AddTitle({XI.QuestContent.Title.MaatMasher})",
              $"{Win(XI.QuestContent.Zone.BalgasDais, 101, 2, QuestStatus.Accepted, 0)}/{Win(XI.QuestContent.Zone.BalgasDais, 103, 2, QuestStatus.Completed, 2)}/" +
              $"{Win(XI.QuestContent.Zone.BalgasDais, 5, 2, QuestStatus.Accepted, 0)}/{Win(XI.QuestContent.Zone.HorlaisPeak, 7, 15, QuestStatus.Completed, 1)}");
        Check("ss:32001 handlers in the five arenas, in the accepted-or-completed state",
              $"{XI.QuestContent.Zone.BalgasDais},{XI.QuestContent.Zone.ChamberOfOracles},{XI.QuestContent.Zone.HorlaisPeak},{XI.QuestContent.Zone.QubiaArena},{XI.QuestContent.Zone.WaughroonShrine}/Accepted,Completed",
              string.Join(",", ssq.States[2].Handlers.Where(h => h.Id == 32001).Select(h => h.Zone)) + "/" +
              string.Join(",", ssq.States[2].Matches.Select(m => m.Status)));

        // Jeuno group 8 (the Nomad Moogle's limit breaks and Martial Mastery). The fake answers the new members (MeritCount,
        // SkillLevel, the counted HasExactly) and records SetMerits and AddQuest
        var lb8Fake = new Fake { MeritCount = 5 };
        lb8Fake.WorkingSkills[XI.QuestContent.Skill.GreatAxe] = 357;
        lb8Fake.TradeItems[XI.QuestContent.Item.KindredsSeal] = 5;
        lb8Fake.Player.SetMerits(2);
        lb8Fake.Player.AddQuest(QuestLog.Jeuno, "BEYOND_INFINITY");
        Check("lb8:the fake answers MeritCount / SkillLevel / HasExactly((item, qty)) and records SetMerits / AddQuest",
              "Player.SetMerits(2),Player.AddQuest(Jeuno,BEYOND_INFINITY)/2/357/0/True/False/False",
              $"{string.Join(",", lb8Fake.Calls)}/{lb8Fake.Player.MeritCount}/{lb8Fake.Player.SkillLevel(XI.QuestContent.Skill.GreatAxe)}/" +
              $"{lb8Fake.Player.SkillLevel(XI.QuestContent.Skill.Sword)}/{lb8Fake.Trade.HasExactly((XI.QuestContent.Item.KindredsSeal, 5))}/" +
              $"{lb8Fake.Trade.HasExactly((XI.QuestContent.Item.KindredsSeal, 4))}/" +
              $"{lb8Fake.Trade.HasExactly((XI.QuestContent.Item.KindredsSeal, 5), (XI.QuestContent.Item.KindredsCrest, 1))}");

        var nwaq = new XI.QuestContent.Jeuno.NewWorldsAwait().Compile();
        var ehq = new XI.QuestContent.Jeuno.ExpandingHorizons().Compile();
        var btstq = new XI.QuestContent.Jeuno.BeyondTheStars().Compile();
        var dpdq = new XI.QuestContent.Jeuno.DormantPowersDislodged().Compile();
        var ptpq = new XI.QuestContent.Jeuno.PreludeToPuissance().Compile();
        var mmq = new XI.QuestContent.Jeuno.MartialMastery().Compile();
        Check("lb8:the six clean", "/////",
              string.Join("/", new[] { nwaq, ehq, btstq, dpdq, ptpq, mmq }.Select(def => string.Join(" | ", QuestScriptValidator.Validate(def)))));
        // the Lua-bug arm (not recorded: a script error): the trade indexes the table by itemWanted - 1, so itemWanted 0
        // (accepted by a GM, no draw) or 11 indexes past it; the port's array index throws there as the Lua errors
        string DpdTrade(int itemWanted)
        {
            var f = new Fake { MeritCount = 10 };
            f.Vars["itemWanted"] = itemWanted;
            f.TradeItems[XI.QuestContent.Item.KindredsCrest] = 1;
            f.TradeItems[XI.QuestContent.Item.OrcishPlateArmor] = 1;
            try
            {
                return dpdq.States[1].Handlers.Single(h => h.Kind == QuestHandlerKind.OnTrade).Reply(f)?.Id.ToString() ?? "null";
            }
            catch (IndexOutOfRangeException)
            {
                return "throws";
            }
        }
        Check("lb8:Dormant Powers Dislodged's trade: itemWanted 3 (the Orcish Plate Armor) plays 10191; 2: nil; 0 and 11 throw",
              "10191/null/throws/throws", $"{DpdTrade(3)}/{DpdTrade(2)}/{DpdTrade(0)}/{DpdTrade(11)}");

        // Jeuno group 13 (Mysteries of Beadeaux I and II): the fake records the new member CompleteQuest
        // (player:completeQuest), the way it records AddQuest / DelQuest
        var jg13Fake = new Fake();
        jg13Fake.Player.CompleteQuest(QuestLog.Jeuno, "MYSTERIES_OF_BEADEAUX_II");
        Check("jg13:the fake records Player.CompleteQuest", "Player.CompleteQuest(Jeuno,MYSTERIES_OF_BEADEAUX_II)",
              string.Join(",", jg13Fake.Calls));

        // Jeuno group 14 (The Goblin Tailor, The Road to Aht Urhgan, The Gobbiebag Parts I-X): the fake answers the new
        // members VanadielRseLocation / VanadielRseRace and records IncrementCharVar (the var then moved) and
        // CompleteMission (npcUtil.completeMission: the next mission then current, true answered)
        var jg14Fake = new Fake { RseLocation = 2, RseRace = 7 };
        jg14Fake.CharVars["[RSE]Gear"] = 1;
        jg14Fake.CurrentMissions[XI.QuestContent.MissionLog.Rov] = XI.QuestContent.Mission.Rov.InescapableBinds;
        jg14Fake.Player.IncrementCharVar("[RSE]Gear", 4);
        bool jg14Completed = jg14Fake.Player.CompleteMission(XI.QuestContent.MissionLog.Rov, XI.QuestContent.Mission.Rov.InescapableBinds,
                                                             XI.QuestContent.MissionLog.Rov, XI.QuestContent.Mission.Rov.DesertWinds);
        Check("jg14:the fake answers VanadielRseLocation / VanadielRseRace and records IncrementCharVar / CompleteMission",
              $"Player.IncrementCharVar([RSE]Gear,4),Player.CompleteMission({XI.QuestContent.MissionLog.Rov},{XI.QuestContent.Mission.Rov.InescapableBinds}," +
              $"{XI.QuestContent.MissionLog.Rov},{XI.QuestContent.Mission.Rov.DesertWinds})/2/7/5/True/{XI.QuestContent.Mission.Rov.DesertWinds}",
              $"{string.Join(",", jg14Fake.Calls)}/{jg14Fake.Player.VanadielRseLocation}/{jg14Fake.Player.VanadielRseRace}/" +
              $"{jg14Fake.Player.CharVar("[RSE]Gear")}/{jg14Completed}/{jg14Fake.Player.CurrentMission(XI.QuestContent.MissionLog.Rov)}");
        var jg14Quests = new QuestScript[]
        {
            new XI.QuestContent.Jeuno.TheGobbiebagPartI(), new XI.QuestContent.Jeuno.TheGobbiebagPartIi(),
            new XI.QuestContent.Jeuno.TheGobbiebagPartIii(), new XI.QuestContent.Jeuno.TheGobbiebagPartIv(),
            new XI.QuestContent.Jeuno.TheGobbiebagPartV(), new XI.QuestContent.Jeuno.TheGobbiebagPartVi(),
            new XI.QuestContent.Jeuno.TheGobbiebagPartVii(), new XI.QuestContent.Jeuno.TheGobbiebagPartViii(),
            new XI.QuestContent.Jeuno.TheGobbiebagPartIx(), new XI.QuestContent.Jeuno.TheGobbiebagPartX(),
            new XI.QuestContent.Jeuno.TheGoblinTailor(), new XI.QuestContent.Jeuno.TheRoadToAhtUrhgan(),
        };
        Check("jg14:the twelve clean", "///////////",
              string.Join("/", jg14Quests.Select(q => string.Join(" | ", QuestScriptValidator.Validate(q.Compile())))));
        // the ten parts: one Lua file each (their containers' slots), the template's params in order
        Check("jg14:the ten Gobbiebag parts name their own Lua files and start sizes",
              "The_Gobbiebag_Part_I:30,The_Gobbiebag_Part_II:35,The_Gobbiebag_Part_III:40,The_Gobbiebag_Part_IV:45,The_Gobbiebag_Part_V:50," +
              "The_Gobbiebag_Part_VI:55,The_Gobbiebag_Part_VII:60,The_Gobbiebag_Part_VIII:65,The_Gobbiebag_Part_IX:70,The_Gobbiebag_Part_X:75",
              string.Join(",", jg14Quests.Take(10).Select((q, i) =>
                  $"{q.LegacyPath.Substring("scripts/quests/jeuno/".Length)}:{XI.QuestContent.Jeuno.GobbiebagQuest.Parts[i].StartInventorySize}")));
        // the era module's condition (its false side cannot be a module flow: the harness fails a flow whose module is off)
        var jg14Module = new XI.QuestContent.Modules.Era.Jeuno.TheGobbiebagPart1To10();
        var jg14Off = new Fake();
        var jg14On = new Fake { MainSettings = { ["RESTRICT_CONTENT"] = 1, ["ENABLE_SOA"] = 0 } };
        var jg14Soa = new Fake { MainSettings = { ["RESTRICT_CONTENT"] = 1, ["ENABLE_SOA"] = 1 } };
        Check("jg14:the Gobbiebag era module: its name, its Lua file, its condition xi.pre(SOA)",
              "era_gobbiebag_fame_requirements|modules/era/lua/quests/jeuno/the_gobbiebag_part_1_to_10.lua|False/True/False",
              $"{jg14Module.Name}|{jg14Module.LuaFile}|{jg14Module.Condition(jg14Off)}/{jg14Module.Condition(jg14On)}/{jg14Module.Condition(jg14Soa)}");

        // Jeuno group 9 (Riding on the Clouds, Beyond Infinity)
        var rotcq = new XI.QuestContent.Jeuno.RidingOnTheClouds().Compile();
        var biq = new XI.QuestContent.Jeuno.BeyondInfinity().Compile();
        Check("lb9:the two clean", "/",
              string.Join("/", new[] { rotcq, biq }.Select(def => string.Join(" | ", QuestScriptValidator.Validate(def)))));
        Check("lb9:Riding on the Clouds' accepted state answers 32 nation NPCs' trades, each replying nothing",
              "32/null", $"{rotcq.States[1].Handlers.Count(h => h.Kind == QuestHandlerKind.OnTrade)}/" +
              (rotcq.States[1].Handlers.Where(h => h.Kind == QuestHandlerKind.OnTrade)
                  .Select(h => { var f = new Fake(); f.TradeItems[XI.QuestContent.Item.KindredsSeal] = 1; return h.Reply(f)?.Id.ToString() ?? "null"; })
                  .Distinct().Single()));
        Check("lb9:Beyond Infinity's 32001 handlers in the four Atori-Tutori arenas, accepted",
              $"{XI.QuestContent.Zone.BalgasDais},{XI.QuestContent.Zone.HorlaisPeak},{XI.QuestContent.Zone.QubiaArena},{XI.QuestContent.Zone.WaughroonShrine}/Accepted",
              string.Join(",", biq.States[2].Handlers.Where(h => h.Id == 32001).Select(h => h.Zone)) + "/" +
              string.Join(",", biq.States[2].Matches.Select(m => m.Status)));

        // Jeuno group 10 (Storms of Fate, Shadows of the Departed, Apocalypse Nigh, Empty Memories)
        var sofq = new XI.QuestContent.Jeuno.StormsOfFate().Compile();
        var sotdq = new XI.QuestContent.Jeuno.ShadowsOfTheDeparted().Compile();
        var anq = new XI.QuestContent.Jeuno.ApocalypseNigh().Compile();
        var emq = new XI.QuestContent.Jeuno.EmptyMemories().Compile();
        Check("lb10:the four clean", "///",
              string.Join("/", new[] { sofq, sotdq, anq, emq }.Select(def => string.Join(" | ", QuestScriptValidator.Validate(def)))));
        // Empty Memories' trade walks the reward table in the Lua's pairs() order (Suffering before Pain, Pain before
        // Animosity): the first recollection found decides; a gil one with the wrong gil answers 108 before a later row
        string EmTrade(int gil, params int[] items)
        {
            var f = new Fake { Status = global::XI.Quests.QuestStatus.Accepted };
            foreach (var i in items) f.TradeItems[i] = 1;
            if (gil > 0) f.TradeItems[65535] = gil;
            var r = emq.States[1].Handlers.Single(h => h.Kind == QuestHandlerKind.OnTrade).Reply(f);
            return r == null ? "null" : $"{r.Id}({string.Join(",", r.Args)})" +
                   string.Concat(f.Calls.Where(c => c.StartsWith("Trade.ConfirmItem", StringComparison.Ordinal)).Select(c => " " + c)) +
                   $" reward={f.QuestLocalVar("rewardItem")}";
        }
        Check("lb10:Empty Memories' trade: Suffering + Pain: 110 (the Hamayumi); Pain + Animosity, no gil: 108 (2000); Fear + 2000 gil: 109",
              $"110() Trade.ConfirmItem({XI.QuestContent.Item.RecollectionOfSuffering},1) reward={XI.QuestContent.Item.Hamayumi}|" +
              $"108(2000) reward=0|" +
              $"109() Trade.ConfirmItem({XI.QuestContent.Item.RecollectionOfFear},1) reward={XI.QuestContent.Item.BottleOfPsychoanima}",
              $"{EmTrade(0, XI.QuestContent.Item.RecollectionOfSuffering, XI.QuestContent.Item.RecollectionOfPain)}|" +
              $"{EmTrade(0, XI.QuestContent.Item.RecollectionOfPain, XI.QuestContent.Item.RecollectionOfAnimosity)}|" +
              $"{EmTrade(2000, XI.QuestContent.Item.RecollectionOfFear)}");
        // Apocalypse Nigh's qm1 finish, option 1: the missions rewound and both quests deleted, in the Lua's order
        var anRewind = new Fake { Option = 1, Status = global::XI.Quests.QuestStatus.Completed };
        anq.States[2].Handlers.Single(h => h.Kind == QuestHandlerKind.OnEventFinish && h.Id == 5).Run(anRewind);
        Check("lb10:Apocalypse Nigh's qm1 finish 1: CoP / Zilart rewound to Dawn / Awakening, both quests deleted",
              $"Player.DelMission({XI.QuestContent.MissionLog.Cop},{XI.QuestContent.Mission.Cop.TheLastVerse})," +
              $"Player.DelMission({XI.QuestContent.MissionLog.Zilart},{XI.QuestContent.Mission.Zilart.TheLastVerse})," +
              $"Player.AddMission({XI.QuestContent.MissionLog.Cop},{XI.QuestContent.Mission.Cop.Dawn})," +
              $"Player.AddMission({XI.QuestContent.MissionLog.Zilart},{XI.QuestContent.Mission.Zilart.Awakening})," +
              $"Player.SetMissionStatus({XI.QuestContent.MissionLog.Zilart},3)," +
              $"Player.SetMissionVar({XI.QuestContent.MissionLog.Cop},{XI.QuestContent.Mission.Cop.Dawn},Status,8)," +
              "Player.DelQuest(Jeuno,SHADOWS_OF_THE_DEPARTED),Player.DelQuest(Jeuno,APOCALYPSE_NIGH)",
              string.Join(",", anRewind.Calls));

        // Jeuno group 11 (Ducal Hospitality, A Chocobo's Tale, In the Mood for Love, Hook, Line and Sinker)
        var dhq = new XI.QuestContent.Jeuno.DucalHospitality().Compile();
        var actq = new XI.QuestContent.Jeuno.AChocobosTale().Compile();
        var itmflq = new XI.QuestContent.Jeuno.InTheMoodForLove().Compile();
        var hlsq = new XI.QuestContent.Jeuno.HookLineAndSinker().Compile();
        Check("lb11:the four clean", "///",
              string.Join("/", new[] { dhq, actq, itmflq, hlsq }.Select(def => string.Join(" | ", QuestScriptValidator.Validate(def)))));
        // Ducal Hospitality's accepted trigger indexes questItemSets[ItemSet] with no check: ItemSet 0 (a GM's state only)
        // is a Lua error (questItemSets[0] is nil), which no flow records; the port throws there likewise. The trade
        // checks ItemSet > 0 first, so at ItemSet 0 it answers nil without an error (flowed: jeuno.ducal_hospitality).
        string DhAccepted(int itemSet, bool trade)
        {
            var f = new Fake { Status = global::XI.Quests.QuestStatus.Accepted };
            f.Vars["ItemSet"] = itemSet;
            if (trade)
            {
                f.TradeItems[XI.QuestContent.Item.PileOfChocoboBedding] = 1;
                f.TradeItems[XI.QuestContent.Item.SetOfKaiserinCosmetics] = 1;
                f.TradeItems[XI.QuestContent.Item.MyFirstMagicKit] = 1;
            }
            try
            {
                var r = dhq.States[1].Handlers.Single(h => h.Kind == (trade ? QuestHandlerKind.OnTrade : QuestHandlerKind.OnTrigger)).Reply(f);
                return r == null ? "null" : $"{r.Id}({string.Join(",", r.Options.OrderBy(o => o.Key).Select(o => $"{o.Key}={o.Value}"))})";
            }
            catch (InvalidOperationException)
            {
                return "throws";
            }
        }
        Check("lb11:Ducal Hospitality accepted: ItemSet 0's trigger throws (the Lua error), its trade is nil; ItemSet 2: 10059 / 10058",
              "throws/null/" +
              $"10059(0=0,1={XI.QuestContent.Item.PileOfChocoboBedding},2={XI.QuestContent.Item.SetOfKaiserinCosmetics},3={XI.QuestContent.Item.MyFirstMagicKit},4=0,5=0,6=1)/" +
              "10058(0=0,6=1)",
              $"{DhAccepted(0, false)}/{DhAccepted(0, true)}/{DhAccepted(2, false)}/{DhAccepted(2, true)}");
    }

    // quest rework B2: the authoring API's shape (sections, matches, handlers, replies) and its offline validator
    static void RunQuestScriptGate()
    {
        Console.WriteLine("\n== QuestScript: the authoring API ==");
        var d = new SampleGoldsmith().Compile();
        Check("script:identity and reward", "Bastok/THE_ELVAAN_GOLDSMITH/10/1/180",
              $"{d.Log}/{d.ConstName}/{d.Reward.Fame}/{d.Reward.FameArea}/{d.Reward.Gil}");
        Check("script:two states in declaration order, with their handlers",
              "Available:OnTrigger@235/Michea=ProgressEvent(215),OnEventFinish@235/215 | Accepted,Completed:OnTrade@235/Michea,OnTrigger@235/Michea,OnEventFinish@235/216",
              string.Join(" | ", d.States.Select(s =>
                  string.Join(",", s.Matches.Select(m => m.Status.ToString())) + ":" +
                  string.Join(",", s.Handlers.Select(h => $"{h.Kind}@{h.Zone}/{h.Entity ?? h.Id.ToString()}" + (h.Fixed != null ? $"={h.Fixed.Kind}({h.Fixed.Id})" : ""))))));

        var q = new FakeQuestContext { Status = QuestStatus.Available };
        Check("script:Available matches state 1 only", "True/False",
              $"{d.States[0].IsMatch(QuestStatus.Available, 0, q)}/{d.States[1].IsMatch(QuestStatus.Available, 0, q)}");
        q.Fame[1] = 1;
        Check("script:Completed at fame 1 without mustZone matches state 2", "True", d.States[1].IsMatch(QuestStatus.Completed, 0, q).ToString());
        q.MustZone = true;
        Check("script:  with mustZone it does not", "False", d.States[1].IsMatch(QuestStatus.Completed, 0, q).ToString());

        var trade = d.States[1].Handlers.First(h => h.Kind == QuestHandlerKind.OnTrade);
        q.TradeResult = false;
        Check("script:a wrong trade replies nothing", "null", trade.Reply(q)?.Kind.ToString() ?? "null");
        q.TradeResult = true;
        Check("script:the ingot replies ProgressEvent 216", "ProgressEvent/216", $"{trade.Reply(q).Kind}/{trade.Reply(q).Id}");
        var finish = d.States[1].Handlers.First(h => h.Kind == QuestHandlerKind.OnEventFinish);
        q.Calls.Clear();
        finish.Run(q);
        Check("script:finishing 216 completes, takes the trade, sets mustZone", "Quest.Complete(),Trade.Complete(),Quest.SetMustZone()", string.Join(",", q.Calls));

        var seq = QuestReply.Sequence(QuestReply.MessageSpecial("ITEM_OBTAINED", 640), QuestReply.Event(5, 1, 2));
        Check("script:a sequence keeps its steps and arguments", "Sequence:MessageSpecial(ITEM_OBTAINED;640),Event(5;1,2)",
              $"{seq.Kind}:" + string.Join(",", seq.Steps.Select(r => $"{r.Kind}({r.TextName ?? r.Id.ToString()};{string.Join(",", r.Args)})")));

        Check("script:the sample validates clean", "", string.Join(" | ", QuestScriptValidator.Validate(d)));
        Check("script:the slips are reported", "step 3 is matched but never set | Michea OnTrigger declared twice in one state",
              string.Join(" | ", QuestScriptValidator.Validate(new SloppyQuest().Compile())));

        // NPC-call quests (Called()): the rules are QuestScriptValidator.CallProblems and CallReplyProblem
        string CallProblems(Action<QuestScriptBuilder> build) => string.Join(" | ", QuestScriptValidator.Validate(new BuiltQuest(build).Compile()));
        string Shape(QuestScriptDefinition d) => $"{d.CallOnly}:" + string.Join(",", d.States.SelectMany(st => st.Handlers).Select(h => d.IsCalled(h).ToString()));
        Check("calls:a valid call-only quest (a called trigger and its event finish) validates clean", "",
              CallProblems(b => b.State(QuestStatus.Available).Npc(235, "Umberto").Called().OnTrigger(QuestReply.Event(473)).OnEventFinish(235, 473, q => { })));
        Check("calls:a called trade with a lambda reply and a state-level event finish (a mixed quest) validates clean", "",
              CallProblems(b => b.State(QuestStatus.Available).Npc(235, "Umberto").Called().OnTrade(q => QuestReply.Event(1)).OnEventFinish(235, 473, q => { })));
        Check("calls:a call-only quest with a called mob death, a zone-in, an event update and finish validates clean", "",
              CallProblems(b =>
              {
                  var st = b.State(QuestStatus.Accepted);
                  st.Npc(152, "Yagudo_Avatar").Called().OnMobDeath(q => { });
                  st.OnZoneIn(152, q => QuestReply.Cutscene(46)).OnEventUpdate(152, 46, q => { }).OnEventFinish(152, 46, q => { });
              }));
        var mobZone = new BuiltQuest(b => b.State(QuestStatus.Accepted).Npc(152, "Yagudo_Avatar").Called().OnMobDeath(q => { }).OnZoneIn(152, q => null)).Compile();
        Check("calls:  it is call-only, the zone-in called", "True:True,True", $"{mobZone.CallOnly}:" + string.Join(",", mobZone.States[0].Handlers.Select(h => mobZone.IsCalled(h).ToString())));
        Check("calls:a state-level trigger area in a call-only quest is refused",
              "OnTriggerAreaEnter 3: a called handler is OnTrigger, OnTrade or OnMobDeath on an entity, OnEventFinish, OnEventUpdate or OnZoneIn state-level",
              CallProblems(b => b.State(QuestStatus.Available).Npc(235, "Umberto").Called().OnTrigger(QuestReply.Event(1)).OnTriggerAreaEnter(235, 3, q => { })));
        Check("calls:mixed, a state-level zone-in is a container handler", "False:True,False,False",
              Shape(new BuiltQuest(b => { b.State(QuestStatus.Available).Npc(235, "Umberto").Called().OnMobDeath(q => { }); b.State(QuestStatus.Accepted).Npc(235, "Samia").OnTrigger(QuestReply.Event(2)).OnZoneIn(235, q => null); }).Compile()));
        Check("calls:a step match in a call-only quest is refused", "state 1: an NPC-call quest cannot match a step",
              CallProblems(b => { b.Steps(2); b.State(QuestStatus.Accepted, step: 2).Npc(235, "Umberto").Called().OnTrigger(QuestReply.Event(1)); }));
        Check("calls:a fixed reply the renderer can't build (KeyItem) is refused", "Umberto OnTrigger: a KeyItem reply is not allowed in an NPC call",
              CallProblems(b => b.State(QuestStatus.Available).Npc(235, "Umberto").Called().OnTrigger(QuestReply.KeyItem(1))));
        Check("calls:a Sequence reply is refused too", "Umberto OnTrigger: a Sequence reply is not allowed in an NPC call",
              CallProblems(b => b.State(QuestStatus.Available).Npc(235, "Umberto").Called().OnTrigger(QuestReply.Sequence(QuestReply.MessageText("A")))));
        Check("calls:one NPC with Called() and container handlers is refused", "Umberto in zone 235 has both Called() and container handlers",
              CallProblems(b =>
              {
                  b.State(QuestStatus.Available).Npc(235, "Umberto").Called().OnTrigger(QuestReply.Event(1));
                  b.State(QuestStatus.Accepted).Npc(235, "Umberto").OnTrigger(QuestReply.Event(2));
              }));
        Check("calls:the other NPC of a mixed quest is fine", "",
              CallProblems(b => b.State(QuestStatus.Available).Npc(235, "Umberto").Called().OnTrigger(QuestReply.Event(1)).Npc(235, "Samia").OnTrigger(QuestReply.Event(2))));
        var evDef = new BuiltQuest(b => b.State(QuestStatus.Available).Npc(235, "Umberto").Called().OnTrigger(QuestReply.Event(1)).OnEventFinish(235, 1, q => { })
                                          .OnEventFinish(235, 2, q => { })).Compile();
        Check("calls:event finishes of a call-only quest are all called", "True,True,True/True",
              string.Join(",", evDef.States[0].Handlers.Select(h => evDef.IsCalled(h).ToString())) + "/" + evDef.CallOnly);
        var shapeOnly = new BuiltQuest(b => b.State(QuestStatus.Available).Npc(235, "Umberto").Called().OnTrigger(QuestReply.Event(1)).OnEventFinish(235, 1, q => { })).Compile();
        var shapeState = new BuiltQuest(b => b.State(QuestStatus.Available).Npc(235, "Umberto").Called().OnTrigger(QuestReply.Event(1)))
            .Compile();
        var shapeStateEv = new BuiltQuest(b => { var st = b.State(QuestStatus.Available); st.Npc(235, "Umberto").Called().OnTrigger(QuestReply.Event(1)); st.OnEventFinish(235, 2, q => { }); }).Compile();
        var shapeMixed = new BuiltQuest(b => { var st = b.State(QuestStatus.Available); st.Npc(235, "Umberto").Called().OnTrigger(QuestReply.Event(1)); st.Npc(235, "Samia").OnTrigger(QuestReply.Event(2)); st.OnEventFinish(235, 2, q => { }); }).Compile();

        Check("calls:call-only, chained event finish: call-only, all called", "True:True,True", Shape(shapeOnly));
        Check("calls:call-only, a state-level event finish: still call-only, the event finish is called", "True:True,True", Shape(shapeStateEv));
        Check("calls:only a called trigger: call-only", "True:True", Shape(shapeState));
        Check("calls:mixed, a state-level event finish: a container handler", "False:True,False,False", Shape(shapeMixed));
        Check("calls:mixed, a state-level event finish in a state with Called() NPCs is refused",
              "state 1: event handlers in a state with Called() NPC handlers must be chained from the Called() NPC builder, or move it to its own state",
              CallProblems(b => { var st = b.State(QuestStatus.Available); st.Npc(235, "Umberto").Called().OnTrigger(QuestReply.Event(1)); st.Npc(235, "Samia").OnTrigger(QuestReply.Event(2)); st.OnEventFinish(235, 2, q => { }); }));
        Check("calls:mixed, an event update after a chained Called() event finish is refused too",
              "state 1: event handlers in a state with Called() NPC handlers must be chained from the Called() NPC builder, or move it to its own state",
              CallProblems(b => { var st = b.State(QuestStatus.Available); st.Npc(235, "Umberto").Called().OnTrigger(QuestReply.Event(1)).OnEventFinish(235, 1, q => { }).OnEventUpdate(235, 1, q => { }); st.Npc(235, "Samia").OnTrigger(QuestReply.Event(2)); }));
        Check("calls:mixed, a state-level event update in a state with a Called() mob death is still refused",
              "state 1: event handlers in a state with Called() NPC handlers must be chained from the Called() NPC builder, or move it to its own state",
              CallProblems(b => { var st = b.State(QuestStatus.Available); st.Npc(152, "Yagudo_Avatar").Called().OnMobDeath(q => { }); st.Npc(152, "Stone_Lid").OnTrigger(QuestReply.Event(2)); st.OnEventUpdate(152, 2, q => { }); }));
        Check("calls:mixed, the event finish chained from the Called() builder is fine", "",
              CallProblems(b => { var st = b.State(QuestStatus.Available); st.Npc(235, "Umberto").Called().OnTrigger(QuestReply.Event(1)).OnEventFinish(235, 1, q => { }); st.Npc(235, "Samia").OnTrigger(QuestReply.Event(2)); }));
        Check("calls:mixed, a state-level event finish in another state (no Called() NPC there) is fine", "",
              CallProblems(b => { b.State(QuestStatus.Available).Npc(235, "Umberto").Called().OnTrigger(QuestReply.Event(1)); var st = b.State(QuestStatus.Accepted); st.Npc(235, "Samia").OnTrigger(QuestReply.Event(2)); st.OnEventFinish(235, 2, q => { }); }));
        Check("calls:CallReplyProblem allows an Event and refuses a KeyItem", "/a KeyItem reply is not allowed in an NPC call",
              QuestScriptValidator.CallReplyProblem(QuestReply.Event(1)) + "/" + QuestScriptValidator.CallReplyProblem(QuestReply.KeyItem(1)));
    }

    sealed class BuiltQuest : QuestScript
    {
        readonly Action<QuestScriptBuilder> _build;
        public BuiltQuest(Action<QuestScriptBuilder> build) : base(QuestLog.Bastok, "X") { _build = build; }
        protected override void Build(QuestScriptBuilder b) => _build(b);
    }

    // quest rework B2: the generated ids agree with the Lua enums they come from (spot checks against scripts/enum)
    static void RunContentIdsGate()
    {
        Console.WriteLine("\n== XI.QuestContent ids == scripts/enum ==");
        Check("ids:Zone.BastokMarkets", "235", XI.QuestContent.Zone.BastokMarkets.ToString());
        Check("ids:Zone.Metalworks", "237", XI.QuestContent.Zone.Metalworks.ToString());
        Check("ids:Item.CopperIngot", "648", XI.QuestContent.Item.CopperIngot.ToString());
        Check("ids:FameArea.Bastok", "1", XI.QuestContent.FameArea.Bastok.ToString());
        Check("ids:Title.BastokWelcomingCommittee", "81", XI.QuestContent.Title.BastokWelcomingCommittee.ToString());
        Check("ids:Slot.Sub", "1", XI.QuestContent.Slot.Sub.ToString());
        Check("ids:Skill.GreatAxe / Marksmanship (xi.skill)", "6/26", $"{XI.QuestContent.Skill.GreatAxe}/{XI.QuestContent.Skill.Marksmanship}");
        string root = null;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null && root == null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "scripts", "enum", "item.lua"))) root = dir.FullName;
        var m = Regex.Match(File.ReadAllText(Path.Combine(root, "scripts", "enum", "key_item.codegen.lua")), @"\bDAWN_TALISMAN\s*=\s*(\d+)");
        Check("ids:KeyItem.DawnTalisman == the Lua value", m.Groups[1].Value, XI.QuestContent.KeyItem.DawnTalisman.ToString());
        // carry-quests-1: the generated Nation, WsUnlock, Weather, Time, Spell and Battlefield (xi.battlefield.id, from
        // scripts/globals/battlefield.lua), and the hand-written Utils (scripts/utils/utils.lua)
        Check("ids:Nation.Windurst / WsUnlock.Retribution / Weather.None / Time.Night (xi.nation, xi.wsUnlock, ...)",
              "2/12/0/7", $"{XI.QuestContent.Nation.Windurst}/{XI.QuestContent.WsUnlock.Retribution}/" +
                         $"{XI.QuestContent.Weather.None}/{XI.QuestContent.Time.Night}");
        Check("ids:Spell.Titan / Spell.ZeidIi / Battlefield.MoonlitPath (xi.magic.spell, xi.battlefield.id)",
              "299/1010/224", $"{XI.QuestContent.Spell.Titan}/{XI.QuestContent.Spell.ZeidIi}/" +
                              $"{XI.QuestContent.Battlefield.MoonlitPath}");
        var cancelled = Regex.Match(File.ReadAllText(Path.Combine(root, "scripts", "utils", "utils.lua")),
                                    @"utils\.EVENT_CANCELLED_OPTION\s*=\s*bit\.lshift\(1,\s*(\d+)\)");
        Check("ids:Utils.EventCancelledOption == utils.EVENT_CANCELLED_OPTION (bit.lshift(1, n))",
              cancelled.Success ? (1 << int.Parse(cancelled.Groups[1].Value)).ToString() : "not found",
              XI.QuestContent.Utils.EventCancelledOption.ToString());
        // the generator gate (python, ~40 s): skipped by a filter none of whose prefixes could name a gen: check; an
        // unfiltered run checks that every check it makes is named so (else skipping it could hide a matching check)
        if (OnlySkips(GeneratorPrefix)) return;
        named_ = new List<string>();
        RunGeneratorGate(root);
        var names = named_;
        named_ = null;
        var stray = names.Where(n => !n.StartsWith(GeneratorPrefix, StringComparison.Ordinal)).ToList();
        Check($"only:every check of the generator gate is named {GeneratorPrefix}... (so --only skips it safely)", $"{names.Count} named {GeneratorPrefix}",
              stray.Count == 0 ? $"{names.Count} named {GeneratorPrefix}" : "not: " + string.Join(" | ", stray));
    }

    // tools/gen_content_ids.py: block comments and long strings, the removed-constant check, --check against the files
    static void RunGeneratorGate(string root)
    {
        Console.WriteLine("\n== tools/gen_content_ids.py == --check, long strings, removed constants ==");
        string tool = Path.Combine(root, "tools", "gen_content_ids.py");
        (int Code, string Output) Py(params string[] args)
        {
            var psi = new System.Diagnostics.ProcessStartInfo("python") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
            psi.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
            foreach (string a in args) psi.ArgumentList.Add(a);
            using var p = System.Diagnostics.Process.Start(psi);
            var err = p.StandardError.ReadToEndAsync();
            var output = p.StandardOutput.ReadToEndAsync();
            if (!p.WaitForExit(120000))
            {
                p.Kill(true);
                Bad($"gen: python {string.Join(" ", args)}: no exit within 120 s");
                return (-1, "timed out");
            }
            return (p.ExitCode, output.Result + err.Result);
        }
        try
        {
            if (Py("--version").Code != 0) throw new System.ComponentModel.Win32Exception();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Console.WriteLine("  [note] python is not on PATH: the generator checks are skipped");
            return;
        }

        var clean = Py(tool, "--check");
        Check("gen:--check on the committed files", "0", clean.Code.ToString());
        if (clean.Code != 0) Console.WriteLine(clean.Output);

        string tmp = Path.Combine(Path.GetTempPath(), "xi-gen-ids-" + Environment.ProcessId);
        Directory.CreateDirectory(tmp);
        try
        {
            // a block comment and a long string in a table: no entry from either, and their braces don't close the table
            string sample = Path.Combine(tmp, "sample.lua");
            File.WriteAllText(sample, "xi.item =\n{\n    REAL = 1,\n--[[\n    IN_COMMENT = 2,\n}\n]]\n    s = [==[\n    IN_STRING = 3, }\n]==],\n" +
                                      "    --[=[ one line ]=] AFTER = 4,\n    LAST = 5,\n}\n");
            string zone = Path.Combine(tmp, "IDs.lua");
            File.WriteAllText(zone, "zones = zones or {}\nzones[xi.zone.BASTOK_MARKETS] =\n{\n    mob =\n    {\n        REAL_MOB = 1,\n" +
                                    "        --[[\n        OLD_MOB = 2,\n        ]]\n    },\n}\n");
            var parsed = Py("-c", "import sys; sys.path.insert(0, sys.argv[1]); import gen_content_ids as g; " +
                                  "print(g.parse(sys.argv[2], 'xi.item')); print(g.zone_sections(sys.argv[3])[1]['mob'])",
                            Path.Combine(root, "tools"), sample, zone);
            Check("gen:long strings and block comments", "[('REAL', 1), ('AFTER', 4), ('LAST', 5)]\n[('REAL_MOB', 1)]",
                  parsed.Output.Replace("\r\n", "\n").Trim());

            string ids = Path.Combine(root, "src", "XI.QuestContent", "Ids");
            string copy = Path.Combine(tmp, "Ids");
            Directory.CreateDirectory(copy);
            foreach (string f in Directory.GetFiles(ids, "*.g.cs")) File.Copy(f, Path.Combine(copy, Path.GetFileName(f)));
            Check("gen:--check --out <copy>", "0", Py(tool, "--check", "--out", copy).Code.ToString());

            // a hand-edited value: --check names the file
            string slot = Path.Combine(copy, "Slot.g.cs");
            string slotText = File.ReadAllText(slot);
            File.WriteAllText(slot, slotText.Replace("public const int Sub = 1;", "public const int Sub = 7;"));
            var edited = Py(tool, "--check", "--out", copy);
            Check("gen:--check on a hand-edited file", "1 True", edited.Code + " " + edited.Output.Contains("Slot.g.cs"));
            File.WriteAllText(slot, slotText);

            // a constant the Lua no longer produces: removed, the run fails, the file is not rewritten
            string job = Path.Combine(copy, "Job.g.cs");
            string jobText = File.ReadAllText(job);
            string withGone = jobText.Replace("    public static class Job\n    {\n", "    public static class Job\n    {\n        public const int Gone = 99;\n");
            File.WriteAllText(job, withGone);
            var removed = Py(tool, "--out", copy);
            Check("gen:a removed constant fails", "1 True", removed.Code + " " + removed.Output.Contains("removed: Job.Gone"));
            Check("gen:a removed constant: nothing written", "True", (File.ReadAllText(job) == withGone).ToString());
            var removedCheck = Py(tool, "--check", "--out", copy);
            Check("gen:--check reports a removed constant", "1 True", removedCheck.Code + " " + removedCheck.Output.Contains("removed: Job.Gone"));
            File.WriteAllText(job, jobText);

            RunGeneratorYamlGate(root, tool, tmp, copy, Py);
        }
        finally
        {
            // the junctions of the yaml mirror go first, without following them into data/zones
            string mirror = Path.Combine(tmp, "data");
            if (Directory.Exists(mirror))
                foreach (string dir in Directory.GetDirectories(mirror))
                    if (new DirectoryInfo(dir).Attributes.HasFlag(FileAttributes.ReparsePoint)) Directory.Delete(dir, false);
            Directory.Delete(tmp, true);
        }
    }

    // quest rework B3b start: Npc constants from data/zones/*/{npcs,mobs}.yaml, on a copy of the yaml (--data) and of the
    // .g.cs files (--out): the ScanEntityScripts reading, a script and a template-only entity, skipped names, a collision,
    // an entity leaving the yaml
    static void RunGeneratorYamlGate(string root, string tool, string tmp, string copy, Func<string[], (int Code, string Output)> py)
    {
        (int Code, string Output) Py(params string[] args) => py(args);

        // the reader: ids at two spaces, script ?? template at four, quotes and a " #" comment dropped, other sections
        // and non-id keys ignored
        string sample = Path.Combine(tmp, "sample.yaml");
        File.WriteAllText(sample, "templates:\n  Tmpl:\n    script: NotASpawn\nspawns:\n  100:\n    template: Only_Template\n" +
                                  "  101:\n    template: Tmpl_Name\n    script:   'Quoted Script' # comment\n  key:\n    script: NotAnId\n" +
                                  "  102:\n    at: [1, 2, 3]\n  103:\n    script: \"Last\"\n");
        var read = Py("-c", "import sys; sys.path.insert(0, sys.argv[1]); import gen_content_ids as g; " +
                            "print(g.yaml_entity_names(sys.argv[2], 'spawns')); print([g.skip_entity(n) for n in ('???', 'HomePoint#1', '2', 'NPC[1]')])",
                      Path.Combine(root, "tools"), sample);
        Check("gen:yaml names (script ?? template, ScanEntityScripts rules)", "['Only_Template', 'Quoted Script', 'Last']\n[True, True, True, False]",
              read.Output.Replace("\r\n", "\n").Trim());
        // an override's Lua key may hold a space when quoted (a yaml name such as "Gloom Phantom")
        var overrides = Py("-c", "import sys; sys.path.insert(0, sys.argv[1]); import gen_content_ids as g; " +
                                 "print([g.override_parts(l) for l in sys.argv[2:]])",
                           Path.Combine(root, "tools"), "Npc.A b_c Bc  # why", "  # a comment", "",
                           "Npc.A \"Gloom Phantom\" GloomPhantomSpaced", "Npc.A \"open C");
        Check("gen:override lines (a quoted key may hold a space; an unclosed quote is one bad field)",
              "[['Npc.A', 'b_c', 'Bc'], None, None, ['Npc.A', 'Gloom Phantom', 'GloomPhantomSpaced'], " +
              "['Npc.A \"open C']]",
              overrides.Output.Replace("\r\n", "\n").Trim());

        // every zone folder is a junction to the real one (no copy of the 80 MB of yaml); the two folders the test
        // mutates are real copies
        string data = Path.Combine(tmp, "data");
        Directory.CreateDirectory(data);
        string[] mutated = { "bastok_markets", "north_gustaberg" };
        var links = new System.Text.StringBuilder("@echo off\r\n");
        foreach (string dir in Directory.GetDirectories(Path.Combine(root, "data", "zones")))
        {
            string name = Path.GetFileName(dir), target = Path.Combine(data, name);
            if (Array.IndexOf(mutated, name) >= 0)
            {
                Directory.CreateDirectory(target);
                foreach (string f in new[] { "npcs.yaml", "mobs.yaml" })
                    if (File.Exists(Path.Combine(dir, f))) File.Copy(Path.Combine(dir, f), Path.Combine(target, f));
            }
            // a junction that fails stops the script with its exit code (else the gate would go on with a folder
            // missing and fail on something else)
            else links.Append("mklink /J \"" + target + "\" \"" + dir + "\" >nul || exit /b 1\r\n");
        }
        string bat = Path.Combine(tmp, "links.bat");
        File.WriteAllText(bat, links.ToString());
        var linkStart = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c \"" + bat + "\"")
        {
            CreateNoWindow = true, UseShellExecute = false,
        };
        using (var cmd = System.Diagnostics.Process.Start(linkStart)!)
        {
            cmd.WaitForExit();
            if (cmd.ExitCode != 0)
            {
                Bad($"gen:yaml the zone folders' junctions (mklink /J into {data}) failed, exit code {cmd.ExitCode}");
                return;
            }
        }
        string npcsYaml = Path.Combine(data, "bastok_markets", "npcs.yaml");
        string npcsText = File.ReadAllText(npcsYaml);
        string mobsYaml = Path.Combine(data, "north_gustaberg", "mobs.yaml");
        string mobsText = File.ReadAllText(mobsYaml);
        void Entities(string path, string text, string section, string entries) =>
            File.WriteAllText(path, text.Replace("\n" + section + ":\n", "\n" + section + ":\n" + entries));
        string npc = Path.Combine(copy, "Npc.g.cs");
        string ZoneClass(string zone)
        {
            string text = File.ReadAllText(npc);
            int at = text.IndexOf("        public static class " + zone + "\n", StringComparison.Ordinal);
            return at < 0 ? "" : text.Substring(at, text.IndexOf("        }\n", at, StringComparison.Ordinal) - at);
        }

        // a scripted NPC and a template-only mob appear; ???, HomePoint#9 and 2 don't
        Entities(npcsYaml, npcsText, "npcs", "  99999001:\n    script: Zz_Script_Npc\n  99999002:\n    script: '???'\n" +
                                             "  99999003:\n    script: HomePoint#9\n  99999004:\n    script: 2\n");
        Entities(mobsYaml, mobsText, "spawns", "  99999005:\n    template: Zz_Template_Mob\n    at: [1, 2, 3]\n");
        var added = Py(tool, "--out", copy, "--data", data);
        Check("gen:yaml entities: the run succeeds", "0", added.Code.ToString());
        if (added.Code != 0) Console.WriteLine(added.Output);
        string bastok = ZoneClass("BastokMarkets");
        Check("gen:yaml NPC with a script", "True", bastok.Contains("public const string ZzScriptNpc = \"Zz_Script_Npc\";").ToString());
        Check("gen:yaml mob with only a template", "True",
              ZoneClass("NorthGustaberg").Contains("public const string ZzTemplateMob = \"Zz_Template_Mob\";").ToString());
        Check("gen:skipped yaml names (???, HomePoint#9, 2)", "False False False",
              bastok.Contains("\"???\"") + " " + bastok.Contains("HomePoint#9") + " " + bastok.Contains("= \"2\""));
        Check("gen:--check clean after a regenerate", "0", Py(tool, "--check", "--out", copy, "--data", data).Code.ToString());

        // the NPC leaves the yaml: removed, the run fails, nothing written
        Entities(npcsYaml, npcsText, "npcs", "");
        string before = File.ReadAllText(npc);
        var gone = Py(tool, "--out", copy, "--data", data);
        Check("gen:a yaml entity removed fails", "1 True", gone.Code + " " + gone.Output.Contains("removed: Npc.BastokMarkets.ZzScriptNpc"));
        Check("gen:a yaml entity removed: nothing written", "True", (File.ReadAllText(npc) == before).ToString());

        // two names giving one identifier in one zone: the collision message
        Entities(npcsYaml, npcsText, "npcs", "  99999001:\n    script: Zz_Script_Npc\n  99999006:\n    script: Zz Pair\n  99999007:\n    script: Zz_Pair\n");
        var collision = Py(tool, "--out", copy, "--data", data);
        Check("gen:yaml names colliding in one zone fail", "1 True", collision.Code + " " +
              collision.Output.Contains("collision in Npc.BastokMarkets: ZzPair from Zz Pair = Zz Pair and Zz_Pair = Zz_Pair; add an override line"));

        // a name with no ASCII letter or digit has no identifier: the run fails naming the zone and the name
        Entities(npcsYaml, npcsText, "npcs", "  99999008:\n    script: \u00c9\u00e9\n");
        var noIdent = Py(tool, "--out", copy, "--data", data);
        Check("gen:a yaml name with an empty identifier fails", "1 True", noIdent.Code + " " +
              noIdent.Output.Contains("no identifier for Npc.BastokMarkets entity '\\xc9\\xe9' ("));
    }

    // quest rework B1: ReplaceAll / InsertQuests / InsertMissions / Rekey batch at most 200 rows a statement and bind,
    // row for row, exactly what the single-row upsert binds
    static void RunStoreBatchGate()
    {
        Console.WriteLine("\n== QuestStateStore: batched inserts ==");
        var qb = new QuestBitBlob();
        for (int id = 0; id < 250; id++) qb.AddQuest(4, (ushort)id);
        for (int id = 0; id < 200; id++) qb.AddQuest(5, (ushort)id);
        var p = CharProgress.Import(42, qb.Bytes, new MissionBitBlob().Bytes, new[] { new CharVar("Quest[4][1]mustZone", 230, 1900000000) });
        var db = new RecordingDb();
        Check("batch:ReplaceAll succeeds", "True", QuestStateStore.ReplaceAll(db, p).ToString());
        // 5 deletes, 450 quest rows in 3 statements (200, 200, 50), the 15 mission rows in 1, the assault and campaign rows
        Check("batch:statements: 5 deletes, 3 quest inserts, 1 mission insert, assault, campaign",
              "DELETE,DELETE,DELETE,DELETE,DELETE,INSERT q 200,INSERT q 200,INSERT q 50,INSERT m 15,INSERT a,INSERT c",
              string.Join(",", db.Ran.Select(r => r.Sql.StartsWith("DELETE") ? "DELETE"
                  : r.Sql.StartsWith("INSERT INTO char_quest_state") ? $"INSERT q {r.Args.Length / 8}"
                  : r.Sql.StartsWith("INSERT INTO char_mission_state") ? $"INSERT m {r.Args.Length / 6}"
                  : r.Sql.StartsWith("INSERT INTO char_assault_state") ? "INSERT a" : "INSERT c")));
        Check("batch:placeholders == bound values in every statement", "True",
              db.Ran.All(r => Placeholders(r.Sql) == r.Args.Length).ToString());
        Check("batch:no ON DUPLICATE in the batched inserts", "True", db.Ran.All(r => !r.Sql.Contains("ON DUPLICATE")).ToString());

        // each quest row binds what Upsert binds for it, in the rows' order
        var single = new RecordingDb();
        foreach (var r in p.Quests.Values) QuestStateStore.Upsert(single, r);
        var batched = db.Ran.Where(r => r.Sql.StartsWith("INSERT INTO char_quest_state")).SelectMany(r => r.Args).ToArray();
        Check("batch:quest values == the single-row upserts'", ArgsText(single.Ran.SelectMany(r => r.Args).ToArray()), ArgsText(batched));
        Check("batch:the JSON goes through NULLIF like the upsert", "True",
              db.Ran.Where(r => r.Sql.StartsWith("INSERT INTO char_quest_state")).All(r => r.Sql.Contains("NULLIF(?, '')")).ToString());
        var singleM = new RecordingDb();
        foreach (var m in p.Missions) QuestStateStore.Upsert(singleM, m);
        Check("batch:mission values == the single-row upserts'", ArgsText(singleM.Ran.SelectMany(r => r.Args).ToArray()),
              ArgsText(db.Ran.Last(r => r.Sql.StartsWith("INSERT INTO char_mission_state")).Args));

        var none = new RecordingDb();
        Check("batch:InsertQuests of nothing runs nothing", "True/0", $"{QuestStateStore.InsertQuests(none, new CharQuestStateRow[0])}/{none.Ran.Count}");

        // Rekey: the changed rows deleted by wire in one statement, then one insert
        var rk = new RecordingDb();
        var rows = p.Quests.Values.Take(3).ToList();
        Check("batch:Rekey succeeds", "True", QuestStateStore.Rekey(rk, rows).ToString());
        Check("batch:Rekey deletes the rows by wire in one statement, then inserts them", "DELETE 7,UInt32:42,UInt8:4,UInt16:0,UInt8:4,UInt16:1,UInt8:4,UInt16:2/INSERT 3",
              string.Join("/", rk.Ran.Select(r => r.Sql.StartsWith("DELETE") ? $"DELETE {r.Args.Length},{ArgsText(r.Args)}" : $"INSERT {r.Args.Length / 8}")));
        var rk2 = new RecordingDb();
        Check("batch:Rekey of 450 rows succeeds", "True", QuestStateStore.Rekey(rk2, p.Quests.Values.ToList()).ToString());
        Check("batch:  3 DELETE statements (200, 200, 50 rows) and 3 INSERTs", "D 401,D 401,D 101,I 200,I 200,I 50",
              string.Join(",", rk2.Ran.Select(r => r.Sql.StartsWith("DELETE") ? $"D {r.Args.Length}" : $"I {r.Args.Length / 8}")));
        Check("batch:  placeholders == bound values in each", "True", rk2.Ran.All(r => Placeholders(r.Sql) == r.Args.Length).ToString());
    }

    // quest rework B1: stable keys from xi.quest.id (read here from scripts/globals/quests.lua), an injective registry,
    // keys on imported and new rows, Rekey, and Diff ignoring keys
    static void RunQuestKeysGate()
    {
        Console.WriteLine("\n== quest keys: the registry, keyed rows, Rekey ==");
        string questsLua = null;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null && questsLua == null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "scripts", "globals", "quests.lua"))) questsLua = Path.Combine(d.FullName, "scripts", "globals", "quests.lua");
        if (questsLua == null) { Bad("keys: scripts/globals/quests.lua not found above " + AppContext.BaseDirectory); return; }

        // xi.quest.area: [xi.questLog.BASTOK] = 'bastok'; xi.quest.id: [xi.quest.area[xi.questLog.BASTOK]] = { NAME = id, ... }
        var logNumbers = new Dictionary<string, int> { ["SANDORIA"] = 0, ["BASTOK"] = 1, ["WINDURST"] = 2, ["JEUNO"] = 3, ["OTHER_AREAS"] = 4, ["OUTLANDS"] = 5,
                                                       ["AHT_URHGAN"] = 6, ["CRYSTAL_WAR"] = 7, ["ABYSSEA"] = 8, ["ADOULIN"] = 9, ["COALITION"] = 10 };
        var areaNames = new Dictionary<string, string>();
        string current = null;
        var reg = new QuestKeyRegistry();
        int entries = 0, refused = 0, longest = 0;
        foreach (var line in File.ReadAllLines(questsLua))
        {
            var an = Regex.Match(line, @"^\s*\[xi\.questLog\.(\w+)\]\s*=\s*'(\w+)'");
            if (an.Success) { areaNames[an.Groups[1].Value] = an.Groups[2].Value; continue; }
            var head = Regex.Match(line, @"^    \[xi\.quest\.area\[xi\.questLog\.(\w+)\]\]\s*=");
            if (head.Success) { current = head.Groups[1].Value; continue; }
            if (line.StartsWith("}")) { if (entries > 0) current = null; continue; }
            var e = Regex.Match(line, @"^        (\w+)\s*=\s*(\d+)");
            if (!e.Success || current == null) continue;
            entries++;
            string key = QuestKeyRegistry.KeyFor(areaNames[current], e.Groups[1].Value);
            longest = Math.Max(longest, key.Length);
            if (!reg.Register(key, new QuestWireRef((QuestLog)logNumbers[current], ushort.Parse(e.Groups[2].Value)))) refused++;
        }
        Check("keys:xi.quest.id entries, all registered", "1084/0/1084", $"{entries}/{refused}/{reg.Count}");
        Check("keys:the longest key fits varchar(96)", "True", (longest <= 96).ToString());
        Check("keys:1/13 is bastok.the_elvaan_goldsmith", "bastok.the_elvaan_goldsmith", reg.KeyOrSynthetic(new QuestWireRef(QuestLog.Bastok, 13)));
        Check("keys:an otherAreas key keeps the Lua spelling", "otherAreas.rycharde_the_chef", reg.KeyOrSynthetic(new QuestWireRef(QuestLog.OtherAreas, 0)));
        Check("keys:an id outside xi.quest.id stays synthetic", "legacy:1/200", reg.KeyOrSynthetic(new QuestWireRef(QuestLog.Bastok, 200)));

        var small = new QuestKeyRegistry();
        Check("keys:Register a new key", "True", small.Register("a.one", new QuestWireRef(QuestLog.Bastok, 1)).ToString());
        Check("keys:Register refuses a taken key", "False", small.Register("a.one", new QuestWireRef(QuestLog.Bastok, 2)).ToString());
        Check("keys:Register refuses a taken wire", "False", small.Register("a.two", new QuestWireRef(QuestLog.Bastok, 1)).ToString());
        Check("keys:  the first registration kept", "a.one/1/legacy:1/2", $"{small.KeyOrSynthetic(new QuestWireRef(QuestLog.Bastok, 1))}/{small.Count}/{small.KeyOrSynthetic(new QuestWireRef(QuestLog.Bastok, 2))}");

        // Import with the registry: real keys, unknown wires synthetic
        var qb = new QuestBitBlob();
        qb.AddQuest(1, 13); qb.AddQuest(1, 200);
        var p = CharProgress.Import(42, qb.Bytes, new MissionBitBlob().Bytes, new[] { new CharVar("Quest[1][4]Prog", 2) }, reg);
        Check("keys:Import keys the rows through the registry", "bastok.the_quadavs_curse,bastok.the_elvaan_goldsmith,legacy:1/200",
              string.Join(",", p.Quests.Values.OrderBy(r => r.QuestId).Select(r => r.QuestKey)));
        Check("keys:Import without a registry stays synthetic", "legacy:1/13",
              CharProgress.Import(42, qb.Bytes, new MissionBitBlob().Bytes, null).Quests[new QuestWireRef(QuestLog.Bastok, 13)].QuestKey);

        // a row created later (SyncQuests, SetVar) takes the registry's key
        var f = CharProgress.FromRows(42, new CharQuestStateRow[0], new CharMissionStateRow[0], reg);
        var qb2 = new QuestBitBlob();
        qb2.AddQuest(1, 18);
        f.SyncQuests(qb2.Bytes);
        f.SetVar("Quest[1][19]Prog", 1, 0);
        Check("keys:new rows (SyncQuests, SetVar) take the registry's key", "bastok.till_death_do_us_part,bastok.fallen_comrades",
              string.Join(",", f.Quests.Values.OrderBy(r => r.QuestId).Select(r => r.QuestKey)));

        // Rekey: a synthetic key, a swap of two names, a real key whose constant is gone, a row already right
        CharQuestStateRow Row(ushort id, string key) => new CharQuestStateRow { CharId = 42, QuestKey = key, Area = QuestLog.Bastok, QuestId = id, Status = QuestStatus.Accepted };
        var loaded = CharProgress.FromRows(42, new[]
        {
            Row(13, "legacy:1/13"), Row(4, "bastok.out_of_ones_shell"), Row(5, "bastok.the_quadavs_curse"), Row(200, "bastok.gone"), Row(18, "bastok.till_death_do_us_part"),
        }, new CharMissionStateRow[0], reg);
        var changed = loaded.Rekey();
        Check("keys:Rekey returns the changed rows, (area, id) order, keys set",
              "1/4=bastok.the_quadavs_curse,1/5=bastok.out_of_ones_shell,1/13=bastok.the_elvaan_goldsmith,1/200=legacy:1/200",
              string.Join(",", changed.Select(r => $"{(int)r.Area}/{r.QuestId}={r.QuestKey}")));
        Check("keys:  a second Rekey changes nothing", "0", loaded.Rekey().Count.ToString());
        Check("keys:Rekey without a registry: everything synthetic", "1/18=legacy:1/18",
              string.Join(",", CharProgress.FromRows(42, new[] { Row(18, "bastok.till_death_do_us_part") }, new CharMissionStateRow[0]).Rekey().Select(r => $"{(int)r.Area}/{r.QuestId}={r.QuestKey}")));

        // Diff compares projections and vars, never keys
        var keyed = CharProgress.Import(42, qb.Bytes, new MissionBitBlob().Bytes, null, reg);
        var synth = CharProgress.Import(42, qb.Bytes, new MissionBitBlob().Bytes, null);
        Check("keys:Diff ignores keys", "", keyed.Diff(synth));
    }

    // a stored row the importers never write: Vars / ClearVarsWithPrefix skip it (no throw), the JSON codec throws (callers guard)
    static void RunMalformedRowGate()
    {
        Console.WriteLine("\n== CharProgress: malformed stored rows ==");
        var qb = new QuestBitBlob();
        var mb = new MissionBitBlob();
        mb.AddMission(3, 5);
        var expected = CharProgress.Import(42, qb.Bytes, mb.Bytes, null);
        var mrow = new CharMissionStateRow { CharId = 42, Area = MissionLog.Bastok, Current = 5 };
        mrow.SoftVars["Prog"] = 3; // no ':' in the key
        var loaded = CharProgress.FromRows(42, new CharQuestStateRow[0], new[] { mrow });
        string vars = null;
        try { vars = VarsText(loaded); loaded.ClearVarsWithPrefix("Mission[1]"); } catch (Exception ex) { vars = "threw " + ex.GetType().Name; }
        Check("malformed:Vars skips a mission key without ':'", "", vars);
        Check("malformed:Diff vs the legacy import", "missions", loaded.Diff(expected));
        string read;
        try { ProgressJsonCodec.Read("{\"a\":1.5}", new Dictionary<string, long>(), new Dictionary<string, uint>()); read = "ok"; }
        catch (Exception) { read = "throws"; }
        Check("malformed:ProgressJsonCodec.Read throws on a float (callers guard)", "throws", read);
    }

    static void RunCharProgressGate()
    {
        Console.WriteLine("\n== CharProgress: import, sync, vars, prune, diff, projection ==");

        // Import == CharacterStateMigration's quest and mission rows for the same legacy state
        var qb = new QuestBitBlob();
        qb.AddQuest(0, 3); qb.AddQuest(1, 18); qb.CompleteQuest(1, 18); qb.AddQuest(9, 200);
        var mb = new MissionBitBlob();
        mb.AddMission(3, 5); mb.SetMissionStatusNibble(3, 7, 2); mb.AddMission(6, 140);
        var vars = new[] { new CharVar("Quest[0][3]Prog", 4), new CharVar("Quest[2][7]mustZone", 230, 1900000000), new CharVar("Mission[3][5]Prog", 2), new CharVar("other", 9) };
        var p = CharProgress.Import(42, qb.Bytes, mb.Bytes, vars);
        var mig = CharacterStateMigration.Import(new LegacyCharRow { CharId = 42, QuestsBlob = qb.Bytes, MissionsBlob = mb.Bytes, CharVars = vars }, null);
        Check("progress:import quests == migration", QuestRowsText(mig.Quests), QuestRowsText(p.Quests.Values));
        Check("progress:import missions == migration", MissionRowsText(mig.Missions), MissionRowsText(p.Missions));
        Check("progress:projections are the blobs", qb.ToHex() + "|" + mb.ToHex(), p.ProjectQuests().ToHex() + "|" + p.ProjectMissions().ToHex());
        Check("progress:vars", "Mission[3][5]Prog=2@0,Quest[0][3]Prog=4@0,Quest[2][7]mustZone=230@1900000000", VarsText(p));

        // SyncQuests after each engine op: the rows' projection is the blob, byte for byte; a row emptied is deleted
        var q = CharProgress.Import(42, new QuestBitBlob().Bytes, new MissionBitBlob().Bytes, null);
        var blob = new QuestBitBlob();
        var steps = new (string Name, Action Op, string Changes)[]
        {
            ("add 1/18", () => blob.AddQuest(1, 18), "1u0d0m"),
            ("add 1/18 again", () => blob.AddQuest(1, 18), "0u0d0m"),
            ("complete 1/18", () => blob.CompleteQuest(1, 18), "1u0d0m"),
            ("add 4/255", () => blob.AddQuest(4, 255), "1u0d0m"),
            ("delCurrent 4/255", () => blob.DelCurrentQuest(4, 255), "0u1d0m"),
            ("del 1/18", () => blob.DelQuest(1, 18), "0u1d0m"),
            ("add 10/0", () => blob.AddQuest(10, 0), "1u0d0m"),
        };
        foreach (var st in steps)
        {
            st.Op();
            var ch = q.SyncQuests(blob.Bytes);
            Check($"progress:sync quests {st.Name}", st.Changes + "|" + blob.ToHex(), ChangesText(ch) + "|" + q.ProjectQuests().ToHex());
        }
        Check("progress:sync quests keys are synthetic", "legacy:10/0", q.Quests[new QuestWireRef(QuestLog.Coalition, 0)].QuestKey);

        // SyncMissions after each engine op, including CoP's current-relative completion and a no-op save (setRank)
        var mblob = new MissionBitBlob();
        var msteps = new (string Name, Action Op, string Changes)[]
        {
            ("save with no change", () => { }, "0u0d0m"),
            ("add 3/1", () => mblob.AddMission(3, 1), "0u0d1m"),
            ("status nibble", () => mblob.SetMissionStatusNibble(3, 5, 1), "0u0d1m"),
            ("complete 3/1", () => mblob.CompleteMission(3, 1), "0u0d1m"),
            ("CoP add 6/300", () => mblob.AddMission(6, 300), "0u0d1m"),
            ("CoP complete 6/300", () => mblob.CompleteMission(6, 300), "0u0d1m"),
            ("del 3/1", () => mblob.DelMission(3, 1), "0u0d1m"),
            ("full status", () => mblob.SetMissionStatusFull(0, 0x12345678), "0u0d1m"),
        };
        foreach (var st in msteps)
        {
            st.Op();
            var ch = q.SyncMissions(mblob.Bytes);
            Check($"progress:sync missions {st.Name}", st.Changes + "|" + mblob.ToHex(), ChangesText(ch) + "|" + q.ProjectMissions().ToHex());
        }

        // SetVar: Quest Prog -> step, other suffixes -> soft vars, Mission -> "<id>:<suffix>"; 0 removes; an emptied row goes
        var v = CharProgress.Import(42, new QuestBitBlob().Bytes, new MissionBitBlob().Bytes, null);
        Check("progress:IsProgressVar", "True,True,True,False,False,False,False",
              string.Join(",", new[] { "Quest[1][18]Prog", "Quest[1][18]mustZone", "Mission[3][1]Prog", "Quest[1][18]", "Mission[3][1]", "Quest[11][0]Prog", "[generic]mustZone" }
                  .Select(n => CharProgress.IsProgressVar(n).ToString())));
        Check("progress:set Prog creates an Available row", "1u0d0m", ChangesText(v.SetVar("Quest[1][18]Prog", 2, 1900000000)));
        Check("progress:set mustZone", "1u0d0m", ChangesText(v.SetVar("Quest[1][18]mustZone", 236, 0)));
        Check("progress:the row", "1/18|legacy:1/18|0|2|{\"@expiry\":{\"Prog\":1900000000},\"mustZone\":236}", QuestRowsText(v.Quests.Values));
        Check("progress:set a mission var", "0u0d1m", ChangesText(v.SetVar("Mission[3][1]Prog", 4, 0)));
        Check("progress:mission row 3 json", "{\"1:Prog\":4}", v.Missions[3].ProgressJson);
        Check("progress:vars after sets", "Mission[3][1]Prog=4@0,Quest[1][18]Prog=2@1900000000,Quest[1][18]mustZone=236@0", VarsText(v));
        Check("progress:other names change nothing", "0u0d0m|0u0d0m|0u0d0m",
              $"{ChangesText(v.SetVar("Quest[1][18]", 5, 0))}|{ChangesText(v.SetVar("NotAQuest", 5, 0))}|{ChangesText(v.SetVar("Quest[1][256]Prog", 5, 0))}");
        Check("progress:Prog 0 keeps the row (mustZone left)", "1u0d0m", ChangesText(v.SetVar("Quest[1][18]Prog", 0, 0)));
        Check("progress:mustZone 0 empties the row: deleted", "0u1d0m|0", $"{ChangesText(v.SetVar("Quest[1][18]mustZone", 0, 0))}|{v.Quests.Count}");
        Check("progress:0 on a quest with no row: nothing", "0u0d0m|0", $"{ChangesText(v.SetVar("Quest[2][2]Prog", 0, 0))}|{v.Quests.Count}");

        // a var on an accepted quest: clearing it leaves the row (status Accepted), upserted not deleted
        var acc = new QuestBitBlob();
        acc.AddQuest(1, 30);
        var w = CharProgress.Import(42, acc.Bytes, new MissionBitBlob().Bytes, new[] { new CharVar("Quest[1][30]Prog", 1) });
        Check("progress:clearing an accepted quest's var keeps it", "1u0d0m|1", $"{ChangesText(w.SetVar("Quest[1][30]Prog", 0, 0))}|{w.Quests.Count}");

        // ClearVarsWithPrefix: every Quest / Mission var whose name starts with the prefix
        var c = CharProgress.Import(42, new QuestBitBlob().Bytes, new MissionBitBlob().Bytes, new[]
        {
            new CharVar("Quest[1][18]Prog", 2), new CharVar("Quest[1][18]mustZone", 236), new CharVar("Quest[1][19]Prog", 1), new CharVar("Mission[3][1]Prog", 4),
        });
        Check("progress:clear prefix Quest[1][18]", "0u1d0m|Mission[3][1]Prog=4@0,Quest[1][19]Prog=1@0",
              $"{ChangesText(c.ClearVarsWithPrefix("Quest[1][18]"))}|{VarsText(c)}");
        Check("progress:clear prefix Mission[3]", "0u0d1m|Quest[1][19]Prog=1@0", $"{ChangesText(c.ClearVarsWithPrefix("Mission[3]"))}|{VarsText(c)}");
        Check("progress:clear prefix with no match", "0u0d0m", ChangesText(c.ClearVarsWithPrefix("Quest[9]")));

        // PruneExpired: expired at expiry <= now, as CharData.GetCharVar reads it
        var e = CharProgress.Import(42, new QuestBitBlob().Bytes, new MissionBitBlob().Bytes, new[]
        {
            new CharVar("Quest[1][18]Prog", 2, 100), new CharVar("Quest[1][19]Wait", 1, 101), new CharVar("Mission[3][1]Prog", 4, 50),
        });
        Check("progress:prune at 100", "0u1d1m|Quest[1][19]Wait=1@101", $"{ChangesText(e.PruneExpired(100))}|{VarsText(e)}");
        Check("progress:prune again: nothing", "0u0d0m", ChangesText(e.PruneExpired(100)));

        // Diff: canonical compare by part
        var baseline = CharProgress.Import(42, qb.Bytes, mb.Bytes, vars);
        Check("progress:diff same", "", baseline.Diff(CharProgress.Import(42, qb.Bytes, mb.Bytes, vars)));
        var qb2 = new QuestBitBlob(); qb2.LoadFromBlob(qb.Bytes); qb2.AddQuest(5, 5);
        var mb2 = new MissionBitBlob(); mb2.LoadFromBlob(mb.Bytes); mb2.AddMission(0, 2);
        Check("progress:diff quests", "quests", baseline.Diff(CharProgress.Import(42, qb2.Bytes, mb.Bytes, vars)));
        Check("progress:diff missions,vars", "missions,vars",
              baseline.Diff(CharProgress.Import(42, qb.Bytes, mb2.Bytes, new[] { new CharVar("Quest[0][3]Prog", 5) })));
        var raw = new QuestBitBlob(); raw.LoadFromBlob(qb.Bytes); raw.RawSet(1, 18, current: true, complete: true);
        Check("progress:diff ignores the raw (1,1) pair (canonical)", "", baseline.Diff(CharProgress.Import(42, raw.Bytes, mb.Bytes, vars)));
        var zeroVar = CharProgress.Import(42, qb.Bytes, mb.Bytes, vars.Concat(new[] { new CharVar("Quest[3][3]Count", 0) }).ToArray());
        Check("progress:diff ignores a var of value 0", "", baseline.Diff(zeroVar));

        // FromRows: stored rows back; a mission area with no row takes the new character's default
        var f = CharProgress.FromRows(42, baseline.Quests.Values, new CharMissionStateRow[0]);
        Check("progress:from rows, no mission rows: the fresh mission log", new MissionBitBlob().ToHex(), f.ProjectMissions().ToHex());
        Check("progress:from rows keeps the quests", qb.ToHex(), f.ProjectQuests().ToHex());

        // ProjectInto copies both projections into the zone's arrays
        var questLog = new byte[704];
        var missionLog = new byte[1050];
        baseline.ProjectInto(questLog, missionLog);
        Check("progress:project into", qb.ToHex() + "|" + mb.ToHex(), HexOf(questLog) + "|" + HexOf(missionLog));
    }

    static string HexOf(byte[] b) => Convert.ToHexString(b);

    // ---- soft-var expiry: the importers keep a char var's expiry, the progress JSON carries it as "@expiry" ----
    static void RunExpiryGate()
    {
        Console.WriteLine("\n== soft-var expiry: importers and progress JSON ==");
        var blob = new QuestBitBlob();
        blob.AddQuest(1, 18);
        var rows = QuestStateImporter.Import(7, blob.Bytes, new[]
        {
            new QuestStateImporter.CharVar("Quest[1][18]Prog", 2, 1900000000),
            new QuestStateImporter.CharVar("Quest[1][18]mustZone", 236),
            new QuestStateImporter.CharVar("Quest[1][18]Timer", 5, 1900000100),
        }, null);
        var r = rows.Find(x => x.Area == QuestLog.Bastok && x.QuestId == 18);
        Check("expiry:quest step", "2", r?.Step.ToString());
        Check("expiry:quest json", "{\"@expiry\":{\"Prog\":1900000000,\"Timer\":1900000100},\"Timer\":5,\"mustZone\":236}", r?.ProgressJson);

        var soft = new Dictionary<string, long>();
        var exp = new Dictionary<string, uint>();
        ProgressJsonCodec.Read(r?.ProgressJson, soft, exp);
        Check("expiry:json read back", "Timer=5,mustZone=236|Prog=1900000000,Timer=1900000100", DumpMap(soft) + "|" + DumpMap(exp));
        Check("expiry:json rewritten identically", r?.ProgressJson, ProgressJsonCodec.Write(soft, exp));
        Check("expiry:no expiry keeps the old json", "{\"mustZone\":12345}",
              ProgressJsonCodec.Write(new Dictionary<string, long> { ["mustZone"] = 12345 }, null));
        Check("expiry:nothing writes null", "null",
              ProgressJsonCodec.Write(new Dictionary<string, long>(), new Dictionary<string, uint>()) ?? "null");
        ProgressJsonCodec.Read(null, soft, exp);
        Check("expiry:null reads as nothing", "0/0", $"{soft.Count}/{exp.Count}");

        var mrows = MissionStateImporter.Import(7, new MissionBitBlob().Bytes,
            new[] { new MissionStateImporter.CharVar("Mission[4][10]Prog", 9, 1900000200) });
        Check("expiry:mission json", "{\"10:Prog\":9,\"@expiry\":{\"10:Prog\":1900000200}}", mrows[4].ProgressJson);

        var all = CharacterStateMigration.Import(new LegacyCharRow
        {
            CharId = 7,
            QuestsBlob = blob.Bytes,
            MissionsBlob = new MissionBitBlob().Bytes,
            CharVars = new[] { new CharVar("Quest[1][18]Prog", 2, 1900000000) },
        }, null);
        Check("expiry:the migration fans the expiry out", "{\"@expiry\":{\"Prog\":1900000000}}",
              all.Quests.Find(x => x.QuestId == 18)?.ProgressJson);
    }

    static string DumpMap<T>(Dictionary<string, T> d)
    {
        var keys = new List<string>(d.Keys);
        keys.Sort(StringComparer.Ordinal);
        return string.Join(",", keys.ConvertAll(k => k + "=" + d[k]));
    }

    static void Check(string name, string expected, string actual)
    {
        bool ok = expected == actual;
        if (ok) Ok(name); else Bad($"{name}: expected {expected} actual {actual}");
    }
    static void Ok(string m)
    {
        named_?.Add(m);
        if (!Counted(m)) return;
        _pass++;
        (out_ ?? Console.Out).WriteLine($"  [PASS] {m}");
    }
    static void Bad(string m)
    {
        named_?.Add(m);
        if (!Counted(m)) return;
        _fail++;
        (out_ ?? Console.Out).WriteLine($"  [FAIL] {m}");
    }

    // --only (quest rework P8): the prefixes (null: no filter), the console the counted checks print to while the rest
    // goes nowhere, and the names of the checks made while a section's names are collected (null: not collecting)
    static List<string> only_;
    static TextWriter out_;
    static List<string> named_;
    const string GeneratorPrefix = "gen:";
    static bool Counted(string m) => only_ == null || only_.Any(p => PrefixCounts(p, m));
    // a prefix counts a name it starts, unless the prefix ends in a digit and the name goes on with one: "outlands g1"
    // counts "outlands g1:x" and "outlands g1 facets:y", not "outlands g10:x" (a group number is whole)
    static bool PrefixCounts(string p, string m) =>
        m.StartsWith(p, StringComparison.Ordinal) &&
        !(p.Length > 0 && char.IsAsciiDigit(p[^1]) && m.Length > p.Length && char.IsAsciiDigit(m[p.Length]));
    // whether the filter leaves out every check named <sectionPrefix>...: no prefix starts it, or is started by it
    static bool OnlySkips(string sectionPrefix) =>
        only_ != null && !only_.Any(p => p.StartsWith(sectionPrefix, StringComparison.Ordinal) || sectionPrefix.StartsWith(p, StringComparison.Ordinal));
}
