using System;
using System.Collections.Generic;
using System.Linq;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // fishingcontest (fishingcontest.cpp) past the load: ProgressContest (Selbina's onZoneTick through
    // ProgressFishingContest, with AUTO_FISHING_CONTEST), ScoreContest, UpdateRankingHistory, the contest and entry
    // writes, SubmitFish / WithdrawFish / ClaimContestReward / GetFishingContestHistory, InitNewContest (the fish from
    // xi.fishingContest.selectContestFish, the criteria and measure rolled), and the luautils globals
    // GetFishingContest, InitNewFishingContest, SetContestParameters and ProgressFishingContest.
    public sealed partial class ZoneHost
    {
        // the fishing contest roll (xirand::GetRandomNumber(min, max), max exclusive)
        public Func<int, int, int> ContestRandom = (min, max) => Random.Shared.Next(min, max);

        private FishingContest LoadFishingContest() =>
            FishingContest.Load(sql => Exec(sql), (loader_.Stats?.Settings ?? new StatSettings()).MaxFakeEntries, log_,
                                f => { fishingContest_ = f; InitNewContest(f); });

        // ---- Lua -------------------------------------------------------------------------------------------------

        // xi.fishingContest.<fn>(args) for its number, or fallback when it is missing or fails
        private long ContestLua(string fn, long fallback, params double[] args)
        {
            if (lua_ == null) return fallback;
            string path = "xi.fishingContest." + fn;
            int top = lua_.Top;
            try
            {
                lua_.GetPath(path);
                if (lua_.IsNil(-1))
                {
                    log_($"fishingcontest:{fn}: function not found");
                    return fallback;
                }
                foreach (var a in args) lua_.Push(a);
                lua_.Call(args.Length, 1);
                return lua_.IsNumber(-1) ? lua_.ToIntegral(-1) : fallback;
            }
            catch (LuaException e)
            {
                ScriptError("fishingcontest:" + fn, e);
                return fallback;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // GetStageChangeTime: xi.fishingContest.getStageChangeTime(stage, stageStartTime), never (0xFFFFFFFF) on an error
        private uint ContestStageChangeTime(FishingContest.Status stage, uint stageStartTime) =>
            (uint)ContestLua("getStageChangeTime", 0xFFFFFFFF, (double)stage, stageStartTime);

        // ---- control ---------------------------------------------------------------------------------------------

        // ProgressContest: past the change time the next stage (HIATUS wraps to a new contest): RELEASING scores it and
        // writes the ranking history, PRESENTING builds the placeholder fishers; then the next change time,
        // xi.fishingContest.onStageProgress and the contest row
        private void ProgressContest()
        {
            var f = Contest;
            if (f.CurrentStatus == FishingContest.Status.Closed) return;
            uint now = (uint)UnixNow();
            if (now <= f.ChangeTime) return;

            var next = (FishingContest.Status)(((int)f.CurrentStatus + 1) % (int)FishingContest.Status.Closed);
            if (next == FishingContest.Status.Releasing)
            {
                ScoreContest(f);
                UpdateRankingHistory(f);
            }
            if (next == FishingContest.Status.Contesting) InitNewContest(f);
            else f.CurrentStatus = next;
            if (next == FishingContest.Status.Presenting)
            {
                f.FakeEntries.Clear();
                f.BuildPlaceholderEntries();
            }
            f.ChangeTime = ContestStageChangeTime(next, now);
            ContestLua("onStageProgress", 0, (double)next);
            WriteContestData(f);
        }

        // UpdateRankingHistory: each placed entry's char gets +1 in its column (1st, 2nd, 3rd, 4th-10th)
        private void UpdateRankingHistory(FishingContest f)
        {
            foreach (var entry in f.Entries)
            {
                uint charId = CharIdFromName(entry.Name);
                if (charId == 0) continue;
                int group = entry.ContestRank > 0 && entry.ContestRank <= 4 ? entry.ContestRank : entry.ContestRank > 4 && entry.ContestRank <= 10 ? 4 : 0;
                if (group == 0) continue;
                if (Exec($"INSERT INTO char_fishing_contest_history (charid, contest_rank_{group}) VALUES (?, 1) " +
                         $"ON DUPLICATE KEY UPDATE contest_rank_{group} = contest_rank_{group} + 1", BoundValue.Of(charId)) == null)
                    log_($"Unable to update player [{entry.Name}] fishing reward history.");
            }
        }

        // ScoreContest: sorted by score (the measure's way), ties to the earlier submission; a score equal to the
        // running one shares its rank (the tally starts at 0), share = how many entries hold that score; each written
        private void ScoreContest(FishingContest f)
        {
            var sorted = f.CurrentMeasure == FishingContest.Measure.Greatest
                ? f.Entries.OrderByDescending(e => e.Score).ThenBy(e => e.SubmitTime).ToList()
                : f.Entries.OrderBy(e => e.Score).ThenBy(e => e.SubmitTime).ToList();
            f.Entries.Clear();
            f.Entries.AddRange(sorted);

            byte contestRank = 1, prevRank = 1;
            uint score = 0;
            foreach (var entry in f.Entries)
            {
                if (entry.Score != score)
                {
                    entry.ContestRank = contestRank;
                    prevRank = contestRank;
                    score = entry.Score;
                }
                else entry.ContestRank = prevRank;
                entry.Share = (byte)f.Entries.Count(e => e.Score == score);
                entry.DatasetB = entry.Share;
                entry.ResultCount = f.RankEntryCount;
                contestRank++;
                WriteContestEntryData(f, entry);
            }
        }

        // InitNewContest(): the fish from Lua, the criteria (size / weight / both) and measure (greatest / smallest)
        // rolled; nothing when the Lua fails
        private void InitNewContest(FishingContest f)
        {
            long fish = ContestLua("selectContestFish", -1);
            if (fish < 0) return;
            byte criteria = (byte)ContestRandom(0, 3);
            byte measure = (byte)ContestRandom(0, 2);
            InitNewContest(f, (ushort)fish, criteria, measure);
        }

        // InitNewContest(fish, criteria, measure): the old contest and its entries gone (memory and database), the new
        // one started now, CONTESTING, and inserted
        private void InitNewContest(FishingContest f, ushort fishId, byte criteria, byte measure)
        {
            if (Exec("DELETE FROM fishing_contest") == null)
            {
                log_("Error removing contest data.");
                return;
            }
            f.Entries.Clear();
            if (Exec("DELETE FROM fishing_contest_entries") == null)
            {
                log_("Error removing contest entry data.");
                return;
            }
            f.FishId = fishId;
            f.Criteria = Math.Min(criteria, (byte)2);
            f.CurrentMeasure = (FishingContest.Measure)Math.Min(measure, (byte)1);
            f.StartTime = (uint)UnixNow();
            f.ChangeTime = ContestStageChangeTime(FishingContest.Status.Contesting, f.StartTime);
            f.CurrentStatus = FishingContest.Status.Contesting;
            WriteContestData(f, initial: true);
        }

        // ---- data ------------------------------------------------------------------------------------------------

        // WriteContestData / WriteInitialContestData: the contest row updated (or inserted), not for invalid data
        private bool WriteContestData(FishingContest f, bool initial = false)
        {
            if (!f.IsValid)
            {
                log_("Contest data invalid.  Ignoring database write.");
                return false;
            }
            var args = new[]
            {
                BoundValue.Of((byte)f.CurrentStatus), BoundValue.Of(f.Criteria), BoundValue.Of((byte)f.CurrentMeasure),
                BoundValue.Of(f.FishId), BoundValue.Of(f.StartTime), BoundValue.Of(f.ChangeTime),
            };
            var r = initial
                ? Exec("INSERT INTO fishing_contest VALUES (?, ?, ?, ?, ?, ?)", args)
                : Exec("UPDATE fishing_contest SET status = ?, criteria = ?, measure = ?, fishid = ?, starttime = ?, nextstagetime = ?", args);
            if (r == null)
            {
                log_("Error writing fishing contest data to database.");
                return false;
            }
            return true;
        }

        // WriteContestEntryData: the entry's row by its char's name, refused once the results are presented
        private bool WriteContestEntryData(FishingContest f, FishingContest.Entry entry)
        {
            if (f.CurrentStatus >= FishingContest.Status.Presenting)
            {
                log_($"Attempted to add entry data after contest closure. Player: {entry.Name}");
                return false;
            }
            uint charId = CharIdFromName(entry.Name);
            if (charId == 0)
            {
                log_($"Failed to look up {entry.Name}");
                return false;
            }
            var r = Exec("INSERT INTO fishing_contest_entries SET charid = ?, mjob = ?, sjob = ?, mlevel = ?, slevel = ?, race = ?, " +
                         "allegiance = ?, fishRank = ?, score = ?, submitTime = ?, contestRank = ?, share = ? ON DUPLICATE KEY UPDATE " +
                         "mjob = VALUES(mjob), sjob = VALUES(sjob), mlevel = VALUES(mlevel), slevel = VALUES(slevel), race = VALUES(race), " +
                         "allegiance = VALUES(allegiance), fishRank = VALUES(fishRank), score = VALUES(score), " +
                         "submitTime = VALUES(submitTime), contestRank = VALUES(contestRank), share = VALUES(share)",
                         BoundValue.Of(charId), BoundValue.Of(entry.MJob), BoundValue.Of(entry.SJob), BoundValue.Of(entry.MLvl),
                         BoundValue.Of(entry.SLvl), BoundValue.Of(entry.Race), BoundValue.Of(entry.Allegiance), BoundValue.Of(entry.FishRank),
                         BoundValue.Of(entry.Score), BoundValue.Of(entry.SubmitTime), BoundValue.Of(entry.ContestRank), BoundValue.Of(entry.Share));
            if (r == null)
            {
                log_($"Error writing fishing contest data to database {entry.Name} ({charId}).");
                return false;
            }
            return true;
        }

        // ---- the player side -------------------------------------------------------------------------------------

        // SubmitFish: the char's earlier entry replaced by one with its jobs, levels, race, allegiance, fishing rank and
        // the score, at the Vana'diel time
        private bool SubmitContestFish(CharData c, uint score)
        {
            var f = Contest;
            int old = f.Entries.FindIndex(e => e.Name == c.Name);
            if (old >= 0) f.Entries.RemoveAt(old);
            var entry = new FishingContest.Entry
            {
                Name = c.Name.Length > 15 ? c.Name.Substring(0, 15) : c.Name,
                MJob = c.MJob,
                SJob = c.SJob,
                MLvl = c.MLvl,
                SLvl = c.SLvl,
                Race = c.MainLook.Race,
                Allegiance = (byte)c.Allegiance,
                FishRank = c.RealSkillRank[Skill.Fishing],
                Score = score,
                SubmitTime = VanadielTimestampNow(),
            };
            if (!WriteContestEntryData(f, entry)) return false;
            f.Entries.Add(entry);
            return true;
        }

        // WithdrawFish: the char's row and entry gone
        private bool WithdrawContestFish(CharData c)
        {
            var f = Contest;
            if (f.PlayerEntry(c.Name) == null) return false;
            if (Exec("DELETE FROM fishing_contest_entries WHERE charid = ?", BoundValue.Of(c.Id)) == null)
            {
                log_("Failed to remove entry from fishing entry database.");
                return false;
            }
            int i = f.Entries.FindIndex(e => e.Name == c.Name);
            if (i < 0) return false;
            f.Entries.RemoveAt(i);
            return true;
        }

        // ClaimContestReward: only while presenting, the char's entry marked claimed
        private bool ClaimContestReward(CharData c)
        {
            if (Contest.CurrentStatus != FishingContest.Status.Presenting)
            {
                log_($"Attempted to issue reward to player [{c.Name}] outside of 'Presenting' status.");
                return false;
            }
            if (Exec("UPDATE fishing_contest_entries SET claimed = 1 WHERE charid = ?", BoundValue.Of(c.Id)) == null)
            {
                log_($"Unable to remove award on fishing contest for player {c.Name}.");
                return false;
            }
            return true;
        }

        // GetFishingContestHistory: the char's placings, zeros without a row
        private (ushort Rank1, ushort Rank2, ushort Rank3, ushort Rank4) ContestHistory(CharData c)
        {
            var r = Exec("SELECT contest_rank_1, contest_rank_2, contest_rank_3, contest_rank_4 FROM char_fishing_contest_history WHERE charid = ?",
                         BoundValue.Of(c.Id));
            return r != null && r.Next()
                ? (r.Get<ushort>("contest_rank_1"), r.Get<ushort>("contest_rank_2"), r.Get<ushort>("contest_rank_3"), r.Get<ushort>("contest_rank_4"))
                : ((ushort)0, (ushort)0, (ushort)0, (ushort)0);
        }

        // ---- bindings --------------------------------------------------------------------------------------------

        private void RegisterFishingContestGlobals()
        {
            // the current contest: status, criteria, measure, fishid, starttime, changetime
            lua_.SetGlobal("GetFishingContest", l =>
            {
                var f = Contest;
                l.NewTable(0, 6);
                void Set(string k, double v) { l.Push(v); l.SetField(-2, k); }
                Set("status", (double)f.CurrentStatus);
                Set("criteria", f.Criteria);
                Set("measure", (double)f.CurrentMeasure);
                Set("fishid", f.FishId);
                Set("starttime", f.StartTime);
                Set("changetime", f.ChangeTime);
                return 1;
            });
            lua_.SetGlobal("InitNewFishingContest", l =>
            {
                InitNewContest(Contest);
                return 0;
            });
            // SetContestParameters(fishId, measure, criteria): memory only, as C++
            lua_.SetGlobal("SetContestParameters", l =>
            {
                var f = Contest;
                f.FishId = (ushort)l.ToIntArg(1);
                f.CurrentMeasure = (FishingContest.Measure)(byte)l.ToIntArg(2);
                f.Criteria = (byte)l.ToIntArg(3);
                return 0;
            });
            lua_.SetGlobal("ProgressFishingContest", l =>
            {
                ProgressContest();
                return 0;
            });
        }

        private void RegisterFishingContestBindings(Dictionary<string, LuaFunction> m)
        {
            m["submitContestFish"] = l =>
            {
                if (Entity(l) is CharData c) SubmitContestFish(c, l.ToUIntArg(2));
                return 0;
            };
            m["withdrawContestFish"] = l =>
            {
                if (Entity(l) is CharData c) WithdrawContestFish(c);
                return 0;
            };
            // { rank1, rank2, rank3, rank4to10 }
            m["getContestRankHistory"] = l =>
            {
                var h = Entity(l) is CharData c ? ContestHistory(c) : default;
                l.NewTable(0, 4);
                l.Push((double)h.Rank1);
                l.SetField(-2, "rank1");
                l.Push((double)h.Rank2);
                l.SetField(-2, "rank2");
                l.Push((double)h.Rank3);
                l.SetField(-2, "rank3");
                l.Push((double)h.Rank4);
                l.SetField(-2, "rank4to10");
                return 1;
            };
            m["claimContestReward"] = l =>
            {
                if (Entity(l) is CharData c) ClaimContestReward(c);
                return 0;
            };
        }
    }
}
