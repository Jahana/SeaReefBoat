// Striking a Balance (Log ID 6, Quest ID 63), a line-for-line port of scripts/quests/ahtUrhgan/Striking_a_Balance.lua
// (quest rework B3b Aht Urhgan, group 10). Aht Urhgan Whitegate (no scripts): Wazyih !pos -94 -6 -93.5 (default 699),
// Saliyahf !pos -59.9 0 64.9 (default 700); trigger area 9 (-103 0 -16 .. -100 0 -12). Al Zahbi:
// blank_striking_a_balance !pos -42.6 0 8.9 (no script; default NOTHING_OUT_OF_ORDINARY), which moves. Available:
// Wazyih 684 with the quest local var Option 0 (its finish: Option 1), else 685 (an Event); Saliyahf 686, whose finish
// sets Option 0 and begins it. Accepted, by Prog (one section each): Prog 0: Saliyahf replaceEvent 687; Wazyih 688
// (Prog 1). Prog 1: Saliyahf 687, Wazyih 689 (replaceEvents); area 9: 690 (Prog 2). Prog 2: Saliyahf replaceEvent 692
// with Option 0 (its finish: Option 1), else 693; Wazyih 691 (Prog 3). Prog 3: Saliyahf as at Prog 2; Wazyih
// replaceEvent 694; Al Zahbi's blank: Prog 4, the blank moved to another spot of positionTable
// (npcUtil.pickNewPosition), and the reply keyItem(MUNAHDAS_PACKAGE). Prog 4: any Whitegate zone-in: 695, whose finish
// takes the package and sets Prog 5. Prog 5: Saliyahf 696, whose finish completes it (three Imperial Bronze Pieces);
// Wazyih replaceEvent 697. The reward is a stack ({ { IMPERIAL_BRONZE_PIECE, 3 } }), which QuestReward cannot hold: 696
// gives the stack first, as npcUtil.completeQuest does, and completes only when it fit (Delivering the Goods' way).
// Kept as the Lua has it: Option is a local var (cleared by a zone change); 684 / 692 set it whatever the option; the
// blank sets Prog 4 and moves before its reply plays; pickNewPosition draws math.randomInt(1, 0) once before it counts
// the table (as npc_util.lua does), and the blank's first spot (-42.6 0 8.9) is in no row, so the first move draws from
// rows 2-7; the zone-in plays 695 from any previous zone; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class StrikingABalance : QuestScript
    {
        public StrikingABalance() : base(QuestLog.AhtUrghan, "STRIKING_A_BALANCE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Striking_a_Balance";

        static readonly double[][] PositionTable =
        {
            new double[] {  53,    0,  60 },
            new double[] { -70,    6,  40 },
            new double[] { -40,    6,  70 },
            new double[] {  18,    0, -30 },
            new double[] { -13,    0, -40 },
            new double[] {  39, -0.5, 116 },
            new double[] {  20,    0, -50 },
        };

        // Saliyahf at Prog 2 and 3
        static QuestReply SaliyahfReply(IQuestContext q)
        {
            if (q.Quest.LocalVar("Option") == 0)
            {
                return QuestReply.ReplaceEvent(692);
            }
            else
            {
                return QuestReply.ReplaceEvent(693);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            // the Lua's reward is item = { { IMPERIAL_BRONZE_PIECE, 3 } }: given in 696 (see the header)
            b.Steps(1, 2, 3, 4, 5);

            b.State(QuestStatus.Available)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Wazyih).OnTrigger(q =>
                {
                    if (q.Quest.LocalVar("Option") == 0)
                    {
                        return QuestReply.ProgressEvent(684);
                    }
                    else
                    {
                        return QuestReply.Event(685);
                    }
                })
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Saliyahf).OnTrigger(QuestReply.ProgressEvent(686))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 684, q =>
                {
                    q.Quest.SetLocalVar("Option", 1);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 686, q =>
                {
                    q.Quest.SetLocalVar("Option", 0);
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 0)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Saliyahf).OnTrigger(QuestReply.ReplaceEvent(687))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Wazyih).OnTrigger(QuestReply.ProgressEvent(688))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 688, q =>
                {
                    q.Quest.SetStep(1);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 1)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Saliyahf).OnTrigger(QuestReply.ReplaceEvent(687))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Wazyih).OnTrigger(QuestReply.ReplaceEvent(689))
                .OnTriggerAreaEnter(Zone.AhtUrhganWhitegate, 9, q => QuestReply.ProgressEvent(690))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 690, q =>
                {
                    q.Quest.SetStep(2);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 2)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Saliyahf).OnTrigger(SaliyahfReply)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Wazyih).OnTrigger(QuestReply.ProgressEvent(691))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 691, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 692, q =>
                {
                    q.Quest.SetLocalVar("Option", 1);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 3)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Saliyahf).OnTrigger(SaliyahfReply)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Wazyih).OnTrigger(QuestReply.ReplaceEvent(694))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 692, q =>
                {
                    q.Quest.SetLocalVar("Option", 1);
                })
                .Npc(Zone.AlZahbi, Npc.AlZahbi.blankstrikingabalance).OnTrigger(q =>
                {
                    q.Quest.SetStep(4);
                    // npcUtil.pickNewPosition(npc:getID(), positionTable): allowCurrentPosition false
                    var newPosition = NpcUtil.PickNewPosition(q, q.Npc, PositionTable);
                    // npc:setPos(newPosition.x, newPosition.y, newPosition.z)
                    q.Npc.SetPos(newPosition[0], newPosition[1], newPosition[2]);
                    return QuestReply.KeyItem(KeyItem.MunahdasPackage);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 4)
                .OnZoneIn(Zone.AhtUrhganWhitegate, q => QuestReply.Event(695))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 695, q =>
                {
                    q.Player.DelKeyItem(KeyItem.MunahdasPackage);
                    q.Quest.SetStep(5);
                });

            b.State(QuestStatus.Accepted, when: q => q.Quest.Step == 5)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Saliyahf).OnTrigger(QuestReply.ProgressEvent(696))
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Wazyih).OnTrigger(QuestReply.ReplaceEvent(697))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 696, q =>
                {
                    // npcUtil.completeQuest gives reward.item first and stops when it does not fit
                    if (q.Player.GiveItems((Item.ImperialBronzePiece, 3)))
                    {
                        q.Quest.Complete();
                    }
                });
        }
    }
}
