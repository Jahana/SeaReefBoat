// Under the Sea (Log ID 4, Quest ID 17), a line-for-line port of scripts/quests/otherAreas/Under_the_Sea.lua (quest rework
// B3b). Selbina: Yaya !pos -18.770 -2.597 -14.929 248, Oswald !pos 47.119 -15.273 7.989 248, Jimaida !pos -17.342
// -2.597 -18.766 248, Zaldon !pos -11.810 -7.287 -6.742 248 (a script: A Boy's Dream's trade). Available at Selbina /
// Rabao fame level >= 2 with xi.settings.map.FISHING_ENABLE true (false by default): Yaya's fixed 31, whose finish begins
// the quest. Accepted: Oswald at Prog 0: 32, finish Prog 1; with the Etched Ring: 37, whose finish completes the quest
// (the Amber Earring, Lil' Cupid), fame +10 San d'Oria and Bastok, and takes the ring; else nil (his default 30).
// Jimaida at Prog 1: 33, finish Prog 2. Zaldon at Prog 2: 34 (the Fat Greedie), finish Prog 3; at Prog 3 a Fat Greedie
// alone: math.randomInt(1, 100) <= 20: 35, whose finish confirms the trade, gives the Etched Ring and sets Prog 4; else 36
// (Event), whose finish confirms the trade (the fish is gone). Completed, while The Sand Charm is available: Oswald 38
// replaceDefault.
// Kept as the Lua has it: Oswald's 37 needs only the ring (any Prog but 0); the finishes are keyed on the csid alone; the
// fame comes after quest:complete; the ring goes only when quest:complete gives the earring.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class UnderTheSea : QuestScript
    {
        public UnderTheSea() : base(QuestLog.OtherAreas, "UNDER_THE_SEA") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Under_the_Sea";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.AmberEarring,
                Title = Title.LilCupid,
            });
            b.Steps(1, 2, 3, 4);

            // Settings.MapFlag(name) (P6): xi.settings.map[name] == true, as the Lua reads it (never Settings.Map(..) == 1: a number 1 is not true in Lua)
            b.State(QuestStatus.Available, when: q => q.Player.FameLevel(FameArea.SelbinaRabao) >= 2 &&
                                                       q.Settings.MapFlag("FISHING_ENABLE"))
                .Npc(Zone.Selbina, Npc.Selbina.Yaya).OnTrigger(QuestReply.ProgressEvent(31))
                .OnEventFinish(Zone.Selbina, 31, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Selbina, Npc.Selbina.Oswald).OnTrigger(q =>
                {
                    if (q.Quest.Step == 0)
                    {
                        return QuestReply.ProgressEvent(32); // Oswald is looking for his ring
                    }
                    else if (q.Player.HasKeyItem(KeyItem.EtchedRing))
                    {
                        return QuestReply.ProgressEvent(37); // You found it!
                    }
                    return null;
                })
                .Npc(Zone.Selbina, Npc.Selbina.Jimaida).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(33); // Go see Zaldon
                    }
                    return null;
                })
                .Npc(Zone.Selbina, Npc.Selbina.Zaldon)
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Step == 2)
                        {
                            return QuestReply.ProgressEvent(34, Item.FatGreedie);
                        }
                        return null;
                    })
                    .OnTrade(q =>
                    {
                        if (q.Quest.Step == 3 &&
                            q.Trade.HasExactly(Item.FatGreedie))
                        {
                            if (q.Player.RandomInt(1, 100) <= 20)
                            {
                                return QuestReply.ProgressEvent(35); // Ring found !
                            }
                            else
                            {
                                return QuestReply.Event(36); // Ring not found
                            }
                        }
                        return null;
                    })
                .OnEventFinish(Zone.Selbina, 32, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.Selbina, 33, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.Selbina, 34, q =>
                {
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.Selbina, 35, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.EtchedRing);
                    q.Quest.SetStep(4);
                })
                .OnEventFinish(Zone.Selbina, 36, q =>
                {
                    q.Trade.Confirm();
                })
                .OnEventFinish(Zone.Selbina, 37, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 10);
                        q.Player.AddFame(FameArea.Bastok, 10);
                        q.Player.DelKeyItem(KeyItem.EtchedRing);
                    }
                });

            b.State(QuestStatus.Completed, when: q => q.Player.QuestStatus(QuestLog.OtherAreas, "THE_SAND_CHARM") == QuestStatus.Available)
                .Npc(Zone.Selbina, Npc.Selbina.Oswald).OnTrigger(QuestReply.Event(38).ReplaceDefault());
        }
    }
}
