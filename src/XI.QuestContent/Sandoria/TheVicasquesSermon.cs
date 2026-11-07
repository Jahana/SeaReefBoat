// The Vicasque's Sermon (Log ID 0, Quest ID 9), a line-for-line port of scripts/quests/sandoria/The_Vicasques_Sermon.lua
// (quest rework B3b, San d'Oria group 10). Abioleget : !pos 128.771 0 118.538 231; Andelain : !pos 664.231 -12.849
// -539.413 101. Offered once Waters of the Cheval is completed: Abioleget's 589 (begun on any option). Accepted:
// Abioleget's WILL_PROVIDE_PITTANCE (Pod of Blue Peas, 70); 70 gil or more to him (npcUtil.tradeHas) plays 591, whose
// finish gives the peas and takes the 70 gil. Andelain (East Ronfaure): a trade with the peas at Prog 0 shows
// THANKS_TO_GODDESS and plays 19 (finish: the trade taken, Prog 1, GATES_OF_PARADISE_OPEN); at Prog 1 his
// APPRECIATE_OFFER_DECLINE; a trade without peas CANNOT_ACCEPT_ALMS; a talk MAY_ONLY_EAT at Prog 0, 19 at Prog 1
// (finish: GATES_OF_PARADISE_OPEN only). Abioleget at Prog 1: 600 (completed: Brass Ring, fame +20, The Benevolent
// One). No outside reference.
// Kept as the Lua file has it: both trades are tradeHas (more gil, other items or more peas are accepted; the finish
// confirms only what tradeHas confirmed); 19's finish shows GATES_OF_PARADISE_OPEN on every finish; a Prog other than 0
// and 1 plays 19 on a trade (THANKS_TO_GODDESS) and its finish then takes nothing.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class TheVicasquesSermon : QuestScript
    {
        public TheVicasquesSermon() : base(QuestLog.Sandoria, "THE_VICASQUES_SERMON") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/The_Vicasques_Sermon";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Sandoria, Item = Item.BrassRing, Title = Title.TheBenevolentOne });

            b.State(QuestStatus.Available, when: q => q.Player.HasCompletedQuest(QuestLog.Sandoria, "WATERS_OF_THE_CHEVAL"))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Abioleget).OnTrigger(QuestReply.ProgressEvent(589))
                .OnEventFinish(Zone.NorthernSanDoria, 589, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Abioleget)
                    .OnTrade(q =>
                    {
                        // npcUtil.tradeHas(trade, { { 'gil', 70 } })
                        if (q.Trade.HasWithGil(70))
                        {
                            return QuestReply.ProgressEvent(591);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Step == 1)
                        {
                            return QuestReply.ProgressEvent(600);
                        }
                        else
                        {
                            return QuestReply.MessageName(Text.NorthernSanDoria.WILL_PROVIDE_PITTANCE, Item.PodOfBluePeas, 70);
                        }
                    })
                .OnEventFinish(Zone.NorthernSanDoria, 591, q =>
                {
                    if (q.Player.GiveItem(Item.PodOfBluePeas))
                    {
                        q.Trade.Confirm();
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 600, q =>
                {
                    q.Quest.Complete();
                })
                .Npc(Zone.EastRonfaure, Npc.EastRonfaure.Andelain)
                    .OnTrade(q =>
                    {
                        if (q.Trade.Has(Item.PodOfBluePeas))
                        {
                            if (q.Quest.Step == 1)
                            {
                                return QuestReply.MessageName(Text.EastRonfaure.APPRECIATE_OFFER_DECLINE, Item.PodOfBluePeas);
                            }
                            else
                            {
                                q.Text.Special(Text.EastRonfaure.THANKS_TO_GODDESS);
                                return QuestReply.ProgressEvent(19);
                            }
                        }
                        else
                        {
                            return QuestReply.MessageSpecial(Text.EastRonfaure.CANNOT_ACCEPT_ALMS);
                        }
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Step == 1)
                        {
                            return QuestReply.ProgressEvent(19);
                        }
                        else
                        {
                            return QuestReply.MessageName(Text.EastRonfaure.MAY_ONLY_EAT, Item.PodOfBluePeas);
                        }
                    })
                .OnEventFinish(Zone.EastRonfaure, 19, q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        q.Trade.Confirm();
                        q.Quest.SetStep(1);
                    }

                    q.Text.Special(Text.EastRonfaure.GATES_OF_PARADISE_OPEN);
                });
        }
    }
}
