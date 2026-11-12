// The Emissary Windurst (Bastok 2-3, Log ID 1, Mission ID 7), a line-for-line port of
// scripts/missions/bastok/2_3_2_The_Emissary_Windurst.lua (quest rework C3 N2). Kupipi !pos 2 0.1 30 242; Melek !pos
// -80 -5 158 240; Uu Zhoumo !pos -179 16 155 145. One section (this mission current), no reward table. Giddeus: Uu
// Zhoumo at status 4, 40 (its finish moves to 5), at 5, 43, from 6 on, 44; the trade of one Aspir Knife, 41 (its
// finish takes the trade and the Dull Sword and moves to status 6). Heaven's Tower: the zone-in at status 2 plays 42
// (its update gives parameter 8 = 1 while The Path Untraveled is the current RoV mission; its finish moves to status
// 3); Kupipi at 3, 239 (progress; with ENABLE_TRUST_QUESTS 1 the fourth parameter is Bastok's nation, the sixth is 1
// with no trust permit and the seventh 1 with neither Semih Lafihna's spell nor his cipher), whose finish moves to
// status 4, gives the Sword Offering and, under that same test, the cipher; at 5, 240 and at 6, 241 (events). Northern
// San d'Oria: Helaku says THE_EMISSARY_PLACEHOLDER. Port Windurst: Ada, Josef, Gold Skull and Melek; Ada plays 51,
// Josef 52; Gold Skull with the Sword Offering and no Dull Sword, 53 (its finish gives the Dull Sword and takes the
// offering), up to status 3, 50, up to 6, 54; Melek up to 5, 49, at 6, 55 (its finish completes the mission, begins
// The Emissary and moves to status 7). Kept as the Lua has it: Uu Zhoumo answers the Aspir Knife trade with 41 at any
// status, so a trade at status 2 to 4 skips Kupipi and the sword swap (41's finish moves to status 6).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Bastok
{
    public sealed class TheEmissaryWindurst : MissionScript
    {
        public TheEmissaryWindurst() : base(XI.Quests.MissionLog.Bastok, "THE_EMISSARY_WINDURST") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/bastok/2_3_2_The_Emissary_Windurst";

        // neither Semih Lafihna's spell nor his cipher
        static bool NeedsSemihTrust(IQuestContext q) =>
            !q.Player.HasSpell(Spell.SemihLafihna) && !q.Player.HasItem(Item.CipherOfSemihsAlterEgo);

        protected override void Build(MissionScriptBuilder b)
        {
            b.State(current: true)
                .Npc(Zone.Giddeus, Npc.Giddeus.UuZhoumo)
                .OnTrade(q => q.Trade.Matches((Item.AspirKnife, 1)) ? QuestReply.ProgressEvent(41) : null)
                .OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Bastok);

                    if (missionStatus == 4)
                    {
                        return QuestReply.ProgressEvent(40);
                    }
                    else if (missionStatus == 5)
                    {
                        return QuestReply.ProgressEvent(43);
                    }
                    else if (missionStatus >= 6)
                    {
                        return QuestReply.ProgressEvent(44);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Giddeus, 40, q =>
                    q.Player.SetMissionStatus(MissionLog.Bastok, 5))
                .OnEventFinish(Zone.Giddeus, 41, q =>
                {
                    q.Trade.Complete();
                    q.Player.DelKeyItem(KeyItem.DullSword);
                    q.Player.SetMissionStatus(MissionLog.Bastok, 6);
                })
                .Npc(Zone.HeavensTower, Npc.HeavensTower.Kupipi).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Bastok);

                    if (missionStatus == 3)
                    {
                        if (q.Settings.MainEquals("ENABLE_TRUST_QUESTS", 1))
                        {
                            int needsSemihTrust = NeedsSemihTrust(q) ? 1 : 0;
                            int hasTrustQuest =
                                q.Player.HasKeyItem(KeyItem.SanDoriaTrustPermit) ||
                                q.Player.HasKeyItem(KeyItem.BastokTrustPermit) ||
                                q.Player.HasKeyItem(KeyItem.WindurstTrustPermit)
                                    ? 0 : 1;

                            return QuestReply.ProgressEvent(239, 0, 0, 0, Nation.Bastok, 0, hasTrustQuest,
                                                            needsSemihTrust);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(239);
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
                    q.Player.MissionStatus(MissionLog.Bastok) == 2 ? QuestReply.Event(42) : null)
                .OnEventUpdate(Zone.HeavensTower, 42, q =>
                {
                    int onPathUntraveled =
                        q.Player.CurrentMission(MissionLog.Rov) == Mission.Rov.ThePathUntraveled ? 1 : 0;

                    q.Player.UpdateEvent(0, 0, 0, 0, 0, 0, 0, onPathUntraveled);
                })
                .OnEventFinish(Zone.HeavensTower, 42, q =>
                    q.Player.SetMissionStatus(MissionLog.Bastok, 3))
                .OnEventFinish(Zone.HeavensTower, 239, q =>
                {
                    q.Player.SetMissionStatus(MissionLog.Bastok, 4);
                    q.Player.GiveKeyItem(KeyItem.SwordOffering);

                    if (q.Settings.MainEquals("ENABLE_TRUST_QUESTS", 1) && NeedsSemihTrust(q))
                    {
                        q.Player.GiveItem(Item.CipherOfSemihsAlterEgo);
                    }
                })
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Helaku)
                .OnTrigger(q => QuestReply.MessageText(Text.NorthernSanDoria.THE_EMISSARY_PLACEHOLDER))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Ada).OnTrigger(q => QuestReply.Event(51))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.GoldSkull).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Bastok);

                    if (q.Player.HasKeyItem(KeyItem.SwordOffering) && !q.Player.HasKeyItem(KeyItem.DullSword))
                    {
                        return QuestReply.ProgressEvent(53);
                    }
                    else if (missionStatus <= 3)
                    {
                        return QuestReply.Event(50);
                    }
                    else if (missionStatus <= 6)
                    {
                        return QuestReply.ProgressEvent(54);
                    }

                    return null;
                })
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Josef).OnTrigger(q => QuestReply.Event(52))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.Melek).OnTrigger(q =>
                {
                    int missionStatus = q.Player.MissionStatus(MissionLog.Bastok);

                    if (missionStatus <= 5)
                    {
                        return QuestReply.ProgressEvent(49);
                    }
                    else if (missionStatus == 6)
                    {
                        return QuestReply.ProgressEvent(55);
                    }

                    return null;
                })
                .OnEventFinish(Zone.PortWindurst, 53, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.DullSword);
                    q.Player.DelKeyItem(KeyItem.SwordOffering);
                })
                .OnEventFinish(Zone.PortWindurst, 55, q =>
                {
                    if (q.Mission.Complete())
                    {
                        q.Player.AddMission(MissionLog.Bastok, Mission.Bastok.TheEmissary);
                        q.Player.SetMissionStatus(MissionLog.Bastok, 7);
                    }
                });
        }
    }
}
