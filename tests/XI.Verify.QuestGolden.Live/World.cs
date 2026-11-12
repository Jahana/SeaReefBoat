using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using XI.Persistence;

// The world tables (server_variables, conquest_system, bcnm_records, unity_system) of a golden world. Every world starts from the same
// rows, the snapshot in tests/golden/world-tables.txt, whatever the database's own rows are and whatever an earlier run
// (or world of the run) left:
// the run's queries go through WorldTablesDatabase, which sends each one that names a world table to the run's private
// copy of it (golden_world_<tag>_<table>, made LIKE the real table), and NewWorld fills the copies from the snapshot.
// So a trace depends neither on the database's history nor on the lane, two shards never share the rows, and a run
// never writes the real world tables (the live server's). The tag is the run's lowest locked char id (the copies are
// the lock holder's own; a crashed run's are reused by the next holder), or p<pid> for startup, which locks no char;
// the copies are dropped at the end of the run.
//
// A trace's header hashes the hashed world tables (server_variables, conquest_system) at its start (world <table>
// <hash>): with the snapshot that is the snapshot's hash, so replay fails a trace whose header differs (the snapshot
// changed since the recording: record it again). The seeded ones (bcnm_records) are copied and restored the same way
// but no header hashes them: their rows are the sql/ seed's, and a trace that reads them shows it in its packets.
// `world-snapshot` writes the file: the hashed tables from the --db's real ones, the seeded ones from sql/.
//
// The routing is the IDatabase the harness hands the zone host and the char loader: any code path that opens its own
// MySqlConnection (the state snapshot's StateConnectionString, the lock connection) bypasses it, so such a path must not
// read or write a world table.
internal static partial class Program
{
    private const string WorldFile = "world-tables.txt";
    private static WorldTablesDatabase world_;
    private static List<WorldTable> worldSnapshot_;

    // one table of the snapshot: its columns (the real table's, in order) and its rows as text
    internal sealed record WorldTable(string Name, List<string> Columns, List<string[]> Rows);

    // the run's private world tables made (empty) for the tag; every query from here on that names a world table
    // goes to them
    private static void OpenWorldTables(string tag)
    {
        foreach (var table in WorldTables)
        {
            string mine = WorldTablesDatabase.PrivateName(table, tag);
            Sql(world_.Inner, $"CREATE TABLE IF NOT EXISTS `{mine}` LIKE `{table}`");
        }
        world_.Tag = tag;
    }

    // the private tables back to the snapshot's rows (NewWorld, before the zone host reads them)
    private static void RestoreWorldTables()
    {
        if (world_?.Tag == null) throw new InvalidOperationException("the world tables are not open (OpenWorldTables)");
        worldSnapshot_ ??= ReadWorldSnapshot(File.ReadAllText(Path.Combine(GoldenDir, WorldFile)), WorldFile);
        foreach (var t in worldSnapshot_)
        {
            string mine = WorldTablesDatabase.PrivateName(t.Name, world_.Tag);
            Sql(world_.Inner, $"DELETE FROM `{mine}`");
            if (t.Rows.Count == 0) continue;
            string row = "(" + string.Join(", ", t.Columns.Select(_ => "?")) + ")";
            Sql(world_.Inner, $"INSERT INTO `{mine}` ({string.Join(", ", t.Columns.Select(c => $"`{c}`"))}) VALUES " +
                              string.Join(", ", t.Rows.Select(_ => row)),
                t.Rows.SelectMany(r => r.Select(BoundValue.Of)).ToArray());
        }
    }

    // the private tables dropped (the end of the run)
    private static void DropWorldTables()
    {
        if (world_?.Tag == null) return;
        try
        {
            foreach (var table in WorldTables) Sql(world_.Inner, $"DROP TABLE IF EXISTS `{WorldTablesDatabase.PrivateName(table, world_.Tag)}`");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"could not drop the world tables of {world_.Tag}: {ex.Message}");
        }
        world_.Tag = null;
        world_.Chars = null;
    }

    // world-snapshot: tests/golden/world-tables.txt written, the hashed world tables from the --db's real ones, the
    // seeded ones from their sql/ file's INSERTs (run into a scratch copy, read back, dropped)
    private static void WriteWorldSnapshot()
    {
        var tables = new List<WorldTable>();
        foreach (var table in WorldTables)
        {
            string from = table;
            if (SeededWorldTables.Contains(table))
            {
                from = $"golden_seed_p{Environment.ProcessId}_{table}";
                Sql(world_.Inner, $"DROP TABLE IF EXISTS `{from}`");
                Sql(world_.Inner, $"CREATE TABLE `{from}` LIKE `{table}`");
                // each INSERT INTO `<table>` statement of the seed file, its rows counted (SeedInserts)
                int n = 0, statements = 0;
                string seed = File.ReadAllText(Path.Combine(Root, "sql", SeedFiles[table]));
                foreach (var (values, rows) in SeedInserts(seed, table))
                {
                    Sql(world_.Inner, $"INSERT INTO `{from}` " + values);
                    n += rows;
                    statements++;
                }
                if (SeedOverrides.TryGetValue(table, out var over)) Sql(world_.Inner, string.Format(over, from));
                var count = Sql(world_.Inner, $"SELECT COUNT(*) AS n FROM `{from}`");
                long got = count != null && count.Next() ? count.Get<long>("n") : -1;
                if (n == 0 || got != n)
                    throw new InvalidOperationException($"sql/{SeedFiles[table]}: {statements} INSERT INTO `{table}` " +
                                                        $"statements of {n} rows, {got} rows");
            }
            try
            {
                var cols = Schema().Where(c => c.Table == table).Select(c => c.Column).ToList();
                var rows = new List<string[]>();
                var r = Sql(world_.Inner, $"SELECT {string.Join(", ", cols.Select(c => $"`{c}`"))} FROM `{from}` ORDER BY {string.Join(", ", cols.Select(c => $"`{c}`"))}");
                while (r != null && r.Next())
                    rows.Add(cols.Select(c => r.IsNull(c) ? throw new InvalidOperationException($"{table}.{c} is NULL: the snapshot holds no NULL") : r.Get<string>(c)).ToArray());
                tables.Add(new WorldTable(table, cols, rows));
            }
            finally
            {
                if (from != table) Sql(world_.Inner, $"DROP TABLE IF EXISTS `{from}`");
            }
        }
        string path = Path.Combine(GoldenDir, WorldFile);
        File.WriteAllText(path, WorldSnapshotText(tables));
        Pass($"{path} written from {dbName_}: " + string.Join(", ", tables.Select(t => $"{t.Name} {t.Rows.Count} rows")));
    }

    // the INSERT INTO `<table>` statements of a SQL file, each as the text after that prefix up to its ';' (not
    // included) and its row count (the parenthesized tuples after its VALUES keyword: a column list before it is not
    // a row): a statement starts a line (after blanks), may span lines and hold a ';' or a parenthesis in a quoted
    // value ('...', "..." or `...`, with backslash escapes and doubled quotes); a -- or # comment runs to the end of
    // its line and /* ... */ to its close, outside a quote. Between statements a /* ... */ block is skipped whole (an
    // INSERT inside it is not read; the text after its close starts no line); a quote there is not tracked (the
    // seed files hold none outside their INSERTs). A statement with no ';' (the file's end) is refused
    internal static List<(string Values, int Rows)> SeedInserts(string sql, string table)
    {
        var found = new List<(string Values, int Rows)>();
        string insert = $"INSERT INTO `{table}` ";
        int i = 0;
        bool lineStart = true;
        while (i < sql.Length)
        {
            char ch = sql[i];
            if (ch == '\n') { lineStart = true; i++; continue; }
            if (lineStart && (ch == ' ' || ch == '\t' || ch == '\r')) { i++; continue; }
            if (ch == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                int close = sql.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = close < 0 ? sql.Length : close + 2;
                lineStart = false;
                continue;
            }
            if (lineStart && string.CompareOrdinal(sql, i, insert, 0, insert.Length) == 0)
            {
                int start = i + insert.Length, depth = 0, rows = 0;
                bool inValues = false;
                char quote = '\0';
                int j = start;
                for (; j < sql.Length; j++)
                {
                    char c = sql[j];
                    if (quote != '\0')
                    {
                        if (c == '\\') j++;
                        else if (c == quote && j + 1 < sql.Length && sql[j + 1] == quote) j++;
                        else if (c == quote) quote = '\0';
                        continue;
                    }
                    if (c is '\'' or '"' or '`') quote = c;
                    else if (c == '#' || c == '-' && j + 1 < sql.Length && sql[j + 1] == '-')
                    {
                        while (j < sql.Length && sql[j] != '\n') j++;
                    }
                    else if (c == '/' && j + 1 < sql.Length && sql[j + 1] == '*')
                    {
                        int close = sql.IndexOf("*/", j + 2, StringComparison.Ordinal);
                        j = close < 0 ? sql.Length : close + 1;
                    }
                    else if (depth == 0 && !inValues && IsKeywordAt(sql, j, "VALUES")) inValues = true;
                    else if (c == '(')
                    {
                        if (depth++ == 0 && inValues) rows++;
                    }
                    else if (c == ')') depth--;
                    else if (c == ';') break;
                }
                if (j >= sql.Length) throw new FormatException($"an INSERT INTO `{table}` statement with no ';'");
                found.Add((sql.Substring(start, j - start), rows));
                i = j + 1;
                lineStart = false;
                continue;
            }
            lineStart = false;
            i++;
        }
        return found;
    }

    // the SQL keyword at i, any case, as a whole word (no letter, digit or _ just before or after it)
    private static bool IsKeywordAt(string sql, int i, string keyword)
    {
        static bool Word(char c) => char.IsLetterOrDigit(c) || c == '_';
        return i + keyword.Length <= sql.Length &&
               string.Compare(sql, i, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) == 0 &&
               (i == 0 || !Word(sql[i - 1])) && (i + keyword.Length == sql.Length || !Word(sql[i + keyword.Length]));
    }

    // the snapshot file: a comment header, then per table `table <name>`, `columns <c1> <c2> ...` and a line per row,
    // its values by tab (a tab, newline or backslash in a value escaped as \t, \n, \\); LF line endings
    internal static string WorldSnapshotText(IEnumerable<WorldTable> tables)
    {
        var sb = new StringBuilder();
        sb.Append("# the world tables every golden world starts from (tests/XI.Verify.QuestGolden.Live/World.cs). A change to\n");
        sb.Append("# server_variables or conquest_system changes every trace's world header: record them again. bcnm_records and\n");
        sb.Append("# unity_system are sql/bcnm_info.sql's and sql/unity_system.sql's seeds, which no header hashes. Written by\n");
        sb.Append("# `world-snapshot`. unity_system's leader 1 row holds members_current 2, the value every trace recorded\n");
        sb.Append("# (Program.SeedOverrides).\n");
        foreach (var t in tables)
        {
            sb.Append($"table {t.Name}\n");
            sb.Append($"columns {string.Join(" ", t.Columns)}\n");
            foreach (var row in t.Rows) sb.Append(string.Join("\t", row.Select(EscapeCell))).Append('\n');
        }
        return sb.ToString();
    }

    internal static List<WorldTable> ReadWorldSnapshot(string text, string file)
    {
        var tables = new List<WorldTable>();
        WorldTable current = null;
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string where = $"{file}:{i + 1}";
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.StartsWith("table ", StringComparison.Ordinal))
            {
                current = new WorldTable(line.Substring(6), null, new List<string[]>());
                tables.Add(current);
                continue;
            }
            if (current == null) throw new FormatException($"{where}: a row before a table line");
            if (line.StartsWith("columns ", StringComparison.Ordinal) && current.Columns == null)
            {
                tables[^1] = current = current with { Columns = line.Substring(8).Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList() };
                continue;
            }
            if (current.Columns == null) throw new FormatException($"{where}: {current.Name} has no columns line");
            var cells = line.Split('\t').Select(UnescapeCell).ToArray();
            if (cells.Length != current.Columns.Count) throw new FormatException($"{where}: {cells.Length} values for {current.Columns.Count} columns");
            current.Rows.Add(cells);
        }
        foreach (var name in WorldTables)
            if (tables.All(t => t.Name != name)) throw new FormatException($"{file}: no table {name}");
        return tables;
    }

    private static string EscapeCell(string v) => v.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\n", "\\n");

    private static string UnescapeCell(string v)
    {
        if (v.IndexOf('\\') < 0) return v;
        var sb = new StringBuilder();
        for (int i = 0; i < v.Length; i++)
        {
            if (v[i] != '\\' || i + 1 == v.Length) { sb.Append(v[i]); continue; }
            char n = v[++i];
            sb.Append(n switch { 't' => '\t', 'n' => '\n', _ => n });
        }
        return sb.ToString();
    }
}

// the run's database with its world tables private: a query naming server_variables, conquest_system, bcnm_records or
// unity_system (as a whole word) goes to golden_world_<tag>_<table>; the rest goes through unchanged. No tag: nothing is rewritten
internal sealed class WorldTablesDatabase : IDatabase
{
    private static readonly Regex Names = new(@"\b(" + string.Join("|", Program.WorldTables) + @")\b");

    public WorldTablesDatabase(IDatabase inner) => Inner = inner;

    public IDatabase Inner { get; }
    public string Tag;
    // the running flow's char ids: the engine's hourly unity recount reads char_profile, which every run on the lane
    // shares, so it counts only these chars (Rewrite); null: nothing beyond the table names is rewritten
    public IReadOnlyList<uint> Chars;

    internal static string PrivateName(string table, string tag) => $"golden_world_{tag}_{table}";

    // ZoneHost.Roe.cs's recount subquery, exactly (SelfTest checks the engine source still has it)
    internal const string UnityRecount = "SELECT unity_leader, COUNT(*) AS members FROM char_profile GROUP BY unity_leader";

    internal static string Rewrite(string query, string tag, IReadOnlyList<uint> chars = null)
    {
        if (tag == null || query == null || !NamesAny(query)) return query;
        var named = Names.Replace(query, m => PrivateName(m.Value, tag));
        if (chars == null || chars.Count == 0 || !named.Contains(UnityRecount, StringComparison.Ordinal)) return named;
        return named.Replace(UnityRecount,
            $"SELECT unity_leader, COUNT(*) AS members FROM char_profile WHERE charid IN ({string.Join(",", chars)}) GROUP BY unity_leader",
            StringComparison.Ordinal);
    }

    // whether the query names a world table at all (a plain loop: every query of the run passes here)
    private static bool NamesAny(string query)
    {
        foreach (var t in Program.WorldTables)
            if (query.Contains(t, StringComparison.Ordinal)) return true;
        return false;
    }

    public ResultSet Execute(string query, IReadOnlyList<BoundValue> parameters) => Inner.Execute(Rewrite(query, Tag, Chars), parameters);
    public ResultSet ExecuteBulk(string query, IReadOnlyList<BoundValue> parameters) => Inner.ExecuteBulk(Rewrite(query, Tag, Chars), parameters);
    public string Schema => Inner.Schema;
    public string Version => Inner.Version;
    public string DriverVersion => Inner.DriverVersion;
    public bool Transaction(Fn transactionFn) => Inner.Transaction(transactionFn);
}
