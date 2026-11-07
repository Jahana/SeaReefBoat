using System;
using XI.Map;

namespace XI.Zone
{
    // packet_system.cpp ValidatedPacketHandler: a C2S packet whose size is outside its struct's range is dropped, then
    // validate() runs and only a valid packet is processed (and becomes m_LastPacketType). Sizes come from the compiled
    // headers (C2sPacketTable); each Register mirrors one c2s/0x*.cpp validate() + process().
    public sealed partial class ZoneHost
    {
        private delegate PacketValidator PacketValidate(MapSession s, CharData c, BasicPacket p);
        private delegate void PacketProcess(MapSession s, CharData c, BasicPacket p);

        private sealed record PacketHandler(C2sPacketInfo Info, PacketValidate Validate, PacketProcess Process);

        private PacketHandler[] packetHandlers_;

        private void Register(ushort id, PacketValidate validate, PacketProcess process)
        {
            var info = C2sPacketTable.Get(id) ?? throw new ArgumentException($"no C2S packet 0x{id:X3}");
            packetHandlers_[id] = new PacketHandler(info, validate, process);
        }

        private static PacketValidator Pv(CharData c) => new PacketValidator(c);

        private static ushort U16(BasicPacket p, int off) => BitConverter.ToUInt16(p.Buffer, off);
        private static uint U32(BasicPacket p, int off) => BitConverter.ToUInt32(p.Buffer, off);

        // MapNetwork.Dispatch
        public void Dispatch(MapSession s, ushort type, BasicPacket p)
        {
            if (s.Char is not CharData c) return;
            charSessions_[c] = s;
            // a char made without the loader (which attaches it as it loads char_effects) still carries effects
            if (c.StatusEffects.Table == null) AttachEffects(c);
            if (packetHandlers_ == null) RegisterPacketHandlers();

            var h = type < packetHandlers_.Length ? packetHandlers_[type] : null;
            if (h == null)
            {
                // what the client sends that is not ported yet, once per opcode
                if (unhandled_.Add(type)) log_($"zone: unhandled packet 0x{type:X3} from {c.Name} ({p.GetSize()} bytes)");
                return;
            }

            uint size = p.GetSize();
            if (size < h.Info.MinSize || size > h.Info.MaxSize)
            {
                log_($"zone: Bad packet size for {h.Info.Name} (0x{type:x3}) from {c.Name}: got {size}, expected [{h.Info.MinSize}, {h.Info.MaxSize}]");
                return;
            }

            var result = h.Validate(s, c, p);
            if (!result.Valid)
            {
                log_($"zone: Invalid {h.Info.Name} packet from {c.Name}: {result.ErrorString}");
                return;
            }
            c.LastPacketType = type;
            h.Process(s, c, p);
        }

        private readonly System.Collections.Generic.HashSet<ushort> unhandled_ = new System.Collections.Generic.HashSet<ushort>();

        private void RegisterPacketHandlers()
        {
            packetHandlers_ = new PacketHandler[512];

            // 0x00A LOGIN; "already logged in" (blowfish accepted + status normal + a decrypted packet) is the session's
            // job here
            Register(0x00A, (s, c, p) => Pv(c).MustEqual(c.Id, U32(p, 12), "Player ID mismatch"),
                (s, c, p) => OnLoginPacket(s, c, p));
            Register(0x00C, (s, c, p) => Pv(c)
                    .MustEqual(U32(p, 4), 0, "ClientState not 0")
                    .MustEqual(U32(p, 8) & 1, 0, "DebugClientFlg not 0"),
                (s, c, p) =>
                {
                    c.MogLockerAccess = HasMogLockerAccess(c);
                    RefreshUnityPoints(c);
                    Push(s, GameOkPackets.OnGameOk(c, AreaOf(c), UnixNow(), LoadUnity(), merits_, enableRoe_,
                                                   BlacklistPackets(c), TakeTimedRecordNotify(c), MogHouseHost(c), treasurePool: false,
                                                   extendedJob: ExtendedJobPackets(c), nowMs: NowMs(),
                                                   infamy: c.Monstrosity != null ? GetPoints(c, "infamy") : 0));
                    // the client builds its monster move list from this set, and drops it if it arrives mid-load
                    if (c.Monstrosity != null) SendFullMonstrosityUpdate(c);
                    SendPendingGmResponse(s, c);
                    c.TreasurePool?.UpdatePool(c);
                    c.ReloadParty = true;
                    SpawnTransport(s, c);
                    // respawn any pets from last zone
                    RespawnZoningPet(c);
                });
            Register(0x011, (s, c, p) => Pv(c)
                    .MustEqual(p.Buffer[4], 2, "unknown00 not 2")
                    .MustEqual(p.Buffer[5], 0, "unknown01 not 0"),
                (s, c, p) => Push(s, LoginPackets.OnZoneLoaded(c, AreaOf(c), UnixNow())));
            // char status Disappear/Shutdown is the zone change / shutdown in flight
            Register(0x015, (s, c, p) => Pv(c)
                    .MustEqual(zoneChanges_.ContainsKey(s) ? 1 : 0, 0, "Character is disappearing")
                    .MustEqual(s.ShuttingDown, 0, "Character is shutting down"),
                (s, c, p) =>
                {
                    if (PendingPositionUpdate(s, c)) return;
                    OnPos(c, p);
                    // some NPCs move or turn, so every position update asks for NPC updates; the C++ syncs PCs on the
                    // zone's next tick (requestedInfoSync), here at once
                    SpawnNpcs(s, c);
                    SpawnMobs(s, c);
                    SpawnPets(s, c);
                    SpawnTrusts(s, c);
                    SpawnPcs(s, c);
                    SendWideScanTrack(s, c);
                });
            Register(0x016, (s, c, p) => Pv(c), (s, c, p) => OnCharReq(s, c, U16(p, 4)));
            Register(0x017, (s, c, p) => Pv(c), OnCharReq2);
            Register(0x01A, (s, c, p) => ValidateAction(c, p), (s, c, p) => OnAction(s, c, p));
            Register(0x05B, (s, c, p) => Pv(c)
                    .OneOf("Mode", U16(p, 0x0E), 0, 1)
                    .IsInEvent(U16(p, 0x12)),
                (s, c, p) => OnEventEnd(s, c, p));
            Register(0x05E, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent)
                    .Range("MyRoomExitBit", p.Buffer[22], 0, 9)
                    .OneOf("MyRoomExitMode", p.Buffer[23], 0, 1, 2, 3, 4, 125, 126, 127),
                (s, c, p) => OnMapRect(s, c, U32(p, 4), p.Buffer[22], p.Buffer[23]));
            Register(0x0E7, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent | BlockedState.AbnormalStatus | BlockedState.Crafting | BlockedState.PreventAction)
                    .OneOf<ReqlogoutMode>(U16(p, C2sReqlogout.OffMode))
                    .OneOf<ReqlogoutKind>(U16(p, C2sReqlogout.OffKind)),
                (s, c, p) => OnReqLogout(s, c, U16(p, C2sReqlogout.OffMode), U16(p, C2sReqlogout.OffKind)));
            // the C++ checks Mode against the REQLOGOUT_MODE enum
            Register(0x0E8, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent | BlockedState.Dead | BlockedState.AbnormalStatus | BlockedState.Crafting |
                               BlockedState.PreventAction | BlockedState.Engaged | BlockedState.Monstrosity)
                    .OneOf<ReqlogoutMode>(U32(p, 4))
                    .MustNotEqual(c.Animation == Anim.Healing && U32(p, 4) == (uint)CampMode.On ? 1 : 0, 1, "Requested healing when already healing")
                    .MustNotEqual(c.Animation != Anim.Healing && U32(p, 4) == (uint)CampMode.Off ? 1 : 0, 1, "Requested stop healing when not healing"),
                (s, c, p) => OnCamp(s, c, U32(p, 4)));

            // ---- the client's own state and settings (ZoneHost.Self) ----
            // CLSTAT: no reply; the client may be reporting its state or lag
            Register(0x00F, (s, c, p) => Pv(c), (s, c, p) => { });
            // FRAGMENTS: the fields are not documented, so nothing is checked
            Register(0x04B, (s, c, p) => Pv(c), OnFragments);
            Register(0x061, (s, c, p) => Pv(c).Range("unknown00", p.Buffer[4], 0, 1), (s, c, p) => SendLocalPlayerPackets(s, c));
            Register(0x053, (s, c, p) => Pv(c)
                    .OneOf("Mode", p.Buffer[5], 0, 1, 2, 3, 4)
                    .Range("Count", p.Buffer[4], 0, 16),
                OnLockstyle);
            Register(0x0DB, (s, c, p) => Pv(c)
                    .OneOf("Kind", p.Buffer[6], 0, 1)
                    .MustEqual(p.Buffer[4], 0, "unknown00 must be 0")
                    .MustEqual(p.Buffer[5], 0, "unknown00 must be 0"),
                OnConfigLanguage);
            Register(0x0E0, (s, c, p) => Pv(c)
                    .OneOf("msgType", U32(p, UserMsgTypeOffset), 0x00, 0x11, 0x12, 0x13, 0x21, 0x22, 0x23, 0x31, 0x32, 0x33,
                           0x41, 0x42, 0x43, 0x51, 0x52, 0x53, 0x61, 0x73)
                    .MustEqual(UserMessage(p).Length <= 123 ? 1 : 0, 1, "sMessage longer than 123 bytes"),
                OnSetUserMsg);
            // BATTLEFIELD_REQ: not implemented in the C++ either
            Register(0x112, (s, c, p) => Pv(c).OneOf("Kind", p.Buffer[4], 0, 1, 2), (s, c, p) => { });
            Register(0x118, (s, c, p) => Pv(c).OneOf("Mode", p.Buffer[4], 0, 1), OnUnityToggle);
            Register(0x0B5, (s, c, p) => ValidateChatStd(c, p), OnChatStd);
            Register(0x0B6, (s, c, p) => ValidateChatName(c, p), OnChatName);
            Register(0x05A, (s, c, p) => Pv(c), OnReqConquest);
            Register(0x11B, (s, c, p) => Pv(c).OneOf("Mode", p.Buffer[4], 0, 1), OnMasteryDisplay);

            // ---- trading (ZoneHost.Trade) ----
            Register(0x032, (s, c, p) => ValidateTradeReq(c, p), OnTradeReq);
            Register(0x033, (s, c, p) => ValidateTradeRes(c, p), OnTradeRes);
            Register(0x034, (s, c, p) => ValidateTradeList(c, p), OnTradeList);
            Register(0x036, (s, c, p) => ValidateItemTransfer(c, p), OnItemTransfer);

            // ---- items and equipment (ZoneHost.Items) ----
            Register(0x028, (s, c, p) => ValidateItemDump(c, p), OnItemDump);
            Register(0x029, (s, c, p) => ValidateItemMove(c, p), OnItemMove);
            // retail honors every container, even one the char cannot reach now
            Register(0x03A, (s, c, p) => Pv(c).IsValidContainer("Category", U32(p, 4)), OnItemStack);
            Register(0x050, (s, c, p) => ValidateEquipSet(c, p), OnEquipSet);
            Register(0x051, (s, c, p) => ValidateEquipsetSet(c, p), OnEquipsetSet);

            // ---- NPC shops (ZoneHost.Shop) ----
            Register(0x083, (s, c, p) => ValidateShopBuy(c, p), OnShopBuy);
            Register(0x084, (s, c, p) => ValidateShopSell(c), OnShopSellReq);
            Register(0x085, (s, c, p) => ValidateShopSellSet(c, p), OnShopSellSet);

            // ---- motions, sitting and the client's config flags (ZoneHost.Motion) ----
            // NETEND: explicitly ignored
            Register(0x00D, (s, c, p) => Pv(c), (s, c, p) => { });
            // the purpose of 0x01C is unknown
            Register(0x01C, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent),
                (s, c, p) => log_($"GP_CLI_COMMAND_UNKNOWN: unknown00={U16(p, 4)} padding00={U16(p, 6)} unknown01={U32(p, 8)}"));
            Register(0x05D, (s, c, p) => ValidateMotion(c, p), OnMotion);
            Register(0x0DC, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent).OneOf("SetFlg", p.Buffer[16], 1, 2), OnConfig);
            Register(0x0EA, (s, c, p) => ValidateSit(c, U32(p, 4)), (s, c, p) => OnSit(s, c, U32(p, 4)));
            Register(0x113, (s, c, p) => ValidateSit(c, U32(p, 4)).Range("ChairId", U32(p, 8), 0, 20),
                (s, c, p) => OnSitChair(s, c, U32(p, 4), U32(p, 8)));
            Register(0x119, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent), (s, c, p) => Push(s, EmoteList(c)));
            Register(0x11D, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent)
                    .MustEqual(U32(p, 4), c.Id, "Character ID mismatch")
                    .MustEqual(U16(p, 8), c.TargId, "Target ID mismatch"),
                OnJump);
            Register(0x0EB, (s, c, p) => Pv(c), (s, c, p) => OnReqSubMapNum(s, c));
            // no list of the valid sub-map numbers per zone yet; the C++ Event-state check is on currentEvent, which is
            // never null (an empty EventInfo outside events), so it never refuses
            Register(0x0F2, (s, c, p) => Pv(c).Range("State", U16(p, 4), 1, 2), OnSubMapChange);

            // ---- guild shops (ZoneHost.Guild) ----
            Register(0x0AA, (s, c, p) => ValidateGuildBuy(c, p), OnGuildBuy);
            Register(0x0AB, (s, c, p) => ValidateGuildList(c), OnGuildBuyList);
            Register(0x0AC, (s, c, p) => ValidateGuildSell(c, p), OnGuildSell);
            Register(0x0AD, (s, c, p) => ValidateGuildList(c), OnGuildSellList);

            // ---- synthesis (ZoneHost.Synth) ----
            Register(0x096, (s, c, p) => ValidateCombineAsk(c, p), OnCombineAsk);
            // EFFECTEND: the client's synth animation finished; the result waits on the synth timer, not this
            Register(0x059, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent), (s, c, p) => { });

            // ---- events (ZoneHost.Events): an event that ends with the char placed somewhere, a typed-in password ----
            Register(0x05C, (s, c, p) => Pv(c).MustEqual(p.Buffer[30], 1, "Mode not 1").IsInEvent(U16(p, 26)), OnEventEndXzy);
            Register(0x060, (s, c, p) => Pv(c).IsInEvent(), OnPasswards);

            // ---- menus (ZoneHost.Menus) ----
            Register(0x064, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent)
                    .MustEqual(U32(p, 4), c.Id, "Character ID mismatch")
                    .MustEqual(U16(p, 72), c.TargId, "Character targid mismatch")
                    .Range("TableIndex", U16(p, 74), 0, 7),
                OnScenarioItem);
            Register(0x10F, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent), (s, c, p) => Push(s, Currencies1(c)));
            Register(0x115, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent), (s, c, p) => Push(s, Currencies2(c)));
            // 0x116 UNITY_MENU (Kind u32 @4: which set of 32 pages) and 0x117 UNITY_QUEST (Kind u32 @4, always 0): the C++'s
            // stubs, GP_SERV_COMMAND_UNITY and the unity standings
            Register(0x116, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent).Range("Kind", U32(p, 4), 0, 1), OnUnityMenu);
            Register(0x117, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent).MustEqual(U32(p, 4), 0, "Kind not 0x0"), OnUnityMenu);
            Register(0x114, (s, c, p) => Pv(c), (s, c, p) => Push(s, GameOkPackets.Homepoints(c)));
            Register(0x058, (s, c, p) => ValidateRecipe(c, p), OnRecipe);
            Register(0x052, (s, c, p) => ValidateEquipsetCheck(c, p), OnEquipsetCheck);
            Register(0x0D8, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent)
                    .MustEqual(c.Id, U32(p, 12), "Character ID mismatch")
                    .MustEqual(c.TargId, U16(p, 4), "Targid mismatch"),
                OnDungeonParam);

            // ---- parties and alliances (ZoneHost.Party) ----
            Register(0x06E, ValidateGroupSolicitReq, OnGroupSolicitReq);
            Register(0x06F, ValidateGroupLeave, OnGroupLeave);
            Register(0x070, ValidateGroupBreakup, OnGroupBreakup);
            Register(0x071, ValidateGroupStrike, OnGroupStrike);
            Register(0x074, ValidateGroupSolicitRes, OnGroupSolicitRes);
            Register(0x076, ValidateGroupListReq, OnGroupListReq);
            Register(0x077, ValidateGroupChange2, OnGroupChange2);
            Register(0x078, (s, c, p) => Pv(c).BlockedBy(BlockedState.InEvent), OnGroupCheckId);
            Register(0x11C, ValidateGroupRequest, OnGroupRequest);

            // ---- the help desk, wide scan, /translate, the world pass, map markers (ZoneHost.Misc) ----
            RegisterMiscHandlers();
            RegisterBazaarHandlers();
            RegisterBlacklistHandlers();
            RegisterInspectHandlers();
            RegisterLinkshellHandlers();
            RegisterRoeHandlers();
            RegisterMeritHandlers();
            RegisterNominateHandlers();
            RegisterWarpHandlers();
            RegisterAssistHandlers();
            RegisterSubcontainerHandlers();
            RegisterMogHouseHandlers();
            RegisterGardenHandlers();
            RegisterMyroomJobHandler();
            RegisterTreasurePoolHandlers();
            RegisterDeliveryBoxHandlers();
            RegisterAuctionHandlers();
            RegisterExtendedJobHandler();
            RegisterFishingHandler();
            RegisterChocoboRaceHandler();
            RegisterItemUseHandler();
        }

        // GP_CLI_COMMAND_ACTION::validate; Talk's blocked states send a release in OnAction instead
        private static PacketValidator ValidateAction(CharData c, BasicPacket p)
        {
            ushort actionId = U16(p, C2sAction.OffActionID);
            var pv = Pv(c).OneOf<ActionId>(actionId);
            if (!pv.Valid) return pv;
            switch ((ActionId)actionId)
            {
                case ActionId.AttackOff:
                    return pv.BlockedBy(BlockedState.Charmed).IsEngaged();
                case ActionId.ChangeTarget:
                    return pv.BlockedBy(BlockedState.PreventAction).IsEngaged();
                case ActionId.HomepointMenu:
                case ActionId.RaiseMenu:
                case ActionId.TractorMenu:
                    return pv.MustEqual(c.Hp <= 0 ? 1 : 0, 1, "Character is not dead.");
                case ActionId.Attack:
                    return pv.BlockedBy(BlockedState.InEvent | BlockedState.Healing | BlockedState.Sitting | BlockedState.Crafting |
                                        BlockedState.Fishing | BlockedState.PreventAction | BlockedState.Gestation);
                case ActionId.CastMagic:
                case ActionId.JobAbility:
                case ActionId.Shoot:
                case ActionId.Weaponskill:
                case ActionId.MonsterSkill:
                    return pv.BlockedBy(BlockedState.InEvent | BlockedState.Healing | BlockedState.Crafting | BlockedState.Fishing |
                                        BlockedState.PreventAction | BlockedState.Mounted | BlockedState.Gestation)
                        .MustEqual(c.Animation == Anim.None || c.Animation == Anim.Attack ? 1 : 0, 1, "Character in invalid animation state.");
                case ActionId.Fish:
                    return pv.BlockedBy(BlockedState.InEvent | BlockedState.Healing | BlockedState.Sitting | BlockedState.Crafting |
                                        BlockedState.Fishing | BlockedState.PreventAction | BlockedState.Mounted);
                case ActionId.Mount:
                    return pv.BlockedBy(BlockedState.InEvent | BlockedState.Healing | BlockedState.Sitting | BlockedState.Crafting |
                                        BlockedState.Fishing | BlockedState.Monstrosity);
                case ActionId.Dismount:
                case ActionId.ChocoboDig:
                    return pv.BlockedBy(BlockedState.PreventAction)
                        .MustEqual(c.Animation == Anim.Chocobo || c.Animation == Anim.Mount ? 1 : 0, 1, "Character is not mounted.");
                default:
                    return pv;
            }
        }
    }
}
