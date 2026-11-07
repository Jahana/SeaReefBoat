using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // GP_CLI_COMMAND_CHOCOBO_RACE_REQ (0x09B): the Chocobo Circuit toteboard's odds (s2c 0x073 CHOCOBO_TOTEBOARD) and a
    // race's field (s2c 0x074 CHOCOBO_LIST), both from xi.chocoboRacing.onRaceRequest; and the luautils globals the race
    // scripts keep their races and bets with (LoadChocoboRaceResult, SaveChocoboRaceResult, GetChocoboRaceQuills,
    // AddChocoboRaceQuills over chocobo_race_results / chocobo_race_quills); the race broadcast's sendChocoboRace
    // (s2c 0x069 CHOCOBO_RACING).
    public sealed partial class ZoneHost
    {
        private const ushort ZoneChocoboCircuit = 70;
        private const int ChocoboRacers = 8, ChocoboSections = 32, ToteboardOdds = 28;
        private const int ChocoboParamSize = 12, ChocoboNameLength = 16, ChocoboSectionSize = 12, ChocoboSectionsPerPacket = 16;

        // GP_CLI_COMMAND_CHOCOBO_RACE_REQ_KIND
        public enum ChocoboRaceKind : uint
        {
            Toteboard = 1,
            ChocoboList = 2,
        }

        private void RegisterChocoboRaceHandler()
        {
            // Param u32 @4 (the attendant color, or a window open/close the client reports), Kind u32 @8
            Register(0x09B, (s, c, p) => Pv(c)
                    .MustEqual(c.Zone, ZoneChocoboCircuit, "Not in Chocobo Circuit")
                    .OneOf<ChocoboRaceKind>(U32(p, 8)),
                OnChocoboRaceReq);
        }

        // GP_CLI_COMMAND_CHOCOBO_RACE_REQ::process
        private void OnChocoboRaceReq(MapSession s, CharData c, BasicPacket p)
        {
            if (lua_ == null) return;
            uint param = U32(p, 4), kind = U32(p, 8);
            const string path = "xi.chocoboRacing.onRaceRequest";
            int top = lua_.Top;
            try
            {
                lua_.GetPath(path);
                if (lua_.IsNil(-1))
                {
                    log_($"luautils::callGlobalFunction: {path}: Function not found");
                    return;
                }
                lua_.PushObject(c);
                lua_.Push((double)param);
                lua_.Push((double)kind);
                lua_.Call(3, 1);

                // board.empty(): no table, or one with no keys at all
                int board = lua_.Top;
                if (!lua_.IsTable(board)) return;
                lua_.PushNil();
                if (!lua_.Next(board)) return;
                lua_.SetTop(board);

                if ((ChocoboRaceKind)kind == ChocoboRaceKind.ChocoboList)
                {
                    Push(s, ChocoboListRace((uint)FieldInt(lua_, board, "weather", 0)));
                    lua_.GetField(board, "chocobos");
                    int chocobos = lua_.Top;
                    if (lua_.IsTable(chocobos))
                    {
                        int count = Math.Min(lua_.ObjLen(chocobos), ChocoboRacers);
                        var stats = new List<byte[]>();
                        var names = new List<string>();
                        for (int i = 1; i <= count; i++)
                        {
                            lua_.RawGetI(chocobos, i);
                            stats.Add(ChocoboParamFromLua(lua_, lua_.Top));
                            names.Add(FieldString(lua_, lua_.Top, "name", ""));
                            lua_.Pop(1);
                        }
                        Push(s, ChocoboListChocobos(stats));
                        Push(s, ChocoboListNames(names));
                    }
                    Push(s, ChocoboListEnd());
                }
                else
                {
                    var odds = new List<ushort>();
                    lua_.GetField(board, "odds");
                    int t = lua_.Top;
                    if (lua_.IsTable(t))
                    {
                        int count = Math.Min(lua_.ObjLen(t), ToteboardOdds);
                        for (int i = 1; i <= count; i++)
                        {
                            lua_.RawGetI(t, i);
                            odds.Add((ushort)(lua_.IsNumber(-1) ? (uint)lua_.ToIntegral(-1) : 0));
                            lua_.Pop(1);
                        }
                    }
                    lua_.SetTop(board);
                    Push(s, ChocoboToteboard(param, (uint)FieldInt(lua_, board, "grade", 0), (uint)FieldInt(lua_, board, "raceNumber", 0), odds));
                }
            }
            catch (LuaException e)
            {
                ScriptError(path, e);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        private static string FieldString(LuaState l, int table, string key, string fallback)
        {
            l.GetField(table, key);
            string v = l.IsString(-1) ? l.ToStringValue(-1) : fallback;
            l.Pop(1);
            return v;
        }

        // ---- S2C ---------------------------------------------------------------------------------------------------

        // GP_SERV_COMMAND_CHOCOBO_TOTEBOARD (0x073): SlotIndex @4 (the request's Param), Ident @8 (grade in bits 18-23,
        // the race number in 0-17), the 28 quinella odds u16 @16
        public static BasicPacket ChocoboToteboard(uint slotIndex, uint grade, uint raceNumber, IReadOnlyList<ushort> odds)
        {
            var w = new PacketWriter(0x073, 0x48);
            w.U32(4, slotIndex);
            w.U32(8, grade << 18 | raceNumber & 0x3FFFF);
            for (int i = 0; i < Math.Min(odds.Count, ToteboardOdds); i++) w.U16(16 + i * 2, odds[i]);
            return w.Packet;
        }

        // GP_SERV_COMMAND_CHOCOBO_LIST (0x074): SlotIndex @4 (left 0, as the C++ leaves it), Mode u16 @16, ParamSize u16
        // @18, the mode's data @20
        private static PacketWriter ChocoboList(ushort mode, int paramSize)
        {
            var w = new PacketWriter(0x074, 0xB4);
            w.U16(16, mode);
            w.U16(18, paramSize);
            return w;
        }

        // mode 1: the race parameters, the weather packed into RaceParams[0]
        public static BasicPacket ChocoboListRace(uint weather)
        {
            var w = ChocoboList(1, 8);
            w.U32(20, (weather << 5) + 8);
            w.U32(24, 0xC0000000);
            return w.Packet;
        }

        // mode 2: the entrants' 12-byte ChocoboParams
        public static BasicPacket ChocoboListChocobos(IReadOnlyList<byte[]> chocobos)
        {
            int count = Math.Min(chocobos.Count, ChocoboRacers);
            var w = ChocoboList(2, count * ChocoboParamSize);
            for (int i = 0; i < count; i++) w.Bytes(20 + i * ChocoboParamSize, chocobos[i]);
            return w.Packet;
        }

        // mode 3: the entrants' names, a u32 pad and 16 name bytes each, at most 15 of them copied
        public static BasicPacket ChocoboListNames(IReadOnlyList<string> names)
        {
            int count = Math.Min(names.Count, ChocoboRacers);
            var w = ChocoboList(3, count * (4 + ChocoboNameLength));
            for (int i = 0; i < count; i++) w.Name(20 + i * (4 + ChocoboNameLength) + 4, names[i], ChocoboNameLength - 1);
            return w.Packet;
        }

        // mode 4: the exchange is done
        public static BasicPacket ChocoboListEnd() => ChocoboList(4, 0).Packet;

        // ---- GP_SERV_COMMAND_CHOCOBO_RACING::ChocoboParam / SectionParam -------------------------------------------
        // ChocoboParam: Orders (bits 0-3) and Item (4-7) in byte 0, the str/end/dsc/rcp stat bytes @4, then a u32 @8 of
        // DNA1-3 (bits 0-8), Ability1 (9-12), Ability2 (13-16), Temperament (17-19), Weather (20-23), Gender (24),
        // Color (25-27), Size (28-30). SectionParam: From[4] and To[4] (8 positions, the even chocobo in the low
        // nibble), then the trigger's User, Targets, Param, Type.

        // ChocoboParam::fromLua: get_or<uint8_t> truncates, the bitfields keep their low bits
        private static byte[] ChocoboParamFromLua(LuaState l, int t)
        {
            var b = new byte[ChocoboParamSize];
            if (!l.IsTable(t)) return b;
            byte F(string key) => (byte)FieldInt(l, t, key, 0);
            b[0] = (byte)(F("orders") & 0x0F | (F("item") & 0x0F) << 4);
            l.GetField(t, "stats");
            int st = l.Top;
            if (l.IsTable(st))
            {
                b[4] = (byte)FieldInt(l, st, "str", 0);
                b[5] = (byte)FieldInt(l, st, "end", 0);
                b[6] = (byte)FieldInt(l, st, "dsc", 0);
                b[7] = (byte)FieldInt(l, st, "rcp", 0);
            }
            l.Pop(1);
            uint dna = (uint)(F("ability1") & 0xF) << 9 | (uint)(F("ability2") & 0xF) << 13 | (uint)(F("temperament") & 0x7) << 17
                     | (uint)(F("weather") & 0xF) << 20 | (uint)(F("gender") & 0x1) << 24 | (uint)(F("color") & 0x7) << 25
                     | (uint)(F("size") & 0x7) << 28;
            BitConverter.GetBytes(dna).CopyTo(b, 8);
            return b;
        }

        // ChocoboParam::toLua: pushes the table
        private static void ChocoboParamToLua(LuaState l, ReadOnlySpan<byte> b)
        {
            uint dna = BitConverter.ToUInt32(b.Slice(8, 4));
            l.NewTable();
            int t = l.Top;
            void Set(int table, string key, uint v) { l.Push((double)v); l.SetField(table, key); }
            l.NewTable();
            Set(t + 1, "str", b[4]);
            Set(t + 1, "end", b[5]);
            Set(t + 1, "dsc", b[6]);
            Set(t + 1, "rcp", b[7]);
            Set(t, "item", (uint)b[0] >> 4);
            Set(t, "orders", b[0] & 0x0Fu);
            Set(t, "size", dna >> 28 & 0x7);
            Set(t, "color", dna >> 25 & 0x7);
            Set(t, "gender", dna >> 24 & 0x1);
            Set(t, "weather", dna >> 20 & 0xF);
            Set(t, "temperament", dna >> 17 & 0x7);
            Set(t, "ability1", dna >> 9 & 0xF);
            Set(t, "ability2", dna >> 13 & 0xF);
            l.SetField(t, "stats");
        }

        // readNibbles then packNibbles: the table at n's 8 values, 2 a byte, the even chocobo in the low nibble
        private static void PackRacerNibbles(LuaState l, int n, byte[] b, int at)
        {
            for (int racer = 0; racer < ChocoboRacers; racer++)
            {
                byte v = 0;
                if (l.IsTable(n))
                {
                    l.RawGetI(n, racer + 1);
                    if (l.IsNumber(-1)) v = (byte)l.ToIntegral(-1);
                    l.Pop(1);
                }
                if (racer % 2 == 0) b[at + racer / 2] = (byte)(v & 0x0F);
                else b[at + racer / 2] |= (byte)((v & 0x0F) << 4);
            }
        }

        // SectionParam::fromLua into b at off
        private static void SectionParamFromLua(LuaState l, int t, byte[] b, int off)
        {
            void Nibbles(string key, int at)
            {
                l.GetField(t, key);
                PackRacerNibbles(l, l.Top, b, at);
                l.Pop(1);
            }
            Nibbles("from", off);
            Nibbles("to", off + 4);
            l.GetField(t, "trigger");
            int tr = l.Top;
            if (l.IsTable(tr))
            {
                b[off + 8] = (byte)FieldInt(l, tr, "user", 0);
                b[off + 9] = (byte)FieldInt(l, tr, "targets", 0);
                b[off + 10] = (byte)FieldInt(l, tr, "param", 0);
                b[off + 11] = (byte)FieldInt(l, tr, "type", 0);
            }
            l.Pop(1);
        }

        // SectionParam::toLua: pushes the table
        private static void SectionParamToLua(LuaState l, ReadOnlySpan<byte> b)
        {
            l.NewTable();
            int t = l.Top;
            void Set(int table, string key, uint v) { l.Push((double)v); l.SetField(table, key); }
            foreach (var (key, at) in new[] { ("from", 0), ("to", 4) })
            {
                l.NewTable();
                for (int racer = 0; racer < ChocoboRacers; racer++)
                {
                    l.Push((double)(racer % 2 == 0 ? b[at + racer / 2] & 0x0F : b[at + racer / 2] >> 4));
                    l.RawSetI(t + 1, racer + 1);
                }
                l.SetField(t, key);
            }
            l.NewTable();
            Set(t + 1, "user", b[8]);
            Set(t + 1, "targets", b[9]);
            Set(t + 1, "param", b[10]);
            Set(t + 1, "type", b[11]);
            l.SetField(t, "trigger");
        }

        // db::extractFromBlob: as much of the blob as fits, the rest zero
        private static byte[] RaceBlob(ResultSet r, string col, int size)
        {
            var b = new byte[size];
            if (!r.IsNull(col))
            {
                var bytes = r.GetBlobBytes(col);
                Array.Copy(bytes, b, Math.Min(bytes.Length, size));
            }
            return b;
        }

        // ---- the race broadcast -------------------------------------------------------------------------------------

        // GP_SERV_COMMAND_CHOCOBO_RACING (0x069): Mode @4, ParamIndex @5, ParamSize @6, the mode's data @8; 0xC8 bytes
        // whatever the mode
        private static PacketWriter ChocoboRacing(byte mode, int paramIndex, int paramSize)
        {
            var w = new PacketWriter(0x069, 0xC8);
            w.U8(4, mode);
            w.U8(5, paramIndex);
            w.U8(6, paramSize);
            return w;
        }

        // CLuaBaseEntity::sendChocoboRace: RACINGPARAMS (the weather and the entrants in RaceParams[0], the high bit and
        // the 2-bit counter in [1]), CHOCOBOPARAMS, the SECTIONPARAMS 16 a packet, RESULTPARAMS (the places), END
        private void SendChocoboRace(CharData c, LuaState l, int race)
        {
            l.GetField(race, "chocobos");
            int chocobos = l.Top;
            bool hasChocobos = l.IsTable(chocobos);
            int entrants = hasChocobos ? Math.Min(l.ObjLen(chocobos), ChocoboRacers) : ChocoboRacers;
            var w = ChocoboRacing(1, 0, 8);
            w.U32(8, (uint)FieldInt(l, race, "weather", 1) << 5 | (uint)entrants);
            w.U32(12, 0x80000000 | (uint)FieldInt(l, race, "counter", 0) & 0x3);
            PushTo(c, w.Packet);
            if (hasChocobos)
            {
                w = ChocoboRacing(2, 0, entrants * ChocoboParamSize);
                for (int i = 0; i < entrants; i++)
                {
                    l.RawGetI(chocobos, i + 1);
                    w.Bytes(8 + i * ChocoboParamSize, ChocoboParamFromLua(l, l.Top));
                    l.Pop(1);
                }
                PushTo(c, w.Packet);
            }
            l.Pop(1);

            l.GetField(race, "sections");
            int sections = l.Top;
            if (l.IsTable(sections))
            {
                int count = Math.Min(l.ObjLen(sections), ChocoboSections);
                for (int offset = 0; offset < count; offset += ChocoboSectionsPerPacket)
                {
                    int n = Math.Min(ChocoboSectionsPerPacket, count - offset);
                    var b = new byte[n * ChocoboSectionSize];
                    for (int i = 0; i < n; i++)
                    {
                        l.RawGetI(sections, offset + i + 1);
                        if (l.IsTable(-1)) SectionParamFromLua(l, l.Top, b, i * ChocoboSectionSize);
                        l.Pop(1);
                    }
                    w = ChocoboRacing(3, offset, b.Length);
                    w.Bytes(8, b);
                    PushTo(c, w.Packet);
                }
            }
            l.Pop(1);

            l.GetField(race, "places");
            if (l.IsTable(-1))
            {
                var places = new byte[4];
                PackRacerNibbles(l, l.Top, places, 0);
                w = ChocoboRacing(4, 0, places.Length);
                w.Bytes(8, places);
                PushTo(c, w.Packet);
            }
            l.Pop(1);
            PushTo(c, ChocoboRacing(5, 0, 0).Packet);
        }

        // ---- luautils globals ----------------------------------------------------------------------------------------

        private void RegisterChocoboRaceGlobals()
        {
            // the race's table, or nil when it has not been posted
            lua_.SetGlobal("LoadChocoboRaceResult", l =>
            {
                uint raceNo = l.ToUIntArg(1);
                var r = Exec("SELECT weather, places, run_at, intro_first, intro_second, chocobos, names, sections " +
                             "FROM chocobo_race_results WHERE race_no = ? LIMIT 1", BoundValue.Of(raceNo));
                if (r == null || !r.Next())
                {
                    l.PushNil();
                    return 1;
                }
                var chocobos = RaceBlob(r, "chocobos", ChocoboRacers * ChocoboParamSize);
                var names = RaceBlob(r, "names", ChocoboRacers * ChocoboNameLength);
                var sections = RaceBlob(r, "sections", ChocoboSections * ChocoboSectionSize);
                uint packed = r.Get<uint>("places");

                l.NewTable();
                int t = l.Top;
                void Set(string key, double v) { l.Push(v); l.SetField(t, key); }
                Set("raceNo", raceNo);
                Set("weather", r.Get<byte>("weather"));
                l.NewTable();
                for (int racer = 0; racer < ChocoboRacers; racer++)
                {
                    l.Push((double)(packed >> racer * 4 & 0x0F));
                    l.RawSetI(t + 1, racer + 1);
                }
                l.SetField(t, "places");
                Set("runAt", r.Get<uint>("run_at"));
                l.NewTable();
                l.Push((double)r.Get<byte>("intro_first"));
                l.RawSetI(t + 1, 1);
                l.Push((double)r.Get<byte>("intro_second"));
                l.RawSetI(t + 1, 2);
                l.SetField(t, "intro");
                l.NewTable();
                for (int racer = 0; racer < ChocoboRacers; racer++)
                {
                    ChocoboParamToLua(l, chocobos.AsSpan(racer * ChocoboParamSize, ChocoboParamSize));
                    var name = names.AsSpan(racer * ChocoboNameLength, ChocoboNameLength);
                    int len = name.IndexOf((byte)0);
                    l.PushBytes(len < 0 ? name : name.Slice(0, len));
                    l.SetField(t + 2, "name");
                    l.RawSetI(t + 1, racer + 1);
                }
                l.SetField(t, "chocobos");
                l.NewTable();
                for (int section = 0; section < ChocoboSections; section++)
                {
                    SectionParamToLua(l, sections.AsSpan(section * ChocoboSectionSize, ChocoboSectionSize));
                    l.RawSetI(t + 1, section + 1);
                }
                l.SetField(t, "sections");
                return 1;
            });

            lua_.SetGlobal("SaveChocoboRaceResult", l =>
            {
                const int t = 1;
                uint places = 0;
                l.GetField(t, "places");
                if (l.IsTable(-1))
                {
                    int pt = l.Top;
                    for (int racer = 0; racer < ChocoboRacers; racer++)
                    {
                        l.RawGetI(pt, racer + 1);
                        if (l.IsNumber(-1)) places |= ((uint)l.ToIntegral(-1) & 0x0F) << racer * 4;
                        l.Pop(1);
                    }
                }
                l.Pop(1);

                var chocobos = new byte[ChocoboRacers * ChocoboParamSize];
                var names = new byte[ChocoboRacers * ChocoboNameLength];
                l.GetField(t, "chocobos");
                int ct = l.Top;
                if (l.IsTable(ct))
                {
                    for (int racer = 0; racer < ChocoboRacers; racer++)
                    {
                        l.RawGetI(ct, racer + 1);
                        int e = l.Top;
                        if (l.IsTable(e))
                        {
                            ChocoboParamFromLua(l, e).CopyTo(chocobos, racer * ChocoboParamSize);
                            l.GetField(e, "name");
                            if (l.IsString(-1))
                            {
                                var name = l.ToBytes(-1);
                                Array.Copy(name, 0, names, racer * ChocoboNameLength, Math.Min(name.Length, ChocoboNameLength - 1));
                            }
                            l.Pop(1);
                        }
                        l.Pop(1);
                    }
                }
                l.Pop(1);

                var sections = new byte[ChocoboSections * ChocoboSectionSize];
                l.GetField(t, "sections");
                int st = l.Top;
                if (l.IsTable(st))
                {
                    for (int section = 0; section < ChocoboSections; section++)
                    {
                        l.RawGetI(st, section + 1);
                        if (l.IsTable(-1)) SectionParamFromLua(l, l.Top, sections, section * ChocoboSectionSize);
                        l.Pop(1);
                    }
                }
                l.Pop(1);

                byte introFirst = 0, introSecond = 0;
                l.GetField(t, "intro");
                if (l.IsTable(-1))
                {
                    int it = l.Top;
                    l.RawGetI(it, 1);
                    if (l.IsNumber(-1)) introFirst = (byte)l.ToIntegral(-1);
                    l.RawGetI(it, 2);
                    if (l.IsNumber(-1)) introSecond = (byte)l.ToIntegral(-1);
                    l.Pop(2);
                }
                l.Pop(1);

                Exec("INSERT INTO chocobo_race_results SET race_no = ?, weather = ?, places = ?, run_at = ?, " +
                     "intro_first = ?, intro_second = ?, chocobos = ?, names = ?, sections = ? " +
                     "ON DUPLICATE KEY UPDATE weather = VALUES(weather), places = VALUES(places), run_at = VALUES(run_at), " +
                     "intro_first = VALUES(intro_first), intro_second = VALUES(intro_second), chocobos = VALUES(chocobos), " +
                     "names = VALUES(names), sections = VALUES(sections)",
                    BoundValue.Of((uint)FieldInt(l, t, "raceNo", 0)), BoundValue.Of((byte)FieldInt(l, t, "weather", 0)),
                    BoundValue.Of(places), BoundValue.Of((uint)FieldInt(l, t, "runAt", 0)), BoundValue.Of(introFirst),
                    BoundValue.Of(introSecond), BoundValue.Of(chocobos), BoundValue.Of(names), BoundValue.Of(sections));
                return 0;
            });

            // quills on each of the 28 pairs, 0 for a pair nobody bet on
            lua_.SetGlobal("GetChocoboRaceQuills", l =>
            {
                var quills = new uint[ToteboardOdds];
                var r = Exec("SELECT pair, quills FROM chocobo_race_quills WHERE race_no = ?", BoundValue.Of(l.ToUIntArg(1)));
                while (r != null && r.Next())
                {
                    byte pair = r.Get<byte>("pair");
                    if (pair < ToteboardOdds) quills[pair] = r.Get<uint>("quills");
                }
                l.NewTable();
                for (int pair = 0; pair < ToteboardOdds; pair++)
                {
                    l.Push((double)quills[pair]);
                    l.RawSetI(-2, pair + 1);
                }
                return 1;
            });

            lua_.SetGlobal("AddChocoboRaceQuills", l =>
            {
                uint raceNo = l.ToUIntArg(1);
                byte pair = (byte)l.ToUIntArg(2);
                uint quills = l.ToUIntArg(3);
                if (pair >= ToteboardOdds)
                {
                    log_($"AddChocoboRaceQuills: pair {pair} is not on the toteboard");
                    return 0;
                }
                Exec("INSERT INTO chocobo_race_quills (race_no, pair, quills) VALUES (?, ?, ?) " +
                     "ON DUPLICATE KEY UPDATE quills = quills + VALUES(quills)",
                    BoundValue.Of(raceNo), BoundValue.Of(pair), BoundValue.Of(quills));
                return 0;
            });
        }
    }
}
