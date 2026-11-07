// Beginnings (BLU AF1; Log ID 6, Quest ID 21), a line-for-line port of scripts/quests/ahtUrhgan/BLU_AF1_Beginnings.lua
// (quest rework B3b Aht Urhgan, group 1). Aht Urhgan Whitegate: Waoud !pos 65 -6 -78 (no script; default 61). The five
// Assault staging NPCs (each with a script: the Assault armband, else 7 / 4; Waudeen 6 / 3): Meyaada (Arrapago Reef
// !pos 22 -8 573), Daswil (Bhaflau Thickets !pos -209 -12 -780), Nahshib and Nareema (Caedarva Mire !pos -274 -8 -64,
// 518 -24 -467), Waudeen (Mount Zhayolm !pos 674 -24 368). Available with Immortal Sentries completed, An Empty Vessel
// completed and its completeEvent cleared, main job BLU at AF1_QUEST_LEVEL (40): Waoud 705, whose option 1 begins it.
// Accepted as a BLU: Waoud 707 with the five brands (it completes: the Immortal's Scimitar, Branded by the Five
// Serpents; Omens' mustZone, Omens' Timer = now + 60), else 706 (gil), whose option 1 with 1000 gil takes it
// (PAY_DIVINATION). Each staging NPC gives its brand: Meyaada 10 (the Springserpent), Daswil 8 (the Skyserpent),
// Nahshib 10 (the Galeserpent), Nareema 12 (the Stoneserpent), Waudeen 10 (the Flameserpent); with the brand: 11 / 9 /
// 11 / 13 / 11, an importantEvent (Progress the first time in a zone, then Event, which alternates with the NPC's
// script).
// Kept as the Lua has it: 707 sets Omens' Timer from GetSystemTime() (the era module beginnings sets JstMidnight());
// the brands stay with the char after the completion; a brand finish gives the key item whatever the char holds.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.AhtUrhgan
{
    public sealed class Beginnings : QuestScript
    {
        public Beginnings() : base(QuestLog.AhtUrghan, "BEGINNINGS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/ahtUrhgan/BLU_AF1_Beginnings";

        static readonly int[] BrandKeyItems =
        {
            KeyItem.BrandOfTheSpringserpent,
            KeyItem.BrandOfTheGaleserpent,
            KeyItem.BrandOfTheFlameserpent,
            KeyItem.BrandOfTheSkyserpent,
            KeyItem.BrandOfTheStoneserpent,
        };

        static bool HasRequiredBrands(IQuestContext q)
        {
            foreach (int keyItem in BrandKeyItems)
            {
                if (!q.Player.HasKeyItem(keyItem))
                {
                    return false;
                }
            }

            return true;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.ImmortalsScimitar,
                Title = Title.BrandedByTheFiveSerpents,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedMission(MissionLog.Toau, Mission.Toau.ImmortalSentries) &&
                    q.Player.HasCompletedQuest(QuestLog.AhtUrghan, "AN_EMPTY_VESSEL") &&
                    q.Player.MainJob == Job.Blu &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF1_QUEST_LEVEL") &&
                    // xi.quest.getVar(player, AHT_URHGAN, AN_EMPTY_VESSEL, 'completeEvent'):
                    // player:getVar('Quest[6][5]completeEvent')
                    q.Player.CharVar("Quest[6][5]completeEvent") == 0)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Waoud).OnTrigger(QuestReply.ProgressEvent(705))
                .OnEventFinish(Zone.AhtUrhganWhitegate, 705, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted, when: q => q.Player.MainJob == Job.Blu)
                .Npc(Zone.AhtUrhganWhitegate, Npc.AhtUrhganWhitegate.Waoud).OnTrigger(q =>
                {
                    if (HasRequiredBrands(q))
                    {
                        return QuestReply.ProgressEvent(707);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(706, q.Player.Gil);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 706, q =>
                {
                    if (q.Option == 1 &&
                        q.Player.Gil >= 1000)
                    {
                        q.Player.DelGil(1000);
                        q.Text.Special(Text.AhtUrhganWhitegate.PAY_DIVINATION);
                    }
                })
                .OnEventFinish(Zone.AhtUrhganWhitegate, 707, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetQuestMustZone(QuestLog.AhtUrghan, "OMENS");
                        // 1 minute wait time
                        q.Player.SetQuestVar(QuestLog.AhtUrghan, "OMENS", "Timer", q.Player.SystemTime + 60);
                    }
                })
                .Npc(Zone.ArrapagoReef, Npc.ArrapagoReef.Meyaada).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.BrandOfTheSpringserpent))
                    {
                        return QuestReply.ProgressEvent(10);
                    }
                    else
                    {
                        return QuestReply.Event(11).ImportantEvent();
                    }
                })
                .OnEventFinish(Zone.ArrapagoReef, 10, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.BrandOfTheSpringserpent);
                })
                .Npc(Zone.BhaflauThickets, Npc.BhaflauThickets.Daswil).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.BrandOfTheSkyserpent))
                    {
                        return QuestReply.ProgressEvent(8);
                    }
                    else
                    {
                        return QuestReply.Event(9).ImportantEvent();
                    }
                })
                .OnEventFinish(Zone.BhaflauThickets, 8, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.BrandOfTheSkyserpent);
                })
                .Npc(Zone.CaedarvaMire, Npc.CaedarvaMire.Nahshib).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.BrandOfTheGaleserpent))
                    {
                        return QuestReply.ProgressEvent(10);
                    }
                    else
                    {
                        return QuestReply.Event(11).ImportantEvent();
                    }
                })
                .Npc(Zone.CaedarvaMire, Npc.CaedarvaMire.Nareema).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.BrandOfTheStoneserpent))
                    {
                        return QuestReply.ProgressEvent(12);
                    }
                    else
                    {
                        return QuestReply.Event(13).ImportantEvent();
                    }
                })
                .OnEventFinish(Zone.CaedarvaMire, 10, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.BrandOfTheGaleserpent);
                })
                .OnEventFinish(Zone.CaedarvaMire, 12, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.BrandOfTheStoneserpent);
                })
                .Npc(Zone.MountZhayolm, Npc.MountZhayolm.Waudeen).OnTrigger(q =>
                {
                    if (!q.Player.HasKeyItem(KeyItem.BrandOfTheFlameserpent))
                    {
                        return QuestReply.ProgressEvent(10);
                    }
                    else
                    {
                        return QuestReply.Event(11).ImportantEvent();
                    }
                })
                .OnEventFinish(Zone.MountZhayolm, 10, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.BrandOfTheFlameserpent);
                });
        }
    }
}
