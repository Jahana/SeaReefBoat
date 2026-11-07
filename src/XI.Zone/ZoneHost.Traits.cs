using System.Collections.Generic;
using XI.Script;

namespace XI.Zone
{
    // Job traits: charutils::BuildingCharTraitsTable, battleutils::AddTraits (chars and mobs), blueutils::CalculateTraits,
    // CBattleEntity / CCharEntity::addTrait / delTrait (the TraitList, its modifiers and the m_TraitList bits COMMAND_DATA
    // sends), the xi.traits.canApplyTrait filter, the CTrait Lua type and the hasTrait binding; a Monipulator's species
    // jobs stand in for the char's, and a level sync rebuilds the table (ApplySyncLevel). Not ported: pets (petutils'
    // wyvern and avatar traits, setPetStats), trusts and the traits' consumers past their modifiers (the attack round's
    // merit bonuses on TRAIT_DOUBLE/TRIPLE_ATTACK, Recycle and the other battleutils hasTrait checks).
    public sealed partial class ZoneHost
    {
        private const string TraitType = "CTrait";
        private TraitTable traitTable_;

        // traits::LoadTraitsList, once, under luautils::IsContentEnabled
        private TraitTable TraitTableOf() => traitTable_ ??= TraitTable.Load(db_, tag => tag == "" || contentEnabled_(tag));

        // charutils::BuildingCharTraitsTable
        internal void BuildCharTraits(CharData c)
        {
            foreach (var t in c.TraitList) c.DelModifier(t.Mod, t.Value);
            c.TraitList.Clear();
            System.Array.Clear(c.Traits);
            c.TraitsBuilt = true;

            byte mjob = c.MJob, sjob = c.SJob, mlvl = c.MLvl, slvl = c.SLvl;
            // a Monipulator has its species' traits, both jobs at the species level
            if (c.Monstrosity != null)
            {
                var species = Monstrosity.SpeciesJobs(c.Monstrosity);
                (mjob, sjob, mlvl, slvl) = (species.MainJob, species.SubJob, species.Level, species.Level);
            }

            var table = TraitTableOf();
            AddTraits(c, table.ForJob(mjob, log_), mlvl);
            AddTraits(c, table.ForJob(sjob, log_), slvl);

            if (mjob == JobBluId || sjob == JobBluId) TraitCalculateBlue(c);

            c.DelModifier(Mod.MEVA, c.TraitMagicEvasion);
            // Player MEVA is Rank G
            c.TraitMagicEvasion = (short)(loader_.Stats?.Growth.GetMaxSkillByRank(12, mlvl) ?? 0);
            c.AddModifier(Mod.MEVA, c.TraitMagicEvasion);
        }

        // mobutils::CalculateMobStats' traits for the main and the sub job (MobStats.AddTraits)
        private void TraitAddMobTraits(MobEntity m)
        {
            var table = TraitTableOf();
            AddTraits(m, table.ForJob(m.MJob, log_), m.MLvl);
            AddTraits(m, table.ForJob(m.SJob, log_), m.SLvl);
        }

        // battleutils::AddTraits
        private void AddTraits(BattleEntity e, IReadOnlyList<Trait> traitList, byte level)
        {
            if (traitList == null) return;
            var c = e as CharData;

            foreach (var trait in traitList)
            {
                if (level >= trait.Level && trait.Level > 0)
                {
                    bool add = true;

                    for (int j = 0; j < e.TraitList.Count; ++j)
                    {
                        var existing = e.TraitList[j];
                        if (existing.Id != trait.Id) continue;

                        // Check if we still have the merit required for this trait
                        if (c != null && existing.MeritId > 0)
                        {
                            if (TraitMeritCount(c, existing.MeritId) == 0)
                            {
                                TraitDel(e, existing);
                                break;
                            }
                            else if (existing.MeritId == trait.MeritId)
                            {
                                add = false;
                                break;
                            }
                        }

                        if (existing.Rank < trait.Rank)
                        {
                            TraitDel(e, existing);
                            break;
                        }
                        else if (existing.Rank > trait.Rank)
                        {
                            add = false;
                            break;
                        }
                        else if (existing.Mod == trait.Mod)
                        {
                            add = false;
                            break;
                        }
                    }

                    // Don't add traits that aren't merited yet
                    if (c != null && trait.MeritId > 0 && TraitMeritCount(c, trait.MeritId) == 0) add = false;

                    // Allow scripts to filter traits per entity and zone
                    if (add && !TraitCanApply(trait, e)) add = false;

                    if (add) TraitAdd(e, trait);
                }
            }
        }

        // PMeritPoints->GetMerit(id)->count (an unknown merit counts 0)
        private static int TraitMeritCount(CharData c, uint meritId) => c.Merits?.Count((ushort)meritId) ?? 0;

        // CBattleEntity::addTrait, and CCharEntity's charutils::addTrait bit
        private static void TraitAdd(BattleEntity e, Trait trait)
        {
            e.TraitList.Add(trait);
            e.AddModifier(trait.Mod, trait.Value);
            if (e is CharData c) TraitSetBit(c, trait.Id, true);
        }

        // CBattleEntity::delTrait, and CCharEntity's charutils::delTrait bit
        private static void TraitDel(BattleEntity e, Trait trait)
        {
            e.DelModifier(trait.Mod, trait.Value);
            e.TraitList.RemoveAll(t => t == trait);
            if (e is CharData c) TraitSetBit(c, trait.Id, false);
        }

        // addBit / delBit on m_TraitList
        private static void TraitSetBit(CharData c, int traitId, bool on)
        {
            if (traitId >= c.Traits.Length * 8) return;
            if (on) c.Traits[traitId >> 3] |= (byte)(1 << (traitId & 7));
            else c.Traits[traitId >> 3] &= (byte)~(1 << (traitId & 7));
        }

        // luautils::callGlobal<bool>("xi.traits.canApplyTrait", PTrait, PEntity, PEntity->loc.zone). The zone goes as
        // nil: there is no CZone Lua type here (the script does not read it yet). Without a script engine there is no
        // filter.
        private bool TraitCanApply(Trait trait, BattleEntity e)
        {
            if (lua_ == null) return true;
            const string path = "xi.traits.canApplyTrait";
            int top = lua_.Top;
            try
            {
                lua_.GetPath(path);
                if (lua_.IsNil(-1))
                {
                    log_($"luautils::callGlobalFunction: {path}: Function not found");
                    return false;
                }
                lua_.PushObject(trait, TraitType);
                lua_.PushObject(e);
                lua_.PushNil();
                lua_.Call(3, 1);
                return lua_.ToBoolean(-1);
            }
            catch (LuaException ex)
            {
                log_($"luautils::callGlobalFunction: {path}: {ex.Message}");
                return false;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // blueutils::CalculateTraits
        private void TraitCalculateBlue(CharData c)
        {
            var traitsList = TraitTableOf().ForJob(JobBluId, log_);
            var points = new SortedDictionary<byte, byte>();
            var traitsToAdd = new List<Trait>();
            int traitTierBonus = c.GetMod(Mod.BLUE_JOB_TRAIT_BONUS);

            foreach (byte setBlueSpell in c.SetBlueSpells)
            {
                if (setBlueSpell == 0) continue;
                if (GetBlueSpell(setBlueSpell + BlueSpellOffset) is not BlueSpell spell) continue;
                byte category = spell.TraitCategory, weight = spell.TraitWeight;
                points[category] = points.TryGetValue(category, out byte total) ? (byte)(total + weight) : weight;
            }

            // Add BLU traits that are eligible
            foreach (var (category, totalWeight) in points)
            {
                foreach (var trait in traitsList)
                {
                    if (trait.Level != 0) continue;
                    // Player is eligible for this Blue Trait
                    if (trait.Category == category && totalWeight >= trait.Points && !trait.JobPointsOnly)
                    {
                        // Same trait ID, trait mod, and is weaker than new trait: erase the lower tier
                        traitsToAdd.RemoveAll(cur => cur.Id == trait.Id && cur.Rank <= trait.Rank && cur.Mod == trait.Mod);
                        traitsToAdd.Add(trait);
                    }
                }
            }

            var upgradedTraits = new List<Trait>();

            // Search for higher tier bonuses to boost them before we check if existing traits are stronger
            if (traitTierBonus > 0)
            {
                for (int i = 0; i < traitsToAdd.Count;)
                {
                    var currentTrait = traitsToAdd[i];
                    int newRank = currentTrait.Rank + traitTierBonus;
                    Trait newTrait = null;
                    foreach (var trait in traitsList)
                        if (trait.Level == 0 && TraitSameBlue(trait, currentTrait) && newRank >= trait.Rank) newTrait = trait;

                    if (newTrait != null)
                    {
                        traitsToAdd.RemoveAt(i);
                        upgradedTraits.Add(newTrait);
                    }
                    else
                    {
                        ++i;
                    }
                }
            }

            // Append in upgraded traits in place of ones that were removed in the search for upgraded traits, if any
            traitsToAdd.AddRange(upgradedTraits);

            // Remove weaker BLU traits: the player has the real job trait
            foreach (var existing in c.TraitList)
                traitsToAdd.RemoveAll(t => existing.Id == t.Id && existing.Mod == t.Mod && existing.Rank >= t.Rank);

            // Remove weaker player traits (their bit stays, as C++)
            foreach (var trait in traitsToAdd)
            {
                for (int i = 0; i < c.TraitList.Count;)
                {
                    var existing = c.TraitList[i];
                    if (existing.Id == trait.Id && existing.Mod == trait.Mod && existing.Rank < trait.Rank)
                    {
                        c.DelModifier(existing.Mod, existing.Value);
                        c.TraitList.RemoveAt(i);
                        continue;
                    }
                    ++i;
                }
            }

            // Finally, add traits to the player
            foreach (var trait in traitsToAdd)
            {
                TraitSetBit(c, trait.Id, true);
                c.TraitList.Add(trait);
                c.AddModifier(trait.Mod, trait.Value);
            }
        }

        // CalculateTraits' isSameBluTrait: Double Attack upgrades to Triple Attack, Gilfinder to Treasure Hunter
        private static bool TraitSameBlue(Trait a, Trait b)
        {
            if (a.Category != b.Category) return false;
            if (a.Mod != b.Mod)
            {
                return a.Mod == Mod.DOUBLE_ATTACK && b.Mod == Mod.TRIPLE_ATTACK || a.Mod == Mod.TRIPLE_ATTACK && b.Mod == Mod.DOUBLE_ATTACK ||
                       a.Mod == Mod.GILFINDER && b.Mod == Mod.TREASURE_HUNTER || a.Mod == Mod.TREASURE_HUNTER && b.Mod == Mod.GILFINDER;
            }
            // Must be after mod ID check due to edge case
            return a.Id == b.Id;
        }

        // CLuaBaseEntity::hasTrait and the CTrait Lua type (lua_trait.cpp)
        private void RegisterTraitBindings(Dictionary<string, LuaFunction> m)
        {
            m["hasTrait"] = l => Ret(l, Entity(l) is BattleEntity b && b.HasTrait(l.ToIntArg(2)));

            static Trait Of(LuaState l) => l.ToObject(1) as Trait ?? throw new System.InvalidOperationException("bad self: expected " + TraitType);
            lua_.RegisterType(TraitType, new Dictionary<string, LuaFunction>
            {
                ["getID"] = l => Ret(l, Of(l).Id),
                ["getJob"] = l => Ret(l, Of(l).Job),
                ["getLevel"] = l => Ret(l, Of(l).Level),
                ["getMod"] = l => Ret(l, (int)Of(l).Mod),
                ["getValue"] = l => Ret(l, Of(l).Value),
                ["getRank"] = l => Ret(l, Of(l).Rank),
                ["getMeritID"] = l => Ret(l, Of(l).MeritId),
            });
        }
    }
}
