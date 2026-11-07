// Gullible's Travels (Log ID 5), ported from the quest branches of scripts/zones/Kazham/npcs/Magriffon.lua (quest rework
// B3b, Outlands group 7; call-only: the script now calls this quest, then Even More Gullible's Travels). Magriffon walks a
// two-node path in Kazham (his onSpawn stays in his script) : !pos 60.6 -12 -33.913 250.
// Magriffon: accepted: 145 (0, the gil asked: the char var MAGRIFFON_GIL_REQUEST); available and Windurst fame level 6:
// the gil asked drawn (math.randomInt(10, 30) * 1000) and kept in MAGRIFFON_GIL_REQUEST, 144 (0, the gil), whose finish
// on 1 begins the quest. His trade, accepted, at least the gil asked: 146; less: nothing (the script's elseif goes no
// further). 146's finish: the trade confirmed, the gil asked taken (delGil), the var 0, Windurst fame +30, the title
// Gullible's Travels set, the quest completed, needToZone. Every other arm of his script is Even More Gullible's
// Travels' or the script's 143.
// Kept as the Lua has it: each talk while available at fame level 6 draws a new sum; the trade is confirmed and the gil
// taken by delGil (the trade's gil is confirmed, then delGil takes the asked sum); the title is set (setTitle), not
// added.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class GulliblesTravels : QuestScript
    {
        public GulliblesTravels() : base(QuestLog.Outlands, "GULLIBLES_TRAVELS") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Magriffon.lua onTrigger: the Gullible's Travels arms, first in its order
            b.State(QuestStatus.Accepted)
                .Npc(Zone.Kazham, Npc.Kazham.Magriffon).Called()
                    .OnTrigger(q => QuestReply.Event(145, 0, q.Player.CharVar("MAGRIFFON_GIL_REQUEST")))
                    // Magriffon.lua onTrade: the Gullible's Travels arm (the elseif ends the trade either way)
                    .OnTrade(q =>
                    {
                        if (q.Trade.Gil >= q.Player.CharVar("MAGRIFFON_GIL_REQUEST"))
                        {
                            return QuestReply.Event(146);
                        }

                        // the script's if/elseif: no other branch for this status
                        return QuestReply.NoAction();
                    });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Windurst) >= 6)
                .Npc(Zone.Kazham, Npc.Kazham.Magriffon).Called().OnTrigger(q =>
                {
                    int gil = q.Player.RandomInt(10, 30) * 1000;
                    q.Player.SetCharVar("MAGRIFFON_GIL_REQUEST", gil);
                    return QuestReply.Event(144, 0, gil);
                });

            // Magriffon.lua onEventFinish (144, 146)
            b.AnyState()
                .OnEventFinish(Zone.Kazham, 144, q => // Gullible's Travels: First CS
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.Kazham, 146, q => // Gullible's Travels: Final CS
                {
                    q.Trade.Confirm();
                    q.Player.DelGil(q.Player.CharVar("MAGRIFFON_GIL_REQUEST"));
                    q.Player.SetCharVar("MAGRIFFON_GIL_REQUEST", 0);
                    q.Player.AddFame(FameArea.Windurst, 30);
                    q.Player.SetTitle(Title.GulliblesTravels);
                    // the raw player:completeQuest, which keeps the quest's own Quest[5][8] vars
                    q.Player.CompleteQuest(QuestLog.Outlands, "GULLIBLES_TRAVELS");
                    q.Player.SetNeedToZone(true);
                });
        }
    }
}
