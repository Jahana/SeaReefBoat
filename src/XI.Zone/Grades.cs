using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace XI.Zone
{
    // Port of src/map/grades.cpp + the grades/stats datasets (data/grades.yaml, data/stats.yaml): which grade each
    // job/race holds per attribute, and what a grade is worth per level. Feeds charutils::CalculateStats.
    //
    // Stat index (GetJobGrade / GetRaceGrades): 0 = hp, 1 = mp, 2..8 = str dex vit agi int mnd chr.
    // Grade (xi::StatRank): 0 = none, 1..7 = a..g. Race index: 0 hume, 1 elvaan, 2 tarutaru, 3 mithra, 4 galka.
    public sealed class Grades
    {
        public const int Levels = 100; // kGrowthLevels
        public const int Ranks  = 8;   // kGrowthRanks
        public const int Races  = 5;   // kGrowthRaces

        private const int AttributeLastLevel = 75;
        private const int MaxLevel           = Levels - 1;

        private static readonly string[] JobKeys = { "war", "mnk", "whm", "blm", "rdm", "thf", "pld", "drk", "bst", "brd", "rng", "sam", "nin", "drg", "smn", "blu", "cor", "pup", "dnc", "sch", "geo", "run" };
        private static readonly string[] RaceKeys = { "hume", "elvaan", "tarutaru", "mithra", "galka" };
        private static readonly string[] AttrKeys = { "hp", "mp", "str", "dex", "vit", "agi", "int", "mnd", "chr" };
        private static readonly string[] GradeKeys = { "a", "b", "c", "d", "e", "f", "g" };
        private static readonly string[] MpGradeKeys = { "a", "b", "c", "d", "f" };   // no job holds an E or G mp grade
        private static readonly int[]    MpGradeRanks = { 1, 2, 3, 4, 6 };

        // grades().Jobs[job][stat] (index 0 = Job::NON and 23 = MON stay all-none) / grades().Races[race][stat]
        private readonly byte[,] jobs_  = new byte[24, 9];
        private readonly byte[,] races_ = new byte[Races, 9];

        // stats(): HP/MP totals per level, attribute half points per rank, the flat post-75 gain
        private readonly ushort[][] jobHp_  = NewCurves(Ranks);
        private readonly ushort[][] jobMp_  = NewCurves(Ranks);
        private readonly ushort[][] raceHp_ = NewCurves(Races);
        private readonly ushort[][] raceMp_ = NewCurves(Races);
        private readonly ushort[][] halfPoints_ = NewCurves(Ranks);
        private readonly byte[] pointsOver75_ = new byte[Levels];
        private bool roundOnce_;

        // map.SJ_MP_DIVISOR -- what a subjob contributes of its own MP (halved on retail)
        public float SjMpDivisor = 2.0f;

        public static Grades Load(string dataDir) =>
            new Grades(MiniYaml.Load(Path.Combine(dataDir, "grades.yaml")), MiniYaml.Load(Path.Combine(dataDir, "stats.yaml")));

        public Grades(Dictionary<string, object> gradesDoc, Dictionary<string, object> statsDoc)
        {
            var grades = gradesDoc.Map("grades");
            var jobs = grades.Map("jobs");
            for (int j = 0; j < JobKeys.Length; j++)
                FillGrades(jobs_, j + 1, jobs.Map(JobKeys[j]));
            var races = grades.Map("races");
            for (int r = 0; r < Races; r++)
                FillGrades(races_, r, races.Map(RaceKeys[r]));

            var stats = statsDoc.Map("stats");
            roundOnce_ = (string)stats["round_once"] == "true";

            var hp = stats.Map("hp");
            var hpByGrade = BuildCurves(hp.Map("graded"), GradeKeys, 1, MaxLevel);
            for (int c = 0; c < GradeKeys.Length; c++) jobHp_[c + 1] = hpByGrade[c];
            BuildCurves(hp.Map("racial"), RaceKeys, 1, MaxLevel).CopyTo(raceHp_, 0);

            var mp = stats.Map("mp");
            var mpByGrade = BuildCurves(mp.Map("graded"), MpGradeKeys, 1, MaxLevel);
            for (int c = 0; c < MpGradeKeys.Length; c++) jobMp_[MpGradeRanks[c]] = mpByGrade[c];
            BuildCurves(mp.Map("racial"), RaceKeys, 1, MaxLevel).CopyTo(raceMp_, 0);

            // attributes are counted in twentieths and floored to half points, so the parts pool before rounding
            var byGrade = BuildCurves(stats.Map("attributes"), GradeKeys, 1, AttributeLastLevel);
            for (int c = 0; c < GradeKeys.Length; c++)
                for (int level = 0; level < Levels; level++)
                    halfPoints_[c + 1][level] = (ushort)(byGrade[c][level] / 10);

            var ungraded = BuildCurves(stats.Map("ungraded"), null, AttributeLastLevel + 1, MaxLevel)[0];
            for (int level = 0; level < Levels; level++) pointsOver75_[level] = (byte)ungraded[level];
        }

        private static ushort[][] NewCurves(int n) => Enumerable.Range(0, n).Select(_ => new ushort[Levels]).ToArray();

        private static byte Rank(string token) => token == "none" ? (byte)0 : (byte)(Array.IndexOf(GradeKeys, token) + 1);

        private static void FillGrades(byte[,] into, int row, Dictionary<string, object> attrs)
        {
            for (int s = 0; s < AttrKeys.Length; s++) into[row, s] = Rank((string)attrs[AttrKeys[s]]);
        }

        // Read a level-major table down its columns, accumulating what each has granted by every level. A row holds until
        // the next level listed, the last runs to lastLevel, and past lastLevel a curve holds what it reached.
        // columns == null is the single unnamed column (the ungraded gain: "76: 0").
        private static ushort[][] BuildCurves(Dictionary<string, object> table, string[] columns, int firstLevel, int lastLevel)
        {
            var rows = table.Select(kv => (level: int.Parse(kv.Key, CultureInfo.InvariantCulture), value: kv.Value)).OrderBy(r => r.level).ToList();
            if (rows.Count == 0 || rows[0].level != firstLevel) throw new FormatException($"stats: table has to open at level {firstLevel}");
            if (rows[^1].level > lastLevel) throw new FormatException($"stats: table keys level {rows[^1].level}, past the {lastLevel} it covers");

            int count = columns?.Length ?? 1;
            var curves = NewCurves(count);
            var running = new uint[count];
            for (int i = 0; i < rows.Count; i++)
            {
                int until = i + 1 < rows.Count ? rows[i + 1].level - 1 : lastLevel;
                var granted = new uint[count];
                for (int c = 0; c < count; c++)
                    granted[c] = uint.Parse(columns == null ? (string)rows[i].value : (string)((Dictionary<string, object>)rows[i].value)[columns[c]], CultureInfo.InvariantCulture);
                for (int level = rows[i].level; level <= until; level++)
                    for (int c = 0; c < count; c++)
                    {
                        running[c] += granted[c];
                        curves[c][level] = (ushort)running[c];
                    }
            }
            for (int level = lastLevel + 1; level < Levels; level++)
                for (int c = 0; c < count; c++) curves[c][level] = curves[c][lastLevel];
            return curves;
        }

        // GrowthCurve::at -- clamps to the last level
        private static ushort At(ushort[] curve, int level) => curve[Math.Min(level, Levels - 1)];

        public byte GetJobGrade(int job, int stat) => jobs_[job, stat];
        public byte GetRaceGrades(int race, int stat) => races_[race, stat];

        // Race, job and subjob parts are each floored to half points, then summed and floored once (round_once); before
        // the April 2014 update each part was floored to a whole point on its own.
        public ushort GetBaseStat(int raceRank, int jobRank, int level, int subJobRank, int subLevel)
        {
            int race = halfPoints_[raceRank][level], job = halfPoints_[jobRank][level], sub = halfPoints_[subJobRank][subLevel];
            int over75 = pointsOver75_[level];
            if (!roundOnce_) return (ushort)(race / 2 + job / 2 + sub / 4 + over75);
            return (ushort)((race + job + sub / 2) / 2 + over75);
        }

        // Race and job HP are whole points added together; the subjob adds half of its own job HP
        public ushort GetBaseHP(int race, int jobGrade, int level, int subJobGrade, int subLevel) =>
            (ushort)(At(raceHp_[race], level) + At(jobHp_[jobGrade], level) + At(jobHp_[subJobGrade], subLevel) / 2);

        // MP is HP's shape, except a job without an MP grade brings no racial MP; when only the subjob has MP the racial
        // part enters at the subjob's level, shared down like the subjob itself.
        public ushort GetBaseMP(int race, int jobGrade, int level, int subJobGrade, int subLevel)
        {
            if (jobGrade > 0)
                return (ushort)(At(raceMp_[race], level) + At(jobMp_[jobGrade], level) + SubJobShare(At(jobMp_[subJobGrade], subLevel)));
            if (subJobGrade > 0)
                return (ushort)(SubJobShare(At(raceMp_[race], subLevel)) + SubJobShare(At(jobMp_[subJobGrade], subLevel)));
            return 0;
        }

        private int SubJobShare(int value) => (int)(value / SjMpDivisor);
    }
}
