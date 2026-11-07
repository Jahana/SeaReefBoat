// A Chocobo's Tale (Log ID 3, Quest ID 72), a line-for-line port of scripts/quests/jeuno/A_Chocobos_Tale.lua (quest
// rework B3b). Nevela : !pos -60.114 0 81.125 244; Wobke : !pos 29.028 0.874 -111.626 234; Outpost Gate : !pos
// 473.566 23.421 413.134 109; qm : !pos -39.370 -11.093 307.285 105. Available once CoP A Vessel Without a Captain is
// completed: Nevela's 10015 begins it. Accepted: Nevela's 10016, at Prog 5 10017 (completed: 5200 gil, Chocobo Love Guru,
// fame +7 in the three nations, the Silver Comet's Collar taken); Wobke's 245 at Prog 0 (Prog 1), 246 at Prog 1-2, 247
// (0, Warding Oil) at Prog 3 (Prog 4), 248 past it; Pashhow Marshlands' Outpost Gate: 21 (0, Warding Oil) at Prog 1 (Prog
// 2), three Bottles of Warding Oil traded at Prog 2: 22 (Prog 3); Batallia Downs' qm at Prog 4 with no Badshah up: with
// the local var nmDefeated set, the collar (Prog 5), else the five Badshahs spawned claimed (YOU_ARE_BEING_ATTACKED); a
// Badshah's death at Prog 4 sets nmDefeated. Completed: Nevela's 10018 (replacing her default).
// Kept Lua quirks: isNMDefeated is true as soon as one Badshah is dead or not spawned (inside a Badshah's onMobDeath it
// is always true); nmDefeated is the killer's own local var (lost on zoning); the trade's Prog is read after the items;
// the oil trade is not taken (22's finish confirms nothing).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class AChocobosTale : QuestScript
    {
        public AChocobosTale() : base(QuestLog.Jeuno, "A_CHOCOBOS_TALE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/A_Chocobos_Tale";

        // isNMSpawned: one of the five Badshahs (BADSHAH_OFFSET .. + 4) is up
        static bool IsNMSpawned(IQuestContext q)
        {
            for (int offset = 0; offset <= 4; offset++)
            {
                if (q.Player.MobIsSpawned(Mob.BatalliaDowns.BADSHAH_OFFSET, offset))
                {
                    return true;
                }
            }

            return false;
        }

        // isNMDefeated: one of the five is dead or not spawned
        static bool IsNMDefeated(IQuestContext q)
        {
            for (int offset = 0; offset <= 4; offset++)
            {
                if (q.Player.MobIsDead(Mob.BatalliaDowns.BADSHAH_OFFSET, offset) ||
                    !q.Player.MobIsSpawned(Mob.BatalliaDowns.BADSHAH_OFFSET, offset))
                {
                    return true;
                }
            }

            return false;
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Gil = 5200, Title = Title.ChocoboLoveGuru });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.AVesselWithoutACaptain))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Nevela).OnTrigger(QuestReply.ProgressEvent(10015))
                .OnEventFinish(Zone.UpperJeuno, 10015, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Nevela).OnTrigger(q =>
                {
                    if (q.Quest.Step == 5)
                    {
                        return QuestReply.ProgressEvent(10017);
                    }
                    else
                    {
                        return QuestReply.Event(10016);
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 10017, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 7);
                        q.Player.AddFame(FameArea.Bastok, 7);
                        q.Player.AddFame(FameArea.Windurst, 7);
                        q.Player.DelKeyItem(KeyItem.SilverCometsCollar);
                    }
                })
                .Npc(Zone.BastokMines, Npc.BastokMines.Wobke).OnTrigger(q =>
                {
                    int questProgress = q.Quest.Step;

                    if (questProgress == 0)
                    {
                        return QuestReply.ProgressEvent(245);
                    }
                    else if (questProgress <= 2)
                    {
                        return QuestReply.Event(246);
                    }
                    else if (questProgress == 3)
                    {
                        return QuestReply.ProgressEvent(247, 0, Item.BottleOfWardingOil);
                    }
                    else
                    {
                        return QuestReply.Event(248);
                    }
                })
                .OnEventFinish(Zone.BastokMines, 245, q =>
                {
                    q.Quest.SetStep(1);
                })
                .OnEventFinish(Zone.BastokMines, 247, q =>
                {
                    q.Quest.SetStep(4);
                })
                .Npc(Zone.PashhowMarshlands, Npc.PashhowMarshlands.OutpostGate)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly((Item.BottleOfWardingOil, 3)) &&
                            q.Quest.Step == 2)
                        {
                            return QuestReply.ProgressEvent(22);
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        // TODO: The followup after CS 21 may be different, and needs capture.

                        if (q.Quest.Step == 1)
                        {
                            return QuestReply.ProgressEvent(21, 0, Item.BottleOfWardingOil);
                        }
                        return null;
                    })
                .OnEventFinish(Zone.PashhowMarshlands, 21, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.PashhowMarshlands, 22, q =>
                {
                    q.Quest.SetStep(3);
                })
                .Npc(Zone.BatalliaDowns, Npc.BatalliaDowns.qmchocobotale).OnTrigger(q =>
                {
                    if (!IsNMSpawned(q) &&
                        q.Quest.Step == 4)
                    {
                        if (q.Quest.LocalVar("nmDefeated") == 1)
                        {
                            q.Quest.SetStep(5);

                            return QuestReply.KeyItem(KeyItem.SilverCometsCollar);
                        }
                        else
                        {
                            for (int offset = 0; offset <= 4; offset++)
                            {
                                // SpawnMob(nmId):updateClaim(player)
                                q.Player.SpawnMob(Mob.BatalliaDowns.BADSHAH_OFFSET, offset, claim: true);
                            }

                            return QuestReply.MessageSpecial(Text.BatalliaDowns.YOU_ARE_BEING_ATTACKED);
                        }
                    }
                    return null;
                })
                .Npc(Zone.BatalliaDowns, Npc.BatalliaDowns.Badshah).OnMobDeath(q =>
                {
                    if (IsNMDefeated(q) &&
                        q.Quest.Step == 4)
                    {
                        q.Quest.SetLocalVar("nmDefeated", 1);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Nevela).OnTrigger(QuestReply.Event(10018).ReplaceDefault());
        }
    }
}
