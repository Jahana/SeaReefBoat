// Hitting the Marquisate (Log ID 2, Quest ID 71), a line-for-line port of
// scripts/quests/windurst/THF_AF3_Hitting_the_Marquisate.lua (quest rework B3b). Nanaa Mihgo : !pos 62 -4 240 241;
// Yatniel : !pos -66.817 -7 -126.594 245; Hagain : !pos 12.62 -16 81.4 249; Garlaige Citadel qm6, qm5, qm12, qm13,
// qm10, qm9, qm15; La Theine Plateau qm2. Once As Thick as Thieves is completed, a THF at AF3_QUEST_LEVEL with no
// mustZone: Nanaa Mihgo's 512 begins it (the Cat Burglar's Note). Yatniel (Lower Jeuno): 10029 (yatnielProg 1), 10030;
// four Quake Grenades traded exactly: 10031 (taken, yatnielProg 2), then 10032. Hagain (Mhaura): 10003 (hagainProg 1,
// the Bomb Incense), 10004 at 1-7; the Chandelier Coal traded alone: 10005 (taken, the incense taken, hagainProg 8),
// then 10006. With the incense, the six Garlaige qms in quest order (hagainProg 1-6) play 50-55 with
// PRESENCE_FROM_CEILING; option 1 sends THE_PRESENCE_MOVES + the qm's offset and adds 1 to hagainProg. qm15 at
// hagainProg 7: within the Chandelier's cooldown THE_PRESENCE_MOVES + 7; else, the Chandelier not up and the incense
// held: HEAT_FROM_CEILING and 56 (option 1: the Chandelier popped 5 s later, a deferred callback; else
// THE_PRESENCE_MOVES + 6). Yatniel 2 and Hagain 8: Nanaa Mihgo's 516 (nanaaProg 1, both others 0), then 517; a Pickaxe
// traded alone to La Theine's qm2 at nanaaProg 1: 119 completes it (the Rogue's Poulaines, Paragon of Thief Excellence;
// the pickaxe and the note taken). Kept as the Lua file has it: the offer's mustZone is the char var, which nothing
// sets (As Thick as Thieves sets a player local var of that name); Yatniel's and Hagain's trades have no progress test;
// the qm finishes look their offset up by the NPC's name (here by the csid each qm alone starts). 56's option 1 (quest
// rework P10, deferred callbacks): `npc:timer(5000, function(npcArg) ... end)`, the Lua closure over the finish's
// player and npc, is q.Npc.Timer(5000, chandelier, WithPlayer.Yes): the callback runs on qm15's own timer with qm15 as
// its Npc and the char that finished 56 as its Player (found by id; gone, it does not run: logged).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class HittingTheMarquisate : QuestScript
    {
        public HittingTheMarquisate() : base(QuestLog.Windurst, "HITTING_THE_MARQUISATE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/THF_AF3_Hitting_the_Marquisate";

        // garlaigeQmInfo, by the qm: { Required Progress Value, Trigger Event, messageSpecial offset }
        static QuestReply GarlaigeQmOnTrigger(IQuestContext q, int requiredProgress, int triggerEvent)
        {
            if (q.Player.HasKeyItem(KeyItem.BombIncense) &&
                q.Quest.Var("hagainProg") == requiredProgress)
            {
                q.Text.Special(Text.GarlaigeCitadel.PRESENCE_FROM_CEILING);
                return QuestReply.ProgressCutscene(triggerEvent, KeyItem.BombIncense);
            }

            return null;
        }

        // garlaigeQmOnEventFinish, with the qm's messageSpecial offset (Lua: garlaigeQmInfo[npc:getName()][3])
        static void GarlaigeQmOnEventFinish(IQuestContext q, int messageOffset)
        {
            if (q.Option == 1)
            {
                // player:messageSpecial(garlaigeID.text.THE_PRESENCE_MOVES + qmData[3]): Presence moved west.
                q.Text.Special(q.Text.Id(Text.GarlaigeCitadel.THE_PRESENCE_MOVES) + messageOffset);
                // quest:incrementVar(player, 'hagainProg', 1)
                q.Quest.IncrementVar("hagainProg", 1);
            }
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.RoguesPoulaines, Title = Title.ParagonOfThiefExcellence });

            // 56's option 1, 5 s on: npc:timer(5000, function(npcArg) ... end), the closure's player and npc (qm15)
            var chandelier = b.Callback("chandelier", q =>
            {
                // npcUtil.popFromQM(player, npc, garlaigeID.mob.CHANDELIER, { hide = 0, claim = false })
                q.Player.PopFromQm(Mob.GarlaigeCitadel.CHANDELIER, hide: 0, claim: false);
                // player:messageSpecial(garlaigeID.text.THE_PRESENCE_MOVES + 5): Something flies out from the ceiling!
                q.Text.Special(q.Text.Id(Text.GarlaigeCitadel.THE_PRESENCE_MOVES) + 5);
            });

            b.State(QuestStatus.Available, when: q =>
                    !q.Quest.MustZone &&
                    q.Player.HasCompletedQuest(QuestLog.Windurst, "AS_THICK_AS_THIEVES") &&
                    q.Player.MainJob == Job.Thf &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF3_QUEST_LEVEL"))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.NanaaMihgo).OnTrigger(QuestReply.ProgressEvent(512))
                .OnEventFinish(Zone.WindurstWoods, 512, q =>
                {
                    q.Quest.Begin();
                    q.Player.GiveKeyItem(KeyItem.CatBurglarsNote);
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.GarlaigeCitadel, Npc.GarlaigeCitadel.qm5).OnTrigger(q => GarlaigeQmOnTrigger(q, 2, 51))
                .Npc(Zone.GarlaigeCitadel, Npc.GarlaigeCitadel.qm6).OnTrigger(q => GarlaigeQmOnTrigger(q, 1, 50))
                .Npc(Zone.GarlaigeCitadel, Npc.GarlaigeCitadel.qm9).OnTrigger(q => GarlaigeQmOnTrigger(q, 6, 55))
                .Npc(Zone.GarlaigeCitadel, Npc.GarlaigeCitadel.qm10).OnTrigger(q => GarlaigeQmOnTrigger(q, 5, 54))
                .Npc(Zone.GarlaigeCitadel, Npc.GarlaigeCitadel.qm12).OnTrigger(q => GarlaigeQmOnTrigger(q, 3, 52))
                .Npc(Zone.GarlaigeCitadel, Npc.GarlaigeCitadel.qm13).OnTrigger(q => GarlaigeQmOnTrigger(q, 4, 53))
                .Npc(Zone.GarlaigeCitadel, Npc.GarlaigeCitadel.qm15).OnTrigger(q =>
                {
                    int hagainProgress = q.Quest.Var("hagainProg");

                    if (hagainProgress == 7)
                    {
                        // GetNPCByID(garlaigeID.npc.CHANDELIER_QM): CHANDELIER_QM is GetFirstID('qm15'), this NPC
                        if (q.Player.SystemTime <= q.Npc.LocalVar("chandelierCooldown"))
                        {
                            // quest:messageSpecial(garlaigeID.text.THE_PRESENCE_MOVES + 7)
                            return QuestReply.MessageSpecial(q.Text.Id(Text.GarlaigeCitadel.THE_PRESENCE_MOVES) + 7);
                        }
                        else if (!q.Player.MobIsSpawned(Mob.GarlaigeCitadel.CHANDELIER) &&
                            q.Player.HasKeyItem(KeyItem.BombIncense))
                        {
                            q.Text.Special(Text.GarlaigeCitadel.HEAT_FROM_CEILING);
                            return QuestReply.ProgressCutscene(56, KeyItem.BombIncense);
                        }
                    }

                    return null;
                })
                .OnEventFinish(Zone.GarlaigeCitadel, 50, q => GarlaigeQmOnEventFinish(q, 1))
                .OnEventFinish(Zone.GarlaigeCitadel, 51, q => GarlaigeQmOnEventFinish(q, 2))
                .OnEventFinish(Zone.GarlaigeCitadel, 52, q => GarlaigeQmOnEventFinish(q, 0))
                .OnEventFinish(Zone.GarlaigeCitadel, 53, q => GarlaigeQmOnEventFinish(q, 0))
                .OnEventFinish(Zone.GarlaigeCitadel, 54, q => GarlaigeQmOnEventFinish(q, 3))
                .OnEventFinish(Zone.GarlaigeCitadel, 55, q => GarlaigeQmOnEventFinish(q, 0))
                .OnEventFinish(Zone.GarlaigeCitadel, 56, q =>
                {
                    if (q.Option == 1)
                    {
                        // npc:timer(5000, function(npcArg) ... end): the callback with the finish's char
                        q.Npc.Timer(5000, chandelier, WithPlayer.Yes);
                    }
                    else
                    {
                        // player:messageSpecial(garlaigeID.text.THE_PRESENCE_MOVES + 6): The presence in the ceiling
                        // still lingers...
                        q.Text.Special(q.Text.Id(Text.GarlaigeCitadel.THE_PRESENCE_MOVES) + 6);
                    }
                })
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.qm2).OnTrade(q =>
                {
                    if (q.Trade.HasExactly(Item.Pickaxe) &&
                        q.Quest.Var("nanaaProg") == 1)
                    {
                        return QuestReply.ProgressEvent(119, 0, Item.RoguesPoulaines, q.Player.Nation, Item.Pickaxe);
                    }

                    return null;
                })
                .OnEventFinish(Zone.LaTheinePlateau, 119, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();
                        q.Player.DelKeyItem(KeyItem.CatBurglarsNote);
                    }
                })
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Yatniel)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly((Item.QuakeGrenade, 4)))
                        {
                            return QuestReply.ProgressEvent(10031);
                        }

                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int yatnielProgress = q.Quest.Var("yatnielProg");

                        if (yatnielProgress == 0)
                        {
                            return QuestReply.ProgressEvent(10029, 0, Item.QuakeGrenade);
                        }
                        else if (yatnielProgress == 1)
                        {
                            return QuestReply.ProgressEvent(10030, 0, Item.QuakeGrenade);
                        }
                        else if (yatnielProgress == 2)
                        {
                            return QuestReply.ProgressEvent(10032);
                        }

                        return null;
                    })
                .OnEventFinish(Zone.LowerJeuno, 10029, q => q.Quest.SetVar("yatnielProg", 1))
                .OnEventFinish(Zone.LowerJeuno, 10031, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetVar("yatnielProg", 2);
                })
                .Npc(Zone.Mhaura, Npc.Mhaura.Hagain)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.LumpOfChandelierCoal))
                        {
                            return QuestReply.ProgressEvent(10005);
                        }

                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        int hagainProgress = q.Quest.Var("hagainProg");

                        if (hagainProgress == 0)
                        {
                            return QuestReply.ProgressEvent(10003, 0, KeyItem.BombIncense, Item.LumpOfChandelierCoal);
                        }
                        else if (hagainProgress == 8)
                        {
                            return QuestReply.ProgressEvent(10006);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(10004, 0, KeyItem.BombIncense, Item.LumpOfChandelierCoal);
                        }
                    })
                .OnEventFinish(Zone.Mhaura, 10003, q =>
                {
                    q.Quest.SetVar("hagainProg", 1);
                    q.Player.GiveKeyItem(KeyItem.BombIncense);
                })
                .OnEventFinish(Zone.Mhaura, 10005, q =>
                {
                    q.Trade.Confirm();
                    q.Player.DelKeyItem(KeyItem.BombIncense);
                    q.Quest.SetVar("hagainProg", 8);
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.NanaaMihgo).OnTrigger(q =>
                {
                    if (q.Quest.Var("yatnielProg") == 2 &&
                        q.Quest.Var("hagainProg") == 8)
                    {
                        return QuestReply.ProgressEvent(516);
                    }
                    else if (q.Quest.Var("nanaaProg") == 1)
                    {
                        return QuestReply.ProgressEvent(517);
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWoods, 516, q =>
                {
                    q.Quest.SetVar("nanaaProg", 1);
                    q.Quest.SetVar("yatnielProg", 0);
                    q.Quest.SetVar("hagainProg", 0);
                });
        }
    }
}
