// Wonder Wands (Log ID 2, Quest ID 48), ported from scripts/zones/Port_Windurst/npcs/Hakkuru-Rinkuru.lua (every branch
// of his script is this quest's, so his script only calls it) and the quest branches of Goltata.lua and
// Yaman-Hachuman.lua (quest rework B3b, Windurst group 21; call-only). Hakkuru-Rinkuru : !pos -111 -4 101 240; Goltata
// : Port Windurst; Yaman-Hachuman : !pos -101.209 -4.25 110.886 240. Hakkuru-Rinkuru has a default action (event 224,
// Port_Windurst/DefaultActions.lua); Making Amends (C#) answers him from its own container (once it is completed, 281,
// replaceDefault, taking turns with his script; a script talk that starts nothing plays that event again). Lure of the
// Wildcat (C#) answers Yaman-Hachuman while it is accepted. Kuroido-Moido's and Ohruru's Wonder Wands lines are in
// Making Amens! and Catch It If You Can (they sit inside those quests' arms).
// Hakkuru-Rinkuru: Making Amends and Making Amens! completed, this quest available, Windurst fame level 5 and no
// needToZone: 259 (option 1: begun). Accepted: 260. Completed with SecondRewardVar 1: 267. Else nothing. The trade,
// accepted: an Oak Staff, a Mythril Rod and a Rose Wand and nothing else (hasItemQty each, getItemCount 3):
// 265 (0, the three); else 260 (0, the three).
// 265's finish: no free slot: ITEM_CANNOT_BE_OBTAINED (the armlets); else SecondRewardVar 1, the trade taken, 4800 gil
// (npcUtil.giveCurrency), the New Moon Armlets (ITEM_OBTAINED), Windurst fame 50, Doctor Shantotto's Guinea Pig,
// completed.
// 267's finish: math.randomInt(1, 3) picks two of the three back (1: the staff and the rod, 2: the staff and the wand,
// 3: the rod and the wand); one free slot: ITEM_CANNOT_BE_OBTAINED for the second; none: for both; else both given
// (ITEM_OBTAINED each) and SecondRewardVar 0. The Lua's nine arms (three rolls times three slot counts) fold into the
// roll's two items: each arm makes the same calls in the same order.
// Goltata: accepted 257 (0, 0, Oak Staff), completed 269, else his 232. Yaman-Hachuman: accepted 256 (0, 0, 0, Mythril
// Rod), completed 268, else his 233 (both standards kept in their scripts).
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): SecondRewardVar is set in 265's finish
// once the space check passes; the Lua's trade set it before 265, so a turn-in refused for space left it set on an
// accepted quest. Kept as the Lua has it: 267 with one free slot gives nothing (the message names the second item
// only); 260 on the trigger has no arguments, the trade's has the three; the finishes read no status.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class WonderWands : QuestScript
    {
        const string SecondRewardVar = "SecondRewardVar";

        public WonderWands() : base(QuestLog.Windurst, "WONDER_WANDS") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Hakkuru-Rinkuru.lua onTrigger: both quests before it completed, available, fame 5, no needToZone
            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Windurst, "MAKING_AMENDS") == QuestStatus.Completed &&
                    q.Player.QuestStatus(QuestLog.Windurst, "MAKING_AMENS") == QuestStatus.Completed &&
                    q.Player.FameLevel(FameArea.Windurst) >= 5 &&
                    !q.Player.NeedToZone)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.HakkuruRinkuru).Called().OnTrigger(QuestReply.Event(259));

            // accepted: the reminder; the trade; Goltata's and Yaman-Hachuman's lines
            b.State(QuestStatus.Accepted)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.HakkuruRinkuru).Called()
                    .OnTrigger(QuestReply.Event(260))
                    // Hakkuru-Rinkuru.lua onTrade: the three, one each, nothing else: the completion; else the items
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasItemQty(Item.OakStaff, 1) &&
                            q.Trade.HasItemQty(Item.MythrilRod, 1) &&
                            q.Trade.HasItemQty(Item.RoseWand, 1) &&
                            q.Trade.ItemCount == 3)
                        {
                            // SecondRewardVar: set in 265's finish (fixed, user ruling 2026-11-04)
                            return QuestReply.Event(265, 0, Item.OakStaff, Item.MythrilRod, Item.RoseWand);
                        }

                        return QuestReply.Event(260, 0, Item.OakStaff, Item.MythrilRod, Item.RoseWand);
                    })
                // Goltata.lua onTrigger
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Goltata).Called()
                    .OnTrigger(QuestReply.Event(257, 0, 0, Item.OakStaff))
                // Yaman-Hachuman.lua onTrigger
                .Npc(Zone.PortWindurst, Npc.PortWindurst.YamanHachuman).Called()
                    .OnTrigger(QuestReply.Event(256, 0, 0, 0, Item.MythrilRod));

            // completed, SecondRewardVar 1: the second reward
            b.State(QuestStatus.Completed, when: q => q.Player.CharVar(SecondRewardVar) == 1)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.HakkuruRinkuru).Called().OnTrigger(QuestReply.Event(267));

            // completed: Goltata's and Yaman-Hachuman's lines
            b.State(QuestStatus.Completed)
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Goltata).Called().OnTrigger(QuestReply.Event(269))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.YamanHachuman).Called().OnTrigger(QuestReply.Event(268));

            // Hakkuru-Rinkuru.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.PortWindurst, 259, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.PortWindurst, 267, q =>
                {
                    // two back by the roll: 1 the staff and the rod, 2 the staff and the wand, 3 the rod and the wand
                    int rand = q.Player.RandomInt(1, 3);
                    int first = rand == 3 ? Item.MythrilRod : Item.OakStaff;
                    int second = rand == 1 ? Item.MythrilRod : Item.RoseWand;

                    if (q.Player.FreeSlotCount == 1)
                    {
                        q.Text.Special(Text.PortWindurst.ITEM_CANNOT_BE_OBTAINED, second);
                    }
                    else if (q.Player.FreeSlotCount == 0)
                    {
                        q.Text.Special(Text.PortWindurst.ITEM_CANNOT_BE_OBTAINED, first);
                        q.Text.Special(Text.PortWindurst.ITEM_CANNOT_BE_OBTAINED, second);
                    }
                    else
                    {
                        q.Player.AddItem(first, 1);
                        q.Player.AddItem(second, 1);
                        q.Text.Special(Text.PortWindurst.ITEM_OBTAINED, first);
                        q.Text.Special(Text.PortWindurst.ITEM_OBTAINED, second);
                        q.Player.SetCharVar(SecondRewardVar, 0);
                    }
                })
                .OnEventFinish(Zone.PortWindurst, 265, q =>
                {
                    if (q.Player.FreeSlotCount == 0)
                    {
                        q.Text.Special(Text.PortWindurst.ITEM_CANNOT_BE_OBTAINED, Item.NewMoonArmlets);
                    }
                    else
                    {
                        // the Lua's trade set it before 265, whatever the space (fixed, user ruling 2026-11-04)
                        q.Player.SetCharVar(SecondRewardVar, 1);
                        q.Trade.Complete();
                        q.Player.GiveCurrency("gil", 4800);
                        q.Player.AddItem(Item.NewMoonArmlets);
                        q.Text.Special(Text.PortWindurst.ITEM_OBTAINED, Item.NewMoonArmlets);
                        q.Player.AddFame(FameArea.Windurst, 50);
                        q.Player.AddTitle(Title.DoctorShantottosGuineaPig);
                        // the raw player:completeQuest: not quest:complete, whose cleanup would clear the quest's own
                        // Quest[2][48] vars
                        q.Player.CompleteQuest(QuestLog.Windurst, "WONDER_WANDS");
                    }
                });
        }
    }
}
