// The Sand Charm (Log ID 4, Quest ID 8), a line-for-line port of scripts/quests/otherAreas/The_Sand_Charm.lua (quest
// rework B3b). Mhaura: Blandine !pos 23 -7 41 249 (two NPCs share the name and the handler; the first is cutscene_only,
// so only the one at the dock end answers), Zexu !pos 31.511 -9.001 23.496 249, Celestina !pos -37.624 -16.050 75.681
// 249 (a goldsmiths' guild shop script). Available at Windurst fame level >= 2: Blandine, on the town side (z <= 29 or
// z >= 38 or x <= 16 or x >= 32: not the docking area): at Prog 0 125, finish Prog 1; at Prog 2 124, finish Prog 3;
// otherwise nil (her default 122). Zexu at Prog 1: 123, finish Prog 2 (else his default 121). Celestina at Prog 3: 126
// (the Sand Charm), whose finish begins the quest (else her script: the guild shop). Accepted: a trade to Celestina
// holding a Sand Charm (npcUtil.tradeHas: other items allowed): 127 (0, the charm), whose finish completes the quest (2000
// exp, 2000 gil, the Map of Bostaunieux Oubliette, fame 40 Windurst), takes the whole trade and sets the char var
// BlandineDialog 1. Completed: Blandine with BlandineDialog 1: 128, whose finish clears it; else 129 replaceDefault.
// Kept as the Lua has it: the finishes (123 / 124 / 125 / 126) are keyed on the csid alone (no Prog check); the trade
// takes everything traded with the charm (tradeComplete); BlandineDialog is a plain char var, not a quest var.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class TheSandCharm : QuestScript
    {
        public TheSandCharm() : base(QuestLog.OtherAreas, "THE_SAND_CHARM") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/The_Sand_Charm";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Exp = 2000,
                Gil = 2000,
                KeyItem = KeyItem.MapOfBostaunieuxOubliette,
                Fame = 40,
                FameArea = FameArea.Windurst,
            });
            b.Steps(1, 2, 3);

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Windurst) >= 2)
                .Npc(Zone.Mhaura, Npc.Mhaura.Blandine).OnTrigger(q =>
                {
                    double xPos = q.Player.XPos;
                    double zPos = q.Player.ZPos;

                    // Cutscenes won't start in the docking area. Must be on town side.
                    if (zPos <= 29 || zPos >= 38 || xPos <= 16 || xPos >= 32)
                    {
                        int questProgress = q.Quest.Step;

                        if (questProgress == 0)
                        {
                            return QuestReply.ProgressEvent(125); // I know he's out there
                        }
                        else if (questProgress == 2)
                        {
                            return QuestReply.ProgressEvent(124); // Celestina doesn't want to hear it anymore
                        }
                    }
                    return null;
                })
                .Npc(Zone.Mhaura, Npc.Mhaura.Zexu).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(123); // Word is, pirates got 'im
                    }
                    return null;
                })
                .Npc(Zone.Mhaura, Npc.Mhaura.Celestina).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(126, Item.SandCharm); // Go get back the sand charm
                    }
                    return null;
                })
                .OnEventFinish(Zone.Mhaura, 123, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.Mhaura, 124, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.Mhaura, 125, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.Mhaura, 126, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Mhaura, Npc.Mhaura.Celestina).OnTrade(q =>
                {
                    if (q.Trade.Has(Item.SandCharm))
                    {
                        return QuestReply.ProgressEvent(127, 0, Item.SandCharm); // He's dead, but he'll be back soon I'm sure
                    }
                    return null;
                })
                .OnEventFinish(Zone.Mhaura, 127, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        q.Player.SetCharVar("BlandineDialog", 1);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.Mhaura, Npc.Mhaura.Blandine).OnTrigger(q =>
                {
                    if (q.Player.CharVar("BlandineDialog") == 1)
                    {
                        return QuestReply.ProgressEvent(128); // I stand here and pray for sailors now
                    }
                    else
                    {
                        return QuestReply.Event(129).ReplaceDefault(); // May the sea be kind...
                    }
                })
                .OnEventFinish(Zone.Mhaura, 128, q =>
                {
                    q.Player.SetCharVar("BlandineDialog", 0);
                });
        }
    }
}
