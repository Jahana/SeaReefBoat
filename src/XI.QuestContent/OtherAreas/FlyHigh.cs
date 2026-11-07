// Fly High (Log ID 4, Quest ID 71), ported from scripts/zones/Tavnazian_Safehold/npcs/Ferchinne.lua (quest rework B3b,
// Other Areas group 12; call-only: every branch of her script is this quest's, so her script only calls it). Ferchinne
// (her DefaultActions 240).
// Trigger, CoP current mission The Savage or that mission completed: available: 241 (its finish: begun); accepted: 242;
// completed: 244. Else nothing (her default).
// Trade, 2 Hippogryph Tailfeathers (npcUtil.tradeHas): accepted: 243 (its finish: npcUtil.completeQuest with the
// Mistmelt, step for step: the Mistmelt, then the raw player:completeQuest, which keeps the quest's own Quest[4][71]
// vars; done: the feathers taken); completed: 245 (its finish: npcUtil.giveItem the Mistmelt; given: the feathers
// taken).
// Kept as the Lua has it: 241 begins with no option read; the trade reads no mission; 243 / 245's finishes read no
// status.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class FlyHigh : QuestScript
    {
        public FlyHigh() : base(QuestLog.OtherAreas, "FLY_HIGH") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Ferchinne).Called()
                    // Ferchinne.lua onTrade
                    .OnTrade(q =>
                    {
                        QuestStatus flyHigh = q.Quest.Status;

                        if (flyHigh == QuestStatus.Accepted &&
                            q.Trade.HasQty((Item.HippogryphTailfeather, 2)))
                        {
                            // 2x Hippogryph Tailfeather
                            return QuestReply.Event(243);
                        }
                        else if (flyHigh == QuestStatus.Completed &&
                            q.Trade.HasQty((Item.HippogryphTailfeather, 2)))
                        {
                            // 2x Hippogryph Tailfeather
                            return QuestReply.Event(245);
                        }

                        return null;
                    })
                    // Ferchinne.lua onTrigger
                    .OnTrigger(q =>
                    {
                        if (q.Player.CurrentMission(MissionLog.Cop) == Mission.Cop.TheSavage ||
                            q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.TheSavage))
                        {
                            QuestStatus flyHigh = q.Quest.Status;

                            if (flyHigh == QuestStatus.Available)
                            {
                                return QuestReply.Event(241);
                            }
                            else if (flyHigh == QuestStatus.Accepted)
                            {
                                return QuestReply.Event(242);
                            }
                            else if (flyHigh == QuestStatus.Completed)
                            {
                                return QuestReply.Event(244);
                            }
                        }

                        return null; // her default 240
                    });

            b.AnyState()
                // Ferchinne.lua onEventFinish 241 / 243 / 245
                .OnEventFinish(Zone.TavnazianSafehold, 241, q =>
                {
                    q.Quest.Begin();
                })
                .OnEventFinish(Zone.TavnazianSafehold, 243, q =>
                {
                    // npcUtil.completeQuest(player, OTHER_AREAS, FLY_HIGH, { item = MISTMELT }), step for step: not
                    // quest:complete, whose cleanup would clear the quest's own Quest[4][71] vars
                    if (q.Player.GiveItem(Item.Mistmelt)) // Mistmelt; false: npcUtil.completeQuest returns false
                    {
                        q.Player.CompleteQuest(QuestLog.OtherAreas, "FLY_HIGH");
                        q.Trade.Confirm();
                    }
                })
                .OnEventFinish(Zone.TavnazianSafehold, 245, q =>
                {
                    if (q.Player.GiveItem(Item.Mistmelt)) // Mistmelt
                    {
                        q.Trade.Confirm();
                    }
                });
        }
    }
}
