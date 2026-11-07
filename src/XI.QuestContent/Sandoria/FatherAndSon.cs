// Father and Son (Log ID 0, Quest ID 4), a line-for-line port of scripts/quests/sandoria/Father_and_Son.lua (quest rework
// B3b, San d'Oria group 3). Ailbeche : !pos 4 -1 24 231; Exoroche : !pos 72 -1 60 230; Helbort : !pos 71 -1 65 230.
// Available: Ailbeche's 508 begins it; Exoroche says EXOROCHE_START (math.randomInt(1, 100) < 30) or one of the six texts
// from EXOROCHE_DIALOG_OFFSET; Helbort's 593 (once per zone, also while accepted). Accepted: Exoroche's 542 (Prog 0 ->
// 1), then EXOROCHE_PLEASE_TELL (progress); Ailbeche's AILBECHE_FATHER_WHERE (progress) until Prog 1, then 509: completed
// (Willow Fishing Rod, fame +20, Lost Child Officer). Completed while Sharpening the Sword is available: exactly the rod
// traded to Ailbeche (without the Family Counselor title) plays 61 (the rod taken, Family Counselor); his trigger shows
// OH_I_WANT_MY_ITEM (npc:showText) with a progress-priority noAction, or with the title AILBECHE_WHEN_FISHING
// (replaceDefault). A Purchase of Arms reads this quest's status; so does Shomo Pochachilo's script (stays).
// The Lua file has no known bug to keep.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class FatherAndSon : QuestScript
    {
        public FatherAndSon() : base(QuestLog.Sandoria, "FATHER_AND_SON") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/Father_and_Son";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Sandoria, Item = Item.WillowFishingRod, Title = Title.LostChildOfficer });

            b.State(QuestStatus.Available)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Exoroche).OnTrigger(q =>
                {
                    if (q.Player.RandomInt(1, 100) < 30)
                    {
                        // quest:message(id) is a Text-type message: the same action as messageText
                        return QuestReply.MessageText(Text.SouthernSanDoria.EXOROCHE_START);
                    }
                    else
                    {
                        // quest:message(EXOROCHE_DIALOG_OFFSET + math.randomInt(0, 5)): a text id plus an offset
                        return QuestReply.Message(q.Text.Id(Text.SouthernSanDoria.EXOROCHE_DIALOG_OFFSET) + q.Player.RandomInt(0, 5));
                    }
                })
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Helbort).OnTrigger(QuestReply.Event(593).OncePerZone())
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Ailbeche).OnTrigger(QuestReply.ProgressEvent(508))
                .OnEventFinish(Zone.NorthernSanDoria, 508, q => q.Quest.Begin());

            b.State(QuestStatus.Accepted)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Ailbeche).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(509);
                    }
                    else
                    {
                        return QuestReply.MessageText(Text.NorthernSanDoria.AILBECHE_FATHER_WHERE).Progress();
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 509, q => q.Quest.Complete())
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Helbort).OnTrigger(QuestReply.Event(593).OncePerZone())
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Exoroche).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(542);
                    }
                    else
                    {
                        return QuestReply.MessageText(Text.SouthernSanDoria.EXOROCHE_PLEASE_TELL).Progress();
                    }
                })
                .OnEventFinish(Zone.SouthernSanDoria, 542, q => q.Quest.SetStep(1));

            b.State(QuestStatus.Completed, when: q =>
                    q.Player.QuestStatus(QuestLog.Sandoria, "SHARPENING_THE_SWORD") == QuestStatus.Available)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Ailbeche)
                    .OnTrade(q =>
                    {
                        if (!q.Player.HasTitle(Title.FamilyCounselor) &&
                            // npcUtil.tradeHasExactly(trade, xi.item.WILLOW_FISHING_ROD)
                            q.Trade.HasExactly(Item.WillowFishingRod))
                        {
                            return QuestReply.ProgressEvent(61);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Player.HasTitle(Title.FamilyCounselor))
                        {
                            return QuestReply.MessageText(Text.NorthernSanDoria.AILBECHE_WHEN_FISHING).ReplaceDefault();
                        }
                        else
                        {
                            // npc:showText(npc, OH_I_WANT_MY_ITEM, xi.item.WILLOW_FISHING_ROD)
                            q.Npc.ShowText(Text.NorthernSanDoria.OH_I_WANT_MY_ITEM, Item.WillowFishingRod);
                            return QuestReply.NoAction().Progress();
                        }
                    })
                .OnEventFinish(Zone.NorthernSanDoria, 61, q =>
                {
                    q.Trade.Confirm();
                    q.Player.AddTitle(Title.FamilyCounselor);
                });
        }
    }
}
