// It's Not Your Vault (Log ID 5), ported from the quest branches of scripts/zones/Norg/npcs/Keal.lua and
// scripts/zones/Sea_Serpent_Grotto/npcs/qm3.lua (quest rework B3b, Outlands group 6; call-only: the scripts now call this
// quest). Keal walks a patrol path in Norg (his onSpawn stays in his script); qm3 : !pos -173 26 252 176.
// Keal: available, Norg fame level 3 and level 5: 36 (Sealed Iron Box), whose finish on option 1 begins the quest;
// accepted: the Sealed Iron Box: 38, else 37 (Map of Sea Serpent Grotto); completed: 39. 38's finish: no free slot:
// ITEM_CANNOT_BE_OBTAINED (Scroll of Tonko: Ichi); else the box taken, the scroll (addItem, ITEM_OBTAINED), Norg fame +30,
// the quest completed. qm3: accepted and no box: the Sealed Iron Box (npcUtil.giveKeyItem); nothing else.
// Kept as the Lua has it: the finishes read no status; 38 gives the scroll by addItem after its own free slot test; qm3
// answers nothing (its script has no other branch). Keal's default (DefaultActions 89) alternates with his script in the
// lookup: every second talk runs the script, and 89 plays after it when the quest answers nothing.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class ItsNotYourVault : QuestScript
    {
        public ItsNotYourVault() : base(QuestLog.Outlands, "ITS_NOT_YOUR_VAULT") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Keal.lua onTrigger, in its order
            b.State(QuestStatus.Available, when: q =>
                    q.Player.FameLevel(FameArea.Norg) >= 3 &&
                    q.Player.MainLevel >= 5)
                .Npc(Zone.Norg, Npc.Norg.Keal).Called().OnTrigger(QuestReply.Event(36, KeyItem.SealedIronBox)); // Start quest

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Norg, Npc.Norg.Keal).Called().OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.SealedIronBox))
                    {
                        return QuestReply.Event(38); // Finish quest
                    }
                    else
                    {
                        return QuestReply.Event(37, KeyItem.MapOfSeaSerpentGrotto); // Reminder/Directions Dialogue
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.Norg, Npc.Norg.Keal).Called().OnTrigger(QuestReply.Event(39)); // New Standard Dialogue

            // qm3.lua onTrigger
            b.State(QuestStatus.Accepted, when: q => !q.Player.HasKeyItem(KeyItem.SealedIronBox))
                .Npc(Zone.SeaSerpentGrotto, Npc.SeaSerpentGrotto.qm3).Called().OnTrigger(q =>
                {
                    q.Player.GiveKeyItem(KeyItem.SealedIronBox);
                    return QuestReply.NoAction();
                });

            // Keal.lua onEventFinish
            b.AnyState()
                .OnEventFinish(Zone.Norg, 36, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                })
                .OnEventFinish(Zone.Norg, 38, q =>
                {
                    if (q.Player.FreeSlotCount == 0)
                    {
                        q.Text.Special(Text.Norg.ITEM_CANNOT_BE_OBTAINED, Item.ScrollOfTonkoIchi);
                    }
                    else
                    {
                        q.Player.DelKeyItem(KeyItem.SealedIronBox);
                        q.Player.AddItem(Item.ScrollOfTonkoIchi); // Scroll of Tonko: Ichi
                        q.Text.Special(Text.Norg.ITEM_OBTAINED, Item.ScrollOfTonkoIchi);
                        q.Player.AddFame(FameArea.Norg, 30);
                        // the raw player:completeQuest, which keeps the quest's own Quest[5][137] vars
                        q.Player.CompleteQuest(QuestLog.Outlands, "ITS_NOT_YOUR_VAULT");
                    }
                });
        }
    }
}
