// It's Raining Mannequins! (Log ID 4, Quest ID 29), a line-for-line port of
// scripts/quests/otherAreas/Its_Raining_Mannequins.lua (quest rework B3b, Other Areas group 4). Mhaura: Fyi Chalmwoh !pos -39.273 -16.000
// 70.126 249 (no script, no default); Selbina: Ramona !pos 12.511 -7.287 2.939 248 (a script: 170 / 172 / 175); Northern
// San d'Oria: Cheupirudaux !pos -138.163 11.999 250.949 231 (the woodworking guild master's script).
// Available: Fyi 305, whose finish begins the quest (any option). Accepted, Prog 0: Fyi 306 (Event); Ramona 1103, whose
// finish gives Ye Olde Mannequin Catalogue (npcUtil.giveKeyItem) and sets Prog 1. Prog 1: Fyi 307 (Event); Cheupirudaux
// 759, whose finish gives the Mannequin Joint Diagrams and sets Prog 2. Prog 2: Fyi 308 (Event); a trade of exactly the
// five mannequin parts: 309, whose finish confirms the trade, sets Prog 3 and Wait = GetSystemTime(). Prog 3: Fyi, at
// GetSystemTime() >= Wait + 60: 311, else 310 (Event). 311's finish: the mannequin of the char's race (Hume M Mannequin +
// race - 1); with a free slot and none held: quest:complete (no reward table), tradeComplete (no trade open), the
// mannequin added with its exdata race and ITEM_OBTAINED; otherwise ITEM_CANNOT_BE_OBTAINED + 4.
// Kept as the Lua has it: 305's finish begins at any option; the key items stay held after the completion; 311's finish
// calls tradeComplete with no trade open (the parts went at 309).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class ItsRainingMannequins : QuestScript
    {
        public ItsRainingMannequins() : base(QuestLog.OtherAreas, "ITS_RAINING_MANNEQUINS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Its_Raining_Mannequins";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3);

            // Speak to Fyi Chalmwoh at G-8 in Mhaura (in the Goldsmithing shop).
            b.State(QuestStatus.Available)
                .Npc(Zone.Mhaura, Npc.Mhaura.FyiChalmwoh).OnTrigger(QuestReply.ProgressEvent(305))
                .OnEventFinish(Zone.Mhaura, 305, q =>
                {
                    q.Quest.Begin();
                });

            // Now go to Selbina and talk to Ramona at H-9 in the Weaver's shop. She'll give you Key Item Ye Olde Mannequin Catalogue.
            b.State(QuestStatus.Accepted, step: 0)
                // Hint to go to Selbina
                .Npc(Zone.Mhaura, Npc.Mhaura.FyiChalmwoh).OnTrigger(QuestReply.Event(306))
                .Npc(Zone.Selbina, Npc.Selbina.Ramona).OnTrigger(QuestReply.ProgressEvent(1103))
                // After this her default cs becomes 175, but we have it down as 170?
                .OnEventFinish(Zone.Selbina, 1103, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.YeOldeMannequinCatalogue);
                    q.Quest.SetStep(1);
                });

            // Now go to Northern San d'Oria and talk to Cheupirudaux at F-3 in front of the Woodworking Guild. He'll give you Key Item Mannequin Joint Diagrams.
            b.State(QuestStatus.Accepted, step: 1)
                // Hint to go to San d'Oria
                .Npc(Zone.Mhaura, Npc.Mhaura.FyiChalmwoh).OnTrigger(QuestReply.Event(307))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Cheupirudaux).OnTrigger(QuestReply.ProgressEvent(759))
                .OnEventFinish(Zone.NorthernSanDoria, 759, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.MannequinJointDiagrams);
                    q.Quest.SetStep(2);
                });

            // Now go back to Mhaura and trade all 5 pieces to Fyi Chalmwoh.
            b.State(QuestStatus.Accepted, step: 2)
                .Npc(Zone.Mhaura, Npc.Mhaura.FyiChalmwoh).OnTrigger(q =>
                {
                    return QuestReply.Event(308);
                })
                .OnTrade(q =>
                {
                    if (q.Trade.HasExactly(
                            Item.MannequinHead,
                            Item.MannequinBody,
                            Item.MannequinHands,
                            Item.MannequinLegs,
                            Item.MannequinFeet))
                    {
                        return QuestReply.ProgressEvent(309);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Mhaura, 309, q =>
                {
                    q.Trade.Confirm();

                    q.Quest.SetStep(3);
                    q.Quest.SetVar("Wait", q.Player.SystemTime);
                });

            // You have to wait about one earth minute to get your reward.
            b.State(QuestStatus.Accepted, step: 3)
                .Npc(Zone.Mhaura, Npc.Mhaura.FyiChalmwoh).OnTrigger(q =>
                {
                    int wait = q.Quest.Var("Wait");
                    if (q.Player.SystemTime >= wait + 60)
                    {
                        return QuestReply.ProgressEvent(311);
                    }
                    else
                    {
                        return QuestReply.Event(310); // Please wait
                    }
                })
                .OnEventFinish(Zone.Mhaura, 311, q =>
                {
                    int race = q.Player.Race;
                    int chosenMannequin = Item.HumeMMannequin + race - 1;
                    if (q.Player.FreeSlotCount > 0 && !q.Player.HasItem(chosenMannequin))
                    {
                        if (q.Quest.Complete())
                        {
                            q.Trade.Complete();
                            // local item = player:addItem({ id = chosenMannequin }) if item then item:setExData({ race = race }) end
                            q.Player.AddItemWithExData(chosenMannequin, "race", race);

                            q.Text.Special(Text.Mhaura.ITEM_OBTAINED, chosenMannequin);
                        }
                    }
                    else
                    {
                        q.Text.Special(q.Text.Id(Text.Mhaura.ITEM_CANNOT_BE_OBTAINED) + 4, chosenMannequin);
                    }
                });
        }
    }
}
