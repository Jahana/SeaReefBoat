// Unbridled Passion (Log ID 2, Quest ID 74; RNG AF3), ported from the Unbridled Passion branches of
// scripts/zones/Windurst_Woods/npcs/Perih_Vashai.lua (quest rework B3b, Windurst group 29; call-only). Perih Vashai :
// !pos 117 -3 92 241.
// Perih Vashai, after Fire and Brimstone's arms (its own C#, called first): Fire and Brimstone completed, main job RNG,
// main level >= AF3_QUEST_LEVEL and this quest available: 541 (0, 13360) (its finish, any option: begun,
// unbridledPassion 1); unbridledPassion 1-6: 542; 7: 546 (0, Hunter's Socks); its finish: npcUtil.completeQuest's
// steps (Hunter's Socks, stopping when they cannot be given; Windurst fame 20; the var cleared; the raw
// player:completeQuest, so the quest's Quest[2][74] vars stay); given: Koh's Letter taken. Else nothing (the NPC's
// default 338).
// 13360 is the Lua's literal (a Twinstone Earring); 546's 14099 is its literal for Hunter's Socks.
// The steps between (Koh Lenbalalako in Mhaura 1 -> 3 and Koh's Letter, the Xarcabard zone-in 3 -> 4,
// Koenigstiger 4 -> 5, the Xarcabard ???s 5 -> 7) read and write only the var and the key item, never the quest: they
// stay in Lua.
// Kept as the Lua has it: the var arms read no status (a GM's var on any char plays them, and 546 completes an
// available quest); 541 begins on any option; 546's finish reads no status; 546 refused (no room, or the rare socks
// held on a completed char's replay) keeps the var 7, the quest's status and Koh's Letter.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class UnbridledPassion : QuestScript
    {
        const string Cs = "unbridledPassion";

        public UnbridledPassion() : base(QuestLog.Windurst, "UNBRIDLED_PASSION") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Perih_Vashai.lua onTrigger, the UNBRIDLED PASSION arms
            b.AnyState()
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.PerihVashai).Called().OnTrigger(q =>
                {
                    // RNG AF2
                    QuestStatus fireAndBrimstone = q.Player.QuestStatus(QuestLog.Windurst, "FIRE_AND_BRIMSTONE");
                    QuestStatus unbridledPassion = q.Quest.Status; // RNG AF3
                    int unbridledPassionCS = q.Player.CharVar(Cs);
                    int lvl = q.Player.MainLevel;
                    int job = q.Player.MainJob;

                    if (fireAndBrimstone == QuestStatus.Completed &&
                        job == Job.Rng &&
                        lvl >= q.Settings.MainNumber("AF3_QUEST_LEVEL") &&
                        unbridledPassion == QuestStatus.Available)
                    {
                        return QuestReply.Event(541, 0, Item.TwinstoneEarring); // start RNG AF3
                    }
                    else if (unbridledPassionCS > 0 && unbridledPassionCS < 7)
                    {
                        return QuestReply.Event(542); // during RNG AF3
                    }
                    else if (unbridledPassionCS == 7)
                    {
                        return QuestReply.Event(546, 0, Item.HuntersSocks); // complete RNG AF3
                    }

                    return null;
                });

            // Perih_Vashai.lua onEventFinish, the UNBRIDLED PASSION arms
            b.AnyState()
                .OnEventFinish(Zone.WindurstWoods, 541, q =>
                {
                    q.Quest.Begin(); // start RNG AF3
                    q.Player.SetCharVar(Cs, 1);
                })
                .OnEventFinish(Zone.WindurstWoods, 546, q =>
                {
                    // npcUtil.completeQuest with Hunter's Socks (14099), fame 20, the fameArea WINDURST and the var
                    // unbridledPassion, step for step: not quest:complete, whose cleanup would clear the quest's own
                    // Quest[2][74] vars. It returns false, with nothing more done, when the socks cannot be given
                    if (!q.Player.GiveItem(Item.HuntersSocks))
                    {
                        return;
                    }
                    q.Player.AddFame(FameArea.Windurst, 20);
                    q.Player.SetCharVar(Cs, 0);
                    q.Player.CompleteQuest(QuestLog.Windurst, "UNBRIDLED_PASSION");
                    q.Player.DelKeyItem(KeyItem.KohsLetter); // complete quest RNG AF3
                });
        }
    }
}
