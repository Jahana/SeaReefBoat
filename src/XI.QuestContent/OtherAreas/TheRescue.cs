// The Rescue (Log ID 4, Quest ID 23), a line-for-line port of scripts/quests/otherAreas/The_Rescue.lua (quest rework B3b,
// Other Areas group 4). Selbina: Thunder Hawk !pos -58 -10 6 248 (no script, default 84); Beadeaux: the Jail Door _43b !pos 56 0.1 -23 147
// (no script, default NOTHING_OUT_OF_ORDINARY).
// Available at Selbina / Rabao fame level >= 1 (always true: a fame level is at least 1): Thunder Hawk 80, whose finish with
// option 70 begins the quest. Accepted: Thunder Hawk, with the Trader's Sack: 81, else 83 (Event); 81's finish:
// quest:complete (2000 exp, 5000 gil, the Map of the Ranguemont Pass, Honorary Citizen of Selbina), then fame +10 San d'Oria
// and Bastok and the sack taken. The Jail Door: without the sack, a trade of exactly a Quadav Charm: 1000; a trigger:
// LOCKED_DOOR_QUADAV_HAS_KEY (a messageSpecial reply); with the sack: nil (the default). 1000's finish: the trade
// confirmed, npcUtil.giveKeyItem the sack. Completed: Thunder Hawk 82 replaceDefault.
// Kept as the Lua has it: the fame check (level >= 1) never fails ("TODO: Fame requirement needs verification"); the fame
// comes after the completion; 1000's finish is keyed on the csid alone (a sack already held is given again: npcUtil's
// "already have" message).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class TheRescue : QuestScript
    {
        public TheRescue() : base(QuestLog.OtherAreas, "THE_RESCUE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/The_Rescue";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Exp = 2000,
                Gil = 5000,
                KeyItem = KeyItem.MapOfTheRanguemontPass,
                Title = Title.HonoraryCitizenOfSelbina,
            });

            // TODO: Fame requirement needs verification
            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.SelbinaRabao) >= 1)
                .Npc(Zone.Selbina, Npc.Selbina.ThunderHawk).OnTrigger(QuestReply.ProgressEvent(80))
                .OnEventFinish(Zone.Selbina, 80, q =>
                {
                    if (q.Option == 70)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Selbina, Npc.Selbina.ThunderHawk).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.TradersSack))
                    {
                        return QuestReply.ProgressEvent(81);
                    }
                    else
                    {
                        return QuestReply.Event(83);
                    }
                })
                .OnEventFinish(Zone.Selbina, 81, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 10);
                        q.Player.AddFame(FameArea.Bastok, 10);
                        q.Player.DelKeyItem(KeyItem.TradersSack);
                    }
                })
                .Npc(Zone.Beadeaux, Npc.Beadeaux._43b).OnTrade(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.TradersSack) &&
                        q.Trade.HasExactly(Item.QuadavCharm))
                    {
                        return QuestReply.ProgressEvent(1000);
                    }
                    return null;
                })
                .OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.TradersSack))
                    {
                        return QuestReply.MessageSpecial(Text.Beadeaux.LOCKED_DOOR_QUADAV_HAS_KEY);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Beadeaux, 1000, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.TradersSack);
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.Selbina, Npc.Selbina.ThunderHawk).OnTrigger(QuestReply.Event(82).ReplaceDefault());
        }
    }
}
