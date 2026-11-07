using System;
using System.Collections.Generic;
using System.Linq;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // Binding batch 10: CLuaBaseEntity's addLinkpearl, forceRezone, getConfrontationEffect / copyConfrontationEffect,
    // addPartyEffect / hasPartyEffect / removePartyEffect, reloadParty, doWildCard and doRandomDeal
    // (battleutils::DoWildCardToEntity / DoRandomDealToEntity), healingWaltz, takeSwipeLungeDamage
    // (battleutils::TakeSwipeLungeDamage) and addDamageFromMultipliers (attackutils::CheckForDamageMultiplier).
    public sealed partial class ZoneHost
    {
        // PHYSICAL_ATTACK_TYPE
        public enum PhysicalAttackType : byte
        {
            Normal = 0, Double = 1, Triple = 2, Zanshin = 3, Kick = 4, Ranged = 5, RapidShot = 6, Samba = 7, Quad = 8, Daken = 9,
            Followup = 10,
        }

        private const byte JobCor = 17;
        private const ushort RecastRandomDeal = 196;
        private const ushort MeritLoadedDeck = 0x0C06;

        // attackutils::CheckForDamageMultiplier: the weapon's occasional extra damage and the REM_OCC multipliers on a
        // proc swing, then the attack type's own double / triple damage chance
        internal static uint PhysicalDamageMultiplier(CharData c, WeaponInfo weapon, uint damage, PhysicalAttackType type, int slot,
            bool allowProc, Random rng)
        {
            if (weapon == null) return damage;
            uint originalDamage = damage;
            int tripleDmg = 0, doubleDmg = 0;
            switch (type)
            {
                case PhysicalAttackType.Ranged:
                case PhysicalAttackType.RapidShot:
                    tripleDmg = c.GetMod(Mod.REM_OCC_DO_TRIPLE_DMG_RANGED) / 10;
                    doubleDmg = c.GetMod(Mod.REM_OCC_DO_DOUBLE_DMG_RANGED) / 10;
                    break;
                case PhysicalAttackType.Normal:
                    if (slot == CharStats.SlotMain) // Only applies to mainhand
                    {
                        tripleDmg = c.GetMod(Mod.REM_OCC_DO_TRIPLE_DMG) / 10;
                        doubleDmg = c.GetMod(Mod.REM_OCC_DO_DOUBLE_DMG) / 10;
                    }
                    break;
            }
            float extraDmg = weapon.Equip == null ? 0 : CharStats.ScaledItemModifier(c, weapon.Equip, Mod.OCC_DO_EXTRA_DMG) / 100.0f;
            int extraChance = weapon.Equip == null ? 0 : CharStats.ScaledItemModifier(c, weapon.Equip, Mod.EXTRA_DMG_CHANCE) / 10;

            if (allowProc)
            {
                if (extraDmg > 3.0f && extraChance > 0 && 1 + rng.Next(100) <= extraChance) return (uint)(damage * extraDmg);
                if (tripleDmg > 0 && 1 + rng.Next(100) <= tripleDmg) return (uint)(damage * 3.0f);
                if (extraDmg > 2.0f && extraChance > 0 && 1 + rng.Next(100) <= extraChance) return (uint)(damage * extraDmg);
                if (doubleDmg > 0 && 1 + rng.Next(100) <= doubleDmg) return (uint)(damage * 2.0f);
                if (extraDmg > 0 && extraChance > 0 && 1 + rng.Next(100) <= extraChance) return (uint)(damage * extraDmg);
            }

            Mod? rate = type switch
            {
                PhysicalAttackType.Zanshin => Mod.ZANSHIN_DOUBLE_DAMAGE,
                PhysicalAttackType.Triple => Mod.TA_TRIPLE_DMG_RATE,
                PhysicalAttackType.Double => Mod.DA_DOUBLE_DMG_RATE,
                PhysicalAttackType.RapidShot => Mod.RAPID_SHOT_DOUBLE_DAMAGE,
                PhysicalAttackType.Samba => Mod.SAMBA_DOUBLE_DAMAGE,
                _ => null,
            };
            if (rate is Mod mod && rng.Next(100) < c.GetMod(mod)) return originalDamage * (type == PhysicalAttackType.Triple ? 3u : 2u);
            return originalDamage;
        }

        // charutils::ForceRezone: back into the same zone, the boundary and the packets dropped, the pet kept
        private void ForceRezone(CharData c)
        {
            if (SessionOf(c) is not { } s) return;
            c.Boundary = 0;
            s.PacketList.Clear();
            zoneChanges_[s] = c.Zone;
            SavePetForZoning(c);
        }

        // battleutils::DoWildCardToEntity: every ability recast but the two-hours' reset, then by the roll TP, the
        // two-hours (Wild Card itself kept for a COR) and MP
        private void DoWildCardToEntity(CharData target, byte roll)
        {
            var recasts = RecastsOf(target);
            recasts.ResetAbilities();
            if (roll >= 3) target.UpdateMask |= CharStats.UpdateHp;

            switch (roll)
            {
                case 3: // 3 grants 1000 TP
                    target.Tp = 1000;
                    break;
                case 4: // 4 grants 3000 TP
                    target.Tp = 3000;
                    break;
                case 5: // Resets Lv1 1HRs and restores 50% MP
                    if (target.MJob != JobCor) recasts.Del(RecastType.Ability, Recast.Special);
                    if (target.MaxMp > 0 && target.Mp < target.MaxMp / 2) target.Mp = target.MaxMp / 2;
                    break;
                case 6: // Resets Lv1/Lv96 1HRs and restores 100% MP
                    recasts.Del(RecastType.Ability, Recast.Special2);
                    if (target.MJob != JobCor) recasts.Del(RecastType.Ability, Recast.Special);
                    AddMp(target, target.MaxMp);
                    break;
            }
            PushTo(target, AbilRecast(target));
        }

        // battleutils::DoRandomDealToEntity: one running ability recast reset (two by RANDOM_DEAL_BONUS), never the
        // two-hours or Random Deal; Loaded Deck always picks a running one, at 50% + its merit with only one running
        private bool DoRandomDealToEntity(CharData c, BattleEntity target)
        {
            var recasts = RecastsOf(target);
            var recastList = recasts.GetRecastList(RecastType.Ability);
            var resetCandidates = new List<int>();
            var activeCooldowns = new List<int>();
            for (int i = 0; i < recastList.Count; ++i)
            {
                var recast = recastList[i];
                if (recast.Id == Recast.Special || recast.Id == Recast.Special2 || recast.Id == RecastRandomDeal) continue;
                resetCandidates.Add(i);
                if (recast.RecastTimeMs > 0) activeCooldowns.Add(i);
            }
            // Evade because we have no abilities that can be reset
            if (resetCandidates.Count == 0 || activeCooldowns.Count == 0) return false;

            var rng = MobRng;
            int loadedDeck = c.Merits?.Value(c, MeritLoadedDeck) ?? 0;
            int loadedDeckChance = 50 + loadedDeck;
            byte resetTwoChance = (byte)Math.Min((sbyte)c.GetMod(Mod.RANDOM_DEAL_BONUS), (sbyte)50);

            if (loadedDeck > 0) // Loaded Deck Merit Version
            {
                if (activeCooldowns.Count > 1)
                {
                    Shuffle(activeCooldowns, rng);
                    loadedDeckChance = 100;
                }
                // Evade because we failed to reset with loaded deck
                if (loadedDeckChance < rng.Next(100)) return false;
                recasts.DeleteByIndex(RecastType.Ability, activeCooldowns[0]);
                if (activeCooldowns.Count > 1 && resetTwoChance >= rng.Next(100)) recasts.DeleteByIndex(RecastType.Ability, activeCooldowns[1]);
            }
            else // Standard Version
            {
                if (resetCandidates.Count > 1) Shuffle(resetCandidates, rng);
                recasts.DeleteByIndex(RecastType.Ability, resetCandidates[0]);
                // Reset 2 abilities by chance (could be 2 abilities that don't need resets)
                if (resetCandidates.Count > 1 && activeCooldowns.Count > 1 && resetTwoChance >= rng.Next(1, 100))
                    recasts.DeleteByIndex(RecastType.Ability, resetCandidates[1]);
            }

            // Update target's recast state: caster's will be handled in CCharEntity::OnAbility.
            if (target != c && target is CharData targetChar) PushTo(targetChar, AbilRecast(targetChar));
            return true;
        }

        // xirand::ShuffleInPlace
        private static void Shuffle(List<int> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; --i)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        // addLinkpearl: a linkpearl (a pearlsack for a GM) of an unbroken linkshell into the inventory, equipped as the
        // second shell if asked. Not ported: the C++'s charutils::LoadInventory reload after the equip (the char's items
        // are already current here); an LS2 already worn leaves its shell's online list, as equipLinkshell does
        private bool AddLinkpearl(CharData c, string lsName, bool equip)
        {
            bool gm = c.AccountGmLevel > 0;
            var r = Exec("SELECT linkshellid, color FROM linkshells WHERE name = ? AND broken = 0", BoundValue.Of(lsName));
            if (r == null || !r.Next()) return false;

            var pearl = new Item { ItemId = gm ? LinkshellItem.PearlsackId : LinkshellItem.LinkpearlId, Quantity = 1 };
            LinkshellItem.SetSignature(pearl, lsName);
            LinkshellItem.SetLsid(pearl, r.Get<uint>("linkshellid"));
            LinkshellItem.SetColor(pearl, r.Get<ushort>("color"));
            pearl.Extra[8] = gm ? LinkshellItem.TypePearlsack : LinkshellItem.TypeLinkpearl;

            int? slot;
            using (var tx = new ItemTransaction(this, c))
            {
                slot = tx.GiveItem(Container.Inventory, pearl);
                if (slot == null || !tx.Commit()) return false;
            }

            if (equip && StorageItem(c, Container.Inventory, (byte)slot.Value) is { } inserted)
            {
                if (LinkshellItem.Equipped(c, LinkshellItem.SlotLink2) is { } old) DelOnlineMember(c, old);
                AddOnlineMember(c, inserted, 2);
                c.Equip.RemoveAll(e => e.EquipSlot == LinkshellItem.SlotLink2);
                c.Equip.Add(new EquipEntry { EquipSlot = LinkshellItem.SlotLink2, InvSlot = inserted.Slot, Container = inserted.Location });
                c.Equip.Sort((a, b) => a.EquipSlot.CompareTo(b.EquipSlot));
                PushTo(c, LoginPackets.ItemList(inserted, LinkshellItem.LockFlgLinkshell));
                SaveCharEquip(c);
                PushTo(c, GroupComlink(c, 2));
                PushTo(c, GameOkPackets.ItemAttr(inserted, ItemLockFlg(c, inserted)));
                PushTo(c, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
            }
            return true;
        }

        private void RegisterSupportBindings(Dictionary<string, LuaFunction> m)
        {
            // addLinkpearl(lsname, equip)
            m["addLinkpearl"] = l =>
            {
                if (Pc(l, "addLinkpearl") is not CharData c) return Ret(l, false);
                return Ret(l, AddLinkpearl(c, l.ToStringArg(2) ?? "", l.ToBoolean(3)));
            };
            // forceRezone: a char only
            m["forceRezone"] = l =>
            {
                if (Entity(l) is CharData c) ForceRezone(c);
                return 0;
            };
            // setBattleID / getBattleID: the battle id a battle entity must share to interact (not an NPC's)
            m["setBattleID"] = l =>
            {
                if (Battle(l) is { } b) b.BattleId = (ushort)l.ToIntArg(2);
                return 0;
            };
            m["getBattleID"] = l => Ret(l, Battle(l)?.BattleId ?? 0);
            // getConfrontationEffect: the power of the first confrontation-flagged effect, 0 for none
            m["getConfrontationEffect"] = l => Ret(l, Battle(l) is { } b ? b.StatusEffects.GetConfrontationEffect() : 0);
            // copyConfrontationEffect(targid): the caller's confrontation effects onto the char or mob at that index, if it
            // already has one (as the C++ copies them); its confrontation power, or 0
            m["copyConfrontationEffect"] = l =>
            {
                if (Battle(l) is not { } b) return Ret(l, 0);
                var target = EntityInArea(b, (ushort)l.ToIntArg(2)) as BattleEntity;
                if (target is not (CharData or MobEntity) || target.StatusEffects.GetConfrontationEffect() == 0) return Ret(l, 0);
                b.StatusEffects.CopyConfrontationEffect(target);
                return Ret(l, target.StatusEffects.GetConfrontationEffect());
            };
            // addPartyEffect(id, icon, power, tick, duration, subid, subpower): the effect on each party member (or the char
            // alone); tick and duration in seconds, missing arguments 0
            m["addPartyEffect"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("CLuaBaseEntity::addPartyEffect() - Non-PC called function.");
                    return 0;
                }
                var args = new ushort[7];
                for (int i = 0; i < 7 && i + 2 <= l.Top; i++) args[i] = (ushort)l.ToIntArg(i + 2);
                foreach (var member in PartyOf(c))
                    member.StatusEffects.AddStatusEffect(new StatusEffect(args[0], args[1], args[2], args[3] * 1000L, args[4] * 1000L, args[5], args[6]));
                return 0;
            };
            // hasPartyEffect(id): every party member in the char's zone has it (true without a party)
            m["hasPartyEffect"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("CLuaBaseEntity::hasPartyEffect() - Non-PC called function.");
                    return Ret(l, false);
                }
                ushort id = (ushort)l.ToIntArg(2);
                return Ret(l, c.Party == null || c.Party.Members.All(m2 => !SameArea(m2, c) || m2.StatusEffects.HasStatusEffect(id)));
            };
            // removePartyEffect(id): off every party member in the char's zone. The C++ dereferences a missing party; here
            // that does nothing
            m["removePartyEffect"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("CLuaBaseEntity::removePartyEffect() - Non-PC called function.");
                    return 0;
                }
                ushort id = (ushort)l.ToIntArg(2);
                if (c.Party != null)
                    foreach (var member in c.Party.Members.ToArray())
                        if (SameArea(member, c)) member.StatusEffects.DelStatusEffect(id);
                return 0;
            };
            // reloadParty: charutils::ReloadParty on the next tick
            m["reloadParty"] = l =>
            {
                if (Pc(l, "reloadParty") is CharData c) c.ReloadParty = true;
                return 0;
            };
            // doWildCard(target, roll)
            m["doWildCard"] = l =>
            {
                if (Pc(l, "doWildCard") is not null && l.ToObject(2) is CharData target) DoWildCardToEntity(target, (byte)l.ToIntArg(3));
                return 0;
            };
            // doRandomDeal(target): whether an ability was reset
            m["doRandomDeal"] = l =>
            {
                if (Pc(l, "doRandomDeal") is not CharData c) return Ret(l, false);
                if (l.ToObject(2) is not BattleEntity target)
                {
                    log_($"Invalid entity type passed as target ({c.Name}).");
                    return Ret(l, false);
                }
                return Ret(l, DoRandomDealToEntity(c, target));
            };
            // healingWaltz: one random waltzable effect erased; its id, or 0
            m["healingWaltz"] = l => Ret(l, Battle(l) is { } b ? b.StatusEffects.HealingWaltz(MobRng) : 0);
            // takeSwipeLungeDamage(caster, damage, attackType, damageType): the damage cap, then the damage taken; damage
            // past 0 wears the damage-flagged effects and may break bind (BindBreakCheck)
            m["takeSwipeLungeDamage"] = l =>
            {
                if (Entity(l) is not BattleEntity defender || l.ToObject(2) is not BattleEntity attacker)
                {
                    log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                    return Ret(l, 0);
                }
                int damage = MeleeFor().DamageCap(defender, l.ToIntArg(3));
                BattleTakeDamage(defender, damage, attacker, (byte)l.ToIntArg(4), (byte)l.ToEnumArg(5)); // xi::DamageType
                if (damage > 0)
                {
                    defender.StatusEffects.DelStatusEffectsByFlag(EffectFlag.Damage);
                    BindBreakCheck(defender);
                }
                return Ret(l, damage);
            };
            // addDamageFromMultipliers(damage, physicalAttackType, slot, allowProc): a char's weapon in that slot through
            // the multipliers; anything else keeps the damage
            m["addDamageFromMultipliers"] = l =>
            {
                uint damage = (uint)l.ToIntArg(2);
                if (Entity(l) is not CharData c) return Ret(l, damage);
                int slot = l.ToIntArg(4);
                var weapon = CharStats.WeaponIn(c, slot);
                if (weapon?.Equip == null) return Ret(l, damage);
                return Ret(l, PhysicalDamageMultiplier(c, weapon, damage, (PhysicalAttackType)l.ToEnumArg(3), slot, l.ToBoolean(5), MobRng));
            };
        }
    }
}
