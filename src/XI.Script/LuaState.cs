using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static XI.Script.LuaNative;

namespace XI.Script
{
    // A C# function callable from Lua: the arguments are on the stack (1..Top), it pushes its results and returns how
    // many. An exception becomes a Lua error at the call site.
    public delegate int LuaFunction(LuaState lua);

    // A value held in the Lua registry (a function, table, ...) so C# can push it again later
    public sealed class LuaRef
    {
        public readonly int Id;
        internal LuaRef(int id) => Id = id;
    }

    // One LuaJIT VM (the C++ server's single sol::state). Every stack helper works on the thread that is current: the
    // main thread, or the coroutine a C# function was called from.
    public sealed unsafe class LuaState : IDisposable
    {
        private static readonly List<LuaState> states_ = new List<LuaState>();

        private readonly IntPtr main_;
        private readonly int stateId_;
        private readonly List<LuaFunction> functions_ = new List<LuaFunction>();
        private readonly Dictionary<object, int> objectIds_ = new Dictionary<object, int>(ReferenceEqualityComparer.Instance);
        private readonly List<object> objects_ = new List<object>();
        private readonly Dictionary<string, int> typeRefs_ = new Dictionary<string, int>();
        private readonly HashSet<string> missing_ = new HashSet<string>();
        private int wrapRef_, cacheRef_, tracebackRef_;

        public IntPtr L { get; private set; }
        public Action<string> Log = _ => { };

        // binding names scripts reached that have no C# implementation yet ("CBaseEntity:getFameLevel")
        public IReadOnlyCollection<string> MissingBindings => missing_;

        public LuaState()
        {
            main_ = L = luaL_newstate();
            if (main_ == IntPtr.Zero) throw new InvalidOperationException("luaL_newstate failed");
            luaL_openlibs(L);
            lock (states_)
            {
                stateId_ = states_.Count;
                states_.Add(this);
            }

            // the message handler every pcall from C# uses (before the first Run)
            GetGlobal("debug");
            GetField(-1, "traceback");
            tracebackRef_ = luaL_ref(L, LUA_REGISTRYINDEX);
            Pop(1);

            // a raw C# function returns (true, results...) or (false, message); the wrapper turns the latter into an
            // error raised in Lua, so no Lua error ever unwinds through a managed frame
            DoString(@"
                local function unwrap(ok, ...)
                    if not ok then error((...), 2) end
                    return ...
                end
                return function(raw) return function(...) return unwrap(raw(...)) end end", "=xi_wrap", 1);
            wrapRef_ = luaL_ref(L, LUA_REGISTRYINDEX);

            // one userdata per C# object, so == on two pushes of the same entity holds
            lua_createtable(L, 0, 0);
            lua_createtable(L, 0, 1);
            PushString("v");
            SetField(-2, "__mode");
            lua_setmetatable(L, -2);
            cacheRef_ = luaL_ref(L, LUA_REGISTRYINDEX);

            SetGlobal("__xi_missing", l =>
            {
                string name = l.ToStringArg(1) + ":" + l.Describe(2);
                if (missing_.Add(name)) Log($"lua: binding not ported: {name}");
                return 0;
            });
        }

        public void Dispose()
        {
            if (main_ != IntPtr.Zero) lua_close(main_);
            lock (states_) states_[stateId_] = null;
        }

        // ---- stack ----------------------------------------------------------------------------------------------

        public int Top => lua_gettop(L);
        public void SetTop(int idx) => lua_settop(L, idx);
        public void Pop(int n) => lua_settop(L, -n - 1);
        // lua_checkstack: room for n more values (a C function is given LUA_MINSTACK, 20); false when it cannot grow
        public bool CheckStack(int n) => lua_checkstack(L, n) != 0;
        public int Type(int idx) => lua_type(L, idx);
        public bool IsNil(int idx) => lua_type(L, idx) <= LUA_TNIL;
        public bool IsNumber(int idx) => lua_type(L, idx) == LUA_TNUMBER;
        public bool IsString(int idx) => lua_type(L, idx) == LUA_TSTRING;
        public bool IsTable(int idx) => lua_type(L, idx) == LUA_TTABLE;
        public bool IsFunction(int idx) => lua_type(L, idx) == LUA_TFUNCTION;
        public bool IsBoolean(int idx) => lua_type(L, idx) == LUA_TBOOLEAN;

        public double ToNumber(int idx) => lua_tonumber(L, idx);
        public bool ToBoolean(int idx) => lua_toboolean(L, idx) != 0;

        public string ToStringValue(int idx)
        {
            var p = lua_tolstring(L, idx, out var len);
            return p == null ? null : Encoding.UTF8.GetString(p, (int)len);
        }

        // the string's raw bytes, as a std::string return keeps them
        public byte[] ToBytes(int idx)
        {
            var p = lua_tolstring(L, idx, out var len);
            return p == null ? null : new ReadOnlySpan<byte>(p, (int)len).ToArray();
        }

        // what tostring gives for a value, without metamethods (for logs)
        public string Describe(int idx) => Type(idx) switch
        {
            LUA_TNIL or LUA_TNONE => "nil",
            LUA_TBOOLEAN => ToBoolean(idx) ? "true" : "false",
            LUA_TNUMBER or LUA_TSTRING => ToStringValue(idx),
            int t => TypeName(t),
        };

        public static string TypeName(int t) => t switch
        {
            LUA_TNIL => "nil", LUA_TBOOLEAN => "boolean", LUA_TLIGHTUSERDATA => "lightuserdata", LUA_TNUMBER => "number",
            LUA_TSTRING => "string", LUA_TTABLE => "table", LUA_TFUNCTION => "function", LUA_TUSERDATA => "userdata",
            LUA_TTHREAD => "thread", _ => "none",
        };

        // ---- typed arguments (sol2 conversions: a missing or mistyped argument is an error) ------------------------

        public double ToNumberArg(int idx)
        {
            if (Type(idx) != LUA_TNUMBER) throw new LuaArgumentException(idx, "number", TypeName(Type(idx)));
            return ToNumber(idx);
        }

        // sol2's getter for an integral type under LuaJIT (sol.hpp unqualified_getter; no integer subtype before 5.3):
        // static_cast<T>(llround(lua_tonumber)), a fraction rounded half away from zero (2.5 is 3, -2.5 is -3, 2.49 is
        // 2), not truncated. The same getter serves sol::object::as, table get / get_or and a call result's get. The
        // caller's narrowing cast ((ushort), (byte), ...) is the static_cast's.
        public static long LlRound(double d) =>
            d >= -9.2233720368547758E18 && d < 9.2233720368547758E18 ? (long)Math.Round(d, MidpointRounding.AwayFromZero) : long.MinValue;

        public long ToIntegral(int idx) => LlRound(ToNumber(idx));
        public long ToIntegralArg(int idx) => LlRound(ToNumberArg(idx));
        public int ToIntArg(int idx) => (int)ToIntegralArg(idx);
        public uint ToUIntArg(int idx) => (uint)ToIntegralArg(idx);

        // sol2's getter for an enum type is lua_tointegerx, under Lua 5.1 / LuaJIT the compat-5.3 one (sol.hpp
        // lua_tointegerx): the number when it is whole, else 0 (1.5 is 0, not 1 or 2)
        public static long EnumOf(double d) => d >= -9.2233720368547758E18 && d < 9.2233720368547758E18 && d == Math.Truncate(d) ? (long)d : 0;

        public long ToEnum(int idx) => EnumOf(ToNumber(idx));
        public int ToEnumArg(int idx) => (int)EnumOf(ToNumberArg(idx));

        public string ToStringArg(int idx)
        {
            int t = Type(idx);
            if (t != LUA_TSTRING && t != LUA_TNUMBER) throw new LuaArgumentException(idx, "string", TypeName(t));
            return ToStringValue(idx);
        }

        public bool ToBooleanArg(int idx) => ToBoolean(idx);

        public double? OptNumber(int idx) => Type(idx) == LUA_TNUMBER ? ToNumber(idx) : null;
        public int OptInt(int idx, int fallback) => Type(idx) == LUA_TNUMBER ? (int)ToIntegral(idx) : fallback;
        public string OptString(int idx) => IsString(idx) || IsNumber(idx) ? ToStringValue(idx) : null;

        public T ToObjectArg<T>(int idx) where T : class =>
            ToObject(idx) as T ?? throw new LuaArgumentException(idx, typeof(T).Name, TypeName(Type(idx)));

        // ---- pushing --------------------------------------------------------------------------------------------

        public void PushNil() => lua_pushnil(L);
        public void Push(double n) => lua_pushnumber(L, n);
        public void Push(bool b) => lua_pushboolean(L, b ? 1 : 0);
        public void PushValue(int idx) => lua_pushvalue(L, idx);
        public void Remove(int idx) => lua_remove(L, idx);
        public void Insert(int idx) => lua_insert(L, idx);

        public void PushString(string s)
        {
            if (s == null)
            {
                lua_pushnil(L);
                return;
            }
            var bytes = Encoding.UTF8.GetBytes(s);
            fixed (byte* p = bytes) lua_pushlstring(L, p, (nuint)bytes.Length);
        }

        // a std::string's bytes as they are (no encoding), as sol pushes one
        public void PushBytes(ReadOnlySpan<byte> bytes)
        {
            fixed (byte* p = bytes) lua_pushlstring(L, p, (nuint)bytes.Length);
        }

        public void Push(object o)
        {
            switch (o)
            {
                case null: PushNil(); break;
                case bool b: Push(b); break;
                case string s: PushString(s); break;
                case double d: Push(d); break;
                case float f: Push((double)f); break;
                case int i: Push((double)i); break;
                case uint u: Push((double)u); break;
                case long l: Push((double)l); break;
                case ulong ul: Push((double)ul); break;
                case short sh: Push((double)sh); break;
                case ushort us: Push((double)us); break;
                case byte by: Push((double)by); break;
                case sbyte sb: Push((double)sb); break;
                case LuaRef r: PushRef(r); break;
                case LuaFunction fn: PushFunction(fn); break;
                default: PushObject(o); break;
            }
        }

        public void PushRef(LuaRef r) => lua_rawgeti(L, LUA_REGISTRYINDEX, r.Id);

        // pops the top value into the registry
        public LuaRef Ref() => new LuaRef(luaL_ref(L, LUA_REGISTRYINDEX));
        public void Unref(LuaRef r) => luaL_unref(L, LUA_REGISTRYINDEX, r.Id);

        // ---- tables and globals ---------------------------------------------------------------------------------

        public void NewTable(int narr = 0, int nrec = 0) => lua_createtable(L, narr, nrec);

        public void GetField(int idx, string k)
        {
            var bytes = Encoding.UTF8.GetBytes(k + "\0");
            fixed (byte* p = bytes) lua_getfield(L, idx, p);
        }

        public void SetField(int idx, string k)
        {
            var bytes = Encoding.UTF8.GetBytes(k + "\0");
            fixed (byte* p = bytes) lua_setfield(L, idx, p);
        }

        public void RawGetI(int idx, int n) => lua_rawgeti(L, idx, n);
        public void RawSetI(int idx, int n) => lua_rawseti(L, idx, n);
        public void GetTable(int idx) => lua_gettable(L, idx);
        public void SetTable(int idx) => lua_settable(L, idx);
        public int ObjLen(int idx) => (int)lua_objlen(L, idx);
        public bool Next(int idx) => lua_next(L, idx) != 0;

        public void GetGlobal(string name) => GetField(LUA_GLOBALSINDEX, name);

        // the global set to the value on the top of the stack, which is popped
        public void SetGlobal(string name) => SetField(LUA_GLOBALSINDEX, name);

        public void SetGlobal(string name, LuaFunction fn)
        {
            PushFunction(fn);
            SetField(LUA_GLOBALSINDEX, name);
        }

        // a.b.c from the globals; nil when any part is missing
        public void GetPath(string path)
        {
            var parts = path.Split('.');
            GetGlobal(parts[0]);
            for (int i = 1; i < parts.Length; i++)
            {
                if (!IsTable(-1) && Type(-1) != LUA_TUSERDATA)
                {
                    Pop(1);
                    PushNil();
                    return;
                }
                GetField(-1, parts[i]);
                lua_remove(L, -2);
            }
        }

        // ---- C# functions ---------------------------------------------------------------------------------------

        public void PushFunction(LuaFunction fn)
        {
            functions_.Add(fn);
            lua_rawgeti(L, LUA_REGISTRYINDEX, wrapRef_);
            lua_pushnumber(L, functions_.Count - 1);
            lua_pushnumber(L, stateId_);
            lua_pushcclosure(L, &Trampoline, 2);
            if (lua_pcall(L, 1, 1, 0) != 0) throw new LuaException(PopError());
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static int Trampoline(IntPtr L)
        {
            int fnId = (int)lua_tonumber(L, UpvalueIndex(1));
            int stateId = (int)lua_tonumber(L, UpvalueIndex(2));
            LuaState state;
            lock (states_) state = states_[stateId];

            var outer = state.L;
            state.L = L;
            try
            {
                int n = state.functions_[fnId](state);
                // results are the top n values: put the status in front of them
                lua_pushboolean(L, 1);
                lua_insert(L, -n - 1);
                return n + 1;
            }
            catch (Exception e)
            {
                lua_settop(L, 0);
                lua_pushboolean(L, 0);
                state.PushString(e is LuaArgumentException or LuaException ? e.Message : e.ToString());
                return 2;
            }
            finally
            {
                state.L = outer;
            }
        }

        // ---- usertypes ------------------------------------------------------------------------------------------

        // sol2 new_usertype: methods are looked up on the type's table; a name with no C# implementation logs once
        // and raises "binding not ported" when called. A name the C++ type does not register at all (CppUserTypes) is
        // nil, as sol's index_fail returns it (a script probing a field, geteffects' effect.deleted).
        public void RegisterType(string name, IReadOnlyDictionary<string, LuaFunction> methods,
                                 IReadOnlyDictionary<string, LuaFunction> metamethods = null)
        {
            NewTable();                     // metatable
            NewTable();                     // methods
            foreach (var (k, fn) in methods)
            {
                PushFunction(fn);
                SetField(-2, k);
            }
            int methodsIdx = Top;
            DoString(@"
                local methods, typeName, cppMethods = ...
                setmetatable(methods, { __index = function(_, k)
                    if cppMethods ~= nil and cppMethods[k] == nil then return nil end
                    __xi_missing(typeName, k)
                    return function() error('binding not ported: ' .. typeName .. ':' .. tostring(k), 2) end
                end })", "=xi_type", 0, () =>
            {
                lua_pushvalue(L, methodsIdx);
                PushString(name);
                if (CppUserTypes.Methods.TryGetValue(name, out var cpp))
                {
                    NewTable();
                    foreach (var k in cpp)
                    {
                        Push(true);
                        SetField(-2, k);
                    }
                }
                else PushNil();
                return 3;
            });
            SetField(-2, "__index");
            PushString(name);
            SetField(-2, "__name");
            if (metamethods != null)
            {
                foreach (var (k, fn) in metamethods)
                {
                    PushFunction(fn);
                    SetField(-2, k);
                }
            }
            if (typeRefs_.TryGetValue(name, out var old)) luaL_unref(L, LUA_REGISTRYINDEX, old);
            typeRefs_[name] = luaL_ref(L, LUA_REGISTRYINDEX);
        }

        // the C# object as a userdata of the registered type (the same userdata for the same object)
        public void PushObject(object o, string typeName = null)
        {
            if (o == null)
            {
                PushNil();
                return;
            }
            typeName ??= TypeNameOf(o);
            if (!objectIds_.TryGetValue(o, out int id))
            {
                id = objects_.Count;
                objects_.Add(o);
                objectIds_[o] = id;
            }

            lua_rawgeti(L, LUA_REGISTRYINDEX, cacheRef_);
            lua_rawgeti(L, -1, id + 1);
            if (!IsNil(-1))
            {
                lua_remove(L, -2);
                return;
            }
            Pop(1);
            var p = lua_newuserdata(L, sizeof(int));
            *(int*)p = id;
            if (!typeRefs_.TryGetValue(typeName, out int mt)) throw new InvalidOperationException($"usertype {typeName} not registered");
            lua_rawgeti(L, LUA_REGISTRYINDEX, mt);
            lua_setmetatable(L, -2);
            lua_pushvalue(L, -1);
            lua_rawseti(L, -3, id + 1);
            lua_remove(L, -2);
        }

        // lets go of an object that is gone on the C# side (a status effect that wore off): its id is not reused, and a
        // userdata Lua still holds for it reads as null
        public void Forget(object o)
        {
            if (o == null || !objectIds_.Remove(o, out int id)) return;
            objects_[id] = null;
            lua_rawgeti(L, LUA_REGISTRYINDEX, cacheRef_);
            lua_pushnil(L);
            lua_rawseti(L, -2, id + 1);
            Pop(1);
        }

        // which usertype a C# object is pushed as; set by the binding layer
        public Func<object, string> TypeNameOf = o => o.GetType().Name;

        public object ToObject(int idx)
        {
            if (Type(idx) != LUA_TUSERDATA) return null;
            var p = lua_touserdata(L, idx);
            int id = *(int*)p;
            return id >= 0 && id < objects_.Count ? objects_[id] : null;
        }

        // ---- running code ---------------------------------------------------------------------------------------

        // loads and runs a chunk; leaves nresults values (LUA_MULTRET: all) on the stack. pushArgs pushes the
        // chunk's arguments and returns how many.
        public int DoString(string code, string chunkName, int nresults = 0, Func<int> pushArgs = null)
        {
            var bytes = Encoding.UTF8.GetBytes(code);
            return Run(bytes, chunkName, nresults, pushArgs);
        }

        // luaL_loadfile + pcall, the chunk named "@<path>" like sol2's safe_script_file
        public int DoFile(string path, string chunkName = null, int nresults = 0) =>
            Run(File.ReadAllBytes(path), chunkName ?? "@" + path, nresults, null);

        private int Run(byte[] code, string chunkName, int nresults, Func<int> pushArgs)
        {
            int baseTop = Top;
            lua_rawgeti(L, LUA_REGISTRYINDEX, tracebackRef_);
            int handler = Top;
            int status;
            var name = Encoding.UTF8.GetBytes(chunkName + "\0");
            fixed (byte* c = code)
            fixed (byte* n = name)
                status = luaL_loadbuffer(L, c, (nuint)code.Length, n);
            if (status != 0)
            {
                string err = PopError();
                lua_remove(L, handler);
                throw new LuaException(err);
            }
            int nargs = 0;
            if (pushArgs != null)
            {
                // arguments are pushed relative to the caller's stack, so copy what they reference first
                nargs = pushArgs();
            }
            status = lua_pcall(L, nargs, nresults, handler);
            lua_remove(L, handler);
            if (status != 0) throw new LuaException(PopError());
            return Top - baseTop;
        }

        // calls the value below the n arguments on the top of the stack; leaves nresults values
        public void Call(int nargs, int nresults)
        {
            int fnIdx = Top - nargs;
            lua_rawgeti(L, LUA_REGISTRYINDEX, tracebackRef_);
            lua_insert(L, fnIdx);
            int status = lua_pcall(L, nargs, nresults, fnIdx);
            lua_remove(L, fnIdx);
            if (status != 0) throw new LuaException(PopError());
        }

        private string PopError()
        {
            string s = Describe(-1);
            Pop(1);
            return s;
        }

        public long MemoryKb => lua_gc(L, LUA_GCCOUNT, 0);
        public void CollectGarbage() => lua_gc(L, LUA_GCCOLLECT, 0);
        public void StepGarbage(int kb) => lua_gc(L, LUA_GCSTEP, kb);
    }

    public class LuaException : Exception
    {
        public LuaException(string message) : base(message) { }
    }

    public sealed class LuaArgumentException : LuaException
    {
        public LuaArgumentException(int idx, string expected, string got)
            : base($"bad argument #{idx} ({expected} expected, got {got})") { }
    }
}
