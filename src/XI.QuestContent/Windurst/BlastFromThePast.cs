// Blast from the Past (Log ID 2, Quest ID 11), a line-for-line port of scripts/quests/windurst/Blast_from_the_Past.lua
// (quest rework B3b). Koru-Moru : !pos -120 -6 124 239. Offered once Star Struck is completed (its 211 sets this quest's
// mustZone), Class Reunion is not accepted and Windurst fame is level 3: Koru-Moru's 214 (option 0: begun). Accepted:
// Tokaka (Port Windurst) 318 at Option 0 (Option 1), Yoran-Oran 221 at Option 1 (Option 2), then 222 (importantEvent);
// Koru-Moru 216, 215 at Option 2. The eighth Fossil Rock of the Maze of Shakhrami (FOSSIL_ROCK_OFFSET + 8) spawns
// Ichorous Ire, claimed, while it is not up and no Burnite Shell Stone is held, else shows FOSSIL_EXTRACTED + 2. The
// stone traded alone to Koru-Moru plays 224 (completed: fame 30, the Great Club, Fossilized Sea Farer; the stone taken;
// Nothing Matters' mustZone set); any other trade plays 225. Completed, once Nothing Matters' mustZone is clear:
// Yoran-Oran's 223 (importantEvent). Kept as the Lua file has it: the Option var is not checked at the turn-in (the stone
// alone completes the quest at any Option); every other Fossil Rock returns nil (its default); the rock's handler returns
// nil after the spawn or the message, so the lookup goes on to the rock's default.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Windurst
{
    public sealed class BlastFromThePast : QuestScript
    {
        public BlastFromThePast() : base(QuestLog.Windurst, "BLAST_FROM_THE_PAST") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/windurst/Blast_from_the_Past";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Fame = 30,
                FameArea = FameArea.Windurst,
                Item = Item.GreatClub,
                Title = Title.FossilizedSeaFarer,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Windurst, "STAR_STRUCK") &&
                    q.Player.QuestStatus(QuestLog.Windurst, "CLASS_REUNION") != QuestStatus.Accepted &&
                    q.Player.FameLevel(FameArea.Windurst) >= 3 &&
                    !q.Quest.MustZone)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KoruMoru).OnTrigger(QuestReply.ProgressEvent(214))
                .OnEventFinish(Zone.WindurstWalls, 214, q =>
                {
                    if (q.Option == 0)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.KoruMoru)
                    .OnTrade(q =>
                    {
                        if (q.Trade.HasExactly(Item.BurniteShellStone))
                        {
                            return QuestReply.ProgressEvent(224);
                        }
                        else
                        {
                            return QuestReply.Event(225);
                        }
                    })
                    .OnTrigger(q =>
                    {
                        if (q.Quest.Var("Option") == 2)
                        {
                            return QuestReply.Event(215);
                        }
                        else
                        {
                            return QuestReply.Event(216);
                        }
                    })
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.YoranOran).OnTrigger(q =>
                {
                    int questOption = q.Quest.Var("Option");

                    if (questOption == 1)
                    {
                        return QuestReply.ProgressEvent(221);
                    }
                    else if (questOption == 2)
                    {
                        return QuestReply.Event(222).ImportantEvent();
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWalls, 221, q => q.Quest.SetVar("Option", 2))
                .OnEventFinish(Zone.WindurstWalls, 224, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Trade.Confirm();

                        q.Player.SetQuestMustZone(QuestLog.Windurst, "NOTHING_MATTERS");
                    }
                })
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Tokaka).OnTrigger(q =>
                {
                    if (q.Quest.Var("Option") == 0)
                    {
                        return QuestReply.ProgressEvent(318);
                    }

                    return null;
                })
                .OnEventFinish(Zone.PortWindurst, 318, q => q.Quest.SetVar("Option", 1))
                .Npc(Zone.MazeOfShakhrami, Npc.MazeOfShakhrami.FossilRock).OnTrigger(q =>
                {
                    // TODO: Break out Fossil Rock into multiple NPC names and update impacted
                    // quests and missions.
                    // shakhramiID.npc.FOSSIL_ROCK_OFFSET
                    int rockOffset = q.Npc.Id - q.Player.NpcIdOf("FOSSIL_ROCK_OFFSET");

                    if (rockOffset == 8)
                    {
                        if (!q.Player.MobIsSpawned(Mob.MazeOfShakhrami.ICHOROUS_IRE) &&
                            !q.Player.HasItem(Item.BurniteShellStone))
                        {
                            // SpawnMob(shakhramiID.mob.ICHOROUS_IRE):updateClaim(player)
                            q.Player.SpawnMob(Mob.MazeOfShakhrami.ICHOROUS_IRE, 0, claim: true);
                        }
                        else
                        {
                            // player:messageSpecial(shakhramiID.text.FOSSIL_EXTRACTED + 2)
                            q.Text.Special(q.Text.Id(Text.MazeOfShakhrami.FOSSIL_EXTRACTED) + 2);
                        }
                    }

                    return null;
                });

            b.State(QuestStatus.Completed, when: q => !q.Player.QuestMustZone(QuestLog.Windurst, "NOTHING_MATTERS"))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.YoranOran).OnTrigger(QuestReply.Event(223).ImportantEvent());
        }
    }
}
