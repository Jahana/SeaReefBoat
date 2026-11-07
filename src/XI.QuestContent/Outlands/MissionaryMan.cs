// Missionary Man (Log ID 5), ported from the quest branches of scripts/zones/Kazham/npcs/Rauteinot.lua (quest rework B3b,
// Outlands group 7; call-only: the script now calls this quest). Rauteinot : !pos -42 -10 -89 250.
// Rauteinot, in the script's order: available and Windurst fame level 3: 137 (0, Slab of Elshimo Marble), whose finish
// on 1 begins the quest and sets MissionaryManVar 1; accepted: MissionaryManVar 1: 138 (0, the marble); 2 or 3: 140; 4:
// 141; completed: 142. The script keeps its 136 otherwise. His trade, MissionaryManVar 1 (any status), one marble and
// one item in all: 139; its finish: MissionaryManVar 2, Rauteinot's Parcel (npcUtil.giveKeyItem), the trade taken. 141's
// finish: the Scroll of Teleport-Yhoat given (npcUtil.giveItem): MissionaryManVar 0, the Sublime Statue taken, Windurst
// fame +30, the quest completed; no room: nothing more.
// Northern San d'Oria's Mulaujeant (the parcel -> the statue, MissionaryManVar 2 -> 3 -> 4) reads and writes the char var
// and key items only: it stays Lua.
// Kept as the Lua has it: the trade reads the char var, not the status; accepted with MissionaryManVar 0 or 5+ falls to
// the script's 136.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class MissionaryMan : QuestScript
    {
        public MissionaryMan() : base(QuestLog.Outlands, "MISSIONARY_MAN") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Rauteinot.lua onTrade
            b.AnyState(when: q => q.Player.CharVar("MissionaryManVar") == 1)
                .Npc(Zone.Kazham, Npc.Kazham.Rauteinot).Called().OnTrade(q =>
                {
                    if (q.Trade.HasItemQty(Item.SlabOfElshimoMarble, 1) &&
                        q.Trade.ItemCount == 1)
                    {
                        return QuestReply.Event(139); // Trading elshimo marble
                    }

                    return null;
                });

            // Rauteinot.lua onTrigger, in its order
            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Windurst) >= 3)
                .Npc(Zone.Kazham, Npc.Kazham.Rauteinot).Called()
                    .OnTrigger(QuestReply.Event(137, 0, Item.SlabOfElshimoMarble)); // Start quest "Missionary Man"

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Kazham, Npc.Kazham.Rauteinot).Called().OnTrigger(q =>
                {
                    int missionaryManVar = q.Player.CharVar("MissionaryManVar");

                    if (missionaryManVar == 1)
                    {
                        return QuestReply.Event(138, 0, Item.SlabOfElshimoMarble); // During quest (before trade marble)
                    }
                    else if (missionaryManVar == 2 || missionaryManVar == 3)
                    {
                        return QuestReply.Event(140); // During quest (after trade marble)
                    }
                    else if (missionaryManVar == 4)
                    {
                        return QuestReply.Event(141); // Finish quest "Missionary Man"
                    }

                    return null;
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.Kazham, Npc.Kazham.Rauteinot).Called().OnTrigger(QuestReply.Event(142)); // New standard dialog

            // Rauteinot.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.Kazham, 137, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Player.SetCharVar("MissionaryManVar", 1);
                    }
                })
                .OnEventFinish(Zone.Kazham, 139, q =>
                {
                    q.Player.SetCharVar("MissionaryManVar", 2);
                    q.Player.GiveKeyItem(KeyItem.RauteinotsParcel);
                    q.Trade.Complete();
                })
                .OnEventFinish(Zone.Kazham, 141, q =>
                {
                    if (q.Player.GiveItem(Item.ScrollOfTeleportYhoat))
                    {
                        q.Player.SetCharVar("MissionaryManVar", 0);
                        q.Player.DelKeyItem(KeyItem.SublimeStatueOfTheGoddess);
                        q.Player.AddFame(FameArea.Windurst, 30);
                        // the raw player:completeQuest, which keeps the quest's own Quest[5][7] vars
                        q.Player.CompleteQuest(QuestLog.Outlands, "MISSIONARY_MAN");
                    }
                });
        }
    }
}
