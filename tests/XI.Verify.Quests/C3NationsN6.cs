// XI.Verify.Quests -- quest rework C3 N6 (docs/superpowers/plans/2026-11-07-quest-rework-c3-lanes-1.md, section 4,
// Nations): the rank 8-9 missions (the 8-1s, 8-2s, 9-1s and 9-2s of the three nations) and their three battlefields
// (the Throne Room's Where Two Paths Converge, Qu'Bia Arena's The Heir to the Light, the Full Moon Fountain's Moon
// Reading) on C#, one fake check per arm no packet shows, as the Lua files had them: each section check on its
// operands, each reply on the reads it branches on (its kind printed), each finish on its option and the items it
// reads.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    internal static void RunC3N6()
    {
        Console.WriteLine("\n== c3 N6: the rank 8-9 missions and their battlefields on C# ==");
        RunC3N6Common();
        RunC3N6Bastok();
        RunC3N6Sandoria();
        RunC3N6Windurst();
        RunC3N6Battlefields();
        RunC3N6Replies();
        RunC3N6BattlefieldArms();
    }

    static readonly QuestHandlerKind N6Fin = QuestHandlerKind.OnEventFinish;
    static readonly QuestHandlerKind N6Trig = QuestHandlerKind.OnTrigger;
    static readonly QuestHandlerKind N6Trade = QuestHandlerKind.OnTrade;
    static readonly QuestHandlerKind N6Zone = QuestHandlerKind.OnZoneIn;
    static readonly QuestHandlerKind N6Upd = QuestHandlerKind.OnEventUpdate;
    static readonly QuestHandlerKind N6Death = QuestHandlerKind.OnMobDeath;

    // a fake on one nation mission: current, status, nation (the log's by default), option, rank (of that nation),
    // completed missions of the log
    static Fake N6Fake(MissionLog log, int id, int current, int status = 0, int nation = -1, int option = 0,
                       int rank = 8, params int[] done) =>
        N4Fake(log, id, current, status, nation, option, rank, done);

    static Fake N6Keys(Fake f, params int[] keyItems)
    {
        foreach (int k in keyItems) f.KeyItems[k] = true;
        return f;
    }

    // a handler's reply (its kind and operands) on the fake, or "threw: ..." for a mutant that reads a missing text
    static string N6Reply(QuestHandlerDecl h, Fake f) => C3NReply(h, f);

    // a handler run on the fake: its calls (a reply handler's calls made while it answers), or "threw: ..." for a
    // mutant that throws, so one mutant's exception fails its check and not the whole run
    static string N6Run(QuestHandlerDecl h, Fake f)
    {
        try
        {
            if (h.Run != null)
                return C2MRun(h, f);
            C2MReply(h, f);
            return string.Join(",", f.Calls);
        }
        catch (Exception e)
        {
            return "threw: " + e.Message;
        }
    }

    // an NPC's handler of a state
    static QuestHandlerDecl N6Npc(MissionScriptDefinition d, int state, QuestHandlerKind kind, ushort zone,
                                  string npc) => C2MHandler(d, state, kind, zone, entity: npc);

    // a zone-wide handler of a state (its csid; 1 for a zone-in)
    static QuestHandlerDecl N6Csid(MissionScriptDefinition d, int state, QuestHandlerKind kind, ushort zone,
                                   int csid = 1) => C2MHandler(d, state, kind, zone, id: csid);

    // the clean / key / LegacyPath / reward line of a mission
    static void N6Header(string label, MissionScript m, MissionScriptDefinition d, string key, string path,
                         string reward)
    {
        var r = d.Reward;
        Check($"c3 N6 {label}: clean, its key and LegacyPath, the reward (gil/rank/rank points/title/item)",
              $"|{key}|{path}|{reward}",
              $"{string.Join(" | ", QuestScriptValidator.Validate(d))}|{m.Key}|{m.LegacyPath}|{r.Gil}/{r.Rank}/" +
              $"{r.RankPoints}/{r.Title}/{r.Item}");
    }

    // section 1 of every N6 mission: no mission current and a char of the log's nation; no rank and no completed
    // test (kept: the Lua reads only the current mission and the nation)
    static void N6Section1(string label, MissionScriptDefinition d, MissionLog log, int id, int none, int other)
    {
        int l = (int)log, foreign = (l + 1) % 3, foreign2 = (l + 2) % 3;
        var s = d.States[0];
        Check($"c3 N6 {label}: section 1 holds with no mission current and a char of nation {l} (rank 1, 8, 10; " +
              $"completed too); fails for nations {foreign} and {foreign2} and with a mission current (this one, " +
              "another)",
              "True True True True False False False False",
              $"{s.IsMissionMatch(N6Fake(log, id, none, rank: 1))} {s.IsMissionMatch(N6Fake(log, id, none))} " +
              $"{s.IsMissionMatch(N6Fake(log, id, none, rank: 10))} " +
              $"{s.IsMissionMatch(N6Fake(log, id, none, done: id))} " +
              $"{s.IsMissionMatch(N6Fake(log, id, none, nation: foreign))} " +
              $"{s.IsMissionMatch(N6Fake(log, id, none, nation: foreign2))} " +
              $"{s.IsMissionMatch(N6Fake(log, id, id))} {s.IsMissionMatch(N6Fake(log, id, other))}");
    }

    // section 2: current only
    static void N6Current(string label, MissionScriptDefinition d, int state, MissionLog log, int id, int none)
    {
        Check($"c3 N6 {label}: section {state + 1} holds while this mission is current only (not with none, not " +
              "with the next)",
              "True False False",
              $"{d.States[state].IsMissionMatch(N6Fake(log, id, id))} " +
              $"{d.States[state].IsMissionMatch(N6Fake(log, id, none))} " +
              $"{d.States[state].IsMissionMatch(N6Fake(log, id, id + 1))}");
    }

    // the gate guards' finishes: option <opt> begins with its zone's YOU_ACCEPT_THE_MISSION; the next and previous
    // options, 24 and 0 do nothing
    static void N6Accept(string label, MissionScriptDefinition d, MissionLog log, int id, int none, int opt,
                         params (ushort Zone, int Csid, ZoneText? Text)[] guards)
    {
        foreach (var (zone, csid, text) in guards)
        {
            var h = N6Csid(d, 0, N6Fin, zone, csid);
            Fake F(int option) => N6Fake(log, id, none, option: option);
            Check($"c3 N6 {label}: zone {zone}'s {csid} finish: option {opt} begins" +
                  (text == null ? " (no message)" : " with its zone's YOU_ACCEPT_THE_MISSION") +
                  $"; options {opt - 1}, {opt + 1}, 24 and 0 do nothing",
                  "Mission.Begin()" + (text == null ? "" : $",Text.Special({text.Value})") + "||||",
                  $"{N6Run(h, F(opt))}|{N6Run(h, F(opt - 1))}|{N6Run(h, F(opt + 1))}|{N6Run(h, F(24))}|" +
                  $"{N6Run(h, F(0))}");
        }
    }

    static readonly (ushort, int, ZoneText?)[] N6BastokGuards =
    {
        (QC.Zone.BastokMarkets, 1001, QC.Text.BastokMarkets.YOU_ACCEPT_THE_MISSION),
        (QC.Zone.BastokMines, 1001, QC.Text.BastokMines.YOU_ACCEPT_THE_MISSION),
        (QC.Zone.Metalworks, 1001, QC.Text.Metalworks.YOU_ACCEPT_THE_MISSION),
        (QC.Zone.PortBastok, 1001, QC.Text.PortBastok.YOU_ACCEPT_THE_MISSION),
    };

    static readonly (ushort, int, ZoneText?)[] N6SandoriaGuards =
    {
        (QC.Zone.SouthernSanDoria, 1009, null),
        (QC.Zone.SouthernSanDoria, 2009, null),
        (QC.Zone.NorthernSanDoria, 1009, null),
    };

    static readonly (ushort, int, ZoneText?)[] N6WindurstGuards =
    {
        (QC.Zone.PortWindurst, 78, QC.Text.PortWindurst.YOU_ACCEPT_THE_MISSION),
        (QC.Zone.WindurstWalls, 93, QC.Text.WindurstWalls.YOU_ACCEPT_THE_MISSION),
        (QC.Zone.WindurstWaters, 111, QC.Text.WindurstWaters.YOU_ACCEPT_THE_MISSION),
        (QC.Zone.WindurstWoods, 114, QC.Text.WindurstWoods.YOU_ACCEPT_THE_MISSION),
    };

    static void RunC3N6Common()
    {
        // the facet members N6 adds
        var f = new Fake();
        ((IQuestContext)f).Text.MessageTextFromNpc(QC.Text.TheBoyahdaTree.WARDEN_SPEECH);
        ((IQuestContext)f).Player.SpawnMob(QC.Mob.OuterHorutotoRuins.JESTER_WHOD_BE_KING_OFFSET, 1);
        Check("c3 N6 facets: MessageTextFromNpc records Text.MessageTextFromNpc(<zone>:<NAME>); SpawnMob(name, " +
              "offset) records Player.SpawnMob(<name>,<offset>) and leaves that offset spawned and alive",
              $"Text.MessageTextFromNpc({QC.Text.TheBoyahdaTree.WARDEN_SPEECH})," +
              $"Player.SpawnMob({QC.Mob.OuterHorutotoRuins.JESTER_WHOD_BE_KING_OFFSET},1)|True True False",
              $"{string.Join(",", f.Calls)}|" +
              $"{f.MobIsSpawned(QC.Mob.OuterHorutotoRuins.JESTER_WHOD_BE_KING_OFFSET, 1)} " +
              $"{f.MobIsAlive(QC.Mob.OuterHorutotoRuins.JESTER_WHOD_BE_KING_OFFSET, 1)} " +
              $"{f.MobIsSpawned(QC.Mob.OuterHorutotoRuins.JESTER_WHOD_BE_KING_OFFSET, 0)}");
        var v = new Fake();
        v.RankPointsHeld = 7;
        Check("c3 N6 facets: RankPoints reads on the view a check sees (IQuestPlayerView)",
              "7", ((IQuestPlayerView)v).RankPoints.ToString());
    }
}
