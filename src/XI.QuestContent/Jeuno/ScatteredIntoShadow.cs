// Scattered into Shadow (Log ID 3, Quest ID 61), a line-for-line port of scripts/quests/jeuno/Scattered_into_Shadow.lua
// (quest rework B3b, Deferred 1). Brutus !pos -55 8 95 244; Underground Pool offset !pos 10 0 250 151 (Feiyin);
// Castle Oztroja chest !pos 7 -16 -193 151; Tebhi !pos -136 24 -21 151. BST AF2: Wings of Gold completed,
// AF2_QUEST_LEVEL, a beastmaster. Brutus offers 141 (long; option 1 begins it with the three Aquaflora key items,
// else Prog 1) then 143 (short). Accepted: Feiyin's three Underground Pools (offsets 0-2 from UNDERGROUND_POOL_OFFSET)
// take Aquaflora 2 (20, Stage bit 1) and 1 (21, Stage bit 0); the middle one pops Dabotz's Ghost while Aquaflora 3
// is held, whose death sets Prog 2, then plays 18 (Aquaflora 3 taken, Prog 3, Stage bit 2). Brutus: 142 while any
// Aquaflora is held, 144 at Prog 3 (Prog 4), 149 at Prog 4, 135 at Prog 5 (completed: Beast Gaiters, fame +7 x3). At
// Prog 4 with Stage 7 a Castle Oztroja chest gives the Beast Collar (the treasure trade's item bypass), which Tebhi
// takes while NORMAL (Prog 5): he stops, turns to the char, fades out 2 s later (tebhiFade) and is CUTSCENE_ONLY 4 s
// later, walking on (tebhiGone), his respawnTime 180 s on. Tebhi loads CUTSCENE_ONLY: the Castle Oztroja zone-in at
// Prog 4 adds the char's TICK listener TEBHI_RESPAWN (tebhiRespawn), which makes him NORMAL (STATUS_VISIBLE) once his
// respawnTime has passed and removes itself when the char is no longer at Prog 4 (or in the zone).
// Osker 145 at Prog 4. Completed: Brutus 151 (importantOnce).
// The deferred closures are P10 callbacks: the Lua's two npc:timer closures are tebhiFade / tebhiGone on Tebhi's own
// queue, the listener's closure is tebhiRespawn. The Lua listener keeps the Tebhi it read at the zone-in
// (GetNPCByID(castleOzID.npc.TEBHI)); the callback reads him again by that id each time (the same NPC: an NPC lives as
// long as its zone), in the char's zone, which is Castle Oztroja past the callback's first test.
// The Lua file has no known bug to keep. Kept as the Lua has it: a declined offer leaves Prog 1, which quest:begin
// keeps (Prog is 1 while accepted until the ghost's death sets 2); the listener's zone test never fails: a zone change
// destroys the char's entity and its listeners with it (~CCharEntity; ZoneHost ReleaseListeners), on both sides.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public sealed class ScatteredIntoShadow : QuestScript
    {
        public ScatteredIntoShadow() : base(QuestLog.Jeuno, "SCATTERED_INTO_SHADOW") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Scattered_into_Shadow";

        // xi.status.NORMAL, xi.status.CUTSCENE_ONLY (scripts/enum/status.codegen.lua)
        const int StatusNormal = 0;
        const int StatusCutsceneOnly = 6;
        // xi.animationString.STATUS_VISIBLE, xi.animationString.STATUS_DISAPPEAR (scripts/enum/animation_string.lua)
        const string StatusVisible = "deru";
        const string StatusDisappear = "kesu";

        // beginQuest
        static void BeginQuest(IQuestContext q)
        {
            q.Quest.Begin();
            q.Player.AddKeyItem(KeyItem.Aquaflora1);
            q.Player.AddKeyItem(KeyItem.Aquaflora2);
            q.Player.AddKeyItem(KeyItem.Aquaflora3);
        }

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward { Item = Item.BeastGaiters });
            b.Steps(1, 2, 3, 4, 5);

            // npc:timer(2000, function(npcArg) ... end): Tebhi fades out
            var tebhiFade = b.Callback("tebhiFade", q => q.Npc.EntityAnimationPacket(StatusDisappear));
            // npc:timer(4000, function(npcArg) ... end): Tebhi hidden, walking on
            var tebhiGone = b.Callback("tebhiGone", q =>
            {
                q.Npc.SetStatus(StatusCutsceneOnly);
                q.Npc.ContinuePath();
            });
            // player:addListener('TICK', 'TEBHI_RESPAWN', function(playerArg) ... end)
            var tebhiRespawn = b.Callback("tebhiRespawn", q =>
            {
                // Not in the zone anymore, or not on this step - clean up listener and return.
                if (q.Zone != Zone.CastleOztroja || q.Quest.Step != 4)
                {
                    q.Player.RemoveListener("TEBHI_RESPAWN");
                    return;
                }

                // the zone-in's tebhi: GetNPCByID(castleOzID.npc.TEBHI)
                var tebhi = q.Player.NpcById(q.Player.NpcIdOf("TEBHI"));

                // Tebhi is already up, return.
                if (tebhi.Status == StatusNormal)
                {
                    return;
                }

                // Not time to respawn Tebhi, return.
                if (q.Player.SystemTime < tebhi.LocalVar("respawnTime"))
                {
                    return;
                }

                // If we make it here, spawn Tebhi.
                tebhi.SetStatus(StatusNormal);
                tebhi.EntityAnimationPacket(StatusVisible);
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.Jeuno, "WINGS_OF_GOLD") &&
                    q.Player.MainLevel >= q.Settings.MainNumber("AF2_QUEST_LEVEL") &&
                    q.Player.MainJob == Job.Bst)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Brutus).OnTrigger(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        return QuestReply.ProgressEvent(143); // (Short dialog)
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(141); // (Long dialog)
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 143, q =>
                {
                    if (q.Option == 1)
                    {
                        BeginQuest(q);
                        q.Text.Special(Text.UpperJeuno.YOU_ARE_GIVEN_THREE_SPRIGS, KeyItem.Aquaflora1);
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 141, q =>
                {
                    if (q.Option == 1)
                    {
                        BeginQuest(q);
                        q.Text.Special(Text.UpperJeuno.YOU_ARE_GIVEN_THREE_SPRIGS, KeyItem.Aquaflora1);
                    }
                    else
                    {
                        q.Quest.SetStep(1);
                    }
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.Feiyin, Npc.Feiyin.UndergroundPool).OnTrigger(q =>
                {
                    int offset = q.Npc.Id - q.Player.NpcIdOf("UNDERGROUND_POOL_OFFSET");

                    if (offset == 0 && q.Player.HasKeyItem(KeyItem.Aquaflora2))
                    {
                        return QuestReply.ProgressEvent(20);
                    }
                    else if (offset == 1)
                    {
                        if (q.Quest.Step == 2)
                        {
                            return QuestReply.ProgressEvent(18);
                        }
                        else if (q.Player.HasKeyItem(KeyItem.Aquaflora3) &&
                                 q.Player.PopFromQm(Mob.Feiyin.DABOTZS_GHOST, hide: 0, claim: true))
                        {
                            return QuestReply.NoAction();
                        }
                    }
                    else if (offset == 2 && q.Player.HasKeyItem(KeyItem.Aquaflora1))
                    {
                        return QuestReply.ProgressEvent(21);
                    }
                    return null;
                })
                .Npc(Zone.Feiyin, Npc.Feiyin.DabotzsGhost).OnMobDeath(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.Aquaflora3))
                    {
                        q.Quest.SetStep(2);
                    }
                })
                .OnEventFinish(Zone.Feiyin, 18, q =>
                {
                    q.Player.DelKeyItem(KeyItem.Aquaflora3);
                    q.Quest.SetStep(3);
                    q.Quest.SetVarBit("Stage", 2);
                })
                .OnEventFinish(Zone.Feiyin, 20, q =>
                {
                    q.Player.DelKeyItem(KeyItem.Aquaflora2);
                    q.Quest.SetVarBit("Stage", 1);
                })
                .OnEventFinish(Zone.Feiyin, 21, q =>
                {
                    q.Player.DelKeyItem(KeyItem.Aquaflora1);
                    q.Quest.SetVarBit("Stage", 0);
                })
                .Npc(Zone.CastleOztroja, Npc.CastleOztroja.Tebhi).OnTrade(q =>
                {
                    if (q.Npc.Status == StatusNormal && q.Trade.HasExactly(Item.BeastCollar))
                    {
                        q.Trade.Complete();
                        q.Quest.SetStep(5);

                        // Tebhi stops and faces the player when traded, then fades away.
                        q.Npc.ClearPath(true);
                        q.Npc.FacePlayer(false);
                        q.Npc.SetLocalVar("respawnTime", q.Player.SystemTime + 180);
                        q.Npc.Timer(2000, tebhiFade);

                        q.Npc.Timer(4000, tebhiGone);

                        return QuestReply.MessageSpecial(Text.CastleOztroja.TEBHI_ACCEPTS, Item.BeastCollar);
                    }
                    return null;
                })
                .Npc(Zone.CastleOztroja, Npc.CastleOztroja.TreasureChest).OnTrade(q =>
                {
                    if (q.Quest.Step == 4 && q.Quest.Var("Stage") == 7 && !q.Player.HasItem(Item.BeastCollar))
                    {
                        q.Trade.TreasureTrade(1, Item.BeastCollar);

                        return QuestReply.NoAction();
                    }
                    return null;
                })
                .OnZoneIn(Zone.CastleOztroja, q =>
                {
                    // Tebhi only spawns if a player on this part of the quest zones in.
                    if (q.Quest.Step != 4)
                    {
                        return null;
                    }

                    var tebhi = q.Player.NpcById(q.Player.NpcIdOf("TEBHI"));

                    if (tebhi == null)
                    {
                        return null;
                    }

                    // Attach a listener to each player to ensure Tebhi reappears if multiple people are on the quest.
                    // Listener cleans up when player zones out or progresses past this step.
                    q.Player.AddListener("TICK", "TEBHI_RESPAWN", tebhiRespawn);
                    return null;
                })
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Brutus).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.Aquaflora1) ||
                        q.Player.HasKeyItem(KeyItem.Aquaflora2) ||
                        q.Player.HasKeyItem(KeyItem.Aquaflora3))
                    {
                        return QuestReply.Event(142);
                    }
                    else if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(144);
                    }
                    else if (q.Quest.Step == 4)
                    {
                        return QuestReply.Event(149);
                    }
                    else if (q.Quest.Step == 5)
                    {
                        return QuestReply.ProgressEvent(135);
                    }
                    return null;
                })
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Osker).OnTrigger(q =>
                {
                    if (q.Quest.Step == 4)
                    {
                        return QuestReply.Event(145);
                    }
                    return null;
                })
                .OnEventFinish(Zone.UpperJeuno, 135, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.AddFame(FameArea.Sandoria, 7);
                        q.Player.AddFame(FameArea.Bastok, 7);
                        q.Player.AddFame(FameArea.Windurst, 7);
                    }
                })
                .OnEventFinish(Zone.UpperJeuno, 144, q => q.Quest.SetStep(4));

            b.State(QuestStatus.Completed)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Brutus).OnTrigger(QuestReply.Event(151).ImportantOnce());
        }
    }
}
