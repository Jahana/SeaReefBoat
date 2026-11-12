// A Century of Hardship (Mine Shaft #2716, Promathia 5-3's Louverance fight), a line-for-line port of
// scripts/battlefields/Mine_Shaft_2716/century_of_hardship.lua (quest rework C3, C3c). A BattlefieldMission of 30
// minutes for six, trusts allowed, no experience lost, capped at xi.settings.main.MAX_LEVEL, entered at _0d0 (index 0)
// and left at _0d1, _0d2 or _0d3, granting 1000 experience. The mission is the Promathia log's Three Paths, entered by
// Louverance's status field (position 2) at 8. Its one group is the five of an area (Movamuq, Chekochuk, Trikotrak,
// Swipostik, Bugbby; the area stride 5), superlinked; the group's allDeath wins once all five are dead. The win sets
// battlefieldWin only while the mission is current; the mission's 32001 finish (Missions/Cop/ThreePaths.cs) reads it.
// onBattlefieldTick runs the Moblins' commands to Bugbby after the base tick: below 90% Bugbby starts the cycle, a
// Moblin commands every 30 to 60 s of GetSystemTime (or at once when a Moblin's own onMobFight asks, at 50% and 25%),
// and Bugbby obeys or refuses. The Lua's closures are its six callbacks (quest rework C3 PR2): the Moblin's reaction to
// a dead Bugbby, Bugbby's refusal line, the Moblin's insult and refusal skill, Heavy Whisk, Mighty Strikes, and the
// char's DEATH listener armed in onBattlefieldEnter and removed in onBattlefieldLeave.
// Kept quirks (as the Lua): the char's DEATH listener never gets a killer (the C++ passes one only from m_OwnerID,
// which a char's never holds: battle_entity.cpp:2312-2328; ZoneHost.MobAi.cs the same), so its body returns at once;
// handleMoblinCommand's dead-Bugbby arm and its reaction timer are dead code (both callers return first when Bugbby
// is not alive, in the same tick); its nil guard too (both callers pass two mobs).
using System;
using System.Collections.Generic;
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.MineShaft2716
{
    public sealed class CenturyOfHardship : BattlefieldScript
    {
        // xi.mobMod.NO_MOVE, xi.behavior.NO_TURN
        public const int MobModNoMove = 65;
        public const int BehaviorNoTurn = 0x00000400;

        // xi.mobSkill
        public const int Frypan1 = 1081, GoblinDiceHeal = 1099, Smokebomb1 = 1082, BombToss1 = 591;
        public const int HeavyWhisk = 358, MightyStrikes1 = 688;

        // the Lua's local mobDialogue, keyed by the area 1 mob: the dialogue's first text, the offset of the death line
        // the DEATH listener shows, the refusal skill (Bugbby's 0 is never used: Bugbby never commands)
        public static readonly (string Mob, ZoneText Base, int Offset, int MobSkill)[] MobDialogue =
        {
            (Mob.MineShaft2716.MOVAMUQ, Text.MineShaft2716.MOVAMUQ_DIALOGUE, 6, Frypan1),
            (Mob.MineShaft2716.CHEKOCHUK, Text.MineShaft2716.CHEKOCHUK_DIALOGUE, 6, GoblinDiceHeal),
            (Mob.MineShaft2716.TRIKOTRAK, Text.MineShaft2716.TRIKOTRAK_DIALOGUE, 6, Smokebomb1),
            (Mob.MineShaft2716.SWIPOSTIK, Text.MineShaft2716.SWIPOSTIK_DIALOGUE, 5, BombToss1),
            (Mob.MineShaft2716.BUGBBY, Text.MineShaft2716.BUGBBY_DIALOGUE, 9, 0),
        };

        private readonly BattlefieldCallback deadBugbearReaction_, bugbearRefusal_, moblinInsult_, heavyWhisk_,
                                             mightyStrikes_, playerDeath_;

        public CenturyOfHardship() : base("CENTURY_OF_HARDSHIP")
        {
            Data = new BattlefieldMissionData
            {
                ZoneId = Zone.MineShaft2716,
                BattlefieldId = Battlefield.CenturyOfHardship,
                CanLoseExp = false,
                IsMission = true,
                AllowTrusts = true,
                MaxPlayers = 6,
                LevelCapSetting = "MAX_LEVEL",
                // utils.minutes(30)
                TimeLimit = 30 * 60,
                Index = 0,
                EntryNpcs = new[] { Npc.MineShaft2716._0d0 },
                ExitNpcs = new[] { Npc.MineShaft2716._0d1, Npc.MineShaft2716._0d2, Npc.MineShaft2716._0d3 },
                MissionArea = XI.Quests.MissionLog.CoP,
                Mission = XI.QuestContent.Mission.Cop.ThreePaths,
                // xi.mission.status.COP.LOUVERANCE
                MissionStatus = 2,
                MissionStatusArea = XI.Quests.MissionLog.CoP,
                RequiredMissionStatus = 8,
                GrantXP = 1000,

                Groups =
                {
                    new BattlefieldGroup
                    {
                        AreaMobIds = new[] { Five(0), Five(5), Five(10) },
                        Superlink = true,

                        AllDeath = (q, mob) =>
                        {
                            q.Battlefield.SetStatus(BattlefieldConst.StatusWon);
                        },
                    },
                },
            };

            // the Moblin reacts to a dead Bugbby (unreachable: see the header)
            deadBugbearReaction_ = Data.Callback("deadBugbearReaction", (q, a) =>
            {
                var mobArg = a.Entity;
                mobArg.MessageText(a.CapturedInt(0) + 4);
            });
            // Bugbby's refusal line
            bugbearRefusal_ = Data.Callback("bugbearRefusal", (q, a) =>
            {
                var bugArg = a.Entity;
                bugArg.MessageText(a.CapturedInt(0));
            });
            // the Moblin insults Bugbby and uses its refusal skill
            moblinInsult_ = Data.Callback("moblinInsult", (q, a) =>
            {
                var mobArg = a.Entity;
                int dialogueBase = a.CapturedInt(0);
                int refusalSkill = a.CapturedInt(1);
                mobArg.MessageText(dialogueBase + 3);

                // if refusalSkill then: every Moblin's entry holds a number, so the arm always runs
                // For Goblin Dice, add random offset to get variant
                if (refusalSkill == GoblinDiceHeal)
                {
                    refusalSkill = refusalSkill + q.Engine.RandomInt(0, 10);
                }

                mobArg.UseMobAbility(refusalSkill);
            });
            // command type 2: Bugbby uses Heavy Whisk
            heavyWhisk_ = Data.Callback("heavyWhisk", (q, a) =>
            {
                var bugArg = a.Entity;
                bugArg.UseMobAbility(HeavyWhisk);
            });
            // command type 3: Bugbby uses Mighty Strikes, once
            mightyStrikes_ = Data.Callback("mightyStrikes", (q, a) =>
            {
                var bugArg = a.Entity;
                bugArg.UseMobAbility(MightyStrikes1);
                bugArg.SetLocalVar("usedMightyStrikes", 1);
            });
            // the char's DEATH listener: the killer's death line (no killer reaches it: see the header)
            playerDeath_ = Data.Callback("playerDeath", (q, a) =>
            {
                var deadPlayer = a.Entity;
                var killer = a.EntityArg(2);
                if (killer == null || !killer.IsAlive)
                {
                    return;
                }

                var bf = deadPlayer.GetBattlefield();
                if (bf == null)
                {
                    return;
                }

                int offset = (bf.Area - 1) * 5;
                int baseMobID = killer.Id - offset;
                var dialogue = Dialogue(q, baseMobID);

                if (dialogue != null)
                {
                    killer.MessageText(q.Text.Id(dialogue.Value.Base) + dialogue.Value.Offset);
                }
            });
        }

        public override BattlefieldData Data { get; }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Mine_Shaft_2716/century_of_hardship";

        // an area's five: mineshaftID.mob.MOVAMUQ + first to + first + 4
        private static BattlefieldMobId[] Five(int first)
        {
            var ids = new BattlefieldMobId[5];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = new BattlefieldMobId(Mob.MineShaft2716.MOVAMUQ, first + i);
            }

            return ids;
        }

        // mineshaftID.mob[key]
        private static int MobId(IBattlefieldContext q, string key) =>
            q.Engine.ZoneMobId(Zone.MineShaft2716, key) ??
            throw new InvalidOperationException($"no {key} in Mine Shaft #2716's IDs");

        // mobDialogue[baseMobID]: null for nil
        private static (ZoneText Base, int Offset, int MobSkill)? Dialogue(IBattlefieldContext q, int baseMobID)
        {
            foreach (var d in MobDialogue)
            {
                if (MobId(q, d.Mob) == baseMobID)
                {
                    return (d.Base, d.Offset, d.MobSkill);
                }
            }

            return null;
        }

        // the Lua's local handleMoblinCommand: the Moblin's command sequence
        private void HandleMoblinCommand(IBattlefieldContext q, IBattlefieldMob moblin, IBattlefieldMob bugbear,
                                         int offset)
        {
            if (moblin == null || bugbear == null)
            {
                return;
            }

            int moblinID = moblin.Id;
            int baseMobID = moblinID - offset;
            moblin.SetLocalVar("inCommandSequence", q.Engine.GetSystemTime() + 3u);
            bugbear.SetLocalVar("inCommandSequence", q.Engine.GetSystemTime() + 3u);

            // Determine which Moblin this is and get dialogue/skill (mobDialogue[baseMobID].base raises on a nil
            // entry)
            var entry = Dialogue(q, baseMobID) ??
                        throw new InvalidOperationException("attempt to index a nil value (field '?')");
            int dialogueBase = q.Text.Id(entry.Base);
            int refusalSkill = entry.MobSkill;

            bool alreadyUsedMightyStrikes = bugbear.LocalVar("usedMightyStrikes") == 1;

            // Moblin looks at Bugbear (or where he died)
            var bugbearPos = bugbear.GetPos();
            moblin.LookAt(bugbearPos.X, bugbearPos.Y, bugbearPos.Z);
            moblin.SetAutoAttackEnabled(false);
            moblin.SetMagicCastingEnabled(false);
            moblin.SetMobMod(MobModNoMove, 1);
            moblin.SetBehavior(moblin.GetBehavior() | BehaviorNoTurn);

            // Select which command to give Bugbear (60% enmity, 30% TP move, 10% Mighty Strikes)
            int randomRoll = q.Engine.RandomInt(1, 100);
            int commandType = randomRoll <= 60 ? 1 : randomRoll <= 90 ? 2 : 3;

            // Moblin issues the command (commandType maps to message offset)
            moblin.MessageText(dialogueBase + commandType - 1);

            // If Bugbear is dead, react after delay
            if (!bugbear.IsAlive)
            {
                moblin.Timer(2000, deadBugbearReaction_, dialogueBase);

                return;
            }

            // Bugbear responds to the command
            var moblinPos = moblin.GetPos();
            bugbear.LookAt(moblinPos.X, moblinPos.Y, moblinPos.Z);
            bugbear.SetAutoAttackEnabled(false);
            bugbear.SetMobMod(MobModNoMove, 1);
            bugbear.SetBehavior(bugbear.GetBehavior() | BehaviorNoTurn);

            // Bugbear can refuse any command, or if Mighty Strikes is commanded but already used
            bool bugbearRefuses = q.Engine.RandomInt(1, 100) <= 20;
            bool forceRefusal = commandType == 3 && alreadyUsedMightyStrikes;

            if (bugbearRefuses || forceRefusal)
            {
                // Bugbear refuses
                int bugbearRefuseMsg = q.Text.Id(Text.MineShaft2716.BUGBBY_DIALOGUE) + 6 + q.Engine.RandomInt(0, 1);
                bugbear.Timer(1000, bugbearRefusal_, bugbearRefuseMsg);

                // Moblin insults Bugbear and uses their refusal skill
                moblin.Timer(2000, moblinInsult_, dialogueBase, refusalSkill);

                return;
            }

            // Bugbby accepts the command
            int bugbearAcceptMsg = q.Text.Id(Text.MineShaft2716.BUGBBY_DIALOGUE) + q.Engine.RandomInt(0, 5);
            bugbear.MessageText(bugbearAcceptMsg);

            // Execute command based on type
            switch (commandType)
            {
                // Command Type 1: Override Bugbear's enmity
                case 1:
                    var moblinTarget = moblin.GetTarget();
                    if (moblinTarget != null)
                    {
                        bugbear.Disengage();
                        bugbear.UpdateEnmity(moblinTarget);
                    }

                    break;

                // Command Type 2: Tell Bugbear to use a TP move
                case 2:
                    bugbear.Timer(2000, heavyWhisk_);
                    break;

                // Command Type 3: Tell Bugbear to use Mighty Strikes
                case 3:
                    bugbear.Timer(2000, mightyStrikes_);
                    break;
            }
        }

        public override void OnBattlefieldEnter(IBattlefieldContext q)
        {
            q.Framework.OnBattlefieldEnter();

            q.Player.AddListener("DEATH", "CENTURY_HARDSHIP_DEATH", playerDeath_);
        }

        public override void OnBattlefieldLeave(IBattlefieldContext q, int leaveCode)
        {
            q.Framework.OnBattlefieldLeave(leaveCode);
            q.Player.RemoveListener("CENTURY_HARDSHIP_DEATH");
        }

        public override void OnBattlefieldTick(IBattlefieldContext q, int tick)
        {
            q.Framework.OnBattlefieldTick(tick);

            var battlefield = q.Battlefield;
            int area = battlefield.Area;
            int offset = (area - 1) * 5;
            var bugbear = q.Engine.MobById(MobId(q, Mob.MineShaft2716.BUGBBY) + offset);

            if (bugbear == null || !bugbear.IsAlive)
            {
                return;
            }

            // Restore mobility for mobs whose inCommandSequence has expired
            for (int i = 0; i <= 3; i++)
            {
                var moblin = q.Engine.MobById(MobId(q, Mob.MineShaft2716.MOVAMUQ) + offset + i);
                if (moblin != null && moblin.IsAlive)
                {
                    uint inCommandSequence = moblin.LocalVar("inCommandSequence");
                    if (inCommandSequence > 0 && q.Engine.GetSystemTime() >= inCommandSequence)
                    {
                        moblin.SetMobMod(MobModNoMove, 0);
                        moblin.SetAutoAttackEnabled(true);
                        moblin.SetMagicCastingEnabled(true);
                        moblin.SetBehavior(moblin.GetBehavior() & ~BehaviorNoTurn);
                        moblin.SetLocalVar("inCommandSequence", 0);
                    }
                }
            }

            uint bugbearSequence = bugbear.LocalVar("inCommandSequence");
            if (bugbearSequence > 0 &&
                q.Engine.GetSystemTime() >= bugbearSequence)
            {
                bugbear.SetMobMod(MobModNoMove, 0);
                bugbear.SetAutoAttackEnabled(true);
                bugbear.SetBehavior(bugbear.GetBehavior() & ~BehaviorNoTurn);
                bugbear.SetLocalVar("inCommandSequence", 0);
            }

            // Check if a mob triggered a command via HP threshold
            double triggeredMobID = battlefield.LocalVar("triggerMoblinCommand");
            if (triggeredMobID > 0)
            {
                var moblin = q.Engine.MobById((int)triggeredMobID);
                if (moblin != null && moblin.IsAlive)
                {
                    HandleMoblinCommand(q, moblin, bugbear, offset);
                }

                battlefield.SetLocalVar("triggerMoblinCommand", 0);
            }

            // Reset command sequence if mobs exit combat (party wipe)
            if (!bugbear.IsEngaged && battlefield.LocalVar("commandSequence") == 1)
            {
                battlefield.SetLocalVar("commandSequence", 0);
                battlefield.SetLocalVar("nextCommandTime", 0);
                return;
            }

            // Trigger: Bugbby drops below 90% HP, start command cycle
            if (battlefield.LocalVar("commandSequence") == 0 &&
                bugbear.IsAlive &&
                bugbear.HPP < 90)
            {
                battlefield.SetLocalVar("commandSequence", 1);
                battlefield.SetLocalVar("nextCommandTime", q.Engine.GetSystemTime() + q.Engine.RandomInt(30, 60));
            }

            // Global command timer - triggers a Moblin to command
            double nextCommandTime = battlefield.LocalVar("nextCommandTime");
            if (nextCommandTime > 0 &&
                q.Engine.GetSystemTime() >= nextCommandTime &&
                bugbear.IsEngaged)
            {
                // Find all alive Moblins
                var aliveMoblins = new List<IBattlefieldMob>();

                for (int i = 0; i <= 3; i++)
                {
                    var moblin = q.Engine.MobById(MobId(q, Mob.MineShaft2716.MOVAMUQ) + offset + i);
                    if (moblin != null && moblin.IsAlive)
                    {
                        aliveMoblins.Add(moblin);
                    }
                }

                // Pick alive Moblin to command Bugbear
                if (aliveMoblins.Count > 0)
                {
                    var selectedMoblin = aliveMoblins[q.Engine.RandomInt(1, aliveMoblins.Count) - 1];
                    HandleMoblinCommand(q, selectedMoblin, bugbear, offset);
                    // Set next global command timer (30-60 seconds)
                    battlefield.SetLocalVar("nextCommandTime", q.Engine.GetSystemTime() + q.Engine.RandomInt(30, 60));
                }
            }
        }
    }
}
