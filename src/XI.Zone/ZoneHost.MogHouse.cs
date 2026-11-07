using System;
using System.Collections.Generic;
using System.Linq;
using XI.Ipc;
using XI.Map;
using XI.Persistence;

namespace XI.Zone
{
    public enum MyroomIsKind : byte { Open = 1, Close = 2, Remodel = 5 }                                // GP_CLI_COMMAND_MYROOM_IS_KIND
    public enum MyroomIsParam2 : ushort { None = 0, Unknown = 1, SandorianStyle = 615, BastokanStyle = 616,
                                          WindurstianStyle = 617, MogPatio = 618 }                      // GP_CLI_COMMAND_MYROOM_IS_PARAM2

    // The mog house a char is in (m_moghouseID, chars.moghouse) and MogHouseContainer: the open-to-visitors state, the
    // visitors (dropped once they left), the host of the one a char is in, visiting a party or alliance member's open
    // mog house (visitMogHouse) and being sent back (expel, returnZone); the 0x05E mog house exit (zmrq) by its exit
    // mode (the area entered from, a city exit the mhflag quest bits allow, a floor change, the Mog Garden). A visitor
    // gets the host's installed furniture as its safes (SendInventory), and each installed mannequin's look at the login
    // (updateMannequins).
    public partial class ZoneHost
    {
        private const ushort MsgStdSuccessfulRemodel = 293;
        private const int KeyItemMogPatioDesignDocument = 3051;

        private void RegisterMogHouseHandlers()
        {
            Register(0x0CB, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent)
                                              .MustEqual(c.MoghouseId, c.Id, "Character not in their mog house")
                                              .MustEqual(p.Buffer[4] == (byte)MyroomIsKind.Remodel && c.MogHouseOpen ? 1 : 0, 0, "Mog House is open to visitors")
                                              .OneOf<MyroomIsKind>(p.Buffer[4])
                                              .OneOf<MyroomIsParam2>(BitConverter.ToUInt16(p.Buffer, 6)),
                     (s, c, p) => OnMyroomIs(s, c, (MyroomIsKind)p.Buffer[4], BitConverter.ToUInt16(p.Buffer, 6)));
            Register(0x0FA, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent)
                                              .IsInMogHouse(MogHouseKind.Own)
                                              .MustEqual(c.MogHouseOpen ? 1 : 0, 0, "Mog House is open to visitors")
                                              .Range("MyroomFloorFlg", p.Buffer[8], 0, 1) // the 2nd floor
                                              .Range("v", p.Buffer[12], 0, 3)              // rotation
                                              .Range("y", p.Buffer[10], 0, 25),            // stacking elevation (parent height / 10)
                     (s, c, p) => OnMyroomLayout(s, c, BitConverter.ToUInt16(p.Buffer, 4), p.Buffer[6], (Container)p.Buffer[7], p.Buffer[8],
                                                 p.Buffer[9], p.Buffer[10], p.Buffer[11], p.Buffer[12]));
            Register(0x0FB, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent)
                                              .IsInMogHouse(MogHouseKind.Own)
                                              .MustEqual(c.MogHouseOpen ? 1 : 0, 0, "Mog House is open to visitors")
                                              .MustNotEqual(BitConverter.ToUInt16(p.Buffer, 4), 0, "MyroomItemNo must not equal 0")
                                              .OneOf("MyroomCategory", p.Buffer[7], MogSafes),
                     (s, c, p) => OnMyroomBankin(s, c, BitConverter.ToUInt16(p.Buffer, 4), p.Buffer[6], (Container)p.Buffer[7]));
        }

        private static readonly long[] MogSafes = { (long)Container.MogSafe, (long)Container.MogSafe2 };

        // CCharEntity::inMogHouse(Own)
        private static bool InOwnMogHouse(CharData c) => c.MoghouseId == c.Id;

        // MogHouseContainer::host: the owner of the mog house the char is in (itself at home) when on this process
        private CharData MogHouseHost(CharData c)
        {
            if (InOwnMogHouse(c)) return c;
            if (c.MoghouseId == 0) return null;
            var (host, _) = CharById(c.MoghouseId);
            return host != null && SameArea(host, c) && InOwnMogHouse(host) ? host : null;
        }

        // CCharSyncPacket with the visited host's 2F flag
        private BasicPacket CharSyncOf(CharData c) => GameOkPackets.CharSync(c, MogHouseHost(c));

        // GP_CLI_COMMAND_MYROOM_IS: open or close the mog house to the party, or remodel the second floor
        private void OnMyroomIs(MapSession s, CharData c, MyroomIsKind kind, ushort param2)
        {
            // a rent-a-room's client hides these, retail honors them when injected
            if (IsRentARoom(c)) log_($"Player {c.Name} modifying Rent-a-Room state.");

            switch (kind)
            {
                case MyroomIsKind.Open:
                    OpenMogHouse(c);
                    break;
                case MyroomIsKind.Close:
                    CloseMogHouse(c);
                    break;
                case MyroomIsKind.Remodel:
                {
                    // retail forces the nation's style on an invalid Param2
                    var defaultStyle = c.Nation switch
                    {
                        1 => MyroomIsParam2.BastokanStyle,
                        2 => MyroomIsParam2.WindurstianStyle,
                        _ => MyroomIsParam2.SandorianStyle,
                    };
                    ushort newStyle = param2;
                    if ((c.MhFlag & 0x0020) == 0) log_($"Player {c.Name} remodeling MH2F without it unlocked.");
                    if (param2 == (ushort)MyroomIsParam2.MogPatio && !c.HasKeyItem(KeyItemMogPatioDesignDocument))
                    {
                        log_($"Player {c.Name} remodeling MH2F to Patio without owning the KI to unlock it.");
                        newStyle = (ushort)defaultStyle;
                    }

                    // 0x0080 and 0x0100 hold the 2F style (0 Sandoria, 1 Bastok, 2 Windurst, 3 Patio); 615 lines it up with Param2
                    ushort oldType = (ushort)((((c.MhFlag & 0x0100) + (c.MhFlag & 0x0080)) >> 7) + 615);
                    c.MhFlag &= unchecked((ushort)~0x0180);
                    // as the C++: a Param2 of 0 or 1 sets the bits of the negative difference
                    c.MhFlag |= unchecked((ushort)((newStyle - 615) << 7));
                    SaveCharStats(c);

                    // the forced zone may bypass this message
                    Push(s, SelfPackets.Message(MsgStdSuccessfulRemodel));

                    // a changed model on the second floor needs a rezone to take effect
                    if (param2 != oldType && (c.MhFlag & 0x0040) != 0)
                    {
                        s.PacketList.Clear();
                        zoneChanges_[s] = c.Zone;
                    }
                    break;
                }
            }
        }

        // isRentARoom: a mog house outside the char's nation (any for a char of no nation)
        private static bool IsRentARoom(CharData c)
        {
            byte region = ZoneRegions.Of(c.Zone);
            return c.Nation switch
            {
                0 => region != ZoneRegions.Sandoria,
                1 => region != ZoneRegions.Bastok,
                2 => region != ZoneRegions.Windurst,
                _ => true,
            };
        }

        // MogHouseContainer::open
        private void OpenMogHouse(CharData c)
        {
            c.MogHouseOpen = true;
            Exec("UPDATE accounts_parties SET partyflag = partyflag | ? WHERE charid = ?", BoundValue.Of((uint)PartyMoghouse), BoundValue.Of(c.Id));
            SendMogHousePartyReload(c);
            PushTo(c, CharSyncOf(c));
        }

        // MogHouseContainer::close (also on any zone-out): the visitors expelled
        private void CloseMogHouse(CharData c)
        {
            if (!c.MogHouseOpen) return;
            c.MogHouseOpen = false;
            Exec("UPDATE accounts_parties SET partyflag = partyflag & ~? WHERE charid = ?", BoundValue.Of((uint)PartyMoghouse), BoundValue.Of(c.Id));
            SendMogHousePartyReload(c);
            c.UpdateMask |= CharStats.UpdateHp;
            PushTo(c, CharSyncOf(c));
            foreach (var visitor in MogHouseVisitors(c)) ExpelMogHouseVisitor(visitor);
            c.MogHouseVisitors.Clear();
        }

        // MogHouseContainer::visitors: the ones still in the host's mog house on this process (the rest dropped)
        private List<CharData> MogHouseVisitors(CharData host)
        {
            var visitors = new List<CharData>();
            host.MogHouseVisitors.RemoveAll(id =>
            {
                var visitor = CharById(id).Item1;
                if (visitor == null || visitor.MoghouseId != host.Id || visitor.Zone != host.Zone) return true;
                visitors.Add(visitor);
                return false;
            });
            return visitors;
        }

        // charutils::updateMannequins: at a login into a mog house, each installed mannequin's 0x026 from the host's
        // storage (a visitor's at its renumbered slot, as SendInventory gives the furniture)
        private void UpdateMannequins(MapSession s, CharData c)
        {
            var owner = MogHouseHost(c);
            if (owner == null) return;
            foreach (var safe in new[] { Container.MogSafe, Container.MogSafe2 })
            {
                byte visitorSlot = 1;
                foreach (var item in owner.Items.Where(i => i.Location == safe && i.Slot <= owner.StorageSize[(int)safe]).OrderBy(i => i.Slot).ToList())
                {
                    if (!item.Installed) continue;
                    byte slot = owner != c ? visitorSlot++ : item.Slot;
                    if (!IsMannequin(item)) continue;
                    if (item.Extra[MannequinRaceOffset] == 0)
                        log_("Invalid Mannequin placed (race of 0 in exdata, when races start at 1). It will be unusable.");
                    Push(s, ItemSubcontainer(owner, safe, slot, item.Extra));
                }
            }
        }

        // MogHouseContainer::addVisitor
        private static void AddMogHouseVisitor(CharData host, CharData visitor)
        {
            if (!host.MogHouseVisitors.Contains(visitor.Id)) host.MogHouseVisitors.Add(visitor.Id);
        }

        // MogHouseContainer::visit: a party (or alliance) member's open mog house in this region of the host's home nation;
        // the NPC used is kept for the way back
        private bool VisitMogHouse(CharData c, uint hostId, uint npcId)
        {
            if (c.Party == null || c.MoghouseId != 0 || hostId == c.Id) return false;
            byte region = ZoneRegions.Of(c.Zone);
            uint allianceId = c.Party.Alliance?.Id ?? 0;
            var r = Exec("SELECT chars.pos_zone, chars.nation FROM accounts_parties JOIN chars ON chars.charid = accounts_parties.charid " +
                         "WHERE accounts_parties.charid = ? AND partyflag & ? AND (partyid = ? OR (allianceid <> 0 AND allianceid = ?)) LIMIT 1",
                         BoundValue.Of(hostId), BoundValue.Of((uint)PartyMoghouse), BoundValue.Of(c.Party.Id), BoundValue.Of(allianceId));
            if (r == null || !r.Next()) return false;
            ushort hostZone = r.Get<ushort>("pos_zone");
            byte hostNation = r.Get<byte>("nation");
            if (ZoneRegions.Of(hostZone) != region || !IsHomeNation(hostNation, region) || ZoneIpp(hostZone) == 0) return false;
            SetCharVar(c, "mh-visit-npc", (int)npcId, 0);
            c.PrevZone = c.Zone;
            SendVisitorTo(c, hostZone, hostId);
            return true;
        }

        // sendVisitorTo: off to the zone, into (or out of) the mog house, the position left to the zone-in
        private void SendVisitorTo(CharData c, ushort destination, uint moghouseId)
        {
            ClearTrusts(c);
            c.MoghouseId = moghouseId;
            (c.X, c.Y, c.Z, c.Rotation) = (0, 0, 0, 0);
            c.Boundary = 0;
            if (SessionOf(c) is not { } s) return;
            s.PacketList.Clear();
            zoneChanges_[s] = destination;
        }

        // MogHouseContainer::expel
        private void ExpelMogHouseVisitor(CharData c) => SendVisitorTo(c, MogHouseReturnZone(c), 0);

        // MogHouseContainer::returnZone: the zone of the NPC used to visit, else the previous zone, else this one
        private ushort MogHouseReturnZone(CharData c)
        {
            uint npcId = (uint)CharVar(c, "mh-visit-npc");
            ushort destination = npcId != 0 ? (ushort)(npcId >> 12 & 0xFFF) : c.PrevZone;
            if (ZoneIpp(destination) == 0) destination = c.Zone;
            return destination;
        }

        // the login's half (GP_CLI_COMMAND_LOGIN): a visitor whose host closed or left while it zoned in lands outside
        private void CheckMogHouseHostOnLogin(CharData c)
        {
            if (c.MoghouseId == 0 || c.MoghouseId == c.Id || CharVar(c, "mh-visit-npc") == 0) return;
            var host = CharById(c.MoghouseId).Item1;
            if (host != null && host.Zone == c.Zone && host.MogHouseOpen && InOwnMogHouse(host)) return;
            c.MoghouseId = 0;
            (c.X, c.Y, c.Z, c.Rotation) = (0, 0, 0, 0);
        }

        // the mog house exit (zmrq), GP_CLI_COMMAND_MAPRECT_MYROOMEXITMODE: AreaEnteredFrom 0 (a visitor goes back to its
        // NPC's zone), Option1-4 a city's exits by MyRoomExitBit, Mog2F 125 / Mog1F 126, MogGarden 127; the destination,
        // or null when refused
        private const byte ExitAreaEnteredFrom = 0, ExitMog2F = 125, ExitMog1F = 126, ExitMogGarden = 127;

        private ushort? MogHouseExit(CharData c, byte exitBit, byte exitMode, ushort startingZone)
        {
            ushort destination = c.Zone;
            if (!ChatCfg.MogGarden && exitMode == ExitMogGarden) exitMode = ExitAreaEnteredFrom;
            bool visiting = c.MoghouseId != 0 && c.MoghouseId != c.Id, inMogHouse = c.MoghouseId != 0;

            // visitors can't change floors or head to the Mog Garden
            if (visiting && exitMode is ExitMog1F or ExitMog2F or ExitMogGarden)
            {
                log_($"GP_CLI_COMMAND_MAPRECT: Mog House visitor {c.Name} requested exit mode {exitMode}");
                return null;
            }
            switch (exitMode)
            {
                case ExitAreaEnteredFrom:
                    if (visiting) destination = MogHouseReturnZone(c);
                    break;
                case 1:
                case 2:
                case 3:
                case 4:
                    if (MogHouseCityExit(exitBit, exitMode) is not ushort city) return null;
                    destination = city;
                    break;
                case ExitMog1F:
                case ExitMog2F:
                    destination = c.Zone;
                    break;
                case ExitMogGarden:
                    destination = ZoneMogGarden;
                    break;
            }

            bool regular = exitMode == ExitAreaEnteredFrom && inMogHouse;
            bool floorChange = inMogHouse && startingZone == destination && exitMode is ExitMog1F or ExitMog2F;
            bool unlocked2F = (c.MhFlag & 0x20) != 0 && ChatCfg.MogHouse2F;
            byte startingRegion = ZoneRegions.Of(startingZone), destinationRegion = ZoneRegions.Of(destination);
            bool exitRegion = destinationRegion is ZoneRegions.Sandoria or ZoneRegions.Bastok or ZoneRegions.Windurst or ZoneRegions.Jeuno
                                                    or ZoneRegions.WestAhtUrhgan or ZoneRegions.AdoulinIslands;
            // the city exit's quest (the mhflag bit of MyRoomExitBit); Adoulin's back-alley exits are always open
            bool questComplete = (c.MhFlag & (exitBit != 0 ? 1 << (exitBit - 1) : 0)) != 0 || startingRegion == ZoneRegions.AdoulinIslands;
            bool questExit = questComplete && startingRegion == destinationRegion && inMogHouse && exitRegion && !floorChange;
            bool gardenExit = destination == ZoneMogGarden && inMogHouse;

            if (regular || questExit || gardenExit)
            {
                if (visiting && exitMode != ExitAreaEnteredFrom) SetCharVar(c, "mh-visit-npc", 0, 0);
                c.MoghouseId = 0;
                (c.X, c.Y, c.Z, c.Rotation) = (0, 0, 0, 0);
                // the 2F tracker
                c.MhFlag &= unchecked((ushort)~0x40);
            }
            else if (floorChange)
            {
                (c.X, c.Y, c.Z, c.Rotation) = (0, 0, 0, 0);
                if (!unlocked2F)
                {
                    log_($"GP_CLI_COMMAND_MAPRECT: Moghouse 2F requested without it being unlocked: {c.Name}");
                    return null;
                }
                c.MhFlag ^= 0x40;
            }
            else
            {
                log_($"GP_CLI_COMMAND_MAPRECT: Moghouse zoneline abuse by {c.Name}");
                return null;
            }
            return destination;
        }

        // moghouseCityExit: the exits index forward from the first zone of a city
        private static ushort? MogHouseCityExit(byte exitBit, byte mode) => (exitBit, mode) switch
        {
            (1, 1) => 230, (1, 2) => 231, (1, 3) => 232,                     // Southern, Northern, Port San d'Oria
            (2, 1) => 234, (2, 2) => 235, (2, 3) => 236,                     // Bastok Mines, Markets, Port Bastok
            (3, 1) => 238, (3, 2) => 239, (3, 3) => 240, (3, 4) => 241,      // Windurst Waters, Walls, Port Windurst, Woods
            (4, 1) => 243, (4, 2) => 244, (4, 3) => 245, (4, 4) => 246,      // Ru'Lude Gardens, Upper, Lower, Port Jeuno
            (5, 1) => 48, (5, 2) => 50,                                      // Al Zahbi, Aht Urhgan Whitegate
            (9, 1) => 256, (9, 2) => 257,                                    // Western, Eastern Adoulin
            _ => null,
        };

        private void SendMogHousePartyReload(CharData c)
        {
            if (c.Party == null) return;
            if (c.Party.Alliance != null) SendIpc(new AllianceReload(c.Party.Alliance.Id));
            else SendIpc(new PartyReload(c.Party.Id));
        }

        // ---- furnishings (0x0FA LAYOUT, 0x0FB BANKIN) ---------------------------------------------------------------
        // Exdata::Furniture: byte 1 holds On2ndFloor (bit 0) and Installed (bit 6), then X @6, Z @7, Y @8, Rotation @9.
        // The C++ calls X the col, Z the level and Y the row; the packet's x / y / z are the col / level / row.

        private const int FurnitureCol = 6, FurnitureLevel = 7, FurnitureRow = 8, FurnitureRotation = 9;

        private FurnishingInfo FurnishingOf(Item item) => item == null ? null : loader_.Stats?.Items.Furnishing(item.ItemId);
        private static bool On2ndFloor(Item item) => (item.Extra[1] & 0x01) != 0;

        private const ushort ItemOrchestrion = 426;
        private const byte MhFlagOn2ndFloor = 0x40;

        // the 1st floor's Moogle: the mog house moogle (face 0x52) standing at z 1.5
        private static bool IsMogHouseMoogle(NpcEntity n) => n.Name == "Moogle" && n.Z == 1.5f && (n.Look[1] & 0xFF) == 0x52;

        // charutils::IsHomeNation: a nation's own city region
        private static bool IsHomeNation(byte nation, byte region) => region switch
        {
            19 => nation == 0, // REGION_TYPE::SANDORIA
            20 => nation == 1, // BASTOK
            21 => nation == 2, // WINDURST
            _ => false,
        };

        // charutils::isOrchestrionPlaced: an orchestrion installed in either safe
        private static bool IsOrchestrionPlaced(CharData c) =>
            c.Items.Exists(i => (i.Location == Container.MogSafe || i.Location == Container.MogSafe2) && i.ItemId == ItemOrchestrion && i.PlacedFurniture);

        // CZoneEntities::SpawnConditionalNPCs: the "you only" NPCs of a mog house, sent to this char as if shown (or
        // gone) and left hidden for everyone else: the 1st floor's Moogle while in the house and not upstairs, the
        // Symphonic Curator in a home-nation house with an orchestrion placed. The nation, 2F flag and orchestrion are
        // the house host's (moghouse().host(): a visitor reads the host's), the char's own outside a house.
        private void SpawnConditionalNpcs(MapSession s, CharData c)
        {
            var owner = MogHouseHost(c) ?? c;
            bool inMogHouse = c.MoghouseId != 0;
            bool inHomeNation = inMogHouse && IsHomeNation(owner.Nation, ZoneRegions.Of(c.Zone));
            bool on2F = (owner.MhFlag & MhFlagOn2ndFloor) != 0;
            bool orchestrion = IsOrchestrionPlaced(owner);
            void Toggle(NpcEntity n, bool visible)
            {
                n.Status = visible ? NpcEntity.StatusNormal : NpcEntity.StatusDisappear;
                UpdateEntityPacket(s, n, EntityUpdate.Spawn, EntityUpdatePacket.UpdateAllMob);
                n.Status = NpcEntity.StatusDisappear;
            }
            foreach (var n in AreaOf(c).Npcs)
            {
                if (IsMogHouseMoogle(n)) Toggle(n, inMogHouse && !on2F);
                else if (n.Name == "Symphonic_Curator") Toggle(n, inHomeNation && orchestrion);
            }
        }

        // 0x0FA GP_SERV_COMMAND_MYROOM_OPERATION, Result Ok
        private static BasicPacket MyroomOperation(Item item, Container location, byte slot)
        {
            var w = new PacketWriter(0x0FA, 16);
            w.U16(4, item.ItemId);
            w.U8(10, slot);
            w.U8(11, (byte)location);
            return w.Packet;
        }

        // MH1F: a 20x24 grid without the 8x18 rectangle at x 6..13, z 0..17; MH2F: 20x26, all usable
        private static bool IsValidFloorCell(int cx, int cz, bool is2F)
        {
            if (is2F) return cx <= 19 && cz <= 25;
            if (cz > 23) return false;
            if (cx <= 5 || (cx >= 14 && cx <= 19)) return true;
            return cx >= 6 && cx <= 13 && cz >= 18;
        }

        // 1F has 5 wall slots, 2F 92
        private static bool IsValidWallSlot(byte slot, bool is2F) => slot <= (is2F ? 91 : 4);

        // a 90 / 270 degree rotation swaps the width and depth
        private static (int W, int D) RotatedSize(FurnishingInfo f, int v) => v == 1 || v == 3 ? (f.SizeY, f.SizeX) : (f.SizeX, f.SizeY);

        // every cell of the footprint on a valid floor cell
        private static bool FootprintInGrid(int ax, int az, int sx, int sz, bool is2F)
        {
            for (int cx = ax; cx < ax + sx; ++cx)
                for (int cz = az; cz < az + sz; ++cz)
                    if (!IsValidFloorCell(cx, cz, is2F)) return false;
            return true;
        }

        // does `other` occupy a floor cell of the rectangle? Wall items and stacked OnTable ones don't
        private static bool CollidesOnFloor(Item other, FurnishingInfo of, int x, int z, int width, int depth, bool is2F)
        {
            if (On2ndFloor(other) != is2F) return false;
            if (of.Placement == FurnishingPlacement.Wall) return false;
            if (of.Placement == FurnishingPlacement.OnTable && other.Extra[FurnitureLevel] != 0) return false;
            var (ow, od) = RotatedSize(of, other.Extra[FurnitureRotation]);
            int ox = other.Extra[FurnitureCol], oz = other.Extra[FurnitureRow];
            return x < ox + ow && ox < x + width && z < oz + od && oz < z + depth;
        }

        // the installed furnishings of both mog safes but the one at selfCat / selfSlot, in container then slot order
        private IEnumerable<(Item Item, FurnishingInfo Info)> InstalledFurnishings(CharData c, Container selfCat = 0, int selfSlot = -1)
        {
            foreach (var cat in new[] { Container.MogSafe, Container.MogSafe2 })
                foreach (var item in c.Items.Where(i => i.Location == cat && i.Slot <= c.StorageSize[(int)cat]).OrderBy(i => i.Slot).ToList())
                {
                    if (cat == selfCat && item.Slot == selfSlot) continue;
                    if (item.Installed && FurnishingOf(item) is FurnishingInfo f) yield return (item, f);
                }
        }

        // CItemContainer::AddBuff on LOC_STORAGE; SetSize refuses a size below the items it holds
        private void AddStorageBuff(CharData c, int buff)
        {
            c.StorageBuff += buff;
            int size = Math.Clamp(c.StorageBuff, 0, 80);
            if (size >= c.Items.Count(i => i.Location == Container.Storage)) c.StorageSize[(int)Container.Storage] = (byte)size;
            else log_($"ItemContainer <{(int)Container.Storage}>: Bad new container size {size}");
        }

        // GP_CLI_COMMAND_MYROOM_LAYOUT: place or move a furnishing; no item when the client is done placing
        private void OnMyroomLayout(MapSession s, CharData c, ushort itemNo, byte itemIndex, Container category, byte floorFlg,
                                    byte x, byte y, byte z, byte v)
        {
            if (itemNo == 0)
            {
                UpdateMoghancement(s, c);
                return;
            }
            if (category != Container.MogSafe && category != Container.MogSafe2)
            {
                log_($"Invalid container requested: {c.Name}");
                return;
            }
            if (itemIndex > c.StorageSize[(int)category])
            {
                log_($"Invalid slot requested: {c.Name}");
                return;
            }
            bool is2F = floorFlg != 0;
            if (is2F && (c.MhFlag & 0x20) == 0)
            {
                log_($"MH2F placement without 2F unlocked: {c.Name}");
                return;
            }

            var item = StorageItem(c, category, itemIndex);
            if (FurnishingOf(item) is not FurnishingInfo info) return;
            if (item.ItemId != itemNo)
            {
                log_($"Layout item id mismatch: {c.Name}");
                return;
            }

            var (width, depth) = RotatedSize(info, v);
            switch (info.Placement)
            {
                case FurnishingPlacement.Floor:
                case FurnishingPlacement.Surface:
                case FurnishingPlacement.OnTable:
                    if (!FootprintInGrid(x, z, width, depth, is2F))
                    {
                        log_($"Invalid placement footprint: {c.Name}");
                        return;
                    }
                    // a stacked item (y > 0) skips this: moving a stack, the client sends it before its parent
                    if (y == 0 && InstalledFurnishings(c, category, itemIndex).Any(o => CollidesOnFloor(o.Item, o.Info, x, z, width, depth, is2F)))
                    {
                        log_($"Placement collides with installed furniture: {c.Name}");
                        return;
                    }
                    // a fresh OnTable placement needs a Surface of the matching height under (x, z); a move skips it for
                    // the same reason
                    if (info.Placement == FurnishingPlacement.OnTable && y > 0 && !item.Installed)
                    {
                        bool hasParent = InstalledFurnishings(c, category, itemIndex).Any(o =>
                        {
                            if (o.Info.Placement != FurnishingPlacement.Surface || On2ndFloor(o.Item) != is2F) return false;
                            if (o.Info.Height / 10 != y) return false;
                            var (pw, pd) = RotatedSize(o.Info, o.Item.Extra[FurnitureRotation]);
                            int px = o.Item.Extra[FurnitureCol], pz = o.Item.Extra[FurnitureRow];
                            return x >= px && x < px + pw && z >= pz && z < pz + pd;
                        });
                        if (!hasParent)
                        {
                            log_($"OnTable placement with no matching Surface parent: {c.Name}");
                            return;
                        }
                    }
                    break;
                case FurnishingPlacement.Wall:
                    if (y != 0 || z != 0)
                    {
                        log_($"Wall placement with non-zero y/z: {c.Name}");
                        return;
                    }
                    if (!IsValidWallSlot(x, is2F))
                    {
                        log_($"Invalid wall slot requested: {c.Name}");
                        return;
                    }
                    if (InstalledFurnishings(c, category, itemIndex).Any(o => o.Info.Placement == FurnishingPlacement.Wall &&
                                                                              On2ndFloor(o.Item) == is2F && o.Item.Extra[FurnitureCol] == x))
                    {
                        log_($"Wall slot already occupied: {c.Name}");
                        return;
                    }
                    break;
            }

            if (floorFlg != 0 && info.IsFlowerpot)
            {
                log_($"{c.Name} has tried to gardening pot {item.ItemId} on 2nd floor");
                return;
            }

            // ItemState PlacedFurniture: only a Free item can take it
            if (!item.PlacedFurniture && IsBusy(c, item))
            {
                log_($"GP_CLI_COMMAND_MYROOM_LAYOUT: could not mark furnishing {item.ItemId} for {c.Name}");
                return;
            }
            item.PlacedFurniture = true;

            bool wasInstalled = item.Installed;
            if (wasInstalled && On2ndFloor(item) != is2F)
            {
                log_($"Floor change on installed furnishing: {c.Name}");
                return;
            }

            item.Extra[1] = (byte)((item.Extra[1] & ~0x01) | 0x40 | floorFlg);
            item.Extra[FurnitureCol] = x;
            item.Extra[FurnitureRow] = z;
            item.Extra[FurnitureLevel] = y;
            item.Extra[FurnitureRotation] = v;

            Push(s, MyroomOperation(item, category, itemIndex));

            var r = Exec("UPDATE char_inventory SET extra = ? WHERE location = ? AND slot = ? AND charid = ? LIMIT 1",
                         BoundValue.Of(item.Extra), BoundValue.Of((byte)category), BoundValue.Of(itemIndex), BoundValue.Of(c.Id));
            if (r != null && r.RowsAffected() != 0 && !wasInstalled)
            {
                // storage only counts on the 1st floor
                if (!is2F) AddStorageBuff(c, info.Storage);
                Push(s, LoginPackets.ItemMax(c));
                OnFurnitureScript(c, item, true);
                SpawnConditionalNpcs(s, c);
            }
            Push(s, GameOkPackets.ItemAttr(item, ItemLockFlg(c, item)));
            Push(s, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
        }

        // GP_CLI_COMMAND_MYROOM_BANKIN: put an installed furnishing back in the safe
        private void OnMyroomBankin(MapSession s, CharData c, ushort itemNo, byte itemIndex, Container category)
        {
            if (itemIndex > c.StorageSize[(int)category])
            {
                log_($"Invalid slot requested: {c.Name}");
                return;
            }
            var item = StorageItem(c, category, itemIndex);
            if (FurnishingOf(item) is not FurnishingInfo info) return;
            if (item.ItemId != itemNo)
            {
                log_($"Bankin item id mismatch: {c.Name}");
                return;
            }
            if (!item.Installed)
            {
                log_($"Bankin requested for an uninstalled item: {c.Name}");
                return;
            }

            // the storage slots its removal takes away (the C++'s uint8 min: a buff below the storage takes none)
            int size = c.StorageSize[(int)Container.Storage];
            int removedSize = size - Math.Min(size, (byte)(c.StorageBuff - info.Storage));
            if (FreeSlots(c, Container.Storage) < removedSize)
            {
                log_("GP_CLI_COMMAND_MYROOM_BANKIN: furnishing can't be removed");
                return;
            }

            // mark(Free) refuses only a transaction's item
            if (item.InTransaction)
            {
                log_($"GP_CLI_COMMAND_MYROOM_BANKIN: could not release furnishing {item.ItemId}");
                return;
            }
            item.PlacedFurniture = false;

            // the floor bit stays: the storage below goes by it
            item.Extra[1] &= unchecked((byte)~0x40);
            item.Extra[FurnitureCol] = item.Extra[FurnitureRow] = item.Extra[FurnitureLevel] = item.Extra[FurnitureRotation] = 0;

            // a flowerpot loses its plant
            if (info.IsFlowerpot) CleanPot(item);

            // a mannequin loses its look and lets its storage items go
            if (IsMannequin(item))
            {
                Push(s, ItemSubcontainer(c, category, itemIndex, new byte[item.Extra.Length]));
                for (int i = 0; i < 8; i++)
                {
                    byte slot = item.Extra[MannequinEquipOffset + i];
                    if (slot == 0) continue;
                    if (StorageItem(c, Container.Storage, slot) is Item equipped) Push(s, LoginPackets.ItemList(equipped, 0));
                    item.Extra[MannequinEquipOffset + i] = 0;
                }
            }

            var r = Exec("UPDATE char_inventory SET extra = ? WHERE location = ? AND slot = ? AND charid = ? LIMIT 1",
                         BoundValue.Of(item.Extra), BoundValue.Of((byte)category), BoundValue.Of(itemIndex), BoundValue.Of(c.Id));
            if (r != null && r.RowsAffected() != 0)
            {
                int newSize = size - removedSize;
                for (int slot = size; slot > newSize; --slot)
                    if (StorageItem(c, Container.Storage, (byte)slot) is Item moving) MoveStorageItem(s, c, moving);

                if (!On2ndFloor(item)) AddStorageBuff(c, -info.Storage);
                Push(s, LoginPackets.ItemMax(c));
                OnFurnitureScript(c, item, false);
                SpawnConditionalNpcs(s, c);
            }
            Push(s, GameOkPackets.ItemAttr(item, ItemLockFlg(c, item)));
            Push(s, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
        }

        // charutils::MoveItem(LOC_STORAGE, slot, ERROR_SLOTID): into the container's first free slot
        private void MoveStorageItem(MapSession s, CharData c, Item item)
        {
            const Container loc = Container.Storage;
            if (FreeSlots(c, loc) == 0)
            {
                log_("charutils::MoveItem: item can't be moved");
                return;
            }
            byte oldSlot = item.Slot;
            int size = c.StorageSize[(int)loc];
            int newSlot = 1;
            while (newSlot <= size && c.FindItem(loc, (byte)newSlot) is Item o && o != item) newSlot++;
            if (newSlot > size)
            {
                log_("charutils::MoveItem: item can't be moved");
                return;
            }
            var r = Exec("UPDATE char_inventory SET slot = ? WHERE charid = ? AND location = ? AND slot = ? LIMIT 1",
                         BoundValue.Of((byte)newSlot), BoundValue.Of(c.Id), BoundValue.Of((byte)loc), BoundValue.Of(oldSlot));
            if (r == null || r.RowsAffected() == 0)
            {
                log_("charutils::MoveItem: item can't be moved");
                return;
            }
            item.Slot = (byte)newSlot;
            bool leak = loader_.Stats?.Settings.LeakExtDataOnItemMove ?? true;
            Push(s, GameOkPackets.EmptySlotAttr(loc, oldSlot, leak ? item.Extra : null));
            Push(s, GameOkPackets.ItemAttr(item, ItemLockFlg(c, item)));
        }

        // CItemFlowerpot::cleanPot (Exdata::FlowerPot): not dried, and no plant, stage, crystals or timestamps
        private static void CleanPot(Item pot)
        {
            pot.Extra[1] &= 0x7F;
            pot.Extra[0] = pot.Extra[2] = pot.Extra[3] = pot.Extra[4] = 0;
            Array.Clear(pot.Extra, 12, 8);
        }

        // CCharEntity::UpdateMoghancement: the moghancement of the 1st floor's dominant element (none on a tie), from
        // its highest-aura furnishing (the highest id on a tie)
        private void UpdateMoghancement(MapSession s, CharData c)
        {
            var elements = new int[8];
            foreach (var (item, f) in InstalledFurnishings(c))
                // an element of 0 indexes before the C++'s array
                if (!On2ndFloor(item) && f.Element >= 1 && f.Element <= 8) elements[f.Element - 1] += f.Aura;

            int dominantElement = 0, dominantAura = 0;
            bool hasTiedElements = false;
            for (int element = 1; element < 9; ++element)
            {
                int aura = (ushort)elements[element - 1];
                if (aura > dominantAura)
                {
                    dominantElement = element;
                    dominantAura = aura;
                    hasTiedElements = false;
                }
                else if (aura == dominantAura) hasTiedElements = true;
            }

            int bestAura = 0;
            ushort newId = 0;
            if (!hasTiedElements && dominantAura > 0)
                foreach (var (item, f) in InstalledFurnishings(c))
                    if (!On2ndFloor(item) && f.Element == dominantElement && (f.Aura > bestAura || (f.Aura == bestAura && f.Moghancement > newId)))
                    {
                        bestAura = f.Aura;
                        newId = f.Moghancement;
                    }

            // the moghancement held is always shown
            if (newId != 0)
            {
                int textId = scripts_?.GetTextIdVariable(c.Zone, "KEYITEM_OBTAINED") ?? 0;
                Push(s, EventPackets.TalkNumWork(c.Id, c.TargId, c.Name, true, (ushort)textId, newId, 0, 0, 0, false));
            }

            if (newId == c.Moghancement) return;
            if (c.Moghancement != 0) SetKeyItemBit(c, c.Moghancement, false);
            if (newId != 0) SetKeyItemBit(c, newId, true);
            // one key item packet when both are in the same table
            int newTable = newId >> 9, currentTable = c.Moghancement >> 9;
            if (newTable == currentTable) Push(s, GameOkPackets.ScenarioItem(c, newTable));
            else
            {
                if (newTable != 0) Push(s, GameOkPackets.ScenarioItem(c, newTable));
                if (currentTable != 0) Push(s, GameOkPackets.ScenarioItem(c, currentTable));
            }
            Exec("UPDATE chars SET keyitems = ? WHERE charid = ? LIMIT 1", BoundValue.Of(c.KeyItems), BoundValue.Of(c.Id));

            Moghancement.Set(c, newId);
            Exec("UPDATE chars SET moghancement = ? WHERE charid = ?", BoundValue.Of(c.Moghancement), BoundValue.Of(c.Id));
        }

        // charutils::addKeyItem / delKeyItem: the keyList bit only
        private void SetKeyItemBit(CharData c, int keyItem, bool add)
        {
            int table = keyItem / 512, index = keyItem % 512;
            if (table >= 8)
            {
                log_($"charutils::{(add ? "addKeyItem" : "delKeyItem")}() - Index {table} exceeds key items table capacity.");
                return;
            }
            int off = table * 128 + index / 8;
            if (add) c.KeyItems[off] |= (byte)(1 << index % 8);
            else c.KeyItems[off] &= (byte)~(1 << index % 8);
        }
    }
}
