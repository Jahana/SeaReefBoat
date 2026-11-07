// Light in the Darkness (Log ID 7, Quest ID 19), a line-for-line port of
// scripts/quests/crystalWar/WOTG_BAS_3_Light_in_the_Darkness.lua (quest rework B3b Crystal War group 3). Bastok Markets
// [S]: Gentle Tiger !pos -203.9 -10 2.2 (default 109), Pagdako !pos -202.1 -6 -93.9 (default 106), Blatherix !pos
// -309.8 -12 -42.8 (no default), Engelhart !pos -80.1 -4.4 -125.3 (default 104). Pashhow Marshlands [S]: Corroded Door
// !pos -385.6 22 456.4 (its script: the Ruhotz Silvermines instance, zones/Ruhotz_Silvermines/instances/
// light_in_the_darkness.lua).
// Available, the WotG mission Back to the Beginning and Fires of Discontent completed: Gentle Tiger 16, whose finish
// begins it. Accepted, by Prog: 0: Pagdako 19 (-> 1). 1: Blatherix 21 (-> 2). 2: Blatherix 22 (an Event); 30 Chunks of
// Goblin Chocolate alone or 5000 gil alone: 23, whose finish confirms the trade, gives the Mine Shaft Key, Prog 3. 3:
// the zone-in to Pashhow Marshlands [S] from Grauberg [S]: 901 (-> 4). 4: nothing of the quest's (the Corroded Door's
// instance takes the key at its registration, plays 4 at Prog 4 / 7, sets Prog 5 when 7 Quadav die, Prog 7 when it fails
// at Prog 4). 5: any zone-in to Pashhow Marshlands [S]: 902 (-> 6). 6: Gentle Tiger 27, whose finish completes it (an
// Adaman Ingot) and then sets Burden of Suspicion's Timer to VanadielUniqueDay() + 1 and its mustZone. 7: Blatherix 25
// (an Event); 10 chocolates alone or 1000 gil alone: 23, whose finish confirms the trade and gives a new key (Prog
// stays 7). Reminders (Events): Gentle Tiger 17 (Prog 0-4, 7), Engelhart 18 (Prog 0-4, 6, 7), Pagdako 20 (1-4, 6, 7),
// Blatherix 24 (3, 4, 6).
// Kept as the Lua has it: 16 begins on any option; the Prog 5 zone-in plays 902 from any zone; Prog 5 has no Bastok
// Markets [S] handler (the defaults answer there: Gentle Tiger 109, Engelhart Fires of Discontent's 165 (Fires
// completed is this quest's precondition), Pagdako 106, Blatherix nothing); the Prog 3 zone-in from any zone but
// Grauberg [S] is nil; 23 at Prog 7 gives a key with no Prog change (the instance's next run reads Prog 7), and a
// second 23 with the key held gives nothing (the giveKeyItem sees it held); no completed section (the "cs 28 before the
// next day" TODO).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class LightInTheDarkness : QuestScript
    {
        public LightInTheDarkness() : base(QuestLog.CrystalWar, "LIGHT_IN_THE_DARKNESS") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_BAS_3_Light_in_the_Darkness";

        protected override void Build(QuestScriptBuilder b)
        {
            // 5 and 7 are set by the instance script (zones/Ruhotz_Silvermines/instances/light_in_the_darkness.lua:
            // onInstanceComplete at Prog 4 / 7, onInstanceFailure at Prog 4), declared so the validator sees them set
            b.Steps(1, 2, 3, 4, 5, 6, 7);

            b.Reward(new QuestReward
            {
                Item = Item.AdamanIngot,
            });

            // Talk to Gentle Tiger at (H-6)
            // TODO: 1 day wait after WOTG Mission: Cait Sith
            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedMission(MissionLog.Wotg, Mission.Wotg.BackToTheBeginning) && // WotG mission requirement.
                    q.Player.HasCompletedQuest(QuestLog.CrystalWar, "FIRES_OF_DISCONTENT"))          // WotG nation quest requirement.
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.ProgressEvent(16))
                .OnEventFinish(Zone.BastokMarketsS, 16, q =>
                {
                    q.Quest.Begin();
                });

            // Talk to Pagdako at (H-9)
            b.State(QuestStatus.Accepted, step: 0)
                // Reminders
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(17))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Engelhart).OnTrigger(QuestReply.Event(18))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Pagdako).OnTrigger(QuestReply.ProgressEvent(19))
                .OnEventFinish(Zone.BastokMarketsS, 19, q =>
                {
                    q.Quest.SetStep(1);
                });

            // Talk to Blatherix at (F-8)
            b.State(QuestStatus.Accepted, step: 1)
                // Reminders
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(17))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Engelhart).OnTrigger(QuestReply.Event(18))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Pagdako).OnTrigger(QuestReply.Event(20))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Blatherix).OnTrigger(QuestReply.ProgressEvent(21))
                .OnEventFinish(Zone.BastokMarketsS, 21, q =>
                {
                    q.Quest.SetStep(2);
                });

            // He asks you to bring him 30 chunks of Goblin Chocolate or 5000 gil to talk.
            b.State(QuestStatus.Accepted, step: 2)
                // Reminders
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(17))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Engelhart).OnTrigger(QuestReply.Event(18))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Pagdako).OnTrigger(QuestReply.Event(20))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Blatherix).OnTrigger(QuestReply.Event(22))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Blatherix).OnTrade(q =>
                {
                    if (q.Trade.HasExactly((Item.ChunkOfGoblinChocolate, 30)) ||
                        q.Trade.HasExactlyWithGil(5000))
                    {
                        return QuestReply.ProgressEvent(23);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BastokMarketsS, 23, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.MineShaftKey);
                    q.Quest.SetStep(3);
                });

            // Enter Pashhow Marshlands (S) from Grauberg (S) for a cutscene
            b.State(QuestStatus.Accepted, step: 3)
                .OnZoneIn(Zone.PashhowMarshlandsS, q =>
                {
                    if (q.Player.PreviousZone == Zone.GraubergS)
                    {
                        return QuestReply.Event(901);
                    }
                    return null;
                })
                .OnEventFinish(Zone.PashhowMarshlandsS, 901, q =>
                {
                    q.Quest.SetStep(4);
                })
                // Reminders
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(17))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Engelhart).OnTrigger(QuestReply.Event(18))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Pagdako).OnTrigger(QuestReply.Event(20))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Blatherix).OnTrigger(QuestReply.Event(24));

            // Check the Corroded Door at (F-5) in Pashhow Marshlands (S) to enter the battlefield for Light in the Darkness
            b.State(QuestStatus.Accepted, step: 4)
                // Instance entry handled in instance script

                // Reminders
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(17))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Engelhart).OnTrigger(QuestReply.Event(18))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Pagdako).OnTrigger(QuestReply.Event(20))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Blatherix).OnTrigger(QuestReply.Event(24));

            // Enter Pashhow Marshlands (S) the instance for a cutscene
            b.State(QuestStatus.Accepted, step: 5)
                .OnZoneIn(Zone.PashhowMarshlandsS, q => QuestReply.Event(902))
                .OnEventFinish(Zone.PashhowMarshlandsS, 902, q =>
                {
                    q.Quest.SetStep(6);
                });

            // Once you have beaten the Quadav in Ruhotz Silvermines, talk to Gentle Tiger for the final cutscene
            b.State(QuestStatus.Accepted, step: 6)
                // Reminders
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Engelhart).OnTrigger(QuestReply.Event(18))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Pagdako).OnTrigger(QuestReply.Event(20))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Blatherix).OnTrigger(QuestReply.Event(24))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.ProgressEvent(27))
                // TODO: Before new day for next quest: cs 28
                .OnEventFinish(Zone.BastokMarketsS, 27, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.SetQuestVar(QuestLog.CrystalWar, "BURDEN_OF_SUSPICION", "Timer", q.Player.VanadielUniqueDay + 1);
                        q.Player.SetQuestMustZone(QuestLog.CrystalWar, "BURDEN_OF_SUSPICION");
                    }
                });

            // Upon failure, a new key is needed. Blatherix will ask for 10 chunks of Goblin Chocolate or 1000 gil to give you one
            b.State(QuestStatus.Accepted, step: 7)
                // Reminders
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(17))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Engelhart).OnTrigger(QuestReply.Event(18))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Pagdako).OnTrigger(QuestReply.Event(20))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Blatherix).OnTrigger(QuestReply.Event(25))
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.Blatherix).OnTrade(q =>
                {
                    if (q.Trade.HasExactly((Item.ChunkOfGoblinChocolate, 10)) ||
                        q.Trade.HasExactlyWithGil(1000))
                    {
                        return QuestReply.ProgressEvent(23);
                    }
                    return null;
                })
                .OnEventFinish(Zone.BastokMarketsS, 23, q =>
                {
                    q.Trade.Confirm();
                    q.Player.GiveKeyItem(KeyItem.MineShaftKey);
                });
        }
    }
}
