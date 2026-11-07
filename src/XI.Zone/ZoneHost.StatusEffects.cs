using System;
using System.Collections.Generic;
using XI.Map;
using XI.Persistence;
using XI.Script;

namespace XI.Zone
{
    // Status effects (CStatusEffectContainer, effects::LoadEffectsParameters, CLuaStatusEffect and the
    // CLuaBaseEntity effect and mod bindings): the gain / tick / lose scripts, the expiry check every tick and the
    // regen and effect ticks every 3 seconds (CZoneEntities::charTick), the 0x037 / 0x067 / 0x063 and party 0x076 a
    // char's change sends (CCharEntity::PostTick), char_effects across zone changes and logouts (persist::flush), and
    // the client's own cancel (0x0F1).
    // Mobs, pets and trusts tick the same way (CZoneEntities::mobTick / petTick / trustTick), their effect scripts
    // run on them, and a wear-off goes only to the effect's origin when it has them spawned; a REGEN_DOWN tick goes
    // through takeDamage, so its death is the usual one (TickCharDeaths / MobDie / PetDie / TrustDie). A mob gains
    // its REGAIN only while engaged; a pet or trust always. TickRegen takes a char's avatar perpetuation
    // (PerpetuationCost, ZoneHost.PlayerPets.cs) and its Monstrosity idle regen. Neither a pet nor a trust has status
    // icons (UpdateStatusIcons is a char's only, and 0x076 lists chars).
    // The persist sweep writes a char's marked position and effects every 5 seconds (MapEngine::persistSweep).
    // Not ported: ReportErrorToPlayer.
    public sealed partial class ZoneHost : IStatusEffectHost
    {
        private const string StatusEffectType = "CStatusEffect";
        private const long EffectCheckIntervalMs = 3000;

        private EffectTable effectTable_;
        private long effectCheckMs_;
        private Random effectRng_ = new Random();
        // the status effects' random numbers from here on seeded (the verify suites that compare two runs)
        internal void SeedEffectRng(int seed) => effectRng_ = new Random(seed);

        // the container's link back to this host, before anything is added (and the char's latents', ZoneHost.Latents.cs)
        private void AttachEffects(BattleEntity c)
        {
            c.StatusEffects.Host = this;
            c.StatusEffects.Table = effectTable_;
            if (c is CharData pc) pc.Latents.Host = this;
        }

        // ---- IStatusEffectHost ---------------------------------------------------------------------------------------

        long IStatusEffectHost.NowMs() => NowMs();

        // SetEffectParams: the item's script for an equipped item's or a food's effect that has its own gain and lose,
        // else the effect's own script (but for an enchantment, an equipped item or a food from an item), else the
        // script of the item the sub type names
        string IStatusEffectHost.EffectScriptName(StatusEffect e, EffectParams p)
        {
            if (e.SourceType != EffectSource.None && e.SourceTypeParam > 0 &&
                (e.SourceType == EffectSource.EquippedItem || e.SourceType == EffectSource.Food) &&
                e.SourceTypeParam <= ushort.MaxValue && LookupItem((ushort)e.SourceTypeParam) is { } source)
            {
                string name = "items/" + source.Name;
                if (HasScriptFunction(name, "onEffectGain") && HasScriptFunction(name, "onEffectLose")) return name;
            }
            if (e.Id != EffectId.Enchantment && e.SourceType != EffectSource.EquippedItem &&
                (e.Id != EffectId.Food || e.SourceTypeParam == 0))
                return "effects/" + p.Name;
            return e.SubId > 0 && e.SubId <= ushort.MaxValue && LookupItem((ushort)e.SubId) is { } item ? "items/" + item.Name : "";
        }

        void IStatusEffectHost.OnEffectGain(BattleEntity c, StatusEffect e) => CallEffectScript("onEffectGain", c, e);
        void IStatusEffectHost.OnEffectTick(BattleEntity c, StatusEffect e) => CallEffectScript("onEffectTick", c, e);
        void IStatusEffectHost.OnEffectLose(BattleEntity c, StatusEffect e) => CallEffectScript("onEffectLose", c, e);
        void IStatusEffectHost.ManeuverChange(BattleEntity c, ushort effectId, bool gain) => OnManeuverChange(c, effectId, gain);
        void IStatusEffectHost.CharmedDespawnPet(CharData c) => DespawnPet(c);

        void IStatusEffectHost.EffectListener(BattleEntity c, string eventName, StatusEffect e)
        {
            if (e != null) TriggerListener(c, eventName, c, e);
            else TriggerListener(c, eventName, c);
        }

        // RemoveStatusEffect's message: to the char, and to whoever cast it when that char is here and can see it; for a
        // mob, pet or trust that is still alive (isDead), only to whoever cast it, when it has that entity spawned
        // (charutils::hasEntitySpawned: its mob, pet or trust spawn list)
        void IStatusEffectHost.EffectWornOff(BattleEntity b, StatusEffect e, ushort messageId)
        {
            if (b is MobEntity m)
            {
                if (StatusEffectContainer.IsDead(m) || e.OriginId == 0) return;
                var (caster, _) = CharById(e.OriginId);
                var spawned = m switch
                {
                    PetEntity => caster?.SpawnPetList,
                    TrustEntity => caster?.SpawnTrustList,
                    _ => caster?.SpawnMobList,
                };
                if (caster != null && SameArea(caster, m) && spawned.Contains(m.Id))
                    PushTo(caster, SelfPackets.BattleMessage(caster.Id, caster.TargId, m.Id, m.TargId, e.Icon, 0, messageId));
                return;
            }
            var c = (CharData)b;
            PushTo(c, SelfPackets.BattleMessage(c, e.Icon, 0, messageId));
            if (e.OriginId == 0 || e.OriginId == c.Id) return;
            var (origin, _) = CharById(e.OriginId);
            if (origin != null && SameArea(origin, c) && origin.SpawnPcList.ContainsKey(c.Id))
                PushTo(origin, SelfPackets.BattleMessage(origin.Id, origin.TargId, c.Id, c.TargId, e.Icon, 0, messageId));
        }

        void IStatusEffectHost.EffectGone(StatusEffect e) => lua_?.Forget(e);

        // ---- luautils ------------------------------------------------------------------------------------------------

        private string EffectScriptFile(string name)
        {
            string file = $"./scripts/{name}.lua";
            if (itemScripts_.Add(file)) scripts_.LoadLuaObjectFromFile(file);
            return file;
        }

        private bool HasScriptFunction(string name, string fn)
        {
            if (lua_ == null) return false;
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(EffectScriptFile(name), fn);
                return lua_.IsFunction(-1);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // luautils::OnEffectGain / OnEffectTick / OnEffectLose: fn(entity, effect) from the effect's script
        private void CallEffectScript(string fn, BattleEntity c, StatusEffect e)
        {
            if (lua_ == null || string.IsNullOrEmpty(e.Name)) return;
            var file = EffectScriptFile(e.Name);
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction(file, fn);
                if (lua_.IsNil(-1)) return;
                lua_.PushObject(c);
                lua_.PushObject(e, StatusEffectType);
                lua_.Call(2, 0);
            }
            catch (LuaException ex)
            {
                ScriptError(fn, ex);
            }
            finally
            {
                lua_.SetTop(top);
            }
        }

        // ---- ticks ---------------------------------------------------------------------------------------------------

        // CZoneEntities::charTick: the expiry check every tick, then every 3 seconds the regen and the effect ticks;
        // then PostTick's re-send of what changed
        private void TickStatusEffects()
        {
            long now = NowMs();
            bool effectTick = now > effectCheckMs_;
            // CZoneEntities::mobTick: the spawned mobs first
            foreach (var (info, _, _) in AllAreas())
            {
                if (info.Spawner == null && info.Mobs.Count == 0) continue;
                foreach (var m in info.Mobs)
                {
                    if (!m.IsSpawned) continue;
                    m.StatusEffects.CheckEffectsExpiry(now);
                    if (effectTick)
                    {
                        TickRegen(m);
                        m.StatusEffects.TickEffects(now);
                    }
                }
            }
            // CZoneEntities::petTick: then the pets
            foreach (var (info, _, _) in AllAreas())
            {
                foreach (var pet in info.Pets.ToArray())
                {
                    if (pet.Status == NpcEntity.StatusDisappear) continue;
                    pet.StatusEffects.CheckEffectsExpiry(now);
                    if (effectTick)
                    {
                        TickRegen(pet);
                        pet.StatusEffects.TickEffects(now);
                    }
                }
            }
            // CZoneEntities::trustTick: then the trusts
            foreach (var (info, _, _) in AllAreas())
            {
                foreach (var trust in info.Trusts.ToArray())
                {
                    if (trust.Status == NpcEntity.StatusDisappear) continue;
                    trust.StatusEffects.CheckEffectsExpiry(now);
                    if (effectTick)
                    {
                        TickRegen(trust);
                        trust.StatusEffects.TickEffects(now);
                    }
                }
            }
            foreach (var (c, s) in new List<KeyValuePair<CharData, MapSession>>(charSessions_))
            {
                c.StatusEffects.CheckEffectsExpiry(now);
                if (effectTick)
                {
                    TickRegen(c);
                    c.StatusEffects.TickEffects(now);
                }
                if (s.InZone) FlushEffects(s, c);
            }
            if (effectTick)
                effectCheckMs_ = effectCheckMs_ + EffectCheckIntervalMs > now ? effectCheckMs_ + EffectCheckIntervalMs : now + EffectCheckIntervalMs;
        }

        // CStatusEffectContainer::TickRegen (nothing while dead): Regen and Refresh do not go below 0; REGEN_DOWN goes
        // through stoneskin and wakes the entity up unless it is a player avatar's Nightmare; REGAIN only for a mob that
        // is engaged (PAI->IsEngaged), any other entity always
        private void TickRegen(BattleEntity c)
        {
            if (StatusEffectContainer.IsDead(c)) return;
            var effects = c.StatusEffects;
            short regen = Math.Max(c.GetMod(Mod.REGEN), (short)0);
            short poison = c.GetMod(Mod.REGEN_DOWN);
            short refresh = (short)(Math.Max(c.GetMod(Mod.REFRESH), (short)0) - c.GetMod(Mod.REFRESH_DOWN));
            short regain = (short)(c.GetMod(Mod.REGAIN) - c.GetMod(Mod.REGAIN_DOWN));
            BattleAddHp(c, regen);
            if (c is CharData { Monstrosity: not null } mon) MonstrosityIdleRegen(mon);

            if (poison != 0)
            {
                int damage = effects.HandleStoneskin(poison);
                if (damage > 0)
                {
                    effects.DelStatusEffectSilent(EffectId.Healing);
                    BattleTakeDamage(c, damage, null, 0, 0);
                    if (!(effects.GetStatusEffect(EffectId.SleepI) is StatusEffect sleep && sleep.Tier >= 4)) effects.WakeUp();
                }
            }

            // a char's avatar perpetuation comes out of the refresh; an avatar leaves when the MP runs out
            if (c is CharData pc && pc.GetMod(Mod.AVATAR_PERPETUATION) > 0)
            {
                BattleAddMp(c, refresh - PerpetuationCost(pc));
                if (pc.Mp == 0 && pc.PPet is PetEntity { PetType: PetType.Avatar }) DespawnPet(pc);
            }
            else BattleAddMp(c, refresh);
            // a mob (TYPE_MOB: a charmed one too) only while engaged; a char, pet or trust always
            if (c is CharData tc) AddTp(tc, regain);
            else if (c is PetEntity or TrustEntity || ((MobEntity)c).Engaged) AddMobTp((MobEntity)c, regain);
            if (c.PPet is AutomatonEntity automaton) automaton.BurdenTick(c);
        }

        // CCharEntity::PostTick: m_EffectsChanged re-sends the status, the sync, the job extras and the icons, and the
        // party's GROUP_EFFECTS; then the updatemask
        private void FlushEffects(MapSession s, CharData c)
        {
            if (c.EffectsChanged)
            {
                Push(s, CharStatus(c));
                Push(s, CharSyncOf(c));
                SendExtendedJobPackets(s, c);
                Push(s, GameOkPackets.StatusIcons(c, NowMs(), UnixNow()));
                c.Party?.PushEffectsPacket();
                c.EffectsChanged = false;
            }
            if (c.UpdateMask != 0) SendUpdateMask(s, c);
        }

        // 0x076 GP_SERV_COMMAND_GROUP_EFFECTS: up to 5 other members, 48 bytes each from @4 (UniqueNo, ActIndex, the
        // icon bits and the 32 icons)
        internal static BasicPacket GroupEffects(IReadOnlyList<CharData> members)
        {
            var w = new PacketWriter(0x076, 244);
            for (int i = 0; i < members.Count && i < 5; i++)
            {
                int at = 4 + 48 * i;
                w.U32(at, members[i].Id);
                w.U16(at + 4, members[i].TargId);
                w.U64(at + 8, members[i].StatusEffects.Bits);
                w.Bytes(at + 16, members[i].StatusEffects.Icons);
            }
            return w.Packet;
        }

        // ---- char_effects --------------------------------------------------------------------------------------------

        internal const string SqlEffects = "SELECT effectid, icon, power, tick, duration, subid, subpower, tier, flags, timestamp, sourcetype, sourcetypeparam, " +
                                            "originid FROM char_effects WHERE charid = ?";

        // CStatusEffectContainer::LoadStatusEffects: an offline-ticking effect has what is left of it (a Visitant that ran
        // out gets a second, to throw the char out once it is in), the shadows come back as mods
        private void LoadStatusEffects(CharData c)
        {
            AttachEffects(c);
            var list = new List<StatusEffect>();
            var rs = Exec(SqlEffects, BoundValue.Of(c.Id));
            long unixNow = UnixNow();
            while (rs != null && rs.Next())
            {
                uint flags = rs.Get<uint>("flags");
                long durationMs = 1000L * rs.Get<uint>("duration");
                ushort id = rs.Get<ushort>("effectid");
                if ((flags & EffectFlag.OfflineTick) != 0)
                {
                    long end = rs.Get<uint>("timestamp") + durationMs / 1000;
                    if (unixNow < end) durationMs = (end - unixNow) * 1000;
                    else if (id == EffectId.Visitant) durationMs = 1000;
                    else continue;
                }
                var e = new StatusEffect(id, rs.Get<ushort>("icon"), rs.Get<ushort>("power"), 1000L * (ushort)rs.Get<uint>("tick"), durationMs,
                                         (ushort)rs.Get<uint>("subid"), (ushort)rs.Get<short>("subpower"), 0, rs.Get<ushort>("tier"), flags,
                                         rs.Get<ushort>("sourcetype"), rs.Get<uint>("sourcetypeparam"), rs.Get<uint>("originid"));
                if (e.Id == EffectId.CopyImage) c.SetModifier(Mod.UTSUSEMI, (short)e.SubPower);
                else if (e.Id == EffectId.Blink) c.SetModifier(Mod.BLINK, (short)e.Power);
                list.Add(e);
            }
            foreach (var e in list) c.StatusEffects.AddStatusEffect(e);
            // nothing changed since the read
            c.Persist &= unchecked((byte)~CharPersist.Effects);
            CharStats.UpdateHealth(c);
        }

        // persist::flush: what does not survive the transition goes, then char_effects is rewritten (durations tick
        // down without anything flagging the char) along with anything else pending
        private void SaveStatusEffects(CharData c, bool logout)
        {
            if (c.StatusEffects.Table == null) return;
            c.StatusEffects.DropEffectsForTransition(logout);
            c.Persist |= CharPersist.Effects;
            PersistChar(c, logout);
        }

        // MapEngine::persistSweep, every kPersistSweepInterval: what each char in the zone marked since the last one.
        // A char zoning or logging out is written by persist::flush instead.
        private const long PersistSweepIntervalMs = 5000;
        private long persistSweepMs_;

        // MapEngine::persistSweep gathers the batch on the main thread and writes it on a worker
        // (spawnOnWorkerThread), so the tick does not wait on one UPDATE per char every 5 s. XI.Host map turns it on; off,
        // the batch is written in the tick (the in-process suites read char_effects right after it).
        public bool PersistOnWorker;

        // PersistBatch::write's flushedAtTeardown: a char's position or effects written on the main thread since a batch
        // was gathered are newer than the batch's, which then drops them. One lock for the batch write and those
        // writes, so neither lands in the middle of the other.
        private readonly object persistLock_ = new object();
        private readonly Dictionary<uint, long> positionWrittenAt_ = new Dictionary<uint, long>(), effectsWrittenAt_ = new Dictionary<uint, long>();
        private long persistSeq_;
        private System.Threading.Tasks.Task persistWrite_ = System.Threading.Tasks.Task.CompletedTask;

        private sealed class PersistBatch
        {
            public long GatheredAt;
            public readonly List<(uint Id, byte Rotation, float X, float Y, float Z, ushort Boundary)> Positions = new();
            public readonly List<(uint Id, List<PersistedEffect> Rows)> Effects = new();
        }

        private void TickPersistSweep()
        {
            long now = NowMs();
            if (now < persistSweepMs_) return;
            persistSweepMs_ = now + PersistSweepIntervalMs;
            var batch = new PersistBatch();
            lock (persistLock_) batch.GatheredAt = ++persistSeq_;
            foreach (var (c, s) in new List<KeyValuePair<CharData, MapSession>>(charSessions_))
            {
                if (s.ShuttingDown != 0 || !s.InZone || zoneChanges_.ContainsKey(s) || leaveGame_.Contains(s)) continue;
                AddToBatch(batch, c, false);
            }
            if (batch.Positions.Count == 0 && batch.Effects.Count == 0) return;
            if (!PersistOnWorker)
            {
                WriteBatch(batch);
                return;
            }
            // one batch after the other, as the interval task awaits its write
            persistWrite_ = persistWrite_.ContinueWith(_ =>
            {
                try { WriteBatch(batch); }
                catch (Exception e) { log_($"persistSweep: batch write failed: {e.Message}"); }
            }, System.Threading.Tasks.TaskScheduler.Default);
        }

        // the last batch on its way, at shutdown
        public void WaitForPersist() => persistWrite_.Wait();

        // PersistBatch::add: the char's pending parts, then nothing pending
        private void AddToBatch(PersistBatch batch, CharData c, bool logout)
        {
            byte pending = c.Persist;
            c.Persist = 0;
            if ((pending & CharPersist.Position) != 0) batch.Positions.Add((c.Id, c.Rotation, c.X, c.Y, c.Z, c.Boundary));
            if ((pending & CharPersist.Effects) != 0 && c.StatusEffects.Table != null)
                batch.Effects.Add((c.Id, c.StatusEffects.BuildPersistRows(logout, NowMs(), UnixNow())));
        }

        // PersistBatch::write: without what the main thread wrote since the gathering, positions then effects
        private void WriteBatch(PersistBatch batch)
        {
            lock (persistLock_)
            {
                bool Newer(Dictionary<uint, long> written, uint id) => written.TryGetValue(id, out long at) && at > batch.GatheredAt;
                batch.Positions.RemoveAll(p => Newer(positionWrittenAt_, p.Id));
                batch.Effects.RemoveAll(e => Newer(effectsWrittenAt_, e.Id));
                // charutils::SaveCharPositions: one transaction
                if (batch.Positions.Count > 0)
                    db_.Transaction(() =>
                    {
                        foreach (var p in batch.Positions)
                            Exec("UPDATE chars SET pos_rot = ?, pos_x = ?, pos_y = ?, pos_z = ?, boundary = ? WHERE charid = ?",
                                 BoundValue.Of(p.Rotation), BoundValue.Of(p.X), BoundValue.Of(p.Y), BoundValue.Of(p.Z), BoundValue.Of(p.Boundary), BoundValue.Of(p.Id));
                    });
                foreach (var (id, rows) in batch.Effects) WriteEffects(id, rows);
            }
        }

        // a position written on the main thread (charutils::SaveCharPosition, SendToZone's): newer than any batch in flight
        private void PositionWritten(uint charId)
        {
            lock (persistLock_) positionWrittenAt_[charId] = ++persistSeq_;
        }

        // persist::flush for one char (a zone change, a logout, a timeout): the pending parts, written now
        private void PersistChar(CharData c, bool logout)
        {
            var batch = new PersistBatch();
            AddToBatch(batch, c, logout);
            lock (persistLock_)
            {
                if (batch.Positions.Count > 0) SavePosition(c);
                foreach (var (id, rows) in batch.Effects)
                {
                    WriteEffects(id, rows);
                    effectsWrittenAt_[id] = ++persistSeq_;
                }
            }
        }

        // effects::SaveEffectRows: the DELETE, then the INSERTs, in one transaction. Two processes writing two
        // chars' rows at once can deadlock on the gap locks of their DELETEs (REPEATABLE READ: the chars' rows share
        // an index gap), which rolls the loser back; the C++ logs it (db::transaction's ShowCritical) and the rows
        // are lost until the char's effects change again. Here it is run again on a deadlock (TransactionRetry, as
        // the quest rows are: the rows are built before the transaction, so a second run writes the same ones), and
        // a write that still failed is logged
        private void WriteEffects(uint charId, List<PersistedEffect> rows)
        {
            if (!db_.TransactionRetrying(() => WriteEffectRows(charId, rows),
                    (attempt, error) => log_($"persist: char {charId} retried its char_effects write " +
                                             $"(attempt {attempt}) after a lock conflict (MySQL {error})")))
                log_($"persist: char {charId}: writing its char_effects rows failed (rolled back)");
        }

        // each statement must go through: a failed one (Execute logs it and returns null) throws, so the transaction
        // rolls back as the C++ db::executeBulk's throw does, the DELETE and the INSERTs with it
        private void WriteEffectRows(uint charId, List<PersistedEffect> rows)
        {
            MustEffectRow(Exec("DELETE FROM char_effects WHERE charid = ?", BoundValue.Of(charId)), "DELETE");
            foreach (var r in rows)
                MustEffectRow(Exec("INSERT INTO char_effects (charid, effectid, icon, power, tick, duration, subid, " +
                                   "subpower, tier, flags, timestamp, sourcetype, sourcetypeparam, originid) " +
                                   "VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)",
                                   BoundValue.Of(charId), BoundValue.Of(r.EffectId), BoundValue.Of(r.Icon),
                                   BoundValue.Of(r.Power), BoundValue.Of(r.Tick), BoundValue.Of(r.Duration),
                                   BoundValue.Of(r.SubId), BoundValue.Of((short)r.SubPower), BoundValue.Of(r.Tier),
                                   BoundValue.Of(r.Flags), BoundValue.Of(r.Timestamp), BoundValue.Of(r.SourceType),
                                   BoundValue.Of(r.SourceTypeParam), BoundValue.Of(r.OriginId)), "INSERT");
        }

        private static void MustEffectRow(ResultSet r, string statement)
        {
            if (r == null) throw new InvalidOperationException($"a char_effects {statement} failed");
        }

        // ---- Lua -----------------------------------------------------------------------------------------------------

        private long UnixOf(long ms) => UnixNow() + (long)Math.Floor((ms - NowMs()) / 1000.0);

        private void RegisterStatusEffectBindings(Dictionary<string, LuaFunction> m)
        {
            static StatusEffect Self(LuaState l) =>
                l.ToObject(1) as StatusEffect ?? throw new InvalidOperationException("bad self: expected " + StatusEffectType);
            // an integral argument (sol2's llround) and an enum one (xi::StatusEffect, EffectSourceType, xi::MobMod)
            static uint U32(LuaState l, int idx) => (uint)l.ToIntegral(idx);
            static uint E(LuaState l, int idx) => (uint)l.ToEnum(idx);

            // CLuaStatusEffect
            lua_.RegisterType(StatusEffectType, new Dictionary<string, LuaFunction>
            {
                ["getEffectType"] = l => Ret(l, Self(l).Id),
                ["getSubType"] = l => Ret(l, Self(l).SubId),
                ["getSourceType"] = l => Ret(l, Self(l).SourceType),
                ["getSourceTypeParam"] = l => Ret(l, Self(l).SourceTypeParam),
                ["getOriginID"] = l => Ret(l, Self(l).OriginId),
                ["setOriginID"] = l => { Self(l).OriginId = U32(l, 2); Self(l).MarkPersistDirty(); return 0; },
                ["setSource"] = l =>
                {
                    var e = Self(l);
                    e.SourceType = (ushort)E(l, 2);
                    e.SourceTypeParam = U32(l, 3);
                    e.MarkPersistDirty();
                    return 0;
                },
                ["setIcon"] = l => { Self(l).SetIcon((ushort)U32(l, 2)); return 0; },
                ["getPower"] = l => Ret(l, Self(l).Power),
                ["setPower"] = l => { Self(l).Power = (ushort)U32(l, 2); Self(l).MarkPersistDirty(); return 0; },
                ["getSubPower"] = l => Ret(l, Self(l).SubPower),
                ["setSubPower"] = l => { Self(l).SubPower = (ushort)U32(l, 2); Self(l).MarkPersistDirty(); return 0; },
                ["setSubIcon"] = l => { Self(l).SetSubIcon((ushort)U32(l, 2)); return 0; },
                ["getTier"] = l => Ret(l, Self(l).Tier),
                ["setTier"] = l => { Self(l).Tier = (ushort)U32(l, 2); Self(l).MarkPersistDirty(); return 0; },
                ["getEffectSlot"] = l => Ret(l, Self(l).Slot),
                ["setEffectSlot"] = l => { Self(l).Slot = (byte)U32(l, 2); return 0; },
                ["getDuration"] = l => Ret(l, (uint)Self(l).DurationMs),
                ["setDuration"] = l => { Self(l).DurationMs = U32(l, 2); Self(l).MarkPersistDirty(); return 0; },
                ["getStartTime"] = l => Ret(l, (uint)UnixOf(Self(l).StartMs)),
                ["setStartTime"] = l => { Self(l).SetStartTime(NowMs() + (U32(l, 2) - UnixNow()) * 1000); return 0; },
                ["resetStartTime"] = l => { Self(l).SetStartTime(NowMs()); return 0; },
                // the ticks left: the whole duration's less those gone by
                ["getLastTick"] = l =>
                {
                    var e = Self(l);
                    return Ret(l, e.TickMs != 0 ? (uint)(e.DurationMs / e.TickMs) - (uint)e.TickCount : 0u);
                },
                ["getTimeRemaining"] = l =>
                {
                    var e = Self(l);
                    return Ret(l, e.DurationMs > 0 ? (uint)Math.Max(e.StartMs - NowMs() + e.DurationMs, 0) : 0u);
                },
                ["getTickCount"] = l => Ret(l, (uint)Self(l).TickCount),
                ["getTick"] = l => Ret(l, (uint)Self(l).TickMs),
                ["setTick"] = l => { Self(l).TickMs = U32(l, 2); return 0; },
                ["addMod"] = l => { Self(l).AddMod((Mod)(ushort)U32(l, 2), (short)l.ToIntArg(3)); return 0; },
                ["getEffectFlags"] = l => Ret(l, Self(l).Flags),
                ["setEffectFlags"] = l => { Self(l).Flags = U32(l, 2); Self(l).MarkPersistDirty(); return 0; },
                ["addEffectFlag"] = l => { Self(l).Flags |= U32(l, 2); Self(l).MarkPersistDirty(); return 0; },
                ["delEffectFlag"] = l => { Self(l).Flags &= ~U32(l, 2); Self(l).MarkPersistDirty(); return 0; },
                ["hasEffectFlag"] = l => Ret(l, Self(l).HasFlag(U32(l, 2))),
                ["getIcon"] = l => Ret(l, Self(l).Icon),
                ["getSubIcon"] = l => Ret(l, Self(l).SubIcon),
            });

            m["addStatusEffect"] = AddStatusEffectBinding;
            m["addBardSong"] = AddBardSongBinding;
            m["getStatusEffect"] = l =>
            {
                if (Battle(l) is not { } c || (!l.IsNil(3) && !l.IsNil(4)))
                {
                    l.PushNil();
                    return 1;
                }
                ushort id = (ushort)E(l, 2);
                StatusEffect e;
                if (!l.IsNil(3)) e = c.StatusEffects.GetStatusEffect(id, (ushort)U32(l, 3));
                else if (!l.IsNil(4) && !l.IsNil(5))
                    e = c.StatusEffects.GetStatusEffectBySource(id, (ushort)E(l, 4), (ushort)U32(l, 5));
                else e = c.StatusEffects.GetStatusEffect(id);
                l.PushObject(e, StatusEffectType);
                return 1;
            };
            m["getStatusEffectBySource"] = l =>
            {
                var e = Battle(l)?.StatusEffects.GetStatusEffectBySource((ushort)E(l, 2), (ushort)E(l, 3), (ushort)U32(l, 4));
                l.PushObject(e, StatusEffectType);
                return 1;
            };
            m["getStatusEffects"] = l =>
            {
                if (Battle(l) is not { } c)
                {
                    l.PushNil();
                    return 1;
                }
                l.NewTable();
                int n = 0;
                foreach (var e in c.StatusEffects.All)
                {
                    l.PushObject(e, StatusEffectType);
                    l.RawSetI(-2, ++n);
                }
                return 1;
            };
            m["getStatusEffectElement"] = l =>
                Ret(l, Battle(l) != null && U32(l, 2) < EffectId.Max ? effectTable_[(ushort)U32(l, 2)].Element : 0);
            m["canGainStatusEffect"] = l =>
            {
                if (Battle(l) is not { } c) return Ret(l, false);
                ushort power = l.IsNumber(3) ? (ushort)U32(l, 3) : (ushort)0;
                ushort id = (ushort)E(l, 2);
                return Ret(l, id < EffectId.Max && c.StatusEffects.CanGainStatusEffect(new StatusEffect(id, 0, power, 0, 0)));
            };
            m["hasStatusEffect"] = l =>
            {
                if (Battle(l) is not { } c) return Ret(l, false);
                ushort id = (ushort)E(l, 2);
                // with a subtype: CStatusEffectContainer::HasStatusEffect(StatusID, SubID), the effect with that subid (not
                // the any-of-these-ids overload)
                return Ret(l, l.IsNil(3) ? c.StatusEffects.HasStatusEffect(id) : c.StatusEffects.HasStatusEffectWithSubId(id, (ushort)U32(l, 3)));
            };
            m["hasStatusEffectByFlag"] = l =>
                Ret(l, Battle(l) is { } c && c.StatusEffects.HasStatusEffectByFlag((ushort)U32(l, 2)));
            m["countEffect"] = l => Ret(l, Battle(l) is { } c ? c.StatusEffects.GetEffectsCount((ushort)E(l, 2)) : 0);
            m["countEffectWithFlag"] = l =>
                Ret(l, Battle(l) is { } c ? c.StatusEffects.GetEffectsCountWithFlag(U32(l, 2)) : 0);
            m["delStatusEffect"] = l =>
            {
                if (Battle(l) is not { } c || (!l.IsNil(3) && !l.IsNil(4))) return Ret(l, false);
                ushort id = (ushort)E(l, 2);
                if (!l.IsNil(3)) return Ret(l, c.StatusEffects.DelStatusEffect(id, (uint)(ushort)U32(l, 3)));
                if (!l.IsNil(4) && !l.IsNil(5))
                    return Ret(l, c.StatusEffects.DelStatusEffectBySource(id, (ushort)E(l, 4), (ushort)U32(l, 5)));
                return Ret(l, c.StatusEffects.DelStatusEffect(id));
            };
            m["delStatusEffectSilent"] = l =>
                Ret(l, Battle(l) is { } c && c.StatusEffects.DelStatusEffectSilent((ushort)E(l, 2)));
            m["delStatusEffectsByFlag"] = l =>
            {
                if (Battle(l) is { } c)
                    c.StatusEffects.DelStatusEffectsByFlag(U32(l, 2), l.ToBoolean(3) ? EffectNotice.Silent : EffectNotice.ShowMessage);
                return 0;
            };
            m["delStatusEffectsByType"] = l =>
            {
                Battle(l)?.StatusEffects.DelStatusEffectsByType((ushort)U32(l, 2));
                return 0;
            };
            m["eraseStatusEffect"] = l =>
                Ret(l, Battle(l) is { } c ? c.StatusEffects.EraseStatusEffect(effectRng_) : EffectId.None);
            m["eraseAllStatusEffect"] = l => Ret(l, Battle(l) is { } c ? c.StatusEffects.EraseAllStatusEffect() : 0);
            m["dispelStatusEffect"] = l =>
                Ret(l, Battle(l) is { } c ? c.StatusEffects.DispelStatusEffect(OptFlag(l, 2), effectRng_) : 0);
            m["dispelAllStatusEffect"] = l =>
                Ret(l, Battle(l) is { } c ? c.StatusEffects.DispelAllStatusEffect(OptFlag(l, 2)) : 0);
            m["stealStatusEffect"] = l =>
            {
                if (Battle(l) is not { } c || l.ToObject(2) is not BattleEntity target) return Ret(l, 0);
                var notice = l.ToBoolean(4) ? EffectNotice.Silent : EffectNotice.ShowMessage;
                if (target.StatusEffects.StealStatusEffect(OptFlag(l, 3), notice, effectRng_) is not StatusEffect stolen) return Ret(l, 0);
                c.StatusEffects.AddStatusEffect(stolen);
                return Ret(l, stolen.Id);
            };
            m["wakeUp"] = l =>
            {
                Battle(l)?.StatusEffects.WakeUp();
                return 0;
            };
            m["hasPreventActionEffect"] = l =>
                Ret(l, Battle(l) is { } c && c.StatusEffects.HasPreventActionEffect());

            // modifiers (CBattleEntity::addModifier / setModifier / delModifier / getMod); mod 0 is nothing
            static bool ModOk(BattleEntity c, uint mod) => mod != 0 && mod < c.ModStat.Length;
            m["addMod"] = l =>
            {
                if (Battle(l) is { } c && ModOk(c, U32(l, 2))) c.AddModifier((Mod)U32(l, 2), (short)l.ToIntArg(3));
                return 0;
            };
            m["setMod"] = l =>
            {
                if (Battle(l) is { } c && ModOk(c, U32(l, 2))) c.SetModifier((Mod)U32(l, 2), (short)l.ToIntArg(3));
                return 0;
            };
            m["delMod"] = l =>
            {
                if (Battle(l) is { } c && ModOk(c, U32(l, 2))) c.DelModifier((Mod)U32(l, 2), (short)l.ToIntArg(3));
                return 0;
            };
            m["getMod"] = l => Ret(l, Battle(l) is { } c && ModOk(c, U32(l, 2)) ? c.GetMod((Mod)U32(l, 2)) : 0);
            // CLuaBaseEntity::getMobMod / setMobMod / addMobMod / delMobMod: mobs only
            static bool MobModOk(uint mod) => mod < MobEntity.MobModCount;
            m["getMobMod"] = l => Ret(l, Mob(l) is { } mb && MobModOk(E(l, 2)) ? mb.GetMobMod((MobMod)E(l, 2)) : 0);
            m["setMobMod"] = l =>
            {
                if (Mob(l) is { } mb && MobModOk(E(l, 2))) mb.SetMobMod((MobMod)E(l, 2), (short)l.ToIntArg(3));
                return 0;
            };
            m["addMobMod"] = l =>
            {
                if (Mob(l) is { } mb && MobModOk(E(l, 2))) mb.AddMobMod((MobMod)E(l, 2), (short)l.ToIntArg(3));
                return 0;
            };
            m["delMobMod"] = l =>
            {
                if (Mob(l) is { } mb && MobModOk(E(l, 2))) mb.AddMobMod((MobMod)E(l, 2), (short)-l.ToIntArg(3));
                return 0;
            };

            // what the HEALING / LEAVEGAME scripts reach
            m["isPC"] = l => Ret(l, Entity(l) is CharData);
            m["getObjType"] = l => Ret(l, ObjTypeOf(Entity(l)));
            m["isMob"] = l => Ret(l, Entity(l) is MobEntity and not PetEntity and not TrustEntity);
            m["isNPC"] = l => Ret(l, Entity(l) is NpcEntity);
            // CLuaBaseEntity::getPet: the linked pet (a non-battle self warns and returns nil, as the C++
            // dynamic_cast<CBattleEntity*> guard)
            m["getPet"] = l =>
            {
                if (Entity(l) is BattleEntity b) { l.PushObject(b.PPet); return 1; }
                log_($"Invalid Entity ({NameOf(Entity(l))}) calling function.");
                l.PushNil();
                return 1;
            };
            // CLuaBaseEntity::isAutomaton: a pet of the four frames' pet ids
            m["isAutomaton"] = l => Ret(l, Entity(l) is PetEntity { PetId: >= PetId.HarlequinFrame and <= PetId.StormwakerFrame });
            m["getCurrentRegion"] = l => Ret(l, ZoneRegions.Of(ZoneOf(Entity(l))));
            m["getGMLevel"] = l => Ret(l, Pc(l, "getGMLevel") is CharData c ? c.AccountGmLevel : 0);
            m["getHPP"] = l => Ret(l, Pc(l, "getHPP") is CharData c ? HpPercent(c) : 0);
            m["addHPLeaveSleeping"] = l => Ret(l, Pc(l, "addHPLeaveSleeping") is CharData c ? AddHp(c, l.ToIntArg(2)) : 0);
            // the UPDATE_HP the PostTick turns into 0x0DF + 0x037 (a char), an entity update (a mob or NPC)
            m["setAnimation"] = l =>
            {
                byte animation = (byte)l.ToEnumArg(2); // xi::Animation
                switch (Entity(l))
                {
                    case BattleEntity b when b.Animation != animation:
                        b.Animation = animation;
                        b.UpdateMask |= CharStats.UpdateHp;
                        break;
                    case NpcEntity n when n.Animation != animation:
                        n.Animation = animation;
                        MarkUpdate(n, CharStats.UpdateHp);
                        break;
                }
                return 0;
            };
            m["messageSystem"] = l =>
            {
                if (Entity(l) is not CharData c)
                {
                    log_($"lua: Function called on non-PC entity ({NameOf(Entity(l))})");
                    return 0;
                }
                ushort msg = (ushort)l.ToEnumArg(2); // MsgStd
                uint p0 = l.IsNil(3) ? 0 : OptU32(l, 3), p1 = l.IsNil(4) ? 0 : OptU32(l, 4);
                PushTo(c, SystemMessage(msg, p0, p1));
                return 0;
            };
            // status Shutdown: the zone tick logs the char out
            m["leaveGame"] = l =>
            {
                if (Pc(l, "leaveGame") is CharData c && SessionOf(c) is MapSession s) leaveGame_.Add(s);
                return 0;
            };
        }

        private const int ObjTypePc = 0x01, ObjTypeNpc = 0x02, ObjTypeMob = 0x04, ObjTypePet = 0x08, ObjTypeTrust = 0x20;

        private static int ObjTypeOf(object e) => e switch
        {
            CharData => ObjTypePc,
            PetEntity => ObjTypePet,
            TrustEntity => ObjTypeTrust,
            MobEntity => ObjTypeMob,
            _ => ObjTypeNpc,
        };

        // CBattleEntity::GetHPP: floored, at least 1 while alive
        private static int HpPercent(CharData c) => c.Hp == 0 ? 0 : Math.Max(1, (int)MathF.Floor((float)c.Hp / c.MaxHp * 100.0f));

        // a flag argument, Dispelable when there is none
        private static uint OptFlag(LuaState l, int idx) => l.IsNumber(idx) ? (uint)l.ToIntegral(idx) : EffectFlag.Dispelable;

        // CLuaBaseEntity::addBardSong(caster, effect, power, tick (s), duration (s), subType, subPower, tier): two songs per
        // singer, one when a char sings without a string or wind instrument, plus its MAXIMUM_SONGS_BONUS
        private int AddBardSongBinding(LuaState l)
        {
            if (Entity(l) is not BattleEntity b)
            {
                log_($"Invalid entity type calling function ({NameOf(Entity(l))}).");
                return Ret(l, false);
            }
            ushort id = (ushort)l.ToEnumArg(3); // xi::StatusEffect; the rest are uint16
            var e = new StatusEffect(id, id, (ushort)l.ToIntArg(4), 1000L * (ushort)l.ToIntArg(5), 1000L * (ushort)l.ToIntArg(6),
                                     (ushort)l.ToIntArg(7), (ushort)l.ToIntArg(8), 0, (ushort)l.ToIntArg(9)); // songs have no sub-icon
            int maxSongs = 2;
            if (l.ToObject(2) is CharData caster)
            {
                if (caster.RangedWeapon is not { Skill: Skill.StringInstrument or Skill.WindInstrument }) maxSongs = 1;
                maxSongs += caster.GetMod(Mod.MAXIMUM_SONGS_BONUS);
            }
            return Ret(l, b.StatusEffects.ApplyBardEffect(e, maxSongs));
        }

        // CLuaBaseEntity::addStatusEffect(effectId, { origin = entity, duration (s), power, tick (s), ... }): a food lasts
        // FOOD_DURATION percent longer
        private int AddStatusEffectBinding(LuaState l)
        {
            if (Entity(l) is not BattleEntity c) return Ret(l, false);
            ushort id = (ushort)l.ToEnumArg(2); // xi::StatusEffect
            if (!l.IsTable(3)) throw new InvalidOperationException("addStatusEffect: expected a table of parameters");

            l.GetField(3, "origin");
            object origin = l.ToObject(-1);
            l.Pop(1);
            uint originId = origin switch
            {
                BattleEntity ob => ob.Id,
                NpcEntity npc => npc.Id,
                _ => throw new InvalidOperationException("addStatusEffect: origin is not an entity"),
            };

            double Num(string key, double fallback)
            {
                l.GetField(3, key);
                double v = l.IsNumber(-1) ? l.ToNumber(-1) : fallback;
                l.Pop(1);
                return v;
            }
            // get_or with an integral default: sol2's llround getter
            long Int(string key, long fallback)
            {
                l.GetField(3, key);
                long v = l.IsNumber(-1) ? l.ToIntegral(-1) : fallback;
                l.Pop(1);
                return v;
            }
            l.GetField(3, "silent");
            bool silent = l.ToBoolean(-1);
            l.Pop(1);

            double duration = Num("duration", 0);
            long durationMs = (long)(ulong)(duration * 1000);
            if (id == EffectId.Food && c.GetMod(Mod.FOOD_DURATION) is short foodDuration && foodDuration != 0)
                durationMs += (long)Math.Floor(durationMs * (foodDuration / 100.0f));

            // power, tick and subPower are get_or(0.0) doubles cast through int64 (truncated); the rest are integral
            var e = new StatusEffect(id, (ushort)Int("icon", id), (ushort)(long)Num("power", 0), 1000L * (uint)(long)Num("tick", 0), durationMs,
                                     (uint)Int("subType", 0), (ushort)(long)Num("subPower", 0), (ushort)(uint)Int("subIcon", 0), (ushort)Int("tier", 0),
                                     (uint)Int("flag", 0), (ushort)Int("sourceType", 0), (uint)Int("sourceTypeParam", 0), originId, (byte)Int("slot", 0));
            return Ret(l, c.StatusEffects.AddStatusEffect(e, silent ? EffectNotice.Silent : EffectNotice.ShowMessage));
        }
    }
}
