// XI.Verify.Quests -- quest rework B3b Windurst group 8 (The Tenshodo Showdown, As Thick as Thieves, Hitting the
// Marquisate): what no trace can see. As Thick as Thieves' completion sets the player local var
// 'Quest[2][71]mustZone' (a local var is not in the database, and nothing reads it: Hitting the Marquisate's offer reads
// the char var of that name), only when the completion succeeds. Hitting the Marquisate's qm15 with the Chandelier up:
// only 56's deferred pop (npc:timer, the stop rule) spawns it, so no flow reaches that arm; with the incense held and
// the cooldown past, the spawned Chandelier gives nil, and the Chandelier down gives 56. The Garlaige qms and 56 are
// cutscenes (startCutscene locks the player): the trace shows the same 0x032 for an event, so the reply kind is checked
// here.
using System;
using XI.Quests;
using XI.Quests.Authoring;
using QC = XI.QuestContent;

internal static partial class Program
{
    static void RunWindurstGroup8()
    {
        Console.WriteLine("== quest rework B3b Windurst group 8: completion local var, qm15 with the Chandelier up, cutscenes ==");
        RunWindurstGroup8LocalVar();
        RunWindurstGroup8Chandelier();
        RunWindurstGroup8Cutscene();
    }

    static void RunWindurstGroup8LocalVar()
    {
        var at = new QC.Windurst.AsThickAsThieves().Compile();
        string Finish(bool completes)
        {
            var f = new Fake { Status = QuestStatus.Accepted, Step = 7, Csid = 508, CompleteResult = completes };
            G2Handler(at, 1, QuestHandlerKind.OnEventFinish, null, 508).Run(f);
            return string.Join(";", f.Calls);
        }

        Check("windurst g8 local var:As Thick as Thieves' 508 sets Quest[2][71]mustZone after the completion (not when it fails)",
              $"Quest.Complete();Player.DelKeyItem({QC.KeyItem.GangWhereaboutsNote});Player.DelKeyItem({QC.KeyItem.FirstSignedForgedEnvelope});" +
              $"Player.DelKeyItem({QC.KeyItem.SecondSignedForgedEnvelope});Player.SetLocalVar(Quest[2][71]mustZone,1)|Quest.Complete()",
              $"{Finish(true)}|{Finish(false)}");
    }

    static void RunWindurstGroup8Chandelier()
    {
        var hm = new QC.Windurst.HittingTheMarquisate().Compile();
        string Qm15(bool spawned)
        {
            var f = new Fake { Status = QuestStatus.Accepted, Npc = new QuestNpcFake(17596831) };
            f.Vars["hagainProg"] = 7;
            f.KeyItems[QC.KeyItem.BombIncense] = true;
            f.Npc.LocalVars["chandelierCooldown"] = f.SystemTime - 1;
            if (spawned)
            {
                f.SpawnedMobs.Add($"{QC.Mob.GarlaigeCitadel.CHANDELIER}+0");
            }

            return G2Reply(G2Handler(hm, 1, QuestHandlerKind.OnTrigger, QC.Npc.GarlaigeCitadel.qm15), f);
        }

        Check("windurst g8 chandelier:qm15 at hagainProg 7, the cooldown past, the incense: the Chandelier up nil, down 56",
              "null|ProgressCutscene(56,193)",
              $"{Qm15(true)}|{Qm15(false)}");
    }

    static void RunWindurstGroup8Cutscene()
    {
        var hm = new QC.Windurst.HittingTheMarquisate().Compile();
        var f = new Fake { Status = QuestStatus.Accepted };
        f.Vars["hagainProg"] = 1;
        f.KeyItems[QC.KeyItem.BombIncense] = true;

        Check("windurst g8 cutscene:Garlaige qm6 at hagainProg 1 with the incense: 50 is a cutscene (startCutscene)",
              "ProgressCutscene(50,193)",
              G2Reply(G2Handler(hm, 1, QuestHandlerKind.OnTrigger, QC.Npc.GarlaigeCitadel.qm6), f));
    }
}
