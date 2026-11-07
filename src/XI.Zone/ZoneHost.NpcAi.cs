using System.Collections.Generic;
using XI.Script;

namespace XI.Zone
{
    // An NPC's AI: luautils::OnNpcSpawn as zoneutils::LoadNPCList ends (every NPC's onSpawn once the zone's NPCs are
    // all in), initNpcAi (a CAIContainer with a CPathFind and no controller), and the controller-less half of
    // CAIContainer::Tick every logic tick: the path followed unless the "pauseNPCPathing" local var is set or a state
    // holds it (the trigger state, the inactive state wait puts on), the PATH listener and onPath on a point, the
    // inactive state ending past its duration, then CNpcEntity::PostTick (the updatemask sent at most every 250 ms). The path bindings are ZoneHost.MobRoam.cs's, the path PathFind.cs's; clearTargID is here.
    //
    // A dynamic NPC that disappears with m_bReleaseTargIDOnDisappear is released (DeleteDynamicNpc,
    // ZoneHost.DynamicEntities.cs). (TICK: ZoneHost.Listeners.cs.)
    public sealed partial class ZoneHost
    {
        // getEntityCachedFunction for an NPC and the call: false when its script has no such function
        private bool CallNpcHook(NpcEntity npc, string fn, params object[] args)
        {
            if (lua_ == null || scripts_ == null) return false;
            int top = lua_.Top;
            try
            {
                scripts_.PushFileFunction($"./scripts/zones/{AreaOf(npc).Name}/npcs/{npc.Name}.lua", fn);
                if (lua_.IsNil(-1)) return false;
                foreach (var a in args) lua_.Push(a);
                lua_.Call(args.Length, 0);
            }
            catch (LuaException e)
            {
                ScriptError("luautils::" + fn, e);
            }
            finally
            {
                lua_.SetTop(top);
            }
            return true;
        }

        // the end of LoadNPCList, once the zone is in zones_ (an NPC may look another up): each NPC's onSpawn
        private void StartNpcs(ZoneInfo info)
        {
            foreach (var npc in new List<NpcEntity>(info.Npcs)) CallNpcHook(npc, "onSpawn", npc);
        }

        // PAI->Inactive(duration, true) for an NPC: CInactiveState forced on top
        private void NpcWait(NpcEntity npc, long durationMs) =>
            npc.Inactive = new InactiveState { EntryMs = NowMs(), DurationMs = durationMs, CanChangeState = true, Below = npc.Inactive };

        // CTriggerState, entered when a pathing NPC is triggered and may change state: it completes on the next logic
        // tick and ends on the one after, holding the path for both; "stopPathingOnTrigger" pauses it for good
        private void NpcTriggered(NpcEntity npc)
        {
            if (npc.PathFind == null || npc.TriggerHoldTicks != 0) return;
            npc.TriggerHoldTicks = 2;
            if (LocalVars(npc).TryGetValue("stopPathingOnTrigger", out var stop) && stop == 1) LocalVars(npc)["pauseNPCPathing"] = 1;
        }

        // CZoneEntities::npcTick for the NPCs with an AI, then every NPC's PostTick
        private void TickNpcs()
        {
            long now = NowMs();
            // a script may load another zone or add an NPC while this runs
            foreach (var (info, _, _) in AllAreas())
            {
                var npcs = info.Npcs;
                List<NpcEntity> released = null;
                for (int i = 0; i < npcs.Count; i++)
                {
                    var npc = npcs[i];
                    // This is only valid for dynamic entities
                    if (npc.Status == NpcEntity.StatusDisappear && npc.ReleaseTargIdOnDisappear)
                    {
                        (released ??= new List<NpcEntity>()).Add(npc);
                        continue;
                    }
                    if ((npc.PathFind != null || npc.Inactive != null) && now >= npc.NextAiTickMs)
                    {
                        npc.NextAiTickMs = now + MobAiTickMs;
                        if (npc.PathFind != null) NpcFollowPath(npc, now);
                        // CInactiveState::Update for an NPC: done once past its duration (never without one), then the
                        // state it was stacked on
                        while (npc.Inactive is { DurationMs: > 0 } w && now > w.EntryMs + w.DurationMs) npc.Inactive = w.Below;
                    }
                    if (npc.UpdateMask != 0 && npc.Status != NpcEntity.StatusDisappear && now > npc.NextUpdateMs)
                    {
                        npc.NextUpdateMs = now + 250;
                        BroadcastNpcUpdate(npc, npc.UpdateMask);
                        npc.UpdateMask = 0;
                    }
                }
                if (released != null)
                    foreach (var npc in released) DeleteDynamicNpc(info, npc, now);
                info.EraseStaleDynamicTargIds(now);
            }
        }

        // CAIContainer::Tick without a controller: CanFollowPath (no state, or one that lets the path go on)
        private void NpcFollowPath(NpcEntity npc, long now)
        {
            if (npc.TriggerHoldTicks > 0)
            {
                npc.TriggerHoldTicks--;
                return;
            }
            if (npc.Inactive != null) return;
            if (LocalVars(npc).TryGetValue("pauseNPCPathing", out var paused) && paused != 0) return;
            npc.PathFind.FollowPath(now);
            if (npc.PathFind.OnPoint)
            {
                TriggerListener(npc, "PATH", npc);
                CallNpcHook(npc, "onPath", npc);
            }
        }

        private void RegisterNpcAiBindings(Dictionary<string, LuaFunction> m)
        {
            // initNpcAi: a fresh AI with a path (any path before is dropped)
            m["initNpcAi"] = l =>
            {
                if (Entity(l) is not NpcEntity npc)
                {
                    log_($"Attempting to init NPC AI for invalid entity type ({NameOf(Entity(l))}).");
                    return 0;
                }
                npc.PathFind = new PathFind(npc, this);
                npc.TriggerHoldTicks = 0;
                return 0;
            };
            // clearTargID: m_TargID (what the entity faces) cleared
            m["clearTargID"] = l =>
            {
                switch (Entity(l))
                {
                    case NpcEntity npc: npc.LookAt = 0; break;
                    case MobEntity mob: mob.BattleTargId = 0; break;
                    case CharData c: c.FaceTarget = 0; break;
                }
                MarkUpdate(Entity(l), EntityUpdatePacket.UpdatePos);
                return 0;
            };
        }
    }
}
