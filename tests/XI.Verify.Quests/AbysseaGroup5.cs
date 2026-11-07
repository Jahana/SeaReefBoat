// XI.Verify.Quests -- quest rework B3b Abyssea group 5 (the Dominion Ops of Abyssea - Uleguerand, #01-#14): each
// definition validates clean, declares no steps and has the Lua's one section (accepted), whose one handler is the op's
// target's OnMobDeath, calling xi.abyssea.dominionOnMobDeath with the op's own id. The flows
// (tests/golden/flows/abyssea.dominion_op_*_uleguerand*.flow) prove each op's zone, mob and op id by its own kills;
// this pins the same table, the Lua files' data, on the fake, and each op's LegacyPath.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunAbysseaGroup5()
    {
        Console.WriteLine("== quest rework B3b Abyssea group 5 ==");
        var ops = new (string Name, QuestScript Quest)[]
        {
            ("#01", new QC.Abyssea.DominionOp01Uleguerand()),
            ("#02", new QC.Abyssea.DominionOp02Uleguerand()),
            ("#03", new QC.Abyssea.DominionOp03Uleguerand()),
            ("#04", new QC.Abyssea.DominionOp04Uleguerand()),
            ("#05", new QC.Abyssea.DominionOp05Uleguerand()),
            ("#06", new QC.Abyssea.DominionOp06Uleguerand()),
            ("#07", new QC.Abyssea.DominionOp07Uleguerand()),
            ("#08", new QC.Abyssea.DominionOp08Uleguerand()),
            ("#09", new QC.Abyssea.DominionOp09Uleguerand()),
            ("#10", new QC.Abyssea.DominionOp10Uleguerand()),
            ("#11", new QC.Abyssea.DominionOp11Uleguerand()),
            ("#12", new QC.Abyssea.DominionOp12Uleguerand()),
            ("#13", new QC.Abyssea.DominionOp13Uleguerand()),
            ("#14", new QC.Abyssea.DominionOp14Uleguerand()),
        };
        foreach (var o in ops)
        {
            Check($"abyssea g5:{o.Name} clean", "",
                  string.Join(" | ", QuestScriptValidator.Validate(o.Quest.Compile())));
            Check($"abyssea g5:{o.Name} declares steps []", "", string.Join(",", o.Quest.Compile().DeclaredSteps));
        }

        // the statuses and steps each state matches, state by state
        string States(QuestScript quest) => string.Join(" ", quest.Compile().States.Select(s => string.Join("/",
            s.Matches.Select(m => (m.AnyStatus ? "any" : m.Status.ToString()) + (m.Step is int st ? ":" + st : "") +
                                  (m.When == null ? "" : "?")))));
        Check("abyssea g5:the states' statuses and steps", string.Join(" | ", Enumerable.Repeat("Accepted", 14)),
              string.Join(" | ", ops.Select(o => States(o.Quest))));

        // each op's handlers: its zone, its target and the call its run makes on an accepted fake
        string Handlers(QuestScript quest) => string.Join(",",
            quest.Compile().States.SelectMany(s => s.Handlers).Select(h =>
            {
                var f = new Fake { Status = QuestStatus.Accepted };
                h.Run?.Invoke(f);
                string kind = h.Kind == QuestHandlerKind.OnMobDeath && !h.Called ? "" : h.Kind + "!";
                return $"{h.Zone}/{h.Entity}:{kind}" + string.Join(";", f.Calls);
            }));
        Check("abyssea g5:each op's one handler: its target's OnMobDeath calls dominionOnMobDeath with its op id",
              "253/Mechanical_Menace:Player.DominionOnMobDeath(574) ; " +
              "253/Spectator:Player.DominionOnMobDeath(575) ; 253/Sub-zero_Gear:Player.DominionOnMobDeath(576) ; " +
              "253/Snowflake:Player.DominionOnMobDeath(577) ; 253/Verglas_Golem:Player.DominionOnMobDeath(578) ; " +
              "253/Olyphant:Player.DominionOnMobDeath(579) ; 253/Svelldrake:Player.DominionOnMobDeath(580) ; " +
              "253/Adasaurus:Player.DominionOnMobDeath(581) ; 253/Ectozoon:Player.DominionOnMobDeath(582) ; " +
              "253/Hoarmite:Player.DominionOnMobDeath(583) ; 253/Ermit_Imp:Player.DominionOnMobDeath(584) ; " +
              "253/Benumbed_Vodoriga:Player.DominionOnMobDeath(585) ; " +
              "253/Bluffalo:Player.DominionOnMobDeath(586) ; 253/Sierra_Tiger:Player.DominionOnMobDeath(587)",
              string.Join(" ; ", ops.Select(o => Handlers(o.Quest))));

        // each op's LegacyPath, its own Lua file: the slot in the containers list (two neighbours swapped land in the
        // same order, so the containers-list gate cannot see it) and the key of CSharpQuests.legacy (!reloadquest)
        Check("abyssea g5:each op's LegacyPath is its own Lua file",
              "Uleguerand_Dominion_Op_01 ; Uleguerand_Dominion_Op_02 ; Uleguerand_Dominion_Op_03 ; " +
              "Uleguerand_Dominion_Op_04 ; Uleguerand_Dominion_Op_05 ; Uleguerand_Dominion_Op_06 ; " +
              "Uleguerand_Dominion_Op_07 ; Uleguerand_Dominion_Op_08 ; Uleguerand_Dominion_Op_09 ; " +
              "Uleguerand_Dominion_Op_10 ; Uleguerand_Dominion_Op_11 ; Uleguerand_Dominion_Op_12 ; " +
              "Uleguerand_Dominion_Op_13 ; Uleguerand_Dominion_Op_14",
              string.Join(" ; ", ops.Select(o => o.Quest.LegacyPath.Substring("scripts/quests/abyssea/".Length))));
    }
}
