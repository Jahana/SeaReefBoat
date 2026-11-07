// Yomi Okuri (SAM AF2; Log ID 5, Quest ID 141), a line-for-line port of scripts/quests/outlands/SAM_AF2_Yomi_Okuri.lua
// (quest rework B3b Outlands, group 1). Norg: Jaucribaix !pos 91 -7 -8 (no script; default 71), Washu !pos 49 -6 15 (no
// script; default 80; shared with Chasing Dreams and Stop Your Whining). Labyrinth of Onzozo: qm2 !pos -176 10 -60 (no
// script; default NEST_OF_LARGE_BIRD), the NM Ubume. Valkurm Dunes: qm3 !pos -767 -4 192 (no script; default
// YOU_SENSE_AN_EVIL_PRESENCE), the NMs Doman and Onryo.
// Available, The Sacred Katana completed, a Samurai from AF2_QUEST_LEVEL: Jaucribaix 142 while the mustZone is set or
// GetSystemTime() < Timer (both set by The Sacred Katana's 141), else 146, which begins the quest on option 1 (Prog 1).
// Accepted: Washu 148 at Prog 1 (Prog 2); the Hecteyes Eye, Bastore Sardine, Slice of Giant Sheep Meat and Frost Turnip
// traded at Stage 0 with neither the wurst nor the feather: 150 (Washu's Tasty Wurst, Prog 3); his trigger: the wurst held
// 151, Stage 0 without it 149. Onzozo's qm2: the wurst held and Ubume not up: 0, whose option 1 takes the wurst and spawns
// Ubume (claimed); Ubume's death at Prog 3 or less sets Stage 1; qm2 at Stage 1 without the feather: 1 (the Yomotsu
// Feather). Jaucribaix at Prog 3 or less: 147 without the feather, 152 with it (the feather taken, Prog 4, the mustZone,
// Wait now + 60); Prog 4: 153 while the mustZone is set or GetSystemTime() < Wait, else 154 (the Yomotsu Hirasaka, Prog 5);
// past that 155 with the hirasaka, 156 with the faded one. Valkurm's qm3 with the hirasaka, the local var valkurmNM 0, at
// night (Vana'diel hour 18 or later, or before 5): 10 with neither NM up, else WHAT_DO_YOU_THINK; 10's option 1 within 2
// yalms with neither up spawns both (claimed) and says SUDDEN_CHILL (farther: MUST_BE_CLOSER). Each NM's death, the hirasaka
// held and the other dead or not spawned: valkurmNM 1; qm3 then 11, whose option 0 trades the hirasaka for the faded one.
// 156 runs quest:complete (Myochin Sune-Ate, Norg fame 20) and on true takes the faded hirasaka and sets A Thief in Norg!?'s
// mustZone and its Timer now + 60.
// Kept as the Lua has it: Ubume's death at Prog 3 or less sets Stage 1 whoever claimed it; qm2's 0 (the wurst, Ubume down)
// comes before the Stage 1 arm; Washu's 150 trade needs Stage 0, so a second wurst is not sold once Ubume died; the
// Stage 0 / wurst test of 149 repeats the arm above it; 1's giveKeyItem of a held feather is its message only; qm3's local
// var is lost on a zone change; the NMs' isDead / not isSpawned and the GetMobByID nil arm (an id with no mob) as written.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Outlands
{
    public sealed class YomiOkuri : QuestScript
    {
        public YomiOkuri() : base(QuestLog.Outlands, "YOMI_OKURI") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/outlands/SAM_AF2_Yomi_Okuri";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 20,
                FameArea = FameArea.Norg,
                Item = Item.MyochinSuneAte,
            });
            b.Steps(1, 2, 3, 4, 5);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Outlands, "THE_SACRED_KATANA") &&
                    q.Player.MainJob == Job.Sam &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF2_QUEST_LEVEL"))
                .Npc(Zone.Norg, Npc.Norg.Jaucribaix).OnTrigger(q =>
                {
                    if (q.Quest.MustZone ||
                        q.Player.SystemTime < q.Quest.Var("Timer"))
                    {
                        return QuestReply.Event(142);
                    }

                    return QuestReply.ProgressEvent(146);
                })
                .OnEventFinish(Zone.Norg, 146, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Quest.Begin();
                        q.Quest.SetStep(1);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.LabyrinthOfOnzozo, Npc.LabyrinthOfOnzozo.qm2).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.WashusTastyWurst) &&
                        !q.Player.MobIsSpawned(Mob.LabyrinthOfOnzozo.UBUME))
                    {
                        return QuestReply.ProgressEvent(0);
                    }
                    else if (
                        q.Quest.Var("Stage") == 1 &&
                        !q.Player.HasKeyItem(KeyItem.YomotsuFeather))
                    {
                        return QuestReply.ProgressEvent(1);
                    }
                    return null;
                })
                .Npc(Zone.LabyrinthOfOnzozo, Npc.LabyrinthOfOnzozo.Ubume).OnMobDeath(q =>
                {
                    if (q.Quest.Step <= 3)
                    {
                        q.Quest.SetVar("Stage", 1);
                    }
                })
                .OnEventFinish(Zone.LabyrinthOfOnzozo, 0, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.DelKeyItem(KeyItem.WashusTastyWurst);
                        q.Player.SpawnMob(Mob.LabyrinthOfOnzozo.UBUME, 0, claim: true);
                    }
                })
                .OnEventFinish(Zone.LabyrinthOfOnzozo, 1, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.YomotsuFeather);
                })
                .Npc(Zone.Norg, Npc.Norg.Jaucribaix).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress <= 3)
                    {
                        if (!q.Player.HasKeyItem(KeyItem.YomotsuFeather))
                        {
                            return QuestReply.Event(147);
                        }

                        return QuestReply.ProgressEvent(152);
                    }
                    else if (questProgress == 4)
                    {
                        if (q.Quest.MustZone ||
                            q.Player.SystemTime < q.Quest.Var("Wait"))
                        {
                            return QuestReply.Event(153);
                        }

                        return QuestReply.ProgressEvent(154);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.YomotsuHirasaka))
                    {
                        return QuestReply.Event(155);
                    }
                    else if (q.Player.HasKeyItem(KeyItem.FadedYomotsuHirasaka))
                    {
                        return QuestReply.ProgressEvent(156);
                    }
                    return null;
                })
                .Npc(Zone.Norg, Npc.Norg.Washu)
                    .OnTrade(q =>
                    {
                        if (q.Quest.Var("Stage") == 0 &&
                            !q.Player.HasKeyItem(KeyItem.WashusTastyWurst) &&
                            !q.Player.HasKeyItem(KeyItem.YomotsuFeather) &&
                            q.Trade.Matches((Item.HecteyesEye, 1), (Item.BastoreSardine1, 1), (Item.SliceOfGiantSheepMeat, 1), (Item.FrostTurnip, 1)))
                        {
                            return QuestReply.ProgressEvent(150);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Step == 1)
                        {
                            return QuestReply.ProgressEvent(148);
                        }
                        else if (q.Player.HasKeyItem(KeyItem.WashusTastyWurst))
                        {
                            return QuestReply.Event(151);
                        }
                        else if (
                            q.Quest.Var("Stage") == 0 &&
                            !q.Player.HasKeyItem(KeyItem.WashusTastyWurst))
                        {
                            return QuestReply.Event(149);
                        }
                        return null;
                    })
                .OnEventFinish(Zone.Norg, 148, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.Norg, 150, q =>
                {
                    q.Trade.Complete();
                    q.Player.GiveKeyItem(KeyItem.WashusTastyWurst);
                    q.Quest.SetStep(3);
                })
                .OnEventFinish(Zone.Norg, 152, q =>
                {
                    q.Player.DelKeyItem(KeyItem.YomotsuFeather);
                    q.Quest.SetStep(4);
                    q.Quest.SetMustZone();
                    q.Quest.SetVar("Wait", q.Player.SystemTime + 60); // 1 minute wait time
                })
                .OnEventFinish(Zone.Norg, 154, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.YomotsuHirasaka);
                    q.Quest.SetStep(5);
                })
                .OnEventFinish(Zone.Norg, 156, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.FadedYomotsuHirasaka);
                        q.Player.SetQuestMustZone(QuestLog.Outlands, "A_THIEF_IN_NORG");
                        q.Player.SetQuestVar(QuestLog.Outlands, "A_THIEF_IN_NORG", "Timer", q.Player.SystemTime + 60); // 1 minute wait time
                    }
                })
                .Npc(Zone.ValkurmDunes, Npc.ValkurmDunes.Doman).OnMobDeath(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.YomotsuHirasaka) &&
                        (q.Player.MobIsDead(Mob.ValkurmDunes.ONRYO) || !q.Player.MobIsSpawned(Mob.ValkurmDunes.ONRYO)))
                    {
                        q.Quest.SetLocalVar("valkurmNM", 1);
                    }
                })
                .Npc(Zone.ValkurmDunes, Npc.ValkurmDunes.Onryo).OnMobDeath(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.YomotsuHirasaka) &&
                        (q.Player.MobIsDead(Mob.ValkurmDunes.DOMAN) || !q.Player.MobIsSpawned(Mob.ValkurmDunes.DOMAN)))
                    {
                        q.Quest.SetLocalVar("valkurmNM", 1);
                    }
                })
                .Npc(Zone.ValkurmDunes, Npc.ValkurmDunes.qm3).OnTrigger(q =>
                {
                    int vanadielHour = q.Player.VanadielHour;

                    if (q.Player.HasKeyItem(KeyItem.YomotsuHirasaka) &&
                        q.Quest.LocalVar("valkurmNM") == 0 &&
                        (vanadielHour >= 18 || vanadielHour < 5))
                    {
                        // Lua: domanMob and onryoMob (GetMobByID never nil with the zone's IDs) and neither spawned
                        if (!q.Player.MobIsSpawned(Mob.ValkurmDunes.DOMAN) &&
                            !q.Player.MobIsSpawned(Mob.ValkurmDunes.ONRYO))
                        {
                            return QuestReply.ProgressEvent(10);
                        }
                        else
                        {
                            return QuestReply.MessageSpecial(Text.ValkurmDunes.WHAT_DO_YOU_THINK);
                        }
                    }
                    else if (
                        q.Quest.LocalVar("valkurmNM") == 1 &&
                        q.Player.HasKeyItem(KeyItem.YomotsuHirasaka))
                    {
                        return QuestReply.ProgressEvent(11);
                    }
                    return null;
                })
                .OnEventFinish(Zone.ValkurmDunes, 10, q =>
                {
                    if (q.Option == 1)
                    {
                        if (q.Npc.PlayerDistance > 2)
                        {
                            q.Text.Special(Text.ValkurmDunes.MUST_BE_CLOSER, KeyItem.YomotsuHirasaka);
                        }
                        else if (
                            !q.Player.MobIsSpawned(Mob.ValkurmDunes.ONRYO) &&
                            !q.Player.MobIsSpawned(Mob.ValkurmDunes.DOMAN))
                        {
                            q.Text.Special(Text.ValkurmDunes.SUDDEN_CHILL);
                            q.Player.SpawnMob(Mob.ValkurmDunes.DOMAN, 0, claim: true);
                            q.Player.SpawnMob(Mob.ValkurmDunes.ONRYO, 0, claim: true);
                        }
                    }
                })
                .OnEventFinish(Zone.ValkurmDunes, 11, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Player.DelKeyItem(KeyItem.YomotsuHirasaka);
                        q.Player.GiveKeyItem(KeyItem.FadedYomotsuHirasaka);
                    }
                });
        }
    }
}
