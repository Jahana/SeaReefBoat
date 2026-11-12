// The Enduring Tumult of War (Promathia 5-1, Log ID 6, Mission ID 448), a line-for-line port of
// scripts/missions/cop/5_1_The_Enduring_Tumult_of_War.lua (quest rework C3). Despachiaire !pos 108 -40 -83 26;
// Chasalvige !pos 96.432 -0.520 134.046 231; Anoki !pos -22.397 -8.199 30.463 232; Cid !pos -12 -12 1 237. The mission
// current. Status 0 gathers four Option bits: Tavnazian Safehold's Despachiaire plays 117 (bit 0), Northern San
// d'Oria's Chasalvige 761 (bit 1; every other time his 764 replaces the default), Port San d'Oria's Anoki 724 (bit 2)
// and then 728 (bit 3); the Port Bastok zone-in plays 306 at Status 0 and its finish sets Status 1 (it does not look
// at the bits). Metalworks' Cid plays 849 at Status 1 (Status 2) and 863 as an important event above it. Psoxja: the
// zone-in at x -300 from Beaucedine Glacier at Status 2 plays 1 (Status 3); the _i98 at Status 3 with Nunyunuwi not
// spawned spawns and claims him and says TRAP_ACTIVATES to the char as the named entity (no action: the default is
// suppressed), his death at Status 3 sets Status 4; _i98 at Status 4 plays the optional cutscene 69 (z under 318) or
// 70 (otherwise); _i99 at Status 4 plays 2 whose finish with option 1 sets the char down in Promyvion - Vahzl.
// Promyvion - Vahzl's zone-in plays 50 at Status 4: the two key items, their two messages and the mission completes.
// Desires of Emptiness is next; no title.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class TheEnduringTumultOfWar : MissionScript
    {
        public TheEnduringTumultOfWar() : base(XI.Quests.MissionLog.CoP, "THE_ENDURING_TUMULT_OF_WAR") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/5_1_The_Enduring_Tumult_of_War";

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.DesiresOfEmptiness,
            });

            b.State(current: true)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Despachiaire).OnTrigger(q =>
                    q.Mission.Var("Status") == 0 && !q.Mission.IsVarBitsSet("Option", 0)
                        ? QuestReply.Event(117).ImportantEvent()
                        : null)
                .OnEventFinish(Zone.TavnazianSafehold, 117, q => q.Mission.SetVarBit("Option", 0))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Chasalvige).OnTrigger(q =>
                    q.Mission.Var("Status") == 0 && !q.Mission.IsVarBitsSet("Option", 1)
                        ? QuestReply.Event(761).ImportantEvent()
                        : QuestReply.Event(764).ReplaceDefault())
                .OnEventFinish(Zone.NorthernSanDoria, 761, q => q.Mission.SetVarBit("Option", 1))
                .Npc(Zone.PortSanDoria, Npc.PortSanDoria.Anoki).OnTrigger(q =>
                {
                    if (q.Mission.Var("Status") == 0)
                    {
                        if (!q.Mission.IsVarBitsSet("Option", 2))
                        {
                            return QuestReply.Event(724).ImportantEvent();
                        }

                        if (!q.Mission.IsVarBitsSet("Option", 3))
                        {
                            return QuestReply.Event(728).ImportantEvent();
                        }
                    }

                    return null;
                })
                .OnEventFinish(Zone.PortSanDoria, 724, q => q.Mission.SetVarBit("Option", 2))
                .OnEventFinish(Zone.PortSanDoria, 728, q => q.Mission.SetVarBit("Option", 3))
                .OnZoneIn(Zone.PortBastok, q => q.Mission.Var("Status") == 0 ? QuestReply.Event(306) : null)
                .OnEventFinish(Zone.PortBastok, 306, q => q.Mission.SetVar("Status", 1))
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid).OnTrigger(q =>
                {
                    int status = q.Mission.Var("Status");

                    if (status == 1)
                    {
                        return QuestReply.ProgressEvent(849);
                    }

                    return status > 1 ? QuestReply.Event(863).ImportantEvent() : null;
                })
                .OnEventFinish(Zone.Metalworks, 849, q => q.Mission.SetVar("Status", 2))
                .Npc(Zone.Psoxja, Npc.Psoxja.i98).OnTrigger(q =>
                {
                    int status = q.Mission.Var("Status");

                    if (status == 3 && !q.Player.MobIsSpawned(Mob.Psoxja.NUNYUNUWI))
                    {
                        q.Player.SpawnMob(Mob.Psoxja.NUNYUNUWI, 0, claim: true);
                        q.Text.MessageNameOfPlayer(Text.Psoxja.TRAP_ACTIVATES);
                        return QuestReply.NoAction();
                    }

                    if (status == 4)
                    {
                        // under z 318 the 69, else the 70
                        return QuestReply.ProgressOptionalCutscene(q.Player.ZPos < 318 ? 69 : 70).CsOption(0).CanSkip();
                    }

                    return null;
                })
                .Npc(Zone.Psoxja, Npc.Psoxja.i99).OnTrigger(q =>
                    q.Mission.Var("Status") == 4 ? QuestReply.ProgressEvent(2) : null)
                .Npc(Zone.Psoxja, Npc.Psoxja.Nunyunuwi).OnMobDeath(q =>
                {
                    if (q.Mission.Var("Status") == 3) q.Mission.SetVar("Status", 4);
                })
                .OnZoneIn(Zone.Psoxja, q =>
                    q.Player.XPos == -300 && q.Player.PreviousZone == Zone.BeaucedineGlacier &&
                    q.Mission.Var("Status") == 2
                        ? QuestReply.Event(1)
                        : null)
                .OnEventFinish(Zone.Psoxja, 1, q => q.Mission.SetVar("Status", 3))
                .OnEventFinish(Zone.Psoxja, 2, q =>
                {
                    // Promyvion - Vahzl
                    if (q.Option == 1) q.Player.SetPos(-14.744, 0.036, -119.736, 1, Zone.PromyvionVahzl);
                })
                .OnZoneIn(Zone.PromyvionVahzl, q => q.Mission.Var("Status") == 4 ? QuestReply.Event(50) : null)
                .OnEventFinish(Zone.PromyvionVahzl, 50, q =>
                {
                    q.Player.AddKeyItem(KeyItem.MysteriousAmuletDrained);
                    q.Player.AddKeyItem(KeyItem.LightOfVahzl);
                    q.Text.Special(Text.PromyvionVahzl.AMULET_RETURNED, KeyItem.MysteriousAmulet);
                    q.Text.Special(Text.PromyvionVahzl.LIGHT_OF_VAHZL, KeyItem.LightOfVahzl);
                    q.Mission.Complete();
                });
        }
    }
}
