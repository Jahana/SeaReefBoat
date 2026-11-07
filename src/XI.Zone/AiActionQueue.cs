using System;
using System.Collections.Generic;
using XI.Script;

namespace XI.Zone
{
    // CAIActionQueue: the Lua functions timer() and queue() hold for an entity, soonest deadline first. A timer runs
    // once its deadline has passed; a queued action also waits until the entity's AI can change state. The server's
    // own actions (useMobAbility's) queue as native callbacks beside them.
    public sealed class AiActionQueue
    {
        private readonly List<(long Deadline, LuaRef Func, Action<object> Native)> timers_ = new List<(long, LuaRef, Action<object>)>();
        private readonly List<(long Deadline, LuaRef Func, Action<object> Native)> actions_ = new List<(long, LuaRef, Action<object>)>();

        public bool IsEmpty => timers_.Count == 0 && actions_.Count == 0;

        public void Push(long deadline, bool checkState, LuaRef func) => Insert(deadline, checkState, func, null);
        public void Push(long deadline, bool checkState, Action<object> native) => Insert(deadline, checkState, null, native);

        private void Insert(long deadline, bool checkState, LuaRef func, Action<object> native)
        {
            var list = checkState ? actions_ : timers_;
            int i = list.Count;
            while (i > 0 && list[i - 1].Deadline > deadline) i--;
            list.Insert(i, (deadline, func, native));
        }

        // checkAction: the next one due (the timers first, then the actions while canChangeState), out of the queue
        public bool TryPopDue(long now, Func<bool> canChangeState, out LuaRef func, out Action<object> native)
        {
            if (timers_.Count > 0 && now > timers_[0].Deadline)
            {
                (_, func, native) = timers_[0];
                timers_.RemoveAt(0);
                return true;
            }
            if (actions_.Count > 0 && now > actions_[0].Deadline && canChangeState())
            {
                (_, func, native) = actions_[0];
                actions_.RemoveAt(0);
                return true;
            }
            func = default;
            native = null;
            return false;
        }

        public void ClearActions(LuaState lua) => Clear(lua, actions_);
        public void ClearTimers(LuaState lua) => Clear(lua, timers_);

        private static void Clear(LuaState lua, List<(long Deadline, LuaRef Func, Action<object> Native)> list)
        {
            foreach (var (_, func, _) in list) if (func != null) lua.Unref(func);
            list.Clear();
        }
    }
}
