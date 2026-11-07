// Rosel the Armorer (Log ID 0, Quest ID 2), a line-for-line port of scripts/quests/sandoria/Rosel_the_Armorer.lua (quest
// rework B3b, San d'Oria group 9). Rosel : !pos 69.895 0 41.073 230; Guilerme : !pos -4.5 0 99 231. Available: Rosel's
// first talk draws the intended prince (Stage = math.randomInt(1, 2): 1 Pieuje, 2 Trion; kept until completion), then
// 523 at Prog 0 (option 0: begun, the Receipt for the Prince; any other option: Prog 1) or 524 at Prog 1 (option 0:
// begun, the receipt), each with Stage - 1 as its fifth argument. Accepted: Rosel's 524 while the receipt is held (no
// finish handler there); Guilerme's 507 with the receipt (the option is the chosen prince: Option = option, the receipt
// taken); Rosel's 527 without it (Stage - 1, Option), whose finish pays 200 gil and fame +20 on option 0, else 100 gil
// and fame +10, then completes it (Entrance Denied). Outside references: globals/npc_util.lua:637 and :741 name it in
// doc comments only (no code).
// Kept as the Lua file has it: the draw happens in onTrigger whenever Stage is 0 (a GM-set Stage is never redrawn); 524
// played while accepted has no finish; 527 pays and adds fame before completing, its option alone deciding the amount.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class RoselTheArmorer : QuestScript
    {
        public RoselTheArmorer() : base(QuestLog.Sandoria, "ROSEL_THE_ARMORER") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Rosel_the_Armorer";

        protected override void Build(QuestScriptBuilder b)
        {
            // Delivered to correct Prince: 200G, Incorrect Prince: 100G
            b.Reward(new QuestReward { Title = Title.EntranceDenied });

            b.State(QuestStatus.Available)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Rosel).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;
                    int questStage = q.Quest.Var("Stage");

                    // On triggering this CS, set a value to determine if Pieuje or Trion is the intended recipient. This
                    // will persist until the quest is completed, and is one higher than the parameter value (1 == Pieuje,
                    // 2 == Trion)
                    if (questStage == 0)
                    {
                        questStage = q.Player.RandomInt(1, 2);
                        q.Quest.SetVar("Stage", questStage);
                    }

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(523, 0, 0, 0, 0, questStage - 1);
                    }
                    else if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(524, 0, 0, 0, 0, questStage - 1);
                    }
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoria, 523, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.ReceiptForThePrince);
                    }
                    else
                    {
                        q.Quest.SetStep(1);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 524, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.ReceiptForThePrince);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Rosel).OnTrigger(q =>
                {
                    int questStage = q.Quest.Var("Stage");
                    if (q.Player.HasKeyItem(KeyItem.ReceiptForThePrince))
                    {
                        return QuestReply.ProgressEvent(524, 0, 0, 0, 0, questStage - 1);
                    }
                    else
                    {
                        int questOption = q.Quest.Var("Option");
                        return QuestReply.ProgressEvent(527, 0, 0, 0, 0, questStage - 1, questOption);
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 527, q =>
                {
                    int gilReward = 100;
                    int fameReward = 10;

                    if (q.Option == 0)
                    {
                        gilReward = 200;
                        fameReward = 20;
                    }

                    q.Player.GiveCurrency("gil", gilReward);
                    q.Player.AddFame(FameArea.Sandoria, fameReward);
                    q.Quest.Complete();
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Guilerme).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.ReceiptForThePrince))
                    {
                        int questStage = q.Quest.Var("Stage");
                        return QuestReply.ProgressEvent(507, 0, 0, 0, 0, 0, 0, questStage - 1);
                    }
                    return null;
                })
                .OnEventFinish(Zone.NorthernSanDoria, 507, q =>
                {
                    // The option returned from this event determines whether the player has chosen correctly or not.
                    // Option 0 is correct answer, while option 1 was an incorrect answer.
                    q.Quest.SetVar("Option", q.Option);
                    q.Player.DelKeyItem(KeyItem.ReceiptForThePrince);
                });
        }
    }
}
