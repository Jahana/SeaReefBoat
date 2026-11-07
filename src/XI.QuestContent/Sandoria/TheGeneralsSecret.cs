// The General's Secret (Log ID 0, Quest ID 60), a line-for-line port of scripts/quests/sandoria/The_Generals_Secret.lua
// (quest rework B3b, San d'Oria group 4; the Lua header names it "A Sentry's Peril", a copy slip). Curilla : !pos 27 0.1
// 0.1 233; Hot Springs : !pos 444 -37 -18 139. Offered at San d'Oria fame level 2: Curilla's 55 (option 1: begun,
// Curilla's Bottle, empty). Accepted: Curilla's 53, or 54 with the full bottle (completed: Lynx Baghnakhs, fame +20, the
// full bottle deleted). The Hot Springs in Horlais Peak swap the empty bottle for the full one. No outside reference.
// Kept as the Lua has it: 53 has no finish handler.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class TheGeneralsSecret : QuestScript
    {
        public TheGeneralsSecret() : base(QuestLog.Sandoria, "THE_GENERALS_SECRET") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/The_Generals_Secret";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Fame = 20, FameArea = FameArea.Sandoria, Item = Item.LynxBaghnakhs });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Sandoria) >= 2)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Curilla).OnTrigger(QuestReply.ProgressEvent(55))
                .OnEventFinish(Zone.ChateauDoraguille, 55, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.CurillasBottleEmpty);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.ChateauDoraguille, Npc.ChateauDoraguille.Curilla).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.CurillasBottleFull))
                    {
                        return QuestReply.ProgressEvent(54);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(53);
                    }
                })
                .OnEventFinish(Zone.ChateauDoraguille, 54, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.CurillasBottleFull);
                    }
                })
                .Npc(Zone.HorlaisPeak, Npc.HorlaisPeak.HotSprings).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.CurillasBottleEmpty))
                    {
                        q.Player.DelKeyItem(KeyItem.CurillasBottleEmpty);
                        return QuestReply.KeyItem(KeyItem.CurillasBottleFull);
                    }
                    return null;
                });
        }
    }
}
