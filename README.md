# SeaReefBoat

A Final Fantasy XI server emulator, forked from [LandSandBoat](https://github.com/LandSandBoat/server) and ported to C#, with a new C# quest engine. This repository holds everything the C# server needs to run.

The port is in progress. Quests, Records of Eminence, the nation, Rise of the Zilart, Chains of Promathia, Treasures of Aht Urhgan and Wings of the Goddess missions, and their battlefields, run on C#; the other missions, battlefields and content still run as LandSandBoat's Lua scripts through the C# Lua host.

| Path | What it is |
|---|---|
| `src/` | The C# servers (`XI.Host` runs every mode) and their libraries |
| `tests/`, `tools/`, `oracle/` | Verification suites, dev tools, golden values taken from the C++ server |
| `settings/` | `default/*.lua` plus local overrides `*.lua`, read the same way as the C++ server |
| `scripts/`, `modules/` | Lua content, run by the C# Lua host |
| `data/` | Zone, mob and NPC yaml, plus the stat and merit tables |
| `sql/`, `tools/dbtool.py`, `tools/migrations/` | Database schema, contents and the C++ project's database tool |
| `res/` | badnames.bin (the character-name filter) and friends |
| `navmeshes/`, `ximeshes/` | Nav and collision meshes |
| `libluajit_64.dll` | The LuaJIT build the C++ server ships, which the Lua host drives |
| `login.cert/key`, `profile.cert/key` | TLS keys (not in git; made on first start if missing) |

The Lua, SQL and data files come from LandSandBoat. The nav meshes come from LandSandBoat's xiNavmeshes and the collision meshes from InoUno's ximeshes.

Some tooling still reads the C++ tree on purpose, because it checks the port against the C++ code:
- the golden capture (`tests/XI.Verify.Golden/capture`, which builds and runs xi_test.exe),
- XI.Verify.Map's packet id check against the C++ enum headers.

## Running

```sh
dotnet publish src/XI.Host -c Release -o run/host     # run/ is ignored
run/host/XI.Host.exe world   db.port=3307
run/host/XI.Host.exe profile db.port=3307
run/host/XI.Host.exe search  db.port=3307
run/host/XI.Host.exe map     db.port=3307 ip=127.0.0.1 port=54230
run/host/XI.Host.exe login   db.port=3307
```

`XI.Host` finds its root by itself. It uses the working directory if that holds `settings/default/main.lua`, and otherwise the first folder above the exe that does. `root=<dir>` overrides this. Settings come from `settings/default/*.lua`, then `settings/*.lua`, then the `XI_<SECTION>_<KEY>` environment variables, then `key=value` flags.

To set up a new database, use `python tools/dbtool.py`, which needs the packages in `tools/requirements.txt`.

## Letting other machines connect

Every server listens on all interfaces. To let LAN, VPN or internet clients reach the map without xiloader
`--hairpin`, set `ZONE_IP_FROM_CLIENT = true` in `settings/network.lua` (run `python tools/dbtool.py` once so
`accounts_sessions.client_server_addr` exists). Each client is then sent the address it typed into its launcher for
every zone whose `zoneip` is 127.x; keep `zoneip` at 127.0.0.1.

To accept clients from the internet: forward TCP 54001, 54002, 54230, 54231, 51220 and 51240 and UDP 54230 (plus any
other map process port) to this PC and allow them through the firewall. Keep 54003 (IPC) and the database port closed,
and decide whether `login.ACCOUNT_CREATION` should stay on.

## License

SeaReefBoat is a modified version of LandSandBoat and is licensed under the GNU General Public
License v3.0, the same license as LandSandBoat. See `LICENSE`.

The C# code is a port of LandSandBoat's C++ server. The Lua scripts, SQL and data files come from
LandSandBoat and its contributors, with changes. Final Fantasy XI is a trademark of Square Enix;
this project is not affiliated with Square Enix.
