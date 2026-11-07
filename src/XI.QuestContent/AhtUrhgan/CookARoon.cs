// Cook-a-roon (Log ID 6, Quest ID 15), a line-for-line port of scripts/quests/ahtUrhgan/Cook-a-roon.lua (quest rework
// B3b Aht Urhgan, draft). Nashmau: Ququroon !pos -2.4 0 66.8 (no script; default event 241, the offer's own csid).
// Available: Ququroon 241, finished with option 1: begun. Accepted: Ququroon 242 (an Event); a trade of exactly the
// five fish (Ahtapot, Istakoz, Istavrit, Istiridye, Mercanbaligi): Prog = randomInt(2, 3), 243 with [7] = Prog; 243's
// finish gives a Bowl of Nashmau Stew at Prog 2, then completes it (no reward table) and confirms the trade. Completed:
// Ququroon 244 (an Event); the same fish trade: Prog = randomInt(2, 3), 243 ([7] = Prog); 243's update (not its finish)
// confirms the trade, gives the stew at Prog 2 and sets Prog 0. Kept as the Lua has it: 241 begins on option 1 only;
// the accepted trade's Prog is drawn in the trade handler (before the lookup picks an action) and read back at the
// finish; the completed section confirms the trade in onEventUpdate, so a 243 finished with no update takes nothing;
// the stew given in that update is not checked; no onTrade while available.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class CookARoon : QuestScript
    {
        public CookARoon() : base(QuestLog.AhtUrghan, "COOK_A_ROON") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/Cook-a-roon";

        // npcUtil.tradeHasExactly(trade, { AHTAPOT, ISTAKOZ, ISTAVRIT_1, ISTIRIDYE, MERCANBALIGI })
        static bool HasTheFish(IQuestContext q) =>
            q.Trade.HasExactly(Item.Ahtapot, Item.Istakoz, Item.Istavrit1, Item.Istiridye, Item.Mercanbaligi);

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(0, 2, 3);

            // Setion: Quest available
            b.State(QuestStatus.Available)
                .Npc(Zone.Nashmau, Npc.Nashmau.Ququroon).OnTrigger(q => QuestReply.ProgressEvent(241))
                .OnEventFinish(Zone.Nashmau, 241, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            // Section: Quest accepted
            b.State(QuestStatus.Accepted)
                .Npc(Zone.Nashmau, Npc.Nashmau.Ququroon).OnTrigger(q => QuestReply.Event(242))
                .OnTrade(q =>
                {
                    if (HasTheFish(q))
                    {
                        // quest:setVar(player, 'Prog', math.randomInt(2, 3)), then [7] = quest:getVar(player, 'Prog'):
                        // the value set
                        int drawn = q.Player.RandomInt(2, 3);
                        q.Quest.SetStep(drawn);
                        return QuestReply.ProgressEvent(243).Option(7, drawn);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Nashmau, 243, q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        q.Player.GiveItem(Item.BowlOfNashmauStew);
                    }

                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                    }
                });

            // Section: Quest complete
            b.State(QuestStatus.Completed)
                .Npc(Zone.Nashmau, Npc.Nashmau.Ququroon).OnTrade(q =>
                {
                    if (HasTheFish(q))
                    {
                        // quest:setVar(player, 'Prog', math.randomInt(2, 3)), then [7] = quest:getVar(player, 'Prog'):
                        // the value set
                        int drawn = q.Player.RandomInt(2, 3);
                        q.Quest.SetStep(drawn);
                        return QuestReply.ProgressEvent(243).Option(7, drawn);
                    }
                    return null;
                })
                .OnTrigger(q => QuestReply.Event(244))
                .OnEventUpdate(Zone.Nashmau, 243, q =>
                {
                    q.Trade.Confirm();
                    if (q.Quest.Step == 2)
                    {
                        q.Player.GiveItem(Item.BowlOfNashmauStew);
                    }

                    q.Quest.SetStep(0);
                });
        }
    }
}
