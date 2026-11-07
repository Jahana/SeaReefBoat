// Unlocking a Myth, 20 jobs: the template xi.jeuno.helpers.UnlockingAMyth
// (scripts/quests/jeuno/helpers.lua:115-352 at 77e0890a; carry-quests-1 dropped it from the file, and a record
// --lua-at takes the file back), a line-for-line port (quest rework B3b). Each of the twenty Lua files
// (scripts/quests/jeuno/Unlocking_A_Myth_<JOB>.lua)
// is UnlockingAMyth:new(xi.job.<JOB>); here each quest is a sealed subclass naming its job (the quest classes are found
// by reflection: the abstract base is skipped). The quest id is UNLOCKING_A_MYTH_WARRIOR - 1 + jobId, so each job's
// constant name is in Jobs. Zalsuhm : !pos -32.990 5.901 -117.458 245.
// Available on the job's main job: Zalsuhm's 10090 while the zoning flag is set and he is upset (Upset_Zalsuhm > 0);
// else (Upset_Zalsuhm cleared if set) 10086 (jobId) with the job's Vigil weapon in the main or ranged slot, 10085
// without. 10086's finish: option 53: upset, the zoning flag set; option jobId: begun. Accepted on the job: the weapon
// traded alone: by its weapon skill points against xi.equipment.vigilWeaponRequiredWsPoints(player) (250; the era module
// modules/era/lua/globals/equipment/unlocking_a_myth.lua overrides it by Nyzul Isle progress): <= 1/5: 10091, <= 4/5:
// 10092, below: 10093, at or above: 10088 (jobId); the trigger: the cutscene 10087. 10088's finish: completed (no reward
// table), MYTHIC_LEARNED (jobId), the job's mythic weapon skill learned; the weapon is kept.
// Kept as the Lua has it: the trade compares the points with requiredWsPoints / 5 and * 4 / 5 as Lua numbers (fractions
// kept: the C# divides in double); the last arm's `wsPoints >= requiredWsPoints` is always true where it is reached, so a
// trade of the weapon always answers; another job's quest is silent (each check reads the main job); the quest can be
// flagged on several jobs.
// Zalsuhm has no script; his DefaultActions event is 10085, which plays when no job's section matches (only the main
// job's quest can). 10090 has no finish handler. The era module overrides the Lua global, not the quest: the C# quest
// reads it through Player.VigilWeaponRequiredWsPoints, which calls the global (the server's Lua module loader, platform
// round P11, applies the module when init.txt lists it and its condition holds: golden
// jeuno.unlocking_a_myth_warrior#era).
using XI.Quests;
using XI.Quests.Authoring;

namespace XI.QuestContent.Jeuno
{
    public abstract class UnlockingAMyth : QuestScript
    {
        // vigilWeaponsData, by job: the quest's constant name and Lua file, the Vigil weapon, the weapon skill it unlocks
        // (xi.wsUnlock)
        public static readonly System.Collections.Generic.IReadOnlyList<(int Job, string ConstName, string LuaJob, int ItemId, int WsUnlockId)> Jobs =
            new (int Job, string ConstName, string LuaJob, int ItemId, int WsUnlockId)[]
        {
            (Job.War, "UNLOCKING_A_MYTH_WARRIOR", "WAR", Item.SturdyAxe, 24),          // KINGS_JUSTICE
            (Job.Mnk, "UNLOCKING_A_MYTH_MONK", "MNK", Item.BurningFists, 15),         // ASCETICS_FURY
            (Job.Whm, "UNLOCKING_A_MYTH_WHITE_MAGE", "WHM", Item.Werebuster, 29),     // MYSTIC_BOON
            (Job.Blm, "UNLOCKING_A_MYTH_BLACK_MAGE", "BLM", Item.MagesStaff, 30),     // VIDOHUNIR
            (Job.Rdm, "UNLOCKING_A_MYTH_RED_MAGE", "RDM", Item.VorpalSword, 20),      // DEATH_BLOSSOM
            (Job.Thf, "UNLOCKING_A_MYTH_THIEF", "THF", Item.Swordbreaker, 17),        // MANDALIC_STAB
            (Job.Pld, "UNLOCKING_A_MYTH_PALADIN", "PLD", Item.BraveBlade, 21),        // ATONEMENT
            (Job.Drk, "UNLOCKING_A_MYTH_DARK_KNIGHT", "DRK", Item.DeathSickle, 25),   // INSURGENCY
            (Job.Bst, "UNLOCKING_A_MYTH_BEASTMASTER", "BST", Item.DoubleAxe, 23),     // PRIMAL_REND
            (Job.Brd, "UNLOCKING_A_MYTH_BARD", "BRD", Item.DancingDagger, 18),        // MORDANT_RIME
            (Job.Rng, "UNLOCKING_A_MYTH_RANGER", "RNG", Item.KillerBow, 33),          // TRUEFLIGHT
            (Job.Sam, "UNLOCKING_A_MYTH_SAMURAI", "SAM", Item.Windslicer, 28),        // TACHI_RANA
            (Job.Nin, "UNLOCKING_A_MYTH_NINJA", "NIN", Item.SasukeKatana, 27),        // BLADE_KAMU
            (Job.Drg, "UNLOCKING_A_MYTH_DRAGOON", "DRG", Item.RadiantLance, 26),      // DRAKESBANE
            (Job.Smn, "UNLOCKING_A_MYTH_SUMMONER", "SMN", Item.ScepterStaff, 31),     // GARLAND_OF_BLISS
            (Job.Blu, "UNLOCKING_A_MYTH_BLUE_MAGE", "BLU", Item.Wightslayer, 22),     // EXPIACION
            (Job.Cor, "UNLOCKING_A_MYTH_CORSAIR", "COR", Item.Quicksilver, 34),       // LEADEN_SALUTE
            (Job.Pup, "UNLOCKING_A_MYTH_PUPPETMASTER", "PUP", Item.InfernoClaws, 16), // STRINGING_PUMMEL
            (Job.Dnc, "UNLOCKING_A_MYTH_DANCER", "DNC", Item.MainGauche, 19),         // PYRRHIC_KLEOS
            (Job.Sch, "UNLOCKING_A_MYTH_SCHOLAR", "SCH", Item.ElderStaff, 32),        // OMNISCIENCE
        };

        readonly int _jobId;
        readonly int _itemId;
        readonly int _wsUnlockId;
        readonly string _luaJob;

        protected UnlockingAMyth(int jobId) : this(Jobs[jobId - 1]) { }

        UnlockingAMyth((int Job, string ConstName, string LuaJob, int ItemId, int WsUnlockId) data) : base(QuestLog.Jeuno, data.ConstName)
        {
            _jobId = data.Job;
            _itemId = data.ItemId;
            _wsUnlockId = data.WsUnlockId;
            _luaJob = data.LuaJob;
        }

        // the require name of the Lua file this quest replaces: its slot in the containers list
        public override string LegacyPath => "scripts/quests/jeuno/Unlocking_A_Myth_" + _luaJob;

        protected override void Build(QuestScriptBuilder b)
        {
            int jobId = _jobId;
            int weaponItemId = _itemId;
            int wsUnlockId = _wsUnlockId;

            // quest can be flagged multiple times for different jobs, no need for additional check here
            // https://www.ffxiah.com/forum/topic/11987/cancel-unlocking-a-myth-quest
            b.State(QuestStatus.Available, when: q => q.Player.MainJob == jobId)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Zalsuhm).OnTrigger(q =>
                {
                    // Must be wearing one of the weapons in the primary slot to flag the quest
                    int weaponMain = q.Player.EquipId(Slot.Main);
                    int weaponRanged = q.Player.EquipId(Slot.Ranged);
                    bool isWearingWeapon = weaponMain == weaponItemId || weaponRanged == weaponItemId;
                    bool upsetZalsuhm = q.Quest.Var("Upset_Zalsuhm") > 0;

                    if (q.Player.NeedToZone && upsetZalsuhm)
                    {
                        return QuestReply.ProgressEvent(10090);
                    }
                    else
                    {
                        if (upsetZalsuhm)
                        {
                            q.Quest.SetVar("Upset_Zalsuhm", 0);
                        }

                        if (isWearingWeapon)
                        {
                            return QuestReply.ProgressEvent(10086, jobId);
                        }
                        else
                        {
                            return QuestReply.ProgressEvent(10085);
                        }
                    }
                })
                .OnEventFinish(Zone.LowerJeuno, 10086, q =>
                {
                    if (q.Option == 53)
                    {
                        q.Quest.SetVar("Upset_Zalsuhm", 1);
                        q.Player.SetNeedToZone(true);
                    }
                    else if (q.Option == jobId)
                    {
                        q.Quest.Begin();
                    }
                });

            b.State(QuestStatus.Accepted, when: q => q.Player.MainJob == jobId)
                .Npc(Zone.LowerJeuno, Npc.LowerJeuno.Zalsuhm)
                    .OnTrade(q =>
                    {
                        // TODO is there a message for trading anything else?
                        if (q.Trade.HasExactly(weaponItemId))
                        {
                            // xi.equipment.vigilWeaponRequiredWsPoints(player): the bridge calls the Lua global
                            // (250, or the era module's override when the Lua module loader applied it)
                            int requiredWsPoints = q.Player.VigilWeaponRequiredWsPoints;
                            int wsPoints = q.Trade.ItemWeaponskillPoints(0);

                            if (wsPoints <= requiredWsPoints / 5.0)
                            {
                                return QuestReply.Event(10091);
                            }
                            else if (wsPoints <= requiredWsPoints * 4 / 5.0)
                            {
                                return QuestReply.Event(10092);
                            }
                            else if (wsPoints < requiredWsPoints)
                            {
                                return QuestReply.Event(10093);
                            }
                            else if (wsPoints >= requiredWsPoints)
                            {
                                return QuestReply.ProgressEvent(10088, jobId);
                            }
                        }
                        return null;
                    })
                    .OnTrigger(q =>
                    {
                        return QuestReply.Cutscene(10087);
                    })
                .OnEventFinish(Zone.LowerJeuno, 10088, q =>
                {
                    if (q.Quest.Complete())
                    {
                        q.Text.Special(Text.LowerJeuno.MYTHIC_LEARNED, jobId);
                        q.Player.AddLearnedWeaponskill(wsUnlockId);
                        // player keeps vigil weapon
                    }
                });
        }
    }
}
