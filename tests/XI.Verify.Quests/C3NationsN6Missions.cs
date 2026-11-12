// XI.Verify.Quests -- quest rework C3 N6: the twelve rank 8-9 missions on C#: each one's header, its section 1 and
// section 2 checks, its gate guards' finishes, and the arms the traces cannot show (kept Lua quirks among them).
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    // the header, section 1, section 2 and the guards of one mission
    static MissionScriptDefinition N6Basics(string label, MissionScript m, string key, string path, string reward,
                                            MissionLog log, int id, int none, int other, int opt,
                                            (ushort, int, ZoneText?)[] guards)
    {
        var d = m.Compile();
        N6Header(label, m, d, key, path, reward);
        N6Section1(label, d, log, id, none, other);
        N6Current(label, d, 1, log, id, none);
        N6Accept(label, d, log, id, none, opt, guards);
        return d;
    }

    static void RunC3N6Bastok()
    {
        const MissionLog B = MissionLog.Bastok;
        int none = QC.Mission.Bastok.None;

        // 8-1 The Chains That Bind Us
        {
            int id = QC.Mission.Bastok.TheChainsThatBindUs;
            var d = N6Basics("chains", new QC.Missions.Bastok.TheChainsThatBindUs(),
                             QC.MissionKey.Bastok.TheChainsThatBindUs,
                             "scripts/missions/bastok/8_1_The_Chains_That_Bind_Us", "//1133//", B, id, none,
                             QC.Mission.Bastok.OnMyWay, 20, N6BastokGuards);
            var h = N6Csid(d, 1, N6Fin, QC.Zone.Metalworks, 767);
            Check("c3 N6 chains: Metalworks 767's finish: option 0 sets status 1; option 1 nothing",
                  "Player.SetMissionStatus(1,1)|",
                  $"{N6Run(h, N6Fake(B, id, id, option: 0))}|{N6Run(h, N6Fake(B, id, id, option: 1))}");
        }

        // 8-2 Enter the Talekeeper
        {
            int id = QC.Mission.Bastok.EnterTheTalekeeper;
            N6Basics("talekeeper", new QC.Missions.Bastok.EnterTheTalekeeper(),
                     QC.MissionKey.Bastok.EnterTheTalekeeper, "scripts/missions/bastok/8_2_Enter_the_Talekeeper",
                     "80000/9///", B, id, none, QC.Mission.Bastok.OnMyWay, 21, N6BastokGuards);
        }

        // 9-1 The Salt of the Earth
        {
            int id = QC.Mission.Bastok.TheSaltOfTheEarth;
            N6Basics("salt", new QC.Missions.Bastok.TheSaltOfTheEarth(), QC.MissionKey.Bastok.TheSaltOfTheEarth,
                     "scripts/missions/bastok/9_1_The_Salt_of_the_Earth", "//1500//", B, id, none,
                     QC.Mission.Bastok.OnMyWay, 22, N6BastokGuards);
        }

        // 9-2 Where Two Paths Converge
        {
            int id = QC.Mission.Bastok.WhereTwoPathsConverge;
            N6Basics("where two paths", new QC.Missions.Bastok.WhereTwoPathsConverge(),
                     QC.MissionKey.Bastok.WhereTwoPathsConverge,
                     "scripts/missions/bastok/9_2_Where_Two_Paths_Converge",
                     $"100000/10//{QC.Title.HeroAmongHeroes}/", B, id, none, QC.Mission.Bastok.OnMyWay, 23,
                     N6BastokGuards);
        }
    }

    static void RunC3N6Sandoria()
    {
        const MissionLog S = MissionLog.Sandoria;
        int none = QC.Mission.Sandoria.None;

        {
            int id = QC.Mission.Sandoria.ComingOfAge;
            N6Basics("coming of age", new QC.Missions.Sandoria.ComingOfAge(), QC.MissionKey.Sandoria.ComingOfAge,
                     "scripts/missions/sandoria/8_1_Coming_of_Age", "//800//", S, id, none,
                     QC.Mission.Sandoria.TheSecretWeapon, 20, N6SandoriaGuards);
        }
        {
            int id = QC.Mission.Sandoria.Lightbringer;
            N6Basics("lightbringer", new QC.Missions.Sandoria.Lightbringer(), QC.MissionKey.Sandoria.Lightbringer,
                     "scripts/missions/sandoria/8_2_Lightbringer", "80000/9///", S, id, none,
                     QC.Mission.Sandoria.TheSecretWeapon, 21, N6SandoriaGuards);
        }
        {
            int id = QC.Mission.Sandoria.BreakingBarriers;
            N6Basics("breaking barriers", new QC.Missions.Sandoria.BreakingBarriers(),
                     QC.MissionKey.Sandoria.BreakingBarriers, "scripts/missions/sandoria/9_1_Breaking_Barriers",
                     "//900//", S, id, none, QC.Mission.Sandoria.TheSecretWeapon, 22, N6SandoriaGuards);
        }
        {
            int id = QC.Mission.Sandoria.TheHeirToTheLight;
            N6Basics("heir", new QC.Missions.Sandoria.TheHeirToTheLight(),
                     QC.MissionKey.Sandoria.TheHeirToTheLight, "scripts/missions/sandoria/9_2_The_Heir_to_the_Light",
                     $"100000/10//{QC.Title.SanDorianRoyalHeir}/", S, id, none,
                     QC.Mission.Sandoria.TheSecretWeapon, 23, N6SandoriaGuards);
        }
    }

    static void RunC3N6Windurst()
    {
        const MissionLog W = MissionLog.Windurst;
        int none = QC.Mission.Windurst.None;

        // 8-1 Vain
        {
            int id = QC.Mission.Windurst.Vain;
            var d = N6Basics("vain", new QC.Missions.Windurst.Vain(), QC.MissionKey.Windurst.Vain,
                             "scripts/missions/windurst/8_1_Vain", "//750//", W, id, none,
                             QC.Mission.Windurst.AwakeningOfTheGods, 20, N6WindurstGuards);
            // Valkurm Dunes' 5 update: z above 45 sends 1; the inner else (3) needs z <= -301 inside z > 45, never
            // (kept: the Lua's nesting); z 45 and below sends nothing
            var u = N6Csid(d, 1, N6Upd, QC.Zone.ValkurmDunes, 5);
            string Up(double z)
            {
                var f = N6Fake(W, id, id, status: 1);
                f.ZPos = z;
                return N6Run(u, f);
            }
            Check("c3 N6 vain: Valkurm Dunes' 5 update: z 46 and 1000 send 1 (the 3 arm unreachable); z 45, 0 and " +
                  "-400 nothing",
                  "Player.UpdateEvent(0,0,0,0,0,1)|Player.UpdateEvent(0,0,0,0,0,1)|||",
                  $"{Up(46)}|{Up(1000)}|{Up(45)}|{Up(0)}|{Up(-400)}");
        }

        // 8-2 The Jester Who'd be King
        {
            int id = QC.Mission.Windurst.TheJesterWhodBeKing;
            N6Basics("jester", new QC.Missions.Windurst.TheJesterWhodBeKing(),
                     QC.MissionKey.Windurst.TheJesterWhodBeKing,
                     "scripts/missions/windurst/8_2_The_Jester_Whod_be_King", "80000/9///", W, id, none,
                     QC.Mission.Windurst.AwakeningOfTheGods, 21, N6WindurstGuards);
        }

        // 9-1 Doll of the Dead
        {
            int id = QC.Mission.Windurst.DollOfTheDead;
            N6Basics("doll", new QC.Missions.Windurst.DollOfTheDead(), QC.MissionKey.Windurst.DollOfTheDead,
                     "scripts/missions/windurst/9_1_Doll_of_the_Dead", $"//800/{QC.Title.GuidingStar}/", W, id,
                     none, QC.Mission.Windurst.AwakeningOfTheGods, 22, N6WindurstGuards);
        }

        // 9-2 Moon Reading
        {
            int id = QC.Mission.Windurst.MoonReading;
            N6Basics("moon reading", new QC.Missions.Windurst.MoonReading(), QC.MissionKey.Windurst.MoonReading,
                     "scripts/missions/windurst/9_2_Moon_Reading",
                     $"100000/10//{QC.Title.VestalChamberlain}/{QC.Item.WindurstianFlag}", W, id, none,
                     QC.Mission.Windurst.AwakeningOfTheGods, 23, N6WindurstGuards);
        }
    }

    static void RunC3N6Battlefields()
    {
        foreach (var (s, label, key, path, zone, bfId, index, req) in new (BattlefieldScript, string, string, string,
                     int, int, int, int)[]
                 {
                     (new QC.Battlefields.ThroneRoom.WhereTwoPathsConverge(), "where two paths bf",
                      QC.BattlefieldKey.WhereTwoPathsConverge,
                      "scripts/battlefields/Throne_Room/where_two_paths_converge",
                      QC.Zone.ThroneRoom, 161, 1, 1),
                     (new QC.Battlefields.QuBiaArena.HeirToTheLight(), "heir bf", QC.BattlefieldKey.HeirToTheLight,
                      "scripts/battlefields/QuBia_Arena/heir_to_the_light", QC.Zone.QubiaArena, 516, 4, 3),
                     (new QC.Battlefields.FullMoonFountain.MoonReading(), "moon reading bf",
                      QC.BattlefieldKey.MoonReading, "scripts/battlefields/Full_Moon_Fountain/moon_reading",
                      QC.Zone.FullMoonFountain, 225, 1, 2),
                 })
        {
            var d = (BattlefieldMissionData)s.Data;
            Check($"c3 N6 {label}: validates clean, its key and LegacyPath; zone, id, index, required status, " +
                  "max players, time limit",
                  $"|{key}|{path}|{zone} {bfId} {index} {req} 6 1800",
                  $"{string.Join(" | ", BattlefieldScriptValidator.Validate(s))}|{s.Key}|{s.LegacyPath}|" +
                  $"{d.ZoneId} {d.BattlefieldId} {d.Index} {d.RequiredMissionStatus} {d.MaxPlayers} {d.TimeLimit}");
        }
    }
}
