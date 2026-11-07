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

// XI.Verify.PlayerPets.Live -- live gate for player pets (ZoneHost.PlayerPets.cs, PetEntity.cs): a char's spawnPet
// (an SMN's Carbuncle and Fire Spirit, a BST's Sheep Familiar, a DRG's named wyvern) under a dynamic targid in the
// zone's pet list, its stats from pet_list and the master (the avatar script's perpetuation cost and -50% PDT, the
// spirit's spells and MPP, the jug pet's level range and its HP at Pet_t's HP scale of 1), the client view (0x00E
// with the pet bit, the special spawn animation once, never triggerable; 0x068 pet sync; the pet's index in 0x037),
// the pet following its master, despawnPet (death, the 5.5 s fade, the targid held 60 s), the jug pet's time running
// out, the pet dying with its master, the avatar's perpetuation (the MP cost, the reduction gear, leaving at 0 MP),
// pet combat (petAttack refused on another's claim; the pet's hits claiming for the master; the mob turning on and
// killing the pet; the pet's kill), blood pacts (ZoneHost.PetSkills.cs: the avatar's commands and pet command bits,
// Healing Ruby on the master with its MP and recast, an unlearned blood pact refused, Poison Nails on a rabbit with the
// claim, enmity and re-engage, a pact out of the avatar's reach), the spirits' casting (the Light Spirit's buffs out of
// combat, the Fire Spirit's spells at its foe), Call Beast (the jug from the ammo slot, used up), a BST's commands on a jug pet (the jug pet's
// abilities and ready move bits, Fight, a Ready move on the pet's foe, Heel, Stay, Reward refused without food, Leave:
// TARGET_PET abilities, hasJugPet), status effects on a pet (a REGEN ticking it up and its wear-off to the origin,
// REGAIN while idle where an idle mob gets none, CHARM_I refused on it, a master gaining CHARM_I losing it), a GEO's auras (ZoneHost.Auras.cs: Indi-Regen's COLURE_ACTIVE on the caster giving
// it GEO_REGEN, refreshed while the aura lasts and gone 4 s after; Geo-Regen's luopan at the target plus the cast
// packet's offset, its stats, its aura reaching the master in range and not out of it, its HP running down, a second
// Geo- spell refused while it stands), a BST's charm (ZoneHost.Charm.cs: a rabbit as the char's pet, Sic learned, the
// claim and allegiance, following its master, fighting another rabbit for its master and taking its swings,
// extendCharm, the charm running out (turning on its master, the other mob forgetting it), Leave (forgetting
// everything), its death freeing the master), a char charmed by a mob (CPlayerCharmController: inactive 5 s, following
// its charmer, engaging the charmer's target, a second char, and hitting it; free again when the charmer goes), zoning
// with the pet (ZoneHost.PetZoning.cs: the
// info kept on the zone request, the char_stats pet columns, the avatar back on game ok with its HP and MP and no
// spawn animation, a job change dropping the info), and the pet leaving with its master's logout. One char in West
// Ronfaure, with the real MariaDB (xidb).
//
// Exit codes: 0 = all checks passed; 1 = a check failed; 3 = DB not reachable (prerequisite).
internal static class Program
{
    private static int _pass, _fail;

    private const uint CharId = 993710, AllyId = 993711;
    private const ushort WestRonfaure = 100;
    private const byte JobBst = 9, JobDrg = 14, JobSmn = 15, JobGeo = 21;
    private const ushort IndiRegen = 768, GeoRegen = 798, AbilityLeave = 71;
    private const ushort CarrotBroth = 17860;
    private const uint Rabbit = 17186822;

    private static void Check(string name, object expected, object actual)
    {
        bool ok = Equals(expected?.ToString(), actual?.ToString());
        if (ok) _pass++; else _fail++;
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}" + (ok ? "" : $"  expected=[{expected}] actual=[{actual}]"));
    }

    private static ulong U(BasicPacket p, int bit, int len) => Battle2Packets.Unpack(p.Buffer, bit, len);

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
            foreach (uint id in new[] { CharId, AllyId })
                Sql(db, $"DELETE FROM `{t}` WHERE charid = ?", BoundValue.Of(id));
        foreach (uint id in new[] { CharId, AllyId })
        {
            Sql(db, "DELETE FROM chars WHERE charid = ?", BoundValue.Of(id));
            Sql(db, "DELETE FROM accounts WHERE id = ?", BoundValue.Of(id));
        }
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
            BoundValue.Of(CharId), BoundValue.Of("ppet" + CharId), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of("Tamer"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(-200.0f), BoundValue.Of(0.0f), BoundValue.Of(-200.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(CharId), BoundValue.Of(CharId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 1500, 500, 15, 75)", BoundValue.Of(CharId));
        Sql(db, "REPLACE INTO char_jobs(charid, smn, bst, drg, geo) VALUES (?, 75, 75, 75, 75)", BoundValue.Of(CharId));
        // the GEO's spells and geomancy skill
        Sql(db, "INSERT INTO char_spells(charid, spellid) VALUES (?, ?), (?, ?)",
            BoundValue.Of(CharId), BoundValue.Of(IndiRegen), BoundValue.Of(CharId), BoundValue.Of(GeoRegen));
        Sql(db, "INSERT INTO char_skills(charid, skillid, value, rank) VALUES (?, 44, 300, 0)", BoundValue.Of(CharId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(CharId));
        // Call Beast's jug: 2 Carrot Broths (subskill 22, the Hare Familiar) in inventory slot 1
        Sql(db, "INSERT INTO char_inventory(charid, location, slot, itemId, quantity, extra) VALUES (?, 0, 1, ?, 2, ?)",
            BoundValue.Of(CharId), BoundValue.Of(CarrotBroth), BoundValue.Of(new byte[24]));
        // the ally a charmed Tamer turns on: a WAR 75 beside it
        Sql(db, "INSERT INTO accounts(id,login,password,content_ids) VALUES (?,?,?,?)",
            BoundValue.Of(AllyId), BoundValue.Of("ppet" + AllyId), BoundValue.Of("x"), BoundValue.Of((byte)1));
        Sql(db, "INSERT INTO chars(charid,accid,charname,pos_zone,pos_prevzone,pos_x,pos_y,pos_z,nation,gmlevel) VALUES (?,?,?,?,?,?,?,?,?,?)",
            BoundValue.Of(AllyId), BoundValue.Of(AllyId), BoundValue.Of("Ally"), BoundValue.Of(WestRonfaure), BoundValue.Of((ushort)0),
            BoundValue.Of(-200.0f), BoundValue.Of(0.0f), BoundValue.Of(-200.0f), BoundValue.Of((byte)0), BoundValue.Of((byte)0));
        Sql(db, "INSERT INTO accounts_sessions(accid,charid,server_addr,server_port) VALUES (?,?,?,?)",
            BoundValue.Of(AllyId), BoundValue.Of(AllyId), BoundValue.Of(0u), BoundValue.Of((ushort)0));
        Sql(db, "REPLACE INTO char_stats(charid, hp, mp, mjob, mlvl) VALUES (?, 1500, 0, 1, 75)", BoundValue.Of(AllyId));
        Sql(db, "REPLACE INTO char_jobs(charid, war) VALUES (?, 75)", BoundValue.Of(AllyId));
        Sql(db, "INSERT IGNORE INTO char_storage(charid) VALUES (?)", BoundValue.Of(AllyId));
        // the wyvern's name: pet_name 2 (Cerulean)
        Sql(db, "REPLACE INTO char_pet(charid, wyvernid) VALUES (?, 2)", BoundValue.Of(CharId));

        var log = new List<string>();
        var zone = new ZoneHost(db, new CharLoader(db, grades, AbilityTable.Load(db, _ => true, 1.0), stats), dataDir, true, log.Add);
        // this suite's scenes are set at places off the zone's navmesh, so its paths are straight lines
        // (XI.Verify.NavMesh.Live walks the mesh)
        zone.NavMeshDir = null;
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
            lua.DoString($"local player = GetPlayerByID({CharId})\nlocal pet = player and player:getPet()\n" + code, "=verify_playerpets");
            return string.Join(",", got);
        }

        var info = zone.ZoneInfoOf(WestRonfaure);
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
        Check("the SMN 75 in West Ronfaure", "15/75/100", $"{c.MJob}/{c.MLvl}/{c.Zone}");

        List<BasicPacket> Sent(ushort opcode) => s.PacketList.Where(p => p.GetOpcode() == opcode).ToList();
        List<string> LuaErrors() => log.Where(l => l.Contains("lua") && (l.Contains("rror") || l.Contains("not ported"))).ToList();

        Console.WriteLine("spawnPet: Carbuncle beside the SMN");
        c.Rotation = 0; // facing east: behind is west
        s.PacketList.Clear();
        log.Clear();
        Lua("player:spawnPet(xi.petId.CARBUNCLE)");
        var pet = c.PPet as PetEntity;
        Check("a PetEntity linked both ways, in the zone's pet list", "True/True/True", $"{pet != null}/{pet?.PMaster == c}/{info.Pets.Contains(pet)}");
        Check("a dynamic targid from 0x700 and its id", $"True/{0x01000000u | (uint)WestRonfaure << 12 | (uint)(pet.TargId + 0x100)}",
            $"{pet.TargId >= 0x700 && pet.TargId <= 0x8FF}/{pet.Id}");
        Check("getPetID, isPet, isAvatar, isJugPet, isMob, getObjType", "8/true/true/false/false/8",
            Lua("report(pet:getPetID() .. '/' .. tostring(pet:isPet()) .. '/' .. tostring(pet:isAvatar()) .. '/' .. tostring(pet:isJugPet()) .. " +
                "'/' .. tostring(pet:isMob()) .. '/' .. pet:getObjType())"));
        float dx = pet.X - c.X, dz = pet.Z - c.Z;
        Check("2.1 yalms behind the master (west), facing its way", "2.1/True/0", $"{MathF.Sqrt(dx * dx + dz * dz):0.0}/{dx < -2.0f}/{pet.Rotation}");
        Check("level 75 from the SMN, its name, the avatar's 1:1 sub level", "75/75/Carbuncle", $"{pet.MLvl}/{pet.SLvl}/{pet.Name}");
        Check("the avatar script ran: -50% PDT, damage offsets 2, a perpetuation cost on the master", "-5000/2/2/True",
            $"{pet.GetMod(Mod.DMGPHYS)}/{pet.GetMobMod(MobMod.DamageOffset)}/{pet.GetMobMod(MobMod.RangedDamageOffset)}/{c.GetMod(Mod.AVATAR_PERPETUATION) > 0}");
        Check("full HP and MP, stats set, the script's weapon damage (level + 2)", "True/True/True/77",
            $"{pet.Hp == pet.MaxHp && pet.MaxHp > 500}/{pet.Mp == pet.MaxMp && pet.MaxMp > 0}/{pet.Stats.All(v => v > 20)}/{pet.MainDamage}");
        Check("the char sees it", true, c.SpawnPetList.Contains(pet.Id));
        var spawn = Sent(0x00E).FirstOrDefault(p => BitConverter.ToUInt32(p.Buffer, 4) == pet.Id);
        Check("0x00E: the pet bit at 0x27, the special spawn animation, not triggerable, its name", "8/4/0/Carbuncle",
            spawn == null ? "none" : $"{spawn.Buffer[0x27] & 0x08}/{spawn.Buffer[0x28] & 0x04}/{spawn.Buffer[0x28] & 0x40}/" +
                                     System.Text.Encoding.ASCII.GetString(spawn.Buffer, 0x34, 9));
        Check("the special spawn animation only the once", 0, pet.SpawnAnimation);
        var sync = Sent(0x068).LastOrDefault();
        Check("0x068 pet sync: the pet's targid, HPP and name", $"{pet.TargId}/100/Carbuncle",
            sync == null ? "none" : $"{BitConverter.ToUInt16(sync.Buffer, 0x0C)}/{sync.Buffer[0x0E]}/" + System.Text.Encoding.ASCII.GetString(sync.Buffer, 0x18, 9));
        var status = Sent(0x037).LastOrDefault();
        Check("0x037: the pet's index in Flags2", pet.TargId, status == null ? -1 : (int)(BitConverter.ToUInt32(status.Buffer, 52) >> 3 & 0xFFFF));
        Check("a second spawnPet is refused while one is out", "True/1", $"{c.PPet == pet}/" +
            Lua("player:spawnPet(xi.petId.IFRIT) report(#tostring(player:getPetID()))"));
        Check("no script errors", "", string.Join(" | ", LuaErrors()));

        Console.WriteLine("/checkparam (0x0DD kind 2) on the pet");
        s.PacketList.Clear();
        Send(0x0DD, 16, b => { BitConverter.GetBytes(pet.Id).CopyTo(b, 4); BitConverter.GetBytes((uint)pet.TargId).CopyTo(b, 8); b[12] = 2; });
        var param = Sent(0x029);
        Check("name, primary, auxiliary, range, defence: no item level line, all on the pet", "733/712/713/714/715 True",
            string.Join("/", param.Select(p => BitConverter.ToUInt16(p.Buffer, 24))) + " " + param.All(p => BitConverter.ToUInt32(p.Buffer, 8) == pet.Id));
        string Pair(int i) => i < param.Count ? $"{BitConverter.ToUInt32(param[i].Buffer, 12)}:{BitConverter.ToUInt32(param[i].Buffer, 16)}" : "none";
        Check("its accuracy and attack, no sub or ranged weapon on the master (0:0), its evasion and defence", "True/0:0/0:0/True",
            $"{Pair(1) != "0:0"}/{Pair(2)}/{Pair(3)}/{Pair(4) != "0:0"}");
        s.PacketList.Clear();
        Send(0x0DD, 16, b => { BitConverter.GetBytes(pet.Id + 1).CopyTo(b, 4); BitConverter.GetBytes((uint)pet.TargId).CopyTo(b, 8); b[12] = 2; });
        Check("someone else's id: no answer", 0, Sent(0x029).Count);

        Console.WriteLine("The pet follows its master");
        c.X += 10;
        Tick(12);
        dx = pet.X - c.X; dz = pet.Z - c.Z;
        float dist = MathF.Sqrt(dx * dx + dz * dz);
        Check("caught up to within about 2 yalms", true, dist <= 2.2f);
        Check("its moves went out (UpdatePos sent)", true, Sent(0x00E).Any(p => BitConverter.ToUInt32(p.Buffer, 4) == pet.Id && (p.Buffer[0x0A] & 0x01) != 0));

        Console.WriteLine("Bindings 12: trigger areas, positionSpecial, isFellow, printAllMods");
        Check("onPlayerTriggerAreaEnter / Leave, clearPlayerTriggerAreas", "true,false,true,false",
              Lua("player:onPlayerTriggerAreaEnter(7) report(tostring(player:isPlayerInTriggerArea(7))) " +
                  "player:onPlayerTriggerAreaLeave(7) report(tostring(player:isPlayerInTriggerArea(7))) " +
                  "player:onPlayerTriggerAreaEnter(8) report(tostring(player:isPlayerInTriggerArea(8))) " +
                  "player:clearPlayerTriggerAreas() report(tostring(player:isPlayerInTriggerArea(8)))"));
        float psX = c.X;
        s.PacketList.Clear();
        Lua("player:positionSpecial({ x = 1, y = 2, z = 3, rot = 64 }, 10)");
        var wpos = Sent(0x05B).FirstOrDefault();
        Check("positionSpecial ROTATE: turned, not moved, a WPOS with the mode", $"64/{psX}/10",
              $"{c.Rotation}/{c.X}/{(wpos == null ? -1 : wpos.Buffer[4 + 18])}");
        Check("isFellow", "false", Lua("report(tostring(player:isFellow()))"));
        int logAt = log.Count;
        Lua("player:printAllMods()");
        Check("printAllMods logs the char's mods", true, log.Skip(logAt).Any(x => x.Contains("mods:")));

        Console.WriteLine("Bindings 13: positionSpecial LOCK / UNLOCK, startSequence, sendDebugPacket, injectPacket, raceChange");
        Lua($"player:positionSpecial({{ x = {c.X}, y = {c.Y}, z = {c.Z}, rot = {c.Rotation} }}, 8)");
        bool lockedOn = c.Locked;
        Lua($"player:positionSpecial({{ x = {c.X}, y = {c.Y}, z = {c.Z}, rot = {c.Rotation} }}, 9)");
        Check("positionSpecial LOCK locks the char, UNLOCK frees it", "True/False", $"{lockedOn}/{c.Locked}");
        bool wasInSequence = c.InSequence;
        Check("startSequence: true, the char in a sequence", "true/True", Lua("report(tostring(player:startSequence()))") + $"/{c.InSequence}");
        c.InSequence = wasInSequence;
        s.PacketList.Clear();
        Lua("player:sendDebugPacket({ 0x3F, 0x02, 0x00, 0x00, 0x11, 0x22 })");
        var debugPacket = Sent(0x03F).FirstOrDefault();
        Check("sendDebugPacket: the table's bytes to the char", "17/34", debugPacket == null ? "none" : $"{debugPacket.Buffer[4]}/{debugPacket.Buffer[5]}");
        string injectFile = Path.Combine(Path.GetTempPath(), $"xi_inject_{Environment.ProcessId}.dat");
        File.WriteAllBytes(injectFile, new byte[] { 0x3E, 0x04, 0x00, 0x00, 0xAB, 0xCD, 0xEF, 0x01, 0x99 });
        Lua($"player:injectPacket('{injectFile.Replace("\\", "/")}')");
        File.Delete(injectFile);
        var injected = Sent(0x03E).FirstOrDefault();
        Check("injectPacket: the file's size x 2 bytes to the char", "171/1/0", injected == null ? "none" : $"{injected.Buffer[4]}/{injected.Buffer[7]}/{injected.Buffer[8]}");
        logAt = log.Count;
        Check("raceChange: a race out of bounds refused", "false", Lua("report(tostring(player:raceChange(9, 0, 0)))"));
        Check("raceChange: the bounds error logged", true, log.Skip(logAt).Any(x => x.Contains("out of bounds")));
        Check("breakLinkshell: no linkshell of that name", "false", Lua("report(tostring(player:breakLinkshell('NoSuchShellXyz')))"));
        s.PacketList.Clear();
        Lua("player:sendLinkshellConcierge({ yourSlot = 5, postedDays = 22, slots = { [0] = { groupId = 47134, groupKey = 0xCA2B, color = 0xF55F, " +
            "flag = 3, name = 'GobTrain', lang = 2, membersGoal = 8, activeTier = 2, characteristics = 0x33CF }, [5] = { groupId = 7 } } })");
        var concierge = Sent(0x048);
        Check("sendLinkshellConcierge: a header and four records, 128 bytes each", "5/True", $"{concierge.Count}/{concierge.All(p => (p.Buffer[1] >> 1) * 4 == 128)}");
        if (concierge.Count == 5)
        {
            byte[] hd = concierge[0].Buffer, r0 = concierge[1].Buffer, r1 = concierge[2].Buffer;
            Check("header: the sentinel, slot 5, listed, registered, '22d'", $"{0xFEFEFEFE:X8}/5/65535/1/22d",
                $"{BitConverter.ToUInt32(hd, 4):X8}/{BitConverter.ToUInt16(hd, 8)}/{BitConverter.ToUInt16(hd, 10)}/{hd[48]}/{System.Text.Encoding.ASCII.GetString(hd, 28, 3)}");
            uint attrs = 1u | 1u << 2 | 8u << 6 | 2u << 14 | 0x33CFu << 16;
            Check("record 0: slot 0 then empties, 0x0203, the body, the attributes", $"0,255,255,255/515/47134/{0xCA2B}/{0xF55F}/3/{attrs:X8}",
                $"{r0[4]},{r0[5]},{r0[6]},{r0[7]}/{BitConverter.ToUInt16(r0, 8)}/{BitConverter.ToUInt32(r0, 16)}/{BitConverter.ToUInt16(r0, 20)}/" +
                $"{BitConverter.ToUInt16(r0, 22)}/{r0[24]}/{BitConverter.ToUInt32(r0, 112):X8}");
            // EncodeStringLinkshell: 6 bits a char, most significant first ('G' 33, 'o' 15)
            Check("record 0: the name packed as a linkshell's", 33 << 2 | 15 >> 4, (int)r0[25]);
            Check("record 1: slot 5 in its second entry", "255,5,255,255/7", $"{r1[4]},{r1[5]},{r1[6]},{r1[7]}/{BitConverter.ToUInt32(r1, 16 + 24)}");
        }
        var mannequin = new Item { Location = Container.Inventory, Slot = 25, ItemId = 256, Quantity = 1 };
        c.Items.Add(mannequin);
        string poseBefore = Lua("report(player:getMannequinPose(256))");
        string poseAfter = Lua("player:setMannequinPose(256, 3, 7) report(player:getMannequinPose(256))");
        Check("get / setMannequinPose: the exdata race and pose", "0/3/7", $"{poseBefore}/{mannequin.Extra[18]}/{poseAfter}");
        c.Items.Remove(mannequin);
        Check("getMannequinPose: none without the mannequin", "0", Lua("report(player:getMannequinPose(256))"));

        Console.WriteLine("despawnPet: the pet dies, fades and frees its targid a minute later");
        s.PacketList.Clear();
        ushort targid = pet.TargId;
        Lua("player:despawnPet()");
        Check("detached at once: no pet, dying, no perpetuation cost", "True/True/3/0",
            $"{c.PPet == null}/{pet.PMaster == null}/{pet.Animation}/{c.GetMod(Mod.AVATAR_PERPETUATION)}");
        sync = Sent(0x068).LastOrDefault();
        Check("the empty pet sync (0x1C)", 0x1C, sync == null ? -1 : sync.Buffer[1] >> 1 << 2);
        Tick(6);
        Check("still there 2.4 s in", true, info.Pets.Contains(pet));
        Tick(10);
        Check("faded after the death's 2.5 s and the despawn's 3 s, out of the zone", "False/False/True",
            $"{info.Pets.Contains(pet)}/{c.SpawnPetList.Contains(pet.Id)}/" +
            Sent(0x00E).Any(p => BitConverter.ToUInt32(p.Buffer, 4) == pet.Id && p.Buffer[0x0A] == 0x30));
        Check("its targid held", true, info.DynamicTargIdTaken(targid));
        now += 61000;
        Tick();
        Check("and freed after 60 s", false, info.DynamicTargIdTaken(targid));

        Console.WriteLine("A Fire Spirit: an elemental's spells and MP");
        log.Clear();
        Lua("player:spawnPet(xi.petId.FIRE_SPIRIT)");
        pet = c.PPet as PetEntity;
        Check("spawned: the elemental's magic timing and spells, the script's MPP 300 and full MP", "True/12/True/300/True",
            $"{pet != null}/{pet?.GetMobMod(MobMod.MagicDelay)}/{pet?.SpellContainer.HasSpells}/{pet?.GetMod(Mod.MPP)}/{pet != null && pet.Mp == pet.MaxMp && pet.MaxMp > 0}");
        Check("no script errors", "", string.Join(" | ", LuaErrors()));
        Lua("player:despawnPet()");
        Tick(16);

        Console.WriteLine("A BST's Sheep Familiar: the jug's level and time");
        c.MJob = JobBst;
        c.SJob = 0;
        Lua("player:addPetMod(xi.mod.HP, 100)");
        Lua("player:spawnPet(xi.petId.SHEEP_FAMILIAR)");
        pet = c.PPet as PetEntity;
        Check("a jug pet of level 33-35 (its cap 35, up to 2 below), an hour to live", "True/True/3600000",
            $"{pet?.PetType == PetType.JugPet}/{pet?.MLvl >= 33 && pet?.MLvl <= 35}/{pet?.JugDurationMs}");
        // LoadJugStats: 17 * level ^ 1.07f (past level 25) at Pet_t's HP scale of 1 (the constructor's; never loaded),
        // plus the pet mods' 100 HP
        int jugHp = (short)(17.0 * Math.Pow(pet?.MLvl ?? 0, (double)1.07f) * 1.0f) + 100;
        Check("Pet_t's HP scale of 1: the jug's HP and the pet mods' 100 HP", $"{jugHp}/{jugHp}", $"{pet?.MaxHp}/{pet?.Hp}");
        Check("the jug script: parry, damage offsets", "3/2", $"{pet?.GetMobMod(MobMod.CanParry)}/{pet?.GetMobMod(MobMod.DamageOffset)}");
        Check("isJugPet, not an avatar", "true/false", Lua("report(tostring(pet:isJugPet()) .. '/' .. tostring(pet:isAvatar()))"));
        now += 3600000;
        Tick(2);
        Check("its time up: despawned from its master", "True/True", $"{c.PPet == null}/{pet?.Animation == Anim.Death}");
        Tick(16);
        Lua("player:delPetMod(xi.mod.HP, 100)");

        Console.WriteLine("A DRG's wyvern: its name from char_pet");
        c.MJob = JobDrg;
        Lua("player:spawnPet(xi.petId.WYVERN)");
        pet = c.PPet as PetEntity;
        Check("the wyvern, named Cerulean, level 75 / 1", "True/Cerulean/75/1", $"{pet?.PetType == PetType.Wyvern}/{pet?.Name}/{pet?.MLvl}/{pet?.SLvl}");
        Check("its script: the base damage multiplier, -40% DT", "50/-4000", $"{pet?.GetMobMod(MobMod.BaseDamageMultiplier)}/{pet?.GetMod(Mod.DMG)}");
        int wyvernDa = pet?.GetMod(Mod.DOUBLE_ATTACK) ?? 0;
        Lua("pet:addWyvernJobTraits(xi.job.WAR, 30)");
        Check("addWyvernJobTraits: a level 30 WAR's Double Attack (10)", 10, (pet?.GetMod(Mod.DOUBLE_ATTACK) ?? 0) - wyvernDa);

        Console.WriteLine("The pet dies with its master");
        c.Hp = 0;
        Tick(2);
        Check("dead and detached", "True/True", $"{pet?.DeathAtMs != 0}/{c.PPet == null}");
        Tick(16);
        c.Hp = c.MaxHp;
        // up again: out of the death state
        c.DeathAtMs = 0;
        c.Animation = 0;

        Console.WriteLine("A wyvern for a non-DRG: refused");
        c.MJob = JobSmn;
        Lua("player:spawnPet(xi.petId.WYVERN)");
        Check("no pet", true, c.PPet == null);

        Console.WriteLine("Perpetuation: the avatar costs MP each effect tick, and leaves when it runs out");
        c.MJob = JobSmn;
        // the job swaps above left the SMN's MP pool at 0: a fixed one
        c.SetModifier(Mod.MP, 1000);
        CharStats.UpdateHealth(c);
        Lua("player:spawnPet(xi.petId.CARBUNCLE)");
        pet = c.PPet as PetEntity;
        int cost = c.GetMod(Mod.AVATAR_PERPETUATION);
        c.Mp = 400;
        Tick(8);
        Check("MP down by the cost each 3 s tick", true, cost > 0 && c.Mp < 400 && (400 - c.Mp) % cost == 0);
        int ticks = (400 - c.Mp) / cost;
        c.SetModifier(Mod.PERPETUATION_REDUCTION, 1);
        int before = c.Mp;
        Tick(8);
        Check("less the reduction gear", true, ticks > 0 && (before - c.Mp) % (cost - 1) == 0 && before > c.Mp);
        c.SetModifier(Mod.PERPETUATION_REDUCTION, 0);
        c.Mp = 1;
        Tick(8);
        Check("out of MP: the avatar leaves", "0/True", $"{c.Mp}/{c.PPet == null}");
        Tick(16);
        c.Mp = c.MaxMp;

        Console.WriteLine("Pet combat: Carbuncle fights a rabbit");
        var rabbit = info.MobById[Rabbit];
        Lua($"SpawnMob({Rabbit})");
        Lua("player:spawnPet(xi.petId.CARBUNCLE)");
        pet = c.PPet as PetEntity;
        rabbit.X = c.X + 4; rabbit.Y = c.Y; rabbit.Z = c.Z;
        rabbit.OwnerId = 1;
        Lua($"player:petAttack(GetMobByID({Rabbit}))");
        Check("a mob claimed by someone else: refused", false, pet.Engaged);
        rabbit.OwnerId = 0;
        rabbit.Unkillable = true;
        Lua($"player:petAttack(GetMobByID({Rabbit}))");
        Check("petAttack: engaged on the rabbit", "True/True/1", $"{pet.Engaged}/{pet.BattleTarget == rabbit}/{pet.Animation}");
        s.PacketList.Clear();
        Tick(20);
        Check("it closed in and hit: the rabbit hurt, claimed for the master, hating the pet", "True/True/True",
            $"{rabbit.Hp < rabbit.MaxHp}/{rabbit.OwnerId == c.Id}/{rabbit.Enmity.CE(pet) > 0}");
        Check("the swings went out as the pet's", true, Sent(0x028).Any(p => BitConverter.ToUInt32(p.Buffer, 5) == pet.Id));
        Check("the rabbit turned on the pet", "True/True", $"{rabbit.Engaged}/{rabbit.BattleTarget == pet}");
        Check("getEntitiesInRange: the rabbit's enemies around the char (the char among them)", "true",
              Lua($"local mob = GetMobByID({Rabbit}) local found = false " +
                  "for _, e in ipairs(mob:getEntitiesInRange(player, 1, 2, 20, 0, 4)) do if e:getID() == player:getID() then found = true end end " +
                  "report(tostring(found))"));
        Check("getMasterThreatMob: none while the rabbit is on the pet", "nil", Lua("report(tostring(pet:getMasterThreatMob()))"));
        var rabbitTarget = rabbit.BattleTarget;
        rabbit.BattleTarget = c;
        rabbit.Enmity.UpdateEnmity(c, 0, 0);
        Check("getMasterThreatMob: the rabbit once it turns on the master", $"{Rabbit}",
              Lua("local t = pet:getMasterThreatMob() report(t and t:getID() or 0)"));
        rabbit.BattleTarget = rabbitTarget;
        // Tandem Strike: the master's power on the pet's RACC and ACC only while both are on the rabbit
        c.SetModifier(Mod.TANDEM_STRIKE_POWER, 20);
        int raccAlone = int.Parse(Lua("report(pet:getRACC())")), accAlone = int.Parse(Lua("report(pet:getACC())"));
        c.BattleTarget = rabbit;
        Check("Tandem Strike: +20 RACC / ACC with the master on the rabbit too", "20/20",
              $"{int.Parse(Lua("report(pet:getRACC())")) - raccAlone}/{int.Parse(Lua("report(pet:getACC())")) - accAlone}");
        c.BattleTarget = null;
        c.SetModifier(Mod.TANDEM_STRIKE_POWER, 0);
        // a level 5 rabbit never touches a level 75 Carbuncle: no evasion, defense or regen left
        pet.AutoAttackEnabled = false;
        pet.SetModifier(Mod.EVA, -2000);
        pet.SetModifier(Mod.DEF, -2000);
        pet.SetModifier(Mod.REGEN, 0);
        pet.Hp = 1;
        for (int i = 0; i < 150 && pet.DeathAtMs == 0; i++) Tick();
        Check("the rabbit killed it: dead, detached", "True/True", $"{pet.DeathAtMs != 0}/{c.PPet == null}");
        Tick(20);
        Check("the rabbit let go of the dead pet", false, rabbit.Engaged && rabbit.BattleTarget == pet);
        rabbit.Unkillable = false;

        Console.WriteLine("Pet combat: the kill");
        Lua("player:spawnPet(xi.petId.CARBUNCLE)");
        pet = c.PPet as PetEntity;
        rabbit.X = c.X + 4; rabbit.Y = c.Y; rabbit.Z = c.Z;
        rabbit.OwnerId = 0;
        rabbit.Enmity.Clear();
        Lua($"player:petAttack(GetMobByID({Rabbit}))");
        for (int i = 0; i < 60 && rabbit.IsAlive; i++) Tick();
        Check("the rabbit died", false, rabbit.IsAlive);
        Tick(4);
        Check("the pet put its weapon away", "False/0", $"{pet.Engaged}/{pet.Animation}");
        Lua("player:despawnPet()");
        Tick(16);

        Console.WriteLine("Blood pacts: Carbuncle's commands, Healing Ruby on its master, Poison Nails on a rabbit");
        void Ja(ushort id, ushort targid, uint targetId)
        {
            s.PacketList.Clear();
            Send(0x01A, 28, b =>
            {
                BitConverter.GetBytes(targetId).CopyTo(b, 4);
                BitConverter.GetBytes(targid).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)ActionId.JobAbility).CopyTo(b, C2sAction.OffActionID);
                C2sAction.SetSkillId(b, id);
            });
        }
        bool PetBit(int b) => (c.PetCommands[b / 8] & 1 << (b % 8)) != 0;
        bool Learned(int id) => (c.Abilities[id / 8] & 1 << (id % 8)) != 0;
        List<ushort> Messages() => Sent(0x029).Select(p => BitConverter.ToUInt16(p.Buffer, 0x18)).ToList();
        bool PetActed() => Sent(0x028).Any(p => BitConverter.ToUInt32(p.Buffer, 5) == pet.Id);
        log.Clear();
        Lua("player:spawnPet(xi.petId.CARBUNCLE)");
        pet = c.PPet as PetEntity;
        Check("the avatar's commands learned with it out (Assault, Release, Blood Pact: Ward)", "True/True/True",
            $"{Learned(88)}/{Learned(90)}/{Learned(172)}");
        Check("pet commands: Healing Ruby, Poison Nails; not Searing Light (Astral Flow) nor Fenrir's Moonlit Charge",
            "True/True/False/False", $"{PetBit(0)}/{PetBit(1)}/{PetBit(6)}/{PetBit(16)}");

        c.Hp = c.MaxHp - 400;
        c.Mp = 500;
        Ja(512, c.TargId, c.Id);
        Tick();
        Check("Healing Ruby: Carbuncle readies it, its packet out", "True/True",
            $"{pet.SkillState?.Skill.Id == 512}/{PetActed()}");
        Tick(4);
        var finish = Sent(0x028).FirstOrDefault(p => U(p, 40, 32) == pet.Id && U(p, 82, 4) == 13);
        Check("the finish: the pet's, category 13, Healing Ruby, on the master", $"{pet.Id}/13/512/1/{c.Id}",
            finish == null ? "none" : $"{U(finish, 40, 32)}/{U(finish, 82, 4)}/{U(finish, 86, 32)}/{U(finish, 72, 6)}/{U(finish, 150, 32)}");
        int perpetuation = c.GetMod(Mod.AVATAR_PERPETUATION);
        Check("the SMN healed, 6 MP spent (besides the perpetuation), the Ward recast started", "True/True/True",
            $"{c.Hp > c.MaxHp - 400}/{(500 - 6 - c.Mp) % perpetuation == 0}/{c.Recasts.HasRecast(RecastType.Ability, 174, 0)}");
        Tick(6);
        Check("the skill state over", true, pet.SkillState == null);

        Console.WriteLine("usePetAbility, getMinimumPetLevel");
        c.Hp = c.MaxHp - 400;
        Lua("player:getPet():usePetAbility(512, player)");
        Check("usePetAbility(Healing Ruby, the master): readied at once, on the master", "True/True",
            $"{pet.SkillState?.Skill.Id == 512}/{pet.SkillState?.Target == c}");
        Tick(4);
        Check("the master healed", true, c.Hp > c.MaxHp - 400);
        Tick(6);
        Lua("player:getPet():usePetAbility(9999, player)");
        Check("usePetAbility with no such pet skill: nothing", true, pet.SkillState == null);
        var minLevel = Sql(db, "SELECT minLevel FROM pet_list WHERE petid = ?", BoundValue.Of(pet.PetId));
        Check("getMinimumPetLevel: pet_list's minLevel", minLevel != null && minLevel.Next() ? minLevel.Get<int>("minLevel").ToString() : "none",
            Lua("report(player:getPet():getMinimumPetLevel())"));

        // the body of the kill above may still be fading: SpawnMob leaves a mob that is spawned alone, as in C++
        for (int i = 0; i < 60 && rabbit.IsSpawned; i++) Tick();
        Lua($"SpawnMob({Rabbit})");
        rabbit.OwnerId = 0;
        rabbit.Enmity.Clear();
        rabbit.Unkillable = true;
        rabbit.SetModifier(Mod.EVA, -2000);
        rabbit.X = pet.X + 1.5f; rabbit.Y = pet.Y; rabbit.Z = pet.Z;
        Ja(528, rabbit.TargId, rabbit.Id);
        Tick();
        Check("Moonlit Charge (Fenrir's) refused: unable to use, no skill", "True/True",
            $"{Messages().Contains(88)}/{pet.SkillState == null}");

        // the move's hit roll (mobPhysicalMove's math.randomInt against the capped hit rate) can miss one time in twenty
        // even at EVA -2000, leaving the rabbit claimed and hating but unhurt: this one move made a sure first hit
        // (mobPhysicalMove's own guaranteedFirstHit), the rest of it as scripted
        Lua("if not verifyPhysicalMove then verifyPhysicalMove = xi.mobskills.mobPhysicalMove " +
            "xi.mobskills.mobPhysicalMove = function(mob, target, skill, action, params) " +
            "if verifySureHit then verifySureHit = false params.guaranteedFirstHit = true end " +
            "return verifyPhysicalMove(mob, target, skill, action, params) end end verifySureHit = true");
        Ja(513, rabbit.TargId, rabbit.Id);
        Tick();
        Check("Poison Nails: readied on the rabbit", true, pet.SkillState?.Skill.Id == 513 && pet.SkillState.Target == rabbit);
        Tick(4);
        Check("the rabbit hurt, claimed for the master, hating the pet and its master", "True/True/True/True",
            $"{rabbit.Hp < rabbit.MaxHp}/{rabbit.OwnerId == c.Id}/{rabbit.Enmity.HasId(pet.Id)}/{rabbit.Enmity.HasId(c.Id)}");
        Lua("verifySureHit = false");
        Check("the Rage recast started", true, c.Recasts.HasRecast(RecastType.Ability, 173, 0));
        Tick(6);
        Check("Carbuncle went back at the rabbit", "True/True", $"{pet.Engaged}/{pet.BattleTarget == rabbit}");

        Lua("player:resetRecast(xi.recast.ABILITY, 173)");
        pet.Engaged = false;
        pet.BattleTarget = null;
        rabbit.X = pet.X + 12; rabbit.Z = pet.Z;
        c.X = pet.X + 6; c.Z = pet.Z;
        Ja(513, rabbit.TargId, rabbit.Id);
        Tick();
        Check("out of Carbuncle's reach: target out of range, no skill", "True/True",
            $"{Messages().Contains(4)}/{pet.SkillState == null}");
        Check("no Lua errors", "", string.Join(" | ", LuaErrors()));
        rabbit.Unkillable = false;
        Lua("player:despawnPet()");
        Tick(16);
        Check("the pet commands blanked with it gone", true, c.PetCommands.All(b => b == 0));

        Console.WriteLine("Spirits: the Light Spirit buffs out of combat, the Fire Spirit casts at its foe");
        // a pet's magic finish (category 4) from the pet
        bool PetCastFinished(PetEntity caster) => Sent(0x028).Any(p => U(p, 40, 32) == caster.Id && U(p, 82, 4) == 4);
        c.MJob = JobSmn;
        c.Mp = c.MaxMp;
        Lua("player:spawnPet(xi.petId.LIGHT_SPIRIT)");
        pet = c.PPet as PetEntity;
        Check("the Light Spirit out, with buffs to cast", "True/True", $"{pet?.PetId == 6}/{pet?.SpellContainer.HasBuffSpells()}");
        // xi.pets.avatar.onMobSpawn holds a spirit's casting behind a pet:timer of 30-90 s (the summoning skill)
        Check("casting held by the spawn script's timer at first", false, pet.MagicCastingEnabled);
        s.PacketList.Clear();
        bool buffed = false;
        for (int i = 0; i < 260 && !buffed; i++)
        {
            Tick();
            buffed = PetCastFinished(pet);
        }
        Check("idle, it cast a buff (CPetController::DoBuffTick)", true, buffed);
        Lua("player:despawnPet()");
        Tick(16);

        for (int i = 0; i < 100 && rabbit.IsSpawned && (!rabbit.IsAlive || rabbit.FadeAtMs != 0); i++) Tick(); // down or fading out
        Lua($"SpawnMob({Rabbit})");
        rabbit.OwnerId = 0;
        rabbit.Enmity.Clear();
        rabbit.Unkillable = true;
        Lua("player:spawnPet(xi.petId.FIRE_SPIRIT)");
        pet = c.PPet as PetEntity;
        rabbit.X = pet.X + 6; rabbit.Y = pet.Y; rabbit.Z = pet.Z;
        // an idle mob far from home with no path back despawns: held up until the pet reaches it
        rabbit.SetMobMod(MobMod.NoDespawn, 1);
        Lua($"player:petAttack(GetMobByID({Rabbit}))");
        Check("the Fire Spirit engaged on the rabbit", true, pet.Engaged && pet.BattleTarget == rabbit);
        s.PacketList.Clear();
        bool cast = false;
        for (int i = 0; i < 260 && !cast; i++)
        {
            Tick();
            cast = PetCastFinished(pet);
        }
        Check("in combat, it cast a spell at its foe", true, cast);
        var spellAt = Sent(0x028).FirstOrDefault(p => U(p, 40, 32) == pet.Id && U(p, 82, 4) == 4);
        Check("the spell landed on the rabbit, claimed for the master", $"{rabbit.Id}/True",
            spellAt == null ? "none" : $"{U(spellAt, 150, 32)}/{rabbit.OwnerId == c.Id}");
        Check("no Lua errors (spirits)", "", string.Join(" | ", LuaErrors()));
        Lua("player:despawnPet()");
        Tick(16);
        rabbit.Unkillable = false;
        rabbit.SetMobMod(MobMod.NoDespawn, 0);
        rabbit.Hp = rabbit.MaxHp;

        Console.WriteLine("BST commands: Call Beast, Fight, a Ready move, Heel, Stay, Reward, Leave");
        c.MJob = JobBst;
        c.SJob = 0;
        c.X = -200; c.Z = -200;
        Lua("player:recalculateAbilitiesTable()");
        // a jug pet without HP mods dies at once, as in C++ (TickPets)
        Lua("player:addPetMod(xi.mod.HP, 1000)");
        Ja(85, c.TargId, c.Id);
        Tick();
        Check("Call Beast without a jug: refused (NO_JUG_PET_ITEM)", "True/True", $"{c.PPet == null}/{Messages().Contains(337)}");
        s.PacketList.Clear();
        Send(0x050, 8, b => { b[4] = 1; b[5] = CharStats.SlotAmmo; b[6] = 0; });
        Check("the Carrot Broth on", CarrotBroth, c.Equipped[CharStats.SlotAmmo]?.ItemId ?? 0);
        Ja(85, c.TargId, c.Id);
        Tick();
        pet = c.PPet as PetEntity;
        Check("Call Beast: the Hare Familiar out, a jug used up", "22/1/True",
            $"{pet?.PetId}/{c.FindItem(Container.Inventory, 1)?.Quantity ?? 0}/{Lua("report(tostring(player:hasJugPet()))") == "true"}");
        Lua("player:resetRecast(xi.recast.ABILITY, 104)");
        Tick(5);
        Ja(85, c.TargId, c.Id);
        Tick();
        Check("Call Beast with a pet out: refused (ALREADY_HAS_A_PET)", "True/True", $"{c.PPet == pet}/{Messages().Contains(315)}");
        Lua("player:despawnPet()");
        Tick(16);
        Lua("player:spawnPet(xi.petId.SHEEP_FAMILIAR)");
        pet = c.PPet as PetEntity;
        Check("hasJugPet", "true", Lua("report(tostring(player:hasJugPet()))"));
        Check("with a jug pet: Ready, Fight, Heel, Stay, Leave learned; not Sic (a charmed pet's)", "True/True/True/True/True/False",
            $"{Learned(251)}/{Learned(69)}/{Learned(70)}/{Learned(73)}/{Learned(71)}/{Learned(72)}");
        Check("the sheep's moves in the pet commands (Lamb Chop .. Sheep Song)", "True/True/True/True",
            $"{PetBit(689 - 512)}/{PetBit(690 - 512)}/{PetBit(691 - 512)}/{PetBit(692 - 512)}");

        Console.WriteLine("Status effects on a pet: its ticks, the wear-off to its origin, REGAIN while idle, no charm, a charmed master");
        log.Clear();
        pet.Hp = pet.MaxHp / 2;
        int regenHp = pet.Hp;
        Lua("player:getPet():addStatusEffect(xi.effect.REGEN, { power = 10, duration = 60, tick = 3, origin = player })");
        Tick(8);
        Check("REGEN on the sheep: its effect script's mod, healing on the petTick regen", "10/True",
            $"{pet.GetMod(Mod.REGEN)}/{pet.Hp >= regenHp + 10}");
        s.PacketList.Clear();
        Lua("player:getPet():delStatusEffect(xi.effect.REGEN)");
        var petWorn = Sent(0x029).FirstOrDefault(p => BitConverter.ToUInt32(p.Buffer, 8) == pet.Id);
        Check("its wear-off to the REGEN's origin (the master has the sheep spawned): 0x029 206 on the sheep", $"{c.Id}/42/206/0",
            petWorn == null ? "none" : $"{BitConverter.ToUInt32(petWorn.Buffer, 4)}/{BitConverter.ToUInt32(petWorn.Buffer, 12)}/" +
                                      $"{BitConverter.ToUInt16(petWorn.Buffer, 24)}/{pet.GetMod(Mod.REGEN)}");
        Lua("player:getPet():addMod(xi.mod.REGAIN, 50)");
        int petTp = pet.Tp;
        // TickRegen: a mob (TYPE_MOB) gains its REGAIN only while engaged
        var idleMob = info.Mobs.First(m => m.IsSpawned && m.IsAlive && !m.Engaged && m.Id != Rabbit);
        idleMob.AddModifier(Mod.REGAIN, 50);
        int idleMobTp = idleMob.Tp;
        Tick(8);
        Check("REGAIN on the idle sheep (a pet: no engagement needed)", "False/True", $"{pet.Engaged}/{pet.Tp > petTp}");
        Check("REGAIN on an idle mob: no TP (a mob only while engaged)", "False/True", $"{idleMob.Engaged}/{idleMob.Tp == idleMobTp}");
        idleMob.DelModifier(Mod.REGAIN, 50);
        Lua("player:getPet():delMod(xi.mod.REGAIN, 50)");
        Check("CHARM_I refused on a pet (it has a master)", "false",
            Lua("report(tostring(player:getPet():addStatusEffect(xi.effect.CHARM_I, { duration = 30, origin = player })))"));
        // HandleEffectGainSideEffects: a char gaining CHARM_I lets its pet go
        Lua("player:addStatusEffect(xi.effect.CHARM_I, { duration = 30, origin = player })");
        Check("CHARM_I on the master: its sheep despawned", "True/True", $"{c.StatusEffects.HasStatusEffect(EffectId.CharmI)}/{c.PPet == null}");
        Lua("player:delStatusEffectSilent(xi.effect.CHARM_I)");
        Check("no Lua errors (pet effects)", "", string.Join(" | ", LuaErrors()));
        Tick(16);
        Lua("player:spawnPet(xi.petId.SHEEP_FAMILIAR)");
        pet = c.PPet as PetEntity;
        Check("the sheep back", true, pet != null);

        // the rabbit Carbuncle beat down earlier: gone first, then up again
        for (int i = 0; i < 100 && rabbit.IsSpawned && (!rabbit.IsAlive || rabbit.FadeAtMs != 0); i++) Tick(); // down or fading out
        Lua($"SpawnMob({Rabbit})");
        rabbit.OwnerId = 0;
        rabbit.Enmity.Clear();
        rabbit.Unkillable = true;
        rabbit.SetModifier(Mod.EVA, -2000);
        rabbit.X = pet.X + 1.5f; rabbit.Y = pet.Y; rabbit.Z = pet.Z;
        // the 0x01A case: a Ready move while the pet is not engaged is refused before the ability starts
        c.X = pet.X - 1; c.Z = pet.Z;
        Ja(691, c.TargId, c.Id);
        Check("Sheep Charge with the sheep not engaged: refused (88), no ability state", "True/True",
            $"{Messages().Contains(88)}/{!c.InAbility}");
        Tick();
        Check("nothing readied", true, pet.SkillState == null);
        // levelRestriction recalculates the pet: the sheep under the cap, its HP kept within the new maximum
        int sheepLvl = pet.MLvl;
        // (the jobs here are set by hand: the SMN's HP / MP the later scenes rely on are put back after)
        var (maxHp, maxMp, baseHp, baseMp) = (c.MaxHp, c.MaxMp, c.BaseMaxHp, c.BaseMaxMp);
        Lua("player:levelRestriction(20)");
        Check("levelRestriction(20): the sheep (was 33-35) at 18-20, still out, its HP within its maximum", "True/True/True/True",
            $"{sheepLvl > 30}/{pet.MLvl >= 18 && pet.MLvl <= 20}/{c.PPet == pet}/{pet.Hp > 0 && pet.Hp <= pet.MaxHp}");
        Lua("player:levelRestriction(0)");
        Check("the restriction lifted: the BST back at 75, the sheep still out", "75/True", $"{c.MLvl}/{c.PPet == pet}");
        (c.MaxHp, c.MaxMp, c.BaseMaxHp, c.BaseMaxMp) = (maxHp, maxMp, baseHp, baseMp);
        Ja(69, rabbit.TargId, rabbit.Id);
        Tick();
        Check("Fight: the sheep engaged on the rabbit", "True/True", $"{pet.Engaged}/{pet.BattleTarget == rabbit}");

        c.X = pet.X - 1; c.Z = pet.Z;
        // (the sheep's swings may have left the unkillable rabbit at 1 HP)
        rabbit.Hp = rabbit.MaxHp;
        int rabbitHp = rabbit.Hp;
        Ja(691, c.TargId, c.Id);
        Tick();
        Check("Sheep Charge (a Ready move, TARGET_PET): readied on the sheep's foe", "True/True",
            $"{pet.SkillState?.Skill.Id == 691}/{pet.SkillState?.Target == rabbit}");
        Tick(3);
        var charge = Sent(0x028).FirstOrDefault(p => U(p, 40, 32) == pet.Id && U(p, 82, 4) == 11);
        Check("the finish: category 11 under the mob skill 3859, on the rabbit", $"{pet.Id}/11/3859/{rabbit.Id}",
            charge == null ? "none" : $"{U(charge, 40, 32)}/{U(charge, 82, 4)}/{U(charge, 86, 32)}/{U(charge, 150, 32)}");
        Check("the rabbit took it", true, rabbit.Hp < rabbitHp);
        Tick(4);

        Ja(70, c.TargId, c.Id);
        Tick();
        Check("Heel: the sheep let go", false, pet.Engaged);
        Tick(5);
        Lua("player:resetRecast(xi.recast.ABILITY, 101)");
        Ja(73, c.TargId, c.Id);
        Tick();
        Check("Stay: the sheep rests", "true", Lua("report(tostring(pet:hasStatusEffect(xi.effect.HEALING)))"));
        Tick(5);
        Ja(78, c.TargId, c.Id);
        Tick();
        Check("Reward (TARGET_PET) without pet food: refused, must have food", true, Messages().Contains(347));
        Tick(5);
        Lua("player:resetRecast(xi.recast.ABILITY, 101)");
        Ja(71, c.TargId, c.Id);
        Tick();
        Check("Leave: the sheep goes", true, c.PPet == null);
        Check("no Lua errors (BST)", "", string.Join(" | ", LuaErrors()));
        Tick(16);
        rabbit.Unkillable = false;
        Lua("player:delPetMod(xi.mod.HP, 1000)");
        c.MJob = JobSmn;

        Console.WriteLine("Geomancy: Indi-Regen's aura on its caster, Geo-Regen's luopan at the offset with its aura");
        // CastMagic (0x01A, ActionID 3) at targid, with the packet's x / z / y offset
        void Cast(ushort spell, ushort targid, float posX = 0, float posZ = 0, float posY = 0)
        {
            s.PacketList.Clear();
            Send(0x01A, 28, b =>
            {
                BitConverter.GetBytes(CharId).CopyTo(b, 4);
                BitConverter.GetBytes(targid).CopyTo(b, C2sAction.OffActIndex);
                BitConverter.GetBytes((ushort)ActionId.CastMagic).CopyTo(b, C2sAction.OffActionID);
                C2sAction.SetSpellId(b, spell);
                C2sAction.SetPosX(b, posX);
                C2sAction.SetPosZ(b, posZ);
                C2sAction.SetPosY(b, posY);
            });
        }
        string Has(string who, string effect) => Lua($"report(tostring({who}:hasStatusEffect(xi.effect.{effect})))");
        c.MJob = JobGeo;
        c.SJob = 0;
        c.Mp = c.MaxMp;
        Cast(IndiRegen, c.TargId);
        Tick(8);
        Check("Indi-Regen: COLURE_ACTIVE on the GEO, an aura for allies of GEO_REGEN", "true/True/0",
            $"{Has("player", "COLURE_ACTIVE")}/{c.StatusEffects.GetStatusEffect(612)?.HasFlag(EffectFlag.Aura)}/" +
            $"{c.StatusEffects.GetStatusEffect(612)?.Tier}");
        // the aura works on its effect ticks (every 3 s)
        Tick(16);
        var geoRegen = c.StatusEffects.GetStatusEffect(539);
        Check("its tick put GEO_REGEN on the GEO: 4 s, refreshed by the aura, silent", "true/4000/True/True",
            $"{Has("player", "GEO_REGEN")}/{geoRegen?.DurationMs}/{geoRegen?.HasFlag(EffectFlag.AlwaysExpiring)}/{geoRegen?.HasFlag(EffectFlag.NoLossMessage)}");
        Check("its power is the aura's potency", true, geoRegen != null && geoRegen.Power == c.StatusEffects.GetStatusEffect(612)?.SubPower);
        Tick(25);
        Check("still on 10 s later (the aura keeps refreshing it)", "true", Has("player", "GEO_REGEN"));
        Lua("player:delStatusEffect(xi.effect.COLURE_ACTIVE)");
        Tick(15);
        Check("the aura gone: GEO_REGEN wears off within 4 s", "false", Has("player", "GEO_REGEN"));

        // an aura for enemies: the mobs with the GEO on their enmity list (its notoriety), in range
        for (int i = 0; i < 100 && rabbit.IsSpawned && (!rabbit.IsAlive || rabbit.FadeAtMs != 0); i++) Tick(); // down or fading out
        Lua($"SpawnMob({Rabbit})");
        rabbit.Enmity.Clear();
        rabbit.X = c.X + 2; rabbit.Y = c.Y; rabbit.Z = c.Z;
        // kept in the aura's range: a rabbit up since an earlier respawn is on a roam, and one held this far from its
        // spawn point despawns at its next idle action
        rabbit.PathFind?.Clear();
        Lua($"GetMobByID({Rabbit}):setMobMod(xi.mobMod.NO_MOVE, 1) GetMobByID({Rabbit}):setMobMod(xi.mobMod.NO_DESPAWN, 1)");
        Lua("player:addStatusEffect(xi.effect.COLURE_ACTIVE, { duration = 0, origin = player, tick = 3, subType = xi.effect.GEO_POISON, " +
            "subPower = 5, tier = xi.auraTarget.ENEMIES, flag = xi.effectFlag.AURA })");
        Tick(16);
        Check("an enemies' aura: not on a mob without enmity toward the GEO", "false", Lua($"report(tostring(GetMobByID({Rabbit}):hasStatusEffect(xi.effect.GEO_POISON)))"));
        rabbit.Enmity.UpdateEnmity(c, 0, 1);
        Tick(16);
        Check("on a mob with it on its enmity list, at its power", "true/5",
            Lua($"report(tostring(GetMobByID({Rabbit}):hasStatusEffect(xi.effect.GEO_POISON)))") + $"/{rabbit.StatusEffects.GetStatusEffect(540)?.Power}");
        Lua("player:delStatusEffect(xi.effect.COLURE_ACTIVE)");
        rabbit.Enmity.Clear();
        Lua($"GetMobByID({Rabbit}):setMobMod(xi.mobMod.NO_MOVE, 0) GetMobByID({Rabbit}):setMobMod(xi.mobMod.NO_DESPAWN, 0)");
        Lua($"DespawnMob({Rabbit})");
        Tick(15);

        Lua("player:resetRecast(xi.recast.MAGIC, 798)");
        c.Mp = c.MaxMp;
        float gx = c.X, gy = c.Y, gz = c.Z;
        Cast(GeoRegen, c.TargId, 3.0f, 0, 0);
        Tick(8);
        pet = c.PPet as PetEntity;
        Check("Geo-Regen: a luopan out", "75/Luopan", $"{pet?.PetId}/{pet?.PetType}");
        Check("at the target plus the packet's x offset", "3.0/0.0/0.0",
            pet == null ? "none" : $"{pet.X - gx:0.0}/{pet.Y - gy:0.0}/{pet.Z - gz:0.0}");
        Check("its stats: the GEO's level, 250 HP per 15 levels (halved damage taken), no speed, flags 0x8B", $"{c.MLvl}/{250 * c.MLvl / 15}/0/139/-5000",
            pet == null ? "none" : $"{pet.MLvl}/{pet.MaxHp}/{pet.Speed}/{pet.EntityFlags}/{pet.GetMod(Mod.DMG)}");
        Check("its aura for allies", "true", Has("pet", "COLURE_ACTIVE"));
        int luopanHp = pet?.Hp ?? 0;
        Tick(15);
        Check("the GEO, 3 yalms off, gets GEO_REGEN from it", "true", Has("player", "GEO_REGEN"));
        Check("the luopan's HP runs down (REGEN_DOWN)", true, pet != null && pet.Hp < luopanHp);
        Check("it stays put", "3.0", pet == null ? "none" : $"{pet.X - gx:0.0}");
        c.X = gx + 30;
        Tick(15);
        Check("out of its range (30 yalms off), GEO_REGEN wears off", "false", Has("player", "GEO_REGEN"));
        c.X = gx;
        Lua("player:resetRecast(xi.recast.MAGIC, 798)");
        Cast(GeoRegen, c.TargId);
        Tick(8);
        Check("another Geo- spell with the luopan out: refused (LUOPAN_ALREADY_PLACED)", "True/True",
            $"{c.PPet == pet}/{Sent(0x029).Any(p => BitConverter.ToUInt16(p.Buffer, 0x18) == 661)}");
        Check("no Lua errors (geomancy)", "", string.Join(" | ", LuaErrors()));
        Lua("player:despawnPet()");
        Tick(16);
        c.MJob = JobSmn;

        Console.WriteLine("Charm: a rabbit as the BST's pet");
        c.MJob = JobBst;
        c.SJob = 0;
        Lua("player:recalculateAbilitiesTable()");
        // a fresh rabbit by the BST, and another one to fight
        void FreshRabbit(MobEntity m, float x)
        {
            for (int i = 0; i < 100 && m.IsSpawned && !m.IsAlive; i++) Tick();
            if (m.IsSpawned) Lua($"DespawnMob({m.Id})");
            for (int i = 0; i < 100 && m.IsSpawned; i++) Tick();
            Lua($"SpawnMob({m.Id})");
            m.Enmity.Clear();
            m.OwnerId = 0;
            m.X = c.X + x; m.Y = c.Y; m.Z = c.Z;
            // moved by hand: no path left from before, and home is here, not its roam region (far from home, idle,
            // it would despawn)
            m.PathFind?.Clear();
            (m.SpawnX, m.SpawnY, m.SpawnZ) = (m.X, m.Y, m.Z);
            m.RoamRegion = null;
        }
        var foe = info.Mobs.First(m => m.Name == rabbit.Name && m.Id != rabbit.Id);
        FreshRabbit(rabbit, 2);
        FreshRabbit(foe, 6);
        Lua($"player:charm(GetMobByID({Rabbit}), 60)");
        Check("charm: the rabbit is the BST's pet, charmed, its master the BST", "True/true/True/1",
            $"{c.PPet == rabbit}/{Lua($"report(tostring(GetMobByID({Rabbit}):isCharmed()))")}/{rabbit.PMaster == c}/{rabbit.Allegiance}");
        Check("unclaimed, no enmity, no TP moves of its own (CPetController)", "0/False/False",
            $"{rabbit.OwnerId}/{rabbit.Enmity.HasId(c.Id)}/{rabbit.MobAbilityEnabled}");
        Check("Sic (a charmed pet's) learned, Ready (a jug pet's) not", "True/False", $"{Learned(72)}/{Learned(251)}");
        Check("getPet is the rabbit", $"{Rabbit}", Lua("report(player:getPet():getID())"));
        long charmUntil = rabbit.CharmTimeMs;
        Lua($"GetMobByID({Rabbit}):extendCharm(10, 20)");
        Check("extendCharm: 10-20 s more", true, rabbit.CharmTimeMs - charmUntil >= 10000 && rabbit.CharmTimeMs - charmUntil < 20000);

        c.X += 12;
        Tick(10);
        float fx = rabbit.X - c.X, fz = rabbit.Z - c.Z;
        Check("it follows its master", true, MathF.Sqrt(fx * fx + fz * fz) < 4);
        c.X -= 12;
        Tick(10);

        foe.X = rabbit.X + 1.5f; foe.Y = rabbit.Y; foe.Z = rabbit.Z;
        foe.SetModifier(Mod.EVA, -2000);
        foe.SetModifier(Mod.ACC, 1000);
        foe.Unkillable = true;
        rabbit.SetModifier(Mod.EVA, -2000);
        rabbit.Unkillable = true;
        int foeHp = foe.Hp, petHp = rabbit.Hp;
        Lua($"player:petAttack(GetMobByID({foe.Id}))");
        Tick(40);
        Check("petAttack: it fights the other rabbit, claimed for its master", "True/True/True",
            $"{rabbit.Engaged && rabbit.BattleTarget == foe}/{foe.Hp < foeHp}/{foe.OwnerId == c.Id}");
        Check("the other rabbit fights back at it", "True/True", $"{foe.BattleTarget == rabbit}/{rabbit.Hp < petHp}");
        Check("a charmed mob keeps a list of its own: the other rabbit's hits put it there", "True/True",
            $"{rabbit.Enmity.HasId(foe.Id)}/{rabbit.Enmity.CE(foe) > 0}");
        // CMagicState::ApplyEnmity: a charmed mob's spell gives its foe enmity (any other mob's gives none)
        foe.Enmity.Clear();
        rabbit.AutoAttackEnabled = false;
        (rabbit.MaxMp, rabbit.Mp) = (100, 100); // a rabbit has none
        // the other rabbit keeps hitting it (ACC 1000 on EVA -2000), and each hit may interrupt a mob's cast
        // (TryInterruptSpell: 5% plus the level gap), which then gives no enmity:
        // SPELLINTERRUPT 100 keeps the cast whole (the roll is still drawn, so the RNG sequence after it is unchanged)
        rabbit.SetModifier(Mod.SPELLINTERRUPT, 100);
        Lua($"GetMobByID({Rabbit}):castSpell(58, GetMobByID({foe.Id}))"); // Paralyze
        for (int t = 0; t < 30 && rabbit.MagicState == null; t++) Tick();
        for (int t = 0; t < 30 && rabbit.MagicState != null; t++) Tick();
        Check("its Paralyze: the other rabbit hates it", "True/True", $"{foe.Enmity.HasId(rabbit.Id)}/{foe.Enmity.CE(rabbit) + foe.Enmity.VE(rabbit) > 0}");
        rabbit.SetModifier(Mod.SPELLINTERRUPT, 0);
        rabbit.AutoAttackEnabled = true;
        (rabbit.MaxMp, rabbit.Mp) = (0, 0);

        Console.WriteLine("Charm: running out, Leave, death");
        rabbit.CharmTimeMs = now - 1;
        Tick();
        Check("the charm runs out: free of its master, a mob again", "True/False/0/True/True",
            $"{c.PPet == null}/{rabbit.IsCharmed}/{rabbit.Allegiance}/{rabbit.PMaster == null}/{rabbit.MobAbilityEnabled}");
        Check("it turns on its master; the other rabbit forgets it", "True/True/False",
            $"{rabbit.Enmity.HasId(c.Id)}/{rabbit.BattleTarget == c}/{foe.Enmity.HasId(rabbit.Id)}");
        // the ability table is rebuilt while the pet is still the master's (as in C++): Sic stays until the next rebuild
        Check("Sic still in the table, as in C++", true, Learned(72));
        rabbit.Unkillable = false;
        foe.Unkillable = false;
        foe.SetModifier(Mod.ACC, 0);

        FreshRabbit(rabbit, 2);
        Lua($"player:charm(GetMobByID({Rabbit}), 60)");
        Tick(5);
        Lua("player:resetRecast(xi.recast.ABILITY, 101)");
        Ja(AbilityLeave, c.TargId, c.Id);
        Tick();
        Check("Leave: let go, it forgets everything", "True/False/False/True",
            $"{c.PPet == null}/{rabbit.IsCharmed}/{rabbit.Enmity.HasId(c.Id)}/{rabbit.BattleTarget == null}");

        // levelRestriction under a charmed mob's level lets it go
        FreshRabbit(rabbit, 2);
        Lua($"player:charm(GetMobByID({Rabbit}), 60)");
        rabbit.MLvl = 10;
        Tick();
        bool charmedFirst = c.PPet == rabbit;
        (maxHp, maxMp, baseHp, baseMp) = (c.MaxHp, c.MaxMp, c.BaseMaxHp, c.BaseMaxMp);
        Lua("player:levelRestriction(5)");
        Check("levelRestriction(5) under the charmed rabbit's 10: detached", "True/True/False", $"{charmedFirst}/{c.PPet == null}/{rabbit.IsCharmed}");
        Lua("player:levelRestriction(0)");
        (c.MaxHp, c.MaxMp, c.BaseMaxHp, c.BaseMaxMp) = (maxHp, maxMp, baseHp, baseMp);
        Tick(5);

        // the Charm ability (xi.job_utils.beastmaster.attemptCharm: a roll of up to 95%, so a few tries)
        FreshRabbit(rabbit, 2);
        Check("the rabbit should be charmable (getCharmChance at least 75, Gauge's SHOULD_BE_ABLE_CHARM)", true,
            int.Parse(Lua($"report(xi.job_utils.beastmaster.getCharmChance(player, GetMobByID({Rabbit}), false))")) >= 75);
        for (int i = 0; i < 5 && c.PPet == null; i++)
        {
            Lua("player:resetRecast(xi.recast.ABILITY, 97)");
            Ja(52, rabbit.TargId, rabbit.Id);
            Tick(5);
        }
        Check("Charm: the rabbit charmed, for a while", "True/True/True", $"{c.PPet == rabbit}/{rabbit.IsCharmed}/{rabbit.CharmTimeMs > now}");
        rabbit.Hp = 0;
        Tick();
        Check("its death frees the master", "True/False", $"{c.PPet == null}/{rabbit.IsCharmed}");
        Check("no Lua errors (charm)", "", string.Join(" | ", LuaErrors()));
        for (int i = 0; i < 100 && rabbit.IsSpawned && (!rabbit.IsAlive || rabbit.FadeAtMs != 0); i++) Tick(); // down or fading out
        FreshRabbit(foe, 30);
        c.MJob = JobSmn;
        Lua("player:recalculateAbilitiesTable()");

        Console.WriteLine("A char charmed by a mob: it follows its charmer and fights for it");
        var s2 = new MapSession { CharId = AllyId, AccountId = AllyId };
        zone.OnLogin(s2);
        var ally = (CharData)s2.Char;
        void Send2(ushort opcode, int size, Action<byte[]> fill = null)
        {
            var buf = new byte[size];
            buf[0] = (byte)opcode; buf[1] = (byte)(buf.Length / 4 << 1 | opcode >> 8);
            fill?.Invoke(buf);
            zone.Dispatch(s2, opcode, new BasicPacket(buf));
            zone.AfterParse(s2);
        }
        Send2(0x00A, 92, buf => BitConverter.GetBytes(AllyId).CopyTo(buf, 12));
        Send2(0x00C, 12);
        Pump();
        Check("the ally in West Ronfaure", "True/100", $"{ally != null}/{ally?.Zone}");
        c.X = -200; c.Y = 0; c.Z = -200;
        ally.X = c.X + 6; ally.Y = c.Y; ally.Z = c.Z;
        FreshRabbit(rabbit, 8);
        rabbit.Unkillable = true;
        Lua($"player:addStatusEffect(xi.effect.CHARM_I, {{ power = 0, duration = 60, origin = GetMobByID({Rabbit}) }})");
        Lua($"GetMobByID({Rabbit}):charm(player)");
        Check("charm: the Tamer is the rabbit's, on its side, inactive a while", "True/True/0/True/true",
            $"{c.IsCharmed}/{c.PMaster == rabbit}/{c.Allegiance}/{c.InactiveState != null}/{Lua("report(tostring(player:isCharmed()))")}");
        // 5 s inactive, then the walk
        Tick(30);
        float cx = c.X - rabbit.X, cz = c.Z - rabbit.Z;
        Check("it follows its charmer", true, MathF.Sqrt(cx * cx + cz * cz) < 3);
        rabbit.Enmity.UpdateEnmity(ally, 0, 100);
        Tick(3);
        Check("the rabbit goes for the ally; the Tamer engages it too", "True/True",
            $"{rabbit.BattleTarget == ally}/{c.CharmTarget == ally}");
        int allyHp = ally.Hp;
        Tick(30);
        float ax = c.X - ally.X, az = c.Z - ally.Z;
        Check("it closes in and hits the ally", "True/True", $"{MathF.Sqrt(ax * ax + az * az) < 4}/{ally.Hp < allyHp}");
        Lua($"DespawnMob({Rabbit})");
        for (int i = 0; i < 100 && rabbit.IsSpawned; i++) Tick();
        Tick(2);
        Check("the charmer gone: CHARM off, free again, a player's side", "false/False/True/1/True",
            $"{Lua("report(tostring(player:hasStatusEffect(xi.effect.CHARM_I)))")}/{c.IsCharmed}/{c.PMaster == null}/{c.Allegiance}/{c.CharmTarget == null}");
        rabbit.Unkillable = false;
        Check("no Lua errors (charmed char)", "", string.Join(" | ", LuaErrors()));
        zone.OnCharTimeout(s2, false);
        Tick(2);

        Console.WriteLine("Zoning: the avatar comes along with its HP and MP");
        Lua("player:spawnPet(xi.petId.CARBUNCLE)");
        pet = c.PPet as PetEntity;
        pet.Hp = pet.MaxHp - 100;
        pet.Mp = pet.MaxMp - 10;
        int zoneHp = pet.Hp, zoneMp = pet.Mp, zoneLevel = pet.SpawnLevel;
        Lua($"player:setPos({c.X}, {c.Y}, {c.Z}, 0, {WestRonfaure})");
        Check("asked to zone: the pet's info kept", $"True/8/{zoneHp}/{zoneMp}/{zoneLevel}",
            $"{c.PetZoning.RespawnPet}/{c.PetZoning.PetId}/{c.PetZoning.PetHp}/{c.PetZoning.PetMp}/{c.PetZoning.PetLevel}");
        Tick(2);
        Check("zoned out: the pet gone from the zone", "True/False", $"{c.PPet == null}/{info.Pets.Contains(pet)}");
        // SendToZone takes the pet's HP again (a regen tick may have come in between)
        int savedHp = c.PetZoning.PetHp;
        Check("the HP kept at the zone-out: the HP asked with, or a regen tick more", true, savedHp >= zoneHp && savedHp <= zoneHp + 10);
        var petRow = Sql(db, "SELECT pet_id, pet_type, pet_hp, pet_mp, pet_level FROM char_stats WHERE charid = ?", BoundValue.Of(CharId));
        Check("the char_stats pet columns saved", $"8/0/{savedHp}/{zoneMp}/{zoneLevel}",
            petRow != null && petRow.Next() ? $"{petRow.Get<int>("pet_id")}/{petRow.Get<int>("pet_type")}/{petRow.Get<int>("pet_hp")}/{petRow.Get<int>("pet_mp")}/{petRow.Get<int>("pet_level")}" : "none");

        s = new MapSession { CharId = CharId, AccountId = CharId };
        zone.OnLogin(s);
        c = (CharData)s.Char;
        Check("the new char loads the pet info", "True/8", $"{c.PetZoning.RespawnPet}/{c.PetZoning.PetId}");
        Send(0x00A, 92, buf => BitConverter.GetBytes(CharId).CopyTo(buf, 12));
        Send(0x00C, 12);
        Pump();
        pet = c.PPet as PetEntity;
        Check("game ok: Carbuncle back, with the HP and MP it left with", $"True/{savedHp}/{zoneMp}",
            $"{pet != null && info.Pets.Contains(pet)}/{pet?.Hp}/{pet?.Mp}");
        var zonedSpawn = Sent(0x00E).FirstOrDefault(p => pet != null && BitConverter.ToUInt32(p.Buffer, 4) == pet.Id);
        Check("no special spawn animation on zone in", "0", zonedSpawn == null ? "none" : $"{zonedSpawn.Buffer[0x28] & 0x04}");
        Check("the info reset once used", "False/0", $"{c.PetZoning.RespawnPet}/{c.PetZoning.PetHp}");
        c.PetZoning.RespawnPet = true;
        c.PetZoning.PetHp = 5;
        Lua("player:changeJob(xi.job.SMN)");
        Check("a job change drops the info", "False/0", $"{c.PetZoning.RespawnPet}/{c.PetZoning.PetHp}");
        Lua("player:despawnPet()");
        Tick(16);

        Console.WriteLine("level_sync.lua: LevelSync restricts the char, its loss lifts it (disableLevelSync, no party)");
        int levelBefore = c.MLvl;
        c.StatusEffects.AddStatusEffect(new StatusEffect(EffectId.LevelSync, EffectId.LevelSync, 1, 0, 0), EffectNotice.Silent);
        Check("LevelSync 1: the main level restricted", "1/1", $"{c.MLvl}/{c.LevelRestriction}");
        c.StatusEffects.DelStatusEffectSilent(EffectId.LevelSync);
        Check("LevelSync gone: the level back, the restriction lifted", $"{levelBefore}/0", $"{c.MLvl}/{c.LevelRestriction}");

        Console.WriteLine("The avatar bodyguard: an idle Carbuncle takes on the mob fighting its master");
        Lua($"SpawnMob({Rabbit})");
        Lua("player:spawnPet(xi.petId.CARBUNCLE)");
        pet = c.PPet as PetEntity;
        rabbit.X = c.X + 3; rabbit.Y = c.Y; rabbit.Z = c.Z;
        rabbit.Unkillable = true;
        rabbit.OwnerId = 1; // claimed by another
        rabbit.Enmity.UpdateEnmityFromDamage(c, 10);
        Tick(3);
        Check("the rabbit on the master, claimed by another: the avatar stays out", "True/False",
            $"{rabbit.Engaged && rabbit.BattleTarget == c}/{pet?.Engaged}");
        rabbit.OwnerId = 0;
        Tick(3);
        Check("the master may claim it: the avatar engages the rabbit", "True/True", $"{pet?.Engaged}/{pet?.BattleTarget == rabbit}");
        Lua("player:despawnPet()");
        Lua($"DespawnMob({Rabbit})");
        for (int i = 0; i < 100 && rabbit.IsSpawned; i++) Tick();
        rabbit.Unkillable = false;
        Tick(16);

        Console.WriteLine("The pet leaves with its master's zone-out");
        Lua("player:spawnPet(xi.petId.CARBUNCLE)");
        pet = c.PPet as PetEntity;
        Check("Carbuncle out", true, pet != null && info.Pets.Contains(pet));
        zone.OnCharTimeout(s, false);
        Tick();
        Check("gone with its master", "True/False", $"{c.PPet == null}/{info.Pets.Contains(pet)}");
    }
}
