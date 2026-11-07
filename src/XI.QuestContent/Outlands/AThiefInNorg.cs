// A Thief in Norg!? (SAM AF3; Log ID 5, Quest ID 142), a line-for-line port of
// scripts/quests/outlands/SAM_AF3_A_Thief_in_Norg.lua (quest rework B3b Outlands, group 1). Norg: Jaucribaix !pos 91 -7 -8 (no
// script; default 71). Port Jeuno: Sanosuke !pos -63 7 0 (no script; default 303). Mhaura: Phoochuchu !pos -4 -4 69 (no
// script; default 300). Bastok Mines: the door _6i8 !pos 70 7 2 (no script, no default). Waughroon Shrine: the zone-in and
// the battlefield Thief in Norg (68; BattlefieldQuest: entry with the Banishing Charm kept, at Prog 6).
// Available, Yomi Okuri completed, a Samurai from AF3_QUEST_LEVEL: Jaucribaix 157 while the mustZone is set or
// GetSystemTime() < Timer (both set by Yomi Okuri's 156), else 158, which begins the quest on option 1 (Prog 1). Accepted:
// Sanosuke 304 at Prog 1 (Prog 2; 305 at 2, 306 from 3); Phoochuchu 301 at Prog 2 (Prog 3; 303 at 3, 302 from 4); the
// Bastok Mines door 186 at Prog 3 (Prog 4); Waughroon Shrine's zone-in at Prog 4: 2 (Prog 5); Jaucribaix: 159 up to Prog 4,
// 166 at 5 (the Banishing Charm, Prog 6 when given), at 6 167 with the charm anywhere (findItem) else 168 (the charm again),
// 160 at 7 (Prog 8), 161 at 8, at 9 163 while the mustZone is set or GetSystemTime() < Wait else 164 (quest:complete:
// Myochin Kabuto, Paragon of Samurai Excellence, Norg fame 20). The battlefield's 32001 with the char's local battlefieldWin
// 68: CHARM_DAMAGED, the Charred Helm, Prog 7. The helm held and a Spool of Gold Thread traded to Jaucribaix: 162 (the
// thread and the helm taken, Prog 9, the mustZone, Wait now + 60). Completed: Jaucribaix 165 (replaceDefault).
// Kept as the Lua has it: 164 does not read what quest:complete returns (a full inventory: the 164 plays again); 32001's
// addKeyItem has no obtained message of its own (CHARM_DAMAGED names it); 168 gives the charm without setting a Prog; the
// zone-in's 2 plays on every Waughroon Shrine zone-in at Prog 4 until it finishes; past Prog 9 Jaucribaix's trigger answers
// nil (no arm).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class AThiefInNorg : QuestScript
    {
        public AThiefInNorg() : base(QuestLog.Outlands, "A_THIEF_IN_NORG") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/SAM_AF3_A_Thief_in_Norg";

        // xi.battlefield.id.THIEF_IN_NORG (scripts/globals/battlefield.lua)
        const int ThiefInNorg = 68;

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Norg,
                Item = Item.MyochinKabuto,
                Title = Title.ParagonOfSamuraiExcellence,
            });
            b.Steps(1, 2, 3, 4, 5, 6, 7, 8, 9);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Outlands, "YOMI_OKURI") &&
                    q.Player.MainJob == Job.Sam &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF3_QUEST_LEVEL"))
                .Npc(Zone.Norg, Npc.Norg.Jaucribaix).OnTrigger(q =>
                {
                    if (q.Quest.MustZone ||
                        q.Player.SystemTime < q.Quest.Var("Timer"))
                    {
                        return QuestReply.Event(157);
                    }

                    return QuestReply.ProgressEvent(158);
                })
                .OnEventFinish(Zone.Norg, 158, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Quest.SetStep(1);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.BastokMines, Npc.BastokMines._6i8).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(186);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BastokMines, 186, q =>
                {
                    q.Quest.SetStep(4);
                })
                .Npc(Zone.Mhaura, Npc.Mhaura.Phoochuchu).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 2)
                    {
                        return QuestReply.ProgressEvent(301);
                    }
                    else if (questProgress == 3)
                    {
                        return QuestReply.Event(303);
                    }
                    else if (questProgress >= 4)
                    {
                        return QuestReply.Event(302);
                    }
                    return null;
                })
                .OnEventFinish(Zone.Mhaura, 301, q =>
                {
                    q.Quest.SetStep(3);
                })
                .Npc(Zone.Norg, Npc.Norg.Jaucribaix)
                    .OnTrade(q =>
                    {
                        if (q.Player.HasKeyItem(KeyItem.CharredHelm) &&
                            q.Trade.Matches((Item.SpoolOfGoldThread, 1)))
                        {
                            return QuestReply.ProgressEvent(162);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int questProgress = q.Quest.Step;

                        if (questProgress <= 4)
                        {
                            return QuestReply.Event(159);
                        }
                        else if (questProgress == 5)
                        {
                            return QuestReply.ProgressEvent(166);
                        }
                        else if (questProgress == 6)
                        {
                            if (q.Player.FindItem(Item.BanishingCharm))
                            {
                                return QuestReply.Event(167);
                            }

                            return QuestReply.ProgressEvent(168);
                        }
                        else if (questProgress == 7)
                        {
                            return QuestReply.ProgressEvent(160);
                        }
                        else if (questProgress == 8)
                        {
                            return QuestReply.Event(161);
                        }
                        else if (questProgress == 9)
                        {
                            if (q.Quest.MustZone ||
                                q.Player.SystemTime < q.Quest.Var("Wait"))
                            {
                                return QuestReply.Event(163);
                            }

                            return QuestReply.ProgressEvent(164);
                        }
                        return null;
                    })
                .OnEventFinish(Zone.Norg, 160, q =>
                {
                    q.Quest.SetStep(8);
                })
                .OnEventFinish(Zone.Norg, 162, q =>
                {
                    q.Trade.Complete();
                    q.Player.DelKeyItem(KeyItem.CharredHelm);
                    q.Quest.SetStep(9);
                    q.Quest.SetMustZone();
                    q.Quest.SetVar("Wait", q.Player.SystemTime + 60); // 1 minute wait time
                })
                .OnEventFinish(Zone.Norg, 164, q =>
                {
                    q.Quest.Complete();
                })
                .OnEventFinish(Zone.Norg, 166, q =>
                {
                    if (q.Player.GiveItem(Item.BanishingCharm))
                    {
                        q.Quest.SetStep(6);
                    }
                })
                .OnEventFinish(Zone.Norg, 168, q =>
                {
                    q.Player.GiveItem(Item.BanishingCharm);
                })
                .Npc(Zone.PortJeuno, Npc.PortJeuno.Sanosuke).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 1)
                    {
                        return QuestReply.ProgressEvent(304);
                    }
                    else if (questProgress == 2)
                    {
                        return QuestReply.Event(305);
                    }
                    else if (questProgress >= 3)
                    {
                        return QuestReply.Event(306);
                    }
                    return null;
                })
                .OnEventFinish(Zone.PortJeuno, 304, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnZoneIn(Zone.WaughroonShrine, q =>
                {
                    if (q.Quest.Step == 4)
                    {
                        return QuestReply.Event(2);
                    }
                    return null;
                })
                .OnEventFinish(Zone.WaughroonShrine, 2, q =>
                {
                    q.Quest.SetStep(5);
                })
                .OnEventFinish(Zone.WaughroonShrine, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == ThiefInNorg)
                    {
                        q.Text.Special(Text.WaughroonShrine.CHARM_DAMAGED, KeyItem.CharredHelm, Item.BanishingCharm);
                        q.Player.AddKeyItem(KeyItem.CharredHelm);
                        q.Quest.SetStep(7);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.Norg, Npc.Norg.Jaucribaix).OnTrigger(QuestReply.Event(165).ReplaceDefault());
        }
    }
}
