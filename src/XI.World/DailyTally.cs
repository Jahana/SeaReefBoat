// Port of world/daily_tally.cpp: midnight-JST daily tally point distribution.

using System;
using XI.Login;
using XI.Persistence;

namespace XI.World
{
    public sealed class DailyTallySettings
    {
        public bool Enable = false;   // main.ENABLE_DAILY_TALLY
        public int Limit = 10000;     // main.DAILY_TALLY_LIMIT
        public int Amount = 10;       // main.DAILY_TALLY_AMOUNT
    }

    public static class DailyTally
    {
        public static void UpdateDailyTallyPoints(IDatabase db, DailyTallySettings settings, Action<string> log = null)
        {
            log ??= _ => { };
            if (!settings.Enable) return;

            int limit = Math.Clamp(settings.Limit, ushort.MinValue, ushort.MaxValue);
            int amount = Math.Clamp(settings.Amount, ushort.MinValue, ushort.MaxValue);

            if (LoginDb.PreparedStmt(db, "UPDATE char_points SET char_points.daily_tally = LEAST(?, char_points.daily_tally + ?) " +
                                         "WHERE char_points.daily_tally > -1", limit, amount) == null)
                log("Failed to update daily tally points");
            else
                log("Distributed daily tally points");

            if (LoginDb.PreparedStmt(db, "DELETE FROM char_vars WHERE varname = 'gobbieBoxUsed'") == null)
                log("Failed to delete daily tally char_vars entries");
        }
    }
}
