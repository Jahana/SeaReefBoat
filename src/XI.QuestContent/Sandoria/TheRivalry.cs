// The Rivalry (Log ID 0, Quest ID 75), a line-for-line port of scripts/quests/sandoria/The_Rivalry.lua (quest rework B3b,
// San d'Oria group 6). Gallijaux : !pos -18 -2 -45 232. Available while The Competition is available too: Gallijaux's 300
// (the two carps as arguments; option 700 begins it), Ufanne's 310. Accepted: Gallijaux's GALLIJAUX_CARP_STATUS (the
// char var carpsTraded), Joulet's JOULET_HELP_OTHER_BROTHER, Ufanne's 309 (0, 0, carpsTraded) and 310 in turn (the char
// local var Cycle, shared with The Competition). A trade to Gallijaux of Moat Carp and Forest Carp only: 301 (the carp
// taken, carpsTraded = the old count + the carp, 10 gil a Moat Carp and 15 a Forest Carp, fame +10), or 303 when the
// count reaches 10000 (completed: Lu Shang's Fishing Rod, Testimonial, Carp Diem, fame +10; then the same, without the
// fame). Completed: the same replies, and 301 for every carp trade. The local vars FishTraded and GilToReward carry the
// trade into the finish. The Competition is its mirror (Joulet's side); each needs the other available.
// The carp-status and brother replies are quest:messageName(text, 0, n, 0, 0, true, false): showText's showName and turn
// as the Lua booleans true, false (QuestArg): the NPC does not turn.
// Kept as the Lua has it: a trade must hold carp only; 303 adds no fame beyond the reward's; the char var carpsTraded
// and the char local var Cycle are shared with the other brother's quest.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class TheRivalry : QuestScript
    {
        public TheRivalry() : base(QuestLog.Sandoria, "THE_RIVALRY") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/sandoria/The_Rivalry";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 10,
                FameArea = FameArea.Sandoria,
                Item = Item.LuShangsFishingRod,
                KeyItem = KeyItem.Testimonial,
                Title = Title.CarpDiem,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Sandoria, "THE_COMPETITION") == QuestStatus.Available)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Ufanne).OnTrigger(QuestReply.Event(310))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Gallijaux).OnTrigger(QuestReply.ProgressEvent(300, Item.MoatCarp1, Item.ForestCarp))
                .OnEventFinish(Zone.PortSanDoria, 300, q =>
                {
                    if (q.Option == 700)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Gallijaux)
                    // TODO (Lua): The number that Gallijaux reports increases over time and eventually resets back to 0
                    // before starting to tick back up.
                    .OnTrigger(q => QuestReply.MessageName(Text.PortSanDoria.GALLIJAUX_CARP_STATUS, 0, q.Player.CharVar("carpsTraded"), 0, 0, true, false))
                    .OnTrade(q =>
                    {
                        int count = q.Trade.ItemCount;
                        int moatCarp = q.Trade.ItemQty(Item.MoatCarp1);
                        int forestCarp = q.Trade.ItemQty(Item.ForestCarp);
                        int fishCountVar = q.Player.CharVar("carpsTraded");
                        int totalFish = moatCarp + forestCarp + fishCountVar;

                        if (moatCarp + forestCarp > 0 && moatCarp + forestCarp == count)
                        {
                            if (totalFish >= 10000)
                            {
                                // Used to transfer the variable into oneventfinish
                                q.Player.SetLocalVar("FishTraded", totalFish);
                                // Forces the gil reward to happen AFTER the event like in captures
                                q.Player.SetLocalVar("GilToReward", moatCarp * 10 + forestCarp * 15);
                                return QuestReply.ProgressEvent(303);
                            }
                            else
                            {
                                q.Player.SetLocalVar("FishTraded", totalFish);
                                q.Player.SetLocalVar("GilToReward", moatCarp * 10 + forestCarp * 15);
                                return QuestReply.ProgressEvent(301);
                            }
                        }
                        return null;
                    })
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Joulet).OnTrigger(QuestReply.MessageName(Text.PortSanDoria.JOULET_HELP_OTHER_BROTHER, 0, 0, 0, 0, true, false))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Ufanne).OnTrigger(q =>
                {
                    int fishCount = q.Player.CharVar("carpsTraded");
                    int cycle = q.Player.LocalVar("Cycle");

                    // NPC cycles dialogue
                    if (cycle == 0)
                    {
                        q.Player.SetLocalVar("Cycle", 1);
                        return QuestReply.Event(309, 0, 0, fishCount);
                    }
                    else if (cycle == 1)
                    {
                        q.Player.SetLocalVar("Cycle", 0);
                        return QuestReply.Event(310);
                    }
                    return null;
                })
                .OnEventFinish(Zone.PortSanDoria, 301, q =>
                {
                    q.Trade.Complete();
                    // As per captures this char var persists for the life of the character
                    q.Player.SetCharVar("carpsTraded", q.Player.LocalVar("FishTraded"));
                    // Ensures that gil is rewarded after the CS
                    q.Player.GiveCurrency("gil", q.Player.LocalVar("GilToReward"));
                    q.Player.AddFame(FameArea.Sandoria, 10);
                })
                .OnEventFinish(Zone.PortSanDoria, 303, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Complete();
                        q.Player.SetCharVar("carpsTraded", q.Player.LocalVar("FishTraded"));
                        q.Player.GiveCurrency("gil", q.Player.LocalVar("GilToReward"));
                    }
                });

            // The player may continue to trade carps to Gallijaux even after obtaining Lu Shang.
            b.State(QuestStatus.Completed)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Gallijaux)
                    .OnTrigger(q => QuestReply.MessageName(Text.PortSanDoria.GALLIJAUX_CARP_STATUS, 0, q.Player.CharVar("carpsTraded"), 0, 0, true, false))
                    .OnTrade(q =>
                    {
                        int count = q.Trade.ItemCount;
                        int moatCarp = q.Trade.ItemQty(Item.MoatCarp1);
                        int forestCarp = q.Trade.ItemQty(Item.ForestCarp);
                        int fishCountVar = q.Player.CharVar("carpsTraded");
                        int totalFish = moatCarp + forestCarp + fishCountVar;

                        if (moatCarp + forestCarp > 0 && moatCarp + forestCarp == count)
                        {
                            q.Player.SetLocalVar("FishTraded", totalFish);
                            q.Player.SetLocalVar("GilToReward", moatCarp * 10 + forestCarp * 15);
                            return QuestReply.ProgressEvent(301);
                        }
                        return null;
                    })
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Joulet).OnTrigger(QuestReply.MessageName(Text.PortSanDoria.JOULET_HELP_OTHER_BROTHER, 0, 0, 0, 0, true, false))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Ufanne).OnTrigger(q =>
                {
                    int fishCount = q.Player.CharVar("carpsTraded");
                    int cycle = q.Player.LocalVar("Cycle");

                    // NPC cycles dialogue
                    if (cycle == 0)
                    {
                        q.Player.SetLocalVar("Cycle", 1);
                        return QuestReply.Event(309, 0, 0, fishCount);
                    }
                    else if (cycle == 1)
                    {
                        q.Player.SetLocalVar("Cycle", 0);
                        return QuestReply.Event(310);
                    }
                    return null;
                })
                .OnEventFinish(Zone.PortSanDoria, 301, q =>
                {
                    q.Trade.Complete();
                    q.Player.SetCharVar("carpsTraded", q.Player.LocalVar("FishTraded"));
                    q.Player.GiveCurrency("gil", q.Player.LocalVar("GilToReward"));
                    q.Player.AddFame(FameArea.Sandoria, 10);
                });
        }
    }
}
