using System;
using System.Collections.Generic;
using XI.Map;

namespace XI.Zone
{
    // Char-to-char visibility (zone_entities.cpp): each client keeps a SpawnPCList of the other chars it has been sent a
    // spawn for, SpawnPCs re-scores it after the char's position updates, and a char's own updates go to every client
    // that has it spawned. Party and alliance members and visible GMs score ahead of distance, and a char a spawned mob
    // is acting on gets a bonus (SpawnPCs' scoreBonus).
    public sealed partial class ZoneHost
    {
        private const float CharacterSyncDistance = 45.0f, CharacterDespawnDistance = 50.0f;
        private const float EntityVerticalRenderDistance = 20.0f, VerticalRenderDistanceOffset = 0.5f;
        private const int CharacterSyncLimitMax = 32, CharacterSwapMax = 5;
        private const float CharacterSyncDistanceSwapThreshold = 30.0f, CharacterSyncPartySignificance = 100000.0f,
                             CharacterSyncAlliSignificance = 10000.0f;
        private const float Charreq2SyncRange = 50.0f;
        private const ushort ZoneMogGarden = 280, ZoneFeretory = 285;

        // m_charList: the chars in the char's zone on this host, not the char itself
        private IEnumerable<(CharData, MapSession)> ZoneChars(CharData c)
        {
            foreach (var (o, os) in charSessions_)
                if (o != c && os.InZone && SameArea(o, c)) yield return (o, os);
        }

        private static float Distance(CharData a, CharData b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        // isWithinVerticalDistance
        private static bool WithinVerticalDistance(CharData source, CharData target) =>
            MathF.Abs(target.Y - source.Y - VerticalRenderDistanceOffset) <= EntityVerticalRenderDistance;

        // getSignificanceScore: a visible GM, the same party, the same alliance
        private static float SignificanceScore(CharData origin, CharData target)
        {
            if (target.AccountGmLevel > 0 && !target.GmHidden) return CharacterSyncAlliSignificance;
            if (origin.Party != null && target.Party != null)
            {
                if (origin.Party.Id == target.Party.Id) return CharacterSyncPartySignificance;
                if (origin.Party.Alliance != null && target.Party.Alliance != null && origin.Party.Alliance.Id == target.Party.Alliance.Id)
                    return CharacterSyncAlliSignificance;
            }
            return 0;
        }

        // PState->target() for a mob: the magic, skill, ranged or ability state on top of the attack state's battle
        // target; none while it roams, and itself while inactive or dead
        private static BattleEntity MobStateTarget(MobEntity m)
        {
            if (m.InactiveState != null || !m.IsAlive || m.IsDead) return m;
            return m.MagicState?.Target ?? m.SkillState?.Target ?? m.RangedState?.Target ?? m.AbilityState?.Target ??
                   (m.Engaged ? m.BattleTarget : null);
        }

        // SpawnPCs' scoreBonus: CHARACTER_SYNC_DISTANCE_SWAP_THRESHOLD for each mob the char has spawned whose state
        // targets another char
        private Dictionary<uint, float> CharScoreBonus(CharData c)
        {
            var scoreBonus = new Dictionary<uint, float>();
            var zone = AreaOf(c);
            foreach (uint id in c.SpawnMobList)
            {
                if (!zone.MobById.TryGetValue(id, out var m)) continue;
                if (MobStateTarget(m) is CharData target && target.Id != c.Id)
                    scoreBonus[target.Id] = scoreBonus.GetValueOrDefault(target.Id) + CharacterSyncDistanceSwapThreshold;
            }
            return scoreBonus;
        }

        // CZoneEntities::GetNewCharTargID: the lowest act index from 0x400 no char in the zone holds
        private ushort NewCharTargId(CharData c)
        {
            var taken = new HashSet<ushort>();
            foreach (var (o, _) in ZoneChars(c)) taken.Add(o.TargId);
            ushort targid = 0x400;
            while (taken.Contains(targid)) targid++;
            if (targid >= 0x700) log_($"zone: targid is high ({targid:X3}), update packets will be ignored!");
            return targid;
        }

        // CCharEntity::updateEntityPacket for a char: merged into the 0x00D for that char still waiting to be sent
        private void UpdateCharPacket(MapSession s, CharData other, EntityUpdate type, byte updatemask)
        {
            foreach (var pending in s.PacketList)
            {
                if (pending.GetOpcode() == 0x00D && BitConverter.ToUInt32(pending.Buffer, 4) == other.Id)
                {
                    LoginPackets.CharPcUpdateWith(pending, other, AreaOf(other), type, updatemask);
                    return;
                }
            }
            s.PacketList.AddLast(LoginPackets.CharPc(other, AreaOf(other), type, updatemask));
        }

        // CZoneEntities::InsertPC's TryAddToNearbySpawnLists: the chars already in range see the new one at once
        private void InsertPc(CharData c)
        {
            foreach (var (o, os) in ZoneChars(c))
            {
                if (Distance(c, o) > EntityRenderDistance || !WithinVerticalDistance(c, o) || c.MoghouseId != o.MoghouseId) continue;
                o.SpawnPcList[c.Id] = c;
                UpdateCharPacket(os, c, EntityUpdate.Spawn, LoginPackets.UpdateAllChar);
            }
        }

        // CZoneEntities::DespawnPC, plus the char's own list starting over in the next zone
        private void DespawnPc(CharData c)
        {
            foreach (var (o, os) in ZoneChars(c))
            {
                if (!o.SpawnPcList.Remove(c.Id)) continue;
                UpdateCharPacket(os, c, EntityUpdate.Despawn, 0);
            }
            c.SpawnPcList.Clear();
        }

        // CZoneEntities::UpdateEntityPacket for a char's ENTITY_UPDATE: the clients that have it spawned
        private void BroadcastCharUpdate(CharData c, byte updatemask)
        {
            if (c.GmHidden) return; // CCharEntity::PostTick sends no update for a hidden GM
            foreach (var (o, os) in ZoneChars(c))
                if (o.SpawnPcList.ContainsKey(c.Id)) UpdateCharPacket(os, c, EntityUpdate.Update, updatemask);
        }

        // CZoneEntities::SpawnPCs
        private void SpawnPcs(MapSession s, CharData c)
        {
            if (c.Zone == ZoneFeretory || c.Zone == ZoneMogGarden) return;

            // Provide bonus score to characters targeted by spawned mobs
            var scoreBonus = CharScoreBonus(c);

            // spawned chars that stay, scored, the worst first (MinHeap) -- at most CHARACTER_SYNC_LIMIT_MAX of them
            var spawned = new List<(float Score, CharData Char)>();
            var toRemove = new List<CharData>();
            foreach (var o in c.SpawnPcList.Values)
            {
                if (!charSessions_.TryGetValue(o, out var os) || !os.InZone || !SameArea(o, c) || o.GmHidden ||
                    c.MoghouseId != o.MoghouseId || !WithinVerticalDistance(c, o))
                {
                    toRemove.Add(o);
                    continue;
                }
                float charDistance = Distance(c, o);
                if (charDistance >= CharacterDespawnDistance)
                {
                    toRemove.Add(o);
                    continue;
                }
                float significance = SignificanceScore(c, o);
                float total = significance + scoreBonus.GetValueOrDefault(o.Id) - charDistance + CharacterSyncDistanceSwapThreshold;
                if (significance < CharacterSyncAlliSignificance) spawned.Add((total, o));
            }
            spawned.Sort((a, b) => a.Score.CompareTo(b.Score));
            if (spawned.Count > CharacterSyncLimitMax) spawned.RemoveRange(0, spawned.Count - CharacterSyncLimitMax);

            foreach (var o in toRemove)
            {
                UpdateCharPacket(s, o, EntityUpdate.Despawn, 0);
                c.SpawnPcList.Remove(o.Id);
            }

            // candidates, the best CHARACTER_SYNC_LIMIT_MAX of them
            var candidates = new List<(float Score, CharData Char)>();
            foreach (var (o, _) in ZoneChars(c))
            {
                if (c.SpawnPcList.ContainsKey(o.Id) || o.GmHidden || c.MoghouseId != o.MoghouseId) continue;
                float charDistance = Distance(c, o);
                if (charDistance > CharacterSyncDistance || !WithinVerticalDistance(c, o)) continue;
                float total = SignificanceScore(c, o) + scoreBonus.GetValueOrDefault(o.Id) - charDistance;
                if (c.SpawnPcList.Count < CharacterSyncLimitMax || (spawned.Count > 0 && total > spawned[0].Score))
                    candidates.Add((total, o));
            }
            if (candidates.Count == 0) return;
            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
            if (candidates.Count > CharacterSyncLimitMax) candidates.RemoveRange(CharacterSyncLimitMax, candidates.Count - CharacterSyncLimitMax);

            int swapCount = 0;
            foreach (var (score, o) in candidates)
            {
                if (swapCount >= CharacterSwapMax) break;
                if (c.SpawnPcList.Count >= CharacterSyncLimitMax)
                {
                    if (spawned.Count == 0 || score <= spawned[0].Score) break;
                    var worst = spawned[0].Char;
                    spawned.RemoveAt(0);
                    c.SpawnPcList.Remove(worst.Id);
                    UpdateCharPacket(s, worst, EntityUpdate.Despawn, 0);
                    swapCount++;
                }
                c.SpawnPcList[o.Id] = o;
                UpdateCharPacket(s, o, EntityUpdate.Spawn, LoginPackets.UpdateAllChar);
                Push(s, CharSyncOf(o));
            }
        }

        // the char with this act index in the char's zone: itself or another char
        private CharData CharByTargId(CharData c, ushort targid)
        {
            if (targid == c.TargId) return c;
            foreach (var (o, _) in ZoneChars(c))
                if (o.TargId == targid) return o;
            return null;
        }

        // resolveByUniqueNo: the entity at the id's act index when the id is its own, else a char in the zone by id
        private object ResolveByUniqueNo(CharData c, uint uniqueNo)
        {
            if (uniqueNo == 0) return null;
            var e = EntityByTargId(c, (ushort)(uniqueNo & 0xFFF));
            uint id = e switch { NpcEntity n => n.Id, MobEntity m => m.Id, CharData o => o.Id, _ => 0 };
            if (e != null && id == uniqueNo) return e;
            if (uniqueNo == c.Id) return c;
            foreach (var (o, _) in ZoneChars(c))
                if (o.Id == uniqueNo) return o;
            return null;
        }

        // GetEntity(ActIndex) for CHARREQ2: a mob or an NPC below 0x400, a char below 0x700, else a pet or a trust
        private object EntityByTargId(CharData c, ushort targid)
        {
            if (targid < 0x700) return EntityAt(c, targid);
            var z = AreaOf(c);
            return (object)z.Pets.Find(p => p.TargId == targid) ?? z.Trusts.Find(t => t.TargId == targid);
        }

        // GP_CLI_COMMAND_CHARREQ2: up to three entities (act index, then two unique numbers), each once
        private void OnCharReq2(MapSession s, CharData c, BasicPacket p)
        {
            var targets = new[] { EntityByTargId(c, U16(p, 4)), ResolveByUniqueNo(c, U32(p, 8)), ResolveByUniqueNo(c, U32(p, 12)) };
            log_($"zone: GP_CLI_COMMAND_CHARREQ2 from {c.Name}: ActIndex={U16(p, 4)} UniqueNo2={U32(p, 8)} UniqueNo3={U32(p, 12)} Flg={U16(p, 16)} Flg2={U16(p, 18)}");

            for (int i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                if (target == null || Array.IndexOf(targets, target, 0, i) >= 0) continue;

                if (target is CharData o)
                {
                    float dist = Distance(c, o);
                    if (dist > Charreq2SyncRange)
                        log_($"zone: GP_CLI_COMMAND_CHARREQ2 from {c.Name}: target {o.Name} ({o.Id}) is {dist:F1}y away (beyond {Charreq2SyncRange}y)");
                    if (o.GmHidden) continue;
                    UpdateCharPacket(s, o, EntityUpdate.Spawn, LoginPackets.UpdateAllChar);
                    if (dist <= Charreq2SyncRange)
                    {
                        Push(s, CharSyncOf(o));
                        Push(s, LoginPackets.CharPc(o, AreaOf(o), EntityUpdate.Update, LoginPackets.SendName));
                    }
                }
                else if (target is NpcEntity n)
                {
                    float dx = c.X - n.X, dy = c.Y - n.Y, dz = c.Z - n.Z;
                    float dist = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (dist > Charreq2SyncRange)
                        log_($"zone: GP_CLI_COMMAND_CHARREQ2 from {c.Name}: target {n.Name} ({n.Id}) is {dist:F1}y away (beyond {Charreq2SyncRange}y)");
                    UpdateEntityPacket(s, n, EntityUpdate.Update, EntityUpdatePacket.UpdateAllMob);
                }
                else if (target is MobEntity m)
                {
                    // a mob, a pet or a trust
                    float dist = Distance(c, m);
                    if (dist > Charreq2SyncRange)
                        log_($"zone: GP_CLI_COMMAND_CHARREQ2 from {c.Name}: target {m.Name} ({m.Id}) is {dist:F1}y away (beyond {Charreq2SyncRange}y)");
                    UpdateEntityPacket(s, m, EntityUpdate.Update, EntityUpdatePacket.UpdateAllMob);
                }
            }
        }
    }
}
