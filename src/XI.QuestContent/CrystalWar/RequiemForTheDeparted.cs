// Requiem for the Departed (Log ID 7, Quest ID 23), ported from the quest branches of scripts/zones/
// The_Eldieme_Necropolis_[S]/npcs/Heptachiond.lua and scripts/zones/Fort_Karugo-Narugo_[S]/npcs/Pecca-Pocca.lua (quest
// rework B3b Crystal War, draft; call-only). Heptachiond !pos 256.196 -31.999 20.073 175; Pecca-Pocca !pos -162.978
// -67.999 -155.099 96.
// Heptachiond, in his order: available, the Bronze Ribbon of Service and level 30+: 105 (finish: the quest added);
// accepted: the Sheaf of Handmade Incense: 107 (finish: npcUtil.completeQuest: the Scroll of Recall-Meriph; done: the
// incense taken); else 106; completed: 108; else his script's 104. Pecca-Pocca: accepted without the incense: 234
// (finish: the incense); else her script's 233 (accepted with the incense, or any other status).
// Kept as the Lua has it: 234 gives another incense if the first is lost; the finishes read no status.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class RequiemForTheDeparted : QuestScript
    {
        public RequiemForTheDeparted() : base(QuestLog.CrystalWar, "REQUIEM_FOR_THE_DEPARTED") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Heptachiond.lua onTrigger, in its order
            b.AnyState()
                .Npc(Zone.TheEldiemeNecropolisS, Npc.TheEldiemeNecropolisS.Heptachiond).Called().OnTrigger(q =>
                {
                    QuestStatus rftd = q.Quest.Status;

                    // Change to BRASS_RIBBON_OF_SERVICE later when Campaign has been added.
                    if (rftd == QuestStatus.Available &&
                        q.Player.HasKeyItem(KeyItem.BronzeRibbonOfService) &&
                        q.Player.MainLevel >= 30)
                    {
                        return QuestReply.Event(105); // Start quest "Requiem for the Departed"
                    }
                    else if (rftd == QuestStatus.Accepted)
                    {
                        if (q.Player.HasKeyItem(KeyItem.SheafOfHandmadeIncense))
                        {
                            return QuestReply.Event(107); // During quest (with Handmade Incense KI)
                        }
                        else
                        {
                            return QuestReply.Event(106); // During quest (before retrieving KI Handmade Incense)
                        }
                    }
                    else if (rftd == QuestStatus.Completed)
                    {
                        return QuestReply.Event(108); // New standard dialog after "Requiem for the Departed"
                    }
                    return null; // the script's 104 (Standard dialog)
                });

            // Pecca-Pocca.lua onTrigger: the quest's arm (234); her script's 233 is the fallback
            b.State(QuestStatus.Accepted, when: q => !q.Player.HasKeyItem(KeyItem.SheafOfHandmadeIncense))
                // to receive KI
                .Npc(Zone.FortKarugoNarugoS, Npc.FortKarugoNarugoS.PeccaPocca).Called().OnTrigger(QuestReply.Event(234));

            // the two scripts' onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.TheEldiemeNecropolisS, 105, q =>
                {
                    q.Quest.Begin();
                })
                .OnEventFinish(Zone.TheEldiemeNecropolisS, 107, q =>
                {
                    // npcUtil.completeQuest step for step, in its order, ending in the raw player:completeQuest: not
                    // quest:complete, whose cleanup would clear the quest's own Quest[7][23] vars
                    if (q.Player.GiveItem(Item.ScrollOfRecallMeriph))
                    {
                        q.Player.CompleteQuest(QuestLog.CrystalWar, "REQUIEM_FOR_THE_DEPARTED");
                        q.Player.DelKeyItem(KeyItem.SheafOfHandmadeIncense);
                    }
                })
                .OnEventFinish(Zone.FortKarugoNarugoS, 234, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.SheafOfHandmadeIncense);
                });
        }
    }
}
