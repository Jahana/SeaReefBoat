// Honor Under Fire (Log ID 7, Quest ID 43), a line-for-line port of
// scripts/quests/crystalWar/WOTG_BAS_8_Honor_Under_Fire.lua (quest rework B3b Crystal War group 4). Bastok Markets [S]:
// Gentle Tiger !pos -203.9 -10 2.2 (default 109). Vunkerl Inlet [S]: qm8 !pos -356.3 -32.1 286 (no default), Beastman
// Ensign !pos 241 -32 243 (no default).
// Available, Quelling the Storm completed and the current WotG mission Crossroads of Time or later: Gentle Tiger 66,
// whose finish begins it. Accepted, by Prog: 0: Gentle Tiger 67 (an Event); qm8 114 (-> 1). 1: Gentle Tiger 67;
// Beastman Ensign 115 (-> 2). 2: Gentle Tiger 68, whose finish gives the Flare Grenade and (giveKeyItem answers true)
// sets Prog 3. 3: Gentle Tiger 69 (an Event); Beastman Ensign 116 (-> 4). 4: Gentle Tiger: the grenade held: 69 (an
// Event); else 70, whose finish gives it again; Everbloom Hollow's 10000 finished: Prog 5 and the char set down by the
// Beastman Ensign in Vunkerl Inlet [S] ("Honor Under Fire instance is not implemented currently"). 5: the zone-in to
// Vunkerl Inlet [S] from Everbloom Hollow: 117 (-> 6). 6: Gentle Tiger 71, whose finish completes it (an Elixir Tank).
// Kept as the Lua has it: 66 begins on any option; 68 always sets Prog 3 (giveKeyItem answers true, the grenade held or
// not); nothing but Everbloom Hollow's 10000 (the instance, not implemented) sets Prog 5; 71 completes with no check of
// its result; no completed section.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class HonorUnderFire : QuestScript
    {
        public HonorUnderFire() : base(QuestLog.CrystalWar, "HONOR_UNDER_FIRE") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_BAS_8_Honor_Under_Fire";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4, 5, 6);

            b.Reward(new QuestReward
            {
                Item = Item.ElixirTank,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.CrystalWar, "QUELLING_THE_STORM") &&
                    q.Player.CurrentMission(MissionLog.Wotg) >= Mission.Wotg.CrossroadsOfTime)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.ProgressEvent(66))
                .OnEventFinish(Zone.BastokMarketsS, 66, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, step: 0)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(67))
                .Npc(Zone.VunkerlInletS, Npc.VunkerlInletS.qm8).OnTrigger(QuestReply.ProgressEvent(114))
                .OnEventFinish(Zone.VunkerlInletS, 114, q =>
                {
                    q.Quest.SetStep(1);
                });

            b.State(QuestStatus.Accepted, step: 1)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(67))
                .Npc(Zone.VunkerlInletS, Npc.VunkerlInletS.BeastmanEnsign).OnTrigger(QuestReply.ProgressEvent(115))
                .OnEventFinish(Zone.VunkerlInletS, 115, q =>
                {
                    q.Quest.SetStep(2);
                });

            b.State(QuestStatus.Accepted, step: 2)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.ProgressEvent(68))
                .OnEventFinish(Zone.BastokMarketsS, 68, q =>
                {
                    if (q.Player.GiveKeyItem(KeyItem.FlareGrenade))
                    {
                        q.Quest.SetStep(3);
                    }
                });

            b.State(QuestStatus.Accepted, step: 3)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.Event(69))
                .Npc(Zone.VunkerlInletS, Npc.VunkerlInletS.BeastmanEnsign).OnTrigger(QuestReply.ProgressEvent(116))
                .OnEventFinish(Zone.VunkerlInletS, 116, q =>
                {
                    q.Quest.SetStep(4);
                });

            b.State(QuestStatus.Accepted, step: 4)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.FlareGrenade))
                    {
                        return QuestReply.Event(69);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(70);
                    }
                })
                .OnEventFinish(Zone.BastokMarketsS, 70, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.FlareGrenade);
                })
                // Honor Under Fire instance is not implemented currently.
                .OnEventFinish(Zone.EverbloomHollow, 10000, q =>
                {
                    q.Quest.SetStep(5);
                    q.Player.SetPos(241.000, -31.995, 242.999, 0, Zone.VunkerlInletS);
                });

            b.State(QuestStatus.Accepted, step: 5)
                .OnZoneIn(Zone.VunkerlInletS, q =>
                {
                    if (q.Player.PreviousZone == Zone.EverbloomHollow)
                    {
                        return QuestReply.Event(117);
                    }
                    return null;
                })
                .OnEventFinish(Zone.VunkerlInletS, 117, q =>
                {
                    q.Quest.SetStep(6);
                });

            b.State(QuestStatus.Accepted, step: 6)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger).OnTrigger(QuestReply.ProgressEvent(71))
                .OnEventFinish(Zone.BastokMarketsS, 71, q =>
                {
                    q.Quest.Complete();
                });
        }
    }
}
