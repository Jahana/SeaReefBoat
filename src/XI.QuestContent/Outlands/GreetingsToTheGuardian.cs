// Greetings to the Guardian (Log ID 5), ported from the quest branches of scripts/zones/Kazham/npcs/Hari_Pakhroib.lua and
// scripts/zones/Ifrits_Cauldron/npcs/Altar_of_Ashes.lua (quest rework B3b, Outlands group 7; call-only: the scripts now
// call this quest). Hari Pakhroib : !pos 55.973 -12 -176.719 250; Altar of Ashes : !pos 16 .1 -58 205. Hari's default
// (DefaultActions 84) alternates with his script in the lookup: every second talk runs the script, so this quest (when
// it answers nothing, the lookup plays 84 after it).
// Hari Pakhroib, in the script's order: accepted: PamamaVar 1: 71, else 69 (0, Bunch of Wild Pamamas); available and
// Windurst fame level 7: 68 (4596 x3), whose finish on 1 begins the quest and sets PamamaVar 0; completed and no
// needToZone: PamamaVar 2: 71, else 72; completed and needToZone: 72. 71's finish: PamamaVar 1: 5000 gil
// (npcUtil.giveCurrency), the quest completed, Windurst fame +16, the Kazham Caller title, PamamaVar 0, needToZone; 2:
// the same less the completion and the title.
// Altar of Ashes: its trade, accepted / completed, a bunch of pamamas among the items: ALTAR_OFFERING (0, the pamamas),
// PamamaVar 1 / 2, the whole trade taken (tradeComplete). Its trigger: accepted and PamamaVar 1 or 2: ALTAR_COMPLETED;
// accepted and 0: ALTAR_INSPECT; else ALTAR_STANDARD.
// Kept as the Lua has it: the altar takes the whole trade (hasItemQty, no item count); accepted with PamamaVar 2 (an
// offering made while completed, then the quest begun again: not possible) is 69; the altar's other answers are the
// quest's (no script default left).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class GreetingsToTheGuardian : QuestScript
    {
        public GreetingsToTheGuardian() : base(QuestLog.Outlands, "GREETINGS_TO_THE_GUARDIAN") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Hari_Pakhroib.lua onTrigger, in its order
            b.State(QuestStatus.Accepted)
                .Npc(Zone.Kazham, Npc.Kazham.HariPakhroib).Called().OnTrigger(q =>
                {
                    if (q.Player.CharVar("PamamaVar") == 1)
                    {
                        return QuestReply.Event(71); // Finish Quest
                    }
                    else
                    {
                        return QuestReply.Event(69, 0, Item.BunchOfWildPamamas); // Reminder Dialogue
                    }
                });

            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Windurst) >= 7)
                .Npc(Zone.Kazham, Npc.Kazham.HariPakhroib).Called()
                    .OnTrigger(QuestReply.Event(68, Item.BunchOfWildPamamas, Item.BunchOfWildPamamas, Item.BunchOfWildPamamas)); // Start Quest

            b.State(QuestStatus.Completed)
                .Npc(Zone.Kazham, Npc.Kazham.HariPakhroib).Called().OnTrigger(q =>
                {
                    if (!q.Player.NeedToZone)
                    {
                        if (q.Player.CharVar("PamamaVar") == 2)
                        {
                            return QuestReply.Event(71); // Finish quest dialogue (no different csid between initial and repeats)
                        }
                        else
                        {
                            return QuestReply.Event(72); // Dialogue for after completion of quest
                        }
                    }
                    else
                    {
                        return QuestReply.Event(72);
                    }
                });

            // Altar_of_Ashes.lua onTrade
            b.State(QuestStatus.Accepted).Or(QuestStatus.Completed)
                .Npc(Zone.IfritsCauldron, Npc.IfritsCauldron.AltarofAshes).Called().OnTrade(q =>
                {
                    if (q.Trade.HasItemQty(Item.BunchOfWildPamamas, 1))
                    {
                        q.Text.Special(Text.IfritsCauldron.ALTAR_OFFERING, 0, Item.BunchOfWildPamamas);
                        // Set variable to reflect first completion of quest (1) / a repeat (2)
                        q.Player.SetCharVar("PamamaVar", q.Quest.Status == QuestStatus.Accepted ? 1 : 2);
                        q.Trade.Complete();
                        return QuestReply.NoAction();
                    }

                    return null;
                });

            // Altar_of_Ashes.lua onTrigger
            b.AnyState()
                .Npc(Zone.IfritsCauldron, Npc.IfritsCauldron.AltarofAshes).Called().OnTrigger(q =>
                {
                    int pamamas = q.Player.CharVar("PamamaVar");

                    if (q.Quest.Status == QuestStatus.Accepted && (pamamas == 1 || pamamas == 2))
                    {
                        return QuestReply.MessageSpecial(Text.IfritsCauldron.ALTAR_COMPLETED);
                    }
                    else if (q.Quest.Status == QuestStatus.Accepted && pamamas == 0)
                    {
                        return QuestReply.MessageSpecial(Text.IfritsCauldron.ALTAR_INSPECT);
                    }
                    else
                    {
                        return QuestReply.MessageSpecial(Text.IfritsCauldron.ALTAR_STANDARD);
                    }
                });

            // Hari_Pakhroib.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.Kazham, 68, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Player.SetCharVar("PamamaVar", 0);
                    }
                })
                .OnEventFinish(Zone.Kazham, 71, q =>
                {
                    int pamamas = q.Player.CharVar("PamamaVar");
                    if (pamamas == 1) // First completion of quest; set title and complete quest
                    {
                        q.Player.GiveCurrency("gil", 5000);
                        // the raw player:completeQuest, which keeps the quest's own Quest[5][2] vars
                        q.Player.CompleteQuest(QuestLog.Outlands, "GREETINGS_TO_THE_GUARDIAN");
                        q.Player.AddFame(FameArea.Windurst, 16);
                        q.Player.AddTitle(Title.KazhamCaller);
                        q.Player.SetCharVar("PamamaVar", 0);
                        q.Player.SetNeedToZone(true);
                    }
                    else if (pamamas == 2) // Repeats of quest
                    {
                        q.Player.GiveCurrency("gil", 5000);
                        q.Player.AddFame(FameArea.Windurst, 16);
                        q.Player.SetCharVar("PamamaVar", 0);
                        q.Player.SetNeedToZone(true);
                    }
                });
        }
    }
}
