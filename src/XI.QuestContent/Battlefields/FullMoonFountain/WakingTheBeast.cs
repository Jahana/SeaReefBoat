// Waking the Beast (Full Moon Fountain BCNM), a line-for-line port of
// scripts/battlefields/Full_Moon_Fountain/waking_the_beast.lua (quest rework C3, PR2). A BattlefieldQuest of the Other
// Areas quest Waking the Beast: 30 minutes for up to 18, no experience lost, index 2, entered at MS_Entrance with the
// six eyes and the Rainbow Resonator (a helper also with the quest completed), left at Moon_Spiral, repeatable (a
// completed char's win sets battlefieldWin too: user ruling 2026-11-05, kept bugs). Its seven phases run in
// onBattlefieldTick: 0 the main Carbuncle (unkillable) to 75%, 1 one avatar, 2 the Carbuncle to 50%, 3 two avatars, 4
// the Carbuncle to 25%, 5 three avatars, 6 five Carbuncles (the main one killable); all five dead: WON and the loot.
// The avatars come in the order setupBattlefield shuffles (utils.shuffle), five seconds (GetSystemTime) after each
// phase change. The Lua's closures are its five callbacks (quest rework C3 PR2): the avatars' and the Carbuncles'
// DEATH listeners, the avatars' two queued actions (the normal spawn animation, the taunt) and the despawning
// Carbuncle's 7 s timer. content.lastEnmityList (the enmity list of the last mob to die or despawn, by area, kept
// across ticks) keeps its entities with IBattlefieldEngine.Keep.
// Kept Lua bugs: handleLoot's first candidate is next(players), the table's first key (1), not a player, so with
// players and no initiator among them the first successful roll indexes a number and raises (here an
// InvalidOperationException with the Lua's message); the DEATH listener of a phase's avatar keeps the battlefield of
// the tick that spawned it (a listener stays on the mob after the fight, until the next spawn replaces it).
using System;
using System.Collections.Generic;
using System.Linq;
using XI.Quests.Authoring;

namespace XI.QuestContent.Battlefields.FullMoonFountain
{
    public sealed class WakingTheBeast : BattlefieldScript
    {
        // xi.spawnAnimation.NORMAL, SPECIAL; xi.animation.DESPAWN; xi.status.DISAPPEAR; xi.mobMod.NO_AGGRO, NO_LINK
        private const int SpawnAnimationNormal = 0, SpawnAnimationSpecial = 1;
        private const int AnimationDespawn = 2;
        private const int StatusDisappear = 2;
        private const int MobModNoAggro = 67, MobModNoLink = 69;

        // local loot
        public static readonly int[] Loot =
        {
            Item.CarbunclesCuffs,
            Item.IfritsBow,
            Item.ShivasShotel,
            Item.TitansBaselarde,
            Item.GarudasSickle,
            Item.LeviathansCouse,
            Item.RamuhsMace,
        };

        // local avatarTwoHours: each avatar's two-hour skill, by the mob's name
        public static readonly IReadOnlyDictionary<string, int> AvatarTwoHours = new Dictionary<string, int>
        {
            ["Carbuncle_Prime"] = 912,
            ["Garuda_Prime"] = 875,
            ["Ifrit_Prime"] = 848,
            ["Leviathan_Prime"] = 866,
            ["Ramuh_Prime"] = 893,
            ["Shiva_Prime"] = 884,
            ["Titan_Prime"] = 857,
        };

        // local spawnType
        private const int SpawnTypeElementalAvatar = 1, SpawnTypeCarbuncle = 2;

        // content.lastEnmityList: the enmity list of the last mob that died or despawned, by area (the content is
        // shared by the areas), each entity kept past the call that read it
        private readonly Dictionary<int, List<(BattlefieldKeptEntity Entity, int Ce, int Ve)>> lastEnmityList_ =
            new Dictionary<int, List<(BattlefieldKeptEntity Entity, int Ce, int Ve)>>();

        private readonly BattlefieldCallback avatarDeath_, avatarNormalAnimation_, avatarTaunt_, carbyDeath_,
                                             carbyGone_;

        public WakingTheBeast() : base("WAKING_THE_BEAST_FULLMOON")
        {
            Data = new BattlefieldQuestData
            {
                ZoneId = Zone.FullMoonFountain,
                BattlefieldId = Battlefield.WakingTheBeastFullmoon,
                CanLoseExp = false,
                MaxPlayers = 18,
                // utils.minutes(30)
                TimeLimit = 30 * 60,
                Index = 2,
                EntryNpcs = new[] { Npc.FullMoonFountain.MSEntrance },
                ExitNpcs = new[] { Npc.FullMoonFountain.MoonSpiral },
                QuestArea = XI.Quests.QuestLog.OtherAreas,
                Quest = "WAKING_THE_BEAST",
                // a completed char's win sets battlefieldWin too: the weekly repeat (user ruling 2026-11-05, kept
                // bugs)
                Repeatable = true,

                Groups =
                {
                    new BattlefieldGroup
                    {
                        AreaMobIds = new[]
                        {
                            new[] { Carby(0) },
                            new[] { Carby(11) },
                            new[] { Carby(22) },
                        },
                        SuperlinkGroup = 1,
                    },

                    // mobs not spawned at start
                    new BattlefieldGroup
                    {
                        AreaMobIds = new[]
                        {
                            Enumerable.Range(1, 10).Select(Carby).ToArray(),
                            Enumerable.Range(12, 10).Select(Carby).ToArray(),
                            Enumerable.Range(23, 10).Select(Carby).ToArray(),
                        },
                        Spawned = false,
                        // store the enmity table of the last mob that dies, by area (the content is shared by areas)
                        Death = (q, mob, _) =>
                            lastEnmityList_[q.Battlefield.Area] = Keep(q, mob.GetEnmityList()),
                        SuperlinkGroup = 1,
                    },
                },
            };

            // the avatar's DEATH listener: tracking variables for the phases, on the battlefield it was spawned in
            avatarDeath_ = Data.Callback("avatarDeath", (q, a) =>
            {
                var battlefield = a.CapturedBattlefield(0);
                a.Entity.SetLocalVar("wasDefeated", 1);
                battlefield.SetLocalVar("avatarsDefeated", battlefield.LocalVar("avatarsDefeated") + 1);
            });
            // back to the normal spawn animation, so chars coming into range see the normal one
            avatarNormalAnimation_ = Data.Callback("avatarNormalAnimation",
                                                   (q, a) => a.Entity.SetSpawnAnimation(SpawnAnimationNormal));
            // the avatar's line to each player of its battlefield
            avatarTaunt_ = Data.Callback("avatarTaunt", (q, a) =>
            {
                var avatarArg = a.Entity;
                if (avatarArg != null)
                {
                    var players = avatarArg.GetBattlefield().Players();
                    foreach (var player in players)
                    {
                        if (player != null)
                        {
                            player.ShowText(avatarArg,
                                            q.Text.Id(Text.FullMoonFountain.LEAVE_THE_AFFAIRS_OF_GODS_TO));
                        }
                    }
                }
            });
            // the Carbuncle's DEATH listener (only needed in phase 6)
            carbyDeath_ = Data.Callback("carbyDeath", (q, a) => a.Entity.SetLocalVar("wasDefeated", 1));
            // after the despawn animation: the name gone (DISAPPEAR), the Carbuncle killed without a message, and
            // targetable again for its next spawn
            carbyGone_ = Data.Callback("carbyGone", (q, a) =>
            {
                var mobArg = a.Entity;
                if (mobArg != null)
                {
                    mobArg.SetStatus(StatusDisappear);
                    mobArg.SetHP(0);
                    mobArg.SetUntargetable(false);
                }
            });
        }

        public override BattlefieldData Data { get; }

        // the require name of the Lua file this battlefield replaces: its slot in the containers list
        public override string LegacyPath => "scripts/battlefields/Full_Moon_Fountain/waking_the_beast";

        // fullMoonFountainID.mob.CARBUNCLE_PRIME + offset
        private static BattlefieldMobId Carby(int offset) => new BattlefieldMobId("CARBUNCLE_PRIME", offset);

        // local mainCarbyIDbyArea: the main Carbuncle (spawned at start) of each area, every other id after it
        private static int MainCarbyId(IBattlefieldContext q, int area)
        {
            int first = q.Engine.ZoneMobId(Zone.FullMoonFountain, "CARBUNCLE_PRIME") ??
                        throw new InvalidOperationException("no CARBUNCLE_PRIME in Full Moon Fountain's IDs");
            return area switch
            {
                1 => first,
                2 => first + 11,
                3 => first + 22,
                _ => throw new InvalidOperationException($"attempt to perform arithmetic on a nil value (area {area})"),
            };
        }

        // an enmity list kept past this call
        private static List<(BattlefieldKeptEntity Entity, int Ce, int Ve)> Keep(
            IBattlefieldContext q, IReadOnlyList<BattlefieldEnmityEntry> list) =>
            list.Select(v => (q.Engine.Keep(v.Entity), v.Ce, v.Ve)).ToList();

        // local handleLoot: three pools (40%, 20%, 10%) for the initiator, else next(players) (see the header)
        private static void HandleLoot(IBattlefieldContext q, IBattlefieldView battlefield)
        {
            var (initiatorId, _) = battlefield.Initiator();
            var players = battlefield.Players();

            // Get the first player, might be nil: next(players) is the first key (1), not a player
            bool firstKey = players.Count > 0;
            IBattlefieldPlayer playerToAddTreasure = null;

            foreach (var player in players)
            {
                if (player != null && player.Id == initiatorId)
                {
                    playerToAddTreasure = player;
                    break;
                }
            }

            if (playerToAddTreasure != null || firstKey)
            {
                // Three loot pools with droprates estimated as follows: 40% -> 20% -> 10%
                double lootChance = 40;
                for (int i = 1; i <= 3; i++)
                {
                    if (q.Engine.RandomInt(1, 100) <= lootChance)
                    {
                        if (playerToAddTreasure == null)
                            throw new InvalidOperationException(
                                "attempt to index local 'playerToAddTreasure' (a number value)");
                        playerToAddTreasure.AddTreasure(Loot[q.Engine.RandomInt(1, 7) - 1], null);
                    }

                    lootChance = lootChance / 2;
                }
            }
        }

        // local checkAllCarbyDead: all five Carbuncles dead in phase 6 wins, once
        private static void CheckAllCarbyDead(IBattlefieldContext q, int mainCarbyID, IBattlefieldView battlefield)
        {
            bool allCarbyDead = true;

            for (int i = 0; i <= 4; i++)
            {
                var carby = q.Engine.MobById(mainCarbyID + i);
                if (carby != null && carby.IsAlive)
                {
                    allCarbyDead = false;
                    break;
                }
            }

            // check also battlefield status as this function can be called even after setting battlefield status to won
            if (allCarbyDead && battlefield.Status != BattlefieldConst.StatusWon)
            {
                battlefield.SetStatus(BattlefieldConst.StatusWon);
                HandleLoot(q, battlefield);
            }
        }

        // local applyEnmity: each entity of the list still in the battlefield on the mob's list, the highest
        // (ce + ve, the last of equals) its target
        private static void ApplyEnmity(IBattlefieldContext q, IBattlefieldMob mob,
                                        List<(BattlefieldKeptEntity Entity, int Ce, int Ve)> enmityList,
                                        IBattlefieldView battlefield)
        {
            IBattlefieldMob highestEnmityTarget = null;
            int highestEnmity = 0;

            foreach (var v in enmityList)
            {
                var target = q.Engine.Entity(v.Entity);

                if (target != null && target.GetBattlefieldID() == battlefield.Id)
                {
                    // we need to explictly add enmity to make sure the target is one the list
                    mob.AddEnmity(target, 1, 0);

                    int totalEnmity = v.Ce + v.Ve;
                    if (totalEnmity >= highestEnmity)
                    {
                        highestEnmity = totalEnmity;
                        highestEnmityTarget = target;
                    }
                }
            }

            if (highestEnmityTarget != null)
            {
                mob.UpdateEnmity(highestEnmityTarget);
            }
        }

        private List<(BattlefieldKeptEntity Entity, int Ce, int Ve)> LastEnmityList(int area) =>
            lastEnmityList_.TryGetValue(area, out var list) ? list : null;

        // local spawnElementalAvatars: the next avatars of the shuffled order (phases 1, 3 and 5)
        private void SpawnElementalAvatars(IBattlefieldContext q, (double X, double Y, double Z, int Rot) spawnPosition,
                                           IBattlefieldView battlefield, int numberAvatarsToSpawn, double phase)
        {
            for (int i = 1; i <= numberAvatarsToSpawn; i++)
            {
                double avatarOrder = battlefield.LocalVar("avatarOrder");
                // use mod to get the right most digit (which represents the next avatar in the order)
                int nextIndex = (int)(avatarOrder - Math.Floor(avatarOrder / 10) * 10);
                // an additional offset of 4 skips the other 4 carbys to get to the avatar IDs
                int avatarID = MainCarbyId(q, battlefield.Area) + 4 + nextIndex;

                // special spawn animation so the avatars enter with their summoning animation
                var avatarPreSpawn = q.Engine.MobById(avatarID);
                if (avatarPreSpawn != null)
                {
                    avatarPreSpawn.SetSpawnAnimation(SpawnAnimationSpecial);
                }

                var avatarPostSpawn = q.Engine.SpawnMob(avatarID);

                if (avatarPostSpawn != null)
                {
                    // also have the avatar track the phase they spawn in
                    avatarPostSpawn.SetLocalVar("phase", (uint)phase);

                    // death listener for setting some useful tracking variables for battlefield
                    avatarPostSpawn.AddListener("DEATH", "WTB_SET_DEATH_VARS", avatarDeath_, battlefield);

                    // back to normal spawn animation after spawning so players moving into range (of 50 yalms) get
                    // just the normal animation
                    avatarPostSpawn.Queue(3000, avatarNormalAnimation_);

                    avatarPostSpawn.SetPos(spawnPosition.X + q.Engine.RandomInt(-3, 3), spawnPosition.Y,
                                           spawnPosition.Z + q.Engine.RandomInt(-3, 3), spawnPosition.Rot);
                    var enmityList = LastEnmityList(battlefield.Area);
                    // if there is a saved enmityList and it is not empty
                    if (enmityList != null && enmityList.Count > 0)
                    {
                        ApplyEnmity(q, avatarPostSpawn, enmityList, battlefield);
                    }

                    // remove that right most digit and update the order
                    battlefield.SetLocalVar("avatarOrder", Math.Floor(avatarOrder / 10));

                    // about half the time a single avatar of the phase shows the text, 2 s later (to wait for the mob
                    // to appear)
                    if (i == numberAvatarsToSpawn && q.Engine.RandomInt(1, 2) == 1)
                    {
                        avatarPostSpawn.Queue(2000, avatarTaunt_);
                    }
                }
            }
        }

        // local spawnCarby: phases 2, 4 and 6
        private void SpawnCarby(IBattlefieldContext q, int carbyID, IBattlefieldView battlefield)
        {
            double mainCarbyHP = battlefield.LocalVar("mainCarbyHP");

            var carby = q.Engine.SpawnMob(carbyID);
            if (carby != null)
            {
                carby.SetHP((int)mainCarbyHP);

                // death listener for setting some useful tracking variables for battlefield (only needed in phase 6)
                carby.AddListener("DEATH", "WTB_SET_DEATH_VARS", carbyDeath_);

                var enmityList = LastEnmityList(battlefield.Area);

                // reanable aggro and linking when spawning again
                carby.SetMobMod(MobModNoAggro, 0);
                carby.SetMobMod(MobModNoLink, 0);

                // if there is a saved enmityList and it is not empty
                if (enmityList != null && enmityList.Count > 0)
                {
                    ApplyEnmity(q, carby, enmityList, battlefield);
                }
            }
        }

        // local despawnCarby: the end of phases 0, 2 and 4
        private void DespawnCarby(IBattlefieldContext q, IBattlefieldMob mainCarby, IBattlefieldView battlefield)
        {
            // the lastEnmityList is set here (not on a death) because the disengage clears the enmity list
            lastEnmityList_[battlefield.Area] = Keep(q, mainCarby.GetEnmityList());
            battlefield.SetLocalVar("mainCarbyHP", mainCarby.HP);
            // prevent from auto aggro or linking after disengaging
            mainCarby.SetMobMod(MobModNoAggro, 1);
            mainCarby.SetMobMod(MobModNoLink, 1);
            // disengage first to get the correct despawn animation
            mainCarby.Disengage();
            // set untargetable so player does not hit carby during despawn process
            mainCarby.SetUntargetable(true);
            // send despawn animation packet so carby looks like he despawns
            mainCarby.SetAnimation(AnimationDespawn);

            // after the despawn animation plays: DISAPPEAR, setHP(0), targetable again
            mainCarby.Timer(7000, carbyGone_);
        }

        // local checkCarbyTwoHour: phase 6's coordinated two-hours
        private static void CheckCarbyTwoHour(IBattlefieldContext q, int mainCarbyID, IBattlefieldView battlefield)
        {
            // if at least one carby has HPP < 10% then trigger 2hr on all carbys, and every 3 mins after that
            bool triggerCoordTwoHour = false;
            double lastCoorTwoHourTime = battlefield.LocalVar("lastCoordTwoHour");

            // if never triggered before then wait until 10% threshold
            if (lastCoorTwoHourTime == 0)
            {
                for (int i = 0; i <= 4; i++)
                {
                    var carby = q.Engine.MobById(mainCarbyID + i);
                    if (carby != null &&
                        // either defeated already
                        ((carby.IsDead && carby.LocalVar("wasDefeated") == 1) ||
                         // or is alive with < 10% HPP
                         (carby.IsAlive && carby.HPP < 10)))
                    {
                        triggerCoordTwoHour = true;
                        battlefield.SetLocalVar("lastCoordTwoHour", q.Engine.GetSystemTime());
                        break;
                    }
                }
            }
            // else check the 3 min timer
            else if (q.Engine.GetSystemTime() > lastCoorTwoHourTime + 180)
            {
                triggerCoordTwoHour = true;
                battlefield.SetLocalVar("lastCoordTwoHour", q.Engine.GetSystemTime());
            }

            if (triggerCoordTwoHour)
            {
                for (int i = 0; i <= 4; i++)
                {
                    var carby = q.Engine.MobById(mainCarbyID + i);
                    if (carby != null && carby.IsAlive && carby.IsEngaged)
                    {
                        UseTwoHour(carby);
                    }
                }
            }
        }

        // local checkElementalAvatarTwoHour: phases 1, 3 and 5
        private static void CheckElementalAvatarTwoHour(IBattlefieldContext q, int mainCarbyID,
                                                        IBattlefieldView battlefield, double phase)
        {
            // if at least one avatar (in the given phase) has HPP < 10% or is dead then trigger 2hr on all avatars in
            // the phase, and every 3 mins after that
            bool triggerCoordTwoHour = false;
            double lastCoorTwoHourTime = battlefield.LocalVar("lastCoordTwoHour");

            // if never triggered before then wait until 10% threshold
            if (lastCoorTwoHourTime == 0)
            {
                for (int i = 5; i <= 10; i++)
                {
                    var avatar = q.Engine.MobById(mainCarbyID + i);
                    if (avatar != null &&
                        avatar.LocalVar("phase") == phase &&
                        // either dead
                        ((avatar.IsDead && avatar.LocalVar("wasDefeated") == 1) ||
                         // or is alive with <10% HPP
                         (avatar.IsAlive && avatar.HPP < 10)))
                    {
                        triggerCoordTwoHour = true;
                        battlefield.SetLocalVar("lastCoordTwoHour", q.Engine.GetSystemTime());
                        break;
                    }
                }
            }
            // else check the 3 min timer
            else if (q.Engine.GetSystemTime() > lastCoorTwoHourTime + 180)
            {
                triggerCoordTwoHour = true;
                battlefield.SetLocalVar("lastCoordTwoHour", q.Engine.GetSystemTime());
            }

            if (triggerCoordTwoHour)
            {
                for (int i = 5; i <= 10; i++)
                {
                    var avatar = q.Engine.MobById(mainCarbyID + i);
                    if (avatar != null &&
                        avatar.LocalVar("phase") == phase &&
                        avatar.IsAlive &&
                        avatar.IsEngaged)
                    {
                        UseTwoHour(avatar);
                    }
                }
            }
        }

        // mob:useMobAbility(avatarTwoHours[mob:getName()]): a name not in the table is useMobAbility(nil)
        private static void UseTwoHour(IBattlefieldMob mob)
        {
            if (AvatarTwoHours.TryGetValue(mob.Name ?? "", out int skill)) mob.UseMobAbility(skill);
            else mob.UseMobAbility();
        }

        // can help if you are doing the quest or already completed the quest
        public override bool EntryRequirement(IBattlefieldContext q, bool isRegistrant)
        {
            bool hasQuestItems = q.Player.HasKeyItem(KeyItem.EyeOfFlames) && q.Player.HasKeyItem(KeyItem.EyeOfFrost) &&
                                 q.Player.HasKeyItem(KeyItem.EyeOfGales) && q.Player.HasKeyItem(KeyItem.EyeOfStorms) &&
                                 q.Player.HasKeyItem(KeyItem.EyeOfTides) && q.Player.HasKeyItem(KeyItem.EyeOfTremors) &&
                                 q.Player.HasKeyItem(KeyItem.RainbowResonator);

            var qd = (BattlefieldQuestData)Data;
            bool prevCompletedQuest = q.Player.QuestStatus(qd.QuestArea, qd.Quest) == XI.Quests.QuestStatus.Completed;

            // registrant must actually be doing the quest
            if (isRegistrant)
            {
                return hasQuestItems;
            }
            // others can help if they are either doing the quest or already completed the quest
            else
            {
                return hasQuestItems || prevCompletedQuest;
            }
        }

        public override void SetupBattlefield(IBattlefieldContext q)
        {
            var battlefield = q.Battlefield;
            // the random order the elemental (non-carby) avatars spawn in (which avatars in which phases)
            var shuffledAvatarIndexes = q.Engine.Shuffle(new[] { 1, 2, 3, 4, 5, 6 });
            // convert the shuffled indexes into a single number: tonumber(table.concat(shuffled, ''))
            double avatarOrder = double.Parse(string.Concat(shuffledAvatarIndexes),
                                              System.Globalization.CultureInfo.InvariantCulture);
            battlefield.SetLocalVar("avatarOrder", avatarOrder);

            // reset the local variables on each elemental avatar: local vars only reset on spawn, and without this an
            // avatar killed by a previous group could not be told from one killed by this group
            int mainCarbyID = MainCarbyId(q, battlefield.Area);
            for (int i = 5; i <= 10; i++)
            {
                var avatar = q.Engine.MobById(mainCarbyID + i);
                if (avatar != null)
                {
                    avatar.SetLocalVar("wasDefeated", 0);
                    avatar.SetLocalVar("phase", 0);
                }
            }

            // the main carby is not killable until going through all phases (and thus at phase 6)
            var mainCarby = q.Engine.MobById(mainCarbyID);
            if (mainCarby != null)
            {
                mainCarby.SetUnkillable(true);
            }
        }

        // local setBattlefieldLocalVarsForPhase
        private static void SetBattlefieldLocalVarsForPhase(IBattlefieldContext q, IBattlefieldView battlefield,
                                                            double phase, int numToSpawn, int typeToSpawn)
        {
            battlefield.SetLocalVar("phase", phase);
            // set up an avatar spawn in 5 seconds
            battlefield.SetLocalVar("spawnTriggerTime", q.Engine.GetSystemTime() + 5.0);
            battlefield.SetLocalVar("numToSpawn", numToSpawn);
            battlefield.SetLocalVar("typeToSpawn", typeToSpawn);
            battlefield.SetLocalVar("lastCoordTwoHour", 0);
        }

        // the main logic of the fight, the phases' changes included
        public override void OnBattlefieldTick(IBattlefieldContext q, int tick)
        {
            // call the general Battlefield tick function first then do custom logic
            q.Framework.OnBattlefieldTick(tick);

            var battlefield = q.Battlefield;
            // the main carbuncle spawned at start (for the area): every other id is after it
            int mainCarbyID = MainCarbyId(q, battlefield.Area);
            var mainCarby = q.Engine.MobById(mainCarbyID);

            // the current phase (0 to 6, see the header)
            double phase = battlefield.LocalVar("phase");

            // a pending spawn of avatars or carbys: the trigger time is set by the phase change
            if (battlefield.LocalVar("spawnTriggerTime") > 0)
            {
                // spawn when the time has come, else wait
                if (q.Engine.GetSystemTime() > battlefield.LocalVar("spawnTriggerTime"))
                {
                    int numToSpawn = (int)battlefield.LocalVar("numToSpawn");

                    if (mainCarby != null &&
                        battlefield.LocalVar("typeToSpawn") == SpawnTypeElementalAvatar)
                    {
                        var spawnPosition = mainCarby.GetPos();
                        SpawnElementalAvatars(q, spawnPosition, battlefield, numToSpawn, phase);
                    }
                    else if (battlefield.LocalVar("typeToSpawn") == SpawnTypeCarbuncle)
                    {
                        // carby is only spawned either as the single main carby or as the final five carbys
                        if (numToSpawn == 5)
                        {
                            for (int i = 0; i <= 4; i++)
                            {
                                SpawnCarby(q, mainCarbyID + i, battlefield);
                            }

                            // the main carby is now killable since it is phase 6
                            if (mainCarby != null)
                            {
                                mainCarby.SetUnkillable(false);
                            }
                        }
                        else
                        {
                            SpawnCarby(q, mainCarbyID, battlefield);
                        }
                    }

                    // reset the spawn variables
                    battlefield.SetLocalVar("spawnTriggerTime", 0);
                    battlefield.SetLocalVar("numToSpawn", 0);
                    battlefield.SetLocalVar("typeToSpawn", 0);
                }

                // no need to do any other phase checks while waiting for a spawn trigger
                return;
            }

            // the total number of elemental (non-carby) avatars defeated (used in phase checks)
            double avatarsDefeated = battlefield.LocalVar("avatarsDefeated");

            // the conditions of the next phase: a change returns, so the next tick starts the wait for the spawn
            if (phase == 0 &&
                mainCarby != null &&
                mainCarby.IsAlive &&
                mainCarby.HPP < 75)
            {
                phase = phase + 1;
                SetBattlefieldLocalVarsForPhase(q, battlefield, phase, 1, SpawnTypeElementalAvatar);
                DespawnCarby(q, mainCarby, battlefield);
                return;
            }
            else if (phase == 1 &&
                     avatarsDefeated == 1)
            {
                phase = phase + 1;
                SetBattlefieldLocalVarsForPhase(q, battlefield, phase, 1, SpawnTypeCarbuncle);
                return;
            }
            else if (phase == 2 &&
                     mainCarby != null &&
                     mainCarby.IsAlive &&
                     mainCarby.HPP < 50)
            {
                phase = phase + 1;
                SetBattlefieldLocalVarsForPhase(q, battlefield, phase, 2, SpawnTypeElementalAvatar);
                DespawnCarby(q, mainCarby, battlefield);
                return;
            }
            else if (phase == 3 &&
                     avatarsDefeated == 3)
            {
                phase = phase + 1;
                SetBattlefieldLocalVarsForPhase(q, battlefield, phase, 1, SpawnTypeCarbuncle);
                return;
            }
            else if (phase == 4 &&
                     mainCarby != null &&
                     mainCarby.IsAlive &&
                     mainCarby.HPP < 25)
            {
                phase = phase + 1;
                SetBattlefieldLocalVarsForPhase(q, battlefield, phase, 3, SpawnTypeElementalAvatar);
                DespawnCarby(q, mainCarby, battlefield);
                return;
            }
            else if (phase == 5 &&
                     avatarsDefeated == 6)
            {
                phase = phase + 1;
                SetBattlefieldLocalVarsForPhase(q, battlefield, phase, 5, SpawnTypeCarbuncle);
                return;
            }

            // the checks not related to phase changes
            // a phase of elemental (non-carby) avatars (1, 3, 5)
            if (phase - Math.Floor(phase / 2) * 2 == 1)
            {
                // check if avatars should use 2hrs
                CheckElementalAvatarTwoHour(q, mainCarbyID, battlefield, phase);
            }
            // the final phase with five carbys
            else if (phase == 6)
            {
                // check if the carbys should use 2hrs
                CheckCarbyTwoHour(q, mainCarbyID, battlefield);
                // check if all carbys are dead (and thus the fight is won)
                CheckAllCarbyDead(q, mainCarbyID, battlefield);
            }
        }
    }
}
