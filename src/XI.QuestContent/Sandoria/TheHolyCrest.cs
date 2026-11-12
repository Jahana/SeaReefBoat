// The Holy Crest (Log ID 0, Quest ID 93, the DRG unlock), ported from the quest branches of
// scripts/zones/Port_San_dOria/npcs/Arminibit.lua, scripts/zones/Port_San_dOria/npcs/Ceraulian.lua,
// scripts/zones/Northern_San_dOria/npcs/Morjean.lua and the win finish of
// Battlefields/GhelsbaOutpost/HolyCrest.cs (quest rework B3b, San d'Oria group 15; call-only).
// Arminibit : !pos -0.841 -8 -122.787 232; Ceraulian : !pos 0 -8 -122 232; Morjean : !pos 99 0 116 231 (his
// default 601); Hut_Door : !pos -162 -11 78 140.
// Arminibit, main level 30 or more and this quest available: 24 (else his script's 587); Ceraulian, main level
// ADVANCED_JOB_LEVEL or more and available: 24, first in his chain (Knight Stalker's arms and his 587 after it). 24's
// finish (either NPC: one csid of Port San d'Oria): the char var TheHolyCrest_Event 1. Morjean,
// TheHolyCrest_Event 2 (any status): 65, whose finish begins the quest and sets the var 3. The battlefield (a
// BattlefieldQuest naming this quest: accepted with the Dragon Curse Remedy, or completed; a status read that stays in
// Lua) wins with 32001, whose finish (option the wyvern's name; content:onEventFinishWin, which
// Battlefield.redirectEventCall calls with no npc, calling xi.cs.questEvent) for an option other than 0 with the
// Dragon Curse Remedy held: the remedy taken, DRG unlocked, the wyvern named option + 1, YOU_CAN_NOW_BECOME_A_DRAGOON,
// then npcUtil.completeQuest step for step (Job Gesture: Dragoon, fame 20 San d'Oria, Heir to the Holy Crest,
// TheHolyCrest_Event cleared, the raw player:completeQuest: not quest:complete, whose cleanup would clear the quest's
// own Quest[0][93] vars).
// Stay Lua (char var / key item only, they never name the quest): Novalmauge (Bostaunieux Oubliette: 1: 6 -> 2; 2 with
// theHolyCrestCheck 0: 7 -> the check 1), Morjean's 62 (3 with the Wyvern Egg, or 4; option 0: 4), the
// Maze of Shakhrami Excavation Point (3, no egg, a Pickaxe traded: the Wyvern Egg), Meriphataud Mountains qm1 (4, the
// egg traded: 56 -> 5, the egg taken, 33), Rahal (5 without the remedy: 60 -> the Dragon Curse Remedy; 5 with it: 122).
// Read only: goblinfootprint, roe_records, Moozo-Koozo (the title), the battlefield's entry checks.
// Kept as the Lua has it: 24 reads no var, so a talk to Arminibit or Ceraulian after Novalmauge's 6 sets the var back
// to 1 while the quest is available; 65 and its finish read no status; the 32001 finish with option 0 does nothing (the
// remedy kept, the quest accepted: the fight must be won again), and it reads no status (a completed char's win with a
// GM's remedy completes again).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class TheHolyCrest : QuestScript
    {
        const string CrestVar = "TheHolyCrest_Event";

        // xi.petType.WYVERN (scripts/enum/pet_type.lua)
        const int PetTypeWyvern = 1;

        public TheHolyCrest() : base(QuestLog.Sandoria, "THE_HOLY_CREST") { }

        protected override void Build(QuestScriptBuilder b)
        {
            b.AnyState()
                // Arminibit.lua onTrigger (his else, 587, stays in his script)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Arminibit).Called().OnTrigger(q =>
                {
                    if (q.Player.MainLevel >= 30 &&
                        q.Quest.Status == QuestStatus.Available)
                    {
                        return QuestReply.Event(24);
                    }

                    return null;
                })
                // Ceraulian.lua onTrigger: the first arm (Knight Stalker's arms and his 587 after it)
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Ceraulian).Called().OnTrigger(q =>
                {
                    if (q.Player.MainLevel >= q.Settings.MainNumber("ADVANCED_JOB_LEVEL") &&
                        q.Quest.Status == QuestStatus.Available)
                    {
                        return QuestReply.Event(24);
                    }

                    return null;
                })
                // Morjean.lua onTrigger: the first arm (his 62 stays in his script)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Morjean).Called().OnTrigger(q =>
                {
                    if (q.Player.CharVar(CrestVar) == 2)
                    {
                        return QuestReply.Event(65);
                    }

                    return null;
                });

            b.AnyState()
                // Arminibit.lua / Ceraulian.lua onEventFinish 24
                .OnEventFinish(Zone.PortSanDoria, 24, q =>
                {
                    q.Player.SetCharVar(CrestVar, 1);
                })
                // Morjean.lua onEventFinish 65
                .OnEventFinish(Zone.NorthernSanDoria, 65, q =>
                {
                    q.Quest.Begin();
                    q.Player.SetCharVar(CrestVar, 3);
                })
                // HolyCrest.cs OnEventFinishWin (32001)
                .OnEventFinish(Zone.GhelsbaOutpost, 32001, q =>
                {
                    if (q.Option != 0 &&
                        q.Player.HasKeyItem(KeyItem.DragonCurseRemedy))
                    {
                        q.Player.DelKeyItem(KeyItem.DragonCurseRemedy);
                        q.Player.UnlockJob(Job.Drg);
                        q.Player.SetPetName(PetTypeWyvern, q.Option + 1);
                        q.Text.Special(Text.GhelsbaOutpost.YOU_CAN_NOW_BECOME_A_DRAGOON);
                        // npcUtil.completeQuest(player, SANDORIA, THE_HOLY_CREST, { fame = 20, fameArea = SANDORIA,
                        // title = HEIR_TO_THE_HOLY_CREST, keyItem = JOB_GESTURE_DRAGOON, var = 'TheHolyCrest_Event' }),
                        // step for step: not quest:complete, whose cleanup would clear the quest's own
                        // Quest[0][93] vars
                        q.Player.GiveKeyItem(KeyItem.JobGestureDragoon);
                        q.Player.AddFame(FameArea.Sandoria, 20);
                        q.Player.AddTitle(Title.HeirToTheHolyCrest);
                        q.Player.SetCharVar(CrestVar, 0);
                        q.Player.CompleteQuest(QuestLog.Sandoria, "THE_HOLY_CREST");
                    }
                });
        }
    }
}
