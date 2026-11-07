// XI.Verify.Quests -- quest rework B3b Crystal War group 9 (Erlene's scholars and the Eldieme / Crawlers' Nest [S]
// askers: A Little Knowledge, On Sabbatical, Downward Helix, Lost in Translocation, The Weekly Adventurer): what no
// trace can see (validator, declared steps). The replies, the ties at Erlene and Gentle Tiger, the settings, Timer,
// spell, effect and key item gates are flows' (crystalWar.a_little_knowledge*, on_sabbatical*, downward_helix*,
// lost_in_translocation*, the_weekly_adventurer*).
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunCrystalWarGroup9()
    {
        Console.WriteLine("== quest rework B3b Crystal War group 9: the validator and the declared steps ==");
        var alk = new QC.CrystalWar.ALittleKnowledge().Compile();
        var os = new QC.CrystalWar.OnSabbatical().Compile();
        var dh = new QC.CrystalWar.DownwardHelix().Compile();
        var lit = new QC.CrystalWar.LostInTranslocation().Compile();
        var twa = new QC.CrystalWar.TheWeeklyAdventurer().Compile();
        Check("crystalwar g9:A Little Knowledge clean", "",
              string.Join(" | ", QuestScriptValidator.Validate(alk)));
        Check("crystalwar g9:On Sabbatical clean", "",
              string.Join(" | ", QuestScriptValidator.Validate(os)));
        Check("crystalwar g9:Downward Helix clean", "",
              string.Join(" | ", QuestScriptValidator.Validate(dh)));
        Check("crystalwar g9:Lost in Translocation clean", "",
              string.Join(" | ", QuestScriptValidator.Validate(lit)));
        Check("crystalwar g9:The Weekly Adventurer clean", "",
              string.Join(" | ", QuestScriptValidator.Validate(twa)));
        Check("crystalwar g9:the declared steps", "1 | 1,2 | 1,2,3,4 |  | 1,2,3,4",
              string.Join(" | ", new[] { alk, os, dh, lit, twa }.Select(d => string.Join(",", d.DeclaredSteps))));
        RunCrystalWarGroup9Kinds(alk, dh, lit, twa);
    }

    // The kinds the flows cannot tell apart: Tucker's, Thorben's, the Eldieme askers', Naiko's and Rakula's replies
    // have no rival or fallback script at their NPC (a priority flip changes nothing a client sees there), and Erlene's
    // vellum trade has no rival to a trade. Erlene's talks, the Gentle Tiger and Indescript Markings (Pashhow) states
    // are flows' (the ties and the rivals).
    static void RunCrystalWarGroup9Kinds(QuestScriptDefinition alk, QuestScriptDefinition dh,
                                         QuestScriptDefinition lit, QuestScriptDefinition twa)
    {
        var trig = QuestHandlerKind.OnTrigger;
        var trade = QuestHandlerKind.OnTrade;
        string R(QuestScriptDefinition d, int state, QuestHandlerKind kind, ushort zone, string npc,
                 int step = 0, Action<Fake> set = null)
        {
            var h = d.States[state].Handlers.Single(x => x.Kind == kind && x.Zone == zone && x.Entity == npc);
            var fk = new Fake { Status = state == 0 ? QuestStatus.Available : QuestStatus.Accepted, Step = step };
            set?.Invoke(fk);
            var r = h.Fixed ?? h.Reply(fk);
            return r == null ? "null" : QuestContextFake.Describe(r);
        }
        ushort nest = QC.Zone.CrawlersNestS, eld = QC.Zone.TheEldiemeNecropolisS;
        string tucker = QC.Npc.CrawlersNestS.Tucker, erlene = QC.Npc.TheEldiemeNecropolisS.Erlene;
        int berry = QC.Item.Rolanberry;
        string Talk(int option) => R(alk, 1, trig, nest, tucker, 0, f => f.Vars["Option"] = option);
        string Swap(int option, int berries, int flints = 0) => R(alk, 1, trade, nest, tucker, 0, f =>
        {
            f.Vars["Option"] = option;
            f.TradeItems[berry] = berries;
            if (flints > 0) f.TradeItems[QC.Item.FlintStone] = flints;
        });
        string Vellum(int job) => R(alk, 1, trade, eld, erlene, 0, f =>
        {
            f.MainJob = job;
            f.TradeItems[QC.Item.SheetOfVellum] = 12;
        });

        // Tucker's talk at Prog 0 by Option, his trade by Option (12 berries; Option 0 and 4, 11 berries, and
        // berries with a Flint Stone fall to the 14), Erlene's trade of 12 Sheets of Vellum by main job
        Check("crystalwar g9 kinds:A Little Knowledge, Tucker and Erlene's trade",
              "ProgressEvent(6) | Event(7) | Event(9) | Event(9) | Event(12) | " +
              "ProgressEvent(14) | ProgressEvent(8) | ProgressEvent(10) | ProgressEvent(11) | ProgressEvent(14) | " +
              "ProgressEvent(14) | ProgressEvent(14) | ProgressEvent(12,1) | ProgressEvent(12,0)",
              string.Join(" | ", new[]
              {
                  Talk(0), Talk(1), Talk(2), Talk(3), Talk(4),
                  Swap(0, 12), Swap(1, 12), Swap(2, 12), Swap(3, 12), Swap(4, 12), Swap(1, 11), Swap(1, 12, 1),
                  Vellum(QC.Job.Smn), Vellum(QC.Job.War),
              }));

        // Once completed, Erlene answers a Scholar under level 5 or one who already has the spells with 15, an Event that
        // replaces the default text (no flow can see its kind)
        Check("crystalwar g9 kinds:A Little Knowledge, Erlene once completed", "Event(15):replaceDefault()",
              R(alk, 2, trig, eld, erlene, 0, f => f.MainJob = QC.Job.War));

        // Downward Helix: Indescript Markings (Sauromugue Champaign [S]) at Prog 3
        Check("crystalwar g9 kinds:Downward Helix, Indescript Markings at Prog 3", "ProgressEvent(4)",
              R(dh, 1, trig, QC.Zone.SauromugueChampaignS, QC.Npc.SauromugueChampaignS.IndescriptMarkings, 3));

        // Lost in Translocation: Thorben's 103 (the offer) and 104-107 (no piece, Option 1, one piece, the three), the
        // three Eldieme askers without their piece
        ushort bat = QC.Zone.BatalliaDownsS;
        string thorben = QC.Npc.BatalliaDownsS.Thorben;
        string Thorben(int option, params int[] pieces) => R(lit, 1, trig, bat, thorben, 0, f =>
        {
            f.Vars["Option"] = option;
            foreach (int piece in pieces) f.KeyItems[piece] = true;
        });
        var left = QC.KeyItem.LeftMapPiece;
        var middle = QC.KeyItem.MiddleMapPiece;
        var right = QC.KeyItem.RightMapPiece;
        var east = QC.Npc.TheEldiemeNecropolisS.Erik;
        Check("crystalwar g9 kinds:Lost in Translocation, Thorben and the Eldieme askers",
              "ProgressEvent(103) | Event(104) | ProgressEvent(105) | Event(106) | ProgressEvent(107) | " +
              "ProgressEvent(3) | ProgressEvent(4) | ProgressEvent(5)",
              string.Join(" | ", new[]
              {
                  R(lit, 0, trig, bat, thorben), Thorben(0), Thorben(0, left), Thorben(1, left),
                  Thorben(0, left, middle, right),
                  R(lit, 1, trig, eld, east), R(lit, 1, trig, eld, QC.Npc.TheEldiemeNecropolisS.Gravestone),
                  R(lit, 1, trig, eld, QC.Npc.TheEldiemeNecropolisS.Sarcophagusmapquest),
              }));

        // The Weekly Adventurer: Naiko-Paneiko's offer 16, his Prog 0 / 1 / 2 / 3 / 4 talks (21, 19 with its
        // parameters, 17, 18, 21) and the completed 20 (replaceDefault); Rakula-Motakula's 3 at Prog 0 and at Prog 4
        string naiko = QC.Npc.CrawlersNestS.NaikoPaneiko, rakula = QC.Npc.RolanberryFieldsS.RakulaMotakula;
        string Naiko(int state, int prog) => R(twa, state, trig, nest, naiko, prog);
        string Rakula(int prog) => R(twa, 1, trig, QC.Zone.RolanberryFieldsS, rakula, prog);
        Check("crystalwar g9 kinds:The Weekly Adventurer, Naiko-Paneiko and Rakula-Motakula",
              "ProgressEvent(16) | ProgressEvent(21) | ProgressEvent(19,171,1,2984,3,0,10,0,0) | ProgressEvent(17) | " +
              "ProgressEvent(18) | ProgressEvent(21) | Event(20):replaceDefault() | " +
              "ProgressEvent(3) | ProgressEvent(3,0,0,0,0,0,0,0,1)",
              string.Join(" | ", new[]
              {
                  Naiko(0, 0), Naiko(1, 0), Naiko(1, 1), Naiko(1, 2), Naiko(1, 3), Naiko(1, 4), Naiko(2, 0),
                  Rakula(0), Rakula(4),
              }));
    }
}
