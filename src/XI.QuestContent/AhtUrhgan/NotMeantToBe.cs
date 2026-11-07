// Not Meant to Be (Log ID 6, Quest ID 64), a line-for-line port of scripts/quests/ahtUrhgan/Not_Meant_to_Be.lua (quest
// rework B3b Aht Urhgan, draft). Nashmau: Fhe Maksojha !pos 19.1 -6 71.3 (no script, no default). Caedarva Mire: qm12
// !pos 457 -7 -270.8 (no script; default NOTHING_HAPPENS), the NMs Lamia No.27 and Moshdahn (mob scripts with no
// onMobDeath). Available (no other condition): Fhe Maksojha 293, whose finish begins it at Prog 1. Accepted: Fhe
// Maksojha 295 / 294 / 296 / 297 at Prog 1 / 2 / 3 / 4 (294 a progress event, the others Events); 294's finish: Prog 3;
// 297's completes it (three Imperial Gold Pieces). qm12: Prog 1: 16 (Prog 2); Prog 3: with Stage below 3 and neither NM
// spawned, 17 (an Event), whose finish spawns both, claimed by the char (when neither is spawned); with Stage 3, 18
// (Prog 4). Each NM's death at Prog 3 sets its Stage bit (Lamia No.27 bit 0, Moshdahn bit 1). Completed: Fhe Maksojha
// 298 (an Event). The reward is a stack ({ { IMPERIAL_GOLD_PIECE, 3 } }), which QuestReward cannot hold: 297 gives the
// stack first, as npcUtil.completeQuest does, and completes only when it fit (Delivering the Goods' way). Kept as the
// Lua has it: 293 begins it on any option; qm12 returns nil at Prog 2 and 4, and at Prog 3 while an NM is spawned with
// Stage below 3 (the default); 17's finish checks the spawns again (the Lua's comment: two chars at once); a death at
// another Prog sets nothing.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class NotMeantToBe : QuestScript
    {
        public NotMeantToBe() : base(QuestLog.AhtUrghan, "NOT_MEANT_TO_BE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Not_Meant_to_Be";

        protected override void Build(QuestScriptBuilder b)
        {
            // the Lua's reward is item = { { IMPERIAL_GOLD_PIECE, 3 } }: given in 297 (see the header)
            b.Steps(1, 2, 3, 4);

            // Section: Quest available
            b.State(QuestStatus.Available)
                .Npc(Zone.Nashmau, Npc.Nashmau.FheMaksojha).OnTrigger(QuestReply.ProgressEvent(293))
                .OnEventFinish(Zone.Nashmau, 293, q =>
                {
                    q.Quest.Begin();
                    q.Quest.SetStep(1);
                });

            // Section: Quest accepted
            b.State(QuestStatus.Accepted)
                .Npc(Zone.Nashmau, Npc.Nashmau.FheMaksojha).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 1)
                    {
                        return QuestReply.Event(295);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(294);
                    }
                    else if (questProgress == 3)
                    {
                        return QuestReply.Event(296);
                    }
                    else if (questProgress == 4)
                    {
                        return QuestReply.Event(297);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Nashmau, 294, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.Nashmau, 297, q =>
                {
                    // npcUtil.completeQuest gives reward.item first and stops when it does not fit
                    if (q.Player.GiveItems((Item.ImperialGoldPiece, 3)))
                    {
                        q.Quest.Complete();
                    }
                })
                .Npc(Zone.CaedarvaMire, Npc.CaedarvaMire.qm12).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;
                    int questStage = q.Quest.Var("Stage");

                    if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(16);
                    }
                    else if (questProgress == 3)
                    {
                        if (questStage < 3 &&
                            !q.Player.MobIsSpawned(Mob.CaedarvaMire.LAMIA_NO27) &&
                            !q.Player.MobIsSpawned(Mob.CaedarvaMire.MOSHDAHN))
                        {
                            return QuestReply.Event(17);
                        }
                        else if (questStage == 3)
                        {
                            return QuestReply.ProgressEvent(18);
                        }
                    }
                    return null;
                })
                .Npc(Zone.CaedarvaMire, Npc.CaedarvaMire.LamiaNo27).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        q.Quest.SetVarBit("Stage", 0);
                    }
                })
                .Npc(Zone.CaedarvaMire, Npc.CaedarvaMire.Moshdahn).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        q.Quest.SetVarBit("Stage", 1);
                    }
                })
                .OnEventFinish(Zone.CaedarvaMire, 16, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.CaedarvaMire, 17, q =>
                {
                    // This conditional exists in both the trigger and the event finish in the original implementation
                    // and left in as additional protection to prevent multiple sets of NMs being spawned if the ???
                    // is hit by multiple players at the same time.  This is a fairly long cutscene!
                    if (!q.Player.MobIsSpawned(Mob.CaedarvaMire.LAMIA_NO27) &&
                        !q.Player.MobIsSpawned(Mob.CaedarvaMire.MOSHDAHN))
                    {
                        // SpawnMob(id):updateClaim(player), each
                        q.Player.SpawnMob(Mob.CaedarvaMire.LAMIA_NO27, 0, claim: true);
                        q.Player.SpawnMob(Mob.CaedarvaMire.MOSHDAHN, 0, claim: true);
                    }
                })
                .OnEventFinish(Zone.CaedarvaMire, 18, q =>
                {
                    q.Quest.SetStep(4);
                });

            // Section: Quest completed
            b.State(QuestStatus.Completed)
                .Npc(Zone.Nashmau, Npc.Nashmau.FheMaksojha).OnTrigger(QuestReply.Event(298));
        }
    }
}
