using System;
using System.Collections.Generic;
using XI.Persistence;

namespace XI.Zone
{
    // CTrait (trait.h) and CBlueTrait (blue_trait.h): a job trait row. A blue trait (a blue_traits row) has level 0, its
    // tier as the rank, and the category and points the set blue magic must reach.
    public sealed class Trait
    {
        public ushort Id;
        public byte Job, Level, Rank;
        public Mod Mod;
        public short Value;
        public uint MeritId;

        // CBlueTrait
        public byte Category, Points;
        public bool JobPointsOnly;
    }

    // traits::LoadTraitsList / GetTraits: the traits by job, each in traitid order, the higher rank first; then the blue
    // traits on BLU's list in tier order. Not ported: ClearTraitsList (the table lives as long as the zone host).
    public sealed class TraitTable
    {
        public const int MaxTraitId = 139; // MAX_TRAIT_ID

        private readonly List<Trait>[] byJob_ = new List<Trait>[CharData.MaxJobs];

        public TraitTable()
        {
            for (int j = 0; j < byJob_.Length; j++) byJob_[j] = new List<Trait>();
        }

        // traits::GetTraits: nothing past MAX_JOBTYPE
        public IReadOnlyList<Trait> ForJob(int job, Action<string> log = null)
        {
            if (job < 0 || job >= byJob_.Length)
            {
                log?.Invoke($"JobID ({job}) exceeds MAX_JOBTYPE.");
                return null;
            }
            return byJob_[job];
        }

        public static TraitTable Load(IDatabase db, Func<string, bool> isContentEnabled)
        {
            var table = new TraitTable();
            var rset = db.Execute("SELECT traitid, job, level, `rank`, modifier, value, content_tag, meritid FROM traits WHERE traitid < ? " +
                                  "ORDER BY job, traitid ASC, `rank` DESC", new List<BoundValue> { BoundValue.Of(MaxTraitId) });
            while (rset != null && rset.Next())
            {
                if (!isContentEnabled(rset.IsNull("content_tag") ? "" : rset.Get<string>("content_tag"))) continue;
                var t = new Trait
                {
                    Id = rset.Get<ushort>("traitid"),
                    Job = rset.Get<byte>("job"),
                    Level = rset.Get<byte>("level"),
                    Rank = rset.Get<byte>("rank"),
                    Mod = (Mod)rset.Get<ushort>("modifier"),
                    Value = rset.Get<short>("value"),
                    MeritId = rset.Get<uint>("meritid"),
                };
                if (t.Job < table.byJob_.Length) table.byJob_[t.Job].Add(t);
            }

            rset = db.Execute("SELECT trait_category, trait_points_needed, traitid, modifier, value, tier, job_points_only FROM blue_traits " +
                              "WHERE traitid < ? ORDER BY tier ASC", new List<BoundValue> { BoundValue.Of(MaxTraitId) });
            while (rset != null && rset.Next())
            {
                table.byJob_[MobStats.JobBlu].Add(new Trait
                {
                    Category = rset.Get<byte>("trait_category"),
                    Id = rset.Get<byte>("traitid"),
                    JobPointsOnly = rset.Get<bool>("job_points_only"),
                    Job = MobStats.JobBlu,
                    Rank = rset.Get<byte>("tier"),
                    Points = rset.Get<byte>("trait_points_needed"),
                    Mod = (Mod)rset.Get<ushort>("modifier"),
                    Value = rset.Get<short>("value"),
                });
            }
            return table;
        }
    }
}
