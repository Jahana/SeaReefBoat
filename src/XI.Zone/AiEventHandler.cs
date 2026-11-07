using System;
using System.Collections.Generic;
using XI.Script;

namespace XI.Zone
{
    // CAIEventHandler: the Lua functions an entity's scripts and mixins listen with, by event name. A listener is
    // keyed by its identifier: adding one drops any other with the same identifier, and removing one while listeners
    // are running waits until they are done.
    public sealed class AiEventHandler
    {
        private readonly Dictionary<string, List<(string Identifier, LuaRef Func)>> listeners_ =
            new Dictionary<string, List<(string, LuaRef)>>(StringComparer.Ordinal);
        private readonly List<string> toRemove_ = new List<string>();
        private readonly List<LuaRef> toUnref_ = new List<LuaRef>();
        private int triggerDepth_;
        // CAIContainer::m_PrevTick: the last zone tick, for TICK's elapsed milliseconds (0 before the first)
        public long LastTickMs;

        public void AddListener(LuaState lua, string eventName, LuaRef func, string identifier)
        {
            RemoveFromAllListeners(lua, identifier);
            if (!listeners_.TryGetValue(eventName, out var list)) listeners_[eventName] = list = new List<(string, LuaRef)>();
            list.Add((identifier, func));
        }

        public void RemoveListener(LuaState lua, string identifier)
        {
            if (triggerDepth_ > 0)
            {
                toRemove_.Add(identifier);
                return;
            }
            RemoveFromAllListeners(lua, identifier);
        }

        public bool HasListener(string eventName) => listeners_.TryGetValue(eventName, out var list) && list.Count > 0;

        // whether a listener of the identifier is held, whatever its event
        // (quest rework P10: QuestScripts.Live's checks)
        public bool Has(string identifier)
        {
            foreach (var list in listeners_.Values)
                foreach (var (id, _) in list)
                    if (id == identifier) return true;
            return false;
        }

        // how many listeners it holds, every event's (quest rework P10: QuestScripts.Live's check of the release)
        public int Count
        {
            get
            {
                int n = 0;
                foreach (var list in listeners_.Values) n += list.Count;
                return n;
            }
        }

        // ~CAIEventHandler (quest rework P10): every listener dropped and its function released, as the C++ handler
        // goes with its CCharEntity on a zone change or a logout; while listeners run, each function is released once
        // they are done
        public void Clear(LuaState lua)
        {
            foreach (var list in listeners_.Values)
            {
                foreach (var (_, func) in list)
                {
                    if (triggerDepth_ > 0) toUnref_.Add(func);
                    else lua.Unref(func);
                }
                list.Clear();
            }
            toRemove_.Clear();
        }

        // the listeners for eventName, each called with args; pushArgs pushes them and returns how many
        public void Trigger(LuaState lua, string eventName, Func<int> pushArgs, Action<string> error)
        {
            if (!listeners_.TryGetValue(eventName, out var list) || list.Count == 0) return;
            triggerDepth_++;
            try
            {
                foreach (var (_, func) in list.ToArray())
                {
                    int top = lua.Top;
                    try
                    {
                        lua.PushRef(func);
                        lua.Call(pushArgs(), 0);
                    }
                    catch (LuaException e)
                    {
                        error($"Error in listener event {eventName}: {e.Message}");
                    }
                    finally
                    {
                        lua.SetTop(top);
                    }
                }
            }
            finally
            {
                triggerDepth_--;
            }
            if (triggerDepth_ > 0) return;
            foreach (var identifier in toRemove_) RemoveFromAllListeners(lua, identifier);
            toRemove_.Clear();
            foreach (var r in toUnref_) lua.Unref(r);
            toUnref_.Clear();
        }

        private void RemoveFromAllListeners(LuaState lua, string identifier)
        {
            foreach (var list in listeners_.Values)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (list[i].Identifier != identifier) continue;
                    // a listener still running holds its function until the trigger is over
                    if (triggerDepth_ > 0) toUnref_.Add(list[i].Func);
                    else lua.Unref(list[i].Func);
                    list.RemoveAt(i);
                }
            }
        }
    }
}
