// Twenty in Pirate Years (Log ID 5), ported from the quest branches of scripts/zones/Norg/npcs/Ryoma.lua and
// scripts/zones/Port_Bastok/npcs/Ensetsu.lua (quest rework B3b, Outlands group 8; call-only: the scripts now call this
// quest; Ryoma's calls it before True Will). Ryoma : !pos -23 0 -9 252; Ensetsu : !pos 32 -7 69 236. Both have a default
// (DefaultActions: Ryoma 94, Ensetsu 27) that the lookup alternates with their script: every second talk runs the script.
// Ryoma, first in his script: available, main job NIN and level AF1_QUEST_LEVEL: 133 (its finish begins the quest and
// sets the char var twentyInPirateYearsCS 1, whatever the option); accepted with the Trick Box: 134. 134's finish: one
// free slot or none: ITEM_CANNOT_BE_OBTAINED (Anju); else the box taken, the Anju and the Zushio
// (addItem, ITEM_OBTAINED each), the needToZone flag set, the var 0, Norg fame +20, the quest completed.
// Ensetsu: Ayame and Kaede completed and this quest available: 247; else twentyInPirateYearsCS 2: 262 (its finish: 3);
// 4: 263.
// The rest stays in Lua (char var and key item only): Port Bastok's Kagetora (261: CS 1 -> 2), Eastern Altepa's qm2
// (the Tsuchigumo, CS 3 -> 4 and the Trick Box) and the Tsuchigumo's death count; Port Bastok's Dalba (a status read for
// his past-event menu).
// Kept as the Lua has it: 133 begins the quest on any option; 134 needs two free slots (getFreeSlotsCount() <= 1
// refuses).
// A deliberate change from the Lua (user ruling 2026-11-05, kept bugs): Ryoma.lua's 134 calls player:needToZone() with
// no argument, a read whose answer is dropped, so nothing was set and I'll Take the Big Box (which waits for
// needToZone to clear) was offered at once; the C# sets the flag (needToZone(true)), so the offer waits for a zone.
// A deliberate change from the Lua (user ruling 2026-11-04, kept Lua bugs): Ryoma.lua's start reads the literal 40, the
// C# reads AF1_QUEST_LEVEL as the other AF1 quests do.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class TwentyInPirateYears : QuestScript
    {
        public TwentyInPirateYears() : base(QuestLog.Outlands, "TWENTY_IN_PIRATE_YEARS") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Ryoma.lua onTrigger: this quest's two arms, first in his order
            // the Lua's literal 40 read as AF1_QUEST_LEVEL (see the header)
            b.State(QuestStatus.Available, when: q =>
                    q.Player.MainJob == Job.Nin && q.Player.MainLevel >= q.Settings.MainNumber("AF1_QUEST_LEVEL"))
                .Npc(Zone.Norg, Npc.Norg.Ryoma).Called().OnTrigger(QuestReply.Event(133)); // Start Quest "20 in Pirate Years"

            b.State(QuestStatus.Accepted, when: q => q.Player.HasKeyItem(KeyItem.TrickBox))
                .Npc(Zone.Norg, Npc.Norg.Ryoma).Called().OnTrigger(QuestReply.Event(134)); // Finish Quest "20 in Pirate Years"

            // Ensetsu.lua onTrigger, in its order
            b.State(QuestStatus.Available, when: q =>
                    q.Player.QuestStatus(QuestLog.Bastok, "AYAME_AND_KAEDE") == QuestStatus.Completed)
                .Npc(Zone.PortBastok, Npc.PortBastok.Ensetsu).Called().OnTrigger(QuestReply.Event(247));

            b.AnyState(when: q => q.Player.CharVar("twentyInPirateYearsCS") == 2)
                .Npc(Zone.PortBastok, Npc.PortBastok.Ensetsu).Called().OnTrigger(QuestReply.Event(262));

            b.AnyState(when: q => q.Player.CharVar("twentyInPirateYearsCS") == 4)
                .Npc(Zone.PortBastok, Npc.PortBastok.Ensetsu).Called().OnTrigger(QuestReply.Event(263));

            // Ryoma.lua onEventFinish (133, 134), Ensetsu.lua onEventFinish (262)
            b.AnyState()
                .OnEventFinish(Zone.Norg, 133, q =>
                {
                    q.Quest.Begin();
                    q.Player.SetCharVar("twentyInPirateYearsCS", 1);
                })
                .OnEventFinish(Zone.Norg, 134, q =>
                {
                    if (q.Player.FreeSlotCount <= 1)
                    {
                        q.Text.Special(Text.Norg.ITEM_CANNOT_BE_OBTAINED, Item.Anju);
                    }
                    else
                    {
                        q.Player.DelKeyItem(KeyItem.TrickBox);
                        q.Player.AddItem(Item.Anju);
                        q.Player.AddItem(Item.Zushio);
                        q.Text.Special(Text.Norg.ITEM_OBTAINED, Item.Anju); // Anju
                        q.Text.Special(Text.Norg.ITEM_OBTAINED, Item.Zushio); // Zushio
                        // the Lua's player:needToZone() set nothing; needToZone(true) (user ruling 2026-11-05)
                        q.Player.SetNeedToZone(true);
                        q.Player.SetCharVar("twentyInPirateYearsCS", 0);
                        q.Player.AddFame(FameArea.Norg, 20);
                        // the raw player:completeQuest, which keeps the quest's own Quest[5][143] vars
                        q.Player.CompleteQuest(QuestLog.Outlands, "TWENTY_IN_PIRATE_YEARS");
                    }
                })
                .OnEventFinish(Zone.PortBastok, 262, q =>
                {
                    q.Player.SetCharVar("twentyInPirateYearsCS", 3);
                });
        }
    }
}
