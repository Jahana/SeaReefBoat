using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XI.Common;
using XI.Map;
using XI.Ipc;
using XI.Persistence;
using XI.Script;
using XI.Zone;

// XI.Verify.DynamicEntities.Live -- live gate for dynamic entities (ZoneHost.DynamicEntities.cs): CLuaZone::insertDynamicEntity
// making an NPC (a dynamic targid and id, DE_<name>, the packet name, the look, its onTrigger cached at
// xi.zones.<zone>.npcs.DE_<name> and answering a talk) and a mob from mob_groups (the group's pool and species, the
// table's levels, its onMobSpawn / onMobDeath cached and called), both leaving the zone once they disappear with their
// targid held for 60 s; CLuaBaseEntity::instantiateMob spawning an ally at the char; and CLuaBattlefield::insertEntity's
// ally (mobutils::InstantiateAlly: Ajido-Marujido in Moon Reading, on the battlefield's ally list with its effect),
// gone with the battlefield's cleanup. One WAR 75 in Full Moon Fountain, with the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 993760;
    private const ushort FullMoonFountain = 170;
    private const ushort MoonReading = 225;
    private const uint AjidoGroup = 33;
    private static (string Name, uint Time, uint Size)? _record;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static ResultSet Sql(IDatabase db, string q, params BoundValue[] a) => db.Execute(q, a);

    // every table with a charid column, read from the schema at run time: chars' char_insert trigger fills char_equip, char_exp,
    // char_history, char_inventory, char_jobs, char_pet, char_points, char_profile, char_storage and char_unlocks and only
    // char_delete clears them, so a run aborted with one of them left behind would make the next run's INSERT INTO chars collide
    private static List<string> CharidTables(IDatabase db)
    {
        var tables = new List<string>();
        var r = Sql(db, "SELECT table_name AS t FROM information_schema.columns WHERE table_schema = DATABASE() AND column_name = 'charid' AND table_name <> 'chars'");
        while (r != null && r.Next()) tables.Add(r.Get<string>("t"));
        foreach (var t in new[] { "char_equip", "char_exp", "char_history", "char_inventory", "char_jobs", "char_pet", "char_points", "char_profile",
                                  "char_storage", "char_unlocks" })
            if (!tables.Contains(t)) tables.Add(t);
        return tables;
    }

    private static void Cleanup(IDatabase db)
    {
        foreach (var t in CharidTables(db))
            Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", BoundValue.Of(CharId));
        Sql(db, "DELETE FROM chars WHERE charid = ?", BoundValue.Of(CharId));
        Sql(db, "DELETE FROM accounts WHERE id = ?", BoundValue.Of(CharId));
        if (_record is { } r)
            Sql(db, "UPDATE bcnm_records SET fastestName = ?, fastestTime = ?, fastestPartySize = ? WHERE bcnmId = ?",
                BoundValue.Of(r.Name), BoundValue.Of(r.Time), BoundValue.Of(r.Size), BoundValue.Of(MoonReading));
    }

    private static string FindDataDir()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var cand = Path.Combine(d.FullName, "data");
            if (File.Exists(Path.Combine(cand, "grades.yaml"))) return cand;
        }
        throw new DirectoryNotFoundException("data (grades.yaml) not found above " + AppContext.BaseDirectory);
    }

    private static int Main(string[] argv)
    {
        int dbPort = argv.Length > 0 ? int.Parse(argv[0]) : 3307;
        var settings = new NetworkSettings { SqlHost = "127.0.0.1", SqlPort = dbPort, SqlLogin = "root", SqlPassword = "root", SqlDatabase = "xidb" };
        using var db = new MySqlDatabase(settings);

        try
        {
            var probe = db.Execute("SELECT 1", Array.Empty<BoundValue>());
            if (probe == null || !probe.Next()) { Console.WriteLine("DB not reachable"); return 3; }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DB not reachable ({ex.GetType().Name}: {ex.Message})");
            return 3;
        }

        try
        {
            var r = Sql(db, "SELECT fastestName, fastestTime, fastestPartySize FROM bcnm_records WHERE bcnmId = ?", BoundValue.Of(MoonReading));
            if (r != null && r.Next()) _record = (r.Get<string>("fastestName"), r.Get<uint>("fastestTime"), r.Get<uint>("fastestPartySize"));
            Cleanup(db);
            Run(db);
        }
        catch (Exception ex)
        {
            _fail++;
            Console.WriteLine($"FAIL exception: {ex}");
        }
        finally
        {
            Cleanup(db);
        }

        Console.WriteLine($"{_pass} passed, {_fail} failed");
        return _fail == 0 ? 0 : 1;
    }

    private static void Run(IDatabase db)
    {
        var dataDir = FindDataDir();
        var grades = Grades.Load(dataDir);
        var stats = new CharStats(GrowthTables.Load(db), new ItemData(db), new StatSettings());

        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of("dyn" + CharId), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Summoner"), BoundValue.Of(FullMoonFountain), BoundValue.Of((ushort)0),
            BoundValue.Of(340.0f), BoundValue.Of(48.5f), BoundValue.Of(-386.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 1500, 100, 1, 75)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 75)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, AbilityTable.Load(db, _ => true, 1.0), stats), dataDir, true, log.Add);
        long now = 1_000_000;
        zone.NowMs = () => now;
        zone.VanaTicksNow = () => 8000 * VanaTime.DAY + 12 * VanaTime.HOUR;
        zone.MobRng = new Random(5);
        var queue = new Queue<IIpcMessage>();
        zone.SendIpc = m => queue.Enqueue(m);
        void Pump()
        {
            for (int round = 0; round < 8; round++)
            {
                while (queue.Count > 0) zone.OnIpc(IpcFraming.Decode(IpcFraming.ToBytesWithHeader(queue.Dequeue())));
                zone.Tick();
                if (queue.Count == 0) return;
            }
        }
        void Tick(int ticks = 1)
        {
            for (int t = 0; t < ticks; t++)
            {
                now += 400;
                zone.Tick();
            }
        }
        using var scripts = new ScriptEngine(Path.GetDirectoryName(dataDir), log.Add, log.Add);
        zone.InitScripts(scripts);
        var lua = scripts.Lua;
        var got = new List<string>();
        lua.SetGlobal("report", l => { got.Add(l.ToStringValue(1)); return 0; });
        string Lua(string code)
        {
            got.Clear();
            lua.DoString($"local player = GetPlayerByID({CharId})\nlocal zone = player and player:getZone()\n" + code, "=verify_dynamic_entities");
            return string.Join(",", got);
        }

        var info = zone.ZoneInfoOf(FullMoonFountain);
        var s = new MapSession { CharId = CharId, AccountId = CharId };
        zone.OnLogin(s);
        var c = (CharData)s.Char;
        void Send(ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s, opcode, new BasicPacket(buf));
            zone.AfterParse(s);
        }
        Send(0x00A, 92, buf => BitConverter.GetBytes(CharId).CopyTo(buf, 12));
        Send(0x00C, 12);
        Pump();
        Check("the WAR 75 in Full Moon Fountain", "1/75/170", $"{c.MJob}/{c.MLvl}/{c.Zone}");
        List<string> LuaErrors() => log.Where(l => l.Contains("lua") && (l.Contains("rror") || l.Contains("not ported"))).ToList();
        log.Clear();

        Console.WriteLine("insertDynamicEntity: an NPC");
        int npcsBefore = info.Npcs.Count;
        Lua("local npc = zone:insertDynamicEntity({ objtype = xi.objType.NPC, name = 'Moogle Helper', look = 82, " +
            "x = 341.0, y = 48.5, z = -385.0, rotation = 64, releaseIdOnDisappear = true, " +
            "onTrigger = function(player, npc) report('triggered ' .. npc:getName()) end }) " +
            "report(npc:getName()) report(npc:getID()) report(npc:getTargID())");
        var npc = info.Npcs.LastOrDefault();
        Check("one more NPC, under a dynamic targid and id", $"{npcsBefore + 1}/True/True/True",
            $"{info.Npcs.Count}/{npc?.TargId >= 0x700}/{npc?.Id == info.DynamicId(npc?.TargId ?? 0)}/{npc != null && info.NpcById.ContainsKey(npc.Id)}");
        if (npc == null) return;
        Check("DE_ name, the packet name, the look, visible, renamed, released on disappearing", "DE_Moogle Helper/Moogle Helper/82/0/True/True/0/32",
            $"{npc.Name}/{npc.PacketName}/{npc.Look[1]}/{npc.Status}/{npc.IsRenamed}/{npc.ReleaseTargIdOnDisappear}/{npc.EntityFlags}/{npc.NamePrefix}");
        Check("its onTrigger cached at xi.zones.Full_Moon_Fountain.npcs.DE_Moogle Helper", "function",
            Lua("report(type(xi.zones.Full_Moon_Fountain.npcs['DE_Moogle Helper'].onTrigger))"));
        var deSpawn = EntityUpdatePacket.Npc(npc, EntityUpdate.Spawn, 0);
        Check("an onTrigger makes it triggerable (setTriggerable): the 0x40 bit at 0x28 of its 0x00E", "True/64",
            $"{npc.Triggerable}/{deSpawn.Buffer[0x28] & 0x40}");
        got.Clear();
        Send(0x01A, 28, b =>
        {
            BitConverter.GetBytes(npc.Id).CopyTo(b, 4);
            BitConverter.GetBytes(npc.TargId).CopyTo(b, C2sAction.OffActIndex);
            BitConverter.GetBytes((ushort)ActionId.Talk).CopyTo(b, C2sAction.OffActionID);
        });
        Check("a talk reaches the cached onTrigger", "triggered DE_Moogle Helper", string.Join(",", got));
        ushort npcTargId = npc.TargId;
        Lua($"GetNPCByID({npc.Id}):setStatus(xi.status.DISAPPEAR)");
        Tick();
        Check("disappeared: out of the zone, the targid held", $"{npcsBefore}/False/True",
            $"{info.Npcs.Count}/{info.NpcById.ContainsKey(npc.Id)}/{info.DynamicTargIdTaken(npcTargId)}");
        Tick(160);
        Check("the targid freed after 60 s", "False", info.DynamicTargIdTaken(npcTargId));

        Console.WriteLine("insertDynamicEntity: a mob from mob_groups");
        int mobsBefore = info.Mobs.Count;
        string spawnOut = Lua($"local mob = zone:insertDynamicEntity({{ objtype = xi.objType.MOB, groupId = {AjidoGroup}, groupZoneId = {FullMoonFountain}, " +
            "name = 'Practice Dummy', x = 345.0, y = 48.5, z = -380.0, rotation = 0, minLevel = 10, maxLevel = 10, releaseIdOnDisappear = true, " +
            "onMobSpawn = function(mob) report('spawned ' .. mob:getName()) end, " +
            "onMobDeath = function(mob, player, optParams) report('died') end }) " +
            "mob:spawn()");
        var mob = info.Mobs.LastOrDefault();
        Check("one more mob, under a dynamic targid, spawned", $"{mobsBefore + 1}/True/True/True",
            $"{info.Mobs.Count}/{mob?.TargId >= 0x700}/{mob != null && info.MobById.ContainsKey(mob.Id)}/{mob?.IsSpawned}");
        if (mob == null) return;
        Check("DE_ name and packet name, the group's pool and species, the table's levels, a mob", "DE_Practice Dummy/Practice Dummy/True/297/10/0",
            $"{mob.Name}/{mob.PacketName}/{mob.Pool != 0}/{mob.Species}/{mob.MLvl}/{mob.Allegiance}");
        Check("onMobSpawn from the cache", "spawned DE_Practice Dummy", spawnOut);
        ushort mobTargId = mob.TargId;
        got.Clear();
        mob.Hp = 0;
        Tick(5);
        Check("killed: dead, onMobDeath from the cache", "True/True", $"{mob.IsDead}/{got.Contains("died")}");
        Tick(60);
        Check("faded out: out of the zone, the targid held", $"{mobsBefore}/False/True",
            $"{info.Mobs.Count}/{info.MobById.ContainsKey(mob.Id)}/{info.DynamicTargIdTaken(mobTargId)}");
        Check("no Lua errors", "", string.Join(" | ", LuaErrors()));

        Console.WriteLine("instantiateMob: an ally at the char");
        mobsBefore = info.Mobs.Count;
        Lua($"player:instantiateMob({AjidoGroup})");
        var ally = info.Mobs.LastOrDefault();
        Check("Ajido-Marujido spawned where the char stands, an ally, released on disappearing", $"{mobsBefore + 1}/Ajido-Marujido/True/1/True/0",
            $"{info.Mobs.Count}/{ally?.Name}/{ally?.IsSpawned}/{ally?.Allegiance}/{ally?.ReleaseTargIdOnDisappear}/{ally?.GetMobMod(MobMod.Charmable)}");
        Check("at the char", "True", ally != null && Math.Abs(ally.X - c.X) < 0.01f && Math.Abs(ally.Z - c.Z) < 0.01f);
        if (ally != null)
        {
            ally.FadeOut();
            Tick();
            Check("faded: out of the zone", $"{mobsBefore}/False", $"{info.Mobs.Count}/{info.MobById.ContainsKey(ally.Id)}");
        }

        Console.WriteLine("CLuaBattlefield::insertEntity: a battlefield ally");
        Check("Moon Reading registered", "2", Lua($"local content = xi.battlefield.contents[{MoonReading}] " +
            "report(player:registerBattlefield(content.battlefieldId, 1, player:getID(), content))"));
        var bf = c.PBattlefield;
        Check("inside Moon Reading", "True/225", $"{bf != null}/{bf?.Id}");
        if (bf == null)
        {
            foreach (var line in log) Console.WriteLine("  log: " + line);
            return;
        }
        mobsBefore = info.Mobs.Count;
        Check("insertEntity(33, true, true): Ajido-Marujido, spawned at the arena's spot", "Ajido-Marujido,75",
            Lua("local ajido = player:getBattlefield():insertEntity(33, true, true) ajido:setSpawn(340.117, 48.752, -383.747, 64) ajido:spawn() " +
                "ajido:setMobLevel(75) report(ajido:getName()) report(ajido:getMainLvl())"));
        var bfAlly = bf.AllyList.FirstOrDefault();
        Check("on the ally list, inside, with the battlefield's effect", "1/True/True/225",
            $"{bf.AllyList.Count}/{bfAlly?.PBattlefield == bf}/{bfAlly?.IsSpawned}/{bfAlly?.StatusEffects.GetStatusEffect(Battlefield.EffectBattlefield)?.Power}");
        Check("getAllies", "1", Lua("report(#player:getBattlefield():getAllies())"));
        Lua("player:getBattlefield():lose()");
        Tick(40);
        Check("the battlefield cleaned up: the ally removed and gone from the zone", $"0/{mobsBefore}/False",
            $"{info.BattlefieldHandler.Battlefields.Count}/{info.Mobs.Count}/{bfAlly != null && info.MobById.ContainsKey(bfAlly.Id)}");
        Check("no Lua errors", "", string.Join(" | ", LuaErrors()));
    }
}
