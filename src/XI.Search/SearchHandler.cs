// src/search/search_handler.cpp SearchHandler: one search connection. Each read is one blowfish-encrypted request
// (async_read_some, no framing), answered with zero or more encrypted packets.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using XI.Crypto;

namespace XI.Search
{
    public sealed class SearchHandler
    {
        private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(10);

        // Blowfish key: 16 fixed bytes, the request's key (16-19), and 4 bytes of its payload (20-23)
        private static readonly byte[] KeyBase =
        {
            0x30, 0x73, 0x3D, 0x6D, 0x3C, 0x31, 0x49, 0x5A, 0x32, 0x7A, 0x42, 0x43, 0x63, 0x38, 0x7B, 0x7E,
        };

        private readonly Stream _stream;
        private readonly string _ipAddress;
        private readonly SearchDataLoader _data;
        private readonly Action<string> _log;
        private readonly Action<string> _debug;
        private readonly byte[] _key = new byte[24];
        private readonly byte[] _buffer = new byte[4096];
        private readonly Queue<byte[]> _searchPackets = new();

        public SearchHandler(Stream stream, string ipAddress, SearchDataLoader data, Action<string> log, Action<string> debug = null)
        {
            _stream = stream;
            _ipAddress = ipAddress;
            _data = data;
            _log = log ?? (_ => { });
            _debug = debug ?? (_ => { });
            KeyBase.CopyTo(_key, 0);
        }

        public async Task RunAsync()
        {
            try
            {
                while (true)
                {
                    Array.Clear(_buffer);
                    int length;
                    using (var cts = new CancellationTokenSource(ReadTimeout))
                    {
                        try { length = await _stream.ReadAsync(_buffer, cts.Token); }
                        catch (OperationCanceledException)
                        {
                            _debug($"Socket timed out from {_ipAddress}");
                            break;
                        }
                    }
                    if (length == 0) break; // EOF

                    _debug($"Received packet from IP {_ipAddress} ({length} bytes)");
                    ReadFunc((ushort)length);
                    while (_searchPackets.Count > 0)
                    {
                        var packet = _searchPackets.Dequeue();
                        int writeLen = packet.Length;
                        Array.Clear(_buffer);
                        packet.CopyTo(_buffer, 0);
                        Encrypt((ushort)writeLen);
                        _debug($"Sending packet to IP {_ipAddress} ({writeLen} bytes)");
                        await _stream.WriteAsync(_buffer.AsMemory(0, writeLen));
                    }
                }
            }
            catch (Exception e)
            {
                _debug($"Socket error from IP {_ipAddress}: {e.Message}");
            }
        }

        private static uint Word(byte[] b, int index) => Ref.U32(b, index * 4);

        private static void Word(byte[] b, int index, uint v) => Ref.U32(b, index * 4, v);

        // decrypt: the key from the packet's last 4 bytes, md5 of the first 20 key bytes as the blowfish key
        private void Decrypt(ushort length)
        {
            Array.Copy(_buffer, length - 4, _key, 16, 4);
            var hash = MD5.HashData(_key.AsSpan(0, 20));
            var blowfish = new Blowfish();
            blowfish.Init(hash);
            int tmp = (ushort)(length - 12) / 4;
            tmp -= tmp % 2;
            for (int i = 0; i < tmp; i += 2)
            {
                uint l = Word(_buffer, i + 2), r = Word(_buffer, i + 3);
                blowfish.Decipher(ref l, ref r);
                Word(_buffer, i + 2, l);
                Word(_buffer, i + 3, r);
            }
            Array.Copy(_buffer, length - 0x18, _key, 20, 4);
        }

        // encrypt: size and "IXFF" header, md5 of the payload before the trailer, blowfish under md5 of all 24 key
        // bytes, then the request's key
        private void Encrypt(ushort length)
        {
            Ref.U16(_buffer, 0x00, length);     // packet size
            Ref.U32(_buffer, 0x04, 0x46465849); // "IXFF"
            var hash = MD5.HashData(_key);
            var blowfish = new Blowfish();
            blowfish.Init(hash);
            MD5.HashData(_buffer.AsSpan(8, length - 0x18 - 0x04)).CopyTo(_buffer, length - 0x18 + 0x04);
            byte tmp = (byte)((length - 12) / 4);
            tmp -= (byte)(tmp % 2);
            for (int i = 0; i < tmp; i += 2)
            {
                uint l = Word(_buffer, i + 2), r = Word(_buffer, i + 3);
                blowfish.Encipher(ref l, ref r);
                Word(_buffer, i + 2, l);
                Word(_buffer, i + 3, r);
            }
            Array.Copy(_key, 16, _buffer, length - 4, 4);
        }

        private bool ValidatePacket(ushort length)
        {
            // whole packet - headersize - hashsize - keysize
            int toHash = length - 0x08 - 0x10 - 0x04;
            var packetHash = MD5.HashData(_buffer.AsSpan(8, toHash));
            for (int i = 0; i < 16; ++i)
            {
                if (_buffer[length - 0x14 + i] != packetHash[i])
                {
                    _log($"Search hash wrong byte {i}: {packetHash[i]:X2} should be {_buffer[length - 0x14 + i]:X2}");
                    return false;
                }
            }
            return true;
        }

        private static string SearchTypeToString(byte type) => type switch
        {
            TcpRequestType.Search => "SEARCH",
            TcpRequestType.SearchAll => "SEARCH_ALL",
            TcpRequestType.IdList => "ID_LIST",
            TcpRequestType.SearchComment => "SEARCH_COMMENT",
            TcpRequestType.GroupList => "GROUP_LIST",
            TcpRequestType.AhRequest => "AH_REQUEST",
            TcpRequestType.AhRequestMore => "AH_REQUEST_MORE",
            TcpRequestType.AhHistorySingle => "AH_HISTORY_SINGLE",
            TcpRequestType.AhHistoryStack => "AH_HISTORY_STACK",
            _ => "UNKNOWN",
        };

        private void ReadFunc(ushort length)
        {
            if (length != Ref.U16(_buffer, 0x00) || length < 28)
            {
                _log($"Search packetsize wrong. Size {length} should be {Ref.U16(_buffer, 0x00)}.");
                return;
            }
            Decrypt(length);
            if (!ValidatePacket(length)) return;

            byte packetType = _buffer[0x0B];
            _log($"Search Request: {SearchTypeToString(packetType)} ({packetType}), size: {length}, ip: {_ipAddress}");
            switch (packetType)
            {
                case TcpRequestType.Search:
                case TcpRequestType.SearchAll:
                    HandleSearchRequest(length);
                    break;
                case TcpRequestType.SearchComment:
                    HandleSearchComment();
                    break;
                case TcpRequestType.IdList:
                    HandleIdListRequest(length);
                    break;
                case TcpRequestType.GroupList:
                    HandleGroupListRequest();
                    break;
                case TcpRequestType.AhRequest:
                case TcpRequestType.AhRequestMore:
                    HandleAuctionHouseRequest();
                    break;
                case TcpRequestType.AhHistorySingle:
                case TcpRequestType.AhHistoryStack:
                    HandleAuctionHouseHistory();
                    break;
                default:
                    _log($"Unknown packet type: {packetType}");
                    break;
            }
        }

        // SearchPacket: copies the packet's bytes; one above 1024 is refused
        private void Push(byte[] data, int length)
        {
            if (length > SearchConstants.PacketMaxSize)
            {
                _log($"Error: search packet with size above {SearchConstants.PacketMaxSize} requested!");
                return;
            }
            _searchPackets.Enqueue(data.AsSpan(0, length).ToArray());
        }

        // HandleGroupListRequest: character list request (party/linkshell)
        private void HandleGroupListRequest()
        {
            uint partyid = Ref.U32(_buffer, 0x10);
            uint allianceid = Ref.U32(_buffer, 0x14);
            uint linkshellid1 = Ref.U32(_buffer, 0x18);
            uint linkshellid2 = Ref.U32(_buffer, 0x1C);
            _log($"SEARCH::PartyID = {partyid}");
            _log($"SEARCH::LinkshellIDs = {linkshellid1}, {linkshellid2}");

            if (partyid != 0 || allianceid != 0)
            {
                var partyList = _data.GetPartyList(partyid, allianceid);
                var packet = new PartyListPacket(partyid, (uint)partyList.Count);
                int membersSent = 0;
                foreach (var player in partyList)
                {
                    if (!packet.AddPlayer(player))
                    {
                        _log($"Party list packet full, sent {membersSent} of {partyList.Count} members for party {partyid}");
                        break;
                    }
                    membersSent++;
                }
                Push(packet.Data, packet.Size);
            }
            else if (linkshellid1 != 0 || linkshellid2 != 0)
            {
                uint linkshellid = linkshellid1 == 0 ? linkshellid2 : linkshellid1;
                var linkshellList = _data.GetLinkshellList(linkshellid);
                uint totalResults = (uint)linkshellList.Count;
                uint currentResult = 0;
                // Iterate through the linkshell list, splitting up the results into smaller chunks.
                do
                {
                    var packet = new LinkshellListPacket(linkshellid, totalResults);
                    while (currentResult < totalResults)
                    {
                        if (!packet.AddPlayer(linkshellList[(int)currentResult])) break;
                        currentResult++;
                    }
                    if (currentResult == totalResults) packet.SetFinal();
                    Push(packet.Data, packet.Size);
                }
                while (currentResult < totalResults);
            }
        }

        private void HandleSearchComment()
        {
            uint playerId = Ref.U32(_buffer, 0x10);
            var comment = _data.GetSearchComment(playerId);
            if (comment.Length == 0) return;
            var packet = new SearchCommentPacket(playerId, comment);
            Push(packet.Data, packet.Size);
        }

        private void HandleSearchRequest(ushort length) => SendPlayersList(ParseSearchRequest(length));

        // HandleIdListRequest: friend list refreshes 20 character IDs at a time. uint16 count at 0x10, uint32 IDs from 0x12
        private void HandleIdListRequest(ushort length)
        {
            const int maxIds = 20;
            const int idsOffset = 0x12;
            int dataLength = (ushort)(length - SearchConstants.PacketTrailerSize);
            if (dataLength < idsOffset) return;

            var sr = new SearchRequest { Nation = 255, Race = 255 }; // any
            int count = Math.Min(Math.Min((int)Ref.U16(_buffer, 0x10), maxIds), (dataLength - idsOffset) / 4);
            for (int index = 0; index < count; ++index) sr.CharacterIds.Add(Ref.U32(_buffer, idsOffset + index * 4));
            if (sr.CharacterIds.Count == 0) return;
            SendPlayersList(sr);
        }

        private void SendPlayersList(SearchRequest sr)
        {
            int totalCount = 0;
            var searchList = _data.GetPlayersList(sr, ref totalCount);
            uint totalResults = (uint)searchList.Count;
            uint currentResult = 0;
            // Iterate through the search list, splitting up the results into smaller chunks.
            do
            {
                var packet = new SearchListPacket((uint)totalCount);
                while (currentResult < totalResults)
                {
                    if (!packet.AddPlayer(searchList[(int)currentResult])) break;
                    currentResult++;
                }
                if (currentResult == totalResults) packet.SetFinal();
                Push(packet.Data, packet.Size);
            }
            while (currentResult < totalResults);
        }

        private void HandleAuctionHouseRequest()
        {
            byte ahCatId = _buffer[0x16];
            // 2 - level, 3 - race, 4 - job, 5 - damage, 6 - delay, 7 - defense, 8 - resistance, 9 - name
            // A repeated key cannot change the order, so the four distinct keys fill every slot the query has.
            var sortKeys = new byte[4];
            int keyCount = 0;
            byte paramCount = _buffer[0x12];
            for (int i = 0; i < paramCount && keyCount < sortKeys.Length; ++i) // Item sort options
            {
                int at = 0x18 + 8 * i;
                byte param = at + 4 <= _buffer.Length ? (byte)Ref.U32(_buffer, at) : (byte)0;
                _log($" Param{i}: {param}");
                bool isSortKey = param == 2 || param == 5 || param == 6 || param == 9;
                if (isSortKey && !sortKeys.Contains(param)) sortKeys[keyCount++] = param;
            }

            var itemList = _data.GetAHItemsToCategory(ahCatId, sortKeys);
            int itemListSize = itemList.Count;
            int packetsCount = itemListSize / 20 + (itemListSize % 20 != 0 ? 1 : 0) + (itemListSize == 0 ? 1 : 0);
            for (int i = 0; i < packetsCount; ++i)
            {
                int firstItem = 20 * i;
                var packet = new AhItemsListPacket((ushort)firstItem);
                packet.SetItemCount((ushort)itemListSize);
                for (int y = firstItem; y < firstItem + 20 && y < itemListSize; ++y) packet.AddItem(itemList[y]);
                Push(packet.Data, packet.Size);
            }
        }

        private void HandleAuctionHouseHistory()
        {
            ushort itemId = Ref.U16(_buffer, 0x12);
            byte stack = _buffer[0x15];
            var historyList = _data.GetAHItemHistory(itemId, stack != 0);
            var item = _data.GetAHItemFromItemID(itemId);
            var packet = new AhHistoryPacket(item, stack);
            foreach (var h in historyList) packet.AddItem(h);
            Push(packet.Data, packet.Size);
        }

        private ulong Unpack(int bitOffset, int length) => Bits.Unpack(_buffer, 0x11, bitOffset, length);

        private static string Sort(byte sortDescending) => sortDescending == 0 ? "ascending" : "descending";

        // _HandleSearchRequest: the search_req the query is built from
        private SearchRequest ParseSearchRequest(ushort length)
        {
            var sr = new SearchRequest();
            int bitOffset = 0;
            byte sortDescending = 0, isPresent = 0, areaCount = 0;
            var name = new byte[16];
            byte nameLen = 0, minLvl = 0, maxLvl = 0, jobid = 0;
            byte raceid = 255;   // 255 because race 0 is an actual filter (hume)
            byte nationid = 255; // 255 because nation 0 is an actual filter (sandoria)
            byte minRank = 0, maxRank = 0;
            var areas = new ushort[15];
            uint flags = 0;
            byte size = _buffer[0x10];
            int workloadBits = size * 8;
            byte commentType = 0;

            while (bitOffset < workloadBits)
            {
                if (bitOffset + 5 >= workloadBits)
                {
                    bitOffset = workloadBits;
                    break;
                }
                var entryType = (SearchType)Unpack(bitOffset, 5);
                bitOffset += 5;
                if (entryType != SearchType.Friend && entryType != SearchType.RequestList && entryType != SearchType.Linkshell &&
                    entryType != SearchType.Linkshell2 && entryType != SearchType.Comment && entryType != SearchType.Flags2)
                {
                    if (bitOffset + 3 >= workloadBits) // so 0000000 at the end does not get interpreted as name entry
                    {
                        bitOffset = workloadBits;
                        break;
                    }
                    sortDescending = (byte)Unpack(bitOffset, 1);
                    bitOffset += 1;
                    isPresent = (byte)Unpack(bitOffset, 1);
                    bitOffset += 1;
                }

                switch (entryType)
                {
                    case SearchType.Name:
                        if (isPresent == 0x1) // Name send
                        {
                            if (bitOffset + 5 >= workloadBits)
                            {
                                bitOffset = workloadBits;
                                break;
                            }
                            // 5-bit field (0-31) clamped to name's 15 chars; still consume every char
                            byte rawNameLen = (byte)Unpack(bitOffset, 5);
                            bitOffset += 5;
                            nameLen = Math.Min(rawNameLen, (byte)(name.Length - 1));
                            name[nameLen] = 0;
                            for (int i = 0; i < rawNameLen; i++)
                            {
                                byte nameChar = (byte)Unpack(bitOffset, 7);
                                bitOffset += 7;
                                if (i < nameLen) name[i] = nameChar;
                            }
                        }
                        break;
                    case SearchType.Area: // Area Code Entry - 10 bit
                        if (isPresent != 0) // 8 Bit = 1 Byte per Area Code
                        {
                            ushort area = (ushort)Unpack(bitOffset, 10);
                            bitOffset += 10;
                            // client controls entry count; only store while areas[] has room
                            if (areaCount < areas.Length) areas[areaCount++] = area;
                        }
                        break;
                    case SearchType.Nation: // Country - 2 bit
                        if (isPresent == 0x1)
                        {
                            byte country = (byte)Unpack(bitOffset, 2);
                            bitOffset += 2;
                            nationid = country;
                            _log($"Nationality Entry found. ({country:X2}) Sorting: ({Sort(sortDescending)}).");
                        }
                        break;
                    case SearchType.Job: // Job - 5 bit
                        if (isPresent == 0x1)
                        {
                            jobid = (byte)Unpack(bitOffset, 5);
                            bitOffset += 5;
                        }
                        break;
                    case SearchType.Level: // Level - 16 bit
                        if (isPresent == 0x1)
                        {
                            minLvl = (byte)Unpack(bitOffset, 8);
                            bitOffset += 8;
                            maxLvl = (byte)Unpack(bitOffset, 8);
                            bitOffset += 8;
                        }
                        break;
                    case SearchType.Race: // Race - 4 bit
                        if (isPresent == 0x1)
                        {
                            raceid = (byte)Unpack(bitOffset, 4);
                            bitOffset += 4;
                            _log($"Race Entry found. ({raceid:X2}) Sorting: ({Sort(sortDescending)}).");
                        }
                        _log($"SortByRace: {Sort(sortDescending)}.");
                        break;
                    case SearchType.Rank: // Rank - 2 byte
                        if (isPresent == 0x1)
                        {
                            minRank = (byte)Unpack(bitOffset, 8);
                            bitOffset += 8;
                            maxRank = (byte)Unpack(bitOffset, 8);
                            bitOffset += 8;
                            _log($"Rank Entry found. ({minRank} - {maxRank}) Sorting: ({Sort(sortDescending)}).");
                        }
                        _log($"SortByRank: {Sort(sortDescending)}.");
                        break;
                    case SearchType.Comment: // 4 Byte
                        commentType = (byte)Unpack(bitOffset, 32);
                        bitOffset += 32;
                        _log($"Comment Entry found. ({commentType:X2}).");
                        break;
                    // the following 4 Entries were generated with /sea (ballista|friend|linkshell|away|inv), so they may be off
                    case SearchType.Linkshell: // 4 Byte
                        sr.LsId = (uint)Unpack(bitOffset, 32);
                        bitOffset += 32;
                        _log($"Linkshell Entry found. Value: {sr.LsId.Value:X8}");
                        break;
                    case SearchType.Linkshell2: // 4 Byte
                        sr.LsId = (uint)Unpack(bitOffset, 32);
                        bitOffset += 32;
                        _log($"Linkshell2 Entry found. Value: {sr.LsId.Value:X8}");
                        break;
                    case SearchType.Friend: // Friend Packet, 0 byte
                        sr.FriendsOnly = true;
                        break;
                    case SearchType.RequestList: // Party request history, 0 byte
                        sr.RequestListOnly = true;
                        break;
                    case SearchType.Flags1: // Flag Entry #1, 2 byte
                        if (isPresent == 0x1)
                        {
                            ushort flags1 = (ushort)Unpack(bitOffset, 16);
                            bitOffset += 16;
                            _log($"Flag Entry #1 ({flags1:X4}) found. Sorting: ({Sort(sortDescending)}).");
                            flags = flags1;
                        }
                        _log($"SortByFlags: {Sort(sortDescending)}");
                        break;
                    case SearchType.Flags2: // Flag Entry #2 - 4 byte
                        flags = (uint)Unpack(bitOffset, 32);
                        bitOffset += 32;
                        break;
                    default:
                        _log($"Unknown Search Param {(byte)entryType}!");
                        break;
                }
            }

            _log($"Name: {(nameLen > 0 ? System.Text.Encoding.Latin1.GetString(name, 0, Array.IndexOf(name, (byte)0)) : "<empty>")} Job: {jobid} Lvls: {minLvl} ~ {maxLvl}");
            sr.JobId = jobid;
            sr.MaxLvl = maxLvl;
            sr.MinLvl = minLvl;
            sr.Race = raceid;
            sr.Nation = nationid;
            sr.MinRank = minRank;
            sr.MaxRank = maxRank;
            sr.Flags = flags;
            sr.CommentType = commentType;
            sr.NameLen = nameLen;
            Array.Copy(areas, sr.ZoneId, sr.ZoneId.Length);
            // sr.name.insert(0, name): the C string
            if (nameLen > 0) sr.Name = name.AsSpan(0, Array.IndexOf(name, (byte)0)).ToArray();

            int dataLength = (ushort)(length - SearchConstants.PacketTrailerSize);
            int trailerOffset = (ushort)(0x11 + size);
            // Friend searches append a uint16 count and the uint32 charids of online friends after the query block
            if (sr.FriendsOnly)
            {
                const int maxFriendIds = 200;
                int idsOffset = (ushort)(trailerOffset + 2);
                if (dataLength >= idsOffset)
                {
                    int count = Math.Min(Math.Min((int)Ref.U16(_buffer, trailerOffset), maxFriendIds), (ushort)((dataLength - idsOffset) / 4));
                    for (int index = 0; index < count; ++index) sr.CharacterIds.Add(Ref.U32(_buffer, idsOffset + index * 4));
                    trailerOffset = (ushort)(idsOffset + count * 4);
                }
            }
            // Request list searches append a uint16 count and the 16-byte names from the client's party request history
            if (sr.RequestListOnly)
            {
                const int maxRequestNames = 64;
                const int nameSize = 16;
                int namesOffset = (ushort)(trailerOffset + 2);
                if (dataLength >= namesOffset)
                {
                    int count = Math.Min(Math.Min((int)Ref.U16(_buffer, trailerOffset), maxRequestNames), (ushort)((dataLength - namesOffset) / nameSize));
                    for (int index = 0; index < count; ++index)
                    {
                        var field = _buffer.AsSpan(namesOffset + index * nameSize, nameSize - 1);
                        int nul = field.IndexOf((byte)0);
                        sr.CharacterNames.Add((nul < 0 ? field : field.Slice(0, nul)).ToArray());
                    }
                }
            }
            // Do not process the last bits, which can interfere with other operations
            // For example: "/blacklist delete Name" and "/sea all Name"
            return sr;
        }
    }
}
