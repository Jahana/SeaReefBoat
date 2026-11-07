using System;
using System.Collections.Generic;
using XI.Common;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // GP_CLI_COMMAND_ITEM_USE (0x037): CItemState, the use of a usable item or of a charged item the char has equipped,
    // on itself, another char, a pet, a mob or a trust (CCharEntity::IsValidTarget; a soultrapper's target passes the
    // base check again). Init claims the stack (ItemUseTransaction), runs the script's onItemCheck and starts
    // the BATTLE2 item start; once the activation time is up the use is interrupted by a move or a lost target, or
    // finishes with onItemUse per target (the item finish BATTLE2) and the stack loses one; the state ends after the
    // animation time. The bindings the item scripts lean on most come along (getHP/getMaxHP/getMP/getMaxMP/getTP,
    // addHP/addMP/addTP) and the CLuaAction the scripts get as onItemUse's last argument.
    // A prevent-action effect refuses the start and interrupts the finish; at the finish paralysis
    // (battleutils::IsParalyzed) and intimidation (IsIntimidated, by the target) stop it too (ActionInterrupts::
    // ItemParalyzed / ItemIntimidated), and while it runs the char's state may not change (CanChangeState).
    // An equipment item used, or a charged one equipped, goes on the item recast list.
    // Not ported: ReportErrorToPlayer; a synth or a trade does not ask whether an item is in use. The ITEM_START / ITEM_USE / ITEM_STATE_EXIT listeners
    // fire where CItemState's do.
    public sealed partial class ZoneHost
    {
        private const string ActionType = "CLuaAction";

        private const ushort MsgBasicTooFarAway = 78, MsgBasicUnableToUseItem = 56, MsgBasicItemUse = 28,
                             MsgBasicItemFailsToActivate = 62, MsgBasicCannotOnThatTarget = 155,
                             MsgBasicCannotUseItems = 445, MsgBasicCannotAttackTarget = 446;

        private const ushort TargetSelf = 0x01, TargetPlayerParty = 0x02, TargetEnemy = 0x04, TargetPlayerAlliance = 0x08,
                             TargetPlayer = 0x10, TargetPlayerDead = 0x20, TargetPet = 0x100;

        private const byte ItemTypeUsable = 5; // item_basic.type (Equipment and Weapon are in Trade.cs)
        private const uint ItemFlagScroll = 0x80;
        private const float ItemUseRange = 12f, ItemAoeRange = 10f;

        // the item_usable row (zeroes for an item without one) and item_basic's type
        internal sealed class UsableInfo
        {
            public byte Type;
            public ushort ValidTargets, Animation;
            public long ActivationMs, AnimationMs;
            public byte MaxCharges, UseDelay;
            public uint ReuseDelay;
            public bool AoE;

            public bool IsEquipment => Type == ItemTypeEquipment || Type == ItemTypeWeapon;
        }

        // CItemState
        internal sealed class ItemUseState
        {
            public Item Item;
            public Container Loc;
            public byte Slot;
            public BattleEntity Target;
            internal UsableInfo Info;
            public ItemTransaction Tx;
            public float StartX, StartZ;
            public long EntryMs, CastMs, AnimMs;
            public bool Completed, Interrupted;
            public BasicPacket ErrorMsg;
        }

        private readonly Dictionary<ushort, UsableInfo> usableInfo_ = new Dictionary<ushort, UsableInfo>();
        private readonly HashSet<string> itemScripts_ = new HashSet<string>(StringComparer.Ordinal);

        private void RegisterItemUseHandler()
        {
            // UniqueNo u32 @4, ItemNum u32 @8, ActIndex u16 @12, PropertyItemIndex u8 @14, Category u32 @16
            Register(0x037, (s, c, p) => Pv(c)
                    .BlockedBy(BlockedState.InEvent | BlockedState.Monstrosity)
                    .MustEqual(c.MoghouseId != 0 ? 1 : 0, 0, "Player is in moghouse")
                    .MustEqual(U32(p, 8), 0, "ItemNum not 0")
                    .OneOf("Category", U32(p, 16), ItemUseContainers),
                OnItemUse);
        }

        private static readonly long[] ItemUseContainers =
        {
            (long)Container.Inventory, (long)Container.TempItems, (long)Container.Wardrobe, (long)Container.Wardrobe2,
            (long)Container.Wardrobe3, (long)Container.Wardrobe4, (long)Container.Wardrobe5, (long)Container.Wardrobe6,
            (long)Container.Wardrobe7, (long)Container.Wardrobe8,
        };

        private void OnItemUse(MapSession s, CharData c, BasicPacket p)
        {
            // PChar->GetEntity(ActIndex): a battle entity (a trust too), else an NPC
            ushort targid = U16(p, 12);
            var target = (object)AbilityEntityByTargId(c, targid) ?? AreaOf(c).Trusts.Find(t => t.TargId == targid) ?? EntityByTargId(c, targid);
            if (target == null) return;

            float dx = c.X - TargetX(target), dy = c.Y - TargetY(target), dz = c.Z - TargetZ(target);
            if (dx * dx + dy * dy + dz * dz > ItemUseRange * ItemUseRange)
            {
                var t = Speaker(target);
                Push(s, SelfPackets.BattleMessage(c.Id, c.TargId, t.Id, t.TargId, 0, 0, MsgBasicTooFarAway));
                return;
            }

            var loc = (Container)U32(p, 16);
            byte slot = p.Buffer[14];
            var item = StorageItem(c, loc, slot);
            if (item == null) return;

            var info = Usable(item.ItemId);
            if (IsBusy(c, item) && !(info?.IsEquipment == true && CharStats.IsEquipped(c, item)))
            {
                log_($"{c.Name} trying to use a claimed item");
                return;
            }

            // PAI->UseItem: nothing without the player controller (jailed)
            if (c.NoController) return;
            if (c.UContainer.Type != UContainerType.UseItem) StartItemUse(s, c, target, loc, slot);
            else Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgBasicUnableToUseItem));
        }

        private static float TargetX(object e) => e is BattleEntity o ? o.X : ((NpcEntity)e).X;
        private static float TargetY(object e) => e is BattleEntity o ? o.Y : ((NpcEntity)e).Y;
        private static float TargetZ(object e) => e is BattleEntity o ? o.Z : ((NpcEntity)e).Z;

        private UsableInfo Usable(ushort id)
        {
            if (usableInfo_.TryGetValue(id, out var info)) return info;
            var r = Exec("SELECT b.type, u.validTargets, u.activation, u.animation, u.animationTime, u.maxCharges, u.useDelay, " +
                         "u.reuseDelay, u.aoe FROM item_basic b LEFT JOIN item_usable u USING (itemid) WHERE b.itemid = ? LIMIT 1",
                         BoundValue.Of(id));
            if (r != null && r.Next())
            {
                bool usable = !r.IsNull("validTargets");
                info = new UsableInfo
                {
                    Type = r.Get<byte>("type"),
                    ValidTargets = usable ? r.Get<ushort>("validTargets") : (ushort)0,
                    ActivationMs = usable ? (long)(r.Get<float>("activation") * 1000) : 0,
                    Animation = usable ? r.Get<ushort>("animation") : (ushort)0,
                    AnimationMs = usable ? r.Get<byte>("animationTime") * 1000L : 0,
                    MaxCharges = usable ? r.Get<byte>("maxCharges") : (byte)0,
                    UseDelay = usable ? r.Get<byte>("useDelay") : (byte)0,
                    ReuseDelay = usable ? r.Get<uint>("reuseDelay") : 0,
                    AoE = usable && r.Get<byte>("aoe") != 0,
                };
            }
            usableInfo_[id] = info;
            return info;
        }

        // the item recast id: slot << 8 | container
        private static ushort ItemRecastId(Item item) => (ushort)(item.Slot << 8 | (byte)item.Location);

        // charutils::EquipItem / UnequipItem for a charged item: equipped with charges left it goes on the item recast
        // list for its reuse time, with its ITEM_ATTR; taken off it leaves the list
        private void ChargedItemRecast(CharData c, Item item, bool equipped)
        {
            if (!equipped)
            {
                RecastsOf(c).Del(RecastType.Item, ItemRecastId(item));
                return;
            }
            if (Usable(item.ItemId) is not { } info) return;
            RecastsOf(c).Add(RecastType.Item, ItemRecastId(item), ReuseSeconds(item, info) * 1000);
            PushTo(c, GameOkPackets.ItemAttr(item, GameOkPackets.LockFlagFor(c, item)));
        }

        // GP_SERV_COMMAND_ITEM_ATTR's Attr overrides: an ITEM_CHARGED item's charge bits, then an unlockable weapon
        // (CItemWeapon::isUnlockable, the item's unlock total above 0) sends 0 for its Attr[0] and Attr[1], the weapon
        // skill points it holds so far
        private void ItemAttrOverride(Item item, byte[] attr)
        {
            ChargedItemAttr(item, attr);
            if (loader_?.Stats?.Items.Equipment(item.ItemId)?.Weapon is { } weapon && weapon.IsUnlockable(item))
            {
                attr[0] = 0;
                attr[1] = 0;
            }
        }

        // GP_SERV_COMMAND_ITEM_ATTR for an ITEM_CHARGED item: the flag, the charges bits (0x80 always, 0x10 partial, 0x20 empty,
        // 0x40 ready to use) and, while it recharges, the next use and the end of the use delay
        private void ChargedItemAttr(Item item, byte[] attr)
        {
            if (loader_?.Stats?.Items.Equipment(item.ItemId) is not { Charged: true } eq || Usable(item.ItemId) is not { } info) return;
            byte flags = 0x80;
            attr[0] = 0x01;
            if (item.Extra[1] < eq.MaxCharges) flags |= 0x10;
            if (item.Extra[1] > 0)
            {
                long reuse = ReuseSeconds(item, info);
                if (reuse == 0)
                {
                    flags |= 0x40;
                }
                else
                {
                    long now = UnixNow();
                    BitConverter.TryWriteBytes(attr.AsSpan(4, 4), LoginPackets.VanadielTimestamp(now + reuse));
                    BitConverter.TryWriteBytes(attr.AsSpan(8, 4), info.UseDelay + LoginPackets.VanadielTimestamp(now));
                }
            }
            else
            {
                flags |= 0x20;
            }
            attr[3] = flags;
        }

        // CItemUsable::getReuseTime in seconds: the later of the use delay after equipping and the reuse delay after
        // the last use (exdata TimeValue1, a Vana'diel timestamp)
        private long ReuseSeconds(Item item, UsableInfo info)
        {
            uint lastUse = BitConverter.ToUInt32(item.Extra, 4);
            long lastUseUnix = lastUse == 0 ? 0 : VanaTime.EpochUnixSec + lastUse;
            long reuse = Math.Max(item.AssignUnixSec + info.UseDelay, lastUseUnix + info.ReuseDelay);
            long now = UnixNow();
            return reuse > now ? reuse - now : 0;
        }

        // CAIContainer::UseItem -> CItemState::init
        private void StartItemUse(MapSession s, CharData c, object target, Container loc, byte slot)
        {
            // CPlayerController::UseItem: canAct and CanChangeState
            if (!CanAct(c) || c.Hp <= 0 || c.Synth != null || c.ItemUse != null || c.WsState != null || c.SkillState != null || c.RangedState != null ||
                c.AbilityState is { Completed: false } || c.MagicState != null) return;

            var item = StorageItem(c, loc, slot);
            var info = item == null ? null : Usable(item.ItemId);
            if (info != null && (info.Type == ItemTypeUsable || info.IsEquipment))
            {
                if (info.IsEquipment)
                {
                    if (!CharStats.IsEquipped(c, item) || item.Extra[1] == 0 || ReuseSeconds(item, info) != 0) item = null;
                }
                else if (IsBusy(c, item))
                {
                    item = null;
                }
            }
            else
            {
                item = null;
            }
            if (item == null)
            {
                Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgBasicUnableToUseItem));
                return;
            }

            var state = new ItemUseState { Item = item, Loc = loc, Slot = slot, Info = info, Target = target as BattleEntity };
            var PTarget = ItemValidTarget(c, state.Target, item.ItemId, info.ValidTargets, out var errMsg);
            if (PTarget == null || errMsg != null)
            {
                if (errMsg != null) Push(s, errMsg);
                return;
            }

            var (error, param, value) = OnItemCheck(PTarget, item, c);
            // a prevent-action effect (sleep, stun, petrification, terror...) refuses it too, with the script's message
            if (error != 0 || c.StatusEffects.HasPreventActionEffect())
            {
                if (error == -1) return;
                if (value == 0)
                {
                    var basic = LookupItem(item.ItemId);
                    param = basic != null && (basic.Flags & ItemFlagScroll) != 0 ? basic.SubId : item.ItemId;
                }
                Push(s, SelfPackets.BattleMessage(c.Id, c.TargId, PTarget.Id, PTarget.TargId, param, value, (ushort)error));
                return;
            }

            c.UContainer.SetType(UContainerType.UseItem);
            if (!info.IsEquipment)
            {
                state.Tx = new ItemTransaction(this, c);
                if (state.Tx.ClaimSlot(loc, slot) == null)
                {
                    state.Tx.Dispose();
                    c.UContainer.Clean();
                    Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgBasicUnableToUseItem));
                    return;
                }
            }

            state.StartX = c.X;
            state.StartZ = c.Z;
            state.EntryMs = NowMs();
            state.CastMs = info.ActivationMs;
            state.AnimMs = info.AnimationMs;
            c.ItemUse = state;

            var action = new ActionPacket { ActorId = c.Id, ActionType = ActionCategory.ItemStart, ActionId = (uint)FourCC.ItemUse };
            var result = action.AddTarget(PTarget.Id).AddResult();
            result.Param = item.ItemId;
            result.MessageId = MsgBasicItemUse;
            TriggerListener(c, "ITEM_START", PTarget, item, action);
            lua_?.Forget(action);
            PushCharInRange(c, Battle2Packets.Battle2(action), true);
            Push(s, LoginPackets.ItemList(item, LockNoSelect));
            Push(s, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
        }

        // CItem::isSoultrapper
        private static bool IsSoultrapper(ushort itemId) => itemId == 18721 || itemId == 18724;

        // CItemState::validatedTarget: CCharEntity::IsValidTarget (applyTargetRestrictions), the target when the char
        // may use the item on it, else null with the message to refuse with. A soultrapper's target passes the base
        // CBattleEntity::IsValidTarget again, its message cleared.
        private BattleEntity ItemValidTarget(CharData c, BattleEntity target, ushort itemId, ushort flags, out BasicPacket errMsg)
        {
            if (target != null && !EntityExists(target)) target = null;
            var valid = AbilityValidTarget(c, target, flags, out errMsg);
            if (valid != null && IsSoultrapper(itemId))
            {
                errMsg = null;
                valid = GetValidTarget(c, target, flags);
            }
            return valid;
        }

        private bool InAllianceOf(CharData of, CharData c)
        {
            foreach (var m in ForAlliance(of))
                if (m.Id == c.Id) return true;
            return false;
        }

        // CCharEntity::ValidTarget for a char initiator: never across confrontations; belligerents only harm each other;
        // a char of the same allegiance is helped only by its own kind (Monipulators, adventurers)
        private static bool CharValidTarget(CharData initiator, CharData target, ushort flags)
        {
            if (target.StatusEffects.GetConfrontationEffect() != initiator.StatusEffects.GetConfrontationEffect()) return false;
            if (Monstrosity.AreBelligerents(initiator, target)) return (flags & TargetEnemy) != 0 && target.Hp > 0;
            if (target.Allegiance == initiator.Allegiance && !Monstrosity.CanAssist(initiator, target)) return false;
            if (target.Hp <= 0) return (flags & TargetPlayerDead) != 0;
            if ((flags & TargetPlayer) != 0 && target.Allegiance == initiator.Allegiance) return true;
            if (BattleValidTarget(initiator.Allegiance, target.Allegiance, flags, target == initiator)) return true;

            bool different = target != initiator;
            bool sameParty = target.Party != null && initiator.Party != null && target.Party == initiator.Party;
            bool sameAlliance = target.Party?.Alliance != null && initiator.Party?.Alliance != null &&
                                target.Party.Alliance == initiator.Party.Alliance;
            if ((flags & TargetPlayerAlliance) != 0 && sameAlliance && different) return true;
            if ((flags & TargetPlayerParty) != 0 && sameParty && different) return true;
            return false;
        }

        // CBattleEntity::ValidTarget for a living target: an enemy across teams (Wyverns, Griffons), across nations (San
        // d'Oria, Bastok, Windurst) or between a mob and a player; without TARGET_ENEMY, only the initiator itself
        private const byte AllegianceSanDoria = 2, AllegianceWindurst = 4, AllegianceWyverns = 5;

        private static bool BattleValidTarget(byte initiator, byte target, ushort flags, bool isSelf)
        {
            if ((flags & TargetEnemy) != 0)
            {
                if (target >= AllegianceWyverns && initiator >= AllegianceWyverns) return target != initiator;
                if (target >= AllegianceSanDoria && target <= AllegianceWindurst && initiator >= AllegianceSanDoria && initiator <= AllegianceWindurst)
                    return target != initiator;
                if (target <= CharData.AllegiancePlayer && initiator <= CharData.AllegiancePlayer) return target != initiator;
                return false;
            }
            return (flags & TargetSelf) != 0 && isSelf;
        }

        private void TickItemUses()
        {
            foreach (var (c, s) in new List<KeyValuePair<CharData, MapSession>>(charSessions_))
                if (c.ItemUse != null && UpdateItemUse(s, c, c.ItemUse))
                    EndItemUse(s, c);
        }

        // CItemState::Update: true once the state is done
        private bool UpdateItemUse(MapSession s, CharData c, ItemUseState st)
        {
            long now = NowMs();
            if (now > st.EntryMs + st.CastMs && !st.Completed)
            {
                st.Interrupted = false;
                TryInterruptItem(c, st);
                var action = new ActionPacket();
                if (st.Interrupted)
                {
                    if (st.ErrorMsg != null) Push(s, new BasicPacket(st.ErrorMsg.Buffer));
                    TriggerListener(c, "ITEM_USE", c, st.Item, action);
                }
                else
                {
                    TriggerListener(c, "ITEM_USE", c, st.Item, action);
                    if (FinishItem(s, c, st, action)) st.Item = null;
                    if (action.Targets.Count != 0)
                    {
                        ProcessActionEffectFlags(c, action);
                        PushCharInRange(c, Battle2Packets.Battle2(action), true);
                    }
                }
                lua_?.Forget(action);
                st.Completed = true;
            }
            else if (st.Completed && now > st.EntryMs + st.CastMs + st.AnimMs)
            {
                c.History[HistoryItemsUsed]++;
                TriggerListener(c, "ITEM_STATE_EXIT", c, st.Item);
                return true;
            }
            return false;
        }

        // CItemState::TryInterrupt
        private void TryInterruptItem(CharData c, ItemUseState st)
        {
            if (st.Item == null) return;
            var target = ItemValidTarget(c, st.Target, st.Item.ItemId, st.Info.ValidTargets, out var errMsg);
            if (st.ErrorMsg == null) st.ErrorMsg = errMsg;
            ushort msg = MsgBasicCannotUseItems;
            if (ItemUserMoved(c, st) || c.StatusEffects.HasPreventActionEffect())
            {
                PushCharInRange(c, Battle2Packets.ItemInterrupt(c), true);
                msg = MsgBasicItemFailsToActivate;
                st.Interrupted = true;
            }
            else if (IsParalyzed(c))
            {
                // the action packets tell it
                ItemStopped(c, st.Target ?? c, MsgIsParalyzed2);
                msg = 0;
                st.Interrupted = true;
            }
            else if (target == null)
            {
                st.Interrupted = true;
            }
            else if (IsIntimidated(c, target))
            {
                ItemStopped(c, st.Target, MsgIsIntimidated);
                msg = 0;
                st.Interrupted = true;
            }
            if (st.Interrupted && st.ErrorMsg == null && msg != 0)
                st.ErrorMsg = SelfPackets.BattleMessage(c, st.Item.ItemId, 0, msg);
        }

        // ActionInterrupts::ItemParalyzed / ItemIntimidated: a magic finish on the target with the message, then the
        // item start's cancel animation
        private void ItemStopped(CharData c, BattleEntity target, ushort msg)
        {
            var finish = new ActionPacket { ActorId = c.Id, ActionType = ActionCategory.MagicFinish };
            var r = finish.AddTarget(target.Id).AddResult();
            r.Animation = AnimSkillInterrupt;
            r.MessageId = msg;
            PushCharInRange(c, Battle2Packets.Battle2(finish), true);
            var stop = new ActionPacket { ActorId = c.Id, ActionType = ActionCategory.ItemStart, ActionId = (uint)FourCC.ItemInterrupt };
            stop.AddTarget(c.Id).AddResult().Animation = AnimSkillInterrupt;
            PushCharInRange(c, Battle2Packets.Battle2(stop), true);
        }

        // CItemState::HasMoved: to a tenth of a yalm
        private static bool ItemUserMoved(CharData c, ItemUseState st) =>
            MathF.Floor(st.StartX * 10 + 0.5f) / 10 != MathF.Floor(c.X * 10 + 0.5f) / 10 ||
            MathF.Floor(st.StartZ * 10 + 0.5f) / 10 != MathF.Floor(c.Z * 10 + 0.5f) / 10;

        // CItemState::FinishItem: true when the use took the stack's last one
        private bool FinishItem(MapSession s, CharData c, ItemUseState st, ActionPacket action)
        {
            if (!OnItemFinish(s, c, st, action) || st.Tx == null) return false;
            bool willBeDestroyed = st.Item != null && st.Item.Quantity == 1;
            // ItemUseTransaction::doCommit: the one used comes off the stack
            if (!st.Tx.Take(st.Loc, st.Slot, 1))
                log_($"ItemUseTransaction: {c.Name} kept the item in slot {st.Slot} after using it");
            st.Tx.Commit();
            return willBeDestroyed;
        }

        // CCharEntity::OnItemFinish: true when a consumable is to be used up
        private bool OnItemFinish(MapSession s, CharData c, ItemUseState st, ActionPacket action)
        {
            var item = st.Item;
            var info = st.Info;
            if (!info.IsEquipment && item.Quantity < 1)
            {
                log_($"OnItemFinish: {c.Name} attempted to use reserved/insufficient {LookupItem(item.ItemId)?.Name} ({item.ItemId}).");
                Push(s, SelfPackets.BattleMessage(c, item.ItemId, 0, MsgBasicItemFailsToActivate));
                return false;
            }

            // findSingleTarget: the target must still be there, alive unless the item raises
            byte findFlags = (info.ValidTargets & TargetPlayerDead) == TargetPlayerDead ? FindFlagsDead : (byte)0;
            var target = st.Target != null && EntityExists(st.Target) ? st.Target : null;
            if (target == null) return false;
            var find = new SkillTargetFind(this, c);
            find.SingleTarget(target, info.ValidTargets, findFlags);
            if (find.Targets.Count == 0) return false;

            action.ActorId = c.Id;
            action.ActionType = ActionCategory.ItemFinish;
            action.ActionId = item.ItemId;

            var targets = new List<BattleEntity>();
            if (info.AoE)
            {
                // findWithinArea(ATTACKER, 10)
                find = new SkillTargetFind(this, c);
                find.WithinArea(c, AoeAttacker, ItemAoeRange, info.ValidTargets, findFlags);
                targets.AddRange(find.Targets);
            }
            else
            {
                targets.Add(target);
            }

            foreach (var t in targets)
            {
                var result = action.AddTarget(t.Id).AddResult();
                result.Resolution = 0; // hit
                result.Animation = info.Animation;
                result.Param = OnItemUseScript(c, t, item, action);
            }
            foreach (var t in targets)
                if (t is CharData tc && SessionOf(tc) is MapSession ts) SendUpdateMask(ts, tc);
            if (c.UpdateMask != 0) SendUpdateMask(s, c);

            if (info.IsEquipment)
            {
                if (info.MaxCharges > 1 && item.Extra[1] > 0) item.Extra[1]--;
                BitConverter.TryWriteBytes(item.Extra.AsSpan(4, 4), (uint)(UnixNow() - VanaTime.EpochUnixSec));
                Exec("UPDATE char_inventory SET extra = ? WHERE charid = ? AND location = ? AND slot = ? LIMIT 1",
                     BoundValue.Of(item.Extra), BoundValue.Of(c.Id), BoundValue.Of((byte)item.Location), BoundValue.Of(item.Slot));
                // the item recast list, from any bag, while charges are left
                if (item.Extra[1] != 0) RecastsOf(c).Add(RecastType.Item, ItemRecastId(item), ReuseSeconds(item, info) * 1000);
                return false;
            }
            return true;
        }

        // the state stack lets go of CItemState: CItemState::Cleanup
        private void EndItemUse(MapSession s, CharData c)
        {
            var st = c.ItemUse;
            if (st == null) return;
            c.ItemUse = null;

            if (!st.Completed && !st.Interrupted && st.Item != null)
            {
                PushCharInRange(c, Battle2Packets.ItemInterrupt(c), true);
                Push(s, SelfPackets.BattleMessage(c, st.Item.ItemId, 0, MsgBasicItemFailsToActivate));
                st.Interrupted = true;
            }
            if (c.UContainer.Type == UContainerType.UseItem) c.UContainer.Clean();
            st.Tx?.Dispose();
            st.Tx = null;

            var now = StorageItem(c, st.Loc, st.Slot);
            if (now != null && now == st.Item) Push(s, LoginPackets.ItemList(st.Item, LockNormal));
            else st.Item = null;
            Push(s, st.Item != null ? GameOkPackets.ItemAttr(st.Item, ItemLockFlg(c, st.Item)) : GameOkPackets.EmptySlotAttr(st.Loc, st.Slot));
            Push(s, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
        }

        // a zone change or a logout clears the state stack
        private void CancelItemUse(MapSession s, CharData c)
        {
            if (c.ItemUse != null) EndItemUse(s, c);
        }

        // ---- luautils ---------------------------------------------------------------------------------------------

        // luautils::OnItemEquip / OnItemUnequip: the item script's hook with the wearer and the item, for an equip that
        // went through and for every unequip
        private void OnItemEquipScript(CharData c, Item item, bool equip)
        {
            if (lua_ == null) return;
            var file = ItemScript(item);
            if (file == null) return;
            string fn = equip ? "onItemEquip" : "onItemUnequip";
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(file, fn);
                if (lua_.IsNil(-1)) return;
                lua_.PushObject(c);
                lua_.PushObject(item, ItemType);
                lua_.Call(2, 0);
            }
            catch (LuaException e)
            {
                ScriptError(fn, e);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnItemDrop: an item gone from the char (a stack used up, thrown away, evicted from or emptied out of
        // the recycle bin), the flag for one that went into the bin
        private void OnItemDropScript(CharData c, Item item, bool recycleBin = false)
        {
            if (lua_ == null) return;
            var file = ItemScript(item);
            if (file == null) return;
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(file, "onItemDrop");
                if (lua_.IsNil(-1)) return;
                lua_.PushObject(c);
                lua_.PushObject(item, ItemType);
                lua_.Push(recycleBin);
                lua_.Call(3, 0);
            }
            catch (LuaException e)
            {
                ScriptError("onItemDrop", e);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnFurniturePlaced / OnFurnitureRemoved: xi.items[name]'s hook with the char
        private void OnFurnitureScript(CharData c, Item item, bool placed)
        {
            if (lua_ == null) return;
            var file = ItemScript(item);
            if (file == null) return;
            string fn = placed ? "onFurniturePlaced" : "onFurnitureRemoved";
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(file, fn);
                if (lua_.IsNil(-1)) return;
                lua_.PushObject(c);
                lua_.Call(1, 0);
            }
            catch (LuaException e)
            {
                ScriptError(fn, e);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        private string ItemScript(Item item)
        {
            var name = LookupItem(item.ItemId)?.Name;
            if (name == null) return null;
            string file = $"./scripts/items/{name}.lua";
            if (itemScripts_.Add(file)) scripts_.LoadLuaObjectFromFile(file);
            return file;
        }

        // luautils::OnItemCheck: (message, param, value); 56 when the script has no onItemCheck or fails
        private (int Error, int Param, int Value) OnItemCheck(BattleEntity target, Item item, CharData caster)
        {
            if (lua_ == null) return (MsgBasicUnableToUseItem, 0, 0);
            var file = ItemScript(item);
            if (file == null) return (MsgBasicUnableToUseItem, 0, 0);

            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(file, "onItemCheck");
                if (lua_.IsNil(-1)) return (MsgBasicUnableToUseItem, 0, 0);
                lua_.PushObject(target);
                lua_.PushObject(item, ItemType);
                lua_.PushObject(caster);
                lua_.Call(3, 3);
                int Num(int idx) => lua_.IsNumber(idx) ? (int)lua_.ToIntegral(idx) : 0;
                return (Num(top + 1), Num(top + 2), Num(top + 3));
            }
            catch (LuaException e)
            {
                ScriptError("onItemCheck", e);
                return (MsgBasicUnableToUseItem, 0, 0);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnItemUse: the number the script returns, else 0
        private int OnItemUseScript(CharData user, BattleEntity target, Item item, ActionPacket action)
        {
            if (lua_ == null) return 0;
            var file = ItemScript(item);
            if (file == null) return 0;

            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(file, "onItemUse");
                if (lua_.IsNil(-1)) return 0;
                lua_.PushObject(target);
                lua_.PushObject(user);
                lua_.PushObject(item, ItemType);
                lua_.PushObject(action, ActionType);
                lua_.Call(4, 1);
                return lua_.IsNumber(-1) ? (int)lua_.ToIntegral(-1) : 0;
            }
            catch (LuaException e)
            {
                ScriptError("onItemUse", e);
                return 0;
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // ---- bindings ---------------------------------------------------------------------------------------------

        private void RegisterItemUseBindings(Dictionary<string, LuaFunction> m)
        {
            m["getHP"] = l => Ret(l, Entity(l) is CharData c ? c.Hp : 0);
            m["getMaxHP"] = l => Ret(l, Entity(l) is CharData c ? c.MaxHp : 0);
            m["getMP"] = l => Ret(l, Entity(l) is CharData c ? c.Mp : 0);
            m["getMaxMP"] = l => Ret(l, Entity(l) is CharData c ? c.MaxMp : 0);
            m["getTP"] = l => Ret(l, Entity(l) is CharData c ? (float)c.Tp : 0);
            m["addHP"] = l => Ret(l, Pc(l, "addHP") is CharData c ? AddHp(c, l.ToIntArg(2)) : 0);
            m["addMP"] = l => Ret(l, Pc(l, "addMP") is CharData c ? AddMp(c, l.ToIntArg(2)) : 0);
            m["addTP"] = l =>
            {
                if (Battle(l) is not { } b) return Ret(l, 0);
                short amount = (short)l.ToIntArg(2);
                if (b is CharData c) return Ret(l, AddTp(c, amount));
                var mob = (MobEntity)b;
                int before = mob.Tp;
                AddMobTp(mob, amount);
                return Ret(l, Math.Abs(before - mob.Tp));
            };

            // CLuaAction: the action_t an item use builds, which the script may retarget or re-message
            static ActionPacket Self(LuaState l) =>
                l.ToObject(1) as ActionPacket ?? throw new InvalidOperationException("bad self: expected " + ActionType);
            static ActionResult Result(LuaState l)
            {
                uint id = l.ToUIntArg(2);
                foreach (var t in Self(l).Targets)
                    if (t.ActorId == id && t.Results.Count != 0) return t.Results[0];
                return null;
            }
            var action = new Dictionary<string, LuaFunction>
            {
                ["ID"] = l =>
                {
                    uint id = l.ToUIntArg(2), newId = l.ToUIntArg(3);
                    foreach (var t in Self(l).Targets)
                        if (t.ActorId == id)
                        {
                            t.ActorId = newId;
                            break;
                        }
                    return 0;
                },
                ["actionID"] = l =>
                {
                    if (l.IsNumber(2))
                    {
                        Self(l).ActionId = l.ToUIntArg(2);
                        return 0;
                    }
                    return Ret(l, Self(l).ActionId);
                },
                ["getParam"] = l => Ret(l, Result(l)?.Param ?? 0),
                ["param"] = l =>
                {
                    if (Result(l) is ActionResult r) r.Param = l.ToIntArg(3);
                    return 0;
                },
                ["getMsg"] = l => Ret(l, Result(l)?.MessageId ?? 0),
                ["messageID"] = l =>
                {
                    if (Result(l) is ActionResult r) r.MessageId = (ushort)l.ToEnumArg(3); // MsgBasic
                    return 0;
                },
                ["animation"] = l =>
                {
                    if (Result(l) is ActionResult r) r.Animation = (ushort)l.ToIntArg(3);
                    return 0;
                },
            };
            AddActionBindings(action);
            lua_.RegisterType(ActionType, action);
            RegisterAttackType();
            RegisterLootType();
        }

        // CCharEntity::addHP: CBattleEntity::addHP (a loss stands the char up), then the HP latents
        private int AddHp(CharData c, int hp)
        {
            if (c.Hp == 0 && hp < 0)
            {
                c.Latents.CheckLatentsHP();
                return 0;
            }
            int cap = Math.Clamp(c.Hp + hp, 0, c.MaxHp);
            hp = c.Hp - cap;
            c.Hp = cap;
            if (hp > 0) StandUp(c);
            if (hp != 0) c.UpdateMask |= CharStats.UpdateHp;
            if (c.Hp == 0 && c.Unkillable) c.Hp = 1;
            c.Latents.CheckLatentsHP();
            return Math.Abs(hp);
        }

        // battleutils::MakeEntityStandUp
        private void StandUp(CharData c)
        {
            if (c.Animation == Anim.Healing)
            {
                c.StatusEffects.DelStatusEffect(EffectId.Healing);
                c.UpdateMask |= CharStats.UpdateHp;
            }
            else if (c.Animation == Anim.Sit || (c.Animation >= Anim.Sitchair0 && c.Animation <= Anim.Sitchair10))
            {
                c.Animation = Anim.None;
                c.UpdateMask |= CharStats.UpdateHp;
            }
        }

        // CCharEntity::addMP: CBattleEntity::addMP, then the MP latents
        private static int AddMp(CharData c, int mp)
        {
            int cap = Math.Clamp(c.Mp + mp, 0, c.MaxMp);
            mp = c.Mp - cap;
            c.Mp = cap;
            if (mp != 0) c.UpdateMask |= CharStats.UpdateHp;
            c.Latents.CheckLatentsMP();
            return Math.Abs(mp);
        }

        // CCharEntity::addTP: CBattleEntity::addTP (a gain is cut by Inhibit TP and scaled by map.PLAYER_TP_MULTIPLIER),
        // then the TP latents
        private int AddTp(CharData c, short tp)
        {
            if (tp > 0)
            {
                float reduce = c.GetMod(Mod.INHIBIT_TP) / 100.0f;
                tp = (short)(tp - tp * reduce);
                tp = (short)(tp * ChatCfg.PlayerTpMultiplier);
            }
            if (tp != 0) c.UpdateMask |= CharStats.UpdateHp;
            int cap = Math.Clamp(c.Tp + tp, 0, 3000);
            int delta = c.Tp - cap;
            c.Tp = cap;
            c.Latents.CheckLatentsTP();
            return Math.Abs(delta);
        }
    }
}
