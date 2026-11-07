using System;
using System.Collections.Generic;

namespace XI.Zone
{
    // CONTAINER_ID: the client's item container index, which every per-container array is ordered by.
    public enum Container : byte
    {
        Inventory = 0, MogSafe = 1, Storage = 2, TempItems = 3, MogLocker = 4, MogSatchel = 5, MogSack = 6, MogCase = 7,
        Wardrobe = 8, MogSafe2 = 9, Wardrobe2 = 10, Wardrobe3 = 11, Wardrobe4 = 12, Wardrobe5 = 13, Wardrobe6 = 14,
        Wardrobe7 = 15, Wardrobe8 = 16, RecycleBin = 17,
    }

    // look_t: the face/race/size and the visible item model per equipment slot (the model ids, not item ids).
    public sealed class Look
    {
        public byte Face, Race, Size;
        public ushort Head, Body, Hands, Legs, Feet, Main, Sub, Ranged;

        // look_t::modelid -- face in the low byte, race in the high one
        public ushort ModelId => (ushort)(Face | Race << 8);

        public Look Clone() => (Look)MemberwiseClone();

        // the memcpy of one look_t over another
        public void CopyFrom(Look o)
        {
            Face = o.Face; Race = o.Race; Size = o.Size;
            Head = o.Head; Body = o.Body; Hands = o.Hands; Legs = o.Legs; Feet = o.Feet;
            Main = o.Main; Sub = o.Sub; Ranged = o.Ranged;
        }
    }

    public sealed class Item
    {
        public Container Location;
        public byte Slot;
        public ushort ItemId;
        public uint Quantity;
        public string Signature = "";       // getSignature: the name packed in the exdata, mirrored by ItemSignature.Set / Get
        public byte[] Extra = new byte[24]; // m_extra: the item's exdata, as char_inventory.extra holds it
        public bool InTransaction;          // ItemState InTransaction: an open transaction has claimed the stack
        public bool InBazaar;               // ItemState Bazaar: on display in the char's bazaar
        public uint CharPrice;              // m_charPrice: char_inventory.bazaar, the bazaar price (0 when not listed)
        public bool Furnishing;             // ITEM_FURNISHING: the id is in item_furnishing
        public bool PlacedFurniture;        // ItemState PlacedFurniture: loaded installed in a mog safe
        public long AssignUnixSec;          // m_AssignTime: when a charged item with charges left was last equipped
        public EquipmentInfo Equip;         // the item's own CItemEquipment once an augment added to it (SetAugmentMod), else null
        public bool Augmented;              // ITEM_AUGMENTED: SetAugmentMod gave it an augment
        public bool Dirty;                  // isDirty: the exdata changed (setExData), saved and re-sent on the char's PostTick
        // CItemWeapon::m_wsunlockpoints of this item once setWeaponskillPointsNeeded set it (null: the template's
        // item_weapon.unlock_points); kept with the item, not saved, as the C++'s instance field
        public ushort? UnlockPointsNeeded;

        // CItemFurnishing::isInstalled: Exdata::Furniture's Installed bit
        public bool Installed => Furnishing && (Extra[1] & 0x40) != 0;
    }

    // one char_equip row: the item in `Container`/`InvSlot` sits in equipment slot `EquipSlot` (SLOTTYPE)
    public struct EquipEntry
    {
        public byte EquipSlot, InvSlot;
        public Container Container;
    }

    // equip_change_t: an equip (container/slot into EquipSlot) or an unequip of `Item` from EquipSlot
    public sealed record EquipChange(Container Container, byte ContainerSlot, byte EquipSlot, Item Item, bool Equipping);

    // DeathParams (death_state.h): the circumstances of a death, decided by whatever caused it and carried for as
    // long as the death lasts
    public readonly record struct DeathParams(bool LosesExp, bool Mijin)
    {
        public static readonly DeathParams Default = new DeathParams(true, false);
    }

    // The slice of CCharEntity the zone-in packets read. Field meanings follow charutils::LoadChar; the derived parts
    // (MaxHp/MaxMp/BaseStats from CalculateStats, the ability bits from BuildingCharAbilityTable) are filled by CharLoader.
    public sealed class CharData : BattleEntity, IPathMover
    {
        // a charmed char is moved by CPlayerCharmController's path (ZoneHost.Charm.cs)
        float IPathMover.X { get => X; set => X = value; }
        float IPathMover.Y { get => Y; set => Y = value; }
        float IPathMover.Z { get => Z; set => Z = value; }
        byte IPathMover.Rotation { get => Rotation; set => Rotation = value; }
        ushort IPathMover.Moving { get => Moving; set => Moving = value; }
        byte IPathMover.Speed => Speed;
        byte IPathMover.Animation => Animation;
        BattleEntity IPathMover.PathBattleTarget => CharmTarget;
        void IPathMover.MarkPositionDirty() => UpdateMask |= CharStats.UpdatePos;
        float IPathMover.PathHitboxRadius => ModelHitboxSize10 / 10.0f * 0.5f;
        // CPlayerCharmController: its path (made with the controller) and the target it fights for its charmer
        internal PathFind CharmPath;
        public BattleEntity CharmTarget;
        public const int MaxContainers = 18;
        public const int MaxJobs = 24;       // MAX_JOBTYPE: Job::NON .. Job::MON
        public const byte AllegiancePlayer = 1; // xi::Allegiance::Player

        public ushort PrevZone, Boundary;
        // loc.destination: the zone the char was sent to (pos_zone at load), which the CharZone IPC names
        public ushort Destination;
        // CZone::CharZoneOut's loc.zone = nullptr on a zone change: getZone() is loc.destination from then on (the
        // effects persist::flush drops see the zone the char is going to)
        public bool ZonedOut;
        // m_TargID: the facetarget of the last position update
        public ushort FaceTarget;
        // m_ActionOffsetPos: a Geo- spell's target position plus the cast packet's offset, where its luopan goes
        public float ActionOffsetX, ActionOffsetY, ActionOffsetZ;
        public uint AccId; // accid: the account the char belongs to
        public byte Nation;

        // expChain_t: the EXP chain the kills build, and when its window closes
        public int ExpChainNumber;
        public long ExpChainTimeMs;

        // capacityChain_t: the capacity chain lv100+ kills build
        public int CapacityChainNumber;
        public long CapacityChainTimeMs;

        // CRecastContainer's loot recasts (LootRecastID): when the special drops come back, cleared on zoning
        public readonly Dictionary<int, long> LootRecastUntilMs = new Dictionary<int, long>();


        // SpawnNPCList: the NPC ids this client has been sent a spawn for
        public readonly HashSet<uint> SpawnNpcList = new HashSet<uint>();
        public readonly HashSet<uint> SpawnMobList = new HashSet<uint>();
        public readonly HashSet<uint> SpawnPetList = new HashSet<uint>();
        public readonly HashSet<uint> SpawnTrustList = new HashSet<uint>();
        // PTrusts: the char's trusts in the order they were called; m_LeaderCreatedPartyTime: when a member last
        // joined while it led (NowMs, timer::now(); getPartyLastMemberJoinedTime converts it with to_utc)
        public readonly List<TrustEntity> PTrusts = new List<TrustEntity>();
        public long LeaderCreatedPartyTime;
        // SpawnPCList: the other chars this client has been sent a spawn for, by id
        public readonly Dictionary<uint, CharData> SpawnPcList = new Dictionary<uint, CharData>();

        public Look Look = new Look();
        public byte[] JobLevels = new byte[MaxJobs];
        public uint UnlockedJobs;
        // m_LevelRestriction: a cap on the main level set by levelRestriction (0: none); a new char on every zone-in
        public uint LevelRestriction;

        public ushort[] BaseStats = new ushort[7]; // str dex vit agi int mnd chr: PChar->stats, which 0x00A copies


        // PLatentEffectContainer
        public readonly LatentEffectContainer Latents;

        // m_EffectsChanged: PostTick re-sends 0x037/0x067/0x063 and the party's 0x076
        public CharData() { TargId = 0x400; Speed = 50; AnimationSpeed = 50; Latents = new LatentEffectContainer(this); }
        public bool EffectsChanged;

        // the equipment that passed EquipArmor, per equipment slot, and m_Weapons[SLOT_MAIN] (an unarmed weapon when
        // nothing is in the main hand)
        public readonly EquipmentInfo[] Equipped = new EquipmentInfo[16];
        public WeaponInfo MainWeapon;
        // m_Weapons[SLOT_SUB / SLOT_RANGED / SLOT_AMMO] as a CItemWeapon: null when unset or a shield, the unarmed weapon
        // once the sub slot is emptied
        public WeaponInfo SubWeapon, RangedWeapon, AmmoWeapon;
        public bool DualWield; // m_dualWield: a weapon (not a shield) is in the sub slot

        // CAttackState and the claim: the battle target (battleTarget_), the attack state's own target (null when not
        // in the state), when the next swing is due (m_attackTime counts down to it), the swing delay, the
        // controller's engage lockout and error-message time, and PClaimedMob
        public BattleEntity BattleTarget, AttackTarget;
        public long SwingDelayMs, EngageLockedUntilMs, LastErrMsgMs;
        public MobEntity ClaimedMob;
        // m_LastAttacker; m_DeathTimestamp, which the death state's timer runs from (NowMs, 0 while alive); the next
        // 16 s death sync (m_deathSyncTime); the zone's clock, set when the char is loaded
        public uint LastAttackerId;
        public long LastAttackMs; // CPlayerController::m_LastAttackTime: its last melee round (CCharEntity::OnAttack)
        public long DeathAtMs, DeathSyncAtMs;
        public Func<long> Clock;
        // nextDeath_ (DeathParams staged by player:die for the next death, null when none) and the death state's own
        // params (CDeathState::params_, set when the death begins): losesExp, mijin (Mijin Gakure: no weakness, half
        // HP back on raise)
        public DeathParams? NextDeath;
        public DeathParams DeathState = DeathParams.Default;
        // CBaseEntity's status (xi::Status: Disappear from the 0x00A's zone-in to its login packets, then Normal; a zone
        // change in flight is zoneChanges_, no Shutdown is kept; and what setStatus gives), animationsub
        // (setAnimationSub: carried by no char packet, read by the trusts' SUB_ANIMATION gambit) and namevis (hideName:
        // carried by no char packet, read by CMagicState's target check)
        public byte Status, AnimationSub, NameVis;
        // m_hasRaise (the raise level held, 4 Arise), m_hasArise, m_weaknessLvl (raises in a row); the death state's
        // raise menu (when it goes, whether it went) and the accepted raise's stand-up
        public byte HasRaise, WeaknessLvl;
        // m_hasTractor and m_StartActionPos: a tractor offer open, and where it pulls the char to
        public byte HasTractor;
        public float TractorX, TractorY, TractorZ;
        public byte TractorRotation;
        public bool HasArise, RaiseSent;
        public long RaiseMenuAtMs, RaiseAcceptedAtMs;

        // InventorySyncState: the equip changes flushEquipChanges sends once the datagram's packets are parsed, the
        // containers they touched (a std::set, so in container order), and the containers SendInventory has synced
        public readonly List<EquipChange> EquipChanges = new List<EquipChange>();
        public readonly SortedSet<Container> DirtyContainers = new SortedSet<Container>();
        public uint SyncedContainers;


        // WorkingSkills (what the client is sent: capped, with bonuses and the blue-text bit) and RealSkills (char_skills,
        // in tenths)
        public readonly ushort[] WorkingSkills = new ushort[Skill.Max], RealSkills = new ushort[Skill.Max];
        public readonly byte[] WorkingSkillRank = new byte[Skill.Max], RealSkillRank = new byte[Skill.Max];

        // char_exp: jobs.exp per job, MeritMode, and the merit / limit points
        public readonly ushort[] JobExp = new ushort[MaxJobs];
        public bool MeritMode;
        public ushort MeritPoints, LimitPoints;

        // char_job_points per job: capacity points, current job points, job points spent
        public readonly (ushort Capacity, ushort Current, ushort Spent)[] JobPoints = new (ushort, ushort, ushort)[MaxJobs];
        // CJobPoints' jptype0..9 levels per job; a job with no char_job_points row loaded has type ids 0
        public readonly byte[,] JobPointLevels = new byte[MaxJobs, 10];
        public readonly bool[] JobPointRows = new bool[MaxJobs];
        // PJobPoints->current_gifts: the job_point_gifts mods RefreshGiftMods added to ModStat
        public readonly List<(int Mod, short Value)> JobPointGifts = new List<(int, short)>();

        // PMeritPoints' upgrades (null when the loader has no merit table)
        public MeritPoints Merits;

        // profile.home_point.destination (chars.home_zone) and profile.unity_leader
        public ushort HomeZone;
        public float HomeX, HomeY, HomeZ;          // profile.home_point.p (chars.home_x/y/z, home_rot)
        public byte HomeRot;
        public WarpRequest RequestedWarp;          // requestedWarp: the zone tick sends the char to its home point
        public AmanState Aman;                     // m_AMAN: built on first use (ZoneHost.Aman)
        public long AfterZoneInAtMs;               // the login's queued zoneutils::AfterZoneIn, 4 s after it (0 none)
        public byte UnityLeader;
        public byte GmLevel;                       // visibleGmLevel
        public byte AccountGmLevel;                // m_GMlevel: chars.gmlevel, what commands are checked against
        public bool GmHidden;                      // m_isGMHidden
        public byte Allegiance = AllegiancePlayer;  // allegiance (setAllegiance)
        public bool PriorityRender;                 // priorityRender: CliPriorityFlag in the 0x037 and 0x00D
        public ushort Costume2;                     // m_Costume2: a model in place of the face, the ranged slot hidden
        public byte ModelSize;                      // modelSize (setModelSize; a char's packets carry look.size)
        public bool WallhackEnabled, FrozenFlagged;  // wallhackEnabled, isFrozenFlagged (setWallhack / setFreezeFlag)
        // isLinkDead: no client update for 5 s (MapSessionContainer::cleanupSessions), the char packets' LinkDeadFlag
        public bool IsLinkDead;
        public ushort EquipBlock, StatsDebilitation; // m_EquipBlock (locked slots), m_StatsDebilitation
        public bool JobMasterDisplay;
        public bool MentorUnlocked;                // CAMANContainer m_mentorUnlocked: chars.mentor > 0
        public uint PlayTimeSec;
        public long LastOnline;                    // UNIX_TIMESTAMP(chars.lastupdate) at load: the last zone or logout
        // m_SaveTime: when this map loaded the char (NowMs, the steady clock): GetPlayTime() counts on timer::now()
        // from here, so an Earth time offset (SetTimeOffset) leaves the play time alone
        public long LoginMs;

        // SAVE_CONF: chars.settings, chatfilters_1, chatfilters_2 memcpy'd in order (0x00A ConfData is these 12 bytes)
        public uint Settings, ChatFilters1, ChatFilters2;
        public bool InviteFlg => (Settings & 1u << 0) != 0;
        public bool AwayFlg => (Settings & 1u << 1) != 0;
        public bool AnonymityFlg => (Settings & 1u << 2) != 0;
        public bool MentorFlg => (Settings & 1u << 25) != 0;
        public bool NewAdventurerOffFlg => (Settings & 1u << 26) != 0;
        public bool DisplayHeadOffFlg => (Settings & 1u << 27) != 0;
        public bool RecruitFlg => (Settings & 1u << 29) != 0;

        // CItemContainer GetSize() per container (GetBuff matches it for every container the loader fills but the
        // storage, whose buff is StorageBuff)
        public byte[] StorageSize = new byte[MaxContainers];
        // each container's m_buff (the size before the 0..80 clamp), as char_storage loaded it
        public int[] ContainerBuff = new int[MaxContainers];
        public int StorageBuff; // LOC_STORAGE's m_buff: the installed furnishings' storage, the size clamped 0..80
        public ushort Moghancement; // m_moghancementID (chars.moghancement): the moghancement key item applied (Moghancement)
        public bool MogLockerAccess; // charutils::hasMogLockerAccess as of the last 0x01C / item move (ZoneHost.Items)
        public uint MoghouseId; // m_moghouseID: whose mog house the char is in (its own id = its own), 0 outside one
        public bool MogHouseOpen; // MogHouseContainer open_: the char's own mog house is open to its party (ZoneHost.MogHouse)
        public readonly List<uint> MogHouseVisitors = new List<uint>(); // MogHouseContainer visitors_
        public ushort? WideScanTarget; // WideScanTarget: the act index of the NPC the char tracks with wide scan (ZoneHost.Misc)
        // GMCallContainer: the packets of the GM call being sent (pktId_, packets_) and when the last one went out
        public byte GmCallPktId;
        public readonly List<byte[]> GmCallPackets = new List<byte[]>();
        public long? GmCallLastMs;
        // CItemContainer LastSortingTime (NowMs; null before the first sort) and SortingPacket per container
        public long?[] LastSortingMs = new long?[MaxContainers];
        public byte[] SortingPackets = new byte[MaxContainers];

        // CCharEntity::Container: the universal container for exchange, synthesis and shops
        public readonly TradeContainer Exchange = new TradeContainer();

        // CCharEntity::UContainer: the universal container, which only the delivery box uses here
        public readonly UContainer UContainer = new UContainer();

        // CAIContainer::QueueAction with no delay: what runs on the char's next tick
        public readonly List<Action> ActionQueue = new List<Action>();
        // m_ah_history: the open auction listings as the sales status window last loaded them (itemid, stack, price),
        // and when (m_AHHistoryTimestamp, ms; the window reloads at most every 5 s)
        public readonly List<(ushort ItemId, byte Stack, uint Price)> AhHistory = new List<(ushort, byte, uint)>();
        public long AhHistoryTimestampMs = long.MinValue;

        // CCharEntity::TradeContainer: what the char offers an NPC (c2s 0x036), which CTradeContainer shows the script
        public readonly TradeContainer Trade = new TradeContainer();
        internal ZoneHost.NpcTradeTransaction NpcTrade;  // the offer to an NPC while its script can still confirm it
        public readonly PendingTrade TradePending = new PendingTrade();
        internal ZoneHost.PlayerTrade PlayerTrade;       // a player trade this char initiated

        // isSettingBazaarPrices, BazaarID (the bazaar being browsed) and BazaarCustomers (who browses this one); a
        // CharData is loaded per zone-in, which starts them clean as the CCharEntity constructor does
        public bool IsSettingBazaarPrices;
        public BazaarRef BazaarId;
        public readonly List<BazaarRef> BazaarCustomers = new List<BazaarRef>();

        // CCharEntity::hasBazaar: something in the inventory is priced and the char is not setting prices (Monstrosity
        // has no state here)
        public bool HasBazaar => !IsSettingBazaarPrices && Items.Exists(i => i.Location == Container.Inventory && i.CharPrice != 0);

        // CBattleEntity::PParty, CCharEntity::InvitePending and ReloadParty (charutils::ReloadParty on the next tick)
        internal ZoneHost.Party Party;
        internal ZoneHost.TreasurePool TreasurePool; // PTreasurePool
        internal ZoneHost.Linkshell Linkshell1, Linkshell2; // PLinkshell1/2: the shells of the equipped LS items
        public readonly PendingInvite InvitePending = new PendingInvite();
        public bool ReloadParty;
        public bool BlockingAid; // m_isBlockingAid: /blockaid, until the char changes areas
        public long? LastProposalCloseMs; // lastProposalCloseTime_: NowMs when its last /nominate closed

        public List<Item> Items = new List<Item>();
        public List<EquipEntry> Equip = new List<EquipEntry>(); // in equipment slot order
        public bool EquipDirty; // char_equip / char_look need saving (SaveCharEquip, SaveCharLook)
        // CCharEntity::persist(): what the next persist sweep writes (CharPersist; equip, look and char vars are
        // written as they change)
        public byte Persist;

        // the bitsets 0x0AA / 0x0AC copy verbatim
        public byte[] SpellList = new byte[128];
        public byte[] WeaponSkills = new byte[32];
        public byte[] Abilities = new byte[64];
        public byte[] PetCommands = new byte[64];
        public PetZoningInfo PetZoning = new PetZoningInfo(); // petZoningInfo

        // m_SetBlueSpells: the set blue magic, one byte per slot (the spell id less 0x200)
        public readonly byte[] SetBlueSpells = new byte[20];
        public readonly AutomatonInfo Automaton = new AutomatonInfo();
        public byte[] Traits = new byte[18];
        // m_magicEvasion: the MEVA BuildingCharTraitsTable added last; TraitsBuilt once it has run (the dual wield
        // check assumes the trait until then)
        public short TraitMagicEvasion;
        public bool TraitsBuilt;
        public byte[] LearnedAbilities = Array.Empty<byte>(); // chars.abilities: ADDTYPE_LEARNED unlocks
        public byte[] LearnedWeaponskills = new byte[8];      // chars.weaponskills: m_LearnedWeaponskills (xi::bitset<64>)

        // CCharEntity::GetGender -- 1 male, 0 female (races alternate m/f, then Mithra 7 and Galka 8)
        public byte Gender => (byte)(Look.Race % 2 ^ (Look.Race > 6 ? 1 : 0));


        // SetRaceHitbox: Tarutaru 0.3, Galka 0.8, everyone else 0.4 -- packets carry it *10
        public byte ModelHitboxSize10 => ModelHitboxOverride is float h ? (byte)(h * 10) : (byte)(Look.Race switch { 5 or 6 => 3, 8 => 8, _ => 4 });
        public float? ModelHitboxOverride; // setHitboxSize: modelHitboxSize in place of the race's
        // m_PMonstrosity: set only while the job is MON (Monstrosity.cs); a Monipulator correlates as its species'
        // ecosystem (m_EcoSystem) and walks at its species' base speed (baseSpeed, else map.BASE_SPEED)
        public MonstrosityData Monstrosity;
        public byte EcoSystem = Ecosystem.Humanoid;
        public byte? BaseSpeedOverride;


        // GetTimeSinceDeath (0 while alive)
        public long MsSinceDeath => DeathAtMs != 0 && Clock != null ? Clock() - DeathAtMs : 0;

        // 6min + GetTimeUntilDeathHomepoint(), in whole seconds: a living character is a full hour from the forced
        // homepoint, a dead one counts down from it
        public uint DeadRemainingSec => (uint)(((6 + 60) * 60 * 1000L - MsSinceDeath) / 1000);

        public Item FindItem(Container c, byte slot) => Items.Find(i => i.Location == c && i.Slot == slot);

        // ---- blob-backed state, kept in the C++ struct layouts the chars/char_unlocks blobs are memcpy'd from ----

        public const int MaxQuestArea = 11, MaxMissionArea = 15;
        public const int QuestLogSize = 64;    // questlog_t: uint8 current[32], complete[32]
        public const int MissionLogSize = 70;  // missionlog_t: uint16 current, statusUpper, statusLower; bool complete[64]

        public byte[] ZonesVisited = new byte[38];                       // m_ZonesVisitedList
        public byte Languages;                                           // search.language (languages_t bits)
        public byte[] KeyItems = new byte[8 * 128];                      // keyitems_t: 8 x {bitset<512> keyList, seenList}
        public byte[] QuestLog = new byte[MaxQuestArea * QuestLogSize];  // questlog_t[MAX_QUESTAREA]
        public byte[] MissionLog = NewMissionLog();                      // missionlog_t[MAX_MISSIONAREA]
        public byte[] AssaultLog = new byte[2 + 128];                    // assaultlog_t
        public byte[] CampaignLog = new byte[2 + 512];                   // campaignlog_t
        public byte[] HomepointTele = new byte[56];                      // telepoint_t: uint32 access[4], int32 menu[10]
        public byte[] SurvivalTele = new byte[56];
        // waypoint_t {uint32 access[2]; bool confirmation} and the teleport_t.eschanPortal after it: 0x063 HOMEPOINTS
        // copies 16 bytes from waypoints.access, which runs into both
        public byte[] Waypoints = new byte[16];
        // teleport_t's masks by TELEPORT_TYPE: outpostSandy / Bastok / Windy, runicPortal, pastMaw, (5 is the abyssea
        // conflux, below), campaignSandy / Bastok / Windy
        public uint[] TeleportMasks = new uint[9];
        public byte[] AbysseaConflux = new byte[9];                      // uint8 abysseaConflux[MAX_ABYSSEAZONES]
        public byte[] UniqueEvents = new byte[20];                       // uint32 m_uniqueEvents[5] (char_unlocks.unique_event)
        public ushort Costume;                                           // m_Costume
        public byte[] MazeVouchers = new byte[8];                        // maze_t: bitset<64> vouchers, bitset<512> runes
        public byte[] MazeRunes = new byte[64];
        public byte[] ClaimedDeeds = new byte[20];                       // uint32 m_claimedDeeds[5] (char_unlocks.claimed_deeds)
        public readonly System.Collections.BitArray EminenceActive = new System.Collections.BitArray(4096); // m_eminenceCache.activemap
        public long EminenceLastWriteoutMs = long.MinValue / 2;          // m_eminenceCache.lastWriteout
        public bool NotifyTimedRecord;                                   // m_eminenceCache.notifyTimedRecord
        public byte[] EminenceLog = new byte[700];                       // eminencelog_t: uint16 active[31]; uint32 progress[31]; uint8 complete[512]
        public byte[] BazaarMessage = Array.Empty<byte>();               // bazaar.message, up to its terminator
        public ushort Title;                                             // profile.title
        public byte[] Titles = new byte[143];                            // m_TitleList: one bit per obtained title
        public byte CampaignAllegiance;                                  // profile.campaign_allegiance

        // char_profile: rank per nation, rank points, and fame indexed by xi::FameArea (3 Jeuno and 4 Selbina/Rabao
        // derive from the nations' fame and are never stored)
        public byte[] Rank = new byte[3];
        public ushort RankPoints;
        public ushort[] Fame = new ushort[16];
        public byte Genkai;                                              // jobs.genkai: the level cap
        public ushort MhFlag;                                            // profile.mhflag: mog house bits (0x20 = 2nd floor)

        // char_points: the row is optional, and 0x110 leaves its 0xFF filler out without one
        public bool HasPointsRow;
        public int AlterEgoPoints, Sparks, Deeds, Plaudits;

        // char_vars: name -> (value, expiry unix seconds; 0 never expires)
        public Dictionary<string, (int Value, uint Expiry)> CharVars = new Dictionary<string, (int, uint)>();

        // quest rework piece A: the char's char_quest_state / char_mission_state rows (ProgressRows), kept next to
        // QuestLog / MissionLog and the Quest[..] / Mission[..] CharVars; null when the rows are off for the char
        public XI.Quests.CharProgress Progress;

        // the event the client is playing, the one a script is about to start (eventPreparation) and the ones queued
        // behind it
        public EventInfo CurrentEvent = new EventInfo();
        public EventInfo EventPreparation = new EventInfo();
        public readonly List<EventInfo> EventQueue = new List<EventInfo>();
        public bool InSequence, GotMessage, Locked;
        // jailutils::Add: the player controller gone (PAI->SetController(nullptr)), so CAIContainer's Engage, Cast,
        // ChangeTarget, Disengage, WeaponSkill, Ability, RangedAttack and UseItem do nothing; the char's next load
        // has it back
        public bool NoController;
        public bool IsInEvent => CurrentEvent.EventId != -1;
        public bool IsNpcLocked => IsInEvent || InSequence;

        // m_LastPacketType: the last C2S packet that passed validation
        public ushort LastPacketType;

        // m_LastSynthTime and the synth in progress (CCraftState with its SynthTransaction)
        public long LastSynthTimeMs = long.MinValue / 2;
        internal ZoneHost.SynthSession Synth;
        internal ZoneHost.ItemUseState ItemUse; // CItemState: an item being used
        internal ZoneHost.WeaponSkillState WsState; // CWeaponSkillState: a weapon skill being used
        public WeaponSkill LastWeaponSkill;         // CPlayerController::m_lastWeaponSkill: the last one started
        public bool InWeaponSkill => WsState != null;
        internal ZoneHost.RangedState RangedState;              // CRangeState: a ranged attack being aimed
        // CPlayerController::m_lastSpellFinishedTime (canAct) and m_lastMoveDistance (0x015's ground distance, which
        // CMagicState reads for a char moving as it starts casting)
        public long LastSpellFinishedMs = long.MinValue / 2;
        public float LastMoveDistance;
        // m_charHistory: the lifetime counters, indexed by HISTORY_TYPE (ZoneHost.CharHistory.cs)
        public readonly uint[] History = new uint[ZoneHost.HistoryCount];
        // m_chocoboUserData (ChocoboUserData_t laid flat, MountUtils) and m_mountId, the mount last called (the packets'
        // MountIndex)
        public readonly byte[] ChocoboUserData = new byte[MountUtils.UserDataSize];
        public byte MountId;
        public bool InRangedAttack => RangedState != null;
        public long LastRangedAttackMs = long.MinValue / 2;     // CBattleEntity::m_LastRangedAttackTime
        internal ZoneHost.AbilityState AbilityState; // CAbilityState: a job ability being used
        public bool InAbility => AbilityState != null;
        public bool UsingItem => ItemUse != null;
        public bool Synthesizing => Synth != null;

        // hookedFish, fishingToken, lastCastTime / nextFishTime (Vana'diel timestamps) and hookDelay (fishingutils)
        public ZoneHost.FishResponse HookedFish;
        public uint FishingToken, LastCastTime, NextFishTime;
        public byte HookDelay = 13;

        // guildShopNpc_: the NPC whose guild shop the char opened last (0 for none)
        public uint GuildShopNpc;

        // m_isStyleLocked (chars.isstylelocked); mainlook, the look a style lock shows (char_look holds it while
        // locked); styleItems, the item per equipment slot whose model it shows (char_style keeps head..ranged)
        public bool StyleLocked;
        public Look MainLook = new Look();
        public readonly ushort[] StyleItems = new ushort[16];

        // the look the appearance packets send
        public Look ShownLook => StyleLocked ? MainLook : Look;

        // charutils::HasItem: anywhere in the char's containers, the recycle bin included unless told otherwise
        public bool HasItem(ushort itemId, bool includeRecycleBin = true) =>
            itemId != 0 && Items.Exists(i => i.ItemId == itemId && (includeRecycleBin || i.Location != Container.RecycleBin));

        // search.messagetype / search.message: the /seacom comment; never loaded, so every login starts with none
        public byte SearchMessageType;
        public byte[] SearchMessage = Array.Empty<byte>();

        // servmesLastOffset_: the last /servmes fragment offset answered
        public int? ServmesLastOffset;

        // keyitems_t: 8 tables of {bitset<512> keyList, seenList}
        public bool HasKeyItem(int keyItem)
        {
            int table = keyItem / 512, index = keyItem % 512;
            if (keyItem < 0 || table >= 8) return false;
            return (KeyItems[table * 128 + index / 8] & 1 << index % 8) != 0;
        }

        // CCharEntity constructor: nation and Zilart logs start at 0xFFFF (none), ToAU/WoTG at 0, CoP at 101
        private static byte[] NewMissionLog()
        {
            var log = new byte[MaxMissionArea * MissionLogSize];
            for (int i = 0; i <= 3; i++) SetU16(log, i * MissionLogSize, 0xFFFF);
            SetU16(log, 6 * MissionLogSize, 101);
            return log;
        }

        private static void SetU16(byte[] b, int off, int v) { b[off] = (byte)v; b[off + 1] = (byte)(v >> 8); }
        private static ushort U16(byte[] b, int off) => (ushort)(b[off] | b[off + 1] << 8);

        public ushort MissionCurrent(int log) => U16(MissionLog, log * MissionLogSize);
        public ushort MissionStatusUpper(int log) => U16(MissionLog, log * MissionLogSize + 2);
        public ushort MissionStatusLower(int log) => U16(MissionLog, log * MissionLogSize + 4);
        public bool MissionComplete(int log, int id) => MissionLog[log * MissionLogSize + 6 + id] != 0;
        public void SetMissionCurrent(int log, int v) => SetU16(MissionLog, log * MissionLogSize, v);
        public void SetMissionStatus(int log, uint v)
        {
            SetU16(MissionLog, log * MissionLogSize + 2, (int)(v >> 16));
            SetU16(MissionLog, log * MissionLogSize + 4, (int)v);
        }
        public void SetMissionComplete(int log, int id, bool v) => MissionLog[log * MissionLogSize + 6 + id] = (byte)(v ? 1 : 0);
        public ushort AssaultCurrent => U16(AssaultLog, 0);
        public bool AssaultComplete(int id) => AssaultLog[2 + id] != 0;
        public ushort CampaignCurrent => U16(CampaignLog, 0);
        public bool CampaignComplete(int id) => CampaignLog[2 + id] != 0;

        // CCharEntity::getCharVar: an expired variable reads as 0
        public int GetCharVar(string name, long unixNow = 0)
        {
            if (!CharVars.TryGetValue(name, out var v)) return 0;
            if (v.Expiry > 0 && unixNow > 0 && v.Expiry <= unixNow) return 0;
            return v.Value;
        }
    }
}
