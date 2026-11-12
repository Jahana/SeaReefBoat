// The Shikaree skillchain AI of the Mithran Trackers' battlefields, shared by Requiem of Sin and Tango with a Tracker
// (scripts/battlefields/Boneyard_Gully/requiem_of_sin.lua and tango_with_a_tracker.lua) and written for Head Wind
// (head_wind.lua, C3b) too: their copies of it are the same functions, with the mob keys, the area stride, the loops
// and the chain tables as parameters. Quest rework C3.
// Tick is the part of onBattlefieldTick after the base tick. The state is the battlefield's local var scState (IDLE 0,
// READY 1, STARTING 2, EXECUTING 3, SOLO 4) and scLeader; the mobs carry scSkill, scMessage, scNextID and
// scPendingTime. The traces reach only IDLE and SOLO; the mob skill scripts (outside the battlefield files, e.g.
// Shikaree_*_ROS_TWT.lua:112-113) move STARTING to EXECUTING and fill scPendingTime. The ids are numbers as in the
// Lua: the mob id of slot i is the first id of the area plus i, and the message and chain tables are keyed by the ids
// of their mob keys, so a table key that is not one of those numbers finds nothing, as in the Lua.
// Kept Lua quirk, from Tango with a Tracker's copy: its resetSkillchain loop runs 1 to 2 from the area's first
// mob (Y): it resets X and the id after X (Requiem's +3 mob, not in the Tango fight), never Y, so after a chain Y keeps
// its magic casting disabled, its scMessage and its scNextID.
using System.Collections.Generic;
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.Shared
{
    // one entry of a skillchain: the mob (its key) that uses the skill, and the skill
    public sealed class ShikareeStep
    {
        public ShikareeStep(string mob, int skill)
        {
            Mob = mob;
            Skill = skill;
        }

        public string Mob { get; }
        public int Skill { get; }
    }

    public sealed class ShikareeSkillchain
    {
        public const int Idle = 0;
        public const int Ready = 1;
        public const int Starting = 2;
        public const int Executing = 3;
        public const int Solo = 4;

        public int ZoneId;
        // the area stride of the mob ids: (area - 1) * Stride
        public int Stride;
        // the key of the mob whose id plus i is the id the loops of getAliveShikarees and checkForNewLeader read
        public string FirstMob;
        // the loops of those two: 0 to Slots - 1
        public int Slots;
        // the loop of resetSkillchain: FirstMob + ResetFrom to FirstMob + ResetTo
        public int ResetFrom;
        public int ResetTo;
        // READY waits for every slot alive (Tango with a Tracker's count == 2)
        public bool RequireAllSlots;
        // msgOffsets: the text of each mob key
        public (string Mob, ZoneText Text)[] MessageOffsets;
        // skillchainData of a leader and the other alive mob (Requiem of Sin): leader key, then the chain with all
        // three alive (the Lua's allAlive) and the chains by the other alive mob's key
        public Dictionary<string, (ShikareeStep[] All, Dictionary<string, ShikareeStep[]> With)> Nested;
        // skillchainData of a leader alone (Tango with a Tracker): leader key, then the chain
        public Dictionary<string, ShikareeStep[]> Flat;

        sealed class Alive
        {
            public readonly List<int> Shikarees = new List<int>();
            public int Count;
            public int LastAlive;
            public bool AllReady = true;
            public int Leader;
        }

        int Id(IBattlefieldContext q, string key) => q.Engine.ZoneMobId(ZoneId, key).Value;

        int? TextOf(IBattlefieldContext q, int baseId)
        {
            foreach (var (mob, text) in MessageOffsets)
            {
                if (Id(q, mob) == baseId)
                {
                    return q.Text.Id(text);
                }
            }

            return null;
        }

        ShikareeStep[] FlatChain(IBattlefieldContext q, int leaderBaseId)
        {
            foreach (var entry in Flat)
            {
                if (Id(q, entry.Key) == leaderBaseId)
                {
                    return entry.Value;
                }
            }

            return null;
        }

        (ShikareeStep[] All, Dictionary<string, ShikareeStep[]> With) LeaderData(IBattlefieldContext q,
            int leaderBaseId)
        {
            foreach (var entry in Nested)
            {
                if (Id(q, entry.Key) == leaderBaseId)
                {
                    return entry.Value;
                }
            }

            return (null, null);
        }

        // readySkillChain
        void ReadySkillChain(IBattlefieldContext q, int leaderShikaree, List<int> aliveShikarees)
        {
            var leaderMob = q.Engine.MobById(leaderShikaree);
            if (leaderMob == null)
            {
                return;
            }

            var battlefield = leaderMob.GetBattlefield();
            if (battlefield == null)
            {
                return;
            }

            // Get leader's current target to share with all participants
            var leaderTarget = leaderMob.GetTarget();
            if (leaderTarget == null)
            {
                return;
            }

            // Select appropriate skillchain data based on the leader and the alive shikaree count
            ShikareeStep[] chainData = null;
            int offset = (battlefield.Area - 1) * Stride;
            int leaderBaseId = leaderShikaree - offset;

            if (Nested != null)
            {
                var leaderData = LeaderData(q, leaderBaseId);

                if (aliveShikarees.Count == 3)
                {
                    // Use 3-mob skillchain
                    chainData = leaderData.All;
                }
                else
                {
                    // Use 2-mob skillchain: index by the other alive mob's baseID
                    for (int i = 0; i < 2; i++)
                    {
                        if (aliveShikarees[i] != leaderShikaree)
                        {
                            int otherBaseId = aliveShikarees[i] - offset;
                            foreach (var entry in leaderData.With)
                            {
                                if (Id(q, entry.Key) == otherBaseId)
                                {
                                    chainData = entry.Value;
                                }
                            }

                            break;
                        }
                    }
                }
            }
            else
            {
                chainData = FlatChain(q, leaderBaseId);
            }

            if (chainData == null)
            {
                return;
            }

            battlefield.SetLocalVar("scState", Starting);

            // Set up the Shikarees with their skill sequence order and behavior; the array position is the order
            for (int i = 0; i < chainData.Length; i++)
            {
                var data = chainData[i];
                int dataMobId = Id(q, data.Mob);
                int mobId = dataMobId + offset;
                var shikareeMob = q.Engine.MobById(mobId);

                if (shikareeMob != null)
                {
                    // Leader of skillchains uses message offset + 2, followers use offset + 3
                    int msgOffset = TextOf(q, dataMobId).Value + (i == 0 ? 2 : 3);

                    shikareeMob.SetLocalVar("scSkill", (uint)data.Skill);
                    shikareeMob.SetLocalVar("scMessage", (uint)msgOffset);
                    shikareeMob.SetMagicCastingEnabled(false);

                    // Force all Shikarees to target the leader's target
                    if (mobId != leaderShikaree)
                    {
                        shikareeMob.UpdateEnmity(leaderTarget);
                    }

                    // Set next mob in chain (next entry in the array, or 0 if last)
                    uint nextId = i + 1 < chainData.Length ? (uint)(Id(q, chainData[i + 1].Mob) + offset) : 0;
                    shikareeMob.SetLocalVar("scNextID", nextId);
                }
            }
        }

        void ResetSkillchain(IBattlefieldContext q, IBattlefieldView battlefield, int offsetId)
        {
            battlefield.SetLocalVar("scState", Idle);
            battlefield.SetLocalVar("scLeader", 0);

            // Re-enable magic casting and clean up for the alive Shikarees
            for (int i = ResetFrom; i <= ResetTo; i++)
            {
                var shikareeMob = q.Engine.MobById(offsetId + i);

                if (shikareeMob != null && shikareeMob.IsAlive)
                {
                    shikareeMob.SetMagicCastingEnabled(true);
                    shikareeMob.SetLocalVar("scSkill", 0);
                    shikareeMob.SetLocalVar("scMessage", 0);
                    shikareeMob.SetLocalVar("scNextID", 0);
                    shikareeMob.SetLocalVar("scPendingTime", 0);
                }
            }
        }

        Alive GetAliveShikarees(IBattlefieldContext q, int offsetId, int scLeaderId)
        {
            var result = new Alive();

            for (int i = 0; i < Slots; i++)
            {
                int currentId = offsetId + i;
                var shikareeMob = q.Engine.MobById(currentId);

                if (shikareeMob != null && shikareeMob.IsAlive)
                {
                    result.Count++;
                    result.LastAlive = currentId;
                    result.Shikarees.Add(currentId);

                    if (shikareeMob.TP < 1000)
                    {
                        result.AllReady = false;
                    }

                    if (currentId == scLeaderId)
                    {
                        result.Leader = currentId;
                    }
                }
            }

            return result;
        }

        // Check if a Shikaree should become the skillchain leader (called in IDLE state)
        void CheckForNewLeader(IBattlefieldContext q, IBattlefieldView battlefield, int offsetId)
        {
            for (int i = 0; i < Slots; i++)
            {
                int currentId = offsetId + i;
                int baseId = Id(q, FirstMob) + i;
                var shikareeMob = q.Engine.MobById(currentId);

                if (shikareeMob != null && shikareeMob.IsAlive && shikareeMob.TP >= 1000)
                {
                    int? leaderMsgOffset = TextOf(q, baseId);

                    if (leaderMsgOffset != null)
                    {
                        battlefield.SetLocalVar("scState", Ready);
                        battlefield.SetLocalVar("scLeader", currentId);
                        shikareeMob.MessageText(leaderMsgOffset.Value + 1);

                        return;
                    }
                }
            }
        }

        // Kick off the beginning of the skillchain
        void StartSkillChain(IBattlefieldContext q, IBattlefieldView battlefield, int offsetId, int scLeaderId)
        {
            var leaderMob = q.Engine.MobById(scLeaderId);

            if (leaderMob == null || !leaderMob.IsAlive)
            {
                ResetSkillchain(q, battlefield, offsetId);

                return;
            }

            if (q.Engine.IsEntityBusy(leaderMob))
            {
                return;
            }

            if (leaderMob.GetTarget() == null)
            {
                return;
            }

            uint scSkill = leaderMob.LocalVar("scSkill");

            if (scSkill > 0)
            {
                leaderMob.UseMobAbility((int)scSkill, leaderMob.GetTarget());
            }
        }

        // Find the next pending mob that's ready to execute their skill
        (IBattlefieldMob Mob, int Skill) FindPendingMob(IBattlefieldContext q, List<int> aliveShikarees)
        {
            foreach (int shikareeId in aliveShikarees)
            {
                var shikareeMob = q.Engine.MobById(shikareeId);

                if (shikareeMob != null &&
                    shikareeMob.IsAlive &&
                    !q.Engine.IsEntityBusy(shikareeMob) &&
                    shikareeMob.LocalVar("scSkill") > 0 &&
                    shikareeMob.LocalVar("scPendingTime") > 0 &&
                    q.Engine.GetSystemTime() >= shikareeMob.LocalVar("scPendingTime"))
                {
                    return (shikareeMob, (int)shikareeMob.LocalVar("scSkill"));
                }
            }

            return (null, 0);
        }

        // Check if all participants have completed their skills
        bool IsSkillchainComplete(IBattlefieldContext q, List<int> aliveShikarees)
        {
            foreach (int shikareeId in aliveShikarees)
            {
                var shikareeMob = q.Engine.MobById(shikareeId);

                if (shikareeMob != null && shikareeMob.IsAlive)
                {
                    if (shikareeMob.LocalVar("scSkill") > 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // Handle EXECUTING state: monitor and execute pending skills
        void ExecuteSkillChain(IBattlefieldContext q, IBattlefieldView battlefield, int offsetId, int scLeaderId,
            List<int> aliveShikarees)
        {
            var scLeaderMob = q.Engine.MobById(scLeaderId);
            var scTarget = scLeaderMob?.GetTarget();

            // Find and execute pending skill
            var (pendingMob, pendingSkill) = FindPendingMob(q, aliveShikarees);

            if (pendingMob != null)
            {
                var target = scTarget ?? pendingMob.GetTarget();

                if (target != null)
                {
                    if (scTarget != null)
                    {
                        pendingMob.UpdateEnmity(scTarget);
                    }

                    pendingMob.UseMobAbility(pendingSkill, target);
                }
            }

            // Reset when all participants have completed their skills
            if (IsSkillchainComplete(q, aliveShikarees))
            {
                ResetSkillchain(q, battlefield, offsetId);
            }
        }

        // onBattlefieldTick, after the base tick
        public void Tick(IBattlefieldContext q)
        {
            var battlefield = q.Battlefield;
            int offset = (battlefield.Area - 1) * Stride;
            int offsetId = Id(q, FirstMob) + offset;
            int scState = (int)battlefield.LocalVar("scState");
            int scLeaderId = (int)battlefield.LocalVar("scLeader");

            // Reset if leader died before initiating skillchain
            if (scState == Ready && scLeaderId != 0)
            {
                var leaderMob = q.Engine.MobById(scLeaderId);

                if (leaderMob == null || !leaderMob.IsAlive)
                {
                    ResetSkillchain(q, battlefield, offsetId);
                    scState = Idle;
                }
            }

            // Get alive shikarees and their status
            var aliveData = GetAliveShikarees(q, offsetId, scLeaderId);

            // Solo mode: only one Shikaree left
            if (aliveData.Count == 1 && aliveData.LastAlive != 0)
            {
                var lastMob = q.Engine.MobById(aliveData.LastAlive);

                if (lastMob != null && scState != Solo)
                {
                    lastMob.SetMobAbilityEnabled(true);
                    ResetSkillchain(q, battlefield, offsetId);
                    battlefield.SetLocalVar("scState", Solo);
                }

                return;
            }

            switch (scState)
            {
                case Idle:
                    CheckForNewLeader(q, battlefield, offsetId);
                    break;

                case Ready:
                    if ((!RequireAllSlots || aliveData.Count == Slots) && aliveData.AllReady && aliveData.Leader != 0)
                    {
                        ReadySkillChain(q, aliveData.Leader, aliveData.Shikarees);
                    }

                    break;

                case Starting:
                    StartSkillChain(q, battlefield, offsetId, scLeaderId);
                    break;

                case Executing:
                    ExecuteSkillChain(q, battlefield, offsetId, scLeaderId, aliveData.Shikarees);
                    break;
            }
        }
    }
}
