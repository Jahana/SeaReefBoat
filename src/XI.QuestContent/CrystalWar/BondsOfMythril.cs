// Bonds of Mythril (Log ID 7, Quest ID 59), a line-for-line port of
// scripts/quests/crystalWar/WOTG_BAS_12_Bonds_of_Mythril.lua (quest rework B3b Crystal War group 5). Bastok Markets
// [S]: Gentle Tiger !pos -203.9 -10 2.2 (default 109). Castle Zvahl Keep [S]: qm !pos -384.3 -52 -95.8 (default
// NOTHING_OUT_OF_ORDINARY), Keep Imps, the Gargouille Warden (GARGOUILLE_WARDEN, placed at the qm). Throne Room [S]:
// _4c1 !pos -115.8 -8.7 0 (no script, no default). Metalworks: Naji (default 700). Available, The Truth Lies Hid
// completed and the current WotG mission Adieu, Lilisette or later: the zone-in to Castle Zvahl Baileys [S] from Castle
// Zvahl Keep [S]: 13, whose finish begins it. Accepted, by Prog: 0: qm 17 (-> 1). 1: qm: ImpKills 4: the Warden popped
// (npcUtil.popFromQM, hide 0; when it pops, ImpKills 0) and A_SHIVER_RUNS_DOWN; else 17 (no Prog change: 17's finish is
// Prog 0's). A Keep Imp's death at Prog 1: ImpKills + 1, at most 4. The Warden's death at Prog 1: Prog 2. 2: qm 18,
// whose finish sets Prog 3 and gives the Zvahl Passkey. Accepted with the passkey (any Prog): _4c1 at Prog 3: 12 (->
// 4); the battlefield win 32001 with battlefieldWin = BONDS_OF_MYTHRIL (354) at Prog 4: Prog 5 ("battlefield is not
// implemented currently"). 5: Gentle Tiger 213 (87, 0, 2964, 0), whose finish sets Prog 6 and the char into the
// Metalworks (setPos). 6: any zone-in to the Metalworks: 972, whose finish sets Prog 7 and the char into North
// Gustaberg [S]. 7: any zone-in to North Gustaberg [S]: 13, whose finish completes it (an Excelsis Ring, Temperer of
// Mythril) and takes the passkey. Completed: Gentle Tiger 214, Naji 971 (Events, replaceDefault: What Price Loyalty's
// completed 356 at Gentle Tiger, and at Naji Trust: Bastok's completed 983 and Bastok mission 3-1's 717 at mission
// status 1 or more rank above them).
// Kept as the Lua has it: 13 begins on any option; 17 at Prog 1 has no finish of its section (its Prog 0 section's
// finish is not active at Prog 1); the Warden's death counts only at Prog 1; the passkey section matches at Prog 5-7
// too; the zone-ins at Prog 6 / 7 play from any zone; nothing but the battlefield sets Prog 5.
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.CrystalWar
{
    public sealed class BondsOfMythril : QuestScript
    {
        public BondsOfMythril() : base(QuestLog.CrystalWar, "BONDS_OF_MYTHRIL") { }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/crystalWar/WOTG_BAS_12_Bonds_of_Mythril";

        protected override void Build(QuestScriptBuilder b)
        {
            b.Steps(1, 2, 3, 4, 5, 6, 7);

            b.Reward(new QuestReward
            {
                Item = Item.ExcelsisRing,
                Title = Title.TempererOfMythril,
            });

            b.State(QuestStatus.Available, when: q =>
                    q.Player.HasCompletedQuest(QuestLog.CrystalWar, "THE_TRUTH_LIES_HID") &&
                    q.Player.CurrentMission(MissionLog.Wotg) >= Mission.Wotg.AdieuLilisette)
                .OnZoneIn(Zone.CastleZvahlBaileysS, q =>
                {
                    if (q.Player.PreviousZone == Zone.CastleZvahlKeepS)
                    {
                        return QuestReply.Event(13);
                    }
                    return null;
                })
                .OnEventFinish(Zone.CastleZvahlBaileysS, 13, q =>
                {
                    q.Quest.Begin();
                });

            b.State(QuestStatus.Accepted, step: 0)
                .Npc(Zone.CastleZvahlKeepS, Npc.CastleZvahlKeepS.qm).OnTrigger(QuestReply.ProgressEvent(17))
                .OnEventFinish(Zone.CastleZvahlKeepS, 17, q =>
                {
                    q.Quest.SetStep(1);
                });

            b.State(QuestStatus.Accepted, step: 1)
                .Npc(Zone.CastleZvahlKeepS, Npc.CastleZvahlKeepS.qm).OnTrigger(q =>
                {
                    if (q.Quest.Var("ImpKills") >= 4)
                    {
                        if (q.Player.PopFromQm(Mob.CastleZvahlKeepS.GARGOUILLE_WARDEN, 0))
                        {
                            q.Quest.SetVar("ImpKills", 0);
                        }

                        return QuestReply.MessageSpecial(Text.CastleZvahlKeepS.A_SHIVER_RUNS_DOWN);
                    }

                    return QuestReply.ProgressEvent(17);
                })
                .Npc(Zone.CastleZvahlKeepS, Npc.CastleZvahlKeepS.KeepImp).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        int impKills = q.Quest.Var("ImpKills");
                        q.Quest.SetVar("ImpKills", System.Math.Min(4, impKills + 1));
                    }
                })
                .Npc(Zone.CastleZvahlKeepS, Npc.CastleZvahlKeepS.GargouilleWarden).OnMobDeath(q =>
                {
                    if (q.Quest.Step == 1)
                    {
                        q.Quest.SetStep(2);
                    }
                });

            b.State(QuestStatus.Accepted, step: 2)
                .Npc(Zone.CastleZvahlKeepS, Npc.CastleZvahlKeepS.qm).OnTrigger(QuestReply.ProgressEvent(18))
                .OnEventFinish(Zone.CastleZvahlKeepS, 18, q =>
                {
                    q.Quest.SetStep(3);
                    q.Player.GiveKeyItem(KeyItem.ZvahlPasskey);
                });

            b.State(QuestStatus.Accepted, when: q => q.Player.HasKeyItem(KeyItem.ZvahlPasskey))
                .Npc(Zone.ThroneRoomS, Npc.ThroneRoomS._4c1).OnTrigger(q =>
                {
                    if (q.Quest.Step == 3)
                    {
                        return QuestReply.ProgressEvent(12);
                    }
                    return null;
                })
                .OnEventFinish(Zone.ThroneRoomS, 12, q =>
                {
                    q.Quest.SetStep(4);
                })
                // Bonds of Mythril battlefield is not implemented currently.
                .OnEventFinish(Zone.ThroneRoomS, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.BondsOfMythril &&
                        q.Quest.Step == 4)
                    {
                        q.Quest.SetStep(5);
                    }
                });

            b.State(QuestStatus.Accepted, step: 5)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger)
                    .OnTrigger(q => QuestReply.ProgressEvent(213, 87, 0, 2964, 0))
                .OnEventFinish(Zone.BastokMarketsS, 213, q =>
                {
                    q.Quest.SetStep(6);
                    q.Player.SetPos(52.272, -14.000, -0.017, 127, Zone.Metalworks);
                });

            b.State(QuestStatus.Accepted, step: 6)
                .OnZoneIn(Zone.Metalworks, q => QuestReply.Event(972))
                .OnEventFinish(Zone.Metalworks, 972, q =>
                {
                    q.Quest.SetStep(7);
                    q.Player.SetPos(380.062, 0.753, 147.908, 194, Zone.NorthGustabergS);
                });

            b.State(QuestStatus.Accepted, step: 7)
                .OnZoneIn(Zone.NorthGustabergS, q => QuestReply.Event(13))
                .OnEventFinish(Zone.NorthGustabergS, 13, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Player.DelKeyItem(KeyItem.ZvahlPasskey);
                    }
                });

            b.State(QuestStatus.Completed)
                .Npc(Zone.BastokMarketsS, Npc.BastokMarketsS.GentleTiger)
                    .OnTrigger(QuestReply.Event(214).ReplaceDefault())
                .Npc(Zone.Metalworks, Npc.Metalworks.Naji).OnTrigger(QuestReply.Event(971).ReplaceDefault());
        }
    }
}
