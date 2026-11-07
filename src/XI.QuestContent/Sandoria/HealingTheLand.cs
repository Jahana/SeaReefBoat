// Healing the Land (Log ID 0, Quest ID 82), ported from the quest branches of scripts/zones/Northern_San_dOria/npcs/Eperdur.lua
// and scripts/zones/Gusgen_Mines/npcs/qm3.lua (quest rework B3b, San d'Oria group 12; call-only: both scripts now call
// this quest). Eperdur : !pos 129 -6 96 231; qm3 : !pos -168 1 311 196. Eperdur's 681 (available, San d'Oria fame level
// 4, main level 10; option 0: begun, the Seal of Banishing); accepted: 682 with the seal, 683 without it (finish: the
// Scroll of Teleport-Holla and, when it fits, Pilgrim to Holla, needToZone, fame +40, completed by the raw
// player:completeQuest, which keeps the quest's own Quest[0][82] vars). Gusgen Mines' qm3, accepted: with the seal,
// the seal taken and FOUND_LOCATION_SEAL; without it IS_ON_THIS_SEAL. The scripts keep their
// own answers when the quest does not act: the qm3's LETTERS_IS_WRITTEN_HERE; Eperdur's script then calls Sorcery of the
// North, whose branches follow this quest's in his script (his DefaultActions 678 takes every other talk, the lookup's).
// Kept as the Lua has it: 683 plays while accepted without the seal (before the qm3 too, if the seal was lost); 681's
// finish adds the quest on option 0 in any status (a completed quest: the log's current bit, still completed); 683's
// finish reads no status; the completion sets needToZone, which Sorcery of the North's offer reads.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Sandoria
{
    public sealed class HealingTheLand : QuestScript
    {
        public HealingTheLand() : base(QuestLog.Sandoria, "HEALING_THE_LAND") { }

        protected override void Build(QuestScriptBuilder b)
        {
            // Eperdur.lua onTrigger, in its order
            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.Sandoria) >= 4 && q.Player.MainLevel >= 10)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Eperdur).Called().OnTrigger(QuestReply.Event(681));

            b.State(QuestStatus.Accepted)
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Eperdur).Called().OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.SealOfBanishing))
                    {
                        return QuestReply.Event(682); // during the quest
                    }
                    else
                    {
                        return QuestReply.Event(683); // finish the quest
                    }
                })
                // Gusgen Mines qm3.lua onTrigger
                .Npc(Zone.GusgenMines, Npc.GusgenMines.qm3).Called().OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.SealOfBanishing))
                    {
                        q.Player.DelKeyItem(KeyItem.SealOfBanishing);
                        return QuestReply.MessageSpecial(Text.GusgenMines.FOUND_LOCATION_SEAL, KeyItem.SealOfBanishing);
                    }
                    else
                    {
                        return QuestReply.MessageSpecial(Text.GusgenMines.IS_ON_THIS_SEAL, KeyItem.SealOfBanishing);
                    }
                });

            // Eperdur.lua onEventFinish (681, 683)
            b.AnyState()
                .OnEventFinish(Zone.NorthernSanDoria, 681, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                        q.Player.GiveKeyItem(KeyItem.SealOfBanishing);
                    }
                })
                .OnEventFinish(Zone.NorthernSanDoria, 683, q =>
                {
                    if (q.Player.GiveItem(Item.ScrollOfTeleportHolla))
                    {
                        q.Player.AddTitle(Title.PilgrimToHolla);
                        q.Player.SetNeedToZone(true);
                        q.Player.AddFame(FameArea.Sandoria, 40);
                        // the raw player:completeQuest, which keeps the quest's own Quest[0][82] vars
                        q.Player.CompleteQuest(QuestLog.Sandoria, "HEALING_THE_LAND");
                    }
                });
        }
    }
}
