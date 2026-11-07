// XI.Verify.Quests -- quest rework B3b hidden quests group 3 (the item errands: Chocobo Whistle, Lost Chick, Crimson
// Orb, Invitation Cards): each definition validates clean and declares its steps; what no flow can reach (a Lost
// Chick owner's update answered for another NPC). The Curse I, the pond counts and the Whistle's names are flows.
using System;
using System.Linq;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunHiddenGroup3()
    {
        Console.WriteLine("== quest rework B3b hidden quests group 3: the item errands; what no flow reaches ==");
        var quests = new (string Name, QuestScript Script)[]
        {
            ("Whistle", new QC.HiddenQuests.ChocoboWhistle()),
            ("Chick", new QC.HiddenQuests.LostChick()),
            ("Orb", new QC.HiddenQuests.CrimsonOrb()),
            ("Cards", new QC.HiddenQuests.InvitationCards()),
        };
        foreach (var (name, script) in quests)
            Check($"hidden g3:{name} clean", "", string.Join(" | ", QuestScriptValidator.Validate(script.Compile())));
        Check("hidden g3:Orb declares steps 1..3", "1,2,3",
              string.Join(",", quests[2].Script.Compile().DeclaredSteps));
        Check("hidden g3:Whistle declares steps 1..3", "1,2,3",
              string.Join(",", quests[0].Script.Compile().DeclaredSteps));
        RunHiddenGroup3Chick(quests[1].Script.Compile());
    }

    static QuestHandlerDecl G3Handler(QuestScriptDefinition d, QuestHandlerKind kind, int id) =>
        d.States.SelectMany(s => s.Handlers).First(x => x.Kind == kind && x.Id == id);

    // Lost Chick: the owner's update answers only for its own NPC (the Lua's npc:getName() check; the flows cannot
    // start an owner's csid from another NPC)
    static void RunHiddenGroup3Chick(QuestScriptDefinition chick)
    {
        var upd = chick.States.SelectMany(s => s.Handlers)
            .First(x => x.Kind == QuestHandlerKind.OnEventUpdate && x.Id == 847);
        string Run(string npcName)
        {
            var f = new Fake { Option = 1, Npc = new QuestNpcFake(1) { Name = npcName } };
            f.CharVars["[ChocoboRaising]LostChick"] = 1 + 0x30 + (1 << 6);
            upd.Run(f);
            return string.Join(";", f.Calls);
        }
        Check("hidden g3:Lost Chick's Cahaurme update answers for Cahaurme only", "False|True",
              $"{Run("Victoire").Contains("UpdateEvent")}|{Run("Cahaurme").Contains("UpdateEvent")}");
    }
}
