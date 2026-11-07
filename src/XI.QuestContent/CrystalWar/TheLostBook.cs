// The Lost Book (Log ID 7, Quest ID 26), a line-for-line port of scripts/quests/crystalWar/The_Lost_Book.lua (quest
// rework B3b Crystal War group 2). Windurst Waters [S]: Door:Rhinostery (South) !pos -1 -4 -198, Door:Optistery !pos -57
// -5 89. Giddeus: Quu Bokye !pos -159 16 181. Castle Oztroja [S]: _qm0 !pos -141 1 -9.
// Available, main level 30+ and the Bronze Ribbon of Service: the Rhinostery door 143, whose finish begins it and sets
// Prog 1. Prog 1: the door 148 (optional dialogue, no finish); Quu Bokye: the Mythril Beastcoin alone: 65, whose finish
// confirms the trade, gives the Leather-bound Book, Prog 2; a talk: 64. Prog 2: the door 144 (-> Prog 3). Prog 3: the
// Optistery door 145, whose finish takes the book, Prog 4; the Rhinostery door 150 (an Event). Prog 4 without the Lynx
// Pelt: _qm0 gives it (npcUtil.giveKeyItem) and answers nil. Prog 4 with the pelt: the Optistery door: a Sheet of Vellum
// alone: 146, whose finish confirms the trade, takes the pelt, Prog 5, Timer = VanadielUniqueDay() + 1. Prog 5 with the
// day turned: the Optistery door 147, whose finish completes it (a Scroll of Retrace).
// Kept as the Lua has it: 143 begins on any option; 148 and 64 are progress events with no finish; _qm0's handler gives
// the key item and returns nil (_qm0 has no default: the trigger answers nothing more); Prog 5 before the day turns has
// no section; 147 completes with no check of its result; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class TheLostBook : QuestScript
    {
        public TheLostBook() : base(QuestLog.CrystalWar, "THE_LOST_BOOK") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/The_Lost_Book";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4, 5);

            b.Reward(new QuestReward
            {
                Item = Item.ScrollOfRetrace,
            });

            // Examine the right Rhinostery door (J-9 of the second map) for a cutscene.
            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainLevel >= 30 &&
                    q.Player.HasKeyItem(KeyItem.BronzeRibbonOfService))
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.DoorRhinosterySouth).OnTrigger(QuestReply.ProgressEvent(143))
                .OnEventFinish(Zone.WindurstWatersS, 143, q =>
                {
                    q.Quest.Begin();
                    q.Quest.SetStep(1);
                });

            // Bring a Mythril Beastcoin and travel to present-day Giddeus and trade Quu Bokye
            // a Mythril Beastcoin to obtain a Leather-bound Book.
            b.State(QuestStatus.Accepted, step: 1)
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.DoorRhinosterySouth).OnTrigger(QuestReply.ProgressEvent(148)) // Optional dialogue
                .Npc(Zone.Giddeus, Npc.Giddeus.QuuBokye).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.MythrilBeastcoin))
                    {
                        return QuestReply.ProgressEvent(65);
                    }
                    return null;
                })
                .Npc(Zone.Giddeus, Npc.Giddeus.QuuBokye).OnTrigger(QuestReply.ProgressEvent(64))
                .OnEventFinish(Zone.Giddeus, 65, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.LeatherBoundBook);
                    q.Quest.SetStep(2);
                });

            // Return to the Rhinostery in Windurst (S), and click the southern door for another cutscene.
            b.State(QuestStatus.Accepted, step: 2)
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.DoorRhinosterySouth).OnTrigger(QuestReply.ProgressEvent(144))
                .OnEventFinish(Zone.WindurstWatersS, 144, q =>
                {
                    q.Quest.SetStep(3);
                });

            // Go to the Optistery (F-8 on the first map) of Windurst (S) and examine the door for a cutscene.
            b.State(QuestStatus.Accepted, step: 3)
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.DoorOptistery).OnTrigger(QuestReply.ProgressEvent(145))
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.DoorRhinosterySouth).OnTrigger(QuestReply.Event(150)) // Optional dialogue
                .OnEventFinish(Zone.WindurstWatersS, 145, q =>
                {
                    q.Player.DelKeyItem(KeyItem.LeatherBoundBook);
                    q.Quest.SetStep(4);
                });

            // Head to Castle Oztroja (S) and examine the ??? at (G-8) on the first map.
            b.State(QuestStatus.Accepted, step: 4, when: q => !q.Player.HasKeyItem(KeyItem.LynxPelt))
                .Npc(Zone.CastleOztrojaS, Npc.CastleOztrojaS.qm0).OnTrigger(q =>
                {
                    q.Player.GiveKeyItem(KeyItem.LynxPelt);
                    return null;
                });

            // Trade a sheet of Vellum to the Optistery door in Windurst Waters (S).
            b.State(QuestStatus.Accepted, step: 4, when: q => q.Player.HasKeyItem(KeyItem.LynxPelt))
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.DoorOptistery).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.SheetOfVellum))
                    {
                        return QuestReply.ProgressEvent(146);
                    }
                    return null;
                })
                .OnEventFinish(Zone.WindurstWatersS, 146, q =>
                {
                    q.Trade.Confirm();
                    q.Player.DelKeyItem(KeyItem.LynxPelt);
                    q.Quest.SetStep(5);
                    q.Quest.SetVar("Timer", q.Player.VanadielUniqueDay + 1);
                });

            // Waited game day
            b.State(QuestStatus.Accepted, step: 5, when: q => q.Quest.Var("Timer") <= q.Player.VanadielUniqueDay)
                .Npc(Zone.WindurstWatersS, Npc.WindurstWatersS.DoorOptistery).OnTrigger(QuestReply.ProgressEvent(147))
                .OnEventFinish(Zone.WindurstWatersS, 147, q =>
                {
                    q.Quest.Complete();
                });
        }
    }
}
