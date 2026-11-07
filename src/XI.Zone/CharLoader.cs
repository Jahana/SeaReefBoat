using System;
using System.Collections.Generic;
using XI.Persistence;

namespace XI.Zone
{
    // ability::LoadAbilitiesList: per job, the abilities in level order (what BuildingCharAbilityTable reads), every
    // ability by id (ability::GetAbility) and the charges (ability::GetCharge).
    public sealed class AbilityTable
    {
        public const int MaxAbilityId = 971;      // MAX_ABILITY_ID
        public const int AbilityPetCommands = 55;  // ABILITY_PET_COMMANDS
        public const int AbilityProvoke = 35;      // ABILITY_PROVOKE
        public const int AbilityHealingRuby = 512; // ABILITY_HEALING_RUBY

        public const ushort AddTypeMerit = 1, AddTypeAstralFlow = 2, AddTypeMainOnly = 4, AddTypeLearned = 8,
            AddTypeLightArts = 16, AddTypeDarkArts = 32, AddTypeJugPet = 64, AddTypeCharmPet = 128, AddTypeAvatar = 256,
            AddTypeAutomaton = 512;

        public readonly struct Ability
        {
            public readonly ushort Id;
            public readonly byte Level;
            public readonly ushort AddType;
            public readonly ushort MeritModId; // the merit an ADDTYPE_MERIT ability needs an upgrade in
            public Ability(ushort id, byte level, ushort addType, ushort meritModId = 0) { Id = id; Level = level; AddType = addType; MeritModId = meritModId; }
        }

        private readonly List<Ability>[] byJob_ = new List<Ability>[CharData.MaxJobs];

        public AbilityTable()
        {
            for (int j = 0; j < byJob_.Length; j++) byJob_[j] = new List<Ability>();
        }

        public IReadOnlyList<Ability> ForJob(int job) => byJob_[job];

        public void Add(int job, Ability a) => byJob_[job].Add(a);

        // PAbilityList: every loaded ability (CAbility) by id; PChargesList in job, level order
        private readonly Dictionary<ushort, AbilityInfo> byId_ = new Dictionary<ushort, AbilityInfo>();
        private readonly List<AbilityCharge> charges_ = new List<AbilityCharge>();

        public void Add(AbilityInfo info) => byId_[info.Id] = info;
        public void AddCharge(AbilityCharge charge) => charges_.Add(charge);

        // ability::GetAbility
        public AbilityInfo Get(ushort id) => byId_.TryGetValue(id, out var a) ? a : null;

        // ability::GetCharge: the last charge row for the recast id the user's main (or sub) job and level reach
        public AbilityCharge GetCharge(BattleEntity user, ushort chargeId)
        {
            AbilityCharge charge = null;
            foreach (var c in charges_)
            {
                if (c.RecastId != chargeId) continue;
                if (user.MJob == c.Job)
                {
                    if (user.MLvl >= c.Level) charge = c;
                    else break;
                }
                else if (user.SJob == c.Job)
                {
                    if (user.SLvl >= c.Level) charge = c;
                    else break;
                }
            }
            return charge;
        }

        // ability::LoadAbilitiesList: the abilities (recast times scaled by map.ABILITY_RECAST_MULTIPLIER) and the
        // abilities_charges rows. The scripts load as they are first used.
        public static AbilityTable Load(IDatabase db, Func<string, bool> isContentEnabled, double recastMultiplier = 1.0)
        {
            var table = new AbilityTable();
            var rset = db.Execute("SELECT abilityId, IFNULL(name, '') AS name, job, level, validTarget, recastTime, message1, animation, animationTime, castTime, " +
                                  "actionType, `range`, isAOE, radius, recastId, CE, VE, meritModID, addType, content_tag " +
                                  "FROM abilities WHERE job < ? AND abilityId < ? ORDER BY job, level ASC",
                new List<BoundValue> { BoundValue.Of(CharData.MaxJobs), BoundValue.Of(MaxAbilityId) });
            while (rset != null && rset.Next())
            {
                if (!isContentEnabled(rset.Get<string>("content_tag"))) continue;
                var info = new AbilityInfo
                {
                    Id = rset.Get<ushort>("abilityId"),
                    Name = rset.Get<string>("name"),
                    Job = rset.Get<byte>("job"),
                    Level = rset.Get<byte>("level"),
                    ValidTarget = rset.Get<ushort>("validTarget"),
                    RecastMultiplier = recastMultiplier,
                    Message = rset.Get<ushort>("message1"),
                    Animation = rset.Get<ushort>("animation"),
                    AnimationTimeMs = rset.Get<ushort>("animationTime"),
                    CastTimeMs = rset.Get<ushort>("castTime"),
                    ActionType = rset.Get<byte>("actionType"),
                    Range = rset.Get<float>("range"),
                    Aoe = rset.Get<byte>("isAOE"),
                    Radius = rset.Get<byte>("radius"),
                    RecastId = rset.Get<ushort>("recastId"),
                    CE = rset.Get<short>("CE"),
                    VE = rset.Get<short>("VE"),
                    MeritModId = (ushort)rset.Get<short>("meritModID"),
                    AddType = (ushort)rset.Get<short>("addType"),
                };
                info.SetRecastTime(rset.Get<ushort>("recastTime") * 1000L);
                table.Add(info);
                table.Add(info.Job, new Ability(info.Id, info.Level, info.AddType, info.MeritModId));
            }
            var rset2 = db.Execute("SELECT recastId, job, level, maxCharges, chargeTime, meritModId FROM abilities_charges ORDER BY job, level ASC",
                new List<BoundValue>());
            while (rset2 != null && rset2.Next())
            {
                table.AddCharge(new AbilityCharge
                {
                    RecastId = rset2.Get<ushort>("recastId"),
                    Job = rset2.Get<byte>("job"),
                    Level = rset2.Get<byte>("level"),
                    MaxCharges = rset2.Get<byte>("maxCharges"),
                    ChargeTimeMs = rset2.Get<uint>("chargeTime") * 1000L,
                    Merit = rset2.Get<ushort>("meritModId"),
                });
            }
            return table;
        }
    }

    // charutils::LoadChar, restricted to what a zone-in needs: the merits (CMeritPoints, in LoadExp), the pet to
    // respawn (LoadFromCharPetSQL's petZoningInfo), the learned weapon skills, the linkshell items, and the residential
    // area's HP / MP refill (zoneutils::IsResidentialArea: a char in a mog house that is not dead comes in full). The
    // traits come in through the zone host's BuildTraits hook, the recasts through AfterLevels. Equipment and its
    // mods, skills and UpdateHealth come from CharStats when given. Key items, quest/mission/eminence logs and the
    // teleport unlocks are kept as the raw blobs the game-OK packets copy.
    public sealed class CharLoader
    {
        // the expansions LoadFromCharSpellsSQL offers FIND_IN_SET
        private static readonly string[] Expansions = { "ROTZ", "COP", "TOAU", "WOTG", "ACP", "AMK", "ASA", "ABYSSEA", "SOA", "ROV", "TVR", "VOIDWATCH" };

        // char_profile's fame column per xi::FameArea; Jeuno (3) and Selbina/Rabao (4) are derived, not stored
        // char_unlocks' teleport_t mask columns by TELEPORT_TYPE (5, the abyssea conflux, is a blob)
        public static readonly string[] TeleportMaskColumns =
        {
            "outpost_sandy", "outpost_bastok", "outpost_windy", "runic_portal", "maw", null, "campaign_sandy", "campaign_bastok", "campaign_windy",
        };

        public static readonly string[] FameColumns =
        {
            "fame_sandoria", "fame_bastok", "fame_windurst", null, null, "fame_norg", "fame_aby_konschtat", "fame_aby_tahrongi",
            "fame_aby_latheine", "fame_aby_misareaux", "fame_aby_vunkerl", "fame_aby_attohwa", "fame_aby_altepa",
            "fame_aby_grauberg", "fame_aby_uleguerand", "fame_adoulin",
        };
        public static readonly string[] StoredFameColumns = Array.FindAll(FameColumns, col => col != null);

        private readonly IDatabase db_;
        private readonly Grades grades_;
        public Grades Grades => grades_;
        private readonly AbilityTable abilities_;
        private readonly Func<string, bool> isContentEnabled_;
        private readonly CharStats stats_;

        // null when the loader skips equipment mods and skills (then the status menu cannot be sent)
        public CharStats Stats => stats_;

        // ability::GetAbility / GetCharge for the zone host
        public AbilityTable Abilities => abilities_;

        // meritNameSpace's merit list (ZoneHost sets it); null loads no merits, so none count
        public MeritTable MeritTable { get; set; }

        // effects::EffectsParams (ZoneHost sets it); the zone hosts share it
        public EffectTable EffectTable { get; set; }

        // the char's quest / mission rows (quest rework piece A)
        public ProgressRows Progress { get; }

        // isContentEnabled: luautils::IsContentEnabled (empty tag => true); null enables everything, which is what the
        // default settings (RESTRICT_CONTENT = 0) amount to.
        public CharLoader(IDatabase db, Grades grades, AbilityTable abilities, CharStats stats = null, Func<string, bool> isContentEnabled = null)
        {
            db_ = db;
            Progress = new ProgressRows(db);
            grades_ = grades;
            abilities_ = abilities;
            stats_ = stats;
            isContentEnabled_ = isContentEnabled ?? (_ => true);
        }

        private ResultSet Query(string sql, uint charId) => db_.Execute(sql, new List<BoundValue> { BoundValue.Of(charId) });

        // the char's reads (LoginReads lists them): the charid the one bound value, but SqlSpells' (and SpellExpansions)
        internal const string SqlChars =
            "SELECT charname, accid, nation, pos_zone, home_zone, home_rot, home_x, home_y, home_z, pos_prevzone, pos_rot, pos_x, pos_y, pos_z, boundary, abilities, weaponskills, playtime, gmlevel, job_master, settings, chatfilters_1, chatfilters_2, " +
            "zones, languages, keyitems, quests, missions, assault, campaign, eminence, titles, campaign_allegiance, isstylelocked, moghouse, mentor, moghancement, " +
            "UNIX_TIMESTAMP(lastupdate) AS lastonline FROM chars WHERE charid = ?";
        internal const string SqlFlags = "SELECT gmModeEnabled, gmHiddenEnabled FROM char_flags WHERE charid = ?";
        internal static readonly string SqlProfile = "SELECT rank_points, rank_sandoria, rank_bastok, rank_windurst, unity_leader, " + string.Join(", ", StoredFameColumns) +
            " FROM char_profile WHERE charid = ?";
        internal const string SqlUnlocks =
            "SELECT outpost_sandy, outpost_bastok, outpost_windy, runic_portal, maw, campaign_sandy, campaign_bastok, campaign_windy, " +
            "homepoints, survivals, abyssea_conflux, waypoints, eschan_portals, claimed_deeds, unique_event, maze_vouchers, maze_runes FROM char_unlocks " +
            "WHERE charid = ?";
        internal const string SqlChocobo = "SELECT chocobo_user_data FROM char_pet WHERE charid = ?";
        internal const string SqlPoints = "SELECT alter_ego_points, spark_of_eminence, deeds, plaudits FROM char_points WHERE charid = ?";
        internal const string SqlVars = "SELECT varname, value, expiry FROM char_vars WHERE charid = ?";
        internal const string SqlLook = "SELECT face, race, size, head, body, hands, legs, feet, main, sub, ranged FROM char_look WHERE charid = ?";
        internal const string SqlStyle = "SELECT head, body, hands, legs, feet, main, sub, ranged FROM char_style WHERE charid = ?";
        internal const string SqlStats = "SELECT mjob, sjob, hp, mp, mhflag, title, bazaar_message, pet_id, pet_type, pet_hp, pet_mp, pet_level FROM char_stats WHERE charid = ?";
        internal const string SqlJobs = "SELECT * FROM char_jobs WHERE charid = ?";
        internal const string SqlMerits = "SELECT meritid, upgrades FROM char_merit WHERE charid = ?";
        internal const string SqlSkills = "SELECT skillid, value, rank FROM char_skills WHERE charid = ?";
        internal const string SqlJobPoints = "SELECT jobid, capacity_points, job_points, job_points_spent, jptype0, jptype1, jptype2, jptype3, jptype4, jptype5, jptype6, jptype7, jptype8, jptype9 FROM char_job_points WHERE charid = ? ORDER BY jobid ASC";
        internal const string SqlStorage = "SELECT inventory, safe, locker, satchel, sack, `case`, wardrobe, wardrobe2, wardrobe3, wardrobe4, wardrobe5, wardrobe6, wardrobe7, wardrobe8 FROM char_storage WHERE charid = ?";
        internal const string SqlInventory = "SELECT i.location, i.slot, i.itemId, i.quantity, i.bazaar, i.signature, i.extra, b.flags FROM char_inventory i " +
            "LEFT JOIN item_basic b ON b.itemid = i.itemId WHERE i.charid = ? ORDER BY i.location, i.slot";
        internal const string SqlEquip = "SELECT slotid, equipslotid, containerid FROM char_equip WHERE charid = ?";
        internal const string SqlSpells = "SELECT char_spells.spellid FROM char_spells JOIN spell_list ON spell_list.spellid = char_spells.spellid WHERE charid = ? AND (spell_list.content_tag IS NULL OR FIND_IN_SET(spell_list.content_tag, ?))";

        // LoadFromCharSpellsSQL's FIND_IN_SET list: the expansions that are enabled
        public string SpellExpansions => string.Join(",", Array.FindAll(Expansions, x => isContentEnabled_(x)));

        // db::extractFromBlob: a NULL blob keeps the constructor's defaults; otherwise the destination is zeroed and
        // the blob copied over it, truncated to whichever is shorter
        private static void Blob(ResultSet r, string col, byte[] dest)
        {
            if (r.IsNull(col)) return;
            var bytes = r.GetBlobBytes(col);
            Array.Clear(dest);
            Array.Copy(bytes, dest, Math.Min(bytes.Length, dest.Length));
        }

        public CharData Load(uint charId)
        {
            var c = new CharData { Id = charId };

            var r = Query(SqlChars, charId);
            if (r == null || !r.Next()) return null;
            c.Name = r.Get<string>("charname");
            c.AccId = r.Get<uint>("accid");
            c.Nation = r.Get<byte>("nation");
            c.Zone = r.Get<ushort>("pos_zone");
            c.PrevZone = r.Get<ushort>("pos_prevzone");
            c.Destination = c.Zone;
            c.Rotation = r.Get<byte>("pos_rot");
            c.X = r.Get<float>("pos_x");
            c.Y = r.Get<float>("pos_y");
            c.Z = r.Get<float>("pos_z");
            c.Boundary = r.Get<ushort>("boundary");
            c.HomeZone = r.Get<ushort>("home_zone");
            (c.HomeX, c.HomeY, c.HomeZ, c.HomeRot) = (r.Get<float>("home_x"), r.Get<float>("home_y"), r.Get<float>("home_z"), r.Get<byte>("home_rot"));
            c.LearnedAbilities = r.IsNull("abilities") ? Array.Empty<byte>() : r.GetBlobBytes("abilities");
            Blob(r, "weaponskills", c.LearnedWeaponskills);
            c.PlayTimeSec = r.Get<uint>("playtime");
            c.JobMasterDisplay = r.Get<uint>("job_master") > 0;
            c.MentorUnlocked = r.Get<uint>("mentor") > 0;
            c.Settings = r.Get<uint>("settings");
            c.ChatFilters1 = r.Get<uint>("chatfilters_1");
            c.ChatFilters2 = r.Get<uint>("chatfilters_2");
            // m_GMlevel is the account's; what packets show is visibleGmLevel, 0 until a GM turns it on
            c.AccountGmLevel = r.Get<byte>("gmlevel");
            c.GmLevel = 0;
            c.Languages = r.Get<byte>("languages");
            Blob(r, "zones", c.ZonesVisited);
            Blob(r, "keyitems", c.KeyItems);
            Blob(r, "quests", c.QuestLog);
            Blob(r, "missions", c.MissionLog);
            Blob(r, "assault", c.AssaultLog);
            Blob(r, "campaign", c.CampaignLog);
            Blob(r, "eminence", c.EminenceLog);
            c.LastOnline = r.IsNull("lastonline") ? 0 : r.Get<long>("lastonline");
            Blob(r, "titles", c.Titles);
            c.CampaignAllegiance = r.Get<byte>("campaign_allegiance");
            c.StyleLocked = r.Get<uint>("isstylelocked") == 1;
            c.MoghouseId = r.Get<uint>("moghouse");
            Moghancement.Set(c, r.Get<ushort>("moghancement"));

            // charutils::LoadCharGMFlags (char_flags): gm mode shows the GM level, hidden stops the char's updates
            r = Query(SqlFlags, charId);
            if (r != null && r.Next())
            {
                if (r.Get<uint>("gmModeEnabled") != 0) c.GmLevel = (byte)Math.Min(c.AccountGmLevel + 3, 7);
                c.GmHidden = r.Get<uint>("gmHiddenEnabled") != 0;
            }

            r = Query(SqlProfile, charId);
            if (r != null && r.Next())
            {
                c.RankPoints = r.Get<ushort>("rank_points");
                c.Rank[0] = r.Get<byte>("rank_sandoria");
                c.Rank[1] = r.Get<byte>("rank_bastok");
                c.Rank[2] = r.Get<byte>("rank_windurst");
                c.UnityLeader = r.Get<byte>("unity_leader");
                for (int area = 0; area < FameColumns.Length; area++)
                    if (FameColumns[area] != null) c.Fame[area] = r.Get<ushort>(FameColumns[area]);
            }

            r = Query(SqlUnlocks, charId);
            if (r != null && r.Next())
            {
                for (int type = 0; type < TeleportMaskColumns.Length; type++)
                    if (TeleportMaskColumns[type] != null) c.TeleportMasks[type] = r.Get<uint>(TeleportMaskColumns[type]);
                Blob(r, "abyssea_conflux", c.AbysseaConflux);
                Blob(r, "unique_event", c.UniqueEvents);
                Blob(r, "claimed_deeds", c.ClaimedDeeds);
                Blob(r, "homepoints", c.HomepointTele);
                Blob(r, "survivals", c.SurvivalTele);
                var waypoints = new byte[12];
                var eschan = new byte[4];
                Blob(r, "waypoints", waypoints);
                Blob(r, "eschan_portals", eschan);
                waypoints.CopyTo(c.Waypoints, 0);
                eschan.CopyTo(c.Waypoints, 12);
                Blob(r, "maze_vouchers", c.MazeVouchers);
                Blob(r, "maze_runes", c.MazeRunes);
            }

            // charutils::LoadFromCharPetSQL
            r = Query(SqlChocobo, charId);
            if (r != null && r.Next()) Blob(r, "chocobo_user_data", c.ChocoboUserData);

            r = Query(SqlPoints, charId);
            if (r != null && r.Next())
            {
                c.HasPointsRow = true;
                c.AlterEgoPoints = r.Get<int>("alter_ego_points");
                c.Sparks = r.Get<int>("spark_of_eminence");
                c.Deeds = r.Get<int>("deeds");
                c.Plaudits = r.Get<int>("plaudits");
            }

            r = Query(SqlVars, charId);
            while (r != null && r.Next())
                c.CharVars[r.Get<string>("varname")] = (r.Get<int>("value"), r.Get<uint>("expiry"));

            // the quest / mission rows: imported or checked against the blobs and vars just read, then QuestLog /
            // MissionLog from them
            Progress.Load(c);

            r = Query(SqlLook, charId);
            if (r != null && r.Next())
            {
                var l = c.Look;
                l.Face = r.Get<byte>("face"); l.Race = r.Get<byte>("race"); l.Size = r.Get<byte>("size");
                l.Head = r.Get<ushort>("head"); l.Body = r.Get<ushort>("body"); l.Hands = r.Get<ushort>("hands");
                l.Legs = r.Get<ushort>("legs"); l.Feet = r.Get<ushort>("feet"); l.Main = r.Get<ushort>("main");
                l.Sub = r.Get<ushort>("sub"); l.Ranged = r.Get<ushort>("ranged");
                c.MainLook = l.Clone();
            }

            // LoadFromCharStyleSQL
            r = Query(SqlStyle, charId);
            if (r != null && r.Next())
            {
                string[] columns = { "main", "sub", "ranged", null, "head", "body", "hands", "legs", "feet" };
                for (int i = 0; i < columns.Length; i++)
                    if (columns[i] != null) c.StyleItems[i] = r.Get<ushort>(columns[i]);
            }

            int hp = 0, mp = 0;
            r = Query(SqlStats, charId);
            if (r != null && r.Next())
            {
                c.MJob = r.Get<byte>("mjob");
                c.SJob = r.Get<byte>("sjob");
                hp = r.Get<int>("hp");
                mp = r.Get<int>("mp");
                c.MhFlag = r.Get<ushort>("mhflag");
                c.Title = r.Get<ushort>("title");
                // read into a 512-byte buffer and taken as a C string
                var msg = new byte[512];
                Blob(r, "bazaar_message", msg);
                int len = Array.IndexOf(msg, (byte)0);
                c.BazaarMessage = msg.AsSpan(0, len < 0 ? msg.Length : len).ToArray();

                // Determine if the pet should be respawned (the jug pet's char vars are read by the zone host).
                int petHp = r.Get<short>("pet_hp");
                if (petHp != 0)
                {
                    c.PetZoning.PetHp = petHp;
                    c.PetZoning.PetId = r.Get<byte>("pet_id");
                    c.PetZoning.PetMp = r.Get<short>("pet_mp");
                    c.PetZoning.PetType = (PetType)r.Get<byte>("pet_type");
                    c.PetZoning.PetLevel = r.Get<byte>("pet_level");
                    c.PetZoning.RespawnPet = true;
                }
            }

            r = Query(SqlJobs, charId);
            if (r != null && r.Next())
            {
                c.UnlockedJobs = r.Get<uint>("unlocked");
                c.Genkai = r.Get<byte>("genkai");
                for (int j = 0; j < JobColumns.Length; j++) c.JobLevels[j + 1] = r.Get<byte>(JobColumns[j]);
            }
            LoadExp(c);
            c.MLvl = c.JobLevels[c.MJob];
            c.SLvl = SubLevel(c.MLvl, c.JobLevels[c.SJob], stats_?.Settings.SubjobRatio ?? 1);
            // LoadFromCharRecastSQL, which the zone host owns
            AfterLevels?.Invoke(c);
            // monstrosity::TryPopulateMonstrosityData: a MON char's data and species state, before the stats
            if (c.MJob == XI.Zone.Monstrosity.JobMon && MonTable?.Invoke() is { } monTable)
                XI.Zone.Monstrosity.TryPopulate(db_, monTable, stats_?.Settings.EnableMonstrosity ?? true, c);

            LoadSkills(c);
            LoadJobPoints(c);
            LoadStorage(c);
            LoadInventory(c);
            LoadSpells(c);

            CalculateStats(c);
            RefreshGiftMods(c);
            // blueutils::LoadSetSpells and puppetutils::LoadAutomaton, which the zone host owns: after the gift mods
            AfterGiftMods?.Invoke(c);
            BuildAbilityTable(c);
            // BuildingCharTraitsTable, which the zone host owns
            BuildTraits?.Invoke(c);

            c.Animation = (byte)(hp == 0 ? 3 : 0); // Animation::Death / None
            // CStatusEffectContainer::LoadStatusEffects, which the zone host owns: before the equipment
            BeforeEquip?.Invoke(c);
            LoadEquip(c);

            // a mog house refills a living char (zoneutils::IsResidentialArea: inMogHouse)
            bool canRestore = c.MoghouseId != 0 && hp > 0;

            // every EquipItem runs UpdateHealth, so what was saved goes in after the equipment; the last UpdateHealth
            // clamps it to the (possibly lower) maximum
            if (stats_ != null)
            {
                stats_.BuildSkillsTable(c);
                stats_.EquipAtLogin(c);
                c.Hp = canRestore ? c.MaxHp : hp;
                c.Mp = canRestore ? c.MaxMp : mp;
                CharStats.UpdateHealth(c);
            }
            else
            {
                c.Hp = canRestore ? c.MaxHp : Math.Min(hp, c.MaxHp);
                c.Mp = canRestore ? c.MaxMp : Math.Min(mp, c.MaxMp);
            }
            return c;
        }

        // CBattleEntity::SetSLevel for a char: the sub job level the ratio allows under the main level
        internal static byte SubLevel(byte mlvl, byte slvl, int ratio) => ratio switch
        {
            0 => 0,
            1 => slvl > (mlvl >> 1) ? (byte)(mlvl == 1 ? 1 : mlvl >> 1) : slvl,
            2 => slvl > mlvl * 2 / 3 ? (byte)(mlvl == 1 ? 1 : mlvl * 2 / 3) : slvl,
            3 => slvl > mlvl ? mlvl : slvl,
            _ => slvl,
        };

        internal static readonly string[] JobColumns =
            { "war", "mnk", "whm", "blm", "rdm", "thf", "pld", "drk", "bst", "brd", "rng", "sam", "nin", "drg", "smn", "blu", "cor", "pup", "dnc", "sch", "geo", "run", "mon" };

        // after JobColumns, which it is made from
        internal static readonly string SqlExp = "SELECT mode, " + string.Join(", ", JobColumns) + ", merits, limits FROM char_exp WHERE charid = ?";

        private const int MaxLimitPoints = 10000;

        // LoadFromCharExpSQL, then CMeritPoints (its upgrades from char_merit) and its setters: merits are capped at
        // MAX_MERIT_POINTS plus the MaxMerit upgrades, limit points below MAX_LIMIT_POINTS
        private void LoadExp(CharData c)
        {
            LoadMerits(c);
            var r = Query(SqlExp, c.Id);
            if (r == null || !r.Next()) return;
            c.MeritMode = r.Get<byte>("mode") != 0;
            for (int j = 0; j < JobColumns.Length; j++) c.JobExp[j + 1] = r.Get<ushort>(JobColumns[j]);
            c.MeritPoints = (ushort)Math.Min((int)r.Get<byte>("merits"), MaxMeritPoints(c));
            c.LimitPoints = (ushort)Math.Min((int)r.Get<ushort>("limits"), MaxLimitPoints - 1);
        }

        // MAX_MERIT_POINTS + GetMeritValue(MaxMerit): the most merit points the char can bank
        public int MaxMeritPoints(CharData c) => (stats_?.Settings.MaxMeritPoints ?? 30) + (c.Merits?.Value(c, MeritTable.MaxMerit) ?? 0);

        // CMeritPoints::LoadMeritPoints: upgrades past a merit's max are cut to it, unknown merits skipped
        private void LoadMerits(CharData c)
        {
            if (MeritTable == null) return;
            c.Merits = new MeritPoints(MeritTable);
            var r = Query(SqlMerits, c.Id);
            while (r != null && r.Next())
            {
                int index = MeritTable.IndexOf((ushort)r.Get<uint>("meritid"));
                if (index < 0) continue;
                c.Merits.Counts[index] = (byte)Math.Min(r.Get<uint>("upgrades"), MeritTable.Entries[index].Upgrade);
            }
        }

        // LoadFromCharSkillsSQL: crafting ranks are kept from Fishing up
        private void LoadSkills(CharData c)
        {
            var r = Query(SqlSkills, c.Id);
            while (r != null && r.Next())
            {
                int id = r.Get<byte>("skillid");
                if (id >= Skill.Max) continue;
                c.RealSkills[id] = r.Get<ushort>("value");
                if (id >= Skill.Fishing) c.RealSkillRank[id] = r.Get<byte>("rank");
            }
        }

        // CJobPoints::LoadJobPoints: the rows found overwrite their jobs, the others keep what they held
        public void LoadJobPoints(CharData c)
        {
            var r = Query(SqlJobPoints, c.Id);
            while (r != null && r.Next())
            {
                int job = (int)r.Get<uint>("jobid");
                if (job < 0 || job >= CharData.MaxJobs) continue;
                c.JobPoints[job] = (r.Get<ushort>("capacity_points"), r.Get<ushort>("job_points"), r.Get<ushort>("job_points_spent"));
                c.JobPointRows[job] = true;
                for (int j = 0; j < 10; j++) c.JobPointLevels[job, j] = r.Get<byte>("jptype" + j);
            }
        }

        // jobpointutils::LoadGifts, read on first use
        private List<(ushort JpNeeded, int Mod, short Value)>[] gifts_;

        private List<(ushort JpNeeded, int Mod, short Value)>[] Gifts()
        {
            if (gifts_ != null) return gifts_;
            gifts_ = new List<(ushort, int, short)>[CharData.MaxJobs];
            for (int j = 0; j < gifts_.Length; j++) gifts_[j] = new List<(ushort, int, short)>();
            var r = db_?.Execute("SELECT jobid, jp_needed, modid, value FROM job_point_gifts ORDER BY jp_needed ASC", new List<BoundValue>());
            while (r != null && r.Next())
            {
                int job = r.Get<byte>("jobid");
                if (job < gifts_.Length) gifts_[job].Add((r.Get<ushort>("jp_needed"), r.Get<ushort>("modid"), r.Get<short>("value")));
            }
            return gifts_;
        }

        // the spells RefreshGiftMods teaches: a job's spells once it has spent enough, guarded by the first spell
        // (or, guard 0, each spell on its own and the update always sent)
        private static readonly (byte Job, ushort Spent, ushort Guard, ushort[] Spells)[] JobPointSpells =
        {
            (4, 100, 849, new ushort[] { 849, 850, 851, 852, 853, 854 }),           // BLM: Fire VI .. Water VI
            (4, 550, 881, new ushort[] { 881 }),                                    // Aspir III
            (4, 1200, 367, new ushort[] { 367 }),                                   // Death
            (10, 100, 871, new ushort[] { 871, 872, 873, 874, 875, 876, 877, 878 }), // BRD: the Threnody IIs
            (8, 100, 856, new ushort[] { 856 }),                                    // DRK: Endark II
            (8, 550, 880, new ushort[] { 880 }),                                    // Drain III
            (21, 100, 0, new ushort[] { 148, 153, 158, 163, 168, 173 }),            // GEO: Fire V .. Water V
            (21, 550, 881, new ushort[] { 881 }),                                   // Aspir III
            (21, 1200, 865, new ushort[] { 865, 866, 867, 868, 869, 870 }),         // Fira III .. Watera III
            (13, 100, 340, new ushort[] { 340 }),                                   // NIN: Utsusemi: San
            (7, 100, 855, new ushort[] { 855 }),                                    // PLD: Enlight II
            (5, 100, 0, new ushort[] { 148, 153, 158, 163, 168, 173 }),             // RDM: Fire V .. Water V
            (5, 550, 884, new ushort[] { 884, 882, 883 }),                          // Addle II, Distract III, Frazzle III
            (5, 1200, 894, new ushort[] { 894, 895 }),                              // Refresh III, Temper II
            (22, 550, 493, new ushort[] { 493 }),                                   // RUN: Temper
            (3, 100, 848, new ushort[] { 848 }),                                    // WHM: Reraise IV
            (3, 1200, 893, new ushort[] { 893 }),                                   // Full Cure
        };

        // jobpointutils::RefreshGiftMods: the main job's gifts up to the job points it has spent (only at 99) replace
        // the last set in ModStat, and its job point spells are learned (and saved). True when MAGIC_DATA is due.
        // addSpell's spell::GetSpell check is not made: these spells are all in spell_list.
        public bool RefreshGiftMods(CharData c)
        {
            foreach (var (mod, value) in c.JobPointGifts) c.ModStat[mod] -= value;
            c.JobPointGifts.Clear();
            int job = c.MJob;
            if (job >= CharData.MaxJobs) return false;
            ushort spent = c.JobPoints[job].Spent;
            foreach (var gift in Gifts()[job])
            {
                if (gift.JpNeeded > spent || c.MLvl < 99) break;
                if (gift.Mod < c.ModStat.Length) c.JobPointGifts.Add((gift.Mod, gift.Value));
            }
            foreach (var (mod, value) in c.JobPointGifts) c.ModStat[mod] += value;

            bool sendUpdate = false;
            foreach (var (spellJob, needed, guard, spells) in JobPointSpells)
            {
                if (spellJob != job || spent < needed) continue;
                if (guard != 0 && HasSpell(c, guard)) continue;
                foreach (ushort spell in spells)
                {
                    if (guard == 0 && HasSpell(c, spell)) continue;
                    c.SpellList[spell / 8] |= (byte)(1 << (spell % 8));
                    db_?.Execute("INSERT IGNORE INTO char_spells VALUES (?, ?) LIMIT 1", new List<BoundValue> { BoundValue.Of(c.Id), BoundValue.Of(spell) });
                }
                sendUpdate = true;
            }
            return sendUpdate;
        }

        private static bool HasSpell(CharData c, int id) => (c.SpellList[id / 8] & 1 << (id % 8)) != 0;

        // LoadFromCharStorageSQL: AddBuff sets each container's size (clamped to 0..80); the mog safes share one
        // purchase, temp items are fixed at 50 and the recycle bin at 10. Storage (LOC 2) grows from the installed
        // furnishings (LoadInventory).
        private void LoadStorage(CharData c)
        {
            var r = Query(SqlStorage, c.Id);
            var s = c.StorageSize;
            s[(int)Container.TempItems] = 50;
            s[(int)Container.RecycleBin] = 10;
            c.ContainerBuff[(int)Container.TempItems] = 50;
            c.ContainerBuff[(int)Container.RecycleBin] = 10;
            if (r == null || !r.Next()) return;
            // CItemContainer::AddBuff from an empty container: m_buff is the column, the size it clamped to 0..80
            byte Buff(string col, Container loc)
            {
                int raw = r.Get<byte>(col);
                c.ContainerBuff[(int)loc] = raw;
                return (byte)Math.Clamp(raw, 0, 80);
            }
            s[(int)Container.Inventory] = Buff("inventory", Container.Inventory);
            s[(int)Container.MogSafe] = Buff("safe", Container.MogSafe);
            s[(int)Container.MogSafe2] = Buff("safe", Container.MogSafe2);
            s[(int)Container.MogLocker] = Buff("locker", Container.MogLocker);
            s[(int)Container.MogSatchel] = Buff("satchel", Container.MogSatchel);
            s[(int)Container.MogSack] = Buff("sack", Container.MogSack);
            s[(int)Container.MogCase] = Buff("case", Container.MogCase);
            s[(int)Container.Wardrobe] = Buff("wardrobe", Container.Wardrobe);
            for (int w = 2; w <= 8; w++)
                s[(int)Container.Wardrobe2 + w - 2] = Buff("wardrobe" + w, (Container)((int)Container.Wardrobe2 + w - 2));
        }

        private void LoadInventory(CharData c)
        {
            var r = Query(SqlInventory, c.Id);
            while (r != null && r.Next())
            {
                var item = new Item { Location = (Container)r.Get<byte>("location"), Slot = r.Get<byte>("slot"), ItemId = r.Get<ushort>("itemId"), Quantity = r.Get<uint>("quantity") };
                // a priced item goes on display (every loaded item is Free, so marking it Bazaar cannot fail)
                item.CharPrice = r.IsNull("bazaar") ? 0 : r.Get<uint>("bazaar");
                item.Furnishing = stats_?.Items.Furnishing(item.ItemId) != null;
                item.InBazaar = item.CharPrice != 0;
                // the blob is copied over the 24-byte m_extra, so a shorter one leaves the rest zero
                if (!r.IsNull("extra"))
                {
                    var extra = r.GetBlobBytes("extra");
                    Array.Copy(extra, item.Extra, Math.Min(extra.Length, item.Extra.Length));
                }
                // the signature column is packed into the exdata (setSignature) of a linkshell, an untyped one taking its
                // type from its id, and of an inscribable item; any other item's getSignature reads its exdata as loaded
                string signature = r.IsNull("signature") ? "" : r.Get<string>("signature");
                if (LinkshellItem.Is(item))
                {
                    if (item.Extra[8] == 0) item.Extra[8] = (byte)(item.ItemId - LinkshellItem.NewLinkshellId);
                    ItemSignature.Set(item, signature);
                }
                else if (!r.IsNull("flags") && (r.Get<uint>("flags") & ItemSignature.ItemFlagInscribable) != 0)
                    ItemSignature.Set(item, signature);
                else
                    item.Signature = ItemSignature.Get(item);
                // an installed furnishing in a safe is placed (marking a Free item cannot fail) and adds its storage
                if (item.Installed && (item.Location == Container.MogSafe || item.Location == Container.MogSafe2))
                {
                    item.PlacedFurniture = true;
                    c.StorageBuff += stats_.Items.Furnishing(item.ItemId).Storage;
                    c.StorageSize[(int)Container.Storage] = (byte)Math.Clamp(c.StorageBuff, 0, 80);
                }
                c.Items.Add(item);
            }
            // the augment pass, once every container is loaded
            if (stats_ != null)
                foreach (var item in c.Items) stats_.Items.ApplyAugments(item);
        }

        // LoadEquip: char_equip rows applied in equipment-slot order; items no longer where the row says are skipped,
        // and slots 16/17 hold only linkshell items
        private void LoadEquip(CharData c)
        {
            var slots = new SortedDictionary<byte, EquipEntry>();
            var r = Query(SqlEquip, c.Id);
            while (r != null && r.Next())
            {
                var e = new EquipEntry { EquipSlot = r.Get<byte>("equipslotid"), InvSlot = r.Get<byte>("slotid"), Container = (Container)r.Get<byte>("containerid") };
                slots[e.EquipSlot] = e;
            }
            foreach (var e in slots.Values)
                if (c.FindItem(e.Container, e.InvSlot) is Item item && (e.EquipSlot < 16 || LinkshellItem.Is(item)))
                    c.Equip.Add(e);
        }

        private void LoadSpells(CharData c)
        {
            var r = db_.Execute(SqlSpells, new List<BoundValue> { BoundValue.Of(c.Id), BoundValue.Of(SpellExpansions) });
            while (r != null && r.Next())
            {
                int id = r.Get<ushort>("spellid");
                if (id < c.SpellList.Length * 8) c.SpellList[id / 8] |= (byte)(1 << (id % 8));
            }
        }

        // runs during Load once the stats and gift mods are in, before the equipment
        public Action<CharData> AfterGiftMods;

        // runs during Load after the abilities are built, before the equipment
        public Action<CharData> BuildTraits;

        // runs during Load once the job levels are set (LoadFromCharRecastSQL's place)
        public Action<CharData> AfterLevels;

        // BuildingCharAbilityTable's learn: runs for each ability added (the zone host adds its recast)
        public Action<CharData, AbilityTable.Ability> AbilityLearned;

        // runs during Load after the animation is set, just before the equipment
        public Action<CharData> BeforeEquip;

        // the dataset a Monipulator's stats and species come from (the zone host's, loaded on first use)
        public Func<MonstrosityTable> MonTable;

        // charutils::CalculateStats
        public void CalculateStats(CharData c)
        {
            int mjob = c.MJob, sjob = c.SJob;
            byte mlvl = c.MLvl, slvl = c.SLvl;
            // a MON job cannot generate stats: it generates as WAR/WAR, a Monipulator as its species' jobs at its level
            if (mjob == 23 || sjob == 23) { mjob = 1; sjob = 1; }
            var monTable = c.Monstrosity != null ? MonTable?.Invoke() : null;
            if (monTable != null)
            {
                var species = XI.Zone.Monstrosity.SpeciesJobs(c.Monstrosity);
                mjob = species.MainJob;
                sjob = species.SubJob;
                mlvl = slvl = species.Level;
            }

            int race = c.Look.Race switch { 1 or 2 => 0, 3 or 4 => 1, 5 or 6 => 2, 7 => 3, 8 => 4, _ => 0 };

            int hpMerits = c.Merits?.Value(c, MeritTable.MaxHp) ?? 0, mpMerits = c.Merits?.Value(c, MeritTable.MaxMp) ?? 0;
            c.MaxHp = c.BaseMaxHp = (grades_.GetBaseHP(race, grades_.GetJobGrade(mjob, 0), mlvl, grades_.GetJobGrade(sjob, 0), slvl) + hpMerits);
            c.MaxMp = c.BaseMaxMp = (grades_.GetBaseMP(race, grades_.GetJobGrade(mjob, 1), mlvl, grades_.GetJobGrade(sjob, 1), slvl) + mpMerits);
            for (int stat = 2; stat <= 8; stat++)
            {
                // Str, Dex, Vit, Agi, Int, Mnd, Chr sit two apart
                int merit = c.Merits?.Value(c, (ushort)(MeritTable.Str + 2 * (stat - 2))) ?? 0;
                c.BaseStats[stat - 2] = (ushort)(grades_.GetBaseStat(grades_.GetRaceGrades(race, stat), grades_.GetJobGrade(mjob, stat), mlvl, grades_.GetJobGrade(sjob, stat), slvl) + merit);
            }
            if (monTable != null) MonstrosityStats(c, monTable);
        }

        // monstrosity::CalculateStats: the species' scale over the mob HP formula (instinct HP comes off, as
        // UpdateHealth adds it unscaled), the MP a BLM or PLD main job or a mage or PLD sub job gives, and stats one under
        // the mob formula, the closest fit to retail; merits count, but only one step per 10 levels
        private void MonstrosityStats(CharData c, MonstrosityTable table)
        {
            var data = c.Monstrosity;
            byte level = XI.Zone.Monstrosity.SpeciesJobs(data).Level;
            if (table.FindSpecies(data.Species) is not { } species) return;
            int CappedMerit(ushort merit, int perStep) => Math.Min(c.Merits?.Value(c, merit) ?? 0, level / 10 * perStep);

            int instinctHp = XI.Zone.Monstrosity.InstinctModTotal(table, data.EquippedInstincts, Mod.HP);
            int baseHp = (int)MobStats.MonipulatorBaseHP(grades_, species.HpMainJob, species.HpSubJob, level) + CappedMerit(MeritTable.MaxHp, 10);
            c.MaxHp = c.BaseMaxHp = species.HpScale * (baseHp + instinctHp) / 100 - instinctHp;

            // TODO (C++): DRK subs and WHM or RDM main jobs are unverified. The PLD split is fit at level 30 only.
            int mainMp = data.MainJob switch
            {
                XI.Zone.Monstrosity.JobBlm => 35 * level + 59, // Metal Slime and Spriggan.G, levels 1-10
                XI.Zone.Monstrosity.JobPld => 17 * level,
                _ => 0,
            };
            int subMp = data.SubJob switch
            {
                XI.Zone.Monstrosity.JobBlm => (35 * level + 59) / 2,
                XI.Zone.Monstrosity.JobWhm => (34 * level + 58) / 2,
                XI.Zone.Monstrosity.JobRdm => (33 * level + 57) / 2,
                XI.Zone.Monstrosity.JobPld => (29 * level + 115) / 2,
                _ => 0,
            };
            int baseMp = mainMp + subMp;
            c.MaxMp = c.BaseMaxMp = baseMp > 0 ? baseMp + CappedMerit(MeritTable.MaxMp, 10) : 0;

            if (species.StatRanks == null) return;
            for (int k = 0; k < 7; k++)
            {
                int family = MobStats.GetBaseToRank(species.StatRanks[k], level);
                int main = MobStats.GetBaseToRank(grades_.GetJobGrade(data.MainJob, k + 2), level);
                int sub = MobStats.GetBaseToRank(grades_.GetJobGrade(data.SubJob, k + 2), level) / 2;
                c.BaseStats[k] = (ushort)(family + main + sub - 1 + CappedMerit((ushort)(MeritTable.Str + 2 * k), 1));
            }
        }

        // BuildingCharAbilityTable: the main job's abilities up to its level, then the sub job's (not MAIN_ONLY), each
        // past CheckAbilityAddtype; a Monipulator's from its species' job and MON.
        public void BuildAbilityTable(CharData c)
        {
            Array.Clear(c.Abilities);
            // a Monipulator learns its species' main job abilities except Provoke, none from its sub job, and Relinquish
            if (c.Monstrosity != null)
            {
                foreach (var job in new[] { c.Monstrosity.MainJob, XI.Zone.Monstrosity.JobMon })
                    foreach (var a in abilities_.ForJob(job))
                    {
                        if (c.MLvl < a.Level) break;
                        if (a.Id < AbilityTable.AbilityHealingRuby && a.Id != AbilityTable.AbilityPetCommands && a.Id != AbilityTable.AbilityProvoke && AddTypeOk(c, a))
                        {
                            SetBit(c.Abilities, a.Id);
                            AbilityLearned?.Invoke(c, a);
                        }
                    }
                return;
            }
            foreach (var a in abilities_.ForJob(c.MJob))
            {
                if (c.MLvl < a.Level) break;
                if (a.Id < AbilityTable.AbilityHealingRuby && a.Id != AbilityTable.AbilityPetCommands && AddTypeOk(c, a))
                {
                    SetBit(c.Abilities, a.Id);
                    AbilityLearned?.Invoke(c, a);
                }
            }
            if (c.SJob == 0) return;
            foreach (var a in abilities_.ForJob(c.SJob))
            {
                if (c.SLvl < a.Level) break;
                if (a.Level != 0 && a.Id < AbilityTable.AbilityHealingRuby && a.Id != AbilityTable.AbilityPetCommands && AddTypeOk(c, a) && (a.AddType & AbilityTable.AddTypeMainOnly) == 0)
                {
                    SetBit(c.Abilities, a.Id);
                    AbilityLearned?.Invoke(c, a);
                }
            }
        }

        private const ushort EffectAstralFlow = 55, EffectLightArts = 358, EffectDarkArts = 359, EffectAddendumWhite = 401,
            EffectAddendumBlack = 402;

        // charutils::CheckAbilityAddtype: a merit ability needs an upgrade in its merit, an Astral Flow one the effect, a
        // learned one the learning, an Arts one its Arts (or Addendum), a pet one the right kind of pet out (a charmed
        // mob is a MobEntity that is no PetEntity); Alexander, Odin and Atomos grant none of the avatar commands
        public static bool AddTypeOk(CharData c, AbilityTable.Ability a)
        {
            ushort t = a.AddType;
            if ((t & AbilityTable.AddTypeMerit) != 0 && (c.Merits == null || !c.Merits.Table.Exists(a.MeritModId) || c.Merits.Count(a.MeritModId) == 0)) return false;
            if ((t & AbilityTable.AddTypeAstralFlow) != 0 && !c.StatusEffects.HasStatusEffect(EffectAstralFlow)) return false;
            if ((t & AbilityTable.AddTypeLearned) != 0 && !(a.Id / 8 < c.LearnedAbilities.Length && (c.LearnedAbilities[a.Id / 8] & 1 << (a.Id % 8)) != 0)) return false;
            if ((t & AbilityTable.AddTypeLightArts) != 0 && !c.StatusEffects.HasStatusEffect(EffectLightArts, EffectAddendumWhite)) return false;
            if ((t & AbilityTable.AddTypeDarkArts) != 0 && !c.StatusEffects.HasStatusEffect(EffectDarkArts, EffectAddendumBlack)) return false;

            var pet = c.PPet as PetEntity;
            bool charmed = c.PPet is MobEntity && pet == null;
            int petBits = t & (AbilityTable.AddTypeJugPet | AbilityTable.AddTypeCharmPet);
            if (petBits == (AbilityTable.AddTypeJugPet | AbilityTable.AddTypeCharmPet) && !(charmed || pet?.PetType == PetType.JugPet)) return false;
            if (petBits == AbilityTable.AddTypeJugPet && pet?.PetType != PetType.JugPet) return false;
            if (petBits == AbilityTable.AddTypeCharmPet && !charmed) return false;
            if ((t & AbilityTable.AddTypeAvatar) != 0)
            {
                if (pet?.PetType != PetType.Avatar) return false;
                if (pet.PetId == PetId.Alexander || pet.PetId == PetId.Odin || pet.PetId == PetId.Atomos) return false;
            }
            if ((t & AbilityTable.AddTypeAutomaton) != 0 && pet?.PetType != PetType.Automaton) return false;
            return true;
        }

        private static void SetBit(byte[] bits, int id)
        {
            if (id / 8 < bits.Length) bits[id / 8] |= (byte)(1 << (id % 8));
        }
    }
}
