// src/search/data_loader.cpp CDataLoader: the search server's queries (players, party and linkshell lists, search
// comments, auction house lists and history) and the auction house expiry.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XI.Persistence;

namespace XI.Search
{
    public sealed class SearchDataLoader
    {
        private const uint AcceptingRequestsFlag = 0x10000000;
        private const byte JobMon = 23; // xi::Job::MON

        private readonly IDatabase _db;
        private readonly Action<string> _log;

        // search.OMIT_NO_HISTORY
        public bool OmitNoHistory;

        // earth_time::timestamp(); tests may pin it
        public Func<uint> Now = () => (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public SearchDataLoader(IDatabase db, Action<string> log = null)
        {
            _db = db;
            _log = log ?? (_ => { });
        }

        private ResultSet Stmt(string query, params object[] args)
        {
            var bound = args.Select(a => a switch
            {
                uint v => BoundValue.Of(v),
                int v => BoundValue.Of(v),
                ushort v => BoundValue.Of(v),
                byte v => BoundValue.Of(v),
                bool v => BoundValue.Of(v),
                string v => BoundValue.Of(v),
                byte[] v => BoundValue.Of(v),
                _ => throw new NotSupportedException($"unbindable argument type {a?.GetType()}"),
            }).ToArray();
            return _db.Execute(query, bound);
        }

        private static byte[] Bytes(ResultSet rset, string key) => Encoding.UTF8.GetBytes(rset.Get<string>(key));

        // GetAHItemHistory: the auction house sale history for a given item
        public List<AuctionHouseHistory> GetAHItemHistory(ushort itemId, bool stack)
        {
            var historyList = new List<AuctionHouseHistory>();
            var rset = Stmt("SELECT sale, sell_date, seller_name, buyer_name " +
                            "FROM auction_house " +
                            "WHERE itemid = ? AND stack = ? AND buyer_name IS NOT NULL " +
                            "ORDER BY sell_date DESC " +
                            "LIMIT 10",
                            itemId, stack);
            if (rset != null && rset.RowsCount() > 0)
            {
                while (rset.Next())
                {
                    historyList.Add(new AuctionHouseHistory
                    {
                        Price = rset.Get<uint>("sale"),
                        Data = rset.Get<uint>("sell_date"),
                        Name1 = Bytes(rset, "seller_name"),
                        Name2 = Bytes(rset, "buyer_name"),
                    });
                }
                historyList.Reverse();
            }
            return historyList;
        }

        private sealed class SortableItem
        {
            public AuctionHouseItem Item;
            public long Level;
            public long Damage;
            public long Delay;
            public byte[] SortName;
        }

        // GetAHItemsToCategory: the list of items sold in this category
        public List<AuctionHouseItem> GetAHItemsToCategory(byte ahCategoryId, byte[] sortKeys)
        {
            var rset = OmitNoHistory
                ? Stmt("SELECT item_basic.itemid, item_basic.stackSize, COUNT(*)-SUM(stack), SUM(stack), " +
                       "CAST(item_basic.sortname AS CHAR(255)) AS sortname, item_equipment.level, item_weapon.dmg, item_weapon.delay " +
                       "FROM (SELECT item_basic.* FROM item_basic " +
                       "INNER JOIN auction_house_items ON item_basic.itemid = auction_house_items.itemid) AS item_basic " +
                       "LEFT JOIN auction_house ON item_basic.itemId = auction_house.itemid AND auction_house.buyer_name IS NULL " +
                       "LEFT JOIN item_equipment ON item_basic.itemid = item_equipment.itemid " +
                       "LEFT JOIN item_weapon ON item_basic.itemid = item_weapon.itemid " +
                       "WHERE aH = ? " +
                       "GROUP BY item_basic.itemid " +
                       "ORDER BY item_basic.itemid",
                       ahCategoryId)
                : Stmt("SELECT item_basic.itemid, item_basic.stackSize, COUNT(*)-SUM(stack), SUM(stack), " +
                       "CAST(item_basic.sortname AS CHAR(255)) AS sortname, item_equipment.level, item_weapon.dmg, item_weapon.delay " +
                       "FROM item_basic " +
                       "LEFT JOIN auction_house ON item_basic.itemId = auction_house.itemid AND auction_house.buyer_name IS NULL " +
                       "LEFT JOIN item_equipment ON item_basic.itemid = item_equipment.itemid " +
                       "LEFT JOIN item_weapon ON item_basic.itemid = item_weapon.itemid " +
                       "WHERE aH = ? " +
                       "GROUP BY item_basic.itemid " +
                       "ORDER BY item_basic.itemid",
                       ahCategoryId);

            var rows = new List<SortableItem>();
            if (rset != null && rset.RowsCount() > 0)
            {
                while (rset.Next())
                {
                    var item = new AuctionHouseItem
                    {
                        ItemID = rset.Get<ushort>("itemid"),
                        SingleAmount = rset.GetOrDefault<uint>("COUNT(*)-SUM(stack)", 0),
                        StackAmount = rset.GetOrDefault<uint>("SUM(stack)", 0),
                        Category = ahCategoryId,
                    };
                    if (rset.Get<uint>("stackSize") == 1) item.StackAmount = unchecked((uint)-1);
                    // INT64_MIN stands in for NULL, which MariaDB puts last in a DESC sort; delay is signed, so -1 is a real value.
                    rows.Add(new SortableItem
                    {
                        Item = item,
                        Level = rset.GetOrDefault("level", long.MinValue),
                        Damage = rset.GetOrDefault("dmg", long.MinValue),
                        Delay = rset.GetOrDefault("delay", long.MinValue),
                        SortName = Bytes(rset, "sortname"),
                    });
                }
            }

            int CompareKey(SortableItem lhs, SortableItem rhs, byte key)
            {
                switch (key)
                {
                    case 2: return rhs.Level.CompareTo(lhs.Level);
                    case 5: return rhs.Damage.CompareTo(lhs.Damage);
                    case 6: return rhs.Delay.CompareTo(lhs.Delay);
                    case 9:
                    {
                        // utf8mb4_general_ci weighs a letter as its capital, so 'a' sorts before '_'.
                        static int Upper(byte c) => c >= 'a' && c <= 'z' ? c - 32 : c;
                        int n = Math.Min(lhs.SortName.Length, rhs.SortName.Length);
                        for (int i = 0; i < n; i++)
                        {
                            int d = Upper(lhs.SortName[i]).CompareTo(Upper(rhs.SortName[i]));
                            if (d != 0) return d;
                        }
                        return lhs.SortName.Length.CompareTo(rhs.SortName.Length);
                    }
                    default: return 0;
                }
            }

            // Stable (OrderBy is), so the itemid order from the query breaks the remaining ties.
            var comparer = Comparer<SortableItem>.Create((lhs, rhs) =>
            {
                foreach (var key in sortKeys)
                {
                    int order = CompareKey(lhs, rhs, key);
                    if (order != 0) return order;
                }
                return 0;
            });
            return rows.OrderBy(r => r, comparer).Select(r => r.Item).ToList();
        }

        // GetAHItemFromItemID: a single item including category and how many are listed
        public AuctionHouseItem GetAHItemFromItemID(ushort itemId)
        {
            var item = new AuctionHouseItem { ItemID = itemId };
            var rset = Stmt("SELECT aH, COUNT(*)-SUM(stack), SUM(stack) " +
                            "FROM item_basic " +
                            "LEFT JOIN auction_house ON item_basic.itemId = auction_house.itemid AND auction_house.buyer_name IS NULL " +
                            "LEFT JOIN item_equipment ON item_basic.itemid = item_equipment.itemid " +
                            "LEFT JOIN item_weapon ON item_basic.itemid = item_weapon.itemid " +
                            "WHERE item_basic.itemid = ?",
                            itemId);
            if (rset != null && rset.RowsCount() > 0 && rset.Next())
            {
                item.Category = rset.Get<ushort>("aH");
                item.SingleAmount = rset.GetOrDefault<uint>("COUNT(*)-SUM(stack)", 0);
                item.StackAmount = rset.GetOrDefault<uint>("SUM(stack)", 0);
            }
            return item;
        }

        // GetPlayersCount: the number of active players in the world
        public uint GetPlayersCount(SearchRequest sr)
        {
            byte jobid = sr.JobId;
            var rset = jobid > 0 && jobid < 21
                ? Stmt("SELECT COUNT(*) FROM accounts_sessions LEFT JOIN char_stats USING (charid) WHERE mjob = ?", jobid)
                : Stmt("SELECT COUNT(*) FROM accounts_sessions");
            if (rset != null && rset.RowsCount() > 0 && rset.Next()) return rset.Get<uint>("COUNT(*)");
            return 0;
        }

        // SAVE_CONF (mmo.h) bits of chars.settings
        private static bool InviteFlg(uint s) => (s & 0x01) != 0;
        private static bool AwayFlg(uint s) => (s & 0x02) != 0;
        private static bool AnonymityFlg(uint s) => (s & 0x04) != 0;
        private static bool MentorFlg(uint s) => (s & (1u << 25)) != 0;
        private static bool RecruitFlg(uint s) => (s & (1u << 29)) != 0;

        private byte Rank(ResultSet rset, byte nation)
        {
            switch (nation)
            {
                case 0: return rset.Get<byte>("rank_sandoria");
                case 1: return rset.Get<byte>("rank_bastok");
                case 2: return rset.Get<byte>("rank_windurst");
                default:
                    _log($"Inconsistent player nation allegiance : {nation}");
                    return 0;
            }
        }

        // GetPlayersList: the characters found in the world that match the search request (job 0 for none);
        // count gets the total when there is one
        public List<SearchEntity> GetPlayersList(SearchRequest sr, ref int count)
        {
            var playersList = new List<SearchEntity>();
            if (sr.FriendsOnly && sr.CharacterIds.Count == 0) return playersList;
            if (sr.RequestListOnly && sr.CharacterNames.Count == 0) return playersList;

            // Bound as a JSON array for JSON_TABLE; performance is comparable or better than a formatted IN list.
            string characterIds = "[" + string.Join(",", sr.CharacterIds) + "]";
            // Unused zone slots repeat the first zone, which leaves the IN list matches unchanged.
            var zones = new ushort[10];
            Array.Copy(sr.ZoneId, zones, zones.Length);
            int firstZero = Array.IndexOf(zones, (ushort)0);
            if (firstZero >= 0) for (int i = firstZero; i < zones.Length; i++) zones[i] = zones[0];

            var args = new List<object> { sr.JobId, sr.JobId, zones[0] };
            args.AddRange(zones.Select(z => (object)z));
            args.AddRange(zones.Select(z => (object)z));
            args.AddRange(new object[] { sr.CommentType, sr.CommentType, characterIds, characterIds });
            var rset = Stmt("SELECT charid, partyid, charname, pos_zone, pos_prevzone, nation, rank_sandoria, rank_bastok, unity_leader, " +
                            "rank_windurst, race, mjob, sjob, mlvl, slvl, languages, settings, seacom_type, disconnecting, gmHiddenEnabled, muted, " +
                            "linkshellid1, linkshellid2 " +
                            "FROM accounts_sessions " +
                            "LEFT JOIN accounts_parties USING (charid) " +
                            "LEFT JOIN chars USING (charid) " +
                            "LEFT JOIN char_look USING (charid) " +
                            "LEFT JOIN char_stats USING (charid) " +
                            "LEFT JOIN char_profile USING(charid) " +
                            "LEFT JOIN char_flags USING(charid) " +
                            "WHERE charname IS NOT NULL " +
                            "AND (? = 0 OR mjob = ?) " +
                            "AND (? = 0 OR pos_zone IN (?, ?, ?, ?, ?, ?, ?, ?, ?, ?) OR (pos_zone = 0 AND pos_prevzone IN (?, ?, ?, ?, ?, ?, ?, ?, ?, ?))) " +
                            "AND (? = 0 OR (seacom_type & 0xF0) = ?) " +
                            "AND (? = '[]' OR charid IN (SELECT id FROM JSON_TABLE(?, '$[*]' COLUMNS (id INT UNSIGNED PATH '$')) AS ids)) " +
                            "ORDER BY charname ASC",
                            args.ToArray());
            if (rset == null || rset.RowsCount() == 0) return playersList;

            int totalResults = 0;   // gives ALL matching criteria (total)
            int visibleResults = 0; // capped
            while (rset.Next())
            {
                var player = new SearchEntity { Name = Bytes(rset, "charname") };
                if (sr.RequestListOnly && !sr.CharacterNames.Any(n => n.AsSpan().SequenceEqual(player.Name))) continue;

                player.Id = rset.Get<uint>("charid");
                player.Zone = rset.Get<ushort>("pos_zone");
                player.PrevZone = rset.Get<ushort>("pos_prevzone");
                player.Nation = rset.Get<byte>("nation");
                player.Mjob = rset.Get<byte>("mjob");
                player.Sjob = rset.Get<byte>("sjob");
                player.Mlvl = rset.Get<byte>("mlvl");
                player.Slvl = rset.Get<byte>("slvl");
                player.Race = rset.Get<byte>("race");
                player.Rank = Rank(rset, player.Nation);

                uint settings = rset.Get<uint>("settings");
                player.Zone = player.Zone == 0 ? player.PrevZone : player.Zone;
                player.Languages = rset.Get<byte>("languages");
                player.Mentor = MentorFlg(settings);
                player.LinkshellId1 = rset.Get<uint>("linkshellid1");
                player.LinkshellId2 = rset.Get<uint>("linkshellid2");
                player.SeacomType = rset.Get<byte>("seacom_type");
                player.Disconnecting = rset.Get<bool>("disconnecting");
                player.GmHidden = rset.Get<bool>("gmHiddenEnabled");
                player.Muted = rset.Get<bool>("muted");
                player.UnityLeader = rset.Get<byte>("unity_leader");
                uint partyid = rset.GetOrDefault<uint>("partyid", 0);

                if (player.Mentor) player.Flags1 |= 0x0001;
                if (partyid == player.Id) player.Flags1 |= 0x0008;
                if (player.SeacomType != 0) player.Flags1 |= 0x0010;
                if (AwayFlg(settings)) player.Flags1 |= 0x0100;
                if (player.Disconnecting) player.Flags1 |= 0x0800;
                if (partyid != 0) player.Flags1 |= 0x2000;
                if (AnonymityFlg(settings)) player.Flags1 |= 0x4000;
                if (InviteFlg(settings)) player.Flags1 |= 0x8000;
                if (player.Muted) player.Flags1 |= 0x20000000;
                if (RecruitFlg(settings)) player.Flags1 |= AcceptingRequestsFlag;
                player.Flags2 = player.Flags1;

                if (player.Mjob == JobMon || player.Sjob == JobMon)
                {
                    player.Mjob = 0;
                    player.Sjob = 0;
                }

                // filter by linkshell ID
                if (sr.LsId.HasValue)
                {
                    uint searchedLsId = sr.LsId.Value;
                    // lsId of 0 is automatic fail, it means the requester did not have a linkshell equipped
                    if (searchedLsId == 0) continue;
                    // Current player does not match the given LS ID
                    if (player.LinkshellId1 != searchedLsId && player.LinkshellId2 != searchedLsId) continue;
                }

                // filter anon players if job/nation/race/rank/level search
                if ((player.Flags1 & 0x4000) != 0 &&
                    (sr.JobId > 0 || sr.Nation != 255 || sr.Race != 255 || sr.MinRank > 0 || sr.MaxRank > 0 || sr.MinLvl > 0 || sr.MaxLvl > 0))
                {
                    continue;
                }

                // filter by job
                if (sr.JobId > 0 && sr.JobId != player.Mjob) continue;

                // filter by nation
                if (sr.Nation != 255 && sr.Nation != player.Nation) continue;

                // filter by race
                if (sr.Race != 255)
                {
                    // hume (male/female)
                    if (sr.Race == 0 && player.Race != 1 && player.Race != 2) continue;
                    // elvaan (male/female)
                    if (sr.Race == 1 && player.Race != 3 && player.Race != 4) continue;
                    // tarutaru (male/female)
                    if (sr.Race == 2 && player.Race != 5 && player.Race != 6) continue;
                    // mithra (female only)
                    else if (sr.Race == 3 && player.Race != 7) continue;
                    // galka (male only)
                    else if (sr.Race == 4 && player.Race != 8) continue;
                }

                // filter by rank
                if (sr.MinRank > 0 && sr.MaxRank >= sr.MinRank)
                {
                    if (player.Rank < sr.MinRank || player.Rank > sr.MaxRank) continue;
                }

                // filter by flag (away, seek party etc.)
                if (sr.Flags != 0)
                {
                    if ((sr.Flags & AcceptingRequestsFlag) != 0)
                    {
                        if ((player.Flags2 & AcceptingRequestsFlag) == 0) continue;
                    }
                    // Check if unity ID is set (bits 22+)
                    else if ((sr.Flags >> 22) != 0)
                    {
                        if (player.UnityLeader != (sr.Flags >> 22)) continue;
                    }
                    // Normal bitwise check for other flags (bits 0-21)
                    else if ((player.Flags2 & sr.Flags) == 0)
                    {
                        continue;
                    }
                }

                // filter by level
                if (sr.MinLvl > 0 && sr.MaxLvl >= sr.MinLvl)
                {
                    if (player.Mlvl < sr.MinLvl || player.Mlvl > sr.MaxLvl) continue;
                }

                // filter by name
                if (sr.NameLen > 0)
                {
                    // can't be this name, too long
                    if (sr.NameLen > player.Name.Length) continue;
                    bool validName = true;
                    for (int i = 0; i < sr.NameLen; i++)
                    {
                        // convert to lowercase for both
                        byte searched = i < sr.Name.Length ? sr.Name[i] : (byte)0;
                        if (Lower(searched) != Lower(player.Name[i]))
                        {
                            validName = false;
                            break;
                        }
                    }
                    if (!validName) continue;
                }

                if (player.GmHidden) continue;

                if (visibleResults < 40)
                {
                    playersList.Add(player);
                    visibleResults++;
                }
                totalResults++;
            }
            if (totalResults > 0) count = totalResults;
            _log($"Found {totalResults} results, displaying {visibleResults}");
            return playersList;
        }

        private static int Lower(byte c) => c >= 'A' && c <= 'Z' ? c + 32 : c;

        private SearchEntity GroupEntity(ResultSet rset)
        {
            var player = new SearchEntity
            {
                Name = Bytes(rset, "charname"),
                Id = rset.Get<uint>("charid"),
                Zone = rset.Get<ushort>("pos_zone"),
                Nation = rset.Get<byte>("nation"),
                Mjob = rset.Get<byte>("mjob"),
                Sjob = rset.Get<byte>("sjob"),
                Mlvl = rset.Get<byte>("mlvl"),
                Slvl = rset.Get<byte>("slvl"),
                Race = rset.Get<byte>("race"),
            };
            player.Rank = Rank(rset, player.Nation);
            return player;
        }

        // GetPartyList: the characters in a given party/alliance group
        public List<SearchEntity> GetPartyList(uint partyId, uint allianceId)
        {
            var partyList = new List<SearchEntity>();
            var rset = Stmt("SELECT charid, partyid, charname, pos_zone, nation, rank_sandoria, rank_bastok, rank_windurst, race, settings, mjob, sjob, mlvl, slvl, languages, seacom_type, disconnecting " +
                            "FROM accounts_sessions " +
                            "LEFT JOIN accounts_parties USING(charid) " +
                            "LEFT JOIN chars USING(charid) " +
                            "LEFT JOIN char_look USING(charid) " +
                            "LEFT JOIN char_stats USING(charid) " +
                            "LEFT JOIN char_profile USING(charid) " +
                            "LEFT JOIN char_flags USING(charid) " +
                            "WHERE IF (allianceid <> 0, allianceid IN (SELECT allianceid FROM accounts_parties WHERE charid = ?) , partyid = ?) " +
                            "ORDER BY charname ASC " +
                            "LIMIT 64",
                            allianceId == 0 ? partyId : allianceId,
                            partyId == 0 ? allianceId : partyId);
            if (rset == null || rset.RowsCount() == 0) return partyList;
            while (rset.Next())
            {
                var player = GroupEntity(rset);
                uint settings = rset.Get<uint>("settings");
                player.Languages = rset.Get<byte>("languages");
                player.Mentor = MentorFlg(settings);
                player.SeacomType = rset.Get<byte>("seacom_type");
                player.Disconnecting = rset.Get<bool>("disconnecting");
                if (player.Mentor) player.Flags1 |= 0x0001;
                if (partyId == player.Id) player.Flags1 |= 0x0008;
                if (player.SeacomType != 0) player.Flags1 |= 0x0010;
                if (AwayFlg(settings)) player.Flags1 |= 0x0100;
                if (player.Disconnecting) player.Flags1 |= 0x0800;
                if (partyId != 0) player.Flags1 |= 0x2000;
                if (AnonymityFlg(settings)) player.Flags1 |= 0x4000;
                if (InviteFlg(settings)) player.Flags1 |= 0x8000;
                player.Flags2 = player.Flags1;
                partyList.Add(player);
            }
            return partyList;
        }

        // GetLinkshellList: the characters in a given linkshell
        public List<SearchEntity> GetLinkshellList(uint linkshellId)
        {
            var linkshellList = new List<SearchEntity>();
            var rset = Stmt("SELECT charid, partyid, charname, pos_zone, nation, rank_sandoria, rank_bastok, rank_windurst, race, settings, mjob, sjob, " +
                            "mlvl, slvl, linkshellid1, linkshellid2, " +
                            "linkshellrank1, linkshellrank2, disconnecting " +
                            "FROM accounts_sessions " +
                            "LEFT JOIN accounts_parties USING (charid) " +
                            "LEFT JOIN chars USING (charid) " +
                            "LEFT JOIN char_look USING (charid) " +
                            "LEFT JOIN char_stats USING (charid) " +
                            "LEFT JOIN char_profile USING(charid) " +
                            "LEFT JOIN char_flags USING(charid) " +
                            "WHERE linkshellid1 = ? OR linkshellid2 = ? " +
                            "ORDER BY charname ASC " +
                            "LIMIT 64",
                            linkshellId, linkshellId);
            if (rset == null || rset.RowsCount() == 0) return linkshellList;
            while (rset.Next())
            {
                var player = GroupEntity(rset);
                player.LinkshellId1 = rset.Get<uint>("linkshellid1");
                player.LinkshellId2 = rset.Get<uint>("linkshellid2");
                player.LinkshellRank1 = rset.Get<byte>("linkshellrank1");
                player.LinkshellRank2 = rset.Get<byte>("linkshellrank2");
                player.Disconnecting = rset.Get<bool>("disconnecting");
                uint partyid = rset.GetOrDefault<uint>("partyid", 0);
                uint settings = rset.Get<uint>("settings");
                if (partyid == player.Id) player.Flags1 |= 0x0008;
                if (AwayFlg(settings)) player.Flags1 |= 0x0100;
                if (player.Disconnecting) player.Flags1 |= 0x0800;
                if (partyid != 0) player.Flags1 |= 0x2000;
                if (AnonymityFlg(settings)) player.Flags1 |= 0x4000;
                if (InviteFlg(settings)) player.Flags1 |= 0x8000;
                player.Flags2 = player.Flags1;
                linkshellList.Add(player);
            }
            return linkshellList;
        }

        public byte[] GetSearchComment(uint playerId)
        {
            var rset = Stmt("SELECT seacom_message FROM accounts_sessions WHERE charid = ?", playerId);
            if (rset != null && rset.RowsCount() > 0 && rset.Next())
            {
                if (rset.IsNull("seacom_message")) return Array.Empty<byte>();
                return rset.GetBlobBytes("seacom_message");
            }
            return Array.Empty<byte>();
        }

        private struct ListingToExpire
        {
            public uint SaleID;
            public uint ItemID;
            public byte ItemStack;
            public byte AhStack;
            public uint SellerID;
            public string SellerName;
        }

        // ExpireAHItems: listings older than the cutoff go back to their sellers' delivery boxes
        public void ExpireAHItems(ushort expireAgeInDays)
        {
            _log($"Expiring auction house listings over {expireAgeInDays} days old");
            uint cutoff = Now() - (uint)expireAgeInDays * 86400u;
            var rset0 = Stmt("SELECT T0.id,T0.itemid,T1.stacksize, T0.stack, T0.seller FROM auction_house T0 INNER JOIN item_basic T1 ON " +
                             "T0.itemid = T1.itemid WHERE T0.buyer_name IS NULL AND T0.date <= ?",
                             cutoff);
            if (rset0 == null)
            {
                _log("Failed to query expired auction house listings");
                return;
            }
            uint expiredAuctions = rset0.RowsCount();
            if (expiredAuctions > 0)
            {
                var listingsToExpire = new List<ListingToExpire>();
                while (rset0.Next())
                {
                    // seller name left out for now, populated later
                    listingsToExpire.Add(new ListingToExpire
                    {
                        SaleID = rset0.Get<uint>("id"),
                        ItemID = rset0.Get<uint>("itemid"),
                        ItemStack = rset0.Get<byte>("stacksize"),
                        AhStack = rset0.Get<byte>("stack"),
                        SellerID = rset0.Get<uint>("seller"),
                        SellerName = "?",
                    });
                }
                foreach (var l in listingsToExpire)
                {
                    var listing = l;
                    var rset1 = Stmt("SELECT charname FROM chars WHERE charid = ?", listing.SellerID);
                    if (rset1 != null && rset1.RowsCount() > 0 && rset1.Next()) listing.SellerName = rset1.Get<string>("charname");

                    // the listing must leave the auction house before its item is returned
                    _db.Transaction(() =>
                    {
                        var rset2 = Stmt("DELETE FROM auction_house WHERE id = ? AND buyer_name IS NULL", listing.SaleID);
                        if (rset2 == null || rset2.RowsAffected() == 0) return;
                        var rset3 = Stmt("INSERT INTO delivery_box (charid, charname, box, itemid, itemsubid, quantity, senderid, sender) VALUES " +
                                         "(?, ?, 1, ?, 0, ?, 0, 'AH-Jeuno')",
                                         listing.SellerID, listing.SellerName, listing.ItemID, (uint)(listing.AhStack == 1 ? listing.ItemStack : (byte)1));
                        if (rset3 == null || rset3.RowsAffected() == 0)
                        {
                            throw new InvalidOperationException($"AH: could not return expired listing {listing.SaleID} to seller {listing.SellerID}");
                        }
                    });
                }
            }
            _log($"Sent {expiredAuctions} expired auction house listings back to sellers");
        }
    }
}
