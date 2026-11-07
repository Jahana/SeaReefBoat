// XI.Verify.Quests -- quest rework B3b Crystal War group 8 (San d'Oria 9-12: Songbirds in a Snowstorm, Blood of Heroes,
// Chasing Shadows, Face of the Future): what no trace can see (validator, declared steps, the maw facet member, the
// replies the flows reach only by a GM).
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunCrystalWarGroup8()
    {
        Console.WriteLine("== quest rework B3b Crystal War group 8: the validator and the maw facet ==");
        var songs = new QC.CrystalWar.SongbirdsInASnowstorm().Compile();
        var blood = new QC.CrystalWar.BloodOfHeroes().Compile();
        var chasing = new QC.CrystalWar.ChasingShadows().Compile();
        var face = new QC.CrystalWar.FaceOfTheFuture().Compile();
        Check("crystalwar g8:Songbirds in a Snowstorm clean", "",
              string.Join(" | ", QuestScriptValidator.Validate(songs)));
        Check("crystalwar g8:Blood of Heroes clean", "",
              string.Join(" | ", QuestScriptValidator.Validate(blood)));
        Check("crystalwar g8:Chasing Shadows clean", "",
              string.Join(" | ", QuestScriptValidator.Validate(chasing)));
        Check("crystalwar g8:Face of the Future clean", "",
              string.Join(" | ", QuestScriptValidator.Validate(face)));
        Check("crystalwar g8:the declared steps",
              "1,2,3,4,5,6,7 | 1,2,3,4,5 | 1,2,3,4,5,6,7,8 | 1,2,3,4,5,6,7,8,9,10",
              string.Join(" | ", new[] { songs, blood, chasing, face }.Select(d => string.Join(",", d.DeclaredSteps))));

        // the maw facet member: the fake answers from its set, and a maw's [S] twin shares its bit
        var f = new Fake();
        bool none = f.HasUnlockedMaw(QC.Zone.BatalliaDowns);
        f.UnlockedMaws.Add(QC.Zone.BatalliaDowns);
        Check("crystalwar g8:HasUnlockedMaw reads the set", "False:True:True",
              $"{none}:{f.HasUnlockedMaw(QC.Zone.BatalliaDowns)}:{f.HasUnlockedMaw(QC.Zone.BatalliaDownsS)}");

        RunCrystalWarGroup8Kinds(songs, blood, chasing, face);
        RunCrystalWarGroup8Sweeps(songs, blood, chasing, face);
    }

    // The replies the flows cannot tell apart: the instances' 10000 finishes (waived in the flows), the title of a
    // completion the Lua error blocks, and the handlers with no rival or fallback script at their NPC (a priority
    // flip changes nothing a client sees there). The Prog, Timer and key item states are flows' (the edges).
    static void RunCrystalWarGroup8Kinds(QuestScriptDefinition songs, QuestScriptDefinition blood,
                                         QuestScriptDefinition chasing, QuestScriptDefinition face)
    {
        var trig = QuestHandlerKind.OnTrigger;
        string R(QuestScriptDefinition d, int state, QuestStatus status, QuestHandlerKind kind, ushort zone, string npc,
                 int step = 0, Action<Fake> set = null)
        {
            var h = d.States[state].Handlers.Single(x => x.Kind == kind && x.Zone == zone && x.Entity == npc);
            var fk = new Fake { Status = status, Step = step, UniqueDay = 8000 };
            set?.Invoke(fk);
            var r = h.Fixed ?? h.Reply(fk);
            return r == null ? "null" : QuestContextFake.Describe(r);
        }
        string Fin(QuestScriptDefinition d, ushort zone, int csid)
        {
            var h = d.States.SelectMany(s => s.Handlers)
                .Single(x => x.Kind == QuestHandlerKind.OnEventFinish && x.Zone == zone && x.Id == csid);
            var fk = new Fake { Status = QuestStatus.Accepted, Csid = csid };
            h.Run(fk);
            return string.Join(";", fk.Calls);
        }
        var ac = QuestStatus.Accepted;
        ushort bds = QC.Zone.BeaucedineGlacierS;
        string foot = QC.Npc.BeaucedineGlacierS.ColossalFootprint;
        // the Footprint's 2 (Prog 2, the key items not held) is the one Progress reply of its NPC without a flow rival
        Check("crystalwar g8 kinds:Songbirds' Footprint at Prog 2", "ProgressEvent(2)",
              R(songs, 1, ac, trig, bds, foot, 2));

        Check("crystalwar g8 kinds:the instances' 10000 finishes (Ghoyu's Reverie, Everbloom Hollow)",
              "Quest.SetStep(4);Player.SetPos(319.8,-7.887,153.741,65,137) | " +
              "Quest.SetStep(5);Player.SetPos(-184.678,-0.266,-112.093,101,142)",
              $"{Fin(blood, QC.Zone.GhoyusReverie, 10000)} | {Fin(face, QC.Zone.EverbloomHollow, 10000)}");
    }

    // What the flows cannot show, each handler's answer at every Prog 0..11, run-length coded ("2:ProgressEvent(2)
    // 3-11:null"): the priority (Progress or Event) of the handlers with no rival or fallback script at their NPC, and
    // the reward titles. A zone-in answers with its cutscene id alone (QuestContextFake.DescribeZoneIn): the bridge
    // returns no priority there, so Event against ProgressEvent in a zone-in is not a difference.
    static void RunCrystalWarGroup8Sweeps(QuestScriptDefinition songs, QuestScriptDefinition blood,
                                          QuestScriptDefinition chasing, QuestScriptDefinition face)
    {
        string Sw(QuestScriptDefinition d, int state, QuestStatus status, QuestHandlerKind kind, ushort zone,
                  string npc, Action<Fake> set = null)
        {
            var h = d.States[state].Handlers.Single(x => x.Kind == kind && x.Zone == zone && x.Entity == npc);
            var per = new List<string>();
            for (int step = 0; step <= 11; step++)
            {
                var fk = new Fake { Status = status, Step = step, UniqueDay = 8000 };
                set?.Invoke(fk);
                if (h.Fixed == null && h.Reply == null)
                {
                    h.Run(fk);
                    per.Add(fk.Calls.Count == 0 ? "-" : string.Join(";", fk.Calls));
                    continue;
                }
                var r = h.Fixed ?? h.Reply(fk);
                per.Add(r == null ? "null" : kind == QuestHandlerKind.OnZoneIn ? QuestContextFake.DescribeZoneIn(r)
                                                                              : QuestContextFake.Describe(r));
            }
            var runs = new List<string>();
            for (int i = 0; i < per.Count;)
            {
                int j = i;
                while (j + 1 < per.Count && per[j + 1] == per[i]) j++;
                runs.Add((i == j ? $"{i}" : $"{i}-{j}") + "=" + per[i]);
                i = j + 1;
            }
            return string.Join(" ", runs);
        }
        var ac = QuestStatus.Accepted;
        var av = QuestStatus.Available;
        var trig = QuestHandlerKind.OnTrigger;
        ushort bds = QC.Zone.BeaucedineGlacierS, xar = QC.Zone.XarcabardS;
        ushort bdn = QC.Zone.BatalliaDownsS;

        var sw = new List<(string Label, string Got)>
        {
            // Songbirds in a Snowstorm
            ("Songbirds Firewood, a Flint Stone", Sw(songs, 1, ac, QuestHandlerKind.OnTrade, bds,
                QC.Npc.BeaucedineGlacierS.CharredFirewood, fk => fk.TradeItems[QC.Item.FlintStone] = 1)),
            ("Songbirds Snow, no Bloodletter",
                Sw(songs, 1, ac, trig, bds, QC.Npc.BeaucedineGlacierS.CompressedSnow)),
            // Blood of Heroes
            ("Blood Spoor available", Sw(blood, 0, av, trig, xar, QC.Npc.XarcabardS.AnimalSpoor)),
            ("Blood Spoor, Timer unset", Sw(blood, 1, ac, trig, xar, QC.Npc.XarcabardS.AnimalSpoor)),
            ("Blood Spoor, Timer tomorrow", Sw(blood, 1, ac, trig, xar, QC.Npc.XarcabardS.AnimalSpoor,
                fk => fk.Vars["Timer"] = 8001)),
            ("Blood Wheel Rut", Sw(blood, 1, ac, trig, xar, QC.Npc.XarcabardS.WheelRut)),
            // Chasing Shadows
            ("Chasing Pit", Sw(chasing, 1, ac, trig, xar, QC.Npc.XarcabardS.BackfilledPit)),
            ("Chasing Compact Footprint", Sw(chasing, 1, ac, trig, xar, QC.Npc.XarcabardS.CompactFootprint)),
            ("Chasing Sunken Footprint", Sw(chasing, 1, ac, trig, xar, QC.Npc.XarcabardS.SunkenFootprint)),
            ("Chasing Snowmelt, no Menechme",
                Sw(chasing, 1, ac, trig, bdn, QC.Npc.BatalliaDownsS.FreshSnowmelt)),
            // Face of the Future
            ("Face Hodgepodge", Sw(face, 0, ac, trig, QC.Zone.JugnerForest, QC.Npc.JugnerForest.MetallicHodgepodge)),
            ("Face Scrape Mark", Sw(face, 0, ac, trig, QC.Zone.YughottGrotto, QC.Npc.YughottGrotto.ScrapeMark)),
            // the rewards
            ("Rewards item/title", $"{songs.Reward.Item}/{songs.Reward.Title} " +
                $"{blood.Reward.Item}/{blood.Reward.Title} " +
                $"{chasing.Reward.Item}/{chasing.Reward.Title} {face.Reward.Item}/{face.Reward.Title}"),
        };

        foreach (var (label, got) in sw)
            Check("crystalwar g8 sweep:" + label, G8Sweeps.TryGetValue(label, out var want) ? want : "(none)", got);
    }

    static readonly Dictionary<string, string> G8Sweeps = new()
    {
        ["Songbirds Firewood, a Flint Stone"] = "0-3=null 4=ProgressEvent(4) 5-11=null",
        ["Songbirds Snow, no Bloodletter"] =
            "0-4=null 5=ProgressEvent(5) 6=ProgressEvent(27) 7=ProgressEvent(6) 8-11=null",
        ["Blood Spoor available"] = "0-11=ProgressEvent(1)",
        ["Blood Spoor, Timer unset"] = "0=null 1=ProgressEvent(3) 2=null 3=ProgressEvent(13) 4-11=null",
        ["Blood Spoor, Timer tomorrow"] = "0=null 1=ProgressEvent(3) 2=null 3=Event(15) 4-11=null",
        ["Blood Wheel Rut"] = "0=ProgressEvent(2) 1-4=null 5=ProgressEvent(7) 6-11=null",
        ["Chasing Pit"] = "0=null 1=ProgressEvent(31) 2-11=null",
        ["Chasing Compact Footprint"] = "0-1=null 2=ProgressEvent(32) 3=null 4=ProgressEvent(34) 5-11=null",
        ["Chasing Sunken Footprint"] = "0-2=null 3=ProgressEvent(33) 4-11=null",
        ["Chasing Snowmelt, no Menechme"] =
            "0-5=null 6=ProgressEvent(115) 7=ProgressEvent(116) 8=ProgressEvent(117) 9-11=null",
        ["Face Hodgepodge"] = "0=null 1=ProgressEvent(45) 2-11=null",
        ["Face Scrape Mark"] = "0-2=null 3=ProgressEvent(1) 4-11=null",
        ["Rewards item/title"] = "4213/ 18612/621 664/ 15856/629",
    };
}
