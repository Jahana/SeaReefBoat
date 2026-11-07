using System;
using System.Collections.Generic;
using System.Text;
using XI.Persistence;

namespace XI.Zone
{
    // fishingcontest: the contest parameters and the entry pool the fish ranking menu (0x04B value1 2) reads. Loaded as
    // InitializeFishingContestSystem does (LoadContestParameters, LoadContestEntries, BuildPlaceholderEntries); the
    // stage progression, scoring, submissions, withdrawals and rewards are in ZoneHost.FishingContest.
    public sealed class FishingContest
    {
        public enum Status : byte { Contesting, Opening, Accepting, Releasing, Presenting, Hiatus, Closed }
        public enum Measure : byte { Greatest, Smallest }

        // FishingContestEntry: the 36-byte block the ranking packet carries
        public sealed class Entry
        {
            public const int Size = 36;
            public string Name = "";
            public byte MJob, SJob, MLvl, SLvl, Race, Allegiance, FishRank;
            public uint Score, SubmitTime;
            public byte ContestRank, ResultCount, Share, DatasetB;

            public Entry Clone() => (Entry)MemberwiseClone();

            // name is char[16] filled with at most 15 bytes
            public void Write(PacketWriter w, int o)
            {
                var name = Encoding.ASCII.GetBytes(Name);
                w.Bytes(o, name.AsSpan(0, Math.Min(name.Length, 15)));
                w.U8(o + 16, MJob);
                w.U8(o + 17, SJob);
                w.U8(o + 18, MLvl);
                w.U8(o + 19, SLvl);
                w.U8(o + 20, Race);
                w.U8(o + 22, Allegiance);
                w.U8(o + 23, FishRank);
                w.U32(o + 24, Score);
                w.U32(o + 28, SubmitTime);
                w.U8(o + 32, ContestRank);
                w.U8(o + 33, ResultCount);
                w.U8(o + 34, Share);
                w.U8(o + 35, DatasetB);
            }
        }

        public Status CurrentStatus = Status.Contesting;
        public byte Criteria;
        public Measure CurrentMeasure = Measure.Greatest;
        public ushort FishId;
        public uint StartTime, ChangeTime = 0xFFFFFFFF;
        public readonly int MaxFakeEntries;
        public readonly List<Entry> Entries = new List<Entry>();
        public readonly List<Entry> FakeEntries = new List<Entry>();

        public FishingContest(int maxFakeEntries) => MaxFakeEntries = maxFakeEntries;

        // CurrentFishingContest.isValid
        public bool IsValid => CurrentStatus <= Status.Closed && Criteria <= 2 && CurrentMeasure <= Measure.Smallest && StartTime > 0 && FishId != 0;

        // initNewContest runs InitNewContest when the database holds no contest
        public static FishingContest Load(Func<string, ResultSet> exec, int maxFakeEntries, Action<string> log, Action<FishingContest> initNewContest = null)
        {
            var f = new FishingContest(maxFakeEntries);
            if (f.LoadParameters(exec))
            {
                initNewContest?.Invoke(f);
                log?.Invoke("No Active Fishing Contest found in database. Initializing new one.");
            }
            f.LoadEntries(exec);
            f.BuildPlaceholderEntries();
            return f;
        }

        // true when the query found no contest
        private bool LoadParameters(Func<string, ResultSet> exec)
        {
            var r = exec("SELECT status, criteria, measure, fishid, starttime, nextstagetime FROM fishing_contest");
            if (r != null && r.Next())
            {
                CurrentStatus = (Status)r.Get<byte>("status");
                Criteria = r.Get<byte>("criteria");
                CurrentMeasure = (Measure)r.Get<byte>("measure");
                FishId = r.Get<ushort>("fishid");
                StartTime = r.Get<uint>("starttime");
                ChangeTime = r.Get<uint>("nextstagetime");
                return false;
            }
            return r != null;
        }

        private void LoadEntries(Func<string, ResultSet> exec)
        {
            var r = exec("SELECT c.charname AS charname, e.mjob AS mjob, e.sjob AS sjob, e.mlevel AS mlevel, e.slevel AS slevel, " +
                         "e.race AS race, e.allegiance AS allegiance, e.fishRank AS fishRank, e.score AS score, " +
                         "e.submitTime AS submitTime, e.contestRank AS contestRank, e.share AS share " +
                         "FROM fishing_contest_entries e LEFT JOIN chars c ON c.charid = e.charid ORDER BY contestRank");
            if (r == null) return;
            Entries.Clear();
            while (r.Next())
            {
                byte share = r.Get<byte>("share");
                Entries.Add(new Entry
                {
                    Name = r.Get<string>("charname") ?? "",
                    MJob = r.Get<byte>("mjob"),
                    SJob = r.Get<byte>("sjob"),
                    MLvl = r.Get<byte>("mlevel"),
                    SLvl = r.Get<byte>("slevel"),
                    Race = r.Get<byte>("race"),
                    Allegiance = r.Get<byte>("allegiance"),
                    FishRank = r.Get<byte>("fishRank"),
                    Score = r.Get<uint>("score"),
                    SubmitTime = r.Get<uint>("submitTime"),
                    ContestRank = r.Get<byte>("contestRank"),
                    Share = share,
                    DatasetB = share,
                });
            }
        }

        // Only while the results are presented: pads the table with fake fishers that score worse than everyone. The
        // loop runs to MAX_FAKE_ENTRIES (not just the number needed), as the C++ does.
        public void BuildPlaceholderEntries()
        {
            if (CurrentStatus != Status.Presenting) return;
            byte maxFake = (byte)MaxFakeEntries;
            byte numReal = RankEntryCount;
            if (numReal >= maxFake) return;

            int entriesNeeded = maxFake - numReal;
            int[] jobLevels = { 75, 60, 50, 40, 30 };
            for (int n = 1; n <= maxFake; n++)
            {
                FakeEntries.Add(new Entry
                {
                    Name = $" SmallFisher{n:00} ",
                    MJob = (byte)((n - 1) % 18 + 1),
                    MLvl = (byte)jobLevels[(n - 1) % 5],
                    Race = (byte)((n - 1) % 7 + 1),
                    Allegiance = (byte)((n - 1) % 3),
                    FishRank = 1, // Recruit
                    Score = (uint)(CurrentMeasure == Measure.Smallest ? 9999 - entriesNeeded + n : entriesNeeded + 1 - n),
                    ContestRank = (byte)(numReal + n),
                });
            }
        }

        // GetFishRankEntry: the real entries, then the fakes
        public Entry RankEntry(int position)
        {
            if (position < Entries.Count) return Entries[position];
            if (FakeEntries.Count > 0) return FakeEntries[(position - Entries.Count) % FakeEntries.Count];
            return null;
        }

        public Entry PlayerEntry(string name)
        {
            foreach (var e in Entries)
                if (e.Name == name) return e;
            return null;
        }

        // the client shows at most 100
        public byte RankEntryCount => (byte)Math.Min(Entries.Count, 100);
    }
}
