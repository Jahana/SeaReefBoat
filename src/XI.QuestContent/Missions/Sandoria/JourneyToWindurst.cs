// Journey to Windurst, part 1 (San d'Oria 2-3, Log ID 0, Mission ID 7), a line-for-line port of
// scripts/missions/sandoria/2_3_2_Journey_to_Windurst.lua (quest rework C3 N2). Kupipi !pos 2 0.1 30 242; Mourices
// !pos -50.646 -0.501 -27.642 241; Uu Zhoumo !pos -179 16 155 145. One section (this mission current), no reward
// table. Giddeus: Uu Zhoumo with the Shield Offering plays 42 (progress; its finish moves to status 6, says
// OFFERED_UP_KEY_ITEM with the key item and takes it). Heaven's Tower: the zone-in at status 3 plays 42 (its update
// gives parameter 8 = 1 while The Path Untraveled is the current RoV mission; its finish moves to status 4); Kupipi
// at 4, 238 (progress; with ENABLE_TRUST_QUESTS 1 the parameters are 1, 1, 1, 1, San d'Oria's nation, 0, 0 and 1 with
// neither Semih Lafihna's spell nor his cipher), whose finish moves to status 5, gives the Shield Offering and, under
// that same test, the cipher; at 5, 240 and at 6, 241 (events). Windurst Woods: from status 3 on Catalia plays 452,
// Erpolant 454 and Forine 453 (progress events); Mourices at 3 to 5 plays 451 and at 6 456 (progress), and a trade of
// two Parana Shields at status 6 plays 457 (progress), whose finish completes the mission, takes the trade, begins
// Journey Abroad and moves to status 7.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Sandoria
{
    public sealed class JourneyToWindurst : MissionScript
    {
        public JourneyToWindurst() : base(XI.Quests.MissionLog.Sandoria, "JOURNEY_TO_WINDURST") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/sandoria/2_3_2_Journey_to_Windurst";

        // neither Semih Lafihna's spell nor his cipher
        static bool NeedsSemihTrust(IQuestContext q) =>
            !q.Player.HasSpell(Spell.SemihLafihna) && !q.Player.FindItem(Item.CipherOfSemihsAlterEgo);

        protected override void Build(MissionScriptBuilder b)
        {
            b.State(current: true)
                .Npc(Zone.Giddeus, Npc.Giddeus.UuZhoumo).OnTrigger(q =>
                {
                    if (q.Player.HasKeyItem(KeyItem.ShieldOffering))
                    {
                        return QuestReply.ProgressEvent(42);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Giddeus, 42, q =>
                {
                    q.Player.SetMissionStatus(MissionLog.Sandoria, 6);
                    q.Text.Special(Text.Giddeus.OFFERED_UP_KEY_ITEM, KeyItem.ShieldOffering);
                    q.Player.DelKeyItem(KeyItem.ShieldOffering);
                })
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Kupipi).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Sandoria);

                    if (missionStatus == 4)
                    {
                        if (q.Settings.MainEquals("ENABLE_TRUST_QUESTS", 1))
                        {
                            int needsSemihTrust = NeedsSemihTrust(q) ? 1 : 0;

                            return QuestReply.ProgressEvent(238, 1, 1, 1, 1, Nation.Sandoria, 0, 0, needsSemihTrust);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(238);
                        }
                    }
                    else if (missionStatus == 5)
                    {
                        return QuestReply.Event(240);
                    }
                    else if (missionStatus == 6)
                    {
                        return QuestReply.Event(241);
                    }

                    return null;
                })
                .OnZoneIn(Zone.HeavensTower, q =>
                    q.Player.MissionStatus(MissionLog.Sandoria) == 3 ? QuestReply.Event(42) : null)
                .OnEventUpdate(Zone.HeavensTower, 42, q =>
                {
                    int onPathUntraveled =
                        q.Player.CurrentMission(MissionLog.Rov) == Mission.Rov.ThePathUntraveled ? 1 : 0;

                    q.Player.UpdateEvent(0, 0, 0, 0, 0, 0, 0, onPathUntraveled);
                })
                .OnEventFinish(Zone.HeavensTower, 42, q =>
                    q.Player.SetMissionStatus(MissionLog.Sandoria, 4))
                .OnEventFinish(Zone.HeavensTower, 238, q =>
                {
                    q.Player.SetMissionStatus(MissionLog.Sandoria, 5);
                    q.Player.GiveKeyItem(KeyItem.ShieldOffering);

                    if (q.Settings.MainEquals("ENABLE_TRUST_QUESTS", 1) && NeedsSemihTrust(q))
                    {
                        q.Player.GiveItem(Item.CipherOfSemihsAlterEgo);
                    }
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Catalia).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(MissionLog.Sandoria) >= 3)
                    {
                        return QuestReply.ProgressEvent(452);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Erpolant).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(MissionLog.Sandoria) >= 3)
                    {
                        return QuestReply.ProgressEvent(454);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Forine).OnTrigger(q =>
                {
                    if (q.Player.MissionStatus(MissionLog.Sandoria) >= 3)
                    {
                        return QuestReply.ProgressEvent(453);
                    }

                    return null;
                })
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.Mourices)
                .OnTrade(q =>
                {
                    if (q.Trade.Matches((Item.ParanaShield, 2)) &&
                        q.Player.MissionStatus(MissionLog.Sandoria) == 6)
                    {
                        return QuestReply.ProgressEvent(457);
                    }

                    return null;
                })
                .OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Sandoria);

                    if (missionStatus >= 3 && missionStatus <= 5)
                    {
                        return QuestReply.ProgressEvent(451);
                    }
                    else if (missionStatus == 6)
                    {
                        return QuestReply.ProgressEvent(456);
                    }

                    return null;
                })
                .OnEventFinish(Zone.WindurstWoods, 457, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Trade.Complete();
                        q.Player.AddMission(MissionLog.Sandoria, Mission.Sandoria.JourneyAbroad);
                        q.Player.SetMissionStatus(MissionLog.Sandoria, 7);
                    }
                });
        }
    }
}
