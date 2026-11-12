// Garden of Antiquity (Promathia 8-1, Log ID 6, Mission ID 800), a line-for-line port of
// scripts/missions/cop/8_1_Garden_of_Antiquity.lua (quest rework C3, C4). The mission current. Al'Taieu's three Rubious
// Crystals (_0x1, _0x2, _0x3) are towers 0, 2 and 1 (towerOption); tower 0's done bit is bit 3 of the CID status
// nibble, tower n's (1, 2) bit n - 1 of the Rubious nibble (xi.mission.status.COP, positions 6 and 7). At Status 1 a
// tower not done pops its three Ru'aern (npcUtil.popFromQM, ids RUAERN + 3 * the crystal's index, hide 0), sets the
// local vars triggerNpc and nmOffset on every alliance member and says OMINOUS_SHADOW; once its nmDefeated bit is set
// it sets its done bit and plays the cutscene 161 (the tower's option) for the first tower done, 163 for the third,
// else 162 with the first tower not done counting from 2 down. A Ru'aern's death counts toward numDefeated when it is
// one of the three at the killer's nmOffset; the third sets the nmDefeated bit of the trigger NPC's tower and resets
// the count (an invalid trigger NPC prints an error and stops). _0x0 plays the cutscene 164 at Status 0 (Status 1) and,
// once all three towers are done, the optional cutscene 100 (cs_option 0, skippable), whose finish sets Status 2 and
// with option 1 sets the char down in the Grand Palace of Hu'Xzoi. There _iya plays 1 at Status 2 (its finish gives the
// Tavnazian Ring and sets Status 3), else DOES_NOT_RESPOND at priority 1000; _iyb plays 2 at Status 3, whose finish
// completes the mission. Once complete, _0x0 still plays 100 (option 1: the same setPos). Kept Lua quirks:
// getRubiousMask reads four bits, the fourth (tower bit 3) being Rubious bit 2, so a set Rubious bit 2 makes the mask
// 15 and _0x0 then gives nothing; and _0x0 still plays 100 at Status 3, after the ring, whose finish sets Status 2
// again, so _iyb goes silent and _iya plays 1 again and tries to give the Tavnazian Ring again. A Fate Decided is next;
// no title.
using System.Collections.Generic;
using XI.Quests.Authoring;

namespace XI.QuestContent.Missions.Cop
{
    public sealed class GardenOfAntiquity : MissionScript
    {
        public GardenOfAntiquity() : base(XI.Quests.MissionLog.CoP, "GARDEN_OF_ANTIQUITY") { }

        // the require name of the Lua file this mission replaces: its slot in the containers list
        public override string LegacyPath => "scripts/missions/cop/8_1_Garden_of_Antiquity";

        // xi.mission.status.COP.CID and RUBIOUS
        const int Cid = 6;
        const int Rubious = 7;

        // NOTE: Tower event parameters do not align with NPCs in the database. This table maps to the expected
        // parameter, and also is used to determine which bit is set in missionStatus.
        static readonly Dictionary<string, int> TowerOption = new Dictionary<string, int>
        {
            [Npc.Altaieu._0x1] = 0,
            [Npc.Altaieu._0x2] = 2,
            [Npc.Altaieu._0x3] = 1,
        };

        // utils.mask.getBit / setBit / countBits
        static bool GetBit(int mask, int pos) => (mask & (1 << pos)) != 0;
        static int SetBit(int mask, int pos) => mask | (1 << pos);

        static int CountBits(int mask, int len)
        {
            int count = 0;

            for (int i = 0; i < len; i++)
            {
                count += (mask >> i) & 1;
            }

            return count;
        }

        // setMissionStatusBit
        static void SetMissionStatusBit(IQuestContext q, int bitNum)
        {
            int statusIndex = bitNum == 0 ? Cid : Rubious;
            int adjustedBit = bitNum == 0 ? 3 : bitNum - 1;
            int missionStatus = q.Mission.StatusAt(statusIndex);
            q.Mission.SetStatusAt(SetBit(missionStatus, adjustedBit), statusIndex);
        }

        // getMissionStatusBit
        static bool GetMissionStatusBit(IQuestContext q, int bitNum)
        {
            int statusIndex = bitNum == 0 ? Cid : Rubious;
            int adjustedBit = bitNum == 0 ? 3 : bitNum - 1;
            int missionStatus = q.Mission.StatusAt(statusIndex);
            return GetBit(missionStatus, adjustedBit);
        }

        // getRubiousMask
        static int GetRubiousMask(IQuestContext q)
        {
            int rubiousMask = 0;

            for (int bitNum = 0; bitNum <= 3; bitNum++)
            {
                if (GetMissionStatusBit(q, bitNum))
                {
                    rubiousMask = SetBit(rubiousMask, bitNum);
                }
            }

            return rubiousMask;
        }

        // rubiousOnTrigger
        static QuestReply RubiousOnTrigger(IQuestContext q)
        {
            string npcName = q.Npc.Name;
            bool nmDefeated = GetBit(q.Mission.LocalVar("nmDefeated"), TowerOption[npcName]);
            int nmOffset = q.Player.MobIdOf(Mob.Altaieu.RUAERN) +
                           (q.Npc.Id - q.Player.NpcIdOf("RUBIOUS_CRYSTAL")) * 3;

            if (q.Mission.Var("Status") == 1 && !GetMissionStatusBit(q, TowerOption[npcName]))
            {
                if (!nmDefeated && q.Player.PopFromQmIds(new[] { nmOffset, nmOffset + 1, nmOffset + 2 }, hide: 0))
                {
                    q.Mission.SetAllianceLocalVars(("triggerNpc", q.Npc.Id), ("nmOffset", nmOffset));
                    return QuestReply.MessageSpecial(Text.Altaieu.OMINOUS_SHADOW);
                }
                else if (nmDefeated)
                {
                    SetMissionStatusBit(q, TowerOption[npcName]);
                    int completedTowers = CountBits(GetRubiousMask(q), 3);

                    if (completedTowers == 1)
                    {
                        return QuestReply.ProgressCutscene(161, TowerOption[npcName]);
                    }
                    else if (completedTowers == 3)
                    {
                        return QuestReply.ProgressCutscene(163);
                    }
                    else
                    {
                        for (int towerNum = 2; towerNum >= 0; towerNum--)
                        {
                            if (!GetMissionStatusBit(q, towerNum))
                            {
                                return QuestReply.ProgressCutscene(162, towerNum);
                            }
                        }
                    }
                }
            }

            return null;
        }

        protected override void Build(MissionScriptBuilder b)
        {
            b.Reward(new MissionReward
            {
                NextLog = XI.Quests.MissionLog.CoP,
                NextMission = Mission.Cop.AFateDecided,
            });

            b.State(current: true)
                .Npc(Zone.Altaieu, Npc.Altaieu._0x0).OnTrigger(q =>
                {
                    int missionStatus = q.Mission.Var("Status");

                    if (missionStatus == 0)
                    {
                        return QuestReply.ProgressCutscene(164);
                    }
                    else if (GetRubiousMask(q) == 7)
                    {
                        return QuestReply.ProgressOptionalCutscene(100).CsOption(0).CanSkip();
                    }

                    return null;
                })
                .Npc(Zone.Altaieu, Npc.Altaieu._0x1).OnTrigger(RubiousOnTrigger)
                .Npc(Zone.Altaieu, Npc.Altaieu._0x2).OnTrigger(RubiousOnTrigger)
                .Npc(Zone.Altaieu, Npc.Altaieu._0x3).OnTrigger(RubiousOnTrigger)
                .Npc(Zone.Altaieu, Npc.Altaieu.Ruaern).OnMobDeath(q =>
                {
                    int nmOffset = q.Mission.LocalVar("nmOffset");
                    int numDefeated = q.Mission.LocalVar("numDefeated");

                    if (nmOffset > 0)
                    {
                        for (int nmId = nmOffset; nmId <= nmOffset + 2; nmId++)
                        {
                            if (q.Mob.Id == nmId)
                            {
                                numDefeated = numDefeated + 1;
                            }
                        }

                        if (numDefeated == 3)
                        {
                            var npcObj = q.Player.NpcById(q.Mission.LocalVar("triggerNpc"));

                            if (npcObj == null)
                            {
                                q.Player.LuaPrint("ERROR: triggerNpc returned invalid NPC object.");
                                return;
                            }

                            int currentMask = q.Mission.LocalVar("nmDefeated");
                            q.Mission.SetLocalVar("nmDefeated", SetBit(currentMask, TowerOption[npcObj.Name]));
                            q.Mission.SetLocalVar("numDefeated", 0);
                        }
                        else
                        {
                            q.Mission.SetLocalVar("numDefeated", numDefeated);
                        }
                    }
                })
                .OnEventFinish(Zone.Altaieu, 100, q =>
                {
                    q.Mission.SetVar("Status", 2);

                    if (q.Option == 1)
                    {
                        q.Player.SetPos(-20, 0.624, -355, 191, Zone.GrandPalaceOfHuxzoi);
                    }
                })
                .OnEventFinish(Zone.Altaieu, 164, q => q.Mission.SetVar("Status", 1))
                .Npc(Zone.GrandPalaceOfHuxzoi, Npc.GrandPalaceOfHuxzoi.iya).OnTrigger(q =>
                {
                    if (q.Mission.Var("Status") == 2)
                    {
                        return QuestReply.ProgressEvent(1);
                    }
                    else
                    {
                        // TODO (Lua): Is this default until another condition is met? Verify
                        return QuestReply.MessageSpecial(Text.GrandPalaceOfHuxzoi.DOES_NOT_RESPOND).SetPriority(1000);
                    }
                })
                .Npc(Zone.GrandPalaceOfHuxzoi, Npc.GrandPalaceOfHuxzoi.iyb).OnTrigger(q =>
                    q.Mission.Var("Status") == 3 ? QuestReply.ProgressEvent(2) : null)
                .OnEventFinish(Zone.GrandPalaceOfHuxzoi, 1, q =>
                {
                    if (q.Player.GiveItem(Item.TavnazianRing))
                    {
                        q.Mission.SetVar("Status", 3);
                    }
                })
                .OnEventFinish(Zone.GrandPalaceOfHuxzoi, 2, q => q.Mission.Complete());

            b.State(current: null, when: q =>
                    q.Player.HasCompletedMission(MissionLog.Cop, Mission.Cop.GardenOfAntiquity))
                .Npc(Zone.Altaieu, Npc.Altaieu._0x0)
                    .OnTrigger(QuestReply.ProgressOptionalCutscene(100).CsOption(0).CanSkip())
                .OnEventFinish(Zone.Altaieu, 100, q =>
                {
                    if (q.Option == 1)
                    {
                        q.Player.SetPos(-20, 0.624, -355, 191, Zone.GrandPalaceOfHuxzoi);
                    }
                });
        }
    }
}
