// Better the Demon You Know (Log ID 4, Quest ID 105), a line-for-line port of
// scripts/quests/otherAreas/Better_The_Demon_You_Know.lua (quest rework B3b, Other Areas group 8). Oldton Movalpolos:
// Koblakiq !pos -64.851 21.834 -117.521 (no script; the default event 13). Castle Zvahl Baileys: qm2 !pos 19.4 -24.141
// 19.185 (no script; default YOU_FIND_NOTHING), the NM Marquis Andrealphus (17436966) and the four demons after him
// (MARQUIS_ANDREALPUS + 1..4: Demon Banneret, Demon Secretary, Demon Banneret, Demon Secretary).
// Available, For the Birds completed and its mustZone clear: Koblakiq 20 (0, Demon Pen), whose finish begins the quest
// (any option). Accepted: Koblakiq: Prog 0: 21 (0, Demon Pen) at priority 101; 1: GetSystemTime() >= Wait: 24, else 23
// at priority 101; 2: 25 at priority 101; 3: 26; else nil. A trade holding a Demon Pen (tradeHas, at any Prog): 22,
// whose finish confirms the trade, sets Prog 1 and Wait = now + 60. 24 finished: Prog 2. 26 finished: quest:complete
// (Goblin Grenade, Apostate for Hire) and on true the Zeelozok's Earplug taken. The qm2: the char's local NMKilled 1
// and Prog 2: the earplug (addKeyItem: no message of its own), Prog 3, the reply KEYITEM_OBTAINED (the earplug); else
// Prog 2 and none of MARQUIS_ANDREALPUS + 0..4 spawned: the five spawned and claimed, MOBLIN_EARPLUG_ON_THE_GROUND, the
// reply MARQUIS_ATTACKS; else nil. Marquis Andrealphus' death at Prog 2: the char's local NMKilled 1 (a zone change
// clears it). Completed: Koblakiq 27 (event, replaceDefault).
// Kept as the Lua has it: the trade plays 22 at any Prog (a pen at Prog 2 sets Prog 1 and a new Wait); only the
// Marquis' death sets NMKilled (his mob script's onMobDeath and onMobDespawn despawn the four demons, so the qm2's
// demon operands never decide on their own); Prog 3 at the qm2: nil.
// The era module modules/era/lua/quests/otherareas/better_the_demon_you_know.lua (xi.pre(SOA)) wraps 22's finish (Wait
// = JstMidnight()): Modules/Era/OtherAreas/BetterTheDemonYouKnow.cs.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.OtherAreas
{
    public sealed class BetterTheDemonYouKnow : QuestScript
    {
        public BetterTheDemonYouKnow() : base(QuestLog.OtherAreas, "BETTER_THE_DEMON_YOU_KNOW") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/otherAreas/Better_The_Demon_You_Know";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Reward(new QuestReward
            {
                Item = Item.GoblinGrenade,
                Title = Title.ApostateForHire,
            });
            b.Steps(1, 2, 3);

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.OtherAreas, "FOR_THE_BIRDS") &&
                    !q.Player.QuestMustZone(QuestLog.OtherAreas, "FOR_THE_BIRDS"))
                .Npc(Zone.OldtonMovalpolos, Npc.OldtonMovalpolos.Koblakiq)
                    .OnTrigger(QuestReply.ProgressCutscene(20, 0, Item.DemonPen))
                .OnEventFinish(Zone.OldtonMovalpolos, 20, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted)
                .Npc(Zone.OldtonMovalpolos, Npc.OldtonMovalpolos.Koblakiq)
                .OnTrigger(q =>
                {
                    int progress = q.Quest.Step;
                    int waitTime = q.Quest.Var("Wait");

                    if (progress == 0)
                    {
                        return QuestReply.Event(21, 0, Item.DemonPen).SetPriority(101); // Reminder to get Demon Pen
                    }
                    else if (progress == 1)
                    {
                        if (q.Player.SystemTime >= waitTime)
                        {
                            return QuestReply.ProgressCutscene(24); // Go to Castle Zvahl Baileys
                        }
                        else
                        {
                            return QuestReply.Event(23).SetPriority(101); // Wait Longer
                        }
                    }
                    else if (progress == 2)
                    {
                        return QuestReply.Event(25).SetPriority(101); // Reminder to go to castle Zvahl Baileys
                    }
                    else if (progress == 3)
                    {
                        return QuestReply.ProgressCutscene(26);
                    }
                    return null;
                })
                .OnTrade(q =>
                {
                    if (q.Trade.Has(Item.DemonPen))
                    {
                        return QuestReply.ProgressCutscene(22);
                    }
                    return null;
                })
                .OnEventFinish(Zone.OldtonMovalpolos, 22, q =>
                {
                    q.Trade.Confirm();
                    q.Quest.SetStep(1);
                    // TODO: Check when the wait starts and how it is rounded. Retail can advance
                    // less than a minute after this cutscene.
                    q.Quest.SetVar("Wait", q.Player.SystemTime + 60); // 1 Minute wait time
                })
                .OnEventFinish(Zone.OldtonMovalpolos, 24, q =>
                {
                    q.Quest.SetStep(2);
                })
                .OnEventFinish(Zone.OldtonMovalpolos, 26, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.ZeelozoksEarplug);
                    }
                })
                .Npc(Zone.CastleZvahlBaileys, Npc.CastleZvahlBaileys.qm2).OnTrigger(q =>
                {
                    int progress = q.Quest.Step;

                    if (q.Player.LocalVar("NMKilled") == 1 &&
                        progress == 2)
                    {
                        q.Player.AddKeyItem(KeyItem.ZeelozoksEarplug);
                        q.Quest.SetStep(3);
                        return QuestReply.MessageSpecial(Text.CastleZvahlBaileys.KEYITEM_OBTAINED,
                            KeyItem.ZeelozoksEarplug);
                    }
                    else if (progress == 2 &&
                        !q.Player.MobIsSpawned(Mob.CastleZvahlBaileys.MARQUIS_ANDREALPUS) &&
                        !q.Player.MobIsSpawned(Mob.CastleZvahlBaileys.MARQUIS_ANDREALPUS, 1) &&
                        !q.Player.MobIsSpawned(Mob.CastleZvahlBaileys.MARQUIS_ANDREALPUS, 2) &&
                        !q.Player.MobIsSpawned(Mob.CastleZvahlBaileys.MARQUIS_ANDREALPUS, 3) &&
                        !q.Player.MobIsSpawned(Mob.CastleZvahlBaileys.MARQUIS_ANDREALPUS, 4))
                    {
                        for (int i = 0; i <= 4; i++)
                        {
                            q.Player.SpawnMob(Mob.CastleZvahlBaileys.MARQUIS_ANDREALPUS, i, claim: true);
                        }

                        q.Text.Special(Text.CastleZvahlBaileys.MOBLIN_EARPLUG_ON_THE_GROUND);
                        return QuestReply.MessageSpecial(Text.CastleZvahlBaileys.MARQUIS_ATTACKS);
                    }
                    return null;
                })
                .Npc(Zone.CastleZvahlBaileys, Npc.CastleZvahlBaileys.MarquisAndrealphus).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 2)
                    {
                        // If the player zones before getting the KI they need to refight the NM.
                        q.Player.SetLocalVar("NMKilled", 1);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.OldtonMovalpolos, Npc.OldtonMovalpolos.Koblakiq)
                    .OnTrigger(QuestReply.Event(27).ReplaceDefault());
        }
    }
}
