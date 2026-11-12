// Dawn (Promathia 8-4, Log ID 6, Mission ID 840), a line-for-line port of scripts/missions/cop/8_4_Dawn.lua (quest
// rework C3, C4). From Dawn on (the CoP log's current mission at or past it, so The Last Verse current too), The Garden
// of Ru'Hmet's _0zy plays 140 at z 360 or below, else 141. The mission current: Empyreal Paradox's TR_Entrance plays 2
// at Status 0 (Status 1); the 32001 finish with Dawn won (battlefield.dawn sets battlefieldWin) at Status 1 sets Status
// 2; the zone-in plays 6 at Status 2 (its finish: Status 3, set down at 540 0 -514 in the zone) and 3 at Status 3 (its
// finish: the Tear of Altana, Timer 1 until the next JST midnight, Option 31, Status 4, set down in Al'Taieu). Ru'Lude
// Gardens' area 1 plays 122 at Status 4 with Option 0 (Status 5), with no Timer check (a kept Lua quirk with no visible
// effect: the Option bits are cleared only by final cutscenes that need the Timer gone); Upper Jeuno's _6s1 129 at
// Status 5 (Status 6, firstRing 1); Tavnazian Safehold's _0qa 543 at Status 6 (Status 7); Lufaise Meadows' area 1 the
// cutscene 116 at Status 7 (the Banisher of Emptiness title, Status 8); Norg's 232 and 234 finishes (Apocalypse Nigh's
// events) with option 1 to 4 at Status 8 complete the mission. At Status 4 with the Timer gone, each set Option bit
// opens its final cutscenes: bit 0 Louverance (Hinaree 757 at LProg 0, Uleguerand Range's zone-in 17 at LProg 1,
// Southern San d'Oria's area 1 758 at LProg 2, whose finish clears the bit), bit 1 Chebukkis (also open while a colored
// drop is owed: Chipmy-Popmy 619 at CProg 0; Warmachine gives the owed drop, else plays cutscene 43 at CProg 1, whose
// finish gives a random colored drop (Red Drop + 0 to 7) or owes it, and clears the bit), bit 2 Shikarees (Mhaura's
// zone-in 322), bit 3 Jabbos (Oldton Movalpolos' zone-in 57), bit 4 Tenzen (Metalworks' Cid 897); each finish clears
// its bit. With the Timer gone and Status 6 or more (or the mission completed), _6s1 plays 84 at firstRing 1, else 204,
// unless a Rajas, Sattva or Tamas Ring is held; update option 4 sends the three rings; finish option 5 to 7 gives that
// ring and sets firstRing 0. The Last Verse is next; no title.
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class Dawn : MissionScript
    {
        public Dawn() : base(XI.Quests.MissionLog.CoP, "DAWN") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/8_4_Dawn";

        // ringItems
        static readonly int[] RingItems =
        {
            Item.RajasRing,
            Item.SattvaRing,
            Item.TamasRing,
        };

        // utils.mask.getBit(mask, pos)
        static bool GetBit(int mask, int pos) => (mask & (1 << pos)) != 0;

        // utils.mask.setBit(mask, pos, false)
        static int ClearBit(int mask, int pos) => mask & ~(1 << pos);

        // the final cutscene sections' check past the mission being current (the state's current: true): Status 4, the
        // Timer gone and the section's bit
        static bool FinalCutscenes(IQuestCheck q, int bit) =>
            q.Mission.Var("Status") == 4 &&
            q.Mission.Var("Timer") == 0 &&
            GetBit(q.Mission.Var("Option"), bit);

        // a final cutscene's finish: its bit cleared
        static void ClearOptionBit(IQuestContext q, int bit) =>
            q.Mission.SetVar("Option", ClearBit(q.Mission.Var("Option"), bit));

        // ringOnEventUpdate
        static void RingOnEventUpdate(IQuestContext q)
        {
            if (q.Option == 4)
            {
                q.Player.UpdateEvent(RingItems);
            }
        }

        // ringOnEventFinish
        static void RingOnEventFinish(IQuestContext q)
        {
            if (q.Option >= 5 &&
                q.Option <= 7 &&
                q.Player.GiveItem(RingItems[q.Option - 5]))
            {
                q.Mission.SetVar("firstRing", 0);
            }
        }

        // missionOnEventFinish
        static void MissionOnEventFinish(IQuestContext q)
        {
            if (q.Option >= 1 &&
                q.Option <= 4 &&
                q.Mission.Var("Status") == 8)
            {
                q.Mission.Complete();
            }
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.TheLastVerse,
            });

            b.State(current: null, when: q => q.Player.CurrentMission(MissionLog.Cop) >= Mission.Cop.Dawn)
                .Npc(Zone.TheGardenOfRuhmet, Npc.TheGardenOfRuhmet._0zy).OnTrigger(q =>
                {
                    if (q.Player.ZPos <= 360)
                    {
                        return QuestReply.ProgressEvent(140);
                    }
                    else
                    {
                        return QuestReply.ProgressEvent(141);
                    }
                });

            b.State(current: true)
                .OnZoneIn(Zone.EmpyrealParadox, q =>
                {
                    int missionStatus = q.Mission.Var("Status");

                    if (missionStatus == 2)
                    {
                        return QuestReply.Event(6);
                    }
                    else if (missionStatus == 3)
                    {
                        return QuestReply.Event(3);
                    }

                    return null;
                })
                .Npc(Zone.EmpyrealParadox, Npc.EmpyrealParadox.TREntrance).OnTrigger(q =>
                    q.Mission.Var("Status") == 0 ? QuestReply.ProgressEvent(2) : null)
                .OnEventFinish(Zone.EmpyrealParadox, 2, q => q.Mission.SetVar("Status", 1))
                .OnEventFinish(Zone.EmpyrealParadox, 3, q =>
                {
                    q.Player.GiveKeyItem(KeyItem.TearOfAltana);
                    q.Mission.SetVar("Timer", 1, (uint)q.Player.JstMidnight);
                    q.Mission.SetVar("Option", 31);
                    q.Mission.SetVar("Status", 4);
                    q.Player.SetPos(0.18, -10, -470.43, 63, Zone.Altaieu);
                })
                .OnEventFinish(Zone.EmpyrealParadox, 6, q =>
                {
                    q.Mission.SetVar("Status", 3);
                    q.Player.SetPos(540, 0, -514, 63, Zone.EmpyrealParadox);
                })
                .OnEventFinish(Zone.EmpyrealParadox, 32001, q =>
                {
                    if (q.Mission.Var("Status") == 1 &&
                        q.Player.LocalVar("battlefieldWin") == Battlefield.Dawn)
                    {
                        q.Mission.SetVar("Status", 2);
                    }
                })
                .OnTriggerAreaEnter(Zone.RuludeGardens, 1, q =>
                    q.Mission.Var("Status") == 4 && q.Mission.Var("Option") == 0
                        ? QuestReply.ProgressEvent(122)
                        : null)
                .OnEventFinish(Zone.RuludeGardens, 122, q => q.Mission.SetVar("Status", 5))
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno._6s1).OnTrigger(q =>
                    q.Mission.Var("Status") == 5 ? QuestReply.ProgressEvent(129) : null)
                .OnEventFinish(Zone.UpperJeuno, 129, q =>
                {
                    q.Mission.SetVar("Status", 6);
                    q.Mission.SetVar("firstRing", 1);
                })
                .Npc(Zone.TavnazianSafehold, Npc.TavnazianSafehold._0qa).OnTrigger(q =>
                    q.Mission.Var("Status") == 6 ? QuestReply.ProgressEvent(543) : null)
                .OnEventFinish(Zone.TavnazianSafehold, 543, q => q.Mission.SetVar("Status", 7))
                .OnTriggerAreaEnter(Zone.LufaiseMeadows, 1, q =>
                    q.Mission.Var("Status") == 7 ? QuestReply.ProgressCutscene(116) : null)
                .OnEventFinish(Zone.LufaiseMeadows, 116, q =>
                {
                    q.Player.AddTitle(Title.BanisherOfEmptiness);
                    q.Mission.SetVar("Status", 8);
                })
                .OnEventFinish(Zone.Norg, 232, MissionOnEventFinish)
                .OnEventFinish(Zone.Norg, 234, MissionOnEventFinish);

            // Final Cutscenes - Louverance (Bit 0)
            b.State(current: true, when: q => FinalCutscenes(q, 0))
                .OnTriggerAreaEnter(Zone.SouthernSanDoria, 1, q =>
                    q.Mission.Var("LProg") == 2 ? QuestReply.ProgressEvent(758) : null)
                .Npc(Zone.SouthernSanDoria, Npc.SouthernSanDoria.Hinaree).OnTrigger(q =>
                    q.Mission.Var("LProg") == 0 ? QuestReply.ProgressEvent(757) : null)
                .OnEventFinish(Zone.SouthernSanDoria, 757, q => q.Mission.SetVar("LProg", 1))
                .OnEventFinish(Zone.SouthernSanDoria, 758, q => ClearOptionBit(q, 0))
                .OnZoneIn(Zone.UleguerandRange, q => q.Mission.Var("LProg") == 1 ? QuestReply.Event(17) : null)
                .OnEventFinish(Zone.UleguerandRange, 17, q => q.Mission.SetVar("LProg", 2));

            // Final Cutscenes - Chebukkis (Bit 1)
            b.State(current: true, when: q =>
                    q.Mission.Var("Status") == 4 &&
                    q.Mission.Var("Timer") == 0 &&
                    (GetBit(q.Mission.Var("Option"), 1) || q.Mission.Var("coloredDropId") > 0))
                .Npc(Zone.PortWindurst, Npc.PortWindurst.ChipmyPopmy).OnTrigger(q =>
                    q.Mission.Var("CProg") == 0 ? QuestReply.ProgressEvent(619) : null)
                .OnEventFinish(Zone.PortWindurst, 619, q => q.Mission.SetVar("CProg", 1))
                .Npc(Zone.BibikiBay, Npc.BibikiBay.Warmachine).OnTrigger(q =>
                {
                    int coloredDropId = q.Mission.Var("coloredDropId");

                    if (coloredDropId > 0 &&
                        q.Player.GiveItem(coloredDropId))
                    {
                        q.Mission.SetVar("coloredDropId", 0);
                    }
                    else if (q.Mission.Var("CProg") == 1)
                    {
                        return QuestReply.ProgressCutscene(43);
                    }

                    return null;
                })
                .OnEventFinish(Zone.BibikiBay, 43, q =>
                {
                    int coloredDropId = Item.RedDrop + q.Player.RandomInt(0, 7);

                    if (!q.Player.GiveItem(coloredDropId))
                    {
                        q.Mission.SetVar("coloredDropId", coloredDropId);
                    }

                    ClearOptionBit(q, 1);
                });

            // Final Cutscenes - Shikarees (Bit 2)
            b.State(current: true, when: q => FinalCutscenes(q, 2))
                .OnZoneIn(Zone.Mhaura, q => QuestReply.Event(322))
                .OnEventFinish(Zone.Mhaura, 322, q => ClearOptionBit(q, 2));

            // Final Cutscenes - Jabbos (Bit 3)
            b.State(current: true, when: q => FinalCutscenes(q, 3))
                .OnZoneIn(Zone.OldtonMovalpolos, q => QuestReply.Event(57))
                .OnEventFinish(Zone.OldtonMovalpolos, 57, q => ClearOptionBit(q, 3));

            // Final Cutscenes - Tenzen (Bit 4)
            b.State(current: true, when: q => FinalCutscenes(q, 4))
                .Npc(Zone.Metalworks, Npc.Metalworks.Cid).OnTrigger(QuestReply.ProgressEvent(897))
                .OnEventFinish(Zone.Metalworks, 897, q => ClearOptionBit(q, 4));

            // Ring granting or replacement
            b.State(current: null, when: q =>
                    ((q.Mission.IsCurrent &&
                      q.Mission.Var("Status") >= 6) ||
                     q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.Dawn)) &&
                    q.Mission.Var("Timer") == 0)
                .Npc(Zone.UpperJeuno, Npc.UpperJeuno._6s1).OnTrigger(q =>
                {
                    foreach (int itemId in RingItems)
                    {
                        if (q.Player.HasItem(itemId))
                        {
                            return null;
                        }
                    }

                    int eventId = q.Mission.Var("firstRing") == 1 ? 84 : 204;
                    return QuestReply.ProgressEvent(eventId);
                })
                .OnEventUpdate(Zone.UpperJeuno, 84, RingOnEventUpdate)
                .OnEventUpdate(Zone.UpperJeuno, 204, RingOnEventUpdate)
                .OnEventFinish(Zone.UpperJeuno, 84, RingOnEventFinish)
                .OnEventFinish(Zone.UpperJeuno, 204, RingOnEventFinish);
        }
    }
}
