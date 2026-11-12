// XI.Verify.Quests -- quest rework C3 N5: the three battlefields (Waughroon Shrine's On My Way, Horlais Peak's The
// Secret Weapon, Balgas Dais' Saintly Invitation) on C#: each one's fields against its Lua file, its groups, the
// allDeath (WON), no hook overridden.
using System.Linq;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    // a mission battlefield's checks: clean with its key and LegacyPath, the Lua file's fields, the groups (offsets
    // per area, every mob of the one IDs.lua name), no hook overridden, the first group's allDeath WON
    static void N5Battlefield(string label, BattlefieldScript s, string key, string path, string fields, string mob,
                              string groups)
    {
        var d = (BattlefieldMissionData)s.Data;
        Check($"{N5} {label}: validates clean, its key and LegacyPath",
              $"|{key}|{path}",
              $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key}|{s.LegacyPath}");
        Check($"{N5} {label}: the Lua file's fields (zone, id, no exp loss, mission, trusts, players, MAX_LEVEL, " +
              "level cap 0, 30 minutes, index 3, entry and exit NPCs, mission area and mission, status area, no " +
              "mission status, required status, no skip status, no title, no area, groups)",
              fields,
              $"{d.ZoneId} {d.BattlefieldId} {d.CanLoseExp} {d.IsMission} {d.AllowTrusts} {d.MaxPlayers} " +
              $"{d.LevelCapSetting} {d.LevelCap} {d.TimeLimit} {d.Index} {string.Join(",", d.EntryNpcs)} " +
              $"{string.Join(",", d.ExitNpcs)} {d.MissionArea} {d.Mission} {d.MissionStatusArea} " +
              $"{d.MissionStatus?.ToString() ?? "null"} {d.RequiredMissionStatus} " +
              $"{d.SkipMissionStatus?.ToString() ?? "null"} {d.Title} {d.Area} {d.Groups.Count}");
        Check($"{N5} {label}: the groups: each area's mob offsets, allDeath, death, spawned, superlink, superlink " +
              "group, party",
              groups,
              string.Join("|", d.Groups.Select(g =>
                  $"{string.Join(";", g.AreaMobIds.Select(a => string.Join(",", a.Select(x => x.Offset))))} " +
                  $"{g.AllDeath != null} {g.Death != null} {g.Spawned?.ToString() ?? "null"} {g.Superlink} " +
                  $"{g.SuperlinkGroup?.ToString() ?? "null"} {g.IsParty}")));
        Check($"{N5} {label}: every group's mobs are {mob} offsets, no Mobs or MobIds list",
              "True",
              d.Groups.All(g => g.Mobs == null && g.MobIds == null &&
                                g.AreaMobIds.All(a => a.All(x => x.Key == mob))).ToString());
        Check($"{N5} {label}: overrides no hook",
              "", string.Join(",", BattlefieldScript.HookNames.Where(s.Overrides)));
        var view = new BattlefieldViewFake { Area = 2 };
        d.Groups[0].AllDeath(new BattlefieldContextFake(s) { Battlefield = view }, null);
        Check($"{N5} {label}: the first group's allDeath: WON (status 2)",
              "Battlefield.SetStatus(2)", string.Join(",", view.Calls));
    }

    static void RunC3N5OnMyWayBattle()
    {
        N5Battlefield("on my way battle", new QC.Battlefields.WaughroonShrine.OnMyWay(), QC.BattlefieldKey.OnMyWay,
                      "scripts/battlefields/Waughroon_Shrine/on_my_way",
                      "144 67 False True True 6 MAX_LEVEL 0 1800 3 BC_Entrance Burning_Circle Bastok " +
                      "19 Bastok null 2 null 0 0 1",
                      QC.Mob.WaughroonShrine.KUJHU_GRANITESKIN,
                      "0,1,2,3;4,5,6,7;8,9,10,11 True False null False null False");
    }

    static void RunC3N5TheSecretWeaponBattle()
    {
        N5Battlefield("secret weapon battle", new QC.Battlefields.HorlaisPeak.TheSecretWeapon(),
                      QC.BattlefieldKey.TheSecretWeapon, "scripts/battlefields/Horlais_Peak/the_secret_weapon",
                      "139 3 False True True 6 MAX_LEVEL 0 1800 3 BC_Entrance Burning_Circle Sandoria " +
                      "19 Sandoria null 2 null 0 0 2",
                      QC.Mob.HorlaisPeak.DAROKBOK_OF_CLAN_REAPER,
                      "0,1,2,3,4;6,7,8,9,10;12,13,14,15,16 True False null True null False|" +
                      "5;11;17 False False False False null False");
    }

    static void RunC3N5SaintlyInvitationBattle()
    {
        N5Battlefield("saintly invitation battle", new QC.Battlefields.BalgasDais.SaintlyInvitation(),
                      QC.BattlefieldKey.SaintlyInvitation, "scripts/battlefields/Balgas_Dais/saintly_invitation",
                      "146 99 False True True 6 MAX_LEVEL 0 1800 3 BC_Entrance Burning_Circle Windurst " +
                      "17 Windurst null 1 null 0 0 2",
                      QC.Mob.BalgasDais.BUU_XOLO_THE_BLOODFACED,
                      "0,1,2,3;6,7,8,9;12,13,14,15 True False null False null False|" +
                      "4,5;10,11;16,17 False False False False null False");
    }
}
