// Three Paths (Promathia 5-3, Log ID 6, Mission ID 530), a line-for-line port of
// scripts/missions/cop/5_3_Three_Paths.lua (quest rework C3, C3b). The mission status word holds three 4-bit path
// fields: Louverance's (position 2), Tenzen's (3) and Ulmia's (4), each read by its own section while it is 14 or
// less. Cid shows the common reminder (851, priority 995, its argument the sum of the paths already at 14:
// Louverance 1, Tenzen 2, Ulmia 4) while the local var cidOption is 0 and no path waits for Cid (Louverance 12,
// Tenzen 12, Ulmia 9; those three have progress events that outrank it anyway, so a replay does not see the guards);
// its finish sets cidOption 1. Louverance's path: Despachiaire 118 (2; every later trigger plays 134), Perih Vashai
// 686 (3) and 687, Warmachine's cutscene 33 (6), Oldton Movalpolos' zone-in at 6 (event 1, 8), Cid 852 at 9
// (cidOption 0, 11), Tarnotik 34 at 11, the Gold Key traded to Mine Shaft #2716's _0d0 at 11 (progress event 3: the
// trade is taken, 12), Cid 853 at 12 (14 and the Companion of Louverance title); the Century of Hardship's 32001 at 8
// sets 9 (that battlefield stays Lua). Tenzen's path: La Theine Plateau qm3 203 (2), Psoxja _09g's cutscene 3 (3),
// Upper Jeuno's Monberaux 74 (the Envelope from Monberaux, 5), Ru'Lude Gardens' Pherimociel 58 (6), Batallia Downs qm4
// (Option 0: cutscene 0 sets Option 1; Option 1: event 1 gives the Delkfutt Recognition Device, Option 0, 8), Lower
// Delkfutt's Tower _545 at 8 with the device (the Disaster Idol pops from it, claimed; its death sets hasKilled; then
// cutscene 25, 9), Psoxja's zone-in at x 220 at 9 (event 4, 11), _09h's cutscene 5 (12), Cid 854 at 12 (14 and the
// Tenzen's Ally title). Ulmia's path: Hinaree 22 (2), Port San d'Oria's zone-in at 2 (event 4, 3), Chasalvige 762
// (4), Kerutoto 876 (6), Yoran-Oran 473 (7), Head Wind's 32001 at 7 sets 8, Flames for the Dead's at 8 sets 9, Cid 855
// at 9 (14 and the Ulmia's Soulmate title). A path's last Cid finish completes the mission when all three are 14 (For
// Whom the Verse is Sung is next, the Treader of an Icy Past title), else sets cidOption 0. Behaviour ported as is:
// Ulmia's section bound 14 cannot be told from 13 or 15 (no Ulmia handler reads a value over 9).
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class ThreePaths : MissionScript
    {
        public ThreePaths() : base(XI.Quests.MissionLog.CoP, "THREE_PATHS") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/5_3_Three_Paths";

        // xi.mission.status.COP positions
        const int Louverance = 2;
        const int Tenzen = 3;
        const int Ulmia = 4;

        // getCidEventArg: the offset of every path at 14
        static int CidEventArg(IQuestContext q)
        {
            int arg = 0;

            if (q.Mission.StatusAt(Louverance) == 14) arg += 1;
            if (q.Mission.StatusAt(Tenzen) == 14) arg += 2;
            if (q.Mission.StatusAt(Ulmia) == 14) arg += 4;

            return arg;
        }

        // isMissionComplete
        static bool IsMissionComplete(IQuestContext q) =>
            q.Mission.StatusAt(Louverance) == 14 && q.Mission.StatusAt(Tenzen) == 14 && q.Mission.StatusAt(Ulmia) == 14;

        // the Cid finish of a path: its 14 and title, then the mission completes or the reminder resets
        static void PathDone(IQuestContext q, int pos, int title)
        {
            q.Mission.SetStatusAt(14, pos);
            q.Player.AddTitle(title);

            if (IsMissionComplete(q))
            {
                q.Mission.Complete();
            }
            else
            {
                q.Mission.SetLocalVar("cidOption", 0);
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                Title = Title.TreaderOfAnIcyPast,
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.ForWhomTheVerseIsSung,
            });

            // Cid reminder dialogue, common to all paths
            b.State(current: true)
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid).OnTrigger(q =>
                {
                    if (q.Mission.LocalVar("cidOption") == 0 &&
                        q.Mission.StatusAt(Louverance) != 12 &&
                        q.Mission.StatusAt(Tenzen) != 12 &&
                        q.Mission.StatusAt(Ulmia) != 9)
                    {
                        return QuestReply.Event(851, CidEventArg(q)).SetPriority(995);
                    }

                    return null;
                })
                .OnEventFinish(Zone.Metalworks, 851, q => q.Mission.SetLocalVar("cidOption", 1));

            // Louverance's Path
            b.State(current: true, when: q => q.Mission.StatusAt(Louverance) <= 14)
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold.Despachiaire).OnTrigger(q =>
                    q.Mission.StatusAt(Louverance) == 0
                        ? QuestReply.Event(118).ImportantEvent()
                        : QuestReply.Event(134))
                .OnEventFinish(Zone.TavnazianSafehold, 118, q => q.Mission.SetStatusAt(2, Louverance))
                .Npc(Zone.WindurstWoods, Npc.WindurstWoods.PerihVashai).OnTrigger(q =>
                {
                    int missionStatus = q.Mission.StatusAt(Louverance);

                    if (missionStatus == 2) return QuestReply.Event(686).ImportantEvent();
                    if (missionStatus == 3) return QuestReply.Event(687).ImportantEvent();
                    return null;
                })
                .OnEventFinish(Zone.WindurstWoods, 686, q => q.Mission.SetStatusAt(3, Louverance))
                .Npc(Zone.BibikiBay, Npc.BibikiBay.Warmachine).OnTrigger(q =>
                    q.Mission.StatusAt(Louverance) == 3 ? QuestReply.ProgressCutscene(33) : null)
                .OnEventFinish(Zone.BibikiBay, 33, q => q.Mission.SetStatusAt(6, Louverance))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.YoranOran).OnTrigger(q =>
                    q.Mission.StatusAt(Louverance) == 6 ? QuestReply.Event(481).ImportantEvent() : null)
                .Npc(Zone.OldtonMovalpolos, Npc.OldtonMovalpolos.Tarnotik).OnTrigger(q =>
                    q.Mission.StatusAt(Louverance) == 11 ? QuestReply.Event(34).ImportantEvent() : null)
                .OnZoneIn(Zone.OldtonMovalpolos, q => q.Mission.StatusAt(Louverance) == 6 ? QuestReply.Event(1) : null)
                .OnEventFinish(Zone.OldtonMovalpolos, 1, q => q.Mission.SetStatusAt(8, Louverance))
                .Npc(Zone.MineShaft2716, Npc.MineShaft2716._0d0).OnTrade(q =>
                    q.Mission.StatusAt(Louverance) == 11 && q.Trade.Matches((Item.GoldKey, 1))
                        ? QuestReply.ProgressEvent(3)
                        : null)
                .OnEventFinish(Zone.MineShaft2716, 3, q =>
                {
                    q.Trade.Complete();
                    q.Mission.SetStatusAt(12, Louverance);
                })
                .OnEventFinish(Zone.MineShaft2716, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.CenturyOfHardship &&
                        q.Mission.StatusAt(Louverance) == 8)
                    {
                        q.Mission.SetStatusAt(9, Louverance);
                    }
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid).OnTrigger(q =>
                {
                    int missionStatus = q.Mission.StatusAt(Louverance);

                    if (missionStatus == 9) return QuestReply.ProgressEvent(852);
                    if (missionStatus == 12) return QuestReply.ProgressEvent(853, CidEventArg(q));
                    return null;
                })
                .OnEventFinish(Zone.Metalworks, 852, q =>
                {
                    q.Mission.SetLocalVar("cidOption", 0);
                    q.Mission.SetStatusAt(11, Louverance);
                })
                .OnEventFinish(Zone.Metalworks, 853, q => PathDone(q, Louverance, Title.CompanionOfLouverance));

            // Tenzen's Path
            b.State(current: true, when: q => q.Mission.StatusAt(Tenzen) <= 14)
                .Npc(Zone.LaTheinePlateau, Npc.LaTheinePlateau.qm3).OnTrigger(q =>
                    q.Mission.StatusAt(Tenzen) == 0 ? QuestReply.Event(203).ImportantEvent() : null)
                .OnEventFinish(Zone.LaTheinePlateau, 203, q => q.Mission.SetStatusAt(2, Tenzen))
                .Npc(Zone.Psoxja, Npc.Psoxja._09g).OnTrigger(q =>
                    q.Mission.StatusAt(Tenzen) == 2 ? QuestReply.ProgressCutscene(3) : null)
                .Npc(Zone.Psoxja, Npc.Psoxja._09h).OnTrigger(q =>
                    q.Mission.StatusAt(Tenzen) == 11 ? QuestReply.ProgressCutscene(5) : null)
                .OnZoneIn(Zone.Psoxja, q =>
                    q.Player.XPos == 220 && q.Mission.StatusAt(Tenzen) == 9 ? QuestReply.Event(4) : null)
                .OnEventFinish(Zone.Psoxja, 3, q => q.Mission.SetStatusAt(3, Tenzen))
                .OnEventFinish(Zone.Psoxja, 4, q => q.Mission.SetStatusAt(11, Tenzen))
                .OnEventFinish(Zone.Psoxja, 5, q => q.Mission.SetStatusAt(12, Tenzen))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Monberaux).OnTrigger(q =>
                {
                    int missionStatus = q.Mission.StatusAt(Tenzen);

                    if (missionStatus == 3) return QuestReply.ProgressEvent(74);
                    if (missionStatus == 5) return QuestReply.Event(5).ImportantEvent();
                    if (missionStatus == 6) return QuestReply.Event(6).ImportantEvent();
                    if (missionStatus == 8) return QuestReply.Event(7).ImportantEvent();
                    return null;
                })
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno.Rosaline).OnTrigger(q => QuestReply.Event(96))
                .OnEventFinish(Zone.UpperJeuno, 74, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.EnvelopeFromMonberaux);
                    q.Mission.SetStatusAt(5, Tenzen);
                })
                .Npc(Zone.RuludeGardens, Npc.RuludeGardens.Pherimociel).OnTrigger(q =>
                    q.Mission.StatusAt(Tenzen) == 5 ? QuestReply.ProgressEvent(58) : null)
                .OnEventFinish(Zone.RuludeGardens, 58, q => q.Mission.SetStatusAt(6, Tenzen))
                .Npc(Zone.BatalliaDowns, Npc.BatalliaDowns.qm4).OnTrigger(q =>
                {
                    if (q.Mission.StatusAt(Tenzen) == 6)
                    {
                        return q.Mission.Var("Option") == 1
                            ? QuestReply.ProgressEvent(1)
                            : QuestReply.ProgressCutscene(0);
                    }

                    return null;
                })
                .OnEventFinish(Zone.BatalliaDowns, 0, q => q.Mission.SetVar("Option", 1))
                .OnEventFinish(Zone.BatalliaDowns, 1, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.DelkfuttRecognitionDevice);
                    q.Mission.SetVar("Option", 0);
                    q.Mission.SetStatusAt(8, Tenzen);
                })
                .Npc(Zone.LowerDelkfuttsTower, Npc.LowerDelkfuttsTower._545).OnTrigger(q =>
                {
                    if (q.Mission.StatusAt(Tenzen) == 8 && q.Player.HasKeyItem(KeyItem.DelkfuttRecognitionDevice))
                    {
                        if (q.Mission.LocalVar("hasKilled") == 0 &&
                            q.Player.PopFromQm(Mob.LowerDelkfuttsTower.DISASTER_IDOL, hide: 0))
                        {
                            return QuestReply.MessageSpecial(Text.LowerDelkfuttsTower.SOMETHING_HUGE_BEARING_DOWN);
                        }
                        else if (q.Mission.LocalVar("hasKilled") == 1)
                        {
                            return QuestReply.ProgressCutscene(25);
                        }
                    }

                    return null;
                })
                .Npc(Zone.LowerDelkfuttsTower, Npc.LowerDelkfuttsTower.DisasterIdol).OnMobDeath(q =>
                {
                    if (q.Mission.LocalVar("hasKilled") == 0) q.Mission.SetLocalVar("hasKilled", 1);
                })
                .OnEventFinish(Zone.LowerDelkfuttsTower, 25, q => q.Mission.SetStatusAt(9, Tenzen))
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid).OnTrigger(q =>
                    q.Mission.StatusAt(Tenzen) == 12 ? QuestReply.ProgressEvent(854, CidEventArg(q)) : null)
                .OnEventFinish(Zone.Metalworks, 854, q => PathDone(q, Tenzen, Title.TenzensAlly));

            // Ulmia's Path
            b.State(current: true, when: q => q.Mission.StatusAt(Ulmia) <= 14)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Hinaree).OnTrigger(q =>
                {
                    int missionStatus = q.Mission.StatusAt(Ulmia);

                    if (missionStatus == 0) return QuestReply.ProgressEvent(22);
                    if (missionStatus == 2) return QuestReply.Event(25).ImportantEvent();
                    return null;
                })
                .OnEventFinish(Zone.SouthernSanDoria, 22, q => q.Mission.SetStatusAt(2, Ulmia))
                .OnZoneIn(Zone.PortSanDoria, q => q.Mission.StatusAt(Ulmia) == 2 ? QuestReply.Event(4) : null)
                .OnEventFinish(Zone.PortSanDoria, 4, q => q.Mission.SetStatusAt(3, Ulmia))
                .Npc(Zone.NorthernSanDoria, Npc.NorthernSanDoria.Chasalvige).OnTrigger(q =>
                {
                    int missionStatus = q.Mission.StatusAt(Ulmia);

                    if (missionStatus == 3) return QuestReply.ProgressEvent(762);
                    if (missionStatus == 4) return QuestReply.Event(763).ImportantEvent();
                    return null;
                })
                .OnEventFinish(Zone.NorthernSanDoria, 762, q => q.Mission.SetStatusAt(4, Ulmia))
                .Npc(Zone.WindurstWaters, Npc.WindurstWaters.Kerutoto).OnTrigger(q =>
                {
                    int missionStatus = q.Mission.StatusAt(Ulmia);

                    if (missionStatus == 4) return QuestReply.ProgressEvent(876);
                    if (missionStatus == 6) return QuestReply.Event(882).ImportantEvent();
                    return null;
                })
                .OnEventFinish(Zone.WindurstWaters, 876, q => q.Mission.SetStatusAt(6, Ulmia))
                .Npc(Zone.WindurstWalls, Npc.WindurstWalls.YoranOran).OnTrigger(q =>
                {
                    int missionStatus = q.Mission.StatusAt(Ulmia);

                    if (missionStatus == 6) return QuestReply.ProgressEvent(473);
                    if (missionStatus == 7) return QuestReply.Event(479).ImportantEvent();
                    return null;
                })
                .OnEventFinish(Zone.WindurstWalls, 473, q => q.Mission.SetStatusAt(7, Ulmia))
                .OnEventFinish(Zone.BoneyardGully, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.HeadWind &&
                        q.Mission.StatusAt(Ulmia) == 7)
                    {
                        q.Mission.SetStatusAt(8, Ulmia);
                    }
                })
                .OnEventFinish(Zone.BearclawPinnacle, 32001, q =>
                {
                    if (q.Player.LocalVar("battlefieldWin") == Battlefield.FlamesForTheDead &&
                        q.Mission.StatusAt(Ulmia) == 8)
                    {
                        q.Mission.SetStatusAt(9, Ulmia);
                    }
                })
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid).OnTrigger(q =>
                    q.Mission.StatusAt(Ulmia) == 9 ? QuestReply.ProgressEvent(855, CidEventArg(q)) : null)
                .OnEventFinish(Zone.Metalworks, 855, q => PathDone(q, Ulmia, Title.UlmiasSoulmate));
        }
    }
}
