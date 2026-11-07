using System;
using System.Collections.Generic;
using System.Linq;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // Mounts and chocobo raising: GP_CLI_COMMAND_ACTION's Mount (the key item for the mount, the level, the zone, the
    // 60 s recast, no hostile mob; the Mounted effect, its script setting the animation, then
    // xi.player.onPlayerMount), Dismount and ChocoboDig (a Gysahl Green taken for xi.chocoboDig.start, given back when
    // the dig is refused or the chocobo leaves it, then GP_SERV_COMMAND_DIG to those near); the char's own chocobo
    // (m_chocoboUserData, char_pet.chocobo_user_data) behind registerChocobo / getFieldChocobo / getChocoboUserData /
    // setChocoboUserData, and the raised chocobo (char_chocobos) behind getChocoboRaisingInfo / setChocoboRaisingInfo /
    // deleteRaisedChocobo. The mount's packet fields and speed are MountUtils'.
    public sealed partial class ZoneHost
    {
        private const ushort KeyItemChocoboCompanion = 3072, KeyItemGladiatorialWritOfSummons = 2351, KeyItemLastMount = 3108;
        private const ushort ItemGysahlGreens = 4545;
        private const ushort MsgCannotPerformActionOnTarget = 71, MsgYourMountRefuses = 339, MsgMountRequiredLevel = 773, MsgCannotMount = 777;
        private const ushort MsgStdYouDontHaveAny = 39;
        private const long MountRecastMs = 60_000;

        // hasEnmityEXPENSIVE: the notoriety container has enmity, or a living mob of another allegiance targets it
        private bool HasEnmityExpensive(BattleEntity b)
        {
            if (b.Notoriety.HasEnmity()) return true;
            foreach (var mob in AreaOf(b).MobById.Values)
                if (mob.IsAlive && mob.BattleTarget == b && mob.Allegiance != EnmityContainer.AllegianceOf(b)) return true;
            return false;
        }

        // GP_CLI_COMMAND_ACTION Mount: the mount the client asks for is the key item that many past Chocobo Companion
        private void OnMount(MapSession s, CharData c, uint requested)
        {
            if (requested > KeyItemLastMount - KeyItemChocoboCompanion) return;
            int mountKeyItem = KeyItemChocoboCompanion + (int)requested;

            if (c.Animation != Anim.None || c.StatusEffects.HasPreventActionEffect())
                Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgCannotPerformActionOnTarget));
            // JP wiki: a gladiator holding the writ cannot ride
            else if (c.HasKeyItem(KeyItemGladiatorialWritOfSummons)) Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgCannotMount));
            else if (!AreaOf(c).CanUseMisc("mount")) Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgCannotUseInArea));
            else if (c.MLvl < 20) Push(s, SelfPackets.BattleMessage(c, 20, 0, MsgMountRequiredLevel));
            else if (c.HasKeyItem(mountKeyItem))
            {
                var recasts = RecastsOf(c);
                if (recasts.HasRecast(RecastType.Ability, Recast.Mount, MountRecastMs))
                {
                    Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgWaitLonger));
                    return;
                }
                if (HasEnmityExpensive(c))
                {
                    Push(s, SelfPackets.BattleMessage(c, 0, 0, MsgYourMountRefuses));
                    return;
                }

                // a registered chocobo stays out for its minutes
                byte minutes = MountUtils.Minutes(MountUtils.FieldChocobo(c));
                long durationMs = requested == 0 && minutes > 0 ? minutes * 60_000L : 30 * 60_000L;
                uint mountId = requested != 0 ? requested + 1 : 0;

                c.MountId = (byte)mountId;
                c.StatusEffects.AddStatusEffect(new StatusEffect(EffectId.Mounted, EffectId.Mounted, (ushort)mountId, 0, durationMs, 0,
                                                                 MountUtils.PersonalChocoboFlag), EffectNotice.Silent);
                recasts.Add(RecastType.Ability, Recast.Mount, MountRecastMs);
                PushTo(c, AbilRecast(c));
                CallPlayerGlobal("xi.player.onPlayerMount", c);
            }
        }

        private static void OnDismount(CharData c)
        {
            c.Animation = Anim.None;
            c.UpdateMask |= CharStats.UpdateHp;
            c.StatusEffects.DelStatusEffectSilent(EffectId.Mounted);
        }

        // GP_CLI_COMMAND_ACTION ChocoboDig: only a rented or personal chocobo digs, for a Gysahl Green
        private void OnChocoboDig(MapSession s, CharData c)
        {
            if (c.MountId != MountUtils.MountChocobo) return;
            var greens = SearchItem(c, Container.Inventory, ItemGysahlGreens);
            if (greens == null)
            {
                Push(s, SystemMessage(MsgStdYouDontHaveAny, ItemGysahlGreens));
                return;
            }
            if (IsBusy(c, greens))
            {
                log_($"GP_CLI_COMMAND_ACTION: {c.Name} trying to use invalid gysahl greens (locked/reserved)");
                Push(s, SystemMessage(MsgStdYouDontHaveAny, ItemGysahlGreens));
                return;
            }

            // kept greens can only go back to their own stack or to a free slot
            bool greensStayInSlot = greens.Quantity > 1;
            byte slot = greens.Slot;
            using var t = new ItemTransaction(this, c);
            if (t.ClaimSlot(Container.Inventory, slot) == null || !t.Take(Container.Inventory, slot, 1))
            {
                log_($"GP_CLI_COMMAND_ACTION: {c.Name} could not spend gysahl greens in slot {slot}");
                Push(s, SystemMessage(MsgStdYouDontHaveAny, ItemGysahlGreens));
                return;
            }

            // a refused dig, or one whose chocobo keeps the greens, rolls them back
            var (dug, keepGreens) = OnChocoboDigLua(c);
            if (!dug) return;
            bool returnsGreens = keepGreens && (greensStayInSlot || FreeSlots(c, Container.Inventory) > 0);
            if (!returnsGreens && !t.Commit()) return;
            if (returnsGreens) t.Dispose();

            PushTo(c, GameOkPackets.ItemSame(1, CharData.MaxContainers, c.SyncedContainers));
            PushCharInRange(c, DigPacket(c), true);
        }

        // luautils::OnChocoboDig: xi.chocoboDig.start(player) answers whether the dig went ahead, then whether the
        // chocobo left the greens (each only when a boolean true)
        private (bool Dug, bool KeepGreens) OnChocoboDigLua(CharData c)
        {
            if (lua_ == null) return (false, false);
            int top = lua_.Top;
            try
            {
                lua_.GetPath("xi.chocoboDig.start");
                if (!lua_.IsFunction(-1))
                {
                    log_("luautils::OnChocoboDig: xi.chocoboDig.start: Function not found");
                    return (false, false);
                }
                lua_.PushObject(c);
                lua_.Call(1, 2);
                bool Returned(int idx) => lua_.Type(idx) == 1 && lua_.ToBoolean(idx);
                return (Returned(-2), Returned(-1));
            }
            catch (LuaException e)
            {
                log_($"luautils::OnChocoboDig: {e.Message}");
                return (false, false);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // GP_SERV_COMMAND_DIG (0x02F)
        private static BasicPacket DigPacket(CharData c)
        {
            var w = new PacketWriter(0x02F, 12);
            w.U32(4, c.Id);
            w.U16(8, c.TargId);
            w.U8(10, 0x01);
            return w.Packet;
        }

        // the char's own chocobo, saved as the raw ChocoboUserData_t
        private void SaveChocoboUserData(CharData c)
        {
            if (Exec("INSERT INTO char_pet SET charid = ?, chocobo_user_data = ? ON DUPLICATE KEY UPDATE chocobo_user_data = VALUES(chocobo_user_data)",
                     BoundValue.Of(c.Id), BoundValue.Of(c.ChocoboUserData)) == null)
                log_($"Failed to save chocobo user data ({c.Name})");
        }

        // a racing silks change while riding the char's own chocobo: the client reads its speed from 0x037
        private void PersonalChocoboGearChanged(CharData c)
        {
            if (MountUtils.IsPersonalChocobo(c)) PushTo(c, CharStatus(c));
        }

        private static readonly string[] RaisingColumns =
        {
            "first_name", "last_name", "sex", "created", "last_update_age", "stage", "location", "color", "allele1", "allele2",
            "allele3", "strength", "endurance", "discernment", "receptivity", "affection", "energy", "satisfaction", "conditions",
            "ability1", "ability2", "personality", "weather_preference", "hunger", "care_plan", "held_item", "locked_plan",
            "appearance", "walk_progress",
        };

        private void RegisterMountBindings(Dictionary<string, LuaFunction> m)
        {
            // registerChocobo(table): the char's own chocobo, its look / speed / minutes and registered stats
            m["registerChocobo"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("CLuaBaseEntity::registerChocobo() - Entity is null, or not PC.");
                    return 0;
                }
                uint U(string key, uint max) => (uint)Math.Min((ulong)(uint)FieldInt(l, 2, key, 0), max);
                bool B(string key) => FieldBool(l, 2, key);
                MountUtils.SetFieldChocobo(c, MountUtils.Properties(B("largeBeak"), B("fullTail"), B("largeTalons"), U("color", 4),
                                                                    U("speed", 0x7F), U("minutes", 0x3F)));
                var d = c.ChocoboUserData;
                d[MountUtils.OffAbility1] = (byte)U("ability1", 0xFF);
                d[MountUtils.OffAbility2] = (byte)U("ability2", 0xFF);
                d[MountUtils.OffStrength] = (byte)U("strength", 0xFF);
                d[MountUtils.OffEndurance] = (byte)U("endurance", 0xFF);
                d[MountUtils.OffDiscernment] = (byte)U("discernment", 0xFF);
                d[MountUtils.OffReceptivity] = (byte)U("receptivity", 0xFF);
                d[MountUtils.OffWeather] = (byte)U("weather", 0xFF);
                d[MountUtils.OffSilksSpeedBonus] = (byte)U("silksSpeedBonus", 0xFF);
                c.MountId = 0;
                SaveChocoboUserData(c);
                return 0;
            };
            // getFieldChocobo: the registered chocobo's look, speed and minutes; nil without one
            m["getFieldChocobo"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("CLuaBaseEntity::getFieldChocobo() - Entity is null, or not PC.");
                    l.PushNil();
                    return 1;
                }
                uint p = MountUtils.FieldChocobo(c);
                if (p == 0)
                {
                    l.PushNil();
                    return 1;
                }
                l.NewTable();
                void Num(string key, double v)
                {
                    l.Push(v);
                    l.SetField(-2, key);
                }
                void Bool(string key, bool v)
                {
                    l.Push(v);
                    l.SetField(-2, key);
                }
                Num("color", MountUtils.Color(p));
                Bool("largeBeak", MountUtils.LargeBeak(p));
                Bool("fullTail", MountUtils.FullTail(p));
                Bool("largeTalons", MountUtils.LargeTalons(p));
                Num("speed", MountUtils.Speed(p));
                Num("minutes", MountUtils.Minutes(p));
                Num("properties", p);
                return 1;
            };
            // getChocoboUserData: the raising state that outlives any one chocobo
            m["getChocoboUserData"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("CLuaBaseEntity::getChocoboUserData() - Entity is null, or not PC.");
                    l.PushNil();
                    return 1;
                }
                var d = c.ChocoboUserData;
                l.NewTable();
                void Num(string key, double v)
                {
                    l.Push(v);
                    l.SetField(-2, key);
                }
                Num("flags", MountUtils.Flags(c));
                Num("chocobosRaised", MountUtils.ChocobosRaised(c));
                Num("registeredAbility1", d[MountUtils.OffAbility1]);
                Num("registeredAbility2", d[MountUtils.OffAbility2]);
                Num("registeredStrength", d[MountUtils.OffStrength]);
                Num("registeredEndurance", d[MountUtils.OffEndurance]);
                Num("registeredDiscernment", d[MountUtils.OffDiscernment]);
                Num("registeredReceptivity", d[MountUtils.OffReceptivity]);
                Num("registeredWeather", d[MountUtils.OffWeather]);
                Num("silksSpeedBonus", d[MountUtils.OffSilksSpeedBonus]);
                return 1;
            };
            // setChocoboUserData({ flags, chocobosRaised }): the fields left out keep their values
            m["setChocoboUserData"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("CLuaBaseEntity::setChocoboUserData() - Entity is null, or not PC.");
                    return 0;
                }
                MountUtils.SetFlags(c, (uint)FieldInt(l, 2, "flags", MountUtils.Flags(c)));
                MountUtils.SetChocobosRaised(c, (ushort)Math.Min((uint)FieldInt(l, 2, "chocobosRaised", MountUtils.ChocobosRaised(c)), 0xFFFFu));
                SaveChocoboUserData(c);
                return 0;
            };

            // getChocoboRaisingInfo: the char's char_chocobos row as a table; nil without one
            m["getChocoboRaisingInfo"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("CLuaBaseEntity::getChocoboRaisingInfo() - Entity is null, or not PC.");
                    l.PushNil();
                    return 1;
                }
                var r = Exec("SELECT charid, " + string.Join(", ", RaisingColumns.Select(col => col == "created" ? "UNIX_TIMESTAMP(`created`) AS created" : col)) +
                             " FROM char_chocobos WHERE charid = ? LIMIT 1", BoundValue.Of(c.Id));
                if (r == null || !r.Next())
                {
                    l.PushNil();
                    return 1;
                }
                l.NewTable();
                l.Push(r.Get<uint>("charid"));
                l.SetField(-2, "charid");
                foreach (string col in RaisingColumns)
                {
                    if (col is "first_name" or "last_name") l.PushString(r.Get<string>(col));
                    else l.Push((double)r.Get<long>(col));
                    l.SetField(-2, col);
                }
                return 1;
            };
            // setChocoboRaisingInfo(table): the char_chocobos row written whole (a missing name is Chocobo, stage 1)
            m["setChocoboRaisingInfo"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("CLuaBaseEntity::setChocoboRaisingInfo() - Entity is null, or not PC.");
                    return Ret(l, false);
                }
                var args = new List<BoundValue> { BoundValue.Of(c.Id) };
                foreach (string col in RaisingColumns)
                {
                    if (col is "first_name" or "last_name") args.Add(BoundValue.Of(FieldString(l, 2, col, "Chocobo")));
                    else args.Add(BoundValue.Of((uint)FieldInt(l, 2, col, col == "stage" ? 1 : 0)));
                }
                string sets = string.Join(", ", RaisingColumns.Select(col => col == "created" ? "created = FROM_UNIXTIME(?)" : col + " = ?"));
                string updates = string.Join(", ", RaisingColumns.Select(col => $"{col} = VALUES({col})"));
                return Ret(l, Exec($"INSERT INTO char_chocobos SET charid = ?, {sets} ON DUPLICATE KEY UPDATE {updates}", args.ToArray()) != null);
            };
            m["deleteRaisedChocobo"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_("CLuaBaseEntity::deleteRaisedChocobo() - Entity is null, or not PC.");
                    return Ret(l, false);
                }
                return Ret(l, Exec("DELETE FROM char_chocobos WHERE charid = ? LIMIT 1", BoundValue.Of(c.Id)) != null);
            };
        }
    }
}
